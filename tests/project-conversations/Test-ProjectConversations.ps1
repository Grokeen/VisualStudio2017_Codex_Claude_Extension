param([string]$OutputDirectory,[string]$ProductAssemblyPath,[string]$ExpectedProductSha256)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$sourceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $sourceRoot ('artifacts/project-conversations/r-'+[guid]::NewGuid().ToString('N').Substring(0,8))}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Fresh evidence directory required.'}
$out=[IO.Directory]::CreateDirectory($OutputDirectory).FullName
$utf8=New-Object Text.UTF8Encoding($false)
$actualProduct=![string]::IsNullOrWhiteSpace($ProductAssemblyPath)
if($actualProduct -ne (![string]::IsNullOrWhiteSpace($ExpectedProductSha256))){throw 'Product path and exact SHA must be supplied together.'}
$productBefore=$null
if($actualProduct){
 $ProductAssemblyPath=[IO.Path]::GetFullPath($ProductAssemblyPath)
 $productBefore=(Get-FileHash -LiteralPath $ProductAssemblyPath -Algorithm SHA256).Hash
 if($ExpectedProductSha256 -notmatch '^[a-fA-F0-9]{64}$' -or $productBefore -ine $ExpectedProductSha256){throw 'Frozen product SHA mismatch.'}
 Copy-Item -LiteralPath $ProductAssemblyPath -Destination (Join-Path $out 'VS2017.AIAssistant.dll')
 if((Get-FileHash -LiteralPath (Join-Path $out 'VS2017.AIAssistant.dll')).Hash -ine $productBefore){throw 'Product copy SHA mismatch.'}
}
$inputs=@('src/VS2017.AIAssistant/Services/ProjectConversationContext.cs','src/VS2017.AIAssistant/Views/AssistantControl.ProjectConversations.cs','src/VS2017.AIAssistant/Chat/MarkdownWorkspaceStore.cs','src/VS2017.AIAssistant/Chat/MarkdownWorkspaceStore.Committed.cs','src/VS2017.AIAssistant/Chat/ChatWorkspace.cs','tests/project-conversations/ProjectContracts.cs','tests/project-conversations/Test-ProjectConversations.ps1')
$before=@($inputs|ForEach-Object{$p=Join-Path $sourceRoot $_;[pscustomobject]@{Path=$_;Sha256=(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}})
$before|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $out 'source-before.json') -Encoding UTF8
foreach($relative in $inputs){Copy-Item -LiteralPath (Join-Path $sourceRoot $relative) -Destination (Join-Path $out ([IO.Path]::GetFileName($relative)))}
$view=[IO.File]::ReadAllText((Join-Path $out 'AssistantControl.ProjectConversations.cs'))
function Extract-Method([string]$text,[string]$signature){
 $start=$text.IndexOf($signature,[StringComparison]::Ordinal);if($start -lt 0){throw 'Missing source method.'}
 $open=$text.IndexOf('{',$start);$depth=1;$end=$open+1
 while($end -lt $text.Length -and $depth -gt 0){if($text[$end] -eq '{'){$depth++};if($text[$end] -eq '}'){$depth--};$end++}
 if($depth -ne 0){throw 'Invalid source method boundary.'};return $text.Substring($start,$end-$start)
}
$work=Extract-Method $view 'private bool HasProjectConversationWork()'
$maps=(Extract-Method $view 'private static Dictionary<TKey, TValue> CloneProjectMap<TKey, TValue>')+"`r`n"+(Extract-Method $view 'private static void RestoreProjectMap<TKey, TValue>')
$fixture=[IO.File]::ReadAllText((Join-Path $out 'ProjectContracts.cs')).Replace('/* EXACT_WORK_METHOD */',$work).Replace('/* EXACT_MAP_METHODS */',$maps)
[IO.File]::WriteAllText((Join-Path $out 'Fixture.cs'),$fixture,$utf8)
$chat=[IO.File]::ReadAllText((Join-Path $out 'ChatWorkspace.cs'))
# Class ends before the next XML header. Locate its closing braces by the same bounded method extractor.
$decl=Extract-Method $chat 'public sealed class ChatMessage'
[IO.File]::WriteAllText((Join-Path $out 'ChatMessage.cs'),"using System; using System.Linq; namespace VS2017.AIAssistant.Chat {`r`n"+$decl+"`r`n}",$utf8)
$csc='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$args=@('/nologo','/langversion:7','/define:PROJECT_CONVERSATION_CORE_TEST','/target:exe',('/out:'+ (Join-Path $out 'Contracts.exe')),'/r:System.Web.Extensions.dll',(Join-Path $out 'ProjectConversationContext.cs'),(Join-Path $out 'MarkdownWorkspaceStore.cs'),(Join-Path $out 'MarkdownWorkspaceStore.Committed.cs'),(Join-Path $out 'ChatMessage.cs'),(Join-Path $out 'Fixture.cs'))
if($actualProduct){$args=@('/nologo','/langversion:7','/target:exe',('/out:'+ (Join-Path $out 'Contracts.exe')),'/r:System.Web.Extensions.dll',('/r:'+(Join-Path $out 'VS2017.AIAssistant.dll')),(Join-Path $out 'Fixture.cs'))}
& $csc @args 2>&1|Out-File -LiteralPath (Join-Path $out 'csc.log') -Encoding UTF8
if($LASTEXITCODE -ne 0){throw 'Private source contract compile failed; original log retained.'}
$process=Start-Process -FilePath (Join-Path $out 'Contracts.exe') -ArgumentList ('"'+$out+'"') -PassThru -WindowStyle Hidden
if(!$process.WaitForExit(60000)){$process.Kill();throw 'Owned source contract process exceeded limit.'}
$after=@($inputs|ForEach-Object{$p=Join-Path $sourceRoot $_;[pscustomobject]@{Path=$_;Sha256=(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}})
$unchanged=($before|ConvertTo-Json -Compress) -ceq ($after|ConvertTo-Json -Compress)
if($actualProduct){$unchanged=$unchanged -and ((Get-FileHash -LiteralPath $ProductAssemblyPath).Hash -ieq $productBefore) -and ((Get-FileHash -LiteralPath (Join-Path $out 'VS2017.AIAssistant.dll')).Hash -ieq $productBefore)}
$rawResult=[IO.File]::ReadAllText((Join-Path $out 'contract-result.json'))|ConvertFrom-Json
$rawResult|Add-Member -NotePropertyName SourceOnly -NotePropertyValue (!$actualProduct)
$rawResult|Add-Member -NotePropertyName ActualStoreSource -NotePropertyValue (!$actualProduct)
$rawResult|Add-Member -NotePropertyName ActualProductAssembly -NotePropertyValue $actualProduct
$rawResult|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
$summary=[ordered]@{SourceRoot=$sourceRoot;SourceOnly=(!$actualProduct);ActualStoreSource=(!$actualProduct);ActualProductAssembly=$actualProduct;ProductSha256=$productBefore;DteAdapterExcluded=$true;WorkMethodExact=$true;WorkGuardIsSourceExcerpt=$true;MapMethodsExact=$true;UiStateRollbackExecuted=$false;CompilerExitCode=0;ExitCode=$process.ExitCode;SourceUnchanged=$unchanged;OwnedPid=$process.Id;NormalExit=$process.HasExited;Gui=$false;Input=$after}
$summary|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $out 'validation.json') -Encoding UTF8
Write-Output $out
if(!$unchanged -or $process.ExitCode -ne 0){throw 'Source contracts failed; evidence preserved.'}
