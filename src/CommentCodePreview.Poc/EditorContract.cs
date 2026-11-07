using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CommentCodePreview.Poc
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PreviewLimits
    /// desc         : 수동 모의 요청의 문맥·입력·응답과 미완료 요청 수를 제한한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class PreviewLimits
    {
        public const int ContextCharacters = 65536, ContextUtf8Bytes = 131072;
        public const int InstructionCharacters = 2048, InstructionUtf8Bytes = 8192;
        public const int SuggestionCharacters = 8192, SuggestionUtf8Bytes = 16384;
        public const int OutstandingRequests = 4;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PreviewState / PreviewReason / RequestOutcome
    /// desc         : 원문이나 공급자 예외를 복사하지 않는 상태와 거절 근거다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public enum PreviewState { Idle, Requesting, Ready, Applying, Accepted, Undone, Discarded, Faulted, Disposed }
    public enum PreviewReason { None, Cancelled, ContextChanged, InvalidContext, ReadOnly, DiffView, Closed, ImeActive, Oversize, InvalidText, Empty, ProviderError, EditorError, Busy, UndoConflict, Disposed }
    public enum RequestOutcome { Ready, Discarded, Rejected, Failed }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SnapshotRejectedException
    /// desc         : 잘못된 문서 경계를 원문 없는 형식화 사유로 보고한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class SnapshotRejectedException : ArgumentException
    {
        public PreviewReason Reason { get; private set; }
        public SnapshotRejectedException(PreviewReason reason) : base("Editor snapshot rejected.") { Reason = reason; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : EditorSnapshot
    /// desc         : UTF-16 위치·문서 버전·컨텍스트 세대와 내용 hash 및 guard를 불변 캡처한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class EditorSnapshot
    {
        public string DocumentId { get; private set; }
        public long Version { get; private set; }
        public long ContextEpoch { get; private set; }
        public int Cursor { get; private set; }
        public int SelectionStart { get; private set; }
        public int SelectionLength { get; private set; }
        public string Content { get; private set; }
        public string ContentHash { get; private set; }
        public bool IsReadOnly { get; private set; }
        public bool IsDiffView { get; private set; }
        public bool IsClosed { get; private set; }
        public bool IsImeActive { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EditorSnapshot
        /// desc         : 상한과 surrogate·CRLF 경계를 검사한 뒤 문맥을 소유하며 파일 I/O는 하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public EditorSnapshot(string documentId, long version, long contextEpoch, string content, int cursor, int selectionStart, int selectionLength, bool isReadOnly, bool isDiffView, bool isClosed, bool isImeActive)
        {
            if (String.IsNullOrWhiteSpace(documentId) || documentId.Length > 256 || version < 0 || contextEpoch < 0) throw new SnapshotRejectedException(PreviewReason.InvalidContext);
            foreach (char value in documentId) if (Char.IsControl(value)) throw new SnapshotRejectedException(PreviewReason.InvalidContext);
            var reason = TextPolicy.Validate(content, PreviewLimits.ContextCharacters, PreviewLimits.ContextUtf8Bytes, false);
            if (reason != PreviewReason.None) throw new SnapshotRejectedException(reason);
            if (selectionStart < 0 || selectionStart > content.Length || selectionLength < 0 || selectionLength > content.Length - selectionStart || !TextPolicy.IsBoundary(content, cursor) || !TextPolicy.IsBoundary(content, selectionStart) || !TextPolicy.IsBoundary(content, selectionStart + selectionLength)) throw new SnapshotRejectedException(PreviewReason.InvalidContext);
            DocumentId = documentId; Version = version; ContextEpoch = contextEpoch; Content = content; Cursor = cursor; SelectionStart = selectionStart; SelectionLength = selectionLength;
            IsReadOnly = isReadOnly; IsDiffView = isDiffView; IsClosed = isClosed; IsImeActive = isImeActive;
            using (var hash = SHA256.Create()) ContentHash = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-", "");
        }

        public PreviewReason BlockReason { get { return IsClosed ? PreviewReason.Closed : IsReadOnly ? PreviewReason.ReadOnly : IsDiffView ? PreviewReason.DiffView : IsImeActive ? PreviewReason.ImeActive : PreviewReason.None; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Matches
        /// desc         : 복원된 동일 좌표라도 세대가 다르면 거절하고 hash와 원문을 모두 대조한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool Matches(EditorSnapshot other)
        {
            return other != null && DocumentId == other.DocumentId && Version == other.Version && ContextEpoch == other.ContextEpoch && Cursor == other.Cursor && SelectionStart == other.SelectionStart && SelectionLength == other.SelectionLength && ContentHash == other.ContentHash && Content == other.Content && IsReadOnly == other.IsReadOnly && IsDiffView == other.IsDiffView && IsClosed == other.IsClosed && IsImeActive == other.IsImeActive;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TextPolicy
    /// desc         : 문자를 재작성하지 않고 크기·UTF-16·CRLF 원자 경계만 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class TextPolicy
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Validate
        /// desc         : 상한과 Unicode·개행 형식을 확인하며 원문을 정규화하거나 실행하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static PreviewReason Validate(string text, int characters, int bytes, bool requireNonWhitespace)
        {
            if (text == null || requireNonWhitespace && String.IsNullOrWhiteSpace(text)) return PreviewReason.Empty;
            if (text.Length > characters) return PreviewReason.Oversize;
            for (int index = 0; index < text.Length; index++)
            {
                char value = text[index];
                if (value == '\0' || Char.IsLowSurrogate(value)) return PreviewReason.InvalidText;
                if (Char.IsHighSurrogate(value)) { if (index + 1 >= text.Length || !Char.IsLowSurrogate(text[index + 1])) return PreviewReason.InvalidText; index++; }
                else if (value == '\r') { if (index + 1 >= text.Length || text[index + 1] != '\n') return PreviewReason.InvalidText; index++; }
            }
            return Encoding.UTF8.GetByteCount(text) > bytes ? PreviewReason.Oversize : PreviewReason.None;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsBoundary
        /// desc         : UTF-16 surrogate 또는 CRLF 쌍 내부 위치를 거절한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static bool IsBoundary(string text, int position)
        {
            return position >= 0 && position <= text.Length && (position == 0 || position == text.Length || !(Char.IsHighSurrogate(text[position - 1]) && Char.IsLowSurrogate(text[position]) || text[position - 1] == '\r' && text[position] == '\n'));
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IUndoReceipt / IAtomicPreviewEditor
    /// desc         : host는 비교·취소 검사·단일 삽입을 한 임계구역에서 수행하고 같은 receipt만 Undo한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface IUndoReceipt { }
    public interface IAtomicPreviewEditor
    {
        EditorSnapshot Capture();
        AtomicApplyResult CompareAndApply(EditorSnapshot expected, string insertion, CancellationToken cancellationToken);
        AtomicUndoResult CompareAndUndo(IUndoReceipt receipt);
    }

    public sealed class AtomicApplyResult
    {
        public bool Applied { get; private set; }
        public PreviewReason Reason { get; private set; }
        public IUndoReceipt Receipt { get; private set; }
        private AtomicApplyResult() { }
        public static AtomicApplyResult Success(IUndoReceipt receipt) { if (receipt == null) throw new ArgumentNullException("receipt"); return new AtomicApplyResult { Applied = true, Receipt = receipt }; }
        public static AtomicApplyResult Rejected(PreviewReason reason) { return new AtomicApplyResult { Reason = reason == PreviewReason.None || !Enum.IsDefined(typeof(PreviewReason), reason) ? PreviewReason.ContextChanged : reason }; }
    }
    public sealed class AtomicUndoResult
    {
        public bool Undone { get; private set; }
        public PreviewReason Reason { get; private set; }
        private AtomicUndoResult() { }
        public static AtomicUndoResult Success() { return new AtomicUndoResult { Undone = true }; }
        public static AtomicUndoResult Rejected(PreviewReason reason) { return new AtomicUndoResult { Reason = reason == PreviewReason.None || !Enum.IsDefined(typeof(PreviewReason), reason) ? PreviewReason.UndoConflict : reason }; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CommentCodeRequest / ICommentCodeProvider
    /// desc         : 명시적으로 요청한 제한 문맥만 전달하며 전송·권한·명령 API를 제공하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class CommentCodeRequest
    {
        public long RequestId { get; private set; }
        public string Instruction { get; private set; }
        public EditorSnapshot Snapshot { get; private set; }
        internal CommentCodeRequest(long id, string instruction, EditorSnapshot snapshot) { RequestId = id; Instruction = instruction; Snapshot = snapshot; }
    }
    public interface ICommentCodeProvider { Task<string> GenerateAsync(CommentCodeRequest request, CancellationToken cancellationToken); }
}
