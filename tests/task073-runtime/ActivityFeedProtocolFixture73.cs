using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ActivityFeedProtocolFixture73
/// desc         : 자기 gate에서 실제 자식 PID와 요청 수명만 제어하는 Claude·Codex 합성 wire 프로세스다.
/// author       : CodexOrig 보조 개발 055
/// create date  : 2026-11-03
/// update date  :
/// </summary>
/// ---------------------------------------------------------------------
internal static class ActivityFeedProtocolFixture73
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    private static string gates;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 로그인·네트워크·사용자 소스 변경 없이 로컬 두 제공자의 요청 wire만 처리한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        Console.InputEncoding = Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Contains("--version")) { Console.WriteLine("fixture-activity73 9.9.9"); return 0; }
        try
        {
            gates = Path.GetFullPath(Environment.GetEnvironmentVariable("TASK073_FIXTURE_GATES") ?? "");
            if (!Directory.Exists(gates) || !gates.Contains(Path.Combine("task073", "runtime")))
                throw new InvalidOperationException("A precreated owned TASK073 gate directory is required.");
            if (args.Contains("--json-schema") || (!args.Contains("app-server") && !args.Contains("--input-format")))
                return Proposal(args, Console.In.ReadToEnd());
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                var packet = Obj(Json.DeserializeObject(line));
                string method = Value(packet, "method"), type = Value(packet, "type");
                object requestId = packet.ContainsKey("id") ? packet["id"] : null;
                if (method == "initialize") Reply(requestId, new { userAgent = "activity73-local-fixture" });
                else if (method == "account/read") Reply(requestId, new { account = new { type = "fixture", planType = "fixture" } });
                else if (method == "model/list") Reply(requestId, new { data = new[] { new { id = "fixture73", model = "fixture73", displayName = "Local fixture", supportedReasoningEfforts = new object[0] } }, nextCursor = (string)null });
                else if (method == "account/rateLimits/read") Reply(requestId, new { rateLimits = new { } });
                else if (method == "thread/start") Reply(requestId, new { thread = new { id = "activity73-thread" } });
                else if (method == "turn/start")
                {
                    var input = Obj(packet["params"]);
                    var blocks = input["input"] as IList;
                    string id = RequestId(Value(Obj(blocks[0]), "text"));
                    Reply(requestId, new { turn = new { id = id, status = "inProgress" } });
                    Event("item/started", new { threadId = "activity73-thread", item = new { id = id, type = "commandExecution" } });
                    Gate(id, "Codex", "agent");
                    bool failed = id.Contains("FAIL");
                    Event("item/completed", new { threadId = "activity73-thread", item = new { id = id, type = "agentMessage", phase = "final_answer", text = "PRIVATE_MESSAGE_73 " + id } });
                    Event("turn/completed", new { threadId = "activity73-thread", turn = new { status = failed ? "failed" : "completed", error = failed ? new { message = "PRIVATE_ERROR_PATH_73" } : null } });
                    Trace(id, "result-written", failed ? "failed" : "completed");
                }
                else if (type == "control_request") Write(new { type = "control_response", response = new { subtype = "success", request_id = packet["request_id"], response = new { } } });
                else if (type == "user")
                {
                    var message = Obj(packet["message"]);
                    var blocks = message["content"] as IList;
                    string id = RequestId(Value(Obj(blocks[0]), "text"));
                    Gate(id, "Claude", "agent");
                    bool failed = id.Contains("FAIL");
                    Write(new { type = "result", subtype = failed ? "error_during_execution" : "success", is_error = failed, result = "PRIVATE_MESSAGE_73 " + id,
                        usage = new { input_tokens = 13, output_tokens = 7, cache_read_input_tokens = 0, cache_creation_input_tokens = 0 } });
                    Trace(id, "result-written", failed ? "failed" : "completed");
                }
            }
            return 0;
        }
        catch (Exception error)
        {
            if (gates != null && Directory.Exists(gates)) File.WriteAllText(Path.Combine(gates, "fixture-fault-" + Process.GetCurrentProcess().Id + ".json"), Json.Serialize(new { Error = error.ToString(), FixtureFault = true }));
            return 91;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Proposal
    /// desc         : 실제 원본을 쓰지 않고 수정안 또는 명시적 실패를 gate 해제 뒤 반환한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Proposal(string[] args, string prompt)
    {
        string id = RequestId(prompt);
        bool claude = args.Contains("--json-schema");
        Gate(id, claude ? "Claude" : "Codex", "proposal");
        if (id.Contains("FAIL")) { Console.Error.WriteLine("PRIVATE_ERROR_PATH_73"); Trace(id, "result-written", "failed"); return 23; }
        object[] edits = id.Contains("EDIT") ? new object[] { new { old_text = "int ActivityFeedValue73 = 1;", new_text = "int ActivityFeedValue73 = 2;" } } : new object[0];
        var result = new { summary = "PRIVATE_MESSAGE_73 " + id, edits = edits };
        int output = Array.IndexOf(args, "--output-last-message");
        if (output >= 0) File.WriteAllText(args[output + 1], Json.Serialize(result), new UTF8Encoding(false));
        if (claude) Write(new { type = "result", subtype = "success", is_error = false, structured_output = result });
        else { Write(new { type = "turn.completed", usage = new { input_tokens = 13, output_tokens = 7, cached_input_tokens = 0 } }); if (output < 0) Write(result); }
        Trace(id, "result-written", "completed"); return 0;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Gate
    /// desc         : 완성된 시작 증거를 원자 공개한 뒤 한 요청의 명시적 release만 제한 시간 기다린다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Gate(string id, string provider, string mode)
    {
        string temporary = Path.Combine(gates, id + ".start." + Process.GetCurrentProcess().Id + ".tmp");
        File.WriteAllText(temporary, Json.Serialize(new { RequestId = id, Provider = provider, Mode = mode, Pid = Process.GetCurrentProcess().Id, StartedAt = DateTimeOffset.UtcNow.ToString("o") }), new UTF8Encoding(false));
        File.Move(temporary, Path.Combine(gates, id + ".start.json"));
        Trace(id, "started", mode);
        if (mode == "agent" && id.Contains("BURST"))
        {
            for (int index = 0; index < 1000; index++)
                if (provider == "Codex") Event("item/agentMessage/delta", new { threadId = "activity73-thread", itemId = id, delta = "PRIVATE_BURST_73" });
                else Write(new { type = "assistant", message = new { content = new[] { new { type = "text", text = "PRIVATE_BURST_73" } } } });
            Trace(id, "progress-frames-written", "1000");
        }
        var watch = Stopwatch.StartNew();
        while (!File.Exists(Path.Combine(gates, id + ".release")))
        { if (watch.ElapsedMilliseconds >= 45000) throw new TimeoutException("Owned fixture release was not provided within 45 seconds."); Thread.Sleep(10); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RequestId
    /// desc         : 현재 user_request에 들어 있는 정확히 한 합성 id만 받아 대기 요청의 원문 혼입을 감지한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string RequestId(string prompt)
    {
        int jsonStart = prompt.IndexOf('\n');
        Dictionary<string, object> body = null;
        try { body = Obj(Json.DeserializeObject(prompt.Substring(jsonStart + 1))); } catch (ArgumentException) { }
        string current = body != null && body.ContainsKey("user_request") ? Value(body, "user_request") : prompt;
        var matches = Regex.Matches(current, @"\bACT73_[A-Z0-9_]+\b").Cast<Match>().Select(match => match.Value).Distinct().ToArray();
        if (matches.Length != 1) throw new InvalidDataException("Exactly one current fixture request id is required.");
        return matches[0];
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Trace
    /// desc         : 프롬프트·명령·경로 없이 실제 fixture 수명 단계만 PID별로 기록한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Trace(string id, string stage, string outcome)
    { File.AppendAllText(Path.Combine(gates, id + ".trace.jsonl"), Json.Serialize(new { RequestId = id, Stage = stage, Outcome = outcome, Pid = Process.GetCurrentProcess().Id, At = DateTimeOffset.UtcNow.ToString("o") }) + "\n", new UTF8Encoding(false)); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Write
    /// desc         : wire 응답을 stdout 한 줄로 기록하고 flush한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Write(object value) { Console.WriteLine(Json.Serialize(value)); Console.Out.Flush(); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Reply
    /// desc         : 실제 요청 id의 JSON-RPC 응답 envelope를 만든다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Reply(object id, object result) { Write(new { id = id, result = result }); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Event
    /// desc         : Codex 실제 runner가 소비하는 공개 알림 envelope를 만든다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Event(string method, object value) { Write(new { method = method, @params = value }); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Obj
    /// desc         : 구조화된 wire 객체를 읽고 예상하지 못한 shape는 빈 객체로 구분한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static Dictionary<string, object> Obj(object value) { return value as Dictionary<string, object> ?? new Dictionary<string, object>(); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Value
    /// desc         : wire 문자열 값을 직접 읽고 원문을 상태 요약으로 사용하지 않는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Value(Dictionary<string, object> value, string key) { return value.ContainsKey(key) ? Convert.ToString(value[key]) : ""; }
}
