#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ThemePolicy
desc         : IDE/Windows 조합과 수동 선택을 실제 정책 소스의 독립 컴파일로 검사한다.
author       : CodexOrig4
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig4 : 공유 bin/obj와 실제 사용자 테마를 변경하지 않는 정책 회귀 추가.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repository ('artifacts/task026/policy-' + [Guid]::NewGuid().ToString('N')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count) { throw '이전 증거를 보존하도록 새 출력 폴더를 지정하세요.' }
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
Add-Type -AssemblyName WindowsBase,PresentationCore
$source = Join-Path $repository 'src/VS2017.AIAssistant/Views/AssistantThemePolicy.cs'
$sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$frozen = Join-Path $OutputDirectory 'AssistantThemePolicy.cs'
Copy-Item -LiteralPath $source -Destination $frozen
if ((Get-FileHash -LiteralPath $frozen).Hash -ne $sourceHash) { throw '정책 사본 해시 불일치' }
$compiler = 'C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$references = 'C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$assembly = Join-Path $OutputDirectory 'ThemePolicy.SourceOnly.dll'
& $compiler '/nologo' '/target:library' '/langversion:7' '/codepage:65001' '/warnaserror+' '/nostdlib+' ('/out:' + $assembly) ('/lib:' + $references) '/reference:mscorlib.dll' '/reference:System.dll' '/reference:WindowsBase.dll' '/reference:PresentationCore.dll' $frozen 2>&1 | Tee-Object -FilePath (Join-Path $OutputDirectory 'compile.log')
if ($LASTEXITCODE -ne 0) { throw '정책 소스 독립 컴파일 실패' }
[Reflection.Assembly]::LoadFrom($assembly) | Out-Null
$light = [Windows.Media.Color]::FromRgb(245,245,245)
$dark = [Windows.Media.Color]::FromRgb(30,30,30)
$transparent = [Windows.Media.Color]::FromArgb(0,255,255,255)
$cases = @(
    @{ Name='Auto: light IDE overrides dark Windows'; Mode='Auto'; Ide=$light; Windows=$false; Expected='light' },
    @{ Name='Auto: dark IDE overrides light Windows'; Mode='Auto'; Ide=$dark; Windows=$true; Expected='dark' },
    @{ Name='Auto: no IDE falls back to light Windows'; Mode='Auto'; Ide=$null; Windows=$true; Expected='light' },
    @{ Name='Auto: no IDE falls back to dark Windows'; Mode='Auto'; Ide=$null; Windows=$false; Expected='dark' },
    @{ Name='Manual Light overrides dark IDE and Windows'; Mode='Light'; Ide=$dark; Windows=$false; Expected='light' },
    @{ Name='Manual Dark overrides light IDE and Windows'; Mode='Dark'; Ide=$light; Windows=$true; Expected='dark' },
    @{ Name='Legacy missing mode follows IDE'; Mode=$null; Ide=$light; Windows=$false; Expected='light' },
    @{ Name='Unknown mode safely follows IDE'; Mode='invalid'; Ide=$dark; Windows=$true; Expected='dark' },
    @{ Name='Case independent manual mode'; Mode='lIgHt'; Ide=$dark; Windows=$false; Expected='light' },
    @{ Name='Transparent host placeholder falls back to Windows'; Mode='Auto'; Ide=$transparent; Windows=$false; Expected='dark' },
    @{ Name='Blue IDE uses its light tool-window background'; Mode='Auto'; Ide=[Windows.Media.Color]::FromRgb(239,239,242); Windows=$false; Expected='light' }
)
$results = @($cases | ForEach-Object {
    $actual = [VS2017.AIAssistant.Views.AssistantThemePolicy]::Resolve($_.Mode,$_.Ide,$_.Windows)
    [pscustomobject]@{Name=$_.Name;Passed=$actual -eq $_.Expected;Expected=$_.Expected;Actual=$actual}
})
$failed = @($results | Where-Object {!$_.Passed}).Count
$result = [pscustomobject]@{Succeeded=$failed -eq 0;Passed=$results.Count-$failed;Failed=$failed;Checks=$results;SourceOnly=$true;ProductAssemblyTested=$false;SourceSha256=$sourceHash;AssemblySha256=(Get-FileHash -LiteralPath $assembly).Hash;WindowShown=$false;WindowsThemeChanged=$false;IdeThemeChanged=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
$result | ConvertTo-Json -Depth 6
if ((Get-FileHash -LiteralPath $source).Hash -ne $sourceHash) { throw '검사 중 정책 소스 변경됨' }
if ($failed) { throw '테마 정책 검사 실패: 원본 결과를 보존합니다.' }
