using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VS2017.AIAssistant.Services;

namespace CodexCode.Task021
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SecureExecutionResult
    /// desc         : 합성 경로·실행 파일로 검증한 승인 계획과 환경 격리 결과를 보관한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-021 승인 전후 명령·거부 여부와 실제 재현 경로 기록.
    /// update date  : 2026-10-18 CodexCode : 실제 합성 native 인수·환경·취소 검사 결과 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class SecureExecutionResult
    {
        public bool Succeeded;
        public List<string> Passed = new List<string>();
        public List<string> Failures = new List<string>();
        public List<object> Evidence = new List<object>();
        public string Error;
        public bool CwdRestored;
        public int SyntheticProcessesStarted;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SecureExecutionChecks
    /// desc         : 자식 PowerShell의 합성 A/B 폴더와 출력 전용 exe로 승인 경계를 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-021 BuildPlan만 호출하며 Git·SVN·배포 실행 금지.
    /// update date  : 2026-10-18 CodexCode : 운영 도구 대신 독립 fixture를 실제 서비스 경로로 실행해 보정 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class SecureExecutionChecks
    {
        private static readonly string[] GitEnvironment = { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_COMMON_DIR", "GIT_NAMESPACE" };

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : 실패 한 건이 나머지 경계 검사를 숨기지 않도록 결과를 누적한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 합성 검증 판정 공통 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Check(SecureExecutionResult result, bool condition, string label)
        { if (condition) result.Passed.Add(label); else result.Failures.Add(label); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Request
        /// desc         : 운영 도구가 아닌 합성 exe를 지정한 독립 요청을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 실제 계정·저장소·서버 사용 배제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static WorkspaceActionRequest Request(WorkspaceOperation operation, string root, string executable)
        { return new WorkspaceActionRequest { Operation = operation, ProjectDirectory = root, ExecutablePath = executable, Message = "SYNTHETIC PLAN ONLY", Files = new[] { "A\\source.txt" } }; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Reject
        /// desc         : 위험 입력이 명령 승인 전에 ArgumentException으로 거부되는지 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 Windows 모호 경로·경계 이탈 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Reject(SecureExecutionResult result, string label, Action action)
        {
            try { action(); result.Failures.Add(label + ": accepted unsafe input"); }
            catch (ArgumentException) { result.Passed.Add(label); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ComparePlan
        /// desc         : 승인 스냅샷을 다른 CWD에서 재검증해 파일·대상이 바뀌지 않는지 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 드라이브 상대 입력을 거부하거나 승인 대상이 같아야 통과.
        /// update date  : 2026-10-18 CodexCode : 보정된 불변 계획 API로 정규화 요청을 재검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ComparePlan(SecureExecutionResult result, string label, WorkspaceActionService service,
            WorkspaceActionRequest request, WorkspacePermissionPolicy policy, string firstDirectory, string secondDirectory, bool mayReject)
        {
            Directory.SetCurrentDirectory(firstDirectory);
            WorkspaceActionPlan before;
            try { before = service.BuildPlan(request, policy); }
            catch (ArgumentException error)
            {
                if (!mayReject) throw;
                result.Passed.Add(label + ": unsafe path rejected before approval");
                result.Evidence.Add(new { Case = label, Rejected = true, ExceptionType = error.GetType().Name }); return;
            }
            Directory.SetCurrentDirectory(secondDirectory);
            var after = service.BuildPlan(before.CreateRequestSnapshot(), policy);
            bool same = before.TargetDirectory == after.TargetDirectory && before.ExecutablePath == after.ExecutablePath && before.Arguments == after.Arguments;
            result.Evidence.Add(new { Case = label, FirstCwd = firstDirectory, SecondCwd = secondDirectory,
                ApprovedCommand = before.DisplayCommand, RebuiltCommand = after.DisplayCommand, PlanStayedIdentical = same });
            Check(result, same, label + ": approved and rebuilt plans are identical");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckPaths
        /// desc         : drive·root·장치·스트림 경로 및 프로젝트 경계 이탈을 거부하는지 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 승인 파일·SVN 대상·프로젝트·exe 경계별 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckPaths(SecureExecutionResult result, WorkspaceActionService service, WorkspacePermissionPolicy policy, string root, string executable)
        {
            string drive = Path.GetPathRoot(root).Substring(0, 2);
            string[] unsafeFiles = { drive + "source.txt", @"\source.txt", "/source.txt", @"\\?\" + Path.Combine(root, "A\\source.txt"), @"\\.\C:\source.txt", "A\\source.txt:hidden", "..\\outside.txt", "A\\*.txt", root, root + "-neighbor\\source.txt" };
            foreach (string file in unsafeFiles)
            {
                var request = Request(WorkspaceOperation.GitCommit, root, executable); request.Files = new[] { file };
                Reject(result, "Unsafe file denied: " + file, () => service.BuildPlan(request, policy));
            }
            foreach (string destination in new[] { drive + "checkout", @"\checkout", "/checkout", "..\\checkout", "checkout:hidden" })
            {
                var request = Request(WorkspaceOperation.SvnCheckout, root, executable); request.Argument = "https://example.invalid/never-connected"; request.DestinationDirectory = destination;
                Reject(result, "Unsafe SVN target denied: " + destination, () => service.BuildPlan(request, policy));
            }
            foreach (string project in new[] { drive + "project", @"\project", "A", @"\\?\" + root })
            {
                var request = Request(WorkspaceOperation.GitFetch, root, executable); request.ProjectDirectory = project;
                Reject(result, "Ambiguous project denied: " + project, () => service.BuildPlan(request, policy));
            }
            foreach (string tool in new[] { drive + "fixture.exe", @"\fixture.exe", "/fixture.exe", @"\\?\" + executable, executable + ":stream.exe", "fixture.cmd" })
            {
                var request = Request(WorkspaceOperation.GitFetch, root, tool);
                Reject(result, "Unsafe executable denied: " + tool, () => service.BuildPlan(request, policy));
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Field
        /// desc         : 합성 exe가 출력한 base64 필드를 실제 argv·환경 값으로 복원한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 셸 구문·한글·빈 인수의 손실 없는 비교.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string[] Field(WorkspaceActionResult result, string prefix)
        {
            return (result.Output ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
                .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(prefix.Length)))).ToArray();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckNativeAsync
        /// desc         : 승인 계획을 실제 출력 전용 exe로 실행해 인수·CWD·환경 격리를 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 권한 철회·취소·timeout도 합성 프로세스로 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task CheckNativeAsync(SecureExecutionResult result, WorkspaceActionService service, WorkspacePermissionPolicy policy, string root, string executable)
        {
            string[] names = GitEnvironment.Concat(new[] { "GIT_TERMINAL_PROMPT", "GCM_INTERACTIVE", "SECURE_FIXTURE_MARKER", "PATH" }).ToArray();
            var previousEnvironment = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
            string marker = Path.Combine(root, "fixture-started.txt");
            try
            {
                foreach (string name in GitEnvironment) Environment.SetEnvironmentVariable(name, "SYNTHETIC-UNTRUSTED-" + name);
                Environment.SetEnvironmentVariable("SECURE_FIXTURE_MARKER", marker);
                Environment.SetEnvironmentVariable("GIT_TERMINAL_PROMPT", "1"); Environment.SetEnvironmentVariable("GCM_INTERACTIVE", "Always");
                var request = Request(WorkspaceOperation.GitCommit, root, executable);
                request.Message = "한글 \"message\" & $(literal)"; string expectedMessage = request.Message;
                var approved = service.BuildPlan(request, policy);
                request.Files[0] = "B\\source.txt"; request.ProjectDirectory = Path.Combine(root, "B"); request.Message = "MUTATED ORIGINAL";
                var exposed = approved.CreateRequestSnapshot(); exposed.Files[0] = "B\\source.txt"; exposed.Message = "MUTATED RETURNED COPY";
                Directory.SetCurrentDirectory(Path.Combine(root, "B"));
                var native = await service.RunApprovedAsync(approved, policy, CancellationToken.None).ConfigureAwait(false); result.SyntheticProcessesStarted++;
                string[] received = Field(native, "ARG:");
                Check(result, native.Succeeded && received.SequenceEqual(new[] { "--literal-pathspecs", "commit", "--only", "-m", expectedMessage, "--", Path.Combine(root, "A\\source.txt") }), "Actual argv preserves canonical approved file and immutable message after original/copy mutation");
                Check(result, Field(native, "CWD:").Single() == root, "Actual native CWD equals approved project after caller CWD change");
                foreach (string name in GitEnvironment) Check(result, Field(native, "ENV:" + name + ":").Single() == "<null>", "Child repository override removed: " + name);
                Check(result, Field(native, "ENV:GIT_TERMINAL_PROMPT:").Single() == "0" && Field(native, "ENV:GCM_INTERACTIVE:").Single() == "Never", "Child interactive credential prompts disabled");
                Check(result, GitEnvironment.All(name => Environment.GetEnvironmentVariable(name) == "SYNTHETIC-UNTRUSTED-" + name), "Environment isolation does not mutate parent Git environment");
                result.Evidence.Add(new { Case = "Native approved snapshot", ApprovedCommand = approved.DisplayCommand, NativeArguments = received, NativeWorkingDirectory = Field(native, "CWD:").Single(), Executable = executable });
                var deploy = Request(WorkspaceOperation.Deploy, root, executable);
                string[] arguments = { "", "space value", "한글", "embedded\"quote", "tail\\", "&|<>$(literal);`" };
                deploy.DeploymentArguments = arguments.ToArray(); var deployment = service.BuildPlan(deploy, policy);
                deploy.DeploymentArguments[0] = "MUTATED"; var deploymentCopy = deployment.CreateRequestSnapshot(); deploymentCopy.DeploymentArguments[1] = "MUTATED";
                native = await service.RunApprovedAsync(deployment, policy, CancellationToken.None).ConfigureAwait(false); result.SyntheticProcessesStarted++;
                Check(result, Field(native, "ARG:").SequenceEqual(arguments), "Native argv round-trips empty, Korean, quote, slash and shell metacharacters without mutable array reuse");
                Environment.SetEnvironmentVariable("PATH", Path.GetDirectoryName(executable));
                var pathPlan = service.BuildPlan(Request(WorkspaceOperation.GitFetch, root, Path.GetFileName(executable)), policy);
                Environment.SetEnvironmentVariable("PATH", Path.Combine(root, "B"));
                native = await service.RunApprovedAsync(pathPlan, policy, CancellationToken.None).ConfigureAwait(false); result.SyntheticProcessesStarted++;
                Check(result, pathPlan.ExecutablePath == Path.GetFullPath(executable) && native.Succeeded, "PATH-resolved executable stays pinned after PATH changes");
                File.Delete(marker);
                try { await service.RunApprovedAsync(approved, new WorkspacePermissionPolicy(), CancellationToken.None).ConfigureAwait(false); result.Failures.Add("Revoked permission accepted"); }
                catch (InvalidOperationException) { Check(result, !File.Exists(marker), "Permission revoked after approval blocks process before start"); }
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    try { await service.RunApprovedAsync(approved, policy, cancelled.Token).ConfigureAwait(false); result.Failures.Add("Pre-cancelled operation accepted"); }
                    catch (OperationCanceledException) { Check(result, !File.Exists(marker), "Pre-cancelled approved operation never starts fixture"); }
                }
                var checkout = Request(WorkspaceOperation.SvnCheckout, root, executable); checkout.Argument = "https://example.invalid/never-connected"; checkout.DestinationDirectory = "new-checkout";
                var checkoutPlan = service.BuildPlan(checkout, policy); checkout.DestinationDirectory = "B\\other-checkout";
                native = await service.RunApprovedAsync(checkoutPlan, policy, CancellationToken.None).ConfigureAwait(false); result.SyntheticProcessesStarted++;
                Check(result, Field(native, "ARG:").Last() == Path.Combine(root, "new-checkout"), "Actual fixture receives approved canonical SVN destination after original mutation");
                Directory.CreateDirectory(Path.Combine(root, "new-checkout")); File.Delete(marker);
                try { await service.RunApprovedAsync(checkoutPlan, policy, CancellationToken.None).ConfigureAwait(false); result.Failures.Add("Existing checkout destination accepted after approval"); }
                catch (ArgumentException) { Check(result, !File.Exists(marker), "Checkout destination state is revalidated before process start"); }
                var hanging = Request(WorkspaceOperation.Deploy, root, executable); hanging.DeploymentArguments = new[] { "--fixture-wait" };
                var hangingPlan = service.BuildPlan(hanging, policy);
                using (var cancel = new CancellationTokenSource())
                {
                    Task<WorkspaceActionResult> running = service.RunApprovedAsync(hangingPlan, policy, cancel.Token);
                    int processId = 0;
                    for (int i = 0; i < 100 && processId <= 0; i++)
                    {
                        try { if (File.Exists(marker)) Int32.TryParse(File.ReadAllText(marker), out processId); }
                        catch (IOException) { }
                        if (processId <= 0) await Task.Delay(20).ConfigureAwait(false);
                    }
                    bool started = processId > 0;
                    cancel.Cancel(); bool cancelled = false;
                    try { await running.ConfigureAwait(false); result.Failures.Add("Running cancellation accepted success"); }
                    catch (OperationCanceledException) { cancelled = true; }
                    if (cancelled) Check(result, started && await WaitForExitAsync(processId).ConfigureAwait(false), "Cancellation stops already-running synthetic process");
                    if (started) result.SyntheticProcessesStarted++;
                }
                File.Delete(marker); bool timedOut = false;
                try { await new WorkspaceActionService(TimeSpan.FromMilliseconds(300)).RunApprovedAsync(hangingPlan, policy, CancellationToken.None).ConfigureAwait(false); result.Failures.Add("Timeout accepted success"); }
                catch (TimeoutException) { timedOut = true; }
                if (timedOut)
                {
                    bool started = File.Exists(marker); int processId = started ? Int32.Parse(File.ReadAllText(marker)) : -1;
                    Check(result, started && await WaitForExitAsync(processId).ConfigureAwait(false), "Timeout stops already-running synthetic process and reports TimeoutException");
                    if (started) result.SyntheticProcessesStarted++;
                }
            }
            finally { foreach (var entry in previousEnvironment) Environment.SetEnvironmentVariable(entry.Key, entry.Value); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WaitForExitAsync
        /// desc         : 취소·timeout 후 합성 프로세스가 실제 종료했는지 최대 2초 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 종료 코드만으로 cleanup 성공을 추정하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task<bool> WaitForExitAsync(int id)
        {
            if (id <= 0) return false;
            try
            {
                using (Process process = Process.GetProcessById(id))
                {
                    for (int i = 0; i < 100 && !process.HasExited; i++) await Task.Delay(20).ConfigureAwait(false);
                    return process.HasExited;
                }
            }
            catch (ArgumentException) { return true; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Run
        /// desc         : 별도 PowerShell 자식에서 합성 검사를 완료하고 원래 CWD를 finally에서 복원한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 드라이브 상대 파일·대상과 정상 경로 대조 검사.
        /// update date  : 2026-10-18 CodexCode : 독립 테스트 호스트 경계에서만 비동기 결과를 동기 수집.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static SecureExecutionResult Run(string fixtureDirectory, string executable)
        {
            var result = new SecureExecutionResult(); string previous = Environment.CurrentDirectory;
            try
            {
                string root = Path.GetFullPath(fixtureDirectory), first = Path.Combine(root, "A"), second = Path.Combine(root, "B");
                Directory.CreateDirectory(first); Directory.CreateDirectory(second);
                File.WriteAllText(Path.Combine(first, "source.txt"), "SYNTHETIC-A"); File.WriteAllText(Path.Combine(second, "source.txt"), "SYNTHETIC-B");
                string drive = Path.GetPathRoot(root);
                if (drive.Length != 3 || drive[1] != ':') throw new InvalidOperationException("This fixture requires a local Windows drive.");
                var service = new WorkspaceActionService(); var policy = new WorkspacePermissionPolicy { AllowSourceControl = true, AllowDeployment = true };
                var request = Request(WorkspaceOperation.GitCommit, root, executable); request.Files = new[] { drive.Substring(0, 2) + "source.txt" };
                ComparePlan(result, "Drive-relative Git file", service, request, policy, first, second, true);
                request = Request(WorkspaceOperation.SvnCheckout, root, executable); request.Argument = "https://example.invalid/never-connected"; request.DestinationDirectory = drive.Substring(0, 2) + "new-checkout";
                ComparePlan(result, "Drive-relative SVN destination", service, request, policy, first, second, true);
                ComparePlan(result, "Project-relative Git file control", service, Request(WorkspaceOperation.GitCommit, root, executable), policy, first, second, false);
                request = Request(WorkspaceOperation.GitCommit, root, executable); request.Files = new[] { Path.Combine(first, "source.txt") };
                ComparePlan(result, "Fully qualified Git file control", service, request, policy, first, second, false);
                CheckPaths(result, service, policy, root, executable);
                CheckNativeAsync(result, service, policy, root, executable).GetAwaiter().GetResult();
                result.Succeeded = result.Failures.Count == 0;
            }
            catch (Exception error) { result.Error = error.ToString(); }
            finally { Directory.SetCurrentDirectory(previous); result.CwdRestored = Environment.CurrentDirectory == previous; }
            return result;
        }
    }
}
