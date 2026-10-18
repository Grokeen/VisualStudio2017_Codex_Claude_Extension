using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : CollaborationTests
/// desc         : 실제 파일 기반 이름 담당자·요청·수신·회신·실패·재시도와 동시 저장을 검사한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 자동 AI 실행 없이 주소와 실제 상태 보존 검증.
/// </summary>
/// ---------------------------------------------------------------------
internal static class CollaborationTests
{
    private static int passed;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 사용자 환경과 별개인 작업공간에서 협업 API의 실제 동작을 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 잠금 실패 시 원문 보존과 재시작 복원까지 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        if (args[0] == "--send")
        {
            try { new ConversationCollaborationStore(args[1]).Send(args[2], "Codex", args[3], "Codex", args[4]); return 0; }
            catch (Exception error) { Console.Error.WriteLine("HRESULT=" + error.HResult.ToString("X")); Console.Error.WriteLine(error); return 1; }
        }
        string output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        try
        {
            string root = Path.Combine(output, "workspace"); var store = new ConversationCollaborationStore(root);
            var a = Record("Codex"); var b = Record("Codex"); var c = Record("Claude");
            a.Messages.Add(new ChatMessage { Role = "user", Text = "공유하지 않은 비밀 원문 SECRET_NOT_SHARED" }); a.Draft = "PRIVATE_DRAFT";
            var alice = store.EnsureAgent(a); var bob = store.EnsureAgent(b); var claire = store.EnsureAgent(c);
            Check(alice.Name == "Codex-" + a.Id.Substring(0, 8) && alice.Name != bob.Name && claire.Name.StartsWith("Claude-"), "각 대화 기본 담당자 이름 고유 지정");
            alice = store.UpdateAgent(a.Id, a.Provider, "설계 담당", "설계와 검토\n실제 근거만 보고", "확장 개발팀", alice.Revision);
            bob = store.UpdateAgent(b.Id, b.Provider, "검토 담당", "코드 검토", "확장 개발팀");
            Check(new ConversationCollaborationStore(root).GetAgent(a.Id, a.Provider).Role == alice.Role, "이름·다중 행 역할·팀 재실행 복원");
            Check(store.EnsureAgent(a).Name == "설계 담당", "등록 재호출은 사용자 이름 보존");
            var accountRecord = Record("Codex");
            string accountDefault = ConversationCollaborationStore.BuildDefaultName(accountRecord.Id, accountRecord.Provider, "계정 사용자");
            var accountStore = new ConversationCollaborationStore(Path.Combine(output, "account-names"));
            Check(accountDefault == "계정 사용자::Codex-" + accountRecord.Id.Substring(0, 8) && accountStore.EnsureAgent(accountRecord, "계정 사용자").Name == accountDefault, "제공한 계정명과 구분자로 새 담당자 기본 이름 생성");
            accountStore.UpdateAgent(accountRecord.Id, accountRecord.Provider, "사용자가 정한 이름", "", "팀");
            Check(accountStore.EnsureAgent(accountRecord, "다른 계정").Name == "사용자가 정한 이름", "계정 변경이나 재등록이 기존 사용자 이름을 덮어쓰지 않음");
            string longName = ConversationCollaborationStore.BuildDefaultName(accountRecord, new string('가', 22) + "😀계정");
            Check(longName.Length <= 40 && longName.Contains("::Codex-") && ConversationCollaborationStore.BuildDefaultName(accountRecord, " ") == "Codex-" + accountRecord.Id.Substring(0, 8), "긴 Unicode 계정명은 40자 이내이며 계정 없음은 기존 이름 대체");
            Throws<InvalidOperationException>(() => store.UpdateAgent(b.Id, b.Provider, "설계 담당", "", "확장 개발팀"), "다른 대화 이름 중복 거부");
            store.UpdateAgent(c.Id, c.Provider, "Helper", "", "기타팀");
            Throws<InvalidOperationException>(() => store.UpdateAgent(b.Id, b.Provider, "ＨＥＬＰＥＲ", "", "확장 개발팀"), "전각·대소문자 이름 중복 거부");
            Throws<InvalidOperationException>(() => store.UpdateAgent(a.Id, a.Provider, "변경", "", "확장 개발팀", 1), "오래된 버전 이름 수정 거부");
            Check(store.ListAgents("확장 개발팀").Length == 2 && store.ListAgents().Length == 3, "공통 협업팀별 담당자 조회");
            Throws<InvalidOperationException>(() => store.Send(a.Id, a.Provider, a.Id, a.Provider, "요청"), "자기 자신에게 협업 요청 거부");
            Throws<InvalidOperationException>(() => store.Send(a.Id, a.Provider, c.Id, c.Provider, "요청"), "다른 협업팀으로 요청 거부");
            string selected = "선택한 코드만 검토\n```cs\nreturn 1;\n```\n😀 ---";
            var request = store.Send(a.Id, a.Provider, b.Id, b.Provider, selected);
            Check(request.State == CollaborationHandoffState.Pending && request.Reply == "" && request.Events.Count == 1, "전송은 대기 상태만 기록하고 답변을 만들지 않음");
            Check(store.ListInbox(b.Id, b.Provider).Single().Id == request.Id && store.ListOutbox(a.Id, a.Provider).Single().Id == request.Id && store.ListInbox(c.Id, c.Provider).Length == 0, "수신함·보낸함은 정확한 대화 주소로 분리");
            Check(!File.ReadAllText(store.FilePath).Contains("SECRET_NOT_SHARED") && !File.ReadAllText(store.FilePath).Contains("PRIVATE_DRAFT"), "대화와 초안 및 첨부를 무단 복사하지 않음");
            Throws<InvalidOperationException>(() => store.BuildPromptContext(b.Id, b.Provider, request.Id), "명시 수신 전 요청을 CLI에 포함하지 않음");
            Throws<InvalidOperationException>(() => store.Receive(request.Id, c.Id, c.Provider), "다른 수신자가 요청 받기 거부");
            Throws<InvalidOperationException>(() => store.Reply(request.Id, b.Id, b.Provider, "가상 답변"), "수신 전 회신 상태 위조 거부");
            var receipt = store.Receive(request.Id, b.Id, b.Provider);
            Check(receipt.State == CollaborationHandoffState.Received && receipt.Events.Count == 2, "사용자 명시 수신을 영속 상태로 기록");
            Check(store.Receive(request.Id, b.Id, b.Provider).Revision == receipt.Revision, "수신 재시도는 중복 상태 변경 없이 처리");
            string prompt = store.BuildPromptContext(b.Id, b.Provider, request.Id);
            Check(prompt.Contains("검토 담당") && prompt.Contains("코드 검토") && prompt.Contains("설계 담당") && prompt.Contains("return 1;") && prompt.Length <= 8000, "실제 CLI용 이름·역할·주소 지정 요청 컨텍스트 구성");
            Check(!store.BuildPromptContext(a.Id, a.Provider).Contains("return 1;") && !prompt.Contains("SECRET_NOT_SHARED"), "선택하지 않은 요청과 비밀 내용을 프롬프트에서 제외");
            Throws<InvalidOperationException>(() => store.BuildPromptContext(a.Id, a.Provider, request.Id), "발신자가 수신자 컨텍스트로 요청 재사용 거부");
            store.SetConversationOpen(a.Id, a.Provider, false);
            string actualReply = "실제 AI 응답에서 선택한 검토 결과: 상수 값을 확인하세요.\n원문 유지 😀";
            var reply = store.Reply(request.Id, b.Id, b.Provider, actualReply);
            Check(reply.State == CollaborationHandoffState.Replied && reply.Reply == actualReply, "발신 대화가 닫혀도 실제 선택 답변은 회신함에 보관");
            Throws<InvalidOperationException>(() => store.Deliver(request.Id, a.Id, a.Provider), "닫힌 발신 대화에서 회신 읽기 거부");
            Check(new ConversationCollaborationStore(root).GetHandoff(request.Id).Reply == actualReply, "담당자와 실제 회신 재실행 복원");
            store.SetConversationOpen(a.Id, a.Provider, true);
            var delivered = store.Deliver(request.Id, a.Id, a.Provider);
            Check(delivered.State == CollaborationHandoffState.Delivered && delivered.Reply == actualReply, "재열린 발신 대화에 실제 회신 전달 완료");
            Check(store.Deliver(request.Id, a.Id, a.Provider).Revision == delivered.Revision && store.Reply(request.Id, b.Id, b.Provider, actualReply).Revision == delivered.Revision, "동일 회신과 읽기 재시도 멱등성");
            Throws<InvalidOperationException>(() => store.Reply(request.Id, b.Id, b.Provider, "다른 답변"), "이미 전달된 답변 덮어쓰기 거부");
            Throws<InvalidOperationException>(() => store.Cancel(request.Id, a.Id, a.Provider), "회신 완료 요청 취소 거부");
            var failure = store.Send(a.Id, a.Provider, b.Id, b.Provider, "실패와 재시도 검사"); store.Receive(failure.Id, b.Id, b.Provider);
            failure = store.MarkFailed(failure.Id, b.Id, b.Provider, "fixture CLI 종료 7");
            Check(failure.State == CollaborationHandoffState.Failed && failure.Reply == "" && failure.ErrorText.Contains("종료 7"), "실제 CLI 실패 기록과 가상 답변 없음");
            Check(store.MarkFailed(failure.Id, b.Id, b.Provider, "fixture CLI 종료 7").Revision == failure.Revision, "동일 실패 재기록 멱등성");
            Throws<InvalidOperationException>(() => store.BuildPromptContext(b.Id, b.Provider, failure.Id), "실패 요청은 명시 재시도 전 실행 컨텍스트 차단");
            var retried = store.Retry(failure.Id, b.Id, b.Provider);
            Check(retried.State == CollaborationHandoffState.Received && retried.ErrorText == "" && retried.Events.Any(e => e.Detail.Contains("종료 7")), "재시도는 이전 실패 이력 보존하고 처리 상태만 복원");
            store.MarkFailed(failure.Id, b.Id, b.Provider, "두 번째 실패");
            Check(store.Cancel(failure.Id, a.Id, a.Provider).State == CollaborationHandoffState.Cancelled, "실패 요청도 발신자가 명시 취소 가능");
            Throws<InvalidOperationException>(() => store.Receive(failure.Id, b.Id, b.Provider), "취소 요청을 다시 수신하지 않음");
            store.SetConversationOpen(b.Id, b.Provider, false);
            Throws<InvalidOperationException>(() => store.Send(a.Id, a.Provider, b.Id, b.Provider, "닫힌 수신자"), "닫힌 수신자에게 새 작업 전송 거부");
            Throws<InvalidOperationException>(() => store.Send(b.Id, b.Provider, a.Id, a.Provider, "닫힌 발신자"), "닫힌 발신자의 새 작업 전송 거부");
            b.IsOpen = true; store.EnsureAgent(b);
            Check(store.GetAgent(b.Id, b.Provider).IsOpen && store.GetAgent(b.Id, b.Provider).Name == "검토 담당", "대화 재열기는 담당자 이름과 이력을 보존");
            var copied = store.GetHandoff(request.Id); copied.Reply = "메모리 변조"; copied.Events.Clear();
            Check(store.GetHandoff(request.Id).Reply == actualReply && store.GetHandoff(request.Id).Events.Count > 0, "반환 객체 변조는 저장 기록에 영향 없음");
            byte[] before = File.ReadAllBytes(store.FilePath);
            using (var locked = new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Throws<IOException>(() => store.Send(a.Id, a.Provider, b.Id, b.Provider, "저장 실패"), "실제 파일 교체 잠금 실패 전달");
            Check(File.ReadAllBytes(store.FilePath).SequenceEqual(before), "저장 실패 시 상태와 요청 원문 보존");
            Check(!Directory.EnumerateFiles(root, "*.tmp").Any(), "실패한 협업 저장 임시 파일 정리");
            var transientLock = new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var releaseLock = Task.Run(() => { System.Threading.Thread.Sleep(100); transientLock.Dispose(); });
            try { var recovered = store.Send(a.Id, a.Provider, b.Id, b.Provider, "일시 파일 잠금 복구"); Check(recovered.State == CollaborationHandoffState.Pending, "일시 파일 교체 잠금은 제한된 재시도로 실제 저장 완료"); }
            finally { releaseLock.Wait(); transientLock.Dispose(); }
            var parallel = Enumerable.Range(0, 12).Select(index => Task.Run(() => new ConversationCollaborationStore(root).Send(a.Id, a.Provider, b.Id, b.Provider, "동시 요청 " + index))).ToArray();
            Task.WaitAll(parallel);
            Check(store.ListInbox(b.Id, b.Provider).Count(h => h.Text.StartsWith("동시 요청 ")) == 12, "여러 저장 인스턴스 동시 요청 분실 방지");
            var children = Enumerable.Range(0, 4).Select(index => Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetEntryAssembly().Location,
                "--send \"" + root + "\" " + a.Id + " " + b.Id + " cross-process-" + index) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true })).ToArray();
            try
            {
                foreach (var child in children) { if (!child.WaitForExit(10000)) { child.Kill(); throw new Exception("다중 프로세스 협업 검증 제한 시간 초과"); } if (child.ExitCode != 0) throw new Exception("다중 프로세스 협업 저장 실패: " + child.StandardError.ReadToEnd()); }
            }
            finally { foreach (var child in children) child.Dispose(); }
            Check(store.ListInbox(b.Id, b.Provider).Count(h => h.Text.StartsWith("cross-process-")) == 4, "실제 별도 프로세스 named mutex 저장 분실 방지");
            var history = store.Send(a.Id, a.Provider, b.Id, b.Provider, "최근 이력 상한 확인"); store.Receive(history.Id, b.Id, b.Provider);
            for (int index = 0; index < 20; index++) { store.MarkFailed(history.Id, b.Id, b.Provider, "재시도 " + index); store.Retry(history.Id, b.Id, b.Provider); }
            Check(store.GetHandoff(history.Id).Events.Count == 32 && store.GetHandoff(history.Id).State == CollaborationHandoffState.Received, "최근 32개 상태 이력 상한과 최신 상태 유지");
            var prefixes = new ConversationCollaborationStore(Path.Combine(output, "same-prefix"));
            var prefixFirst = Record("Codex"); prefixFirst.Id = "abcdef12" + Guid.NewGuid().ToString("N").Substring(8);
            var prefixSecond = Record("Codex"); prefixSecond.Id = "abcdef12" + Guid.NewGuid().ToString("N").Substring(8);
            Check(prefixes.EnsureAgent(prefixFirst).Name != prefixes.EnsureAgent(prefixSecond).Name, "짧은 대화 ID 충돌에도 담당자 이름 고유성 유지");
            string report = store.ExportMarkdown("확장 개발팀"); string reportText = File.ReadAllText(report);
            Check(report == store.ReportPath && reportText.StartsWith("---\n") && reportText.Contains(actualReply) && reportText.Contains("fixture CLI 종료 7") && reportText.Contains("Delivered"), "공통 Markdown 보고에 실제 회신·실패·상태 이력 포함");
            Check(!reportText.Contains("SECRET_NOT_SHARED") && !reportText.Contains("Helper"), "선택 팀 보고에 다른 팀과 비공유 대화 제외");
            byte[] reportBefore = File.ReadAllBytes(report);
            using (var locked = new FileStream(report, FileMode.Open, FileAccess.Read, FileShare.None)) Throws<IOException>(() => store.ExportMarkdown(), "보고서 실제 교체 잠금 실패 전달");
            Check(File.ReadAllBytes(report).SequenceEqual(reportBefore), "보고서 저장 실패 시 이전 Markdown 보존");
            Throws<ArgumentException>(() => store.EnsureAgent(new ConversationRecord { Id = "..\\escape", Provider = "Codex" }), "협업 주소 경로 주입 거부");
            Throws<ArgumentException>(() => store.UpdateAgent(a.Id, a.Provider, "여러\n줄 이름", "", "팀"), "이름 제어 문자 거부");
            Throws<ArgumentException>(() => store.Send(a.Id, a.Provider, b.Id, b.Provider, new string('x', 6001)), "요청 텍스트 크기 제한");
            Throws<ArgumentException>(() => store.Reply(request.Id, b.Id, b.Provider, new string('x', 12001)), "회신 텍스트 크기 제한");
            var bounded = new ConversationCollaborationStore(Path.Combine(output, "corrupt")); Directory.CreateDirectory(Path.GetDirectoryName(bounded.FilePath)); File.WriteAllText(bounded.FilePath, "{bad JSON");
            Throws<InvalidDataException>(() => bounded.EnsureAgent(a), "손상 협업 파일을 덮어쓰지 않고 오류 전달");
            Check(File.ReadAllText(bounded.FilePath) == "{bad JSON", "손상 파일 원문 보존");
            using (var huge = File.Create(bounded.FilePath)) huge.SetLength(4 * 1024 * 1024 + 1);
            Throws<InvalidDataException>(() => bounded.ListAgents(), "협업 metadata 4MB 읽기 제한");
            var linked = new ConversationCollaborationStore(args[1]);
            Throws<IOException>(() => linked.EnsureAgent(a), "정션 경로로 협업 저장 우회 거부");
            File.WriteAllText(Path.Combine(output, "result.json"), new JavaScriptSerializer().Serialize(new { Passed = passed, Failed = 0, Metadata = store.FilePath, Report = store.ReportPath }), Encoding.UTF8);
            Console.WriteLine("PASS " + passed + "/" + passed); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); File.WriteAllText(Path.Combine(output, "result.json"), new JavaScriptSerializer().Serialize(new { Passed = passed, Failed = 1, Error = error.ToString() }), Encoding.UTF8); return 1; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Record
    /// desc         : 실제 사용자 데이터 없는 최소 대화 식별자를 만든다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 기존 대화 모델에 협업 필드를 추가하지 않는 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ConversationRecord Record(string provider) { return new ConversationRecord { Id = Guid.NewGuid().ToString("N"), Provider = provider, CreatedAt = DateTimeOffset.Now, IsOpen = true }; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 관찰 가능한 결과가 기대와 같은지 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 통과 항목 이름과 실패 위치를 출력.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Throws
    /// desc         : 잘못된 주소·상태와 실패 저장을 지정한 오류로 거부하는지 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 뜻밖의 성공은 검사 실패로 처리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Throws<T>(Action action, string name) where T : Exception { try { action(); } catch (T) { Check(true, name); return; } throw new Exception("FAIL expected " + typeof(T).Name + ": " + name); }
}
