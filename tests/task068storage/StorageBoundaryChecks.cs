using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : StorageBoundaryChecks
/// desc         : TASK068 원자 저장·불변 참조·긴 경로·4MB 분할을 실제 수정 사본에서 독립 검증한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : 기존19개 검사의 기대를 완화하지 않고 경계 실패를 추가 검증.
///                2026-10-20 CodexOrig : 실제 DLL에서도 동일한 내부 실패 주입을 reflection으로 연결하고 전체 예외 원문 보존.
/// </summary>
/// ---------------------------------------------------------------------
internal static class StorageBoundaryChecks
{
    private static string root, fixtures;
    private static int number, passed, failed;
    private static readonly List<object> outcomes = new List<object>();
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-18T10:00:00Z", CultureInfo.InvariantCulture);
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
        root = args[0]; fixtures = args[1];
        Test("Same_UTC_hour_splits_before_four_MiB_including_MD_overhead", Split);
        Test("After_chunks_fault_preserves_head_metadata_and_no_ghost", () => Fault("after-chunks"));
        Test("After_immutable_index_fault_preserves_head_metadata_and_no_ghost", () => Fault("after-index"));
        Test("Before_main_commit_fault_preserves_head_metadata_and_no_ghost", () => Fault("before-main"));
        Test("Crash_orphan_chunk_and_index_are_never_read_or_accepted", Orphan);
        Test("Old_snapshot_cursor_excludes_later_appends", Snapshot);
        Test("Index_corruption_is_explicit_and_preserves_cursor", CorruptIndex);
        Test("Chunk_corruption_is_explicit_but_latest20_remains_lazy", CorruptChunk);
        Test("Updating_one_recent_message_preserves_every_other_history_message", Upsert);
        Test("Invalid_duplicate_id_and_timestamp_cannot_change_main", Invalid);
        Test("History_directory223_supports_27char_chunks_and_short_atomic_sibling", LongPath);
        Test("Same_provider_other_conversations_write_independently", Independent);
        Test("Manual_main_edit_after_load_is_stale_and_preserves_both_inputs", ManualEdit);
        bool productDll = typeof(MarkdownWorkspaceStore).Assembly != typeof(StorageBoundaryChecks).Assembly;
        File.WriteAllText(Path.Combine(root, "boundary-result.json"), new JavaScriptSerializer().Serialize(new {
            Passed = passed, Failed = failed, ProductSourceCopy = !productDll, ActualProductDll = productDll,
            LoadedProductAssembly = typeof(MarkdownWorkspaceStore).Assembly.Location, Outcomes = outcomes }), new UTF8Encoding(false));
        Console.WriteLine("Storage boundary checks " + passed + "/" + (passed + failed)); return failed == 0 ? 0 : 1;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Test
    /// desc         : 한 비동기·경계 시나리오를 실행하고 최초 오류의 전체 근거를 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Test(string name, Action action)
    {
        try { action(); passed++; outcomes.Add(new { Name = name, Pass = true, Error = "" }); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; outcomes.Add(new { Name = name, Pass = false, Error = e.ToString() }); Console.WriteLine("FAIL " + name + ": " + e); }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Must
    /// desc         : 관찰 가능한 저장·화면 결과가 기대와 다르면 원문을 보존하고 검사를 실패시킨다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Must(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Store
    /// desc         : 개인 출력 내부의 충돌 없는 저장 루트를 할당해 사용자 기록을 분리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static MarkdownWorkspaceStore Store() { return new MarkdownWorkspaceStore(Path.Combine(fixtures, (++number).ToString("D2"))); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Message
    /// desc         : 격리된 실제 저장·WPF 소스에서 Message 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ChatMessage Message(int i, string text = null) { return new ChatMessage { Id = i.ToString("x32"), Role = i % 2 == 0 ? "user" : "assistant", CreatedAt = At.AddSeconds(i), Text = text ?? "body-" + i }; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Seed
    /// desc         : 실제 저장소에 독립 대화를 만들어 시간과 원문이 고정된 검사 기록을 준비한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ConversationRecord Seed(MarkdownWorkspaceStore s, int count)
    {
        var r = s.Create("Codex"); r.Messages = Enumerable.Range(0, count).Select(i => Message(i)).ToList(); s.Save(r); return r;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Load
    /// desc         : 격리된 실제 저장·WPF 소스에서 Load 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ConversationRecord Load(MarkdownWorkspaceStore s) { return s.LoadOpen("Codex").Single(); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : All
    /// desc         : 실제 이전 페이지 API를 끝까지 읽어 누락·중복과 순서 위반을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static List<ChatMessage> All(MarkdownWorkspaceStore s, ConversationRecord r)
    {
        var result = r.Messages.ToList();
        for (int i = 0; i < 30 && result.Count > 0; i++) { var page = s.LoadOlder(r, result[0], 20); if (page.Length == 0) return result; result.InsertRange(0, page); }
        throw new InvalidDataException("Page termination failed.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : History
    /// desc         : 격리된 실제 저장·WPF 소스에서 History 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string History(MarkdownWorkspaceStore s, ConversationRecord r) { return Path.ChangeExtension(s.GetPath(r), ".history"); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Split
    /// desc         : 격리된 실제 저장·WPF 소스에서 Split 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Split()
    {
        var s = Store(); var r = s.Create("Codex");
        r.Messages = Enumerable.Range(0, 100).Select(i => Message(i, new string('x', 41900) + i.ToString("D2"))).ToList(); s.Save(r);
        var files = Directory.GetFiles(History(s, r), "*.md");
        Must(files.Length >= 2 && files.All(p => new FileInfo(p).Length <= 4 * 1024 * 1024), "MD overhead was not included in rollover.");
        var all = All(s, Load(s)); Must(all.Count == 100 && all.Select(m => m.Id).Distinct().Count() == 100, "Rollover lost/duplicated messages.");
        Must(all.Select(m => m.Text).SequenceEqual(r.Messages.Select(m => m.Text)), "Rollover changed payload/order.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Fault
    /// desc         : 격리된 실제 저장·WPF 소스에서 Fault 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Fault(string stage)
    {
        var s = Store(); var original = Seed(s, 40); var r = Load(s); byte[] head = File.ReadAllBytes(s.GetPath(r));
        string revision = r.StorageRevision, manifest = r.HistoryManifest, last = r.LastArchivedMessageId, title = r.Title; var updated = r.UpdatedAt;
        r.Messages.Add(Message(100, "unaccepted-ghost-" + stage)); r.Draft = "keep-next-draft";
        SetFaultInjector(s, position => { if (position == stage) throw new IOException("Injected " + stage); });
        bool failed = false; try { s.Save(r); } catch (IOException) { failed = true; } finally { SetFaultInjector(s, null); }
        Must(failed && head.SequenceEqual(File.ReadAllBytes(s.GetPath(r))), "Fault changed committed head.");
        Must(r.StorageRevision == revision && r.HistoryManifest == manifest && r.LastArchivedMessageId == last && r.Title == title && r.UpdatedAt == updated, "Fault changed commit metadata.");
        Must(r.Messages.Last().Text == "unaccepted-ghost-" + stage && r.Draft == "keep-next-draft", "Fault erased unaccepted input/draft.");
        Must(All(s, Load(s)).Count == 40, "Fault exposed staged body.");
        var next = Load(s); next.Messages.Add(Message(101, "accepted-after-fault")); s.Save(next);
        var all = All(s, Load(s)); Must(all.Count == 41 && !all.Any(m => m.Text.StartsWith("unaccepted-ghost-")), "Later save exposed ghost.");
        Must(!Directory.GetFiles(History(s, r), "*.tmp").Any(), "Temporary output left after fault.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Orphan
    /// desc         : 격리된 실제 저장·WPF 소스에서 Orphan 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Orphan()
    {
        var s = Store(); var r = Seed(s, 100); string history = History(s, r);
        File.WriteAllBytes(Path.Combine(history, "2000010100-deadbeef-0000.md"), new byte[] { 255, 254, 0 });
        File.WriteAllText(Path.Combine(history, "i-deadbeefdeadbeef.json"), "{invalid orphan index}");
        Must(All(s, Load(s)).Count == 100, "Unreferenced orphan was read.");
        var current = Load(s); current.Messages.Add(Message(100)); s.Save(current);
        Must(All(s, Load(s)).Count == 101, "Orphan was imported on next save.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Snapshot
    /// desc         : 격리된 실제 저장·WPF 소스에서 Snapshot 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Snapshot()
    {
        var s = Store(); Seed(s, 100); var snapshot = Load(s); var writer = Load(s);
        writer.Messages.Add(Message(100)); s.Save(writer);
        var old = All(s, snapshot); Must(old.Count == 100 && !old.Any(m => m.Id == Message(100).Id), "Old snapshot included later write.");
        Must(All(s, Load(s)).Count == 101, "New snapshot missing append.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CorruptIndex
    /// desc         : 격리된 실제 저장·WPF 소스에서 CorruptIndex 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void CorruptIndex()
    {
        var s = Store(); Seed(s, 100); var r = Load(s); string cursor = r.Messages[0].Id;
        string path = Path.Combine(History(s, r), r.HistoryManifest); File.AppendAllText(path, " ");
        bool rejected = false; try { s.LoadOlder(r, r.Messages[0], 20); } catch (InvalidDataException) { rejected = true; }
        Must(rejected && r.Messages[0].Id == cursor, "Corrupt index silently advanced/replaced cursor.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CorruptChunk
    /// desc         : 격리된 실제 저장·WPF 소스에서 CorruptChunk 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void CorruptChunk()
    {
        var s = Store(); var original = Seed(s, 100); var r = Load(s);
        string path = Directory.GetFiles(History(s, r), "*.md").First(); File.AppendAllText(path, "tampered");
        Must(Load(s).Messages.Count == 20, "Latest eagerly opened corrupted historical chunk.");
        bool rejected = false; try { s.LoadOlder(r, r.Messages[0], 20); } catch (InvalidDataException) { rejected = true; }
        Must(rejected, "Corrupt chunk was accepted.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Upsert
    /// desc         : 격리된 실제 저장·WPF 소스에서 Upsert 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Upsert()
    {
        var s = Store(); Seed(s, 100); var r = Load(s); string id = r.Messages[3].Id;
        r.Messages[3].Text = "updated with original identity"; s.Save(r); var all = All(s, Load(s));
        Must(all.Count == 100 && all.Select(m => m.Id).Distinct().Count() == 100 && all.Single(m => m.Id == id).Text == "updated with original identity", "Upsert lost/duplicated history.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Invalid
    /// desc         : 격리된 실제 저장·WPF 소스에서 Invalid 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Invalid()
    {
        var s = Store(); var r = Seed(s, 40); byte[] head = File.ReadAllBytes(s.GetPath(r));
        r.Messages.Add(Message(0)); bool duplicate = false; try { s.Save(r); } catch (InvalidDataException) { duplicate = true; } r.Messages.RemoveAt(r.Messages.Count - 1);
        r.Messages[0].CreatedAt = default(DateTimeOffset); bool time = false; try { s.Save(r); } catch (InvalidDataException) { time = true; }
        Must(duplicate && time && head.SequenceEqual(File.ReadAllBytes(s.GetPath(r))), "Invalid request changed committed head.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : LongPath
    /// desc         : 격리된 실제 저장·WPF 소스에서 LongPath 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void LongPath()
    {
        string prefix = Path.Combine(fixtures, "long");
        var r = new ConversationRecord { Id = Guid.NewGuid().ToString("N"), Provider = "Codex", CreatedAt = At };
        int targetRootLength = 223 - ("\\conversations\\codex\\" + r.Id + ".history").Length;
        string path = prefix + new string('p', targetRootLength - prefix.Length);
        var s = new MarkdownWorkspaceStore(path); r.Messages.Add(Message(0)); s.Save(r);
        string history = History(s, r); Must(history.Length == 223, "Long-path fixture is not original223 directory boundary.");
        string file = Path.Combine(history, "2026101810-0000.md");
        var atomic = typeof(MarkdownWorkspaceStore).GetMethod("AtomicWrite", BindingFlags.Instance | BindingFlags.NonPublic);
        atomic.Invoke(s, new object[] { file, "preserved atomic sibling" });
        Must(File.ReadAllText(file) == "preserved atomic sibling" && !Directory.GetFiles(history, "*.tmp").Any(), "Short sibling atomic write failed.");
        var current = Load(s); current.Messages.Add(Message(1)); s.Save(current); Must(Load(s).Messages.Count == 2, "Long-path history append failed.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Independent
    /// desc         : 격리된 실제 저장·WPF 소스에서 Independent 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Independent()
    {
        var s = Store(); var a = s.Create("Codex"); var b = s.Create("Codex");
        a.Messages.Add(Message(0)); b.Messages.Add(Message(1)); System.Threading.Tasks.Parallel.Invoke(() => s.Save(a), () => s.Save(b));
        var records = s.LoadOpen("Codex"); Must(records.Length == 2 && records.All(r => r.Messages.Count == 1), "Independent conversations affected each other.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ManualEdit
    /// desc         : 격리된 실제 저장·WPF 소스에서 ManualEdit 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ManualEdit()
    {
        var s = Store(); Seed(s, 40); var r = Load(s); string path = s.GetPath(r);
        string manual = File.ReadAllText(path).Replace("body-39", "edit-39"); File.WriteAllText(path, manual, new UTF8Encoding(false));
        r.Messages.Add(Message(100, "preserved unaccepted request"));
        bool stale = false; try { s.Save(r); } catch (StaleConversationWriteException) { stale = true; }
        Must(stale && File.ReadAllText(path) == manual && r.Messages.Last().Text == "preserved unaccepted request", "Manual edit conflict lost saved or caller input.");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SetFaultInjector
    /// desc         : source와 고정 DLL 양쪽의 같은 내부 실패 주입 속성을 연결하며 reflection 예외의 inner 원문을 그대로 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 실제 DLL32개 검사의 기대를 변경하지 않는 접근 경계.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void SetFaultInjector(MarkdownWorkspaceStore store, Action<string> injector)
    {
        var property = typeof(MarkdownWorkspaceStore).GetProperty("StorageFaultInjector", BindingFlags.Instance | BindingFlags.NonPublic);
        Must(property != null && property.PropertyType == typeof(Action<string>) && property.GetSetMethod(true) != null, "Actual storage fault injector contract is missing.");
        property.SetValue(store, injector, null);
    }
}
