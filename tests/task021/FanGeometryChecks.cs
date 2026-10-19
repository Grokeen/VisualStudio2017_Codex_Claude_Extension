using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CodexCode.Task021
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FanGeometryResult
    /// desc         : 부채꼴 표시·클릭·상태 검사의 실제 통과 목록과 첫 실패를 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-021 소스 fixture와 고정 제품 DLL을 같은 검사로 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class FanGeometryResult
    {
        public bool Succeeded;
        public List<string> Checks = new List<string>();
        public List<string> GeometrySnapshots = new List<string>();
        public string Error;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FanGeometryChecks
    /// desc         : 실제 WPF hit 검사로 사각 모서리와 원점이 버튼에서 제외되는지 독립 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-021 자동 축소·재진입·지연 닫기·종료까지 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class FanGeometryChecks
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Call
        /// desc         : 같은 실제 메뉴 API를 반사 호출하고 원래 제품 예외를 그대로 보고한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 공개 제품 API를 검사 목적으로 확장하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static object Call(UserControl view, string name, params object[] args)
        { return view.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, args); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Field
        /// desc         : 열림 횟수와 해제된 타이머 등 실제 내부 상태를 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 표시 모양과 상태 결과를 함께 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static object Field(UserControl view, string name)
        { return view.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : 첫 실패 이전에 실제 통과한 독립 요구만 결과에 보관한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 조건 불충족을 정상 종료로 숨기지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Check(FanGeometryResult result, bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); result.Checks.Add(message); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Pump
        /// desc         : 짧은 실제 Dispatcher 진행으로 애니메이션과 175밀리초 타이머를 처리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 고정 대기 동안 WPF 입력 큐를 계속 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Pump(int milliseconds)
        {
            var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (DateTime.UtcNow < until)
            { Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { })); Thread.Sleep(5); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HitButton
        /// desc         : InputHitTest가 실제로 반환한 시각 요소의 소유 버튼을 찾는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 FillContains만으로 클릭 검사를 대신하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Button HitButton(UIElement surface, Point point)
        {
            var item = surface.InputHitTest(point) as DependencyObject;
            while (item != null && !(item is Button)) item = VisualTreeHelper.GetParent(item);
            return item as Button;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FindVisuals
        /// desc         : 버튼 템플릿의 실제 Path와 내용 표시자를 전수 탐색한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 사각 Border 위에 그림만 덧붙인 구현을 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<DependencyObject> FindVisuals(DependencyObject parent)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index); yield return child;
                foreach (var item in FindVisuals(child)) yield return item;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Capture
        /// desc         : 화면 포커스를 빼앗지 않은 실제 WPF 시각 트리를 PNG로 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 일반·200픽셀·확대 글꼴 모양을 직접 열람 가능.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Capture(UserControl view, string path)
        {
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) png.Save(file);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RecordGeometry
        /// desc         : 실제 제품의 트리거·부채꼴 좌표계를 화면 너비와 함께 기록한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 후보 DLL과 단순 fixture 배치 차이를 판별.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RecordGeometry(FanGeometryResult result, UserControl view, Canvas panel, ToggleButton trigger)
        {
            var triggerBounds = trigger.TransformToAncestor(view).TransformBounds(new Rect(0, 0, trigger.ActualWidth, trigger.ActualHeight));
            var panelBounds = panel.TransformToAncestor(view).TransformBounds(new Rect(0, 0, panel.ActualWidth, panel.ActualHeight));
            result.GeometrySnapshots.Add("Client=" + view.ActualWidth + "x" + view.ActualHeight + "; trigger=" + triggerBounds + "; panel=" + panelBounds + "; layout=" + panel.LayoutTransform.Value);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Run
        /// desc         : 원본 소스 또는 고정 DLL의 실제 메뉴 버튼을 격리 창에서 검사하고 정리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 외부 명령·AI·계정 연결 없이 실제 기하 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static FanGeometryResult Run(UserControl view, string output)
        {
            var result = new FanGeometryResult(); Window owner = null;
            try
            {
                view.Width = 420; view.Height = 420;
                owner = new Window { Content = view, Width = 420, Height = 420, WindowStyle = WindowStyle.None,
                    ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
                owner.Show(); owner.UpdateLayout(); Call(view, "InitializeFanMenu"); Call(view, "InitializeFanMenu");
                var panel = (Canvas)view.FindName("FanPanel"); var settings = (Button)view.FindName("SettingsButton");
                var help = (Button)view.FindName("HelpButton"); var trigger = (ToggleButton)view.FindName("OptionsToggle");
                int before = (int)Field(view, "fanOpenCount"); Call(view, "OpenFanMenu"); owner.UpdateLayout(); Pump(280);
                RecordGeometry(result, view, panel, trigger);
                Check(result, (bool)Field(view, "fanMenuOpen") && panel.IsVisible && trigger.IsChecked == true, "An explicit open displays the real menu and synchronizes its trigger");
                Check(result, panel.Children.Count == 2 && panel.Children.OfType<Button>().Count() == 2, "Only the two sector Buttons form the fan; there is no decorative background hiding rectangular buttons");
                foreach (var button in new[] { settings, help })
                {
                    var paths = FindVisuals(button).OfType<System.Windows.Shapes.Path>().ToArray();
                    Check(result, button.Clip is CombinedGeometry && paths.Length == 1 && !FindVisuals(button).OfType<Border>().Any(), button.Name + " uses one actual Path template and a nonrectangular clip");
                    Check(result, button.RenderTransform.Value.IsIdentity, button.Name + " keeps its actual hit region stationary during opening");
                }
                var helpCenter = new Point(143, 100); var settingsCenter = new Point(206, 141);
                Check(result, HitButton(panel, helpCenter) == help && HitButton(panel, settingsCenter) == settings, "Each sector center hits its own native Button through the actual WPF input route");
                var empty = new[] { new Point(1, 1), new Point(259, 1), new Point(1, 199), new Point(259, 199), new Point(240, 20), new Point(240, 60), new Point(176.41, 121.77), new Point(130, 24) };
                Check(result, empty.All(p => HitButton(panel, p) == null), "Bounding corners, the inner hole, the angular gap and the outside arc are not clickable");
                int samples = 0;
                for (double x = 0.25; x < 260; x += 9)
                    for (double y = 0.25; y < 200; y += 9)
                    {
                        var point = new Point(x, y); bool inHelp = help.Clip.FillContains(point), inSettings = settings.Clip.FillContains(point);
                        if (inHelp && inSettings) throw new InvalidOperationException("Sector clips overlap");
                        var expected = inHelp ? help : inSettings ? settings : null;
                        if (HitButton(panel, point) != expected) throw new InvalidOperationException("Actual hit target differs from sector clip at " + point);
                        samples++;
                    }
                Check(result, samples > 600, "More than 600 actual input samples match the two disjoint sector clips");
                for (int index = 0; index < 100; index++) Call(view, "OpenFanMenu");
                Check(result, (int)Field(view, "fanOpenCount") == before + 1, "One hundred hover re-entries do not restart the opening animation");
                Capture(view, System.IO.Path.Combine(output, "fan-420.png"));
                foreach (double width in new[] { 200.0, 260.0 })
                {
                    view.Width = width; owner.Width = width; owner.UpdateLayout(); Pump(40);
                    RecordGeometry(result, view, panel, trigger);
                    var bounds = panel.TransformToAncestor(view).TransformBounds(new Rect(0, 0, panel.ActualWidth, panel.ActualHeight));
                    Check(result, bounds.Left >= -0.5 && bounds.Right <= view.ActualWidth + 0.5, "The whole fan stays inside a " + width + " pixel client width");
                    var triggerBounds = trigger.TransformToAncestor(view).TransformBounds(new Rect(0, 0, trigger.ActualWidth, trigger.ActualHeight));
                    for (double x = triggerBounds.Left + 1; x < triggerBounds.Right; x += 4)
                        for (double y = triggerBounds.Top + 1; y < triggerBounds.Bottom; y += 4)
                        {
                            var localTrigger = view.TranslatePoint(new Point(x, y), trigger);
                            var actualTriggerHit = trigger.InputHitTest(localTrigger);
                            if (actualTriggerHit == null) continue;
                            var point = view.TranslatePoint(new Point(x, y), panel); var hit = HitButton(panel, point);
                            if (hit != null) {
                                result.GeometrySnapshots.Add("Overlap client=" + x + "," + y + "; fan=" + point + "; button=" + hit.Name + "; triggerLocal=" + localTrigger + "; triggerHit=" + (actualTriggerHit == null ? "none" : actualTriggerHit.GetType().FullName));
                                Capture(view, System.IO.Path.Combine(output, "fan-overlap-" + width + ".png"));
                                throw new InvalidOperationException("A fan sector overlaps the trigger after scaling");
                            }
                        }
                    Check(result, HitButton(panel, helpCenter) == help && HitButton(panel, settingsCenter) == settings, "Scaled sectors remain reachable and do not intercept the trigger at width " + width);
                    Capture(view, System.IO.Path.Combine(output, "fan-" + width + ".png"));
                }
                view.FontSize = 28; owner.UpdateLayout(); Pump(50); Capture(view, System.IO.Path.Combine(output, "fan-font28.png"));
                Check(result, FindVisuals(help).OfType<TextBlock>().Any(t => t.Text == "안내" && t.FontSize == 28) && FindVisuals(settings).OfType<TextBlock>().Any(t => t.Text == "설정" && t.FontSize == 28), "Sector labels inherit the selected large text size rather than freezing to a tiny font");
                view.FontSize = 14; Call(view, "ScheduleFanClose");
                Check(result, ((DispatcherTimer)Field(view, "fanCloseTimer")).IsEnabled && ((DispatcherTimer)Field(view, "fanCloseTimer")).Interval == TimeSpan.FromMilliseconds(280), "Pointer exit schedules the Role 01:40 280 millisecond close grace");
                Pump(90); Check(result, (bool)Field(view, "fanMenuOpen"), "The crossing grace period does not close prematurely");
                Call(view, "FanRegionEntered", panel, null);
                Check(result, !((DispatcherTimer)Field(view, "fanCloseTimer")).IsEnabled, "Entering either shared menu region cancels pending close");
                Call(view, "ScheduleFanClose"); Pump(340);
                Check(result, !(bool)Field(view, "fanMenuOpen") && trigger.IsChecked == false, "A pointer and focus outside both regions closes the menu after the grace period");
                Call(view, "ScheduleFanOpen");
                Check(result, ((DispatcherTimer)Field(view, "fanOpenTimer")).IsEnabled && ((DispatcherTimer)Field(view, "fanOpenTimer")).Interval == TimeSpan.FromMilliseconds(220) && !(bool)Field(view, "fanMenuOpen"), "Hover intent waits 220ms without immediately covering the content");
                Call(view, "FanRegionLeft", trigger, null);
                Check(result, !((DispatcherTimer)Field(view, "fanOpenTimer")).IsEnabled, "Crossing past the trigger cancels unexecuted opening");
                Call(view, "ScheduleFanOpen"); Call(view, "CloseFanMenu");
                Check(result, !((DispatcherTimer)Field(view, "fanOpenTimer")).IsEnabled, "Dismissal cancels a pending hover before its tick");
                Call(view, "ScheduleFanOpen"); Call(view, "OpenFanMenu");
                Check(result, (bool)Field(view, "fanMenuOpen") && !((DispatcherTimer)Field(view, "fanOpenTimer")).IsEnabled, "Explicit click or keyboard open bypasses dwell and consumes its reservation");
                Call(view, "OpenFanMenu"); Call(view, "FanOwnerDeactivated", owner, EventArgs.Empty);
                Check(result, !(bool)Field(view, "fanMenuOpen"), "Owner deactivation closes the fan immediately");
                Call(view, "OpenFanMenu"); Call(view, "ScheduleFanClose"); int last = (int)Field(view, "fanOpenCount");
                Call(view, "DisposeFanMenu"); Call(view, "DisposeFanMenu"); Call(view, "OpenFanMenu"); Pump(230);
                Check(result, Field(view, "fanCloseTimer") == null && Field(view, "fanOpenTimer") == null && !(bool)Field(view, "fanMenuOpen") && (int)Field(view, "fanOpenCount") == last, "Disposal releases both timers and blocks delayed reopening");
                result.Succeeded = true;
            }
            catch (Exception error) { result.Error = error.ToString(); }
            finally
            {
                var disposable = view as IDisposable; if (disposable != null) disposable.Dispose();
                else Call(view, "DisposeFanMenu");
                if (owner != null) owner.Close();
            }
            return result;
        }
    }
}
