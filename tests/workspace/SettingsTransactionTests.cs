using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : SettingsTransactionTests
/// desc         : 설정 두 파일의 실제 잠금 실패와 원문 복구 및 외부 변경 보존을 검사한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 개인 설정 없이 독립 디렉터리에서 실패 경계 검증.
/// </summary>
/// ---------------------------------------------------------------------
internal static class SettingsTransactionTests
{
    private static int passed;
    private static string root;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : JSON 존재·없음·손상·추가 필드와 롤백 실패를 관찰 가능한 파일 상태로 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 FileShare 잠금 및 결정적인 외부 파일 변경 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        try
        {
            var json = new AssistantSettingsStore(Path.Combine(root, "settings.json"));
            var workspace = new MarkdownWorkspaceStore(Path.Combine(root, "workspace"));
            var settings = new AssistantSettings { Instructions = "새 공통 규칙", CodexPath = "new.exe", FontSize = 18 };
            AssistantSettingsTransaction.Save(json, workspace, settings);
            Check(json.Load().CodexPath == "new.exe" && workspace.LoadInstructions() == "새 공통 규칙", "성공 시 JSON과 공통 Markdown 모두 저장");
            string[] originals = {
                "{\r\n  \"CodexPath\": \"old.exe\", \"FutureField\": { \"key\": 7 }, \"FontSize\": 12\r\n}",
                "}{ 손상된 JSON 원문\r\n",
                ""
            };
            for (int index = 0; index < originals.Length; index++)
            {
                byte[] raw = new UTF8Encoding(index == 0).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(originals[index])).ToArray();
                File.WriteAllBytes(json.FilePath, raw);
                byte[] beforeMarkdown = File.ReadAllBytes(workspace.InstructionsPath);
                using (var locked = new FileStream(workspace.InstructionsPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var failure = Throws<InvalidOperationException>(() => AssistantSettingsTransaction.Save(json, workspace, settings), "Markdown 실제 파일 잠금 실패 전달 " + index);
                    Check(failure.Message.Contains("원래 상태로 복구") && !(failure.InnerException is AggregateException), "성공한 롤백과 부분 저장을 구분 " + index);
                    Check(File.ReadAllBytes(json.FilePath).SequenceEqual(raw), "JSON 원문 바이트 정확한 복원 " + index);
                }
                Check(File.ReadAllBytes(workspace.InstructionsPath).SequenceEqual(beforeMarkdown), "Markdown 실패 시 기존 원문 보존 " + index);
                Check(!Directory.EnumerateFiles(root, "*.tmp").Any() && !Directory.EnumerateFiles(workspace.RootPath, "*.tmp").Any(), "두 저장소 임시 파일 정리 " + index);
            }
            File.Delete(json.FilePath);
            using (var locked = new FileStream(workspace.InstructionsPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Throws<InvalidOperationException>(() => AssistantSettingsTransaction.Save(json, workspace, settings), "원래 JSON 없음 상태의 Markdown 저장 실패 전달");
                Check(!File.Exists(json.FilePath), "원래 없었던 JSON은 이번 생성 파일만 삭제");
            }
            json.Save(new AssistantSettings { CodexPath = "old.exe" });
            byte[] beforeJson = File.ReadAllBytes(json.FilePath);
            byte[] beforeRules = File.ReadAllBytes(workspace.InstructionsPath);
            using (var locked = new FileStream(json.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                Throws<IOException>(() => AssistantSettingsTransaction.Save(json, workspace, settings), "기존 JSON 읽기 잠금 시 저장 시작 거부");
            Check(File.ReadAllBytes(json.FilePath).SequenceEqual(beforeJson) && File.ReadAllBytes(workspace.InstructionsPath).SequenceEqual(beforeRules), "JSON 저장 전 실패는 양쪽 원문 보존");
            File.SetAttributes(json.FilePath, FileAttributes.ReadOnly);
            try
            {
                Throws<UnauthorizedAccessException>(() => AssistantSettingsTransaction.Save(json, workspace, settings), "읽기 가능한 JSON의 실제 교체 권한 실패 전달");
                Check(File.ReadAllBytes(json.FilePath).SequenceEqual(beforeJson) && File.ReadAllBytes(workspace.InstructionsPath).SequenceEqual(beforeRules), "JSON 저장 실패 시 공통 규칙 저장은 시작하지 않음");
            }
            finally { File.SetAttributes(json.FilePath, FileAttributes.Normal); }
            string blockedDirectory = Path.Combine(root, "directory-is-file");
            File.WriteAllText(blockedDirectory, "기존 파일");
            var unavailableWorkspace = new MarkdownWorkspaceStore(blockedDirectory);
            Throws<InvalidOperationException>(() => AssistantSettingsTransaction.Save(json, unavailableWorkspace, settings), "첫 공통 규칙 폴더를 만들 수 없는 실패 처리");
            Check(File.ReadAllBytes(json.FilePath).SequenceEqual(beforeJson) && File.ReadAllText(blockedDirectory) == "기존 파일", "공통 파일이 없던 초기 실패에도 기존 파일과 설정 보존");
            byte[] changed = Encoding.UTF8.GetBytes("외부 프로세스가 작성한 설정");
            var conflict = Throws<InvalidOperationException>(() => InvokeCore(json, settings, () => { File.WriteAllBytes(json.FilePath, changed); throw new IOException("공통 규칙 경계 실패"); }), "JSON 외부 변경과 Markdown 실패 동시 처리");
            Check(File.ReadAllBytes(json.FilePath).SequenceEqual(changed), "외부 변경된 JSON은 롤백으로 덮어쓰지 않음");
            Check(conflict.Message.Contains("일부만 저장") && conflict.Message.Contains("다른 작업") && conflict.InnerException is AggregateException, "외부 변경으로 복구 중단 시 부분 저장과 두 원인 표시");
            json.Save(new AssistantSettings());
            conflict = Throws<InvalidOperationException>(() => InvokeCore(json, settings, () => { File.Delete(json.FilePath); throw new IOException("공통 규칙 경계 실패"); }), "JSON 외부 삭제와 Markdown 실패 동시 처리");
            Check(!File.Exists(json.FilePath) && conflict.Message.Contains("일부만 저장"), "외부에서 삭제한 JSON을 롤백으로 재생성하지 않음");
            json.Save(new AssistantSettings());
            FileStream rollbackLock = null;
            try
            {
                using (var markdownLock = new FileStream(workspace.InstructionsPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var partial = Throws<InvalidOperationException>(() => InvokeCore(json, settings, () => {
                        rollbackLock = new FileStream(json.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        workspace.SaveInstructions(settings.Instructions);
                    }), "실제 JSON 교체 잠금으로 롤백 실패 유도");
                    Check(partial.Message.Contains("일부만 저장") && partial.InnerException is AggregateException && ((AggregateException)partial.InnerException).InnerExceptions.Count == 2, "원래 Markdown 오류와 JSON 복원 오류를 함께 전달");
                }
            }
            finally { if (rollbackLock != null) rollbackLock.Dispose(); }
            Check(json.Load().CodexPath == settings.CodexPath && File.ReadAllBytes(workspace.InstructionsPath).SequenceEqual(beforeRules), "롤백 실패 후 실제 부분 저장 상태 확인");
            Check(!Directory.EnumerateFiles(root, "*.tmp").Any(), "롤백 실패 후 복구 임시 파일 정리");
            byte[] largeOriginal = Enumerable.Repeat((byte)65, 65537).ToArray();
            File.WriteAllBytes(json.FilePath, largeOriginal);
            Throws<InvalidDataException>(() => AssistantSettingsTransaction.Save(json, workspace, settings), "64KB 초과 기존 JSON 스냅샷 거부");
            Check(File.ReadAllBytes(json.FilePath).SequenceEqual(largeOriginal), "과도한 기존 JSON을 변경하지 않음");
            File.WriteAllBytes(json.FilePath, beforeJson);
            settings.CodexPath = new string('\u0001', 4096); settings.ClaudePath = new string('\u0001', 4096); settings.Instructions = new string('\u0001', 8000);
            Throws<InvalidDataException>(() => AssistantSettingsTransaction.Save(json, workspace, settings), "이스케이프 이후 64KB를 넘는 새 JSON 거부");
            Check(File.ReadAllBytes(json.FilePath).SequenceEqual(beforeJson), "과도한 새 JSON은 저장 전 거부하여 기존 설정 보존");
            File.WriteAllText(Path.Combine(root, "result.json"), new JavaScriptSerializer().Serialize(new { Passed = passed, Failed = 0 }), Encoding.UTF8);
            Console.WriteLine("PASS " + passed + "/" + passed); return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            File.WriteAllText(Path.Combine(root, "result.json"), new JavaScriptSerializer().Serialize(new { Passed = passed, Failed = 1, Error = error.ToString() }), Encoding.UTF8);
            return 1;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : InvokeCore
    /// desc         : 두 파일 저장 사이의 외부 변경을 결정적으로 재현한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 최종 제품 DLL에서도 같은 비공개 경계를 검증 가능.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void InvokeCore(AssistantSettingsStore store, AssistantSettings settings, Action writeInstructions)
    {
        try { typeof(AssistantSettingsTransaction).GetMethod("SaveCore", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { store, settings, writeInstructions }); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 관찰한 파일 상태와 오류 문구가 예상한 결과인지 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 사용자에게 보이는 성공·실패 구분 검사.
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
    /// desc         : 오류 유형을 확인하면서 추가 메시지 검증에 사용할 예외를 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 뜻밖의 성공이나 다른 예외는 테스트 실패 처리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static T Throws<T>(Action action, string name) where T : Exception
    {
        try { action(); }
        catch (T error) { Check(true, name); return error; }
        throw new Exception("FAIL (expected " + typeof(T).Name + "): " + name);
    }
}
