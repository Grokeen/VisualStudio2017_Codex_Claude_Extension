using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Providers;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ClaudeNativeProbe
/// desc         : 실제 CLI의 버전·옵션·인증 상태·추론 없는 초기화만 허용 목록으로 기록한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK-060 자격 증명 원문과 초기화 응답을 로그에 쓰지 않음.
/// </summary>
/// ---------------------------------------------------------------------
internal static class ClaudeNativeProbe
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 2000000 };
    private static readonly List<object> Checks = new List<object>();
    private static string Executable, DirectoryPath;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 인증 확인 뒤 합성 빈 환경에서 제어 초기화만 수행하고 원문 없이 결과를 남긴다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 실제 모델 요청·로그인·계정 파일 직접 읽기 금지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        Executable = Path.GetFullPath(args[0]); DirectoryPath = Path.GetFullPath(args[1]);
        try {
            var version = Command("--version");
            Check("Native signed-package CLI reports 2.1.302", version.Item1 == 0 && version.Item2.Trim() == "2.1.302 (Claude Code)");
            var help = Command("--help");
            var required = new[] { "--input-format", "--output-format", "--json-schema", "--tools", "--disallowedTools", "--strict-mcp-config", "--setting-sources", "--permission-mode", "--no-session-persistence", "--include-partial-messages", "--effort", "--model" };
            Check("Native CLI exposes proposal, image stream, permission and model options", help.Item1 == 0 && required.All(help.Item2.Contains));
            var auth = Command("auth status --json");
            bool? loggedIn = null;
            try { var state = Json.DeserializeObject(auth.Item2) as IDictionary<string, object>; object value;
                if (state != null && state.TryGetValue("loggedIn", out value) && value is bool) loggedIn = (bool)value;
            } catch (ArgumentException) { }
            File.WriteAllText(Path.Combine(DirectoryPath, "auth-status-sanitized.json"), Json.Serialize(new { LoggedIn = loggedIn, ExitCode = auth.Item1, Category = loggedIn == true ? "authenticated" : loggedIn == false ? "authentication-required" : "status-unavailable", RawOutputSaved = false, LiveInferencePerformed = false }));
            Check("Auth status yields a boolean without persisting account output", loggedIn.HasValue && (auth.Item1 == 0 || (auth.Item1 == 1 && loggedIn == false)));
            bool productAuthenticated = new CliAccountService().CheckAsync("Claude", Executable, CancellationToken.None).GetAwaiter().GetResult();
            Check("Product account status agrees with native status without exposing account output", loggedIn.HasValue && productAuthenticated == loggedIn.Value);
            Initialize();
            return 0;
        } catch (Exception error) {
            Checks.Add(new { Name = "unexpected", Passed = false, Category = error.GetType().Name });
            Console.WriteLine("FAIL native probe: " + error.GetType().Name); return 1;
        } finally { File.WriteAllText(Path.Combine(DirectoryPath, "result.json"), Json.Serialize(new { Checks, LiveInferencePerformed = false, UserPromptSent = false, RawAuthOutputSaved = false })); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Command
    /// desc         : 숨김 CLI의 표준 출력을 메모리에만 수집하며 제한 시간 후 자기 Job을 종료한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 stderr·인증 문자열은 반환하거나 저장하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static Tuple<int, string> Command(string arguments)
    {
        using (var process = new Process()) using (var job = Job()) {
            process.StartInfo = new ProcessStartInfo(Executable, arguments) { WorkingDirectory = DirectoryPath, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            process.Start(); Attach(job, process);
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(25000)) throw new TimeoutException();
            Task.WaitAll(stdout, stderr); return Tuple.Create(process.ExitCode, stdout.Result);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Initialize
    /// desc         : 새 config와 빈 MCP·도구 설정에서 initialize만 보내고 응답 개수·상태만 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 환경의 자격 증명 변수는 자식에서 제거하고 사용자 메시지를 보내지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Initialize()
    {
        string config = Path.Combine(DirectoryPath, "fresh-config"); System.IO.Directory.CreateDirectory(config);
        string mcp = Path.Combine(DirectoryPath, "empty-mcp.json"); File.WriteAllText(mcp, "{\"mcpServers\":{}}");
        using (var process = new Process()) using (var job = Job()) {
            process.StartInfo = new ProcessStartInfo(Executable, "--bare --print --input-format stream-json --output-format stream-json --verbose --tools \"\" --disallowedTools \"mcp__*\" --strict-mcp-config --mcp-config \"" + mcp + "\" --setting-sources \"\" --no-session-persistence") {
                WorkingDirectory = DirectoryPath, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = new UTF8Encoding(false)
            };
            foreach (string name in process.StartInfo.EnvironmentVariables.Keys.Cast<string>().ToArray())
                if (name.StartsWith("ANTHROPIC_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("CLAUDE_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("AWS_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("GOOGLE_", StringComparison.OrdinalIgnoreCase)) process.StartInfo.EnvironmentVariables.Remove(name);
            process.StartInfo.EnvironmentVariables["CLAUDE_CONFIG_DIR"] = config;
            process.StartInfo.EnvironmentVariables["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1";
            process.StartInfo.EnvironmentVariables["DISABLE_AUTOUPDATER"] = "1";
            process.Start(); Attach(job, process); var stderr = process.StandardError.ReadToEndAsync();
            process.StandardInput.WriteLine("{\"type\":\"control_request\",\"request_id\":\"task060-init\",\"request\":{\"subtype\":\"initialize\"}}"); process.StandardInput.Flush();
            var deadline = Stopwatch.StartNew(); bool accepted = false; int models = 0;
            while (deadline.ElapsedMilliseconds < 22000) {
                var lineTask = process.StandardOutput.ReadLineAsync();
                if (!lineTask.Wait(Math.Max(1, 22000 - (int)deadline.ElapsedMilliseconds))) break;
                string line = lineTask.Result; if (line == null) break;
                var message = Json.DeserializeObject(line) as IDictionary<string, object>; object value;
                if (message == null || !message.TryGetValue("type", out value) || Convert.ToString(value) != "control_response") continue;
                var response = message["response"] as IDictionary<string, object>;
                if (response == null || Convert.ToString(response["request_id"]) != "task060-init") continue;
                accepted = Convert.ToString(response["subtype"]) == "success";
                var payload = response.ContainsKey("response") ? response["response"] as IDictionary<string, object> : null;
                if (payload != null && payload.TryGetValue("models", out value) && value is object[]) models = ((object[])value).Length;
                break;
            }
            Check("Native no-auth initialization accepts product control envelope without a user turn", accepted);
            File.WriteAllText(Path.Combine(DirectoryPath, "initialize-sanitized.json"), Json.Serialize(new { Accepted = accepted, ModelCount = models, FreshConfig = true, ToolsDisabled = true, InheritedCredentialVariablesRemoved = true, UserPromptSent = false, RawResponseSaved = false }));
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Job
    /// desc         : 제품과 동일한 프로세스 그룹 수명 관리 객체를 생성한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 다른 CLI·IDE 프로세스는 종료하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IDisposable Job() { return (IDisposable)Activator.CreateInstance(typeof(CliEditProvider).Assembly.GetType("VS2017.AIAssistant.Providers.ProcessJob", true), true); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Attach
    /// desc         : 검증에서 시작한 자식 프로세스만 소유한 Job에 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 외부 사용자 프로세스는 연결하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Attach(IDisposable job, Process process) { job.GetType().GetMethod("Attach", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(job, new object[] { process }); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 허용한 합격 상태와 공개 검사 이름만 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 실패 원문과 계정 정보 출력 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, bool passed) { Checks.Add(new { Name = name, Passed = passed }); Console.WriteLine((passed ? "PASS " : "FAIL ") + name); if (!passed) throw new InvalidOperationException(); }
}
