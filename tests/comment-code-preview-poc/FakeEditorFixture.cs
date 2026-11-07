using System;
using System.Threading;
using CommentCodePreview.Poc;

namespace CommentCodePreview.Poc.Tests
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FakeEditorFixture
    /// desc         : 실제 문서·SDK 없이 동일 lock 안에서 재비교·단일 삽입·보수적 Undo를 수행한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class FakeEditorFixture : IAtomicPreviewEditor
    {
        private sealed class Receipt : IUndoReceipt
        { internal FakeEditorFixture Owner; internal EditorSnapshot Before, After; internal bool Used; }
        private readonly object sync = new object();
        private string id = "anonymous-document-A", text;
        private long version = 1, epoch = 1;
        private int cursor, selectionStart, selectionLength;
        private bool readOnly, diff, closed, ime;
        internal Action<FakeEditorFixture> BeforeAtomicApply;
        internal int ApplyAttempts { get; private set; }
        internal int Insertions { get; private set; }
        internal int UndoAttempts { get; private set; }
        internal int Undos { get; private set; }
        internal int Saves { get { return 0; } }
        internal int Commands { get { return 0; } }
        internal string Text { get { lock (sync) return text; } }
        internal FakeEditorFixture(string text) { this.text = text; cursor = selectionStart = text.Length; }
        public EditorSnapshot Capture() { lock (sync) return Snapshot(); }
        private EditorSnapshot Snapshot() { return new EditorSnapshot(id, version, epoch, text, cursor, selectionStart, selectionLength, readOnly, diff, closed, ime); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CompareAndApply
        /// desc         : 재현 hook 뒤에도 비교·취소 확인과 삽입을 한 임계구역에서 수행한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public AtomicApplyResult CompareAndApply(EditorSnapshot expected, string insertion, CancellationToken cancellationToken)
        {
            lock (sync)
            {
                ApplyAttempts++;
                var hook = BeforeAtomicApply; BeforeAtomicApply = null; if (hook != null) hook(this);
                if (cancellationToken.IsCancellationRequested) return AtomicApplyResult.Rejected(PreviewReason.Cancelled);
                var current = Snapshot();
                if (current.BlockReason != PreviewReason.None) return AtomicApplyResult.Rejected(current.BlockReason);
                if (!expected.Matches(current)) return AtomicApplyResult.Rejected(PreviewReason.ContextChanged);
                string combined = text.Insert(cursor, insertion);
                EditorSnapshot after;
                try { after = new EditorSnapshot(id, version + 1, epoch + 1, combined, cursor + insertion.Length, cursor + insertion.Length, 0, readOnly, diff, closed, ime); }
                catch (SnapshotRejectedException error) { return AtomicApplyResult.Rejected(error.Reason); }
                text = combined; cursor += insertion.Length; selectionStart = cursor; selectionLength = 0; version++; epoch++; Insertions++;
                return AtomicApplyResult.Success(new Receipt { Owner = this, Before = current, After = after });
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CompareAndUndo
        /// desc         : 마지막 postimage와 모든 문맥이 일치할 때만 preimage를 한 번 복원한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public AtomicUndoResult CompareAndUndo(IUndoReceipt opaque)
        {
            lock (sync)
            {
                UndoAttempts++;
                var receipt = opaque as Receipt;
                if (receipt == null || receipt.Owner != this || receipt.Used) return AtomicUndoResult.Rejected(PreviewReason.UndoConflict);
                var current = Snapshot();
                if (current.BlockReason != PreviewReason.None) return AtomicUndoResult.Rejected(current.BlockReason);
                if (!receipt.After.Matches(current)) return AtomicUndoResult.Rejected(PreviewReason.UndoConflict);
                receipt.Used = true; text = receipt.Before.Content; cursor = receipt.Before.Cursor; selectionStart = receipt.Before.SelectionStart; selectionLength = receipt.Before.SelectionLength; version++; epoch++; Undos++;
                return AtomicUndoResult.Success();
            }
        }
        internal void MoveCursor(int value) { lock (sync) { cursor = selectionStart = value; selectionLength = 0; epoch++; } }
        internal void Select(int start, int length) { lock (sync) { selectionStart = start; selectionLength = length; cursor = start + length; epoch++; } }
        internal void SwitchDocument(string value) { lock (sync) { id = value; epoch++; } }
        internal void AdvanceVersion() { lock (sync) version++; }
        internal void ReplaceExternal(string value) { lock (sync) { text = value; cursor = selectionStart = value.Length; selectionLength = 0; version++; epoch++; } }
        internal void MutateWithoutVersion(string value) { lock (sync) { text = value; cursor = selectionStart = Math.Min(cursor, value.Length); selectionLength = 0; } }
        internal void SetReadOnly(bool value) { lock (sync) { readOnly = value; epoch++; } }
        internal void SetDiff(bool value) { lock (sync) { diff = value; epoch++; } }
        internal void SetClosed(bool value) { lock (sync) { closed = value; epoch++; } }
        internal void SetIme(bool value) { lock (sync) { ime = value; epoch++; } }
    }
}
