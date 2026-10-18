#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ScmIdentity
desc         : SCM 이름 조회를 네이티브 fixture와 임시 Git 저장소에서 독립 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 공개 이름·로그인 확인 구분과 출력/취소 경계 검사.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$ProductAssemblyPath,[string]$OutputDirectory,[string]$CompilerPath,[string]$RealGitPath)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputDirectory) { $OutputDirectory=Join-Path $repositoryDir ('artifacts\task015\scm-identity-'+[guid]::NewGuid().ToString('N')) }
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if (!$CompilerPath) { $CompilerPath='C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe' }
if (!$RealGitPath) { $gitCommand=Get-Command git.exe -ErrorAction SilentlyContinue; if($gitCommand){$RealGitPath=$gitCommand.Source}else{$RealGitPath=''} }
$executable=Join-Path $OutputDirectory 'Scm Identity 한글.exe'
$arguments=@('/nologo','/target:exe','/langversion:7','/codepage:65001','/reference:System.Web.Extensions.dll',('/out:'+$executable),(Join-Path $PSScriptRoot 'ScmIdentityTests.cs'))
if ($ProductAssemblyPath) {
    $ProductAssemblyPath=(Resolve-Path -LiteralPath $ProductAssemblyPath).Path
    Copy-Item -LiteralPath $ProductAssemblyPath -Destination (Join-Path $OutputDirectory 'VS2017.AIAssistant.dll')
    $arguments+=('/reference:'+(Join-Path $OutputDirectory 'VS2017.AIAssistant.dll'))
} else {
    $arguments+=(Join-Path $repositoryDir 'src\VS2017.AIAssistant\Services\ScmIdentityService.cs')
    $arguments+=(Join-Path $repositoryDir 'src\VS2017.AIAssistant\Providers\ProviderExecutionOptions.cs')
    $arguments+=(Join-Path $repositoryDir 'src\VS2017.AIAssistant\Providers\ProcessJob.cs')
}
& $CompilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'SCM 이름 검사 컴파일 실패' }
& $executable $OutputDirectory $RealGitPath
if ($LASTEXITCODE -ne 0) { throw "SCM 이름 검사 실패: $OutputDirectory" }
