using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VS2017.AIAssistant.Chat;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ActivityFeedContracts
/// desc         : 실제 순수 집계 소스의 소유권·상태 중첩·안정 이름·개인정보·상한 계약을 검증한다.
/// author       : CodexOrig
/// create date  : 2026-11-03
/// </summary>
/// ---------------------------------------------------------------------
internal static class ActivityFeedContracts
{
    private static int passed;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 각 독립 계약이 실패하면 실행을 중단하고 원래 실패를 남긴다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, bool condition)
    {
        if (!condition) throw new InvalidOperationException("FAIL " + name);
        passed++; Console.WriteLine("PASS " + name);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Row
    /// desc         : 본문과 제목 없이 실제 저장 식별자 형식의 합성 상태 입력을 만든다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ConversationActivityInput Row(int id, ConversationRunState state = ConversationRunState.Idle, string provider = "Claude")
    {
        return new ConversationActivityInput { Provider = provider, Id = id.ToString("x32"), State = state, IsOpen = true,
            LastActivityAt = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero) };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Snapshot
    /// desc         : 동일한 실제 집계 API를 필터별로 호출한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ConversationActivitySnapshot Snapshot(ConversationActivityInput input, ConversationActivityFilter filter = ConversationActivityFilter.All)
    { return ConversationActivityFeed.Build(new[] { input }, filter); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SingleEnumeration
    /// desc         : 외부 입력을 여러 번 순회하지 않는 집계 경계를 관찰한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class SingleEnumeration : IEnumerable<ConversationActivityInput>
    {
        internal int Enumerations;
        private readonly IEnumerable<ConversationActivityInput> rows;
        internal SingleEnumeration(IEnumerable<ConversationActivityInput> rows) { this.rows = rows; }
        public IEnumerator<ConversationActivityInput> GetEnumerator()
        {
            if (++Enumerations != 1) throw new InvalidOperationException("Input enumerated twice.");
            return rows.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 실제 집계 소스만 별도 프로세스에서 검사하고 제품 UI·저장·CLI 실행은 하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    public static int Main()
    {
        try
        {
            var a = Row(1, ConversationRunState.Completed); a.HasUnread = true;
            var b = Row(1, ConversationRunState.Failed, "Codex");
            var pair = ConversationActivityFeed.Build(new[] { a, b }, ConversationActivityFilter.All);
            Check("provider plus id keeps separate rows", pair.TotalCount == 2 && pair.VisibleItems.Select(r => r.Provider).Distinct().Count() == 2);
            Check("unread is independent and exact", pair.UnreadCount == 1);
            Check("failed and completed counts remain distinct", pair.FailedOrStoppedCount == 1 && pair.CompletedCount == 1);
            string safeA = pair.VisibleItems.Single(r => r.Provider == "Claude").SafeName;
            Check("safe name contains provider and fixed hash token", System.Text.RegularExpressions.Regex.IsMatch(safeA, "^Claude · 대화 [0-9A-F]{12}$"));
            Check("safe name never contains original identifier", !safeA.Contains(a.Id));
            Check("provider participates in stable name", pair.VisibleItems.Select(r => r.SafeName.Substring(r.SafeName.LastIndexOf(' ') + 1)).Distinct().Count() == 2);
            a.Ordinal = 999;
            Check("legacy ordinal never changes stable name", Snapshot(a).VisibleItems.Single().SafeName == safeA);
            Check("insertion does not rename existing room", ConversationActivityFeed.Build(new[] { Row(2), a }, ConversationActivityFilter.All).VisibleItems.Single(r => r.Id == a.Id).SafeName == safeA);
            var recovered = Row(1, ConversationRunState.Completed);
            Check("reconstructed record keeps stable identity name", Snapshot(recovered).VisibleItems.Single().SafeName == safeA);
            Check("zero ordinal remains a valid room", Snapshot(recovered).TotalCount == 1);

            var live = Row(3, ConversationRunState.Completed); live.HasLiveRun = true;
            Check("live request overrides stale completed state", Snapshot(live).VisibleItems.Single().Kind == ConversationActivityKind.Working && Snapshot(live).CompletedCount == 0);
            live.SaveFailed = true;
            Check("save failure remains primary fixed status", Snapshot(live).VisibleItems.Single().Kind == ConversationActivityKind.SaveFailed);
            Check("save failure does not hide actual running filter", Snapshot(live, ConversationActivityFilter.Running).FilteredCount == 1 && Snapshot(live).RunningCount == 1);
            Check("save failure and live remains attention", Snapshot(live, ConversationActivityFilter.Attention).FilteredCount == 1 && Snapshot(live).AttentionCount == 1);
            Check("save failure and live summary reports both fixed facts", Snapshot(live).VisibleItems.Single().Summary.Contains("저장") && Snapshot(live).VisibleItems.Single().Summary.Contains("실행 중"));
            Check("save failure never enters completed filter", Snapshot(live, ConversationActivityFilter.Completed).FilteredCount == 0);
            live.State = ConversationRunState.Failed;
            Check("save and execution failure retain failure filter", Snapshot(live, ConversationActivityFilter.FailedOrStopped).FilteredCount == 1);
            live.SaveFailed = false;
            Check("terminal failure is never completed during cleanup", Snapshot(live).VisibleItems.Single().Kind == ConversationActivityKind.Failed && Snapshot(live).CompletedCount == 0);

            var queue = Row(4, ConversationRunState.Completed); queue.PendingCount = 3;
            Check("pending requests require attention", Snapshot(queue).AttentionCount == 1);
            Check("pending requests remain in running and waiting filter", Snapshot(queue, ConversationActivityFilter.Running).FilteredCount == 1 && Snapshot(queue).RunningCount == 1);
            Check("pending requests are visibly waiting", Snapshot(queue).VisibleItems.Single().Kind == ConversationActivityKind.Waiting && Snapshot(queue).VisibleItems.Single().Summary.Contains("3개"));
            Check("pending requests suppress old completion", Snapshot(queue).CompletedCount == 0);
            queue.HasProposal = true;
            Check("pending proposal with queue remains review", Snapshot(queue).VisibleItems.Single().Kind == ConversationActivityKind.Review && Snapshot(queue).AttentionCount == 1);
            Check("review does not erase queued count", Snapshot(queue).VisibleItems.Single().PendingCount == 3 && Snapshot(queue).RunningCount == 1);
            queue.HasProposal = false; queue.PendingCount = 0;
            Check("no typed change proposal has no review", Snapshot(queue).VisibleItems.Single().Kind == ConversationActivityKind.Completed);
            queue.HasChoices = true;
            Check("typed choices require fixed review", Snapshot(queue).VisibleItems.Single().Kind == ConversationActivityKind.Review);
            queue.HasChoices = false; queue.PendingCount = -4; queue.State = ConversationRunState.Idle;
            Check("negative pending input is never displayed", Snapshot(queue).VisibleItems.Single().PendingCount == 0 && Snapshot(queue).RunningCount == 0 && !Snapshot(queue).VisibleItems.Single().Summary.Contains("-4"));

            foreach (var state in new[] { ConversationRunState.Failed, ConversationRunState.Stopped, ConversationRunState.Interrupted })
                Check("terminal state preserved " + state, Snapshot(Row(5, state)).FailedOrStoppedCount == 1 && Snapshot(Row(5, state)).CompletedCount == 0);
            var orphan = Row(6, ConversationRunState.Running);
            Check("unowned running is interrupted", Snapshot(orphan).VisibleItems.Single().Kind == ConversationActivityKind.Interrupted && Snapshot(orphan).RunningCount == 0);
            orphan.SaveFailed = true;
            Check("save failure does not hide unowned running interruption", Snapshot(orphan).FailedOrStoppedCount == 1 && Snapshot(orphan).CompletedCount == 0);
            Check("idle is not inferred complete", Snapshot(Row(7)).CompletedCount == 0);
            Check("unknown persisted enum rejected", Snapshot(Row(8, (ConversationRunState)999)).TotalCount == 0);
            var closed = Row(9); closed.IsOpen = false;
            Check("closed rooms excluded", Snapshot(closed).TotalCount == 0);
            Check("unknown provider rejected", Snapshot(Row(9, provider: "private-provider-path")).TotalCount == 0);
            foreach (var id in new[] { "../private", "C:\\private\\token", new string('f', 31), new string('F', 32), new string('a', 31) + "\n", " ", null })
            {
                var invalid = Row(10); invalid.Id = id;
                Check("unsafe identifier rejected " + (id == null ? "null" : id.Length.ToString()), Snapshot(invalid).TotalCount == 0);
            }
            Check("ambiguous composite keys are all rejected", ConversationActivityFeed.Build(new[] { a, a, b }, ConversationActivityFilter.All).TotalCount == 1);
            Check("null enumerable yields empty snapshot", ConversationActivityFeed.Build(null, ConversationActivityFilter.All).TotalCount == 0);
            Check("null records are skipped", ConversationActivityFeed.Build(new ConversationActivityInput[] { null, recovered }, ConversationActivityFilter.All).TotalCount == 1);

            var many = Enumerable.Range(100, 110).Select(n => { var row = Row(n, ConversationRunState.Completed); row.LastActivityAt = row.LastActivityAt.AddDays(-n); return row; }).ToList();
            var oldActive = Row(999, ConversationRunState.Completed, "Codex"); oldActive.HasLiveRun = true; oldActive.SaveFailed = true; oldActive.LastActivityAt = oldActive.LastActivityAt.AddYears(-1); many.Add(oldActive);
            var limited = ConversationActivityFeed.Build(many, ConversationActivityFilter.All);
            Check("full counts precede display limit", limited.TotalCount == 111 && limited.FilteredCount == 111 && limited.VisibleItems.Length == 80);
            Check("old active attention row prioritized", limited.VisibleItems.First().Id == oldActive.Id && limited.HiddenRunningCount == 0);
            var overflow = Enumerable.Range(300, 90).Select(n => { var row = Row(n); row.HasLiveRun = true; row.SaveFailed = true; return row; }).ToArray();
            var burst = ConversationActivityFeed.Build(overflow, ConversationActivityFilter.Running);
            Check("overlapping active failures remain counted", burst.RunningCount == 90 && burst.AttentionCount == 90);
            Check("hidden active attention counts are explicit", burst.VisibleItems.Length == 80 && burst.HiddenRunningCount == 10 && burst.HiddenAttentionCount == 10);
            Check("running filter total remains uncapped", burst.FilteredCount == 90);
            var sequence = new SingleEnumeration(Enumerable.Range(1000, 1000).Select(n => Row(n)));
            var thousand = ConversationActivityFeed.Build(sequence, ConversationActivityFilter.All);
            Check("external input enumerated once", sequence.Enumerations == 1 && thousand.TotalCount == 1000);
            Check("stable ordering independent of enumeration order", ConversationActivityFeed.Build(many.AsEnumerable().Reverse(), ConversationActivityFilter.All).VisibleItems.Select(r => r.Id).SequenceEqual(limited.VisibleItems.Select(r => r.Id)));
            string visible = String.Join("|", pair.VisibleItems.Select(r => r.SafeName + r.Status + r.Summary + r.Glyph + r.BrushKey));
            Check("visible strings never expose exact internal ids", !visible.Contains(a.Id) && !visible.Contains(b.Id));
            Check("snapshot does not mutate source state", a.HasUnread && a.State == ConversationRunState.Completed && live.State == ConversationRunState.Failed);
            Console.WriteLine("TOTAL " + passed + " 0"); return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }
}
