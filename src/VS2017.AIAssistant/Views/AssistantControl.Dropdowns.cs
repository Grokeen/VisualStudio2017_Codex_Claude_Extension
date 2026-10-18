using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Services;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 입력 버튼에 붙는 모델·강도·권한과 대화 대상 드롭다운을 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-017 이미지7~11·13의 비모달 선택 동작 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private Popup dropdownPopup;
        private FrameworkElement dropdownAnchor;
        private TextBlock dropdownMessage;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShowDropdown
        /// desc         : 누른 버튼 위에 한 개의 선택 창을 열고 바깥 클릭·Escape로 닫는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 창 크기 제한·키보드 순환·테마와 선택 오류 구역 추가.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool ShowDropdown(FrameworkElement anchor, string title, UIElement content, UIElement navigation = null, UIElement footer = null)
        {
            if (busy || disposed) return false;
            bool same = dropdownPopup != null && dropdownPopup.IsOpen && dropdownAnchor == anchor;
            CloseDropdown(); CloseFan(); if (same) return false;
            dropdownAnchor = anchor;
            var panel = new Grid { MaxHeight = Math.Max(180, Math.Min(590, SystemParameters.WorkArea.Height - 100)) };
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition());
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
            if (navigation != null) { Grid.SetRow(navigation, 1); panel.Children.Add(navigation); }
            var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetRow(scroll, 2); panel.Children.Add(scroll);
            if (footer != null) { Grid.SetRow(footer, 3); panel.Children.Add(footer); }
            dropdownMessage = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightSalmon, Margin = new Thickness(0, 10, 0, 0) };
            Grid.SetRow(dropdownMessage, 4); panel.Children.Add(dropdownMessage);
            var surface = new Border { Background = new SolidColorBrush(Color.FromRgb(37, 37, 37)), BorderBrush = new SolidColorBrush(Color.FromRgb(78, 78, 78)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Width = Math.Min(370, SystemParameters.WorkArea.Width - 24), Child = panel };
            surface.Resources.MergedDictionaries.Add(Resources);
            surface.SetResourceReference(ForegroundProperty, Microsoft.VisualStudio.Shell.VsBrushes.ToolWindowTextKey);
            KeyboardNavigation.SetTabNavigation(surface, KeyboardNavigationMode.Cycle);
            surface.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { CloseDropdown(); anchor.Focus(); e.Handled = true; } };
            dropdownPopup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Top, VerticalOffset = -7, AllowsTransparency = true, StaysOpen = false, PopupAnimation = PopupAnimation.Fade, Child = surface };
            dropdownPopup.IsOpen = true;
            surface.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseDropdown
        /// desc         : 대화·제공자 전환과 요청·해제 시 선택 창의 시각 트리를 정리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 닫힌 팝업의 입력 대상과 이전 제공자 문맥 제거.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CloseDropdown()
        {
            if (dropdownPopup != null) { dropdownPopup.IsOpen = false; dropdownPopup.Child = null; }
            dropdownPopup = null; dropdownAnchor = null; dropdownMessage = null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShowModelDropdown
        /// desc         : 광고된 모델과 강도 게이지·Fast를 같은 드롭다운에서 선택해 저장한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 이미지의 버튼 연결 선택 창과 잘못된 모델 조합 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ShowModelDropdown(FrameworkElement anchor)
        {
            var catalog = ProviderModelCatalog.ForProvider(selectedProvider);
            var panel = new StackPanel();
            var model = new ComboBox { IsEditable = true, IsTextSearchEnabled = false, Text = ModelName.Text, Margin = new Thickness(0, 4, 0, 16), MinWidth = 160 };
            model.Items.Add(""); foreach (var entry in catalog.Models) model.Items.Add(entry.Id);
            panel.Children.Add(new TextBlock { Text = "☷ 상세 모델 · 직접 입력 가능", FontWeight = FontWeights.SemiBold }); panel.Children.Add(model);
            var label = new TextBlock { FontSize = 19, Foreground = new SolidColorBrush(Color.FromRgb(159, 181, 255)), Margin = new Thickness(0, 4, 0, 8) };
            var gauge = new Slider { Minimum = 0, SmallChange = 1, LargeChange = 1, TickFrequency = 1, IsSnapToTickEnabled = true, TickPlacement = TickPlacement.BottomRight, ToolTip = "생각 강도 · 화살표 키로 조절", Margin = new Thickness(4, 4, 4, 20), Style = (Style)FindResource("EffortGauge") };
            var fast = new CheckBox { Content = "ϟ Fast · 빠른 응답", Margin = new Thickness(0, 8, 0, 10) };
            var details = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Silver, FontSize = 12, Margin = new Thickness(0, 0, 0, 16) };
            string[] levels = new string[0]; bool updating = false;
            Action refresh = () => {
                updating = true;
                try {
                    var capability = catalog.Find(model.Text);
                    levels = new[] { "auto" }.Concat(capability == null ? new[] { "low", "medium", "high" } : capability.Efforts.ToArray()).Distinct().ToArray();
                    gauge.Maximum = levels.Length - 1; gauge.Value = Math.Max(0, Array.IndexOf(levels, efforts[selectedProvider]));
                    label.Text = EffortLabel(levels[(int)gauge.Value]);
                    fast.IsEnabled = capability != null && capability.SupportsFast; fast.IsChecked = fast.IsEnabled && speeds[selectedProvider] == "fast";
                    details.Text = (capability == null ? "CLI 기본 또는 직접 입력 모델 · 고급 강도와 Fast 지원 미확인" : capability.DisplayName + (capability.MinimumCliVersion == null ? "" : " · CLI " + capability.MinimumCliVersion + " 이상")) + "\nFast는 계정 지원이 필요하며 사용량이 증가합니다. 생각 강도는 추론량이며 정확도 점수가 아닙니다." + (String.IsNullOrEmpty(catalog.Warning) ? "" : "\n" + catalog.Warning);
                } finally { updating = false; }
            };
            model.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => refresh()));
            gauge.ValueChanged += (s, e) => { if (!updating && levels.Length > 0) label.Text = EffortLabel(levels[Math.Min(levels.Length - 1, (int)gauge.Value)]); };
            panel.Children.Add(new TextBlock { Text = "◉ 생각 강도", FontWeight = FontWeights.SemiBold }); panel.Children.Add(label); panel.Children.Add(gauge);
            panel.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 12) }); panel.Children.Add(fast); panel.Children.Add(details);
            var apply = new Button { Content = "✓ 선택 적용", HorizontalAlignment = HorizontalAlignment.Stretch };
            apply.Click += (s, e) => {
                try {
                    var option = new ProviderExecutionOptions(selectedProvider, model.Text, levels[(int)gauge.Value], fast.IsChecked == true ? "fast" : "standard", catalog);
                    var settings = settingsStore.Load();
                    if (selectedProvider == "Codex") { settings.CodexModel = option.Model; settings.CodexEffort = option.ReasoningEffort; settings.CodexSpeed = option.Speed; }
                    else { settings.ClaudeModel = option.Model; settings.ClaudeEffort = option.ReasoningEffort; settings.ClaudeSpeed = option.Speed; }
                    settingsStore.Save(settings); models[selectedProvider] = option.Model; efforts[selectedProvider] = option.ReasoningEffort; speeds[selectedProvider] = option.Speed;
                    ModelName.Text = option.Model; CloseDropdown(); RefreshExperience();
                } catch (Exception error) { if (dropdownMessage != null) dropdownMessage.Text = error.Message; }
            };
            var footer = new Border { Background = new SolidColorBrush(Color.FromRgb(27, 27, 27)), Padding = new Thickness(10), Margin = new Thickness(0, 12, 0, 0), CornerRadius = new CornerRadius(6), Child = apply };
            var navigation = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            foreach (var item in new[] { new { Title = "☷ 모델", Target = (FrameworkElement)model }, new { Title = "◉ 강도", Target = (FrameworkElement)gauge }, new { Title = "ϟ 속도", Target = (FrameworkElement)fast } }) {
                var target = item.Target; var jump = new Button { Content = item.Title, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(7, 4, 7, 4) };
                jump.Click += (s, e) => { target.BringIntoView(); target.Focus(); }; navigation.Children.Add(jump);
            }
            refresh(); ShowDropdown(anchor, "모델과 응답 방식", panel, navigation, footer);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FastOptionsClicked
        /// desc         : 번개 버튼도 같은 드롭다운의 실제 지원 속도를 선택하도록 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 독립 토글 대신 모델·게이지와 함께 속도 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FastOptionsClicked(object sender, RoutedEventArgs e)
        {
            refreshingOptions = true; try { FastToggle.IsChecked = speeds[selectedProvider] == "fast"; } finally { refreshingOptions = false; }
            ShowModelDropdown(FastToggle);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PermissionsClicked
        /// desc         : 확장의 권한 프리셋을 버튼 위에서 고르고 상세 작업 창은 별도로 연다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 이미지9의 드롭다운과 실제 작업 개별 승인 경계 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PermissionsClicked(object sender, RoutedEventArgs e)
        {
            var body = new StackPanel();
            string current = !workspacePolicy.AllowSourceControl && !workspacePolicy.AllowDatabaseSchema && !workspacePolicy.AllowDeployment ? "승인 요청" : workspacePolicy.AllowSourceControl && workspacePolicy.AllowDatabaseSchema ? workspacePolicy.AllowDeployment ? "전체 권한" : "내 대신 승인" : "사용자 지정";
            foreach (string name in new[] { "승인 요청", "내 대신 승인", "전체 권한" }) {
                var pick = new Button { Content = (name == current ? "✓ " : "○ ") + name, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8) };
                pick.Click += (s, a) => {
                    try {
                        bool basic = name != "승인 요청", deploy = name == "전체 권한";
                        var settings = settingsStore.Load(); settings.AllowSourceControl = basic; settings.AllowDatabaseSchema = basic; settings.AllowDeployment = deploy; settingsStore.Save(settings);
                        workspacePolicy = new WorkspacePermissionPolicy { AllowSourceControl = basic, AllowDatabaseSchema = basic, AllowDeployment = deploy };
                        CloseDropdown(); StatusLabel.Text = name + " 권한을 저장했습니다. 실제 명령은 계획 확인과 개별 실행 동의가 필요합니다.";
                    } catch (Exception error) { if (dropdownMessage != null) dropdownMessage.Text = error.Message; }
                }; body.Children.Add(pick);
            }
            body.Children.Add(new TextBlock { Text = "승인 요청: 세 작업 권한 해제\n내 대신 승인: 형상관리·DB 허용\n전체 권한: 배포까지 허용\n소스 수정안 비교·승인과 명령 실행 동의는 유지됩니다.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Silver, FontSize = 12, Margin = new Thickness(0, 4, 0, 16) });
            var actions = new Button { Content = "⚒ DB·형상관리·배포 작업 열기" };
            actions.Click += (s, a) => { CloseDropdown(); OpenWorkspaceActions(s, a); }; body.Children.Add(actions);
            ShowDropdown(PermissionsButton, "⚿ 작업 권한", body);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AgentPickerClicked
        /// desc         : 고유 A~Z AI를 선택해 실제 대화 대상으로 전환하고 역할 설정을 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 이미지13의 고정 이름 드롭다운과 이전 초안 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AgentPickerClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            if (!scmLookupCompleted) ReadScmIdentity(this, new RoutedEventArgs());
            try {
                var body = new StackPanel();
                foreach (var agent in collaborationStore.ListAssignableAgents(selectedProvider)) {
                    var target = agent;
                    var button = new Button { Content = (activeRecords[selectedProvider].Id == agent.ConversationId ? "✓ " : "○ ") + agent.Name, ToolTip = agent.Role, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 7) };
                    button.Click += (s, a) => { if (ActivateAssignedAgent(target)) CloseDropdown(); }; body.Children.Add(button);
                }
                body.Children.Add(new TextBlock { Text = "선택한 AI의 대화로 이동합니다. AI마다 역할·기록을 유지하며 A~Z를 모두 사용하면 A에게 이어서 지시합니다.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.Silver, Margin = new Thickness(0, 10, 0, 14) });
                var configure = new Button { Content = "⚙ 역할·협업 설정" }; configure.Click += (s, a) => { CloseDropdown(); AgentClicked(s, a); }; body.Children.Add(configure);
                var refresh = new Button { Content = "↻ 계정 이름 다시 확인", Margin = new Thickness(0, 8, 0, 0) };
                refresh.Click += RefreshScmClicked;
                body.Children.Add(refresh);
                ShowDropdown(AgentButton, "♙ AI 지정하여 대화", body);
            } catch (Exception error) { StatusLabel.Text = "AI 목록 확인 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshScmClicked
        /// desc         : 계정 재조회 뒤 아직 열린 같은 목록만 갱신하고 오류를 화면에 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-017 닫힌 선택 창의 지연 재열림과 비동기 이벤트 예외 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async void RefreshScmClicked(object sender, RoutedEventArgs e)
        {
            try {
                var currentPopup = dropdownPopup; var refresh = sender as Button; if (refresh != null) refresh.IsEnabled = false;
                await RefreshScmIdentityAsync(true);
                if (!disposed && currentPopup != null && dropdownPopup == currentPopup && currentPopup.IsOpen) { CloseDropdown(); AgentPickerClicked(AgentButton, new RoutedEventArgs()); }
            } catch (Exception error) { if (!disposed) StatusLabel.Text = "계정 이름 재확인 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivateAssignedAgent
        /// desc         : 저장된 AI 주소로 열린 기록을 재사용하거나 닫힌 기록을 복원한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 대화 전환 전 초안 저장 실패 시 현재 대상을 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool ActivateAssignedAgent(ConversationAgentIdentity agent)
        {
            try {
                if (!PersistCurrentConversation()) return false;
                var record = ResolveAssignedRecord(agent, null);
                if (!conversationRecords[selectedProvider].Any(r => r.Id == record.Id)) conversationRecords[selectedProvider].Add(record);
                activeRecords[selectedProvider] = record; ClearProposal(); ShowChat(); MarkCurrentConversationRead(); UpdateButtons();
                return true;
            } catch (Exception error) { StatusLabel.Text = "AI 대화 전환 실패: " + error.Message; return false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResolveAssignedRecord
        /// desc         : 새 슬롯 또는 보관된 슬롯의 같은 Markdown 주소를 열고 이름 상태를 맞춘다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 최초 시작과 한도 재사용에서도 추가 대화 파일 생성을 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private ConversationRecord ResolveAssignedRecord(ConversationAgentIdentity agent, ConversationRecord created)
        {
            if (created != null) return created;
            System.Collections.Generic.List<ConversationRecord> records;
            var record = conversationRecords.TryGetValue(agent.Provider, out records) ? records.FirstOrDefault(r => r.Id == agent.ConversationId) : null;
            if (record == null) record = workspaceStore.Load(workspaceStore.GetPath(new ConversationRecord { Id = agent.ConversationId, Provider = agent.Provider }), agent.Provider);
            if (!record.IsOpen) { record.IsOpen = true; workspaceStore.Save(record); }
            collaborationStore.SetConversationOpen(record.Id, record.Provider, true); return record;
        }
    }
}
