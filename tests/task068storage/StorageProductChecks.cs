using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : StorageProductChecks
/// desc         : TASK068 보조 개발에서 실제 저장 소스 사본의 페이지·실패·경합 계약을 독립 검증한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : 제품 수정과 OS 설정 변경 없이 실행별 원본 해시·재현 자료 보존.
///                2026-10-20 CodexOrig : 실제 고정 DLL 참조와 source compile을 실행 어셈블리로 구분해 같은 검사 수행.
/// </summary>
/// ---------------------------------------------------------------------
internal static class StorageProductChecks
{
    private static string root;
    private static string fixtures;
    private static int fixtureNumber;
    private static readonly List<object> outcomes = new List<object>();
    private static readonly List<string> observations = new List<string>();
    private static int passed, failed;
    private static DateTimeOffset Start = DateTimeOffset.Parse("2026-10-18T00:00:00Z", CultureInfo.InvariantCulture);

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 개인 fixture에서 모든 경계 검사를 실행하고 실패 원문과 범위를 JSON으로 남긴다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        root = args[0];
        fixtures = args[1];
        Check("Save100_LoadOpen20_LoadOlder80_exact_ids_roles_text_order", Pages);
        Check("Recent20_resave_preserves_history_and_first_title", Resave);
        Check("Append_after_recent_window_preserves_complete120", AppendRecent);
        Check("Manual_title_survives_window_resave", ManualTitle);
        Check("Main_write_failure_preserves_committed_main_bytes", FailedMainBytes);
        Check("Main_write_failure_restores_in_memory_archive_commit_marker", FailedMarker);
        Check("Failed_user_must_not_reappear_in_later_older_page", Ghost);
        Check("Clock_rollback_does_not_lose_predecessor", ClockRollback);
        Check("Same_instant_different_offsets_has_one_hour_bucket", Offsets);
        Check("Cross_timezone_reopen_preserves_hour_pages", ZoneReopen);
        Check("DST_repeated_hour_does_not_reorder_append_sequence", DstRollback);
        Check("Stale_two_controllers_reject_old_writer_preserving_input_and_committed_A", StaleWriters);
        Check("Parallel_same_conversation_preserves_all_successful_appends", ConcurrentWriters);
        Check("Legacy_load_is_read_only_and_preserves_original_conversation_time", LegacyRead);
        Check("Legacy_messages_get_stable_ids_across_repeated_read", LegacyIdentity);
        Check("Legacy_100_migrates_without_duplicate_or_missing_messages", LegacyMigration);
        Check("Legacy_two_readers_migrate_once_without_duplicates", LegacyDoubleMigration);
        Check("Latest_load_does_not_read_locked_older_hour", LockedOlderLatest);
        Check("LoadOlder_does_not_read_later_hour_outside_cursor", LockedLaterPage);
        bool productDll = typeof(MarkdownWorkspaceStore).Assembly != typeof(StorageProductChecks).Assembly;
        var result = new { ActualProductSource = !productDll, ActualProductDll = productDll, ProductFrozen = productDll,
            LoadedProductAssembly = typeof(MarkdownWorkspaceStore).Assembly.Location, GuiUsed = false, SharedBuildUsed = false,
            LocalZone = TimeZoneInfo.Local.Id, FixtureRoot = fixtures, Passed = passed, Failed = failed, Outcomes = outcomes, Observations = observations };
        File.WriteAllText(Path.Combine(root, "result.json"), new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Serialize(result), new UTF8Encoding(false));
        Console.WriteLine((productDll ? "Actual fixed product DLL: " : "Actual product source: ") + passed + "/" + (passed + failed));
        return failed == 0 ? 0 : 1;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 한 저장 계약을 실행하고 다른 실패를 숨기지 않도록 개별 결과를 누적한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, Action test)
    {
        try { test(); passed++; outcomes.Add(new { Name = name, Pass = true, Error = "" }); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; outcomes.Add(new { Name = name, Pass = false, Error = e.ToString() }); Console.WriteLine("FAIL " + name + ": " + e.GetType().Name + ": " + e.Message); }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Require
    /// desc         : 관찰 가능한 저장 계약을 검사해 성공처럼 처리된 누락이나 덮어쓰기를 실패로 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Require(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Store
    /// desc         : 개인 출력 내부의 충돌 없는 저장 루트를 할당해 사용자 기록을 분리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static MarkdownWorkspaceStore Store(string name)
    {
        string path = Path.Combine(fixtures, (++fixtureNumber).ToString("D2"));
        observations.Add("Fixture " + name + " => " + path);
        return new MarkdownWorkspaceStore(path);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Message
    /// desc         : 격리된 실제 저장·WPF 소스에서 Message 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ChatMessage Message(int index, DateTimeOffset? at = null, string text = null)
    {
        return new ChatMessage { Id = index.ToString("x32", CultureInfo.InvariantCulture), Role = index % 2 == 0 ? "user" : "assistant", Text = text ?? ("message-" + index + "\n원문 <!-- assistant-message-end --> : exact"), CreatedAt = at ?? Start.AddMinutes(index) };
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Seed
    /// desc         : 실제 저장소에 독립 대화를 만들어 시간과 원문이 고정된 검사 기록을 준비한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ConversationRecord Seed(MarkdownWorkspaceStore store, int count)
    {
        var record = store.Create("Codex");
        record.Messages = Enumerable.Range(0, count).Select(i => Message(i)).ToList();
        store.Save(record); return record;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Reload
    /// desc         : 격리된 실제 저장·WPF 소스에서 Reload 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ConversationRecord Reload(MarkdownWorkspaceStore store) { return store.LoadOpen("Codex").Single(); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : All
    /// desc         : 실제 이전 페이지 API를 끝까지 읽어 누락·중복과 순서 위반을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static List<ChatMessage> All(MarkdownWorkspaceStore store, ConversationRecord record)
    {
        var result = record.Messages.ToList(); var seen = new HashSet<string>(result.Select(m => m.Id), StringComparer.Ordinal);
        for (int page = 0; page < 30 && result.Count > 0; page++)
        {
            var older = store.LoadOlder(record, result[0], 20);
            if (older.Length == 0) return result;
            foreach (var item in older) Require(seen.Add(item.Id), "Duplicate cursor/message id " + item.Id);
            result.InsertRange(0, older);
        }
        throw new InvalidOperationException("Paging failed to terminate within 30 pages.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Exact
    /// desc         : 격리된 실제 저장·WPF 소스에서 Exact 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Exact(List<ChatMessage> actual, IEnumerable<ChatMessage> expected)
    {
        var wanted = expected.ToArray();
        Require(actual.Count == wanted.Length, "Expected " + wanted.Length + " messages; actual " + actual.Count);
        for (int i = 0; i < wanted.Length; i++) Require(actual[i].Id == wanted[i].Id && actual[i].Role == wanted[i].Role && actual[i].Text == wanted[i].Text,
            "Sequence/payload mismatch at " + i + ": " + actual[i].Text);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pages
    /// desc         : 격리된 실제 저장·WPF 소스에서 Pages 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Pages()
    {
        var s = Store("pages"); var original = Seed(s, 100); var current = Reload(s);
        Require(current.Messages.Count == 20 && current.Messages[0].Id == original.Messages[80].Id, "Main body was not latest20.");
        Exact(All(s, current), original.Messages);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Resave
    /// desc         : 격리된 실제 저장·WPF 소스에서 Resave 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Resave()
    {
        var s = Store("resave"); var original = Seed(s, 100); var current = Reload(s);
        string title = current.Title; s.Save(current); var again = Reload(s);
        Require(again.Title == title && again.FirstUserMessageId == original.Messages[0].Id, "First automatic title changed to a later window question.");
        Exact(All(s, again), original.Messages);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AppendRecent
    /// desc         : 격리된 실제 저장·WPF 소스에서 AppendRecent 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void AppendRecent()
    {
        var s = Store("append"); var original = Seed(s, 100); var current = Reload(s);
        current.Messages.AddRange(Enumerable.Range(100, 20).Select(i => Message(i))); s.Save(current);
        Exact(All(s, Reload(s)), original.Messages.Concat(Enumerable.Range(100, 20).Select(i => Message(i))));
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ManualTitle
    /// desc         : 격리된 실제 저장·WPF 소스에서 ManualTitle 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ManualTitle()
    {
        var s = Store("title"); Seed(s, 100); var current = Reload(s); s.Rename(current, "수동 제목 : 유지"); s.Save(current);
        Require(Reload(s).Title == "수동 제목 : 유지", "Manual title changed.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FailMain
    /// desc         : 격리된 실제 저장·WPF 소스에서 FailMain 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ConversationRecord FailMain(MarkdownWorkspaceStore s)
    {
        var current = Reload(s); var ghost = Message(101, Start.AddHours(2), "rejected-user-GHOST"); ghost.Role = "user"; current.Messages.Add(ghost);
        bool rejected = false;
        using (var locked = new FileStream(s.GetPath(current), FileMode.Open, FileAccess.Read, FileShare.Read))
        { try { s.Save(current); } catch (IOException) { rejected = true; } }
        Require(rejected, "Main write fault was not injected."); return current;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FailedMainBytes
    /// desc         : 격리된 실제 저장·WPF 소스에서 FailedMainBytes 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void FailedMainBytes()
    {
        var s = Store("main-bytes"); var r = Seed(s, 40); var before = File.ReadAllBytes(s.GetPath(r)); FailMain(s);
        Require(before.SequenceEqual(File.ReadAllBytes(s.GetPath(r))), "Main bytes changed after reported failure.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FailedMarker
    /// desc         : 격리된 실제 저장·WPF 소스에서 FailedMarker 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void FailedMarker()
    {
        var s = Store("main-marker"); Seed(s, 40); string before = Reload(s).LastArchivedMessageId; var failedRecord = FailMain(s);
        Require(failedRecord.LastArchivedMessageId == before, "Rejected save advanced record.LastArchivedMessageId to " + failedRecord.LastArchivedMessageId);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Ghost
    /// desc         : 격리된 실제 저장·WPF 소스에서 Ghost 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Ghost()
    {
        var s = Store("ghost"); Seed(s, 40); FailMain(s); var current = Reload(s);
        Require(!current.Messages.Any(m => m.Text.Contains("GHOST")), "Main contained rejected user before next save.");
        current.Messages.Add(Message(102, Start.AddHours(2).AddMinutes(1), "accepted-user-after-failure")); s.Save(current);
        var page = s.LoadOlder(Reload(s), current.Messages.Last(), 100);
        File.WriteAllLines(Path.Combine(root, "ghost-page.txt"), page.Select(m => m.Id + " " + m.Text));
        Require(!page.Any(m => m.Text.Contains("GHOST")), "Uncommitted rejected user appears in accepted history page.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ClockRollback
    /// desc         : 격리된 실제 저장·WPF 소스에서 ClockRollback 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ClockRollback()
    {
        var s = Store("clock-rollback"); var r = s.Create("Codex");
        r.Messages.Add(Message(0, Start.AddHours(3))); r.Messages.Add(Message(1, Start.AddHours(2))); s.Save(r);
        var older = s.LoadOlder(r, r.Messages[1], 20);
        Require(older.Length == 1 && older[0].Id == r.Messages[0].Id, "Clock rollback loses/reorders predecessor; page count=" + older.Length);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Offsets
    /// desc         : 격리된 실제 저장·WPF 소스에서 Offsets 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Offsets()
    {
        var s = Store("offsets"); var r = s.Create("Codex");
        r.Messages.Add(Message(0, Start.ToOffset(TimeSpan.FromHours(9))));
        r.Messages.Add(Message(1, Start.ToOffset(TimeSpan.FromHours(-4)))); s.Save(r);
        var paths = Directory.GetFiles(Path.ChangeExtension(s.GetPath(r), ".history"), "*.md");
        Require(paths.Length == 1, "Same instant created different buckets.");
        Require(s.LoadOlder(r, r.Messages[1], 20).Single().Id == r.Messages[0].Id, "Same instant offset ordering changed.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : InZone
    /// desc         : 격리된 실제 저장·WPF 소스에서 InZone 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void InZone(TimeZoneInfo zone, Action action)
    {
        var cacheField = typeof(TimeZoneInfo).GetFields(BindingFlags.Static | BindingFlags.NonPublic).FirstOrDefault(f => f.Name.IndexOf("cachedData", StringComparison.OrdinalIgnoreCase) >= 0);
        if (cacheField == null) throw new NotSupportedException("Framework TimeZoneInfo cachedData not available; do not alter host settings.");
        var cache = cacheField.GetValue(null);
        var local = cache.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).FirstOrDefault(f => f.FieldType == typeof(TimeZoneInfo) && f.Name.IndexOf("local", StringComparison.OrdinalIgnoreCase) >= 0);
        if (local == null) throw new NotSupportedException("Framework process-local time zone cache field unavailable.");
        var before = local.GetValue(cache);
        try { local.SetValue(cache, zone); Require(TimeZoneInfo.Local.Id == zone.Id, "Process-only zone injection was not effective."); action(); }
        finally { local.SetValue(cache, before); }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ZoneReopen
    /// desc         : 격리된 실제 저장·WPF 소스에서 ZoneReopen 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ZoneReopen()
    {
        var s = Store("zone-reopen"); ConversationRecord r = null;
        InZone(TimeZoneInfo.Utc, () => { r = Seed(s, 100); });
        InZone(TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"), () => { Exact(All(s, Reload(s)), r.Messages); });
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DstRollback
    /// desc         : 격리된 실제 저장·WPF 소스에서 DstRollback 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void DstRollback()
    {
        InZone(TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"), () => {
            var s = Store("dst"); var r = s.Create("Codex");
            r.Messages.Add(Message(0, DateTimeOffset.Parse("2026-11-01T01:59:00-04:00")));
            r.Messages.Add(Message(1, DateTimeOffset.Parse("2026-11-01T01:01:00-05:00"))); s.Save(r);
            Require(s.LoadOlder(r, r.Messages[1], 20).Single().Id == r.Messages[0].Id, "Repeated hour reordered two monotonic UTC messages.");
        });
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : StaleWriters
    /// desc         : 격리된 실제 저장·WPF 소스에서 StaleWriters 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void StaleWriters()
    {
        var s = Store("stale-writers"); Seed(s, 40); var other = new MarkdownWorkspaceStore(s.RootPath);
        var a = Reload(s); var b = Reload(other);
        a.Messages.Add(Message(101, Start.AddHours(2), "accepted-A")); s.Save(a);
        b.Messages.Add(Message(102, Start.AddHours(2).AddMinutes(1), "pending-B"));
        byte[] before = File.ReadAllBytes(s.GetPath(a));
        bool stale = false; try { other.Save(b); } catch (StaleConversationWriteException) { stale = true; }
        Require(stale && b.Messages.Last().Text == "pending-B" && before.SequenceEqual(File.ReadAllBytes(s.GetPath(a))), "Old writer was not explicitly rejected with its input and committed main preserved.");
        var latest = Reload(s);
        Require(latest.Messages.Any(m => m.Text == "accepted-A") && !latest.Messages.Any(m => m.Text == "pending-B"), "Stale writer overwrote committed A or exposed unaccepted B.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConcurrentWriters
    /// desc         : 격리된 실제 저장·WPF 소스에서 ConcurrentWriters 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ConcurrentWriters()
    {
        var s = Store("concurrent-writers"); Seed(s, 40);
        var pending = Enumerable.Range(0, 8).Select(i => new { Store = new MarkdownWorkspaceStore(s.RootPath), Record = Reload(s), Index = i }).ToArray();
        var accepted = new System.Collections.Concurrent.ConcurrentBag<string>(); var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.ForEach(pending, entry => {
            var message = Message(200 + entry.Index, Start.AddHours(3).AddMinutes(entry.Index), "parallel-accepted-" + entry.Index);
            entry.Record.Messages.Add(message);
            try { entry.Store.Save(entry.Record); accepted.Add(message.Id); } catch (IOException e) { errors.Add(e.GetType().Name); }
        });
        var all = All(s, Reload(s)); var missing = accepted.Where(id => !all.Any(m => m.Id == id)).ToArray();
        observations.Add("Parallel writes accepted=" + accepted.Count + ", IO rejected=" + errors.Count + ", accepted missing=" + missing.Length);
        Require(accepted.Count == 1 && missing.Length == 0, "Expected exactly one accepted original-revision writer and zero lost accepted messages; accepted=" + accepted.Count + ",missing=" + missing.Length);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Legacy
    /// desc         : 격리된 실제 저장·WPF 소스에서 Legacy 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ConversationRecord Legacy(MarkdownWorkspaceStore s, int count)
    {
        var r = s.Create("Codex"); r.CreatedAt = DateTimeOffset.Parse("2026-09-01T10:15:00+09:00"); s.Save(r);
        string path = s.GetPath(r); string content = File.ReadAllText(path); string header = content.Substring(0, content.IndexOf("---\n\n", 4, StringComparison.Ordinal) + 5);
        header = Regex.Replace(header, @"(?m)^(last_archived_message_id|first_user_message_id):[^\n]*\n", "");
        var body = new StringBuilder(header);
        foreach (var m in Enumerable.Range(0, count).Select(i => Message(i)))
        {
            body.Append("## ").Append(m.Role == "user" ? "사용자" : "AI").Append("\n\n<!-- assistant-message role=").Append(m.Role).Append(" chars=").Append(m.Text.Length).Append(" -->\n").Append(m.Text).Append("\n<!-- assistant-message-end -->\n\n");
        }
        body.Append("## 작성 중인 요청\n\n<!-- assistant-draft chars=0 -->\n\n<!-- assistant-draft-end -->\n");
        File.WriteAllText(path, body.ToString(), new UTF8Encoding(false)); return r;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : LegacyRead
    /// desc         : 격리된 실제 저장·WPF 소스에서 LegacyRead 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void LegacyRead()
    {
        var s = Store("legacy-read"); var source = Legacy(s, 100); string path = s.GetPath(source); var before = File.ReadAllBytes(path); var r = Reload(s);
        Require(r.Messages.Count == 100 && r.CreatedAt == source.CreatedAt && r.Messages.All(m => m.CreatedAt == source.CreatedAt), "Legacy original conversation timestamp fallback changed.");
        Require(before.SequenceEqual(File.ReadAllBytes(path)), "Read rewrote legacy original.");
        observations.Add("Legacy has no per-message original timestamps; current fallback stamps every message with conversation.CreatedAt (not actual event time).");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : LegacyIdentity
    /// desc         : 격리된 실제 저장·WPF 소스에서 LegacyIdentity 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void LegacyIdentity()
    {
        var s = Store("legacy-id"); Legacy(s, 40); var a = Reload(s); var b = Reload(s);
        Require(a.Messages.Select(m => m.Id).SequenceEqual(b.Messages.Select(m => m.Id)), "Two legacy readers assign different IDs to identical persisted messages.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : LegacyMigration
    /// desc         : 격리된 실제 저장·WPF 소스에서 LegacyMigration 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void LegacyMigration()
    {
        var s = Store("legacy-migrate"); Legacy(s, 100); var record = Reload(s); var original = record.Messages.ToList(); s.Save(record); Exact(All(s, Reload(s)), original);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : LegacyDoubleMigration
    /// desc         : 격리된 실제 저장·WPF 소스에서 LegacyDoubleMigration 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void LegacyDoubleMigration()
    {
        var s = Store("legacy-double"); Legacy(s, 100); var first = Reload(s); var second = Reload(s);
        s.Save(first); byte[] before = File.ReadAllBytes(s.GetPath(first));
        bool stale = false; try { s.Save(second); } catch (StaleConversationWriteException) { stale = true; }
        Require(stale && second.Messages.Count == 100 && before.SequenceEqual(File.ReadAllBytes(s.GetPath(first))), "Old legacy reader must be explicitly rejected and original/caller payloads preserved.");
        var all = All(s, Reload(s));
        Require(all.Count == 100 && all.Select(m => m.Text).Distinct(StringComparer.Ordinal).Count() == 100,
            "Two successful legacy migrations produced " + all.Count + " messages for original100.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : LockedOlderLatest
    /// desc         : 격리된 실제 저장·WPF 소스에서 LockedOlderLatest 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void LockedOlderLatest()
    {
        var s = Store("locked-older-latest"); var record = Seed(s, 100); string oldest = Directory.GetFiles(Path.ChangeExtension(s.GetPath(record), ".history"), "*.md").OrderBy(p => p, StringComparer.Ordinal).First();
        using (var locked = new FileStream(oldest, FileMode.Open, FileAccess.Read, FileShare.None)) Require(Reload(s).Messages.Count == 20, "Latest tried reading archive.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : LockedLaterPage
    /// desc         : 격리된 실제 저장·WPF 소스에서 LockedLaterPage 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void LockedLaterPage()
    {
        var s = Store("locked-later-page"); var record = Seed(s, 100); string latest = Directory.GetFiles(Path.ChangeExtension(s.GetPath(record), ".history"), "*.md").OrderBy(p => p, StringComparer.Ordinal).Last();
        using (var locked = new FileStream(latest, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var older = s.LoadOlder(record, record.Messages[40], 20);
            Require(older.Length == 20 && older[0].Id == record.Messages[20].Id, "Earlier page incorrect.");
        }
    }
}
