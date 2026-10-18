#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-HelpContent
desc         : 상세 도움말을 실제 소스 또는 고정 제품 DLL로 독립 컴파일하여 검사한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 공유 bin·obj와 실제 계정에 접근하지 않는 콘텐츠 검사.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath,[string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repositoryDir ('artifacts/task021/help-content-'+[guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$referenceDir='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$executable=Join-Path $OutputDirectory 'HelpContentChecks.exe'
$compilerArgs=@('/nologo','/target:exe','/langversion:7','/codepage:65001','/nostdlib+',('/out:'+$executable),('/lib:'+$referenceDir),'/reference:mscorlib.dll','/reference:System.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll','/reference:WindowsBase.dll','/reference:PresentationCore.dll','/reference:PresentationFramework.dll','/reference:System.Xaml.dll',(Join-Path $PSScriptRoot 'HelpContentChecks.cs'))
if($AssemblyPath){
    $AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
    Copy-Item -LiteralPath $AssemblyPath -Destination (Join-Path $OutputDirectory 'VS2017.AIAssistant.dll')
    $compilerArgs+=('/reference:'+(Join-Path $OutputDirectory 'VS2017.AIAssistant.dll'))
}else{
    $hostStub=@'
using System;
using System.Windows.Controls;
namespace VS2017.AIAssistant.Views { public sealed class AssistantControl : UserControl { } }
namespace Microsoft.VisualStudio.Shell { public static class VsBrushes { public static readonly object ToolWindowBackgroundKey="DialogBackground"; public static readonly object ToolWindowTextKey="DialogForeground"; } public static class ThreadHelper { public static bool CheckAccess(){return false;} } public static class Package { public static object GetGlobalService(Type type){return null;} } }
namespace Microsoft.VisualStudio.Shell.Interop { public sealed class SVsUIShell { } public interface IVsUIShell { int GetDialogOwnerHwnd(out IntPtr handle); } }
'@
    $stubPath=Join-Path $OutputDirectory 'HostStub.cs'
    [IO.File]::WriteAllText($stubPath,$hostStub,(New-Object Text.UTF8Encoding($false)))
    $compilerArgs+=@($stubPath,(Join-Path $repositoryDir 'src/VS2017.AIAssistant/Views/AssistantDialogs.cs'),(Join-Path $repositoryDir 'src/VS2017.AIAssistant/Views/AssistantHelpContent.cs'))
    $assets=Get-Content -Encoding UTF8 -LiteralPath (Join-Path $repositoryDir 'src/VS2017.AIAssistant/obj/project.assets.json') -Raw | ConvertFrom-Json
    $packageRoot=$assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    $threadingEntry=$assets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties | Where-Object {$_.Name -like 'Microsoft.VisualStudio.Threading/*'} | Select-Object -First 1
    $threadingRelative=$threadingEntry.Value.compile.PSObject.Properties.Name | Where-Object {$_ -like '*/Microsoft.VisualStudio.Threading.dll'} | Select-Object -First 1
    $threadingPath=Join-Path $packageRoot ($assets.libraries.($threadingEntry.Name).path+'/'+$threadingRelative)
    $compilerArgs+=('/reference:'+$threadingPath)
    Copy-Item -LiteralPath $threadingPath -Destination (Join-Path $OutputDirectory 'Microsoft.VisualStudio.Threading.dll')
}
& $compiler @compilerArgs
if($LASTEXITCODE -ne 0){throw '상세 도움말 독립 컴파일 실패'}
& $executable $OutputDirectory
if($LASTEXITCODE -ne 0){throw "상세 도움말 검사 실패: $OutputDirectory"}
