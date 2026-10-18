using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodexCode.Task024
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AccountLoginResult
    /// desc         : 인증을 호출하지 않는 계정 화면 회귀 검사의 실제 성공과 실패를 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 실제 계정 인증 성공 근거로 집계하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class AccountLoginResult
    {
        public List<string> Passed { get; } = new List<string>();
        public List<string> Failures { get; } = new List<string>();
        public bool Succeeded { get { return Failures.Count == 0; } }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AccountLoginChecks
    /// desc         : 고정 실행 경로·명시 클릭·취소·종료·늦은 계정 결과·비밀 오류 비공개를 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 모든 인증 callback은 합성이며 실제 CLI·GUI 호출 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class AccountLoginChecks
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Run
        /// desc         : 지정된 assembly의 계정 화면을 만들고 callback 경계만 메모리 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 Window.Show·Dispatcher loop·네트워크·인증 호출 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static AccountLoginResult Run(Assembly assembly)
        {
            var result = new AccountLoginResult();
            object host = Activator.CreateInstance(assembly.GetType("VS2017.AIAssistant.Views.AssistantControl", true));
            var type = assembly.GetType("VS2017.AIAssistant.Views.AccountLoginDialog", true);
            var windows = new List<Window>();
            try
            {
                int logins = 0, checks = 0; string lastProvider = null, lastPath = null; bool throwLogin = false;
                Func<string, string, CancellationToken, Task<bool>> status = (p, e, token) => Task.FromResult(true);
                Action<string, string> login = (p, e) => { logins++; lastProvider = p; lastPath = e; if (throwLogin) throw new InvalidOperationException("password=do-not-persist token=private-fixture"); };
                Func<string, string, CancellationToken, Task<bool>> checker = (p, e, token) => { checks++; lastProvider = p; lastPath = e; return status(p, e, token); };
                var paths = new Dictionary<string, string> { { "Codex", @"C:\synthetic\codex.exe" }, { "Claude", @"C:\synthetic\claude.exe" } };
                var dialog = Create(type, host, "Codex", paths, login, checker); windows.Add(dialog);
                var choice = (ComboBox)Read(dialog, "providerChoice");
                var loginButton = (Button)Read(dialog, "loginButton"); var checkButton = (Button)Read(dialog, "checkButton"); var cancelButton = (Button)Read(dialog, "cancelCheckButton");
                Check(result, "Opening a separate login panel performs no login status call or window show", () => Assert(logins == 0 && checks == 0 && !dialog.IsVisible));
                Check(result, "Only Codex and Claude can be selected", () => Assert(choice.Items.Cast<string>().SequenceEqual(new[] { "Codex", "Claude" }) && (string)choice.SelectedItem == "Codex"));
                Check(result, "No username password API-key or editable credential field exists", () => Assert(!Descendants((DependencyObject)Get(dialog, "Body")).Any(x => x is TextBox || x is PasswordBox) && !choice.IsEditable));
                Check(result, "Direct password handling is explained without claiming a password API", () => Assert(Descendants((DependencyObject)Get(dialog, "Body")).OfType<TextBlock>().Any(x => x.Text.Contains("아이디와 비밀번호") && x.Text.Contains("직접 받거나 저장하지"))));
                Check(result, "Account panel follows a palette change without collecting credentials", () => {
                    var owner = (FrameworkElement)host; object background = owner.FindResource("AssistantBackgroundBrush"), foreground = owner.FindResource("AssistantForegroundBrush"), muted = owner.FindResource("AssistantMutedForegroundBrush");
                    try
                    { owner.Resources["AssistantBackgroundBrush"] = Brushes.White; owner.Resources["AssistantForegroundBrush"] = Brushes.Black; owner.Resources["AssistantMutedForegroundBrush"] = Brushes.DarkSlateGray; Assert(ReferenceEquals(dialog.Background, Brushes.White) && ReferenceEquals(dialog.Foreground, Brushes.Black) && ReferenceEquals(((TextBlock)Read(dialog, "executableLabel")).Foreground, Brushes.DarkSlateGray)); }
                    finally { owner.Resources["AssistantBackgroundBrush"] = background; owner.Resources["AssistantForegroundBrush"] = foreground; owner.Resources["AssistantMutedForegroundBrush"] = muted; }
                });
                paths["Codex"] = @"C:\changed-after-open\wrong.exe";
                loginButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(result, "Explicit login uses the provider and immutable executable snapshot", () => Assert(logins == 1 && lastProvider == "Codex" && lastPath == @"C:\synthetic\codex.exe"));
                Check(result, "Launching login does not assert authentication success", () => Assert(Message(dialog).Contains("창 열기를 요청") && !Message(dialog).Contains("로그인 정보가 있습니다")));
                loginButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(result, "Repeated login is guarded until a user status action", () => Assert(logins == 1 && !loginButton.IsEnabled));
                choice.SelectedItem = "Claude"; loginButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(result, "Claude uses its own executable snapshot without changing Codex settings", () => Assert(logins == 2 && lastProvider == "Claude" && lastPath == @"C:\synthetic\claude.exe"));
                Check(result, "Provider change shows the corresponding official authentication link", () => Assert(((string)Get(Read(dialog, "instructions"), "Markdown")).Contains("https://code.claude.com/docs/en/authentication")));
                var success = StartCheck(dialog);
                Check(result, "Successful fixture status reports credentials but does not promise model entitlement", () => Assert(success.IsCompleted && !success.IsFaulted && Message(dialog).Contains("로그인 정보가 있습니다") && Message(dialog).Contains("실제 모델")));
                status = (p, e, token) => Task.FromResult(false); StartCheck(dialog);
                Check(result, "Signed-out fixture status asks for explicit official login", () => Assert(Message(dialog).Contains("로그인 정보가 없습니다")));
                status = (p, e, token) => Task.FromException<bool>(new InvalidOperationException("token=private-fixture password=do-not-persist")); StartCheck(dialog);
                Check(result, "Raw credential-like status exceptions never enter the panel", () => Assert(!Message(dialog).Contains("private-fixture") && !Message(dialog).Contains("password=") && Message(dialog).Contains("확인하지 못했습니다")));
                choice.SelectedItem = "Codex"; throwLogin = true; loginButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(result, "Raw credential-like login exceptions never enter the panel", () => Assert(!Message(dialog).Contains("private-fixture") && !Message(dialog).Contains("password=") && Message(dialog).Contains("열지 못했습니다")));
                throwLogin = false;
                var pending = new TaskCompletionSource<bool>(); CancellationToken pendingToken = CancellationToken.None;
                status = (p, e, token) => { pendingToken = token; token.Register(() => pending.TrySetCanceled()); return pending.Task; };
                var pendingOperation = StartCheck(dialog);
                Check(result, "Pending status disables provider switching and duplicate auth while enabling cancel", () => Assert(!pendingOperation.IsCompleted && !choice.IsEnabled && !loginButton.IsEnabled && !checkButton.IsEnabled && cancelButton.IsEnabled));
                int beforeDuplicate = checks; StartCheck(dialog);
                Check(result, "Repeated status clicks do not create another request", () => Assert(checks == beforeDuplicate));
                cancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(result, "Cancel reaches only the local status token and restores controls", () => Assert(pendingToken.IsCancellationRequested && pendingOperation.IsCompleted && !pendingOperation.IsFaulted && choice.IsEnabled && checkButton.IsEnabled && !cancelButton.IsEnabled));
                Check(result, "Completed cancellation has a completed cancellation message", () => Assert(Message(dialog).Contains("취소했습니다") && !Message(dialog).Contains("취소하고 있습니다")));

                var late = new TaskCompletionSource<bool>(); CancellationToken lateToken = CancellationToken.None;
                status = (p, e, token) => { lateToken = token; return late.Task; };
                var lateOperation = StartCheck(dialog); dialog.Close(); string closedMessage = Message(dialog); late.SetResult(true);
                Check(result, "Closing cancels the status token even when a callback ignores cancellation", () => Assert(lateToken.IsCancellationRequested));
                Check(result, "Late status after close does not change the closed panel", () => Assert(lateOperation.IsCompleted && !lateOperation.IsFaulted && Message(dialog) == closedMessage && (bool)Read(dialog, "closed")));

                var stale = new TaskCompletionSource<bool>();
                var other = Create(type, host, "Codex", new Dictionary<string, string>(), login, (p, e, token) => stale.Task); windows.Add(other);
                var otherOperation = StartCheck(other); var otherChoice = (ComboBox)Read(other, "providerChoice"); otherChoice.SelectedItem = "Claude"; stale.SetResult(true);
                Check(result, "A stale Codex result cannot be displayed as a Claude login", () => Assert(otherOperation.IsCompleted && !otherOperation.IsFaulted && Message(other).StartsWith("Claude ", StringComparison.Ordinal) && Message(other).Contains("아직 확인하지")));
                Check(result, "Rejected provider never constructs a usable account panel", () => {
                    try { var invalid = Create(type, host, "Other --args", paths, login, checker); invalid.Close(); throw new InvalidOperationException("Unsupported provider accepted."); }
                    catch (TargetInvocationException error) { Assert(error.GetBaseException() is ArgumentException); }
                });
                Check(result, "Nested login resolves its settings button window before the IDE source window", () => {
                    var settingsButton = new Button(); var settingsWindow = new Window { Content = settingsButton };
                    var sourceWindow = new Window { Content = host };
                    try
                    { var resolve = host.GetType().GetMethod("ResolveAccountLoginOwner", BindingFlags.NonPublic | BindingFlags.Static); Assert(ReferenceEquals(resolve.Invoke(null, new object[] { settingsButton, settingsWindow, host }), settingsWindow)); }
                    finally { sourceWindow.Content = null; settingsWindow.Content = null; sourceWindow.Close(); settingsWindow.Close(); }
                });
                Check(result, "Detached menu entry uses active settings owner and falls back to the source window", () => {
                    var settingsWindow = new Window(); var sourceWindow = new Window { Content = host };
                    try
                    {
                        var resolve = host.GetType().GetMethod("ResolveAccountLoginOwner", BindingFlags.NonPublic | BindingFlags.Static);
                        Assert(ReferenceEquals(resolve.Invoke(null, new object[] { new Button(), settingsWindow, host }), settingsWindow));
                        Assert(ReferenceEquals(resolve.Invoke(null, new object[] { new Button(), null, host }), sourceWindow));
                    }
                    finally { sourceWindow.Content = null; sourceWindow.Close(); settingsWindow.Close(); }
                });
            }
            catch (Exception error) { result.Failures.Add("Setup/completion: " + error.GetBaseException().Message); }
            finally { foreach (var window in windows) window.Close(); var disposable = host as IDisposable; if (disposable != null) disposable.Dispose(); }
            return result;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Create
        /// desc         : 실제 로그인 대신 callback을 주입하는 내부 생성자만 사용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 실 계정·CLI 접근 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Window Create(Type type, object host, string provider, IDictionary<string, string> paths, Action<string, string> login, Func<string, string, CancellationToken, Task<bool>> checker)
        { return (Window)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { host, provider, paths, login, checker }, null); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StartCheck
        /// desc         : 클릭에서 사용하는 같은 관찰 가능한 작업을 직접 호출한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 Dispatcher loop 없이 inline fixture로 완료.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Task StartCheck(Window dialog) { return (Task)dialog.GetType().GetMethod("CheckStatusAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dialog, null); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Message
        /// desc         : 공통 안내 영역의 공개된 문구만 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 비밀 원문 미표시 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Message(Window dialog) { return ((TextBlock)Get(dialog, "Message")).Text; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Get
        /// desc         : 제품 공개 표시 속성을 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 설정 변경 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static object Get(object value, string name) { return value.GetType().GetProperty(name).GetValue(value); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Read
        /// desc         : 고정된 계정 화면의 수명·취소 필드를 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 실 운영 상태 조회 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static object Read(object value, string name) { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Descendants
        /// desc         : 자격 증명 입력란의 존재 여부를 논리 트리로 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 창 표시 없이 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        { yield return root; foreach (object child in LogicalTreeHelper.GetChildren(root)) { var item = child as DependencyObject; if (item != null) foreach (var descendant in Descendants(item)) yield return descendant; } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Assert
        /// desc         : 인증 화면 계약 위반을 실패로 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 실제 결과 기록.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Assert(bool condition) { if (!condition) throw new InvalidOperationException("Account login contract assertion failed."); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : 개별 검사 결과를 수집한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 실패 항목을 성공으로 바꾸지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Check(AccountLoginResult result, string name, Action action)
        { try { action(); result.Passed.Add(name); } catch (Exception error) { result.Failures.Add(name + ": " + error.GetBaseException().Message); } }
    }
}
