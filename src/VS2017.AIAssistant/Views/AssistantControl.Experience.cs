using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Services;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MiddleTitleConverter
    /// desc         : 대화 제목의 가운데를 줄여 질문의 앞과 끝을 알아볼 수 있게 한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class MiddleTitleConverter : IValueConverter
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Convert
        /// desc         : 긴 대화 제목을 가운데 말줄임표로 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        { string text = System.Convert.ToString(value); return text.Length <= 18 ? text : text.Substring(0, 9) + "…" + text.Substring(text.Length - 6); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConvertBack
        /// desc         : 표시 전용 제목에서 원문 변경을 허용하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { return Binding.DoNothing; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : Markdown 대화·모델·권한·키 입력·알림과 소스 링크를 연결한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private MarkdownWorkspaceStore workspaceStore;
        private readonly Dictionary<string, List<ConversationRecord>> conversationRecords = new Dictionary<string, List<ConversationRecord>>();
        private readonly Dictionary<string, ConversationRecord> activeRecords = new Dictionary<string, ConversationRecord>();
        private readonly Dictionary<string, string> efforts = new Dictionary<string, string>();
        private WorkspacePermissionPolicy workspacePolicy = new WorkspacePermissionPolicy();
        private bool experienceReady, syncingModel, switchingConversation, imeComposing;
        private DispatcherTimer draftTimer, toastTimer;
        private int toastVersion;
        private string reviewFilePath;
        private int reviewLine = 1;
        private readonly List<string> workspaceWarnings = new List<string>();
        private readonly List<string> notifications = new List<string>();
        private DependencyPropertyDescriptor statusDescriptor;
        private readonly System.Diagnostics.Stopwatch activityClock = new System.Diagnostics.Stopwatch();
        private DispatcherTimer activityTimer;
        private readonly Dictionary<string, string> conversationActivities = new Dictionary<string, string>();

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeExperience
        /// desc         : 전역 Markdown 대화와 규칙 및 입력 이벤트를 복원한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 대화 목록 배치 복원과 본문 확인 이벤트 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InitializeExperience(AssistantSettings settings)
        {
            Resources[VsBrushes.ToolWindowBackgroundKey] = new SolidColorBrush(Color.FromRgb(23, 23, 23));
            Resources[VsBrushes.ToolWindowTextKey] = new SolidColorBrush(Color.FromRgb(235, 235, 235));
            efforts["Codex"] = ValidEffort(settings.CodexEffort); efforts["Claude"] = ValidEffort(settings.ClaudeEffort);
            workspacePolicy = new WorkspacePermissionPolicy { AllowSourceControl = settings.AllowSourceControl, AllowDatabaseSchema = settings.AllowDatabaseSchema, AllowDeployment = settings.AllowDeployment };
            string root = Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH");
            string isolated = Environment.GetEnvironmentVariable("VS2017_AI_SETTINGS_PATH");
            if (string.IsNullOrEmpty(root) && !string.IsNullOrEmpty(isolated)) root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(isolated)), "workspace");
            workspaceStore = new MarkdownWorkspaceStore(root);
            foreach (string name in new[] { "Claude", "Codex" })
            {
                List<ConversationRecord> records;
                try { records = workspaceStore.LoadOpen(name).ToList(); workspaceWarnings.AddRange(workspaceStore.LastLoadWarnings); }
                catch (Exception error) { records = new List<ConversationRecord>(); StatusLabel.Text = "대화 복원 실패: " + error.Message; }
                if (records.Count == 0)
                {
                    try { records.Add(workspaceStore.Create(name)); }
                    catch (Exception error) { records.Add(new ConversationRecord { Id = Guid.NewGuid().ToString("N"), Provider = name, CreatedAt = DateTimeOffset.Now, UpdatedAt = DateTimeOffset.Now }); StatusLabel.Text = "대화 저장 준비 실패: " + error.Message; }
                }
                conversationRecords[name] = records; activeRecords[name] = records[0];
            }
            try
            {
                if (File.Exists(workspaceStore.InstructionsPath)) RulesText.Text = workspaceStore.LoadInstructions();
                else if (!string.IsNullOrEmpty(settings.Instructions)) workspaceStore.SaveInstructions(settings.Instructions);
            }
            catch (Exception error) { StatusLabel.Text = "공통 규칙 읽기 실패: " + error.Message; }
            RulesPathLabel.Text = workspaceStore.InstructionsPath;
            draftTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            draftTimer.Tick += (s, a) => { draftTimer.Stop(); if (!busy && !disposed) PersistCurrentConversation(); };
            Instruction.TextChanged += (s, a) => { if (experienceReady && !switchingConversation && !busy) { draftTimer.Stop(); draftTimer.Start(); } };
            TextCompositionManager.AddPreviewTextInputStartHandler(Instruction, (s, a) => imeComposing = true);
            TextCompositionManager.AddPreviewTextInputHandler(Instruction, (s, a) => imeComposing = false);
            RequestModel.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(RequestModelChanged));
            PreviewKeyDown += (s, a) => { if (a.Key == Key.Escape && FanPanel.Visibility == Visibility.Visible) { CloseFan(); a.Handled = true; } };
            var menu = new ContextMenu(); menu.Resources.MergedDictionaries.Add(Resources);
            var rename = new MenuItem { Header = "✎ 이름 변경" }; rename.Click += RenameChatClicked;
            var close = new MenuItem { Header = "× 대화 닫기" }; close.Click += CloseChatClicked;
            menu.Items.Add(rename); menu.Items.Add(close); ConversationList.ContextMenu = menu;
            ConversationList.PreviewMouseRightButtonDown += (s, a) => {
                DependencyObject item = a.OriginalSource as DependencyObject;
                while (item != null && !(item is ListBoxItem)) item = VisualTreeHelper.GetParent(item);
                if (item is ListBoxItem) ((ListBoxItem)item).IsSelected = true;
            };
            toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) }; toastTimer.Tick += FadeToast;
            activityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            activityTimer.Tick += (sender, args) => RefreshActivityTime();
            statusDescriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock)); statusDescriptor.AddValueChanged(StatusLabel, StatusChanged);
            ClaudeTabLogo.Source = LoadBrand("Claude"); CodexTabLogo.Source = LoadBrand("Codex");
            experienceReady = true; InitializeConversationTracking(settings); RefreshExperience();
            if (workspaceWarnings.Count > 0) StatusLabel.Text = "일부 대화를 복원하지 못했습니다. 최근 알림에서 확인하세요.\n" + string.Join("\n", workspaceWarnings);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidEffort
        /// desc         : 이전 JSON에 없는 모드를 CLI 기본값으로 복구한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ValidEffort(string effort) { return new[] { "auto", "low", "medium", "high" }.Contains(effort) ? effort : "auto"; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LoadBrand
        /// desc         : 동봉한 공식 제공자 로고를 네트워크 없이 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static ImageSource LoadBrand(string name)
        { var image = new BitmapImage(new Uri("pack://application:,,,/VS2017.AIAssistant;component/Assets/" + name + ".png", UriKind.Absolute)); image.Freeze(); return image; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PersistCurrentConversation
        /// desc         : 현재 입력과 메시지를 현재 대화 Markdown에 저장한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool PersistCurrentConversation()
        {
            if (!experienceReady || !activeRecords.ContainsKey(selectedProvider)) return true;
            try { var chat = ActiveChat; var record = activeRecords[selectedProvider]; chat.Draft = Instruction.Text; record.Draft = chat.Draft; record.Messages = chat.Messages.Select(m => new ChatMessage { Role = m.Role, Text = m.Text }).ToList(); workspaceStore.Save(record); RefreshConversationList(); return true; }
            catch (Exception error) { StatusLabel.Text = "대화 저장 실패: " + error.Message + " 전환하기 전에 다시 저장하세요."; return false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshExperience
        /// desc         : 현재 AI의 모델·모드·로고와 대화 목록 및 변경 요약을 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshExperience()
        {
            if (!experienceReady) return;
            syncingModel = true;
            try { RequestModel.Items.Clear(); RequestModel.Items.Add("CLI 기본"); if (selectedProvider == "Claude") foreach (string model in new[] { "sonnet", "opus", "haiku" }) RequestModel.Items.Add(model); RequestModel.Text = string.IsNullOrEmpty(ModelName.Text) ? "CLI 기본" : ModelName.Text; ModeChoice.SelectedValue = efforts[selectedProvider]; }
            finally { syncingModel = false; }
            BrandWatermark.Source = selectedProvider == "Claude" ? ClaudeTabLogo.Source : CodexTabLogo.Source;
            EmptyHint.Visibility = ActiveChat.Messages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ConversationStartedLabel.Text = "대화 시작 · " + activeRecords[selectedProvider].CreatedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm");
            string activity;
            ActivityLabel.Text = conversationActivities.TryGetValue(activeRecords[selectedProvider].Id, out activity) ? activity : "";
            ActivityLabel.Visibility = ActivityLabel.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            RefreshConversationList(); UpdateChangeCard();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshConversationList
        /// desc         : 제목 변경과 현재 선택을 재진입 없이 목록에 반영한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshConversationList()
        {
            if (!experienceReady) return; switchingConversation = true;
            try { ConversationList.ItemsSource = conversationRecords[selectedProvider].ToArray(); ConversationList.SelectedItem = activeRecords[selectedProvider]; }
            finally { switchingConversation = false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateConversation
        /// desc         : 기존 대화를 보관하고 같은 AI의 새 하위 대화를 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CreateConversation()
        {
            if (!experienceReady || busy || disposed) return;
            try { if (!PersistCurrentConversation()) return; var record = workspaceStore.Create(selectedProvider); conversationRecords[selectedProvider].Insert(0, record); activeRecords[selectedProvider] = record; ClearProposal(); ShowChat(); UpdateButtons(); HistoryToggle.IsChecked = true; HistoryToggled(this, new RoutedEventArgs()); StatusLabel.Text = "새 대화를 열었습니다. 이전 대화는 왼쪽 목록에 보관됩니다."; }
            catch (Exception error) { StatusLabel.Text = "새 대화 생성 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConversationSelected
        /// desc         : 선택한 대화로 전환하고 이전 대화의 승인을 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 선택한 대화만 읽음 처리하고 다른 대화 상태 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ConversationSelected(object sender, SelectionChangedEventArgs e)
        {
            if (switchingConversation || !experienceReady || busy || disposed) return;
            var next = ConversationList.SelectedItem as ConversationRecord; if (next == null) return;
            if (next == activeRecords[selectedProvider]) { MarkCurrentConversationRead(); return; }
            if (!PersistCurrentConversation()) { RefreshConversationList(); return; } activeRecords[selectedProvider] = next; MarkCurrentConversationRead(); ClearProposal(); ShowChat(); UpdateButtons();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RenameChatClicked
        /// desc         : 사용자가 지정한 대화 제목을 Markdown과 목록에 저장한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RenameChatClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return; var record = ConversationList.SelectedItem as ConversationRecord; if (record == null) return;
            var dialog = new AssistantDialog(this, "✎ 대화 이름", "질문에 따라 정해진 제목을 바꿀 수 있습니다.") { Height = 300 };
            var input = new TextBox { Text = record.Title, MaxLength = 120 }; dialog.Body.Content = input;
            var save = new Button { Content = "✓ 이름 저장", Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            save.Click += (s, a) => { try { workspaceStore.Rename(record, input.Text); dialog.DialogResult = true; } catch (Exception error) { dialog.Message.Text = error.Message; } };
            dialog.Actions.Children.Insert(0, save); dialog.ShowDialog(); RefreshConversationList();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseChatClicked
        /// desc         : 대화를 보관하고 다음 시작의 자동 복원에서 제외한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CloseChatClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return; var record = ConversationList.SelectedItem as ConversationRecord; if (record == null) return;
            try
            {
                if (!PersistCurrentConversation()) return;
                var records = conversationRecords[selectedProvider];
                var replacement = records.Count == 1 ? workspaceStore.Create(selectedProvider) : null;
                try { workspaceStore.Close(record); }
                catch { if (replacement != null) try { workspaceStore.Close(replacement); } catch (Exception) { } throw; }
                records.Remove(record); chats.Remove(record.Id); if (replacement != null) records.Add(replacement);
                activeRecords[selectedProvider] = records[0]; ClearProposal(); ShowChat(); UpdateButtons(); StatusLabel.Text = "대화를 닫았습니다. 불러오기에서 Markdown을 다시 열 수 있습니다.";
            }
            catch (Exception error) { StatusLabel.Text = "대화 닫기 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LoadChatClicked
        /// desc         : 선택한 AI의 대화 Markdown을 다시 열거나 가져온다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void LoadChatClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed || !experienceReady) return;
            var picker = new OpenFileDialog { Filter = "저장한 대화 Markdown (*.md)|*.md", CheckFileExists = true, InitialDirectory = workspaceStore.RootPath };
            if (picker.ShowDialog() != true) return;
            try { if (!PersistCurrentConversation()) return; var record = workspaceStore.Load(picker.FileName, selectedProvider); var list = conversationRecords[selectedProvider]; list.RemoveAll(r => r.Id == record.Id); list.Insert(0, record); chats.Remove(record.Id); activeRecords[selectedProvider] = record; ClearProposal(); ShowChat(); UpdateButtons(); HistoryToggle.IsChecked = true; HistoryToggled(this, e); StatusLabel.Text = "대화를 불러왔습니다."; }
            catch (Exception error) { StatusLabel.Text = "대화 불러오기 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HistoryToggled
        /// desc         : 좁은 창에서는 목록을 겹쳐 표시하고 넓은 창에서는 좌측에 배치한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 드래그 경계 레이아웃과 목록 배치 저장 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void HistoryToggled(object sender, RoutedEventArgs e)
        {
            ApplyHistoryLayout(); SaveHistoryLayout();
            if (HistoryToggle.IsChecked == true) HistoryPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PromptKeyDown
        /// desc         : 실제 키 이벤트를 공통 입력 정책으로 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PromptKeyDown(object sender, KeyEventArgs e) { if (HandlePromptKey(e.Key, Keyboard.Modifiers)) e.Handled = true; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HandlePromptKey
        /// desc         : Enter는 전송하고 Ctrl+Enter는 줄바꿈하며 IME 조합을 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal bool HandlePromptKey(Key key, ModifierKeys modifiers)
        {
            if (key != Key.Enter || imeComposing || (modifiers & ModifierKeys.Alt) != 0) return false;
            if (busy || disposed) return true;
            if ((modifiers & ModifierKeys.Control) != 0) { if (Instruction.Text.Length - Instruction.SelectionLength + Environment.NewLine.Length <= Instruction.MaxLength) { int position = Instruction.SelectionStart; Instruction.SelectedText = Environment.NewLine; Instruction.CaretIndex = position + Environment.NewLine.Length; } return true; }
            if (!string.IsNullOrWhiteSpace(Instruction.Text)) GenerateClicked(GenerateButton, new RoutedEventArgs()); return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequestModelChanged
        /// desc         : 하단 모델 입력을 실제 Provider 요청 모델로 반영한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RequestModelChanged(object sender, TextChangedEventArgs e)
        { if (!experienceReady || syncingModel || busy) return; syncingModel = true; try { ModelName.Text = RequestModel.Text == "CLI 기본" ? "" : RequestModel.Text; models[selectedProvider] = ModelName.Text; } finally { syncingModel = false; } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SettingsModelChanged
        /// desc         : 설정 창의 모델을 하단 선택기에 동기화한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SettingsModelChanged(object sender, TextChangedEventArgs e)
        { if (!experienceReady || syncingModel) return; syncingModel = true; try { RequestModel.Text = ModelName.Text.Length == 0 ? "CLI 기본" : ModelName.Text; } finally { syncingModel = false; } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ModeChanged
        /// desc         : 생각 강도를 다음 실제 CLI 요청에 사용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ModeChanged(object sender, SelectionChangedEventArgs e)
        { if (experienceReady && !syncingModel && ModeChoice.SelectedValue != null) efforts[selectedProvider] = ValidEffort(System.Convert.ToString(ModeChoice.SelectedValue)); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StatusChanged
        /// desc         : 안내를 화면 중앙에 표시하고 최근 메시지를 보관한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void StatusChanged(object sender, EventArgs e)
        {
            if (disposed || !experienceReady || string.IsNullOrEmpty(StatusLabel.Text)) return;
            notifications.Add(StatusLabel.Text); while (notifications.Count > 20) notifications.RemoveAt(0);
            toastVersion++; toastTimer.Stop(); ToastHost.BeginAnimation(OpacityProperty, null); ToastHost.Opacity = 1; ToastHost.Visibility = Visibility.Visible;
            StatusLabel.Foreground = StatusLabel.Text.Contains("실패") || StatusLabel.Text.Contains("오류") ? Brushes.LightSalmon : Brushes.WhiteSmoke;
            ToastHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))); toastTimer.Start();
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FadeToast
        /// desc         : 새 안내를 이전 애니메이션이 가리지 않도록 서서히 숨긴다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FadeToast(object sender, EventArgs e)
        { toastTimer.Stop(); int version = toastVersion; var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(800)); fade.Completed += (s, a) => { if (version == toastVersion) ToastHost.Visibility = Visibility.Collapsed; }; ToastHost.BeginAnimation(OpacityProperty, fade); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RecentNotificationsClicked
        /// desc         : 사라진 중앙 알림과 저장 실패를 언제든 다시 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 최근 20개 안내 기록 제공.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RecentNotificationsClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new AssistantDialog(this, "ⓘ 최근 알림", "최근 20개 안내입니다. 계정 비밀정보는 표시하지 않습니다.");
            dialog.Body.Content = new TextBox { Text = notifications.Count == 0 ? "아직 알림이 없습니다." : string.Join(Environment.NewLine + Environment.NewLine, notifications), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            dialog.ShowDialog();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OptionsHovered
        /// desc         : 상단 우측 메뉴를 마우스로 펼친다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OptionsHovered(object sender, MouseEventArgs e) { OptionsToggle.IsChecked = true; OpenFan(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OptionsClicked
        /// desc         : 키보드와 클릭으로 설정 메뉴를 토글한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OptionsClicked(object sender, RoutedEventArgs e) { if (OptionsToggle.IsChecked == true) OpenFan(); else CloseFan(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenFan
        /// desc         : 메뉴 버튼을 원점에서 펼치는 애니메이션을 시작한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OpenFan()
        {
            if (busy || disposed) return; FanPanel.Visibility = Visibility.Visible;
            foreach (var button in new[] { SettingsButton, HelpButton }) { var move = new TranslateTransform(); button.RenderTransform = move; move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(70, 0, TimeSpan.FromMilliseconds(190))); move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-25, 0, TimeSpan.FromMilliseconds(190))); }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseFan
        /// desc         : 펼친 메뉴와 토글 상태를 함께 접는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CloseFan() { FanPanel.Visibility = Visibility.Collapsed; OptionsToggle.IsChecked = false; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OptionsMouseLeft
        /// desc         : 원점으로 이동할 때 유지하고 메뉴 밖에서만 접는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OptionsMouseLeft(object sender, MouseEventArgs e)
        { var point = Mouse.GetPosition(OptionsToggle); if (point.X >= 0 && point.Y >= 0 && point.X <= OptionsToggle.ActualWidth && point.Y <= OptionsToggle.ActualHeight) return; if (!FanPanel.IsKeyboardFocusWithin) CloseFan(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PermissionsClicked
        /// desc         : 작업 권한과 대상 프로젝트를 실제 서비스 창에 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PermissionsClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return; string directory = "";
            try { var navigation = documents as ISourceNavigationService; if (navigation != null) directory = navigation.GetProjectDirectory(); else if (documents != null) directory = Path.GetDirectoryName(documents.GetActiveDocument().Capture().FilePath); } catch (Exception) { }
            var actions = new WorkspaceActionsDialog(this, directory, workspacePolicy, request => { if (request.Length > 16000) throw new InvalidOperationException("DB 요청이 16000자 제한을 초과했습니다."); Instruction.Text = request; StatusLabel.Text = "DB 스키마와 요청을 입력란에 넣었습니다. 전송 전에 확인하세요."; });
            actions.PermissionsSaved = policy => {
                var saved = settingsStore.Load(); saved.AllowSourceControl = policy.AllowSourceControl; saved.AllowDatabaseSchema = policy.AllowDatabaseSchema; saved.AllowDeployment = policy.AllowDeployment;
                settingsStore.Save(saved); workspacePolicy = policy;
            };
            actions.ShowDialog();
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SettingsContentResized
        /// desc         : 창 높이에 맞춰 공통 instruction 입력 영역을 조절한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SettingsContentResized(object sender, SizeChangedEventArgs e) { if (RulesText != null) RulesText.Height = Math.Max(110, Math.Min(550, e.NewSize.Height - 300)); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CliHelpClicked
        /// desc         : 설치·경로 선택·로그인을 그림형 단계로 안내한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CliHelpClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new AssistantDialog(this, "⚙ CLI 환경 설정", "설치 → 실행 파일 선택 → 계정 로그인 → 연결 확인 순서입니다."); var body = new StackPanel();
            foreach (var step in new[] { new[] { "1  ⇩", "공식 Windows CLI 설치", "공식 설치 안내에서 Codex 또는 Claude의 Windows 네이티브 실행 파일을 설치하세요." }, new[] { "2  ▣", "실행 파일 선택", "설정의 ‘찾기’에서 codex.exe 또는 claude.exe를 선택하세요. PATH가 설정되어 있으면 비워도 됩니다." }, new[] { "3  ↗", "계정 연결", "‘계정 연결’을 누르고 열린 CLI·브라우저에서 직접 로그인하세요. 이 창에는 암호·토큰을 넣지 않습니다." }, new[] { "4  ✓", "연결 확인 후 저장", "‘연결 확인’이 성공하면 ‘설정 저장’을 누르세요. 모델·생각 모드는 하단에서 고를 수 있습니다." } })
            { var words = new StackPanel(); words.Children.Add(new TextBlock { Text = step[0] + "  " + step[1], FontSize = 18, FontWeight = FontWeights.SemiBold }); words.Children.Add(new TextBlock { Text = step[2], TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) }); body.Children.Add(new Border { Child = words, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 12) }); }
            var official = new Button { Content = "↗ " + selectedProvider + " 공식 설치 안내" };
            official.Click += (s, a) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(selectedProvider == "Claude" ? "https://code.claude.com/docs/en/setup" : "https://developers.openai.com/codex/cli") { UseShellExecute = true });
            body.Children.Add(official); dialog.Body.Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; dialog.ShowDialog();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateChangeCard
        /// desc         : 수정안의 줄 증감과 소스 링크를 표시하고 승인을 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateChangeCard()
        {
            if (ChangeCard == null) return; bool changes = pendingProposal != null && pendingProposal.HasChanges;
            ChangeCard.Visibility = changes || session.CanRestore ? Visibility.Visible : Visibility.Collapsed;
            if (changes) { reviewFilePath = pendingProposal.Source.FilePath; reviewLine = pendingProposal.FirstChangedLine; ChangeSummary.Text = "변경된 파일 1개 · +" + pendingProposal.AddedLines + " −" + pendingProposal.RemovedLines; TargetLabel.Text = reviewFilePath; TargetLabel.ToolTip = reviewFilePath; }
            SourceLink.IsEnabled = !string.IsNullOrEmpty(reviewFilePath) && documents is ISourceNavigationService;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SourceLinkClicked
        /// desc         : 변경 파일을 실제 IDE 소스 탭으로 연다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SourceLinkClicked(object sender, RoutedEventArgs e)
        { try { var navigation = documents as ISourceNavigationService; if (navigation == null || string.IsNullOrEmpty(reviewFilePath)) throw new InvalidOperationException("소스 이동 서비스를 사용할 수 없습니다."); navigation.NavigateTo(reviewFilePath, reviewLine); } catch (Exception error) { StatusLabel.Text = "소스 이동 실패: " + error.Message; } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ViewChangesClicked
        /// desc         : 변경 카드에서 전체 원본 및 수정안 비교를 토글한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ViewChangesClicked(object sender, RoutedEventArgs e) { ReviewPanel.IsExpanded = !ReviewPanel.IsExpanded; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeExperience
        /// desc         : 초안을 저장하고 타이머와 상태 이벤트를 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Role 추가 사용자 흐름 구현.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 진행 중 요청을 창 종료 시 중지 상태로 저장.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeExperience()
        {
            if (!experienceReady) return;
            var active = activeRecords[selectedProvider];
            if (active.State == ConversationRunState.Running) { active.State = ConversationRunState.Stopped; active.HasUnread = true; }
            PersistCurrentConversation();
            foreach (var record in conversationRecords.Values.SelectMany(r => r))
            {
                ChatSession chat; if (!chats.TryGetValue(record.Id, out chat)) continue;
                try { record.Draft = chat.Draft; record.Messages = chat.Messages.Select(m => new ChatMessage { Role = m.Role, Text = m.Text }).ToList(); workspaceStore.Save(record); }
                catch (Exception error) { StatusLabel.Text = "종료 전 대화 저장 실패: " + error.Message; }
            }
            draftTimer.Stop(); toastTimer.Stop(); statusDescriptor.RemoveValueChanged(StatusLabel, StatusChanged); experienceReady = false;
            activityTimer.Stop(); activityClock.Stop();
            ThinkingIndicator.BeginAnimation(OpacityProperty, null); ThinkingIndicator.Visibility = Visibility.Collapsed;
            ToastHost.BeginAnimation(OpacityProperty, null);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateActivity
        /// desc         : 실제 실행 구간만 측정하고 대화별 소요 시간을 화면에 구분한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 추가 대화 참고 화면의 실제 진행 표시 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateActivity(bool running)
        {
            if (!experienceReady) return;
            if (running && !activityClock.IsRunning) { activityClock.Restart(); activityTimer.Start(); RefreshActivityTime(); }
            else if (!running && activityClock.IsRunning)
            {
                activityClock.Stop(); activityTimer.Stop();
                string text = "작업 소요 · " + FormatActivityDuration();
                conversationActivities[activeRecords[selectedProvider].Id] = text; ActivityLabel.Text = text; ActivityLabel.Visibility = Visibility.Visible;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshActivityTime
        /// desc         : 실행 중인 작업의 측정된 경과 시간만 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 허구의 도구 실행 내역 없이 진행 상태 제공.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshActivityTime()
        {
            ActivityLabel.Text = FormatActivityDuration() + " 동안 작업 중입니다."; ActivityLabel.Visibility = Visibility.Visible;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FormatActivityDuration
        /// desc         : 실제 Stopwatch 시간을 분과 초로 표현한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 작업 경과 시간 서식 추가.
        /// </summary>
        /// ---------------------------------------------------------------------
        private string FormatActivityDuration()
        {
            return ((int)activityClock.Elapsed.TotalMinutes).ToString() + "분 " + activityClock.Elapsed.Seconds + "초";
        }
    }
}
