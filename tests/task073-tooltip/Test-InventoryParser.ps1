#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-InventoryParser
desc         : Execute only the pinned real driver AST inventory seam and its fixed-input guard with anonymous PS5 JSON cases.
author       : CodexOrig
create date  : 2026-11-03
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop' -or $PSVersionTable.PSVersion.Major -ne 5){throw 'Run this narrow contract in Windows PowerShell 5.1.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowed=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts/task073-tooltip'))+[IO.Path]::DirectorySeparatorChar
if(!$OutputDirectory){$OutputDirectory=$allowed+'parser-'+[Guid]::NewGuid().ToString('N').Substring(0,8)}
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(!$out.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $out)){throw 'Fresh owned artifact nonce required; existing evidence is immutable.'}
for($part=$out;$part;$part=[IO.Path]::GetDirectoryName($part)){if((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Output links are not permitted.'}}
$driver=Join-Path $PSScriptRoot 'Test-KeyboardToolTip.ps1'
$expectedDriver='3B7ED7A26FA802798055B4C65541EDF54BFDFAAC27ED27DC89D6E1F7E727EEB1'
$driverBefore=(Get-FileHash -LiteralPath $driver -Algorithm SHA256).Hash
if($driverBefore -ne $expectedDriver){throw 'The reviewed driver source SHA changed; do not silently test another revision.'}
$selfBefore=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
$parseErrors=$null;$tokens=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($driver,[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count -ne 0){throw 'Pinned driver does not parse.'}
$assignments=@()
foreach($variable in @('decodedRecords','records')){
    $found=@($ast.FindAll({param($node) $node -is [System.Management.Automation.Language.AssignmentStatementAst] -and $node.Left -is [System.Management.Automation.Language.VariableExpressionAst] -and $node.Left.VariablePath.UserPath -ceq $variable},$true))
    if($found.Count -ne 1){throw 'The exact inventory assignment seam must be unique.'}
    $assignments+=$found[0]
}
if($assignments[0].Extent.EndLineNumber+1 -ne $assignments[1].Extent.StartLineNumber){throw 'The reviewed two-step seam is not adjacent and ordered.'}
$functions=@()
foreach($functionName in @('Get-Hash','No-Links','Fixed-Input')){
    $found=@($ast.FindAll({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $functionName},$true))
    if($found.Count -ne 1){throw 'The reviewed fixed-input function must be unique.'}
    $functions+=$found[0]
}
[void][IO.Directory]::CreateDirectory($out)
Copy-Item -LiteralPath $driver -Destination (Join-Path $out 'driver-source.ps1')
$seamText=($assignments|ForEach-Object{$_.Extent.Text}) -join "`r`n"
$functionText=($functions|ForEach-Object{$_.Extent.Text}) -join "`r`n"
[IO.File]::WriteAllText((Join-Path $out 'extracted-assignments.ps1'),$seamText,[Text.UTF8Encoding]::new($true))
[IO.File]::WriteAllText((Join-Path $out 'extracted-guard-functions.ps1'),$functionText,[Text.UTF8Encoding]::new($true))
$parseSeam=[ScriptBlock]::Create('param([string]$inventory)'+"`r`n"+$seamText+"`r`n"+'[pscustomobject]@{Count=$records.Count;Records=$records}')
. ([ScriptBlock]::Create($functionText))
$checks=New-Object 'System.Collections.Generic.List[object]'
$caseResults=New-Object 'System.Collections.Generic.List[object]'
$failureType=$null;$failureHResult=$null
function Assert-Contract([string]$Name,[bool]$Passed){$checks.Add([pscustomobject]@{Name=$Name;Passed=$Passed});if(!$Passed){throw ('Parser contract failed: '+$Name)}}
try{
    $data=@([ordered]@{RelativePath='synthetic/one.txt';Hash=('A'*64);Bytes=7},[ordered]@{RelativePath='synthetic/two.txt';Hash=('B'*64);Bytes=11},[ordered]@{RelativePath='synthetic/three.txt';Hash=('C'*64);Bytes=13})
    $cases=@([pscustomobject]@{Name='empty';Json='[]';Expected=@()},[pscustomobject]@{Name='one';Json=('['+(ConvertTo-Json -InputObject $data[0] -Compress)+']');Expected=@($data[0])},[pscustomobject]@{Name='three';Json=(ConvertTo-Json -InputObject $data -Compress);Expected=$data})
    foreach($case in $cases){
        $inventory=Join-Path $out ($case.Name+'.json');[IO.File]::WriteAllText($inventory,$case.Json,[Text.UTF8Encoding]::new($true))
        $parsed=& $parseSeam $inventory
        $expectedCount=@($case.Expected).Count
        Assert-Contract ($case.Name+' real seam count') ($parsed.Count -eq $expectedCount)
        $actualRows=@($parsed.Records);$fieldsMatch=$actualRows.Count -eq $expectedCount
        for($index=0;$index -lt $expectedCount;$index++){
            $row=$actualRows[$index];$expected=$case.Expected[$index]
            $fieldsMatch=$fieldsMatch -and $row.PSObject.Properties.Name -contains 'RelativePath' -and $row.PSObject.Properties.Name -contains 'Hash' -and $row.PSObject.Properties.Name -contains 'Bytes'
            $fieldsMatch=$fieldsMatch -and $row.RelativePath -ceq $expected.RelativePath -and $row.Hash -ceq $expected.Hash -and [long]$row.Bytes -eq [long]$expected.Bytes
        }
        Assert-Contract ($case.Name+' real seam field preservation') $fieldsMatch
        $caseResults.Add([pscustomobject]@{Case=$case.Name;ExpectedCount=$expectedCount;ActualCount=$parsed.Count;FieldMatch=$fieldsMatch;SyntheticJsonSha256=(Get-FileHash -LiteralPath $inventory).Hash})
    }
    $present=Join-Path $out 'anonymous-fixed-input.txt';[IO.File]::WriteAllText($present,'anonymous fixed input',[Text.UTF8Encoding]::new($false))
    $presentHash=Get-Hash $present;$guardOutput=@(Fixed-Input $present $presentHash)
    Assert-Contract 'actual fixed input accepts existing matching file' ($guardOutput.Count -eq 0 -and (Get-Hash $present) -eq $presentHash)
    $missing=Join-Path $out 'intentionally-absent.txt';$rejected=$false;$missingType=$null;$missingHResult=$null
    try{Fixed-Input $missing $presentHash}catch{$rejected=$true;$missingType=$_.Exception.GetType().FullName;$missingHResult=$_.Exception.HResult}
    Assert-Contract 'actual fixed input rejects missing file' ($rejected -and !(Test-Path -LiteralPath $missing))
    $caseResults.Add([pscustomobject]@{Case='missing-fixed-input';Rejected=$rejected;ExceptionType=$missingType;HResult=$missingHResult})
}catch{$failureType=$_.Exception.GetType().FullName;$failureHResult=$_.Exception.HResult}
$driverAfter=(Get-FileHash -LiteralPath $driver).Hash;$selfAfter=(Get-FileHash -LiteralPath $PSCommandPath).Hash
$copyHash=(Get-FileHash -LiteralPath (Join-Path $out 'driver-source.ps1')).Hash
$unchanged=$driverAfter -eq $expectedDriver -and $driverBefore -eq $driverAfter -and $copyHash -eq $expectedDriver -and $selfBefore -eq $selfAfter
$result=[ordered]@{
    Succeeded=($null -eq $failureType -and $checks.Count -eq 8 -and @($checks|Where-Object{-not $_.Passed}).Count -eq 0 -and $unchanged)
    Scope='Windows PowerShell JSON array seam and real fixed-input guard subset only'
    Passed=@($checks|Where-Object Passed).Count;Failed=@($checks|Where-Object{-not $_.Passed}).Count;ExpectedAssertions=8;Checks=$checks;Cases=$caseResults
    PowerShellVersion=$PSVersionTable.PSVersion.ToString();PSEdition=$PSVersionTable.PSEdition
    DriverSourceSha256=$expectedDriver;DriverBeforeSha256=$driverBefore;DriverAfterSha256=$driverAfter;CopiedDriverSha256=$copyHash;SelfSha256=$selfAfter;InputsUnchanged=$unchanged
    Assignments=@($assignments|ForEach-Object{[ordered]@{Variable=$_.Left.VariablePath.UserPath;StartLine=$_.Extent.StartLineNumber;EndLine=$_.Extent.EndLineNumber}})
    Functions=@($functions|ForEach-Object{[ordered]@{Name=$_.Name;StartLine=$_.Extent.StartLineNumber;EndLine=$_.Extent.EndLineNumber}})
    AssignmentExcerptSha256=(Get-FileHash -LiteralPath (Join-Path $out 'extracted-assignments.ps1')).Hash
    FunctionExcerptSha256=(Get-FileHash -LiteralPath (Join-Path $out 'extracted-guard-functions.ps1')).Hash
    FailureType=$failureType;FailureHResult=$failureHResult
    FullDriverExecuted=$false;FullPreflightTested=$false;ActualProductDll=$false;ProductAssemblyLoaded=$false;CompilerExecuted=$false;WindowShown=$false;ActualIde=$false;UserProfileOrSettingsTouched=$false;NetworkRequested=$false
    Limitation='Executes exact pinned AST extents, not a copied parser algorithm. No full candidate preflight, product UI, keyboard tooltip or IDE result is inferred.'
}
$result|ConvertTo-Json -Depth 9|Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
[pscustomobject]$result|Select-Object Succeeded,Passed,Failed,ExpectedAssertions,PowerShellVersion,InputsUnchanged|Format-List
Write-Output $out
if(!$result.Succeeded){exit 1}
