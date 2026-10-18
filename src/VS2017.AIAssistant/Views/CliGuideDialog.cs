using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using VS2017.AIAssistant.Services;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CliGuideDialog
    /// desc         : 동일한 검정 안내 UI에서 로컬 PDF·텍스트 설명서와 별도 승인 설치 동작을 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 설치 안내 WebView2와 누락·런타임 실패 대체 경로 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class CliGuideDialog : AssistantDialog
    {
        private readonly AssistantControl source;
        private readonly CliGuideService service;
        private readonly Grid viewerHost;
        private readonly TextBox markdown;
        private readonly Button installer;
        private WebView2 browser;
        private bool closed, loading, pdfLoaded;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CliGuideDialog
        /// desc         : 문서는 읽기만 하고 설치는 확인 버튼을 누른 뒤에만 별도 실행하도록 화면을 구성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 고정 설명·PDF 기본 앱·텍스트 대체·설치 동작을 일관된 하단에 배치.
        ///                2026-10-18 CodexCode : 기반 생성자의 WPF 검사와 조건부 VS COM 조회 계약을 명시.
        /// </summary>
        /// ---------------------------------------------------------------------
        public CliGuideDialog(AssistantControl source, CliGuideService service = null)
#pragma warning disable VSTHRD010 // base는 WPF Dispatcher를 검사하며 실제 VS 주 스레드인 분기에만 COM 소유자를 조회한다.
            : base(source, "▤ CLI 설치 및 시작 안내", "기존 PDF 설명서를 읽고, 필요한 경우 확인 후 설치 창을 여세요. 안내를 보는 것만으로 설치되지는 않습니다.")
#pragma warning restore VSTHRD010
        {
            this.source = source; this.service = service ?? new CliGuideService();
            Width = Math.Min(980, MaxWidth); Height = Math.Min(790, MaxHeight);
            viewerHost = new Grid { Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)) };
            markdown = new TextBox { IsReadOnly = true, Text = this.service.ReadMarkdownFallback(), TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), Foreground = Brushes.Gainsboro, BorderThickness = new Thickness(0), Padding = new Thickness(12), FontSize = 14 };
            viewerHost.Children.Add(markdown); Body.Content = viewerHost;
            FooterContent.Content = new TextBlock { Text = CliGuideService.Overview, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Silver, FontSize = 12, Margin = new Thickness(0, 0, 0, 10) };
            var retry = ActionButton("↻ PDF 보기", "로컬 PDF를 WebView로 다시 표시"); retry.Click += RetryClicked;
            var text = ActionButton("¶ 텍스트 안내", "WebView 없이 Markdown 원문 읽기"); text.Click += TextClicked;
            var external = ActionButton("↗ 기본 PDF 앱", "사용자 기본 PDF 앱으로 설명서만 열기"); external.Click += ExternalPdfClicked;
            installer = ActionButton("⇩ CLI 설치", "설치 내용을 확인한 뒤 원본 배치 실행"); installer.Style = TryFindResource("PrimaryButton") as Style; installer.Click += InstallClicked;
            Actions.Children.Insert(0, retry); Actions.Children.Insert(1, text); Actions.Children.Insert(2, external); Actions.Children.Insert(3, installer);
            this.service.InstallationChanged += InstallationChanged;
            Loaded += GuideLoaded; Closed += GuideClosed;
            Message.Text = "PDF를 준비합니다. WebView2가 없으면 같은 내용의 텍스트 안내를 볼 수 있습니다.";
            UpdateInstallationState();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActionButton
        /// desc         : 기존 안내창과 같은 중앙 정렬·아이콘·도움말을 가진 동작 버튼을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 좁은 화면에서는 공통 WrapPanel에서 줄바꿈.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Button ActionButton(string text, string tip)
        { return new Button { Content = text, ToolTip = tip, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(10, 6, 10, 6) }; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GuideLoaded
        /// desc         : 실제 표시된 뒤에만 WebView2를 준비하고 오류는 오프라인 텍스트로 처리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 생성자·headless 검사에서 브라우저나 설치를 실행하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100", Justification = "WPF Loaded event; the awaited browser initialization catches its failures.")]
        private async void GuideLoaded(object sender, RoutedEventArgs args) { await ShowPdfAsync(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RetryClicked
        /// desc         : 사용자가 선택한 경우 로컬 PDF 보기를 다시 시도한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 설치 확인 동작과 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100", Justification = "WPF button event; browser initialization handles expected errors.")]
        private async void RetryClicked(object sender, RoutedEventArgs args) { await ShowPdfAsync(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShowPdfAsync
        /// desc         : 전용 프로필과 제한된 로컬 PDF로 WebView2를 시작하고 닫힌 창에는 게시하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 기존 브라우저 로그인 프로필을 재사용하지 않고 외부 문서 탐색·다운로드·권한 요청을 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async System.Threading.Tasks.Task ShowPdfAsync()
        {
            RequireUiThread(); if (closed || loading) return; loading = true;
            try
            {
                string pdf = service.GetPdfPath(); DisposeBrowser();
                var view = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.FromArgb(30, 30, 30) };
                browser = view; pdfLoaded = false; viewerHost.Children.Add(view);
                var environment = await CoreWebView2Environment.CreateAsync(null, service.GetBrowserDataDirectory(), new CoreWebView2EnvironmentOptions { Language = "ko-KR" });
                RequireUiThread(); if (closed || !ReferenceEquals(browser, view)) return;
                await view.EnsureCoreWebView2Async(environment);
                RequireUiThread(); if (closed || !ReferenceEquals(browser, view)) return;
                var core = view.CoreWebView2;
                core.Settings.AreDevToolsEnabled = false; core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreDefaultScriptDialogsEnabled = false; core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsWebMessageEnabled = false; core.Settings.AreHostObjectsAllowed = false;
                core.Settings.IsBuiltInErrorPageEnabled = false; core.Settings.IsScriptEnabled = false;
                core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
                core.NavigationStarting += NavigationStarting; core.FrameNavigationStarting += FrameNavigationStarting;
                core.NavigationCompleted += NavigationCompleted; core.NewWindowRequested += NewWindowRequested;
                core.DownloadStarting += DownloadStarting; core.PermissionRequested += PermissionRequested;
                core.LaunchingExternalUriScheme += LaunchingExternalUriScheme; core.ProcessFailed += BrowserProcessFailed;
                core.AddWebResourceRequestedFilter("http://*", CoreWebView2WebResourceContext.All);
                core.AddWebResourceRequestedFilter("https://*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += NetworkRequested;
                markdown.Visibility = Visibility.Collapsed;
                Message.Text = "동봉된 로컬 PDF 설명서입니다. 외부 링크는 이 안내창에서 열지 않습니다.";
                core.Navigate(new Uri(pdf).AbsoluteUri);
            }
            catch (Exception) { if (!closed) ShowFallback("동봉 PDF가 없거나 PDF 화면을 준비하지 못했습니다. WebView2 Runtime이 없거나 초기화할 수 없는 경우에는 텍스트 안내 또는 기본 PDF 앱을 이용하세요."); }
            finally { loading = false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigationStarting
        /// desc         : 검증된 PDF 이외의 최상위 탐색을 취소한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 외부 주소를 기본 브라우저로 자동 전달하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs args) { if (!service.IsAllowedNavigation(args.Uri)) { args.Cancel = true; Message.Text = "외부 링크는 안내 화면에서 열지 않습니다. 필요한 공식 사이트는 설명서를 확인하세요."; } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FrameNavigationStarting
        /// desc         : PDF 내부 프레임도 원격 주소로 이동하지 못하게 한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 빈 내부 프레임 외 임의 탐색은 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FrameNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs args) { if (args.Uri != "about:blank" && !service.IsAllowedNavigation(args.Uri)) args.Cancel = true; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigationCompleted
        /// desc         : 최초 PDF 읽기 실패를 404 화면 대신 읽을 수 있는 대체 안내로 바꾼다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 이미 표시한 PDF의 차단 링크 때문에 본문을 제거하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs args) { if (closed) return; if (args.IsSuccess) pdfLoaded = true; else if (!pdfLoaded) ShowFallback("PDF를 읽지 못했습니다. 아래 텍스트 안내 또는 기본 PDF 앱을 이용하세요."); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NewWindowRequested
        /// desc         : PDF 링크에서 별도 웹 창이나 설치 동작을 만들지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 명시적 기본 PDF 앱 버튼과 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs args) { args.Handled = true; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DownloadStarting
        /// desc         : 안내 문서에서 시작하는 다운로드를 취소한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 설치 다운로드는 사용자 승인한 기존 배치만 담당.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DownloadStarting(object sender, CoreWebView2DownloadStartingEventArgs args) { args.Cancel = true; args.Handled = true; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PermissionRequested
        /// desc         : 로컬 안내에서 카메라·마이크·알림 등의 권한을 허용하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 기존 브라우저 권한과 독립.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs args) { args.State = CoreWebView2PermissionState.Deny; args.Handled = true; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LaunchingExternalUriScheme
        /// desc         : PDF에서 외부 프로토콜 앱을 시작하지 못하게 한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 명령·파일 연결 자동 실행 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void LaunchingExternalUriScheme(object sender, CoreWebView2LaunchingExternalUriSchemeEventArgs args) { args.Cancel = true; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NetworkRequested
        /// desc         : 문서 콘텐츠의 http·https 하위 요청도 로컬 차단 응답으로 처리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 웹 로그인·문서 업로드·원격 그림 요청 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void NetworkRequested(object sender, CoreWebView2WebResourceRequestedEventArgs args) { var core = sender as CoreWebView2; if (core != null) args.Response = core.Environment.CreateWebResourceResponse(Stream.Null, 403, "Blocked", "Content-Type: text/plain"); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BrowserProcessFailed
        /// desc         : 브라우저 프로세스 오류에도 오프라인 설명을 계속 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 내부 원문 오류나 사용자 경로를 UI에 복사하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void BrowserProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs args) { if (!closed) ShowFallback("PDF 화면이 종료되어 텍스트 안내로 전환했습니다. 기본 PDF 앱으로도 설명서를 볼 수 있습니다."); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TextClicked
        /// desc         : 사용자가 선택한 경우 오프라인 Markdown 원문으로 전환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : HTML·스크립트 해석 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void TextClicked(object sender, RoutedEventArgs args) { ShowFallback("WebView 없이 동봉된 텍스트 설명서를 표시합니다."); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShowFallback
        /// desc         : 브라우저 수명을 해제하고 선택 가능한 텍스트 안내를 같은 영역에 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 문서 누락도 기본 절차로 안내.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ShowFallback(string message) { RequireUiThread(); DisposeBrowser(); markdown.Text = service.ReadMarkdownFallback(); markdown.Visibility = Visibility.Visible; Message.Text = message; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ExternalPdfClicked
        /// desc         : 버튼 선택 시에만 검증된 PDF를 기본 앱으로 열고 오류는 일관된 안내 영역에 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 임의 URL이나 파일 열기 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ExternalPdfClicked(object sender, RoutedEventArgs args) { try { service.OpenPdfExternally(); Message.Text = "기본 PDF 앱에 동봉된 설명서 열기를 요청했습니다."; } catch (Exception) { ShowFallback("기본 PDF 앱으로 설명서를 열지 못했습니다. 아래 텍스트 안내를 이용하세요."); } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InstallClicked
        /// desc         : 같은 안내 UI의 설치·취소 확인을 거친 경우에만 배치 실행 경계로 진행한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 중복 실행·실제 시작·취소를 각각 안내하고 성공을 추정하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InstallClicked(object sender, RoutedEventArgs args)
        {
            RequireUiThread();
            if (service.IsInstallationRunning) { Message.Text = "설치 창이 이미 실행 중입니다. 열린 설치 창의 결과를 확인하세요."; return; }
            var confirm = new InstallationConfirmationDialog(source) { Owner = this };
            bool approved = confirm.ShowDialog() == true;
            try
            {
                var status = service.LaunchInstaller(approved);
                Message.Text = status == CliInstallerLaunchStatus.Cancelled ? "설치를 취소했습니다. 안내는 계속 볼 수 있습니다." : status == CliInstallerLaunchStatus.AlreadyRunning ? "설치 창이 이미 실행 중입니다. 기존 창을 확인하세요." : "일반 사용자 권한으로 설치 창을 열었습니다. 설치 성공이나 계정 로그인 완료를 뜻하지 않습니다. 설치 창의 SUCCESS를 확인한 뒤 Visual Studio를 다시 열어 주세요.";
                UpdateInstallationState();
            }
            catch (Exception) { Message.Text = "설치 배치를 열지 못했거나 원본 검증을 통과하지 못했습니다. 실행 여부를 확인하고 확장을 다시 설치하거나 PDF의 수동 설치 안내를 이용하세요."; UpdateInstallationState(); }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InstallationChanged
        /// desc         : 실제 설치 프로세스 종료를 해당 안내 UI에만 게시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 닫힌 창에는 상태를 게시하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InstallationChanged(object sender, EventArgs args)
        {
            if (closed) return;
            if (Dispatcher.CheckAccess()) UpdateInstallationState();
            else
            {
#pragma warning disable VSTHRD001 // WPF 표시만 비동기로 예약하며 COM 호출이나 동기 대기를 수행하지 않는다.
                var posted = Dispatcher.InvokeAsync(() => { RequireUiThread(); if (!closed) UpdateInstallationState(); });
#pragma warning restore VSTHRD001
            }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateInstallationState
        /// desc         : 설치 중 재실행을 막고 실제 종료 코드가 있으면 결과 확인을 안내한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 창 종료와 로그인 완료를 혼동하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateInstallationState() { RequireUiThread(); installer.IsEnabled = !service.IsInstallationRunning; if (!service.IsInstallationRunning && service.LastExitCode.HasValue) Message.Text = "설치 창이 종료되었습니다 (종료 코드 " + service.LastExitCode.Value + "). 설치 창의 결과·SUCCESS를 확인한 뒤 별도 로그인과 확장 연결 확인을 진행하세요."; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GuideClosed
        /// desc         : 자식 WebView와 UI 이벤트를 해제하고 별도 설치 프로세스를 강제 종료하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 초기화 await가 끝나도 닫힌 창은 재생성하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void GuideClosed(object sender, EventArgs args) { closed = true; Loaded -= GuideLoaded; service.InstallationChanged -= InstallationChanged; DisposeBrowser(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeBrowser
        /// desc         : 이전 WebView의 탐색·프로세스·권한 이벤트와 네이티브 수명을 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 재시도·텍스트 전환·닫기에서 같은 정리 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeBrowser()
        {
            var view = browser; browser = null; if (view == null) return;
            try
            {
                var core = view.CoreWebView2;
                if (core != null) { core.NavigationStarting -= NavigationStarting; core.FrameNavigationStarting -= FrameNavigationStarting; core.NavigationCompleted -= NavigationCompleted; core.NewWindowRequested -= NewWindowRequested; core.DownloadStarting -= DownloadStarting; core.PermissionRequested -= PermissionRequested; core.LaunchingExternalUriScheme -= LaunchingExternalUriScheme; core.ProcessFailed -= BrowserProcessFailed; core.WebResourceRequested -= NetworkRequested; }
            }
            finally { viewerHost.Children.Remove(view); view.Dispose(); }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequireUiThread
        /// desc         : 안내 UI와 브라우저 수명을 소유 WPF 스레드에서만 변경한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : COM 문서·사용자 계정 조회 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RequireUiThread() { if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("CLI 안내창은 UI 스레드에서 사용하세요."); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InstallationConfirmationDialog
        /// desc         : 일반 메시지 박스 대신 같은 검정 안내창에 설치·취소 선택을 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 기본 선택은 취소이고 승인 버튼만 실제 승인 결과를 반환.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class InstallationConfirmationDialog : AssistantDialog
        {
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : InstallationConfirmationDialog
            /// desc         : 사용자 계정·네트워크·PATH 변경 설명 뒤 승인 여부를 명시적으로 묻는다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 표시만으로 설치를 호출하지 않음.
            ///                2026-10-18 CodexCode : 기반 생성자의 WPF 검사와 조건부 VS COM 조회 계약을 명시.
            /// </summary>
            /// ---------------------------------------------------------------------
            internal InstallationConfirmationDialog(AssistantControl source)
#pragma warning disable VSTHRD010 // base의 WPF 스레드 검사와 조건부 VS COM guard는 그대로 유지한다.
                : base(source, "⇩ CLI를 설치할까요?", "아래 내용을 확인하고 설치 또는 취소를 선택하세요.")
#pragma warning restore VSTHRD010
            {
                Width = Math.Min(560, MaxWidth); Height = Math.Min(460, MaxHeight);
                Body.Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new TextBlock { Text = CliGuideService.Confirmation, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gainsboro } };
                var cancel = ActionButton("× 취소", "설치를 시작하지 않고 안내로 돌아가기"); cancel.IsCancel = true; cancel.IsDefault = true; cancel.Click += (sender, args) => { DialogResult = false; };
                var install = ActionButton("⇩ 설치", "일반 사용자 권한으로 원본 설치 배치 실행"); install.Style = TryFindResource("PrimaryButton") as Style; install.Click += (sender, args) => { DialogResult = true; };
                Actions.Children.Clear(); Actions.Children.Add(cancel); Actions.Children.Add(install);
            }
        }
    }
}
