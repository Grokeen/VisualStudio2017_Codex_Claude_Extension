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
/// name         : MessageCliFixture
/// desc         : 실제 Codex/Claude 읽기 전용 CLI 인수·stdin을 보존하는 합성 응답 EXE다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK069 로그인·네트워크·모델 추론 없음.
/// </summary>
/// ---------------------------------------------------------------------
internal static class MessageCliFixture
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 실제 받은 메시지만 식별하고 격리 gate 또는 합성 응답을 반환한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : payload·pid·cwd·인수 증거를 원자 공개한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        Console.InputEncoding = Console.OutputEncoding = new UTF8Encoding(false);
        if (Array.IndexOf(args, "--version") >= 0) { Console.WriteLine("2.1.302"); return 0; }
        try
        {
            var json = new JavaScriptSerializer(); string raw = Console.In.ReadToEnd();
            var prompt = json.Deserialize<Dictionary<string, object>>(raw.Substring(raw.IndexOf('\n') + 1));
            string id = Regex.Match(Convert.ToString(prompt["agent_context"]), @"MSG_[A-Z0-9_]+").Value;
            if (id.Length == 0) throw new InvalidDataException("No controlled message identifier.");
            string root = Environment.GetEnvironmentVariable("TASK069_MESSAGES_GATES");
            string path = Path.Combine(root, id + "." + Process.GetCurrentProcess().Id + ".start.json");
            File.WriteAllText(path + ".tmp", json.Serialize(new { Id = id, Pid = Process.GetCurrentProcess().Id, Arguments = args, Directory = Environment.CurrentDirectory, Prompt = prompt, Raw = raw }), new UTF8Encoding(false)); File.Move(path + ".tmp", path);
            if (id.Contains("HOLD") || id.Contains("CANCEL") || id.Contains("DISPOSE"))
            {
                var watch = Stopwatch.StartNew(); while (!File.Exists(Path.Combine(root, id + ".release"))) { if (watch.ElapsedMilliseconds > 20000) throw new TimeoutException("Fixture wait expired."); Thread.Sleep(5); }
            }
            if (id == "MSG_ERROR" && !File.Exists(Path.Combine(root, id + ".success"))) { Console.Error.WriteLine("Controlled local CLI failure."); return 11; }
            object[] edits = id == "MSG_EDIT" ? new object[] { new { old_text = "", new_text = "SHOULD_NOT_APPLY" } } : new object[0];
            var result = new { summary = "reply-" + id, edits };
            Console.WriteLine(Array.IndexOf(args, "--print") >= 0 ? json.Serialize(new { type = "result", is_error = false, structured_output = result }) : json.Serialize(result));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 12; }
    }
}
