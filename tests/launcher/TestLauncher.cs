using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : TestSuite
/// desc         : 허용된 검사 ID·표시명·제품 파일 요구와 기본 실행 포함 여부를 보관한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : IDE와 실제 AI 검사는 전체 검사에서 제외.
/// </summary>
/// ---------------------------------------------------------------------
internal sealed class TestSuite
{
    internal readonly string Id, Title;
    internal readonly bool Default, Assembly, Vsix;
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TestSuite
    /// desc         : 변경되지 않는 검사 카탈로그 항목을 생성한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실행 경로는 별도 고정 wrapper에서 결정.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal TestSuite(string id, string title, bool included, bool assembly = false, bool vsix = false)
    { Id = id; Title = title; Default = included; Assembly = assembly; Vsix = vsix; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ToString
    /// desc         : GUI 목록에 명칭과 선택 실행 여부를 표시한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 외부 실행 검사를 명확히 구분.
    /// </summary>
    /// ---------------------------------------------------------------------
    public override string ToString() { return Title + "  [" + Id + "]" + (Default ? "" : " · 직접 선택"); }
}

/// ---------------------------------------------------------------------
/// <summary>
/// name         : LauncherOptions
/// desc         : 실행할 검사와 사용자가 지정한 제품·Codex 파일 경로를 보관한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 기본 경로 선택과 CLI 입력을 분리.
/// </summary>
/// ---------------------------------------------------------------------
internal sealed class LauncherOptions
{
    internal string Mode = "gui", Suite, Assembly, Vsix, Codex;
}

/// ---------------------------------------------------------------------
/// <summary>
/// name         : TestLauncher
/// desc         : 프로젝트 최상위에서 GUI 또는 CLI로 기존 검사를 재현하며 제품을 빌드하지 않는다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 고정 검사·불변 DLL·로그·취소·종료 코드 제공.
/// </summary>
/// ---------------------------------------------------------------------
internal static class TestLauncher
{
    internal static readonly string Root = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
    internal static readonly TestSuite[] Suites = {
        new TestSuite("compatibility", "지원 버전·패키지 정책", true),
        new TestSuite("build-entry", "빌드 실행 경로 fixture", true),
        new TestSuite("markdown", "Markdown 대화 저장·불러오기", true, true),
        new TestSuite("settings", "공통 규칙·개인 설정 저장", true, true),
        new TestSuite("workspace-actions", "Git·작업 권한·DB 경계", true, true),
        new TestSuite("editing", "소스 편집·CLI fixture·팝업", true, true),
        new TestSuite("foundation", "VSIX 등록·화면·테마", true, true, true),
        new TestSuite("experience", "입력·대화 탐색·UX", true, true),
        new TestSuite("ide", "실제 VS2017 별도 인스턴스", false, false, true),
        new TestSuite("live-codex", "실제 Codex 계정·AI 요청", false, true)
    };
    [DllImport("kernel32.dll")] private static extern bool FreeConsole();

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 인수 없는 실행은 GUI만 열며 명시한 CLI 검사만 실행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 성공0·검사실패1·입력오류2·취소130 전파.
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var options = Parse(args);
            if (options.Mode == "gui")
            {
                FreeConsole(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new LauncherForm(options)); return 0;
            }
            Console.OutputEncoding = new UTF8Encoding(false);
            if (options.Mode == "help") { Console.WriteLine("VisualStudio_AIChat_ExtentionTest.exe\n  --list\n  --run <suite> [--assembly <DLL>] [--vsix <VSIX>] [--codex <EXE>]\n  --all [--assembly <DLL>] [--vsix <VSIX>]\n인수 없이 실행하면 검정색 GUI가 열립니다. --all은 IDE·실제 AI 검사를 제외합니다."); return 0; }
            if (options.Mode == "list") { foreach (var suite in Suites) Console.WriteLine(suite.Id + "\t" + suite.Title + (suite.Default ? "\tdefault" : "\topt-in")); return 0; }
            using (var cancel = new CancellationTokenSource())
            {
                ConsoleCancelEventHandler handler = (sender, e) => { e.Cancel = true; cancel.Cancel(); };
                Console.CancelKeyPress += handler;
                try { return RunAsync(Select(options), options, Console.Write, cancel.Token).GetAwaiter().GetResult(); }
                finally { Console.CancelKeyPress -= handler; }
            }
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("검사를 중지했습니다."); return 130; }
        catch (Exception error) { Console.Error.WriteLine("실행 오류: " + error.Message); return 2; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Parse
    /// desc         : 정해진 CLI 옵션만 받아 셸 명령이나 임의 스크립트 입력을 허용하지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 중복 실행 모드·알 수 없는 옵션 거부.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static LauncherOptions Parse(string[] args)
    {
        var result = new LauncherOptions(); bool mode = false;
        for (int index = 0; index < args.Length; index++)
        {
            string item = args[index];
            if (item == "--all" || item == "--list" || item == "--help" || item == "--run")
            {
                if (mode) throw new ArgumentException("실행 모드는 하나만 선택하세요."); mode = true; result.Mode = item.Substring(2);
                if (item == "--run") { if (++index >= args.Length) throw new ArgumentException("--run 뒤에 검사 ID를 입력하세요."); result.Suite = args[index]; }
            }
            else if (item == "--assembly" || item == "--vsix" || item == "--codex")
            {
                if (++index >= args.Length) throw new ArgumentException(item + " 뒤에 파일 경로를 입력하세요.");
                if (item == "--assembly") result.Assembly = args[index]; else if (item == "--vsix") result.Vsix = args[index]; else result.Codex = args[index];
            }
            else throw new ArgumentException("알 수 없는 옵션: " + item);
        }
        return result;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Select
    /// desc         : 전체 검사는 기본 항목만, 단일 검사는 ID에 일치하는 항목만 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : IDE·실제 AI 실행의 자동 선택 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static TestSuite[] Select(LauncherOptions options)
    {
        if (options.Mode == "all") return Suites.Where(s => s.Default).ToArray();
        var selected = Suites.FirstOrDefault(s => s.Id == options.Suite);
        if (selected == null) throw new ArgumentException("검사 ID를 확인하세요. --list로 목록을 볼 수 있습니다.");
        return new[] { selected };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ResolveFile
    /// desc         : 실행기 위치를 기준으로 경로를 정규화하고 프로젝트 바깥의 입력을 거부한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 루트 폴더와 파일 존재 여부 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static string ResolveFile(string value, string extension)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("필요한 파일 경로가 없습니다.");
        string full = Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(Root, value));
        if (!full.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("제품 DLL·VSIX는 프로젝트 폴더 안의 파일을 선택하세요.");
        if (!string.Equals(Path.GetExtension(full), extension, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) throw new FileNotFoundException("파일을 확인하세요: " + full);
        return full;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Hash
    /// desc         : 실제 검사 DLL과 VSIX의 SHA256을 실행 로그에 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 공유 빌드 출력과 검사 대상 식별.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Hash(string path)
    { using (var input = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", ""); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PrepareInputs
    /// desc         : 배포 VSIX를 우선 사용하고 DLL만 고유 검사 폴더에 추출하며 제품 출력은 건드리지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 배포본 부재 시 기존 Release 산출물 사용, 자동 빌드 없음.
    /// update date  : 2026-10-18 CodexOrig : TASK-014 v0.6.0 배포본을 기본 검사 대상으로 선택.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void PrepareInputs(TestSuite[] suites, LauncherOptions options, string output, Action<string> log)
    {
        bool explicitVsix = !string.IsNullOrWhiteSpace(options.Vsix);
        if (explicitVsix) options.Vsix = ResolveFile(options.Vsix, ".vsix");
        if (suites.Any(s => s.Vsix) || (suites.Any(s => s.Assembly) && string.IsNullOrWhiteSpace(options.Assembly)))
        {
            if (string.IsNullOrWhiteSpace(options.Vsix)) options.Vsix = File.Exists(Path.Combine(Root, "releases/v0.6.0/VS2017.AIAssistant-0.6.0.vsix")) ? "releases/v0.6.0/VS2017.AIAssistant-0.6.0.vsix" : "src/VS2017.AIAssistant/bin/Release/VS2017.AIAssistant.vsix";
            if (suites.Any(s => s.Vsix) || File.Exists(Path.Combine(Root, options.Vsix))) options.Vsix = ResolveFile(options.Vsix, ".vsix");
        }
        if (suites.Any(s => s.Assembly))
        {
            if (string.IsNullOrWhiteSpace(options.Assembly))
            {
                if (!explicitVsix && File.Exists(Path.Combine(Root, "releases/v0.6.0/VS2017.AIAssistant.dll"))) options.Assembly = "releases/v0.6.0/VS2017.AIAssistant.dll";
                else if (!string.IsNullOrWhiteSpace(options.Vsix) && File.Exists(options.Vsix))
                {
                    string destination = Path.Combine(output, "ProductReference"); Directory.CreateDirectory(destination);
                    options.Assembly = Path.Combine(destination, "VS2017.AIAssistant.dll");
                    using (var zip = ZipFile.OpenRead(options.Vsix))
                    {
                        var entry = zip.GetEntry("VS2017.AIAssistant.dll"); if (entry == null || entry.Length > 4194304) throw new InvalidDataException("VSIX의 제품 DLL을 확인하세요.");
                        using (var input = entry.Open()) using (var file = File.Create(options.Assembly))
                        { var buffer = new byte[4096]; int count, total = 0; while ((count = input.Read(buffer, 0, buffer.Length)) > 0) { total += count; if (total > 4194304) throw new InvalidDataException("제품 DLL 크기 제한을 초과했습니다."); file.Write(buffer, 0, count); } }
                    }
                }
                else options.Assembly = "src/VS2017.AIAssistant/bin/Release/VS2017.AIAssistant.dll";
            }
            options.Assembly = ResolveFile(options.Assembly, ".dll"); log("DLL: " + options.Assembly + "\nSHA256: " + Hash(options.Assembly) + "\n");
        }
        if (!string.IsNullOrWhiteSpace(options.Vsix) && File.Exists(options.Vsix)) log("VSIX: " + options.Vsix + "\nSHA256: " + Hash(options.Vsix) + "\n");
        if (suites.Any(s => s.Id == "live-codex"))
        {
            if (string.IsNullOrWhiteSpace(options.Codex)) throw new ArgumentException("실제 AI 검사는 --codex 또는 GUI의 Codex .exe 경로를 지정하세요.");
            options.Codex = Path.GetFullPath(options.Codex);
            if (!File.Exists(options.Codex) || !string.Equals(Path.GetExtension(options.Codex), ".exe", StringComparison.OrdinalIgnoreCase)) throw new FileNotFoundException("네이티브 Codex .exe를 선택하세요.");
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunAsync
    /// desc         : 검사를 순서대로 실행하고 로그·해시·개별 종료 코드·요약을 고유 폴더에 저장한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실패를 종료 코드로 전파하고 취소 시 후속 검사 중단.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static async Task<int> RunAsync(TestSuite[] suites, LauncherOptions options, Action<string> display, CancellationToken cancellation)
    {
        string output;
        do { output = Path.Combine(Root, "artifacts", "tl", Guid.NewGuid().ToString("N").Substring(0, 10)); } while (Directory.Exists(output));
        Directory.CreateDirectory(output);
        string logPath = Path.Combine(output, "launcher.log"); var gate = new object();
        using (var writer = new StreamWriter(logPath, false, new UTF8Encoding(true)))
        {
            writer.AutoFlush = true;
            Action<string> log = text => { lock (gate) { writer.Write(text); display(text); } };
            log("검사 시작: " + DateTimeOffset.Now.ToString("o") + "\n검사 로그: " + logPath + "\n");
            try
            {
                ResolveFile("tests/launcher/Invoke-LauncherSuite.ps1", ".ps1"); PrepareInputs(suites, options, output, log);
                int failures = 0;
                foreach (var suite in suites)
                {
                    cancellation.ThrowIfCancellationRequested(); string suiteOutput = Path.Combine(output, suite.Id); Directory.CreateDirectory(suiteOutput);
                    log("\n▶ " + suite.Id + " · " + suite.Title + "\n");
                    int code = await RunChildAsync(suite, options, suiteOutput, log, cancellation).ConfigureAwait(false);
                    if (code != 0) failures++; log("\n" + (code == 0 ? "✓ 통과" : "⚠ 실패") + " " + suite.Id + " · 종료 코드 " + code + "\n");
                    File.AppendAllText(Path.Combine(output, "summary.txt"), suite.Id + "\texit=" + code + Environment.NewLine, Encoding.UTF8);
                }
                log("\n완료: " + (suites.Length - failures) + "/" + suites.Length + " 검사 묶음 통과\n"); return failures == 0 ? 0 : 1;
            }
            catch (OperationCanceledException) { log("\n■ 검사를 중지했습니다. 후속 검사는 실행하지 않습니다.\n"); return 130; }
            catch (Exception error) { log("\n⚠ 실행 오류: " + error.Message + "\n"); return 1; }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunChildAsync
    /// desc         : 정해진 PowerShell 5.1을 셸 없이 실행하며 취소·닫기·시간 제한 시 자식 트리도 종료한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : -STA·고정 wrapper·인수 인코딩·15분 제한 적용.
    /// update date  : 2026-10-18 CodexOrig : TASK-014 PowerShell7의 모듈 경로를 넘기지 않아 Windows PowerShell5.1 기본 모듈 복원.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static async Task<int> RunChildAsync(TestSuite suite, LauncherOptions options, string output, Action<string> log, CancellationToken cancellation)
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string powershell = Path.Combine(windows, Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var args = new List<string> { "-NoLogo", "-NoProfile", "-STA", "-ExecutionPolicy", "Bypass", "-File", ResolveFile("tests/launcher/Invoke-LauncherSuite.ps1", ".ps1"), "-Suite", suite.Id, "-OutputDirectory", output };
        if (suite.Assembly) { args.Add("-AssemblyPath"); args.Add(options.Assembly); }
        if (suite.Vsix) { args.Add("-VsixPath"); args.Add(options.Vsix); }
        if (suite.Id == "live-codex") { args.Add("-CodexPath"); args.Add(options.Codex); }
        var info = new ProcessStartInfo(powershell, string.Join(" ", args.Select(Quote))) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Root, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        // PowerShell 7에서 실행해도 자식 Windows PowerShell 5.1은 자체 기본 모듈 경로를 구성한다.
        info.EnvironmentVariables.Remove("PSModulePath");
        using (var child = new Process { StartInfo = info }) using (var job = new LauncherJob()) using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
        {
            deadline.CancelAfter(TimeSpan.FromMinutes(15));
            try
            {
                if (!child.Start()) throw new InvalidOperationException("PowerShell을 시작하지 못했습니다."); job.Attach(child); child.StandardInput.Close();
                using (deadline.Token.Register(job.Dispose))
                {
                    Task stdout = PumpAsync(child.StandardOutput, log), stderr = PumpAsync(child.StandardError, log);
                    while (!child.HasExited) { if (stdout.IsFaulted) await stdout.ConfigureAwait(false); if (stderr.IsFaulted) await stderr.ConfigureAwait(false); await Task.Delay(100, deadline.Token).ConfigureAwait(false); }
                    await Task.WhenAll(stdout, stderr).ConfigureAwait(false); deadline.Token.ThrowIfCancellationRequested(); return child.ExitCode;
                }
            }
            catch (OperationCanceledException) { if (cancellation.IsCancellationRequested) throw; log("⚠ 검사 제한 시간 15분 초과\n"); return 124; }
            finally { try { if (!child.HasExited) child.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PumpAsync
    /// desc         : 각 출력 스트림을 8MB로 제한하고 GUI와 로그에 즉시 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 표준 출력·오류의 교착 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static async Task PumpAsync(StreamReader reader, Action<string> log)
    { var buffer = new char[2048]; int count, total = 0; while ((count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0) { total += count; if (total > 8388608) throw new InvalidDataException("검사 출력 제한을 초과했습니다."); log(new string(buffer, 0, count)); } }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Quote
    /// desc         : Windows 네이티브 argv 규칙으로 인수를 감싸 셸 특수문자를 문자로 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 따옴표·끝 역슬래시·빈 문자열 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Quote(string value)
    {
        if (value == null || value.Any(c => c == '\0' || c == '\r' || c == '\n')) throw new ArgumentException("파일 경로에 제어 문자를 사용할 수 없습니다.");
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char ch in value) { if (ch == '\\') { slashes++; continue; } result.Append('\\', ch == '"' ? slashes * 2 + 1 : slashes); result.Append(ch); slashes = 0; }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
}

/// ---------------------------------------------------------------------
/// <summary>
/// name         : LauncherForm
/// desc         : 검정색 GUI에서 검사 선택·실행·중지와 실시간 로그를 제공한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 열기만으로 검사를 실행하지 않으며 실행 중 닫기는 취소 후 처리.
/// update date  : 2026-10-18 CodexOrig : TASK-014 기본 배포 버전 안내를 v0.6.0으로 갱신.
/// </summary>
/// ---------------------------------------------------------------------
internal sealed class LauncherForm : Form
{
    private readonly CheckedListBox suites = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
    private readonly TextBox assembly = new TextBox { Dock = DockStyle.Fill }, vsix = new TextBox { Dock = DockStyle.Fill }, codex = new TextBox { Dock = DockStyle.Fill };
    private readonly TextBox output = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both };
    private readonly Button selected = new Button { Text = "▶ 선택 검사 실행", AutoSize = true }, all = new Button { Text = "✓ 기본 전체 검사", AutoSize = true }, stop = new Button { Text = "■ 중지", AutoSize = true, Enabled = false };
    private readonly Label status = new Label { Text = "준비 · 실행할 검사를 선택하세요.", AutoSize = true };
    private CancellationTokenSource cancellation;
    private bool closing;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : LauncherForm
    /// desc         : 검사 목록·제품 경로·동작·로그를 크기 조절 가능한 한 화면에 배치한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 IDE·AI 검사는 명시적으로 선택해야 실행.
    /// update date  : 2026-10-18 CodexOrig : TASK-014 기본 배포 버전 안내를 v0.6.0으로 갱신.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal LauncherForm(LauncherOptions options)
    {
        Text = "Visual Studio AI Chat · 테스트 실행기"; Width = 960; Height = 850; MinimumSize = new Size(700, 620); StartPosition = FormStartPosition.CenterScreen; Font = new Font("맑은 고딕", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 10 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        for (int index = 2; index < 8; index++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "실행할 검사를 고르세요 · IDE·실제 AI 검사는 직접 선택할 때만 실행됩니다.", AutoSize = true, Padding = new Padding(0, 0, 0, 8) }, 0, 0);
        foreach (var suite in TestLauncher.Suites) suites.Items.Add(suite, false); layout.Controls.Add(suites, 0, 1);
        layout.Controls.Add(new Label { Text = "DLL / VSIX 경로 (비우면 배포 v0.6.0 → 기존 Release 산출물, 자동 빌드 없음)", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, 0, 2);
        var paths = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 3 }; paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95)); paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        paths.Controls.Add(new Label { Text = "제품 DLL", AutoSize = true }, 0, 0); paths.Controls.Add(assembly, 1, 0); paths.Controls.Add(new Label { Text = "제품 VSIX", AutoSize = true }, 0, 1); paths.Controls.Add(vsix, 1, 1); paths.Controls.Add(new Label { Text = "Codex .exe", AutoSize = true }, 0, 2); paths.Controls.Add(codex, 1, 2); layout.Controls.Add(paths, 0, 3);
        assembly.Text = options.Assembly ?? ""; vsix.Text = options.Vsix ?? ""; codex.Text = options.Codex ?? "";
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 8, 0, 8) }; actions.Controls.Add(selected); actions.Controls.Add(all); actions.Controls.Add(stop); layout.Controls.Add(actions, 0, 4);
        layout.Controls.Add(new Label { Text = "DLL·VSIX는 이 프로젝트 안의 파일을 지정하세요. 실제 AI 검사에는 로그인된 네이티브 Codex가 필요합니다.", AutoSize = true }, 0, 5);
        layout.Controls.Add(new Label { Text = "실행 로그 · 전체 기록은 artifacts/tl/<실행 ID> 에 저장됩니다.", AutoSize = true, Padding = new Padding(0, 8, 0, 4) }, 0, 6);
        layout.Controls.Add(new Label { Text = "CLI: --list | --run <검사 ID> | --all | --assembly <DLL> | --vsix <VSIX>", AutoSize = true }, 0, 7);
        output.Font = new Font("Consolas", 10); layout.Controls.Add(output, 0, 8); layout.Controls.Add(status, 0, 9); Theme(this);
        selected.Click += async (sender, e) => await StartAsync(suites.CheckedItems.Cast<TestSuite>().ToArray());
        all.Click += async (sender, e) => await StartAsync(TestLauncher.Suites.Where(s => s.Default).ToArray());
        stop.Click += (sender, e) => { if (cancellation != null) { status.Text = "중지 중 · 실행한 자식 프로세스를 정리합니다."; cancellation.Cancel(); } };
        FormClosing += (sender, e) => { if (cancellation != null) { e.Cancel = true; closing = true; cancellation.Cancel(); status.Text = "종료 중 · 검사 프로세스를 정리합니다."; } };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Theme
    /// desc         : 기본 WinForms 컨트롤을 동일한 검정 계열과 읽을 수 있는 글자색으로 지정한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 버튼·입력·목록·로그의 일관된 명도 적용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Theme(Control parent)
    {
        parent.BackColor = Color.FromArgb(25, 25, 25); parent.ForeColor = Color.Gainsboro;
        if (parent is TextBox || parent is CheckedListBox) parent.BackColor = Color.FromArgb(35, 35, 35);
        var button = parent as Button; if (button != null) { button.FlatStyle = FlatStyle.Flat; button.BackColor = Color.FromArgb(50, 50, 50); button.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90); }
        foreach (Control child in parent.Controls) Theme(child);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : StartAsync
    /// desc         : 명시적으로 고른 검사만 시작하고 실행 중 설정 변경과 중복 실행을 막는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 완료·실패·취소 코드를 상태줄에 표시.
    /// </summary>
    /// ---------------------------------------------------------------------
    private async Task StartAsync(TestSuite[] chosen)
    {
        if (cancellation != null) return; if (chosen.Length == 0) { status.Text = "먼저 검사 항목을 선택하세요."; return; }
        cancellation = new CancellationTokenSource(); selected.Enabled = all.Enabled = suites.Enabled = assembly.Enabled = vsix.Enabled = codex.Enabled = false; stop.Enabled = true; output.Clear(); status.Text = "검사 실행 중…";
        try { int code = await TestLauncher.RunAsync(chosen, new LauncherOptions { Assembly = assembly.Text, Vsix = vsix.Text, Codex = codex.Text }, Append, cancellation.Token); status.Text = code == 0 ? "✓ 모든 선택 검사 통과 · 종료 코드 0" : code == 130 ? "■ 검사 중지 · 종료 코드 130" : "⚠ 검사 실패 · 종료 코드 " + code; }
        catch (Exception error) { status.Text = "⚠ " + error.Message; }
        finally { cancellation.Dispose(); cancellation = null; selected.Enabled = all.Enabled = suites.Enabled = assembly.Enabled = vsix.Enabled = codex.Enabled = true; stop.Enabled = false; if (closing) Close(); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Append
    /// desc         : 작업 스레드의 출력을 UI 스레드로 보내고 화면 로그 메모리를 제한한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 원본 전체 로그는 파일에 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private void Append(string text)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired) { BeginInvoke(new Action<string>(Append), text); return; }
        if (output.TextLength > 524288) output.Text = output.Text.Substring(output.TextLength - 262144); output.AppendText(text);
    }
}

/// ---------------------------------------------------------------------
/// <summary>
/// name         : LauncherJob
/// desc         : 실행기가 소유한 검사 프로세스와 하위 프로세스를 Windows Job으로 묶는다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 중지·닫기 시 자신의 검사 프로세스만 정리.
/// </summary>
/// ---------------------------------------------------------------------
internal sealed class LauncherJob : IDisposable
{
    private readonly SafeFileHandle handle;
    [StructLayout(LayoutKind.Sequential)] private struct Basic { internal long ProcessTime, JobTime; internal uint Flags; internal UIntPtr Minimum, Maximum; internal uint Active; internal UIntPtr Affinity; internal uint Priority, Scheduling; }
    [StructLayout(LayoutKind.Sequential)] private struct Io { internal ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct Extended { internal Basic Basic; internal Io Io; internal UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(SafeFileHandle job, int kind, ref Extended data, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : LauncherJob
    /// desc         : 마지막 핸들이 닫히면 소유한 하위 프로세스도 종료하도록 지정한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : Job 생성 실패 시 검사 시작 차단.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal LauncherJob()
    {
        handle = CreateJobObject(IntPtr.Zero, null); if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        var data = new Extended(); data.Basic.Flags = 0x2000;
        if (!SetInformationJobObject(handle, 9, ref data, (uint)Marshal.SizeOf(data))) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new System.ComponentModel.Win32Exception(error); }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Attach
    /// desc         : 시작한 검사 프로세스만 Job에 연결한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 기존 사용자 IDE나 셸 프로세스에는 접근하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal void Attach(Process process) { if (!AssignProcessToJobObject(handle, process.Handle)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Dispose
    /// desc         : Job 핸들을 해제하여 자기 검사 프로세스 트리를 정리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : SafeHandle로 중복 취소도 안전하게 처리.
    /// </summary>
    /// ---------------------------------------------------------------------
    public void Dispose() { handle.Dispose(); }
}
