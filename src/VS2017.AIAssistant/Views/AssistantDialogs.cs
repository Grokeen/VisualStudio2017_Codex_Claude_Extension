using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DialogSection
    /// desc         : 고정 목차의 아이콘·표시 제목·실제 스크롤 대상을 묶는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-017 공통 팝업 목차 계약 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class DialogSection
    {
        public string Icon { get; private set; }
        public string Title { get; private set; }
        public FrameworkElement Target { get; private set; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DialogSection
        /// desc         : 제목과 이동 대상이 있는 불변 목차 항목을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 컨트롤 참조로 이동.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DialogSection(string icon, string title, FrameworkElement target)
        {
            if (String.IsNullOrWhiteSpace(title) || target == null) throw new ArgumentException("목차 제목과 이동 대상이 필요합니다.");
            Icon = icon ?? ""; Title = title; Target = target;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GuideStep
    /// desc         : 안내 카드 요약과 상세 설명을 별도로 보관한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실행 동작 없이 설명과 절차 그림만 표시.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class GuideStep
    {
        public string Number { get; private set; }
        public string Icon { get; private set; }
        public string Title { get; private set; }
        public string Summary { get; private set; }
        public ReadOnlyCollection<string> Details { get; private set; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GuideStep
        /// desc         : 문자열 단계 번호와 여러 상세 문단을 복사하여 보관한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 단일 상세 문자열 또는 여러 문단으로 구성 가능.
        /// </summary>
        /// ---------------------------------------------------------------------
        public GuideStep(string number, string icon, string title, string summary, params string[] details)
        {
            if (String.IsNullOrWhiteSpace(title)) throw new ArgumentException("안내 제목이 필요합니다.");
            Number = number ?? ""; Icon = icon ?? ""; Title = title; Summary = summary ?? "";
            Details = Array.AsReadOnly((details ?? new string[0]).Where(x => !String.IsNullOrWhiteSpace(x)).ToArray());
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantDialog
    /// desc         : 설정과 사용 안내에 동일한 테마·화면 중앙 배치·스크롤·닫기 버튼을 제공한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : VS 소유 창으로 열어 모달 입력과 키보드 닫기를 지원.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    /// </summary>
    /// ---------------------------------------------------------------------
    public class AssistantDialog : Window
    {
        public WrapPanel Actions { get; private set; }
        public TextBlock Message { get; private set; }
        public ContentControl Body { get; private set; }
        public WrapPanel Navigation { get; private set; }
        public Border Footer { get; private set; }
        public ContentControl FooterContent { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantDialog
        /// desc         : 제목·본문·고정 하단 동작을 작업 영역 크기에 맞춰 배치한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : CenterScreen과 VS 테마 리소스를 공통 적용.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
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
            var layout = new Grid();
            layout.SetResourceReference(Panel.BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition());
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new StackPanel { Margin = new Thickness(20, 20, 20, 16) };
            header.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            header.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
            layout.Children.Add(header);
            Navigation = new WrapPanel { Margin = new Thickness(20, 0, 20, 14), Visibility = Visibility.Collapsed };
            AutomationProperties.SetName(Navigation, "화면 목차"); Grid.SetRow(Navigation, 1); layout.Children.Add(Navigation);
            Body = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Margin = new Thickness(20, 0, 20, 0) };
            Grid.SetRow(Body, 2); layout.Children.Add(Body);
            var footer = new StackPanel();
            FooterContent = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch }; footer.Children.Add(FooterContent);
            Message = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            footer.Children.Add(Message);
            Actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            var close = new Button { Content = "× 닫기", IsCancel = true, MinWidth = 84, Margin = new Thickness(8, 0, 0, 0) };
            close.Click += (sender, args) => Close();
            Actions.Children.Add(close); footer.Children.Add(Actions);
            Footer = new Border { Child = footer, Background = Gray(32), BorderBrush = Gray(63), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 14, 20, 16), Margin = new Thickness(0, 16, 0, 0) };
            Grid.SetRow(Footer, 3); layout.Children.Add(Footer); Content = layout;
            Loaded += (sender, args) => { if (SystemParameters.ClientAreaAnimation) BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))); };
            Closed += (sender, args) => BeginAnimation(OpacityProperty, null);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SetSections
        /// desc         : 고정 목차에서 입력 값을 바꾸지 않고 실제 본문 구역으로 스크롤한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 키보드 버튼 조작과 좁은 폭 줄바꿈 지원.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void SetSections(ScrollViewer scroll, params DialogSection[] sections)
        {
            if (scroll == null) throw new ArgumentNullException(nameof(scroll));
            Navigation.Children.Clear();
            foreach (var section in sections ?? new DialogSection[0])
            {
                if (section == null) continue;
                var item = section;
                var button = new Button { Content = item.Icon + " " + item.Title, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 6, 10, 6), ToolTip = item.Title + " 구역으로 이동" };
                AutomationProperties.SetName(button, item.Title + " 구역으로 이동");
                button.Click += (sender, args) => {
                    if (!item.Target.IsDescendantOf(scroll)) return;
                    scroll.UpdateLayout(); var content = scroll.Content as UIElement;
                    if (content != null) scroll.ScrollToVerticalOffset(item.Target.TranslatePoint(new Point(0, 0), content).Y);
                    else item.Target.BringIntoView();
                };
                Navigation.Children.Add(button);
            }
            Navigation.Visibility = Navigation.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateSection
        /// desc         : 아이콘·소제목·입력 내용 사이에 일정한 간격을 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 검정 계열 구역에 내부 16·구역 간 20 픽셀 적용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static Border CreateSection(string icon, string title, UIElement content)
        {
            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = (icon ?? "") + "  " + title, Foreground = Brushes.Gainsboro, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12), TextWrapping = TextWrapping.Wrap });
            if (content != null) body.Children.Add(content);
            return new Border { Background = Gray(35), BorderBrush = Gray(65), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 20), Child = body };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SetGuide
        /// desc         : 안내 카드와 그림이 있는 상세 화면을 같은 창에서 전환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 외부 실행 없이 카드 hover·키보드 선택·목록 복귀 제공.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void SetGuide(params GuideStep[] steps)
        {
            Navigation.Children.Clear(); Navigation.Visibility = Visibility.Collapsed;
            Body.Content = new GuideGallery((steps ?? new GuideStep[0]).Where(x => x != null).ToArray());
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Gray
        /// desc         : 구역·푸터·절차 그림에 사용할 읽기 전용 회색 브러시를 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 같은 색 계열의 명도로 영역 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static SolidColorBrush Gray(byte level)
        { var brush = new SolidColorBrush(Color.FromRgb(level, level, level)); brush.Freeze(); return brush; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GuideGallery
        /// desc         : 고정 클릭 영역 안의 카드만 강조하고 상세 화면에서 목록으로 돌아간다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : hover 진동 방지·키보드 초점 복귀와 실제 Image 제공.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class GuideGallery : ContentControl
        {
            private readonly List<Button> cards = new List<Button>();
            private readonly ScrollViewer overview;

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : GuideGallery
            /// desc         : 안정된 카드 hit 영역에 마우스와 키보드 선택 동작을 연결한다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 버튼 내부 표면만 이동하여 반복 진입 이벤트 방지.
            /// </summary>
            /// ---------------------------------------------------------------------
            public GuideGallery(GuideStep[] steps)
            {
                HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
                var list = new StackPanel { Margin = new Thickness(3, 4, 3, 4) };
                overview = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                foreach (var step in steps)
                {
                    var words = new StackPanel();
                    words.Children.Add(new TextBlock { Text = step.Number + "  " + step.Icon + "  " + step.Title, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
                    words.Children.Add(new TextBlock { Text = step.Summary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
                    words.Children.Add(new TextBlock { Text = "자세히 보기  →", FontSize = 12, Foreground = Brushes.Silver, Margin = new Thickness(0, 10, 0, 0) });
                    var surface = new Border { Background = Gray(37), BorderBrush = Gray(80), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Child = words, IsHitTestVisible = false, RenderTransform = new TranslateTransform() };
                    var button = new Button { Content = surface, DataContext = step, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Brushes.Gainsboro, Margin = new Thickness(0, 0, 0, 16), ToolTip = step.Title + " 상세 설명" };
                    var hit = new FrameworkElementFactory(typeof(Border)); hit.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                    hit.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
                    button.Template = new ControlTemplate(typeof(Button)) { VisualTree = hit };
                    AutomationProperties.SetName(button, step.Title + " 상세 설명");
                    button.MouseEnter += (sender, args) => Highlight(button);
                    button.MouseLeave += (sender, args) => Highlight(button.IsKeyboardFocusWithin ? button : null);
                    button.GotKeyboardFocus += (sender, args) => Highlight(button);
                    button.LostKeyboardFocus += (sender, args) => Highlight(button.IsMouseOver ? button : null);
                    button.Click += (sender, args) => { Dispatcher.VerifyAccess(); ShowDetail(step, button); };
                    cards.Add(button); list.Children.Add(button);
                }
                Content = overview; Unloaded += (sender, args) => StopAnimations();
            }

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : Highlight
            /// desc         : 선택 카드의 깊이와 테두리를 강조하고 나머지는 부드럽게 어둡게 한다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 버튼 좌표는 고정하고 내부 표면만 두 픽셀 이동.
            /// </summary>
            /// ---------------------------------------------------------------------
            private void Highlight(Button selected)
            {
                foreach (var card in cards)
                {
                    bool active = card == selected; var surface = (Border)card.Content;
                    Panel.SetZIndex(card, active ? 1 : 0); surface.BorderBrush = Gray(active ? (byte)210 : (byte)80);
                    double opacity = selected == null || active ? 1 : .45, y = active ? -2 : 0;
                    var transform = (TranslateTransform)surface.RenderTransform;
                    if (SystemParameters.ClientAreaAnimation)
                    {
                        surface.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(150)), HandoffBehavior.SnapshotAndReplace);
                        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, TimeSpan.FromMilliseconds(150)), HandoffBehavior.SnapshotAndReplace);
                    }
                    else { surface.Opacity = opacity; transform.Y = y; }
                }
            }

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : ShowDetail
            /// desc         : 절차 그림·상세 문단을 표시하고 복귀하면 원래 카드에 초점을 돌린다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 중첩 모달이나 외부 실행 없이 같은 본문에서 전환.
            /// </summary>
            /// ---------------------------------------------------------------------
            private void ShowDetail(GuideStep step, Button source)
            {
                Dispatcher.VerifyAccess();
                Highlight(null); var detail = new StackPanel();
                var back = new Button { Content = "← 안내 목록으로", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16) };
                back.Click += (sender, args) => { Dispatcher.VerifyAccess(); Content = overview; FocusAfterLayout(source); };
                detail.Children.Add(back);
                detail.Children.Add(new TextBlock { Text = step.Number + "  " + step.Icon + "  " + step.Title, FontSize = 21, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
                var image = new Image { Source = Illustration(step), Stretch = Stretch.Uniform, MaxHeight = 210, Margin = new Thickness(0, 0, 0, 16) };
                AutomationProperties.SetName(image, step.Title + " 절차 그림"); detail.Children.Add(image);
                detail.Children.Add(new TextBlock { Text = step.Summary, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
                IEnumerable<string> paragraphs = step.Details.Count == 0 ? (IEnumerable<string>)new[] { step.Summary } : step.Details;
                foreach (string paragraph in paragraphs) detail.Children.Add(new TextBlock { Text = paragraph, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) });
                Content = new ScrollViewer { Content = detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                FocusAfterLayout(back);
            }

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : FocusAfterLayout
            /// desc         : 본문 교체의 로드·레이아웃 처리가 끝난 UI 순서에서 연결된 요소에 초점을 준다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 지연된 IsLoaded 상태로 분리된 카드에 초점을 주는 회귀 수정.
            /// </summary>
            /// ---------------------------------------------------------------------
            private static void FocusAfterLayout(FrameworkElement element)
            {
                element.Dispatcher.VerifyAccess();
                // 이미 UI 스레드이며 Content 교체 뒤 Loaded보다 낮은 Input 순서에서 초점을 복원한다.
                // 스레드 전환이나 동기 대기가 아닌 유한 UI 순서 예약이므로 이 호출에만 경고를 억제한다.
#pragma warning disable VSTHRD001
                element.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => {
                    if (!element.IsLoaded || !element.IsVisible || PresentationSource.FromVisual(element) == null) return;
                    element.BringIntoView(); element.Focus();
                }));
#pragma warning restore VSTHRD001
            }

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : Illustration
            /// desc         : 설치·연결·요청·검토·저장 절차를 해상도 독립 그림으로 설명한다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 외부 파일 없이 실제 ImageSource에 작업 흐름 표시.
            /// </summary>
            /// ---------------------------------------------------------------------
            private static ImageSource Illustration(GuideStep step)
            {
                string[] labels; string title = step.Title;
                if (title.Contains("설치")) labels = new[] { "공식 설치 안내", "Windows CLI", "실행 파일 준비" };
                else if (title.Contains("파일 선택") || title.Contains("경로")) labels = new[] { "설정 · 찾기", "codex.exe / claude.exe", "실행 경로 확인" };
                else if (title.Contains("로그인") || title.Contains("계정 연결") || title.Contains("AI 연결")) labels = new[] { "계정 연결", "CLI · 브라우저 로그인", "연결 확인" };
                else if (title.Contains("확인")) labels = new[] { "연결 확인", "연결됨 확인", "설정 저장" };
                else if (title.Contains("검토") || title.Contains("비교") || title.Contains("승인")) labels = new[] { "원본 ↔ 수정안", "변경 내용 확인", "승인 후 적용" };
                else if (title.Contains("저장")) labels = new[] { "편집기에 적용", "적용 내용 저장", "파일 저장 완료" };
                else labels = new[] { "현재 파일 선택", "요청 · 첨부", "보내기" };
                var drawing = new DrawingGroup();
                using (var dc = drawing.Open())
                {
                    dc.DrawRoundedRectangle(Gray(29), new Pen(Gray(76), 1), new Rect(0, 0, 480, 182), 10, 10);
                    DrawText(dc, step.Icon + "  " + title, 20, 16, 18, Brushes.Gainsboro);
                    for (int i = 0; i < 3; i++)
                    {
                        double x = 18 + i * 155;
                        dc.DrawRoundedRectangle(Gray(i == 2 ? (byte)65 : (byte)43), new Pen(Gray(105), 1), new Rect(x, 63, 133, 94), 7, 7);
                        DrawText(dc, (i + 1).ToString(), x + 55, 72, 25, Brushes.White);
                        DrawText(dc, labels[i], x + 7, 118, 12, Brushes.Gainsboro, 120);
                        if (i < 2) DrawText(dc, "→", x + 136, 95, 18, Brushes.Silver);
                    }
                }
                drawing.Freeze(); var image = new DrawingImage(drawing); image.Freeze(); return image;
            }

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : DrawText
            /// desc         : 절차 그림에 한글 안내와 번호를 지정한 폭으로 그린다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 명도 대비와 일정한 기준선 유지.
            /// </summary>
            /// ---------------------------------------------------------------------
            private static void DrawText(DrawingContext context, string text, double x, double y, double size, Brush brush, double width = 430)
            {
                var formatted = new FormattedText(text, System.Globalization.CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight, new Typeface("Malgun Gothic"), size, brush, 1.0) { MaxTextWidth = width, TextAlignment = width == 120 ? TextAlignment.Center : TextAlignment.Left };
                context.DrawText(formatted, new Point(x, y));
            }

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : StopAnimations
            /// desc         : 안내가 닫히면 표면에 남은 유한 애니메이션 시계를 해제한다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 종료 뒤 타이머나 UI 접근을 남기지 않음.
            /// </summary>
            /// ---------------------------------------------------------------------
            private void StopAnimations()
            {
                foreach (var card in cards) { var surface = (Border)card.Content; surface.BeginAnimation(OpacityProperty, null); ((TranslateTransform)surface.RenderTransform).BeginAnimation(TranslateTransform.YProperty, null); }
            }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GettingStartedDialog
    /// desc         : 처음 사용하는 사람이 네 단계 그림형 카드로 요청·검토·저장을 익히도록 안내한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 다시 보지 않기와 계정 설정 바로 가기 제공.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        public GettingStartedDialog(AssistantControl source, bool hidden)
            : base(source, "? 사용 안내", "파일을 열고, 요청하고, 비교한 뒤 저장하세요. 항목을 선택하면 그림과 함께 자세히 볼 수 있습니다.")
        {
            SetGuide(
                new GuideStep("1", "⚙", "AI 연결", "Codex 또는 Claude 탭을 고르고 설정에서 계정을 연결하세요.", "‘계정 연결’에서 열린 공식 CLI 또는 브라우저로 로그인한 뒤 ‘연결 확인’을 누르세요.", "CLI 경로를 지정하거나 PATH의 실행 파일을 사용할 수 있습니다. 암호와 토큰은 이 창에 입력하지 않습니다."),
                new GuideStep("2", "✎", "현재 파일에 요청", "소스 파일을 열고 요청을 작성하세요. 파일과 이미지를 첨부할 수 있습니다.", "Enter 또는 ‘보내기’로 요청합니다. Ctrl+Enter는 줄바꿈입니다.", "이미지·문서를 끌어오거나 붙여넣을 수 있습니다. 첨부 항목 위에 마우스를 올리면 삭제할 수 있습니다."),
                new GuideStep("3", "✓", "변경 비교와 승인", "원본과 변경 내용을 확인한 뒤 동의하면 승인하세요.", "변경 카드의 파일 이름을 누르면 실제 소스 위치로 이동합니다. 비교 내용을 펼쳐 원본과 수정안을 검토하세요.", "동의 체크 후 ‘승인 후 적용’을 누르면 편집기에 반영됩니다. 원본이 달라졌다면 다시 요청해야 합니다."),
                new GuideStep("4", "▣", "직접 저장", "‘적용 내용 저장’을 눌러 편집기 변경을 파일에 저장하세요.", "승인 후 적용과 파일 저장은 별도 동작입니다. 내용을 확인한 뒤 ‘적용 내용 저장’을 누르세요.", "필요하면 ‘직전 적용 되돌리기’로 편집기 내용을 복구할 수 있습니다."));
            HideNextTime = new CheckBox { Content = "다시 보지 않기", IsChecked = hidden, Margin = new Thickness(0, 0, 0, 8) };
            var preference = new StackPanel(); preference.Children.Add(HideNextTime);
            preference.Children.Add(new TextBlock { Text = "언제든 상단 ‘사용 안내’ 또는 우클릭 메뉴에서 다시 열 수 있습니다.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Silver, FontSize = 12, Margin = new Thickness(0, 0, 0, 10) });
            FooterContent.Content = preference;
            ConnectButton = new Button { Content = "⚙ 계정 설정 열기", Margin = new Thickness(0, 0, 8, 0) };
            StartButton = new Button { Content = "✓ 시작하기", IsDefault = true, Style = (Style)FindResource("PrimaryButton") };
            ConnectButton.Click += (sender, args) => { OpenSettings = true; Close(); };
            StartButton.Click += (sender, args) => Close();
            Actions.Children.Clear(); Actions.Children.Add(ConnectButton); Actions.Children.Add(StartButton);
        }

    }
}
