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
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
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
        private string selectedProvider = "Claude";
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
        ///                2026-10-19 CodexOrig : TASK-025 생성 경계에서 WPF UI 스레드를 실제 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        public AssistantControl()
        {
            Dispatcher.VerifyAccess();
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        ///                2026-10-18 CodexOrig : TASK-020 실제 토큰·진행 Provider 구독 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Initialize(IActiveDocumentService documentService, IAiEditProvider editProvider)
        {
            documents = documentService ?? throw new ArgumentNullException(nameof(documentService));
            provider = editProvider ?? throw new ArgumentNullException(nameof(editProvider));
            AttachRuntimeProvider();
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 실제 요청 시작·성공·실패·취소를 대화별 상태로 기록.
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
        ///                2026-10-18 CodexOrig : TASK-020 전체 액세스 분기와 선택 문맥·요청별 실제 사용량 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100", Justification = "WPF/Dispatcher event handler; awaited operation handles cancellation and failures.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD109", Justification = "WPF Click은 이미 Dispatcher 스레드이며 VerifyAccess로 계약을 검사한다. IDE 전용 JTF 초기화 없이 독립 WPF에서도 즉시 입력 검증한다.")]
        private async void GenerateClicked(object sender, RoutedEventArgs e)
        {
            Dispatcher.VerifyAccess();
            if (busy || documents == null || disposed) return;
            if (AgentFullAccessEnabled) { await RunFullAccessFromUiAsync(); return; }
            ClearProposal();
            ChatSession chat = ActiveChat;
            ConversationRecord record = activeRecords[selectedProvider];
            string progressOutcome = "failed";
            try
            {
                if (string.IsNullOrWhiteSpace(Instruction.Text)) throw new InvalidOperationException("수정 요청을 입력하세요.");
                pendingDocument = documents.GetActiveDocument();
                DocumentSnapshot source = pendingDocument.Capture();
                source.RequireValidSelection();
                MandatoryInstructionStore.Ensure(workspaceStore.RootPath);
                if (System.IO.File.Exists(workspaceStore.InstructionsPath)) RulesText.Text = workspaceStore.LoadInstructions();
                TargetLabel.Text = "대상: " + source.FilePath + (source.HasUnsavedChanges ? " (미저장 편집 내용 포함)" : "");
                if (source.HasSelection) TargetLabel.Text += " · 선택 " + source.Selection.StartLine + "~" + source.Selection.EndLine + "행";
                OriginalPreview.Text = source.Content;
                new ProviderExecutionOptions(selectedProvider, ModelName.Text, efforts[selectedProvider], speeds[selectedProvider]);
                var context = new ChatRequestContext(RulesText.Text, ModelName.Text, chat.Messages.ToArray(), chat.Attachments.ToArray(), efforts[selectedProvider], speeds[selectedProvider]);
                chat.Add("user", Instruction.Text + (context.Attachments.Length == 0 ? "" : "\n첨부: " + string.Join(", ", context.Attachments.Select(a => a.Name))));
                FollowConversation(true);
                busy = true;
                SetConversationState(record, ConversationRunState.Running);
                if (!PersistCurrentConversation()) throw new InvalidOperationException("대화 기록을 저장한 뒤 다시 요청하세요.");
                requestCancellation = new CancellationTokenSource();
                BeginRuntimeRequest();
                StatusLabel.Text = selectedProvider + " 수정안 생성 중…";
                UpdateButtons();
                context = await RunCollaboratorsAsync(record, source, Instruction.Text, context, requestCancellation.Token);
                var contextProvider = provider as IAiContextEditProvider;
                if (contextProvider == null && (context.Attachments.Length > 0 || context.Instructions.Length > 0 || context.History.Length > 0))
                    throw new InvalidOperationException("이 Provider는 대화 컨텍스트를 지원하지 않습니다.");
                EditProposal proposal = contextProvider == null
                    ? await provider.GenerateAsync(selectedProvider, ExecutablePath.Text, source, Instruction.Text, requestCancellation.Token)
                    : await contextProvider.GenerateWithContextAsync(selectedProvider, ExecutablePath.Text, source, Instruction.Text, context, requestCancellation.Token);
                if (disposed) return;
                source.RequireUnchanged(pendingDocument.Capture());
                pendingProposal = proposal;
                AddRuntimeFinalReply(proposal.Summary);
                FollowConversation(false);
                ProposedPreview.Text = proposal.ProposedText;
                DiffPreview.Text = proposal.ChangePreview;
                StatusLabel.Text = proposal.Summary + (proposal.HasChanges ? "\n변경 비교 후 적용을 승인하세요." : "\n적용할 변경이 없습니다.");
                ReviewTabs.SelectedIndex = 0;
                ReviewPanel.IsExpanded = proposal.HasChanges;
                Instruction.Clear();
                EmptyHint.Visibility = Visibility.Collapsed;
                progressOutcome = "completed";
                SetConversationState(record, ConversationRunState.Completed);
            }
            catch (OperationCanceledException) { progressOutcome = "stopped"; if (!disposed) { SetConversationState(record, ConversationRunState.Stopped); StatusLabel.Text = "요청을 중지했습니다. 소스는 변경하지 않았습니다."; chat.Add("system", StatusLabel.Text); } }
            catch (Exception exception) { if (!disposed) { SetConversationState(record, ConversationRunState.Failed); StatusLabel.Text = "요청 실패: " + exception.Message; chat.Add("system", StatusLabel.Text); } }
            finally
            {
                try { if (!disposed && requestCancellation != null) { FlushRuntimeProgress(); CompleteProgressRequest(progressOutcome); } }
                finally {
                    busy = false;
                    if (requestCancellation != null) { requestCancellation.Dispose(); requestCancellation = null; }
                    if (!disposed) { UpdateButtons(); FollowConversation(false); PersistCurrentConversation(); }
                }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApplyClicked
        /// desc         : 화면의 승인 상태와 원본 일치를 검증한 뒤 현재 파일 버퍼에 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 승인 전·처리 중 적용 차단.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        ///                2026-10-18 CodexOrig : TASK-020 실제로 적용한 변경의 이력 카드 기록.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ApplyClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            try
            {
                session.Apply(pendingDocument, pendingProposal, ReviewApproval.IsChecked == true);
                RecordProposalApplied(pendingProposal);
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        ///                2026-10-18 CodexOrig : TASK-020 현재 대화에서 적용한 세션만 저장하도록 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            try { if (!CanUsePresentedEditSession()) throw new InvalidOperationException("현재 대화에서 적용한 수정안만 저장할 수 있습니다."); session.Save(); StatusLabel.Text = "적용 내용을 저장했습니다."; }
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        ///                2026-10-18 CodexOrig : TASK-020 현재 대화의 복구와 실행 이력 갱신.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RestoreClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            try
            {
                if (!CanUsePresentedEditSession()) throw new InvalidOperationException("현재 대화에서 적용한 수정안만 복구할 수 있습니다.");
                session.Restore(true);
                RecordProposalRestored();
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 직접 선택한 AI의 대화를 읽음 처리.
        ///                2026-10-18 CodexOrig : TASK-020 선택 항목·내부 제공자 동기화 및 연결 세대 초기화.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ProviderChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExecutablePath == null || ReviewApproval == null) return;
            if (!ReferenceEquals(e.OriginalSource, ProviderChoice)) return;
            string nextProvider = Convert.ToString((ProviderChoice.SelectedItem as TabItem)?.Tag);
            if (nextProvider == selectedProvider || (nextProvider != "Codex" && nextProvider != "Claude")) return;
            if (busy) { ProviderChoice.SelectedItem = ProviderChoice.Items.Cast<TabItem>().First(t => Convert.ToString(t.Tag) == selectedProvider); return; }
            executablePaths[selectedProvider] = ExecutablePath.Text;
            SaveDraft();
            if (!PersistCurrentConversation())
            {
                ProviderChoice.SelectedItem = ProviderChoice.Items.Cast<TabItem>().First(t => Convert.ToString(t.Tag) == selectedProvider);
                return;
            }
            selectedProvider = nextProvider;
            RuntimeProviderChanged();
            string path;
            ExecutablePath.Text = executablePaths.TryGetValue(selectedProvider, out path) ? path : "";
            ShowChat();
            MarkCurrentConversationRead();
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
        /// update date  : 2026-10-18 CodexCode : TASK-017 이미지 분석 기반 드롭다운·안내·대화 지정 흐름 개선.
        ///                2026-10-18 CodexOrig : TASK-020 실제 생각 중 그라데이션과 대화별 적용 상태 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateButtons()
        {
            GenerateButton.IsEnabled = !busy && documents != null && provider != null;
            ProviderChoice.IsEnabled = !busy;
            if (busy) { CloseDropdown(); CloseFan(); }
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
            SaveButton.IsEnabled = !busy && session.CanSave && CanUsePresentedEditSession();
            RestoreButton.IsEnabled = !busy && session.CanRestore && CanUsePresentedEditSession();
            if (RequestModel != null) RequestModel.IsEnabled = ModeChoice.IsEnabled = PermissionsButton.IsEnabled = LoadChatButton.IsEnabled = ConversationList.IsEnabled = OptionsToggle.IsEnabled = !busy;
            if (ModelOptionsButton != null) { ModelOptionsButton.IsEnabled = AgentButton.IsEnabled = !busy; var capability = ProviderModelCatalog.ForProvider(selectedProvider).Find(ModelName.Text); FastToggle.IsEnabled = !busy && capability != null && capability.SupportsFast; }
            UpdateChangeCard();
            UpdateActivity(busy);
            UpdateThinkingAnimation(busy);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : IDE가 도구 창을 해제할 때 진행 중인 AI 요청을 취소한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 종료 중 비동기 UI 갱신 차단.
        /// update date  : 2026-10-18 CodexOrig : 종료 시 설정·사용 안내 팝업도 해제.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
        ///                2026-10-19 CodexOrig : TASK-025 팝업·테마 해제 전 WPF UI 스레드를 실제 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            Dispatcher.VerifyAccess();
            ConversationScroll.RemoveHandler(System.Windows.Input.Mouse.PreviewMouseWheelEvent, new System.Windows.Input.MouseWheelEventHandler(ConversationViewed));
            DisposeExperience();
            disposed = true;
            if (requestCancellation != null) requestCancellation.Cancel();
            CloseDialogs();
            if (optionsDialog != null) optionsDialog.Close();
        }
    }
}
