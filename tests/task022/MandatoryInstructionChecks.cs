using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : MandatoryInstructionChecks
/// desc         : 내장 규칙·숨김 사본·경쟁 생성·사용자 원문 보존을 실제 합성 파일로 검사한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : TASK-022 소스 독립·고정 제품 DLL reflection 공통 회귀 검사.
/// </summary>
/// ---------------------------------------------------------------------
internal static class MandatoryInstructionChecks
{
    private static readonly List<string> Passed = new List<string>(), Failed = new List<string>();
    private static Type store;
    private static string root, assemblyPath;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 독립 검사 또는 네 개 자식 생성 fixture를 실행하며 결과·실패 코드를 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 운영 명령·GUI·계정 없는 파일 검사 진입점.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        string error = null; bool child = args.Length > 0 && args[0] == "--child";
        root = Path.GetFullPath(args[child ? 1 : 0]); assemblyPath = args[child ? 2 : 1];
        Assembly assembly = assemblyPath == "-" ? Assembly.GetExecutingAssembly() : Assembly.LoadFrom(assemblyPath);
        store = assembly.GetType("VS2017.AIAssistant.Chat.MandatoryInstructionStore", true);
        string report = args[child ? 3 : 2];
        try
        {
            if (child) { string notice; Ensure(root, out notice); Check(String.IsNullOrEmpty(notice), "Competing process sees canonical complete copy"); }
            else Run();
        }
        catch (Exception failure) { error = failure.ToString(); Failed.Add("Unexpected exception"); }
        var result = new { Succeeded = Failed.Count == 0, Passed = Passed.Count, Failed = Failed.Count, Checks = Passed, Failures = Failed, Error = error,
            Mode = assemblyPath == "-" ? "SourceOnlyFilesystemFixture" : "FrozenProductFilesystemFixture", ProductAssemblyPath = assemblyPath == "-" ? null : assemblyPath,
            ProductAssemblySha256 = assemblyPath == "-" ? null : Hash(assemblyPath), CanonicalCharacters = DefaultText.Length, CanonicalSha256 = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(DefaultText))),
            GuiInvoked = false, AiInvoked = false, PersonalSettingsRead = false, SharedProductBuild = false, FixtureDirectory = root, CheckedAt = DateTimeOffset.Now.ToString("O") };
        string json = new JavaScriptSerializer().Serialize(result); File.WriteAllText(report, json, new UTF8Encoding(true)); Console.WriteLine(json);
        return Failed.Count == 0 ? 0 : 1;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DefaultText
    /// desc         : 실제 검사 대상의 내장 기본 문안 속성을 조회한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 테스트 복제 문자열을 실행 기준으로 사용하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string DefaultText { get { return (string)store.GetProperty("DefaultInstructions").GetValue(null, null); } }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Ensure
    /// desc         : 소스 또는 실제 제품의 out notice overload를 동일하게 호출한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 실제 제품 코드 검증 계약.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Ensure(string directory, out string notice)
    {
        object[] args = { directory, null };
        try { string path = (string)store.GetMethod("Ensure", new[] { typeof(string), typeof(string).MakeByRefType() }).Invoke(null, args); notice = (string)args[1]; return path; }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Compose
    /// desc         : 순수 합성 함수의 실제 소스·제품 구현을 호출한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 재조합·공백·사용자 내용 보존 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Compose(string user) { return (string)store.GetMethod("Compose").Invoke(null, new object[] { user }); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 독립 판정을 누적해 한 실패가 다른 경계를 가리지 않게 한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 실제 검사 이름·실패 수 기록.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool condition, string label) { if (condition) Passed.Add(label); else Failed.Add(label); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Reject
    /// desc         : 모호 경로·reparse를 거부하며 다른 경로를 생성하지 않는지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 경계 예외 종류 확인.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Reject(string path, string label)
    { string notice; try { Ensure(path, out notice); Failed.Add(label); } catch (ArgumentException) { Passed.Add(label); } }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Hash
    /// desc         : 선택한 고정 제품 파일과 보존 대상의 SHA-256을 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 출력 assembly 검증과 파일 변경 증거.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Hash(string path)
    { using (var hash = SHA256.Create()) using (var input = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", ""); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 실제 임시 파일에서 생성·숨김·보존·복구와 thread/process 경쟁을 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 최신 Role8 문안과 사용자 8000자 보존·경계 회귀.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Run()
    {
        Directory.CreateDirectory(root); string notice, workspace = Path.Combine(root, "workspace"); Directory.CreateDirectory(workspace);
        string userPath = Path.Combine(workspace, "instructions.md"), user = "  사용자 원문\r\n기존 규칙 그대로  "; File.WriteAllText(userPath, user, new UTF8Encoding(false));
        string userHash = Hash(userPath), path = Ensure(workspace, out notice);
        Check(path == Path.Combine(workspace, ".mandatory-instructions.md") && File.Exists(path), "First load creates only managed copy at canonical workspace path");
        Check(String.IsNullOrEmpty(notice) && File.ReadAllText(path, Encoding.UTF8) == DefaultText, "New copy exactly matches compiled canonical rules");
        Check((File.GetAttributes(path) & FileAttributes.Hidden) != 0, "Managed copy has Windows Hidden attribute");
        Check(Hash(userPath) == userHash && Directory.GetFiles(workspace).Length == 2, "Existing user instructions and unrelated files are preserved");
        DateTime written = File.GetLastWriteTimeUtc(path); string firstHash = Hash(path);
        Check((string)store.GetMethod("Ensure", new[] { typeof(string) }).Invoke(null, new object[] { workspace }) == path && Hash(path) == firstHash && File.GetLastWriteTimeUtc(path) == written, "Second Ensure is idempotent without content rewrite");
        Check(DefaultText.Contains("1. 다른 코덱스가 작업할 수 있으니, 특정 파일을 만들어 서로 간의 협업 진행.") && DefaultText.Contains("2. 작업을 진행한 소스에는 소스관련 설명 주석 추가."), "Canonical rules preserve latest Role8 collaboration and source-comment wording");
        Check(new[] { "name         : ClassOrMethodName", "desc         : 메소드나 클래스에 대한 내용 정의", "author       : 작업자명", "create date  : 2025-11-10", "update date  : 2026-10-17 코덱스Code : ~ 개발하였음.", "2026-10-18 코덱스Orig : ~ 수정", "2026-10-19 코덱스Orig : ~ 추가", "유사한 주석이 있는 경우, 해당 방식으로 구조 변경. 기존 주석 내용은 수정 불가." }.All(DefaultText.Contains), "Five-field example, original dates/authors and unchanged-comment requirement are preserved");
        Check(Compose(null) == DefaultText && Compose("") == DefaultText, "Null/empty user instructions use canonical rules once");
        Check(Compose(user) == DefaultText + "\n\n" + user, "User whitespace and line endings are preserved exactly");
        string longUser = "  \r\n" + new string('한', 7994) + "  ";
        Check(longUser.Length == 8000 && Compose(longUser).EndsWith(longUser, StringComparison.Ordinal) && Compose(longUser).Length == DefaultText.Length + 2 + 8000, "Full 8000-character Korean user instructions remain untruncated");
        Check(Compose(Compose(longUser)) == Compose(longUser) && Compose(DefaultText) == DefaultText, "Repeated composition is idempotent");
        Check(Compose(DefaultText + "\n\n" + DefaultText + "\n\n" + user) == Compose(user), "Repeated canonical prefixes collapse to one");
        string inside = "USER BEGIN\n" + DefaultText + "\nUSER END";
        Check(Compose(inside) == DefaultText + "\n\n" + inside, "Canonical-looking text inside user content is not stripped");
        File.SetAttributes(path, FileAttributes.Normal); File.WriteAllText(path, "TAMPERED_MANAGED_COPY_NOT_MODEL_INSTRUCTIONS"); File.SetAttributes(path, FileAttributes.Archive | FileAttributes.ReadOnly);
        firstHash = Hash(path); Ensure(workspace, out notice);
        Check(Hash(path) == firstHash && !String.IsNullOrWhiteSpace(notice), "Modified copy is preserved and returns explicit notice");
        Check((File.GetAttributes(path) & (FileAttributes.Archive | FileAttributes.ReadOnly | FileAttributes.Hidden)) == (FileAttributes.Archive | FileAttributes.ReadOnly | FileAttributes.Hidden), "Adding Hidden preserves ReadOnly and Archive bits");
        Check(!Compose(user).Contains("TAMPERED_MANAGED_COPY_NOT_MODEL_INSTRUCTIONS"), "Request composition never imports modified managed copy");
        File.SetAttributes(path, FileAttributes.Normal); File.WriteAllBytes(path, new byte[] { 0xff, 0xfe, 0xff }); firstHash = Hash(path); Ensure(workspace, out notice);
        Check(Hash(path) == firstHash && !String.IsNullOrWhiteSpace(notice), "Malformed copy remains unchanged and does not replace canonical rules");
        File.SetAttributes(path, FileAttributes.Normal); File.WriteAllText(path, new string('X', 1024 * 1024)); firstHash = Hash(path); Ensure(workspace, out notice);
        Check(Hash(path) == firstHash && !String.IsNullOrWhiteSpace(notice), "Oversized copy is bounded during comparison and preserved");
        File.SetAttributes(path, FileAttributes.Normal); File.Delete(path); Ensure(workspace, out notice);
        Check(File.ReadAllText(path) == DefaultText && String.IsNullOrEmpty(notice) && (File.GetAttributes(path) & FileAttributes.Hidden) != 0, "Deleted copy is recreated canonical and hidden");
        Check(Hash(userPath) == userHash, "User file remains unchanged after tamper and recovery cases");
        string missing = Path.Combine(root, "missing", "nested"); string recovered = Ensure(missing, out notice);
        Check(File.Exists(recovered), "Missing workspace parents are created without changing other files");
        var tasks = Enumerable.Range(0, 16).Select(index => Task.Run(() => { string detail; return Ensure(Path.Combine(root, "thread-race"), out detail) + "|" + detail; })).ToArray();
        Task.WaitAll(tasks);
        Check(tasks.Select(task => task.Result).Distinct().Count() == 1 && tasks[0].Result.EndsWith("|", StringComparison.Ordinal), "16 simultaneous callers return one complete canonical copy without notices");
        RunProcesses();
        foreach (string invalid in new[] { "relative", "C:relative", @"\root-relative", "/root-relative", @"\\?\" + workspace, workspace + ":stream" }) Reject(invalid, "Ambiguous/device/stream root rejected: " + invalid);
        Reject(Path.Combine(root, "reparse-root"), "Junction workspace root rejected");
        Reject(Path.Combine(root, "reparse-root", "child"), "Junction ancestor of missing workspace rejected");
        Reject(Path.Combine(root, "reparse-leaf"), "Reparse point at managed-copy leaf rejected");
        Check(!File.Exists(Path.Combine(root, "reparse-target", ".mandatory-instructions.md")), "Reparse rejection does not write target folder");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunProcesses
    /// desc         : 네 독립 합성 프로세스가 같은 사본을 CreateNew 경쟁으로 생성하는 실제 경계를 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 static gate만으로 통과하는 thread 검사와 분리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void RunProcesses()
    {
        var children = new List<Process>(); string directory = Path.Combine(root, "process-race");
        try
        {
            for (int i = 0; i < 4; i++) children.Add(Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,
                "--child \"" + directory + "\" \"" + assemblyPath + "\" \"" + Path.Combine(root, "child-" + i + ".json") + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }));
            bool success = true;
            foreach (var child in children) { if (!child.WaitForExit(5000)) { child.Kill(); success = false; } else success = success && child.ExitCode == 0; }
            Check(success && File.ReadAllText(Path.Combine(directory, ".mandatory-instructions.md")) == DefaultText, "Four independent processes preserve one canonical CreateNew result");
        }
        finally { foreach (var child in children) { try { if (!child.HasExited) child.Kill(); } catch (InvalidOperationException) { } child.Dispose(); } }
    }
}
