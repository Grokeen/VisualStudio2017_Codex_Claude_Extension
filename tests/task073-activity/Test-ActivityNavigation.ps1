#requires -Version 5.1
[CmdletBinding()]
param([string]$ProductSourceRoot,[string]$OutputDirectory,[ValidateSet('navigation','persist','persist-list')][string]$CaseGroup='navigation')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $ProductSourceRoot){$ProductSourceRoot=$repo}
$sourceRoot=[IO.Path]::GetFullPath($ProductSourceRoot)
$allowed=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts/task073-activity'))+[IO.Path]::DirectorySeparatorChar
if(-not $OutputDirectory){$OutputDirectory=$allowed+'n-'+[Guid]::NewGuid().ToString('N').Substring(0,12)}
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(-not $out.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Only the authorized artifact subtree is writable.'}
if(Test-Path -LiteralPath $out){throw 'Fresh output path required. Evidence is immutable.'}
[void][IO.Directory]::CreateDirectory($out)
$controller=Join-Path $sourceRoot 'src/VS2017.AIAssistant/Views/AssistantControl.ActivityFeed.cs'
$experience=Join-Path $sourceRoot 'src/VS2017.AIAssistant/Views/AssistantControl.Experience.cs'
$view=Join-Path $sourceRoot 'src/VS2017.AIAssistant/Views/AssistantControl.xaml.cs'
$requestQueue=Join-Path $sourceRoot 'src/VS2017.AIAssistant/Views/AssistantControl.RequestQueue.cs'
$chat=Join-Path $sourceRoot 'src/VS2017.AIAssistant/Chat/ChatWorkspace.cs'
$model=Join-Path $sourceRoot 'src/VS2017.AIAssistant/Chat/ConversationActivityFeed.cs'
$store=Join-Path $sourceRoot 'src/VS2017.AIAssistant/Chat/MarkdownWorkspaceStore.cs'
$committed=Join-Path $sourceRoot 'src/VS2017.AIAssistant/Chat/MarkdownWorkspaceStore.Committed.cs'
$fixture=Join-Path $PSScriptRoot 'ActivityNavigationContracts.cs'
$template=Join-Path $PSScriptRoot 'NavigationControllerHarness.template.cs'
$inputs=@($controller,$experience,$view,$requestQueue,$chat,$model,$store,$committed,$fixture,$template,$PSCommandPath)|ForEach-Object{[ordered]@{Path=$_;Sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}}
function Read-ExactMember([string]$source,[string]$signature) {
    $start=$source.IndexOf($signature,[StringComparison]::Ordinal)
    if($start -lt 0){throw ('Required member missing: '+$signature)}
    $next=$source.IndexOf('        /// ---------------------------------------------------------------------',$start,[StringComparison]::Ordinal)
    if($next -lt 0){$match=[regex]::Match($source.Substring($start),'(?m)^        \}');if(-not $match.Success){throw 'Member boundary missing.'};$next=$start+$match.Index+$match.Length}
    return $source.Substring($start,$next-$start).TrimEnd()
}
$controllerText=[IO.File]::ReadAllText($controller)
$members=@('private sealed class ActivityOwnershipIndex','private ActivityOwnershipIndex BuildActivityOwnershipIndex()','private bool TryResolveActivityOwner(','private bool TryMarkActivityConversationRead(','private static string ActivityKey(','private void SetActivitySaveFailure(','private bool TryOpenActivityConversation(','private void PreserveActivityPendingProposal(')|ForEach-Object{Read-ExactMember $controllerText $_}
$hasFailureGetter=$controllerText.Contains('private string GetActivityNavigationFailureMessage()')
if($hasFailureGetter){$members+=Read-ExactMember $controllerText 'private string GetActivityNavigationFailureMessage()'}
else{$members+='private string GetActivityNavigationFailureMessage() { return activityNavigationFailureMessage; }'}
foreach($signature in @('private bool PersistCurrentConversation()','private bool PersistConversation(','private void ConversationSelected(')){$members+=Read-ExactMember ([IO.File]::ReadAllText($experience)) $signature}
$members+=Read-ExactMember ([IO.File]::ReadAllText($view)) 'private void ProviderChanged('
$fingerprint=Read-ExactMember ([IO.File]::ReadAllText($requestQueue)) 'private static string ConversationFingerprint('
$members+=$fingerprint.Replace('private static string ConversationFingerprint(','private static string ExactConversationFingerprint(')
$harness=[IO.File]::ReadAllText($template).Replace('/* EXACT_MEMBERS */',($members -join "`r`n`r`n"))
[IO.File]::WriteAllText((Join-Path $out 'ExactNavigationController.cs'),$harness,[Text.UTF8Encoding]::new($true))
$chatText=[IO.File]::ReadAllText($chat)
$start=$chatText.IndexOf('public sealed class ChatMessage',[StringComparison]::Ordinal)
$nextName=$chatText.IndexOf('/// name         : ChatSession',$start,[StringComparison]::Ordinal)
$end=$chatText.LastIndexOf('    /// ---------------------------------------------------------------------',$nextName,[StringComparison]::Ordinal)
if($start -lt 0 -or $end -le $start){throw 'ChatMessage boundary missing.'}
[IO.File]::WriteAllText((Join-Path $out 'ExactChatMessage.cs'),('using System; namespace VS2017.AIAssistant.Chat {'+$chatText.Substring($start,$end-$start)+'}'),[Text.UTF8Encoding]::new($true))
foreach($path in @($model,$store,$committed,$fixture)){Copy-Item -LiteralPath $path -Destination $out}
$compiler=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$exe=Join-Path $out 'ActivityNavigationContracts.exe'
$compilerArguments=@('/nologo','/target:exe','/langversion:7','/warnaserror+',('/out:'+$exe),'/reference:System.Web.Extensions.dll')+@(Get-ChildItem -LiteralPath $out -Filter '*.cs'|ForEach-Object{$_.FullName})
& $compiler @compilerArguments 2>&1|Set-Content -LiteralPath (Join-Path $out 'compile.log') -Encoding UTF8
$compileExit=$LASTEXITCODE;$runExit=-1
$workspace=Join-Path $out 'w'
if($compileExit -eq 0){& $exe $workspace $CaseGroup 2>&1|Set-Content -LiteralPath (Join-Path $out 'contracts.log') -Encoding UTF8;$runExit=$LASTEXITCODE}
$lines=if(Test-Path -LiteralPath (Join-Path $out 'contracts.log')){@(Get-Content -LiteralPath (Join-Path $out 'contracts.log'))}else{@()}
$passed=@($lines|Where-Object{$_ -like 'PASS *'}).Count
$expected=if($CaseGroup -eq 'persist'){17}elseif($CaseGroup -eq 'persist-list'){3}else{23}
$unchanged=$true;foreach($inputRecord in $inputs){if((Get-FileHash -LiteralPath $inputRecord.Path -Algorithm SHA256).Hash -ne $inputRecord.Sha256){$unchanged=$false}}
$copies=@(Get-ChildItem -LiteralPath $out -Filter '*.cs'|ForEach-Object{[ordered]@{Name=$_.Name;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
$result=[ordered]@{Succeeded=($compileExit -eq 0 -and $runExit -eq 0 -and $passed -eq $expected -and $unchanged);CompileExit=$compileExit;RunExit=$runExit;Passed=$passed;ExpectedAssertions=$expected;CaseGroup=$CaseGroup;SourceRoot=$sourceRoot;InputsUnchanged=$unchanged;Inputs=$inputs;PrivateSourceCopies=$copies;Workspace=$workspace;ActualStoreSource=$true;ExactNavigationAndPersistMethodSubstrings=$true;FailureGetterFromProduct=$hasFailureGetter;FingerprintMethodBody='Exact source body, declaration renamed; instance wrapper injects a deterministic failure on one configured call';ActualWpf=$false;ActualProductDll=$false;ActualIde=$false;NativeProcesses=$false;Stubs=@('UI dispatcher/thread check and selection events','UI controls and ChatSession container','presentation/list refresh with explicit failure injection','native run/queue identity sentinels, not native runtime');UserWorkspaceTouched=$false;Gui=0;Cli=0;Network=0}
$result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
[pscustomobject]$result|Select-Object Succeeded,CompileExit,RunExit,Passed,ExpectedAssertions,InputsUnchanged|Format-List
Write-Output $out
if(-not $result.Succeeded){exit 1}
