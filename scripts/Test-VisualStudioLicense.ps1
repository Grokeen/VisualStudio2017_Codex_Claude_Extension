#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-VisualStudioLicense
desc         : 실제 IDE 테스트의 라이선스 경고 관찰 근거를 버전별로 확인한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : TASK-020 MSBuild 성공과 IDE 라이선스 관찰을 구분.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$EvidenceRoot,[ValidateSet('2017','2019','2022')][string[]]$Versions=@('2017','2019','2022'))
$ErrorActionPreference='Stop'
if(!$EvidenceRoot){$EvidenceRoot=Join-Path $PSScriptRoot '..\artifacts\ide'}
$observations=@(Get-ChildItem -LiteralPath $EvidenceRoot -Filter 'license-observation.json' -Recurse -ErrorAction SilentlyContinue)
$rows=foreach($year in $Versions){
    $major=switch($year){'2017'{'15'}'2019'{'16'}'2022'{'17'}}
    $latest=$observations | Sort-Object LastWriteTime -Descending | ForEach-Object {
        $file=$_;try{$value=Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8|ConvertFrom-Json;if($value.IdeMajor -eq $major){[pscustomobject]@{Value=$value;Path=$file.FullName}}}catch{}
    } | Select-Object -First 1
    [pscustomobject]@{Version=$year;Inspected=($null -ne $latest -and $latest.Value.MainWindowInspected);WarningObserved=if($latest){$latest.Value.BlockingLicenseTextObserved}else{$null};Evidence=if($latest){$latest.Path}else{''}}
}
$rows | ConvertTo-Json -Depth 4
Write-Output '이 검사는 테스트 시점의 표시 상태만 확인합니다. 유효한 계정·구독의 인증이나 경고 영구 제거를 보장하지 않습니다.'
Write-Output '로그인/갱신이 필요한 경우 각 IDE의 파일 > 계정 설정에서 정식 계정으로 완료하세요.'
Write-Output '공식 안내: https://learn.microsoft.com/en-us/visualstudio/ide/how-to-unlock-visual-studio'
if(@($rows|Where-Object {!$_.Inspected -or $_.WarningObserved -eq $true}).Count -gt 0){exit 2}
exit 0
