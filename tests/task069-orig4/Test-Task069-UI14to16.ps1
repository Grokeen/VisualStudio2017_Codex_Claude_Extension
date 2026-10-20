#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-Task069-UI14to16
 desc        : TASK069 Role #14/#15/#16 상태 표시, 테마 대비, 고정 대화 헤더 회귀 경계.
author       : CodexOrig4
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig4 : 제품 빌드 없이 source/XAML/palette acceptance 검사.
               2026-10-21 CodexCode : 사용자·AI 톤온톤 구분 강화 및 입력란과 사용자 bubble 동일색 검사.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){if(@(Get-ChildItem -LiteralPath $output -Force).Count -ne 0){throw 'Use a new or empty output directory.'}}
$null=New-Item -ItemType Directory -Path $output -Force
$checks=New-Object 'Collections.Generic.List[string]'
$errors=New-Object 'Collections.Generic.List[string]'
function Check([string]$Name,[bool]$Passed){if($Passed){$checks.Add($Name)}else{$errors.Add($Name)}}
function Contrast([string]$Foreground,[string]$Background){
    function Lum([string]$Hex){$v=@(0,2,4|ForEach-Object{[Convert]::ToInt32($Hex.Substring($_,2),16)/255.0});$l=@($v|ForEach-Object{if($_ -le .04045){$_/12.92}else{[Math]::Pow(($_+.055)/1.055,2.4)}});return .2126*$l[0]+.7152*$l[1]+.0722*$l[2]}
    $a=Lum $Foreground;$b=Lum $Background;if($a -lt $b){$t=$a;$a=$b;$b=$t};return ($a+.05)/($b+.05)
}
$control=Get-Content -Raw (Join-Path $repo 'src/VS2017.AIAssistant/Views/AssistantControl.xaml')
$code=Get-Content -Raw (Join-Path $repo 'src/VS2017.AIAssistant/Views/AssistantControl.xaml.cs')
$experience=Get-Content -Raw (Join-Path $repo 'src/VS2017.AIAssistant/Views/AssistantControl.Experience.cs')
$queue=Get-Content -Raw (Join-Path $repo 'src/VS2017.AIAssistant/Views/AssistantControl.RequestQueue.cs')
$rooms=Get-Content -Raw (Join-Path $repo 'src/VS2017.AIAssistant/Views/AssistantControl.ConcurrentRooms.cs')
$palette=Get-Content -Raw (Join-Path $repo 'src/VS2017.AIAssistant/Views/AssistantPalette.cs')
$theme=Get-Content -Raw (Join-Path $repo 'src/VS2017.AIAssistant/Views/AssistantTheme.xaml')
try{$xmlDoc=New-Object System.Xml.XmlDocument;$xmlDoc.Load((Join-Path $repo 'src/VS2017.AIAssistant/Views/AssistantControl.xaml'));Check 'AssistantControl.xaml parses as XML' $true}catch{Check 'AssistantControl.xaml parses as XML' $false}
$buttons=[regex]::Match($code,'bool currentWorkPresent\s*=\s*activeBusy\s*\|\|\s*queuedRequests\.Count\s*>\s*0\s*\|\|\s*currentRoomWaiting;\s*GenerateButton\.Visibility\s*=\s*currentWorkPresent\s*\?\s*Visibility\.Collapsed\s*:\s*Visibility\.Visible;\s*StopButton\.Visibility\s*=\s*currentWorkPresent\s*\?\s*Visibility\.Visible\s*:\s*Visibility\.Collapsed;')
Check 'Generate/Stop visibility is mutually exclusive for current busy/waiting work' $buttons.Success
Check 'Current selected room state, not all-room count, drives roomBusy' ($code.Contains('bool roomBusy = IsCurrentRoomRunning;') -and $code.Contains('roomQueues.TryGetValue(activeRecords[selectedProvider].Id'))
Check 'Enter still dispatches through GenerateClicked without button visibility gating' ($experience.Contains('if (!string.IsNullOrWhiteSpace(Instruction.Text)) GenerateClicked(GenerateButton, new RoutedEventArgs());') -and -not ([regex]::IsMatch($experience,'GenerateButton\.(IsVisible|Visibility)')))
Check 'Existing eight-request queue caps remain in both queue paths' ($queue.Contains('private const int MaximumQueuedRequests = 8;') -and $queue.Contains('queuedRequests.Count >= MaximumQueuedRequests') -and $rooms.Contains('queue.Count >= MaximumQueuedRequests'))
Check 'Composer and user bubble reference the same semantic surface' ($control.Contains('Value="user"><Setter TargetName="MessageBubble" Property="Background" Value="{DynamicResource AssistantUserMessageBrush}"') -and $control.Contains('x:Name="ComposerSurface" Grid.Row="5" Grid.Column="2" Background="{DynamicResource AssistantUserMessageBrush}"'))
Check 'Assistant reply has separate, rounded, bounded surface' ($control.Contains('Value="assistant"><Setter TargetName="MessageBubble" Property="Background" Value="{DynamicResource AssistantReplyBackgroundBrush}"') -and $control.Contains('Property="CornerRadius" Value="12"') -and $control.Contains('Property="MaxWidth" Value="760"'))
Check 'User and reply brushes are defined and applied by every palette branch' ($theme.Contains('x:Key="AssistantUserMessageBrush"') -and $theme.Contains('x:Key="AssistantReplyBackgroundBrush"') -and $palette.Contains('AssistantUserMessageBrush:40424A:FFFFFF') -and $palette.Contains('AssistantReplyBackgroundBrush:292B33:DCE3EA') -and $palette.Contains('Set(resources, "AssistantUserMessageBrush", light ? "FFFFFF" : "424449")') -and $palette.Contains('Set(resources, "AssistantReplyBackgroundBrush", light ? "DCE3EA" : "292B30")') -and $palette.Contains('Set(resources, "AssistantUserMessageBrush", "4B4F63")') -and $palette.Contains('Set(resources, "AssistantReplyBackgroundBrush", "303342")'))
Check 'Header remains outside the message ScrollViewer with dynamic name tooltip and wrap' ($control.Contains('x:Name="ConversationHeader" Grid.Row="0"') -and $control.IndexOf('x:Name="ConversationHeader"') -lt $control.IndexOf('x:Name="ConversationScroll"') -and $control.Contains('x:Name="CurrentAgentLabel"') -and $control.Contains('TextWrapping="Wrap" MaxWidth="560"') -and $control.Contains('ToolTip="{Binding Text, RelativeSource={RelativeSource Self}}"'))
Check 'Header preserves original date label and existing conversation scroll hooks' ($control.Contains('x:Name="ConversationStartedLabel"') -and $control.Contains('ToolTip="이 대화가 생성된 실제 시각"') -and $control.Contains('ScrollChanged="ConversationViewportChanged"') -and $control.Contains('PreviewMouseWheel="ConversationViewportMouseWheel"'))
Check 'History splitter, toggle, resize and auto-hide hooks remain present' ($control.Contains('x:Name="HistoryColumn"') -and $control.Contains('x:Name="HistorySplitter"') -and $control.Contains('Click="HistoryToggled"') -and $control.Contains('DragCompleted="HistoryResizeCompleted"') -and $control.Contains('KeyUp="HistoryResizeKeyUp"'))
$scenarios=@(
    @{Name='VS dark';FG='EBEBEB';BG='292B33';Surface='40424A'},
    @{Name='VS light';FG='20252B';BG='DCE3EA';Surface='FFFFFF'},
    @{Name='Kakao dark';FG='F1F3F4';BG='292B30';Surface='424449'},
    @{Name='Kakao light';FG='191919';BG='DCE3EA';Surface='FFFFFF'},
    @{Name='Dracula';FG='F8F8F2';BG='303342';Surface='4B4F63'}
)
$results=foreach($s in $scenarios){$ratio=[Math]::Round((Contrast $s.FG $s.BG),2);$toneRatio=[Math]::Round((Contrast $s.Surface $s.BG),2);[pscustomobject]@{Theme=$s.Name;Contrast=$ratio;Pass=($ratio -ge 7);ToneContrast=$toneRatio;TonePass=($toneRatio -ge 1.25);ReplyDarker=([Convert]::ToInt32($s.BG.Substring(0,2),16) -lt [Convert]::ToInt32($s.Surface.Substring(0,2),16))}}
foreach($r in $results){Check ($r.Theme+' AI text contrast >=7:1') $r.Pass;Check ($r.Theme+' reply surface darker than composer') $r.ReplyDarker;Check ($r.Theme+' user/reply tone contrast >=1.25:1') $r.TonePass}
$result=[ordered]@{Status=if($errors.Count -eq 0){'PASS'}else{'FAIL'};ChecksPassed=$checks.Count;ChecksFailed=$errors.Count;Checks=@($checks.ToArray());Errors=@($errors.ToArray());Contrast=$results;VisualStudioCapture='Pending integrated C5 exact DLL; no product build or visible IDE performed by this test.'}
$result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
if($errors.Count){$errors|ForEach-Object{Write-Error $_};exit 1}
Write-Output ("TASK069 #14-#16 checks PASS: {0}/{0}." -f $checks.Count)
