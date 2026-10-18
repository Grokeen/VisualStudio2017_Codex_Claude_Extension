using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace VS2017.AIAssistant.Providers
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProviderModelDescriptor
    /// desc         : CLI가 광고한 모델·생각 강도·속도 기능의 불변 복사본이다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 모델별 지원 기능과 표시 이름을 별도로 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ProviderModelDescriptor
    {
        public string Provider { get; private set; }
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public ReadOnlyCollection<string> Efforts { get; private set; }
        public string DefaultEffort { get; private set; }
        public bool SupportsFast { get; private set; }
        public string SpeedDescription { get; private set; }
        public int Priority { get; private set; }
        public string UpgradeModel { get; private set; }
        public Version MinimumCliVersion { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProviderModelDescriptor
        /// desc         : 지원 강도를 값 복사하고 모델 식별자와 기본 강도를 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 호출자가 전달한 배열의 사후 변경 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ProviderModelDescriptor(string provider, string id, string displayName, IEnumerable<string> efforts,
            string defaultEffort = "auto", bool supportsFast = false, string speedDescription = "", int priority = 0,
            string upgradeModel = "", Version minimumCliVersion = null)
        {
            ProviderExecutionOptions.ValidateProvider(provider);
            if (String.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, @"\A[A-Za-z0-9][A-Za-z0-9._:/@+\[\]-]{0,119}\z"))
                throw new ArgumentException("모델 카탈로그의 식별자가 올바르지 않습니다.");
            var values = (efforts ?? new string[0]).ToArray();
            if (values.Length > 32 || values.Any(x => !ProviderExecutionOptions.IsEffortToken(x) || x == "auto"))
                throw new ArgumentException("모델 카탈로그의 생각 강도가 올바르지 않습니다.");
            values = values.Distinct(StringComparer.Ordinal).ToArray();
            if (defaultEffort != "auto" && !values.Contains(defaultEffort, StringComparer.Ordinal))
                throw new ArgumentException("기본 생각 강도가 모델의 지원 목록에 없습니다.");
            Provider = provider; Id = id; DisplayName = SafeLabel(displayName, id, 160);
            Efforts = Array.AsReadOnly(values); DefaultEffort = defaultEffort; SupportsFast = supportsFast;
            SpeedDescription = SafeLabel(speedDescription, "", 240); Priority = priority;
            UpgradeModel = SafeLabel(upgradeModel, "", 120); MinimumCliVersion = minimumCliVersion;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SafeLabel
        /// desc         : 카탈로그의 설명에서 제어 문자와 과도한 표시 길이를 배제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 사용자 표시 문자열을 메타데이터 범위로 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string SafeLabel(string value, string fallback, int maximum)
        { return String.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(Char.IsControl) ? fallback : value; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ToString
        /// desc         : 모델 선택기에서 내부 식별자와 별도의 표시 이름을 사용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : UI 모델 항목 문자열 제공.
        /// </summary>
        /// ---------------------------------------------------------------------
        public override string ToString() { return DisplayName; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProviderModelCatalog
    /// desc         : 자격 정보 없이 로컬 Codex 캐시와 공식 Claude 모델 정보를 제한 크기로 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 숨긴 모델·손상 JSON·과도한 캐시의 UI 유출 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ProviderModelCatalog
    {
        private static readonly object discoveryGate = new object();
        private static readonly Dictionary<string, ProviderModelCatalog> discovered = new Dictionary<string, ProviderModelCatalog>();
        public const int MaximumCacheBytes = 2 * 1024 * 1024;
        public ReadOnlyCollection<ProviderModelDescriptor> Models { get; private set; }
        public string SourcePath { get; private set; }
        public string Warning { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProviderModelCatalog
        /// desc         : 모델 목록을 불변 값으로 보관하고 우선순위대로 정렬한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 외부 목록의 사후 변경과 중복 식별자 배제.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ProviderModelCatalog(IEnumerable<ProviderModelDescriptor> models, string sourcePath = "", string warning = "")
        {
            var values = (models ?? new ProviderModelDescriptor[0]).ToArray();
            if (values.Length > 256 || values.Any(x => x == null)) throw new ArgumentException("모델 목록이 올바르지 않습니다.");
            if (values.GroupBy(x => x.Provider + "/" + x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1))
                throw new ArgumentException("모델 식별자가 중복되었습니다.");
            Models = Array.AsReadOnly(values.OrderBy(x => x.Priority).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray());
            SourcePath = sourcePath ?? ""; Warning = warning ?? "";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Find
        /// desc         : 대소문자를 임의로 바꾸지 않고 실제 모델 식별자로 찾는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 직접 입력한 알 수 없는 모델은 null로 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ProviderModelDescriptor Find(string model)
        { return Models.FirstOrDefault(x => String.Equals(x.Id, model, StringComparison.Ordinal)); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ForProvider
        /// desc         : 선택한 CLI의 카탈로그를 제공하되 인증 파일은 읽지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 기본 CLI와 직접 입력 대체 경로 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static ProviderModelCatalog ForProvider(string provider)
        {
            ProviderExecutionOptions.ValidateProvider(provider);
            lock (discoveryGate) { ProviderModelCatalog current; if (discovered.TryGetValue(provider, out current)) return current; }
            return provider == "Codex" ? LoadCodex() : ForClaude();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PublishDiscovered
        /// desc         : 연결된 CLI가 반환한 카탈로그만 게시하고 이전 연결의 자료는 명시적으로 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 연결 시 자동 모델·기능 새로고침.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void PublishDiscovered(string provider, ProviderModelCatalog catalog)
        {
            ProviderExecutionOptions.ValidateProvider(provider);
            if (catalog != null && catalog.Models.Any(m => m.Provider != provider)) throw new ArgumentException("다른 AI의 모델 목록입니다.");
            lock (discoveryGate) { if (catalog == null) discovered.Remove(provider); else discovered[provider] = catalog; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LoadCodex
        /// desc         : CODEX_HOME 또는 사용자 기본 폴더의 모델 캐시만 읽고 실패 시 기본 입력으로 복구한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 파일 증가·잘못된 UTF8·독점 잠금·손상 메타데이터를 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static ProviderModelCatalog LoadCodex(string cachePath = null)
        {
            string path = cachePath;
            try
            {
                if (path == null)
                {
                    string isolatedCache = Environment.GetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH");
                    if (!String.IsNullOrWhiteSpace(isolatedCache))
                    { if (!Path.IsPathRooted(isolatedCache)) throw new ArgumentException("모델 캐시 격리 경로는 절대 경로여야 합니다."); path = isolatedCache; }
                    else
                    {
                        string root = Environment.GetEnvironmentVariable("CODEX_HOME");
                        if (String.IsNullOrWhiteSpace(root)) root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
                        path = Path.Combine(root, "models_cache.json");
                    }
                }
                path = Path.GetFullPath(path);
                using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var bytes = new MemoryStream())
                {
                    if (input.Length > MaximumCacheBytes) throw new InvalidDataException("모델 캐시 크기가 제한을 초과했습니다.");
                    byte[] buffer = new byte[8192]; int count;
                    while ((count = input.Read(buffer, 0, Math.Min(buffer.Length, MaximumCacheBytes + 1 - (int)bytes.Length))) > 0)
                    { bytes.Write(buffer, 0, count); if (bytes.Length > MaximumCacheBytes) throw new InvalidDataException("모델 캐시 크기가 제한을 초과했습니다."); }
                    return FromCodexJson(new UTF8Encoding(false, true).GetString(bytes.ToArray()), path);
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException || error is DecoderFallbackException || error is System.Security.SecurityException)
            { return new ProviderModelCatalog(null, path, "모델 캐시를 읽지 못했습니다. CLI 기본 또는 직접 입력 모델을 사용하세요."); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FromCodexJson
        /// desc         : 엄격한 JSON 문법을 검사한 뒤 공개된 모델 기능 필드만 복사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : identity·지침·토큰·그 밖의 캐시 필드는 제품 모델에 보관하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static ProviderModelCatalog FromCodexJson(string json, string sourcePath = "")
        {
            if (json == null || json.Length > MaximumCacheBytes || new UTF8Encoding(false, true).GetByteCount(json) > MaximumCacheBytes)
                throw new InvalidDataException("모델 캐시 크기가 올바르지 않습니다.");
            if (json.Length > 0 && json[0] == '\uFEFF') json = json.Substring(1);
            StrictJson.Validate(json);
            var root = new JavaScriptSerializer { MaxJsonLength = MaximumCacheBytes, RecursionLimit = 40 }.DeserializeObject(json) as Dictionary<string, object>;
            object raw;
            if (root == null || !root.TryGetValue("models", out raw) || !(raw is object[]) || ((object[])raw).Length > 256)
                throw new InvalidDataException("모델 캐시 목록 형식이 올바르지 않습니다.");
            var models = new List<ProviderModelDescriptor>(); int skipped = 0;
            foreach (var item in (object[])raw)
            {
                var model = item as Dictionary<string, object>;
                if (model == null) { skipped++; continue; }
                string visibility = Text(model, "visibility");
                if (visibility.Length > 0 && visibility != "list") continue;
                if (IsUnavailable(model)) continue;
                try
                {
                    string id = Text(model, "slug");
                    if (models.Any(x => x.Id == id)) { skipped++; continue; }
                    var levels = new List<string>(); object values;
                    if (model.TryGetValue("supported_reasoning_levels", out values) && values is object[])
                    {
                        foreach (var level in (object[])values)
                        {
                            string effort = level is string ? (string)level : Text(level as Dictionary<string, object>, "effort");
                            if (!ProviderExecutionOptions.IsEffortToken(effort) || effort == "auto") throw new ArgumentException("잘못된 생각 강도입니다.");
                            levels.Add(effort);
                        }
                    }
                    bool fast = false; string speedDescription = "";
                    if (model.TryGetValue("additional_speed_tiers", out values) && values is object[]) fast = ((object[])values).OfType<string>().Contains("fast", StringComparer.Ordinal);
                    if (model.TryGetValue("service_tiers", out values) && values is object[])
                    {
                        foreach (var tier in (object[])values)
                        {
                            var tierObject = tier as Dictionary<string, object>; string tierId = tier is string ? (string)tier : Text(tierObject, "id");
                            if (tierId == "fast" || tierId == "priority") { fast = true; speedDescription = Text(tierObject, "description"); }
                        }
                    }
                    int priority = Int32.MaxValue;
                    if (model.TryGetValue("priority", out values) && values is int && (int)values >= 0) priority = (int)values;
                    string upgrade = "";
                    if (model.TryGetValue("upgrade", out values)) upgrade = values is string ? (string)values : Text(values as Dictionary<string, object>, "id");
                    string defaultEffort = Text(model, "default_reasoning_level");
                    if (defaultEffort.Length == 0) defaultEffort = "auto";
                    models.Add(new ProviderModelDescriptor("Codex", id, Text(model, "display_name"), levels, defaultEffort, fast, speedDescription, priority, upgrade));
                }
                catch (ArgumentException) { skipped++; }
            }
            return new ProviderModelCatalog(models, sourcePath, skipped == 0 ? "" : "일부 모델의 기능 메타데이터가 잘못되어 목록에서 제외했습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsUnavailable
        /// desc         : 명시적으로 사용할 수 없는 모델은 선택 목록에서 제외한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 캐시 버전별 문자열·불리언·객체 가용성 형식 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsUnavailable(Dictionary<string, object> model)
        {
            object value; if (!model.TryGetValue("availability", out value) || value == null) return false;
            if (value is bool) return !(bool)value;
            var details = value as Dictionary<string, object>;
            if (details != null)
            {
                object available;
                if ((details.TryGetValue("available", out available) || details.TryGetValue("is_available", out available)) && available is bool && !(bool)available) return true;
                value = Text(details, "status");
            }
            return value is string && new[] { "unavailable", "disabled", "denied", "not_available" }.Contains((string)value, StringComparer.OrdinalIgnoreCase);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Text
        /// desc         : 임의 객체를 문자열로 변환하지 않고 알려진 문자열 필드만 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 중첩 객체와 비문자 필드의 정보 노출 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Text(Dictionary<string, object> obj, string key)
        { object value; return obj != null && obj.TryGetValue(key, out value) && value is string ? (string)value : ""; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ForClaude
        /// desc         : 공식 문서의 명시 모델 기능과 보수적인 별칭 기능을 제공하며 계정 권한은 추정하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 최신 모델을 명시하면 실제 지원 강도·Fast 및 최소 CLI 버전을 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static ProviderModelCatalog ForClaude()
        {
            var models = new List<ProviderModelDescriptor>();
            foreach (string alias in new[] { "opus", "sonnet", "haiku" })
                models.Add(new ProviderModelDescriptor("Claude", alias, "Claude " + alias, new[] { "low", "medium", "high" }, "auto", alias == "opus", alias == "opus" ? "Opus Fast · 별도 사용량/계정 권한 필요" : "", models.Count));
            foreach (var row in new[] {
                new[] { "claude-opus-5-5", "Claude Opus 5.5", "280", "fast" },
                new[] { "claude-sonnet-5-5", "Claude Sonnet 5.5", "284", "" },
                new[] { "claude-haiku-5-5", "Claude Haiku 5.5", "293", "" },
                new[] { "claude-fable-5-1", "Claude Fable 5.1", "257", "" },
                new[] { "claude-opus-5", "Claude Opus 5", "219", "fast" },
                new[] { "claude-opus-4-8", "Claude Opus 4.8", "154", "fast" } })
                models.Add(new ProviderModelDescriptor("Claude", row[0], row[1], new[] { "low", "medium", "high", "xhigh", "max" },
                    row[0].EndsWith("5-5", StringComparison.Ordinal) ? "medium" : "high", row[3] == "fast", row[3] == "fast" ? "Opus Fast · 별도 사용량/계정 권한 필요" : "", models.Count, "", new Version(2, 1, Int32.Parse(row[2]))));
            return new ProviderModelCatalog(models, "https://code.claude.com/docs/en/model-config");
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProviderExecutionOptions
    /// desc         : 모델·생각 강도·속도를 검증하여 기존 승인 수정안 CLI에 실제 인수를 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : sandbox와 도구 권한을 바꾸지 않는 요청 옵션 계약.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ProviderExecutionOptions
    {
        public string Provider { get; private set; }
        public string Model { get; private set; }
        public string ReasoningEffort { get; private set; }
        public string Speed { get; private set; }
        public ProviderModelDescriptor ModelCapability { get; private set; }
        public Version MinimumCliVersion { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProviderExecutionOptions
        /// desc         : 광고되지 않은 고급 강도와 Fast를 막고 직접 입력 모델의 기본 경로를 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : enum을 고정하지 않고 모델별 광고된 강도 토큰을 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ProviderExecutionOptions(string provider, string model = "", string reasoningEffort = "auto", string speed = "standard", ProviderModelCatalog catalog = null)
        {
            ValidateProvider(provider); model = (model ?? "").Trim();
            if (model.Length > 120 || model.Any(Char.IsControl) || model.StartsWith("-", StringComparison.Ordinal)) throw new ArgumentException("모델 이름은 옵션 기호로 시작하지 않는 120자 이하 값이어야 합니다.");
            if (!IsEffortToken(reasoningEffort)) throw new ArgumentException("생각 강도 값이 올바르지 않습니다.");
            if (speed != "standard" && speed != "fast") throw new ArgumentException("속도는 standard 또는 fast를 선택하세요.");
            Provider = provider; Model = model; ReasoningEffort = reasoningEffort; Speed = speed;
            catalog = catalog ?? ProviderModelCatalog.ForProvider(provider); ModelCapability = catalog.Find(model);
            if (ModelCapability != null && ModelCapability.Provider != provider) throw new ArgumentException("모델 제공자가 선택한 AI와 다릅니다.");
            string[] allowed = ModelCapability == null ? new[] { "low", "medium", "high" } : ModelCapability.Efforts.ToArray();
            if (reasoningEffort != "auto" && !allowed.Contains(reasoningEffort, StringComparer.Ordinal))
                throw new ArgumentException("선택한 모델에서 지원이 확인되지 않은 생각 강도입니다. 모델을 선택하거나 자동 강도를 사용하세요.");
            if (speed == "fast" && (ModelCapability == null || !ModelCapability.SupportsFast))
                throw new ArgumentException("선택한 모델에서 Fast 지원이 확인되지 않았습니다. 지원 모델을 선택하거나 기본 속도를 사용하세요.");
            MinimumCliVersion = ModelCapability == null ? null : ModelCapability.MinimumCliVersion;
            if (provider == "Claude" && speed == "fast" && (MinimumCliVersion == null || MinimumCliVersion < new Version(2, 1, 205))) MinimumCliVersion = new Version(2, 1, 205);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BuildProposalArguments
        /// desc         : 별도의 셸 해석 없이 전달할 실제 모델·강도·속도 인수 벡터를 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Codex service_tier·Claude fastMode를 CLI 세션에만 적용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string[] BuildProposalArguments()
        {
            var values = new List<string>();
            if (Model.Length > 0) { values.Add("--model"); values.Add(Model); }
            if (ReasoningEffort != "auto") { values.Add(Provider == "Codex" ? "--config" : "--effort"); values.Add(Provider == "Codex" ? "model_reasoning_effort=\"" + ReasoningEffort + "\"" : ReasoningEffort); }
            if (Provider == "Codex" && Speed == "fast")
            { values.Add("--config"); values.Add("service_tier=\"fast\""); values.Add("--config"); values.Add("features.fast_mode=true"); }
            else if (Provider == "Claude")
            { values.Add("--settings"); values.Add(Speed == "fast" ? "{\"fastMode\":true}" : "{\"fastMode\":false}"); }
            return values.ToArray();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BuildProposalArguments
        /// desc         : 요청 컨텍스트의 문자열 값에서 검증된 인수 벡터를 바로 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 기존 Provider 통합용 편의 계약 제공.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string[] BuildProposalArguments(string provider, string model, string reasoningEffort, string speed)
        { return new ProviderExecutionOptions(provider, model, reasoningEffort, speed).BuildProposalArguments(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidateCliVersion
        /// desc         : 호출자가 확인한 CLI 버전이 명시 모델과 비대화형 Fast 최소 조건을 만족하는지 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 설치되지 않은 Claude를 지원 확인으로 오인하지 않도록 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void ValidateCliVersion(Version installedVersion)
        { if (MinimumCliVersion != null && (installedVersion == null || installedVersion < MinimumCliVersion)) throw new InvalidOperationException("선택한 모델/속도에는 " + Provider + " CLI " + MinimumCliVersion + " 이상이 필요합니다."); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidateProvider
        /// desc         : 지원하지 않는 제공자와 잘못된 대소문자를 명시적으로 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 임의 실행기 옵션으로의 잘못된 연결 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void ValidateProvider(string provider)
        { if (provider != "Codex" && provider != "Claude") throw new ArgumentException("지원하지 않는 AI 제공자입니다."); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsEffortToken
        /// desc         : 모델 광고 값에 사용할 수 있는 소문자 강도 토큰을 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 새 강도 광고를 허용하면서 TOML·인수 제어 문자 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static bool IsEffortToken(string value)
        { return value != null && Regex.IsMatch(value, @"\A[a-z][a-z0-9_-]{0,31}\z"); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : QuoteArgument
        /// desc         : Windows 네이티브 인수의 공백·따옴표·마지막 역슬래시를 올바르게 인코딩한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : ProcessStartInfo.Arguments에서 JSON 인수가 분리되지 않게 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string QuoteArgument(string argument)
        {
            if (argument == null || argument.Any(c => c == '\0' || c == '\r' || c == '\n')) throw new ArgumentException("명령 인수에 사용할 수 없는 문자가 있습니다.");
            var value = new StringBuilder("\""); int slashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\') { slashes++; continue; }
                if (character == '"') { value.Append('\\', slashes * 2 + 1).Append('"'); slashes = 0; continue; }
                value.Append('\\', slashes).Append(character); slashes = 0;
            }
            return value.Append('\\', slashes * 2).Append('"').ToString();
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : StrictJson
    /// desc         : JavaScriptSerializer의 완화 문법을 쓰기 전에 표준 JSON과 중복 키를 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 작은따옴표·주석·뒤 쉼표·과도한 중첩·중복 키 거부.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class StrictJson
    {
        private readonly string json; private int position;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StrictJson
        /// desc         : 검사 대상 문자열의 현재 읽기 위치를 초기화한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 제한된 캐시 문자열 전용 검사기 구성.
        /// </summary>
        /// ---------------------------------------------------------------------
        private StrictJson(string value) { json = value; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Validate
        /// desc         : 단일 JSON 값 이후에 추가 내용이 없는지 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 최상위 추가 코드와 후행 데이터를 거부.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void Validate(string value)
        { var reader = new StrictJson(value); reader.ReadValue(0); reader.Space(); if (reader.position != value.Length) reader.Fail(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Space
        /// desc         : JSON이 허용하는 네 가지 공백만 건너뛴다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 임의 유니코드 제어 공백 배제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Space() { while (position < json.Length && " \r\n\t".IndexOf(json[position]) >= 0) position++; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Take
        /// desc         : 다음 구분자가 기대한 문자일 때만 소비한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 배열과 객체의 닫힘 상태 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool Take(char character) { Space(); if (position < json.Length && json[position] == character) { position++; return true; } return false; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadValue
        /// desc         : 제한 깊이 안에서 객체·배열·문자열·리터럴·숫자를 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 파서 스택을 소모하는 과도한 중첩 거부.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ReadValue(int depth)
        {
            Space(); if (depth > 32 || position >= json.Length) Fail();
            char current = json[position];
            if (current == '{')
            {
                position++; var keys = new HashSet<string>(StringComparer.Ordinal); if (Take('}')) return;
                do { Space(); string key = ReadString(); if (!keys.Add(key) || !Take(':')) Fail(); ReadValue(depth + 1); if (Take('}')) return; } while (Take(','));
                Fail();
            }
            else if (current == '[')
            { position++; if (Take(']')) return; do { ReadValue(depth + 1); if (Take(']')) return; } while (Take(',')); Fail(); }
            else if (current == '"') ReadString();
            else if (current == 't' || current == 'f' || current == 'n')
            { string literal = current == 't' ? "true" : current == 'f' ? "false" : "null"; if (position + literal.Length > json.Length || String.CompareOrdinal(json, position, literal, 0, literal.Length) != 0) Fail(); position += literal.Length; }
            else ReadNumber();
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadString
        /// desc         : 큰따옴표 문자열의 제어 문자와 이스케이프를 검증하여 객체 키를 비교한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 유니코드 이스케이프로 숨긴 중복 키도 검출.
        /// </summary>
        /// ---------------------------------------------------------------------
        private string ReadString()
        {
            if (position >= json.Length || json[position] != '"') Fail(); int start = position++;
            while (position < json.Length)
            {
                char current = json[position++];
                if (current == '"') return new JavaScriptSerializer { MaxJsonLength = ProviderModelCatalog.MaximumCacheBytes }.Deserialize<string>(json.Substring(start, position - start));
                if (current < 32) Fail();
                if (current != '\\') continue;
                if (position >= json.Length) Fail(); current = json[position++];
                if (current == 'u') { for (int i = 0; i < 4; i++) { if (position >= json.Length || !Uri.IsHexDigit(json[position++])) Fail(); } }
                else if ("\"\\/bfnrt".IndexOf(current) < 0) Fail();
            }
            Fail(); return null;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadNumber
        /// desc         : JSON 숫자의 부호·소수점·지수와 필수 숫자를 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : NaN·Infinity·앞자리 0·불완전 숫자 배제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ReadNumber()
        {
            if (position < json.Length && json[position] == '-') position++;
            if (position >= json.Length) Fail();
            if (json[position] == '0') position++;
            else { if (json[position] < '1' || json[position] > '9') Fail(); Digits(); }
            if (position < json.Length && json[position] == '.') { position++; int before = position; Digits(); if (before == position) Fail(); }
            if (position < json.Length && (json[position] == 'e' || json[position] == 'E'))
            { position++; if (position < json.Length && (json[position] == '+' || json[position] == '-')) position++; int before = position; Digits(); if (before == position) Fail(); }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Digits
        /// desc         : ASCII 숫자만 연속으로 소비한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 숫자 표기에 문자가 섞이는 경우 거부.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Digits() { while (position < json.Length && json[position] >= '0' && json[position] <= '9') position++; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Fail
        /// desc         : 민감할 수 있는 캐시 원문을 오류 메시지에 넣지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 안정된 형식 오류만 호출자에게 전달.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Fail() { throw new InvalidDataException("모델 캐시가 표준 JSON 형식이 아닙니다."); }
    }
}
