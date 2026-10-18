using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Services;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : SvnIntegrationChecks
/// desc         : 실제 SVN 클라이언트와 루프백 서버에서 승인 작업 및 격리 배포를 확인한다.
/// author       : CodexOrig
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexOrig : TASK-020 감사 F05 실제 네트워크 연동의 로컬 검증.
/// </summary>
/// ---------------------------------------------------------------------
internal static class SvnIntegrationChecks
{
    private static readonly List<object> Checks = new List<object>();
    private static string Root, Bin;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 테스트가 만든 서버만 종료하고 결과를 실제 종료 코드와 함께 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 로컬 서버의 수명 격리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        Root = Path.GetFullPath(args[0]); Bin = Path.GetFullPath(args[1]);
        try { RunAsync().GetAwaiter().GetResult(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); Checks.Add(new { Name = "unexpected", Passed = false, Error = error.Message }); return 1; }
        finally { File.WriteAllText(Path.Combine(Root, "result.json"), new JavaScriptSerializer().Serialize(new { ActualSvnServer = true, Address = "127.0.0.1 only", ProductionServerTested = false, Checks })); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 실제 연동의 성공과 실패 근거를 별도로 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 fixture와 실제 서버 검증 구분.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, bool condition) { Checks.Add(new { Name = name, Passed = condition }); Console.WriteLine((condition ? "PASS " : "FAIL ") + name); if (!condition) throw new Exception(name); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 명시한 테스트 실행 파일을 셸 해석 없이 실행한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 SVN 도구와 테스트 저장소 준비.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Run(string file, params string[] args)
    {
        var info = new ProcessStartInfo(file, String.Join(" ", args.Select(WorkspaceActionPlan.QuoteArgument))) { WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var process = Process.Start(info)) { var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); if (!process.WaitForExit(15000)) { process.Kill(); throw new TimeoutException("Local tool timeout"); } if (process.ExitCode != 0) throw new InvalidOperationException(error.Result); return output.Result; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunAsync
    /// desc         : 실제 루프백 체크아웃·커밋·업데이트와 정확히 승인된 격리 배포를 실행한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 서버·배포와 운영 환경 미검증의 경계.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static async Task RunAsync()
    {
        string repos = Path.Combine(Root, "Repositories"), repo = Path.Combine(repos, "repo"); Directory.CreateDirectory(repos);
        Run(Path.Combine(Bin, "svnadmin.exe"), "create", repo);
        File.WriteAllText(Path.Combine(repo, "conf", "svnserve.conf"), "[general]\nanon-access = write\nauth-access = write\nrealm = TASK020 isolated test\n");
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var info = new ProcessStartInfo(Path.Combine(Bin, "svnserve.exe"), String.Join(" ", new[] { "--daemon", "--foreground", "--listen-host", "127.0.0.1", "--listen-port", port.ToString(), "--root", repos }.Select(WorkspaceActionPlan.QuoteArgument))) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Root };
        using (var server = Process.Start(info)) {
            try {
                bool ready = false;
                for (int attempt = 0; attempt < 30 && !server.HasExited; attempt++) {
                    try { using (var client = new TcpClient()) { client.Connect(IPAddress.Loopback, port); ready = true; break; } } catch (SocketException) { }
                    await Task.Delay(100);
                }
                Check("actual svnserve listens on loopback only", ready);
                string seed = Path.Combine(Root, "Seed"); Directory.CreateDirectory(seed); File.WriteAllText(Path.Combine(seed, "Probe.txt"), "before");
                string url = "svn://127.0.0.1:" + port + "/repo", svn = Path.Combine(Bin, "svn.exe");
                Run(svn, "import", "--non-interactive", "--no-auth-cache", seed, url, "-m", "isolated initial data");
                string workspace = Path.Combine(Root, "Workspace"); Directory.CreateDirectory(workspace);
                var service = new WorkspaceActionService(); var policy = new WorkspacePermissionPolicy { AllowSourceControl = true, AllowDeployment = true };
                foreach (string checkout in new[] { "A", "B" }) {
                    var request = new WorkspaceActionRequest { Operation = WorkspaceOperation.SvnCheckout, ProjectDirectory = workspace, ExecutablePath = svn, Argument = url, DestinationDirectory = checkout };
                    var plan = service.BuildPlan(request, policy); var result = await service.RunApprovedAsync(plan, policy, CancellationToken.None);
                    Check("approved actual SVN network checkout " + checkout, result.Succeeded && File.ReadAllText(Path.Combine(workspace, checkout, "Probe.txt")) == "before");
                }
                string first = Path.Combine(workspace, "A"), second = Path.Combine(workspace, "B"); File.WriteAllText(Path.Combine(first, "Probe.txt"), "after");
                var commit = new WorkspaceActionRequest { Operation = WorkspaceOperation.SvnCommit, ProjectDirectory = first, ExecutablePath = svn, Message = "TASK020 actual isolated commit", Files = new[] { "Probe.txt" } };
                var committed = await service.RunApprovedAsync(service.BuildPlan(commit, policy), policy, CancellationToken.None);
                Check("approved actual SVN network commit creates revision 2", committed.Succeeded && Run(Path.Combine(Bin, "svnlook.exe"), "youngest", repo).Trim() == "2");
                var update = new WorkspaceActionRequest { Operation = WorkspaceOperation.SvnUpdate, ProjectDirectory = second, ExecutablePath = svn };
                var updated = await service.RunApprovedAsync(service.BuildPlan(update, policy), policy, CancellationToken.None);
                Check("approved actual SVN update receives committed file", updated.Succeeded && File.ReadAllText(Path.Combine(second, "Probe.txt")) == "after");
                try { service.BuildPlan(update, new WorkspacePermissionPolicy()); Check("SVN denied without explicit permission", false); } catch (InvalidOperationException) { Check("SVN denied without explicit permission", true); }
                string script = Path.Combine(workspace, "Deploy.ps1"); File.WriteAllText(script, "[IO.File]::WriteAllText((Join-Path $PWD 'isolated-deployed.txt'),'PASS')");
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
                var deploy = new WorkspaceActionRequest { Operation = WorkspaceOperation.Deploy, ProjectDirectory = workspace, ExecutablePath = powershell, DeploymentArguments = new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script } };
                var deployed = await service.RunApprovedAsync(service.BuildPlan(deploy, policy), policy, CancellationToken.None);
                Check("approved isolated deployment performs an actual command", deployed.Succeeded && File.ReadAllText(Path.Combine(workspace, "isolated-deployed.txt")) == "PASS");
            }
            finally { if (!server.HasExited) server.Kill(); server.WaitForExit(5000); }
            Check("only the test server is stopped after verification", server.HasExited);
        }
    }
}
