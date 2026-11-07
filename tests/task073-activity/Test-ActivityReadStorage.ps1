#requires -Version 5.1
[CmdletBinding()]
param([string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowed=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts/task073-activity'))+[IO.Path]::DirectorySeparatorChar
if(-not $OutputDirectory){$OutputDirectory=$allowed+'s-'+[Guid]::NewGuid().ToString('N').Substring(0,12)}
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(-not $out.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Only the authorized task073-activity artifact subtree is writable.'}
if(Test-Path -LiteralPath $out){throw 'Fresh output path required; evidence cannot be overwritten.'}
[void][IO.Directory]::CreateDirectory($out)
$controller=Join-Path $repo 'src/VS2017.AIAssistant/Views/AssistantControl.ActivityFeed.cs'
$requestQueue=Join-Path $repo 'src/VS2017.AIAssistant/Views/AssistantControl.RequestQueue.cs'
$chat=Join-Path $repo 'src/VS2017.AIAssistant/Chat/ChatWorkspace.cs'
$model=Join-Path $repo 'src/VS2017.AIAssistant/Chat/ConversationActivityFeed.cs'
$store=Join-Path $repo 'src/VS2017.AIAssistant/Chat/MarkdownWorkspaceStore.cs'
$committed=Join-Path $repo 'src/VS2017.AIAssistant/Chat/MarkdownWorkspaceStore.Committed.cs'
$fixture=Join-Path $PSScriptRoot 'ActivityReadStorageContracts.cs'
$template=Join-Path $PSScriptRoot 'ReadControllerHarness.template.cs'
$inputs=@($controller,$requestQueue,$chat,$model,$store,$committed,$fixture,$template)|ForEach-Object{[ordered]@{Path=$_;Sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}}
function Read-ExactMember([string]$source,[string]$signature) {
    $start=$source.IndexOf($signature,[StringComparison]::Ordinal)
    if($start -lt 0){throw ('Required current member is missing: '+$signature)}
    $next=$source.IndexOf('        /// ---------------------------------------------------------------------',$start,[StringComparison]::Ordinal)
    if($next -lt 0){$match=[regex]::Match($source.Substring($start),'(?m)^        \}');if(-not $match.Success){throw 'Member boundary missing.'};$next=$start+$match.Index+$match.Length}
    return $source.Substring($start,$next-$start).TrimEnd()
}
$controllerText=[IO.File]::ReadAllText($controller)
$members=@('private sealed class ActivityOwnershipIndex','private ActivityOwnershipIndex BuildActivityOwnershipIndex()','private bool TryResolveActivityOwner(','private bool TryMarkActivityConversationRead(','private static string ActivityKey(','private void SetActivitySaveFailure(')|ForEach-Object{Read-ExactMember $controllerText $_}
$members+=Read-ExactMember ([IO.File]::ReadAllText($requestQueue)) 'private static string ConversationFingerprint('
$harness=[IO.File]::ReadAllText($template).Replace('/* EXACT_MEMBERS */',($members -join "`r`n`r`n"))
[IO.File]::WriteAllText((Join-Path $out 'ExactReadController.cs'),$harness,[Text.UTF8Encoding]::new($true))
$chatText=[IO.File]::ReadAllText($chat)
$start=$chatText.IndexOf('public sealed class ChatMessage',[StringComparison]::Ordinal)
$nextName=$chatText.IndexOf('/// name         : ChatSession',$start,[StringComparison]::Ordinal)
$end=$chatText.LastIndexOf('    /// ---------------------------------------------------------------------',$nextName,[StringComparison]::Ordinal)
if($start -lt 0 -or $end -le $start){throw 'ChatMessage boundary could not be determined.'}
[IO.File]::WriteAllText((Join-Path $out 'ExactChatMessage.cs'),('using System; namespace VS2017.AIAssistant.Chat {'+$chatText.Substring($start,$end-$start)+'}'),[Text.UTF8Encoding]::new($true))
foreach($path in @($model,$store,$committed,$fixture)){Copy-Item -LiteralPath $path -Destination $out}
$compiler=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$exe=Join-Path $out 'ActivityReadStorageContracts.exe'
$arguments=@('/nologo','/target:exe','/langversion:7','/warnaserror+',('/out:'+$exe),'/reference:System.Web.Extensions.dll')+@(Get-ChildItem -LiteralPath $out -Filter '*.cs'|ForEach-Object{$_.FullName})
& $compiler @arguments 2>&1|Set-Content -LiteralPath (Join-Path $out 'compile.log') -Encoding UTF8
$compileExit=$LASTEXITCODE;$runExit=-1
$workspace=Join-Path $out 'w'
if($compileExit -eq 0){& $exe $workspace 2>&1|Set-Content -LiteralPath (Join-Path $out 'contracts.log') -Encoding UTF8;$runExit=$LASTEXITCODE}
$lines=if(Test-Path -LiteralPath (Join-Path $out 'contracts.log')){@(Get-Content -LiteralPath (Join-Path $out 'contracts.log'))}else{@()}
$passed=@($lines|Where-Object{$_ -like 'PASS *'}).Count
$unchanged=$true;foreach($inputRecord in $inputs){if((Get-FileHash -LiteralPath $inputRecord.Path -Algorithm SHA256).Hash -ne $inputRecord.Sha256){$unchanged=$false}}
$copies=@(Get-ChildItem -LiteralPath $out -Filter '*.cs'|ForEach-Object{[ordered]@{Name=$_.Name;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
$result=[ordered]@{Succeeded=($compileExit -eq 0 -and $runExit -eq 0 -and $passed -eq 42 -and $unchanged);CompileExit=$compileExit;RunExit=$runExit;Passed=$passed;ExpectedAssertions=42;InputsUnchanged=$unchanged;Inputs=$inputs;PrivateSourceCopies=$copies;Workspace=$workspace;ActualStoreSource=$true;ExactControllerMethodSubstrings=$true;ActualWpf=$false;ActualProductDll=$false;ActualIde=$false;Stubs=@('Dispatcher.VerifyAccess thread-boundary check','RefreshActivityFeedBadge counter','surrounding non-UI container fields');UserWorkspaceTouched=$false;Gui=0;Cli=0;Network=0}
$result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
[pscustomobject]$result|Select-Object Succeeded,CompileExit,RunExit,Passed,InputsUnchanged|Format-List
Write-Output $out
if(-not $result.Succeeded){exit 1}
