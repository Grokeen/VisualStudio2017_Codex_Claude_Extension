using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace VS2017.AIAssistant.Editing
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : EditProposal
    /// desc         : AI의 정확한 문자열 교체 제안을 원본에 검증하고 미리보기를 만든다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 모호한 일치·겹침·과도한 변경 거부 구현.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class EditProposal
    {
        public const int MaxDocumentLength = 200000;
        public const string JsonSchema = "{\"type\":\"object\",\"properties\":{\"summary\":{\"type\":\"string\"},\"edits\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"old_text\":{\"type\":\"string\"},\"new_text\":{\"type\":\"string\"}},\"required\":[\"old_text\",\"new_text\"],\"additionalProperties\":false}}},\"required\":[\"summary\",\"edits\"],\"additionalProperties\":false}";
        public DocumentSnapshot Source { get; private set; }
        public string ProposedText { get; private set; }
        public string Summary { get; private set; }
        public string ChangePreview { get; private set; }
        public int AddedLines { get; private set; }
        public int RemovedLines { get; private set; }
        public int FirstChangedLine { get; private set; }
        public bool HasChanges { get { return !string.Equals(Source.Content, ProposedText, StringComparison.Ordinal); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EditProposal
        /// desc         : 검증된 스냅샷과 수정안만 생성할 수 있도록 생성자를 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 불변 수정안 초기화.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private EditProposal() { }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Parse
        /// desc         : CLI JSON을 엄격하게 읽고 모든 교체를 동일한 원본 기준으로 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 단일 파일 변경 및 원본 줄 위치 비교 구현.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static EditProposal Parse(DocumentSnapshot source, string json)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Content.Length > MaxDocumentLength) throw new InvalidOperationException("현재 파일은 200,000자 제한을 초과합니다.");
            if (json == null || json.Length > 2000000) throw new InvalidOperationException("AI 응답 크기가 올바르지 않습니다.");
            Dictionary<string, object> root;
            try { root = new JavaScriptSerializer { MaxJsonLength = 2000000, RecursionLimit = 16 }.DeserializeObject(json) as Dictionary<string, object>; }
            catch (ArgumentException) { throw new InvalidOperationException("AI 응답이 올바른 수정안 JSON이 아닙니다."); }
            if (root == null || root.Count != 2 || !root.ContainsKey("summary") || !root.ContainsKey("edits") || !(root["summary"] is string))
                throw new InvalidOperationException("수정안의 summary/edits 형식이 올바르지 않습니다.");
            var edits = root["edits"] as object[];
            if (edits == null || edits.Length > 64 || ((string)root["summary"]).Length > 4000)
                throw new InvalidOperationException("수정안은 최대 64개 변경과 4,000자 설명을 허용합니다.");
            string newline = source.Content.Contains("\r\n") ? "\r\n" : "\n";
            var changes = new List<Tuple<int, string, string>>();
            foreach (object item in edits)
            {
                var edit = item as Dictionary<string, object>;
                if (edit == null || edit.Count != 2 || !edit.ContainsKey("old_text") || !edit.ContainsKey("new_text") ||
                    !(edit["old_text"] is string) || !(edit["new_text"] is string))
                    throw new InvalidOperationException("수정안은 old_text/new_text 문자열만 포함해야 합니다.");
                string oldText = NormalizeNewLines((string)edit["old_text"], newline);
                string newText = NormalizeNewLines((string)edit["new_text"], newline);
                bool emptyDocumentInsertion = source.Content.Length == 0 && edits.Length == 1 && oldText.Length == 0;
                if ((!emptyDocumentInsertion && oldText.Length == 0) || oldText.Length > MaxDocumentLength || newText.Length > MaxDocumentLength)
                    throw new InvalidOperationException("원본 교체 범위가 비어 있거나 변경 크기가 너무 큽니다.");
                int start = emptyDocumentInsertion ? 0 : source.Content.IndexOf(oldText, StringComparison.Ordinal);
                if (!emptyDocumentInsertion && (start < 0 || source.Content.IndexOf(oldText, start + 1, StringComparison.Ordinal) >= 0))
                    throw new InvalidOperationException("원본에서 교체할 내용이 없거나 여러 번 발견되었습니다. 수정안을 다시 생성하세요.");
                changes.Add(Tuple.Create(start, oldText, newText));
            }
            changes = changes.OrderBy(change => change.Item1).ToList();
            for (int index = 1; index < changes.Count; index++)
                if (changes[index].Item1 < changes[index - 1].Item1 + changes[index - 1].Item2.Length)
                    throw new InvalidOperationException("겹치는 수정 범위는 적용할 수 없습니다.");
            var result = new StringBuilder(source.Content);
            foreach (var change in changes.AsEnumerable().Reverse())
            {
                result.Remove(change.Item1, change.Item2.Length).Insert(change.Item1, change.Item3);
                if (result.Length > MaxDocumentLength * 2) throw new InvalidOperationException("수정된 파일의 크기가 제한을 초과합니다.");
            }
            var preview = new StringBuilder();
            int added = 0, removed = 0;
            foreach (var change in changes)
            {
                if (change.Item2 != change.Item3)
                {
                    var oldLines = change.Item2.Replace("\r\n", "\n").Split('\n');
                    var newLines = change.Item3.Replace("\r\n", "\n").Split('\n');
                    int prefix = 0, suffix = 0;
                    while (prefix < Math.Min(oldLines.Length, newLines.Length) && oldLines[prefix] == newLines[prefix]) prefix++;
                    while (suffix < Math.Min(oldLines.Length, newLines.Length) - prefix && oldLines[oldLines.Length - suffix - 1] == newLines[newLines.Length - suffix - 1]) suffix++;
                    removed += change.Item2.Length == 0 ? 0 : oldLines.Length - prefix - suffix;
                    added += change.Item3.Length == 0 ? 0 : newLines.Length - prefix - suffix;
                }
                int startLine = 1 + source.Content.Take(change.Item1).Count(character => character == '\n');
                int endLine = startLine + change.Item2.Count(character => character == '\n');
                preview.AppendLine("원본 줄 " + startLine + " ~ " + endLine);
                preview.AppendLine("--- 변경 전").AppendLine(change.Item2);
                preview.AppendLine("+++ 변경 후").AppendLine(change.Item3).AppendLine();
            }
            return new EditProposal { Source = source, ProposedText = result.ToString(), Summary = (string)root["summary"], ChangePreview = preview.ToString(), AddedLines = added, RemovedLines = removed,
                FirstChangedLine = changes.Count == 0 ? 1 : 1 + source.Content.Take(changes[0].Item1).Count(c => c == '\n') };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NormalizeNewLines
        /// desc         : AI 교체 조각의 개행을 원본 문서의 개행 방식에 맞춘다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : CRLF/LF 차이로 인한 잘못된 교체 방지.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string NormalizeNewLines(string value, string newline)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", newline);
        }
    }
}
