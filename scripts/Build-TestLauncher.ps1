#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Build-TestLauncher
desc         : Windows .NET Framework 기본 컴파일러로 최상위 테스트 실행기를 재현한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 제품 빌드·패키지 변경 없이 단독 exe 생성.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$CompilerPath)
$ErrorActionPreference = 'Stop'
$launcherRepository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$CompilerPath) {
    $CompilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (!(Test-Path -LiteralPath $CompilerPath)) { $CompilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
}
if (!(Test-Path -LiteralPath $CompilerPath)) { throw '.NET Framework C# 컴파일러를 찾지 못했습니다.' }
$launcherOutput = Join-Path $launcherRepository 'VisualStudio_AIChat_ExtentionTest.exe'
$launcherArguments = @('/nologo','/target:exe','/platform:anycpu','/optimize+','/langversion:5',('/out:' + $launcherOutput),'/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll', (Join-Path $launcherRepository 'tests\launcher\TestLauncher.cs'))
& $CompilerPath @launcherArguments
if ($LASTEXITCODE -ne 0) { throw "테스트 실행기 컴파일 실패: $LASTEXITCODE" }
Get-Item -LiteralPath $launcherOutput | Select-Object FullName, Length
Get-FileHash -LiteralPath $launcherOutput -Algorithm SHA256
