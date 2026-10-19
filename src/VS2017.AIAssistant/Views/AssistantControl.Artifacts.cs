using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VS2017.AIAssistant.Chat;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : AI 답변을 사용자가 선택한 새 문서·코드 파일로 내보내는 명시적 동작을 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK061 CLI 실제 파일 생성과 답변 원문 저장을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddArtifactActions
        /// desc         : AI 답변 메뉴에 전체 답변과 단일 코드 블록의 새 파일 만들기를 추가한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 열 때마다 현재 메시지 원문을 확인하며 사용자·시스템 메시지는 제외.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddArtifactActions(ContextMenu menu, MarkdownPreview preview)
        {
            Dispatcher.VerifyAccess();
            var all = new MenuItem { Header = "답변 전체를 새 파일로 만들기…", ToolTip = "Markdown·텍스트·HTML·Word(.docx)·코드 파일 · 기존 파일 보존" };
            var code = new MenuItem { Header = "코드 블록만 새 파일로 만들기…", ToolTip = "완결된 코드 블록이 하나인 답변에서만 사용 가능합니다." };
            all.Click += (sender, args) => { Dispatcher.VerifyAccess(); SaveAnswerArtifact(preview, false); };
            code.Click += (sender, args) => { Dispatcher.VerifyAccess(); SaveAnswerArtifact(preview, true); };
            menu.Items.Insert(1, all); menu.Items.Insert(2, code);
            menu.Opened += (sender, args) =>
            {
                Dispatcher.VerifyAccess();
                var message = preview.DataContext as ChatMessage;
                bool answer = !disposed && message != null && message.Role == "assistant";
                all.Visibility = code.Visibility = answer ? Visibility.Visible : Visibility.Collapsed;
                string body, extension;
                all.IsEnabled = answer && (preview.Markdown ?? "").Length <= AssistantArtifactWriter.MaxCharacters;
                code.IsEnabled = answer && AssistantArtifactWriter.TryExtractSingleCodeBlock(preview.Markdown, out body, out extension);
            };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SaveAnswerArtifact
        /// desc         : 원문을 고정한 후 저장 창에서 사용자가 고른 새 파일 하나만 생성한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 자동 저장·AI 경로 신뢰·실행 없음, 취소·실패 시 답변과 기존 파일 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SaveAnswerArtifact(MarkdownPreview preview, bool codeOnly)
        {
            Dispatcher.VerifyAccess();
            var message = preview.DataContext as ChatMessage;
            if (disposed || message == null || message.Role != "assistant") return;
            string content = preview.Markdown ?? "", extension = ".md";
            if (codeOnly && !AssistantArtifactWriter.TryExtractSingleCodeBlock(content, out content, out extension)) return;
            var dialog = new SaveFileDialog
            {
                Title = "답변으로 새 파일 만들기 · 기존 파일은 보존됩니다",
                Filter = AssistantArtifactWriter.SaveFilter, FilterIndex = codeOnly ? 5 : 1,
                FileName = codeOnly ? "AI-Code" + extension : "AI-Answer.md", DefaultExt = extension,
                AddExtension = true, OverwritePrompt = false, CheckPathExists = true, ValidateNames = true
            };
            try
            {
                // CLI가 이미 만든 파일을 추정·복사하지 않는다. 지금 선택한 답변 원문을 새 문서로 저장한다.
                bool confirmed = dialog.ShowDialog(Window.GetWindow(this)) == true;
                if (AssistantArtifactWriter.TryCreateNew(confirmed, dialog.FileName, content))
                    StatusLabel.Text = "새 파일을 만들었습니다: " + Path.GetFileName(dialog.FileName) + " · 파일을 자동 실행하지 않습니다.";
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException ||
                error is NotSupportedException || error is System.Security.SecurityException || error is System.Xml.XmlException)
            { StatusLabel.Text = "파일을 만들지 못했습니다. " + error.Message; }
        }
    }
}
