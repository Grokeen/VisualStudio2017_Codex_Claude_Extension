#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-Choices
desc         : 실제 ChatMessage 선택지 getter만 추출해 일반 목록 오인을 독립 검사한다.
author       : CodexCode
create date  : 2026-10-20
update date  : 2026-10-20 CodexCode : 감사 A68-02의 selected 완료 설명 오인 회귀 검사.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$source = [IO.File]::ReadAllText((Join-Path $root 'src\VS2017.AIAssistant\Chat\ChatWorkspace.cs'))
$start = $source.IndexOf('public sealed class ChatMessage', [StringComparison]::Ordinal)
if ($start -lt 0) { throw 'ChatMessage class missing.' }
$open = $source.IndexOf('{', $start)
$depth = 0; $end = -1
for ($index = $open; $index -lt $source.Length; $index++) {
    if ($source[$index] -eq '{') { $depth++ }
    elseif ($source[$index] -eq '}') { $depth--; if ($depth -eq 0) { $end = $index + 1; break } }
}
if ($end -lt 0) { throw 'ChatMessage class boundary missing.' }
$actual = $source.Substring($start, $end - $start)
$directory = Join-Path $root ('artifacts\task067\choice-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
$program = @'
internal static class ChoiceChecks {
    private static int passed;
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; }
    private static string[] Options(string role, string value) { return new VS2017.AIAssistant.Chat.ChatMessage { Role = role, Text = value }.Choices; }
    private static int Main() {
        try {
            Check(Options("assistant", "원하는 방법을 선택해 주세요.\n1. 기존 UI 유지\n2. 새 화면 추가").Length == 2, "Korean choice request");
            Check(Options("assistant", "Choose one:\n1. Keep UI\n2. Add page").Length == 2, "English choice request");
            Check(Options("assistant", "Please select:\n1. Keep UI\n2. Add page").Length == 2, "English select request");
            Check(Options("assistant", "어느 것으로 진행할까요?\n1. 유지\n2. 변경").Length == 2, "Korean question request");
            Check(Options("assistant", "선택한 파일을 검사했습니다.\n1. 빌드\n2. 테스트").Length == 0, "ordinary numbered summary");
            Check(Options("assistant", "The selected file has been checked.\n1. build\n2. test").Length == 0, "selected is not a choice verb");
            Check(Options("assistant", "선택해 주세요.\n```text\n1. 코드\n2. 예제").Length == 0, "open code fence");
            Check(Options("assistant", "선택해 주세요.\n1. 첫째\n1. 둘째").Length == 0, "duplicate numbers");
            Check(Options("assistant", "선택해 주세요.\n4. 넷째\n2. 둘째").Length == 0, "out of order numbers");
            Check(Options("user", "선택해 주세요.\n1. 첫째\n2. 둘째").Length == 0, "user text is not a choice card");
            Console.WriteLine("PASS " + passed + "/" + passed); return 0;
        } catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }
}
'@
$program = $program.Replace('\"', '"')
$text = "using System;`nnamespace VS2017.AIAssistant.Chat {`n" + $actual + "`n}`n" + $program
$cs = Join-Path $directory 'ChoiceChecks.cs'; $exe = Join-Path $directory 'ChoiceChecks.exe'
[IO.File]::WriteAllText($cs, $text, [Text.UTF8Encoding]::new($true))
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe'
& $compiler /nologo /langversion:7 /target:exe ('/out:' + $exe) $cs
if ($LASTEXITCODE -ne 0) { throw 'Choice fixture compilation failed.' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Choice fixture failed.' }
Write-Output ('SourceSHA256=' + (Get-FileHash (Join-Path $root 'src\VS2017.AIAssistant\Chat\ChatWorkspace.cs')).Hash)
Write-Output ('Evidence=' + $directory)
