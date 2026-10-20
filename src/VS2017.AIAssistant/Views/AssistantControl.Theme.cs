using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using VS2017.AIAssistant.Chat;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : IDE 우선 자동 테마와 개인 밝게·어둡게 선택을 저장 거래 및 창 수명에 연결한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : Role 추가15의 VS 우선·Windows 보조·명시적 화면 설정 구현.
    ///                2026-10-19 CodexOrig4 : 인계 후 공통 정책·SDK 대체 조회·갱신 병합과 표시 모드 관찰 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private string themeMode = "auto";
        private string visualThemePreset = "vs";
        private bool themeInitialized, themeSynchronizing, themeEventsAttached, ideThemeEventsAttached;
        private bool themePresetSynchronizing;
        private int themeRefreshQueued;
        private Dispatcher themeDispatcher;
        private Action themeRefreshCallback;
        private bool themePaletteApplied, appliedHighContrast;
        private Color? appliedHostBackground, appliedHostForeground;
        internal string AppliedThemeMode { get; private set; }
        internal string AppliedVisualThemePreset { get; private set; }

        private bool GetCompletionNotificationsEnabled()
        { Dispatcher.VerifyAccess(); return CompletionNotificationsChoice.IsChecked == true; }

        private void RestoreCompletionNotifications(bool enabled)
        { Dispatcher.VerifyAccess(); CompletionNotificationsChoice.IsChecked = enabled; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeTheme
        /// desc         : 저장한 모드를 초기화하고 표시 중에만 운영체제·IDE 색 변경 알림을 구독한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 이전 설정에 필드가 없으면 자동 모드로 호환한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InitializeTheme(AssistantSettings settings)
        {
            Dispatcher.VerifyAccess();
            themeDispatcher = Dispatcher;
            themeRefreshCallback = RefreshThemeOnDispatcher;
            themeInitialized = true;
            RestoreVisualThemePreset(settings.VisualThemePreset);
            RestoreThemeMode(settings.ThemeMode);
            InitializeIdeThemeInstallation();
            Loaded += AssistantThemeLoaded; Unloaded += AssistantThemeUnloaded;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RestoreThemeMode
        /// desc         : 초기 로드와 설정 취소에서 선택값·현재 미리보기 팔레트를 함께 복원한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 취소한 테마를 설정 JSON에 저장하거나 다음 열기에 남기지 않는다.
        ///                2026-10-19 CodexOrig4 : 이전 초안의 소문자 계약 유지 및 공통 모드 정규화 적용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RestoreThemeMode(string value)
        {
            Dispatcher.VerifyAccess();
            themeMode = AssistantThemePolicy.NormalizeMode(value);
            themeSynchronizing = true;
            try { ThemeModeChoice.SelectedValue = themeMode; }
            finally { themeSynchronizing = false; }
            RefreshAssistantTheme();
        }

        private static string NormalizeVisualThemePreset(string value)
        {
            if (String.Equals(value, "kakao", StringComparison.OrdinalIgnoreCase)) return "kakao";
            if (String.Equals(value, "dracula", StringComparison.OrdinalIgnoreCase)) return "dracula";
            return "vs";
        }

        private void RestoreVisualThemePreset(string value)
        {
            Dispatcher.VerifyAccess();
            visualThemePreset = NormalizeVisualThemePreset(value);
            themePresetSynchronizing = true;
            try { VisualThemePresetChoice.SelectedValue = visualThemePreset; }
            finally { themePresetSynchronizing = false; }
            RefreshAssistantTheme();
        }

        private void VisualThemePresetChanged(object sender, SelectionChangedEventArgs args)
        {
            Dispatcher.VerifyAccess();
            if (!themeInitialized || themePresetSynchronizing || disposed) return;
            var item = args.AddedItems.Count > 0 ? args.AddedItems[0] as ComboBoxItem : VisualThemePresetChoice.SelectedItem as ComboBoxItem;
            var selected = item == null ? null : item.Tag as string;
            if (selected == "vs" || selected == "kakao" || selected == "dracula")
            { visualThemePreset = selected; RefreshAssistantTheme(); }
        }

        /// <summary>Called by the settings transaction after its persisted settings write succeeds.</summary>
        private void CommitVisualThemePreset()
        { Dispatcher.VerifyAccess(); visualThemePreset = NormalizeVisualThemePreset(visualThemePreset); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ThemeModeChanged
        /// desc         : 화면 설정에서 고른 모드를 즉시 미리보고 저장은 기존 명시적 버튼에 맡긴다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 생성 중 SelectionChanged와 종료 뒤 이벤트 무시.
        ///                2026-10-19 CodexOrig4 : SelectionChanged 시 아직 이전 값인 SelectedValue 대신 새 항목의 Tag 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ThemeModeChanged(object sender, SelectionChangedEventArgs args)
        {
            Dispatcher.VerifyAccess();
            if (!themeInitialized || themeSynchronizing || disposed) return;
            var item = args.AddedItems.Count > 0 ? args.AddedItems[0] as ComboBoxItem : ThemeModeChoice.SelectedItem as ComboBoxItem;
            string selected = item == null ? null : item.Tag as string;
            if (selected == "auto" || selected == "light" || selected == "dark") { themeMode = selected; RefreshAssistantTheme(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshAssistantTheme
        /// desc         : 실제 IDE 배경과 글자색을 우선 읽고 호스트가 없을 때 Windows 개인 모드를 따른다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 전역 리소스·시스템 설정 변경 없이 해당 확장 사전만 갱신.
        ///                2026-10-19 CodexOrig4 : 실제 IDE SDK 대체 조회 및 단일 우선순위 정책으로 통일.
        ///                2026-10-20 CodexOrig : TASK069 재표시에서 같은 팔레트의 자원 재할당·문서 재생성을 생략.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshAssistantTheme()
        {
            if (disposed || !themeInitialized) return;
            Dispatcher.VerifyAccess();
            Color? hostBackground = null, hostForeground = null;
            if (themeMode == "auto" && Application.Current != null)
            {
                var background = Application.Current.TryFindResource(VsBrushes.ToolWindowBackgroundKey) as SolidColorBrush;
                var foreground = Application.Current.TryFindResource(VsBrushes.ToolWindowTextKey) as SolidColorBrush;
                if (background != null && background.Color.A == 255)
                { hostBackground = background.Color; if (foreground != null && foreground.Color.A == 255) hostForeground = foreground.Color; }
            }
            if (themeMode == "auto" && !hostBackground.HasValue && IsVisualStudioHost())
            {
                var background = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
                var foreground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
                if (background.A == 255)
                {
                    hostBackground = Color.FromArgb(background.A, background.R, background.G, background.B);
                    if (foreground.A == 255) hostForeground = Color.FromArgb(foreground.A, foreground.R, foreground.G, foreground.B);
                }
            }
            string resolved = AssistantThemePolicy.Resolve(themeMode, hostBackground, themeMode == "auto" && !hostBackground.HasValue && WindowsUsesLightMode());
            bool highContrast = SystemParameters.HighContrast;
            string preset = highContrast ? "vs" : visualThemePreset;
            if (themePaletteApplied && AppliedThemeMode == resolved && AppliedVisualThemePreset == preset &&
                appliedHighContrast == highContrast && appliedHostBackground == hostBackground && appliedHostForeground == hostForeground &&
                Resources.Contains("AssistantBackgroundBrush") && Resources.Contains("AssistantForegroundBrush")) return;
            AppliedThemeMode = resolved;
            bool light = resolved == "light";
            AssistantPalette.Apply(Resources, light, hostBackground, hostForeground);
            AppliedVisualThemePreset = preset;
            if (!highContrast) AssistantPalette.ApplyPreset(Resources, visualThemePreset, light);
            Resources[VsBrushes.ToolWindowBackgroundKey] = Resources["AssistantBackgroundBrush"];
            Resources[VsBrushes.ToolWindowTextKey] = Resources["AssistantForegroundBrush"];
            SetResourceReference(BackgroundProperty, "AssistantBackgroundBrush");
            SetResourceReference(ForegroundProperty, "AssistantForegroundBrush");
            appliedHostBackground = hostBackground; appliedHostForeground = hostForeground;
            appliedHighContrast = highContrast; themePaletteApplied = true;
            if (thinkingAnimationActive) { UpdateThinkingAnimation(false); UpdateThinkingAnimation(true); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WindowsUsesLightMode
        /// desc         : IDE가 없는 화면의 보조 모드를 읽기 전용으로 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 지원되지 않거나 접근할 수 없는 개인 설정은 어두운 기본값 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool WindowsUsesLightMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                { object value = key == null ? null : key.GetValue("AppsUseLightTheme"); return value is int && (int)value != 0; }
            }
            catch (Exception error) when (error is UnauthorizedAccessException || error is System.Security.SecurityException || error is System.IO.IOException)
            { return false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantThemeLoaded
        /// desc         : 실제 VS 프로세스에서만 SDK 테마 알림을 구독하고 단독 WPF는 독립 실행을 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 같은 컨트롤 재표시에서 중복 static 구독 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AssistantThemeLoaded(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess();
            if (disposed || themeEventsAttached) return;
            SystemEvents.UserPreferenceChanged += AssistantSystemThemeChanged; themeEventsAttached = true;
            if (IsVisualStudioHost())
                { VSColorTheme.ThemeChanged += AssistantIdeThemeChanged; ideThemeEventsAttached = true; }
            RefreshAssistantTheme();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantThemeUnloaded
        /// desc         : 비표시 컨트롤을 운영체제와 SDK static 이벤트가 붙잡지 않게 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 실제 해제와 재표시를 모두 지원.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AssistantThemeUnloaded(object sender, RoutedEventArgs args)
        {
            if (themeEventsAttached) { SystemEvents.UserPreferenceChanged -= AssistantSystemThemeChanged; themeEventsAttached = false; }
            if (ideThemeEventsAttached) { VSColorTheme.ThemeChanged -= AssistantIdeThemeChanged; ideThemeEventsAttached = false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantSystemThemeChanged
        /// desc         : 시스템 알림을 소유 Dispatcher에 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 종료한 Dispatcher 또는 해제된 컨트롤 게시 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AssistantSystemThemeChanged(object sender, UserPreferenceChangedEventArgs args) { QueueThemeRefresh(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantIdeThemeChanged
        /// desc         : IDE 테마 변경 뒤 실제 공유 토큰을 다시 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 명시적 Light·Dark 모드는 IDE 알림에도 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AssistantIdeThemeChanged(ThemeChangedEventArgs args) { QueueThemeRefresh(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : QueueThemeRefresh
        /// desc         : 테마 알림의 스레드와 화면 수명 경계를 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 실제 변경을 WPF 스레드에서 한 번 수행하도록 예약.
        ///                2026-10-19 CodexOrig4 : 연속 시스템 알림은 병합하고 명시 모드 및 종료 Dispatcher는 게시하지 않음.
        ///                2026-10-19 CodexOrig4 : 외부 스레드는 초기 캡처한 Dispatcher/콜백만 사용하고 콜백에서 UI 접근을 검증.
        ///                2026-10-19 CodexOrig : TASK-025 SDK17에서 게시 결과를 관찰하고 취소된 게시의 대기 상태 복원.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void QueueThemeRefresh()
        {
            var scheduler = themeDispatcher;
            if (disposed || !themeEventsAttached || themeMode != "auto" || scheduler == null || scheduler.HasShutdownStarted) return;
            if (System.Threading.Interlocked.Exchange(ref themeRefreshQueued, 1) != 0) return;
            // 운영체제 알림 스레드는 기다리지 않는다. 독립 WPF에서도 동작하도록 VS JTF 없이 소유 Dispatcher로 게시한다.
#pragma warning disable VSTHRD001 // Nonblocking WPF dispatcher post with explicit priority; callback verifies owner UI access.
            var operation = scheduler.BeginInvoke(DispatcherPriority.Background, themeRefreshCallback);
#pragma warning restore VSTHRD001
            if (operation.Status == DispatcherOperationStatus.Aborted)
                System.Threading.Interlocked.Exchange(ref themeRefreshQueued, 0);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshThemeOnDispatcher
        /// desc         : 외부 알림에서 예약한 색상 갱신을 소유 WPF 스레드에서만 실행한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : SDK 스레드 분석과 독립 WPF 호스트 양쪽에서 같은 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshThemeOnDispatcher()
        {
            Dispatcher.VerifyAccess();
            System.Threading.Interlocked.Exchange(ref themeRefreshQueued, 0);
            if (!disposed && themeEventsAttached) RefreshAssistantTheme();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsVisualStudioHost
        /// desc         : SDK 서비스가 없는 독립 WPF 검사에서 VS 전용 네이티브 초기화를 피한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 실제 IDE 프로세스만 SDK 대체 조회 및 알림을 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsVisualStudioHost()
        {
            using (var process = Process.GetCurrentProcess())
                return string.Equals(process.ProcessName, "devenv", StringComparison.OrdinalIgnoreCase);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeTheme
        /// desc         : IDE 종료에서도 테마 이벤트와 화면 수명 구독을 빠짐없이 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 여러 번 종료해도 static 핸들러가 남지 않게 한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeTheme()
        { AssistantThemeUnloaded(this, new RoutedEventArgs()); Loaded -= AssistantThemeLoaded; Unloaded -= AssistantThemeUnloaded; themeInitialized = false; }
    }
}
