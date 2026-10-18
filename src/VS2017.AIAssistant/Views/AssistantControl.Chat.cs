using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Providers;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : Chat 대화·첨부·계정·개인 설정을 기존 승인 편집 UI에 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : Role 3~8번의 사용자 입력 기능 구현.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private readonly Dictionary<string, ChatSession> chats = new Dictionary<string, ChatSession>();
        private readonly Dictionary<string, string> models = new Dictionary<string, string>();
        private readonly AssistantSettingsStore settingsStore = new AssistantSettingsStore();
        private ChatSession ActiveChat
        {
            get
            {
                ConversationRecord record;
                string key = activeRecords.TryGetValue(selectedProvider, out record) ? record.Id : selectedProvider;
                ChatSession chat;
                if (!chats.TryGetValue(key, out chat))
                {
                    chats[key] = chat = new ChatSession();
                    if (record != null) { foreach (var message in record.Messages) chat.Messages.Add(message); chat.Draft = record.Draft; }
                }
                return chat;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeChat
        /// desc         : 저장한 개인 설정과 붙여넣기 핸들러를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 손상된 설정은 기본값으로 복구하고 안내.
        /// update date  : 2026-10-18 CodexOrig : 중앙 설정 팝업과 첫 사용 안내 연결.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InitializeChat()
        {
            AssistantSettings settings;
            try { settings = settingsStore.Load(); }
            catch (Exception) { settings = new AssistantSettings(); StatusLabel.Text = "설정을 읽지 못해 기본값을 사용합니다. 설정 저장으로 다시 구성할 수 있습니다."; }
            executablePaths["Codex"] = settings.CodexPath; executablePaths["Claude"] = settings.ClaudePath;
            models["Codex"] = settings.CodexModel; models["Claude"] = settings.ClaudeModel;
            ExecutablePath.Text = settings.ClaudePath; RulesText.Text = settings.Instructions;
            FontSize = settings.FontSize;
            TextSizeChoice.SelectedValue = settings.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (Control control in new Control[] { ModelName, RulesText, AttachmentList })
            {
                control.SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
                control.SetResourceReference(ForegroundProperty, VsBrushes.ToolWindowTextKey);
            }
            DataObject.AddPastingHandler(Instruction, PromptPasting);
            CommandManager.AddPreviewCanExecuteHandler(Instruction, PasteCanExecute);
            CommandManager.AddPreviewExecutedHandler(Instruction, PasteExecuted);
            InitializeExperience(settings);
            ShowChat();
            InitializeDialogs(settings.HideWelcome);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SaveDraft
        /// desc         : 탭 전환 직전 프롬프트와 모델 선택을 메모리에 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : Provider별 작성 중 입력 분리.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SaveDraft() { ActiveChat.Draft = Instruction.Text; models[selectedProvider] = ModelName.Text; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShowChat
        /// desc         : 선택한 탭의 대화·첨부·입력을 다시 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 다른 Provider의 대화 전송 방지.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ShowChat()
        {
            ConversationMessages.ItemsSource = ActiveChat.Messages;
            AttachmentList.ItemsSource = ActiveChat.Attachments;
            Instruction.Text = ActiveChat.Draft;
            string model; ModelName.Text = models.TryGetValue(selectedProvider, out model) ? model : "";
            AccountLabel.Text = "연결 확인을 눌러 " + selectedProvider + " 계정 상태를 확인하세요.";
            ConversationScroll.ScrollToEnd();
            RefreshExperience();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NewChatClicked
        /// desc         : 현재 Provider의 대화·첨부·승인만 초기화한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 디스크나 다른 탭의 대화는 변경하지 않음.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void NewChatClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            CreateConversation();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AttachFiles
        /// desc         : 파일 선택/드롭 결과를 먼저 검증한 뒤 일괄 첨부한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 4개 제한과 요청 중 변경 차단.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void AttachFiles(string[] paths)
        {
            if (busy || disposed) throw new InvalidOperationException("요청 완료 후 첨부를 변경하세요.");
            if (paths == null || ActiveChat.Attachments.Count + paths.Length > 4) throw new InvalidOperationException("첨부는 최대 4개입니다.");
            var attachments = paths.Select(ChatAttachment.FromFile).ToArray();
            foreach (var attachment in attachments) ActiveChat.Attachments.Add(attachment);
            StatusLabel.Text = "첨부 내용을 선택한 AI에 함께 보냅니다. 전송 전 목록을 확인하세요.";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AttachClicked
        /// desc         : 이미지/텍스트 파일을 여러 개 선택해 프롬프트에 첨부한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 파일 선택 버튼 연결.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AttachClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            var dialog = new OpenFileDialog { Multiselect = true, CheckFileExists = true, Filter = "이미지 및 텍스트 파일|*.*" };
            if (dialog.ShowDialog() == true)
                try { AttachFiles(dialog.FileNames); } catch (Exception error) { StatusLabel.Text = "첨부 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AttachmentsDragOver
        /// desc         : 외부 파일을 놓을 수 있음을 표시하고 텍스트 드래그는 편집기에 위임한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 파일 드롭의 기본 텍스트 삽입 방지.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AttachmentsDragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            e.Effects = busy || disposed ? DragDropEffects.None : DragDropEffects.Copy; e.Handled = true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AttachmentsDropped
        /// desc         : 탐색기에서 드롭한 파일을 실행하지 않고 첨부 목록에 추가한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 오류 시 부분 첨부 없이 안내.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AttachmentsDropped(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            e.Handled = true;
            try { AttachFiles((string[])e.Data.GetData(DataFormats.FileDrop)); }
            catch (Exception error) { StatusLabel.Text = "첨부 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PasteAttachmentData
        /// desc         : 클립보드 파일/이미지를 첨부하고 일반 텍스트는 편집기에 둔다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 스크린샷 Ctrl+V와 파일 복사 지원.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool PasteAttachmentData(IDataObject data)
        {
            if (data.GetDataPresent(DataFormats.FileDrop)) { AttachFiles((string[])data.GetData(DataFormats.FileDrop)); return true; }
            var bitmap = data.GetData(DataFormats.Bitmap) as BitmapSource;
            if (bitmap == null) return false;
            if (busy || disposed || ActiveChat.Attachments.Count >= 4) throw new InvalidOperationException("요청 완료 후 최대 4개까지 첨부하세요.");
            if ((long)bitmap.PixelWidth * bitmap.PixelHeight > 16000000) throw new InvalidOperationException("이미지는 1600만 픽셀 이하로 붙여넣으세요.");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                ActiveChat.Attachments.Add(ChatAttachment.FromImage(stream.ToArray(), "붙여넣은 이미지.png"));
            }
            StatusLabel.Text = "이미지를 첨부했습니다. 프롬프트와 함께 보낼 수 있습니다.";
            return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PromptPasting
        /// desc         : 기본 붙여넣기 명령에서 이미지를 가로채 첨부하고 텍스트는 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : Ctrl+V 및 컨텍스트 메뉴 붙여넣기 연결.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PromptPasting(object sender, DataObjectPastingEventArgs e)
        {
            try { if (PasteAttachmentData(e.DataObject)) e.CancelCommand(); }
            catch (Exception error) { e.CancelCommand(); StatusLabel.Text = "붙여넣기 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PasteCanExecute
        /// desc         : 텍스트 전용 편집기가 이미지 Ctrl+V 명령을 미리 차단하지 않도록 한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 이미지·파일의 붙여넣기 명령 활성화.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PasteCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Command != ApplicationCommands.Paste) return;
            try
            {
                if (!Clipboard.ContainsImage() && !Clipboard.ContainsFileDropList()) return;
                e.CanExecute = !busy && !disposed; e.Handled = true;
            }
            catch (System.Runtime.InteropServices.ExternalException) { }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PasteExecuted
        /// desc         : 이미지·파일의 Ctrl+V와 기본 붙여넣기 명령을 첨부 처리로 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 일반 텍스트 붙여넣기는 기본 동작 유지.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PasteExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command != ApplicationCommands.Paste || busy || disposed) return;
            try
            {
                if (!Clipboard.ContainsImage() && !Clipboard.ContainsFileDropList()) return;
                e.Handled = true; PasteClicked(sender, e);
            }
            catch (System.Runtime.InteropServices.ExternalException error) { e.Handled = true; StatusLabel.Text = "붙여넣기 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PasteClicked
        /// desc         : 버튼으로 이미지/파일/일반 텍스트 붙여넣기를 제공한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 기존 프롬프트의 선택 영역에 텍스트 삽입.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PasteClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            try
            {
                var data = Clipboard.GetDataObject();
                if (data != null && PasteAttachmentData(data)) return;
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (Instruction.Text.Length - Instruction.SelectionLength + text.Length > 16000) throw new InvalidOperationException("프롬프트는 16000자 이하로 입력하세요.");
                    Instruction.SelectedText = text;
                }
            }
            catch (Exception error) { StatusLabel.Text = "붙여넣기 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RemoveAttachmentClicked
        /// desc         : 선택한 첨부만 제거하고 원본 파일은 변경하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 전송 전 첨부 취소 지원.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RemoveAttachmentClicked(object sender, RoutedEventArgs e)
        { if (!busy && !disposed) ActiveChat.Attachments.Remove((ChatAttachment)((Button)sender).Tag); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TextSizeChanged
        /// desc         : 글자 크기를 프롬프트·대화·검토 화면에 즉시 반영한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 10~28 범위의 가독성 설정.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void TextSizeChanged(object sender, SelectionChangedEventArgs e)
        {
            double size;
            var item = e.AddedItems.Count > 0 ? e.AddedItems[0] as ComboBoxItem : null;
            if (item != null && double.TryParse(Convert.ToString(item.Content), out size) && size >= 10 && size <= 28) FontSize = size;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SettingsExpanded
        /// desc         : 설정과 변경 검토가 동시에 입력 영역을 가리지 않도록 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 좁거나 낮은 창에서도 설정 접근을 유지.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SettingsExpanded(object sender, RoutedEventArgs e)
        { if (ReviewPanel != null) ReviewPanel.IsExpanded = false; ResizeChat(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReviewExpanded
        /// desc         : 변경 검토를 펼치면 설정을 접고 대화와 프롬프트를 함께 배치한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 승인 UI의 스크롤 접근 보장.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ReviewExpanded(object sender, RoutedEventArgs e)
        { if (SettingsPanel != null) SettingsPanel.IsExpanded = false; ResizeChat(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PanelCollapsed
        /// desc         : 패널을 접으면 대화에 사용 가능한 높이를 복원한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 창 높이에 맞춘 레이아웃 갱신.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PanelCollapsed(object sender, RoutedEventArgs e) { ResizeChat(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChatSizeChanged
        /// desc         : 도킹·분리·크기 변경 시 입력 영역까지 스크롤할 수 있게 한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 낮은 창에서 내용 잘림 방지.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ChatSizeChanged(object sender, SizeChangedEventArgs e) { ResizeChat(); if (HistoryToggle != null) HistoryToggled(sender, e); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResizeChat
        /// desc         : 일반 높이에서는 입력을 아래에 두고 작은 창에서는 전체 스크롤을 제공한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 펼친 패널에 필요한 최소 콘텐츠 높이 확보.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ResizeChat()
        {
            if (LayoutRoot == null || SettingsPanel == null || ReviewPanel == null) return;
            double minimum = ReviewPanel.IsExpanded ? 780 : 530;
            LayoutRoot.Height = double.NaN;
            LayoutRoot.MinHeight = Math.Max(ActualHeight - 20, minimum);
            ConversationScroll.MaxHeight = Math.Max(150, ActualHeight - (ReviewPanel.IsExpanded ? 550 : 290));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SaveSettingsClicked
        /// desc         : 경로·모델·공통 규칙·글자 크기를 명시적으로 저장한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 계정 토큰이나 대화 원문은 저장하지 않음.
        /// update date  : 2026-10-18 CodexOrig : 안내 선택 보존·저장 후 팝업 닫기·오류 표시.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SaveSettingsClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            try
            {
                AssistantSettingsTransaction.Save(settingsStore, workspaceStore, CaptureSettings());
                SaveDraft(); executablePaths[selectedProvider] = ExecutablePath.Text;
                StatusLabel.Text = "설정을 저장했습니다. 다음 실행에도 유지됩니다.";
                if (activeSettingsDialog != null) activeSettingsDialog.DialogResult = true;
            }
            catch (Exception error)
            {
                StatusLabel.Text = "설정 저장 실패: " + error.Message;
                if (activeSettingsDialog != null) activeSettingsDialog.Message.Text = StatusLabel.Text;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LoginClicked
        /// desc         : 선택한 CLI의 공식 로그인 창을 열고 인증을 사용자에게 맡긴다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 계정 연결 버튼 구현.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void LoginClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            try { new CliAccountService().BeginLogin(selectedProvider, ExecutablePath.Text); AccountLabel.Text = "열린 CLI/브라우저에서 로그인한 뒤 연결 확인을 누르세요."; }
            catch (Exception error) { AccountLabel.Text = "연결 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AccountCheckClicked
        /// desc         : UI를 멈추지 않고 로그인 상태를 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 실패·취소·해제 후 UI 갱신 차단.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async void AccountCheckClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            busy = true; requestCancellation = new CancellationTokenSource(); UpdateButtons();
            AccountLabel.Text = "연결 확인 중…";
            try
            {
                bool connected = await new CliAccountService().CheckAsync(selectedProvider, ExecutablePath.Text, requestCancellation.Token);
                if (!disposed) AccountLabel.Text = connected ? "연결됨 · 공식 CLI 로그인 계정을 사용합니다." : "로그인이 필요합니다. 계정 연결을 누르세요.";
            }
            catch (Exception error) { if (!disposed) AccountLabel.Text = "연결 확인 실패: " + error.Message; }
            finally
            {
                busy = false; requestCancellation.Dispose(); requestCancellation = null;
                if (!disposed) UpdateButtons();
            }
        }
    }
}
