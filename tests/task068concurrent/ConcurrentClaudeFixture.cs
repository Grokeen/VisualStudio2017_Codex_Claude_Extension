using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ConcurrentClaudeFixture
/// desc         : 요청별 게이트·PID·원문을 보존하는 개인 Claude stream-json EXE다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK068 로그인/실추론/네트워크 없이 실제 프로세스 경계 검사.
/// </summary>
/// ---------------------------------------------------------------------
internal static class ConcurrentClaudeFixture
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : initialize에 응답하고 고유 user_request의 명시적 파일 게이트만 기다린다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 파일은 개인 gate 루트에만 쓰며 모델/명령 실행은 하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        try { return Execute(args); }
        catch (Exception error)
        {
            string directory = Environment.GetEnvironmentVariable("TASK068_CONCURRENT_GATES");
            File.WriteAllText(Path.Combine(directory, "fixture-error-" + Process.GetCurrentProcess().Id + ".txt"), error.GetType().FullName + "\n" + error.Message, new UTF8Encoding(false));
            return 9;
        }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Execute
    /// desc         : 합성 wire 입력과 오류를 개인 evidence에 보존하며 단일 요청 gate를 처리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 하네스 문제와 제품 문제를 구별할 원문을 남긴다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Execute(string[] args)
    {
        Console.InputEncoding = Console.OutputEncoding = new UTF8Encoding(false);
        if (Array.IndexOf(args, "--version") >= 0) { Console.WriteLine("2.1.302"); return 0; }
        var json = new JavaScriptSerializer(); string line;
        if (Array.IndexOf(args, "--json-schema") >= 0)
        {
            string raw = Console.In.ReadToEnd();
            var prompt = json.Deserialize<Dictionary<string, object>>(raw.Substring(raw.IndexOf('\n') + 1));
            string id = Convert.ToString(prompt["user_request"]);
            Gate(id, "proposal", raw, prompt, json);
            object[] edits = id.Contains("_EDIT") ? new object[] { new { old_text = "alpha", new_text = "beta" } } : new object[0];
            Console.WriteLine(json.Serialize(new { type = "result", subtype = "success", is_error = false, structured_output = new { summary = "final-" + id, edits }, usage = new { input_tokens = 13, output_tokens = 7, cache_read_input_tokens = 0, cache_creation_input_tokens = 0 } }));
            return 0;
        }
        while ((line = Console.ReadLine()) != null)
        {
            File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("TASK068_CONCURRENT_GATES"), "stdin-" + Process.GetCurrentProcess().Id + ".jsonl"), line + "\n", new UTF8Encoding(false));
            var root = json.Deserialize<Dictionary<string, object>>(line);
            if (Convert.ToString(root["type"]) == "control_request")
            { Console.WriteLine(json.Serialize(new { type = "control_response", response = new { subtype = "success", request_id = root["request_id"], response = new { } } })); Console.Out.Flush(); continue; }
            if (Convert.ToString(root["type"]) != "user") continue;
            var message = (Dictionary<string, object>)root["message"];
            var content = (System.Collections.IList)message["content"];
            string text = Convert.ToString(((Dictionary<string, object>)content[0])["text"]);
            var prompt = json.Deserialize<Dictionary<string, object>>(text.Substring(text.IndexOf('\n') + 1));
            string id = Convert.ToString(prompt["user_request"]);
            Console.WriteLine(json.Serialize(new { type = "assistant", message = new { content = new[] { new { type = "text", text = "progress-" + id } } } })); Console.Out.Flush();
            Gate(id, "full", line, prompt, json);
            bool fail = id.Contains("ERROR");
            Console.WriteLine(json.Serialize(new { type = "result", subtype = fail ? "error_during_execution" : "success", is_error = fail, result = "final-" + id, usage = new { input_tokens = id == "REQ_A_USAGE_ROLE" ? 31 : id == "REQ_B_USAGE_ROLE" ? 53 : 13, output_tokens = id == "REQ_A_USAGE_ROLE" ? 3 : id == "REQ_B_USAGE_ROLE" ? 5 : 7, cache_read_input_tokens = 0, cache_creation_input_tokens = 0 } })); Console.Out.Flush();
        }
        return 0;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Gate
    /// desc         : 모드에 관계없이 고유 요청과 실제 PID·원문을 보존한 후 개인 gate만 기다린다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 제안 모드의 격리 cwd를 원래 프로젝트와 혼동하지 않는다.
    ///                2026-10-20 CodexOrig : 완료한 start JSON만 원자 공개하여 하네스 read/write race 제거.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Gate(string id, string mode, string raw, object prompt, JavaScriptSerializer json)
    {
        if (!Regex.IsMatch(id, @"\AREQ_[A-Z0-9_]+\z")) throw new InvalidDataException("Unexpected fixture request identifier.");
        string gates = Environment.GetEnvironmentVariable("TASK068_CONCURRENT_GATES");
        string pending = Path.Combine(gates, id + ".start.tmp-" + Process.GetCurrentProcess().Id);
        File.WriteAllText(pending, json.Serialize(new { Id = id, Mode = mode, Pid = Process.GetCurrentProcess().Id, Directory = Environment.CurrentDirectory, RawRequest = raw, Prompt = prompt, StartedAt = DateTimeOffset.Now.ToString("o") }), new UTF8Encoding(false));
        File.Move(pending, Path.Combine(gates, id + ".start.json"));
        var watch = Stopwatch.StartNew();
        while (!File.Exists(Path.Combine(gates, id + ".release"))) { if (watch.ElapsedMilliseconds > 25000) throw new TimeoutException("Fixture gate timed out."); Thread.Sleep(5); }
    }
}
