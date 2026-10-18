using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Chat;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 현재 파일의 AI 수정안 생성·비교·승인·적용·저장 화면.
    /// author       : CodexCode
    /// create date  : 2026-10-17
    /// update date  : 2026-10-18 CodexCode : 실제 단일 파일 편집 워크플로와 취소 기능 연결.
    /// update date  : 2026-10-18 CodexOrig : Chat 탭·누적 대화·첨부·영속 설정 연결.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl : UserControl, IDisposable
    {
        private IActiveDocumentService documents;
        private IAiEditProvider provider;
        private IEditorDocument pendingDocument;
        private EditProposal pendingProposal;
        private readonly SourceEditSession session = new SourceEditSession();
        private readonly Dictionary<string, string> executablePaths = new Dictionary<string, string>();
        private string selectedProvider = "Codex";
        private CancellationTokenSource requestCancellation;
        private bool busy;
        private bool disposed;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantControl
        /// desc         : 화면을 로드하고 IDE의 동적 테마 리소스를 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-17
        /// update date  : 2026-10-18 CodexCode : 요청·미리보기 입력란의 테마 처리.
        /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
        /// </summary>
        /// ---------------------------------------------------------------------
        public AssistantControl()
        {
            InitializeComponent();
            SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
            SetResourceReference(ForegroundProperty, VsBrushes.ToolWindowTextKey);
            foreach (Control control in new Control[] { Instruction, ExecutablePath, DiffPreview, OriginalPreview, ProposedPreview })
            {
                control.SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
                control.SetResourceReference(ForegroundProperty, VsBrushes.ToolWindowTextKey);
            }
            InitializeChat();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Initialize
        /// desc         : 도구 창이 생성된 뒤 IDE 문서와 AI Provider 서비스를 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 서비스 주입과 초기 버튼 상태 설정.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Initialize(IActiveDocumentService documentService, IAiEditProvider editProvider)
        {
            documents = documentService ?? throw new ArgumentNullException(nameof(documentService));
            provider = editProvider ?? throw new ArgumentNullException(nameof(editProvider));
            UpdateButtons();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GenerateClicked
        /// desc         : 활성 문서를 고정하고 비동기 AI 수정안을 받아 비교 화면에 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 요청 중 UI 유지 및 실패·취소 처리.
        /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async void GenerateClicked(object sender, RoutedEventArgs e)
        {
            if (busy || documents == null || disposed) return;
            ClearProposal();
            ChatSession chat = ActiveChat;
            try
            {
                if (string.IsNullOrWhiteSpace(Instruction.Text)) throw new InvalidOperationException("수정 요청을 입력하세요.");
                pendingDocument = documents.GetActiveDocument();
                DocumentSnapshot source = pendingDocument.Capture();
                TargetLabel.Text = "대상: " + source.FilePath + (source.HasUnsavedChanges ? " (미저장 편집 내용 포함)" : "");
                OriginalPreview.Text = source.Content;
                var context = new ChatRequestContext(RulesText.Text, ModelName.Text, chat.Messages.ToArray(), chat.Attachments.ToArray());
                chat.Add("user", Instruction.Text + (context.Attachments.Length == 0 ? "" : "\n첨부: " + string.Join(", ", context.Attachments.Select(a => a.Name))));
                ConversationScroll.ScrollToEnd();
                busy = true;
                requestCancellation = new CancellationTokenSource();
                StatusLabel.Text = selectedProvider + " 수정안 생성 중…";
                UpdateButtons();
                var contextProvider = provider as IAiContextEditProvider;
                if (contextProvider == null && (context.Attachments.Length > 0 || context.Instructions.Length > 0 || context.History.Length > 0))
                    throw new InvalidOperationException("이 Provider는 대화 컨텍스트를 지원하지 않습니다.");
                EditProposal proposal = contextProvider == null
                    ? await provider.GenerateAsync(selectedProvider, ExecutablePath.Text, source, Instruction.Text, requestCancellation.Token)
                    : await contextProvider.GenerateWithContextAsync(selectedProvider, ExecutablePath.Text, source, Instruction.Text, context, requestCancellation.Token);
                if (disposed) return;
                source.RequireUnchanged(pendingDocument.Capture());
                pendingProposal = proposal;
                chat.Add("assistant", proposal.Summary);
                ConversationScroll.ScrollToEnd();
                ProposedPreview.Text = proposal.ProposedText;
                DiffPreview.Text = proposal.ChangePreview;
                StatusLabel.Text = proposal.Summary + (proposal.HasChanges ? "\n변경 비교 후 적용을 승인하세요." : "\n적용할 변경이 없습니다.");
                ReviewTabs.SelectedIndex = 0;
                ReviewPanel.IsExpanded = proposal.HasChanges;
            }
            catch (OperationCanceledException) { if (!disposed) { StatusLabel.Text = "요청을 중지했습니다. 소스는 변경하지 않았습니다."; chat.Add("system", StatusLabel.Text); } }
            catch (Exception exception) { if (!disposed) { StatusLabel.Text = "요청 실패: " + exception.Message; chat.Add("system", StatusLabel.Text); } }
            finally
            {
                busy = false;
                if (requestCancellation != null) { requestCancellation.Dispose(); requestCancellation = null; }
                if (!disposed) UpdateButtons();
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApplyClicked
        /// desc         : 화면의 승인 상태와 원본 일치를 검증한 뒤 현재 파일 버퍼에 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 승인 전·처리 중 적용 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ApplyClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            try
            {
                session.Apply(pendingDocument, pendingProposal, ReviewApproval.IsChecked == true);
                pendingProposal = null;
                ReviewApproval.IsChecked = false;
                StatusLabel.Text = "편집기에 적용했습니다. 확인 후 '적용 내용 저장' 또는 Ctrl+S로 저장하세요.";
            }
            catch (Exception exception) { StatusLabel.Text = "적용 실패: " + exception.Message; }
            UpdateButtons();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SaveClicked
        /// desc         : 사용자가 명시적으로 요청한 경우만 IDE 저장 기능을 호출한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 저장 전 이후 편집·외부 변경 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            try { session.Save(); StatusLabel.Text = "적용 내용을 저장했습니다."; }
            catch (Exception exception) { StatusLabel.Text = "저장 실패: " + exception.Message; }
            UpdateButtons();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RestoreClicked
        /// desc         : 직전 적용 이후 편집이 없을 때 원래 버퍼를 복구하며 저장은 별도로 둔다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 승인된 복구 버튼과 충돌 처리 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RestoreClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            try
            {
                session.Restore(true);
                ClearProposal();
                StatusLabel.Text = "직전 적용 전의 편집 내용으로 복구했습니다. 저장하려면 편집기에서 Ctrl+S를 누르세요.";
            }
            catch (Exception exception) { StatusLabel.Text = "복구 실패: " + exception.Message + " 필요하면 편집기의 실행 취소(Ctrl+Z)를 사용하세요."; }
            UpdateButtons();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StopClicked
        /// desc         : 현재 CLI 요청과 하위 프로세스의 취소를 요청한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 취소 요청 후 중복 클릭 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void StopClicked(object sender, RoutedEventArgs e)
        {
            if (requestCancellation != null) requestCancellation.Cancel();
            StopButton.IsEnabled = false;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProviderChanged
        /// desc         : Provider별 실행 파일 경로를 창 수명 동안 구분하고 이전 승인을 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 재시작 없는 Provider 전환 구현.
        /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ProviderChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExecutablePath == null || ReviewApproval == null) return;
            executablePaths[selectedProvider] = ExecutablePath.Text;
            SaveDraft();
            selectedProvider = Convert.ToString(ProviderChoice.SelectedValue);
            string path;
            ExecutablePath.Text = executablePaths.TryGetValue(selectedProvider, out path) ? path : "";
            ShowChat();
            ClearProposal();
            UpdateButtons();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BrowseClicked
        /// desc         : 사용자가 네이티브 CLI exe를 선택하도록 파일 대화 상자를 연다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 공백 포함 실행 경로 지원.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void BrowseClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Windows 실행 파일 (*.exe)|*.exe", CheckFileExists = true, Multiselect = false };
            if (dialog.ShowDialog() == true) ExecutablePath.Text = dialog.FileName;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApprovalChanged
        /// desc         : 승인 체크 변경을 적용 버튼 상태에 반영한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 미승인 적용 버튼 비활성화.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ApprovalChanged(object sender, RoutedEventArgs e) { if (ApplyButton != null) UpdateButtons(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ClearProposal
        /// desc         : 새 요청·Provider 전환 시 이전 수정안과 승인을 제거한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 수정안 간 승인 재사용 방지.
        /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ClearProposal()
        {
            pendingProposal = null;
            pendingDocument = null;
            ReviewApproval.IsChecked = false;
            DiffPreview.Clear();
            ProposedPreview.Clear();
            if (ReviewPanel != null) ReviewPanel.IsExpanded = false;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateButtons
        /// desc         : 처리 중 재진입, 서비스 미연결, 미승인 적용을 UI에서도 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 생성·중지·적용·저장·복구 상태 반영.
        /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
        /// update date  : 2026-10-18 CodexOrig : 설정 팝업·안내 진입 버튼도 작업 중 비활성화.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateButtons()
        {
            GenerateButton.IsEnabled = !busy && documents != null && provider != null;
            ProviderChoice.IsEnabled = !busy;
            ExecutablePath.IsEnabled = !busy;
            BrowseButton.IsEnabled = !busy;
            Instruction.IsEnabled = !busy;
            StopButton.IsEnabled = busy;
            AttachButton.IsEnabled = PasteButton.IsEnabled = NewChatButton.IsEnabled = !busy;
            AttachmentList.IsEnabled = SettingsPanel.IsEnabled = SettingsContent.IsEnabled = !busy;
            SettingsButton.IsEnabled = HelpButton.IsEnabled = !busy;
            AccountCheckButton.IsEnabled = LoginButton.IsEnabled = SaveSettingsButton.IsEnabled = !busy;
            ReviewApproval.IsEnabled = !busy && pendingProposal != null && pendingProposal.HasChanges;
            ApplyButton.IsEnabled = ReviewApproval.IsEnabled && ReviewApproval.IsChecked == true;
            SaveButton.IsEnabled = !busy && session.CanSave;
            RestoreButton.IsEnabled = !busy && session.CanRestore;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : IDE가 도구 창을 해제할 때 진행 중인 AI 요청을 취소한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 종료 중 비동기 UI 갱신 차단.
        /// update date  : 2026-10-18 CodexOrig : 종료 시 설정·사용 안내 팝업도 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            disposed = true;
            if (requestCancellation != null) requestCancellation.Cancel();
            CloseDialogs();
        }
    }
}
