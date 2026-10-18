using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : AlphabetAgentRoutingTests
/// desc         : 영구 A~Z 이름·기존 기록 보존·한도 시 A 재사용과 생성 실패 복구를 검사한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 공유 빌드와 개인 설정을 사용하지 않는 실제 Markdown 검사.
/// </summary>
/// ---------------------------------------------------------------------
internal static class AlphabetAgentRoutingTests
{
    private static int passed;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 별도 작업공간에서 기존 협업 주소와 Markdown 원문 및 26명 제한을 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 파일 잠금·콜백 실패·동시 생성 경계까지 확인.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        try
        {
            string root = Path.Combine(output, "migration"); var workspace = new MarkdownWorkspaceStore(root); var store = new ConversationCollaborationStore(root);
            var first = workspace.Create("Codex"); first.Messages.Add(new ChatMessage { Role = "user", Text = "기존 원문 😀\n---\n유지" }); workspace.Save(first);
            var second = workspace.Create("Codex"); var oldFirst = store.EnsureAgent(first); var oldSecond = store.EnsureAgent(second);
            oldFirst = store.UpdateAgent(first.Id, first.Provider, "예전 설계 이름", "설계 역할", "팀"); oldSecond = store.UpdateAgent(second.Id, second.Provider, "예전 검토 이름", "검토 역할", "팀");
            var request = store.Send(first.Id, "Codex", second.Id, "Codex", "기존 협업 요청 원문");
            byte[] sourceBefore = File.ReadAllBytes(workspace.GetPath(first));
            store.ConfigureNaming("Codex", "홍길동"); var namedFirst = store.GetAgent(first.Id, "Codex"); var namedSecond = store.GetAgent(second.Id, "Codex");
            Check(namedFirst.Name == "홍길동코덱스A" && namedSecond.Name == "홍길동코덱스B", "사용자명·AI 이름·알파벳 한 자리의 순서대로 명명");
            Check(namedFirst.LegacyName == "예전 설계 이름" && namedFirst.Role == "설계 역할" && namedFirst.Team == "팀", "마이그레이션은 예전 이름·역할·팀을 보존");
            Check(File.ReadAllBytes(workspace.GetPath(first)).SequenceEqual(sourceBefore), "명명 마이그레이션은 기존 Markdown 원문을 변경하지 않음");
            Check(store.GetHandoff(request.Id).FromName == "예전 설계 이름" && store.GetHandoff(request.Id).ToName == "예전 검토 이름", "이미 전달한 요청의 당시 이름과 이력 보존");
            store.Receive(request.Id, second.Id, "Codex"); store.Reply(request.Id, second.Id, "Codex", "실제 이전 요청 회신");
            Check(store.Deliver(request.Id, first.Id, "Codex").State == CollaborationHandoffState.Delivered, "이름 변경 이후에도 기존 ID 주소로 협업 회신 전달");
            var stable = new ConversationCollaborationStore(root).GetAgent(first.Id, "Codex");
            Check(stable.Name == namedFirst.Name && stable.Letter == "A" && stable.ConversationId == first.Id, "재시작에서 영구 문자와 기존 대화 주소 복원");
            store.ConfigureNaming("Codex", "다른 사용자"); stable = store.GetAgent(first.Id, "Codex");
            Check(stable.Name == "다른 사용자코덱스A" && stable.Letter == "A" && stable.LegacyName == "예전 설계 이름", "명시 사용자 변경은 문자와 원래 별칭을 유지하며 접두만 갱신");
            Check(store.EnsureAgent(first, "무시해야 할 계정").Name == stable.Name, "EnsureAgent는 명시한 사용자 이름 설정을 묵시적으로 덮어쓰지 않음");
            Throws<InvalidOperationException>(() => store.UpdateAgent(first.Id, "Codex", "임의 이름", "", "팀"), "명명 적용 후 자유 이름 변경 거부");
            Check(store.UpdateAgent(first.Id, "Codex", stable.Name, "새 역할", "새 팀").Role == "새 역할", "고정 이름으로 역할·팀 편집은 계속 지원");
            Check(ConversationCollaborationStore.BuildAlphabetName("홍길동", "Claude", "A") == "홍길동클로드A", "요청한 한국어 Claude 이름 예시와 정확히 일치");
            string oldRoot = Path.Combine(output, "old-schema"); Directory.CreateDirectory(oldRoot); var oldRecord = new ConversationRecord { Id = Guid.NewGuid().ToString("N"), Provider = "Claude" };
            var originalSchema = new { Version = 1, Agents = new[] { new { ConversationId = oldRecord.Id, Provider = "Claude", Name = "예전 버전 AI", Role = "기존 역할", Team = "기존 팀", IsOpen = true, Revision = 1L, UpdatedAt = DateTimeOffset.Now } }, Handoffs = new object[0] };
            File.WriteAllText(Path.Combine(oldRoot, "collaboration.json"), new JavaScriptSerializer().Serialize(originalSchema), Encoding.UTF8);
            var oldStore = new ConversationCollaborationStore(oldRoot); oldStore.ConfigureNaming("Claude", "홍길동");
            Check(oldStore.GetAgent(oldRecord.Id, "Claude").Name == "홍길동클로드A" && oldStore.GetAgent(oldRecord.Id, "Claude").LegacyName == "예전 버전 AI", "Naming·Letter 필드가 없는 실제 이전 JSON 형식에서 무손실 마이그레이션");
            string unicode = ConversationCollaborationStore.BuildAlphabetName(new string('가', 35) + "😀추가", "Claude", "Z");
            Check(unicode.Length <= 40 && unicode.EndsWith("클로드Z") && !char.IsHighSurrogate(unicode[unicode.Length - 5]), "긴 사용자 이름은 Unicode 문자를 자르지 않고 이름 상한 준수");
            Throws<ArgumentException>(() => ConversationCollaborationStore.BuildAlphabetName("사용자", "Codex", "AA"), "알파벳 두 자리와 Z 이후 AA 생성 거부");
            root = Path.Combine(output, "permanent"); workspace = new MarkdownWorkspaceStore(root); store = new ConversationCollaborationStore(root);
            var oldRecords = new List<ConversationRecord>();
            for (int index = 0; index < 28; index++) { var record = workspace.Create("Codex"); record.Draft = "기존 초안 " + index; workspace.Save(record); store.EnsureAgent(record); oldRecords.Add(record); }
            store.UpdateAgent(oldRecords[26].Id, "Codex", "홍길동코덱스A", "과거 역할", "기본 협업팀");
            workspace.Close(oldRecords[0]); store.SetConversationOpen(oldRecords[0].Id, "Codex", false);
            var originalFiles = oldRecords.ToDictionary(r => workspace.GetPath(r), r => File.ReadAllBytes(workspace.GetPath(r)));
            store.ConfigureNaming("Codex", "홍길동"); var roster = store.ListAssignableAgents("Codex");
            Check(roster.Length == 26 && roster[0].Letter == "A" && roster[25].Letter == "Z", "기존 28개 기록에서 Provider별 26개 영구 슬롯만 배정");
            Check(!roster[0].IsOpen && roster[0].ConversationId == oldRecords[0].Id, "닫힌 A도 슬롯과 원래 주소 유지");
            Check(store.ListAgents().Length == 28 && store.GetAgent(oldRecords[26].Id, "Codex").IsLegacyArchive && store.GetAgent(oldRecords[27].Id, "Codex").LegacyName.Length > 0, "26명 초과 과거 담당자는 삭제하지 않고 읽기 보관");
            Check(store.UpdateAgent(oldRecords[0].Id, "Codex", roster[0].Name, "현재 A 역할", "기본 협업팀").Role == "현재 A 역할", "과거 보관 이름이 canonical A와 같아도 현재 역할·팀 저장 허용");
            Check(originalFiles.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value)), "26명 초과 마이그레이션도 모든 기존 Markdown 바이트 보존");
            Throws<InvalidOperationException>(() => store.BuildPromptContext(oldRecords[26].Id, "Codex"), "과거 초과 대화의 새 요청은 A 지정 안내로 제한");
            int created = 0;
            var route = store.RouteNewConversation("Codex", "홍길동", () => { created++; return workspace.Create("Codex"); });
            Check(route.ReusedExisting && route.CreatedRecord == null && route.Agent.ConversationId == oldRecords[0].Id && created == 0, "26명 도달 시 생성 콜백 없이 기존 A를 반환");
            using (var routeLock = new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Check(store.RouteNewConversation("Codex", "홍길동", () => { created++; return workspace.Create("Codex"); }).ReusedExisting && created == 0, "기존 A 선택은 변경 없는 메타데이터를 다시 쓰지 않음");
            Check(Directory.EnumerateFiles(Path.GetDirectoryName(workspace.GetPath(oldRecords[0])), "*.md").Count() == 28, "한도 도달 동작이 새 Markdown 파일을 만들지 않음");
            var reopened = workspace.Load(workspace.GetPath(oldRecords[0]), "Codex");
            Check(store.EnsureAgent(reopened).Name == "홍길동코덱스A" && store.GetPrimaryAgent("Codex").IsOpen && reopened.Draft == "기존 초안 0", "닫힌 A의 실제 파일 재열기 경로와 기존 초안 보존");
            workspace.Close(oldRecords[1]); store.SetConversationOpen(oldRecords[1].Id, "Codex", false);
            Check(store.RouteNewConversation("Codex", "홍길동", () => { created++; return workspace.Create("Codex"); }).ReusedExisting && created == 0, "닫힌 B의 문자를 새 대화에 재배정하지 않음");
            var claude = store.RouteNewConversation("Claude", "홍길동", () => { created++; return workspace.Create("Claude"); });
            Check(!claude.ReusedExisting && claude.Agent.Name == "홍길동클로드A" && claude.CreatedRecord.Provider == "Claude" && created == 1, "Codex 26명과 독립된 Claude A 슬롯 생성");
            root = Path.Combine(output, "concurrent"); workspace = new MarkdownWorkspaceStore(root); store = new ConversationCollaborationStore(root); store.ConfigureNaming("Codex", "병렬 사용자");
            var results = new AgentRouteResult[30];
            Parallel.For(0, 30, index => { results[index] = new ConversationCollaborationStore(root).RouteNewConversation("Codex", "병렬 사용자", () => workspace.Create("Codex"), record => workspace.Close(record)); });
            Check(results.Count(r => !r.ReusedExisting) == 26 && results.Count(r => r.ReusedExisting) == 4, "동시 새 대화 요청도 정확히 26개 생성 후 A 재사용");
            Check(workspace.LoadOpen("Codex").Length == 26 && store.ListAssignableAgents("Codex").Select(a => a.Name).Distinct().Count() == 26, "동시 경계에서도 27번째 파일과 중복 이름을 만들지 않음");
            root = Path.Combine(output, "callback-fail"); store = new ConversationCollaborationStore(root); store.ConfigureNaming("Codex", "사용자"); byte[] metadataBefore = File.ReadAllBytes(store.FilePath);
            Throws<IOException>(() => store.RouteNewConversation("Codex", "사용자", () => { throw new IOException("fixture 생성 실패"); }), "생성 콜백 실패를 원래 오류로 전달");
            Check(File.ReadAllBytes(store.FilePath).SequenceEqual(metadataBefore) && store.ListAssignableAgents("Codex").Length == 0, "생성 콜백 실패 시 슬롯과 메타데이터 미변경");
            foreach (int recoveryMode in new[] { 0, 1, 2 })
            {
                root = Path.Combine(output, "metadata-fail-" + recoveryMode); workspace = new MarkdownWorkspaceStore(root); store = new ConversationCollaborationStore(root); store.ConfigureNaming("Codex", "사용자"); metadataBefore = File.ReadAllBytes(store.FilePath);
                FileStream locked = null; AgentRouteException failure = null;
                try
                {
                    Action<ConversationRecord> rollback = recoveryMode == 0 ? null : recoveryMode == 1 ? (Action<ConversationRecord>)(record => workspace.Close(record)) : record => { throw new IOException("fixture 복구 실패"); };
                    try { store.RouteNewConversation("Codex", "사용자", () => { var record = workspace.Create("Codex"); locked = new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read); return record; }, rollback); }
                    catch (AgentRouteException error) { failure = error; }
                }
                finally { if (locked != null) locked.Dispose(); }
                Check(failure != null && failure.CreatedRecord != null && File.Exists(workspace.GetPath(failure.CreatedRecord)), "메타데이터 실패 시 생성 기록 주소와 원문을 오류에 보존 " + recoveryMode);
                Check(File.ReadAllBytes(store.FilePath).SequenceEqual(metadataBefore) && store.ListAssignableAgents("Codex").Length == 0, "메타데이터 교체 실패는 AI 슬롯 상태를 변경하지 않음 " + recoveryMode);
                if (recoveryMode == 1) Check(failure.RollbackSucceeded && workspace.LoadOpen("Codex").Length == 0 && !failure.CreatedRecord.IsOpen, "복구 콜백 성공은 새 파일을 삭제하지 않고 닫힘 상태로 보관");
                else Check(!failure.RollbackSucceeded && workspace.LoadOpen("Codex").Length == 1 && (recoveryMode == 0 ? failure.RollbackError == null : failure.RollbackError.Message.Contains("fixture 복구 실패")), "복구 미제공·실패는 부분 생성 상태와 오류를 정확히 전달 " + recoveryMode);
            }
            root = Path.Combine(output, "windows-fallback"); workspace = new MarkdownWorkspaceStore(root); store = new ConversationCollaborationStore(root);
            route = store.RouteNewConversation("Claude", null, () => workspace.Create("Claude"));
            Check(route.Agent.Name == ConversationCollaborationStore.BuildAlphabetName(Environment.UserName, "Claude", "A"), "계정 공급값이 없으면 Windows 사용자 표시명으로 대체");
            File.WriteAllText(Path.Combine(output, "result.json"), new JavaScriptSerializer().Serialize(new { Passed = passed, Failed = 0 }), Encoding.UTF8);
            Console.WriteLine("PASS " + passed + "/" + passed); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); File.WriteAllText(Path.Combine(output, "result.json"), new JavaScriptSerializer().Serialize(new { Passed = passed, Failed = 1, Error = error.ToString() }), Encoding.UTF8); return 1; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 실제 이름·파일·슬롯 결과를 확인하고 항목별 결과를 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실패 즉시 원인을 보존하여 검사를 중단.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Throws
    /// desc         : 실패 경계에서 예상한 오류가 전달되는지 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 뜻밖의 성공이나 다른 오류 형식은 실패로 처리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Throws<T>(Action action, string name) where T : Exception { try { action(); } catch (T) { Check(true, name); return; } throw new Exception("FAIL expected " + typeof(T).Name + ": " + name); }
}
