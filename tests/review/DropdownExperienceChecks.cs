namespace CodexCode.WorkspaceReview
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Threading;
    using System.IO;
    using VS2017.AIAssistant.Chat;
    using VS2017.AIAssistant.Views;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WorkspaceExperience
    /// desc         : 이미지 요구의 드롭다운·목차·하단 배치를 실제 WPF 표면에서 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-018 모달과 구분되는 열린 Popup 검사 기반 추가.
    ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static partial class WorkspaceExperience
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DropdownRoots
        /// desc         : 현재 검사 프로세스의 열린 WPF 드롭다운 표면만 찾는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 별도 Window가 아닌 PopupRoot의 실제 표시 확인.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static FrameworkElement[] DropdownRoots()
        {
            return PresentationSource.CurrentSources.OfType<HwndSource>().Select(s => s.RootVisual).OfType<FrameworkElement>()
                .Where(root => root.IsVisible && root.GetType().Name == "PopupRoot").ToArray();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenDropdown
        /// desc         : 실제 버튼을 눌러 anchor 드롭다운이 열렸는지 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : modal 검사 재사용 없이 비동기 배치 완료 대기.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static FrameworkElement OpenDropdown(AssistantControl control, string buttonName)
        {
            var button = control.FindName(buttonName) as ButtonBase;
            if (button == null) throw new InvalidOperationException("Dropdown button is missing: " + buttonName);
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            PumpUntil(() => DropdownRoots().Length > 0, 2500);
            FrameworkElement root = DropdownRoots().Last(); root.UpdateLayout();
            if (PresentationSource.CurrentSources.OfType<HwndSource>().Any(s => s.RootVisual is AssistantDialog && ((AssistantDialog)s.RootVisual).IsVisible))
                throw new InvalidOperationException("A modal dialog replaced the requested anchor dropdown: " + buttonName);
            return root;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InspectDropdown
        /// desc         : 실제 드롭다운의 선택 결과를 검사하고 실패해도 검사 소유 메뉴를 정리한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-018 모델·강도·Fast·권한 공통 사용자 경로.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void InspectDropdown(AssistantControl control, string buttonName, Action<FrameworkElement> inspect)
        {
            try { inspect(OpenDropdown(control, buttonName)); }
            finally { CloseDropdowns(control); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseDropdowns
        /// desc         : 테스트 소유 Popup을 닫아 다음 검사에 열린 메뉴를 남기지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 실패 경로에서도 검사 인스턴스의 Popup만 정리.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CloseDropdowns(AssistantControl control)
        {
            foreach (var field in typeof(AssistantControl).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)) {
                var popup = field.GetValue(control) as Popup;
                if (popup != null) popup.IsOpen = false;
                var menu = field.GetValue(control) as ContextMenu;
                if (menu != null) menu.IsOpen = false;
            }
            control.UpdateLayout();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckToolbarPlacement
        /// desc         : 대화 버튼이 목록 하단에 있고 좁은 폭에서 가로 스크롤로 접근 가능한지 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : image-13 하단 정렬과 누락 없는 버튼 접근 검증.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckToolbarPlacement(AssistantControl control, Window owner)
        {
            var toggle = (ToggleButton)control.FindName("HistoryToggle"); toggle.IsChecked = true; toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            owner.UpdateLayout();
            var panel = (FrameworkElement)control.FindName("HistoryPanel");
            var buttons = new[] { "NewChatButton", "LoadChatButton", "AgentButton" }.Select(n => (FrameworkElement)control.FindName(n)).ToArray();
            Check(buttons.All(b => panel.IsAncestorOf(b)), "Conversation actions belong to the conversation sidebar rather than the top action row");
            var scroll = Descendants(panel).OfType<ScrollViewer>().FirstOrDefault(v => buttons.All(b => v.IsAncestorOf(b)));
            Check(scroll != null && scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto && scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled,
                "The sidebar action row provides horizontal scrolling when the buttons exceed its width");
            Point listBottom = ((FrameworkElement)control.FindName("ConversationList")).TranslatePoint(new Point(0, ((FrameworkElement)control.FindName("ConversationList")).ActualHeight), panel);
            Check(buttons.All(b => b.TranslatePoint(new Point(0, 0), panel).Y >= listBottom.Y - 2), "Conversation actions stay below the visible conversation list");
            Check(Convert.ToString(((ContentControl)control.FindName("AgentButton")).Content).Contains("AI 지정하여 대화"), "A single AI selection dropdown replaces name-specific action buttons");
            scroll.ScrollToRightEnd(); owner.UpdateLayout();
            Point agentRight = buttons[2].TranslatePoint(new Point(buttons[2].ActualWidth, 0), scroll);
            Check(scroll.ScrollableWidth > 0 && agentRight.X <= scroll.ActualWidth + 2, "Horizontal scrolling brings the complete AI selector into the narrow sidebar viewport");
            scroll.ScrollToHome();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckGuideAndSettingsImages
        /// desc         : 안내 카드의 실제 hover·그림 상세·뒤로가기와 목차 스크롤·하단 분리를 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-018 image12·15·16 사용자 결과 확인.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        ///                2026-10-19 CodexOrig : 상세 설명의 Markdown 원문과 실제 렌더 문서, 동봉 그림을 함께 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckGuideAndSettingsImages(AssistantControl control, string output)
        {
            InspectModal(() => Click((Button)control.FindName("HelpButton")), dialog => {
                var cards = Descendants(dialog.Body).OfType<Button>().Where(b => b.DataContext is GuideStep).ToArray();
                Check(cards.Length == 4, "The user guide presents four actionable illustrated procedure cards");
                var selected = cards[1]; var originalPosition = selected.TranslatePoint(new Point(0, 0), dialog.Body);
                selected.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
                PumpUntil(() => ((Border)cards[0].Content).Opacity < 0.6, 1500);
                Check(((Border)selected.Content).Opacity > 0.95 && Panel.GetZIndex(selected) > Panel.GetZIndex(cards[0]) && ((TranslateTransform)((Border)selected.Content).RenderTransform).Y < -1,
                    "Hovering a guide card brings it forward and dims the other cards");
                Check(selected.TranslatePoint(new Point(0, 0), dialog.Body) == originalPosition && !((Border)selected.Content).IsHitTestVisible,
                    "Guide animation keeps its clickable area fixed so moving content cannot trigger a hover loop");
                Capture(dialog, Path.Combine(output, "Guide-hover.png")); Click(selected); dialog.UpdateLayout();
                var preview = Descendants(dialog.Body).OfType<MarkdownPreview>().Single();
                var step = (GuideStep)selected.DataContext;
                Check(Descendants(dialog.Body).OfType<Image>().Any(i => i.Source != null && i.ActualHeight > 20) &&
                    preview.Markdown == String.Join("\n\n", step.Details) &&
                    new System.Windows.Documents.TextRange(preview.TextView.Document.ContentStart, preview.TextView.Document.ContentEnd).Text.Length > 30,
                    "Clicking a guide card opens its packaged illustration and renders the complete detailed Markdown in the same window");
                Capture(dialog, Path.Combine(output, "Guide-detail.png"));
                Click(Descendants(dialog.Body).OfType<Button>().Single(b => Convert.ToString(b.Content) == "← 뒤로" && Convert.ToString(b.ToolTip) == "안내 목록으로 돌아가기"));
                PumpUntil(() => Descendants(dialog.Body).OfType<Button>().Count(b => b.DataContext is GuideStep) == 4 && selected.IsKeyboardFocusWithin, 1500);
                Check(selected.IsKeyboardFocusWithin, "Returning from guide detail restores the original card and keyboard focus");
            });
            InspectModal(() => Click((Button)control.FindName("CliHelpButton")), dialog => {
                var cards = Descendants(dialog.Body).OfType<Button>().Where(b => b.DataContext is GuideStep).ToArray();
                Check(cards.Length == 4, "CLI setup uses the same interactive four-step explanation gallery");
                Click(cards[0]); dialog.UpdateLayout();
                Check(Descendants(dialog.Body).OfType<Image>().Any(i => i.Source != null), "The CLI installation step opens a procedure illustration rather than another modal window");
                Capture(dialog, Path.Combine(output, "Cli-detail.png"));
            });
            InspectModal(() => Click((Button)control.FindName("SettingsButton")), dialog => {
                dialog.Height = Math.Min(600, dialog.MaxHeight); dialog.UpdateLayout();
                var topics = dialog.SidebarNavigation.Children.OfType<ToggleButton>().ToArray();
                Check(topics.Length == 4 && dialog.SidebarNavigation.IsVisible && topics.All(b => Convert.ToString(b.Content).Any(c => !Char.IsLetterOrDigit(c) && !Char.IsWhiteSpace(c))),
                    "Settings exposes four icon-labeled pages in a left sidebar");
                var scroll = (ScrollViewer)control.FindName("SettingsContent");
                topics.Last().RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); dialog.UpdateLayout();
                Check(topics.Last().IsChecked == true && topics.Take(3).All(b=>b.IsChecked==false) && ((FrameworkElement)control.FindName("DisplaySection")).IsVisible && !((FrameworkElement)control.FindName("InstructionSection")).IsVisible,
                    "Selecting the display page shows its controls and hides the other sections while retaining navigation");
                var footerColor = ((SolidColorBrush)dialog.Footer.Background).Color;
                var bodyColor = ((SolidColorBrush)dialog.Background).Color;
                Check(footerColor != bodyColor && dialog.Footer.BorderThickness.Top > 0 && !scroll.IsAncestorOf(dialog.Footer),
                    "Save and close remain in a visually distinct fixed footer outside the scrolling input body");
                Capture(dialog, Path.Combine(output, "Settings-index.png"));
            });
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckFanMenuStability
        /// desc         : 반복 진입 시 한 번만 열리고 부채꼴 버튼이 원점을 가리지 않는지 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-018 image14 이벤트·배치·Escape 복구 확인.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckFanMenuStability(AssistantControl control, Window owner)
        {
            PrivateCall(control, "CloseFan");
            var toggle = (ToggleButton)control.FindName("OptionsToggle"); var fan = (Canvas)control.FindName("FanPanel");
            var countField = typeof(AssistantControl).GetField("fanOpenCount", BindingFlags.Instance | BindingFlags.NonPublic);
            int before = (int)countField.GetValue(control);
            for (int i = 0; i < 15; i++) toggle.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
            owner.UpdateLayout();
            var intent = (DispatcherTimer)typeof(AssistantControl).GetField("fanOpenTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control);
            Check(!fan.IsVisible && (int)countField.GetValue(control) == before && intent != null && intent.IsEnabled && intent.Interval.TotalMilliseconds == 220,
                "Repeated managed pointer-enter events schedule one 220ms hover intent without opening immediately");
            // Managed MouseEnter does not move the real pointer. Exercise the separate explicit activation path.
            PrivateCall(control, "OpenFanMenu"); owner.UpdateLayout();
            Check(fan.IsVisible && (int)countField.GetValue(control) == before + 1 && !intent.IsEnabled,
                "Explicit activation opens once and consumes pending hover intent");
            for (int i = 0; i < 15; i++) toggle.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
            Check((int)countField.GetValue(control) == before + 1, "Further enter events leave the open fan animation running without restarting it");
            var help = (Button)control.FindName("HelpButton"); var settings = (Button)control.FindName("SettingsButton");
            var helpHit = fan.InputHitTest(new Point(143, 100)) as DependencyObject;
            var settingsHit = fan.InputHitTest(new Point(206, 141)) as DependencyObject;
            Check(help.Clip != null && settings.Clip != null &&
                (ReferenceEquals(helpHit, help) || helpHit != null && help.IsAncestorOf(helpHit)) &&
                (ReferenceEquals(settingsHit, settings) || settingsHit != null && settings.IsAncestorOf(settingsHit)),
                "The actual fan sectors route their different angles to distinct help and settings buttons");
            var point = toggle.TranslatePoint(new Point(toggle.ActualWidth / 2, toggle.ActualHeight / 2), fan);
            Check(fan.InputHitTest(point) == null, "The fan overlay cannot intercept the original toggle hit area");
            toggle.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseLeaveEvent });
            fan.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
            Check(fan.IsVisible && (int)countField.GetValue(control) == before + 1, "Crossing from the trigger to the fan cancels delayed closing without a reopen loop");
            control.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(control), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(!fan.IsVisible && toggle.IsChecked != true, "Escape closes the fan and clears its toggle state");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckTooltipTheme
        /// desc         : 실제 도구 설명 팝업의 검정 팔레트와 긴 안내 줄바꿈·크기 제한을 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-018 작업 중 추가된 Role9 tooltip 요구 확인.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckTooltipTheme(AssistantControl control, string output)
        {
            var button = (Button)control.FindName("ModelOptionsButton"); object original = button.ToolTip;
            var tip = new ToolTip { Content = string.Join(" ", Enumerable.Repeat("모델이 지원하는 생각 강도와 응답 속도를 확인하세요.", 14)), PlacementTarget = button, StaysOpen = true };
            try {
                button.ToolTip = tip; tip.IsOpen = true;
                PumpUntil(() => tip.IsVisible && tip.ActualWidth > 0, 1500);
                var background = tip.Background as SolidColorBrush; var foreground = tip.Foreground as SolidColorBrush;
                Check(background != null && foreground != null && background.Color.R < 65 && background.Color.G < 65 && background.Color.B < 65 && foreground.Color.R > 170 && tip.Padding.Left >= 6,
                    "An actual tooltip inherits the product's dark background, readable text and padding");
                Check(tip.ActualWidth <= 430 && tip.ActualHeight > 70 && Descendants(tip).OfType<TextBlock>().Any(t => t.TextWrapping != TextWrapping.NoWrap),
                    "A long actual tooltip wraps inside a bounded popup instead of extending across the screen");
                Capture(tip, Path.Combine(output, "Tooltip-theme.png"));
            }
            finally { tip.IsOpen = false; button.ToolTip = original; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckAlphabetRouting
        /// desc         : 실제 새 대화 버튼과 AI 목록으로 A~Z 제한·A 복귀·요청 대상과 기록 보존을 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-018 27번째 요청의 기존 A 라우팅 회귀.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckAlphabetRouting(string output)
        {
            string oldSettings = Environment.GetEnvironmentVariable("VS2017_AI_SETTINGS_PATH"), oldWorkspace = Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH");
            AssistantControl control = null; Window owner = null;
            string folder = Path.Combine(output, "alphabet-routing"); Directory.CreateDirectory(folder);
            try {
                Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", Path.Combine(folder, "settings.json")); Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", Path.Combine(folder, "workspace"));
                new AssistantSettingsStore().Save(new AssistantSettings { HideWelcome = true });
                var source = new SyntheticDocument(Path.Combine(folder, "Quota.cs")); var provider = new CollaborationProvider();
                control = new AssistantControl(); control.Initialize(new SyntheticDocuments { Document = source }, provider);
                typeof(AssistantControl).GetField("scmLookupStarted", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(control, true);
                new ConversationCollaborationStore(new MarkdownWorkspaceStore().RootPath).ConfigureNaming("Codex", "검증사용자");
                owner = new Window { Content = control, Width = 820, Height = 850 }; owner.Show(); owner.UpdateLayout();
                var tabs = (TabControl)control.FindName("ProviderChoice"); tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(t => Convert.ToString(t.Tag) == "Codex");
                var prompt = (TextBox)control.FindName("Instruction"); var list = (ListBox)control.FindName("ConversationList");
                var first = (ConversationRecord)list.SelectedItem; prompt.Text = "A에 보존할 작성 중 요청";
                for (int i = 1; i < 26; i++) Click((Button)control.FindName("NewChatButton"));
                var store = new MarkdownWorkspaceStore(); var collaboration = new ConversationCollaborationStore(store.RootPath);
                var agents = collaboration.ListAssignableAgents("Codex"); var primary = agents.Single(a => a.Letter == "A");
                Check(agents.Length == 26 && agents.Select(a => a.Letter).SequenceEqual(Enumerable.Range(0, 26).Select(n => ((char)('A' + n)).ToString())) && primary.ConversationId == first.Id,
                    "Twenty-six real new-conversation actions produce one permanent A-Z AI address per letter");
                Check(agents.All(a => a.Name.EndsWith("코덱스" + a.Letter)) && agents.Select(a => a.Name).Distinct().Count() == 26,
                    "Every assignable AI name follows the user/provider/single-letter format without duplicate names");
                var second = agents.Single(a => a.Letter == "B");
                InspectDropdown(control, "AgentButton", root => Click(Descendants(root).OfType<Button>().Single(b => Convert.ToString(b.Content).EndsWith(second.Name))));
                Check(((ConversationRecord)list.SelectedItem).Id == second.ConversationId && store.LoadOpen("Codex").Single(r => r.Id == first.Id).Draft == "A에 보존할 작성 중 요청",
                    "Choosing B from the actual AI dropdown switches to B while preserving A's draft");
                InspectDropdown(control, "AgentButton", root => Click(Descendants(root).OfType<Button>().Single(b => Convert.ToString(b.Content).EndsWith(primary.Name))));
                PrivateCall(control, "CloseChatClicked", control, new RoutedEventArgs());
                Check(!collaboration.GetAgent(first.Id, "Codex").IsOpen && store.LoadOpen("Codex").Length == 25,
                    "Closing A archives its conversation without freeing or replacing its permanent AI address");
                Click((Button)control.FindName("NewChatButton"));
                Check(((ConversationRecord)list.SelectedItem).Id == first.Id && prompt.Text == "A에 보존할 작성 중 요청" && store.LoadOpen("Codex").Length == 26 && Directory.GetFiles(Path.Combine(store.RootPath, "conversations", "codex"), "*.md").Length == 26,
                    "The twenty-seventh new request reopens the original A and its draft without creating or deleting conversation files");
                prompt.Text = "A로 요청을 전달해 주세요"; Click((Button)control.FindName("GenerateButton"));
                PumpUntil(() => provider.Calls.Count == 1 && !((Button)control.FindName("StopButton")).IsEnabled && ((Button)control.FindName("GenerateButton")).IsEnabled, 4000);
                Check(provider.Calls[0].AgentContext.Contains(primary.Name) && store.LoadOpen("Codex").Single(r => r.Id == first.Id).State == ConversationRunState.Completed && source.ReplaceCount == 0,
                    "A request after the A-Z limit reaches A's actual provider context and leaves source unchanged before approval");
                OpenDropdown(control, "AgentButton");
                var closeTimer = (DispatcherTimer)typeof(AssistantControl).GetField("fanCloseTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control);
                control.Dispose();
                PumpUntil(() => DropdownRoots().Length == 0, 1200);
                Check(DropdownRoots().Length == 0 && !closeTimer.IsEnabled,
                    "Disposing a conversation closes its dropdown and stops the fan's pending timer");
            }
            finally {
                if (control != null) control.Dispose(); if (owner != null) owner.Close();
                Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", oldSettings); Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", oldWorkspace);
            }
        }
    }
}
