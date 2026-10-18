using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VS2017.AIAssistant.Providers
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProviderRuntimeService
    /// desc         : 연결할 때 CLI 프로토콜로 계정·모델·한도를 조회하며 모델을 요청하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 동적 모델·계정·사용량 조회.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ProviderRuntimeService
    {
        private static readonly object gate = new object();
        private static readonly Dictionary<string, ProviderRuntimeInfo> snapshots = new Dictionary<string, ProviderRuntimeInfo>();

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Find
        /// desc         : 같은 실행 파일에서 조회한 정보만 현재 연결 정보로 재사용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 동적 모델·계정·사용량 조회.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static ProviderRuntimeInfo Find(string provider, string executable)
        {
            string path;
            try { path = CliEditProvider.ResolveExecutable(executable, provider == "Codex" ? "codex.exe" : "claude.exe"); }
            catch (Exception error) when (error is IOException || error is InvalidOperationException || error is ArgumentException || error is System.ComponentModel.Win32Exception) { return null; }
            lock (gate) { ProviderRuntimeInfo result; return snapshots.TryGetValue(provider + "|" + path, out result) ? result : null; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadAsync
        /// desc         : 제한 시간 안에 메타데이터만 조회하고 미지원 CLI·미연결을 별도 상태로 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 동적 모델·계정·사용량 조회.
        /// </summary>
        /// ---------------------------------------------------------------------
        public async Task<ProviderRuntimeInfo> ReadAsync(string provider, string executable, CancellationToken cancellation)
        {
            ProviderExecutionOptions.ValidateProvider(provider);
            string path = CliEditProvider.ResolveExecutable(executable, provider == "Codex" ? "codex.exe" : "claude.exe");
            var info = new ProviderRuntimeInfo { Provider = provider, ExecutablePath = path, Plan = "확인 불가", Usage = new ProviderUsageSnapshot() };
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation)) {
                deadline.CancelAfter(TimeSpan.FromSeconds(18));
                try {
                    if (provider == "Codex") await ReadCodexAsync(path, info, deadline.Token).ConfigureAwait(false);
                    else await ReadClaudeAsync(path, info, deadline.Token).ConfigureAwait(false);
                    info.Status = "연결된 CLI에서 모델 목록을 갱신했습니다. 모델 목록은 유료 사용 권한 보장이 아니며 실제 요청 시 서버가 확인합니다.";
                    if (provider == "Claude" && !info.Authenticated.HasValue) info.Status += " 모델 초기화만으로 로그인 여부를 확정하지 않습니다. 계정 연결 확인에서 별도로 검사하세요.";
                }
                catch (OperationCanceledException) {
                    cancellation.ThrowIfCancellationRequested();
                    info.Status = "CLI 기능 조회 시간이 초과되었습니다. 기본/직접 입력 모델을 사용할 수 있습니다.";
                }
                catch (Exception error) when (error is IOException || error is InvalidOperationException || error is ArgumentException || error is System.ComponentModel.Win32Exception || error is System.Security.SecurityException) {
                    info.Status = "CLI 기능 조회를 지원하지 않거나 연결하지 못했습니다. CLI 버전·로그인을 확인하세요.";
                }
            }
            cancellation.ThrowIfCancellationRequested();
            info.CheckedAt = DateTimeOffset.Now;
            lock (gate) snapshots[provider + "|" + path] = info;
            return info;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeCodexAsync
        /// desc         : 설치된 app-server의 초기화 계약을 사용하고 사용자 인증 파일은 직접 열지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 동적 모델·계정·사용량 조회.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static async Task InitializeCodexAsync(NativeJsonSession session, CancellationToken cancellation)
        {
            await session.CallAsync("initialize", new { clientInfo = new { name = "grokeen_vs_assistant", title = "Visual Studio AI Assistant", version = "0.9.0" } }, cancellation).ConfigureAwait(false);
            await session.SendAsync(new { method = "initialized", @params = new { } }, cancellation).ConfigureAwait(false);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadCodexAsync
        /// desc         : 페이지가 있는 모델 목록과 계정·한도 공개 메타데이터를 읽는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 동적 모델·계정·사용량 조회.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task ReadCodexAsync(string path, ProviderRuntimeInfo info, CancellationToken cancellation)
        {
            using (var session = new NativeJsonSession(path, new[] { "app-server" }, Path.GetTempPath())) {
                await InitializeCodexAsync(session, cancellation).ConfigureAwait(false);
                var account = RuntimeJson.Child(await session.CallAsync("account/read", new { refreshToken = false }, cancellation).ConfigureAwait(false), "account");
                info.Plan = RuntimeJson.Text(account, "planType");
                if (info.Plan.Length == 0) info.Plan = RuntimeJson.Text(account, "type");
                info.Authenticated = account.Count > 0;
                var models = new List<ProviderModelDescriptor>(); string cursor = null;
                var cached = ProviderModelCatalog.LoadCodex();
                for (int page = 0; page < 8; page++) {
                    var response = await session.CallAsync("model/list", new { limit = 100, includeHidden = false, cursor }, cancellation).ConfigureAwait(false);
                    foreach (var raw in RuntimeJson.Items(RuntimeJson.Get(response, "data"))) {
                        var item = RuntimeJson.Object(raw);
                        if (Equals(RuntimeJson.Get(item, "hidden"), true)) continue;
                        string id = RuntimeJson.Text(item, "model", 120); if (id.Length == 0) id = RuntimeJson.Text(item, "id", 120);
                        var efforts = RuntimeJson.Items(RuntimeJson.Get(item, "supportedReasoningEfforts")).Select(e => RuntimeJson.Text(RuntimeJson.Object(e), "reasoningEffort", 20)).Where(ProviderExecutionOptions.IsEffortToken).Where(e => e != "auto").Distinct().ToArray();
                        var old = cached.Find(id);
                        bool fast = RuntimeJson.Items(RuntimeJson.Get(item, "supportedServiceTiers")).OfType<string>().Any(t => t == "fast" || t == "priority") || (old != null && old.SupportsFast);
                        models.Add(new ProviderModelDescriptor("Codex", id, RuntimeJson.Text(item, "displayName", 160), efforts, "auto", fast, fast ? "CLI가 광고한 빠른 응답 옵션 · 실제 계정 지원은 요청 시 확인" : ""));
                    }
                    cursor = RuntimeJson.Text(response, "nextCursor"); if (cursor.Length == 0) break;
                }
                info.Catalog = new ProviderModelCatalog(models.GroupBy(m => m.Id).Select(g => g.First()), "Codex app-server model/list · " + DateTimeOffset.Now.ToString("g"));
                info.SupportsProtocol = true;
                try {
                    var quota = await session.CallAsync("account/rateLimits/read", new { }, cancellation).ConfigureAwait(false);
                    var primary = RuntimeJson.Child(RuntimeJson.Child(quota, "rateLimits"), "primary"); double percent;
                    if (Double.TryParse(Convert.ToString(RuntimeJson.Get(primary, "usedPercent"), System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out percent) && percent >= 0 && percent <= 100) info.Usage.QuotaUsedPercent = percent;
                    var reset = RuntimeJson.Count(primary, "resetsAt"); if (reset.HasValue && reset.Value <= 253402300799L) info.Usage.QuotaResetsAt = DateTimeOffset.FromUnixTimeSeconds(reset.Value);
                    info.Usage.Source = "Codex account/rateLimits/read";
                } catch (InvalidOperationException) { info.Usage.Source = "계정 한도 조회 미지원"; }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadClaudeAsync
        /// desc         : Claude SDK initialize 응답의 실제 모델 기능과 계정 종류를 사용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 동적 모델·계정·사용량 조회.
        ///                2026-10-18 CodexOrig : 실제 미로그인 CLI도 계정 항목을 반환하므로 초기화 정보로 인증을 확정하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task ReadClaudeAsync(string path, ProviderRuntimeInfo info, CancellationToken cancellation)
        {
            using (var session = new NativeJsonSession(path, new[] { "--print", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose" }, Path.GetTempPath())) {
                var response = await session.ControlAsync("initialize", cancellation).ConfigureAwait(false);
                var models = new List<ProviderModelDescriptor>();
                foreach (var raw in RuntimeJson.Items(RuntimeJson.Get(response, "models"))) {
                    var item = RuntimeJson.Object(raw);
                    string id = RuntimeJson.Text(item, "value", 120);
                    var efforts = RuntimeJson.Items(RuntimeJson.Get(item, "supportedEffortLevels")).OfType<string>().Where(e => e != "auto" && ProviderExecutionOptions.IsEffortToken(e));
                    models.Add(new ProviderModelDescriptor("Claude", id, RuntimeJson.Text(item, "displayName", 160), efforts, "auto", Equals(RuntimeJson.Get(item, "supportsFastMode"), true), RuntimeJson.Text(item, "description", 240)));
                }
                var account = RuntimeJson.Child(response, "account");
                info.Plan = RuntimeJson.Text(account, "subscriptionType"); if (info.Plan.Length == 0) info.Plan = RuntimeJson.Text(account, "apiProvider");
                if (info.Plan.Length == 0) info.Plan = "CLI가 요금제를 제공하지 않음";
                info.Authenticated = null;
                info.Catalog = new ProviderModelCatalog(models.GroupBy(m => m.Id).Select(g => g.First()), "Claude SDK initialize · " + DateTimeOffset.Now.ToString("g"));
                info.SupportsProtocol = true;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ParseUsage
        /// desc         : Codex thread/exec 및 Claude message/result가 실제 보고한 토큰만 해석한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 동적 모델·계정·사용량 조회.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static ProviderUsageSnapshot ParseUsage(string provider, string json)
        {
            if (String.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{", StringComparison.Ordinal)) return null;
            var message = RuntimeJson.Parse(json); IDictionary<string, object> usage;
            long? window = null; string source;
            if (provider == "Codex") {
                if (RuntimeJson.Text(message, "method") == "thread/tokenUsage/updated") {
                    var tokenUsage = RuntimeJson.Child(RuntimeJson.Child(message, "params"), "tokenUsage");
                    usage = RuntimeJson.Child(tokenUsage, "total"); window = RuntimeJson.Count(tokenUsage, "modelContextWindow");
                    source = "Codex thread/tokenUsage/updated";
                    return new ProviderUsageSnapshot { InputTokens = RuntimeJson.Count(usage, "inputTokens"), OutputTokens = RuntimeJson.Count(usage, "outputTokens"), CachedInputTokens = RuntimeJson.Count(usage, "cachedInputTokens"), TotalTokens = RuntimeJson.Count(usage, "totalTokens"), ContextWindow = window, Source = source };
                }
                usage = RuntimeJson.Child(message, "usage"); source = "Codex exec turn.completed";
            }
            else {
                usage = RuntimeJson.Child(message, "usage");
                if (usage.Count == 0) usage = RuntimeJson.Child(RuntimeJson.Child(message, "message"), "usage");
                if (usage.Count == 0) {
                    var streamEvent = RuntimeJson.Child(message, "event");
                    usage = RuntimeJson.Child(streamEvent, "usage");
                    if (usage.Count == 0) usage = RuntimeJson.Child(RuntimeJson.Child(streamEvent, "message"), "usage");
                }
                source = "Claude CLI usage";
            }
            if (usage.Count == 0) return null;
            var input = RuntimeJson.Count(usage, "input_tokens"); var output = RuntimeJson.Count(usage, "output_tokens");
            var cached = RuntimeJson.Count(usage, provider == "Codex" ? "cached_input_tokens" : "cache_read_input_tokens");
            if (provider == "Claude") input = SumKnown(input, cached, RuntimeJson.Count(usage, "cache_creation_input_tokens"));
            return new ProviderUsageSnapshot { InputTokens = input, OutputTokens = output, CachedInputTokens = cached, TotalTokens = SumKnown(input, output), Source = source };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SumKnown
        /// desc         : 모든 구성값이 보고된 경우에만 오버플로 없이 합계를 구한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 미제공 토큰의 추정 합산 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static long? SumKnown(params long?[] values)
        {
            if (values.Any(value => !value.HasValue)) return null;
            try { checked { long total = 0; foreach (long? value in values) total += value.Value; return total; } }
            catch (OverflowException) { return null; }
        }
    }
}

