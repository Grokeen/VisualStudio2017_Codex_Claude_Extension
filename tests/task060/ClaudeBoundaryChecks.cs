using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ClaudeBoundaryChecks
/// desc         : 합성 CLI를 통해 Claude 수정안·이미지·권한·오류 경계의 제품 호출을 검증한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK-060 운영 계정과 사용자 파일 없이 독립 자식 프로세스 검사.
/// </summary>
/// ---------------------------------------------------------------------
internal static class ClaudeBoundaryChecks
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 2000000 };
    private static readonly List<object> Checks = new List<object>();
    private static string Root, Fixture, Probe;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 검증 호출과 합성 CLI 호출을 분리하고 결과·실패를 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 모든 기록은 생성한 합성 내용만 포함.
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length != 1 || !Directory.Exists(args[0])) return RunFixture(args);
        Root = args[0]; Fixture = Assembly.GetExecutingAssembly().Location; Probe = Path.Combine(Root, "probe");
        Environment.SetEnvironmentVariable("TASK060_PROBE", Probe);
        try { RunAsync().GetAwaiter().GetResult(); return 0; }
        catch (Exception error) { Console.WriteLine("FAIL " + error); Checks.Add(new { Name = "unexpected", Passed = false, Category = error.GetType().Name }); return 1; }
        finally { File.WriteAllText(Path.Combine(Root, "result.json"), Json.Serialize(new { Checks, LiveInferencePerformed = false })); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunAsync
    /// desc         : 실제 제품을 합성 subprocess에 연결해 안전한 수정안과 전체 액세스를 구분한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 실패·취소·추론 옵션과 이미지 원문 전달 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static async Task RunAsync()
    {
        string sourcePath = Path.Combine(Root, "Source.cs"); File.WriteAllText(sourcePath, "return 1;");
        var source = new DocumentSnapshot(sourcePath, "return 1;", "synthetic", "CSharp", false, false);
        var attachment = ChatAttachment.FromImage(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII="), "synthetic.png");
        var context = new ChatRequestContext("SYNTHETIC_RULE", "sonnet", null, new[] { attachment, ChatAttachment.FromText("SYNTHETIC_REFERENCE", "reference.txt") }, "high", "standard", "SYNTHETIC_AGENT");
        SetMode("success"); var provider = new CliEditProvider(TimeSpan.FromSeconds(8));
        var proposal = await provider.GenerateWithContextAsync("Claude", Fixture, source, "SYNTHETIC_REQUEST", context, CancellationToken.None);
        Check("Proposal image result is parsed while original disk stays unchanged", proposal.ProposedText == "return 2;" && File.ReadAllText(sourcePath) == "return 1;");
        string[] argv = Json.Deserialize<string[]>(File.ReadAllText(Probe + ".args"));
        Check("Proposal disables built-in tools, MCP and user/project settings", After(argv, "--tools") == "" && After(argv, "--disallowedTools") == "mcp__*" && argv.Contains("--strict-mcp-config") && After(argv, "--setting-sources") == "" && argv.Contains("--no-session-persistence") && !argv.Contains("bypassPermissions"));
        Check("Proposal transmits explicit model, effort, speed and image input format", After(argv, "--model") == "sonnet" && After(argv, "--effort") == "high" && After(argv, "--input-format") == "stream-json" && After(argv, "--settings").Contains("fastMode"));
        string input = File.ReadAllText(Probe + ".input"); var message = Json.DeserializeObject(input.Trim()) as IDictionary<string, object>;
        var body = (IDictionary<string, object>)message["message"]; var blocks = (object[])body["content"];
        var image = (IDictionary<string, object>)blocks[1]; var imageSource = (IDictionary<string, object>)image["source"];
        Check("Proposal image uses the official user/content/base64 image envelope", Convert.ToString(message["type"]) == "user" && Convert.ToString(body["role"]) == "user" && message["parent_tool_use_id"] == null && Convert.ToString(image["type"]) == "image" && Convert.ToString(imageSource["type"]) == "base64" && Convert.ToString(imageSource["media_type"]) == "image/png" && Convert.ToString(imageSource["data"]) == attachment.ImageBase64);
        Check("Prompt, source, instructions and image bytes remain outside argv", input.Contains("SYNTHETIC_REQUEST") && input.Contains("SYNTHETIC_REFERENCE") && input.Contains("SYNTHETIC_AGENT") && !argv.Any(v => v.Contains("SYNTHETIC") || v.Contains(attachment.ImageBase64)));
        int observed = 0; provider.ProtocolMessage += (name, line) => observed++;
        proposal = await provider.GenerateWithContextAsync("Claude", Fixture, source, "SYNTHETIC_REQUEST", context, CancellationToken.None);
        argv = Json.Deserialize<string[]>(File.ReadAllText(Probe + ".args"));
        Check("Observed proposal uses verbose stream-json and keeps structured result", observed > 0 && After(argv, "--output-format") == "stream-json" && argv.Contains("--verbose") && proposal.ProposedText == "return 2;");
        foreach (string mode in new[] { "error-envelope", "missing-structured", "malformed", "nonzero" }) {
            SetMode(mode); bool rejected = false;
            try { await provider.GenerateAsync("Claude", Fixture, source, "SYNTHETIC_REQUEST", CancellationToken.None); }
            catch (InvalidOperationException error) { rejected = !error.Message.Contains("PRIVATE_FIXTURE_ERROR"); }
            Check("Proposal rejects " + mode + " without exposing raw fixture error", rejected && File.ReadAllText(sourcePath) == "return 1;");
        }
        SetMode("sleep"); using (var cancel = new CancellationTokenSource(350)) {
            bool canceled = false;
            try { await provider.GenerateAsync("Claude", Fixture, source, "SYNTHETIC_REQUEST", cancel.Token); } catch (OperationCanceledException) { canceled = true; }
            Check("Proposal cancellation is distinct from success", canceled);
        }
        SetMode("sleep"); bool timedOut = false;
        try { await new CliEditProvider(TimeSpan.FromMilliseconds(350)).GenerateAsync("Claude", Fixture, source, "SYNTHETIC_REQUEST", CancellationToken.None); } catch (TimeoutException) { timedOut = true; }
        Check("Proposal deadline is reported as timeout", timedOut);
        SetMode("runtime-success"); var runtime = new ClaudeRuntimeRunner { Timeout = TimeSpan.FromSeconds(8) };
        var result = await runtime.RunAgentAsync(Fixture, Root, "SYNTHETIC_REQUEST", context, null, null, CancellationToken.None);
        argv = Json.Deserialize<string[]>(File.ReadAllText(Probe + ".args")); input = File.ReadAllText(Probe + ".input");
        Check("Explicit full access uses bypass permission and stream input with image", result.Text == "synthetic-runtime-ok" && After(argv, "--permission-mode") == "bypassPermissions" && argv.Contains("--include-partial-messages") && input.Contains(attachment.ImageBase64) && input.Contains("SYNTHETIC_REFERENCE"));
        Check("Full access marks no persistent CLI session and passes model options", argv.Contains("--no-session-persistence") && After(argv, "--model") == "sonnet" && After(argv, "--effort") == "high");
        foreach (string mode in new[] { "runtime-permission", "runtime-error", "runtime-abrupt" }) {
            SetMode(mode); bool rejected = false;
            try { await runtime.RunAgentAsync(Fixture, Root, "SYNTHETIC_REQUEST", new ChatRequestContext(), null, null, CancellationToken.None); }
            catch (Exception error) { rejected = (error is IOException || error is InvalidOperationException) && !error.Message.Contains("PRIVATE_FIXTURE_ERROR"); }
            Check("Full access rejects " + mode + " without success or raw error", rejected);
        }
        var fast = new ChatRequestContext("", "opus", null, null, "high", "fast");
        foreach (string mode in new[] { "version-old", "version-malformed", "version-nonzero", "version-oversized" }) {
            SetMode(mode); bool rejected = false;
            try { await runtime.RunAgentAsync(Fixture, Root, "SYNTHETIC_REQUEST", fast, null, null, CancellationToken.None); }
            catch (InvalidOperationException error) { rejected = !error.Message.Contains("PRIVATE_FIXTURE_ERROR"); }
            string starts = File.ReadAllText(Probe + ".starts");
            Check("Full access preflight rejects " + mode + " before initialize or a user turn", rejected && starts.Contains("version") && !starts.Contains("agent"));
        }
        SetMode("version-model-old"); bool modelRejected = false;
        try { await runtime.RunAgentAsync(Fixture, Root, "SYNTHETIC_REQUEST", new ChatRequestContext("", "claude-haiku-5-5", null, null, "high", "standard"), null, null, CancellationToken.None); }
        catch (InvalidOperationException error) { modelRejected = error.Message.Contains("2.1.293"); }
        Check("Explicit model minimum is checked even at standard speed", modelRejected && !File.ReadAllText(Probe + ".starts").Contains("agent"));
        SetMode("version-current");
        result = await runtime.RunAgentAsync(Fixture, Root, "SYNTHETIC_REQUEST", fast, null, null, CancellationToken.None);
        Check("Supported Claude Fast checks version before starting exactly one agent", result.Text == "synthetic-runtime-ok" && File.ReadAllLines(Probe + ".starts").SequenceEqual(new[] { "version", "agent" }));
        SetMode("version-sleep");
        using (var cancel = new CancellationTokenSource(350)) {
            bool canceled = false;
            try { await runtime.RunAgentAsync(Fixture, Root, "SYNTHETIC_REQUEST", fast, null, null, cancel.Token); } catch (OperationCanceledException) { canceled = true; }
            Check("Cancellation during version preflight starts no agent", canceled && !File.ReadAllText(Probe + ".starts").Contains("agent"));
        }
        SetMode("version-sleep"); timedOut = false;
        try { await new ClaudeRuntimeRunner { Timeout = TimeSpan.FromMilliseconds(350) }.RunAgentAsync(Fixture, Root, "SYNTHETIC_REQUEST", fast, null, null, CancellationToken.None); } catch (TimeoutException) { timedOut = true; }
        Check("Runner deadline includes version preflight and starts no agent", timedOut && !File.ReadAllText(Probe + ".starts").Contains("agent"));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunFixture
    /// desc         : 운영 AI를 호출하지 않는 제어/수정안 서버로 합성 입력과 오류를 반환한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 실행 모드별 실패를 재현하고 비밀은 합성 표식만 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int RunFixture(string[] args)
    {
        string probe = Environment.GetEnvironmentVariable("TASK060_PROBE"), mode = Environment.GetEnvironmentVariable("TASK060_MODE");
        if (args.SequenceEqual(new[] { "--version" })) {
            File.AppendAllText(probe + ".starts", "version\n");
            if (mode == "version-sleep") { Thread.Sleep(30000); return 0; }
            if (mode == "version-nonzero") { Console.Error.WriteLine("PRIVATE_FIXTURE_ERROR"); return 9; }
            if (mode == "version-oversized") { Console.WriteLine(new string('x', 8192)); return 0; }
            Console.WriteLine(mode == "version-old" ? "2.1.204 (Claude Code)" : mode == "version-model-old" ? "2.1.292 (Claude Code)" : mode == "version-malformed" ? "PRIVATE_FIXTURE_ERROR" : "2.1.302 (Claude Code)"); return 0;
        }
        File.AppendAllText(probe + ".starts", "agent\n");
        File.WriteAllText(probe + ".args", Json.Serialize(args));
        if (mode == "sleep") { Thread.Sleep(30000); return 0; }
        if (mode.StartsWith("runtime-", StringComparison.Ordinal) || mode.StartsWith("version-", StringComparison.Ordinal)) {
            string line; while ((line = Console.ReadLine()) != null) {
                var msg = Json.DeserializeObject(line) as IDictionary<string, object>;
                if (Convert.ToString(msg["type"]) == "control_request") { Write(new { type = "control_response", response = new { subtype = "success", request_id = msg["request_id"], response = new { models = new object[0] } } }); continue; }
                File.WriteAllText(probe + ".input", line);
                if (mode == "runtime-abrupt") return 17;
                if (mode == "runtime-permission") { Write(new { type = "control_request", request_id = "permission", request = new { subtype = "can_use_tool", tool_name = "Bash", input = "PRIVATE_FIXTURE_ERROR" } }); continue; }
                Write(new { type = "result", subtype = mode == "runtime-error" ? "error_during_execution" : "success", is_error = mode == "runtime-error", result = mode == "runtime-error" ? "PRIVATE_FIXTURE_ERROR" : "synthetic-runtime-ok" });
            }
            return 0;
        }
        File.WriteAllText(probe + ".input", Console.In.ReadToEnd());
        if (mode == "nonzero") { Console.Error.WriteLine("PRIVATE_FIXTURE_ERROR"); return 7; }
        if (mode == "malformed") { Console.WriteLine("PRIVATE_FIXTURE_ERROR"); return 0; }
        if (mode == "missing-structured") { Write(new { type = "result", subtype = "success", is_error = false, result = "PRIVATE_FIXTURE_ERROR" }); return 0; }
        Write(new { type = "result", subtype = mode == "error-envelope" ? "error_during_execution" : "success", is_error = mode == "error-envelope", result = "PRIVATE_FIXTURE_ERROR", structured_output = new { summary = "synthetic", edits = new[] { new { old_text = "return 1;", new_text = "return 2;" } } } });
        return 0;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SetMode
    /// desc         : 다음 합성 자식 프로세스의 실행 모드를 지정한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 실패 시 후속 성공으로 덮어쓰지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void SetMode(string mode) { Environment.SetEnvironmentVariable("TASK060_MODE", mode); if (File.Exists(Probe + ".starts")) File.Delete(Probe + ".starts"); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : After
    /// desc         : 실제 자식이 받은 argv에서 옵션의 다음 값을 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 빈 값과 누락된 옵션을 구분.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string After(string[] argv, string name) { int index = Array.IndexOf(argv, name); return index >= 0 && index + 1 < argv.Length ? argv[index + 1] : null; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Write
    /// desc         : 합성 서버 응답을 JSON 한 줄로 내보내고 즉시 flush한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 모델/네트워크 호출 없이 합성 프로토콜 제공.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Write(object value) { Console.WriteLine(Json.Serialize(value)); Console.Out.Flush(); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 성공·실패 결과를 저장하며 첫 실패에서 검사를 중단한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK-060 합성 오류 원문은 제품 메시지 검증에서만 비교.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, bool passed) { Checks.Add(new { Name = name, Passed = passed }); Console.WriteLine((passed ? "PASS " : "FAIL ") + name); if (!passed) throw new InvalidOperationException(name); }
}
