#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ObservedSchema
desc         : 실제 관측한 thread/turn 필드를 설치 CLI가 생성한 로컬 schema와 읽기 비교한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : TASK069 전체 Draft07 검증과 필드 계약 검증 범위를 구분한다.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ResultPath,
    [Parameter(Mandatory=$true)][string]$SchemaDirectory,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputPath){throw 'Preserve previous schema evidence.'}
$result=Get-Content -LiteralPath $ResultPath -Raw -Encoding UTF8|ConvertFrom-Json
$checks=@();$schemaRecords=@()
foreach($observation in $result.Observations|Where-Object {$_.PSObject.Properties.Name -contains 'ThreadStart'}){
    foreach($kind in @('ThreadStart','TurnStart')){
        $value=$observation.$kind
        $schemaFile=Join-Path $SchemaDirectory ('v2/'+$kind+'Params.json')
        $schema=Get-Content -LiteralPath $schemaFile -Raw -Encoding UTF8|ConvertFrom-Json
        $schemaRecords+=[ordered]@{Name=$kind;Sha256=(Get-FileHash -LiteralPath $schemaFile).Hash}
        foreach($required in $schema.required){if($value.PSObject.Properties.Name -notcontains $required){throw 'Actual wire misses required schema field.'}}
        foreach($property in $value.PSObject.Properties){
            $fieldSchema=$schema.properties.($property.Name)
            if(!$fieldSchema){throw ('Wire field absent from local schema: '+$property.Name)}
            $allowed=@($fieldSchema.type)
            if($property.Name -eq 'effort'){
                if($fieldSchema.anyOf[0].'$ref' -ne '#/definitions/ReasoningEffort' -or $schema.definitions.ReasoningEffort.type -ne 'string' -or $property.Value -isnot [string] -or $property.Value.Length -lt $schema.definitions.ReasoningEffort.minLength){throw 'Actual effort differs from local schema.'}
            }elseif($property.Name -eq 'approvalPolicy'){
                if($schema.definitions.AskForApproval.oneOf[0].enum -cnotcontains $property.Value){throw 'Actual approval policy differs from local schema.'}
            }elseif($property.Name -eq 'sandbox'){
                if($schema.definitions.SandboxMode.enum -cnotcontains $property.Value){throw 'Actual sandbox differs from local schema.'}
            }elseif($null -eq $property.Value){
                if($allowed -notcontains 'null'){throw 'Unexpected null wire value.'}
            }elseif($allowed -contains 'string'){
                if($property.Value -isnot [string]){throw 'Actual wire string type differs.'}
            }elseif($allowed -contains 'boolean'){
                if($property.Value -isnot [bool]){throw 'Actual wire boolean type differs.'}
            }elseif($allowed -contains 'array'){
                if($property.Value -isnot [array]){throw 'Actual wire array type differs.'}
                foreach($input in $property.Value){if($input.type -cne 'text' -or $input.text -isnot [string]){throw 'Unexpected text input shape.'}}
            }else{throw ('Unchecked local schema type: '+$property.Name)}
            $checks+=[ordered]@{Scenario=$observation.Scenario;Method=$kind;Field=$property.Name;Passed=$true}
        }
    }
}
if($checks.Count -ne 20){throw 'Expected the two observed Codex scenarios and their 20 thread/turn fields.'}
$evidence=[ordered]@{ProductAssemblySha256=$result.ProductAssemblySha256;InputResultSha256=(Get-FileHash -LiteralPath $ResultPath).Hash;SchemaRecords=$schemaRecords;ActualWireFieldChecks=$checks.Count;Passed=$checks.Count;Failed=0;Checks=$checks;FullDraft07ValidatorUsed=$false;Scope='Observed required fields, property existence, simple types, effort ref/minLength, approval/sandbox enums and text input shape.';LoginAttempted=$false;InferenceInvoked=$false;NetworkRequested=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
$evidence|ConvertTo-Json -Depth 9|Set-Content -LiteralPath $OutputPath -Encoding UTF8
$evidence|ConvertTo-Json -Depth 3
