using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 실행 중 입력과 제한된 FIFO 요청을 분리하고 승인 경계를 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 순차 실행과 문맥 소유권 고정.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private const int MaximumQueuedRequests = 8;
        private readonly Queue<QueuedUiRequest> queuedRequests = new Queue<QueuedUiRequest>();
        private readonly Dictionary<string, string> persistedConversationFingerprints = new Dictionary<string, string>();
        private readonly Dictionary<ChatSession, QueueHistoryState> queuedHistory = new Dictionary<ChatSession, QueueHistoryState>();
        private QueuedUiRequest executingRequest;
        private bool drainingRequests;
        private Task requestQueueTask = Task.CompletedTask;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CurrentRequestProvider
        /// desc         : 현재 실행 요청의 고정 제공자를 완료 알림에 전달한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal string CurrentRequestProvider { get { return executingRequest == null ? "" : executingRequest.Provider; } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CurrentRequestTitle
        /// desc         : 작성 중 초안 대신 현재 실행 요청의 고정 제목을 전달한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal string CurrentRequestTitle { get { return executingRequest == null ? "" : executingRequest.Instruction; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : QueuedUiRequest
        /// desc         : 제출 시점의 제공자·대화·모드·문서·입력 문맥을 실행까지 보관한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 이후 작성 중 입력과 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class QueuedUiRequest
        {
            internal string Provider, Executable, Instruction, Directory;
            internal bool FullAccess;
            internal ConversationRecord Record;
            internal ChatSession Chat;
            internal IEditorDocument Document;
            internal DocumentSnapshot Source;
            internal ChatRequestContext Context;
            internal IAiEditProvider EditProvider;
            internal string[] CollaboratorIds;
            internal long Sequence;
            internal ChatMessage SubmittedMessage;
            internal QueueHistoryState HistoryState;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : QueueHistoryState
        /// desc         : 대화의 제출·완료 순서를 보관하여 미래 대기 입력을 실행 문맥에서 제외한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 선행 완료 답변을 순서대로 후속 요청에 전달.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class QueueHistoryState
        {
            internal long NextSequence;
            internal readonly Dictionary<ChatMessage, long> Order = new Dictionary<ChatMessage, long>();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EnqueueCurrentRequest
        /// desc         : 입력 검증이 성공할 때만 대기열에 넣고 다음 입력을 위한 빈 초안을 연다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 대기 8건 상한과 명시적 문맥 고정.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void EnqueueCurrentRequest()
        {
            Dispatcher.VerifyAccess();
            var request = CaptureCurrentRequest(false);
            queuedRequests.Enqueue(request);
            if (busy || HasPendingReview) StatusLabel.Text = "대기 요청 " + queuedRequests.Count + "개 · 순서대로 실행합니다." + (HasPendingReview ? " 현재 수정안을 승인·적용하거나 명시적으로 건너뛴 뒤 계속합니다. 중지로 대기 요청을 취소할 수 있습니다." : "");
            UpdateButtons();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureCurrentRequest
        /// desc         : 대화방 동시 실행과 기존 대기열이 같은 검증·문맥 캡처를 사용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexCode : 원래 읽기 전용 문서는 입력을 소비하기 전에 거절한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private QueuedUiRequest CaptureCurrentRequest(bool independent)
        {
            Dispatcher.VerifyAccess();
            if (string.IsNullOrWhiteSpace(Instruction.Text)) throw new InvalidOperationException("요청을 입력하세요.");
            if (Instruction.Text.Length > 16000) throw new InvalidOperationException("프롬프트는 16000자 이하로 입력하세요.");
            if (!independent && queuedRequests.Count >= MaximumQueuedRequests) throw new InvalidOperationException("대기 요청은 최대 8개입니다. 입력 내용은 그대로 보존했습니다.");
            Queue<QueuedUiRequest> roomQueue;
            if (independent && roomQueues.TryGetValue(activeRecords[selectedProvider].Id, out roomQueue) && roomQueue.Count >= MaximumQueuedRequests)
                throw new InvalidOperationException("이 대화방의 대기 요청은 최대 8개입니다. 입력 내용은 그대로 보존했습니다.");
            if (!independent && busy && !drainingRequests) throw new InvalidOperationException("문서 읽기나 계정 확인이 끝난 뒤 전송하세요. 입력은 계속 작성할 수 있습니다.");
            var request = new QueuedUiRequest {
                Provider = selectedProvider, Executable = ExecutablePath.Text, Instruction = Instruction.Text,
                FullAccess = AgentFullAccessEnabled, Record = activeRecords[selectedProvider], Chat = ActiveChat,
                EditProvider = provider
            };
            QueueHistoryState historyState;
            if (!queuedHistory.TryGetValue(request.Chat, out historyState)) {
                historyState = new QueueHistoryState();
                for (int index = 0; index < request.Chat.Messages.Count; index++) historyState.Order[request.Chat.Messages[index]] = index - request.Chat.Messages.Count;
                queuedHistory[request.Chat] = historyState;
            }
            request.HistoryState = historyState;
            request.Sequence = ++historyState.NextSequence * 2;
            request.CollaboratorIds = request.Record.CollaboratorIds.ToArray();
            var navigation = documents as ISourceNavigationService;
            request.Directory = navigation == null ? null : navigation.GetProjectDirectory();
            if (request.FullAccess) {
                if (string.IsNullOrWhiteSpace(request.Directory)) throw new InvalidOperationException("전체 액세스로 작업할 프로젝트 폴더를 열어 주세요.");
                var fullAccess = documents as IFullAccessDocumentService;
                if (fullAccess != null) {
                    request.Source = fullAccess.CaptureFullAccessContext();
                    if (request.Source != null) request.Document = documents.GetActiveDocument();
                }
                else { request.Document = documents.GetActiveDocument(); request.Source = request.Document.Capture(); }
                if (request.Source != null && request.Source.HasUnsavedChanges) throw new InvalidOperationException("CLI 직접 수정 전에 편집 중인 파일을 저장하세요.");
                if (request.Source != null && request.Source.IsReadOnly) {
                    var protection = sharedExternalEditing as IExternalEditingScopeState;
                    if (protection == null || !protection.IsTemporarilyReadOnly(request.Source.FilePath))
                        throw new InvalidOperationException("읽기 전용 문서는 수정할 수 없습니다. 입력은 그대로 보존했습니다.");
                }
            }
            else { request.Document = documents.GetActiveDocument(); request.Source = request.Document.Capture(); }
            if (request.Source != null) request.Source.RequireValidSelection();
            if (request.Source == null && request.CollaboratorIds.Length > 0) throw new InvalidOperationException("협업 검토에 사용할 편집 파일을 선택하세요.");
            MandatoryInstructionStore.Ensure(workspaceStore.RootPath);
            string instructions = File.Exists(workspaceStore.InstructionsPath) ? workspaceStore.LoadInstructions() : RulesText.Text;
            request.Context = new ChatRequestContext(instructions, ModelName.Text, request.Chat.Messages.ToArray(), request.Chat.Attachments.ToArray(), efforts[request.Provider], speeds[request.Provider]);
            new ProviderExecutionOptions(request.Provider, request.Context.Model, request.Context.ReasoningEffort, request.Context.Speed);
            var previousMessages = request.Chat.Messages.ToArray();
            request.Chat.Add("user", request.Instruction + (request.Context.Attachments.Length == 0 ? "" : "\n첨부: " + string.Join(", ", request.Context.Attachments.Select(item => item.Name))));
            var submittedMessage = request.Chat.Messages.Last();
            historyState.Order[submittedMessage] = request.Sequence;
            request.SubmittedMessage = submittedMessage;
            string draft = Instruction.Text;
            Instruction.Clear(); request.Chat.Draft = "";
            if (!PersistCurrentConversation()) {
                request.Chat.Messages.Clear();
                foreach (var previousMessage in previousMessages) request.Chat.Messages.Add(previousMessage);
                historyState.Order.Remove(submittedMessage);
                Instruction.Text = draft; request.Chat.Draft = draft;
                throw new InvalidOperationException("대화 기록 저장에 실패해 대기에 등록하지 않았습니다. 입력을 보존했습니다.");
            }
            return request;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HasPendingReview
        /// desc         : 미승인 수정안이 있으면 다음 요청의 실행을 보류한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool HasPendingReview { get { return pendingProposal != null && pendingProposal.HasChanges; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BuildQueuedExecutionContext
        /// desc         : 고정 옵션에 선행 완료 답변을 합치고 현재·미래 대기 입력을 제외한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 대화별 제출 순서로 CLI 문맥 재구성.
        /// </summary>
        /// ---------------------------------------------------------------------
        private ChatRequestContext BuildQueuedExecutionContext(QueuedUiRequest request)
        {
            PruneQueueHistory(request);
            var history = request.Chat.Messages.Where(message => request.HistoryState.Order.ContainsKey(message) && request.HistoryState.Order[message] < request.Sequence).OrderBy(message => request.HistoryState.Order[message]);
            return new ChatRequestContext(request.Context.Instructions, request.Context.Model, history.ToArray(), request.Context.Attachments, request.Context.ReasoningEffort, request.Context.Speed, request.Context.AgentContext);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PruneQueueHistory
        /// desc         : 대화 저장 상한에서 제거된 메시지 참조를 순서 장부에서도 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 메시지 100개 수명과 메모리 상한 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void PruneQueueHistory(QueuedUiRequest request)
        {
            var live = new HashSet<ChatMessage>(request.Chat.Messages);
            foreach (var removed in request.HistoryState.Order.Keys.Where(message => !live.Contains(message)).ToArray()) request.HistoryState.Order.Remove(removed);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequireQueuedOwnership
        /// desc         : 실행 직전 대화·권한·프로젝트와 협업 주소가 고정 문맥과 일치하는지 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 다른 대상 실행과 권한 승격 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RequireQueuedOwnership(QueuedUiRequest request)
        {
            if (disposed || request.Provider != selectedProvider || !ReferenceEquals(request.Record, activeRecords[selectedProvider]) ||
                !ReferenceEquals(request.Chat, ActiveChat) || request.FullAccess != AgentFullAccessEnabled || !ReferenceEquals(request.EditProvider, provider) ||
                !request.CollaboratorIds.SequenceEqual(request.Record.CollaboratorIds))
                throw new InvalidOperationException("대기 요청의 대화·권한·참여자가 변경되었습니다. 대기 요청을 취소하고 다시 전송하세요.");
            var navigation = documents as ISourceNavigationService;
            string directory = navigation == null ? null : navigation.GetProjectDirectory();
            if (!string.Equals(directory, request.Directory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("대기 요청의 프로젝트가 변경되었습니다. 다시 전송하세요.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DrainRequestQueueAsync
        /// desc         : 단일 소비자가 FIFO를 실행하며 실패·취소·승인 대기에서 안전하게 멈춘다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 자동 재시도·자동 적용·미승인 덮어쓰기 금지.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD109", Justification = "WPF UI 입력/승인 경로에서만 호출하며 Dispatcher.VerifyAccess로 진입 계약을 검증한다.")]
        private async Task DrainRequestQueueAsync()
        {
            Dispatcher.VerifyAccess();
            if (drainingRequests || busy || disposed || HasPendingReview) return;
            drainingRequests = true;
            try {
                while (!disposed && queuedRequests.Count > 0 && !HasPendingReview) {
                    executingRequest = queuedRequests.Dequeue();
                    bool succeeded = executingRequest.FullAccess ? await RunFullAccessFromUiAsync(executingRequest) : await RunQueuedEditAsync(executingRequest);
                    foreach (var message in executingRequest.Chat.Messages.Where(item => !executingRequest.HistoryState.Order.ContainsKey(item))) executingRequest.HistoryState.Order[message] = executingRequest.Sequence + 1;
                    PruneQueueHistory(executingRequest);
                    if (!succeeded) { CancelQueuedRequests("실패·취소 이후 대기 요청은 실행하지 않았습니다. 대화의 원문을 확인해 다시 전송하세요."); break; }
                }
            }
            catch (Exception error) { if (!disposed) { CancelQueuedRequests("대기 실행 실패: " + error.Message); StatusLabel.Text = error.Message; } }
            finally {
                executingRequest = null; drainingRequests = false;
                if (!disposed) { if (HasPendingReview && queuedRequests.Count > 0) StatusLabel.Text += "\n대기 요청 " + queuedRequests.Count + "개 · 현재 수정안을 승인·적용하거나 명시적으로 건너뛴 뒤 계속합니다. 중지로 대기 요청을 취소할 수 있습니다."; UpdateButtons(); }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DiscardProposalAndContinueClicked
        /// desc         : 사용자가 명시적으로 건너뛴 미적용 수정안만 제거하고 다음 대기를 재개한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 적용·저장 없이 승인 대기 해소.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DiscardProposalAndContinueClicked(object sender, System.Windows.RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess();
            if (roomProposals.ContainsKey(activeRecords[selectedProvider].Id))
            {
                if (disposed || busy) return;
                ActiveChat.Add("system", "사용자가 현재 수정안을 건너뛰었습니다. 소스에 적용하지 않았습니다.");
                if (!PersistCurrentConversation()) return;
                string id = activeRecords[selectedProvider].Id;
                roomProposals.Remove(id);
                ClearProposal();
                StartNextRoomRequest(id);
                UpdateButtons();
                return;
            }
            if (disposed || busy || !HasPendingReview || queuedRequests.Count == 0) return;
            ActiveChat.Add("system", "사용자가 현재 수정안 건너뛰기를 요청했습니다. 소스에 적용하거나 저장하지 않고 다음 요청을 진행합니다.");
            if (!PersistCurrentConversation()) return;
            ClearProposal();
            UpdateButtons();
            ResumeRequestQueue();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResumeRequestQueue
        /// desc         : 명시적 적용 성공 뒤 같은 단일 소비자의 관찰 가능한 Task로 대기를 재개한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 승인 실패 시 재개하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ResumeRequestQueue() { requestQueueTask = DrainRequestQueueAsync(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CancelQueuedRequests
        /// desc         : 실행하지 않은 요청 원문을 대화에 남기고 대기 문맥의 메모리를 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 중지·실패·Dispose의 잔여 실행 차단.
        ///                2026-10-20 CodexOrig : TASK055 후속 원래 CLI·저장 오류를 보존하고 실제 잔여 취소 때만 안내 추가.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CancelQueuedRequests(string reason)
        {
            int cancelledCount = queuedRequests.Count;
            while (queuedRequests.Count > 0) {
                var queued = queuedRequests.Dequeue();
                queued.HistoryState.Order[queued.SubmittedMessage] = long.MaxValue;
                if (reason != null && !disposed) queued.Chat.Add("system", "[실행하지 않은 대기 요청]\n" + queued.Instruction + "\n" + reason);
            }
            if (reason != null && !disposed && cancelledCount > 0) {
                string notice = "대기 요청 " + cancelledCount + "개 취소: " + reason;
                StatusLabel.Text = string.IsNullOrWhiteSpace(StatusLabel.Text) ? notice : StatusLabel.Text + "\n" + notice;
                PersistCurrentConversation();
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConversationFingerprint
        /// desc         : 저장할 상태가 동일하면 동기 파일 쓰기와 대화 목록 재생성을 건너뛴다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 내용·상태·읽음·역할 메타데이터 전체 비교.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ConversationFingerprint(ConversationRecord record)
        {
            string json = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.Serialize(record);
            using (var digest = System.Security.Cryptography.SHA256.Create()) return Convert.ToBase64String(digest.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json)));
        }
    }
}
