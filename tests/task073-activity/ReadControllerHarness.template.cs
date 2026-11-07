using System;
using System.Collections.Generic;
using System.Linq;
using VS2017.AIAssistant.Chat;

// This container supplies no WPF or product UI. Mark/index/key/failure/fingerprint members
// are exact current-source substrings inserted by the driver. Dispatcher and badge are stubs.
internal sealed class ActivityReadControllerHarness
{
    private sealed class ThreadBoundary
    {
        private readonly int thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        internal void VerifyAccess() { if (thread != System.Threading.Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Wrong fixture thread."); }
    }
    private readonly ThreadBoundary Dispatcher = new ThreadBoundary();
    private readonly Dictionary<string, List<ConversationRecord>> conversationRecords = new Dictionary<string, List<ConversationRecord>>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> persistedConversationFingerprints = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> activitySaveFailures = new HashSet<string>(StringComparer.Ordinal);
    private readonly MarkdownWorkspaceStore workspaceStore;
    private MarkdownWorkspaceStore ConversationStorage { get { return workspaceStore; } }
    private ConversationRecord activityReadCasTarget = null;
    private bool disposed = false;
    private bool experienceReady = true;
    internal int BadgeRefreshes;
    internal object SelectedRoom = new object();
    internal object Approval = new object();
    internal string InputDraft = "current-unrelated-ui-draft";
    internal ActivityReadControllerHarness(MarkdownWorkspaceStore store) { workspaceStore = store; }
    internal void Register(string provider, ConversationRecord record)
    {
        List<ConversationRecord> records;
        if (!conversationRecords.TryGetValue(provider, out records)) conversationRecords[provider] = records = new List<ConversationRecord>();
        records.Add(record);
    }
    internal bool Mark(ConversationActivityItem item) { return TryMarkActivityConversationRead(item); }
    internal bool Failed(ConversationRecord record) { return activitySaveFailures.Contains(ActivityKey(record)); }
    internal bool FailedKey(string provider, string id) { return activitySaveFailures.Contains(provider + "\0" + id); }
    internal bool HasCachedFingerprint(string id) { return persistedConversationFingerprints.ContainsKey(id); }
    internal void SeedCachedFingerprint(string id) { persistedConversationFingerprints[id] = "stale-private-cache"; }
    internal void FlagFailure(ConversationRecord record) { SetActivitySaveFailure(record, true); }
    internal void CacheAsPreviouslyRead(ConversationRecord record)
    {
        bool unread = record.HasUnread; record.HasUnread = false;
        persistedConversationFingerprints[record.Id] = ConversationFingerprint(record); record.HasUnread = unread;
    }
    internal void SetDisposed() { disposed = true; }
    internal void SetUnready() { experienceReady = false; }
    private void RefreshActivityFeedBadge() { BadgeRefreshes++; }

/* EXACT_MEMBERS */
}
