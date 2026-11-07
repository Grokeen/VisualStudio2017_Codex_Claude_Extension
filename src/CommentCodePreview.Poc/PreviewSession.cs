using System;
using System.Threading;
using System.Threading.Tasks;

namespace CommentCodePreview.Poc
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CodePreview / PreviewStatus / PreviewRequestResult
    /// desc         : 미리보기 원문과 원문 없는 실행 상태를 분리하고 명시 수락에 요청 ID를 요구한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class CodePreview
    {
        public long RequestId { get; private set; }
        public string Text { get; private set; }
        internal CodePreview(long id, string text) { RequestId = id; Text = text; }
    }
    public sealed class PreviewStatus
    {
        public long RequestId { get; private set; }
        public PreviewState State { get; private set; }
        public PreviewReason Reason { get; private set; }
        public bool CanAccept { get { return State == PreviewState.Ready; } }
        public bool CanUndo { get { return State == PreviewState.Accepted; } }
        internal PreviewStatus(long id, PreviewState state, PreviewReason reason) { RequestId = id; State = state; Reason = reason; }
    }
    public sealed class PreviewRequestResult
    {
        public long RequestId { get; private set; }
        public RequestOutcome Outcome { get; private set; }
        public PreviewReason Reason { get; private set; }
        internal PreviewRequestResult(long id, RequestOutcome outcome, PreviewReason reason) { RequestId = id; Outcome = outcome; Reason = reason; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PreviewSession
    /// desc         : 명시 요청·비파괴 미리보기·원자 수락·보수적 Undo만 제공하는 SDK 독립 상태기계다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class PreviewSession : IDisposable
    {
        private sealed class Ticket
        {
            internal long Id;
            internal IAtomicPreviewEditor Editor;
            internal EditorSnapshot Snapshot;
            internal CancellationTokenSource Cancellation;
            internal CancellationToken Token;
            internal int Retired, Completed, Disposed;
        }
        private readonly object sync = new object();
        private readonly ICommentCodeProvider provider;
        private Ticket active;
        private CodePreview preview;
        private IUndoReceipt receipt;
        private IAtomicPreviewEditor undoEditor;
        private PreviewState state = PreviewState.Idle;
        private PreviewReason reason;
        private long serial, acceptedId;
        private int outstanding;
        private bool disposeRequested;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PreviewSession
        /// desc         : 명시 공급자만 보관하고 생성자에서 요청이나 편집을 시작하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public PreviewSession(ICommentCodeProvider provider)
        { if (provider == null) throw new ArgumentNullException("provider"); this.provider = provider; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequestAsync
        /// desc         : 호출 때만 모의 공급자를 실행하며 새 요청·취소·문맥 변화 뒤 도착한 답변은 폐기한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public async Task<PreviewRequestResult> RequestAsync(IAtomicPreviewEditor editor, string explicitComment, CancellationToken cancellationToken)
        {
            if (editor == null) throw new ArgumentNullException("editor");
            Ticket previous, ticket; long issuedId;
            lock (sync)
            {
                if (state == PreviewState.Disposed) return new PreviewRequestResult(serial, RequestOutcome.Rejected, PreviewReason.Disposed);
                if (state == PreviewState.Applying) return new PreviewRequestResult(serial, RequestOutcome.Rejected, PreviewReason.Busy);
                previous = active; active = null; preview = null; receipt = null; undoEditor = null;
                issuedId = ++serial;
                if (outstanding >= PreviewLimits.OutstandingRequests) { ticket = null; state = PreviewState.Discarded; reason = PreviewReason.Busy; }
                else
                {
                    var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    ticket = new Ticket { Id = serial, Editor = editor, Cancellation = cancellation, Token = cancellation.Token };
                    active = ticket; outstanding++; state = PreviewState.Requesting; reason = PreviewReason.None;
                }
            }
            Retire(previous);
            if (ticket == null) return new PreviewRequestResult(issuedId, RequestOutcome.Rejected, PreviewReason.Busy);
            bool providerStarted = false;
            try
            {
                if (ticket.Token.IsCancellationRequested) return Finish(ticket, PreviewReason.Cancelled, false);
                var instructionReason = TextPolicy.Validate(explicitComment, PreviewLimits.InstructionCharacters, PreviewLimits.InstructionUtf8Bytes, true);
                if (instructionReason != PreviewReason.None) return Finish(ticket, instructionReason, false);
                ticket.Snapshot = editor.Capture();
                if (ticket.Snapshot == null) return Finish(ticket, PreviewReason.InvalidContext, false);
                if (ticket.Snapshot.BlockReason != PreviewReason.None) return Finish(ticket, ticket.Snapshot.BlockReason, false);
                lock (sync) { if (active != ticket || state != PreviewState.Requesting) return new PreviewRequestResult(ticket.Id, RequestOutcome.Discarded, PreviewReason.ContextChanged); }
                providerStarted = true;
                string text = await provider.GenerateAsync(new CommentCodeRequest(ticket.Id, explicitComment, ticket.Snapshot), ticket.Token).ConfigureAwait(false);
                providerStarted = false;
                if (ticket.Token.IsCancellationRequested) return Finish(ticket, PreviewReason.Cancelled, false);
                var textReason = TextPolicy.Validate(text, PreviewLimits.SuggestionCharacters, PreviewLimits.SuggestionUtf8Bytes, true);
                if (textReason != PreviewReason.None) return Finish(ticket, textReason, false);
                var current = editor.Capture();
                if (current == null) return Finish(ticket, PreviewReason.InvalidContext, false);
                if (current.BlockReason != PreviewReason.None) return Finish(ticket, current.BlockReason, false);
                if (!ticket.Snapshot.Matches(current)) return Finish(ticket, PreviewReason.ContextChanged, false);
                bool ready = false;
                lock (sync)
                {
                    if (active == ticket && state == PreviewState.Requesting && !ticket.Token.IsCancellationRequested)
                    { preview = new CodePreview(ticket.Id, text); state = PreviewState.Ready; reason = PreviewReason.None; ready = true; }
                }
                return ready ? new PreviewRequestResult(ticket.Id, RequestOutcome.Ready, PreviewReason.None) : Finish(ticket, ticket.Token.IsCancellationRequested ? PreviewReason.Cancelled : PreviewReason.ContextChanged, false);
            }
            catch (OperationCanceledException) { return Finish(ticket, PreviewReason.Cancelled, false); }
            catch (SnapshotRejectedException error) { return Finish(ticket, error.Reason, false); }
            catch (Exception) { return Finish(ticket, providerStarted ? PreviewReason.ProviderError : PreviewReason.EditorError, true); }
            finally
            {
                lock (sync) { outstanding--; }
                Interlocked.Exchange(ref ticket.Completed, 1); TryDispose(ticket);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetStatus / GetPreview
        /// desc         : 취소 토큰을 반영하고 현재 요청의 상태·미리보기만 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public PreviewStatus GetStatus() { ObserveCancellation(); lock (sync) { return new PreviewStatus(serial, state, reason); } }
        public CodePreview GetPreview() { ObserveCancellation(); lock (sync) { return state == PreviewState.Ready ? preview : null; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Accept
        /// desc         : 표시한 요청 ID가 같은 경우에만 atomic compare-and-apply를 한 번 호출한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool Accept(long previewRequestId)
        {
            ObserveCancellation(); Ticket ticket; bool applied = false;
            lock (sync)
            {
                if (state != PreviewState.Ready || preview == null || preview.RequestId != previewRequestId || active == null) return false;
                ticket = active; state = PreviewState.Applying;
                try
                {
                    var result = ticket.Editor.CompareAndApply(ticket.Snapshot, preview.Text, ticket.Token);
                    if (result != null && result.Applied && result.Receipt != null)
                    { receipt = result.Receipt; undoEditor = ticket.Editor; acceptedId = ticket.Id; state = PreviewState.Accepted; reason = PreviewReason.None; applied = true; }
                    else { state = PreviewState.Discarded; reason = result == null ? PreviewReason.EditorError : result.Reason; }
                }
                catch (Exception) { state = PreviewState.Faulted; reason = PreviewReason.EditorError; }
                active = null; preview = null;
                if (disposeRequested) { state = PreviewState.Disposed; reason = PreviewReason.Disposed; receipt = null; undoEditor = null; }
            }
            Retire(ticket); return applied;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Undo
        /// desc         : 마지막 수락 ID와 같은 receipt의 compare-and-undo만 한 번 허용한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool Undo(long acceptedRequestId)
        {
            lock (sync)
            {
                if (state != PreviewState.Accepted || receipt == null || undoEditor == null || acceptedId != acceptedRequestId) return false;
                state = PreviewState.Applying; bool undone = false;
                try
                {
                    var result = undoEditor.CompareAndUndo(receipt);
                    undone = result != null && result.Undone;
                    state = undone ? PreviewState.Undone : PreviewState.Discarded;
                    reason = undone ? PreviewReason.None : result == null ? PreviewReason.EditorError : result.Reason;
                }
                catch (Exception) { state = PreviewState.Faulted; reason = PreviewReason.EditorError; }
                receipt = null; undoEditor = null;
                if (disposeRequested) { state = PreviewState.Disposed; reason = PreviewReason.Disposed; }
                return undone;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Cancel / NotifyContextChanged
        /// desc         : 새 자동 요청 없이 활성 제안을 폐기하며 수락 후 외부 변화는 Undo 권리도 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool Cancel() { return Invalidate(PreviewReason.Cancelled, false); }
        public bool NotifyContextChanged() { return Invalidate(PreviewReason.ContextChanged, true); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Invalidate
        /// desc         : 현재 요청만 잠금 안에서 분리하고 공급자 취소는 잠금 밖에서 전달한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool Invalidate(PreviewReason why, bool clearAccepted)
        {
            Ticket retired;
            lock (sync)
            {
                if (state == PreviewState.Disposed || state == PreviewState.Applying || !clearAccepted && (state == PreviewState.Accepted || state == PreviewState.Undone)) return false;
                retired = active; active = null; preview = null; receipt = null; undoEditor = null; state = PreviewState.Discarded; reason = why;
            }
            Retire(retired); return true;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ObserveCancellation
        /// desc         : 미리보기 이후 취소된 외부 토큰도 다음 조회·수락에서 폐기 상태로 반영한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ObserveCancellation()
        {
            Ticket retired = null;
            lock (sync)
            {
                if (active != null && state != PreviewState.Applying && active.Token.IsCancellationRequested)
                { retired = active; active = null; preview = null; state = PreviewState.Discarded; reason = PreviewReason.Cancelled; }
            }
            Retire(retired);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Finish
        /// desc         : 이전 요청의 종료가 새 요청 상태를 덮지 않게 소유 ticket만 완료한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private PreviewRequestResult Finish(Ticket ticket, PreviewReason why, bool fault)
        {
            lock (sync) { if (active == ticket) { active = null; preview = null; state = fault ? PreviewState.Faulted : PreviewState.Discarded; reason = why; } }
            Retire(ticket); return new PreviewRequestResult(ticket.Id, fault ? RequestOutcome.Failed : RequestOutcome.Discarded, why);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Retire
        /// desc         : 제안을 한 번만 취소하고 완료 전 공급자의 토큰을 조기 해제하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Retire(Ticket ticket)
        {
            if (ticket == null) return;
            if (Interlocked.Exchange(ref ticket.Retired, 1) == 0) { try { ticket.Cancellation.Cancel(); } catch (AggregateException) { } catch (ObjectDisposedException) { } }
            TryDispose(ticket);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryDispose
        /// desc         : 취소·완료 경쟁에서도 source를 정확히 한 번 정리한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void TryDispose(Ticket ticket)
        { if (Volatile.Read(ref ticket.Retired) != 0 && Volatile.Read(ref ticket.Completed) != 0 && Interlocked.Exchange(ref ticket.Disposed, 1) == 0) ticket.Cancellation.Dispose(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 현재 요청과 제안을 폐기하고 완료되지 않은 공급자는 취소하되 외부 버퍼는 바꾸지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            Ticket retired;
            lock (sync) { if (state == PreviewState.Disposed) return; if (state == PreviewState.Applying) { disposeRequested = true; return; } retired = active; active = null; preview = null; receipt = null; undoEditor = null; state = PreviewState.Disposed; reason = PreviewReason.Disposed; }
            Retire(retired);
        }
    }
}
