using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
#if TASK073_ROOT_RESOLVER
using CodexOrig.Task073.Validation;
#endif

/// ---------------------------------------------------------------------
/// <summary>
/// name         : KeyboardToolTipProbe
/// desc         : 실제 후보의 공개 tooltip 동작을 자기 창의 실제 키보드 초점과 공개 이벤트로 검사한다.
/// author       : CodexOrig
/// create date  : 2026-11-03
/// </summary>
/// ---------------------------------------------------------------------
internal static class KeyboardToolTipProbe
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CheckRow
    /// desc         : 고정 검사 이름과 실제 판정만 결과로 전달한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class CheckRow { public string Name; public bool Passed; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : EnvironmentUnavailable
    /// desc         : 활성 창·로드·초점 미충족을 제품 실패와 구분한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class EnvironmentUnavailable : Exception { internal EnvironmentUnavailable(string name) : base(name) { } }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Words
    /// desc         : 원래 binding 보존을 검사할 익명 문자열의 변경 알림만 제공한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class Words : INotifyPropertyChanged
    {
        private string text;
        public string Text { get { return text; } set { text = value; var changed = PropertyChanged; if (changed != null) changed(this, new PropertyChangedEventArgs("Text")); } }
        public event PropertyChangedEventHandler PropertyChanged;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : OriginalTip
    /// desc         : 검사 전 tooltip 값·binding 참조와 mouse 서비스 값을 메모리에 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class OriginalTip
    {
        internal Button Button;
        internal object LocalValue;
        internal BindingExpression Binding;
        internal int InitialDelay, Duration, BetweenDelay;
        internal bool MouseEnabled;
    }
    private static readonly List<CheckRow> checks = new List<CheckRow>();
    private static readonly List<ToolTip> tips = new List<ToolTip>();
    private static readonly HashSet<Button> owned = new HashSet<Button>();
    private static readonly HashSet<string> cases = new HashSet<string>(StringComparer.Ordinal);
    private static readonly string[] requiredCases = { "string-1", "string-2", "string-3", "focus-leave", "escape-first", "escape-second", "binding-preserved", "unloaded", "disabled", "reenabled", "original-mouse-values", "window-close" };
    private static Window window;
    private static StackPanel panel;
    private static Button[] buttons;
    private static TextBox sink;
    private static MethodInfo enable, enabled;
    private static bool showCalled, windowClosed, keyboardFocusUsed, escapeRaised;
    private static string environmentStage;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 고정 후보 DLL만 로드하고 환경 미충족·기능 실패·정상 종료를 구분한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        string outcome = "Failed", errorType = null; int? errorHResult = null;
        string candidateName = null;
        bool loadedExact = false, assemblyUnchanged = false;
        IDisposable resolver = null;
        Application app = null;
        int exit = 1;
        try
        {
            if (args.Length != 4) throw new InvalidOperationException("Fixed argument tuple required.");
            var config = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(args[3]));
            candidateName = Convert.ToString(config["CandidateName"]);
            if (String.IsNullOrWhiteSpace(candidateName) || !String.Equals(Hash(args[0]), args[1], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Candidate identity rejected.");
#if TASK073_ROOT_RESOLVER
            var directories = ((System.Collections.IEnumerable)config["RuntimeDirectories"]).Cast<object>().Select(value => Convert.ToString(value)).ToArray();
            resolver = new ManagedCandidateResolver(args[0], directories);
#endif
            var assembly = Assembly.LoadFrom(args[0]);
            loadedExact = String.Equals(Path.GetFullPath(assembly.Location), Path.GetFullPath(args[0]), StringComparison.OrdinalIgnoreCase) && Hash(assembly.Location) == args[1];
            if (!loadedExact) throw new InvalidDataException("Loaded assembly copy differs.");
            var behavior = assembly.GetType("VS2017.AIAssistant.Views.AssistantKeyboardToolTip", true, false);
            enable = behavior.GetMethod("SetIsEnabled", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(DependencyObject), typeof(bool) }, null);
            enabled = behavior.GetMethod("GetIsEnabled", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(DependencyObject) }, null);
            if (enable == null || enabled == null) throw new MissingMethodException("Public attached behavior missing.");
            app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler(Opened), true);
            Exercise();
            outcome = "Passed"; exit = 0;
        }
        catch (EnvironmentUnavailable error) { outcome = "NotRun"; environmentStage = error.Message; errorType = error.GetType().FullName; errorHResult = error.HResult; exit = 2; }
        catch (Exception error)
        {
            var invocation = error as TargetInvocationException;
            var actual = invocation != null && invocation.InnerException != null ? invocation.InnerException : error;
            errorType = actual.GetType().FullName; errorHResult = actual.HResult;
        }
        finally
        {
            try
            {
                if (window != null && !windowClosed) { window.Close(); Drain(); }
                if (tips.Any(tip => tip.IsOpen)) { outcome = "Failed"; exit = 1; }
            }
            catch (Exception error) { outcome = "Failed"; exit = 1; errorType = error.GetType().FullName; errorHResult = error.HResult; }
            if (resolver != null) resolver.Dispose();
            if (args.Length == 4)
            {
                assemblyUnchanged = File.Exists(args[0]) && Hash(args[0]) == args[1];
                if (!assemblyUnchanged) { outcome = "Failed"; exit = 1; }
                if (outcome == "Passed" && (!cases.SetEquals(requiredCases) || checks.Any(row => !row.Passed))) { outcome = "Failed"; exit = 1; }
                var result = new {
                    Succeeded = outcome == "Passed" && cases.SetEquals(requiredCases) && checks.All(row => row.Passed) && loadedExact && assemblyUnchanged,
                    Outcome = outcome, ExitCode = exit, Passed = checks.Count(row => row.Passed), Failed = checks.Count(row => !row.Passed), Checks = checks,
                    RequiredCases = requiredCases, CompletedCases = cases.OrderBy(value => value).ToArray(), AllRequiredCasesExecuted = cases.SetEquals(requiredCases),
                    ActualProductDll = loadedExact, LoadedExactCopy = loadedExact, AssemblyUnchanged = assemblyUnchanged, SourceCompiled = false,
                    Candidate = candidateName, ProductSha256 = args[1], StaticTypeOnly = true, PrivateProductHandlersInvoked = false, PrivateGuardStateChanged = false,
                    WindowShowCalled = showCalled, WindowClosed = windowClosed, OpenOwnedToolTips = tips.Count(tip => tip.IsOpen), EnvironmentStage = environmentStage,
                    ErrorType = errorType, ErrorHResult = errorHResult, PhysicalKeyboardTested = false, PhysicalMouseTested = false,
                    KeyboardFocusApiUsed = keyboardFocusUsed, EscapeRoutedEventRaised = escapeRaised, OtherWindowsAutomated = false, ActualIde = false,
                    UserProfileOrSettingsUsed = false, CliUsed = false, NetworkRequested = false, AiUsed = false,
                    Limitation = "Actual product static behavior in an owned active WPF window. Keyboard.Focus and routed Escape are API tests, not physical input. Mouse tooltip invocation and whole product UI are not tested."
                };
                File.WriteAllText(args[2], new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }.Serialize(result), new System.Text.UTF8Encoding(false));
            }
            if (app != null) app.Shutdown(exit);
        }
        return exit;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Exercise
    /// desc         : 합성 문자열 세 개의 표시·초점·닫기·수명과 원래 binding을 공개 API로 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Exercise()
    {
        panel = new StackPanel { Margin = new Thickness(18) };
        window = new Window { Title = "TASK073 anonymous tooltip verification", Width = 420, Height = 260, Content = panel, ShowInTaskbar = false, ShowActivated = true, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        window.Closed += (sender, args) => windowClosed = true;
        sink = new TextBox { Text = "anonymous focus sink", Margin = new Thickness(0, 12, 0, 0) };
        string[] strings = { "Anonymous complete setting description one, retained beyond a deliberately short button caption.", "Anonymous complete bound description two, retained without changing its original tooltip binding.", "Anonymous complete guide description three, retained through unload and repeated enable cycles." };
        var words = new Words { Text = strings[1] };
        buttons = new Button[3];
        for (int index = 0; index < buttons.Length; index++)
        {
            var button = new Button { Content = "Anonymous " + (index + 1), Height = 28, Margin = new Thickness(0, 0, 0, 8), Focusable = true };
            if (index == 1) BindingOperations.SetBinding(button, FrameworkElement.ToolTipProperty, new Binding("Text") { Source = words, Mode = BindingMode.OneWay });
            else ToolTipService.SetToolTip(button, strings[index]);
            ToolTipService.SetInitialShowDelay(button, 777); ToolTipService.SetShowDuration(button, 12345); ToolTipService.SetBetweenShowDelay(button, 321); ToolTipService.SetIsEnabled(button, true);
            owned.Add(button); buttons[index] = button; panel.Children.Add(button);
            SetEnabled(button, true); SetEnabled(button, true);
        }
        panel.Children.Add(sink);
        var originals = buttons.Select(button => new OriginalTip { Button = button, LocalValue = button.ReadLocalValue(FrameworkElement.ToolTipProperty), Binding = BindingOperations.GetBindingExpression(button, FrameworkElement.ToolTipProperty), InitialDelay = ToolTipService.GetInitialShowDelay(button), Duration = ToolTipService.GetShowDuration(button), BetweenDelay = ToolTipService.GetBetweenShowDelay(button), MouseEnabled = ToolTipService.GetIsEnabled(button) }).ToArray();
        showCalled = true; window.Show(); window.Activate();
        Wait(() => window.IsLoaded && window.IsActive, "owned-window-loaded-active", true);
        Focus(sink);
        ToolTip previous = null;
        for (int index = 0; index < buttons.Length; index++)
        {
            var tip = FocusAndOpen(buttons[index], strings[index]);
            if (previous != null) Check("prior tip closed on focus leaving " + index, !previous.IsOpen && previous.Content == null && previous.PlacementTarget == null);
            previous = tip; cases.Add("string-" + (index + 1));
        }
        Focus(sink); Wait(() => !previous.IsOpen, "focus-leave-closes", false);
        Check("focus leave clears tip content and placement", previous.Content == null && previous.PlacementTarget == null); cases.Add("focus-leave");

        var escapeTip = FocusAndOpen(buttons[0], strings[0]);
        var firstEscape = Escape(buttons[0]);
        Check("first routed escape dismisses the owned tip and is handled", firstEscape.Handled && !escapeTip.IsOpen && escapeTip.Content == null && escapeTip.PlacementTarget == null); cases.Add("escape-first");
        var secondEscape = Escape(buttons[0]); Check("second routed escape stays available to parent behavior", !secondEscape.Handled); cases.Add("escape-second");
        Focus(sink); words.Text = "Anonymous replacement bound description, supplied only by the fixture source object."; Drain();
        Check("original tooltip binding tracks source updates", ReferenceEquals(originals[1].Binding, BindingOperations.GetBindingExpression(buttons[1], FrameworkElement.ToolTipProperty)) && Convert.ToString(ToolTipService.GetToolTip(buttons[1])) == words.Text);
        FocusAndOpen(buttons[1], words.Text); cases.Add("binding-preserved");

        Focus(sink); var unloadTip = FocusAndOpen(buttons[2], strings[2]); panel.Children.Remove(buttons[2]);
        Wait(() => !buttons[2].IsLoaded && !unloadTip.IsOpen, "real-unloaded-closes-owned-tip", false);
        Check("unloaded clears transient references", unloadTip.Content == null && unloadTip.PlacementTarget == null); cases.Add("unloaded");
        panel.Children.Insert(2, buttons[2]); Wait(() => buttons[2].IsLoaded, "reinsert-loads-control", true); Focus(sink);
        var disableTip = FocusAndOpen(buttons[2], strings[2]); SetEnabled(buttons[2], false);
        Check("disable closes owned tip without changing original mouse value", !(bool)enabled.Invoke(null, new object[] { buttons[2] }) && !disableTip.IsOpen && Convert.ToString(ToolTipService.GetToolTip(buttons[2])) == strings[2]);
        Focus(sink); int countDisabled = tips.Count; Focus(buttons[2]); Drain(); Check("disabled behavior creates no keyboard popup", tips.Count == countDisabled); cases.Add("disabled");
        for (int repeat = 0; repeat < 3; repeat++)
        {
            Focus(sink); SetEnabled(buttons[2], false); SetEnabled(buttons[2], true); SetEnabled(buttons[2], true);
            FocusAndOpen(buttons[2], strings[2]);
        }
        cases.Add("reenabled");
        foreach (var original in originals)
        {
            Check("original tooltip local value and binding preserved " + Array.IndexOf(buttons, original.Button), ReferenceEquals(original.LocalValue, original.Button.ReadLocalValue(FrameworkElement.ToolTipProperty)) && ReferenceEquals(original.Binding, BindingOperations.GetBindingExpression(original.Button, FrameworkElement.ToolTipProperty)));
            Check("original mouse tooltip service settings preserved " + Array.IndexOf(buttons, original.Button), original.InitialDelay == ToolTipService.GetInitialShowDelay(original.Button) && original.Duration == ToolTipService.GetShowDuration(original.Button) && original.BetweenDelay == ToolTipService.GetBetweenShowDelay(original.Button) && original.MouseEnabled == ToolTipService.GetIsEnabled(original.Button));
        }
        cases.Add("original-mouse-values");
        window.Close(); Drain();
        Check("closing owned window closes and clears every observed transient tip", windowClosed && tips.All(tip => !tip.IsOpen && tip.PlacementTarget == null && tip.Content == null)); cases.Add("window-close");
        Check("all required behavior cases executed", cases.SetEquals(requiredCases));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FocusAndOpen
    /// desc         : 실제 초점 획득 한 번당 공개 Opened 이벤트 하나와 실제 popup 표시를 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ToolTip FocusAndOpen(Button button, string expected)
    {
        int before = tips.Count; Focus(button);
        Wait(() => tips.Count > before, "keyboard-tooltip-opened", false); Drain();
        Check("one owned tooltip per real focus entry", tips.Count == before + 1);
        var tip = tips[tips.Count - 1];
        Wait(() => tip.IsOpen && tip.IsVisible && PresentationSource.FromVisual(tip) != null && tip.ActualWidth > 0, "popup-has-live-presentation-source", false);
        var text = tip.Content as TextBlock;
        Check("popup contains exact full anonymous tooltip text", text != null && text.Text == expected && text.TextWrapping == TextWrapping.Wrap);
        Check("popup uses exact focused owner without taking focus", ReferenceEquals(tip.PlacementTarget, button) && button.IsKeyboardFocused && !tip.Focusable && !tip.IsHitTestVisible);
        return tip;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Opened
    /// desc         : 공개 Opened 이벤트에서 자기 버튼 소유 tooltip만 관찰한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Opened(object sender, RoutedEventArgs args)
    {
        var tip = sender as ToolTip ?? args.OriginalSource as ToolTip;
        if (tip != null && tip.PlacementTarget is Button && owned.Contains((Button)tip.PlacementTarget)) tips.Add(tip);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SetEnabled
    /// desc         : 실제 제품의 공개 attached setter만 호출한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void SetEnabled(Button button, bool value) { enable.Invoke(null, new object[] { button, value }); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Focus
    /// desc         : 자기 활성 창에서 Keyboard.Focus의 실제 초점 결과를 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Focus(FrameworkElement element)
    {
        if (!window.IsActive || !element.IsLoaded || !element.IsVisible) throw new EnvironmentUnavailable("own-window-or-target-not-active-loaded-visible");
        keyboardFocusUsed = true; Keyboard.Focus(element);
        Wait(() => window.IsActive && element.IsKeyboardFocusWithin && ReferenceEquals(Keyboard.FocusedElement, element), "actual-keyboard-focus-unavailable", true);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Escape
    /// desc         : 자기 버튼에 공개 routed Escape를 발생시키며 물리 입력으로 주장하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static KeyEventArgs Escape(Button button)
    {
        var source = PresentationSource.FromVisual(button);
        if (source == null) throw new EnvironmentUnavailable("own-presentation-source-unavailable");
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent, Source = button };
        escapeRaised = true; button.RaiseEvent(args); Drain(); return args;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 실패를 기록한 뒤 즉시 종료하여 미실행 계약을 통과로 세지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, bool value) { checks.Add(new CheckRow { Name = name, Passed = value }); if (!value) throw new InvalidOperationException("Owned tooltip contract failed."); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Wait
    /// desc         : 제한된 자기 dispatcher 대기 뒤 환경 미충족과 기능 실패를 구분한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Wait(Func<bool> predicate, string name, bool environment)
    {
        var watch = Stopwatch.StartNew(); while (!predicate() && watch.ElapsedMilliseconds < 2000) Pump();
        if (predicate()) return; if (environment) throw new EnvironmentUnavailable(name); Check(name, false);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Drain
    /// desc         : 자기 이벤트 큐 두 회를 처리하여 지연 중복 알림도 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Drain() { Pump(); Pump(); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pump
    /// desc         : 자기 WPF dispatcher를 짧게 처리하고 외부 창이나 OS 입력을 사용하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Pump()
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(15) };
        timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Hash
    /// desc         : 명시한 후보 사본의 SHA만 읽고 파일 내용을 변경하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
}
