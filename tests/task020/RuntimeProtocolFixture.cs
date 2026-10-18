using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : RuntimeProtocolFixture
/// desc         : 실제 자식 프로세스와 JSON 응답으로 네이티브 CLI의 경계 조건을 재현한다.
/// author       : CodexOrig
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
/// </summary>
/// ---------------------------------------------------------------------
internal static class RuntimeProtocolFixture
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 16000000 };
    private static readonly string Mode = Environment.GetEnvironmentVariable("TASK020_FIXTURE_MODE") ?? "success";
    private static readonly string Trace = Environment.GetEnvironmentVariable("TASK020_FIXTURE_TRACE");

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Write
    /// desc         : 합성 프로토콜 메시지를 stdout에 한 줄씩 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Write(object value) { Console.WriteLine(Json.Serialize(value)); Console.Out.Flush(); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Reply
    /// desc         : 요청 ID에 대응하는 실제 JSON 응답 envelope를 만든다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Reply(object id, object value) { Write(new { id, result = value }); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Event
    /// desc         : 합성 작업의 실제 시작·완료 알림을 보낸다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Event(string method, object value) { Write(new { method, @params = value }); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Obj
    /// desc         : 합성 JSON 객체를 읽고 잘못된 형식을 빈 객체로 구분한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static Dictionary<string, object> Obj(object value) { return value as Dictionary<string, object> ?? new Dictionary<string, object>(); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Value
    /// desc         : 합성 JSON 필드를 문자열로 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Value(Dictionary<string, object> value, string key) { return value.ContainsKey(key) ? Convert.ToString(value[key]) : ""; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 격리 검증을 실행하고 실패를 종료 코드와 결과 파일에 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Main(string[] args)
    {
        Console.InputEncoding = Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Contains("--version")) { Console.WriteLine("fixture 9.9.9"); return; }
        if (args.Contains("--json-schema")) { Proposal(args, Console.In.ReadToEnd(), true); return; }
        if (!args.Contains("app-server") && !args.Contains("--input-format")) { Proposal(args, Console.In.ReadToEnd(), false); return; }
        string line;
        while ((line = Console.ReadLine()) != null) {
            var message = Obj(Json.DeserializeObject(line));
            string method = Value(message, "method");
            object id = message.ContainsKey("id") ? message["id"] : null;
            if (Trace != null) File.AppendAllText(Trace, (method.Length > 0 ? method : Value(message, "type")) + "\n");
            if (Mode == "stall-initialize" && (method == "initialize" || Value(message, "type") == "control_request")) { Child(); Thread.Sleep(60000); continue; }
            if (method == "initialize") { Reply(id, new { userAgent = "fixture" }); }
            if (method == "account/read") { if (Mode == "collision") Write(new { id, method = "server/request", @params = new { } }); Reply(id, new { account = new { type = "chatgpt", planType = "fixture-plan" } }); }
            if (method == "model/list") {
                var parameters = Obj(message["params"]);
                bool second = Value(parameters, "cursor") == "second";
                Reply(id, new { data = new[] { new { id = second ? "future-two" : "future-one", model = second ? "future-two" : "future-one", displayName = "Discovered future model", hidden = false, supportedReasoningEfforts = new[] { new { reasoningEffort = "high" }, new { reasoningEffort = "xhigh" } }, supportedServiceTiers = new[] { "fast" } } }, nextCursor = second ? null : "second" });
            }
            if (method == "account/rateLimits/read") Reply(id, new { rateLimits = new { primary = new { usedPercent = 42.5, resetsAt = 1800000000L } } });
            if (method == "thread/start") {
                var parameters = Obj(message["params"]);
                if (Value(parameters, "sandbox") != "danger-full-access" || Value(parameters, "approvalPolicy") != "never") throw new Exception("Incorrect full-access contract");
                Reply(id, new { thread = new { id = "fixture-thread" } });
            }
            if (method == "turn/start") {
                var payload = Obj(message["params"]);
                var blocks = payload.ContainsKey("input") ? payload["input"] as object[] : null;
                if (Trace != null && blocks != null && blocks.Length > 0) File.WriteAllText(Trace + ".prompt", Value(Obj(blocks[0]), "text"));
                Reply(id, new { turn = new { id = "fixture-turn", status = "inProgress" } });
                if (Mode == "server-request") { Write(new { id = "1", method = "item/commandExecution/requestApproval", @params = new { threadId = "fixture-thread" } }); continue; }
                if (Mode == "failed") { Event("turn/completed", new { threadId = "fixture-thread", turn = new { status = "failed", error = new { message = "SECRET_SHOULD_NOT_PERSIST" } } }); continue; }
                Event("item/started", new { threadId = "fixture-thread", item = new { type = "commandExecution", id = "cmd1" } });
                ExecuteChanges();
                Event("thread/tokenUsage/updated", new { threadId = "fixture-thread", tokenUsage = new { total = new { inputTokens = 13, outputTokens = 7, cachedInputTokens = 3, totalTokens = 20 }, modelContextWindow = 100000 } });
                Event("item/completed", new { threadId = "fixture-thread", item = new { type = "commandExecution", id = "cmd1", status = "completed" } });
                if (Mode != "commands-only") {
                foreach (string part in new[] { "@@ -1 +1 @@\n-old\n+intermediate\n", "@@ -1 +1 @@\n-intermediate\n+new\n" })
                    Event("item/completed", new { threadId = "fixture-thread", item = new { type = "fileChange", id = "file", status = "completed", changes = new[] { new { path = "NativeFixture.txt", kind = new { type = "update" }, diff = part } } } });
                Event("turn/diff/updated", new { threadId = "fixture-thread", diff = "diff --git a/NativeFixture.txt b/NativeFixture.txt\n--- a/NativeFixture.txt\n+++ b/NativeFixture.txt\n@@ -1 +1 @@\n-old\n+new\ndiff --git a/Second.txt b/Second.txt\n--- /dev/null\n+++ b/Second.txt\n@@ -0,0 +1 @@\n+++actualCode\n" });
                }
                Event("thread/tokenUsage/updated", new { threadId = "fixture-thread", tokenUsage = new { total = new { inputTokens = 15, outputTokens = 9, cachedInputTokens = 3, totalTokens = 24 }, modelContextWindow = 100000 } });
                Event("item/completed", new { threadId = "fixture-thread", item = new { type = "agentMessage", phase = "commentary", text = "실제 fixture 테스트 실행을 완료했습니다." } });
                Event("item/completed", new { threadId = "fixture-thread", item = new { type = "agentMessage", phase = "final_answer", text = "Fixture actual file edit and command assertion passed." } });
                Event("turn/completed", new { threadId = "fixture-thread", turn = new { status = "completed" } });
            }
            if (Value(message, "type") == "control_request") {
                Write(new { type = "control_response", response = new { subtype = "success", request_id = Value(message, "request_id"), response = new { models = new[] { new { value = "claude-future", displayName = "Future Claude", supportedEffortLevels = new[] { "high", "max" }, supportsFastMode = true } }, account = new { subscriptionType = "fixture-account" } } } });
            }
            if (Value(message, "type") == "user") {
                var payload = Obj(message["message"]);
                var blocks = payload.ContainsKey("content") ? payload["content"] as object[] : null;
                if (Trace != null && blocks != null && blocks.Length > 0) File.WriteAllText(Trace + ".prompt", Value(Obj(blocks[0]), "text"));
                if (Mode == "failed") { Write(new { type = "result", subtype = "error_during_execution", is_error = true, result = "SECRET_SHOULD_NOT_PERSIST" }); continue; }
                if (Mode != "commands-only") for (int edit = 0; edit < 2; edit++) {
                    Write(new { type = "assistant", message = new { content = new[] { new { type = "tool_use", id = "edit" + edit, name = "Edit", input = new { file_path = "NativeFixture.txt", old_string = edit == 0 ? "old" : "intermediate", new_string = edit == 0 ? "intermediate" : "new" } } } } });
                    File.WriteAllText("NativeFixture.txt", edit == 0 ? "intermediate" : "new");
                    Write(new { type = "user", message = new { content = new[] { new { type = "tool_result", tool_use_id = "edit" + edit, is_error = false, content = "done" } } } });
                }
                Write(new { type = "stream_event", @event = new { type = "message_delta", usage = new { output_tokens = 4 } } });
                ExecuteChanges();
                Write(new { type = "result", subtype = "success", is_error = false, result = "Claude fixture actual command assertion passed.", usage = new { input_tokens = 10, output_tokens = 6, cache_read_input_tokens = 2, cache_creation_input_tokens = 3 } });
            }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Proposal
    /// desc         : 원본을 변경하지 않는 수정안과 실제 usage fixture를 출력한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Proposal(string[] args, string prompt, bool claude)
    {
        var result = new { summary = "readonly fixture proposal", edits = new[] { new { old_text = "oldValue", new_text = "newValue" } } };
        if (Trace != null) File.WriteAllText(Trace, prompt);
        int output = Array.IndexOf(args, "--output-last-message");
        if (output >= 0) File.WriteAllText(args[output + 1], Json.Serialize(result));
        if (claude) Write(new { type = "result", subtype = "success", is_error = false, structured_output = result, usage = new { input_tokens = 10, output_tokens = 2, cache_read_input_tokens = 0, cache_creation_input_tokens = 0 } });
        else { Write(new { type = "turn.completed", usage = new { input_tokens = 12, output_tokens = 3, cached_input_tokens = 2 } }); if (output < 0) Write(result); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ExecuteChanges
    /// desc         : 격리 파일을 수정하고 실제 하위 PowerShell 명령으로 결과를 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ExecuteChanges()
    {
        File.WriteAllText("NativeFixture.txt", "new"); File.WriteAllText("Second.txt", "++actualCode\n");
        var start = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"if([IO.File]::ReadAllText('NativeFixture.txt') -ne 'new'){exit 7}; [IO.File]::WriteAllText('actual-command-passed.txt','PASS')\"") { UseShellExecute = false, CreateNoWindow = true };
        using (var process = Process.Start(start)) { process.WaitForExit(); if (process.ExitCode != 0) throw new Exception("Actual assertion command failed"); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Child
    /// desc         : Job 취소 검사를 위한 별도 하위 프로세스를 실행한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Child()
    {
        var start = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command Start-Sleep -Seconds 60") { UseShellExecute = false, CreateNoWindow = true };
        var child = Process.Start(start); if (Trace != null) File.WriteAllText(Trace + ".pid", child.Id.ToString());
    }
}
