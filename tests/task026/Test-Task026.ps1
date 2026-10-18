#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-Task026
desc         : 명시한 실제 제품 DLL을 새 STA 프로세스로 검사하고 해당 DLL 해시의 결과만 통합한다.
author       : CodexOrig4
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig4 : 소스 fixture 수치는 실제 제품 통과 수에 합산하지 않음.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
$before=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
if(!$OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot ('../../artifacts/task026/product-'+[Guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count){throw '새 출력 폴더를 지정하세요.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$runtimeOutput=Join-Path $OutputDirectory 'runtime'
$previousErrorPreference=$ErrorActionPreference
try{
    $ErrorActionPreference='Continue'
    & (Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe') -NoLogo -NoProfile -NonInteractive -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-ThemeRuntime.ps1') -AssemblyPath $AssemblyPath -OutputDirectory $runtimeOutput 2>&1 | Tee-Object -FilePath (Join-Path $OutputDirectory 'runtime.log')
}finally{$ErrorActionPreference=$previousErrorPreference}
$code=$LASTEXITCODE
$resultPath=Join-Path $runtimeOutput 'result.json'
if(!(Test-Path -LiteralPath $resultPath)){throw "테마 검사 결과가 없습니다. exit=$code"}
$result=Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8|ConvertFrom-Json
if($result.SourceOnly -or !$result.ProductAssemblyTested -or $result.ProductAssemblySha256 -ne $before){throw '실제 제품 결과 또는 고정 DLL 해시 불일치'}
$same=(Get-FileHash -LiteralPath $AssemblyPath).Hash -eq $before
$summary=[pscustomobject]@{Succeeded=$code -eq 0 -and $result.Succeeded -and $same;Passed=$result.Passed;Failed=$result.Failed;AssemblyPath=$AssemblyPath;AssemblySha256=$before;SourceOnly=$false;WindowShown=$false;ActualIdeTested=$false;ResultPath=$resultPath;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
$summary|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
$summary|ConvertTo-Json -Depth 5
if(!$summary.Succeeded){throw '실제 제품 테마 검사 실패: 원본 결과를 보존합니다.'}
