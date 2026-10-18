#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-OnboardingJourney
desc         : 격리된 실제 WPF 창으로 첫 사용·안내 숨김·수동 재열기와 설정 보존을 독립 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 기존 v0.3 설정에서 시작하는 v0.4 사용자 흐름과 좁은 안내 화면 검증.
-------------------------------------------------------------------------
.SYNOPSIS
    Run with Windows PowerShell 5.1 -STA after receiving a frozen product DLL.
.DESCRIPTION
    Does not build the product, launch Visual Studio, connect an AI account, or read personal settings.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [string]$OutputDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or
    [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw 'Windows PowerShell 5.1 -STA로 실행하세요.'
}
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repositoryDir ('artifacts\task011\journey-' + [guid]::NewGuid().ToString('N')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
foreach ($framework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Web.Extensions')) {
    Add-Type -AssemblyName $framework
}

# SDK 의존성은 프로젝트의 NuGet 캐시에서 읽고 제품/공유 빌드 폴더에는 쓰지 않는다.
$assetGraph = Get-Content -LiteralPath (Join-Path $repositoryDir 'src\VS2017.AIAssistant\obj\project.assets.json') -Raw | ConvertFrom-Json
$cacheRoot = $assetGraph.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$referencePaths = @{}
foreach ($entry in $assetGraph.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) {
    if ($entry.Value.PSObject.Properties.Name -contains 'compile') {
        foreach ($item in $entry.Value.compile.PSObject.Properties.Name) {
            if ($item -like '*.dll') {
                $referencePaths[[IO.Path]::GetFileNameWithoutExtension($item)] = Join-Path $cacheRoot ($assetGraph.libraries.($entry.Name).path + '/' + $item)
            }
        }
    }
}
$resolveHandler = [ResolveEventHandler]{
    param($sender, $eventArgs)
    $name = (New-Object Reflection.AssemblyName($eventArgs.Name)).Name
    if ($referencePaths.ContainsKey($name)) { return [Reflection.Assembly]::LoadFrom($referencePaths[$name]) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolveHandler)
$previousSettingsPath = $env:VS2017_AI_SETTINGS_PATH
try {
    $env:VS2017_AI_SETTINGS_PATH = Join-Path $OutputDirectory 'isolated-settings.json'
    $productAssembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
    $null = [Reflection.Assembly]::LoadFrom($referencePaths['Microsoft.VisualStudio.Shell.15.0'])
    if ($productAssembly.GetName().Version -lt [version]'0.4.0.0') { throw 'v0.4.0 이상의 고정 제품 DLL이 필요합니다.' }
    # 형식별 어셈블리 경로를 명시하여 PowerShell의 기본 참조 목록에 의존하지 않는다.
    $referenceAssemblies = @($AssemblyPath, 'System.dll', 'System.Core.dll', 'System.Web.Extensions.dll',
        [Windows.DependencyObject].Assembly.Location, [Windows.Media.Visual].Assembly.Location,
        [Windows.Window].Assembly.Location, [System.Xaml.XamlReader].Assembly.Location)
    Add-Type -ReferencedAssemblies $referenceAssemblies -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Views;

namespace CodexCode.JourneyReview
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : JourneyResult
    /// desc         : 실제 WPF 사용자 흐름의 독립 검사 결과와 실패 사유를 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : UI 스레드 예외도 JSON 보고서에 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class JourneyResult
    {
        public List<string> Checks = new List<string>();
        public string Error;
        public bool Succeeded { get { return Error == null; } }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : OnboardingJourney
    /// desc         : 합성 설정과 실제 Loaded/모달 창으로 사용 안내·설정 흐름을 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 개인정보·CLI·IDE 없이 UI 상태 전이를 재현.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class OnboardingJourney
    {
        private static JourneyResult result;
        private static string output;
        private static object backgroundKey, foregroundKey;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : 기대한 사용자 결과가 아니면 남은 UI를 정리하도록 오류를 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 검사별 사람이 읽을 수 있는 근거 기록.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Check(bool passed, string description)
        {
            if (!passed) throw new InvalidOperationException(description);
            result.Checks.Add(description);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PreserveSettings
        /// desc         : 사용 안내 선택이 기존 계정 경로·모델·규칙·글자 크기를 덮지 않는지 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 저장된 값과 아직 저장하지 않은 UI 편집을 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void PreserveSettings(AssistantSettings actual, AssistantSettings expected)
        {
            Check(actual.CodexPath == expected.CodexPath && actual.ClaudePath == expected.ClaudePath &&
                actual.CodexModel == expected.CodexModel && actual.ClaudeModel == expected.ClaudeModel &&
                actual.Instructions == expected.Instructions && actual.FontSize == expected.FontSize,
                "Guide choice preserves both CLI paths, both models, saved rules and font size");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FindMessageBox
        /// desc         : 실제 DataTemplate이 생성한 대화 텍스트를 시각 트리에서 찾는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 직접 입력란과 동적 메시지의 우클릭 메뉴를 분리 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static TextBox FindMessageBox(DependencyObject node, string text)
        {
            var box = node as TextBox;
            if (box != null && box.Text == text) return box;
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            {
                var found = FindMessageBox(VisualTreeHelper.GetChild(node, index), text);
                if (found != null) return found;
            }
            return null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureNarrowGuide
        /// desc         : 제품이 허용하는 최소 폭의 실제 안내 창에서 하단 동작이 잘리지 않는지 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 밝은/어두운 테마 PNG와 버튼 가시 영역 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CaptureNarrowGuide(GettingStartedDialog guide, AssistantControl control)
        {
            guide.Width = guide.MinWidth; guide.Height = 600; guide.UpdateLayout();
            var visual = (FrameworkElement)guide.Content;
            foreach (var theme in new[] { "Light", "Dark" })
            {
                control.Resources[backgroundKey] = theme == "Light" ? Brushes.White : Brushes.Black;
                control.Resources[foregroundKey] = theme == "Light" ? Brushes.Black : Brushes.White;
                guide.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Render);
                var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(visual.ActualWidth)),
                    Math.Max(1, (int)Math.Ceiling(visual.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
                // 최상위 Grid의 Margin에 따른 시각 오프셋을 이미지 잘림으로 오인하지 않도록 로컬 영역을 그린다.
                var capture = new DrawingVisual();
                var offset = VisualTreeHelper.GetOffset(visual);
                using (var drawing = capture.RenderOpen())
                    drawing.DrawRectangle(new VisualBrush(visual) { ViewboxUnits = BrushMappingMode.Absolute,
                        Viewbox = new Rect(offset.X, offset.Y, visual.ActualWidth, visual.ActualHeight), Stretch = Stretch.Fill },
                        null, new Rect(0, 0, visual.ActualWidth, visual.ActualHeight));
                bitmap.Render(capture);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(Path.Combine(output, "Guide-" + theme + "-300.png"))) encoder.Save(file);
            }
            foreach (var button in new[] { guide.ConnectButton, guide.StartButton })
            {
                Rect bounds = button.TransformToAncestor(visual).TransformBounds(new Rect(button.RenderSize));
                Check(bounds.Left >= -1 && bounds.Right <= visual.ActualWidth + 1 && bounds.Top >= -1 && bounds.Bottom <= visual.ActualHeight + 1,
                    "Guide action is fully visible at declared minimum width: " + button.Content);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Visit
        /// desc         : 실제 창을 열고 첫 표시·수동 안내·설정 버튼 동작을 제한 시간 안에 재현한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 모달 창은 DispatcherTimer로 조작·종료하고 실패 시 모두 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static AssistantControl Visit(string mode, bool hide, Action<AssistantControl> prepare = null, Action<AssistantDialog, AssistantControl> inspect = null)
        {
            var control = new AssistantControl();
            control.Resources[backgroundKey] = Brushes.White; control.Resources[foregroundKey] = Brushes.Black;
            if (prepare != null) prepare(control);
            var owner = new Window { Title = "Synthetic onboarding review", Width = 760, Height = 720,
                Content = control, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(40) };
            var watch = Stopwatch.StartNew();
            bool seen = false, triggered = false; Exception failure = null;
            timer.Tick += (sender, args) =>
            {
                try
                {
                    if (watch.Elapsed.TotalSeconds > 12) throw new TimeoutException("Popup journey timed out: " + mode);
                    var dialog = owner.OwnedWindows.OfType<AssistantDialog>().FirstOrDefault(w => w.IsVisible);
                    if (dialog != null)
                    {
                        if (mode == "suppressed") throw new InvalidOperationException("Hidden first-use guide opened again");
                        if (seen) return;
                        seen = true;
                        Check(dialog.WindowStartupLocation == WindowStartupLocation.CenterScreen && dialog.Owner == owner,
                            "Popup uses CenterScreen and the current host owner: " + mode);
                        if (inspect != null) inspect(dialog, control);
                        var guide = dialog as GettingStartedDialog;
                        if (guide != null)
                        {
                            guide.HideNextTime.IsChecked = hide;
                            guide.StartButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                        else if (dialog.IsVisible) dialog.Close();
                        return;
                    }
                    if (seen || (mode == "suppressed" && watch.ElapsedMilliseconds >= 650)) { owner.Close(); return; }
                    if (mode != "first" && mode != "suppressed" && !triggered && watch.ElapsedMilliseconds >= 250)
                    {
                        triggered = true;
                        control.Dispatcher.BeginInvoke(new Action(() => ((Button)control.FindName(mode == "manual" ? "HelpButton" : "SettingsButton"))
                            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent))), DispatcherPriority.Background);
                    }
                }
                catch (Exception error)
                {
                    failure = error; control.Dispose();
                    foreach (var window in owner.OwnedWindows.Cast<Window>().ToArray()) window.Close();
                    owner.Close();
                }
            };
            try { timer.Start(); owner.ShowDialog(); }
            finally
            {
                timer.Stop(); control.Dispose();
                foreach (var window in owner.OwnedWindows.Cast<Window>().ToArray()) window.Close();
                owner.Close();
            }
            if (failure != null) throw failure;
            Check(mode == "suppressed" ? !seen : seen, "Expected popup visibility: " + mode);
            return control;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Run
        /// desc         : 이전 설정·첫 안내·숨김·수동 재표시·설정 저장 보존을 사용자 순서대로 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 창 렌더와 재생성으로 영속 설정의 회귀 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static JourneyResult Run(string path, string directory, object background, object foreground)
        {
            result = new JourneyResult(); output = directory; backgroundKey = background; foregroundKey = foreground;
            var app = Application.Current ?? new Application(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                var original = new AssistantSettings { CodexPath = @"C:\Synthetic CLI\codex.exe", ClaudePath = @"C:\Synthetic CLI\claude.exe",
                    CodexModel = "synthetic-codex", ClaudeModel = "synthetic-claude", Instructions = "저장된 합성 규칙", FontSize = 16 };
                var serializer = new JavaScriptSerializer();
                var legacy = serializer.Deserialize<Dictionary<string, object>>(serializer.Serialize(original)); legacy.Remove("HideWelcome");
                File.WriteAllText(path, serializer.Serialize(legacy));
                var store = new AssistantSettingsStore(path);
                Check(!store.Load().HideWelcome, "Existing v0.3 settings default to showing the first-use guide");
                Visit("first", true, c => ((TextBox)c.FindName("RulesText")).Text = "아직 저장하지 않은 합성 규칙", (d, c) => {
                    var guide = (GettingStartedDialog)d;
                    Check(guide.HideNextTime.IsChecked == false, "First-use guide starts with suppression unchecked");
                    var headers = c.ContextMenu.Items.OfType<MenuItem>().Select(i => Convert.ToString(i.Header)).ToArray();
                    Check(headers.Contains("설정") && headers.Contains("사용 안내"), "Chat context menu exposes settings and usage guide");
                    var prompt = (TextBox)c.FindName("Instruction");
                    Check(prompt.ContextMenu.Items.OfType<MenuItem>().Any(i => i.Command == System.Windows.Input.ApplicationCommands.Paste && i.CommandTarget == prompt),
                        "Prompt context menu retains its targeted paste command");
                });
                Check(store.Load().HideWelcome, "Do not show again persists after closing the first guide");
                PreserveSettings(store.Load(), original);
                Visit("suppressed", true);
                Visit("manual", false, null, (d, c) => Check(((GettingStartedDialog)d).HideNextTime.IsChecked == true,
                    "Manual usage guide still opens with the persisted suppression choice"));
                Check(!store.Load().HideWelcome, "Unchecking suppression in the manual guide enables first-use guidance again");
                PreserveSettings(store.Load(), original);
                Visit("first", true);
                var closed = Visit("settings", true, null, (d, c) => {
                    ((TextBox)c.FindName("RulesText")).Text = "닫아서 취소할 규칙";
                    c.FontSize = 24;
                });
                Check(((TextBox)closed.FindName("RulesText")).Text == original.Instructions && closed.FontSize == original.FontSize,
                    "Closing settings without saving restores the previous rules and font size");
                PreserveSettings(store.Load(), original);
                Visit("settings", true, null, (d, c) => {
                    ((TextBox)c.FindName("RulesText")).Text = "명시적으로 저장한 합성 규칙";
                    ((Button)c.FindName("SaveSettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
                Check(store.Load().HideWelcome && store.Load().Instructions == "명시적으로 저장한 합성 규칙",
                    "Explicit settings save preserves the usage-guide suppression choice");
                Visit("manual", true, c => {
                    var messages = (System.Collections.IList)((ItemsControl)c.FindName("ConversationMessages")).ItemsSource;
                    messages.Add(new ChatMessage { Role = "assistant", Text = "DYNAMIC_CONTEXT_MENU_PROBE" });
                }, (d, c) => {
                    var box = FindMessageBox(c, "DYNAMIC_CONTEXT_MENU_PROBE");
                    Check(box != null && box.ContextMenu != null && box.ContextMenu.Items.OfType<MenuItem>().Any(i => Convert.ToString(i.Header) == "설정"),
                        "Dynamically created conversation text exposes the settings context menu");
                    CaptureNarrowGuide((GettingStartedDialog)d, c);
                });
            }
            catch (Exception error) { result.Error = error.ToString(); }
            return result;
        }
    }
}
'@
    $journey = [CodexCode.JourneyReview.OnboardingJourney]::Run($env:VS2017_AI_SETTINGS_PATH, $OutputDirectory,
        [Microsoft.VisualStudio.Shell.VsBrushes]::ToolWindowBackgroundKey,
        [Microsoft.VisualStudio.Shell.VsBrushes]::ToolWindowTextKey)
    $report = [pscustomobject]@{
        Succeeded=$journey.Succeeded; CheckKind='IndependentWpfOnboardingJourney'; Passed=$journey.Checks.Count
        Checks=$journey.Checks; Error=$journey.Error; ProductVersion=$productAssembly.GetName().Version.ToString()
        AssemblySha256=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
        IdeRuntimeTest=$false; AccountLoginAttempted=$false; PersonalSettingsRead=$false
        OutputDirectory=$OutputDirectory; CheckedAt=(Get-Date).ToString('O')
    }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    $report | ConvertTo-Json -Depth 5
    if (!$journey.Succeeded) { throw "온보딩 사용자 흐름 검증 실패: $OutputDirectory" }
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolveHandler)
    $env:VS2017_AI_SETTINGS_PATH = $previousSettingsPath
}
