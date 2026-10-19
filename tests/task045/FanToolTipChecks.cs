using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CodexOrig.Task045
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FanToolTipChecks
    /// desc         : 고정 제품의 실제 툴팁 창과 메뉴 상태를 대조하고 독립 WPF 입력 검사 창을 제공한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-045 이미 열린 툴팁·재진입·닫힘·접근성·수명 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class FanToolTipChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<string> checks = new List<string>();
        private static FrameworkElement view;
        private static string output;
        private static int helpClicks, settingsClicks, deactivations;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Call
        /// desc         : 고정 제품의 비공개 메뉴 동작을 호출하고 실제 예외를 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : TASK-045 테스트 전용 제품 API 추가 없이 상태를 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static object Call(string name, params object[] args)
        { return view.GetType().GetMethod(name, Private).Invoke(view, args); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Field
        /// desc         : 제품 XAML 요소 및 메뉴 내부 상태를 읽는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : TASK-045 실제 제품 로드 결과에만 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static T Field<T>(string name)
        { return (T)view.GetType().GetField(name, Private).GetValue(view); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Pump
        /// desc         : 툴팁 Popup 및 기존 메뉴 애니메이션의 Dispatcher 작업을 처리한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : TASK-045 합성 입력과 실제 Windows 입력을 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Pump(int milliseconds)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (DateTime.UtcNow < until)
            { Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { })); Thread.Sleep(5); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : 첫 실패를 명확히 반환하고 이미 성공한 조건을 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : TASK-045 성공 수 부풀림이나 실패 덮어쓰기 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Verify
        /// desc         : 열린 툴팁을 실제 표시한 뒤 메뉴 전환·복구·반복 열기와 수명을 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : TASK-045 200/260 폭과 밝게/어둡게 모드별 실제 Popup 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Verify()
        {
            ToggleButton trigger = Field<ToggleButton>("OptionsToggle");
            ToolTip tip = trigger.ToolTip as ToolTip;
            Check(tip != null && Convert.ToString(tip.Content) == "설정 및 사용 안내", "Product XAML retains a concrete tooltip with the original help text");
            Check(AutomationProperties.GetName(trigger) == "설정 및 사용 안내 메뉴", "Trigger Automation Name remains unchanged");
            Check(AutomationProperties.GetName(Field<Button>("HelpButton")) == "사용 안내" && AutomationProperties.GetName(Field<Button>("SettingsButton")) == "설정", "Menu item Automation Names remain unchanged");
            Check(Field<DispatcherTimer>("fanCloseTimer").Interval == TimeSpan.FromMilliseconds(175), "Existing 175ms pointer movement grace remains unchanged");
            Window.GetWindow(view).Activate(); Call("OpenFanMenu"); Pump(250);
            Button help = Field<Button>("HelpButton"), settings = Field<Button>("SettingsButton");
            Keyboard.Focus(help); Pump(30);
            Check(help.IsKeyboardFocused, "WPF keyboard navigation can focus the Help sector");
            help.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); Pump(30);
            Check(settings.IsKeyboardFocused, "WPF Tab traversal moves from Help to Settings");
            var escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            settings.RaiseEvent(escape); Pump(30);
            Check(escape.Handled && !Field<bool>("fanMenuOpen") && trigger.IsKeyboardFocused && ToolTipService.GetIsEnabled(trigger), "Managed Escape route closes menu, restores trigger focus and enables closed-state help");
            foreach (double width in new[] { 200.0, 260.0 }) foreach (string mode in new[] { "light", "dark" })
            {
                string label = width + "px " + mode;
                view.Width = width; Call("RestoreThemeMode", mode); view.UpdateLayout(); Pump(50);
                Call("CloseFanMenu");
                Check(ToolTipService.GetIsEnabled(trigger) && Field<Canvas>("FanPanel").Visibility == Visibility.Collapsed, label + " closed menu retains tooltip service");
                tip.PlacementTarget = trigger; tip.IsOpen = true; Pump(60);
                Check(tip.IsOpen && PresentationSource.FromVisual(tip) != null, label + " actual tooltip Popup is connected before menu opens");
                Call("OpenFanMenu"); Pump(250);
                Check(!tip.IsOpen && !ToolTipService.GetIsEnabled(trigger) && Field<Canvas>("FanPanel").IsVisible, label + " opening menu closes the visible tooltip and prevents delayed reopening");
                int opens = Field<int>("fanOpenCount"); Call("OpenFanMenu"); Pump(20);
                Check(Field<int>("fanOpenCount") == opens && !tip.IsOpen && !ToolTipService.GetIsEnabled(trigger), label + " reentry does not replay menu opening or revive tooltip");
                Call("CloseFanMenu"); Pump(20);
                Check(ToolTipService.GetIsEnabled(trigger) && !tip.IsOpen && !Field<bool>("fanMenuOpen"), label + " closing restores help availability with menu collapsed");
                tip.IsOpen = true; Pump(40);
                Check(tip.IsOpen && PresentationSource.FromVisual(tip) != null, label + " closed-state tooltip can be shown again");
                tip.IsOpen = false;
            }
            view.GetType().GetField("busy", Private).SetValue(view, true); Call("OpenFanMenu");
            Check(!Field<bool>("fanMenuOpen") && ToolTipService.GetIsEnabled(trigger), "Busy-state rejection keeps menu closed and does not suppress closed-state help");
            view.GetType().GetField("busy", Private).SetValue(view, false);
            Call("OpenFanMenu"); Call("DisposeFanMenu");
            Check(!tip.IsOpen && !Field<bool>("fanMenuOpen") && Field<DispatcherTimer>("fanCloseTimer") == null, "Disposal leaves no tooltip or fan timer open");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SnapshotState
        /// desc         : 실제 OS 입력 뒤의 메뉴·툴팁·포커스·클릭 수를 임시 검증 폴더에 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : TASK-045 마우스나 키 입력을 자체 주입하지 않는 관찰 전용 기록.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void SnapshotState(object sender, EventArgs args)
        {
            ToggleButton trigger = Field<ToggleButton>("OptionsToggle");
            ToolTip tip = trigger.ToolTip as ToolTip;
            string focus = Keyboard.FocusedElement is FrameworkElement ? ((FrameworkElement)Keyboard.FocusedElement).Name : "";
            var data = new { At = DateTimeOffset.Now.ToString("o"), Width = view.ActualWidth, OwnerActive = Window.GetWindow(view).IsActive, Deactivations = deactivations, MenuOpen = Field<bool>("fanMenuOpen"), TooltipEnabled = ToolTipService.GetIsEnabled(trigger), TooltipOpen = tip != null && tip.IsOpen, TriggerMouseOver = trigger.IsMouseOver, MenuMouseOver = Field<Canvas>("FanPanel").IsMouseOver, Focus = focus, HelpClicks = helpClicks, SettingsClicks = settingsClicks, OpenCount = Field<int>("fanOpenCount") };
            File.WriteAllText(Path.Combine(output, "interactive-state.json"), new JavaScriptSerializer().Serialize(data));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Main
        /// desc         : 개인 설정·발견 기능을 격리하고 지정한 고정 DLL만 독립 WPF 창으로 연다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : TASK-045 IDE·계정·실제 CLI를 실행하지 않는 제품 검사 호스트.
        /// </summary>
        /// ---------------------------------------------------------------------
        [STAThread]
        public static int Main(string[] args)
        {
            Window window = null; DispatcherTimer observer = null;
            try
            {
                if (args.Length > 1) throw new ArgumentException("At most one local test configuration is allowed.");
                var serializer = new JavaScriptSerializer();
                string configurationPath = args.Length == 0 ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configuration.json") : args[0];
                var config = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(configurationPath));
                output = (string)config["OutputDirectory"];
                string productPath = (string)config["AssemblyPath"];
                var dependencies = serializer.ConvertToType<Dictionary<string, string>>(config["Dependencies"]);
                AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs request)
                {
                    string name = new AssemblyName(request.Name).Name;
                    if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
                    string adjacent = Path.Combine(Path.GetDirectoryName(productPath), name + ".dll");
                    if (File.Exists(adjacent)) return Assembly.LoadFrom(adjacent);
                    return dependencies.ContainsKey(name) ? Assembly.LoadFrom(dependencies[name]) : null;
                };
                Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", Path.Combine(output, "settings.json"));
                Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", Path.Combine(output, "workspace"));
                Environment.SetEnvironmentVariable("VS2017_AI_DISABLE_RUNTIME_DISCOVERY", "1");
                File.WriteAllText(Path.Combine(output, "settings.json"), serializer.Serialize(new { HideWelcome = true, FontSize = 14, ThemeMode = (string)config["ThemeMode"] }));
                Assembly.LoadFrom(dependencies["Microsoft.VisualStudio.Shell.15.0"]);
                view = (FrameworkElement)Activator.CreateInstance(Assembly.LoadFrom(productPath).GetType("VS2017.AIAssistant.Views.AssistantControl", true));
                foreach (string name in new[] { "scmLookupStarted", "scmLookupCompleted" }) view.GetType().GetField(name, Private).SetValue(view, true);
                bool interactive = (bool)config["Interactive"];
                view.Width = Convert.ToDouble(config["Width"]);
                window = new Window { Title = "TASK045 Menu Verification", Width = 620, Height = 800, Content = view, ShowActivated = interactive, WindowStartupLocation = WindowStartupLocation.CenterScreen };
                if (interactive)
                {
                    window.Deactivated += delegate { deactivations++; };
                    EventManager.RegisterClassHandler(typeof(Button), ButtonBase.ClickEvent, new RoutedEventHandler(delegate(object sender, RoutedEventArgs eventArgs)
                    { if (sender == Field<Button>("HelpButton")) helpClicks++; if (sender == Field<Button>("SettingsButton")) settingsClicks++; }), true);
                    observer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) }; observer.Tick += SnapshotState; observer.Start();
                    window.ShowDialog();
                }
                else { window.Show(); Pump(120); Verify(); }
                File.WriteAllText(Path.Combine(output, "result.json"), serializer.Serialize(new { Succeeded = true, Passed = checks.Count, Checks = checks, Interactive = interactive, PersonalSettingsRead = false, CliDiscoveryEnabled = false, AiInvoked = false }));
                return 0;
            }
            catch (Exception error)
            {
                if (output != null) File.WriteAllText(Path.Combine(output, "result.json"), new JavaScriptSerializer().Serialize(new { Succeeded = false, Passed = checks.Count, Checks = checks, Error = error.ToString() }));
                return 1;
            }
            finally
            {
                if (observer != null) observer.Stop();
                if (view is IDisposable) ((IDisposable)view).Dispose();
                if (window != null) window.Close();
            }
        }
    }
}
