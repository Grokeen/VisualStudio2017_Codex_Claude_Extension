using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Services;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 공통 workspace는 유지하고 대화·협업 저장과 메모리 캐시만 솔루션 범위로 전환한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private sealed class ProjectScopeSession { internal Action Restore; }
        private readonly Dictionary<string, ProjectScopeSession> projectSessions = new Dictionary<string, ProjectScopeSession>(StringComparer.Ordinal);
        private readonly HashSet<Task> projectObservedTasks = new HashSet<Task>();
        private ProjectConversationContext projectContext;
        private ProjectConversationSwitchGate projectSwitch;
        private MarkdownWorkspaceStore projectConversationStore;
        private DispatcherTimer projectContextTimer;
        private Button projectScopeButton;
        private bool projectTransitionApplying;
        private bool projectTransitionFailed;
        private int projectCaptureDepth;
        private long projectCaptureGeneration;

        private MarkdownWorkspaceStore ConversationStorage { get { return projectConversationStore ?? workspaceStore; } }
        partial void ProjectConversationViewsChanging();
        partial void CaptureProjectConversationViewState(ref Action restore);

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AttachProjectConversations
        /// desc         : 실제 DTE 이벤트와 idle 전환을 연결하고 원래 전역 기록은 명시 메뉴로 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void AttachProjectConversations(ProjectConversationContext context)
        {
            Dispatcher.VerifyAccess();
            if (projectContext != null || context == null || !experienceReady) return;
            projectContext = context; projectSwitch = new ProjectConversationSwitchGate(ProjectConversationIdentity.Legacy());
            projectSessions[projectSwitch.Loaded.Key] = CaptureProjectScope();
            projectContext.Changed += ProjectContextChanged;
            InstallProjectScopeMenu();
            projectContextTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            projectContextTimer.Tick += ProjectContextTick; projectContextTimer.Start();
            ProjectContextChanged(this, EventArgs.Empty);
        }

        private void InstallProjectScopeMenu()
        {
            Dispatcher.VerifyAccess();
            var parent = HistoryPanel.Child as DockPanel;
            if (parent == null) return;
            projectScopeButton = new Button { Content = "대화 범위 ▾", Margin = new Thickness(0, 0, 0, 6), HorizontalContentAlignment = HorizontalAlignment.Left };
            var menu = new ContextMenu(); menu.Resources.MergedDictionaries.Add(Resources);
            var current = new MenuItem { Header = "현재 솔루션 대화로 돌아가기" };
            current.Click += (sender, args) => { Dispatcher.VerifyAccess(); projectTransitionFailed = false; projectContext.Refresh(); projectSwitch.Request(projectContext.Current); TryApplyProjectConversationContext(); };
            var legacy = new MenuItem { Header = "이전 전역 기록 보기 · 이 범위에서 전송 안 함" };
            legacy.Click += (sender, args) => { Dispatcher.VerifyAccess(); projectTransitionFailed = false; projectSwitch.Request(ProjectConversationIdentity.Legacy()); TryApplyProjectConversationContext(); };
            menu.Items.Add(current); menu.Items.Add(legacy);
            projectScopeButton.Click += (sender, args) => { Dispatcher.VerifyAccess(); menu.PlacementTarget = projectScopeButton; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true; };
            DockPanel.SetDock(projectScopeButton, Dock.Top); parent.Children.Insert(1, projectScopeButton);
        }

        private void ProjectContextChanged(object sender, EventArgs args)
        {
            Dispatcher.VerifyAccess(); if (disposed || projectContext == null) return;
            projectTransitionFailed = false;
            projectSwitch.Request(projectContext.Current);
            if (projectSwitch.Pending) StatusLabel.Text = "이전 솔루션 작업을 마치거나 중지한 뒤 대화 목록을 전환합니다. 새 요청은 전환 후 전송해 주세요.";
            TryApplyProjectConversationContext();
        }
        private void ProjectContextTick(object sender, EventArgs args)
        { Dispatcher.VerifyAccess(); if (!disposed && projectContext != null) { projectContext.Refresh(); TryApplyProjectConversationContext(); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HasProjectConversationWork
        /// desc         : 실행·큐·실제 Task·모달 입력·요청 캡처가 끝나기 전에는 store를 교체하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool HasProjectConversationWork()
        {
            foreach (var run in roomRuns.Values.Concat(collaboratingRooms.Values)) if (run != null && run.Task != null && !run.Task.IsCompleted) projectObservedTasks.Add(run.Task);
            projectObservedTasks.RemoveWhere(task => task.IsCompleted);
            return projectTransitionApplying || projectCaptureDepth != 0 || ComponentDispatcher.IsThreadModal || busy || drainingRequests || executingRequest != null || queuedRequests.Count > 0 || roomRuns.Count > 0 || collaboratingRooms.Count > 0 || roomQueues.Values.Any(queue => queue != null && queue.Count > 0) || projectObservedTasks.Count > 0 || requestQueueTask != null && !requestQueueTask.IsCompleted;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryApplyProjectConversationContext
        /// desc         : 이전 초안을 실제 저장한 안전 시점에만 새 범위를 복원하고 실패하면 원래 세션으로 복귀한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private void TryApplyProjectConversationContext()
        {
            Dispatcher.VerifyAccess();
            if (projectContext == null || projectSwitch == null || disposed || !experienceReady) return;
            if (projectTransitionFailed) { UpdateProjectScopeCaption(); return; }
            var proposed = projectSwitch.Propose(HasProjectConversationWork());
            if (proposed == null) { UpdateProjectScopeCaption(); return; }
            if (!PersistCurrentConversation()) { projectTransitionFailed = true; UpdateProjectScopeCaption(); return; }
            if (pendingProposal != null && pendingProposal.Source != null && pendingProposal.HasChanges && pendingDocument != null)
                roomProposals[activeRecords[selectedProvider].Id] = new RoomProposal { Proposal = pendingProposal, Document = pendingDocument };
            var previous = CaptureProjectScope(); projectSessions[projectSwitch.Loaded.Key] = previous;
            projectTransitionApplying = true;
            try
            {
                CloseDropdown(); CloseFan();
                ProjectConversationViewsChanging();
                ProjectScopeSession cached;
                if (projectSessions.TryGetValue(proposed.Key, out cached)) cached.Restore();
                else LoadNewProjectScope(proposed);
                ClearProposal(); ShowChat(); RestoreRoomPresentation(); UpdateButtons();
                if (!projectSwitch.Commit(proposed)) { previous.Restore(); ClearProposal(); ShowChat(); RestoreRoomPresentation(); UpdateButtons(); return; }
                StatusLabel.Text = proposed.IsLegacy ? "기존 전역 기록입니다. 현재 솔루션 대화로 돌아간 뒤 새 요청을 전송해 주세요." : proposed.HasSolution ? "현재 솔루션의 대화 목록으로 전환했습니다." : "솔루션 없음 그룹의 대화 목록으로 전환했습니다.";
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is InvalidOperationException || error is NotSupportedException || error is System.Security.SecurityException)
            {
                previous.Restore(); projectTransitionFailed = true;
                if (draftTimer != null) draftTimer.Stop();
                // 실패한 새 범위의 입력이 이전 저장소로 자동 저장되지 않도록 원래 초안을 먼저 복원한다.
                Instruction.Text = ActiveChat.Draft ?? "";
                try { ClearProposal(); ShowChat(); RestoreRoomPresentation(); UpdateButtons(); }
                catch (Exception restoreError) when (restoreError is IOException || restoreError is UnauthorizedAccessException || restoreError is ArgumentException || restoreError is InvalidOperationException || restoreError is NotSupportedException || restoreError is System.Security.SecurityException) { }
                StatusLabel.Text = "솔루션 대화 저장소를 준비하지 못했습니다. 이전 기록과 초안은 보존했습니다. 저장 상태를 확인한 뒤 대화 범위 메뉴에서 다시 전환해 주세요.";
            }
            finally { projectTransitionApplying = false; UpdateProjectScopeCaption(); }
        }

        private void LoadNewProjectScope(ProjectConversationIdentity identity)
        {
            Dispatcher.VerifyAccess();
            projectConversationStore = identity.IsLegacy ? workspaceStore : new MarkdownWorkspaceStore(identity.EnsureStoreRoot(workspaceStore.RootPath));
            collaborationStore = new ConversationCollaborationStore(ConversationStorage.RootPath, () => !Dispatcher.CheckAccess());
            conversationRecords.Clear(); activeRecords.Clear(); chats.Clear(); persistedConversationFingerprints.Clear(); queuedHistory.Clear();
            roomProposals.Clear(); roomChanges.Clear(); roomStatuses.Clear(); roomUsages.Clear(); conversationActivities.Clear(); roomQueues.Clear();
            foreach (string providerName in new[] { "Claude", "Codex" })
            {
                collaborationStore.ConfigureNaming(providerName, Environment.UserName);
                var records = ConversationStorage.LoadOpen(providerName).ToList();
                if (records.Count == 0)
                {
                    var route = collaborationStore.RouteNewConversation(providerName, Environment.UserName, () => ConversationStorage.Create(providerName), record => ConversationStorage.Close(record));
                    records.Add(ResolveAssignedRecord(route.Agent, route.CreatedRecord));
                }
                foreach (var record in records.OrderBy(record => record.CreatedAt)) collaborationStore.EnsureAgent(record);
                conversationRecords[providerName] = records; activeRecords[providerName] = records[0];
            }
        }

        private ProjectScopeSession CaptureProjectScope()
        {
            Action viewRestore = null;
            CaptureProjectConversationViewState(ref viewRestore);
            var store = ConversationStorage; var collaboration = collaborationStore;
            var records = CloneProjectMap(conversationRecords); var active = CloneProjectMap(activeRecords); var cachedChats = CloneProjectMap(chats);
            var fingerprints = CloneProjectMap(persistedConversationFingerprints); var history = CloneProjectMap(queuedHistory);
            var proposals = CloneProjectMap(roomProposals); var changes = CloneProjectMap(roomChanges); var statuses = CloneProjectMap(roomStatuses); var usages = CloneProjectMap(roomUsages); var activities = CloneProjectMap(conversationActivities); var queues = CloneProjectMap(roomQueues);
            return new ProjectScopeSession { Restore = () => {
                projectConversationStore = store; collaborationStore = collaboration;
                RestoreProjectMap(conversationRecords, records); RestoreProjectMap(activeRecords, active); RestoreProjectMap(chats, cachedChats);
                RestoreProjectMap(persistedConversationFingerprints, fingerprints); RestoreProjectMap(queuedHistory, history);
                RestoreProjectMap(roomProposals, proposals); RestoreProjectMap(roomChanges, changes); RestoreProjectMap(roomStatuses, statuses); RestoreProjectMap(roomUsages, usages); RestoreProjectMap(conversationActivities, activities); RestoreProjectMap(roomQueues, queues);
                if (viewRestore != null) viewRestore();
            } };
        }
        private static Dictionary<TKey, TValue> CloneProjectMap<TKey, TValue>(Dictionary<TKey, TValue> source) { return new Dictionary<TKey, TValue>(source, source.Comparer); }
        private static void RestoreProjectMap<TKey, TValue>(Dictionary<TKey, TValue> destination, Dictionary<TKey, TValue> source) { destination.Clear(); foreach (var pair in source) destination.Add(pair.Key, pair.Value); }
        private void UpdateProjectScopeCaption()
        {
            Dispatcher.VerifyAccess();
            if (projectScopeButton == null || projectSwitch == null) return;
            projectScopeButton.Content = projectTransitionFailed ? "대화 전환 실패 ▾" : projectSwitch.Pending ? "대화 전환 대기 ▾" : projectSwitch.Loaded.IsLegacy ? "이전 전역 기록 ▾" : projectSwitch.Loaded.HasSolution ? "솔루션 대화 ▾" : "솔루션 없음 ▾";
            projectScopeButton.ToolTip = projectTransitionFailed ? "저장 상태를 확인한 뒤 메뉴의 현재 솔루션 대화로 돌아가기를 눌러 다시 시도해 주세요." : projectSwitch.Pending ? "이전 솔루션 작업 완료 후 자동 전환합니다. 새 요청은 그 뒤 전송할 수 있습니다." : "현재 솔루션 대화 또는 보존한 이전 전역 기록을 선택합니다.";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EnsureProjectConversationRequestContext
        /// desc         : 실제 선택 솔루션과 활성 대화 범위가 다르면 입력을 소비하기 전에 전송을 거절한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private void EnsureProjectConversationRequestContext()
        {
            Dispatcher.VerifyAccess(); if (projectContext == null) return;
            projectContext.Refresh();
            if (projectTransitionApplying || !projectSwitch.CanSend(projectContext.Current) || projectCaptureDepth > 0 && projectCaptureGeneration != projectContext.Generation)
                throw new InvalidOperationException("현재 솔루션 대화로 전환한 뒤 전송해 주세요. 이전 솔루션 작업이 남아 있으면 완료하거나 중지해야 합니다. 입력은 보존했습니다.");
        }
        private void BeginProjectConversationCapture() { Dispatcher.VerifyAccess(); EnsureProjectConversationRequestContext(); if (projectCaptureDepth == 0 && projectContext != null) projectCaptureGeneration = projectContext.Generation; projectCaptureDepth++; }
        private void EndProjectConversationCapture() { Dispatcher.VerifyAccess(); if (projectCaptureDepth > 0) projectCaptureDepth--; }
        internal void DetachProjectConversations()
        {
            Dispatcher.VerifyAccess();
            if (projectContextTimer != null) { projectContextTimer.Stop(); projectContextTimer.Tick -= ProjectContextTick; projectContextTimer = null; }
            if (projectContext != null) projectContext.Changed -= ProjectContextChanged;
            projectContext = null;
        }
    }
}
