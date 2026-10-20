using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace VS2017.AIAssistant.Chat
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AttachmentDisplayInfo
    /// desc         : 첨부 이름·텍스트 종류·실제 읽은 본문 분량과 재사용 동작만 표시한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #13 본문 재전사·전송 완료 추정 없이 캡처된 첨부 수치 안내.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class AttachmentDisplayInfo
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IconText
        /// desc         : 이미지 미리보기와 구분되는 텍스트/추출 문서 종류 배지를 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 파일명과 별도로 TXT/DOC 글자 아이콘을 제공한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string IconText(ChatAttachment item)
        { return item == null || item.IsImage ? "" : item.IsDocument ? "DOC" : "TXT"; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TextSize
        /// desc         : 텍스트 본문의 실제 문자 수와 UTF8 분량을 원문 변경 없이 계산한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 파일 원본 크기와 추출된 본문 크기를 혼동하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string TextSize(ChatAttachment item)
        {
            if (item == null) return "";
            if (item.IsImage) return "이미지 포함";
            string text = item.Text ?? "";
            return "본문 " + text.Length.ToString("N0", CultureInfo.CurrentCulture) + "자 · UTF-8 " + Encoding.UTF8.GetByteCount(text).ToString("N0", CultureInfo.CurrentCulture) + "바이트";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Tooltip
        /// desc         : 전체 파일명·본문 분량과 다음 전송에도 남는 첨부 동작을 안내한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 접근성 도움말과 동일한 정보이며 첨부 본문을 노출하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string Tooltip(ChatAttachment item)
        {
            return item == null ? "" : item.Name + "\n" + TextSize(item) + "\n요청과 함께 보낼 첨부입니다. 다음 요청에도 유지되며 ×로 제거할 수 있습니다.";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SubmissionSummary
        /// desc         : 이번 요청에 고정한 파일명·분량만 사용자 메시지에 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 수신·모델 사용 완료를 주장하지 않으며 원문은 attachments에만 담는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string SubmissionSummary(ChatAttachment[] items)
        {
            if (items == null || items.Length == 0) return "";
            return "\n첨부 본문 포함 요청 · " + items.Length + "개\n" + String.Join("\n", items.Select(item => "• " + item.Name + " — " + TextSize(item))) +
                "\n첨부는 다음 요청에도 유지됩니다. 제외하려면 입력란의 ×로 제거하세요.";
        }
    }
}
