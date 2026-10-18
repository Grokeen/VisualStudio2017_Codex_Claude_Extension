using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Editing;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : DiffReviewChecks
/// desc         : 줄 계수·줄 위치·원문 불변성과 실제 unified 변경 부호의 경계를 독립 확인한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : TASK-021 빈 파일·끝 개행·여러 교체의 표시 회귀 추가.
/// </summary>
/// ---------------------------------------------------------------------
internal static class DiffReviewChecks
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    private static readonly List<object> Results = new List<object>();
    private static string root;
    private static int passed, failed;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 18개 관찰 가능한 수정안 사례를 실행하고 재현 가능한 결과와 어셈블리 해시를 남긴다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 소스 검사와 실제 배포 DLL 검사에서 같은 사례 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        Case("CRLF 한 줄 전체 삭제의 카드·hunk 계수 일치", () => {
            var p = Parse("a\r\n", "a\r\n", "");
            Counts(p, 0, 1); Require(p.ProposedText == "" && p.FirstChangedLine == 1 && Lines(p).Contains("-a") && !Lines(p).Contains("-"));
        });
        Case("빈 파일에 끝 개행 있는 한 줄 삽입", () => {
            var p = Parse("", "", "a\r\n");
            Counts(p, 1, 0); Require(p.ProposedText == "a\n" && Lines(p).Contains("+a") && !Lines(p).Contains("+"));
        });
        Case("실제 마지막 빈 줄 하나의 삭제는 보존", () => {
            var p = Parse("x\n\n", "x\n\n", "x\n");
            Counts(p, 0, 1); Require(p.ProposedText == "x\n" && p.FirstChangedLine == 2 && Lines(p).Count(l => l == "-") == 1 && Lines(p).Contains(" x"));
        });
        Case("끝 빈 줄 있는 파일 전체 삭제는 두 줄", () => {
            var p = Parse("x\n\n", "x\n\n", "");
            Counts(p, 0, 2); Require(Lines(p).Contains("-x") && Lines(p).Count(l => l == "-") == 1);
        });
        Case("공통 앞뒤 문맥에는 변경 부호를 붙이지 않음", () => {
            var p = Parse("head\nold\ntail\n", "head\nold\ntail\n", "head\nnew\ntail\n");
            Counts(p, 1, 1); Require(p.FirstChangedLine == 2 && Lines(p).Contains(" head") && Lines(p).Contains(" tail") && !Lines(p).Contains("+head") && !Lines(p).Contains("-tail"));
        });
        Case("앞선 무변경 교체가 실제 최초 수정 줄을 가리지 않음", () => {
            var p = ParseMany("stay\nbetween\nold\n", "stay\n", "stay\n", "old", "new");
            Counts(p, 1, 1); Require(p.FirstChangedLine == 3 && HunkCount(p) == 1 && p.ProposedText == "stay\nbetween\nnew\n");
        });
        Case("실제 소스의 ---와 +++ 접두어를 비교 원문에 보존", () => {
            var p = Parse("-- old", "-- old", "++ new");
            Counts(p, 1, 1); Require(Lines(p).Contains("--- old") && Lines(p).Contains("+++ new") && Lines(p)[0].StartsWith("@@", StringComparison.Ordinal));
        });
        Case("역순 요청의 여러 hunk를 실제 원본 줄 순서로 표시", () => {
            var p = ParseMany("a\nalpha\nmiddle\nbeta\nend\n", "beta", "BETA", "alpha", "ALPHA");
            Require(p.AddedLines == 2 && p.RemovedLines == 2 && p.FirstChangedLine == 2 && HunkCount(p) == 2);
            Require(p.UnifiedChangePreview.IndexOf("@@ 원본 2줄", StringComparison.Ordinal) < p.UnifiedChangePreview.IndexOf("@@ 원본 4줄", StringComparison.Ordinal) && p.ProposedText == "a\nALPHA\nmiddle\nBETA\nend\n");
        });
        Case("AI LF 조각은 CRLF 파일 방식으로 정규화", () => {
            var p = Parse("a\r\nb\r\n", "a\nb", "a\nc");
            Counts(p, 1, 1); Require(p.ProposedText == "a\r\nc\r\n" && p.FirstChangedLine == 2 && Lines(p).Contains("-b") && Lines(p).Contains("+c"));
        });
        Case("EOF 개행만 추가한 변경은 별도 표식으로 표현", () => {
            var p = Parse("x", "x", "x\n");
            Counts(p, 0, 0); Require(p.HasChanges && p.ProposedText == "x\n" && p.UnifiedChangePreview.Contains("\\ 줄 끝 개행 추가"));
        });
        Case("EOF 개행만 제거한 변경은 별도 표식으로 표현", () => {
            var p = Parse("x\n", "x\n", "x");
            Counts(p, 0, 0); Require(p.HasChanges && p.ProposedText == "x" && p.UnifiedChangePreview.Contains("\\ 줄 끝 개행 제거"));
        });
        Case("같은 교체는 변경 카드 근거를 만들지 않음", () => {
            var p = Parse("same\n", "same\n", "same\n");
            Require(!p.HasChanges && p.AddedLines == 0 && p.RemovedLines == 0 && p.UnifiedChangePreview == "" && p.FirstChangedLine == 1);
        });
        Case("교체 없는 답변은 원본만 유지", () => {
            var source = Snapshot("그대로\r\n");
            var p = EditProposal.Parse(source, "{\"summary\":\"수정 없음\",\"edits\":[]}");
            Require(!p.HasChanges && p.UnifiedChangePreview == "" && p.ProposedText == source.Content && p.AddedLines == 0 && p.RemovedLines == 0);
        });
        Case("비교 생성은 디스크·전체 원문·선택·해시를 변경하지 않음", () => {
            string file = Path.Combine(root, "원문 불변.cs"), text = "// 앞\r\n\t한글😀 old\r\n// 뒤\r\n";
            File.WriteAllText(file, text, new UTF8Encoding(true)); byte[] diskBefore = File.ReadAllBytes(file);
            var selection = DocumentSelection.FromOffsets(text, text.IndexOf("old", StringComparison.Ordinal), 3);
            var source = new DocumentSnapshot(file, text, "whole-file-hash", "프로젝트 문맥", true, false, selection);
            var p = EditProposal.Parse(source, Plan("old", "new"));
            Require(ReferenceEquals(p.Source, source) && source.Content == text && source.DiskVersion == "whole-file-hash" && source.ProjectContext == "프로젝트 문맥" && source.HasUnsavedChanges && !source.IsReadOnly);
            Require(ReferenceEquals(source.Selection, selection) && source.Selection.Text == "old" && diskBefore.SequenceEqual(File.ReadAllBytes(file)) && p.ProposedText == text.Replace("old", "new"));
        });
        Case("문맥 사이 실제 두 줄 삭제는 한 hunk로 표시", () => {
            var p = Parse("head\nremove1\nremove2\ntail\n", "head\nremove1\nremove2\ntail\n", "head\ntail\n");
            Counts(p, 0, 2); Require(p.FirstChangedLine == 2 && Lines(p).Contains("-remove1") && Lines(p).Contains("-remove2") && Lines(p).Contains(" head") && Lines(p).Contains(" tail"));
        });
        Case("공통 뒷줄 앞의 삽입은 실제 추가 줄만 계산", () => {
            var p = Parse("abc", "abc", "first\nabc");
            Counts(p, 1, 0); Require(p.FirstChangedLine == 1 && Lines(p).Contains("+first") && Lines(p).Contains(" abc") && !Lines(p).Contains("+abc"));
        });
        Case("두 줄을 한 줄로 합치면 제거2·추가1 표시", () => {
            var p = Parse("a\nb", "a\nb", "ab");
            Counts(p, 1, 2); Require(Lines(p).Contains("-a") && Lines(p).Contains("-b") && Lines(p).Contains("+ab") && p.ProposedText == "ab");
        });
        Case("Unicode 부분 앵커와 다른 hunk의 앞뒤 원문 보존", () => {
            string text = "// 시작\n\t한글😀 old 값\nkeep\nlast\n";
            var p = ParseMany(text, "old", "new", "last", "final");
            Require(p.AddedLines == 2 && p.RemovedLines == 2 && p.FirstChangedLine == 2 && HunkCount(p) == 2 && p.Source.Content == text);
            Require(p.ProposedText == "// 시작\n\t한글😀 new 값\nkeep\nfinal\n" && Lines(p).Contains("-old") && Lines(p).Contains("+new") && Lines(p).Contains("-last") && Lines(p).Contains("+final"));
        });
        string assembly = typeof(EditProposal).Assembly.Location;
        File.WriteAllText(Path.Combine(root, "result.json"), Json.Serialize(new { Passed = passed, Failed = failed, Checks = Results, ProductAssembly = assembly, ProductSha256 = Hash(assembly) }), new UTF8Encoding(true));
        Console.WriteLine((failed == 0 ? "PASS " : "FAIL ") + passed + "/" + (passed + failed));
        return failed == 0 ? 0 : 1;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Snapshot
    /// desc         : 실제 사용자 파일과 분리된 고정 원문 스냅샷을 만든다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 모든 사례가 같은 전체 문서 계약을 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static DocumentSnapshot Snapshot(string text) { return new DocumentSnapshot(Path.Combine(root, "fixture.cs"), text, "disk-hash", "fixture", false, false); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Parse
    /// desc         : 단일 실제 문자열 교체를 생산 파서에 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 표시 알고리즘을 검사 코드에 복제하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static EditProposal Parse(string source, string before, string after) { return EditProposal.Parse(Snapshot(source), Plan(before, after)); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ParseMany
    /// desc         : 한 파일의 서로 다른 두 교체를 원래 요청 순서로 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 무변경 첫 교체와 역순 hunk 경계 확인.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static EditProposal ParseMany(string source, string before1, string after1, string before2, string after2)
    { return EditProposal.Parse(Snapshot(source), Json.Serialize(new { summary = "두 변경", edits = new[] { new { old_text = before1, new_text = after1 }, new { old_text = before2, new_text = after2 } } })); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Plan
    /// desc         : Unicode·개행을 안전한 JSON 문자열 값으로 직렬화한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 직접 이스케이프 오류 없이 실제 파서 입력 구성.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Plan(string before, string after) { return Json.Serialize(new { summary = "변경 비교", edits = new[] { new { old_text = before, new_text = after } } }); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Lines
    /// desc         : 비교 문자열의 관찰 가능한 행을 운영체제 개행과 무관하게 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 변경·문맥 부호의 실제 출력 확인에 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string[] Lines(EditProposal proposal) { return proposal.UnifiedChangePreview.Replace("\r\n", "\n").Split('\n'); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : HunkCount
    /// desc         : 출력된 비교 구역 수만 관찰해 여러 교체의 경계를 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 소스의 부호 문자를 hunk로 오인하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int HunkCount(EditProposal proposal) { return Lines(proposal).Count(line => line.StartsWith("@@ 원본 ", StringComparison.Ordinal)); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Counts
    /// desc         : 카드 수치와 단일 hunk의 공개 계수가 같은 기대값을 보여주는지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 phantom 행 회귀를 카드·상세 양쪽에서 차단.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Counts(EditProposal proposal, int added, int removed)
    { Require(proposal.AddedLines == added && proposal.RemovedLines == removed && HunkCount(proposal) == 1 && Lines(proposal).Single(line => line.StartsWith("@@ 원본 ", StringComparison.Ordinal)).Contains("줄 · 제거 " + removed + "줄 / 추가 " + added + "줄 @@")); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Require
    /// desc         : 사례의 관찰 결과가 다를 때 즉시 해당 사례를 실패시킨다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 나머지 사례는 계속 실행하여 원인 범위 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("기대한 원문·계수·비교 출력과 다릅니다."); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Case
    /// desc         : 한 의미 있는 회귀의 성공 또는 실제 예외를 결과에 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실패 사례 이름·예외와 전체 exit code 제공.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Case(string name, Action action)
    {
        try { action(); passed++; Results.Add(new { Name = name, Passed = true, Error = "" }); Console.WriteLine("PASS: " + name); }
        catch (Exception error) { failed++; Results.Add(new { Name = name, Passed = false, Error = error.ToString() }); Console.WriteLine("FAIL: " + name + " " + error.Message); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Hash
    /// desc         : 실제 실행된 생산 코드 어셈블리의 SHA-256을 결과에 고정한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 다른 후보 DLL과 결과 혼동 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Hash(string path) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
}
