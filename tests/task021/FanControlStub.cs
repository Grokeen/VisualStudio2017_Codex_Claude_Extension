using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 공유 제품 빌드 없이 실제 Fan 소스를 검사하는 최소 WPF 화면을 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-021 서비스·계정·개인 설정을 읽지 않는 독립 fixture.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl : UserControl
    {
        private bool busy, disposed;
        private readonly Canvas FanPanel = new Canvas { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        private readonly Button SettingsButton = new Button { Name = "SettingsButton", Content = "⚙ 설정", ToolTip = "계정·규칙·글자 크기" };
        private readonly Button HelpButton = new Button { Name = "HelpButton", Content = "? 사용 안내", ToolTip = "사용 안내" };
        // 실제 후보 제품의 스크롤바·본문 여백을 포함한 원점 경계를 재현한다.
        private readonly ToggleButton OptionsToggle = new ToggleButton { Content = "☷", Width = 32, Height = 30.62, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(29, 12, 29, 0) };

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantControl
        /// desc         : 실제 화면과 같은 이름의 메뉴 원점·Canvas·기존 버튼만 초기화한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 실제 클릭 핸들러 연결을 가정하지 않는 기하 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        public AssistantControl()
        {
            busy = false; disposed = false; FontSize = 14; Background = new SolidColorBrush(Color.FromRgb(23, 23, 23)); Foreground = Brushes.WhiteSmoke;
            NameScope.SetNameScope(this, new NameScope());
            RegisterName("FanPanel", FanPanel); RegisterName("SettingsButton", SettingsButton); RegisterName("HelpButton", HelpButton); RegisterName("OptionsToggle", OptionsToggle);
            var layout = new Grid(); layout.Children.Add(OptionsToggle);
            FanPanel.Children.Add(new Border()); FanPanel.Children.Add(SettingsButton); FanPanel.Children.Add(HelpButton);
            layout.Children.Add(FanPanel); Content = layout;
        }
    }
}
