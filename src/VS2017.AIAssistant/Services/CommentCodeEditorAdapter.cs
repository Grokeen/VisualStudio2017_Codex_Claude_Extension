using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using CommentCodePreview.Poc;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VS2017.AIAssistant.Services
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CommentCodeEditorAdapter
    /// desc         : 실제 VS 텍스트 버전과 선택·커서를 고정하고 커서 삽입 한 트랜잭션 및 조건부 Undo만 제공한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class CommentCodeEditorAdapter : IAtomicPreviewEditor, IDisposable
    {
        private readonly Dispatcher dispatcher;
        private readonly IVsTextManager manager;
        private readonly IVsTextView nativeView;
        private readonly IWpfTextView view;
        private readonly ITextUndoHistory history;
        private readonly WindowEvents windows;
        private readonly DocumentEvents documents;
        private readonly Document document;
        private readonly string identity = Guid.NewGuid().ToString("N");
        private long epoch;
        private bool disposed, ownEdit, imeActive;
        internal event EventHandler ContextChanged;
        internal string FilePath { get; private set; }

        private sealed class Receipt : IUndoReceipt
        {
            internal CommentCodeEditorAdapter Owner;
            internal string AfterContent;
            internal long AfterVersion, AfterEpoch;
            internal ITextUndoTransaction Transaction;
            internal bool Used;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CommentCodeEditorAdapter
        /// desc         : 현재 열린 C# 일반 문서의 실제 WPF view·버퍼·Undo history를 확인하며 projection과 가상 선택은 거절한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        internal CommentCodeEditorAdapter(IServiceProvider services, Dispatcher dispatcher)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (services == null || dispatcher == null) throw new ArgumentNullException(nameof(services));
            dispatcher.VerifyAccess(); this.dispatcher = dispatcher;
            var textManager = services.GetService(typeof(SVsTextManager)) as IVsTextManager;
            if (textManager == null) throw new InvalidOperationException("텍스트 관리자 서비스를 확인할 수 없습니다.");
            manager = textManager;
            var components = services.GetService(typeof(SComponentModel)) as IComponentModel;
            if (components == null) throw new InvalidOperationException("편집기 구성 서비스를 확인할 수 없습니다.");
            ErrorHandler.ThrowOnFailure(manager.GetActiveView(0, null, out nativeView));
            var adapters = components.GetService<IVsEditorAdaptersFactoryService>();
            if (adapters == null) throw new InvalidOperationException("편집기 어댑터 서비스를 확인할 수 없습니다.");
            view = adapters.GetWpfTextView(nativeView);
            var dte = services.GetService(typeof(DTE)) as DTE;
            if (dte == null) throw new InvalidOperationException("자동화 문서 서비스를 확인할 수 없습니다.");
            document = dte.ActiveDocument;
            if (view == null || document == null || !view.TextBuffer.ContentType.IsOfType("CSharp") ||
                !view.Roles.Contains(PredefinedTextViewRoles.Document) || view.TextBuffer != view.TextDataModel.DocumentBuffer)
                throw new InvalidOperationException("열린 일반 C# 소스 편집기만 지원합니다. diff·projection 화면은 지원하지 않습니다.");
            FilePath = document.FullName;
            var registry = components.GetService<ITextUndoHistoryRegistry>();
            if (registry == null || !registry.TryGetHistory(view.TextBuffer, out history))
                throw new InvalidOperationException("문서의 Undo history를 확인할 수 없습니다.");
            windows = dte.Events.WindowEvents; documents = dte.Events.DocumentEvents;
            windows.WindowActivated += WindowActivated; documents.DocumentClosing += DocumentClosing;
            view.Caret.PositionChanged += CaretChanged; view.Selection.SelectionChanged += SelectionChanged;
            view.Closed += ViewClosed; view.TextBuffer.Changed += BufferChanged;
            view.TextBuffer.ReadOnlyRegionsChanged += ReadOnlyChanged;
            TextCompositionManager.AddPreviewTextInputStartHandler(view.VisualElement, CompositionStarted);
            TextCompositionManager.AddPreviewTextInputUpdateHandler(view.VisualElement, CompositionStarted);
            TextCompositionManager.AddPreviewTextInputHandler(view.VisualElement, CompositionEnded);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Capture / CaptureOnUI
        /// desc         : worker 캡처를 IDE JTF로 UI에 직렬화하며 hash 대신 실제 ITextVersion.VersionNumber를 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// update date  : 2026-11-07 CodexOrig : strict SDK 분석에 따라 Dispatcher.Invoke 대신 JTF의 실제 UI 전환을 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public EditorSnapshot Capture()
        {
            if (!dispatcher.CheckAccess())
            {
                return ThreadHelper.JoinableTaskFactory.Run(async () => {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return CaptureOnUI();
                });
            }
            dispatcher.VerifyAccess();
            return CaptureOnUI();
        }

        private EditorSnapshot CaptureOnUI()
        {
            ThreadHelper.ThrowIfNotOnUIThread(); dispatcher.VerifyAccess();
            if (disposed || view.IsClosed) throw new SnapshotRejectedException(PreviewReason.Closed);
            IVsTextView active; ErrorHandler.ThrowOnFailure(manager.GetActiveView(0, null, out active));
            if (!Object.ReferenceEquals(active, nativeView)) throw new SnapshotRejectedException(PreviewReason.ContextChanged);
            if (view.Caret.Position.VirtualBufferPosition.IsInVirtualSpace || view.Selection.Mode != TextSelectionMode.Stream ||
                view.Selection.Start.IsInVirtualSpace || view.Selection.End.IsInVirtualSpace ||
                (view.Selection.IsEmpty ? view.Selection.SelectedSpans.Count > 1 : view.Selection.SelectedSpans.Count != 1))
                throw new SnapshotRejectedException(PreviewReason.InvalidContext);
            ITextSnapshot current = view.TextBuffer.CurrentSnapshot;
            if (current.Length > PreviewLimits.ContextCharacters) throw new SnapshotRejectedException(PreviewReason.Oversize);
            var selected = view.Selection.StreamSelectionSpan.SnapshotSpan;
            bool diff = !view.Roles.Contains(PredefinedTextViewRoles.Editable) || view.Roles.Contains("DIFF") ||
                view.Roles.Contains("LEFTDIFF") || view.Roles.Contains("RIGHTDIFF") || view.Roles.Contains("INLINEDIFF") ||
                view.TextBuffer != view.TextDataModel.DocumentBuffer;
            bool readOnly = document.ReadOnly || !view.TextBuffer.CheckEditAccess() || view.TextBuffer.IsReadOnly(new Span(0, current.Length));
            bool composing = imeActive || ReadImeState();
            return new EditorSnapshot(identity, current.Version.VersionNumber, epoch, current.GetText(),
                view.Caret.Position.BufferPosition.Position, selected.Start.Position, selected.Length, readOnly, diff, false, composing);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CompareAndApply
        /// desc         : 같은 UI 임계구역에서 스냅샷·취소를 재검사한 뒤 커서에만 삽입하고 선택 원문과 미저장 변경을 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public AtomicApplyResult CompareAndApply(EditorSnapshot expected, string insertion, CancellationToken cancellationToken)
        {
            ThreadHelper.ThrowIfNotOnUIThread(); dispatcher.VerifyAccess();
            if (cancellationToken.IsCancellationRequested) return AtomicApplyResult.Rejected(PreviewReason.Cancelled);
            EditorSnapshot before = CaptureOnUI();
            if (before.BlockReason != PreviewReason.None) return AtomicApplyResult.Rejected(before.BlockReason);
            if (expected == null || !expected.Matches(before)) return AtomicApplyResult.Rejected(PreviewReason.ContextChanged);
            if (history.CurrentTransaction != null || view.TextBuffer.EditInProgress) return AtomicApplyResult.Rejected(PreviewReason.Busy);
            int start = before.Cursor;
            string postimage = before.Content.Substring(0, start) + insertion + before.Content.Substring(start);
            // commit 후 한도 예외로 receipt가 유실되지 않도록 완성 버퍼의 모든 경계를 먼저 검사한다.
            try { new EditorSnapshot(identity, before.Version, epoch, postimage, start + insertion.Length, start, insertion.Length, false, false, false, false); }
            catch (SnapshotRejectedException error) { return AtomicApplyResult.Rejected(error.Reason); }
            var receipt = new Receipt { Owner = this, AfterContent = postimage, AfterEpoch = epoch };
            ownEdit = true;
            try
            {
                using (var transaction = history.CreateTransaction("AI 주석 코드 제안 수락"))
                using (var edit = view.TextBuffer.CreateEdit())
                {
                    if (cancellationToken.IsCancellationRequested) { edit.Cancel(); transaction.Cancel(); return AtomicApplyResult.Rejected(PreviewReason.Cancelled); }
                    if (!edit.Replace(new Span(start, 0), insertion)) { edit.Cancel(); transaction.Cancel(); return AtomicApplyResult.Rejected(PreviewReason.ReadOnly); }
                    ITextSnapshot applied = edit.Apply();
                    if (edit.Canceled) { transaction.Cancel(); return AtomicApplyResult.Rejected(PreviewReason.EditorError); }
                    receipt.AfterVersion = applied.Version.VersionNumber; receipt.Transaction = transaction;
                    transaction.Complete();
                    return AtomicApplyResult.Success(receipt);
                }
            }
            finally { ownEdit = false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CompareAndUndo
        /// desc         : 적용 직후 버전·문맥 및 최상위 transaction이 같은 경우에만 실제 Undo(1)를 호출한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public AtomicUndoResult CompareAndUndo(IUndoReceipt receipt)
        {
            ThreadHelper.ThrowIfNotOnUIThread(); dispatcher.VerifyAccess();
            var own = receipt as Receipt;
            if (own == null || own.Owner != this || own.Used || history.CurrentTransaction != null || !history.CanUndo ||
                !Object.ReferenceEquals(history.LastUndoTransaction, own.Transaction)) return AtomicUndoResult.Rejected(PreviewReason.UndoConflict);
            var current = CaptureOnUI();
            if (current.BlockReason != PreviewReason.None || current.DocumentId != identity || current.ContextEpoch != own.AfterEpoch ||
                current.Version != own.AfterVersion || current.Content != own.AfterContent) return AtomicUndoResult.Rejected(PreviewReason.UndoConflict);
            ownEdit = true;
            try { history.Undo(1); own.Used = true; return AtomicUndoResult.Success(); }
            finally { ownEdit = false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadImeState
        /// desc         : 조합 문자열을 읽지 않고 native 조합 길이만 검사하며 HWND 확인 실패는 적용 거절로 처리한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool ReadImeState()
        {
            dispatcher.VerifyAccess();
            var source = System.Windows.PresentationSource.FromVisual(view.VisualElement) as HwndSource;
            if (source == null || source.Handle == IntPtr.Zero) throw new SnapshotRejectedException(PreviewReason.InvalidContext);
            IntPtr context = ImmGetContext(source.Handle);
            if (context == IntPtr.Zero) return false;
            try { int length = ImmGetCompositionString(context, 8, IntPtr.Zero, 0); if (length < 0) throw new SnapshotRejectedException(PreviewReason.InvalidContext); return length > 0; }
            finally { ImmReleaseContext(source.Handle, context); }
        }

        [DllImport("imm32.dll", EntryPoint = "ImmGetCompositionStringW")] private static extern int ImmGetCompositionString(IntPtr context, uint index, IntPtr output, uint length);
        [DllImport("imm32.dll")] private static extern IntPtr ImmGetContext(IntPtr hwnd);
        [DllImport("imm32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr context);

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Changed / native event handlers
        /// desc         : 탭·커서·선택·내용·읽기 전용·IME 변화의 epoch를 증가시키며 자기 적용·Undo는 session에 재진입하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Changed() { dispatcher.VerifyAccess(); if (disposed || ownEdit) return; epoch++; var handler = ContextChanged; if (handler != null) handler(this, EventArgs.Empty); }
        private void WindowActivated(Window gotFocus, Window lostFocus) { ThreadHelper.ThrowIfNotOnUIThread(); if (gotFocus != null && gotFocus.Document != null) Changed(); }
        private void DocumentClosing(Document closing) { ThreadHelper.ThrowIfNotOnUIThread(); if (Object.ReferenceEquals(closing, document)) Changed(); }
        private void CaretChanged(object sender, CaretPositionChangedEventArgs args) { dispatcher.VerifyAccess(); Changed(); }
        private void SelectionChanged(object sender, EventArgs args) { dispatcher.VerifyAccess(); Changed(); }
        private void ViewClosed(object sender, EventArgs args) { dispatcher.VerifyAccess(); Changed(); }
        private void BufferChanged(object sender, TextContentChangedEventArgs args) { dispatcher.VerifyAccess(); Changed(); }
        private void ReadOnlyChanged(object sender, SnapshotSpanEventArgs args) { dispatcher.VerifyAccess(); Changed(); }
        private void CompositionStarted(object sender, TextCompositionEventArgs args) { dispatcher.VerifyAccess(); imeActive = true; Changed(); }
        private void CompositionEnded(object sender, TextCompositionEventArgs args) { dispatcher.VerifyAccess(); imeActive = false; Changed(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 소유한 view와 DTE 이벤트만 해제하며 버퍼·파일·IDE 실행 상태는 변경하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread(); dispatcher.VerifyAccess(); if (disposed) return; disposed = true;
            windows.WindowActivated -= WindowActivated; documents.DocumentClosing -= DocumentClosing;
            view.Caret.PositionChanged -= CaretChanged; view.Selection.SelectionChanged -= SelectionChanged; view.Closed -= ViewClosed;
            view.TextBuffer.Changed -= BufferChanged; view.TextBuffer.ReadOnlyRegionsChanged -= ReadOnlyChanged;
            TextCompositionManager.RemovePreviewTextInputStartHandler(view.VisualElement, CompositionStarted);
            TextCompositionManager.RemovePreviewTextInputUpdateHandler(view.VisualElement, CompositionStarted);
            TextCompositionManager.RemovePreviewTextInputHandler(view.VisualElement, CompositionEnded); ContextChanged = null;
        }
    }
}
