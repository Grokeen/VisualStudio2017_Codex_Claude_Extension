using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Automation;
using VS2017.AIAssistant.Services;
using VS2017.AIAssistant.Views;

/// <summary>Anonymous offscreen C8 acceptance for TASK069 Role #18; injects only a synthetic read-only break frame.</summary>
internal static class CaptureC8DebuggingHelper
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string output;
    private static readonly List<object> checks = new List<object>();
    private static int failures;
    private static string formattedContext;

    [STAThread]
    private static int Main(string[] args)
    {
        string productPath = Path.GetFullPath(args[0]);
        var dependencies = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(args[1]));
        output = Path.GetFullPath(args[2]);
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            string name = new AssemblyName(eventArgs.Name).Name;
            string sibling = Path.Combine(Path.GetDirectoryName(productPath), name + ".dll");
            if (File.Exists(sibling)) return Assembly.LoadFrom(sibling);
            string mapped;
            return dependencies.TryGetValue(name, out mapped) && File.Exists(mapped) ? Assembly.LoadFrom(mapped) : null;
        };
        Assembly.LoadFrom(productPath);
        Directory.CreateDirectory(output);
        CheckServiceFormattingAndBounds();
        var control = new AssistantControl();
        try { VerifyUiAndCapture(control); }
        finally { control.Dispose(); }
        var result = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(new
        {
            Status = failures == 0 ? "PASS" : "FAIL",
            ProductCandidate = "TASK069 C8 frozen VS2017 Release exact DLL",
            Fixture = new { Debugger = "synthetic break frame; no actual IDE connection", Identity = "사용자 · Codex", Source = "DebugSample.cs", SendOrAi = false },
            WindowShown = false, PopupOpened = false, OsInputUsed = false, NetworkUsed = false,
            Checks = checks, Failures = failures,
            Captures = new[] { "vs-dark-wide-debug-break.png", "vs-dark-narrow-debug-break.png", "vs-light-wide-debug-break.png", "vs-light-narrow-debug-break.png", "kakao-dark-wide-debug-break.png", "kakao-dark-narrow-debug-break.png", "kakao-light-wide-debug-break.png", "kakao-light-narrow-debug-break.png", "dracula-dark-wide-debug-break.png", "dracula-dark-narrow-debug-break.png", "vs-dark-idle-helper-hidden.png", "vs-dark-inflight-helper-disabled.png", "vs-dark-success-draft.png", "vs-dark-error-preserves-draft.png", "vs-dark-limit-preserves-draft.png", "vs-dark-changed-draft-preserved.png" }
        });
        File.WriteAllText(Path.Combine(output, "capture-result.json"), result, new UTF8Encoding(false));
        Console.WriteLine(result);
        return failures == 0 ? 0 : 1;
    }

    private static void VerifyUiAndCapture(AssistantControl control)
    {
        SetPrivateText(control, "CurrentAgentLabel", "사용자 · Codex");
        object chat = control.GetType().GetProperty("ActiveChat", Private).GetValue(control, null);
        MethodInfo add = chat.GetType().GetMethod("Add", new[] { typeof(string), typeof(string) });
        add.Invoke(chat, new object[] { "user", "합성 오류 분석 요청을 준비해 주세요." });
        add.Invoke(chat, new object[] { "assistant", "합성 응답입니다. 실제 전송은 수행하지 않습니다." });
        var messages = (System.Collections.ICollection)chat.GetType().GetProperty("Messages").GetValue(chat, null);
        int messageCount = messages.Count;
        var provider = new SyntheticProvider { Context = formattedContext, CanCaptureState = false };
        control.GetType().GetMethod("SetDebuggingContextProvider", Private).Invoke(control, new object[] { provider });
        var button = (Button)GetField(control, "DebuggingInfoButton");
        var prompt = (TextBox)GetField(control, "Instruction");
        Check("DBG action is hidden outside break mode", button.Visibility == Visibility.Collapsed, new { Visibility = button.Visibility.ToString() });
        Capture(control, "vs-dark-idle-helper-hidden.png", 620, 760);
        provider.CanCaptureState = true; provider.RaiseStateChanged(); control.UpdateLayout();
        Check("DBG action appears and enables for a readable break frame", button.Visibility == Visibility.Visible && button.IsEnabled, new { Visibility = button.Visibility.ToString(), button.IsEnabled });
        Check("DBG action exposes accessible name and privacy tooltip", AutomationProperties.GetName(button) == "디버깅 정보 초안 추가" && Convert.ToString(button.ToolTip).Contains("자동 전송하지 않습니다"), new { Name = AutomationProperties.GetName(button), ToolTip = Convert.ToString(button.ToolTip) });

        foreach (var theme in new[] {
            new ThemeCase("vs-dark", "dark", "vs"), new ThemeCase("vs-light", "light", "vs"),
            new ThemeCase("kakao-dark", "dark", "kakao"), new ThemeCase("kakao-light", "light", "kakao"),
            new ThemeCase("dracula-dark", "dark", "dracula") })
        {
            SetTheme(control, theme.Mode, theme.Preset);
            Check(theme.Name + " DBG button follows active palette and remains visible", button.Visibility == Visibility.Visible && button.ActualWidth > 0, new { Visibility = button.Visibility.ToString(), Width = button.ActualWidth });
            Capture(control, theme.Name + "-wide-debug-break.png", 620, 760);
            Capture(control, theme.Name + "-narrow-debug-break.png", 390, 760);
        }

        SetTheme(control, "dark", "vs");
        prompt.Text = "기존 사용자 요청"; prompt.Select(2, 3);
        provider.Mode = SyntheticProviderMode.Pending;
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); control.UpdateLayout();
        Check("click starts one read and disables the button while pending", provider.CaptureCount == 1 && !button.IsEnabled && prompt.Text == "기존 사용자 요청", new { provider.CaptureCount, button.IsEnabled });
        Capture(control, "vs-dark-inflight-helper-disabled.png", 620, 760);
        provider.Complete(formattedContext); PumpUntil(() => button.IsEnabled, 5000);
        string expected = "기존 사용자 요청\n\n" + formattedContext;
        Check("success appends exact bounded context and preserves selection", prompt.Text == expected && prompt.SelectionStart == 2 && prompt.SelectionLength == 3, new { PromptLength = prompt.Text.Length, prompt.SelectionStart, prompt.SelectionLength });
        Check("success shows review-before-send status and does not add a chat message", Convert.ToString(((TextBlock)GetField(control, "StatusLabel")).Text).Contains("확인한 뒤 보내세요") && messages.Count == messageCount, new { Status = ((TextBlock)GetField(control, "StatusLabel")).Text, Messages = messages.Count });
        Capture(control, "vs-dark-success-draft.png", 620, 760);

        prompt.Text = "오류가 있어도 이 문장은 보존"; string beforeError = prompt.Text;
        provider.Mode = SyntheticProviderMode.Failure;
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); PumpUntil(() => button.IsEnabled, 5000);
        Check("read failure leaves original draft and reports a recoverable state", prompt.Text == beforeError && ((TextBlock)GetField(control, "StatusLabel")).Text.Contains("읽지 못했습니다") && button.IsEnabled, new { Status = ((TextBlock)GetField(control, "StatusLabel")).Text });
        Capture(control, "vs-dark-error-preserves-draft.png", 620, 760);

        prompt.Text = new string('x', 15990); string beforeLimit = prompt.Text; provider.Mode = SyntheticProviderMode.Immediate;
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); PumpUntil(() => button.IsEnabled, 5000);
        Check("16k overflow is atomic and preserves the full prior draft", prompt.Text == beforeLimit && ((TextBlock)GetField(control, "StatusLabel")).Text.Contains("16,000자") && button.IsEnabled, new { DraftLength = prompt.Text.Length, Status = ((TextBlock)GetField(control, "StatusLabel")).Text });
        Capture(control, "vs-dark-limit-preserves-draft.png", 620, 760);

        prompt.Text = "초안 변경 경계"; provider.Mode = SyntheticProviderMode.Pending;
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        prompt.Text = "사용자가 클릭 뒤 수정한 초안"; provider.Complete(formattedContext); PumpUntil(() => button.IsEnabled, 5000);
        Check("concurrent draft edit wins over captured debugger context", prompt.Text == "사용자가 클릭 뒤 수정한 초안" && ((TextBlock)GetField(control, "StatusLabel")).Text.Contains("초안이 바뀌어 추가하지 않았습니다"), new { Draft = prompt.Text, Status = ((TextBlock)GetField(control, "StatusLabel")).Text });
        Capture(control, "vs-dark-changed-draft-preserved.png", 620, 760);
        provider.CanCaptureState = false; provider.RaiseStateChanged(); control.UpdateLayout();
        Check("DBG action hides when debugger leaves break mode", button.Visibility == Visibility.Collapsed, new { Visibility = button.Visibility.ToString() });
        Check("all DBG actions leave conversation message count unchanged", messages.Count == messageCount, new { Before = messageCount, After = messages.Count });
    }

    private static void SetTheme(AssistantControl control, string mode, string preset)
    {
        SelectTag((ComboBox)GetField(control, "ThemeModeChoice"), mode);
        SelectTag((ComboBox)GetField(control, "VisualThemePresetChoice"), preset);
        control.UpdateLayout();
    }
    private static void SelectTag(ComboBox combo, string tag)
    { var item = combo.Items.Cast<ComboBoxItem>().First(value => String.Equals(Convert.ToString(value.Tag), tag, StringComparison.OrdinalIgnoreCase)); combo.SelectedItem = item; }
    private static object GetField(AssistantControl control, string name)
    { var field = control.GetType().GetField(name, Private); return field == null ? null : field.GetValue(control); }
    private static void SetPrivateText(AssistantControl control, string name, string value)
    {
        object target = GetField(control, name); var text = target as TextBlock; if (text != null) { text.Text = value; return; }
        var dependencyObject = target as DependencyObject; if (dependencyObject != null) ToolTipService.SetToolTip(dependencyObject, value);
    }
    private static void Capture(AssistantControl control, string filename, int width, int height)
    {
        control.Measure(new Size(width, height)); control.Arrange(new Rect(0, 0, width, height)); control.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(control);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, filename))) encoder.Save(stream);
    }
    private static void PumpUntil(Func<bool> condition, int timeoutMilliseconds)
    {
        var dispatcher = Dispatcher.CurrentDispatcher; var frame = new DispatcherFrame(); var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        timer.Interval = TimeSpan.FromMilliseconds(10); timer.Tick += (sender, args) => { if (condition() || DateTime.UtcNow >= deadline) frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        if (!condition()) throw new TimeoutException("Dispatcher did not complete the pending UI action.");
    }
    private static void Check(string name, bool pass, object details)
    { if (!pass) failures++; checks.Add(new { Name = name, Pass = pass, Details = details }); }

    private static void CheckServiceFormattingAndBounds()
    {
        var reader = new SyntheticReader(); var service = new DebuggingContextService(reader, Dispatcher.CurrentDispatcher);
        reader.Frame.IsBreakMode = false; Check("real service hides outside break mode", !service.CanCapture && reader.DetailReads == 0, null);
        reader.Frame.IsBreakMode = true; Check("real service CanCapture performs metadata-only read", service.CanCapture && reader.DetailReads == 0, null);
        formattedContext = service.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult();
        Check("real service output is bounded and uses a basename", formattedContext.Length <= 5000 && formattedContext.Contains("DebugSample.cs") && !formattedContext.Contains("C:\\synthetic"), new { Length = formattedContext.Length });
        Check("real service includes current line and +/-3 excerpt", formattedContext.Contains("97 :") && formattedContext.Contains("100 > ") && formattedContext.Contains("103 :"), null);
        Check("real service truncates stack to top three and excludes values", formattedContext.Contains("SyntheticEntry.OnLoad()") && !formattedContext.Contains("HiddenFrame.ShouldNotAppear") && formattedContext.Contains("변수 값·식 평가"), null);
        int details = reader.DetailReads; var cancelled = new CancellationTokenSource(); cancelled.Cancel(); bool cancelPass = false; try { service.CaptureAsync(cancelled.Token); } catch (OperationCanceledException) { cancelPass = true; }
        Check("cancelled capture reads no additional debugger context", cancelPass && reader.DetailReads == details, null); service.Dispose(); Check("service disposes read-only reader", reader.Disposed, null);
        var moved = new SyntheticReader { ChangeLineDuringValidation = true }; var movedService = new DebuggingContextService(moved, Dispatcher.CurrentDispatcher); bool movedRejected = false; try { movedService.CaptureAsync(CancellationToken.None); } catch (InvalidOperationException) { movedRejected = true; } Check("service rejects a moved debug frame", movedRejected, null); movedService.Dispose();
        var wide = new SyntheticReader { InvalidSourceRange = true }; var wideService = new DebuggingContextService(wide, Dispatcher.CurrentDispatcher); bool wideRejected = false; try { wideService.CaptureAsync(CancellationToken.None); } catch (InvalidOperationException) { wideRejected = true; } Check("service rejects source outside bounded line range", wideRejected, null); wideService.Dispose();
    }

    private sealed class SyntheticReader : IDebuggingContextReader
    {
        internal DebuggingFrameData Frame = new DebuggingFrameData { IsBreakMode = true, FilePath = @"C:\synthetic\DebugSample.cs", LineNumber = 100, FunctionName = "SyntheticWorker.Process()", SourceStartLine = 97,
            SourceLines = new[] { "class SyntheticWorker", "{", "    void Process()", "        var result = BuildInput();", "        return result;", "    }", "}" },
            StackFunctions = new[] { "SyntheticWorker.Process()", "SyntheticService.Run()", "SyntheticEntry.OnLoad()", "HiddenFrame.ShouldNotAppear" } };
        internal int DetailReads; internal bool Disposed; internal bool ChangeLineDuringValidation; internal bool InvalidSourceRange; private int validationReads;
        public event EventHandler StateChanged;
        public DebuggingFrameData ReadCurrent(bool includeDetails)
        {
            if (includeDetails) DetailReads++; else validationReads++;
            int line = Frame.LineNumber + (ChangeLineDuringValidation && !includeDetails && DetailReads > 0 ? 1 : 0);
            int first = InvalidSourceRange && includeDetails ? Frame.LineNumber - 4 : Frame.SourceStartLine;
            return new DebuggingFrameData { IsBreakMode = Frame.IsBreakMode, FilePath = Frame.FilePath, LineNumber = line, FunctionName = Frame.FunctionName, SourceStartLine = first, SourceLines = includeDetails ? Frame.SourceLines : new string[0], StackFunctions = includeDetails ? Frame.StackFunctions : new string[0] };
        }
        public void Dispose() { Disposed = true; }
    }

    private enum SyntheticProviderMode { Immediate, Pending, Failure }
    private sealed class SyntheticProvider : IDebuggingContextProvider
    {
        private TaskCompletionSource<string> pending;
        internal string Context; internal bool CanCaptureState; internal int CaptureCount; internal SyntheticProviderMode Mode; internal bool IsDisposed;
        public event EventHandler StateChanged;
        public bool CanCapture { get { return !IsDisposed && CanCaptureState; } }
        public Task<string> CaptureAsync(CancellationToken cancellation)
        {
            CaptureCount++;
            if (Mode == SyntheticProviderMode.Failure) return Task.FromException<string>(new InvalidOperationException("synthetic read failure"));
            if (Mode == SyntheticProviderMode.Pending) { pending = new TaskCompletionSource<string>(); cancellation.Register(() => pending.TrySetCanceled()); return pending.Task; }
            return Task.FromResult(Context);
        }
        internal void Complete(string value) { if (pending == null) throw new InvalidOperationException("No pending capture."); pending.TrySetResult(value); pending = null; }
        internal void RaiseStateChanged() { var handler = StateChanged; if (handler != null) handler(this, EventArgs.Empty); }
        public void Dispose() { IsDisposed = true; CanCaptureState = false; }
    }

    private sealed class ThemeCase { internal readonly string Name, Mode, Preset; internal ThemeCase(string name, string mode, string preset) { Name = name; Mode = mode; Preset = preset; } }
}
