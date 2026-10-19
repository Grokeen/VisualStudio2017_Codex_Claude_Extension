#requires -Version 5.1
<#
name         : Test-NotificationContent
desc         : 고정 제품 또는 명시적 source host의 요청 요약·실제 완료 수와 native 구조체 ABI를 검사한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : 실제 OS 알림/계정 호출 없이 privacy·UTF16·32/64bit 계약을 검증.
#>
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$outputPath=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $outputPath) -and @(Get-ChildItem -LiteralPath $outputPath -Force).Count){throw 'Preserve existing evidence.'}
$null=New-Item -ItemType Directory -Path $outputPath -Force
foreach($framework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Windows.Forms','System.Drawing')){Add-Type -AssemblyName $framework}
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath).Path)
$content=$assembly.GetType('VS2017.AIAssistant.Views.CompletionNotificationContent',$true)
$static=[Reflection.BindingFlags]'Static,NonPublic'
$titleMethod=$content.GetMethod('SafeRequestTitle',$static)
$bodyMethod=$content.GetMethod('Body',$static)
$passed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Check([string]$name,[bool]$condition){if($condition){$passed.Add($name)}else{$failed.Add($name)}}
function Title([AllowEmptyString()][string]$requestText){return $titleMethod.Invoke($null,[object[]]@($requestText))}
try{
    Check 'Free-form task title is excluded' ((Title '부가가치세 계산 화면 추가') -eq 'AI 요청')
    Check 'Empty task falls back without exposing another draft' ((Title '') -eq 'AI 요청')
    Check 'Control and bidi text is excluded' ((Title "보고서`r`n작성`t$([char]0x202e)완료") -eq 'AI 요청')
    foreach($sample in @('직원 홍길동의 연봉 9000만원 보고서를 검토해줘','내부프로젝트 비밀계약 1억원','고객 김하나 주민등록번호 보고서')){
        Check 'Names amounts and internal project text excluded' ((Title $sample) -eq 'AI 요청')
    }
    foreach($sample in @('token=secretValue','PASSWORD: secretValue','api_key="secretValue"','비밀번호=secretValue')){
        Check ('Credential value hidden: '+($sample -split '[:=]')[0]) (!(Title $sample).Contains('secretValue'))
    }
    foreach($sample in @('https://name:pass@example.test/path?token=xyz','C:\customer\private\file.cs','\\server\share\secret.txt','sample@example.test')){
        Check 'Address or identity hidden' (!(Title $sample).Contains($sample))
    }
    Check 'Long token hidden' (!(Title ('sk-'+('a'*80))).Contains('a'*20))
    $emoji=[Char]::ConvertFromUtf32(0x1F680)
    Check 'Emoji task text excluded' ((Title ('작업 '+$emoji+' 완료')) -eq 'AI 요청')
    $boundary=Title (('가'*71)+$emoji+'끝')
    Check 'Truncation keeps UTF16 pairs intact' ($boundary.Length -le 73 -and ![Char]::IsHighSurrogate($boundary[$boundary.Length-2]))
    $watch=[Diagnostics.Stopwatch]::StartNew();$bounded=Title ('x'*1000000);$watch.Stop()
    Check 'Input work is bounded before regex scanning' ($watch.ElapsedMilliseconds -lt 1500 -and $bounded.Length -le 73)
    $noticeType=$assembly.GetType('VS2017.AIAssistant.Views.ProgressNotice',$true)
    $notice=[Activator]::CreateInstance($noticeType)
    $apply=$noticeType.GetMethod('Apply')
    $null=$apply.Invoke($notice,[object[]]@('file','f1','completed','',0))
    $null=$apply.Invoke($notice,[object[]]@('command','c1','failed','',1))
    $null=$apply.Invoke($notice,[object[]]@('tool','t1','started','',$null))
    $body=$bodyMethod.Invoke($null,[object[]]@('주간 보고서 작성',$notice))
    Check 'Request subject never reaches OS body' (!$body.Contains('주간 보고서 작성') -and !$body.Contains('요청:'))
    Check 'Only reported completed work is counted' ($body.Contains('파일 1') -and !$body.Contains('명령') -and !$body.Contains('조회·도구'))
    $emptyNotice=[Activator]::CreateInstance($noticeType)
    $emptyBody=$bodyMethod.Invoke($null,[object[]]@('설명 요청',$emptyNotice))
    Check 'No work events means answer confirmation, not fake counts' ($emptyBody.Contains('답변을 확인') -and !$emptyBody.Contains('보고된 완료'))
    $proposalBody=$bodyMethod.Invoke($null,[object[]]@('proposal',$emptyNotice))
    Check 'Known result categories distinguish answer and prepared proposal' ($proposalBody.Contains('수정안 준비') -and $emptyBody.Contains('질문 답변') -and $proposalBody -cne $emptyBody)
    Check 'Completed file category derives from reported work not prompt text' ($body.Contains('파일 작업 완료 보고'))
    Check 'Shell text buffers remain bounded' ($body.Length -lt 256 -and $emptyBody.Length -lt 256)
    foreach($sample in @('직원 홍길동의 연봉 9000만원 보고서를 검토해줘','비공개사업 프로젝트','C:\private\secret.txt','token=SecretValue',('가'*1000000))){
        $privateBody=$bodyMethod.Invoke($null,[object[]]@($sample,$notice))
        Check 'Arbitrary request does not alter structured completion summary' ($privateBody -ceq $body)
    }
    $native=$assembly.GetType('VS2017.AIAssistant.Views.BrandedCompletionNotification',$true)
    $data=$native.GetNestedType('NotificationData',[Reflection.BindingFlags]::NonPublic)
    $size=[Runtime.InteropServices.Marshal]::SizeOf([Type]$data)
    Check 'NOTIFYICONDATA matches native pointer-size ABI' ($size -eq $(if([IntPtr]::Size -eq 8){976}else{956}))
}
catch{$failed.Add($_.Exception.ToString())}
$result=[ordered]@{Succeeded=$failed.Count -eq 0;Passed=$passed.Count;Failed=$failed.Count;Checks=$passed;Errors=$failed;AssemblySha256=(Get-FileHash -LiteralPath $AssemblyPath).Hash;PointerBytes=[IntPtr]::Size;OsNotificationShown=$false;LiveAiUsed=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
$result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $outputPath 'result.json') -Encoding UTF8
$result|ConvertTo-Json -Depth 5
if($failed.Count){exit 1}
exit 0
