using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// SOURCE_ONLY: 테마·Thinking 제품 소스의 독립 컴파일 전용 host. 실제 제품 통합 검증과 구분한다.
namespace VS2017.AIAssistant.Chat
{
    public sealed class AssistantSettings { public string ThemeMode { get; set; } = "dark"; }
}
namespace VS2017.AIAssistant.Views
{
    public static class ThemeFixtureResources { public static ResourceDictionary Current; }
    public static class HoverScrollPolicy
    {
        public static readonly DependencyProperty ContainMouseWheelProperty = DependencyProperty.RegisterAttached("ContainMouseWheel", typeof(bool), typeof(HoverScrollPolicy));
        public static void SetContainMouseWheel(DependencyObject target, bool value) { target.SetValue(ContainMouseWheelProperty, value); }
        public static bool GetContainMouseWheel(DependencyObject target) { return (bool)target.GetValue(ContainMouseWheelProperty); }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 실제 테마 정책·팔레트·변경 알림·Thinking 소스를 실행하는 최소 메모리 fixture.
    /// author       : CodexOrig4
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig4 : 사용자 설정·실제 계정·IDE·GUI 동작을 대체하지 않는다고 명시.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl : UserControl, IDisposable
    {
        private bool disposed;
        private readonly ComboBox ThemeModeChoice = new ComboBox { SelectedValuePath = "Tag" };
        private readonly TextBlock ThinkingIndicator = new TextBlock { Text = "생각하는 중…" }, ThinkingAboveIndicator = new TextBlock();
        private readonly ScrollViewer ConversationScroll = new ScrollViewer();

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantControl
        /// desc         : 공통 XAML 자원을 사용하는 대표 컨트롤을 화면 표시 없이 생성한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 입력·버튼·선택·스크롤·메뉴·툴팁 대표 상태 포함.
        /// </summary>
        /// ---------------------------------------------------------------------
        public AssistantControl()
        {
            Resources.MergedDictionaries.Add(ThemeFixtureResources.Current);
            foreach (string mode in new[] { "auto", "light", "dark" }) ThemeModeChoice.Items.Add(new ComboBoxItem { Tag = mode, Content = mode });
            ThemeModeChoice.SelectionChanged += ThemeModeChanged;
            NameScope.SetNameScope(this, new NameScope()); RegisterName("ThemeModeChoice", ThemeModeChoice);
            var panel = new StackPanel { Margin = new Thickness(22) };
            panel.Children.Add(new TextBlock { Text = "Claude · Codex", FontSize = 23, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 14) });
            panel.Children.Add(ThemeModeChoice);
            panel.Children.Add(new TextBox { Text = "프로젝트 검토 요청 / Markdown 미리보기", Margin = new Thickness(0, 12, 0, 12) });
            var buttons = new WrapPanel(); buttons.Children.Add(new Button { Content = "설정", Margin = new Thickness(0, 0, 10, 0) });
            buttons.Children.Add(new Button { Content = "사용 안내", Style = (Style)FindResource("GuideActionButton"), Margin = new Thickness(0, 0, 10, 0) });
            buttons.Children.Add(new Button { Content = "CLI 설치", Style = (Style)FindResource("InstallActionButton") }); panel.Children.Add(buttons);
            panel.Children.Add(new CheckBox { Content = "다음 실행에도 화면 설정 유지", IsChecked = true, Margin = new Thickness(0, 14, 0, 14) });
            var history = new ListBox { Height = 94, Style = (Style)FindResource("DarkHistoryListBox") }; history.Items.Add("선택한 대화 · 코드 검토"); history.Items.Add("지난 대화 · 테스트 결과"); history.SelectedIndex = 0; panel.Children.Add(history);
            var link = new TextBlock { Text = "↗ Source.cs:12 · 소스 열기", Margin = new Thickness(0, 14, 0, 12) }; link.SetResourceReference(TextBlock.ForegroundProperty, "AssistantLinkBrush"); panel.Children.Add(link);
            ThinkingIndicator.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush"); panel.Children.Add(ThinkingIndicator);
            Content = panel; Width = 640; Height = 405;
            InitializeTheme(new Chat.AssistantSettings());
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsConversationAtEnd
        /// desc         : fixture의 바닥 위치만 실제 Thinking 메서드에 전달한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 실제 Viewport 통합 검증과 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsConversationAtEnd(double offset, double height) { return height - offset < 2; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 검사 뒤 실제 제품 테마·애니메이션 해제 메서드를 호출한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : static 구독과 시계 잔존 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose() { if (disposed) return; UpdateThinkingAnimation(false); DisposeTheme(); disposed = true; }
    }
}
