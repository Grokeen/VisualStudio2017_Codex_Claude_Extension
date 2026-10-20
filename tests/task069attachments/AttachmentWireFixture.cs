using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : AttachmentWireFixture
/// desc         : 계정·네트워크 없이 실제 제품 자식 프로세스의 argv와 JSON 요청을 관측한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK069 모델·강도·속도·권한 전달을 native wire로 검증.
/// </summary>
/// ---------------------------------------------------------------------
internal static class AttachmentWireFixture
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 32000000 };
    private static string log;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 자기 환경 변수의 개인 디렉터리에만 실행 인자와 요청을 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실제 Codex/Claude 실행·인증·HTTP를 호출하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        Console.InputEncoding = Console.OutputEncoding = new UTF8Encoding(false);
        string directory = Environment.GetEnvironmentVariable("TASK069_ATTACHMENTS_WIRE");
        if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return 9;
        log = Path.Combine(directory, Process.GetCurrentProcess().Id + ".wire.jsonl");
        Record(new { Kind = "argv", Arguments = args, WorkingDirectory = Environment.CurrentDirectory, Pid = Process.GetCurrentProcess().Id });
        if (args.Contains("--version")) { Console.WriteLine("2.1.999 (Claude Code fixture)"); return 0; }
        if (args.Contains("exec") || args.Contains("--json-schema"))
        {
            string input = Console.In.ReadToEnd(); Record(new { Kind = "stdin", Text = input });
            var proposal = new { summary = "isolated attachment fixture", edits = new object[0] };
            int output = Array.IndexOf(args, "--output-last-message");
            if (output >= 0) File.WriteAllText(args[output + 1], Json.Serialize(proposal), new UTF8Encoding(false));
            if (args.Contains("--json-schema")) Write(new { type = "result", subtype = "success", is_error = false, structured_output = proposal });
            else Write(new { type = "turn.completed", usage = new { input_tokens = 1, output_tokens = 1, cached_input_tokens = 0 } });
            return 0;
        }
        string line;
        while ((line = Console.ReadLine()) != null)
        {
            var message = Json.Deserialize<Dictionary<string, object>>(line); Record(new { Kind = "wire", Message = message });
            string method = Text(message, "method"); object id = message.ContainsKey("id") ? message["id"] : null;
            if (method == "initialize") Reply(id, new { userAgent = "TASK069-local-fixture" });
            if (method == "thread/start") Reply(id, new { thread = new { id = "attachment-thread" } });
            if (method == "turn/start")
            {
                Reply(id, new { turn = new { id = "attachment-turn", status = "inProgress" } });
                Write(new { method = "item/completed", @params = new { threadId = "attachment-thread", item = new { type = "agentMessage", phase = "final_answer", text = "isolated attachments completed" } } });
                Write(new { method = "turn/completed", @params = new { threadId = "attachment-thread", turn = new { status = "completed" } } });
            }
            if (Text(message, "type") == "control_request")
                Write(new { type = "control_response", response = new { subtype = "success", request_id = Text(message, "request_id"), response = new { } } });
            if (Text(message, "type") == "user") Write(new { type = "result", subtype = "success", is_error = false, result = "isolated attachments completed" });
        }
        return 0;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Text
    /// desc         : fixture JSON에서 존재하는 문자열 필드만 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 암묵적 계정/파일 조회 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Text(Dictionary<string, object> value, string name) { return value.ContainsKey(name) ? Convert.ToString(value[name]) : ""; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Record
    /// desc         : 현재 fixture가 실제 받은 요청만 줄 단위로 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 읽은 사용자의 인증 데이터는 존재하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Record(object value) { File.AppendAllText(log, Json.Serialize(value) + "\n", new UTF8Encoding(false)); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Write
    /// desc         : flush한 합성 JSON 응답만 stdout으로 전달한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 원격 추론 응답으로 주장하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Write(object value) { Console.WriteLine(Json.Serialize(value)); Console.Out.Flush(); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Reply
    /// desc         : 받은 JSON-RPC 식별자에 대응하는 로컬 응답을 만든다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 앱서버 필수 handshake만 구현한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Reply(object id, object result) { Write(new { id, result }); }
}
