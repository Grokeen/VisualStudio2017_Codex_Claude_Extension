#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ProviderOptions
desc         : 모델·강도·Fast helper를 공유 제품 빌드와 분리해 컴파일하고 실제 인수 전달을 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 합성 모델 캐시·엄격 JSON·지원 기능·Windows argv 검사.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$ProductAssemblyPath,[string]$OutputDirectory,[string]$CompilerPath)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputDirectory) { $OutputDirectory=Join-Path $repositoryDir ('artifacts\task015\provider-options-'+[guid]::NewGuid().ToString('N')) }
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if (!$CompilerPath) { $CompilerPath='C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe' }
if (!(Test-Path -LiteralPath $CompilerPath)) { throw 'C# 7 컴파일러를 지정하세요.' }
$executable=Join-Path $OutputDirectory 'Provider Options 한글.exe'
$arguments=@('/nologo','/target:exe','/langversion:7','/codepage:65001','/reference:System.Web.Extensions.dll',('/out:'+$executable),(Join-Path $PSScriptRoot 'ProviderOptionsTests.cs'))
if ($ProductAssemblyPath) {
    $ProductAssemblyPath=(Resolve-Path -LiteralPath $ProductAssemblyPath).Path
    Copy-Item -LiteralPath $ProductAssemblyPath -Destination (Join-Path $OutputDirectory 'VS2017.AIAssistant.dll')
    $arguments+=('/reference:'+(Join-Path $OutputDirectory 'VS2017.AIAssistant.dll'))
} else { $arguments+=(Join-Path $repositoryDir 'src\VS2017.AIAssistant\Providers\ProviderExecutionOptions.cs') }
& $CompilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Provider 옵션 검사 컴파일 실패' }
& $executable $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw "Provider 옵션 검사 실패: $OutputDirectory" }
