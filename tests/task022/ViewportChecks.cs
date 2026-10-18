using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ViewportChecks
/// desc         : 고정 제품 DLL을 바깥 ScrollViewer에 호스팅하여 입력 고정·읽기 위치·dark 기록을 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 실제 문서·CLI·계정 조회 없이 합성 대화만 렌더링.
/// </summary>
/// ---------------------------------------------------------------------
public static class ViewportChecks
{
    public static readonly List<string> Checks = new List<string>(), Errors = new List<string>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 승인된 실제 UI 검사에서만 창을 만들고 폭·높이·스크롤·생각 중 위치를 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 외부 무한 측정과 padding·margin을 포함한 합성 호스트 재현.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run(string directory)
    {
        AssistantControl control = null;
        Window window = null;
        try
        {
            ViewPolicyChecks.RunMath(); Checks.AddRange(ViewPolicyChecks.Checks);
            Check(Environment.GetEnvironmentVariable("VS2017_AI_DISABLE_RUNTIME_DISCOVERY") == "1", "Runtime discovery is disabled before any control is created");
            new AssistantSettingsStore().Save(new AssistantSettings { HideWelcome = true, ShowHistory = false, HistoryWidth = 180 });
            control = new AssistantControl { Foreground = Brushes.Gainsboro, Background = new SolidColorBrush(Color.FromRgb(23, 23, 23)), Margin = new Thickness(6) };
            typeof(AssistantControl).GetField("scmLookupStarted", Private).SetValue(control, true);
            var chat = (ChatSession)typeof(AssistantControl).GetProperty("ActiveChat", Private).GetValue(control, null);
            chat.Messages.Clear(); chat.Add("assistant", "VIEWPORT_NORMAL_MESSAGE");
            for (int i = 0; i < 60; i++) chat.Add(i % 2 == 0 ? "user" : "assistant", "SYNTHETIC_MESSAGE_" + i + "\n" + string.Join("\n", Enumerable.Repeat("이전 대화를 읽는 동안 입력란은 화면에 남습니다. Synthetic content only.", 7)));
            var paddingHost = new Border { Padding = new Thickness(9), BorderThickness = new Thickness(2), Margin = new Thickness(4), Background = Brushes.White, Child = control };
            var outer = new ScrollViewer { Content = paddingHost, Padding = new Thickness(8), Background = Brushes.White, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            window = new Window { Content = outer, Width = 760, Height = 520, Background = Brushes.White, ShowActivated = false, ShowInTaskbar = false, Title = "Isolated conversation viewport review" };
            var lightList = new Style(typeof(ListBox)); lightList.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White)); window.Resources[typeof(ListBox)] = lightList;
            window.Show(); Pump(); Pump();
            var scroll = Named<ScrollViewer>(control, "ConversationScroll");
            var composer = Named<Border>(control, "ComposerSurface");
            Check(outer.ViewportHeight > 0 && outer.ScrollableHeight <= 2 && control.ActualHeight < outer.ViewportHeight && scroll.ScrollableHeight > 1000, "Infinite outer measurement converges inside the padded host while only the conversation has a long scroll extent");
            Check(Inside(composer, control) && Inside(Named<TextBox>(control, "Instruction"), control), "The composer and prompt stay visible with long conversation content");
            var messages = Named<ItemsControl>(control, "ConversationMessages");
            DependencyObject first = messages.ItemContainerGenerator.ContainerFromIndex(0);
            var body = Descendants<TextBox>(first).First(box => box.Name == "MessageBody");
            var bubble = Descendants<Border>(first).First(border => border.Name == "MessageBubble");
            var progress = Descendants<FrameworkElement>(first).First(item => item.Name == "ProgressMessage");
            Check(body.Text == "VIEWPORT_NORMAL_MESSAGE" && bubble.Visibility == Visibility.Visible && progress.Visibility == Visibility.Collapsed && progress.GetType().GetProperty("Notice").GetValue(progress, null) == null, "The progress converter leaves ordinary assistant content visible and does not expose an empty progress card");
            Call(control, "PresentExecutedChanges", new[] { new ChangeCardFile(Path.Combine(directory, "synthetic.cs"), 1, 1, "@@ original 1 @@\n-old\n+new", 1) }, "SYNTHETIC_VERIFIED_RESULT");
            Named<Expander>(control, "ReviewPanel").IsExpanded = true; Pump(); Pump();
            Check(outer.ScrollableHeight <= 2 && Inside(composer, control) && scroll.ScrollableHeight > scroll.ViewportHeight, "Expanded change review grows only the inner conversation extent and cannot push the composer outside");
            Snapshot(control, Path.Combine(directory, "viewport-padded-review.png"));
            paddingHost.Padding = new Thickness(0); paddingHost.BorderThickness = new Thickness(0); paddingHost.Margin = new Thickness(0); control.Margin = new Thickness(0); outer.Padding = new Thickness(0);
            foreach (int width in new[] { 245, 390, 760 })
            {
                window.Width = width; window.Height = 420; Pump(); Pump();
                Check(outer.ScrollableHeight <= 2 && Inside(composer, control) && Inside(composer, outer) && Inside(Named<Button>(control, "GenerateButton"), outer) && Inside(Named<Button>(control, "PermissionsButton"), outer), "The fixed composer and action groups remain in the short " + width + "px host");
                Snapshot(control, Path.Combine(directory, "viewport-" + width + ".png"));
            }
            window.Width = 390; window.Height = 440; control.FontSize = 28; Pump(); Pump();
            Check(outer.ScrollableHeight <= 2 && Inside(composer, control) && Inside(Named<Button>(control, "GenerateButton"), control), "Large conversation fonts preserve the fixed composer within a short host");
            control.FontSize = 14; window.Width = 760; window.Height = 520; Pump(); Pump();
            var history = Named<ListBox>(control, "ConversationList"); Named<ToggleButton>(control, "HistoryToggle").IsChecked = true; Call(control, "ApplyHistoryLayout"); Pump(); Pump(); history.ApplyTemplate();
            var historyScroll = (ScrollViewer)history.Template.FindName("PART_ScrollViewer", history); historyScroll.ApplyTemplate();
            var historyPresenter = (ScrollContentPresenter)historyScroll.Template.FindName("PART_ScrollContentPresenter", historyScroll);
            Check(history.Background.ToString() == "#FF1E1E1E" && historyScroll.Background.ToString() == "#FF1E1E1E" && ((Border)historyPresenter.Parent).Background.ToString() == "#FF1E1E1E", "History list, viewport and unused-area backdrop remain dark under an explicit light host ListBox style");
            Check(historyPresenter.ActualHeight > 100 && history.Items.Count > 0 && Inside(Named<Border>(control, "HistoryPanel"), control), "The dark history template fills its unused viewport and remains within the host");
            Snapshot(control, Path.Combine(directory, "viewport-dark-history.png"));
            Named<ToggleButton>(control, "HistoryToggle").IsChecked = false; Call(control, "ApplyHistoryLayout"); Pump();
            var footer = Named<TextBlock>(control, "ThinkingIndicator"); var above = Named<TextBlock>(control, "ThinkingAboveIndicator");
            var originalFooter = footer.Foreground; var originalAbove = above.Foreground;
            Call(control, "FollowConversation", true); Pump(); Call(control, "UpdateThinkingAnimation", true); Pump(); Pump();
            var brush = footer.Foreground as LinearGradientBrush;
            var transform = brush == null ? null : brush.RelativeTransform as TranslateTransform;
            Check(footer.Visibility == Visibility.Visible && above.Visibility == Visibility.Collapsed && IsUnder(footer, scroll) && !IsUnder(above, scroll), "Busy at the bottom shows the existing ThinkingIndicator only at the conversation footer");
            Check(brush != null && transform != null && ReferenceEquals(above.Foreground, brush) && transform.HasAnimatedProperties, "Footer and upper thinking labels share exactly one animated gradient brush");
            double footerHeight = footer.ActualHeight;
            scroll.ScrollToVerticalOffset(scroll.ScrollableHeight * .35); Pump(); Pump();
            Check(above.Visibility == Visibility.Visible && footer.Visibility == Visibility.Hidden && Math.Abs(footer.ActualHeight - footerHeight) < 1, "Reading earlier content shows upper thinking while hidden footer space is preserved");
            double readingOffset = scroll.VerticalOffset;
            for (int i = 0; i < 3; i++) { chat.Add("assistant", "NEW_SYNTHETIC_PROGRESS_" + i + "\n" + string.Join("\n", Enumerable.Repeat("New content must not take over the reading position.", 10))); Call(control, "FollowConversation", false); }
            Pump(); Pump();
            Check(Math.Abs(scroll.VerticalOffset - readingOffset) < 2 && above.Visibility == Visibility.Visible && footer.Visibility == Visibility.Hidden, "New progress content cannot steal a user's earlier reading position");
            Snapshot(control, Path.Combine(directory, "viewport-reading-above.png"));
            scroll.ScrollToEnd(); Pump(); Pump();
            Check(above.Visibility == Visibility.Collapsed && footer.Visibility == Visibility.Visible && AtEnd(scroll), "Manually returning to the bottom moves thinking back to the footer");
            chat.Add("assistant", "SYNTHETIC_FOLLOW_AFTER_MANUAL_BOTTOM\n" + new string('X', 300)); Call(control, "FollowConversation", false); Pump(); Pump();
            Check(AtEnd(scroll), "Automatic following resumes after the user returns to the bottom");
            scroll.ScrollToTop(); Pump(); Call(control, "FollowConversation", true); Pump(); Pump();
            Check(AtEnd(scroll) && above.Visibility == Visibility.Collapsed && footer.Visibility == Visibility.Visible, "An explicit send or conversation switch can force the latest position");
            double firstX = transform.X; Pump(); double secondX = transform.X;
            Check(secondX != firstX && transform.HasAnimatedProperties, "The shared thinking clock actually advances while the footer is displayed");
            Snapshot(control, Path.Combine(directory, "viewport-thinking-footer.png"));
            control.Dispose(); Pump();
            Check(!transform.HasAnimatedProperties && footer.Visibility == Visibility.Collapsed && above.Visibility == Visibility.Collapsed && ReferenceEquals(footer.Foreground, originalFooter) && ReferenceEquals(above.Foreground, originalAbove), "Disposal removes the shared clock and restores both foregrounds");
            Check(!(bool)typeof(AssistantControl).GetField("conversationFollowScheduled", Private).GetValue(control), "Disposal also removes the one-shot follow-layout subscription");
        }
        catch (Exception error) { Errors.Add(error.ToString()); }
        finally { if (control != null) control.Dispose(); if (window != null) window.Close(); Pump(); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Named / Call
    /// desc         : 기존 이름·private 통합 API를 사용하고 제품 공개 계약을 추가하지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 제품 인스턴스의 대화 표시 흐름만 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static T Named<T>(AssistantControl control, string name) where T : class { return (T)control.FindName(name); }
    private static void Call(AssistantControl control, string name, params object[] values) { typeof(AssistantControl).GetMethod(name, Private).Invoke(control, values); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Inside / AtEnd / IsUnder
    /// desc         : 실제 렌더 위치와 조상·스크롤 상태를 비교한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 단순 속성값이 아닌 사용자에게 표시되는 경계를 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool Inside(FrameworkElement item, FrameworkElement host) { var p = item.TranslatePoint(new Point(), host); return item.ActualHeight > 0 && item.ActualWidth > 0 && p.X >= -1 && p.Y >= -1 && p.X + item.ActualWidth <= host.ActualWidth + 1 && p.Y + item.ActualHeight <= host.ActualHeight + 1; }
    private static bool AtEnd(ScrollViewer viewer) { return viewer.ScrollableHeight - viewer.VerticalOffset <= 2; }
    private static bool IsUnder(DependencyObject child, DependencyObject parent) { for (var current = child; current != null; current = VisualTreeHelper.GetParent(current)) if (ReferenceEquals(current, parent)) return true; return false; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Descendants
    /// desc         : 실제 적용된 template의 시각 자식을 탐색한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 정상 메시지가 진행 카드에 가려지지 않는지 확인.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { DependencyObject child = VisualTreeHelper.GetChild(parent, i); if (child is T) yield return (T)child; foreach (T nested in Descendants<T>(child)) yield return nested; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pump
    /// desc         : 유한 시간만 레이아웃·입력·애니메이션을 처리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 CLI·설치·네트워크를 시작하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Pump() { var frame = new DispatcherFrame(); var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(160) }; timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Snapshot
    /// desc         : 컨트롤 자체의 정규화된 검증 PNG만 저장한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 사용자 데스크톱·다른 창·문서는 캡처하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Snapshot(FrameworkElement control, string path) { var bitmap = new RenderTargetBitmap((int)Math.Ceiling(control.ActualWidth), (int)Math.Ceiling(control.ActualHeight), 96, 96, PixelFormats.Pbgra32); var visual = new DrawingVisual(); using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(new VisualBrush(control), null, new Rect(0, 0, control.ActualWidth, control.ActualHeight)); bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var output = File.Create(path)) encoder.Save(output); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 실패 시 즉시 중단하고 실제 통과한 UI 검사만 계수한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : source-only 실행을 제품 성공으로 합산하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); Checks.Add(name); }
}
