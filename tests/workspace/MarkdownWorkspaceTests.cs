using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : MarkdownWorkspaceTests
/// desc         : Markdown 원문 왕복·닫기·다시 열기·공통 규칙과 비정상 경로를 독립 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 사용자 환경을 쓰지 않는 임시 작업공간 검사 추가.
///                2026-10-20 CodexCode : 시간별 기록 분할·스크롤 복원·중복 없는 재저장 검증.
///                2026-10-20 CodexOrig : TASK068 오래된 저장자는 거절하고 원문·초안을 보존한 뒤 최신 기록을 닫는 계약 검증.
/// </summary>
/// ---------------------------------------------------------------------
internal static class MarkdownWorkspaceTests
{
    private static int passed;
    private static string output;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 독립 테스트 디렉터리에만 기록하고 검사 결과와 원문 Markdown을 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 파일 시스템의 원자 교체와 오류 복원까지 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        try
        {
            var store = new MarkdownWorkspaceStore(Path.Combine(output, "workspace"));
            Check(store.LoadOpen("Codex").Length == 0, "없는 작업공간은 빈 대화 목록");
            Check(store.LoadInstructions() == "", "없는 공통 instruction은 빈 문자열");
            var first = store.Create("Codex");
            Check(first.Id.Length == 32 && first.IsOpen && File.Exists(store.GetPath(first)), "새 대화는 고유 파일로 즉시 저장");
            Check(store.LoadOpen("Codex").Single().Id == first.Id, "새 인스턴스 복원을 위한 열림 상태 저장");
            var other = store.Create("Claude Code");
            Check(other.Provider == "Claude" && store.LoadOpen("Claude").Length == 1 && store.LoadOpen("Codex").Length == 1, "AI별 대화 파일과 표시 이름 분리");
            string question = "한글 요청 😀\r\n---\n```csharp\nreturn \"hello\";\n```\n<!-- assistant-message-end -->\n\n## 작성 중인 요청\n\n<!-- assistant-draft chars=0 -->\n\u0001 끝";
            string answer = "# 답변\n\n- **강조**\n- [링크](https://example.test)\n\n~~~\n---\n~~~\r\n마지막 개행\n";
            string draft = "\n초안\r\n<!-- assistant-draft-end -->\n😀";
            first.Messages.Add(new ChatMessage { Role = "user", Text = question });
            first.Messages.Add(new ChatMessage { Role = "assistant", Text = answer });
            first.Messages.Add(new ChatMessage { Role = "system", Text = "안내: 계정 연결 정보가 아니라 사용자 안내" });
            first.Draft = draft;
            store.Save(first);
            var restored = new MarkdownWorkspaceStore(store.RootPath).LoadOpen("Codex").Single();
            Check(restored.Messages.Count == 3 && restored.Messages[0].Text == question && restored.Messages[1].Text == answer, "구분자·펜스·Unicode·개행을 포함한 원문 메시지 정확한 왕복");
            Check(restored.Draft == draft, "초안의 선행 개행과 종료 표식도 정확한 왕복");
            Check(restored.Messages[2].Role == "system", "안내 메시지 역할 보존");
            Check(restored.Title.StartsWith("한글 요청 😀") && !restored.Title.Any(char.IsControl), "첫 사용자 질문에서 한 줄 제목 자동 생성");
            string markdown = File.ReadAllText(store.GetPath(first), Encoding.UTF8);
            Check(markdown.Contains(question) && markdown.Contains(answer) && !markdown.Contains("ImageBase64"), "대화 본문은 읽을 수 있는 실제 Markdown이며 첨부 모델 제외");
            string[] required = { "layout:", "title:", "date:", "permalink:", "categories:", "tags:", "pin:", "news_id:", "source_url:", "source_date:", "reviewed_at:", "excerpt:" };
            Check(required.All(markdown.Contains), "Role 13번 front matter 필수 항목 포함");
            Check(File.ReadAllBytes(store.GetPath(first)).Take(3).SequenceEqual(new byte[] { 45, 45, 45 }), "UTF-8 BOM 없이 표준 Markdown 헤더 시작");
            var paging = store.Create("Codex");
            DateTimeOffset hour = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(9));
            for (int index = 0; index < 45; index++)
                paging.Messages.Add(new ChatMessage { Role = index % 2 == 0 ? "user" : "assistant", Text = "시간별 메시지 " + index, CreatedAt = hour.AddHours(index / 15).AddMinutes(index % 15) });
            store.Save(paging);
            string historyDirectory = Path.Combine(Path.GetDirectoryName(store.GetPath(paging)), paging.Id + ".history");
            Check(Directory.GetFiles(historyDirectory, "*.md").Length == 3, "메시지 시각을 기준으로 한 시간마다 Markdown 파일 분할");
            var recent = new MarkdownWorkspaceStore(store.RootPath).LoadOpen("Codex").Single(record => record.Id == paging.Id);
            Check(recent.Messages.Count == 20 && recent.Messages[0].Text == "시간별 메시지 25", "재시작 복원은 최신 20개만 읽음");
            var olderPage = store.LoadOlder(recent, recent.Messages[0], 20);
            var oldestPage = store.LoadOlder(recent, olderPage[0], 20);
            Check(olderPage.Length == 20 && olderPage[0].Text == "시간별 메시지 5" && oldestPage.Length == 5 && oldestPage[0].Text == "시간별 메시지 0", "상단 스크롤은 이전 20개와 마지막 5개를 순서대로 복원");
            recent.Draft = "최신 기록의 저장된 초안";
            store.Save(recent);
            Check(Directory.GetFiles(historyDirectory, "*.md").Length == 3 && store.LoadOlder(recent, recent.Messages[0], 40).Length == 25, "재저장해도 시간별 기록이 중복되지 않음");
            string committedPaging = File.ReadAllText(store.GetPath(paging));
            paging.Draft = "오래된 화면의 미전송 요청";
            Throws<StaleConversationWriteException>(() => store.Close(paging), "오래된 화면의 닫기 저장은 명시적으로 거절");
            Check(paging.IsOpen && paging.Draft == "오래된 화면의 미전송 요청" && File.ReadAllText(store.GetPath(paging)) == committedPaging,
                "오래된 저장자 거절은 열림 상태·미전송 초안·기존 확정 파일을 모두 보존");
            store.Close(recent);
            first.Messages[0].Text = new string('가', 150) + "😀";
            store.Save(first);
            Check(first.Title.Length <= 120 && first.Title.EndsWith("…"), "긴 자동 제목을 120자 이하로 제한");
            store.Rename(first, "직접 정한 \"제목\": #1 😀");
            first.Messages[0].Text = "다른 질문";
            store.Save(first);
            Check(store.LoadOpen("Codex").Single().Title == "직접 정한 \"제목\": #1 😀" && !first.IsTitleAutomatic, "수동 이름은 질문 변경 후에도 보존");
            Throws<ArgumentException>(() => store.Rename(first, "여러\n줄"), "제목의 제어 문자 거부");
            Throws<ArgumentException>(() => store.Rename(first, " "), "빈 이름 거부");
            store.Close(first);
            Check(!first.IsOpen && store.LoadOpen("Codex").Length == 0 && File.Exists(store.GetPath(first)), "닫은 탭은 자동 복원에서 제외하고 파일 보관");
            var reopened = store.Load(store.GetPath(first), "Codex");
            Check(reopened.Id == first.Id && reopened.IsOpen && store.LoadOpen("Codex").Length == 1, "로컬 보관 대화를 같은 ID로 다시 열기");
            Throws<InvalidDataException>(() => store.Load(store.GetPath(first), "Claude"), "다른 AI로 대화 가져오기 거부");
            string external = Path.Combine(output, "외부 대화.md");
            File.Copy(store.GetPath(first), external);
            string externalBefore = File.ReadAllText(external);
            var imported = store.Load(external, "Codex");
            Check(imported.Id != first.Id && store.LoadOpen("Codex").Length == 2, "외부 대화의 중복 ID는 새 ID로 가져오기");
            Check(File.ReadAllText(external) == externalBefore, "외부 원본 파일은 변경하지 않음");
            var ordered = store.LoadOpen("Codex");
            Check(ordered[0].CreatedAt <= ordered[1].CreatedAt, "대화 목록은 생성 시각 순으로 복원");
            string instructions = "\n# 공통 규칙\r\n\n- 모든 프로젝트에서 한글로 설명\n---\n```md\n---\n```\n😀\n";
            store.SaveInstructions(instructions);
            Check(new MarkdownWorkspaceStore(store.RootPath).LoadInstructions() == instructions, "공통 instruction의 원문 Markdown과 개행 보존");
            Check(File.ReadAllText(store.InstructionsPath).Contains(instructions), "공통 instruction을 일반 Markdown 본문으로 저장");
            Check(store.LoadOpen("Claude").Length == 1 && new MarkdownWorkspaceStore(store.RootPath).InstructionsPath == store.InstructionsPath, "모든 AI와 프로젝트에서 같은 instruction 위치 사용");
            var selection = new ChatMessage { Role = "assistant", Text = "원하는 방법을 선택해 주세요.\n1. 기존 UI 패턴 유지\n2. 새 화면 추가" };
            Check(selection.Choices.Length == 2 && selection.Choices[0] == "기존 UI 패턴 유지" && new ChatMessage { Role = "user", Text = selection.Text }.Choices.Length == 0, "AI가 선택을 요청한 번호 목록만 선택 버튼으로 제공");
            string manuallyEdited = File.ReadAllText(store.InstructionsPath).Replace("한글로 설명", "간결하게 설명");
            File.WriteAllText(store.InstructionsPath, manuallyEdited, new UTF8Encoding(true));
            Check(store.LoadInstructions().Contains("간결하게 설명"), "일반 편집기의 BOM 포함 수동 instruction 수정 읽기");
            Throws<ArgumentException>(() => store.SaveInstructions(new string('x', 8001)), "instruction 8000자 상한 검사");
            Throws<ArgumentException>(() => store.Create("..\\outside"), "Provider 경로 주입 거부");
            var unsafeRecord = new ConversationRecord { Id = "..\\outside", Provider = "Codex", CreatedAt = DateTimeOffset.Now };
            Throws<InvalidDataException>(() => store.Save(unsafeRecord), "대화 ID 상위 경로 이동 거부");
            unsafeRecord.Id = "CON";
            Throws<InvalidDataException>(() => store.GetPath(unsafeRecord), "장치 파일명을 ID로 거부");
            imported.Draft = new string('x', 16001);
            Throws<InvalidDataException>(() => store.Save(imported), "초안 16000자 상한 검사");
            imported.Draft = "";
            imported.Messages.Add(new ChatMessage { Role = "tool", Text = "not saved" });
            Throws<InvalidDataException>(() => store.Save(imported), "허용하지 않은 메시지 역할 거부");
            imported.Messages.RemoveAt(imported.Messages.Count - 1);
            string protectedPath = store.GetPath(imported);
            byte[] before = File.ReadAllBytes(protectedPath);
            using (var locked = new FileStream(protectedPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                imported.Draft = "파일 잠금 중 저장";
                Throws<IOException>(() => store.Save(imported), "원자 교체 실패를 호출자에게 전달");
                Throws<IOException>(() => store.Close(imported), "닫기 저장 실패를 호출자에게 전달");
                Check(imported.IsOpen, "닫기 저장 실패 시 열림 상태 복구");
                string title = imported.Title;
                Throws<IOException>(() => store.Rename(imported, "실패할 이름"), "이름 저장 실패를 호출자에게 전달");
                Check(imported.Title == title, "이름 저장 실패 시 기존 이름 복구");
            }
            Check(File.ReadAllBytes(protectedPath).SequenceEqual(before), "원자 저장 실패 후 기존 기록 원문 보존");
            Check(!Directory.EnumerateFiles(Path.GetDirectoryName(protectedPath), "*.tmp").Any(), "원자 저장 실패 후 임시 파일 정리");
            string corruptPath = Path.Combine(Path.GetDirectoryName(protectedPath), new string('a', 32) + ".md");
            File.WriteAllText(corruptPath, "---\nnot valid\n---\n", Encoding.UTF8);
            Check(store.LoadOpen("Codex").Length == 2 && store.LastLoadWarnings.Length == 1, "손상 기록을 건너뛰고 정상 대화 복원 유지");
            Throws<InvalidDataException>(() => store.Load(corruptPath, "Codex"), "손상 기록의 명시적 불러오기는 오류 안내");
            string spoof = Path.Combine(output, "위조 경계.md");
            File.WriteAllText(spoof, File.ReadAllText(store.GetPath(first)).Replace("chars=5", "chars=9999999"), Encoding.UTF8);
            string actual = File.ReadAllText(store.GetPath(first));
            int number = actual.IndexOf("chars=", StringComparison.Ordinal) + 6;
            int numberEnd = actual.IndexOf(' ', number);
            File.WriteAllText(spoof, actual.Substring(0, number) + "9999999" + actual.Substring(numberEnd), Encoding.UTF8);
            Throws<InvalidDataException>(() => store.Load(spoof, "Codex"), "본문 길이 위조와 잘린 데이터 거부");
            File.WriteAllBytes(spoof, new byte[] { 0xff, 0xfe, 0xff });
            Throws<InvalidDataException>(() => store.Load(spoof, "Codex"), "잘못된 UTF-8 파일 거부");
            using (var huge = File.Create(spoof)) huge.SetLength(4 * 1024 * 1024 + 1);
            Throws<InvalidDataException>(() => store.Load(spoof, "Codex"), "4MB 초과 파일은 읽기 전에 거부");
            string unchanged = File.ReadAllText(store.GetPath(first));
            first.Messages.Clear();
            for (int index = 0; index < 101; index++) first.Messages.Add(new ChatMessage { Role = "assistant", Text = "응답" });
            Throws<InvalidDataException>(() => store.Save(first), "100개 초과 메시지 저장 거부");
            first.Messages.Clear();
            first.Messages.Add(new ChatMessage { Role = "assistant", Text = new string('가', 1500000) });
            Throws<InvalidDataException>(() => store.Save(first), "UTF-8 실제 바이트로 4MB 저장 상한 검사");
            Check(File.ReadAllText(store.GetPath(first)) == unchanged, "크기 제한 실패 후 기존 기록 보존");
            var bounded = new MarkdownWorkspaceStore(Path.Combine(output, "bounded-workspace"));
            var boundedRecord = bounded.Create("Codex");
            string boundedDirectory = Path.GetDirectoryName(bounded.GetPath(boundedRecord));
            for (int index = 0; index < 499; index++) File.WriteAllText(Path.Combine(boundedDirectory, "reserved-" + index + ".md"), "");
            Throws<IOException>(() => bounded.Create("Codex"), "AI별 500개 기록 생성 상한 검사");
            boundedRecord.Draft = "기존 대화는 계속 저장";
            bounded.Save(boundedRecord);
            Check(File.ReadAllText(bounded.GetPath(boundedRecord)).Contains("기존 대화는 계속 저장"), "개수 제한에 도달해도 기존 기록 저장 허용");
            var linked = new MarkdownWorkspaceStore(args[1]);
            Throws<IOException>(() => linked.Create("Codex"), "정션을 통한 저장 루트 우회 거부");
            Check(!Directory.Exists(Path.Combine(args[2], "conversations")), "정션 대상 폴더에 기록을 생성하지 않음");
            string previousEnv = Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH");
            try
            {
                Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", Path.Combine(output, "env-workspace"));
                Check(new MarkdownWorkspaceStore().RootPath == Path.Combine(output, "env-workspace"), "환경 변수로 개인 작업공간과 테스트 격리");
            }
            finally { Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", previousEnv); }
            File.WriteAllText(Path.Combine(output, "result.json"), new JavaScriptSerializer().Serialize(new { Passed = passed, Failed = 0, Workspace = store.RootPath }), Encoding.UTF8);
            Console.WriteLine("PASS " + passed + "/" + passed);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            File.WriteAllText(Path.Combine(output, "result.json"), new JavaScriptSerializer().Serialize(new { Passed = passed, Failed = 1, Error = error.ToString() }), Encoding.UTF8);
            return 1;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 관찰 가능한 결과가 기대값과 다르면 검증을 중단한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 통과 항목 이름을 테스트 기록에 출력.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        passed++; Console.WriteLine("PASS: " + name);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Throws
    /// desc         : 비정상 파일과 실패 저장이 예상한 오류로 거부되는지 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 예외 형식까지 확인하고 뜻밖의 성공은 실패 처리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Throws<T>(Action action, string name) where T : Exception
    {
        try { action(); }
        catch (T) { Check(true, name); return; }
        throw new Exception("FAIL (expected " + typeof(T).Name + "): " + name);
    }
}
