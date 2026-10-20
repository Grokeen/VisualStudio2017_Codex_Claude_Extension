using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;

namespace VS2017.AIAssistant.Providers
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CollaborationMessageRunner
    /// desc         : 사용자가 승인한 수신 메시지만 격리 CLI로 답하고 실제 회신을 원자 저장한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 프로젝트·첨부·다른 대화 없이 read-only/tools-off 응답.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class CollaborationMessageRunner
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReplyAsync
        /// desc         : 명시 수신/재시도 뒤 실제 CLI summary만 보관하며 수정안·자동 재전송은 거절한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 승인 취소 무쓰기·수신 주소 검증·프로세스 간 중복 실행 배제.
        /// </summary>
        /// ---------------------------------------------------------------------
        public async Task<CollaborationHandoff> ReplyAsync(ConversationCollaborationStore store, string handoffId,
            string recipientId, string provider, string executable, ChatRequestContext settings, bool approved, CancellationToken cancellation)
        {
            if (!approved) throw new OperationCanceledException("메시지 수신과 AI 응답 실행을 승인하지 않았습니다.");
            if (store == null || settings == null) throw new ArgumentNullException(store == null ? "store" : "settings");
            cancellation.ThrowIfCancellationRequested();
            var item = store.GetHandoff(handoffId);
            if (item == null || item.ToConversationId != recipientId || item.ToProvider != provider)
                throw new InvalidOperationException("현재 수신자에게 도착한 메시지만 응답할 수 있습니다.");
            // GUID는 GetHandoff가 검증했고 파일 위치는 고정 workspace store에서만 파생한다.
            using (var lease = AcquireReplyLease(store.FilePath, handoffId))
            {
                item = store.GetHandoff(handoffId);
                if (item.State == CollaborationHandoffState.Replied || item.State == CollaborationHandoffState.Delivered)
                    throw new InvalidOperationException("이미 실제 회신이 저장되어 있습니다. 회신을 확인하세요.");
                if (item.State == CollaborationHandoffState.Failed) store.Retry(handoffId, recipientId, provider);
                else store.Receive(handoffId, recipientId, provider);
                try
                {
                    string identity = store.BuildPromptContext(recipientId, provider, handoffId);
                    var context = new ChatRequestContext(settings.Instructions, settings.Model, null, null,
                        settings.ReasoningEffort, settings.Speed, identity);
                    var source = new DocumentSnapshot("collaboration-message.txt", "", "", "메시지 응답 전용 · 프로젝트 파일 접근 없음", false, false);
                    var result = await new CliEditProvider().GenerateWithContextAsync(provider, executable, source,
                        "명시 수신한 협업 메시지를 담당 역할에 따라 답하세요. summary에 실제 답변을 작성하고 edits는 빈 배열로 반환하세요. 파일·명령 실행이나 다른 AI에게 자동 전송하지 마세요.", context, cancellation).ConfigureAwait(false);
                    cancellation.ThrowIfCancellationRequested();
                    if (result.HasChanges) throw new InvalidOperationException("메시지 응답에서 수정안이 반환되어 적용·회신하지 않았습니다.");
                    if (String.IsNullOrWhiteSpace(result.Summary) || result.Summary.Length > 12000)
                        throw new InvalidOperationException("회신은 비어 있지 않은 12,000자 이하의 실제 답변이어야 합니다.");
                    return store.Reply(handoffId, recipientId, provider, result.Summary);
                }
                catch (Exception error)
                {
                    try
                    {
                        if (store.GetHandoff(handoffId).State == CollaborationHandoffState.Received)
                            store.MarkFailed(handoffId, recipientId, provider, error is OperationCanceledException ? "사용자가 AI 메시지 응답을 중지했습니다." : "AI 메시지 응답 실패: " + error.GetType().Name);
                    }
                    catch (Exception) { /* 원래 CLI/취소 오류와 기존 수신 기록은 보존한다. */ }
                    throw;
                }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AcquireReplyLease
        /// desc         : 같은 메시지의 동시 CLI를 배제하며 내부 협업 파일 경로는 사용자에게 노출하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : async continuation과 무관하게 해제할 수 있는 파일 handle 수명 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static FileStream AcquireReplyLease(string storePath, string handoffId)
        {
            try { return new FileStream(storePath + ".message-" + handoffId + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException error) { throw new InvalidOperationException("메시지 처리 잠금을 확보하지 못했습니다. 다른 창에서 처리 중인지 확인하세요.", error); }
        }
    }
}
