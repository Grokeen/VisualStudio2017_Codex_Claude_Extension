#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Build-OfflineThemeInstaller
desc         : 검증된 로컬 VSIX 7개와 설치 스크립트를 단일 오프라인 WinForms EXE에 내장한다.
author       : CodexCode
create date  : 2026-10-20
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [string]$PackageDirectory,
    [string]$OutputDirectory,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$PackageVersion='0.14.1'
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(!$PackageDirectory){$PackageDirectory=Join-Path $repository ('releases/v'+$PackageVersion)}
if(!$OutputDirectory){$OutputDirectory=Join-Path $repository ('artifacts/task069/installer-'+[guid]::NewGuid().ToString('N'))}
$packageRoot=[IO.Path]::GetFullPath($PackageDirectory)
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw '기존 설치기 출력은 보존합니다. 새 폴더를 지정하세요.'}
$names=@(
    "VS2017.15.7.AIAssistant-$PackageVersion.vsix",
    "VS2017.AIAssistant-$PackageVersion.vsix",
    "VS2019.AIAssistant-$PackageVersion.vsix",
    "VS2022.AIAssistant-$PackageVersion.vsix",
    "Grokeen-VisualStudio-Themes-$PackageVersion-VS2017.vsix",
    "Grokeen-VisualStudio-Themes-$PackageVersion-VS2019.vsix",
    "Grokeen-VisualStudio-Themes-$PackageVersion-VS2022.vsix"
)
$sumsPath=Join-Path $packageRoot 'SHA256SUMS.txt'
$lines=[IO.File]::ReadAllLines($sumsPath)
foreach($name in $names){
    $path=Join-Path $packageRoot $name
    $matching=@($lines|Where-Object{$_ -match ('^[A-Fa-f0-9]{64}\s+\*?'+[regex]::Escape($name)+'$')})
    if($matching.Count -ne 1 -or !(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $matching[0].Substring(0,64)){
        throw "설치기 내장 원본 체크섬 오류: $name"
    }
}
$sourceTemplate=Join-Path $repository 'installer/OfflineThemeInstaller/Program.cs'
$installerScript=Join-Path $PSScriptRoot 'Install-AIAssistant.ps1'
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
if(!(Test-Path -LiteralPath $csc)){throw '.NET Framework C# compiler가 없습니다.'}
$null=New-Item -ItemType Directory -Path $output
$source=Join-Path $output 'Program.generated.cs'
$sourceText=[IO.File]::ReadAllText($sourceTemplate)
if($sourceText.IndexOf('__PACKAGE_VERSION__',[StringComparison]::Ordinal) -lt 0){throw '설치기 버전 템플릿 표식이 없습니다.'}
[IO.File]::WriteAllText($source,$sourceText.Replace('__PACKAGE_VERSION__',$PackageVersion),(New-Object Text.UTF8Encoding($false)))
$exe=Join-Path $output "Grokeen-AI-Theme-Offline-Installer-$PackageVersion.exe"
$arguments=@('/nologo','/target:winexe','/platform:anycpu','/codepage:65001','/optimize+',('/out:'+$exe),'/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll')
foreach($item in @(@{Name='Install-AIAssistant.ps1';Path=$installerScript},@{Name='SHA256SUMS.txt';Path=$sumsPath})){
    $arguments+=('/resource:'+$item.Path+',Payload.'+$item.Name)
}
foreach($name in $names){$arguments+=('/resource:'+(Join-Path $packageRoot $name)+',Payload.'+$name)}
$arguments+=$source
& $csc @arguments 2>&1|Tee-Object -FilePath (Join-Path $output 'compile.log')
if($LASTEXITCODE -ne 0){throw '오프라인 설치기 컴파일 실패.'}
$result=[pscustomobject]@{
    Version=$PackageVersion;Exe=$exe;Bytes=(Get-Item -LiteralPath $exe).Length;Sha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    Payloads=@($names|ForEach-Object{[pscustomobject]@{Name=$_;Sha256=(Get-FileHash -LiteralPath (Join-Path $packageRoot $_) -Algorithm SHA256).Hash}})
    ScriptSha256=(Get-FileHash -LiteralPath $installerScript -Algorithm SHA256).Hash;NetworkUsed=$false;Signed=$false
}
$result|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
Write-Host "오프라인 설치기 생성: $exe"
$result
