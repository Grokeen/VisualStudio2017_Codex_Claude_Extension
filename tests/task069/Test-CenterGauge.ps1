#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-CenterGauge
desc         : Dracula 설치 게이지의 중앙 대칭 채움과 실제 50% 픽셀을 확인한다.
author       : CodexCode
create date  : 2026-10-20
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$InstallerExe,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$ActualInstallLog
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(!(Test-Path -LiteralPath $output)){New-Item -ItemType Directory -Path $output | Out-Null}
$assembly=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($InstallerExe))
$type=$assembly.GetType('Grokeen.OfflineThemeInstaller.CenterGauge',$true)
$gauge=[Activator]::CreateInstance($type,$true)
$bitmap=$null
try {
    $gauge.Size=New-Object Drawing.Size(802,47)
    $value=$type.GetProperty('Value',[Reflection.BindingFlags]'Instance,NonPublic')
    $displayed=$type.GetField('displayedValue',[Reflection.BindingFlags]'Instance,NonPublic')
    $value.SetValue($gauge,50,$null)
    $displayed.SetValue($gauge,50)
    $bitmap=New-Object Drawing.Bitmap(802,47)
    $gauge.DrawToBitmap($bitmap,[Drawing.Rectangle]::FromLTRB(0,0,802,47))
    $path=Join-Path $output 'dracula-center-gauge-50.png'
    $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
    $track=[Drawing.Color]::FromArgb(40,42,54).ToArgb()
    $leftOuter=$bitmap.GetPixel(100,34).ToArgb()
    $rightOuter=$bitmap.GetPixel(700,34).ToArgb()
    $leftFill=$bitmap.GetPixel(300,34).ToArgb()
    $rightFill=$bitmap.GetPixel(500,34).ToArgb()
    if($leftOuter -ne $track -or $rightOuter -ne $track -or $leftFill -eq $track -or $rightFill -eq $track){throw '50% 게이지가 중앙 양쪽에 대칭으로 표시되지 않습니다.'}
    $value.SetValue($gauge,-1,$null)
    if($value.GetValue($gauge,$null) -ne 0){throw '하한 제한 실패'}
    $value.SetValue($gauge,101,$null)
    if($value.GetValue($gauge,$null) -ne 100){throw '상한 제한 실패'}
    $displayed.SetValue($gauge,100)
    $type.GetMethod('Reset',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($gauge,@()) | Out-Null
    if($value.GetValue($gauge,$null) -ne 0 -or $displayed.GetValue($gauge) -ne 0){throw '재시도 초기화 실패'}
    Write-Host "PASS Dracula center gauge symmetry, 0..100 bounds and retry reset: $path"
    if($ActualInstallLog){
        $lines=@(Get-Content -LiteralPath $ActualInstallLog -Encoding UTF8 | Where-Object {$_ -match '^INSTALL_PROGRESS (\d+)/(\d+)$'})
        if(!$lines.Count){throw '실제 설치 진행 신호가 없습니다.'}
        $total=$lines.Count
        for($i=1;$i -le $total;$i++){
            if($lines[$i-1] -ne "INSTALL_PROGRESS $i/$total"){throw "실제 설치 진행 신호가 순서대로 이어지지 않습니다: $($lines[$i-1])"}
        }
        Write-Host "PASS 실제 설치 진행 신호 $total/$total"
    }
} finally {
    if($bitmap){$bitmap.Dispose()}
    $gauge.Dispose()
}
