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
        ///                2026-10-19 CodexOrig4 : 열린 모델·권한 팝업의 표면·본문·오류를 주 화면 팔레트에 동적으로 연결.
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
            dropdownMessage = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
            dropdownMessage.SetResourceReference(TextBlock.ForegroundProperty, "AssistantErrorBrush");
            Grid.SetRow(dropdownMessage, 4); panel.Children.Add(dropdownMessage);
            var surface = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Width = Math.Min(370, SystemParameters.WorkArea.Width - 24), Child = panel };
            surface.Resources.MergedDictionaries.Add(Resources);
            surface.SetResourceReference(Border.BackgroundProperty, "AssistantPopupBackgroundBrush");
            surface.SetResourceReference(Border.BorderBrushProperty, "AssistantPopupBorderBrush");
            surface.SetResourceReference(ForegroundProperty, "AssistantForegroundBrush");
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
        ///                2026-10-19 CodexOrig4 : 강도·모델설명·고정 하단 영역도 같은 동적 팔레트 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ShowModelDropdown(FrameworkElement anchor)
        {
            var catalog = ProviderModelCatalog.ForProvider(selectedProvider);
            var panel = new StackPanel();
            var model = new ComboBox { IsEditable = true, IsTextSearchEnabled = false, Text = ModelName.Text, Margin = new Thickness(0, 4, 0, 16), MinWidth = 160 };
            model.Items.Add(""); foreach (var entry in catalog.Models) model.Items.Add(entry.Id);
            panel.Children.Add(new TextBlock { Text = "☷ 상세 모델 · 직접 입력 가능", FontWeight = FontWeights.SemiBold }); panel.Children.Add(model);
            var label = new TextBlock { FontSize = 19, Margin = new Thickness(0, 4, 0, 8) };
            label.SetResourceReference(TextBlock.ForegroundProperty, "AssistantLinkBrush");
            var gauge = new Slider { Minimum = 0, SmallChange = 1, LargeChange = 1, TickFrequency = 1, IsSnapToTickEnabled = true, TickPlacement = TickPlacement.BottomRight, ToolTip = "생각 강도 · 화살표 키로 조절", Margin = new Thickness(4, 4, 4, 20), Style = (Style)FindResource("EffortGauge") };
            var fast = new CheckBox { Content = "ϟ Fast · 빠른 응답", Margin = new Thickness(0, 8, 0, 10) };
            var details = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 16) };
            details.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush");
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
            var footer = new Border { Padding = new Thickness(10), Margin = new Thickness(0, 12, 0, 0), CornerRadius = new CornerRadius(6), Child = apply };
            footer.SetResourceReference(Border.BackgroundProperty, "AssistantSurfaceBrush");
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
        /// update date  : 2026-10-18 CodexCode : TASK-021 추가 이미지 요구와 감사 보완 흐름 반영.
        ///                2026-10-19 CodexOrig4 : 권한 설명을 동적 보조 텍스트 자원으로 통일.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PermissionsClicked(object sender, RoutedEventArgs e)
        {
            var body = new StackPanel();
            string current = AgentFullAccessEnabled ? "전체 액세스" : !workspacePolicy.AllowSourceControl && !workspacePolicy.AllowDatabaseSchema && !workspacePolicy.AllowDeployment ? "승인 요청" : "작업 도구 허용";
            foreach (string name in new[] { "승인 요청", "작업 도구 허용", "전체 액세스" }) {
                var pick = new Button { Content = (name == current ? "✓ " : "○ ") + name, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8) };
                pick.Click += (s, a) => {
                    var previousPolicy = workspacePolicy;
                    try {
                        bool basic = name != "승인 요청", deploy = name == "전체 액세스";
                        var settings = settingsStore.Load();
                        settings.AllowSourceControl = basic; settings.AllowDatabaseSchema = basic; settings.AllowDeployment = deploy; settings.AgentFullAccess = deploy;
                        settingsStore.Save(settings);
                        workspacePolicy = new WorkspacePermissionPolicy { AllowSourceControl = basic, AllowDatabaseSchema = basic, AllowDeployment = deploy };
                        SetAgentFullAccess(deploy);
                        CloseDropdown(); StatusLabel.Text = deploy ? "전체 액세스를 선택했습니다. 다음 요청부터 AI가 현재 Windows 사용자 권한으로 파일 수정·명령·자체 테스트를 수행합니다." : name + " 권한을 저장했습니다. AI 수정안은 비교·승인 후 적용하며 작업 도구의 명령은 별도로 실행합니다.";
                    } catch (Exception error) { workspacePolicy = previousPolicy; if (dropdownMessage != null) dropdownMessage.Text = error.Message; }
                }; body.Children.Add(pick);
            }
            var permissionDescription = CreateThemedDescription("승인 요청: AI가 수정안을 제안하고 사용자가 비교·적용·저장합니다.\n작업 도구 허용: 같은 검토 방식에 DB 스키마·형상관리 도구를 추가합니다.\n전체 액세스: AI가 소스를 직접 수정하고 명령·빌드·테스트를 이어서 실행합니다. 현재 Windows 사용자에게 허용된 파일과 네트워크에 접근하며 관리자 권한을 자동 획득하지 않습니다.\n요청 중에는 ■ 중지로 실행을 취소할 수 있습니다. 실행된 파일 변경은 변경 카드에서 확인하세요. 다음 요청 전에 권한을 다시 낮출 수 있습니다.", new Thickness(0, 4, 0, 16));
            permissionDescription.FontSize = 12; body.Children.Add(permissionDescription);
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
                var agentDescription = CreateThemedDescription("선택한 AI의 대화로 이동합니다. AI마다 역할·기록을 유지하며 A~Z를 모두 사용하면 A에게 이어서 지시합니다.", new Thickness(0, 10, 0, 14));
                agentDescription.FontSize = 12; body.Children.Add(agentDescription);
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
        // WPF RoutedEventHandler는 void 계약이며 이 핸들러는 await 전체의 예외를 아래 catch에서 처리한다.
#pragma warning disable VSTHRD100
        private async void RefreshScmClicked(object sender, RoutedEventArgs e)
        {
            try {
                var currentPopup = dropdownPopup; var refresh = sender as Button; if (refresh != null) refresh.IsEnabled = false;
                await RefreshScmIdentityAsync(true);
                if (!disposed && currentPopup != null && dropdownPopup == currentPopup && currentPopup.IsOpen) { CloseDropdown(); AgentPickerClicked(AgentButton, new RoutedEventArgs()); }
            } catch (Exception error) { if (!disposed) StatusLabel.Text = "계정 이름 재확인 실패: " + error.Message; }
        }
#pragma warning restore VSTHRD100

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivateAssignedAgent
        /// desc         : 저장된 AI 주소로 열린 기록을 재사용하거나 닫힌 기록을 복원한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 대화 전환 전 초안 저장 실패 시 현재 대상을 보존.
        ///                2026-10-20 CodexCode : 담당 AI 재선택 시 방의 미승인 수정안을 다시 표시.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool ActivateAssignedAgent(ConversationAgentIdentity agent)
        {
            try {
                if (!PersistCurrentConversation()) return false;
                var record = ResolveAssignedRecord(agent, null);
                if (!conversationRecords[selectedProvider].Any(r => r.Id == record.Id)) conversationRecords[selectedProvider].Add(record);
                activeRecords[selectedProvider] = record; ClearProposal(); ShowChat(); MarkCurrentConversationRead(); RestoreRoomPresentation(); UpdateButtons();
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
