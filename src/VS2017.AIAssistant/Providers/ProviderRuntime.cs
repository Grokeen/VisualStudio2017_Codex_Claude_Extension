using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace VS2017.AIAssistant.Providers
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProviderUsageSnapshot
    /// desc         : CLI가 실제 보고한 토큰·할당량만 보관하고 미제공 값은 null로 구분한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ProviderUsageSnapshot
    {
        public long? InputTokens { get; set; }
        public long? OutputTokens { get; set; }
        public long? CachedInputTokens { get; set; }
        public long? ContextWindow { get; set; }
        public long? TotalTokens { get; set; }
        public double? QuotaUsedPercent { get; set; }
        public DateTimeOffset? QuotaResetsAt { get; set; }
        public string Source { get; set; }
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Describe
        /// desc         : 확인된 토큰과 계정 한도를 서로 다른 단위로 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string Describe()
        {
            Func<long?, string> value = n => n.HasValue ? n.Value.ToString("N0", CultureInfo.CurrentCulture) : "확인 불가";
            return "실제 보고 사용량\n입력(캐시 포함): " + value(InputTokens) + " tokens\n출력: " + value(OutputTokens) +
                " tokens\n캐시 입력: " + value(CachedInputTokens) + " tokens\n합계: " + value(TotalTokens) +
                " tokens\n컨텍스트 한도: " + value(ContextWindow) + " tokens\n계정 할당량 사용률: " +
                (QuotaUsedPercent.HasValue ? QuotaUsedPercent.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "확인 불가") +
                (QuotaResetsAt.HasValue ? "\n한도 초기화: " + QuotaResetsAt.Value.ToLocalTime().ToString("g") : "") +
                "\n출처: " + (Source ?? "CLI가 아직 사용량을 제공하지 않음") +
                "\n갱신: " + UpdatedAt.ToLocalTime().ToString("HH:mm:ss") + "\n수신한 이벤트마다 갱신합니다. 미제공 수치는 추정하지 않습니다.";
        }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProviderRuntimeInfo
    /// desc         : 연결된 CLI가 반환한 계정 종류와 모델 기능의 조회 결과다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ProviderRuntimeInfo
    {
        public string Provider { get; set; }
        public string ExecutablePath { get; set; }
        public bool? Authenticated { get; set; }
        public string Plan { get; set; }
        public string Status { get; set; }
        public bool SupportsProtocol { get; set; }
        public ProviderModelCatalog Catalog { get; set; }
        public ProviderUsageSnapshot Usage { get; set; }
        public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.Now;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AgentFileChange
    /// desc         : CLI가 실제 보고한 파일 변경과 원문 diff를 화면에 전달한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class AgentFileChange
    {
        public string Path { get; set; }
        public string Kind { get; set; }
        public string Diff { get; set; }
        public bool HasLineCounts { get; set; } = true;
        public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.Now;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AgentExecutionResult
    /// desc         : 전체 권한 실행의 실제 최종 답변·파일 변경·토큰을 보관한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class AgentExecutionResult
    {
        public string Text { get; set; }
        public AgentFileChange[] Changes { get; set; } = new AgentFileChange[0];
        public ProviderUsageSnapshot Usage { get; set; }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RuntimeJson
    /// desc         : 네이티브 CLI 프로토콜의 선택 필드를 유효한 값으로만 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class RuntimeJson
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Object
        /// desc         : JSON 객체가 아닌 응답을 빈 객체로 구분한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static IDictionary<string, object> Object(object value)
        { return value as IDictionary<string, object> ?? new Dictionary<string, object>(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Get
        /// desc         : 미제공 JSON 필드를 null로 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static object Get(IDictionary<string, object> value, string key)
        { object result; return value != null && value.TryGetValue(key, out result) ? result : null; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Child
        /// desc         : 중첩 객체의 형식을 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static IDictionary<string, object> Child(IDictionary<string, object> value, string key)
        { return Object(Get(value, key)); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Text
        /// desc         : 메타데이터 텍스트의 크기와 제어 문자를 제한한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string Text(IDictionary<string, object> value, string key, int maximum = 500)
        {
            string text = Get(value, key) as string;
            return text == null ? "" : new string(text.Where(c => !Char.IsControl(c) || c == '\n' || c == '\r' || c == '\t').Take(maximum).ToArray());
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Items
        /// desc         : JSON 배열만 순회하며 객체를 배열로 오인하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static object[] Items(object value)
        { return value as object[] ?? (value is System.Collections.ArrayList ? ((System.Collections.ArrayList)value).ToArray() : new object[0]); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Count
        /// desc         : 음수·오버플로·미제공 수치를 알려진 토큰 수로 표시하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static long? Count(IDictionary<string, object> value, string key)
        {
            long parsed; object raw = Get(value, key);
            return raw != null && Int64.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out parsed) && parsed >= 0 ? (long?)parsed : null;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Parse
        /// desc         : 프로토콜 JSON 크기와 재귀 깊이를 제한한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static IDictionary<string, object> Parse(string value)
        { return new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024, RecursionLimit = 64 }.DeserializeObject(value) as IDictionary<string, object> ?? new Dictionary<string, object>(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Serialize
        /// desc         : 요청 데이터를 셸 인수가 아닌 stdin JSON으로 직렬화한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 CLI 메타데이터와 전체 권한 실행 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string Serialize(object value)
        { return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024, RecursionLimit = 64 }.Serialize(value); }
    }
}

