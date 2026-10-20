using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 실제 CLI 요청의 실행·취소·결과를 대화방별로 소유해 동시 작업을 허용한다.
    /// author       : CodexCode
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RoomRun
        /// desc         : 한 대화방의 실행·취소·사용량·공개 진행 상태를 소유한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class RoomRun
        {
            internal QueuedUiRequest Request;
            internal CancellationTokenSource Cancellation = new CancellationTokenSource();
            internal Task Task;
            internal int ProgressCount;
            internal AgentFileChange[] Changes = new AgentFileChange[0];
            internal ProviderUsageSnapshot Usage;
            internal ProgressNotice Notice = new ProgressNotice();
            internal ChatMessage NoticeRow;
            internal readonly HashSet<string> ProgressReplies = new HashSet<string>(StringComparer.Ordinal);
            internal readonly object UsageGate = new object();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RoomProposal
        /// desc         : 대화방별 미승인 수정안과 적용 대상 문서를 함께 보관한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class RoomProposal
        {
            internal EditProposal Proposal;
            internal IEditorDocument Document;
        }

        private readonly Dictionary<string, RoomRun> roomRuns = new Dictionary<string, RoomRun>();
        private readonly Dictionary<string, Queue<QueuedUiRequest>> roomQueues = new Dictionary<string, Queue<QueuedUiRequest>>();
        private readonly Dictionary<string, RoomProposal> roomProposals = new Dictionary<string, RoomProposal>();
        private readonly Dictionary<string, AgentFileChange[]> roomChanges = new Dictionary<string, AgentFileChange[]>();
        private readonly Dictionary<string, string> roomStatuses = new Dictionary<string, string>();
        private readonly Dictionary<string, ProviderUsageSnapshot> roomUsages = new Dictionary<string, ProviderUsageSnapshot>();
        private readonly Dictionary<string, RoomRun> collaboratingRooms = new Dictionary<string, RoomRun>(StringComparer.Ordinal);
        private IExternalEditingScope sharedExternalEditing;
        private int sharedExternalEditingUsers;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsRoomShown
        /// desc         : 백그라운드 결과를 현재 선택 방 화면에만 표시할지 결정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool IsRoomShown(ConversationRecord record)
        { return record != null && selectedProvider == record.Provider && activeRecords.ContainsKey(selectedProvider) && ReferenceEquals(activeRecords[selectedProvider], record); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsCurrentRoomRunning
        /// desc         : 현재 방의 자체 실행 또는 협업 실행 여부를 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool IsCurrentRoomRunning
        { get { return experienceReady && activeRecords.ContainsKey(selectedProvider) && (roomRuns.ContainsKey(activeRecords[selectedProvider].Id) || collaboratingRooms.ContainsKey(activeRecords[selectedProvider].Id)); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : QueueConcurrentRoomRequest
        /// desc         : 다른 대화방은 즉시 병렬 실행하고 같은 대화방의 후속 요청만 순서대로 보관한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void QueueConcurrentRoomRequest(QueuedUiRequest request)
        {
            Dispatcher.VerifyAccess();
            string id = request.Record.Id;
            if (roomRuns.ContainsKey(id) || roomProposals.ContainsKey(id) || collaboratingRooms.ContainsKey(id))
            {
                Queue<QueuedUiRequest> queue;
                if (!roomQueues.TryGetValue(id, out queue)) roomQueues[id] = queue = new Queue<QueuedUiRequest>();
                if (queue.Count >= MaximumQueuedRequests) throw new InvalidOperationException("이 대화방의 대기 요청은 최대 8개입니다.");
                queue.Enqueue(request);
                roomStatuses[id] = "이 대화방의 대기 요청 " + queue.Count + "개";
                StatusLabel.Text = roomStatuses[id];
            }
            else StartRoomRequest(request);
            UpdateButtons();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StartRoomRequest
        /// desc         : 실행 조건과 기록 저장을 확인한 뒤 방별 AI 작업을 시작한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void StartRoomRequest(QueuedUiRequest request)
        {
            Dispatcher.VerifyAccess();
            RequireRoomRequestOwnership(request);
            var run = new RoomRun { Request = request };
            roomRuns.Add(request.Record.Id, run);
            request.Record.State = ConversationRunState.Running;
            roomStatuses[request.Record.Id] = request.Provider + " 작업 중…";
            if (IsRoomShown(request.Record)) StatusLabel.Text = roomStatuses[request.Record.Id];
            if (!PersistConversation(request.Record, request.Chat))
            {
                roomRuns.Remove(request.Record.Id); run.Cancellation.Dispose();
                request.Record.State = ConversationRunState.Failed;
                roomStatuses[request.Record.Id] = "대화 기록을 저장하지 못해 실행하지 않았습니다.";
                request.Chat.Add("system", roomStatuses[request.Record.Id]);
                throw new IOException(roomStatuses[request.Record.Id]);
            }
            run.Task = RunRoomRequestAsync(run);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequireRoomRequestOwnership
        /// desc         : 대화 이동은 허용하면서 실행 직전 권한·프로젝트·참여자 드리프트를 막는다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RequireRoomRequestOwnership(QueuedUiRequest request)
        {
            Dispatcher.VerifyAccess();
            ChatSession ownerChat;
            if (disposed || !request.Record.IsOpen || request.Record.Provider != request.Provider ||
                !chats.TryGetValue(request.Record.Id, out ownerChat) || !ReferenceEquals(ownerChat, request.Chat) ||
                request.FullAccess != AgentFullAccessEnabled || !ReferenceEquals(request.EditProvider, provider) ||
                !request.CollaboratorIds.SequenceEqual(request.Record.CollaboratorIds))
                throw new InvalidOperationException("대기 요청의 대화·권한·참여자가 변경되었습니다. 다시 전송하세요.");
            var navigation = documents as ISourceNavigationService;
            string directory = navigation == null ? null : navigation.GetProjectDirectory();
            if (!String.Equals(directory, request.Directory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("대기 요청의 프로젝트가 변경되었습니다. 다시 전송하세요.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunRoomRequestAsync
        /// desc         : 요청 스냅샷과 별도 취소 토큰으로 AI를 실행하고 소유 대화에만 결과를 반영한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD109", Justification = "WPF Dispatcher에서 시작하고 await가 SynchronizationContext를 유지하며 각 검사는 그 계약을 확인한다.")]
        private async Task RunRoomRequestAsync(RoomRun run)
        {
            Dispatcher.VerifyAccess();
            var request = run.Request;
            var record = request.Record;
            var chat = request.Chat;
            bool succeeded = false;
            bool ownsExternalEditing = false;
            try
            {
                RequireRoomRequestOwnership(request);
                if (request.FullAccess) RequireRoomSourceUnchanged(request);
                var context = BuildQueuedExecutionContext(request);
                if (request.CollaboratorIds.Length > 0)
                    context = await RunRoomCollaboratorsAsync(run, context);
                else context = new ChatRequestContext(context.Instructions, context.Model, context.History, context.Attachments,
                    context.ReasoningEffort, context.Speed, collaborationStore.BuildPromptContext(record.Id, record.Provider));
                Dispatcher.VerifyAccess();
                if (request.FullAccess)
                {
                    RequireRoomRequestOwnership(request);
                    RequireRoomSourceUnchanged(request);
                    string editorContext = request.Source == null ? "선택한 편집 파일 없음. 현재 프로젝트 폴더의 사용자 요청을 수행하세요." :
                        "현재 편집 파일: " + request.Source.FilePath + (request.Source.HasSelection ? "\n선택한 코드(" + request.Source.Selection.StartLine + "~" + request.Source.Selection.EndLine + "행):\n" + request.Source.Selection.Text : "");
                    string agentContext = editorContext + (String.IsNullOrWhiteSpace(context.AgentContext) ? "" : "\n" + context.AgentContext);
                    context = new ChatRequestContext(context.Instructions, context.Model, context.History, context.Attachments, context.ReasoningEffort, context.Speed,
                        agentContext.Length > 8000 ? agentContext.Substring(0, 8000) : agentContext);
                    ownsExternalEditing = AcquireRoomExternalEditing();
                    AgentExecutionResult result = request.Provider == "Codex"
                        ? await new CodexRuntimeRunner().RunAgentAsync(request.Executable, request.Directory, request.Instruction, context, usage => QueueRoomUsage(run, usage), files => Volatile.Write(ref run.Changes, files.ToArray()), run.Cancellation.Token, progress => QueueRoomProgress(run, progress))
                        : await new ClaudeRuntimeRunner().RunAgentAsync(request.Executable, request.Directory, request.Instruction, context, usage => QueueRoomUsage(run, usage), files => Volatile.Write(ref run.Changes, files.ToArray()), run.Cancellation.Token, progress => QueueRoomProgress(run, progress));
                    run.Cancellation.Token.ThrowIfCancellationRequested();
                    PresentRoomUsage(run, result.Usage);
                    if (!run.ProgressReplies.Contains(result.Text))
                        chat.Add("assistant", result.Text);
                    roomChanges[record.Id] = result.Changes;
                    if (IsRoomShown(record)) PresentRuntimeChanges(result.Changes, "전체 액세스 · 실행 중 확인된 변경");
                    roomStatuses[record.Id] = "전체 액세스 실행 완료 · 실제 결과와 파일 변경을 확인하세요.";
                }
                else
                {
                    request.Source.RequireUnchanged(request.Document.Capture());
                    var edit = request.EditProvider is RuntimeEditProvider ? new RuntimeEditProvider() : request.EditProvider;
                    var runtime = edit as RuntimeEditProvider;
                    if (runtime != null) runtime.ProgressUpdated += progress => QueueRoomProgress(run, progress);
                    var contextProvider = edit as IAiContextEditProvider;
                    EditProposal proposal = contextProvider == null
                        ? await edit.GenerateAsync(request.Provider, request.Executable, request.Source, request.Instruction, run.Cancellation.Token)
                        : await contextProvider.GenerateWithContextAsync(request.Provider, request.Executable, request.Source, request.Instruction, context, run.Cancellation.Token);
                    run.Cancellation.Token.ThrowIfCancellationRequested();
                    request.Source.RequireUnchanged(request.Document.Capture());
                    request.Source.RequireUnchanged(proposal.Source);
                    chat.Add("assistant", proposal.Summary);
                    if (proposal.HasChanges)
                    {
                        roomProposals[record.Id] = new RoomProposal { Proposal = proposal, Document = request.Document };
                        if (IsRoomShown(record)) ShowRoomProposal(roomProposals[record.Id]);
                    }
                    roomStatuses[record.Id] = proposal.Summary + (proposal.HasChanges ? "\n변경 비교 후 적용을 승인하세요." : "\n적용할 변경이 없습니다.");
                }
                record.State = ConversationRunState.Completed;
                succeeded = true;
            }
            catch (OperationCanceledException)
            {
                record.State = ConversationRunState.Stopped;
                roomStatuses[record.Id] = "요청을 중지했습니다. 이미 수행된 파일 변경은 확인하세요.";
                chat.Add("system", roomStatuses[record.Id]);
            }
            catch (Exception error)
            {
                record.State = ConversationRunState.Failed;
                roomStatuses[record.Id] = "요청 실패: " + error.Message;
                chat.Add("system", roomStatuses[record.Id]);
            }
            finally
            {
                Dispatcher.VerifyAccess();
                if (ownsExternalEditing) { try { ReleaseRoomExternalEditing(chat); } catch (Exception) { chat.Add("system", "편집기 동기화 상태를 확인하세요."); } }
                run.Notice.Complete(succeeded ? "completed" : record.State == ConversationRunState.Stopped ? "stopped" : "failed");
                if (run.NoticeRow != null) run.NoticeRow.Text = run.Notice.MarkdownText;
                record.HasUnread = !IsRoomShown(record) || !IsConversationInView();
                foreach (var message in chat.Messages.Where(item => !request.HistoryState.Order.ContainsKey(item)))
                    request.HistoryState.Order[message] = request.Sequence + 1;
                PruneQueueHistory(request);
                roomRuns.Remove(record.Id);
                run.Cancellation.Dispose();
                bool saved = PersistConversation(record, chat);
                if (!saved)
                {
                    roomStatuses[record.Id] = "대화 기록 저장 실패 · 현재 답변은 화면에 남아 있습니다. 다시 저장한 뒤 진행하세요.";
                    CancelRoomQueue(record.Id, "대화 기록을 저장하지 못했습니다.");
                }
                if (!disposed && saved && succeeded && GetCompletionNotificationsEnabled() && IsLoaded)
                {
                    progressNotice = run.Notice;
                    completionRequestProvider = request.Provider;
                    completionResultKind = roomProposals.ContainsKey(record.Id) ? "proposal" : "answer";
                    ShowCompletionNotification();
                }
                if (!disposed && IsRoomShown(record)) { StatusLabel.Text = roomStatuses[record.Id]; UpdateButtons(); FollowConversation(false); }
                if (!disposed && saved && succeeded && !roomProposals.ContainsKey(record.Id)) StartNextRoomRequest(record.Id);
                else if (!succeeded) CancelRoomQueue(record.Id, "앞선 요청이 완료되지 않아 실행하지 않았습니다.");
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequireRoomSourceUnchanged
        /// desc         : 다른 방 전환과 독립적으로 캡처한 문서의 내용·디스크 상태를 실행 전에 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexCode : IDE가 임시로 보호한 읽기 전용 상태만 동시 실행에서 허용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RequireRoomSourceUnchanged(QueuedUiRequest request)
        {
            Dispatcher.VerifyAccess();
            var fullAccess = documents as IFullAccessDocumentService;
            if (fullAccess != null) fullAccess.CaptureFullAccessContext();
            if (request.Source == null) return;
            request.Source.RequireValidSelection();
            if (request.Document == null) throw new InvalidOperationException("선택한 편집 문서를 다시 확인할 수 없습니다.");
            var current = request.Document.Capture();
            var protection = sharedExternalEditing as IExternalEditingScopeState;
            bool temporarilyProtected = current != null && current.IsReadOnly && protection != null &&
                protection.IsTemporarilyReadOnly(current.FilePath);
            if (current == null || !String.Equals(current.FilePath, request.Source.FilePath, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(current.Content, request.Source.Content, StringComparison.Ordinal) ||
                !String.Equals(current.DiskVersion, request.Source.DiskVersion, StringComparison.Ordinal) ||
                current.HasUnsavedChanges || (current.IsReadOnly && !temporarilyProtected))
                throw new InvalidOperationException("선택한 문서가 변경되었습니다. 내용을 확인한 뒤 다시 전송하세요.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AcquireRoomExternalEditing
        /// desc         : 동시 전체 액세스 요청이 IDE 보호 상태 하나를 함께 사용하게 한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool AcquireRoomExternalEditing()
        {
            Dispatcher.VerifyAccess();
            var synchronized = documents as IExternalEditingDocumentService;
            if (synchronized == null) return false;
            if (sharedExternalEditing == null)
                sharedExternalEditing = synchronized.BeginExternalEditing(() =>
                {
                    foreach (var active in roomRuns.Values.Where(value => value.Request.FullAccess).ToArray())
                        active.Cancellation.Cancel();
                });
            sharedExternalEditingUsers++;
            return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReleaseRoomExternalEditing
        /// desc         : 마지막 전체 액세스 요청이 끝날 때만 IDE 보호 상태를 복원한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ReleaseRoomExternalEditing(ChatSession chat)
        {
            Dispatcher.VerifyAccess();
            if (--sharedExternalEditingUsers != 0) return;
            var editing = sharedExternalEditing;
            sharedExternalEditing = null;
            if (editing == null) return;
            try { editing.Dispose(); }
            finally { if (!String.IsNullOrWhiteSpace(editing.Notice)) chat.Add("system", editing.Notice); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunRoomCollaboratorsAsync
        /// desc         : 협업 AI의 메시지와 상태를 해당 방에 기록하며 주 요청과 다른 방의 실행을 분리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD109", Justification = "WPF Dispatcher에서 시작하고 await가 SynchronizationContext를 유지하며 각 검사는 그 계약을 확인한다.")]
        private async Task<ChatRequestContext> RunRoomCollaboratorsAsync(RoomRun run, ChatRequestContext context)
        {
            Dispatcher.VerifyAccess();
            var request = run.Request;
            var source = request.Source;
            if (source == null) throw new InvalidOperationException("협업 검토에 사용할 편집 파일을 선택하세요.");
            var own = collaborationStore.EnsureAgent(request.Record);
            string identity = collaborationStore.BuildPromptContext(request.Record.Id, request.Record.Provider);
            var replies = new List<string>();
            foreach (string id in request.CollaboratorIds)
            {
                run.Cancellation.Token.ThrowIfCancellationRequested();
                var peers = conversationRecords.Values.SelectMany(records => records).Where(record => record.Id == id && record.IsOpen).ToArray();
                if (peers.Length != 1) throw new InvalidOperationException("협업 대화 주소가 없거나 중복됩니다. AI 설정에서 참여자를 확인하세요.");
                var peer = peers[0];
                if (roomRuns.ContainsKey(id) || collaboratingRooms.ContainsKey(id)) throw new InvalidOperationException("협업 AI 대화방이 다른 작업 중입니다. 완료 후 다시 요청하세요.");
                collaboratingRooms.Add(id, run);
                try
                {
                    var agent = collaborationStore.EnsureAgent(peer);
                    if (agent.Team != own.Team) throw new InvalidOperationException("협업 참여자의 팀이 변경되었습니다. 참여자를 다시 선택하세요.");
                    string handoffText = request.Instruction.Length <= 5000 ? request.Instruction : request.Instruction.Substring(0, 5000) + "\n[공유 요청 일부 생략]";
                    var handoff = collaborationStore.Send(request.Record.Id, request.Record.Provider, peer.Id, peer.Provider, handoffText);
                    ChatSession peerChat;
                    if (!chats.TryGetValue(id, out peerChat))
                    {
                        peerChat = new ChatSession { Draft = peer.Draft };
                        foreach (var message in peer.Messages) peerChat.Messages.Add(message);
                        chats[id] = peerChat;
                    }
                    try
                    {
                        collaborationStore.Receive(handoff.Id, peer.Id, peer.Provider);
                        peer.State = ConversationRunState.Running; peer.HasUnread = true;
                        peerChat.Add("user", own.Name + "의 협업 검토 요청:\n" + handoffText);
                        SavePeerConversation(peer, peerChat);
                        roomStatuses[id] = agent.Name + " 협업 검토 중…";
                        string peerExecutable, peerModel, peerEffort, peerSpeed;
                        executablePaths.TryGetValue(peer.Provider, out peerExecutable);
                        models.TryGetValue(peer.Provider, out peerModel);
                        efforts.TryGetValue(peer.Provider, out peerEffort);
                        speeds.TryGetValue(peer.Provider, out peerSpeed);
                        bool sameProvider = peer.Provider == request.Provider;
                        var peerContext = new ChatRequestContext(context.Instructions, sameProvider ? context.Model : peerModel,
                            peerChat.Messages.ToArray(), context.Attachments, sameProvider ? context.ReasoningEffort : peerEffort,
                            sameProvider ? context.Speed : peerSpeed, collaborationStore.BuildPromptContext(peer.Id, peer.Provider, handoff.Id));
                        var edit = new RuntimeEditProvider();
                        edit.ProgressUpdated += progress => QueueRoomProgress(run, progress);
                        var result = await edit.GenerateWithContextAsync(peer.Provider, sameProvider ? request.Executable : peerExecutable,
                            source, "공유 요청을 담당 역할에 따라 검토하여 수정안을 제안하세요.\n" + handoffText, peerContext, run.Cancellation.Token);
                        run.Cancellation.Token.ThrowIfCancellationRequested();
                        IEditorDocument document = request.Document ?? documents.GetActiveDocument();
                        source.RequireUnchanged(document.Capture());
                        string reply = result.Summary + "\n" + result.ChangePreview;
                        if (reply.Length > 10000) reply = reply.Substring(0, 10000) + "\n[검토 결과 일부 생략]";
                        collaborationStore.Reply(handoff.Id, peer.Id, peer.Provider, reply);
                        collaborationStore.Deliver(handoff.Id, request.Record.Id, request.Record.Provider);
                        peerChat.Add("assistant", reply); peer.State = ConversationRunState.Completed;
                        SavePeerConversation(peer, peerChat);
                        replies.Add(agent.Name + " (" + agent.Role + "):\n" + reply);
                        roomStatuses[id] = agent.Name + " 협업 검토 완료";
                    }
                    catch (OperationCanceledException)
                    {
                        peer.State = ConversationRunState.Stopped; roomStatuses[id] = "협업 요청이 중지되었습니다."; peerChat.Add("system", roomStatuses[id]);
                        CleanupCollaboration(handoff, request.Record, peer, peerChat, true, "협업 요청이 중지되었습니다.");
                        throw;
                    }
                    catch (Exception error)
                    {
                        peer.State = ConversationRunState.Failed; roomStatuses[id] = "협업 실패: " + error.Message; peerChat.Add("system", roomStatuses[id]);
                        CleanupCollaboration(handoff, request.Record, peer, peerChat, false, error.Message);
                        throw;
                    }
                    finally { TryCollaborationCleanup(() => collaborationStore.ExportMarkdown(own.Team), "협업 보고서 저장 실패"); }
                }
                finally
                {
                    Dispatcher.VerifyAccess();
                    collaboratingRooms.Remove(id);
                    StartNextRoomRequest(id);
                    if (IsRoomShown(peer)) { StatusLabel.Text = roomStatuses[id]; UpdateButtons(); }
                }
            }
            if (replies.Count > 0)
            {
                int allowance = Math.Max(0, 7800 - identity.Length), each = allowance / replies.Count;
                identity += "\n다음은 이번 요청에서 실제 전달된 동료 검토입니다. 원본과 대조하여 판단하고 최종 수정안을 작성하세요.\n" +
                    String.Join("\n\n", replies.Select(reply => reply.Length <= each ? reply : reply.Substring(0, each) + " [일부 생략]"));
            }
            return new ChatRequestContext(context.Instructions, context.Model, context.History, context.Attachments,
                context.ReasoningEffort, context.Speed, identity);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : QueueRoomProgress
        /// desc         : 공개 CLI 이벤트를 요청 소유 방의 작업 카드와 답변으로 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void QueueRoomProgress(RoomRun run, AgentProgress progress)
        {
            if (progress == null || Interlocked.Increment(ref run.ProgressCount) > 80) return;
#pragma warning disable VSTHRD001
            var posted = Dispatcher.InvokeAsync(() =>
            {
                if (disposed || run.Cancellation.IsCancellationRequested || !roomRuns.ContainsKey(run.Request.Record.Id)) return;
                if (progress.Kind == "message")
                {
                    if (!String.IsNullOrWhiteSpace(progress.Text) && run.ProgressReplies.Add(progress.Text))
                        run.Request.Chat.Add("assistant", progress.Text);
                }
                else if (run.Notice.Apply(progress.Kind, progress.WorkId, progress.State, progress.Detail, progress.ExitCode))
                {
                    if (run.NoticeRow == null)
                    {
                        run.NoticeRow = new ChatMessage { Role = "system", Text = run.Notice.MarkdownText };
                        ProgressNoticeConverter.Attach(run.NoticeRow, run.Notice);
                        run.Request.Chat.Messages.Add(run.NoticeRow);
                    }
                    else run.NoticeRow.Text = run.Notice.MarkdownText;
                }
                if (IsRoomShown(run.Request.Record)) FollowConversation(false);
            }, DispatcherPriority.Input);
#pragma warning restore VSTHRD001
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : QueueRoomUsage
        /// desc         : CLI 사용량 콜백을 해당 대화방 UI 상태로 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void QueueRoomUsage(RoomRun run, ProviderUsageSnapshot usage)
        {
            if (usage == null) return;
            lock (run.UsageGate) run.Usage = MergeRoomUsage(run.Usage, usage);
#pragma warning disable VSTHRD001
            var posted = Dispatcher.InvokeAsync(() => { Dispatcher.VerifyAccess(); if (!disposed && roomRuns.ContainsKey(run.Request.Record.Id)) PresentRoomUsage(run, null); }, DispatcherPriority.Input);
#pragma warning restore VSTHRD001
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MergeRoomUsage
        /// desc         : 분할된 실제 CLI 토큰 이벤트의 알려진 수치를 한 방에 누적한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private static ProviderUsageSnapshot MergeRoomUsage(ProviderUsageSnapshot previous, ProviderUsageSnapshot update)
        {
            var merged = new ProviderUsageSnapshot {
                InputTokens = update.InputTokens ?? (previous == null ? null : previous.InputTokens),
                OutputTokens = update.OutputTokens ?? (previous == null ? null : previous.OutputTokens),
                CachedInputTokens = update.CachedInputTokens ?? (previous == null ? null : previous.CachedInputTokens),
                ContextWindow = update.ContextWindow ?? (previous == null ? null : previous.ContextWindow),
                TotalTokens = update.TotalTokens ?? (previous == null ? null : previous.TotalTokens),
                QuotaUsedPercent = update.QuotaUsedPercent ?? (previous == null ? null : previous.QuotaUsedPercent),
                QuotaResetsAt = update.QuotaResetsAt ?? (previous == null ? null : previous.QuotaResetsAt),
                Source = update.Source ?? (previous == null ? null : previous.Source), UpdatedAt = update.UpdatedAt
            };
            if (!merged.TotalTokens.HasValue) merged.TotalTokens = ProviderRuntimeService.SumKnown(merged.InputTokens, merged.OutputTokens);
            return merged;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PresentRoomUsage
        /// desc         : 현재 표시 중인 방의 토큰만 툴팁에 반영하고 다른 방 사용량은 따로 보관한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PresentRoomUsage(RoomRun run, ProviderUsageSnapshot usage)
        {
            Dispatcher.VerifyAccess();
            lock (run.UsageGate)
            {
                if (usage != null) run.Usage = MergeRoomUsage(run.Usage, usage);
                usage = run.Usage;
            }
            if (usage == null) return;
            roomUsages[run.Request.Record.Id] = usage;
            if (!IsRoomShown(run.Request.Record)) return;
            usage.QuotaUsedPercent = runtimeInfo == null || runtimeInfo.Usage == null ? usage.QuotaUsedPercent : runtimeInfo.Usage.QuotaUsedPercent;
            usage.QuotaResetsAt = runtimeInfo == null || runtimeInfo.Usage == null ? usage.QuotaResetsAt : runtimeInfo.Usage.QuotaResetsAt;
            runtimeUsage = usage;
            RefreshRuntimeTooltip();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShowRoomProposal
        /// desc         : 선택 방의 미승인 수정안만 비교 화면에 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ShowRoomProposal(RoomProposal value)
        {
            pendingProposal = value.Proposal; pendingDocument = value.Document;
            OriginalPreview.Text = value.Proposal.Source.Content;
            ProposedPreview.Text = value.Proposal.ProposedText;
            DiffPreview.Text = value.Proposal.ChangePreview;
            TargetLabel.Text = "대상: " + value.Proposal.Source.FilePath;
            ReviewApproval.IsChecked = false;
            ReviewTabs.SelectedIndex = 0;
            ReviewPanel.IsExpanded = true;
            UpdateButtons();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RestoreRoomPresentation
        /// desc         : 대화방 이동 시 그 방의 수정안·파일 변경·사용량·상태를 되살린다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RestoreRoomPresentation()
        {
            if (!experienceReady) return;
            string id = activeRecords[selectedProvider].Id;
            RoomProposal proposal;
            if (roomProposals.TryGetValue(id, out proposal)) ShowRoomProposal(proposal);
            AgentFileChange[] changes;
            if (roomChanges.TryGetValue(id, out changes)) PresentRuntimeChanges(changes, "전체 액세스 · 확인된 변경");
            string status;
            if (roomStatuses.TryGetValue(id, out status)) StatusLabel.Text = status;
            ProviderUsageSnapshot usage;
            if (roomUsages.TryGetValue(id, out usage)) { runtimeUsage = usage; RefreshRuntimeTooltip(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StartNextRoomRequest
        /// desc         : 같은 방의 다음 요청만 순차 실행하고 실행 조건이 바뀌면 남은 요청을 멈춘다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void StartNextRoomRequest(string id)
        {
            Dispatcher.VerifyAccess();
            Queue<QueuedUiRequest> queue;
            if (roomQueues.TryGetValue(id, out queue) && queue.Count > 0 && !roomRuns.ContainsKey(id) && !roomProposals.ContainsKey(id) && !collaboratingRooms.ContainsKey(id))
            {
                var request = queue.Dequeue();
                try { StartRoomRequest(request); }
                catch (Exception error)
                {
                    request.Record.State = ConversationRunState.Failed;
                    roomStatuses[id] = "대기 요청을 실행하지 않았습니다: " + error.Message;
                    request.Chat.Add("system", roomStatuses[id]);
                    CancelRoomQueue(id, "앞선 대기 요청의 실행 조건이 변경되었습니다.");
                    PersistConversation(request.Record, request.Chat);
                    if (IsRoomShown(request.Record)) { StatusLabel.Text = roomStatuses[id]; UpdateButtons(); }
                }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CancelRoomQueue
        /// desc         : 실행하지 않은 요청을 소유 방에 기록하고 자동 실행을 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CancelRoomQueue(string id, string reason)
        {
            Queue<QueuedUiRequest> queue;
            if (!roomQueues.TryGetValue(id, out queue)) return;
            while (queue.Count > 0)
            {
                var request = queue.Dequeue();
                request.Chat.Add("system", "[실행하지 않은 대기 요청]\n" + request.Instruction + "\n" + reason);
                PersistConversation(request.Record, request.Chat);
            }
        }


        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StopCurrentRoomRun
        /// desc         : 현재 방의 실행 또는 승인 대기열만 중지하고 다른 방은 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool StopCurrentRoomRun()
        {
            if (!experienceReady) return false;
            string id = activeRecords[selectedProvider].Id;
            RoomRun run;
            if (!roomRuns.TryGetValue(id, out run) && !collaboratingRooms.TryGetValue(id, out run))
            {
                Queue<QueuedUiRequest> waiting;
                if (!roomQueues.TryGetValue(id, out waiting) || waiting.Count == 0) return false;
                CancelRoomQueue(id, "사용자가 대기 요청을 중지했습니다.");
                roomStatuses[id] = "이 대화방의 대기 요청을 중지했습니다.";
                StatusLabel.Text = roomStatuses[id];
                return true;
            }
            CancelRoomQueue(id, "사용자가 대기 요청을 중지했습니다.");
            run.Cancellation.Cancel();
            return true;
        }
    }
}
