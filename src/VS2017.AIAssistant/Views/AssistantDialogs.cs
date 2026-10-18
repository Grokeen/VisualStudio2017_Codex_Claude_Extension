using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantDialog
    /// desc         : 설정과 사용 안내에 동일한 테마·화면 중앙 배치·스크롤·닫기 버튼을 제공한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : VS 소유 창으로 열어 모달 입력과 키보드 닫기를 지원.
    /// </summary>
    /// ---------------------------------------------------------------------
    public class AssistantDialog : Window
    {
        public WrapPanel Actions { get; private set; }
        public TextBlock Message { get; private set; }
        public ContentControl Body { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantDialog
        /// desc         : 제목·본문·고정 하단 동작을 작업 영역 크기에 맞춰 배치한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : CenterScreen과 VS 테마 리소스를 공통 적용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public AssistantDialog(AssistantControl source, string title, string description)
        {
            Dispatcher.VerifyAccess();
            Title = title; Width = 560; Height = 660; MinWidth = 300; MinHeight = 280;
            MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
            Width = Math.Min(Width, MaxWidth); Height = Math.Min(Height, MaxHeight);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false; ResizeMode = ResizeMode.CanResize; FontSize = 14;
            Resources.MergedDictionaries.Add(source.Resources);
            SetResourceReference(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
            SetResourceReference(ForegroundProperty, VsBrushes.ToolWindowTextKey);
            var owner = GetWindow(source);
            if (owner != null) Owner = owner;
            else
            {
                var shell = Package.GetGlobalService(typeof(SVsUIShell)) as IVsUIShell;
                IntPtr handle;
                if (shell != null && shell.GetDialogOwnerHwnd(out handle) >= 0 && handle != IntPtr.Zero)
                    new WindowInteropHelper(this).Owner = handle;
            }
            var layout = new Grid { Margin = new Thickness(20) };
            layout.SetResourceReference(Panel.BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition());
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            header.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.SemiBold });
            header.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
            layout.Children.Add(header);
            Body = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Grid.SetRow(Body, 1); layout.Children.Add(Body);
            var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            Message = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            footer.Children.Add(Message);
            Actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            var close = new Button { Content = "× 닫기", IsCancel = true, MinWidth = 84, Margin = new Thickness(8, 0, 0, 0) };
            close.Click += (sender, args) => Close();
            Actions.Children.Add(close); footer.Children.Add(Actions);
            Grid.SetRow(footer, 2); layout.Children.Add(footer); Content = layout;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GettingStartedDialog
    /// desc         : 처음 사용하는 사람이 네 단계 그림형 카드로 요청·검토·저장을 익히도록 안내한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 다시 보지 않기와 계정 설정 바로 가기 제공.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class GettingStartedDialog : AssistantDialog
    {
        public CheckBox HideNextTime { get; private set; }
        public Button StartButton { get; private set; }
        public Button ConnectButton { get; private set; }
        public bool OpenSettings { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GettingStartedDialog
        /// desc         : 번호·큰 기호·실제 버튼 이름을 사용해 사용 순서를 짧게 표현한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 수동 재열기에서도 저장된 안내 표시 선택을 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        public GettingStartedDialog(AssistantControl source, bool hidden)
            : base(source, "? Chat 사용 안내", "파일을 열고, 요청하고, 비교한 뒤 저장하세요.")
        {
            var steps = new StackPanel();
            AddStep(steps, "1", "⚙", "AI 연결", "Codex 또는 Claude Code 탭을 고르고, 설정에서 ‘계정 연결’ → ‘연결 확인’을 누르세요.");
            AddStep(steps, "2", "✎", "현재 파일에 요청", "소스 파일을 선택한 뒤 Chat으로 돌아와 요청을 입력하고 ‘보내기’를 누르세요. 이미지·텍스트도 첨부할 수 있습니다.");
            AddStep(steps, "3", "✓", "변경 비교와 승인", "‘수정안 검토’에서 원본과 변경을 확인하세요. 동의 체크 후 ‘승인 후 적용’을 눌러 편집기에 반영합니다.");
            AddStep(steps, "4", "▣", "직접 저장", "‘적용 내용 저장’을 눌러 파일에 저장하세요. ‘직전 적용 되돌리기’로 편집기 내용을 되돌릴 수 있습니다.");
            HideNextTime = new CheckBox { Content = "다시 보지 않기", IsChecked = hidden, Margin = new Thickness(0, 10, 0, 8) };
            steps.Children.Add(HideNextTime);
            steps.Children.Add(new TextBlock { Text = "언제든 상단 ‘사용 안내’ 또는 우클릭 메뉴에서 다시 열 수 있습니다.", TextWrapping = TextWrapping.Wrap });
            Body.Content = new ScrollViewer { Content = steps, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            ConnectButton = new Button { Content = "⚙ 계정 설정 열기", Margin = new Thickness(0, 0, 8, 0) };
            StartButton = new Button { Content = "✓ 시작하기", IsDefault = true, Style = (Style)FindResource("PrimaryButton") };
            ConnectButton.Click += (sender, args) => { OpenSettings = true; Close(); };
            StartButton.Click += (sender, args) => Close();
            Actions.Children.Clear(); Actions.Children.Add(ConnectButton); Actions.Children.Add(StartButton);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddStep
        /// desc         : 좁은 화면에서도 줄바꿈되는 번호·기호·제목·행동 카드 한 개를 만든다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 참고 포스터의 단계별 시각 안내를 프로그램 절차로 구성.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void AddStep(Panel parent, string number, string icon, string title, string text)
        {
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) }); row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new TextBlock { Text = number + "\n" + icon, FontSize = 24, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Foreground = System.Windows.Media.Brushes.RoyalBlue });
            var words = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            words.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 16 });
            words.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            Grid.SetColumn(words, 1); row.Children.Add(words);
            parent.Children.Add(new Border { Child = row, BorderBrush = System.Windows.Media.Brushes.Gray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 10) });
        }
    }
}
