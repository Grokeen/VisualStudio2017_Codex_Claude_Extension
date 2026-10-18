using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Editing;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : SelectionContextChecks
/// desc         : 선택 원문과 범위·줄·열 및 전체 파일 승인·저장 기준을 독립 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 감사 F06의 선택 문맥과 기존 편집 안전성 회귀 추가.
/// </summary>
/// ---------------------------------------------------------------------
internal static class SelectionContextChecks
{
    private static int passed;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 사용자 문서가 아닌 별도 디렉터리의 Unicode·줄바꿈 fixture로 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 전체 해시와 적용·저장·되돌리기 기준도 함께 확인.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        try
        {
            string content = "앞부분\r\n\t한글😀 코드\r\n마지막\n";
            var legacy = new DocumentSnapshot(Path.Combine(root, "기존.cs"), content, "disk-version", "context", true, false);
            Check(!legacy.HasSelection && legacy.Selection == null && legacy.SelectionError == "", "기존 여섯 인수 스냅샷은 선택 없는 전체 문서로 호환");
            Check(typeof(DocumentSnapshot).GetConstructor(new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool), typeof(bool) }) != null, "이미 컴파일된 소비자를 위한 여섯 인수 생성자 바이너리 계약 보존");
            legacy.RequireValidSelection(); Check(legacy.Content == content && legacy.DiskVersion == "disk-version", "선택 없는 요청의 전체 원문·디스크 기준 보존");
            int start = content.IndexOf('\t'), end = content.IndexOf("마지막", StringComparison.Ordinal);
            var selected = DocumentSelection.FromOffsets(content, start, end - start);
            Check(selected.Text == "\t한글😀 코드\r\n" && selected.StartOffset == start && selected.EndOffset == end && selected.Length == end - start, "선택 원문과 0기준 UTF-16 종료 미포함 범위 정확성");
            Check(selected.StartLine == 2 && selected.StartColumn == 1 && selected.EndLine == 3 && selected.EndColumn == 1, "CRLF 다음 줄의 1기준 시작·종료 위치");
            var reversed = DocumentSelection.FromEndpoints(content, end, start);
            Check(reversed.Text == selected.Text && reversed.StartOffset == selected.StartOffset && reversed.EndOffset == selected.EndOffset, "역방향 선택을 같은 연속 원문으로 정규화");
            var snapshot = new DocumentSnapshot(legacy.FilePath, content, legacy.DiskVersion, legacy.ProjectContext, true, false, selected);
            snapshot.RequireValidSelection(); Check(snapshot.HasSelection && snapshot.Content == content && snapshot.Selection.Text == selected.Text, "선택 코드는 전체 버퍼를 대체하지 않는 별도 문맥");
            var tab = DocumentSelection.FromOffsets(content, start + 1, 2);
            Check(tab.Text == "한글" && tab.StartColumn == 2 && tab.EndColumn == 4, "탭은 화면 폭이 아닌 문자 한 칸으로 열 계산");
            var emoji = DocumentSelection.FromOffsets(content, start + 3, 2);
            Check(emoji.Text == "😀" && emoji.StartColumn == 4 && emoji.EndColumn == 6, "Unicode surrogate 두 단위와 한글 원문 보존");
            var entire = DocumentSelection.FromOffsets(content, 0, content.Length);
            Check(entire.Text == content && entire.StartLine == 1 && entire.StartColumn == 1 && entire.EndLine == 4 && entire.EndColumn == 1, "전체 선택의 마지막 빈 줄과 종료 위치 정확성");
            Check(DocumentSelection.FromOffsets(content, content.Length, 0) == null && DocumentSelection.FromEndpoints(content, start, start) == null, "빈 선택과 문서 끝 caret은 선택 없음");
            Check(DocumentSelection.FromOffsets("", 0, 0) == null, "빈 문서는 선택 없는 기존 요청으로 처리");
            var mixed = DocumentSelection.FromOffsets("a\rb\nc\r\nd", 5, 3);
            Check(mixed.Text == "\r\nd" && mixed.StartLine == 3 && mixed.StartColumn == 2 && mixed.EndLine == 4 && mixed.EndColumn == 2, "CR·LF·CRLF 혼합 줄바꿈 위치 보존");
            Throws<ArgumentOutOfRangeException>(() => DocumentSelection.FromOffsets(content, -1, 2), "음수 시작 위치 거부");
            Throws<ArgumentOutOfRangeException>(() => DocumentSelection.FromOffsets(content, 0, -1), "음수 선택 길이 거부");
            Throws<ArgumentOutOfRangeException>(() => DocumentSelection.FromOffsets(content, content.Length + 1, 0), "문서 밖 시작 위치 거부");
            Throws<ArgumentOutOfRangeException>(() => DocumentSelection.FromOffsets(content, 1, Int32.MaxValue), "큰 길이와 정수 오버플로 범위 거부");
            Throws<ArgumentOutOfRangeException>(() => DocumentSelection.FromEndpoints(content, Int32.MaxValue, 0), "역방향 정규화 전 잘못된 끝점 거부");
            Throws<ArgumentNullException>(() => DocumentSelection.FromOffsets(null, 0, 0), "전체 원문 누락 거부");
            Throws<ArgumentException>(() => DocumentSelection.FromOffsets(content, content.IndexOf('\n'), 1), "CRLF 중간 시작 경계 거부");
            Throws<ArgumentException>(() => DocumentSelection.FromOffsets(content, 0, content.IndexOf('\n')), "CRLF 중간 종료 경계 거부");
            Throws<ArgumentException>(() => DocumentSelection.FromOffsets(content, start + 4, 1), "Unicode 문자 중간 시작 경계 거부");
            Throws<ArgumentException>(() => DocumentSelection.FromOffsets(content, start + 3, 1), "Unicode 문자 중간 종료 경계 거부");
            Throws<ArgumentException>(() => new DocumentSnapshot(legacy.FilePath, content.Replace("한글", "다른"), "disk-version", "", false, false, selected), "다른 버퍼의 선택 원문을 문서에 연결하지 않음");
            var sameTextDifferentLines = DocumentSelection.FromOffsets("xxsame", 2, 4);
            Throws<ArgumentException>(() => new DocumentSnapshot(legacy.FilePath, "\r\nsame", "v", "", false, false, sameTextDifferentLines), "동일 부분 문자열이라도 줄 정보가 다른 선택은 거부");
            var badSelection = new DocumentSnapshot(legacy.FilePath, content, "disk-version", "context", false, false, selectionError: "사각형 선택은 지원하지 않습니다.");
            Throws<InvalidOperationException>(() => badSelection.RequireValidSelection(), "지원하지 않는 선택 상태는 요청 시작에 명확히 거부");
            Check(!badSelection.HasSelection && badSelection.Content == content && badSelection.SelectionError.Contains("사각형"), "추출 오류 상태에서도 전체 버퍼와 충돌 기준 보존");
            Throws<ArgumentException>(() => new DocumentSnapshot(legacy.FilePath, content, "v", "", false, false, selected, "실패"), "선택 원문과 추출 오류의 모순 상태 거부");
            Throws<ArgumentException>(() => new DocumentSnapshot(legacy.FilePath, content, "v", "", false, false, selectionError: new string('x', 1025)), "선택 오류 메시지 크기 제한");
            snapshot.RequireUnchanged(legacy); Check(true, "선택 해제는 전체 문서 불변 검사를 실패시키지 않음");
            snapshot.RequireUnchanged(badSelection); Check(true, "선택 모드 변경은 적용·저장 충돌 검사를 막지 않음");
            Throws<InvalidOperationException>(() => snapshot.RequireUnchanged(new DocumentSnapshot(legacy.FilePath, content + "수정", "disk-version", "", false, false)), "선택 밖 버퍼 수정도 기존 전체 원문 충돌 검사로 거부");
            Throws<InvalidOperationException>(() => snapshot.RequireUnchanged(new DocumentSnapshot(legacy.FilePath, content, "different-disk", "", false, false)), "선택과 무관한 외부 디스크 수정도 계속 거부");
            Throws<InvalidOperationException>(() => snapshot.RequireUnchanged(new DocumentSnapshot(legacy.FilePath, content, "disk-version", "", false, true)), "선택과 무관한 읽기 전용 상태도 계속 거부");
            var doc = new FixtureDocument(Path.Combine(root, "전체 적용.cs"), "// 앞부분\r\n\treturn 1;\r\n// 뒷부분\r\n");
            doc.Start = doc.Buffer.IndexOf("return", StringComparison.Ordinal); doc.Length = "return 1;".Length;
            var before = doc.Capture(); byte[] originalBytes = File.ReadAllBytes(doc.FilePath);
            var proposal = EditProposal.Parse(before, "{\"summary\":\"선택 코드 수정\",\"edits\":[{\"old_text\":\"return 1;\",\"new_text\":\"return 2;\"}]}");
            Check(proposal.Source.HasSelection && proposal.Source.Content == doc.Buffer && proposal.ProposedText == doc.Buffer.Replace("return 1;", "return 2;"), "수정안은 선택 원문이 아닌 전체 스냅샷을 적용 기준으로 유지");
            doc.Start = 0; doc.Length = 0; var session = new SourceEditSession(); session.Apply(doc, proposal, true);
            Check(doc.Buffer.Contains("return 2;") && doc.Buffer.Contains("앞부분") && doc.Buffer.Contains("뒷부분") && Convert.ToBase64String(File.ReadAllBytes(doc.FilePath)) == Convert.ToBase64String(originalBytes), "승인 후 전체 버퍼의 앞뒤 문맥 유지·자동 저장 없음");
            doc.SelectionError = "선택 모드가 바뀌었습니다."; session.Save();
            Check(File.ReadAllText(doc.FilePath) == proposal.ProposedText, "승인 후 선택 모드 변경은 명시적 저장을 막지 않음");
            session.Restore(true); Check(doc.Buffer == before.Content && File.ReadAllText(doc.FilePath) == proposal.ProposedText, "선택 문맥과 무관하게 전체 원본 버퍼 복원·디스크 저장 분리");
            File.WriteAllText(Path.Combine(root, "result.json"), new JavaScriptSerializer().Serialize(new { Passed = passed, Failed = 0, ProductAssembly = typeof(DocumentSnapshot).Assembly.Location }), Encoding.UTF8);
            Console.WriteLine("PASS " + passed + "/" + passed); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); File.WriteAllText(Path.Combine(root, "result.json"), new JavaScriptSerializer().Serialize(new { Passed = passed, Failed = 1, Error = error.ToString() }), Encoding.UTF8); return 1; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FixtureDocument
    /// desc         : 실제 임시 파일 해시와 메모리 버퍼를 분리한 최소 편집 어댑터.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 선택 모드 변경 후 적용·저장·복원 회귀 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class FixtureDocument : IEditorDocument
    {
        public string FilePath, Buffer, SelectionError = "";
        public int Start, Length;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FixtureDocument
        /// desc         : 검사 디렉터리의 합성 원문을 디스크와 버퍼에 동일하게 준비한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 사용자 소스는 열거나 쓰지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public FixtureDocument(string path, string content) { FilePath = path; Buffer = content; File.WriteAllText(path, content, new UTF8Encoding(true)); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Capture
        /// desc         : 실제 파일 바이트 해시와 현재 버퍼의 선택 문맥을 독립 고정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 선택 오류가 있어도 승인 충돌 확인용 캡처는 허용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot Capture()
        {
            string version; using (var hash = SHA256.Create()) version = Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(FilePath)));
            return new DocumentSnapshot(FilePath, Buffer, version, "fixture", Buffer != File.ReadAllText(FilePath), false, SelectionError.Length == 0 ? DocumentSelection.FromOffsets(Buffer, Start, Length) : null, SelectionError);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Replace
        /// desc         : 전체 스냅샷이 같은 경우만 메모리 버퍼를 교체한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 파일 저장은 이 메서드에서 수행하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Replace(DocumentSnapshot expected, string content) { expected.RequireUnchanged(Capture()); Buffer = content; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Save
        /// desc         : 명시 저장 요청에만 전체 버퍼를 합성 파일에 기록한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 저장 전 전체 버퍼·디스크 버전 일치 검사 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Save(DocumentSnapshot expected) { expected.RequireUnchanged(Capture()); File.WriteAllText(FilePath, Buffer, new UTF8Encoding(true)); }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 선택 문맥과 파일 상태의 관찰 결과를 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실패 즉시 원인을 보존하여 중단.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Throws
    /// desc         : 잘못된 선택과 충돌을 기대한 오류 형식으로 거부하는지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 뜻밖의 성공은 검사 실패 처리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Throws<T>(Action action, string name) where T : Exception { try { action(); } catch (T) { Check(true, name); return; } throw new Exception("FAIL expected " + typeof(T).Name + ": " + name); }
}
