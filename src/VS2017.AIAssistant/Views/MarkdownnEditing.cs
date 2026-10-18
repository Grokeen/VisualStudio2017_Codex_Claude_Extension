// [기능 폴더] Features/Markdown
// [역할] 목록·인용 이어쓰기 등의 마크다운 편집을 보조합니다.
// [읽는 순서] 편집 이벤트 → 현재 줄 분석 → 다음 줄 생성
// [안내] 같은 폴더의 README.md에 관련 파일·연결 지점이 있습니다.
// 기능: Markdownn 편집기의 목록·인용 자동 이어쓰기와 키보드 서식 입력을 계산합니다.
// 출처: 사용자 제공 Markdown_Memo_Interchange/RemoteControlDemo/Features/Markdown/MarkdownnEditing.cs.
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownnEditing
    /// desc         : 제공된 Markdownn 목록·인용·체크 편집을 원문 보존형 유한 helper로 이식한다.
    /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : namespace/.NET462 호환, 행 상한·정규식 timeout 추가. 원본 주석 본문 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class MarkdownnEditing
    {
        internal const int MaximumLineLength = 32768;
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);
        private static readonly Regex ListLine = new Regex(@"^([ \t]*)([-*+]|\d+[.)])([ \t]+)(\[[ xX]\](?:[ \t]+|$))?(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Regex QuoteLine = new Regex(@"^([ \t]*)(>+[ \t]*)(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant, MatchTimeout);

        /// <summary>
        /// name         : IsListLine
        /// desc         : 유한한 한 줄에서 원래 Markdown 목록 기호를 판별한다.
        /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 과대 행·timeout은 자동 편집하지 않음.
        /// </summary>
        internal static bool IsListLine(string line)
        {
            if (line != null && line.Length > MaximumLineLength) return false;
            try { return ListLine.IsMatch(line ?? string.Empty); }
            catch (RegexMatchTimeoutException) { return false; }
        }

        /// <summary>
        /// name         : IsEmptyListLine
        /// desc         : 빈 목록·체크 항목에서 Enter가 목록을 종료할지 계산한다.
        /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 입력 초과 시 원문을 빈 항목으로 오판하지 않음.
        /// </summary>
        internal static bool IsEmptyListLine(string line)
        {
            if (line != null && line.Length > MaximumLineLength) return false;
            try { var match = ListLine.Match(line ?? string.Empty); return match.Success && match.Groups[5].Length == 0; }
            catch (RegexMatchTimeoutException) { return false; }
        }

        // 기존 목록 기호·들여쓰기·본문과 체크 상태를 유지하며 체크 목록으로 바꿉니다.
        // 완료 전환은 실제 체크 항목에만 적용하고 일반 줄은 그대로 둡니다.
        /// <summary>
        /// name         : TaskLine
        /// desc         : 목록 기호와 들여쓰기를 보존해 체크 목록 생성 또는 체크 상태 전환을 계산한다.
        /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 과대 입력·regex timeout에서 원문을 그대로 반환.
        /// </summary>
        internal static string TaskLine(string line, bool toggle)
        {
            line = line ?? string.Empty;
            if (line.Length > MaximumLineLength) return line;
            try
            {
                var list = ListLine.Match(line);
                if (list.Success)
                {
                    if (list.Groups[4].Success)
                    {
                        if (!toggle) return line;
                        int state = list.Groups[4].Index + 1;
                        return line.Substring(0, state) + (line[state] == ' ' ? "x" : " ") + line.Substring(state + 1);
                    }
                    if (toggle) return line;
                    return list.Groups[1].Value + list.Groups[2].Value + list.Groups[3].Value + "[ ] " + list.Groups[5].Value;
                }
                if (toggle) return line;
                int indent = 0;
                while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t')) indent++;
                return line.Substring(0, indent) + "- [ ] " + line.Substring(indent);
            }
            catch (RegexMatchTimeoutException) { return line; }
        }

        // 빈 항목에서 Enter를 누르면 목록을 종료하고, 내용이 있으면 같은 단계의 다음 항목을 시작합니다.
        /// <summary>
        /// name         : Continuation
        /// desc         : 다음 번호·미완료 체크·인용 깊이를 계산하며 빈 항목에서는 종료를 알린다.
        /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 정수 overflow·과대 행은 원문 수정을 유발하지 않음.
        /// </summary>
        internal static string Continuation(string line, out bool removeMarker)
        {
            removeMarker = false;
            if (line != null && line.Length > MaximumLineLength) return null;
            try
            {
                var list = ListLine.Match(line ?? string.Empty);
                if (list.Success)
                {
                    if (list.Groups[5].Value.Length == 0) { removeMarker = true; return list.Groups[1].Value; }
                    string marker = list.Groups[2].Value;
                    if (char.IsDigit(marker[0]))
                    {
                        int number;
                        if (int.TryParse(marker.Substring(0, marker.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out number) && number < int.MaxValue)
                            marker = (number + 1).ToString(CultureInfo.InvariantCulture) + marker.Substring(marker.Length - 1);
                    }
                    return list.Groups[1].Value + marker + list.Groups[3].Value + (list.Groups[4].Success ? "[ ] " : string.Empty);
                }
                var quote = QuoteLine.Match(line ?? string.Empty);
                if (quote.Success)
                {
                    if (quote.Groups[3].Value.Length == 0) { removeMarker = true; return quote.Groups[1].Value; }
                    return quote.Groups[1].Value + quote.Groups[2].Value;
                }
                return null;
            }
            catch (RegexMatchTimeoutException) { return null; }
        }
    }
}
