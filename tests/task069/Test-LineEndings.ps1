param([string]$OutputDirectory,[string]$AssemblyPath)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repo ('artifacts/task069/line-endings-'+[guid]::NewGuid().ToString('N'))}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Preserve existing evidence.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory
$exe=Join-Path $OutputDirectory 'LineEndingChecks.exe'
$compiler='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$compileArgs=@('/nologo','/target:exe','/langversion:5','/reference:System.Core.dll',('/out:'+$exe),(Join-Path $PSScriptRoot 'LineEndingChecks.cs'))
if($AssemblyPath){$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path;$compileArgs+=@('/define:PRODUCT',('/reference:'+$AssemblyPath));Copy-Item -LiteralPath $AssemblyPath -Destination $OutputDirectory}
else{$compileArgs+=(Join-Path $repo 'src/VS2017.AIAssistant/Providers/AgentFileTracker.cs')}
& $compiler @compileArgs
if($LASTEXITCODE){throw 'Compile failed.'}
& $exe (Join-Path $OutputDirectory 'fixture')
if($LASTEXITCODE){throw 'Line ending contract failed.'}
if($AssemblyPath){[pscustomobject]@{ProductSourceCompiled=$false;AssemblyPath=$AssemblyPath;Sha256=(Get-FileHash -LiteralPath $AssemblyPath).Hash}|ConvertTo-Json|Set-Content (Join-Path $OutputDirectory 'assembly.json') -Encoding utf8}
