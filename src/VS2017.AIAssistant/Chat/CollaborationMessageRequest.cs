using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace VS2017.AIAssistant.Chat
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CollaborationMessageRequest
    /// desc         : AI가 작성한 전달 초안을 파싱하며 실제 전송 권한이나 실행으로 취급하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 정확한 수신 주소·본문의 사용자 검토용 fenced protocol.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class CollaborationMessageRequest
    {
        public string RecipientId { get; private set; }
        public string Provider { get; private set; }
        public string Message { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryParse
        /// desc         : 완결된 단일 ai_message fence의 알려진 문자열 필드만 초안으로 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 중복 JSON 키·추가 필드·모호 주소·여러 요청·대용량 거절.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static bool TryParse(string answer, out CollaborationMessageRequest request)
        {
            request = null;
            if (String.IsNullOrWhiteSpace(answer) || answer.Length > 128 * 1024) return false;
            var fences = Regex.Matches(answer, @"(?m)^```ai_message[ \t]*\r?\n([\s\S]*?)\r?\n```[ \t]*$", RegexOptions.CultureInvariant);
            if (fences.Count != 1 || Regex.Matches(answer, @"(?m)^```ai_message\b").Count != 1) return false;
            string json = fences[0].Groups[1].Value;
            if (json.Length > 9000) return false;
            // 고정 3개 필드의 JSON 문자열만 허용해 serializer의 중복 키 덮어쓰기를 차단한다.
            const string value = @"""(?:[^""\\\x00-\x1f]|\\(?:[""\\/bfnrt]|u[0-9a-fA-F]{4}))*""";
            var members = Regex.Matches(json, @"""(recipient_id|provider|message)""\s*:\s*(" + value + ")");
            if (members.Count != 3 || members.Cast<Match>().Select(m => m.Groups[1].Value).Distinct().Count() != 3) return false;
            string remainder = Regex.Replace(json, @"""(recipient_id|provider|message)""\s*:\s*(" + value + ")", "");
            if (!Regex.IsMatch(remainder, @"\A\s*\{\s*,\s*,\s*\}\s*\z")) return false;
            try
            {
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(json);
                string id = data["recipient_id"], provider = data["provider"], message = data["message"];
                if (!Regex.IsMatch(id ?? "", @"\A[0-9a-f]{32}\z") || (provider != "Codex" && provider != "Claude") ||
                    String.IsNullOrWhiteSpace(message) || message.Length > 6000 || message.IndexOf('\0') >= 0) return false;
                request = new CollaborationMessageRequest { RecipientId = id, Provider = provider, Message = message };
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
    }
}
