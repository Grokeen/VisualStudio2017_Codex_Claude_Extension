using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VS2017.AIAssistant.Services;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : WorkspaceActionTests
/// desc         : 임시 Git 저장소와 독립 네이티브 fixture로 프로젝트 작업의 범위·권한을 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 실제 원격 서버·DB·배포 환경을 사용하지 않는 회귀 검사.
/// </summary>
/// ---------------------------------------------------------------------
internal static class WorkspaceActionTests
{
    private static int passed;
    private static string root, git, fixture, tfsFixture;
    private static readonly WorkspacePermissionPolicy allowed = new WorkspacePermissionPolicy { AllowSourceControl = true, AllowDeployment = true };

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : fixture 모드와 검증 모드를 분리하며 실패 시 종료 코드 1을 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 인수 왕복·취소·출력 제한을 프로세스 경계에서 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "fixture")
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            if (args[1] == "hang") { if (args.Length > 2) File.WriteAllText(args[2], Process.GetCurrentProcess().Id.ToString()); Thread.Sleep(10000); return 0; }
            if (args[1] == "large") { Console.Write(new string('x', 140000)); return 0; }
            if (args[1] == "fail") { Console.Error.Write("fixture failure"); return 7; }
            foreach (string value in args.Skip(2)) Console.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
            return 0;
        }
        try {
            root = args[0]; git = args[1]; fixture = args[2]; tfsFixture = args[3]; Run().GetAwaiter().GetResult();
#if PRODUCT
            WorkspaceDialogChecks.Run(root, git, Check);
#endif
            Console.WriteLine("PASS " + passed + " workspace action checks"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 입력 차단·실제 로컬 Git 명령·fixture 종료·DB 권한 사전 검사를 수행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 프로젝트 외부 파일과 미추적 파일을 건드리지 않음 확인.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static async Task Run()
    {
        var service = new WorkspaceActionService();
        var request = Request(WorkspaceOperation.GitFetch);
        Reject(() => service.BuildPlan(request, new WorkspacePermissionPolicy()), "SCM default denied");
        request = Request(WorkspaceOperation.Deploy); request.ExecutablePath = fixture;
        Reject(() => service.BuildPlan(request, new WorkspacePermissionPolicy()), "Deployment default denied");
        request.ExecutablePath = "cmd.cmd"; Reject(() => service.BuildPlan(request, allowed), "Batch denied");
        request = Request(WorkspaceOperation.GitCommit); request.Message = "message";
        Reject(() => service.BuildPlan(request, allowed), "Commit requires explicit files");
        request.Files = new[] { "..\\outside.cs" }; Reject(() => service.BuildPlan(request, allowed), "Parent escape denied");
        request.Files = new[] { "*.cs" }; Reject(() => service.BuildPlan(request, allowed), "Wildcard denied");
        request.Files = new[] { root }; Reject(() => service.BuildPlan(request, allowed), "Root folder denied");
        request = Request(WorkspaceOperation.GitCheckout); request.Argument = "--orphan"; Reject(() => service.BuildPlan(request, allowed), "Option branch denied");
        request = Request(WorkspaceOperation.GitPush); request.Argument = "--force"; Reject(() => service.BuildPlan(request, allowed), "Option remote denied");
        request = Request(WorkspaceOperation.GitFetch); request.ProjectDirectory = Path.Combine(root, "missing"); Reject(() => service.BuildPlan(request, allowed), "Missing project denied");
        await RejectAsync(() => service.ReadSqlSchemaAsync("invalid", new WorkspacePermissionPolicy(), CancellationToken.None), "Database default denied before connect");
        await RejectAsync(() => service.ReadSqlSchemaAsync("Integrated Security=true", new WorkspacePermissionPolicy { AllowDatabaseSchema = true }, CancellationToken.None), "Database requires server and catalog");
        await RejectAsync(() => service.ReadSqlSchemaAsync("Server=unused;Database=unused;AttachDBFilename=C:\\unused.mdf", new WorkspacePermissionPolicy { AllowDatabaseSchema = true }, CancellationToken.None), "Database implicit file attach rejected before connect", typeof(ArgumentException));

        string local = Path.Combine(root, "local 한글"), bare = Path.Combine(root, "remote.git"), other = Path.Combine(root, "other");
        Directory.CreateDirectory(local); Raw(root, "init", "--bare", bare); Raw(local, "init"); Configure(local);
        File.WriteAllText(Path.Combine(local, "one.cs"), "first\n"); File.WriteAllText(Path.Combine(local, "two.cs"), "second\n");
        Raw(local, "add", "--", "one.cs", "two.cs"); Raw(local, "commit", "-m", "initial"); Raw(local, "branch", "-M", "main");
        Raw(local, "remote", "add", "origin", bare); Raw(local, "push", "--set-upstream", "origin", "main");
        request = Request(WorkspaceOperation.GitFetch, local); request.Argument = "origin";
        Check((await service.RunAsync(request, allowed, CancellationToken.None)).Succeeded, "Git fetch actual local bare remote");
        File.WriteAllText(Path.Combine(local, "one.cs"), "changed\n"); File.WriteAllText(Path.Combine(local, "two.cs"), "staged unrelated\n");
        File.WriteAllText(Path.Combine(local, "untracked.cs"), "not included\n"); Raw(local, "add", "--", "two.cs");
        request = Request(WorkspaceOperation.GitCommit, local); request.Message = "한글 \"message\" $(literal)"; request.Files = new[] { "one.cs" };
        Check((await service.RunAsync(request, allowed, CancellationToken.None)).Succeeded, "Git commit selected tracked file");
        Check(Raw(local, "show", "--format=", "--name-only", "HEAD").Trim() == "one.cs", "Commit excludes other staged file");
        Check(Raw(local, "diff", "--cached", "--name-only").Trim() == "two.cs", "Other staged content preserved");
        Check(Raw(local, "ls-files", "--", "untracked.cs").Trim().Length == 0, "Untracked file not added");
        Raw(local, "reset", "HEAD", "--", "two.cs"); Raw(local, "checkout", "--", "two.cs");
        request = Request(WorkspaceOperation.GitPush, local); request.Argument = "origin";
        Check((await service.RunAsync(request, allowed, CancellationToken.None)).Succeeded, "Git push actual local bare remote");
        Raw(root, "clone", "--branch", "main", bare, other); Configure(other); File.WriteAllText(Path.Combine(other, "one.cs"), "remote change\n");
        Raw(other, "add", "--", "one.cs"); Raw(other, "commit", "-m", "remote update"); Raw(other, "push");
        request = Request(WorkspaceOperation.GitPull, local);
        var pullResult = await service.RunAsync(request, allowed, CancellationToken.None);
        Check(pullResult.Succeeded && File.ReadAllText(Path.Combine(local, "one.cs")).Trim() == "remote change", "Git pull ff-only actual local remote " + pullResult.Error);
        Raw(local, "branch", "feature"); request = Request(WorkspaceOperation.GitCheckout, local); request.Argument = "feature";
        Check((await service.RunAsync(request, allowed, CancellationToken.None)).Succeeded && Raw(local, "branch", "--show-current").Trim() == "feature", "Git checkout existing branch");

        foreach (WorkspaceOperation value in new[] { WorkspaceOperation.SvnUpdate, WorkspaceOperation.SvnCommit, WorkspaceOperation.TfsGet, WorkspaceOperation.TfsCheckout, WorkspaceOperation.TfsCheckin })
        {
            request = Request(value); request.ExecutablePath = fixture; request.Message = "checked message"; request.Files = new[] { "explicit.cs" };
            var plan = service.BuildPlan(request, allowed); Check(!plan.Arguments.Contains("/force") && !plan.Arguments.Contains("/override") && !plan.Arguments.Contains("--force"), value + " command no forced override");
        }
        string previousPath = Environment.GetEnvironmentVariable("PATH"), previousVs = Environment.GetEnvironmentVariable("VSINSTALLDIR");
        try {
            string install = Path.Combine(root, "Installed VS");
            string tf = Path.Combine(install, @"Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(tf)); File.Copy(tfsFixture, tf);
            Environment.SetEnvironmentVariable("VSINSTALLDIR", install); Environment.SetEnvironmentVariable("PATH", "");
            foreach (WorkspaceOperation value in new[] { WorkspaceOperation.TfsGet, WorkspaceOperation.TfsCheckout, WorkspaceOperation.TfsCheckin }) {
                request = Request(value); request.ExecutablePath = ""; request.Message = "한글 체크인"; request.Files = new[] { "explicit.cs" };
                var plan = service.BuildPlan(request, allowed);
                Check(plan.ExecutablePath == tf, value + " discovers installed TF outside PATH");
                var execution = await service.RunAsync(request, allowed, CancellationToken.None);
                string[] vector = execution.Output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Select(x => Encoding.UTF8.GetString(Convert.FromBase64String(x))).ToArray();
                Check(execution.Succeeded && vector[0] == (value == WorkspaceOperation.TfsGet ? "get" : value == WorkspaceOperation.TfsCheckout ? "checkout" : "checkin") &&
                    (value != WorkspaceOperation.TfsCheckin || vector.Contains("/comment:한글 체크인")), value + " actual local fixture receives command and exact Unicode comment");
            }
        }
        finally { Environment.SetEnvironmentVariable("PATH", previousPath); Environment.SetEnvironmentVariable("VSINSTALLDIR", previousVs); }
        request = Request(WorkspaceOperation.SvnCheckout); request.ExecutablePath = fixture; request.Argument = "https://example.invalid/repo"; request.DestinationDirectory = "new-copy";
        Check(service.BuildPlan(request, allowed).Arguments.Contains("new-copy"), "SVN checkout explicit new child");
        request.DestinationDirectory = "..\\escape"; Reject(() => service.BuildPlan(request, allowed), "SVN checkout parent escape denied");
        request = Request(WorkspaceOperation.TfsCheckin); request.ExecutablePath = fixture; request.Files = new[] { "explicit.cs" }; request.Message = "@unintended-comment-file";
        Reject(() => service.BuildPlan(request, allowed), "TFVC comment file reference denied");
        request.Message = "message"; request.Files = new[] { "explicit.cs;C12" }; Reject(() => service.BuildPlan(request, allowed), "TFVC implicit version spec denied");

        string[] values = { "한글 space", "a\"b", "trailing\\", "", "$(literal)&whoami", "\\\"" };
        request = Request(WorkspaceOperation.Deploy); request.ExecutablePath = fixture; request.DeploymentArguments = new[] { "fixture", "echo" }.Concat(values).ToArray();
        var result = await service.RunAsync(request, allowed, CancellationToken.None);
        string[] decoded = result.Output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Take(values.Length).Select(s => Encoding.UTF8.GetString(Convert.FromBase64String(s))).ToArray();
        Check(values.SequenceEqual(decoded), "Windows argv Unicode quotes slash empty literal roundtrip");
        request.DeploymentArguments = new[] { "fixture", "fail" }; result = await service.RunAsync(request, allowed, CancellationToken.None);
        Check(!result.Succeeded && result.ExitCode == 7 && result.Error.Contains("fixture failure"), "Nonzero result preserved");
        request.DeploymentArguments = new[] { "fixture", "large" }; await RejectAsync(() => service.RunAsync(request, allowed, CancellationToken.None), "Oversize stdout rejected", typeof(InvalidOperationException));
        string timeoutMarker = Path.Combine(root, "timeout-pid.txt"), cancelMarker = Path.Combine(root, "cancel-pid.txt");
        request.DeploymentArguments = new[] { "fixture", "hang", timeoutMarker };
        var timer = Stopwatch.StartNew(); await RejectAsync(() => new WorkspaceActionService(TimeSpan.FromMilliseconds(500)).RunAsync(request, allowed, CancellationToken.None), "Timeout terminates fixture", typeof(TimeoutException));
        Check(timer.Elapsed < TimeSpan.FromSeconds(4), "Timeout returns promptly");
        Check(Exited(timeoutMarker), "Timed out native process is gone");
        request.DeploymentArguments = new[] { "fixture", "hang", cancelMarker };
        using (var cancel = new CancellationTokenSource(500)) await RejectAsync(() => service.RunAsync(request, allowed, cancel.Token), "Cancellation terminates fixture", typeof(OperationCanceledException));
        Check(Exited(cancelMarker), "Cancelled native process is gone");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Request
    /// desc         : 격리된 테스트 폴더를 기본 대상으로 사용한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 사용자 저장소와 분리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static WorkspaceActionRequest Request(WorkspaceOperation operation, string directory = null)
    { return new WorkspaceActionRequest { Operation = operation, ProjectDirectory = directory ?? root, ExecutablePath = git }; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Configure
    /// desc         : 합성 저장소에만 커밋 작성자 정보를 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 전역 Git 설정 변경 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Configure(string directory) { Raw(directory, "config", "user.name", "Fixture"); Raw(directory, "config", "user.email", "fixture@example.invalid"); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Raw
    /// desc         : 합성 Git 저장소 준비와 결과 확인만 네이티브 명령으로 수행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 셸을 거치지 않는 고정 인수 실행.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Raw(string directory, params string[] args)
    {
        using (var process = Process.Start(new ProcessStartInfo(git, string.Join(" ", args.Select(WorkspaceActionPlan.QuoteArgument))) { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }))
        { string output = process.StandardOutput.ReadToEnd(), error = process.StandardError.ReadToEnd(); process.WaitForExit(); if (process.ExitCode != 0) throw new Exception("Fixture Git failed: " + error); return output; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 기대 결과를 이름과 함께 남겨 실패 위치를 식별한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 개별 회귀 검사 집계.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Reject
    /// desc         : 유효하지 않은 요청이 실행 전에 거부되는지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 예외 없는 허용은 테스트 실패.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (Exception) { rejected = true; } Check(rejected, name); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RejectAsync
    /// desc         : 비동기 작업의 권한 오류·제한·취소를 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 프로세스 수명 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static async Task RejectAsync(Func<Task> action, string name, Type expected = null) { bool rejected = false; try { await action(); } catch (Exception error) { rejected = expected == null || expected.IsInstanceOfType(error); } Check(rejected, name); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Exited
    /// desc         : fixture가 기록한 PID를 사용하여 취소 후 실제 종료를 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 자기 테스트 프로세스만 조회하며 종료 명령을 보내지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool Exited(string marker)
    {
        if (!File.Exists(marker)) return false; int id = int.Parse(File.ReadAllText(marker));
        try { using (var process = Process.GetProcessById(id)) return process.WaitForExit(2000); } catch (ArgumentException) { return true; }
    }
}
