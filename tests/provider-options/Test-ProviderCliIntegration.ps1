#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ProviderCliIntegration
desc         : 지정한 실제 제품 DLL을 참조해 모델·Fast·협업 문맥의 네이티브 전달을 검사한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : TASK-015 실제 계정 없이 고정 제품 subprocess 통합 검증.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$assemblyFile = (Resolve-Path -LiteralPath $AssemblyPath).Path
$output = Join-Path $repository ('artifacts\pi\' + [guid]::NewGuid().ToString('N').Substring(0,10))
$null = New-Item -ItemType Directory -Path $output
Copy-Item -LiteralPath $assemblyFile -Destination $output
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$testExe = Join-Path $output 'ProviderCliIntegrationTests.exe'
$arguments = @('/nologo','/target:exe','/langversion:5',('/out:' + $testExe),('/reference:' + $assemblyFile),'/reference:System.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll', (Join-Path $PSScriptRoot 'ProviderCliIntegrationTests.cs'))
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "Provider 통합 검사 컴파일 실패: $LASTEXITCODE" }
& $testExe $output 2>&1 | Tee-Object -FilePath (Join-Path $output 'results.log')
if ($LASTEXITCODE -ne 0) { throw "Provider 통합 검사 실패: $LASTEXITCODE ($output)" }
[pscustomobject]@{ Directory=$output; AssemblySHA256=(Get-FileHash -LiteralPath $assemblyFile -Algorithm SHA256).Hash; Result=(Get-Content (Join-Path $output 'result.json') -Raw -Encoding UTF8 | ConvertFrom-Json) } | ConvertTo-Json -Depth 5
