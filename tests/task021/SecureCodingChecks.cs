using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Chat;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : SecureCodingChecks
/// desc         : 실제 디스크 해시 helper의 정상 호환성과 읽기 중 2MiB 상한을 합성 스트림으로 확인한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 실제 IDE 실행과 구분한 가용성 경계의 독립 control 검사 추가.
/// </summary>
/// ---------------------------------------------------------------------
internal static class SecureCodingChecks
{
    private const int Limit = 2 * 1024 * 1024;
    private static readonly List<object> Results = new List<object>();
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    private static MethodInfo compute;
    private static int passed, failed;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : bounded 해시 12건과 설정 거래·크기·원문 경계를 실제 파일·프로세스로 독립 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 작은 메모리 fixture만 사용하고 실행 근거와 실제 DLL 해시 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--settings-child") return Child(args[1], args[2]);
        if (args.Length != 1) return 2;
        string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        var paths = Json.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "references.json"), Encoding.UTF8));
        ResolveEventHandler resolver = (sender, item) => { string path; return paths.TryGetValue(new AssemblyName(item.Name).Name, out path) ? Assembly.LoadFrom(path) : null; };
        AppDomain.CurrentDomain.AssemblyResolve += resolver;
        try
        {
            compute = typeof(DocumentSnapshot).Assembly.GetType("VS2017.AIAssistant.Services.VisualStudioEditorDocument", true).GetMethod("ComputeBoundedDiskVersion", BindingFlags.Static | BindingFlags.NonPublic);
            if (compute == null) throw new MissingMethodException("실제 SDK 어댑터의 bounded 디스크 해시 helper가 없습니다.");
            Case("UTF8 한글·surrogate·CRLF 원문의 기존 SHA-256과 일치", () => {
                byte[] bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("한글😀\r\n\treturn 1;\r\n")).ToArray();
                using (var input = new MemoryStream(bytes, false)) Require(Call(input) == Expected(bytes) && input.CanRead);
            });
            Case("UTF16 BOM 원문의 기존 SHA-256과 일치", () => {
                byte[] bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("앞\r\n한글😀\r\n뒤")).ToArray();
                using (var input = new MemoryStream(bytes, false)) Require(Call(input) == Expected(bytes));
            });
            Case("UTF32 BOM 원문의 기존 SHA-256과 일치", () => {
                byte[] bytes = Encoding.UTF32.GetPreamble().Concat(Encoding.UTF32.GetBytes("앞\n한글😀\n뒤")).ToArray();
                using (var input = new MemoryStream(bytes, false)) Require(Call(input) == Expected(bytes));
            });
            Case("정확히 2MiB인 파일은 마지막 EOF 확인 후 허용", () => {
                byte[] bytes = Enumerable.Repeat((byte)0xA5, Limit).ToArray();
                using (var input = new ProbeStream(bytes, true)) Require(Call(input) == Expected(bytes) && input.TotalRead == Limit && input.ReadCalls == Limit / 8192 + 1);
            });
            Case("이미 2MiB를 넘는 탐색 가능 입력은 읽기 전에 거부", () => {
                using (var input = new ProbeStream(new byte[16], true, Limit + 1L)) { Throws<InvalidOperationException>(() => Call(input)); Require(input.ReadCalls == 0 && input.TotalRead == 0 && input.CanRead); }
            });
            Case("처음 작아도 계속 커지는 입력은 2MiB+1바이트에서 중단", () => {
                using (var input = new ProbeStream(null, true, 16, true)) { Throws<InvalidOperationException>(() => Call(input)); Require(input.TotalRead == Limit + 1 && input.ReadCalls == Limit / 8192 + 1 && input.MaxRequested == 8192); }
            });
            Case("Length 없는 무한 스트림도 2MiB+1바이트에서 중단", () => {
                using (var input = new ProbeStream(null, false, null, true)) { Throws<InvalidOperationException>(() => Call(input)); Require(input.TotalRead == Limit + 1 && input.ReadCalls == Limit / 8192 + 1 && input.MaxRequested == 8192); }
            });
            Case("비탐색 유한 스트림의 짧은 읽기를 모두 해시", () => {
                byte[] bytes = Encoding.UTF8.GetBytes("한글 " + new string('x', 301));
                using (var input = new ProbeStream(bytes, false, null, false, 1)) Require(Call(input) == Expected(bytes) && input.TotalRead == bytes.Length && input.ReadCalls == bytes.Length + 1);
            });
            Case("읽기 예외는 숨기지 않고 원문 바이트·스트림 소유권 보존", () => {
                byte[] bytes = Encoding.UTF8.GetBytes(new string('z', 100)); byte[] before = (byte[])bytes.Clone();
                using (var input = new ProbeStream(bytes, false, null, false, 11, 33)) { Throws<IOException>(() => Call(input)); Require(input.TotalRead == 33 && input.CanRead && bytes.SequenceEqual(before)); }
            });
            Case("빈 스트림은 표준 빈 SHA-256과 일치", () => {
                using (var input = new MemoryStream(new byte[0], false)) Require(Call(input) == Expected(new byte[0]) && input.CanRead);
            });
            Case("누락·닫힌 스트림을 읽기 전에 명확히 거부", () => {
                Throws<ArgumentNullException>(() => Call(null)); var input = new ProbeStream(new byte[1]); input.Dispose(); Throws<ArgumentException>(() => Call(input)); Require(input.ReadCalls == 0);
            });
            Case("비탐색 2MiB 경계도 허용하며 입력 원문을 변경하지 않음", () => {
                byte[] bytes = new byte[Limit]; bytes[0] = 7; bytes[Limit - 1] = 19; string expected = Expected(bytes);
                using (var input = new ProbeStream(bytes)) Require(Call(input) == expected && input.TotalRead == Limit && input.CanRead && bytes[0] == 7 && bytes[Limit - 1] == 19 && Expected(bytes) == expected);
            });
            SettingsChecks(root);
        }
        catch (Exception error) { failed++; Results.Add(new { Name = "실제 helper 연결", Passed = false, Error = error.ToString() }); }
        finally { AppDomain.CurrentDomain.AssemblyResolve -= resolver; }
        string assembly = typeof(DocumentSnapshot).Assembly.Location;
        File.WriteAllText(Path.Combine(root, "result.json"), Json.Serialize(new { Succeeded = failed == 0, Passed = passed, Failed = failed, Checks = Results, ProductAssembly = assembly, ProductSha256 = HashFile(assembly), Evidence = "Actual production helper with synthetic streams; actual settings API with isolated filesystem and child processes; no real EnvDTE or large disk files", IdeInvoked = false, NetworkRequested = false, PersonalCredentialsRead = false }), new UTF8Encoding(true));
        Console.WriteLine((failed == 0 ? "PASS " : "FAIL ") + passed + "/" + (passed + failed)); return failed == 0 ? 0 : 1;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SettingsChecks
    /// desc         : 실제 두 프로세스의 ABA 순서와 설정 읽기·쓰기 잠금 및 64KiB 바이트 경계를 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 순서·IO 실패는 제어하고 파일 상태와 실제 공개 API의 반환값을 관찰.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void SettingsChecks(string root)
    {
        Case("전체 거래와 Store.Save·Load의 같은 스레드 중첩 잠금 허용", () => {
            string folder = Fixture(root, "nested"); var store = Store(folder); var workspace = Workspace(folder);
            using (Lock(store.FilePath)) using (Lock(store.FilePath)) { AssistantSettingsTransaction.Save(store, workspace, Settings()); Require(store.Load().CodexPath == "new.exe" && workspace.LoadInstructions() == "new rules"); }
        });
        Case("실제 두 프로세스에서 첫 거래 실패 후 성공한 두 번째 저장 보존", () => {
            string folder = Fixture(root, "aba-after"); var store = Store(folder); var workspace = Workspace(folder);
            AssistantSettingsTransaction.Save(store, workspace, new AssistantSettings { CodexPath = "old.exe", Instructions = "old rules" });
            Process child = null;
            try
            {
                Throws<InvalidOperationException>(() => SaveCore(store, Settings(), () => {
                    child = StartChild("save", folder.ToUpperInvariant()); WaitFile(Path.Combine(folder, "ready"));
                    Require(!child.WaitForExit(200) && !File.Exists(Path.Combine(folder, "done")));
                    throw new IOException("첫 거래 Markdown 경계의 합성 실패");
                }));
                Require(child.WaitForExit(5000) && child.ExitCode == 0 && File.Exists(Path.Combine(folder, "done")));
                Require(store.Load().CodexPath == "new.exe" && store.Load().Instructions == "new rules" && workspace.LoadInstructions() == "new rules");
            }
            finally { StopChild(child); }
        });
        Case("다른 프로세스가 잠근 설정은 5초 유한 대기 후 비밀 없는 오류", () => {
            string folder = Fixture(root, "finite-wait-secret-marker"); Process child = StartChild("hold", folder);
            try
            {
                WaitFile(Path.Combine(folder, "ready")); var clock = Stopwatch.StartNew();
                var error = Throws<IOException>(() => { using (Lock(Store(folder).FilePath)) { } }); clock.Stop();
                Require(clock.Elapsed >= TimeSpan.FromSeconds(4.5) && clock.Elapsed < TimeSpan.FromSeconds(10) && !error.Message.Contains("secret-marker") && error.Message.Contains("다른 창"));
                File.WriteAllText(Path.Combine(folder, "release"), "release"); Require(child.WaitForExit(3000) && child.ExitCode == 0);
                using (Lock(Store(folder).FilePath)) { }
            }
            finally { StopChild(child); }
        });
        Case("종료된 실제 프로세스의 abandoned Mutex를 획득하고 해제", () => {
            string folder = Fixture(root, "abandoned"), path = Store(folder).FilePath;
            string name; using (var hash = SHA256.Create()) name = "Local\\VS2017AISettings-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()))).Replace("-", "");
            using (var keeper = new Mutex(false, name))
            {
                Process child = StartChild("abandon", folder);
                try
                {
                    WaitFile(Path.Combine(folder, "ready")); bool unexpectedlyFree = keeper.WaitOne(0); if (unexpectedlyFree) keeper.ReleaseMutex(); Require(!unexpectedlyFree);
                    File.WriteAllText(Path.Combine(folder, "release"), "release"); Require(child.WaitForExit(3000) && child.ExitCode == 0);
                    using (Lock(path)) { }
                    using (Lock(path)) { }
                }
                finally { StopChild(child); }
            }
        });
        Case("한 설정의 잠금은 다른 경로의 실제 저장을 막지 않음", () => {
            string first = Fixture(root, "independent-a"), second = Fixture(root, "independent-b"); Process child = null;
            try { using (Lock(Store(first).FilePath)) { child = StartChild("save", second); Require(child.WaitForExit(3000) && child.ExitCode == 0 && Store(second).Load().CodexPath == "new.exe"); } }
            finally { StopChild(child); }
        });
        Case("다른 스레드의 Store.Load는 두 파일 거래 완료 전 읽지 않음", () => {
            string folder = Fixture(root, "reader"); var store = Store(folder); var workspace = Workspace(folder); Task<AssistantSettings> read = null;
            AssistantSettingsTransaction.Save(store, workspace, new AssistantSettings { CodexPath = "old.exe", Instructions = "old rules" });
            using (var started = new ManualResetEventSlim())
            {
                SaveCore(store, Settings(), () => { read = Task.Run(() => { started.Set(); return store.Load(); }); Require(started.Wait(1000) && !read.Wait(200)); workspace.SaveInstructions("new rules"); });
                Require(read.Wait(3000) && read.Result.CodexPath == "new.exe" && workspace.LoadInstructions() == "new rules");
            }
        });
        Case("UTF8 BOM 설정 읽기는 원문 바이트와 한글을 유지", () => {
            string folder = Fixture(root, "bom"); var store = Store(folder); byte[] bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"CodexPath\":\"한글.exe\",\"Instructions\":\"공통 규칙\"}")).ToArray();
            File.WriteAllBytes(store.FilePath, bytes); var settings = store.Load(); Require(settings.CodexPath == "한글.exe" && settings.Instructions == "공통 규칙" && File.ReadAllBytes(store.FilePath).SequenceEqual(bytes));
        });
        Case("64KiB를 넘는 설정 읽기는 원문 변경 없이 거부", () => {
            string folder = Fixture(root, "load-limit"); var store = Store(folder); byte[] bytes = Enumerable.Repeat((byte)'A', 65537).ToArray(); File.WriteAllBytes(store.FilePath, bytes);
            Throws<InvalidDataException>(() => store.Load()); Require(File.ReadAllBytes(store.FilePath).SequenceEqual(bytes));
        });
        Case("직접 Store.Save도 이스케이프 뒤 64KiB 초과를 쓰기 전에 거부", () => {
            string folder = Fixture(root, "save-limit"); var store = Store(folder); store.Save(new AssistantSettings()); byte[] before = File.ReadAllBytes(store.FilePath);
            var settings = new AssistantSettings { CodexPath = new string('\u0001', 4096), ClaudePath = new string('\u0001', 4096), Instructions = new string('\u0001', 8000) };
            Throws<InvalidDataException>(() => store.Save(settings)); Require(File.ReadAllBytes(store.FilePath).SequenceEqual(before) && !Directory.EnumerateFiles(folder, "*.tmp").Any());
        });
        Case("손상된 UTF8·JSON 읽기 오류에 비밀 원문을 표시하지 않음", () => {
            string folder = Fixture(root, "invalid-format"); var store = Store(folder);
            File.WriteAllBytes(store.FilePath, new byte[] { 0xFF, 0xFE, 0xFF }); Throws<InvalidDataException>(() => store.Load());
            File.WriteAllText(store.FilePath, "PRIVATE_SECRET_MARKER", new UTF8Encoding(false)); var error = Throws<InvalidDataException>(() => store.Load()); Require(!error.Message.Contains("PRIVATE_SECRET_MARKER"));
            store.Save(Settings()); Require(store.Load().Instructions == "new rules");
        });
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Child
    /// desc         : 검사 자기 실행 파일의 별도 프로세스에서 실제 설정 저장 또는 잠금 보유를 수행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : abandoned 사례만 의도적으로 Dispose 없이 종료하며 실제 사용자 파일에 접근하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Child(string mode, string folder)
    {
        try
        {
            if (mode == "save") { File.WriteAllText(Path.Combine(folder, "ready"), "ready"); AssistantSettingsTransaction.Save(Store(folder), Workspace(folder), Settings()); File.WriteAllText(Path.Combine(folder, "done"), "success"); return 0; }
            using (Lock(Store(folder).FilePath))
            {
                File.WriteAllText(Path.Combine(folder, "ready"), "ready"); WaitFile(Path.Combine(folder, "release"), 15000);
                if (mode == "abandon") Environment.Exit(0);
                else if (mode != "hold") throw new ArgumentException("지원하지 않는 검사 모드");
            }
            return 0;
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(folder, "child-error.txt"), error.ToString()); return 1; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Fixture
    /// desc         : 지정된 결과 폴더의 고유 하위 경로만 준비한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 개인 설정·워크스페이스 환경값을 사용하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Fixture(string root, string name) { string path = Path.Combine(root, name); Directory.CreateDirectory(path); return path; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Store
    /// desc         : 경로를 명시한 실제 설정 저장소를 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 계정 조회 없이 공개 제품 API 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static AssistantSettingsStore Store(string folder) { return new AssistantSettingsStore(Path.Combine(folder, "settings.json")); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Workspace
    /// desc         : 실제 공통 규칙 저장을 고유 검사 디렉터리로 한정한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 두 파일 거래를 메모리 모형 대신 파일 상태로 관찰.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static MarkdownWorkspaceStore Workspace(string folder) { return new MarkdownWorkspaceStore(Path.Combine(folder, "workspace")); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Settings
    /// desc         : 비밀이 없는 동일한 새 설정으로 ABA 바이트 동일 조건을 만든다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 CLI 실행 파일이나 계정을 지정하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static AssistantSettings Settings() { return new AssistantSettings { CodexPath = "new.exe", Instructions = "new rules" }; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Lock
    /// desc         : 실제 생산 helper의 경로별 Mutex를 소스·고정 DLL 양쪽에서 획득한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 범위 끝의 Dispose를 실제 소유 스레드에서 수행.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IDisposable Lock(string path) { try { return (IDisposable)typeof(AssistantSettingsTransaction).GetMethod("AcquireSettingsLock", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { path }); } catch (TargetInvocationException error) { throw error.InnerException ?? error; } }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SaveCore
    /// desc         : 첫 거래의 Markdown 경계만 제어하고 실제 JSON 저장·복구·Mutex를 실행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 오류 주입 범위와 실제 제품 동작을 구분.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void SaveCore(AssistantSettingsStore store, AssistantSettings settings, Action write)
    { try { typeof(AssistantSettingsTransaction).GetMethod("SaveCore", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { store, settings, write }); } catch (TargetInvocationException error) { throw error.InnerException ?? error; } }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : StartChild
    /// desc         : 자기 검사 실행 파일만 별도 프로세스로 시작한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 쉘·계정·AI·외부 명령을 실행하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static Process StartChild(string mode, string folder)
    { return Process.Start(new ProcessStartInfo(typeof(SecureCodingChecks).Assembly.Location, "--settings-child " + mode + " \"" + folder + "\"") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = folder }); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WaitFile
    /// desc         : 실제 자식 프로세스의 경계 신호를 유한 시간만 기다린다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 검증 프로세스의 무한 대기 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void WaitFile(string path, int milliseconds = 5000) { var clock = Stopwatch.StartNew(); while (!File.Exists(path)) { if (clock.ElapsedMilliseconds > milliseconds) throw new TimeoutException("검사 프로세스 신호가 없습니다."); Thread.Sleep(10); } }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : StopChild
    /// desc         : 실패 경로에도 검사 자신이 시작한 프로세스만 정리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 IDE·다른 작업 프로세스는 종료하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void StopChild(Process process) { if (process == null) return; try { if (!process.HasExited) process.Kill(); process.WaitForExit(2000); } finally { process.Dispose(); } }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : HashFile
    /// desc         : 실행한 코드 어셈블리의 SHA-256을 통상적인 16진수로 남긴다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 후보 DLL의 검증 결과를 정확히 식별.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string HashFile(string path) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Call
    /// desc         : 소스 fixture와 제품 DLL에서 동일한 실제 비공개 helper를 호출하고 원래 예외를 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 테스트 안에 제한 해시 알고리즘을 재구현하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Call(Stream input) { try { return (string)compute.Invoke(null, new object[] { input }); } catch (TargetInvocationException error) { throw error.InnerException ?? error; } }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Expected
    /// desc         : 기존 표준 SHA-256 결과를 비교 기준으로 계산한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 정상 디스크 버전 문자열의 기존 Base64 계약 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Expected(byte[] bytes) { using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(bytes)); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Case
    /// desc         : 한 실제 경계의 통과·실패와 원인을 결과에 남기고 나머지 사례를 계속 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 전체 exit code와 관찰 근거를 함께 제공.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Case(string name, Action action)
    { try { action(); passed++; Results.Add(new { Name = name, Passed = true, Error = "" }); Console.WriteLine("PASS: " + name); } catch (Exception error) { failed++; Results.Add(new { Name = name, Passed = false, Error = error.ToString() }); Console.WriteLine("FAIL: " + name + " " + error.Message); } }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Require
    /// desc         : 출력 해시와 관찰한 읽기 횟수·바이트가 계약과 다르면 실패시킨다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 종료 여부만으로 bounded 읽기 성공을 오인하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Require(bool value) { if (!value) throw new InvalidOperationException("해시 또는 읽기 경계 관찰 결과가 다릅니다."); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Throws
    /// desc         : 예상 예외 형식만 실제 거부로 인정한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 엉뚱한 컴파일·reflection 오류를 보안 차단으로 오인하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static T Throws<T>(Action action) where T : Exception { try { action(); } catch (T error) { return error; } throw new InvalidOperationException("예상한 예외가 없습니다: " + typeof(T).Name); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProbeStream
    /// desc         : 큰 실제 파일 없이 길이·성장·무한·짧은 읽기·오류를 관찰하는 읽기 전용 control 스트림이다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 운영 파일이나 무한 메모리를 할당하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class ProbeStream : Stream
    {
        private readonly byte[] bytes; private readonly bool seek, infinite; private readonly long advertised; private readonly int maxChunk, failAt; private bool closed;
        internal int TotalRead, ReadCalls, MaxRequested;
        public override bool CanRead { get { return !closed; } }
        public override bool CanSeek { get { return seek; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { if (!seek) throw new NotSupportedException(); return advertised; } }
        public override long Position { get { return TotalRead; } set { throw new NotSupportedException(); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProbeStream
        /// desc         : 원문 배열과 관찰할 스트림 동작만 고정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 무한 모드도 요청된 버퍼 크기만 채움.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal ProbeStream(byte[] bytes, bool seek = false, long? advertised = null, bool infinite = false, int maxChunk = Int32.MaxValue, int failAt = -1)
        { this.bytes = bytes ?? new byte[0]; this.seek = seek; this.advertised = advertised ?? this.bytes.Length; this.infinite = infinite; this.maxChunk = maxChunk; this.failAt = failAt; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Read
        /// desc         : 읽기 횟수와 전달 바이트를 기록하고 지정한 길이·실패 경계를 그대로 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 생산 helper가 max+1 이상 요청하거나 읽는지 관찰.
        /// </summary>
        /// ---------------------------------------------------------------------
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (closed) throw new ObjectDisposedException("ProbeStream"); ReadCalls++; MaxRequested = Math.Max(MaxRequested, count);
            if (failAt >= 0 && TotalRead >= failAt) throw new IOException("합성 원문의 읽기 오류");
            int size = Math.Min(count, maxChunk); if (!infinite) size = Math.Min(size, bytes.Length - TotalRead);
            if (infinite) { for (int index = 0; index < size; index++) buffer[offset + index] = 0x5A; }
            else Array.Copy(bytes, TotalRead, buffer, offset, size);
            TotalRead += size; return size;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 호출자가 닫기 전 helper가 스트림을 임의 종료했는지 관찰한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 원문 스트림 수명 소유권 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override void Dispose(bool disposing) { closed = true; base.Dispose(disposing); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Flush
        /// desc         : 읽기 전용 fixture의 flush에는 부작용이 없다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Stream 추상 계약 구현.
        /// </summary>
        /// ---------------------------------------------------------------------
        public override void Flush() { }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Seek
        /// desc         : helper가 길이 외 임의 위치 변경을 시도하면 실패시킨다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 순차 읽기 계약 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SetLength
        /// desc         : 원문 길이 수정은 fixture에서도 허용하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 해시의 읽기 전용 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        public override void SetLength(long value) { throw new NotSupportedException(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Write
        /// desc         : helper가 원문에 쓰기를 시도하면 즉시 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 예외 경로를 포함한 원문 불변성 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }
}
