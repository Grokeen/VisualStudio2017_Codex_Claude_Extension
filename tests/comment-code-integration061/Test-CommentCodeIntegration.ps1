[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$CscPath,
    [Parameter(Mandatory=$true)][string]$EditorSdkAssembliesPath,
    [Parameter(Mandatory=$true)][string]$ComponentModelHostPath,
    [string]$NugetPackagesPath = (Join-Path $env:USERPROFILE '.nuget/packages')
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw '새 OutputDirectory가 필요합니다. 기존 증거는 덮어쓰지 않습니다.'}
$null=New-Item -ItemType Directory -Path $output
$source=Join-Path $repo 'src/VS2017.AIAssistant'
$core=Join-Path $repo 'src/CommentCodePreview.Poc'
$common=@((Join-Path $core 'EditorContract.cs'),(Join-Path $source 'Editing/DocumentSnapshot.cs'),(Join-Path $source 'Editing/EditProposal.cs'),(Join-Path $source 'Providers/CommentCodeProviderAdapter.cs'))
$inputs=@($common)+@((Join-Path $core 'PreviewSession.cs'),(Join-Path $source 'Services/CommentCodeEditorAdapter.cs'),(Join-Path $source 'Providers/CliEditProvider.cs'),(Join-Path $source 'Providers/ProcessJob.cs'),(Join-Path $source 'Providers/ProviderExecutionOptions.cs'),(Join-Path $source 'Views/AssistantControl.CommentCode.cs'),(Join-Path $source 'Views/CommentCodePreviewDialog.cs'))+@(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
$before=@($inputs | Sort-Object -Unique | ForEach-Object {[pscustomobject]@{Path=$_;SHA256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}})
$before | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'inputs.json') -Encoding utf8
function Compile([string]$name,[string[]]$files,[string[]]$references,[string]$entry) {
    $exe=Join-Path $output ($name+'.exe')
    $arguments=@('/nologo','/target:exe','/langversion:7','/r:System.Web.Extensions.dll',('/out:'+$exe))
    if($entry){$arguments+=('/main:'+$entry)}
    $arguments+=@($references | ForEach-Object {'/r:'+$_});$arguments+=$files
    $diagnostics=@(& $CscPath @arguments 2>&1);$exit=$LASTEXITCODE
    [pscustomobject]@{ExitCode=$exit;Diagnostics=$diagnostics} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output ($name+'-compile.json')) -Encoding utf8
    if($exit -ne 0){throw ($diagnostics -join "`n")}
    return $exe
}
function Run([string]$exe,[string[]]$runArguments,[string]$name,[int]$expected) {
    $lines=@(& $exe @runArguments 2>&1);$exit=$LASTEXITCODE
    $lines | Set-Content -LiteralPath (Join-Path $output ($name+'.log')) -Encoding utf8
    $passes=@($lines | Where-Object {"$_" -match '^PASS '}).Count
    if($exit -ne 0 -or $passes -ne $expected){throw "$name exit=$exit assertions=$passes/$expected"}
    return [pscustomobject]@{Name=$name;ExitCode=$exit;Passed=$passes;Expected=$expected}
}
$provider=Compile 'provider' (@((Join-Path $PSScriptRoot 'ProviderBoundaryFixture.cs'))+$common) @() ''
$results=@(Run $provider @() 'provider-boundary' 26)
$sdkNames=@('Microsoft.VisualStudio.CoreUtility','Microsoft.VisualStudio.Text.Data','Microsoft.VisualStudio.Text.Logic','Microsoft.VisualStudio.Text.UI','Microsoft.VisualStudio.Text.UI.Wpf','Microsoft.VisualStudio.Editor')
$references=@($sdkNames | ForEach-Object {Join-Path $EditorSdkAssembliesPath ($_+'.dll')})
$references+=@($ComponentModelHostPath)
foreach($relative in @('microsoft.visualstudio.shell.15.0/15.9.28307/lib/net45/Microsoft.VisualStudio.Shell.15.0.dll','microsoft.visualstudio.shell.framework/15.9.28307/lib/net45/Microsoft.VisualStudio.Shell.Framework.dll','envdte/8.0.2/lib/net10/EnvDTE.dll','microsoft.visualstudio.textmanager.interop/7.10.6071/lib/net11/Microsoft.VisualStudio.TextManager.Interop.dll','microsoft.visualstudio.textmanager.interop.8.0/8.0.50728/lib/net11/Microsoft.VisualStudio.TextManager.Interop.8.0.dll','microsoft.visualstudio.ole.interop/7.10.6071/lib/Microsoft.VisualStudio.OLE.Interop.dll','microsoft.visualstudio.shell.interop/7.10.6072/lib/net11/Microsoft.VisualStudio.Shell.Interop.dll','microsoft.visualstudio.threading/15.8.122/lib/net46/Microsoft.VisualStudio.Threading.dll')){$references+=Join-Path $NugetPackagesPath $relative}
foreach($name in @('WindowsBase','PresentationCore','PresentationFramework')){$references+=Join-Path $env:WINDIR ('Microsoft.NET/Framework64/v4.0.30319/WPF/'+$name+'.dll')}
$references | ForEach-Object {[pscustomobject]@{Path=$_;SHA256=(Get-FileHash -LiteralPath $_).Hash;Assembly=[Reflection.AssemblyName]::GetAssemblyName($_).FullName}} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'compile-references.json') -Encoding utf8
$uiFiles=@((Join-Path $PSScriptRoot 'ProviderBoundaryFixture.cs'),(Join-Path $PSScriptRoot 'UiBoundaryFixture.cs'))+$common+@((Join-Path $core 'PreviewSession.cs'),(Join-Path $source 'Services/CommentCodeEditorAdapter.cs'),(Join-Path $source 'Views/AssistantControl.CommentCode.cs'),(Join-Path $source 'Views/CommentCodePreviewDialog.cs'))
$ui=Compile 'ui' $uiFiles $references 'UiBoundaryFixture'
$results+=Run $ui @() 'ui-boundary' 14
$wireFiles=@((Join-Path $PSScriptRoot 'NativeWireFixture.cs'))+$common+@((Join-Path $source 'Providers/CliEditProvider.cs'),(Join-Path $source 'Providers/ProcessJob.cs'),(Join-Path $source 'Providers/ProviderExecutionOptions.cs'))
$wire=Compile 'wire' $wireFiles @() ''
$results+=Run $wire @('fixture-parent',(Join-Path $output 'wire-captures')) 'synthetic-wire' 28
foreach($item in $before){if((Get-FileHash -LiteralPath $item.Path).Hash -ne $item.SHA256){throw '검사 중 소스 입력 변경'}}
[pscustomobject]@{Passed=68;Expected=68;Suites=$results;SourceUnchanged=$true;ActualIDE=$false;ActualAI=$false;VendorInferenceCLIExecuted=$false;UIInstantiated=$false;ProductMSBuild=$false;SyntheticSubprocess=$true;SdkCompileOnly=$true;ShippingApproved=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $output 'result.json')
