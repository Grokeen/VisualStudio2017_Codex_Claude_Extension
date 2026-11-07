#requires -Version 5.1
[CmdletBinding()]
param([string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $OutputDirectory){$OutputDirectory=Join-Path $repo ('artifacts/task073-activity/model-'+[Guid]::NewGuid().ToString('N'))}
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out){throw 'OutputDirectory already exists. Evidence must not be overwritten.'}
[void][IO.Directory]::CreateDirectory($out)
$model=Join-Path $repo 'src/VS2017.AIAssistant/Chat/ConversationActivityFeed.cs'
$store=Join-Path $repo 'src/VS2017.AIAssistant/Chat/MarkdownWorkspaceStore.cs'
$fixture=Join-Path $PSScriptRoot 'ActivityFeedContracts.cs'
$inputs=@($model,$store,$fixture) | ForEach-Object { [ordered]@{Path=$_;Sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash} }
Copy-Item -LiteralPath $model -Destination (Join-Path $out 'ConversationActivityFeed.cs')
Copy-Item -LiteralPath $fixture -Destination (Join-Path $out 'ActivityFeedContracts.cs')
$enum=[regex]::Match([IO.File]::ReadAllText($store),'public enum ConversationRunState\s*\{[^}]+\}')
if(-not $enum.Success){throw 'Exact persisted enum declaration could not be extracted.'}
[IO.File]::WriteAllText((Join-Path $out 'PersistedEnum.cs'),('namespace VS2017.AIAssistant.Chat {'+$enum.Value+'}'),[Text.UTF8Encoding]::new($true))
$compiler=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$exe=Join-Path $out 'ActivityFeedContracts.exe'
$compilerArguments=@('/nologo','/target:exe','/langversion:7','/warnaserror+',('/out:'+$exe),(Join-Path $out 'ConversationActivityFeed.cs'),(Join-Path $out 'PersistedEnum.cs'),(Join-Path $out 'ActivityFeedContracts.cs'))
& $compiler @compilerArguments 2>&1 | Set-Content -LiteralPath (Join-Path $out 'compile.log') -Encoding UTF8
$compileExit=$LASTEXITCODE
$runExit=-1
if($compileExit -eq 0){& $exe 2>&1 | Set-Content -LiteralPath (Join-Path $out 'contracts.log') -Encoding UTF8;$runExit=$LASTEXITCODE}
$lines=if(Test-Path -LiteralPath (Join-Path $out 'contracts.log')){@(Get-Content -LiteralPath (Join-Path $out 'contracts.log'))}else{@()}
$passed=@($lines|Where-Object{$_ -like 'PASS *'}).Count
$unchanged=$true
foreach($inputRecord in $inputs){if((Get-FileHash -LiteralPath $inputRecord.Path -Algorithm SHA256).Hash -ne $inputRecord.Sha256){$unchanged=$false}}
$result=[ordered]@{Succeeded=($compileExit -eq 0 -and $runExit -eq 0 -and $passed -eq 55 -and $unchanged);CompileExit=$compileExit;RunExit=$runExit;Passed=$passed;ExpectedAssertions=55;InputsUnchanged=$unchanged;Inputs=$inputs;SourceCompiled=$true;ActualProductDll=$false;ActualControllerRuntime=$false;ActualIde=$false;StoreExecuted=$false;Gui=0;Cli=0;Network=0}
$result|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
[pscustomobject]$result|Select-Object Succeeded,CompileExit,RunExit,Passed,InputsUnchanged|Format-List
Write-Output $out
if(-not $result.Succeeded){exit 1}
