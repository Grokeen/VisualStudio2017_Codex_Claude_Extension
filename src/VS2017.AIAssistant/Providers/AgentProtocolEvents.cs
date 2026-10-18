using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VS2017.AIAssistant.Providers
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AgentProgress
    /// desc         : CLI가 공개한 진행 이벤트만 전달하며 내부 추론과 명령 본문은 수집하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 Role 추가 9번의 실제 중간 진행 표시.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class AgentProgress
    {
        public string Text { get; set; }
        public string Kind { get; set; }
        public string WorkId { get; set; }
        public string State { get; set; }
        public string Detail { get; set; }
        public int? ExitCode { get; set; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AgentProtocolEvents
    /// desc         : 공개 작업 이벤트와 누적 diff를 해석하여 실제 실행 근거를 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 중간 상태·다중 파일·반복 편집 처리.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class AgentProtocolEvents
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ParseProgressEvents
        /// desc         : Claude의 한 메시지에 함께 담긴 공개 작업도 빠짐없이 개별 상태로 전달한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 여러 tool_use·tool_result의 ID별 상태 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static AgentProgress[] ParseProgressEvents(string provider, string json)
        {
            var message = RuntimeJson.Parse(json);
            string type = RuntimeJson.Text(message, "type");
            if (provider == "Claude" && (type == "assistant" || type == "user")) {
                var results = new List<AgentProgress>();
                foreach (var block in RuntimeJson.Items(RuntimeJson.Get(RuntimeJson.Child(message, "message"), "content")).Take(200)) {
                    var single = new Dictionary<string, object> { { "type", type }, { "message", new Dictionary<string, object> { { "content", new[] { block } } } } };
                    var progress = ParseProgress(provider, RuntimeJson.Serialize(single));
                    if (progress != null) results.Add(progress);
                }
                return results.ToArray();
            }
            var activity = ParseProgress(provider, json);
            return activity == null ? new AgentProgress[0] : new[] { activity };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ParseProgress
        /// desc         : 명령·파일·도구의 실제 시작과 종료만 문구로 바꾼다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 추정 진행률과 숨은 추론 표시 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static AgentProgress ParseProgress(string provider, string json)
        {
            var message = RuntimeJson.Parse(json);
            string method = RuntimeJson.Text(message, "method"), type = RuntimeJson.Text(message, "type");
            if (provider == "Codex") {
                bool completed = method == "item/completed" || type == "item.completed";
                bool started = method == "item/started" || type == "item.started";
                if (!started && !completed) return null;
                var item = method.Length > 0 ? RuntimeJson.Child(RuntimeJson.Child(message, "params"), "item") : RuntimeJson.Child(message, "item");
                string itemType = RuntimeJson.Text(item, "type"), status = RuntimeJson.Text(item, "status");
                string state = !completed ? "시작" : status == "failed" || status == "declined" ? "실패" : "완료";
                if (itemType == "commandExecution" || itemType == "command_execution") return Work(item, "command", state, SafeCommand(RuntimeJson.Text(item, "command", 2000)));
                if (itemType == "fileChange" || itemType == "file_change") return Work(item, "file", state, String.Join(", ", RuntimeJson.Items(RuntimeJson.Get(item, "changes")).Take(3).Select(change => SafeFile(RuntimeJson.Text(RuntimeJson.Object(change), "path", 500)))));
                if (itemType == "mcpToolCall" || itemType == "mcp_tool_call" || itemType == "webSearch" || itemType == "web_search") return Work(item, "tool", state, itemType == "webSearch" || itemType == "web_search" ? "웹 검색" : "연결된 도구");
                if (completed && itemType == "agentMessage" && RuntimeJson.Text(item, "phase") == "commentary")
                    return new AgentProgress { Kind = "message", Text = RuntimeJson.Text(item, "text", 2000) };
            }
            else if (type == "assistant") {
                foreach (var raw in RuntimeJson.Items(RuntimeJson.Get(RuntimeJson.Child(message, "message"), "content"))) {
                    var block = RuntimeJson.Object(raw);
                    if (RuntimeJson.Text(block, "type") == "tool_use") {
                        string name = RuntimeJson.Text(block, "name");
                        var input = RuntimeJson.Child(block, "input");
                        string kind = name == "Bash" ? "command" : name == "Edit" || name == "Write" ? "file" : "tool";
                        return Work(block, kind, "시작", kind == "command" ? SafeCommand(RuntimeJson.Text(input, "command", 2000)) : kind == "file" ? SafeFile(RuntimeJson.Text(input, "file_path", 500)) : "연결된 도구");
                    }
                    // thinking 블록은 공개 진행 메시지에 포함하지 않는다.
                    if (RuntimeJson.Text(block, "type") == "text") return new AgentProgress { Kind = "message", Text = RuntimeJson.Text(block, "text", 2000) };
                }
            }
            else if (type == "user") {
                foreach (var raw in RuntimeJson.Items(RuntimeJson.Get(RuntimeJson.Child(message, "message"), "content"))) {
                    var block = RuntimeJson.Object(raw);
                    if (RuntimeJson.Text(block, "type") == "tool_result") return new AgentProgress { Kind = "tool", WorkId = RuntimeJson.Text(block, "tool_use_id", 120), State = Equals(RuntimeJson.Get(block, "is_error"), true) ? "실패" : "완료", Text = Equals(RuntimeJson.Get(block, "is_error"), true) ? "⌕ 도구 작업 실패" : "⌕ 도구 작업 완료" };
                }
            }
            return null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Work
        /// desc         : 실제 작업 ID·단계·종료 코드를 보존하고 명령 인수나 출력 본문을 표시하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 20:25 요청의 작업별 중복 갱신과 세부 진행 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static AgentProgress Work(IDictionary<string, object> item, string kind, string state, string detail)
        {
            int code;
            int? exit = Int32.TryParse(Convert.ToString(RuntimeJson.Get(item, "exitCode") ?? RuntimeJson.Get(item, "exit_code"), System.Globalization.CultureInfo.InvariantCulture), out code) ? (int?)code : null;
            if (state == "완료" && exit.HasValue && exit.Value != 0) state = "실패";
            string label = kind == "command" ? "⌘ 명령 실행" : kind == "file" ? "✎ 파일 변경" : "⌕ 도구 작업";
            return new AgentProgress { Kind = kind, WorkId = RuntimeJson.Text(item, "id", 120), State = state, Detail = detail, ExitCode = exit, Text = label + " " + state + (String.IsNullOrWhiteSpace(detail) ? "" : " · " + detail) + (exit.HasValue ? " · 종료 코드 " + exit.Value : "") };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SafeCommand
        /// desc         : 알려진 실행 파일과 제한된 하위 작업명만 공개하고 인수·스크립트·비밀값은 제외한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 진행을 빌드·테스트·형상관리·셸 실행으로 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string SafeCommand(string command)
        {
            var match = Regex.Match(command ?? "", "^\\s*(?:\"(?<exe>[^\"]+)\"|(?<exe>[^\\s;|&]+))(?<rest>.*)$");
            string file = SafeFile(match.Groups["exe"].Value).ToLowerInvariant();
            if (file.EndsWith(".exe", StringComparison.Ordinal)) file = file.Substring(0, file.Length - 4);
            string name;
            switch (file) { case "powershell": case "pwsh": name = "PowerShell"; break; case "cmd": case "bash": case "sh": name = "셸"; break; case "dotnet": name = ".NET"; break; case "msbuild": name = "MSBuild"; break; case "csc": name = "C# 컴파일"; break; case "git": name = "Git"; break; case "svn": name = "SVN"; break; case "tf": name = "TFVC"; break; case "npm": name = "npm"; break; case "node": name = "Node.js"; break; case "python": case "python3": name = "Python"; break; default: name = "외부 명령"; break; }
            if (file == "dotnet" || file == "npm" || file == "git" || file == "svn" || file == "tf") {
                var action = Regex.Match(match.Groups["rest"].Value, "^\\s+(build|test|restore|run|format|status|diff|log|checkout|update|commit|checkin)(?:\\s|$)");
                if (action.Success) name += " " + action.Groups[1].Value;
            }
            return name;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SafeFile
        /// desc         : 진행 이름에는 디렉터리·제어문자를 제외한 짧은 파일 이름만 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 파일별 진행 상세와 안전한 표시 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string SafeFile(string path)
        {
            try { string name = Path.GetFileName(path ?? ""); return name.Length > 80 || name.Any(Char.IsControl) ? "파일" : name; }
            catch (ArgumentException) { return "파일"; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ParseAggregatedDiff
        /// desc         : turn 누적 diff의 파일 경계를 분리하여 같은 파일의 이전 수정도 유지한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 Codex turn/diff/updated 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static AgentFileChange[] ParseAggregatedDiff(string directory, string diff)
        {
            if (String.IsNullOrEmpty(diff)) return new AgentFileChange[0];
            var results = new List<AgentFileChange>();
            var lines = diff.Replace("\r\n", "\n").Split('\n');
            var current = new StringBuilder(); string file = null, previous = null;
            Action flush = () => { if (file != null) results.Add(new AgentFileChange { Path = Path.GetFullPath(Path.IsPathRooted(file) ? file : Path.Combine(directory, file)), Kind = "누적 변경", Diff = current.ToString() }); current.Clear(); file = null; previous = null; };
            bool hunk = false, numbered = false; int oldRemaining = 0, newRemaining = 0;
            foreach (string line in lines) {
                if (line.StartsWith("diff --git ", StringComparison.Ordinal)) { flush(); hunk = false; }
                if (!hunk && line.StartsWith("--- ", StringComparison.Ordinal)) { if (file != null) flush(); previous = DecodePath(line.Substring(4)); }
                if (!hunk && line.StartsWith("+++ ", StringComparison.Ordinal)) file = DecodePath(line.Substring(4)) ?? previous;
                if (line.StartsWith("@@", StringComparison.Ordinal)) { hunk = true; numbered = TryHunkCounts(line, out oldRemaining, out newRemaining); }
                else if (hunk && numbered) ConsumeHunkLine(line, ref oldRemaining, ref newRemaining);
                if (numbered && oldRemaining <= 0 && newRemaining <= 0) hunk = false;
                current.AppendLine(line);
            }
            flush(); return results.ToArray();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DecodePath
        /// desc         : diff 경로의 접두어와 JSON 호환 인용을 해제하며 해석 불가능한 경로는 생략한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 공백·삭제 파일 경로 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string DecodePath(string value)
        {
            value = value.Split('\t')[0].Trim();
            if (value == "/dev/null") return null;
            if (value.StartsWith("\"", StringComparison.Ordinal)) {
                try { value = RuntimeJson.Text(RuntimeJson.Parse("{\"path\":" + value + "}"), "path", 32768); }
                catch (ArgumentException) { return null; }
            }
            if (value.StartsWith("a/", StringComparison.Ordinal) || value.StartsWith("b/", StringComparison.Ordinal)) value = value.Substring(2);
            return value.Length == 0 || value.IndexOf('\0') >= 0 ? null : value;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CountDiff
        /// desc         : hunk 안의 부호만 세어 ++ 또는 --로 시작하는 실제 코드도 빠뜨리지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 파일 헤더와 소스 줄 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static bool CountDiff(string diff, out int added, out int removed)
        {
            added = removed = 0; bool hunk = false, found = false, numbered = false; int oldRemaining = 0, newRemaining = 0;
            if (String.IsNullOrEmpty(diff)) return false;
            foreach (string line in diff.Replace("\r\n", "\n").Split('\n')) {
                if (line.StartsWith("diff --git ", StringComparison.Ordinal)) hunk = false;
                if (line.StartsWith("@@", StringComparison.Ordinal)) { hunk = true; found = true; numbered = TryHunkCounts(line, out oldRemaining, out newRemaining); continue; }
                if (!hunk) continue;
                if (line.StartsWith("+", StringComparison.Ordinal)) added++;
                if (line.StartsWith("-", StringComparison.Ordinal)) removed++;
                if (numbered) { ConsumeHunkLine(line, ref oldRemaining, ref newRemaining); if (oldRemaining <= 0 && newRemaining <= 0) hunk = false; }
            }
            return found;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryHunkCounts
        /// desc         : 표준 hunk 행수를 읽어 실제 코드와 다음 파일 헤더를 구분한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 반복한 파일 헤더의 가짜 추가·삭제 계수 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool TryHunkCounts(string line, out int oldCount, out int newCount)
        {
            oldCount = newCount = 1; var match = Regex.Match(line, @"^@@ -\d+(?:,(\d+))? \+\d+(?:,(\d+))? @@");
            return match.Success && (!match.Groups[1].Success || Int32.TryParse(match.Groups[1].Value, out oldCount)) && (!match.Groups[2].Success || Int32.TryParse(match.Groups[2].Value, out newCount));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConsumeHunkLine
        /// desc         : 추가·삭제·공통 문맥에 맞춰 남은 hunk 행수를 줄인다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 표준 diff의 종료 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ConsumeHunkLine(string line, ref int oldCount, ref int newCount)
        {
            if (line.StartsWith("-", StringComparison.Ordinal) || line.StartsWith(" ", StringComparison.Ordinal)) oldCount--;
            if (line.StartsWith("+", StringComparison.Ordinal) || line.StartsWith(" ", StringComparison.Ordinal)) newCount--;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FirstChangedLine
        /// desc         : 표준 hunk의 실제 수정 후 시작 행을 소스 이동 위치로 사용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 직접 변경 카드의 소스 위치 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static int FirstChangedLine(string diff)
        {
            var match = Regex.Match(diff ?? "", @"(?m)^@@ -\d+(?:,\d+)? \+(\d+)"); int line;
            return match.Success && Int32.TryParse(match.Groups[1].Value, out line) ? Math.Max(1, line) : 1;
        }
    }
}
