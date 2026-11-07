using System;
using System.IO;
using System.Linq;
using VS2017.AIAssistant.Chat;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ActivityNavigationContracts
/// desc         : 실제 전환 메서드 발췌와 CAS 저장으로 소유권·병렬 방·전역 실행 보호를 격리 검사한다.
/// author       : CodexOrig
/// create date  : 2026-11-03
/// </summary>
/// ---------------------------------------------------------------------
internal static class ActivityNavigationContracts
{
    private static int passed;
    private static void Check(string name, bool value) { if (!value) throw new InvalidOperationException("FAIL " + name); passed++; Console.WriteLine("PASS " + name); }
    private static ConversationRecord Seed(MarkdownWorkspaceStore store, string provider, string draft)
    {
        var record = store.Create(provider); record.IsTitleAutomatic = false; record.Title = "synthetic-private-title";
        record.Draft = draft; record.Messages.Add(new ChatMessage { Role = "user", Text = "synthetic-private-original" }); record.HasUnread = true; store.Save(record); return record;
    }
    public static int Main(string[] arguments)
    {
        try
        {
            var store = new MarkdownWorkspaceStore(Path.GetFullPath(arguments[0]));
            var typedInput = new ConversationActivityInput { Provider = "Claude", Id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", IsOpen = true, HasUnread = false, HasLiveRun = false, HasProposal = false, HasChoices = false, SaveFailed = false, PendingCount = 0, State = ConversationRunState.Idle, LastActivityAt = DateTimeOffset.UtcNow };
            if (ConversationActivityFeed.Build(new[] { typedInput }, ConversationActivityFilter.All).TotalCount != 1) throw new InvalidOperationException("Typed model fixture setup failed.");
            if (arguments.Length > 1 && arguments[1] == "persist-list") return PersistListContracts(store);
            if (arguments.Length > 1 && arguments[1] == "persist") return PersistContracts(store);
            ConversationRecord a = Seed(store, "Claude", "draft-a"), b = Seed(store, "Claude", "draft-b"), c = Seed(store, "Codex", "draft-c");
            var records = new[] { a, b, c }; var controller = new ActivityNavigationHarness(store, records, a);
            object execution = new object(), queued1 = new object(), queued2 = new object();
            controller.SetBusy(true); controller.SetExecuting(execution); controller.Enqueue(queued1); controller.Enqueue(queued2);
            controller.Input = "unsubmitted-draft-a"; var proposal = controller.SetProposal();
            Check("exact current room can reopen while global busy and queue exist", controller.Open(a) && controller.Selected(a) && controller.HasReadTarget(a));
            Check("same room reopen preserves input proposal execution and FIFO", controller.Input == "unsubmitted-draft-a" && ReferenceEquals(controller.CurrentProposal, proposal) && ReferenceEquals(controller.Executing, execution) && controller.GlobalQueue.SequenceEqual(new[] { queued1, queued2 }));
            Check("other room cannot steal legacy global execution", !controller.Open(b) && controller.Selected(a) && ReferenceEquals(controller.Executing, execution));
            Check("global execution refusal has fixed safe guidance", controller.Failure == "이 실행이 끝나거나 중지한 뒤 다른 대화로 이동해 주세요.");
            Check("global refusal preserves draft proposal and queue order", controller.Input == "unsubmitted-draft-a" && ReferenceEquals(controller.CurrentProposal, proposal) && controller.GlobalQueue.SequenceEqual(new[] { queued1, queued2 }));
            controller.SetBusy(false); controller.SetExecuting(null);
            Check("legacy queued future requests still block another room", !controller.Open(c) && controller.Selected(a));

            var native = new ActivityNavigationHarness(store, records, a);
            object nativeA = new object(), nativeC = new object(), nativeQueued = new object();
            native.NativeRuns[a.Id] = nativeA; native.NativeRuns[c.Id] = nativeC;
            native.NativeQueues[a.Id] = new System.Collections.Generic.Queue<object>(); native.NativeQueues[a.Id].Enqueue(nativeQueued);
            native.Input = "next-unsubmitted-a"; var nativeProposal = native.SetProposal();
            Check("native background rooms allow same provider navigation", native.Open(b) && native.Selected(b));
            Check("source unsubmitted draft is saved without changing target input", store.LoadOpen("Claude").Single(r => r.Id == a.Id).Draft == "next-unsubmitted-a" && native.Input == "draft-b");
            Check("source review proposal remains owned by its room", native.HasPreservedProposal(a, nativeProposal) && native.CurrentProposal == null);
            Check("same provider round trip restores exact source proposal and draft", native.Open(a) && native.Selected(a) && ReferenceEquals(native.CurrentProposal, nativeProposal) && native.Input == "next-unsubmitted-a");
            native.Input = "cross-provider-next-a";
            Check("native background rooms allow cross provider navigation", native.Open(c) && native.Selected(c) && native.Input == "draft-c");
            Check("cross provider round trip restores source draft and proposal", native.Open(a) && native.Selected(a) && native.Input == "cross-provider-next-a" && ReferenceEquals(native.CurrentProposal, nativeProposal));
            Check("native execution identities and room FIFO remain untouched", ReferenceEquals(native.NativeRuns[a.Id], nativeA) && ReferenceEquals(native.NativeRuns[c.Id], nativeC) && native.NativeQueues[a.Id].Count == 1 && ReferenceEquals(native.NativeQueues[a.Id].Peek(), nativeQueued));
            Check("navigation does not implicitly acknowledge target unread", a.HasUnread && b.HasUnread && c.HasUnread);

            var stale = Seed(store, "Claude", "stale-draft"); var stalePeer = Seed(store, "Codex", "peer-draft");
            var staleHarness = new ActivityNavigationHarness(store, new[] { stale, stalePeer }, stale); var staleProposal = staleHarness.SetProposal();
            var external = store.LoadOpen("Claude").Single(r => r.Id == stale.Id); external.Draft = "external-writer-draft"; store.Save(external);
            byte[] committed = File.ReadAllBytes(store.GetPath(stale)); staleHarness.Input = "local-next-input";
            Check("source stale CAS prevents cross provider navigation", !staleHarness.Open(stalePeer) && staleHarness.Selected(stale));
            Check("source CAS failure preserves input proposal and committed bytes", staleHarness.Input == "local-next-input" && ReferenceEquals(staleHarness.CurrentProposal, staleProposal) && File.ReadAllBytes(store.GetPath(stale)).SequenceEqual(committed));
            Check("source CAS failure keeps typed failure and fixed guidance", staleHarness.HasStorageFailure(stale) && staleHarness.Failure == "대화를 저장하지 못해 이동하지 않았습니다. 저장 상태를 확인한 뒤 다시 시도해 주세요.");
            var safe = new ActivityNavigationHarness(store, records, a); safe.SetDraining(true);
            Check("global draining boundary blocks different room", !safe.Open(b)); safe.SetDraining(false); safe.SetExecuting(execution);
            Check("captured legacy execution boundary blocks different room", !safe.Open(b));
            Check("ownership rejection replaces earlier busy guidance", !safe.OpenItem(new ConversationActivityItem { Provider = "Claude", Id = "../synthetic" }) && safe.Failure == "대화 소유 상태가 변경되어 이동하지 않았습니다. 활동함을 새로 열어 확인해 주세요.");
            safe.SetExecuting(null); safe.AddCollision("Codex", b.Id);
            Check("closed foreign duplicate blocks exact identity navigation", !safe.Open(b) && safe.Selected(a));

            var readTarget = Seed(store, "Codex", "unread-target"); var readSource = Seed(store, "Claude", "read-source");
            var read = new ActivityNavigationHarness(store, new[] { readSource, readTarget }, readSource); var readExternal = store.LoadOpen("Codex").Single(r => r.Id == readTarget.Id);
            readExternal.Draft = "changed-unread-target"; store.Save(readExternal); byte[] readBytes = File.ReadAllBytes(store.GetPath(readTarget));
            Check("opening stale unread target can succeed independently of read CAS", read.Open(readTarget) && read.Selected(readTarget));
            Check("stale target acknowledgement fails without changing selection or unread", !read.MarkSelectedRead() && read.Selected(readTarget) && readTarget.HasUnread && read.HasStorageFailure(readTarget) && File.ReadAllBytes(store.GetPath(readTarget)).SequenceEqual(readBytes));
            Console.WriteLine("TOTAL " + passed + " 0"); return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PersistContracts
    /// desc         : 실제 저장 후 캐시·목록 오류와 진짜 CAS 실패를 결정적 대체 경계로 구분한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int PersistContracts(MarkdownWorkspaceStore store)
    {
        var record = Seed(store, "Claude", "before-cache-fault");
        var harness = new ActivityNavigationHarness(store, new[] { record }, record);
        harness.Input = "committed-cache-fault-draft"; var proposal = harness.SetProposal();
        harness.SeedCache(record); harness.FlagFailure(record); harness.FailFingerprintCall = 2;
        string beforeRevision = record.StorageRevision;
        bool persisted = harness.PersistSelected();
        Check("committed save returns true despite post commit fingerprint failure", persisted);
        Check("fingerprint failure retains committed revision draft and original source", record.StorageRevision != beforeRevision && store.LoadOpen("Claude").Single(r => r.Id == record.Id).Draft == "committed-cache-fault-draft" && record.Messages[0].Text == "synthetic-private-original");
        Check("post commit fingerprint failure invalidates stale cache and storage failure", !harness.HasCache(record) && !harness.HasStorageFailure(record));
        Check("post commit fingerprint failure preserves selected room input and review proposal", harness.Selected(record) && harness.Input == "committed-cache-fault-draft" && ReferenceEquals(harness.CurrentProposal, proposal));
        string cacheFaultRevision = record.StorageRevision; byte[] cacheFaultBytes = File.ReadAllBytes(store.GetPath(record));
        Check("post commit fingerprint failure does not trigger automatic retry", record.StorageRevision == cacheFaultRevision && File.ReadAllBytes(store.GetPath(record)).SequenceEqual(cacheFaultBytes) && harness.FingerprintCalls == 2);
        var externalAfterCacheFailure = store.LoadOpen("Claude").Single(r => r.Id == record.Id);
        externalAfterCacheFailure.Draft = "external-after-cache-fault"; store.Save(externalAfterCacheFailure);
        byte[] externalAfterCacheBytes = File.ReadAllBytes(store.GetPath(record)); harness.Input = "new-user-draft-after-cache-fault";
        Check("cache invalidation cannot skip next real stale CAS check", !harness.PersistSelected() && harness.HasStorageFailure(record));
        Check("stale explicit save after cache failure preserves external bytes", File.ReadAllBytes(store.GetPath(record)).SequenceEqual(externalAfterCacheBytes));
        Check("stale explicit save preserves pending user draft and source proposal", harness.Input == "new-user-draft-after-cache-fault" && record.Draft == "new-user-draft-after-cache-fault" && ReferenceEquals(harness.CurrentProposal, proposal));

        var listRecord = Seed(store, "Codex", "before-list-fault"); var listHarness = new ActivityNavigationHarness(store, new[] { listRecord }, listRecord);
        listHarness.Input = "committed-list-fault-draft"; var listProposal = listHarness.SetProposal(); listHarness.FailListRefresh = true;
        Check("committed save returns true despite list refresh failure", listHarness.PersistSelected());
        Check("post commit list failure retains valid cache and no storage failure", listHarness.HasCache(listRecord) && !listHarness.HasStorageFailure(listRecord));
        string listRevision = listRecord.StorageRevision; byte[] listBytes = File.ReadAllBytes(store.GetPath(listRecord));
        Check("same explicit save after list failure does not commit again", listHarness.PersistSelected() && listRecord.StorageRevision == listRevision && File.ReadAllBytes(store.GetPath(listRecord)).SequenceEqual(listBytes));
        Check("post commit list failure preserves source draft and pending proposal", listHarness.Selected(listRecord) && listHarness.Input == "committed-list-fault-draft" && ReferenceEquals(listHarness.CurrentProposal, listProposal) && store.LoadOpen("Codex").Single(r => r.Id == listRecord.Id).Draft == "committed-list-fault-draft");

        var stale = Seed(store, "Claude", "stale-source"); var staleHarness = new ActivityNavigationHarness(store, new[] { stale }, stale);
        var staleExternal = store.LoadOpen("Claude").Single(r => r.Id == stale.Id); staleExternal.Draft = "independent-writer"; store.Save(staleExternal);
        byte[] staleBytes = File.ReadAllBytes(store.GetPath(stale)); string staleRevision = stale.StorageRevision;
        Check("actual stale CAS remains false and typed storage failure", !staleHarness.PersistSelected() && staleHarness.HasStorageFailure(stale));
        Check("stale CAS cannot replace external committed bytes", stale.StorageRevision == staleRevision && File.ReadAllBytes(store.GetPath(stale)).SequenceEqual(staleBytes));
        var io = Seed(store, "Claude", "io-source"); var ioHarness = new ActivityNavigationHarness(store, new[] { io }, io); ioHarness.Input = "io-pending-draft";
        byte[] ioBytes = File.ReadAllBytes(store.GetPath(io)); string ioRevision = io.StorageRevision;
        store.StorageFaultInjector = stage => { if (stage == "before-main") throw new IOException("private-store-fault"); };
        Check("actual store failure remains false and typed storage failure", !ioHarness.PersistSelected() && ioHarness.HasStorageFailure(io)); store.StorageFaultInjector = null;
        Check("actual store failure preserves original bytes and revision", io.StorageRevision == ioRevision && File.ReadAllBytes(store.GetPath(io)).SequenceEqual(ioBytes));
        Check("explicit retry of actual store failure succeeds", ioHarness.PersistSelected() && !ioHarness.HasStorageFailure(io) && store.LoadOpen("Claude").Single(r => r.Id == io.Id).Draft == "io-pending-draft");
        Console.WriteLine("TOTAL " + passed + " 0"); return 0;
    }

    private static int PersistListContracts(MarkdownWorkspaceStore store)
    {
        var record = Seed(store, "Claude", "before-list-only"); var harness = new ActivityNavigationHarness(store, new[] { record }, record);
        harness.Input = "committed-list-only"; harness.FailListRefresh = true;
        Check("committed save returns true despite isolated list failure", harness.PersistSelected());
        Check("list failure leaves committed content with valid cache and no SaveFailed", store.LoadOpen("Claude").Single().Draft == "committed-list-only" && harness.HasCache(record) && !harness.HasStorageFailure(record));
        string revision = record.StorageRevision; byte[] bytes = File.ReadAllBytes(store.GetPath(record));
        Check("explicit save after isolated list failure has no duplicate commit", harness.PersistSelected() && revision == record.StorageRevision && bytes.SequenceEqual(File.ReadAllBytes(store.GetPath(record))));
        Console.WriteLine("TOTAL " + passed + " 0"); return 0;
    }
}
