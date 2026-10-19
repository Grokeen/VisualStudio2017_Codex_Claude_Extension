#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ClaudeVerification
desc         : 고정 제품 DLL의 기존 Provider 검사와 Claude 비인증 초기화를 격리 실행한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : TASK-060 실제 인증 출력은 메모리에서만 처리하고 허용된 상태만 보존.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$ClaudePath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string[]]$SuiteNames=@('runtime','provider-cli','provider-options','claude-boundaries','native-cli')
)
$ErrorActionPreference='Stop'
$repoDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
$ClaudePath=(Resolve-Path -LiteralPath $ClaudePath).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$compilerPath=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$roslynPath='C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe'
$runs=@()
foreach($suite in @(
    @{Name='runtime'; Source='tests\task020\RuntimeChecks.cs'; Fixture='tests\task020\RuntimeProtocolFixture.cs'},
    @{Name='provider-cli'; Source='tests\provider-options\ProviderCliIntegrationTests.cs'},
    @{Name='provider-options'; Source='tests\provider-options\ProviderOptionsTests.cs'},
    @{Name='claude-boundaries'; Source='tests\task060\ClaudeBoundaryChecks.cs'},
    @{Name='native-cli'; Source='tests\task060\ClaudeNativeProbe.cs'}
)) {
    if($suite.Name -notin $SuiteNames){continue}
    $suiteDir=Join-Path $OutputDirectory $suite.Name
    $null=New-Item -ItemType Directory -Path $suiteDir -Force
    $dllPath=Join-Path $suiteDir 'VS2017.AIAssistant.dll'
    Copy-Item -LiteralPath $AssemblyPath -Destination $dllPath
    $exePath=Join-Path $suiteDir 'Checks.exe'
    $compiler=$compilerPath
    if($suite.Name -eq 'provider-options'){$compiler=$roslynPath}
    $compileArgs=@('/nologo','/target:exe','/codepage:65001',('/out:'+$exePath),('/reference:'+$dllPath),'/reference:System.Web.Extensions.dll',(Join-Path $repoDir $suite.Source))
    & $compiler @compileArgs
    if($LASTEXITCODE -ne 0){throw ('Compile failed: '+$suite.Name)}
    $runArgs=@($suiteDir)
    if($suite.Fixture){
        $fixturePath=Join-Path $suiteDir 'NativeProtocolFixture.exe'
        & $compilerPath /nologo /target:exe "/out:$fixturePath" /reference:System.Web.Extensions.dll (Join-Path $repoDir $suite.Fixture)
        if($LASTEXITCODE -ne 0){throw 'Fixture compile failed'}
        $runArgs=@($fixturePath,$suiteDir)
    }
    if($suite.Name -eq 'native-cli'){$runArgs=@($ClaudePath,$suiteDir)}
    & $exePath @runArgs 2>&1 | Tee-Object -FilePath (Join-Path $suiteDir 'results.log')
    $runExit=$LASTEXITCODE
    $runs+= [pscustomobject]@{Name=$suite.Name;ExitCode=$runExit;Result=(Join-Path $suiteDir 'result.json')}
    $runs | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'suites.json') -Encoding UTF8
    if($runExit -ne 0){throw ('Suite failed: '+$suite.Name)}
}
$signature=Get-AuthenticodeSignature -LiteralPath $ClaudePath
[pscustomobject]@{
    AssemblyPath=$AssemblyPath;AssemblySha256=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
    ClaudeVersionExpected='2.1.302';ClaudeSha256=(Get-FileHash -LiteralPath $ClaudePath -Algorithm SHA256).Hash
    ClaudeSignatureStatus=[string]$signature.Status;SignedByAnthropic=($signature.SignerCertificate.Subject -like '*Anthropic*')
    LiveInferencePerformed=$false;CredentialFilesReadByTest=$false;RawAuthOutputSaved=$false
    Suites=$runs
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
