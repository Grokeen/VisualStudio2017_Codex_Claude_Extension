using System;
using Task = System.Threading.Tasks.Task;
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
using Microsoft.VisualStudio.Threading;

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
        /// update date  : 2026-10-18 CodexCode : WPF UI 검사와 VS COM 소유 창 조회의 스레드 경계를 분리.
        ///                2026-10-18 CodexCode : TASK-022 닫기 명령의 실제 내용 영역을 양축 중앙 정렬.
        /// </summary>
        /// ---------------------------------------------------------------------
        public AssistantDialog(AssistantControl source, string title, string description)
        {
            if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("안내 창은 해당 WPF UI 스레드에서만 만들 수 있습니다.");
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
            else if (ThreadHelper.CheckAccess())
            {
                // 실제 VS COM 조회에만 메인 스레드 검증을 적용한다.
#pragma warning disable VSTHRD108 // WPF 단독 창도 지원하므로 VS 주 스레드인 분기에서만 COM 소유자를 조회한다.
                ThreadHelper.ThrowIfNotOnUIThread();
#pragma warning restore VSTHRD108
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
            var close = new Button { Content = "× 닫기", HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, IsCancel = true, MinWidth = 84, Margin = new Thickness(8, 0, 0, 0) };
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
        ///                2026-10-18 CodexCode : TASK-022 목차 이동 버튼의 글자 중앙 정렬 명시.
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
                var button = new Button { Content = item.Icon + " " + item.Title, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 6, 10, 6), ToolTip = item.Title + " 구역으로 이동" };
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
            ///                2026-10-18 CodexCode : TASK-022 긴 설명 카드의 읽기 정렬은 유지하고 표면은 내용 영역에 맞춤.
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
                    var button = new Button { Content = surface, DataContext = step, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Brushes.Gainsboro, Margin = new Thickness(0, 0, 0, 16), ToolTip = step.Title + " 상세 설명" };
                    var hit = new FrameworkElementFactory(typeof(Border)); hit.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                    var cardContent = new FrameworkElementFactory(typeof(ContentPresenter));
                    cardContent.SetBinding(FrameworkElement.HorizontalAlignmentProperty, new System.Windows.Data.Binding("HorizontalContentAlignment") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                    cardContent.SetBinding(FrameworkElement.VerticalAlignmentProperty, new System.Windows.Data.Binding("VerticalContentAlignment") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                    hit.AppendChild(cardContent);
                    button.Template = new ControlTemplate(typeof(Button)) { VisualTree = hit };
                    AutomationProperties.SetName(button, step.Title + " 상세 설명");
                    button.MouseEnter += (sender, args) => Highlight(button);
                    button.MouseLeave += (sender, args) => Highlight(button.IsKeyboardFocusWithin ? button : null);
                    button.GotKeyboardFocus += (sender, args) => Highlight(button);
                    button.LostKeyboardFocus += (sender, args) => Highlight(button.IsMouseOver ? button : null);
                    button.Click += (sender, args) => { if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("안내는 WPF UI 스레드에서만 열 수 있습니다."); ShowDetail(step, button); };
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
            ///                2026-10-18 CodexCode : TASK-022 목록 복귀 버튼의 배치와 내부 글자 정렬을 분리.
            /// </summary>
            /// ---------------------------------------------------------------------
            private void ShowDetail(GuideStep step, Button source)
            {
                if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("상세 안내는 WPF UI 스레드에서만 열 수 있습니다.");
                Highlight(null); var detail = new StackPanel();
                var back = new Button { Content = "← 안내 목록으로", HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16) };
                back.Click += (sender, args) => { if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("안내 목록은 WPF UI 스레드에서만 복원할 수 있습니다."); Content = overview; FocusAfterLayout(source); };
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
            /// update date  : 2026-10-18 CodexCode : WPF 검사·Input 예약 순서를 유지하고 완료·취소·실패를 관찰.
            /// </summary>
            /// ---------------------------------------------------------------------
            private static void FocusAfterLayout(FrameworkElement element)
            {
                if (!element.Dispatcher.CheckAccess()) throw new InvalidOperationException("안내 초점은 해당 WPF UI 스레드에서만 예약할 수 있습니다.");
                // 이미 UI 스레드이며 Content 교체 뒤 Loaded보다 낮은 Input 순서에서 초점을 복원한다.
                // 스레드 전환이나 동기 대기가 아닌 유한 UI 순서 예약이므로 이 호출에만 경고를 억제한다.
#pragma warning disable VSTHRD001
                var operation = element.Dispatcher.InvokeAsync(new Action(() => {
                    if (!element.IsLoaded || !element.IsVisible || PresentationSource.FromVisual(element) == null) return;
                    element.BringIntoView(); element.Focus();
                }), DispatcherPriority.Input);
#pragma warning restore VSTHRD001
                ObserveFocusCompletionAsync(operation.Task).Forget();
            }

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : ObserveFocusCompletionAsync
            /// desc         : 유한 UI 예약의 취소·예외를 관찰하며 종료 뒤 UI에 다시 접근하지 않는다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : SDK17 분석기의 미관찰 작업 경고를 실제 완료 관찰로 해결.
            /// update date  : 2026-10-18 CodexCode : 같은 Dispatcher에서 만든 유한 예약만 관찰하는 await 경계를 명시.
            /// </summary>
            /// ---------------------------------------------------------------------
            private static async Task ObserveFocusCompletionAsync(Task completion)
            {
#pragma warning disable VSTHRD003 // 바로 위 같은 Dispatcher에서 생성한 유한 작업이며 Join/동기 대기 없이 완료만 관찰한다.
                try { await completion.ConfigureAwait(false); }
#pragma warning restore VSTHRD003
                catch (OperationCanceledException) { }
                catch (Exception error) { System.Diagnostics.Trace.TraceWarning("안내 초점 예약 실패: {0}", error.GetType().Name); }
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
        /// update date  : 2026-10-18 CodexCode : TASK-021 연결·요청·검토·저장과 직접 실행·실패 복구 설명 보강.
        /// update date  : 2026-10-18 CodexCode : 기반 생성자의 WPF 스레드 검사와 조건부 VS COM 조회 계약을 명시.
        ///                2026-10-18 CodexCode : TASK-022 연결·시작 명령 버튼의 내부 내용 중앙 정렬.
        /// </summary>
        /// ---------------------------------------------------------------------
        public GettingStartedDialog(AssistantControl source, bool hidden)
#pragma warning disable VSTHRD010 // base는 WPF Dispatcher를 검사하고 실제 VS 주 스레드인 경우에만 COM 소유자를 조회한다.
            : base(source, "? 사용 안내", "파일을 열고, 요청하고, 비교한 뒤 저장하세요. 항목을 선택하면 그림과 함께 자세히 볼 수 있습니다.")
#pragma warning restore VSTHRD010
        {
            SetGuide(
                new GuideStep("1", "⚙", "AI 연결", "사용할 AI와 실행 파일을 선택하고 로그인·연결 확인·설정 저장 순서로 준비하세요.",
                    "1. AI 선택: 상단 Codex 또는 Claude 탭을 고릅니다. 설정의 ‘현재 AI’와 실제 사용할 제공자가 같은지 확인하세요. 두 제공자의 실행 경로·로그인·모델 설정은 구분됩니다.",
                    "2. CLI 설치: 설정의 ‘CLI 설치·로그인 방법’에서 공식 Windows 설치 안내를 확인하세요. 확장 설치와 공식 CLI 설치는 별도입니다. 설치 뒤 IDE가 새 PATH를 모르면 다시 시작하세요.",
                    "3. 실행 파일: ‘찾기’로 codex.exe 또는 claude.exe를 지정하거나 PATH에 등록되어 있으면 경로를 비웁니다. .cmd·.bat 래퍼와 실행 명령 전체를 경로 칸에 넣지 않습니다.",
                    "4. 공식 로그인: ‘계정 연결’에서 열린 공식 CLI 또는 브라우저로 로그인한 뒤 확장으로 돌아오세요. 암호·API 키·토큰을 이 안내나 공통 규칙·프롬프트에 입력하지 않습니다.",
                    "5. 연결 확인: ‘연결 확인’을 누르고 실제 결과를 읽으세요. 로그인 창을 열었다는 사실과 인증 성공은 다릅니다. 성공하면 ‘설정 저장’을 누릅니다. 닫기·취소는 저장 전 입력을 복구합니다.",
                    "6. 계정 구분: Git 사용자 이름과 AI 담당자 이름은 AI 서비스 로그인 상태를 증명하지 않습니다. 여러 계정·조직을 쓰면 공식 CLI의 실제 계정과 이용 조건을 확인하세요.",
                    "7. 모델 준비: 처음에는 CLI 기본 모델·자동 생각 강도·일반 속도로 연결을 확인할 수 있습니다. 목록이나 직접 입력 모델의 최종 사용 가능 여부는 실제 서비스가 판정합니다.",
                    "8. 속도와 사용량: 생각 강도는 추론량이고 Fast는 응답 속도입니다. Fast는 모델·CLI·계정 지원이 필요하고 사용량이 늘 수 있습니다. 요금제·잔여 토큰·정확도 비율은 추정하지 않습니다.",
                    "9. 연결 실패: 파일 없음은 실행 경로·PATH, 로그인 요구는 공식 인증 상태, 접근 거부는 조직·보안 정책과 네트워크를 확인하세요. 실제 오류 원인을 고친 뒤 다시 연결하고 저장합니다."),
                new GuideStep("2", "✎", "현재 파일에 요청", "대상 파일·선택 범위·대화를 확인하고 필요한 설명과 첨부를 함께 보내세요.",
                    "1. 작업 대상: Visual Studio에서 질문할 소스를 먼저 엽니다. 요청 직전 현재 파일과 선택 영역을 확인하세요. 선택 영역과 파일 전체 중 어떤 범위를 대상으로 하는지 요청에 분명히 적습니다.",
                    "2. 요청 작성: 바꿀 동작·현재 문제·기대 결과와 유지할 조건을 적으세요. 예: ‘이 메서드의 빈 입력 오류를 수정하고 공개 API는 유지해 주세요. 검증한 방법도 설명해 주세요.’",
                    "3. 보내기: Enter 또는 ‘보내기’로 요청합니다. Ctrl+Enter는 줄바꿈입니다. 실행 중에는 현재 요청의 완료·중지 상태를 먼저 확인하세요. 여러 차례 대화와 실행 중 요청 대기열은 같은 기능이 아닙니다.",
                    "4. 첨부: 이미지·문서를 끌어오거나 붙여넣을 수 있습니다. 첨부 위에 마우스를 올리면 삭제할 수 있습니다. 잘못 선택한 파일은 전송 전에 제거하고, 문서 본문 읽기 성공 여부를 확인하세요.",
                    "5. 문서 지원: PDF·구형 Office는 PC에 등록된 Windows IFilter에 따라 본문 추출이 달라집니다. 스캔 이미지의 OCR은 제공하지 않습니다. 암호·손상·크기 제한 오류는 원인을 해결하거나 필요한 텍스트 사본으로 바꿉니다.",
                    "6. AI 대상: ‘AI 지정하여 대화’에서 실제 받을 대화를 고릅니다. 이름은 사용자명·AI·A~Z 슬롯을 구분하며 모델 ID와 다릅니다. 슬롯을 모두 쓰면 A의 기존 대화를 사용하므로 이전 문맥과 대상을 확인하세요.",
                    "7. 대화와 역할: 공통 규칙은 모든 프로젝트에서 공유하고, 대화 역할·팀은 역할·협업 설정에서 지정합니다. 기록은 누적되지만 모든 과거 메시지가 매 요청에 무제한 전달되는 것은 아닙니다.",
                    "8. 협업: 참여자를 지정하면 실제 검토 호출 후 주 담당자가 결과를 종합합니다. 같은 제공자·팀과 현재 지원 범위를 확인하세요. 추가 호출로 시간·사용량이 늘며 동료 답변이 자동으로 파일에 적용되지는 않습니다.",
                    "9. 사용량: 도구 설명의 토큰 값은 CLI가 보고한 요청 정보입니다. 보고되지 않은 항목은 확인할 수 없습니다. 계정의 월간 잔여량·최종 청구액과 혼동하지 말고 공식 서비스 화면도 확인하세요.",
                    "10. 요청 실패: 최근 알림과 현재 AI·모델·권한·대상 파일을 확인합니다. 인증·모델·한도·첨부 오류를 구분해 해결하고, 이미 실행된 작업이 있는지 살핀 뒤 다시 보내세요."),
                new GuideStep("3", "✓", "변경 비교와 승인", "수정안 검토와 CLI 직접 실행의 차이를 이해하고 실제 변경을 확인하세요.",
                    "1. 수정안 비교: 비교 모드의 답변은 변경 제안입니다. 파일의 추가·제거 줄 수와 요약을 읽고 원본·수정안·차이를 대조하세요. 요약만 읽고 승인한 것으로 간주하지 않습니다.",
                    "2. 소스 이동: 변경 카드의 파일 이름을 누르면 실제 소스 위치로 이동합니다. 파일 경로와 바꾸려던 위치가 맞는지 확인하고 비교 내용을 펼쳐 상세 변경을 읽으세요.",
                    "3. 검토 기준: 새 코드뿐 아니라 제거된 동작·오류 처리·호환성·데이터 영향도 확인합니다. 필요한 테스트가 실행되었는지, 설명에 적힌 테스트와 실제 결과가 같은지 구분하세요.",
                    "4. 승인 후 적용: 비교한 변경에 동의하면 체크한 뒤 ‘승인 후 적용’을 누릅니다. 비교 중 원본이 달라졌다면 오래된 제안을 그대로 덮지 말고 현재 내용으로 다시 요청하세요.",
                    "5. 전체 access: 직접 실행 모드에서는 연결된 CLI가 현재 OS 사용자 권한 안에서 파일 수정과 명령·빌드·테스트를 진행할 수 있습니다. 작업 폴더와 허용 범위를 먼저 확인하세요. 관리자 권한이나 조직 제한을 자동으로 우회하지 않습니다.",
                    "6. 권한의 차이: AI가 계획을 설명한 것, 수정안을 제안한 것, 실제 명령을 실행한 것은 서로 다릅니다. 선택한 실행 모드와 실행 기록을 읽고 실제로 일어난 작업을 확인하세요.",
                    "7. 외부 작업: DB·형상관리·배포는 대상 서버·저장소·명령·현재 권한을 확인합니다. SQL 작성과 SQL 실행, 커밋 생성과 원격 반영, 파일 저장과 배포 완료를 같은 결과로 해석하지 마세요.",
                    "8. 복구 범위: 비교 모드의 ‘직전 적용 되돌리기’는 CLI 직접 변경·외부 DB·원격 형상관리·배포 전체를 자동으로 되돌리는 기능이 아닙니다. 중요한 변경은 시작 전에 보관하고 실제 차이를 검토하세요.",
                    "9. 보류와 재요청: 의도가 다르거나 불완전하면 승인하지 않고 보완 질문을 보냅니다. 실행 중단·오류 뒤에는 파일과 외부 작업 상태를 먼저 확인하고 중복 실행을 피하세요."),
                new GuideStep("4", "▣", "직접 저장", "편집기 적용·디스크 저장·테스트·대화 기록을 각각 확인하고 실패 시 상태부터 점검하세요.",
                    "1. 파일 저장: 비교 모드의 ‘승인 후 적용’은 편집기를 바꾸는 단계입니다. 내용을 확인한 뒤 ‘적용 내용 저장’을 눌러 파일에 저장하세요. AI 답변 수신만으로 파일 저장이 끝난 것은 아닙니다.",
                    "2. 검증: 프로젝트에 맞는 빌드·테스트를 실행하고 실제 결과를 읽으세요. AI가 테스트 방법을 설명한 것과 실제 실행 성공은 다릅니다. 실패하면 오류와 관련 소스를 확인한 뒤 보완 요청을 보냅니다.",
                    "3. 비교 모드 복구: 필요하면 ‘직전 적용 되돌리기’로 해당 적용의 편집기 내용을 복구합니다. 이후 수동 수정이나 디스크 변경이 있으면 현재 상태를 확인하고 안내에 따라 처리하세요.",
                    "4. 직접 실행 복구: 전체 access에서 이미 실행한 명령은 중지 버튼만으로 취소·롤백되지 않을 수 있습니다. 변경 파일·Git 차이·생성 파일·외부 작업 결과를 확인한 뒤 필요한 복구를 결정하세요.",
                    "5. 대화 저장: 대화와 초안은 로컬 Markdown 기록으로 관리합니다. 대화를 닫으면 다음 시작의 자동 열기에서 제외되며, 불러오기에서 다시 선택할 수 있습니다. 파일 저장과 대화 저장은 별개입니다.",
                    "6. 설정과 규칙 저장: ‘설정 저장’은 계정 경로·모델·표시 설정과 공통 규칙을 유지합니다. 공통 규칙은 모든 프로젝트에서 공유되므로 표시된 Markdown 위치와 내용을 확인하세요.",
                    "7. 저장 실패: 파일 잠금·쓰기 권한·디스크 상태와 최근 알림을 확인합니다. 대화·제목·초안을 잃지 않도록 실제 저장 상태를 확인한 뒤 창을 닫거나 재시도하세요. 실패를 성공으로 가정하지 않습니다.",
                    "8. 오류 후 확인 순서: 현재 AI와 대화 대상 → 실행 상태 → 편집기 내용 → 디스크 파일 → 형상관리 차이 → 외부 작업 결과 순서로 살핍니다. 확인 전 같은 배포·커밋·DB 작업을 반복하지 마세요.",
                    "9. 안내 다시 보기: 우클릭의 ‘최근 알림’에서 사라진 결과를 읽고, ‘사용 안내’에서 절차를 다시 볼 수 있습니다. ‘다시 보지 않기’는 자동 안내만 조절하며 수동 안내는 계속 열 수 있습니다."));
            HideNextTime = new CheckBox { Content = "다시 보지 않기", IsChecked = hidden, Margin = new Thickness(0, 0, 0, 8) };
            var preference = new StackPanel(); preference.Children.Add(HideNextTime);
            preference.Children.Add(new TextBlock { Text = "언제든 상단 ‘사용 안내’ 또는 우클릭 메뉴에서 다시 열 수 있습니다.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Silver, FontSize = 12, Margin = new Thickness(0, 0, 0, 10) });
            FooterContent.Content = preference;
            ConnectButton = new Button { Content = "⚙ 계정 설정 열기", HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            StartButton = new Button { Content = "✓ 시작하기", HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, IsDefault = true, Style = (Style)FindResource("PrimaryButton") };
            ConnectButton.Click += (sender, args) => { OpenSettings = true; Close(); };
            StartButton.Click += (sender, args) => Close();
            Actions.Children.Clear(); Actions.Children.Add(ConnectButton); Actions.Children.Add(StartButton);
        }

    }
}
