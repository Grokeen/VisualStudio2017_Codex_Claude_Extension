#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-Task069Orig4
desc         : TASK069-3/6/7 작업기록 입력 방식과 독립 Visual Studio 테마 패키지를 검사한다.
author       : CodexOrig4
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig4 : 공용 bin/obj·설치·IDE 실행 없이 전용 산출물로 검사.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Windows PowerShell 5.1 -NoProfile -STA로 실행하세요.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){if(@(Get-ChildItem -LiteralPath $output -Force).Count -ne 0){throw 'TASK069 검사 출력은 새 경로 또는 빈 경로여야 합니다.'}}
$null=New-Item -ItemType Directory -Path $output -Force
$checks=New-Object 'Collections.Generic.List[string]'
$errors=New-Object 'Collections.Generic.List[string]'
function Check([string]$Name,[bool]$Passed){if($Passed){$checks.Add($Name)}else{$errors.Add($Name)}}
function Get-Luminance([string]$Hex){
    $rgb=@(0,2,4 | ForEach-Object {[Convert]::ToInt32($Hex.Substring($_,2),16)/255.0})
    $linear=@($rgb | ForEach-Object {if($_ -le .04045){$_/12.92}else{[Math]::Pow(($_+.055)/1.055,2.4)}})
    return .2126*$linear[0]+.7152*$linear[1]+.0722*$linear[2]
}
function Get-Contrast([string]$First,[string]$Second){$a=Get-Luminance $First;$b=Get-Luminance $Second;return ([Math]::Max($a,$b)+.05)/([Math]::Min($a,$b)+.05)}
function Get-ThemeColor([xml]$Document,[string]$Category,[string]$Name,[string]$Property='Background'){
    $entry=$Document.SelectSingleNode("//Category[@Name='$Category']/Color[@Name='$Name']")
    if(!$entry){throw "Theme token is missing: $Category/$Name"}
    $value=$entry.SelectSingleNode($Property)
    if(!$value){throw "Theme color field is missing: $Category/$Name/$Property"}
    return $value.GetAttribute('Source').Substring(2)
}
$themeOutput=Join-Path $output 'theme-build'
$powershell=Join-Path $PSHOME 'powershell.exe'
& $powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts/Build-VisualStudioThemes.ps1') -OutputDirectory $themeOutput -Version 0.13.0 *> (Join-Path $output 'theme-build-console.log')
if($LASTEXITCODE -ne 0){throw '세 IDE 대상 독립 테마 빌드가 실패했습니다.'}
$build=Get-Content -LiteralPath (Join-Path $themeOutput 'result.json') -Raw | ConvertFrom-Json
Check 'VS2017/2019/2022 theme VSIX builds have zero warnings and errors' (@($build.Packages).Count -eq 3 -and @($build.Packages | Where-Object {$_.BuildExitCode -ne 0 -or $_.Warnings -ne 0 -or $_.Errors -ne 0}).Count -eq 0)
$dracula=[xml](Get-Content -LiteralPath (Join-Path $themeOutput 'definitions/Dracula.xml') -Raw)
$track=Get-ThemeColor $dracula 'Environment' 'ScrollBarBackground'
$thumb=Get-ThemeColor $dracula 'Environment' 'ScrollBarThumbBackground'
$thumbHover=Get-ThemeColor $dracula 'Environment' 'ScrollBarThumbMouseOverBackground'
Check ('Dracula scrollbar thumb contrasts with its track ('+[Math]::Round((Get-Contrast $track $thumb),2)+':1)') ((Get-Contrast $track $thumb) -ge 3)
Check ('Dracula hover thumb contrasts with its track ('+[Math]::Round((Get-Contrast $track $thumbHover),2)+':1)') ((Get-Contrast $track $thumbHover) -ge 3)
Check 'Dracula scrollbar track and thumb use distinct semantic colors' ($track -eq '282A36' -and $thumb -eq '6272A4')
$kakao=[xml](Get-Content -LiteralPath (Join-Path $themeOutput 'definitions/Kakao.xml') -Raw)
$normal=Get-ThemeColor $kakao 'Cider' 'TabItem'
$normalText=Get-ThemeColor $kakao 'Cider' 'TabItem' 'Foreground'
$hover=Get-ThemeColor $kakao 'Cider' 'TabItemMouseOver'
$hoverText=Get-ThemeColor $kakao 'Cider' 'TabItemMouseOver' 'Foreground'
$selected=Get-ThemeColor $kakao 'Cider' 'TabItemSelected'
$selectedText=Get-ThemeColor $kakao 'Cider' 'TabItemSelected' 'Foreground'
Check ('Kakao hovered tab keeps readable white text on charcoal ('+[Math]::Round((Get-Contrast $hover $hoverText),2)+':1)') ($hover -eq '34363A' -and $hoverText -eq 'FFFFFF' -and (Get-Contrast $hover $hoverText) -ge 4.5)
Check ('Kakao normal, hovered and selected tabs remain visually distinct') ($normal -ne $hover -and $hover -ne $selected -and $normal -ne $selected)
Check ('Kakao selected tab retains dark text on its yellow accent ('+[Math]::Round((Get-Contrast $selected $selectedText),2)+':1)') ($selected -eq 'FEE500' -and $selectedText -eq '191919' -and (Get-Contrast $selected $selectedText) -ge 4.5)
Check 'Kakao document and tool-window hover tokens use charcoal, white text and yellow border' ((Get-ThemeColor $kakao 'Environment' 'FileTabHotGradientTop') -eq '34363A' -and (Get-ThemeColor $kakao 'Environment' 'FileTabHotText') -eq 'FFFFFF' -and (Get-ThemeColor $kakao 'Environment' 'ToolWindowTabMouseOverBackgroundBegin') -eq '34363A' -and (Get-ThemeColor $kakao 'Environment' 'ToolWindowTabMouseOverText') -eq 'FFFFFF' -and (Get-ThemeColor $kakao 'Environment' 'ToolWindowTabMouseOverBorder') -eq 'FEE500')
$progressOutput=Join-Path $output 'progress-source'
& $powershell -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $repo 'tests/task022/Test-ProgressPresentation.ps1') -OutputDirectory $progressOutput *> (Join-Path $output 'progress-source-console.log')
if($LASTEXITCODE -ne 0){throw 'TASK022 source-only 진행 표시 회귀 묶음이 실패했습니다.'}
$assemblyPath=Join-Path $progressOutput 'ProgressProductionSourceWithHostFixture.dll'
[Reflection.Assembly]::LoadFrom($assemblyPath) | Out-Null
$presenterType=([Reflection.Assembly]::LoadFrom($assemblyPath)).GetType('VS2017.AIAssistant.Views.ProgressNoticePresenter')
$presenter=[Activator]::CreateInstance($presenterType)
$header=$presenterType.GetField('header',[Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::NonPublic).GetValue($presenter)
$storeProperty=[System.Windows.UIElement].GetProperty('EventHandlersStore',[Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::NonPublic)
$eventStore=$storeProperty.GetValue($header)
$hoverHandlers=$eventStore.GetRoutedEventHandlers([System.Windows.Input.Mouse]::MouseEnterEvent)
$clickHandlers=$eventStore.GetRoutedEventHandlers([System.Windows.Controls.Primitives.ButtonBase]::ClickEvent)
Check 'Progress detail has no automatic MouseEnter handler' ($null -eq $hoverHandlers -or $hoverHandlers.Length -eq 0)
Check 'Progress detail opens through the existing explicit click handler' (@($clickHandlers).Count -eq 1 -and $clickHandlers[0].Handler.Method.Name -eq 'HeaderClicked')
$help=[System.Windows.Automation.AutomationProperties]::GetHelpText($header)
Check 'Progress detail exposes its explicit action to assistive technology' ($help.Contains('Enter') -and $help.Length -ge 10)
$progressResult=Get-Content -LiteralPath (Join-Path $progressOutput 'result.json') -Raw | ConvertFrom-Json
Check ('Progress source-only WPF regression passed ('+$progressResult.Passed+' checks)') ($progressResult.Succeeded -and $progressResult.Failed -eq 0)
$result=[pscustomobject]@{Succeeded=$errors.Count -eq 0;Passed=$checks.Count;Failed=$errors.Count;Checks=@($checks);Errors=@($errors);ThemePackages=@($build.Packages);ThemeDefinitionHashes=@{Kakao=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $themeOutput 'definitions/Kakao.xml')).Hash;Dracula=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $themeOutput 'definitions/Dracula.xml')).Hash};ProgressSourceAssemblySha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $assemblyPath).Hash;ProgressSourceRegressionPassed=$progressResult.Passed;WindowShown=$false;NativePointerUsed=$false;InstalledVisualStudioChanged=$false;UserThemeChanged=$false;Version=$build.Version;CheckedAt=[DateTimeOffset]::Now.ToString('O')}
$result | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
$result | ConvertTo-Json -Depth 7
if(!$result.Succeeded){throw "TASK069 Orig4 검증 실패: $output"}
