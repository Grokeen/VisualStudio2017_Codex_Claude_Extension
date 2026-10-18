#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Render-UserExperience
desc         : 고정 제품 DLL의 실제 WPF에 합성 예제를 넣어 사용자 문서 화면을 렌더한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : 개인 설정·대화·계정과 분리한 Chat/설정/안내 렌더.
update date  : 2026-10-18 CodexOrig : TASK-018 드롭다운·이미지 요구 및 기본14개 검사 반영.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$OutputDirectory, [string]$Version = '0.9')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw 'Windows PowerShell 5.1 -STA로 실행하세요.' }
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$AssemblyPath) { $AssemblyPath = Join-Path $repositoryDir 'src\VS2017.AIAssistant\bin\Release\VS2017.AIAssistant.dll' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repositoryDir 'docs\assets' }
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$previewRoot = Join-Path $repositoryDir ('artifacts\task020\preview-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $previewRoot -Force
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml
$graph = Get-Content (Join-Path $repositoryDir 'src\VS2017.AIAssistant\obj\project.assets.json') -Raw | ConvertFrom-Json
$cache = $graph.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$references = @{}
foreach ($dependency in $graph.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) {
    foreach ($entry in $dependency.Value.compile.PSObject.Properties.Name) {
        if ($entry -like '*.dll') { $references[[IO.Path]::GetFileNameWithoutExtension($entry)] = Join-Path $cache ($graph.libraries.($dependency.Name).path + '/' + $entry) }
    }
}
$resolver = [ResolveEventHandler]{ param($sender,$eventArgs) $key=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name; if ($references.ContainsKey($key)) { return [Reflection.Assembly]::LoadFrom($references[$key]) }; return $null }
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
$oldSettings = $env:VS2017_AI_SETTINGS_PATH
$oldWorkspace = $env:VS2017_AI_WORKSPACE_PATH
$hostWindow = $null
$control = $null
try {
    $env:VS2017_AI_SETTINGS_PATH = Join-Path $previewRoot 'settings.json'
    $env:VS2017_AI_WORKSPACE_PATH = Join-Path $previewRoot 'workspace'
    $assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath).Path)
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : CodexOrigPreview
/// desc         : 실제 제품 시각 트리를 여백 오프셋 없이 렌더하고 제품 소유 팝업만 닫는다.
/// author       : CodexOrig
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexOrig : WPF 문서 화면 생성 전용 보조 타입.
/// </summary>
/// ---------------------------------------------------------------------
public static class CodexOrigPreview
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CaptureFan
    /// desc         : 제품 옵션 메뉴의 hover를 실행하고 애니메이션 완료 후 실제 배치를 저장한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-018 부채꼴 메뉴와 고정 hit 영역의 사용자 화면 근거.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void CaptureFan(FrameworkElement root, ToggleButton trigger, FrameworkElement fan, string path)
    {
        Window.GetWindow(root).Activate(); trigger.Focus();
        trigger.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent });
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        timer.Tick += (s, a) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
        if (fan.Visibility != Visibility.Visible) throw new InvalidOperationException("The fan menu closed before it could be captured.");
        Save(root, path);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CaptureDropdown
    /// desc         : 버튼에 연결된 실제 WPF Popup 표면을 합성 사용자 화면으로 저장한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-018 모달과 구분되는 드롭다운 시각 근거 생성.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void CaptureDropdown(ButtonBase button, object control, string path)
    {
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        timer.Tick += (s, a) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
        var root = PresentationSource.CurrentSources.OfType<HwndSource>().Select(s => s.RootVisual).OfType<FrameworkElement>().LastOrDefault(r => r.IsVisible && r.GetType().Name == "PopupRoot");
        if (root == null) throw new InvalidOperationException("Requested dropdown is not visible: " + button.Name);
        Save(root, path);
        foreach (var field in control.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)) {
            var popup = field.GetValue(control) as Popup; if (popup != null) popup.IsOpen = false;
        }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Save
    /// desc         : 본문 경계에 맞춘 실제 WPF 시각 트리를 PNG로 저장한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : Margin에 의한 우측/하단 잘림 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Save(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var visual = new DrawingVisual();
        var bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
        var offset = VisualTreeHelper.GetOffset(element);
        var brush = new VisualBrush(element) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(offset.X, offset.Y, bounds.Width, bounds.Height), Stretch = Stretch.Fill };
        using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(brush, null, bounds);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CapturePopup
    /// desc         : 실제 버튼 이벤트로 연 팝업을 렌더한 후 닫고 시간 초과를 보고한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 개인 IDE 창을 열거나 닫지 않는 소유 창 제한.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void CapturePopup(Button button, Window owner, string path)
    {
        Exception failure = null; bool captured = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (sender, args) => {
            var popup = PresentationSource.CurrentSources.OfType<HwndSource>().Select(s => s.RootVisual).OfType<Window>().FirstOrDefault(w => w.Owner == owner && w.IsVisible);
            timer.Stop();
            try
            {
                if (popup == null) throw new InvalidOperationException("Product popup did not open.");
                var suppression = popup.GetType().GetProperty("HideNextTime");
                if (suppression != null) ((CheckBox)suppression.GetValue(popup, null)).IsChecked = false;
                Save((FrameworkElement)popup.Content, path); captured = true;
            }
            catch (Exception error) { failure = error; }
            finally { if (popup != null) popup.Close(); }
        };
        timer.Start();
        try { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
        finally { timer.Stop(); }
        if (failure != null) throw failure;
        if (!captured) throw new InvalidOperationException("Popup capture was not completed.");
    }
}
'@ -ReferencedAssemblies @('System','System.Core',[System.Windows.Markup.IQueryAmbient].Assembly.Location,[Windows.UIElement].Assembly.Location,[Windows.Window].Assembly.Location,[Windows.Threading.Dispatcher].Assembly.Location)
    $settings = New-Object VS2017.AIAssistant.Chat.AssistantSettings
    $settings.HideWelcome = $true; $settings.FontSize = 14; $settings.ClaudeModel = 'sonnet'
    $settingsStore = New-Object VS2017.AIAssistant.Chat.AssistantSettingsStore -ArgumentList $env:VS2017_AI_SETTINGS_PATH
    $settingsStore.Save($settings)
    $store = New-Object VS2017.AIAssistant.Chat.MarkdownWorkspaceStore -ArgumentList $env:VS2017_AI_WORKSPACE_PATH
    $record = $store.Create('Claude')
    foreach ($entry in @(@('user','이 메서드가 어떤 일을 하는지 설명해 주세요.'),@('assistant','문자열 앞뒤 공백을 제거합니다. null 입력에 대한 처리가 필요합니다.'),@('user','null이면 빈 문자열을 반환하도록 바꿔 주세요. 기존 메서드 이름은 유지해 주세요.'),@('assistant','null 입력을 빈 문자열로 처리하는 변경을 준비했습니다. 파일의 변경 비교를 확인해 주세요.'))) {
        $message = New-Object VS2017.AIAssistant.Chat.ChatMessage; $message.Role=$entry[0]; $message.Text=$entry[1]; $record.Messages.Add($message)
    }
    $record.State='Completed'; $store.Save($record); $store.Rename($record,'입력 검증 · null 처리')
    $other = $store.Create('Claude'); $other.State='Completed'; $other.HasUnread=$true; $store.Rename($other,'SQL 조회 조건 정리')
    $store.SaveInstructions("응답은 한국어로 작성합니다.`n기존 API와 들여쓰기를 유지합니다.")
    $agentStore = New-Object VS2017.AIAssistant.Chat.ConversationCollaborationStore -ArgumentList $env:VS2017_AI_WORKSPACE_PATH
    foreach($r in $store.LoadOpen('Claude')) { $a=$agentStore.EnsureAgent($r); $null=$agentStore.UpdateAgent($r.Id,'Claude',$a.Name,'코드 품질과 예외 처리를 검토합니다.','개발 검토팀',$a.Revision) }
    $null=$agentStore.ConfigureNaming('Claude','예시사용자')
    $control = New-Object VS2017.AIAssistant.Views.AssistantControl
    $null=$agentStore.ConfigureNaming('Claude','예시사용자')
    $control.GetType().GetMethod('RefreshExperience',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($control,@()) | Out-Null
    $control.FindName('DiffPreview').Text = "--- 변경 전`nreturn input.Trim();`n`n+++ 변경 후`nreturn (input ?? string.Empty).Trim();"
    $control.FindName('OriginalPreview').Text = 'return input.Trim();'
    $control.FindName('ProposedPreview').Text = 'return (input ?? string.Empty).Trim();'
    $control.FindName('TargetLabel').Text = 'Example/Normalize.cs'
    $control.FindName('ChangeSummary').Text = '변경된 파일 1개 · +1 −1'
    $control.FindName('ReviewPanel').IsExpanded=$true
    $control.FindName('ModeChoice').SelectedValue='high'
    $control.FindName('HistoryToggle').IsChecked=$true
    $control.FindName('HistoryToggle').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Primitives.ButtonBase]::ClickEvent)))
    $hostWindow = New-Object Windows.Window
    $hostWindow.Title='Synthetic product preview'; $hostWindow.Content=$control; $hostWindow.Width=720; $hostWindow.Height=1020; $hostWindow.ShowInTaskbar=$false
    $hostWindow.Show()
    $control.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ContextIdle)
    # 합성 데이터로 실제 변경 카드의 바인딩/레이아웃을 표시한다. CLI나 소스 적용은 실행하지 않는다.
    $samplePath = Join-Path $previewRoot 'Normalize.cs'
    $snapshot = New-Object VS2017.AIAssistant.Editing.DocumentSnapshot -ArgumentList @($samplePath,'return input.Trim();','synthetic','Synthetic preview',$false,$false)
    $proposal = [VS2017.AIAssistant.Editing.EditProposal]::Parse($snapshot,'{"summary":"null 입력을 빈 문자열로 처리합니다.","edits":[{"old_text":"return input.Trim();","new_text":"return (input ?? string.Empty).Trim();"}]}')
    $bindingFlags = [Reflection.BindingFlags]'Instance,NonPublic'
    $control.GetType().GetField('pendingProposal',$bindingFlags).SetValue($control,$proposal)
    $control.GetType().GetMethod('UpdateChangeCard',$bindingFlags).Invoke($control,@()) | Out-Null
    $control.FindName('TargetLabel').Text='Example/Normalize.cs'
    $control.FindName('ReviewPanel').IsExpanded=$true
    $control.FindName('ChangeCard').Visibility='Visible'
    $control.FindName('ReviewApproval').IsEnabled=$true
    $control.FindName('SourceLink').IsEnabled=$true
    $visual = New-Object Windows.Media.DrawingVisual
    $drawing = $visual.RenderOpen()
    $drawing.DrawRoundedRectangle([Windows.Media.Brushes]::DarkSlateGray,$null,(New-Object Windows.Rect(0,0,180,90)),8,8)
    $caption = New-Object Windows.Media.FormattedText -ArgumentList @('null → empty',[Globalization.CultureInfo]::InvariantCulture,[Windows.FlowDirection]::LeftToRight,(New-Object Windows.Media.Typeface('Segoe UI')),20,[Windows.Media.Brushes]::White)
    $drawing.DrawText($caption,(New-Object Windows.Point(16,28))); $drawing.Close()
    $image = New-Object Windows.Media.Imaging.RenderTargetBitmap(180,90,96,96,[Windows.Media.PixelFormats]::Pbgra32); $image.Render($visual)
    $imageEncoder = New-Object Windows.Media.Imaging.PngBitmapEncoder; $imageEncoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($image))
    $imageStream = New-Object IO.MemoryStream; $imageEncoder.Save($imageStream)
    $attachment=[VS2017.AIAssistant.Chat.ChatAttachment]::FromImage($imageStream.ToArray(),'reference.png'); $imageStream.Dispose()
    $control.FindName('AttachmentList').ItemsSource=@($attachment)
    $renderRoot=$control.FindName('LayoutRoot'); $renderRoot.Background=$control.Background
    $control.FindName('ReviewPanel').IsExpanded=$false
    [CodexOrigPreview]::Save($renderRoot,(Join-Path $OutputDirectory ('chat-' + $Version + '-preview.png')))
    $control.FindName('ReviewPanel').IsExpanded=$true
    [CodexOrigPreview]::Save($renderRoot,(Join-Path $OutputDirectory ('review-' + $Version + '.png')))
    [CodexOrigPreview]::CapturePopup($control.FindName('HelpButton'),$hostWindow,(Join-Path $OutputDirectory ('getting-started-' + $Version + '.png')))
    [CodexOrigPreview]::CapturePopup($control.FindName('SettingsButton'),$hostWindow,(Join-Path $OutputDirectory ('settings-' + $Version + '.png')))
    [CodexOrigPreview]::CapturePopup($control.FindName('CliHelpButton'),$hostWindow,(Join-Path $OutputDirectory ('cli-setup-' + $Version + '.png')))
    [CodexOrigPreview]::CaptureDropdown($control.FindName('ModelOptionsButton'),$control,(Join-Path $OutputDirectory ('model-options-' + $Version + '.png')))
    [CodexOrigPreview]::CaptureDropdown($control.FindName('AgentButton'),$control,(Join-Path $OutputDirectory ('agent-options-' + $Version + '.png')))
    [CodexOrigPreview]::CaptureDropdown($control.FindName('PermissionsButton'),$control,(Join-Path $OutputDirectory ('permissions-' + $Version + '.png')))
    $control.FindName('ReviewPanel').IsExpanded=$false
    $control.FindName('ConversationActionsScroll').ScrollToRightEnd()
    [CodexOrigPreview]::Save($renderRoot,(Join-Path $OutputDirectory ('conversation-actions-' + $Version + '.png')))
    [CodexOrigPreview]::CaptureFan($control,$control.FindName('OptionsToggle'),$control.FindName('FanPanel'),(Join-Path $OutputDirectory ('options-fan-' + $Version + '.png')))
    [pscustomobject]@{ AssemblySHA256=(Get-FileHash -LiteralPath $AssemblyPath).Hash; SyntheticConversation=$true; OutputDirectory=$OutputDirectory; SettingsAndWorkspace=$previewRoot } | ConvertTo-Json | Set-Content (Join-Path $previewRoot 'render.json') -Encoding UTF8
    Write-Output $previewRoot
}
finally {
    if ($control) { $control.Dispose() }
    if ($hostWindow) { $hostWindow.Close() }
    $env:VS2017_AI_SETTINGS_PATH=$oldSettings; $env:VS2017_AI_WORKSPACE_PATH=$oldWorkspace
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}
