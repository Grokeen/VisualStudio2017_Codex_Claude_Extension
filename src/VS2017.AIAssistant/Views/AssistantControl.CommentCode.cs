using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using CommentCodePreview.Poc;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Services;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 사용자의 수동 주석 요청과 전송 동의를 실제 읽기 전용 CLI 및 명시 수락 미리보기에 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private PreviewSession commentCodeSession;
        private CommentCodeEditorAdapter commentCodeEditor;
        private CommentCodePreviewDialog commentCodeDialog;
        private CancellationTokenSource commentCodeCancellation;
        private bool commentCodeRequesting;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddCommentCodeActions
        /// desc         : 자동 트리거 없이 수동 요청·취소·직전 제안 Undo 명령만 메뉴에 추가한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddCommentCodeActions(ContextMenu menu)
        {
            Dispatcher.VerifyAccess();
            var request = new MenuItem { Header = "주석으로 코드 제안 요청…", ToolTip = "열린 C# 편집기의 현재 위치 · 실제 AI 전송 동의 후 미리보기" };
            var cancel = new MenuItem { Header = "주석 코드 요청 취소" };
            var undo = new MenuItem { Header = "직전 주석 코드 제안 되돌리기" };
            request.Click += CommentCodeRequested; cancel.Click += CommentCodeCancelled; undo.Click += CommentCodeUndo;
            menu.Opened += (sender, args) => {
                Dispatcher.VerifyAccess(); request.IsEnabled = !disposed && !busy && !commentCodeRequesting && commentCodeDialog == null;
                cancel.IsEnabled = !disposed && commentCodeRequesting;
                undo.IsEnabled = !disposed && commentCodeSession != null && commentCodeSession.GetStatus().CanUndo;
            };
            menu.Items.Add(new Separator()); menu.Items.Add(request); menu.Items.Add(cancel); menu.Items.Add(undo);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CommentCodeRequested
        /// desc         : 실제 문맥을 먼저 고정하고 명시 동의 후 공급자를 호출하며 기존 대화 초안은 비우지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100", Justification = "WPF Click async void event; cancellation and errors are caught and reported without source text.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD109", Justification = "WPF Click begins on the Dispatcher and explicitly verifies access; continuation captures its UI context.")]
        private async void CommentCodeRequested(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess();
            if (disposed || busy || commentCodeRequesting || commentCodeDialog != null) return;
            DisposeCommentCode();
            bool requestAgain = false;
            try
            {
                commentCodeEditor = new CommentCodeEditorAdapter(Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider, Dispatcher);
                EditorSnapshot snapshot = commentCodeEditor.Capture();
                if (snapshot.BlockReason != PreviewReason.None) { StatusLabel.Text = "현재 편집기 상태에서는 요청할 수 없습니다: " + snapshot.BlockReason; return; }
                string explicitComment = ExtractExplicitSourceComment(snapshot);
                if (String.IsNullOrWhiteSpace(explicitComment) || explicitComment.Length > PreviewLimits.InstructionCharacters ||
                    System.Text.Encoding.UTF8.GetByteCount(explicitComment) > PreviewLimits.InstructionUtf8Bytes)
                { StatusLabel.Text = "열린 C# 파일의 주석(2,048자/8KB 이하)을 선택하거나 현재·직전 // 주석 줄에 커서를 두세요. 대화 초안은 유지했습니다."; return; }
                string requestedProvider = selectedProvider, requestedExecutable = ExecutablePath.Text, requestedModel = ModelName.Text;
                string disclosure = requestedProvider + " / 모델: " + (String.IsNullOrWhiteSpace(requestedModel) ? "CLI 기본 모델" : requestedModel) +
                    "\n파일 이름, 현재 C# 버퍼 전체(" + snapshot.Content.Length + "자), 명시한 주석과 기본 안전 지침을 AI에 전송합니다. " +
                    "선택한 내용은 참고 문맥으로 보존하고 현재 커서에 코드를 삽입합니다. 대화 기록·첨부는 보내지 않습니다. CLI 인증 및 서비스 요금이 적용될 수 있습니다.\n" +
                    "빈 임시 폴더의 읽기 전용 제안 경로를 사용합니다. 수락 전 소스 변경, 자동 저장 및 제안 코드 실행은 하지 않습니다.\n" +
                    "확장은 CLI 도구·설정 hook을 억제하지만 조직 관리 정책에 따른 CLI 부가 동작까지 통제하지는 않습니다. 지원하지 않는 CLI 옵션이나 요청 거절 시 다른 공급자·모델·전체 권한으로 자동 전환하지 않습니다.\n\n전송하여 코드 제안을 요청할까요?";
                if (MessageBox.Show(Window.GetWindow(this), disclosure, "주석 코드 제안 · 전송 범위 확인", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
                // 동의 창을 열고 닫는 동안 바뀐 문서/커서를 새로 캡처하여 승인 범위를 넓히지 않는다.
                if (!snapshot.Matches(commentCodeEditor.Capture())) { StatusLabel.Text = "동의 중 편집기 문맥이 바뀌었습니다. 다시 요청하세요."; return; }
                commentCodeSession = new PreviewSession(new CommentCodeProviderAdapter(requestedProvider, requestedExecutable, commentCodeEditor.FilePath, requestedModel));
                commentCodeEditor.ContextChanged += CommentCodeContextChanged;
                commentCodeCancellation = new CancellationTokenSource(); commentCodeRequesting = true;
                StatusLabel.Text = "실제 " + requestedProvider + " CLI에 코드 제안을 요청 중입니다. 요청 취소 메뉴를 사용할 수 있습니다.";
                PreviewRequestResult result = await commentCodeSession.RequestAsync(commentCodeEditor, explicitComment, commentCodeCancellation.Token);
                Dispatcher.VerifyAccess();
                if (disposed || commentCodeSession == null) return;
                CodePreview preview = commentCodeSession.GetPreview();
                if (result.Outcome != RequestOutcome.Ready || preview == null) { StatusLabel.Text = "코드 제안을 적용하지 않았습니다: " + result.Reason; return; }
                long requestId = preview.RequestId;
                commentCodeDialog = new CommentCodePreviewDialog(this, BuildCommentCodePresentation(snapshot, commentCodeEditor.FilePath, preview.Text),
                    () => AcceptCommentCode(requestId), () => { Dispatcher.VerifyAccess(); if (commentCodeSession != null) commentCodeSession.Cancel(); },
                    () => { Dispatcher.VerifyAccess(); requestAgain = true; });
                commentCodeDialog.ShowDialog(); commentCodeDialog = null;
            }
            catch (OperationCanceledException) { StatusLabel.Text = "주석 코드 요청을 취소했습니다. 원문과 초안은 유지했습니다."; }
            catch (Exception) { StatusLabel.Text = "코드 제안 요청을 완료하지 못했습니다. CLI 연결과 일반 C# 편집기·크기·선택 상태를 확인하세요. 원문과 초안은 유지했습니다."; }
            finally
            {
                Dispatcher.VerifyAccess(); commentCodeRequesting = false;
                if (commentCodeCancellation != null) { commentCodeCancellation.Dispose(); commentCodeCancellation = null; }
                commentCodeDialog = null;
                if (commentCodeSession == null || !commentCodeSession.GetStatus().CanUndo) DisposeCommentCode();
            }
            if (requestAgain && !disposed) CommentCodeRequested(sender, args);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AcceptCommentCode / CommentCodeUndo
        /// desc         : 원자 editor 검증 결과만 UI에 반영하고 원문을 새 문자열로 덮어쓰는 Undo는 제공하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private CommentCodePreviewUiAcceptResult AcceptCommentCode(long id)
        {
            Dispatcher.VerifyAccess();
            if (commentCodeSession != null && commentCodeSession.Accept(id))
            { StatusLabel.Text = "제안을 현재 버퍼에 반영했습니다. 저장은 하지 않았습니다. 편집기 Undo 한 번으로 되돌릴 수 있습니다."; return CommentCodePreviewUiAcceptResult.Applied(); }
            return CommentCodePreviewUiAcceptResult.Stale("현재 문서·버전·선택·커서·편집 가능 상태가 요청 시점과 일치하지 않습니다.");
        }

        private void CommentCodeUndo(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess();
            bool undone = commentCodeSession != null && commentCodeSession.Undo(commentCodeSession.GetStatus().RequestId);
            StatusLabel.Text = undone ? "주석 코드 제안을 Undo 한 번으로 되돌렸습니다. 저장은 하지 않았습니다." : "이후 편집 또는 문맥 변화가 있어 전용 Undo를 실행하지 않았습니다. 현재 원문은 유지했습니다.";
        }

        private void CommentCodeCancelled(object sender, RoutedEventArgs args)
        { Dispatcher.VerifyAccess(); if (commentCodeSession != null) commentCodeSession.Cancel(); if (commentCodeCancellation != null) commentCodeCancellation.Cancel(); }

        private void CommentCodeContextChanged(object sender, EventArgs args)
        {
            Dispatcher.VerifyAccess();
            if (commentCodeSession != null) commentCodeSession.NotifyContextChanged();
            if (commentCodeDialog != null) commentCodeDialog.SetStale("요청 뒤 편집기 상태가 변경되었습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ExtractExplicitSourceComment
        /// desc         : 대화 초안을 요청으로 바꾸지 않고 선택한 주석 또는 현재·빈 줄 직전의 // 주석만 읽는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string ExtractExplicitSourceComment(EditorSnapshot source)
        {
            if (source.SelectionLength > 0)
            {
                string selected = source.Content.Substring(source.SelectionStart, source.SelectionLength);
                string trimmed = selected.TrimStart();
                return trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("/*", StringComparison.Ordinal) ? selected : "";
            }
            int start = source.Cursor == 0 ? 0 : source.Content.LastIndexOf('\n', source.Cursor - 1) + 1;
            int end = source.Content.IndexOf('\n', source.Cursor); if (end < 0) end = source.Content.Length;
            string current = source.Content.Substring(start, end - start).TrimEnd('\r');
            if (current.TrimStart().StartsWith("//", StringComparison.Ordinal)) return current;
            if (!String.IsNullOrWhiteSpace(current) || start == 0) return "";
            int previousEnd = start - 1;
            int previousStart = previousEnd == 0 ? 0 : source.Content.LastIndexOf('\n', previousEnd - 1) + 1;
            string previous = source.Content.Substring(previousStart, previousEnd - previousStart).TrimEnd('\r');
            return previous.TrimStart().StartsWith("//", StringComparison.Ordinal) ? previous : "";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BuildCommentCodePresentation
        /// desc         : UTF-16 원본 위치를 1기준 줄·열로 변환하고 제한된 앞뒤 문맥을 변경 없이 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static CommentCodePreviewUiPresentation BuildCommentCodePresentation(EditorSnapshot source, string path, string insertion)
        {
            int start = source.Cursor;
            int line = 1, column = 1;
            for (int index = 0; index < start; index++)
            {
                if (source.Content[index] == '\r') { if (index + 1 < start && source.Content[index + 1] == '\n') index++; line++; column = 1; }
                else if (source.Content[index] == '\n') { line++; column = 1; }
                else column++;
            }
            int before = Math.Max(0, start - 1000), after = Math.Min(source.Content.Length, start + 1000);
            if (!TextPolicy.IsBoundary(source.Content, before)) before++;
            if (!TextPolicy.IsBoundary(source.Content, after)) after--;
            return new CommentCodePreviewUiPresentation(path, line, column, source.Content.Substring(before, start - before),
                "", insertion, source.Content.Substring(start, after - start));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeCommentCode
        /// desc         : 자기 요청과 이벤트만 취소·해제하고 사용자 문서나 디스크를 변경하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeCommentCode()
        {
            Dispatcher.VerifyAccess();
            var dialog = commentCodeDialog; commentCodeDialog = null;
            if (commentCodeSession != null) { commentCodeSession.Dispose(); commentCodeSession = null; }
            if (commentCodeCancellation != null) { commentCodeCancellation.Cancel(); commentCodeCancellation.Dispose(); commentCodeCancellation = null; }
            if (commentCodeEditor != null) { commentCodeEditor.ContextChanged -= CommentCodeContextChanged; commentCodeEditor.Dispose(); commentCodeEditor = null; }
            if (dialog != null) dialog.Close();
        }
    }
}
