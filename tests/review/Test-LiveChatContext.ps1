#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-LiveChatContext
desc         : 합성 이미지·텍스트·대화·instruction을 실제 Codex에 보내 멀티모달 컨텍스트를 독립 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 공유 빌드와 독립된 DLL 경로 및 합성 데이터만 사용.
-------------------------------------------------------------------------
.SYNOPSIS
    Explicit live Codex test. No user project, no IDE edits, no account UI automation.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$CodexPath
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Windows PowerShell 5.1 -STA로 실행하세요.' }
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$CodexPath = (Resolve-Path -LiteralPath $CodexPath).Path
foreach ($framework in @('WindowsBase','PresentationCore','PresentationFramework','System.Web.Extensions')) { Add-Type -AssemblyName $framework }
$assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
if ($assembly.GetName().Version -lt [version]'0.3.0.0') { throw 'v0.3.0 이상의 제품 DLL이 필요합니다.' }
$outputDir = Join-Path $repositoryDir ('artifacts\task009\live-context-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $outputDir -Force
$imagePath = Join-Path $outputDir 'SyntheticNumber.png'
$visual = New-Object Windows.Media.DrawingVisual
$drawing = $visual.RenderOpen()
$drawing.DrawRectangle([Windows.Media.Brushes]::White,$null,(New-Object Windows.Rect(0,0,160,160)))
$number = New-Object Windows.Media.FormattedText('7',[Globalization.CultureInfo]::InvariantCulture,[Windows.FlowDirection]::LeftToRight,(New-Object Windows.Media.Typeface('Segoe UI')),100,[Windows.Media.Brushes]::Black)
$drawing.DrawText($number,(New-Object Windows.Point(50,12)))
$drawing.Close()
$bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap(160,160,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($visual)
$encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$imageStream = [IO.File]::Create($imagePath)
try { $encoder.Save($imageStream) } finally { $imageStream.Dispose() }
$notePath = Join-Path $outputDir 'ReferenceNote.txt'
[IO.File]::WriteAllText($notePath,'Reference label: NOTE_OK. This is synthetic test data.',(New-Object Text.UTF8Encoding($false)))
$samplePath = Join-Path $outputDir 'Probe.cs'
$source = "// Synthetic chat-context test only.`r`nclass Probe { int GetValue() { return 1; } }`r`n"
[IO.File]::WriteAllText($samplePath,$source,(New-Object Text.UTF8Encoding($true)))
$snapshot = [VS2017.AIAssistant.Editing.DocumentSnapshot]::new($samplePath,$source,'synthetic-disk','C# 7.0 / .NET Framework 4.6.2',$false,$false)
$historyMessage = [VS2017.AIAssistant.Chat.ChatMessage]::new()
$historyMessage.Role = 'user'
$historyMessage.Text = 'For the next response include the history marker HISTORY_OK in the summary.'
$attachments = [VS2017.AIAssistant.Chat.ChatAttachment[]]@(
    [VS2017.AIAssistant.Chat.ChatAttachment]::FromFile($imagePath),
    [VS2017.AIAssistant.Chat.ChatAttachment]::FromFile($notePath)
)
$context = [VS2017.AIAssistant.Chat.ChatRequestContext]::new(
    'Include RULES_OK in the response summary. Preserve all source except the requested return statement.',
    '',[VS2017.AIAssistant.Chat.ChatMessage[]]@($historyMessage),$attachments)
$account = [VS2017.AIAssistant.Providers.CliAccountService]::new()
$connected = $account.CheckAsync('Codex',$CodexPath,[Threading.CancellationToken]::None).GetAwaiter().GetResult()
if (!$connected) { throw 'Codex CLI에 먼저 로그인하세요.' }
$provider = [VS2017.AIAssistant.Providers.CliEditProvider]::new([TimeSpan]::FromSeconds(90))
$request = 'Read the single digit drawn in the attached image. Change only the return 1; statement in GetValue to return that digit. Preserve all other text. Include the reference note label in the summary, and follow the prior conversation request and current user instructions.'
$proposal = $provider.GenerateWithContextAsync('Codex',$CodexPath,$snapshot,$request,$context,[Threading.CancellationToken]::None).GetAwaiter().GetResult()
$checks = [ordered]@{
    ExactImageBasedEdit = $proposal.ProposedText -ceq $source.Replace('return 1;','return 7;')
    RulesMarker = $proposal.Summary.Contains('RULES_OK')
    HistoryMarker = $proposal.Summary.Contains('HISTORY_OK')
    TextAttachmentMarker = $proposal.Summary.Contains('NOTE_OK')
    OriginalDiskUnchanged = [IO.File]::ReadAllText($samplePath) -ceq $source
    AccountConnected = $connected
}
$succeeded = @($checks.Values | Where-Object { !$_ }).Count -eq 0
$result = [pscustomobject]@{
    Succeeded=$succeeded; CheckKind='LiveCodexImageTextHistoryRules'; ProductVersion=$assembly.GetName().Version.ToString()
    AssemblySha256=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
    CodexVersion=(& $CodexPath --version | Out-String).Trim(); UserProjectSourceSent=$false; IdeRuntimeTest=$false
    Checks=$checks; Summary=$proposal.Summary; OutputDirectory=$outputDir; CheckedAt=(Get-Date).ToString('O')
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputDir 'result.json') -Encoding UTF8
$result | ConvertTo-Json -Depth 5
if (!$succeeded) { throw "멀티모달 컨텍스트 검증 실패: $outputDir" }
