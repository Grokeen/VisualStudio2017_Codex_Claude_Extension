using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VS2017.AIAssistant.Chat;

// Product member bodies are inserted without rewriting. These surrounding controls,
// dispatcher, ChatSession and presentation callbacks are private non-WPF substitutes.
internal sealed class ActivityNavigationHarness
{
    private sealed class ThreadBoundary
    {
        private readonly int thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        internal void VerifyAccess() { if (thread != System.Threading.Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Wrong fixture thread."); }
    }
    private sealed class SelectionChangedEventArgs : EventArgs { internal object OriginalSource; }
    private sealed class TabItem { internal object Tag; }
    private sealed class SelectionControl
    {
        internal readonly List<object> Items = new List<object>();
        internal Action<SelectionChangedEventArgs> Changed;
        private object selected;
        internal object SelectedItem { get { return selected; } set { if (ReferenceEquals(value, selected)) return; selected = value; if (Changed != null) Changed(new SelectionChangedEventArgs { OriginalSource = this }); } }
    }
    private sealed class TextControl { internal string Text = ""; }
    private sealed class ChatSession { internal string Draft = ""; internal readonly List<ChatMessage> Messages = new List<ChatMessage>(); }
    internal sealed class Proposal { internal object Source = new object(); internal bool HasChanges = true; }
    private sealed class RoomProposal { internal Proposal Proposal; internal object Document; }
    private readonly ThreadBoundary Dispatcher = new ThreadBoundary();
    private readonly Dictionary<string, List<ConversationRecord>> conversationRecords = new Dictionary<string, List<ConversationRecord>>(StringComparer.Ordinal);
    private readonly Dictionary<string, ConversationRecord> activeRecords = new Dictionary<string, ConversationRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, ChatSession> chats = new Dictionary<string, ChatSession>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> persistedConversationFingerprints = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> activitySaveFailures = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> roomStatuses = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, RoomProposal> roomProposals = new Dictionary<string, RoomProposal>(StringComparer.Ordinal);
    internal readonly Dictionary<string, object> NativeRuns = new Dictionary<string, object>();
    internal readonly Dictionary<string, Queue<object>> NativeQueues = new Dictionary<string, Queue<object>>();
    private readonly Dictionary<string, string> executablePaths = new Dictionary<string, string>();
    private readonly Queue<object> queuedRequests = new Queue<object>();
    private readonly SelectionControl ProviderChoice = new SelectionControl();
    private readonly SelectionControl ConversationList = new SelectionControl();
    private readonly TextControl ExecutablePath = new TextControl();
    private readonly TextControl Instruction = new TextControl();
    private readonly TextControl StatusLabel = new TextControl();
    private readonly object ReviewApproval = new object();
    private readonly MarkdownWorkspaceStore workspaceStore;
    private MarkdownWorkspaceStore ConversationStorage { get { return workspaceStore; } }
    private ConversationRecord activityNavigationTarget;
    private ConversationRecord activityReadTarget;
    private ConversationRecord activityReadCasTarget;
    private bool deferActivityRead;
    private string activityNavigationFailureMessage = "Private fixture initial state";
    private bool disposed = false, experienceReady = true, switchingConversation = false;
    private bool busy, drainingRequests;
    private object executingRequest;
    private string selectedProvider;
    private Proposal pendingProposal;
    private object pendingDocument;
    internal int PresentationRestores;
    internal int FingerprintCalls;
    internal int FailFingerprintCall;
    internal bool FailListRefresh;
    internal ActivityNavigationHarness(MarkdownWorkspaceStore store, IEnumerable<ConversationRecord> records, ConversationRecord selected)
    {
        workspaceStore = store;
        foreach (var record in records)
        {
            List<ConversationRecord> list;
            if (!conversationRecords.TryGetValue(record.Provider, out list)) conversationRecords[record.Provider] = list = new List<ConversationRecord>();
            list.Add(record);
            var chat = new ChatSession { Draft = record.Draft }; chat.Messages.AddRange(record.Messages); chats.Add(record.Id, chat);
            if (!activeRecords.ContainsKey(record.Provider)) activeRecords[record.Provider] = record;
        }
        selectedProvider = selected.Provider; activeRecords[selected.Provider] = selected;
        ProviderChoice.Items.Add(new TabItem { Tag = "Claude" }); ProviderChoice.Items.Add(new TabItem { Tag = "Codex" });
        ProviderChoice.SelectedItem = ProviderChoice.Items.Cast<TabItem>().First(t => Convert.ToString(t.Tag) == selectedProvider);
        ConversationList.SelectedItem = selected;
        ProviderChoice.Changed = args => ProviderChanged(ProviderChoice, args);
        ConversationList.Changed = args => ConversationSelected(ConversationList, args);
        Instruction.Text = ActiveChat.Draft;
    }
    private ChatSession ActiveChat { get { return chats[activeRecords[selectedProvider].Id]; } }
    internal bool Open(ConversationRecord record) { return TryOpenActivityConversation(new ConversationActivityItem { Provider = record.Provider, Id = record.Id }); }
    internal bool OpenItem(ConversationActivityItem item) { return TryOpenActivityConversation(item); }
    internal bool Selected(ConversationRecord record) { return selectedProvider == record.Provider && ReferenceEquals(activeRecords[selectedProvider], record); }
    internal string Failure { get { return GetActivityNavigationFailureMessage(); } }
    internal string Input { get { return Instruction.Text; } set { Instruction.Text = value; } }
    internal void SetBusy(bool value) { busy = value; }
    internal void SetDraining(bool value) { drainingRequests = value; }
    internal void SetExecuting(object value) { executingRequest = value; }
    internal object Executing { get { return executingRequest; } }
    internal void Enqueue(object request) { queuedRequests.Enqueue(request); }
    internal object[] GlobalQueue { get { return queuedRequests.ToArray(); } }
    internal Proposal SetProposal() { pendingDocument = new object(); return pendingProposal = new Proposal(); }
    internal Proposal CurrentProposal { get { return pendingProposal; } }
    internal bool HasPreservedProposal(ConversationRecord record, Proposal proposal) { RoomProposal value; return roomProposals.TryGetValue(record.Id, out value) && ReferenceEquals(value.Proposal, proposal); }
    internal bool MarkSelectedRead() { return TryMarkActivityConversationRead(new ConversationActivityItem { Provider = selectedProvider, Id = activeRecords[selectedProvider].Id }); }
    internal bool HasStorageFailure(ConversationRecord record) { return activitySaveFailures.Contains(ActivityKey(record)); }
    internal bool PersistSelected() { return PersistCurrentConversation(); }
    internal bool HasCache(ConversationRecord record) { return persistedConversationFingerprints.ContainsKey(record.Id); }
    internal void SeedCache(ConversationRecord record) { persistedConversationFingerprints[record.Id] = "stale-private-cache"; }
    internal void FlagFailure(ConversationRecord record) { SetActivitySaveFailure(record, true); }
    internal bool HasReadTarget(ConversationRecord record) { return ReferenceEquals(activityReadTarget, record) && ReferenceEquals(activityReadCasTarget, record); }
    internal void AddCollision(string provider, string id) { conversationRecords[provider].Add(new ConversationRecord { Provider = provider, Id = id, IsOpen = false }); }
    private void RefreshActivityFeedBadge() { }
    private void SaveDraft() { ActiveChat.Draft = Instruction.Text; }
    private void RuntimeProviderChanged() { }
    private void MarkCurrentConversationRead() { throw new InvalidOperationException("Activity navigation must defer implicit read."); }
    private bool IsRoomShown(ConversationRecord record) { return Selected(record); }
    private void UpdateButtons() { }
    private void RefreshConversationList()
    {
        if (FailListRefresh) { FailListRefresh = false; throw new IOException("private-list-fault"); }
        switchingConversation = true;
        try { ConversationList.SelectedItem = activeRecords[selectedProvider]; }
        finally { switchingConversation = false; }
    }
    private void ShowChat() { Instruction.Text = ActiveChat.Draft; RefreshConversationList(); }
    private void ClearProposal() { pendingProposal = null; pendingDocument = null; }
    private void RestoreRoomPresentation()
    {
        PresentationRestores++;
        RoomProposal proposal;
        if (roomProposals.TryGetValue(activeRecords[selectedProvider].Id, out proposal)) { pendingProposal = proposal.Proposal; pendingDocument = proposal.Document; }
    }

    private string ConversationFingerprint(ConversationRecord record)
    {
        FingerprintCalls++;
        if (FingerprintCalls == FailFingerprintCall) throw new IOException("private-fingerprint-fault");
        return ExactConversationFingerprint(record);
    }

/* EXACT_MEMBERS */
}
