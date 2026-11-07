using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using VS2017.AIAssistant.Chat;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ActivityReadStorageContracts
/// desc         : 실제 CAS 저장 소스와 읽음 메소드 사본을 격리 경로에서 검사한다.
/// author       : CodexOrig
/// create date  : 2026-11-03
/// </summary>
/// ---------------------------------------------------------------------
internal static class ActivityReadStorageContracts
{
    private static int passed;
    private static void Check(string name, bool value) { if (!value) throw new InvalidOperationException("FAIL " + name); passed++; Console.WriteLine("PASS " + name); }
    private static string Digest(string path) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))); }
    private static ConversationActivityItem Item(ConversationRecord record) { return new ConversationActivityItem { Provider = record.Provider, Id = record.Id }; }
    private static ConversationRecord Seed(MarkdownWorkspaceStore store, string provider)
    {
        var record = store.Create(provider);
        record.Title = "fixed-synthetic-private-title"; record.IsTitleAutomatic = false;
        record.Draft = "fixed-synthetic-record-draft"; record.State = ConversationRunState.Completed; record.HasUnread = true;
        record.Messages.Add(new ChatMessage { Role = "user", Text = "fixed-synthetic-body-before-read" });
        store.Save(record); return record;
    }
    public static int Main(string[] arguments)
    {
        try
        {
            string root = Path.GetFullPath(arguments[0]);
            if (Directory.Exists(root)) throw new InvalidOperationException("Fresh private workspace required.");
            Directory.CreateDirectory(root);
            var store = new MarkdownWorkspaceStore(root);
            var target = Seed(store, "Claude"); var peer = Seed(store, "Codex");
            var controller = new ActivityReadControllerHarness(store); controller.Register("Claude", target); controller.Register("Codex", peer);
            object selected = controller.SelectedRoom, approval = controller.Approval; string uiDraft = controller.InputDraft;
            var messages = target.Messages; var message = messages[0]; string draft = target.Draft, text = message.Text;
            string targetPath = store.GetPath(target), peerPath = store.GetPath(peer), before = Digest(targetPath), peerBefore = Digest(peerPath);
            var originalOtherMarkdown = Directory.GetFiles(root, "*.md", SearchOption.AllDirectories).Where(p => p != targetPath).ToDictionary(p => p, Digest, StringComparer.OrdinalIgnoreCase);
            Check("explicit owned read succeeds through actual CAS", controller.Mark(Item(target)) && !target.HasUnread);
            Check("read state is durable after reload", !store.LoadOpen("Claude").Single().HasUnread);
            Check("successful read changes target metadata bytes", Digest(targetPath) != before);
            Check("other room unread and bytes preserved", peer.HasUnread && Digest(peerPath) == peerBefore);
            Check("record message list and object references preserved", Object.ReferenceEquals(messages, target.Messages) && Object.ReferenceEquals(message, target.Messages[0]));
            Check("record draft and message text references preserved", Object.ReferenceEquals(draft, target.Draft) && Object.ReferenceEquals(text, target.Messages[0].Text));
            Check("message and peer Markdown bytes unchanged", originalOtherMarkdown.All(p => File.Exists(p.Key) && Digest(p.Key) == p.Value));
            Check("selection approval and current input unchanged", Object.ReferenceEquals(selected, controller.SelectedRoom) && Object.ReferenceEquals(approval, controller.Approval) && Object.ReferenceEquals(uiDraft, controller.InputDraft));
            string alreadyRead = Digest(targetPath), revision = target.StorageRevision;
            controller.FlagFailure(target);
            Check("already read is a no op", controller.Mark(Item(target)) && Digest(targetPath) == alreadyRead && target.StorageRevision == revision);
            Check("no op read does not clear storage failure flag", controller.Failed(target));

            var stale = Seed(store, "Claude"); var independent = store.LoadOpen("Claude").Single(r => r.Id == stale.Id);
            var staleController = new ActivityReadControllerHarness(store); staleController.Register("Claude", stale); staleController.CacheAsPreviouslyRead(stale);
            independent.Draft = "changed-by-independent-test-writer"; store.Save(independent);
            string committed = Digest(store.GetPath(stale)); var staleMessages = stale.Messages; string staleDraft = stale.Draft;
            Check("stale read is rejected despite cached read fingerprint", !staleController.Mark(Item(stale)));
            Check("stale read restores unread and typed failure", stale.HasUnread && staleController.Failed(stale));
            Check("stale read cannot overwrite external committed bytes", Digest(store.GetPath(stale)) == committed);
            Check("stale read preserves draft and message references", Object.ReferenceEquals(staleDraft, stale.Draft) && Object.ReferenceEquals(staleMessages, stale.Messages));

            var io = Seed(store, "Claude"); var ioController = new ActivityReadControllerHarness(store); ioController.Register("Claude", io);
            string ioPath = store.GetPath(io), ioBefore = Digest(ioPath), oldRevision = io.StorageRevision; var ioMessages = io.Messages; string ioDraft = io.Draft;
            store.StorageFaultInjector = stage => { if (stage == "before-main") throw new IOException("injected private write failure"); };
            Check("commit IO failure returns false", !ioController.Mark(Item(io)));
            store.StorageFaultInjector = null;
            Check("commit IO failure restores unread and typed failure", io.HasUnread && ioController.Failed(io));
            Check("commit IO failure preserves original bytes and revision", Digest(ioPath) == ioBefore && io.StorageRevision == oldRevision);
            Check("commit IO failure preserves body and draft references", Object.ReferenceEquals(io.Messages, ioMessages) && Object.ReferenceEquals(io.Draft, ioDraft));
            Check("retry succeeds and clears typed failure only after save", ioController.Mark(Item(io)) && !io.HasUnread && !ioController.Failed(io));

            var locked = Seed(store, "Claude"); var lockController = new ActivityReadControllerHarness(store); lockController.Register("Claude", locked);
            string lockPath = Path.Combine(Path.GetDirectoryName(store.GetPath(locked)), locked.Id + ".history", ".writer.lock");
            string lockedBefore = Digest(store.GetPath(locked));
            using (var handle = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                Check("bounded exclusive lock contention is rejected", !lockController.Mark(Item(locked)));
            Check("locked read restores state and preserves bytes", locked.HasUnread && lockController.Failed(locked) && Digest(store.GetPath(locked)) == lockedBefore);

            var closing = Seed(store, "Claude"); var closingController = new ActivityReadControllerHarness(store); closingController.Register("Claude", closing);
            string closingPath = store.GetPath(closing), closingBefore = Digest(closingPath);
            closing.PropertyChanged += (sender, args) => { if (args.PropertyName == "HasUnread" && !closing.HasUnread) closing.IsOpen = false; };
            Check("owner closed during acknowledgement is revalidated before CAS", !closingController.Mark(Item(closing)) && closing.HasUnread && Digest(closingPath) == closingBefore);
            var changed = Seed(store, "Claude"); var changedController = new ActivityReadControllerHarness(store); changedController.Register("Claude", changed);
            string changedPath = store.GetPath(changed), changedBefore = Digest(changedPath); var originalItem = Item(changed);
            changed.PropertyChanged += (sender, args) => { if (args.PropertyName == "HasUnread" && !changed.HasUnread) changed.Provider = "Codex"; };
            Check("provider changed during acknowledgement cannot write another owner path", !changedController.Mark(originalItem) && changed.HasUnread && Digest(changedPath) == changedBefore && !File.Exists(store.GetPath(changed)));
            Check("failed read is attributed only to captured provider key", changedController.FailedKey(originalItem.Provider, originalItem.Id) && !changedController.FailedKey(changed.Provider, changed.Id));
            var changedId = Seed(store, "Claude"); var idController = new ActivityReadControllerHarness(store); idController.Register("Claude", changedId);
            string originalIdPath = store.GetPath(changedId), originalIdBytes = Digest(originalIdPath); var originalIdItem = Item(changedId);
            changedId.PropertyChanged += (sender, args) => { if (args.PropertyName == "HasUnread" && !changedId.HasUnread) changedId.Id = "ffffffffffffffffffffffffffffffff"; };
            Check("id changed during acknowledgement cannot write another record", !idController.Mark(originalIdItem) && changedId.HasUnread && Digest(originalIdPath) == originalIdBytes && !File.Exists(store.GetPath(changedId)));
            Check("failed read is attributed only to captured id key", idController.FailedKey(originalIdItem.Provider, originalIdItem.Id) && !idController.FailedKey(changedId.Provider, changedId.Id));
            var conflict = Seed(store, "Claude"); var conflictController = new ActivityReadControllerHarness(store); conflictController.Register("Claude", conflict);
            string conflictPath = store.GetPath(conflict), conflictBefore = Digest(conflictPath);
            conflict.PropertyChanged += (sender, args) => { if (args.PropertyName == "HasUnread" && !conflict.HasUnread) conflictController.Register("Codex", new ConversationRecord { Provider = "Codex", Id = conflict.Id }); };
            Check("new identity collision during acknowledgement blocks CAS", !conflictController.Mark(Item(conflict)) && conflict.HasUnread && Digest(conflictPath) == conflictBefore);

            var duplicate = Seed(store, "Claude"); var duplicateController = new ActivityReadControllerHarness(store); duplicateController.Register("Claude", duplicate);
            var foreignClosed = new ConversationRecord { Provider = "Codex", Id = duplicate.Id, IsOpen = false };
            duplicateController.Register("Codex", foreignClosed); string duplicateBefore = Digest(store.GetPath(duplicate));
            Check("closed foreign provider same id blocks ambiguous action", !duplicateController.Mark(Item(duplicate)) && duplicate.HasUnread);
            Check("ambiguous action leaves original bytes untouched", Digest(store.GetPath(duplicate)) == duplicateBefore);
            var sameProvider = new ActivityReadControllerHarness(store); sameProvider.Register("Claude", duplicate); sameProvider.Register("Claude", duplicate);
            Check("duplicate composite owner rejected", !sameProvider.Mark(Item(duplicate)));
            var wrongProvider = new ActivityReadControllerHarness(store); wrongProvider.Register("Codex", duplicate);
            Check("provider dictionary owner mismatch rejected", !wrongProvider.Mark(Item(duplicate)));
            var absent = new ActivityReadControllerHarness(store);
            Check("unregistered item is rejected", !absent.Mark(Item(duplicate)));
            Check("path like item id rejected before save", !controller.Mark(new ConversationActivityItem { Provider = "Claude", Id = "../outside.md" }));
            Check("closed target rejected", !duplicateController.Mark(Item(foreignClosed)));
            var disposed = new ActivityReadControllerHarness(store); disposed.Register("Claude", duplicate); disposed.SetDisposed();
            Check("disposed controller does not acknowledge read", !disposed.Mark(Item(duplicate)));
            var unready = new ActivityReadControllerHarness(store); unready.Register("Claude", duplicate); unready.SetUnready();
            Check("unready controller does not acknowledge read", !unready.Mark(Item(duplicate)));
            var restart = Seed(store, "Codex"); restart.State = ConversationRunState.Running; store.Save(restart);
            Check("actual persisted running reload is interrupted unread", store.LoadOpen("Codex").Single(r => r.Id == restart.Id).State == ConversationRunState.Interrupted && store.LoadOpen("Codex").Single(r => r.Id == restart.Id).HasUnread);
            var input = new ConversationActivityInput { Provider = "Claude", Id = duplicate.Id, IsOpen = true, HasUnread = false, HasLiveRun = false, HasProposal = false, HasChoices = false, SaveFailed = false, PendingCount = 0, State = ConversationRunState.Idle, LastActivityAt = DateTimeOffset.UtcNow };
            Check("actual record enum fits feed model", ConversationActivityFeed.Build(new[] { input }, ConversationActivityFilter.All).TotalCount == 1);
            var dense = store.Create("Claude"); dense.Title = "dense-cache-private-fixture"; dense.IsTitleAutomatic = false; dense.HasUnread = true;
            dense.Messages.Add(new ChatMessage { Role = "user", Text = new string('<', 1400000) });
            dense.Messages.Add(new ChatMessage { Role = "assistant", Text = new string('<', 500000) }); store.Save(dense);
            var denseController = new ActivityReadControllerHarness(store); denseController.Register("Claude", dense);
            denseController.SeedCachedFingerprint(dense.Id); denseController.FlagFailure(dense);
            var denseMessages = dense.Messages; string denseBody = dense.Messages[0].Text, denseDraft = dense.Draft;
            var denseOtherMarkdown = Directory.GetFiles(root, "*.md", SearchOption.AllDirectories).Where(p => p != store.GetPath(dense)).ToDictionary(p => p, Digest, StringComparer.OrdinalIgnoreCase);
            bool denseMarked = denseController.Mark(Item(dense));
            Check("post commit fingerprint failure cannot roll back durable read", denseMarked && !dense.HasUnread && !store.LoadOpen("Claude").Single(r => r.Id == dense.Id).HasUnread);
            Check("post commit cache failure invalidates stale cache and clears old storage failure", !denseController.HasCachedFingerprint(dense.Id) && !denseController.Failed(dense));
            Check("post commit cache failure preserves body draft references and other Markdown bytes", Object.ReferenceEquals(denseMessages, dense.Messages) && Object.ReferenceEquals(denseBody, dense.Messages[0].Text) && Object.ReferenceEquals(denseDraft, dense.Draft) && denseOtherMarkdown.All(p => File.Exists(p.Key) && Digest(p.Key) == p.Value));
            string denseRevision = dense.StorageRevision, denseBytes = Digest(store.GetPath(dense));
            Check("post commit cache failure leaves next already read operation a no op", denseController.Mark(Item(dense)) && dense.StorageRevision == denseRevision && Digest(store.GetPath(dense)) == denseBytes);
            Console.WriteLine("TOTAL " + passed + " 0"); return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }
}
