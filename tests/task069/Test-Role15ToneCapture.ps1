#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-Role15ToneCapture
desc         : 실제 제품 DLL의 사용자·AI 대화색을 다섯 팔레트에서 렌더링해 비교한다.
author       : CodexCode
create date  : 2026-10-21
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProductDll,
    [Parameter(Mandatory=$true)][string]$AssetsPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=[IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
$sourceDirectory=$output+'-source'
if(Test-Path -LiteralPath $output){throw '기존 캡처 결과는 보존하고 새 출력 폴더를 지정하세요.'}
if(Test-Path -LiteralPath $sourceDirectory){throw '기존 캡처 소스는 보존하고 새 출력 폴더를 지정하세요.'}
$null=New-Item -ItemType Directory -Path $sourceDirectory
$base=[IO.File]::ReadAllText((Join-Path $repo 'tests/task069-orig4/CaptureC5Ui14to16.cs'))
if($base.Split(@('control.FindResource("AssistantSurfaceBrush")'),[StringSplitOptions]::None).Count -ne 2){throw '기존 대화색 검사 소스가 변경됐습니다.'}
if($base.Split(@('darker && contrast >= 4.5'),[StringSplitOptions]::None).Count -ne 2){throw '기존 대비 검사 소스가 변경됐습니다.'}
if($base.Split(@('var userBrush = user == null ? null : user.Background as SolidColorBrush;'),[StringSplitOptions]::None).Count -ne 2){throw '기존 사용자 bubble 검사 소스가 변경됐습니다.'}
$source=$base.Replace('control.FindResource("AssistantSurfaceBrush")','control.FindResource("AssistantUserMessageBrush")').Replace('darker && contrast >= 4.5','darker && Contrast(surface.Color, reply.Color) >= 1.25 && contrast >= 7.0').Replace('reply is darker with >=4.5:1 text contrast','reply is darker with >=7:1 text and >=1.25:1 tone contrast').Replace('TASK069 C5 frozen VS2017 Release exact DLL','TASK070 Role15 tone candidate VS2017 Release DLL')
$source=$source.Replace('var userBrush = user == null ? null : user.Background as SolidColorBrush;', 'var userBrush = user == null ? null : user.Background as SolidColorBrush;' + "`n        var composerBrush = ((Border)GetField(control, `"ComposerSurface`" )).Background as SolidColorBrush;`n        Check(theme.Name + `" live composer equals user bubble`", composerBrush != null && userBrush != null && composerBrush.Color == userBrush.Color, new { Composer = composerBrush == null ? `"missing`" : composerBrush.Color.ToString(), User = userBrush == null ? `"missing`" : userBrush.Color.ToString() });")
$derived=Join-Path $sourceDirectory 'CaptureRole15Tone.cs'
[IO.File]::WriteAllText($derived,$source,(New-Object Text.UTF8Encoding($false)))
$dllHash=(Get-FileHash -LiteralPath $ProductDll -Algorithm SHA256).Hash
$assetsHash=(Get-FileHash -LiteralPath $AssetsPath -Algorithm SHA256).Hash
& (Join-Path $repo 'tests/task069-orig4/Test-CaptureC5Ui14to16.ps1') -ProductDll $ProductDll -AssetsPath $AssetsPath -ExpectedDllSha256 $dllHash -ExpectedAssetsSha256 $assetsHash -OutputDirectory $OutputDirectory -CaptureSourcePath $derived
if($LASTEXITCODE -and $LASTEXITCODE -ne 0){throw "대화색 캡처 실패: $LASTEXITCODE"}
