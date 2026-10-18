using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using VS2017.AIAssistant.Providers;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 계정 로그인 전용 창을 열고 기존 CLI 인증 경계를 재사용한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 Role14 공식 인증 지원 조사 후 별도 로그인 진입점 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private AccountLoginDialog activeAccountLoginDialog;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AccountLoginClicked
        /// desc         : 현재 AI별 실행 경로 사본을 고정한 뒤 별도 로그인 창만 연다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 열기만으로 인증·상태 프로세스를 실행하지 않음.
        ///                2026-10-19 CodexCode : 설정 모달에서 연 경우 실제 호출 창을 소유자로 지정하여 중첩 입력 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AccountLoginClicked(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess(); if (busy || disposed) return;
            if (activeAccountLoginDialog != null) { activeAccountLoginDialog.Activate(); return; }
            var paths = new Dictionary<string, string>(executablePaths, StringComparer.Ordinal);
            paths[selectedProvider] = ExecutablePath.Text;
            var dialog = new AccountLoginDialog(this, selectedProvider, paths);
            var owner = ResolveAccountLoginOwner(sender, activeSettingsDialog, this);
            if (owner != null) dialog.Owner = owner;
            dialog.ConnectionChecked += (providerName, executable, connected) => {
                if (disposed || selectedProvider != providerName || !String.Equals(ExecutablePath.Text, executable, StringComparison.Ordinal)) return;
                AccountLabel.Text = connected ? "로그인 정보가 있습니다 · 실제 모델 이용 가능 여부는 요청 결과를 확인하세요." : "로그인 정보가 없습니다. 공식 로그인 후 다시 확인하세요.";
            };
            activeAccountLoginDialog = dialog;
            try { dialog.ShowDialog(); }
            finally { if (ReferenceEquals(activeAccountLoginDialog, dialog)) activeAccountLoginDialog = null; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResolveAccountLoginOwner
        /// desc         : 호출 버튼의 실제 창·현재 설정 창·도구 창 순서로 로그인 소유자를 찾는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 설정 모달이 이미 비활성화한 IDE를 중첩 로그인 소유자로 재사용하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Window ResolveAccountLoginOwner(object sender, Window settingsDialog, FrameworkElement source)
        {
            var origin = sender as DependencyObject;
            var window = origin == null ? null : Window.GetWindow(origin);
            return window ?? settingsDialog ?? (source == null ? null : Window.GetWindow(source));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseAccountLoginDialog
        /// desc         : 도구 창 종료 시 계정 확인 작업을 취소하고 소유 로그인 창을 닫는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 이미 사용자가 연 공식 인증 창은 강제 종료하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CloseAccountLoginDialog()
        {
            Dispatcher.VerifyAccess(); var dialog = activeAccountLoginDialog; activeAccountLoginDialog = null;
            if (dialog != null) dialog.Close();
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AccountLoginDialog
    /// desc         : 제공자별 공식 CLI 로그인과 유한한 상태 확인을 별도 화면에서 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 암호·토큰 수집 없이 브라우저 인증을 사용자가 진행.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class AccountLoginDialog : AssistantDialog
    {
        private readonly Dictionary<string, string> executableSnapshots = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Action<string, string> beginLogin;
        private readonly Func<string, string, CancellationToken, Task<bool>> checkStatus;
        private readonly ComboBox providerChoice;
        private readonly TextBlock executableLabel;
        private readonly MarkdownPreview instructions;
        private readonly Button loginButton, checkButton, cancelCheckButton;
        private CancellationTokenSource statusCancellation;
        private int statusGeneration;
        private bool closed, loginWindowOpened;
        internal event Action<string, string, bool> ConnectionChecked;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AccountLoginDialog
        /// desc         : 실제 인증 서비스는 명시적 버튼 동작에서만 호출하도록 준비한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 설치된 CLI의 인증 저장소와 조직 정책을 그대로 사용.
        ///                2026-10-19 CodexOrig : TASK-025 Dispatcher를 검증하는 대상 생성자로의 위임 계약을 SDK17에 명시.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD010", Justification = "Delegates directly to the constructor that verifies its WPF Dispatcher; VS services remain guarded separately so standalone STA fixtures do not require a VS host.")]
        public AccountLoginDialog(AssistantControl source, string initialProvider, IDictionary<string, string> executablePaths)
            : this(source, initialProvider, executablePaths, null, null) { }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AccountLoginDialog
        /// desc         : 검사용 인증 callback도 같은 제공자·경로 사본·취소 경계를 거치도록 구성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 콜백 주입은 내부 fixture 경계이며 실제 비밀을 취급하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal AccountLoginDialog(AssistantControl source, string initialProvider, IDictionary<string, string> executablePaths,
            Action<string, string> login, Func<string, string, CancellationToken, Task<bool>> checker)
#pragma warning disable VSTHRD010 // 기반 창은 WPF Dispatcher를 검증하고 실제 VS 주 스레드 분기에서만 COM 소유자를 조회한다.
            : base(source, "↗ 계정 로그인", "사용할 AI를 선택하고 공식 로그인 창에서 인증을 마친 뒤 상태를 확인하세요.")
#pragma warning restore VSTHRD010
        {
            RequireProvider(initialProvider);
            foreach (string provider in new[] { "Codex", "Claude" })
            { string path; executableSnapshots[provider] = executablePaths != null && executablePaths.TryGetValue(provider, out path) ? path ?? "" : ""; }
            var service = new CliAccountService(); beginLogin = login ?? service.BeginLogin; checkStatus = checker ?? service.CheckAsync;
            Width = Math.Min(650, MaxWidth); Height = Math.Min(660, MaxHeight);
            var body = new StackPanel();
            var heading = new TextBlock { Text = "로그인할 AI", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
            body.Children.Add(heading);
            providerChoice = new ComboBox { ItemsSource = new[] { "Codex", "Claude" }, SelectedItem = initialProvider, Width = 160, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12) };
            AutomationProperties.SetName(providerChoice, "로그인할 AI 선택"); body.Children.Add(providerChoice);
            executableLabel = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16), FontSize = 12 };
            executableLabel.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush"); body.Children.Add(executableLabel);
            var explanation = new TextBlock { Text = "이 확장은 OpenAI·Claude의 아이디와 비밀번호를 직접 받거나 저장하지 않습니다. 공식 CLI가 연 브라우저에서 본인 계정으로 로그인하세요.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) };
            body.Children.Add(explanation);
            instructions = new MarkdownPreview { UseInternalScrolling = false };
            instructions.LinkRequested += (sender, args) => { Dispatcher.VerifyAccess(); source.NavigateMarkdownLink(args); }; body.Children.Add(instructions);
            Body.Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var note = new TextBlock { Text = "인증 정보는 공식 CLI가 관리합니다. 로그인 창을 여는 것과 인증 성공은 다릅니다. 이 창을 닫아도 이미 연 공식 로그인 창은 남아 있을 수 있습니다.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 10) };
            note.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush"); FooterContent.Content = note;
            loginButton = ActionButton("↗ 공식 로그인 열기", "공식 CLI 로그인 절차를 사용자가 진행합니다."); loginButton.Click += LoginRequested;
            checkButton = ActionButton("✓ 로그인 상태 확인", "공식 CLI가 보고하는 인증 정보 유무만 확인합니다."); checkButton.Click += CheckRequested;
            cancelCheckButton = ActionButton("■ 확인 중지", "이 창의 상태 확인만 취소합니다."); cancelCheckButton.IsEnabled = false; cancelCheckButton.Click += CancelRequested;
            Actions.Children.Insert(0, loginButton); Actions.Children.Insert(1, checkButton); Actions.Children.Insert(2, cancelCheckButton);
            providerChoice.SelectionChanged += ProviderChanged; Closed += DialogClosed; RefreshProvider();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActionButton
        /// desc         : 공통 의미 색상과 가운데 정렬을 따르는 계정 동작 버튼을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 테마 변경 시 기존 공통 리소스 키로 갱신.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Button ActionButton(string label, string tip)
        {
            var button = new Button { Content = label, ToolTip = tip, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 8, 6) };
            button.SetResourceReference(StyleProperty, "GuideActionButton"); return button;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequireProvider
        /// desc         : 고정된 두 제공자 외에는 로그인 인수를 구성하지 못하게 한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 임의 프로그램·인수 입력 경로 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RequireProvider(string provider)
        { if (provider != "Codex" && provider != "Claude") throw new ArgumentException("지원하지 않는 AI입니다.", nameof(provider)); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProviderChanged
        /// desc         : 진행 중인 확인이 없을 때 선택한 AI의 설명과 경로만 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 제공자를 바꾸어도 자동 로그인·상태 조회 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ProviderChanged(object sender, SelectionChangedEventArgs args)
        { if (!closed && statusCancellation == null) RefreshProvider(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshProvider
        /// desc         : 제공자별 공식 인증 절차와 고정 실행 경로를 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 암호 직접 전송 API가 있다는 추정 없이 공식 문서를 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshProvider()
        {
            string provider = providerChoice.SelectedItem as string; RequireProvider(provider); statusGeneration++;
            string path = executableSnapshots[provider]; loginWindowOpened = false;
            executableLabel.Text = "CLI 실행 경로: " + (String.IsNullOrWhiteSpace(path) ? "자동 검색 · 설정의 경로를 사용합니다." : path);
            instructions.Markdown = provider == "Codex"
                ? "## Codex · OpenAI 계정\n\n1. **공식 로그인 열기**에서 `codex login`을 시작합니다.\n2. 공식 브라우저 화면에서 ChatGPT 계정으로 로그인합니다.\n3. 이 창으로 돌아와 **로그인 상태 확인**을 선택합니다.\n\n[OpenAI 공식 인증 안내](https://learn.chatgpt.com/docs/auth)\n\nAPI 키·조직 인증을 쓰는 경우에는 공식 CLI 안내와 조직 정책을 따르세요. 이 화면은 키나 비밀번호를 받지 않습니다."
                : "## Claude · Anthropic 계정\n\n1. **공식 로그인 열기**에서 `claude auth login`을 시작합니다.\n2. 공식 브라우저에서 본인 Claude 계정의 인증을 마칩니다.\n3. 이 창으로 돌아와 **로그인 상태 확인**을 선택합니다.\n\n[Claude 공식 인증 안내](https://code.claude.com/docs/en/authentication)\n\nClaude 구독·Console·조직/클라우드 인증은 서로 다를 수 있습니다. 실제 CLI와 조직에서 허용한 방법을 사용하세요.";
            Message.Text = provider + " 로그인 상태를 아직 확인하지 않았습니다."; SetChecking(false);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LoginRequested
        /// desc         : 사용자 클릭에 한하여 공식 서비스의 고정 로그인 동작을 호출한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 프로세스를 연 사실을 인증 성공으로 간주하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void LoginRequested(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess(); if (closed || statusCancellation != null || loginWindowOpened) return;
            string provider = providerChoice.SelectedItem as string; RequireProvider(provider);
            try
            {
                beginLogin(provider, executableSnapshots[provider]); loginWindowOpened = true; loginButton.IsEnabled = false;
                Message.Text = provider + " 공식 로그인 창 열기를 요청했습니다. 인증을 마친 뒤 로그인 상태 확인을 누르세요.";
            }
            catch (Exception) { Message.Text = "공식 로그인 창을 열지 못했습니다. CLI 설치·실행 경로와 조직 정책을 확인하세요."; }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckRequested
        /// desc         : WPF 클릭에서 관찰 가능한 상태 확인 작업을 시작한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 예상 실패·취소는 작업 내부에서 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100", Justification = "WPF Click event; status check observes cancellation and handles failures without exposing CLI output.")]
        private async void CheckRequested(object sender, RoutedEventArgs args) { await CheckStatusAsync(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckStatusAsync
        /// desc         : AI·경로·세대를 고정하고 해당 창의 유한한 CLI 상태 확인 결과만 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 종료·취소 뒤 게시 금지와 원문 오류 비공개.
        ///                2026-10-19 CodexCode : WPF 소유 UI의 비동기 상태 작업 경계와 await 후 Dispatcher를 명시 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD109", Justification = "WPF-owned UI operation: entered from the dialog Click on its Dispatcher, verified before and after await; does not require a Visual Studio JTF host or switch an arbitrary background caller into the UI.")]
        private async Task CheckStatusAsync()
        {
            Dispatcher.VerifyAccess(); if (closed || statusCancellation != null) return;
            string provider = providerChoice.SelectedItem as string; RequireProvider(provider);
            string executable = executableSnapshots[provider]; int generation = ++statusGeneration;
            var cancellation = new CancellationTokenSource(); statusCancellation = cancellation;
            SetChecking(true); Message.Text = provider + " 로그인 정보 확인 중…";
            try
            {
                bool connected = await checkStatus(provider, executable, cancellation.Token);
                Dispatcher.VerifyAccess();
                if (closed || cancellation.IsCancellationRequested || generation != statusGeneration || !String.Equals(providerChoice.SelectedItem as string, provider, StringComparison.Ordinal)) return;
                Message.Text = connected ? provider + " 로그인 정보가 있습니다. 실제 모델·구독·이용 권한은 요청 결과와 공식 계정을 확인하세요." : provider + " 로그인 정보가 없습니다. 공식 로그인 후 다시 확인하세요.";
                var handler = ConnectionChecked; if (handler != null) handler(provider, executable, connected);
            }
            catch (OperationCanceledException) { if (!closed && generation == statusGeneration) Message.Text = "로그인 상태 확인을 취소했습니다."; }
            catch (Exception) { if (!closed && generation == statusGeneration) Message.Text = "로그인 상태를 확인하지 못했습니다. CLI 버전·경로·네트워크·조직 정책을 확인하세요."; }
            finally
            {
                if (ReferenceEquals(statusCancellation, cancellation)) statusCancellation = null;
                if (!closed && cancellation.IsCancellationRequested && generation != statusGeneration) Message.Text = "로그인 상태 확인을 취소했습니다.";
                cancellation.Dispose(); loginWindowOpened = false;
                if (!closed) { if (!String.Equals(providerChoice.SelectedItem as string, provider, StringComparison.Ordinal)) RefreshProvider(); else SetChecking(false); }
            }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SetChecking
        /// desc         : 확인 중에는 제공자 변경과 중복 인증을 막고 취소 동작만 활성화한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 AI 요청의 공용 busy·취소 토큰을 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SetChecking(bool checking)
        { providerChoice.IsEnabled = !checking; checkButton.IsEnabled = !checking; loginButton.IsEnabled = !checking && !loginWindowOpened; cancelCheckButton.IsEnabled = checking; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CancelRequested
        /// desc         : 이 화면의 상태 확인에만 취소를 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 다른 AI 요청·공식 로그인 창에는 접근하지 않음.
        ///                2026-10-19 CodexCode : 동기 취소 완료가 재진입해도 완료 문구를 진행 중 문구로 덮지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CancelRequested(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess(); var cancellation = statusCancellation; if (cancellation == null || closed) return;
            statusGeneration++; cancelCheckButton.IsEnabled = false; Message.Text = "로그인 상태 확인을 취소하고 있습니다…"; cancellation.Cancel();
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DialogClosed
        /// desc         : 창 수명을 닫고 뒤늦은 결과·이벤트 게시를 차단한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 상태 작업의 CTS는 해당 작업 finally에서 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DialogClosed(object sender, EventArgs args)
        {
            closed = true; statusGeneration++; providerChoice.SelectionChanged -= ProviderChanged; ConnectionChecked = null;
            if (statusCancellation != null) statusCancellation.Cancel();
        }
    }
}
