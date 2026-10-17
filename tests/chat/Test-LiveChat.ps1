#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-LiveChat
desc         : 실제 Codex에 합성 이미지·텍스트·대화·instruction을 보내 수정안을 검증한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : 사용자 소스 없이 이미지 인수와 컨텍스트의 실제 동작 확인.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$CodexPath)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Windows PowerShell 5.1 -STA로 실행하세요.' }
Add-Type -AssemblyName PresentationCore, WindowsBase, PresentationFramework, System.Web.Extensions
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$null = [Reflection.Assembly]::LoadFrom((Join-Path $repository 'src\VS2017.AIAssistant\bin\Release\VS2017.AIAssistant.dll'))
$output = Join-Path $repository ('artifacts\chat\live-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $output -Force
$imagePath = Join-Path $output '합성 이미지 두 사각형.png'
$visual = New-Object Windows.Media.DrawingVisual
$drawing = $visual.RenderOpen()
$drawing.DrawRectangle([Windows.Media.Brushes]::White, $null, (New-Object Windows.Rect(0,0,200,120)))
$drawing.DrawRectangle([Windows.Media.Brushes]::Red, $null, (New-Object Windows.Rect(20,30,50,50)))
$drawing.DrawRectangle([Windows.Media.Brushes]::Red, $null, (New-Object Windows.Rect(115,30,50,50)))
$drawing.Close()
$bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap(200,120,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($visual)
$encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$file = [IO.File]::Create($imagePath)
try { $encoder.Save($file) } finally { $file.Dispose() }
$textPath = Join-Path $output '추가 조건.txt'
[IO.File]::WriteAllText($textPath, '이미지의 빨간 사각형 개수만 반환값에 반영하고 다른 코드는 유지한다.', (New-Object Text.UTF8Encoding($true)))
$sourcePath = Join-Path $output 'Probe.cs'
$source = "// Synthetic image and chat test only.`r`nclass Probe { int GetValue() { return 1; } }`r`n"
[IO.File]::WriteAllText($sourcePath, $source, (New-Object Text.UTF8Encoding($true)))
$snapshot = [VS2017.AIAssistant.Editing.DocumentSnapshot]::new($sourcePath,$source,'synthetic','C# 7.0 / .NET Framework 4.6.2',$false,$false)
$history = [VS2017.AIAssistant.Chat.ChatMessage[]]@(
    ([VS2017.AIAssistant.Chat.ChatMessage]@{Role='user';Text='이전 합의: 이미지의 빨간 사각형 수를 GetValue 메서드의 반환값으로 사용한다.'}),
    ([VS2017.AIAssistant.Chat.ChatMessage]@{Role='assistant';Text='네, GetValue의 숫자 리터럴만 변경하겠습니다.'})
)
$attachments = [VS2017.AIAssistant.Chat.ChatAttachment[]]@([VS2017.AIAssistant.Chat.ChatAttachment]::FromFile($imagePath),[VS2017.AIAssistant.Chat.ChatAttachment]::FromFile($textPath))
$context = [VS2017.AIAssistant.Chat.ChatRequestContext]::new('한국어로 답하고 summary에 규칙확인 문구를 포함한다. 사용자 승인 전 파일을 변경하지 않는다.','',$history,$attachments)
$provider = [VS2017.AIAssistant.Providers.CliEditProvider]::new([TimeSpan]::FromSeconds(90))
$connected = [VS2017.AIAssistant.Providers.CliAccountService]::new().CheckAsync('Codex',$CodexPath,[Threading.CancellationToken]::None).GetAwaiter().GetResult()
if (-not $connected) { throw 'Codex 로그인이 필요합니다.' }
$proposal = $provider.GenerateWithContextAsync('Codex',$CodexPath,$snapshot,'이전 대화와 첨부 이미지 및 텍스트 조건에 따라 수정안을 만들어 주세요. 반환값 외의 모든 텍스트를 보존하세요.',$context,[Threading.CancellationToken]::None).GetAwaiter().GetResult()
$expected = $source.Replace('return 1;', 'return 2;')
$result = [pscustomobject]@{
    CheckedAt=[DateTimeOffset]::Now.ToString('o'); Provider='Codex'; LoggedIn=$connected
    CorrectImageDerivedEdit=($proposal.ProposedText -eq $expected); InstructionsObserved=$proposal.Summary.Contains('규칙확인')
    OriginalDiskUnchanged=([IO.File]::ReadAllText($sourcePath) -eq $source); UserProjectSourceSent=$false
    Summary=$proposal.Summary; ImagePath=$imagePath; IdeRuntimeTest=$false
}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
$result | ConvertTo-Json
if (-not $result.CorrectImageDerivedEdit -or -not $result.InstructionsObserved -or -not $result.OriginalDiskUnchanged) { throw "실제 컨텍스트 검사 실패: $output" }
Write-Output "PASS: 실제 Codex 이미지/대화/instruction 수정안. 결과: $output"
