#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-QuestionFixtureData
desc         : 제안 질문 계약의 검사 데이터가 읽기 가능하고 서로 구분되는지만 확인한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : 제품 파서/호스트 미실행·컴파일 없음·로그인/실추론/GUI 없음.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$destination=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $destination){throw 'Preserve existing evidence; choose a new directory.'}
$null=New-Item -ItemType Directory -Path $destination
$path=Join-Path $PSScriptRoot 'QuestionProtocolCases.json'
$hash=(Get-FileHash -LiteralPath $path).Hash
$data=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json
if($data.SchemaVersion -ne 1 -or $data.ParserCases.Count -lt 30 -or $data.InteractionCases.Count -lt 10){throw 'Fixture data incomplete.'}
if(@($data.ParserCases|Group-Object Id|Where-Object Count -ne 1).Count){throw 'Parser case IDs must be unique.'}
if(@($data.InteractionCases|Group-Object Id|Where-Object Count -ne 1).Count){throw 'Interaction case IDs must be unique.'}
$validatedExamples=0
foreach($case in $data.ParserCases){
    if(!$case.Id -or !$case.SourceRole -or [string]::IsNullOrEmpty($case.RawText) -or !$case.Reason){throw 'Fixture case is incomplete.'}
    if($case.ExpectedQuestionCount -gt 0){
        $text=$case.RawText.Trim()
        $lines=$text -split '\r?\n'
        if($lines[0] -cne '```ask_user' -or $lines[$lines.Length-1] -cne '```'){throw 'Expected valid example must use the proposed fence.'}
        $json=[string]::Join("`n",$lines[1..($lines.Length-2)])
        if($json.Length -gt $data.MaximumPayloadCharacters){throw 'Valid example exceeds payload budget.'}
        $sample=$json|ConvertFrom-Json
        if($sample.version -ne 1 -or $sample.questions.Count -ne $case.ExpectedQuestionCount){throw 'Expected valid example and fixture count differ.'}
        $validatedExamples++
    }
}
if((Get-FileHash -LiteralPath $path).Hash -ne $hash){throw 'Fixture changed while read.'}
$result=[ordered]@{
    FixtureDataValidated=$true;ParserCases=$data.ParserCases.Count;InteractionCases=$data.InteractionCases.Count
    ValidJsonExamplesChecked=$validatedExamples;FixtureSha256=$hash
    ProductParserTested=$false;ProductUiTested=$false;ProductBuildRun=$false
    ContractAcceptedForImplementation=$false;RemoteAiUsed=$false;LoginAttempted=$false;GuiUsed=$false
    Limitation='Data readiness only. No proposed question parser, card, submit, persistence, or CLI behavior has been implemented or validated by this script.'
    CheckedAt=[DateTimeOffset]::Now.ToString('o')
}
$result|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $destination 'result.json') -Encoding UTF8
$result|ConvertTo-Json -Depth 4
