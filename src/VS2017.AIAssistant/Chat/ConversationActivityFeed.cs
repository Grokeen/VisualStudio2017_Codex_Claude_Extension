using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace VS2017.AIAssistant.Chat
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConversationActivityFilter
    /// desc         : 열린 대화의 확인 필요·실행·완료·실패 항목을 분류한다.
    /// author       : CodexCode
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    internal enum ConversationActivityFilter { All, Attention, Running, Completed, FailedOrStopped }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConversationActivityKind
    /// desc         : 실제 저장 상태와 실행 소유권에서 확인 가능한 활동 단계만 나타낸다.
    /// author       : CodexCode
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    internal enum ConversationActivityKind { Idle, Waiting, Working, Review, Completed, Failed, Stopped, Interrupted, SaveFailed }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConversationActivityInput
    /// desc         : 원문·제목·경로 없이 메모리에서 집계할 형식화된 상태 입력이다.
    /// author       : CodexCode
    /// create date  : 2026-11-03
    /// update date  : 2026-11-03 CodexOrig : 순번을 이름에 사용하지 않고 실제 실행·대기·제안 상태만 집계.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class ConversationActivityInput
    {
        internal string Provider;
        internal string Id;
        internal int Ordinal = 0;
        internal bool IsOpen;
        internal bool HasUnread;
        internal bool HasLiveRun;
        internal bool HasProposal;
        internal bool HasChoices;
        internal bool SaveFailed;
        internal int PendingCount;
        internal ConversationRunState State;
        internal DateTimeOffset LastActivityAt;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConversationActivityItem
    /// desc         : UI에 노출 가능한 고정 문구와 정확한 provider·id 이동 키를 분리한다.
    /// author       : CodexCode
    /// create date  : 2026-11-03
    /// update date  : 2026-11-03 CodexOrig : 안정 식별명과 독립된 실행·대기·실패 필터 근거를 유지.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class ConversationActivityItem
    {
        internal string Provider { get; set; }
        internal string Id { get; set; }
        internal string SafeName { get; set; }
        internal string Status { get; set; }
        internal string Summary { get; set; }
        internal string Glyph { get; set; }
        internal string BrushKey { get; set; }
        internal DateTimeOffset LastActivityAt { get; set; }
        internal bool HasUnread { get; set; }
        internal bool NeedsAttention { get; set; }
        internal ConversationActivityKind Kind { get; set; }
        internal bool HasLiveRun { get; set; }
        internal int PendingCount { get; set; }
        internal bool FailedOrStopped { get; set; }
        internal bool IsRunningOrQueued { get { return HasLiveRun || PendingCount > 0; } }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConversationActivitySnapshot
    /// desc         : 표시 행 수와 무관하게 모든 열린 대화의 필터 수를 정확히 보관한다.
    /// author       : CodexCode
    /// create date  : 2026-11-03
    /// update date  : 2026-11-03 CodexOrig : 표시 상한 밖의 주의·실행 항목도 전체 집계에 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class ConversationActivitySnapshot
    {
        internal ConversationActivityItem[] VisibleItems { get; set; }
        internal int TotalCount { get; set; }
        internal int FilteredCount { get; set; }
        internal int UnreadCount { get; set; }
        internal int AttentionCount { get; set; }
        internal int RunningCount { get; set; }
        internal int CompletedCount { get; set; }
        internal int FailedOrStoppedCount { get; set; }
        internal int HiddenAttentionCount { get; set; }
        internal int HiddenRunningCount { get; set; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConversationActivityFeed
    /// desc         : 저장 파일을 읽지 않고 열린 대화별 최신 한 행을 안전한 형식화 상태로 만든다.
    /// author       : CodexCode
    /// create date  : 2026-11-03
    /// update date  : 2026-11-03 CodexOrig : 유효 소유 키를 한 번 집계하고 안전 이름·고정 문구만 생성.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class ConversationActivityFeed
    {
        internal const int MaximumVisibleItems = 80;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Build
        /// desc         : 중복 복합 키를 제거하고 전체 수를 센 뒤 필터·표시 상한을 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig : 유효 복합 키를 한 번 집계하고 독립 실행·확인 필요 수를 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static ConversationActivitySnapshot Build(IEnumerable<ConversationActivityInput> inputs, ConversationActivityFilter filter)
        {
            var valid = new List<ConversationActivityInput>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var input in inputs ?? Enumerable.Empty<ConversationActivityInput>())
            {
                if (input == null || !input.IsOpen || !IsValidIdentity(input.Provider, input.Id) ||
                    !Enum.IsDefined(typeof(ConversationRunState), input.State)) continue;
                valid.Add(input); string key = input.Provider + "\0" + input.Id; int count;
                counts.TryGetValue(key, out count); counts[key] = count + 1;
            }
            var rows = new List<ConversationActivityItem>();
            foreach (var input in valid)
            {
                if (counts[input.Provider + "\0" + input.Id] != 1) continue;
                rows.Add(Create(input));
            }
            rows.Sort((left, right) => {
                int priority = Priority(left).CompareTo(Priority(right));
                if (priority != 0) return priority;
                int date = right.LastActivityAt.CompareTo(left.LastActivityAt);
                return date != 0 ? date : StringComparer.Ordinal.Compare(left.Provider + "\0" + left.Id, right.Provider + "\0" + right.Id);
            });
            var matching = rows.Where(row => Matches(row, filter)).ToArray();
            var visible = matching.Take(MaximumVisibleItems).ToArray();
            var hidden = matching.Skip(visible.Length).ToArray();
            return new ConversationActivitySnapshot {
                VisibleItems = visible,
                TotalCount = rows.Count, FilteredCount = matching.Length,
                UnreadCount = rows.Count(row => row.HasUnread),
                AttentionCount = rows.Count(row => row.NeedsAttention),
                RunningCount = rows.Count(row => row.IsRunningOrQueued),
                CompletedCount = rows.Count(row => row.Kind == ConversationActivityKind.Completed),
                FailedOrStoppedCount = rows.Count(row => row.FailedOrStopped),
                HiddenAttentionCount = hidden.Count(row => row.NeedsAttention),
                HiddenRunningCount = hidden.Count(row => row.IsRunningOrQueued)
            };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Priority
        /// desc         : 표시 상한에서 확인 필요와 실제 실행을 오래된 완료 기록보다 먼저 보여준다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig : 저장 실패 등과 겹쳐도 실제 실행·대기 항목을 먼저 표시.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static int Priority(ConversationActivityItem item)
        { return item.NeedsAttention ? (item.IsRunningOrQueued ? 0 : 1) : item.IsRunningOrQueued ? 2 : 3; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Create
        /// desc         : 제목과 메시지를 참조하지 않고 상태 우선순위에 따라 고정 요약을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig : 순번 대신 안전한 안정 식별명과 독립 상태 플래그·고정 요약 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static ConversationActivityItem Create(ConversationActivityInput input)
        {
            ConversationActivityKind kind;
            if (input.SaveFailed) kind = ConversationActivityKind.SaveFailed;
            else if (input.State == ConversationRunState.Failed) kind = ConversationActivityKind.Failed;
            else if (input.State == ConversationRunState.Stopped) kind = ConversationActivityKind.Stopped;
            else if (input.State == ConversationRunState.Interrupted) kind = ConversationActivityKind.Interrupted;
            else if (input.HasLiveRun) kind = ConversationActivityKind.Working;
            else if (input.State == ConversationRunState.Running) kind = ConversationActivityKind.Interrupted;
            else if (input.HasProposal || input.HasChoices) kind = ConversationActivityKind.Review;
            else if (input.PendingCount > 0) kind = ConversationActivityKind.Waiting;
            else if (input.State == ConversationRunState.Completed) kind = ConversationActivityKind.Completed;
            else kind = ConversationActivityKind.Idle;
            string status, summary, glyph, brush;
            switch (kind)
            {
                case ConversationActivityKind.SaveFailed: status = "저장 실패 · 확인 필요"; summary = "대화 저장을 다시 확인하세요."; glyph = "!"; brush = "AssistantWarningBrush"; break;
                case ConversationActivityKind.Working: status = "실행 중"; summary = input.PendingCount > 0 ? "요청 실행 중 · 대기 " + input.PendingCount + "개" : "요청을 실행하고 있습니다."; glyph = "◌"; brush = "AssistantLinkBrush"; break;
                case ConversationActivityKind.Waiting: status = "대기 중"; summary = "대기 요청 " + input.PendingCount + "개"; glyph = "◷"; brush = "AssistantLinkBrush"; break;
                case ConversationActivityKind.Interrupted: status = "이전 실행 중단"; summary = "실행 상태를 대화에서 확인하세요."; glyph = "!"; brush = "AssistantWarningBrush"; break;
                case ConversationActivityKind.Failed: status = "실패"; summary = "실패 내용을 대화에서 확인하세요."; glyph = "!"; brush = "AssistantRemovedBrush"; break;
                case ConversationActivityKind.Stopped: status = "중지됨"; summary = "중지 결과를 대화에서 확인하세요."; glyph = "■"; brush = "AssistantWarningBrush"; break;
                case ConversationActivityKind.Review: status = "확인 필요"; summary = input.HasProposal ? "수정안 검토가 필요합니다." : "대화에서 선택을 확인하세요."; glyph = "◆"; brush = "AssistantWarningBrush"; break;
                case ConversationActivityKind.Completed: status = "완료"; summary = "완료된 대화를 확인하세요."; glyph = "✓"; brush = "AssistantAddedBrush"; break;
                default: status = "유휴"; summary = "새 요청을 기다립니다."; glyph = "○"; brush = "AssistantMutedForegroundBrush"; break;
            }
            int pending = Math.Max(0, input.PendingCount);
            if (kind == ConversationActivityKind.SaveFailed && input.HasLiveRun) summary += " · 요청 실행 중";
            if (kind != ConversationActivityKind.Waiting && kind != ConversationActivityKind.Working && pending > 0) summary += " · 대기 " + pending + "개";
            if (kind != ConversationActivityKind.Review && input.HasProposal) summary += " · 수정안 검토 필요";
            else if (kind != ConversationActivityKind.Review && input.HasChoices) summary += " · 선택 필요";
            return new ConversationActivityItem {
                Provider = input.Provider, Id = input.Id, SafeName = SafeName(input.Provider, input.Id),
                Status = status, Summary = summary, Glyph = glyph, BrushKey = brush,
                LastActivityAt = input.LastActivityAt, HasUnread = input.HasUnread,
                NeedsAttention = input.SaveFailed || input.HasProposal || input.HasChoices || pending > 0 || IsFailure(kind) || input.HasUnread,
                Kind = kind, HasLiveRun = input.HasLiveRun, PendingCount = pending,
                FailedOrStopped = IsFailure(kind) || input.State == ConversationRunState.Failed || input.State == ConversationRunState.Stopped || input.State == ConversationRunState.Interrupted ||
                    input.State == ConversationRunState.Running && !input.HasLiveRun
            };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Matches
        /// desc         : 필터 수를 표시 상한 전에 계산할 수 있도록 상태를 분류한다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig : 저장 실패와 실행 상태가 겹쳐도 각 필터에서 실제 근거 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool Matches(ConversationActivityItem row, ConversationActivityFilter filter)
        {
            switch (filter)
            {
                case ConversationActivityFilter.Attention: return row.NeedsAttention;
                case ConversationActivityFilter.Running: return row.IsRunningOrQueued;
                case ConversationActivityFilter.Completed: return row.Kind == ConversationActivityKind.Completed;
                case ConversationActivityFilter.FailedOrStopped: return row.FailedOrStopped;
                default: return true;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsFailure
        /// desc         : 실패·중지·재시작 중단을 별도 필터에만 확정 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsFailure(ConversationActivityKind kind)
        { return kind == ConversationActivityKind.Failed || kind == ConversationActivityKind.Stopped || kind == ConversationActivityKind.Interrupted; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsValidIdentity
        /// desc         : 저장 모델과 동일한 제공자 및 소문자 32자리 식별자만 허용한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static bool IsValidIdentity(string provider, string id)
        {
            if ((provider != "Claude" && provider != "Codex") || id == null || id.Length != 32) return false;
            foreach (char value in id) if (!((value >= '0' && value <= '9') || (value >= 'a' && value <= 'f'))) return false;
            return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SafeName
        /// desc         : 제목·원문·원래 식별자를 노출하지 않는 안정된 제공자별 해시 이름을 만든다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string SafeName(string provider, string id)
        {
            using (var digest = SHA256.Create())
                return provider + " · 대화 " + BitConverter.ToString(digest.ComputeHash(Encoding.UTF8.GetBytes(provider + "\0" + id)), 0, 6).Replace("-", "");
        }
    }
}
