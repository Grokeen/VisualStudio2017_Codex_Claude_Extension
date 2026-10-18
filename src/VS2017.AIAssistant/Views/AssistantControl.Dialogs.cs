using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 우클릭 설정·중앙 팝업·처음 사용 안내를 기존 Chat 컨트롤에 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : Role 8~11의 일관된 UI와 첫 사용 흐름 구현.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private AssistantDialog activeSettingsDialog;
        private GettingStartedDialog activeGuide;
        private bool hideWelcome, welcomeScheduled;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeDialogs
        /// desc         : 처음 표시되는 시점의 안내와 일반/텍스트 편집 영역 우클릭 메뉴를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 기본 잘라내기·복사·붙여넣기 명령과 Ctrl+V 보존.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InitializeDialogs(bool hidden)
        {
            hideWelcome = hidden; ContextMenu = CreateAssistantMenu(null);
            foreach (var box in new[] { Instruction, ExecutablePath, ModelName, RulesText, DiffPreview, OriginalPreview, ProposedPreview })
                box.ContextMenu = CreateAssistantMenu(box);
            Loaded += ChatLoaded;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChildLoaded
        /// desc         : 누적 대화에 나중에 생성되는 읽기 전용 텍스트에도 복사·설정 메뉴를 제공한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : DataTemplate 텍스트의 기본 편집 명령 유지.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ChildLoaded(object sender, RoutedEventArgs e)
        {
            var box = e.OriginalSource as TextBox;
            if (box != null && box.ContextMenu == null) box.ContextMenu = CreateAssistantMenu(box);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateAssistantMenu
        /// desc         : 텍스트 메뉴에는 편집 명령을 앞에 두고 모든 영역에 설정·사용 안내를 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 팝업 메뉴에도 동일한 VS 테마 리소스 적용.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private ContextMenu CreateAssistantMenu(TextBox box)
        {
            var menu = new ContextMenu(); menu.Resources.MergedDictionaries.Add(Resources);
            if (box != null)
            {
                if (!box.IsReadOnly) menu.Items.Add(new MenuItem { Header = "잘라내기", Icon = new TextBlock { Text = "✂" }, Command = ApplicationCommands.Cut, CommandTarget = box });
                menu.Items.Add(new MenuItem { Header = "복사", Icon = new TextBlock { Text = "▤" }, Command = ApplicationCommands.Copy, CommandTarget = box });
                if (!box.IsReadOnly) menu.Items.Add(new MenuItem { Header = "붙여넣기", Icon = new TextBlock { Text = "▣" }, Command = ApplicationCommands.Paste, CommandTarget = box });
                menu.Items.Add(new MenuItem { Header = "모두 선택", Command = ApplicationCommands.SelectAll, CommandTarget = box });
                menu.Items.Add(new Separator());
            }
            var settings = new MenuItem { Header = "설정", Icon = new TextBlock { Text = "⚙" } };
            var help = new MenuItem { Header = "사용 안내", Icon = new TextBlock { Text = "?" } };
            settings.Click += SettingsClicked; help.Click += HelpClicked;
            menu.Opened += (sender, args) => { settings.IsEnabled = help.IsEnabled = !busy && !disposed; };
            menu.Items.Add(settings); menu.Items.Add(help);
            var recent = new MenuItem { Header = "최근 알림", Icon = new TextBlock { Text = "ⓘ" } };
            recent.Click += RecentNotificationsClicked; menu.Items.Add(recent); return menu;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChatLoaded
        /// desc         : 실제 Chat 화면이 처음 보일 때 저장한 선택에 따라 사용 안내를 한 번 연다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 생성자·단순 크기 측정 중 모달 창이 열리는 것을 방지.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ChatLoaded(object sender, RoutedEventArgs e)
        {
            if (welcomeScheduled) return;
            welcomeScheduled = true;
            var timer = new DispatcherTimer(DispatcherPriority.ContextIdle, Dispatcher) { Interval = TimeSpan.FromMilliseconds(1) };
            timer.Tick += (timerSender, args) =>
            {
                timer.Stop();
                if (!disposed && !hideWelcome && IsVisible) ShowUsageGuide();
            };
            timer.Start();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureSettings
        /// desc         : 선택된 Provider의 미저장 입력까지 포함한 현재 개인 설정 스냅샷을 만든다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 취소 복구와 명시적 저장에서 같은 필드를 사용.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 설정 저장 시 사용자가 조절한 대화 목록 배치를 보존.
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        private AssistantSettings CaptureSettings()
        {
            return new AssistantSettings {
                CodexPath = selectedProvider == "Codex" ? ExecutablePath.Text : executablePaths["Codex"],
                ClaudePath = selectedProvider == "Claude" ? ExecutablePath.Text : executablePaths["Claude"],
                CodexModel = selectedProvider == "Codex" ? ModelName.Text : models["Codex"],
                ClaudeModel = selectedProvider == "Claude" ? ModelName.Text : models["Claude"],
                Instructions = RulesText.Text, FontSize = FontSize, HideWelcome = hideWelcome,
                CodexEffort = efforts.ContainsKey("Codex") ? efforts["Codex"] : "auto", ClaudeEffort = efforts.ContainsKey("Claude") ? efforts["Claude"] : "auto",
                CodexSpeed = speeds["Codex"], ClaudeSpeed = speeds["Claude"],
                AllowSourceControl = workspacePolicy.AllowSourceControl, AllowDatabaseSchema = workspacePolicy.AllowDatabaseSchema, AllowDeployment = workspacePolicy.AllowDeployment,
                HistoryWidth = historyWidth, ShowHistory = HistoryToggle.IsChecked == true
            };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SettingsClicked
        /// desc         : 기존 설정 컨트롤을 화면 중앙 모달 창으로 이동하고 취소 시 입력을 복원한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 저장 성공 후 닫기·취소·Esc·연결 확인 중 닫기 처리.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SettingsClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            CloseFan();
            if (activeSettingsDialog != null) { activeSettingsDialog.Activate(); return; }
            var before = CaptureSettings();
            var saveParent = (Panel)SaveSettingsButton.Parent;
            int saveIndex = saveParent.Children.IndexOf(SaveSettingsButton);
            var dialog = new AssistantDialog(this, "⚙ 설정", "AI 계정·모델·공통 규칙·글자 크기를 설정하세요. 저장하면 다음 실행에도 유지됩니다.");
            SettingsPanel.Content = null; saveParent.Children.Remove(SaveSettingsButton);
            activeSettingsDialog = dialog; SettingsProviderLabel.Text = "현재 AI: " + selectedProvider;
            dialog.Body.Content = SettingsContent; dialog.Actions.Children.Insert(0, SaveSettingsButton);
            SaveSettingsButton.IsDefault = true;
            try
            {
                if (dialog.ShowDialog() != true)
                {
                    executablePaths["Codex"] = before.CodexPath; executablePaths["Claude"] = before.ClaudePath;
                    models["Codex"] = before.CodexModel; models["Claude"] = before.ClaudeModel;
                    ExecutablePath.Text = selectedProvider == "Codex" ? before.CodexPath : before.ClaudePath;
                    ModelName.Text = selectedProvider == "Codex" ? before.CodexModel : before.ClaudeModel;
                    RulesText.Text = before.Instructions; FontSize = before.FontSize;
                    TextSizeChoice.SelectedValue = before.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    RefreshExperience();
                    StatusLabel.Text = "설정 변경을 취소했습니다.";
                }
            }
            finally
            {
                if (busy && requestCancellation != null) requestCancellation.Cancel();
                dialog.Body.Content = null; dialog.Actions.Children.Remove(SaveSettingsButton);
                saveParent.Children.Insert(saveIndex, SaveSettingsButton); SaveSettingsButton.IsDefault = false;
                SettingsPanel.Content = SettingsContent; activeSettingsDialog = null;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HelpClicked
        /// desc         : 다시 보지 않기 선택 여부와 관계없이 사용 안내를 수동으로 다시 연다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 상단과 우클릭의 동일한 진입 경로 제공.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void HelpClicked(object sender, RoutedEventArgs e) { if (!busy && !disposed) ShowUsageGuide(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShowUsageGuide
        /// desc         : 단계별 사용 안내를 열고 안내 표시 선택만 기존 저장 파일에 병합한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 경로·모델·규칙 보존 및 저장 실패 시 창 안 오류 표시.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ShowUsageGuide()
        {
            if (activeGuide != null) { activeGuide.Activate(); return; }
            var guide = new GettingStartedDialog(this, hideWelcome); activeGuide = guide;
            guide.Closing += (sender, args) =>
            {
                if (disposed || guide.HideNextTime.IsChecked == hideWelcome) return;
                try
                {
                    var saved = settingsStore.Load(); saved.HideWelcome = guide.HideNextTime.IsChecked == true;
                    settingsStore.Save(saved); hideWelcome = saved.HideWelcome;
                }
                catch (Exception error) { guide.Message.Text = "안내 설정 저장 실패: " + error.Message + " 체크를 원래대로 돌리면 닫을 수 있습니다."; args.Cancel = true; }
            };
            try { guide.ShowDialog(); }
            finally { activeGuide = null; }
            if (guide.OpenSettings && !disposed) SettingsClicked(this, new RoutedEventArgs());
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseDialogs
        /// desc         : 확장 해제 시 소유한 팝업을 닫아 남은 UI 접근을 차단한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : IDE 종료 시 안내 설정 쓰기는 생략.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CloseDialogs()
        {
            if (activeGuide != null) activeGuide.Close();
            if (activeSettingsDialog != null) activeSettingsDialog.Close();
        }
    }
}
