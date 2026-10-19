using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using VS2017.AIAssistant.Editing;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 답변 Markdown 링크와 공통 규칙·요청의 작성/미리보기를 명시적인 사용자 동작에 연결한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 이미지30·31·36의 원시 Markdown과 링크 표시 보완.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private AssistantDialog activeMarkdownDialog;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkdownLinkRequested
        /// desc         : 실제 답변에서 사용자가 누른 링크만 공통 탐색 경계로 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 내용 바인딩과 문서 렌더 중 자동 실행 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void MarkdownLinkRequested(object sender, MarkdownLinkEventArgs args)
        { Dispatcher.VerifyAccess(); NavigateMarkdownLink(args); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkdownPreviewLoaded
        /// desc         : 읽기 전용 Markdown 본문에 기존 복사·선택·설정 메뉴를 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 새 렌더러에서도 원래 텍스트 우클릭 기능 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void MarkdownPreviewLoaded(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess();
            var preview = sender as MarkdownPreview;
            if (preview != null && preview.TextView.ReadLocalValue(ContextMenuProperty) == DependencyProperty.UnsetValue)
            {
                var menu = CreateAssistantMenu(preview.TextView);
                var source = new MenuItem { Header = "Markdown 원문 복사", Icon = new TextBlock { Text = "M↓", FontWeight = FontWeights.Bold } };
                source.Click += (copySender, copyArgs) => { Dispatcher.VerifyAccess(); CopyMarkdownSource(preview.Markdown); };
                menu.Items.Insert(0, source); menu.Items.Insert(1, new Separator());
                AddArtifactActions(menu, preview);
                preview.TextView.ContextMenu = menu;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CopyMarkdownSource
        /// desc         : 사용자가 명시적으로 고른 답변의 서식·링크 원문을 클립보드로 복사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 렌더된 텍스트 선택 복사와 원문 복사를 함께 제공한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CopyMarkdownSource(string markdown)
        {
            Dispatcher.VerifyAccess();
            if (disposed) return;
            try { Clipboard.SetText(markdown ?? "", TextDataFormat.UnicodeText); }
            catch (Exception error) when (error is System.Runtime.InteropServices.ExternalException || error is System.Security.SecurityException)
            { StatusLabel.Text = "다른 프로그램이 클립보드를 사용 중입니다. 다시 복사하세요."; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigateMarkdownLink
        /// desc         : 웹 링크는 HTTP(S)만, 로컬 링크는 IDE 텍스트 탐색 서비스만 사용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : URI 자격정보·제어문자·UNC·명령 scheme과 자동 파일 실행 차단.
        ///                2026-10-19 CodexOrig : 명시적으로 연 Markdown의 상대 링크는 해당 문서 폴더를 우선 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void NavigateMarkdownLink(MarkdownLinkEventArgs args)
        {
            Dispatcher.VerifyAccess();
            if (args == null || disposed) return;
            // 문서 내부 제목 이동은 렌더러의 anchor 탐색에 맡기고 외부 링크로 소비하지 않는다.
            if ((args.Target ?? "").Trim().StartsWith("#", StringComparison.Ordinal)) return;
            args.Handled = true;
            try
            {
                string target = (args.Target ?? "").Trim();
                if (target.Length == 0 || target.Length > 4096) throw new InvalidOperationException("링크 길이를 확인하세요.");
                foreach (char value in target) if (char.IsControl(value)) throw new InvalidOperationException("링크에 제어 문자가 있습니다.");
                Uri address;
                if (Uri.TryCreate(target, UriKind.Absolute, out address) &&
                    (address.Scheme == Uri.UriSchemeHttps || address.Scheme == Uri.UriSchemeHttp))
                {
                    if (address.UserInfo.Length != 0) throw new InvalidOperationException("자격정보를 포함한 링크는 열 수 없습니다.");
                    Process.Start(new ProcessStartInfo { FileName = address.AbsoluteUri, UseShellExecute = true });
                    return;
                }
                if (!args.IsLocalFile || target.StartsWith(@"\\", StringComparison.Ordinal))
                    throw new InvalidOperationException("지원하지 않는 링크입니다.");
                target = (args.FilePath ?? "").Trim();
                if (target.Length == 0 || target.Length > 4096 ||
                    (target.Length > 1 && target[1] == ':' && (target.Length < 3 || (target[2] != '\\' && target[2] != '/'))))
                    throw new InvalidOperationException("완전한 로컬 소스 경로를 확인하세요.");
                Uri.TryCreate(target, UriKind.Absolute, out address);
                if (address != null && address.IsAbsoluteUri)
                {
                    if (!address.IsFile || address.IsUnc) throw new InvalidOperationException("로컬 소스 링크를 확인하세요.");
                    target = address.LocalPath;
                }
                else target = Uri.UnescapeDataString(target);
                var navigation = documents as ISourceNavigationService;
                if (navigation == null) throw new InvalidOperationException("IDE 소스 탐색 서비스를 사용할 수 없습니다.");
                string baseDirectory = args.DocumentBaseDirectory;
                if (!String.IsNullOrEmpty(baseDirectory) && (!Path.IsPathRooted(baseDirectory) || baseDirectory.StartsWith(@"\\", StringComparison.Ordinal)))
                    throw new InvalidOperationException("문서의 로컬 기준 폴더를 확인하세요.");
                string path = Path.IsPathRooted(target) ? Path.GetFullPath(target) : Path.GetFullPath(Path.Combine(String.IsNullOrEmpty(baseDirectory) ? navigation.GetProjectDirectory() : baseDirectory, target));
                if (path.StartsWith(@"\\", StringComparison.Ordinal) || !File.Exists(path))
                    throw new InvalidOperationException("로컬 소스 파일을 찾을 수 없습니다.");
                navigation.NavigateTo(path, Math.Max(1, Math.Min(1000000, args.Line)));
            }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is UnauthorizedAccessException || error is InvalidOperationException || error is FormatException || error is NotSupportedException || error is System.Runtime.InteropServices.COMException || error is System.ComponentModel.Win32Exception || error is System.Security.SecurityException)
            { StatusLabel.Text = "링크를 열지 못했습니다. 주소와 로컬 파일을 확인하세요. (" + error.GetType().Name + ")"; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RulesMarkdownClicked
        /// desc         : 사용자 공통 규칙의 원문을 보존한 채 별도 Markdown 작성 창을 연다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 필수 내장 규칙과 사용자 규칙 저장 경계 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RulesMarkdownClicked(object sender, RoutedEventArgs args)
        { Dispatcher.VerifyAccess(); OpenMarkdownEditor(RulesText, "✎ 공통 규칙 Markdown"); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PromptMarkdownClicked
        /// desc         : 현재 요청 초안을 작성·미리보기로 편집하며 요청은 자동 전송하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 기존 Enter·Ctrl+Enter와 IME 동작 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PromptMarkdownClicked(object sender, RoutedEventArgs args)
        { Dispatcher.VerifyAccess(); OpenMarkdownEditor(Instruction, "✎ 요청 Markdown"); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkdownDocumentClicked
        /// desc         : 요청과 공통 규칙의 길이 제한을 바꾸지 않고 독립 Markdown 문서 작성기를 연다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 새 문서·불러오기·UTF-8 저장과 변경 보호 기능 제공.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void MarkdownDocumentClicked(object sender, RoutedEventArgs args)
        { Dispatcher.VerifyAccess(); OpenMarkdownEditor(null, "✎ Markdown 문서 작성"); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenMarkdownEditor
        /// desc         : 적용을 누를 때만 원래 입력란을 변경하고 취소·창 종료는 초안을 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 기존 입력 길이·설정 저장·대화 전송을 우회하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OpenMarkdownEditor(TextBox target, string title)
        {
            Dispatcher.VerifyAccess();
            if (busy || disposed) return;
            if (activeMarkdownDialog != null) { activeMarkdownDialog.Activate(); return; }
            var editor = new MarkdownEditor { MaxLength = target == null ? 128 * 1024 : target.MaxLength > 0 ? target.MaxLength : 16000, Text = target == null ? "" : target.Text };
            editor.MarkSaved();
            editor.Preview.LinkRequested += MarkdownLinkRequested;
            var dialog = new AssistantDialog(this, title, target == null ? "Markdown 문서를 작성·미리보고 UTF-8로 저장하세요. 현재 요청과 공통 규칙은 별도로 유지합니다." : "작성과 미리보기를 전환하세요. ‘입력란에 적용’을 눌러야 원래 입력이 바뀝니다.");
            var targetOwner = Window.GetWindow(target ?? (DependencyObject)this);
            if (targetOwner != null) dialog.Owner = targetOwner;
            dialog.MinWidth = Math.Max(240, Math.Min(640, SystemParameters.WorkArea.Width - 32));
            dialog.MinHeight = Math.Max(220, Math.Min(480, SystemParameters.WorkArea.Height - 32));
            dialog.Width = Math.Max(dialog.MinWidth, Math.Min(1040, SystemParameters.WorkArea.Width - 48));
            dialog.Height = Math.Max(dialog.MinHeight, Math.Min(780, SystemParameters.WorkArea.Height - 48));
            dialog.Body.Content = editor;
            var apply = new Button { Content = "✓ 입력란에 적용", Style = (Style)FindResource("PrimaryButton"), Margin = new Thickness(0, 0, 8, 0) };
            apply.Click += (sender, args) =>
            {
                if (disposed || target == null || (target.MaxLength > 0 && editor.Text.Length > target.MaxLength)) return;
                target.Text = editor.Text; target.CaretIndex = target.Text.Length; editor.MarkSaved(); dialog.Close(); target.Focus();
            };
            if (target != null) dialog.Actions.Children.Insert(0, apply);
            dialog.Closing += (sender, args) => { if (!disposed && !editor.ConfirmClose(() => dialog.Close())) args.Cancel = true; };
            activeMarkdownDialog = dialog;
            try { dialog.ShowDialog(); }
            finally { activeMarkdownDialog = null; editor.Preview.LinkRequested -= MarkdownLinkRequested; editor.Dispose(); }
        }
    }
}
