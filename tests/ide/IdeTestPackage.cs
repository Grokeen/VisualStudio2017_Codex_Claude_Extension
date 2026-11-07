using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using System.Web.Script.Serialization;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CodexCode.IdeIntegration
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IdeTestPackage
    /// desc         : 별도 테스트 인스턴스에서만 실제 제품 패키지를 실행하고 검증 결과를 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 합성 소스 전용 SDK 통합 테스트 진입점.
    ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
    /// </summary>
    /// ---------------------------------------------------------------------
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid("75548a83-738a-4d27-86e0-0db6e24a42ba")]
    public sealed class IdeTestPackage : AsyncPackage
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeAsync
        /// desc         : 실행별 설정이 있는 경우에만 테스트를 예약한다. 일반 IDE에서는 실행하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 환경 변수 가드와 비동기 실행 예약.
        ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override Task InitializeAsync(CancellationToken token, IProgress<ServiceProgressData> progress)
        {
            string configPath = Environment.GetEnvironmentVariable("CODEXCODE_IDE_TEST_CONFIG");
            if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            {
                var scheduled = JoinableTaskFactory.RunAsync(() => RunAsync(configPath));
            }
            return Task.CompletedTask;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunAsync
        /// desc         : 메뉴 명령으로 제품을 로드한 뒤 실제 IDE 내 테스트를 수행하고 소유 인스턴스를 종료한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 단계별 진단과 실패 시 결과 보존.
        ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
        ///                2026-11-03 CodexOrig 보조 개발 055 : 격리 worktree에서만 TASK073 restore 분기와 종료 경계 진단을 연결.
        ///                2026-11-03 CodexOrig 보조 개발 055 : exercise에만 실제 IDE 활성화 rendezvous를 두고 restore의 사전 읽기 순서를 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task RunAsync(string configPath)
        {
            var json = new JavaScriptSerializer();
            var config = json.Deserialize<TestConfig>(File.ReadAllText(configPath));
            string progressPath = Path.Combine(config.OutputDirectory, "progress.log");
            DTE dte = null;
            bool ownsInstance = false;
            try
            {
                if (!string.IsNullOrEmpty(config.ActivityFeedPhase) && config.ActivityFeedPhase != "exercise" && config.ActivityFeedPhase != "restore")
                    throw new InvalidDataException("Unknown TASK073 phase.");
                File.AppendAllText(progressPath, DateTime.Now.ToString("O") + " helper initialized\r\n");
                await Task.Delay(3000);
                await JoinableTaskFactory.SwitchToMainThreadAsync();
                dte = await GetServiceAsync(typeof(DTE)) as DTE;
                if (dte == null || !dte.RegistryRoot.Contains("CodexCodeTest"))
                    throw new InvalidOperationException("A dedicated CodexCodeTest instance is required.");
                ownsInstance = true;
                File.AppendAllText(progressPath, "DTE ready: " + dte.RegistryRoot + "\r\n");
                object input = null, output = null;
                dte.Commands.Raise("{c9201782-e997-4591-a509-7dabd90e97ce}", 0x0100, ref input, ref output);
                File.AppendAllText(progressPath, "production menu command returned\r\n");
                var shell = (IVsShell)await GetServiceAsync(typeof(SVsShell));
                if (shell == null) throw new InvalidOperationException("Visual Studio shell service is unavailable.");
                Guid productGuid = new Guid("f31272f1-3188-4cca-b6e1-37c118cf7824");
                IVsPackage product;
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(shell.IsPackageLoaded(ref productGuid, out product));
                if (product == null) throw new InvalidOperationException("Menu did not load production package.");
                if (config.ActivityFeedPhase == "exercise")
                {
                    await WaitForStartupActivationAsync(dte, config);
                    await JoinableTaskFactory.SwitchToMainThreadAsync();
                    dte.Commands.Raise("{c9201782-e997-4591-a509-7dabd90e97ce}", 0x0100, ref input, ref output);
                    File.AppendAllText(progressPath, "exercise production menu returned after actual activation acknowledgement\r\n");
                }
                // 제품 패키지가 로드된 뒤 제품 타입을 사용하는 별도 메서드로 진입한다.
                if (config.ActivityFeedPhase == "restore")
                    await IdeTests.RunActivityFeedRestoreAsync((Package)product, dte, config);
                else await IdeTests.RunAsync((Package)product, dte, config);
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(config.OutputDirectory, "result.json"), json.Serialize(new {
                    Succeeded = false, FatalError = exception.ToString(), IdeRuntimeTest = true
                }));
                File.AppendAllText(progressPath, exception + "\r\n");
            }
            finally
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();
                if (ownsInstance && dte != null && config.CloseWhenDone)
                {
                    // 테스트가 만든 문서만 닫으며, 다른 사용자 IDE 프로세스에는 접근하지 않는다.
                    if (string.IsNullOrEmpty(config.ActivityFeedPhase))
                    {
                        dte.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
                        dte.Solution.Close(false);
                        dte.Quit();
                    }
                    else
                    {
                        string phase = "documents-close";
                        try
                        {
                            ShutdownObservation73.Mark(dte, config.OutputDirectory, "before-documents-close");
                            dte.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
                            ShutdownObservation73.Mark(dte, config.OutputDirectory, "after-documents-close");
                            phase = "solution-close";
                            ShutdownObservation73.Mark(dte, config.OutputDirectory, "before-solution-close");
                            dte.Solution.Close(false);
                            ShutdownObservation73.Mark(dte, config.OutputDirectory, "after-solution-close");
                            phase = "dte-quit";
                            ShutdownObservation73.Mark(dte, config.OutputDirectory, "before-dte-quit");
                            dte.Quit();
                            ShutdownObservation73.Mark(dte, config.OutputDirectory, "after-dte-quit-returned");
                        }
                        catch (Exception shutdownError)
                        {
                            ShutdownObservation73.Mark(dte, config.OutputDirectory, phase + "-threw", shutdownError);
                            File.WriteAllText(Path.Combine(config.OutputDirectory, "task073-shutdown-error.json"), json.Serialize(new { Phase = phase, Error = shutdownError.ToString() }));
                            File.AppendAllText(progressPath, "TASK073 shutdown failure: " + shutdownError + "\r\n");
                            throw;
                        }
                    }
                }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WaitForStartupActivationAsync
        /// desc         : exercise 본 검사 전에 Root의 실제 전용 IDE 활성화만 최대 120초 기다리며 restore에서는 호출하지 않는다.
        /// author       : CodexOrig 보조 개발 055
        /// create date  : 2026-11-03
        /// update date  :
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task WaitForStartupActivationAsync(DTE dte, TestConfig config)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            if (config.ActivityFeedPhase != "exercise") throw new InvalidOperationException("Startup activation is exercise-only.");
            string ready = Path.Combine(config.OutputDirectory, "startup-activation-ready.json");
            string release = Path.Combine(config.OutputDirectory, "startup-activation-release.json");
            if (File.Exists(ready) || File.Exists(release)) throw new InvalidOperationException("Startup activation requires fresh owned evidence.");
            string registry = dte.RegistryRoot;
            int start = registry.LastIndexOf("CodexCodeTest", StringComparison.Ordinal);
            if (start < 0) throw new InvalidOperationException("Startup activation requires the exact dedicated IDE instance.");
            string nonce = Guid.NewGuid().ToString("N");
            var json = new JavaScriptSerializer();
            File.WriteAllText(ready, json.Serialize(new { Token = nonce, OwnedIdePid = System.Diagnostics.Process.GetCurrentProcess().Id,
                RootSuffix = registry.Substring(start), BeforeMainSuite = true, Phase = "exercise", MaximumWaitMilliseconds = 120000,
                GuiExecutor = "CodexOrig", HelperActivationOrFocusWrites = false }), new System.Text.UTF8Encoding(false));
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int polls = 0;
            while (!File.Exists(release))
            {
                if (clock.ElapsedMilliseconds >= 120000) throw new TimeoutException("Startup actual IDE activation was not acknowledged within 120 seconds.");
                polls++;
                await Task.Delay(50);
                await JoinableTaskFactory.SwitchToMainThreadAsync();
            }
            if (clock.ElapsedMilliseconds > 120000) throw new TimeoutException("Startup activation release exceeded its 120 second bound.");
            if ((File.GetAttributes(release) & FileAttributes.ReparsePoint) != 0 || new FileInfo(release).Length > 4096)
                throw new InvalidDataException("Startup activation release must be a small regular JSON file.");
            var data = json.Deserialize<System.Collections.Generic.Dictionary<string, object>>(File.ReadAllText(release, System.Text.Encoding.UTF8));
            if (!data.ContainsKey("Token") || Convert.ToString(data["Token"]) != nonce ||
                !data.ContainsKey("GuiExecutor") || Convert.ToString(data["GuiExecutor"]) != "CodexOrig" ||
                !data.ContainsKey("ActivatedOwnedIdeUsingCua") || !(data["ActivatedOwnedIdeUsingCua"] is bool) || !(bool)data["ActivatedOwnedIdeUsingCua"])
                throw new InvalidDataException("Startup release must match the one-use token and actual Root CUA activation acknowledgement.");
            var window = System.Windows.Application.Current == null ? null : System.Windows.Application.Current.MainWindow;
            File.WriteAllText(Path.Combine(config.OutputDirectory, "startup-activation-received.json"), json.Serialize(new {
                BeforeMainSuite = true, Phase = "exercise", WaitMilliseconds = clock.Elapsed.TotalMilliseconds, Polls = polls,
                RootAcknowledgedActualCuaActivation = true, MainWindowPresent = window != null,
                MainWindowIsActive = window == null ? (bool?)null : window.IsActive, HelperActivationOrFocusWrites = false }), new System.Text.UTF8Encoding(false));
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TestConfig
    /// desc         : 테스트 런처가 별도 프로세스에 전달하는 합성 솔루션과 CLI 경로 설정.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실행별 결과 경로와 실제 AI 선택 경로 추가.
    ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
    ///                2026-11-03 CodexOrig 보조 개발 055 : 실제 활동함 phase와 고정 fixture·snapshot 해시 설정을 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class TestConfig
    {
        public string OutputDirectory { get; set; }
        public string SolutionPath { get; set; }
        public string FixturePath { get; set; }
        public string LiveCodexPath { get; set; }
        public string ExpectedIdeMajor { get; set; } = "15";
        public string RuntimeFixturePath { get; set; }
        public string ExpectedProductSha256 { get; set; }
        public string ExpectedGuideSha256 { get; set; }
        public bool CloseWhenDone { get; set; }
        public string ActivityFeedPhase { get; set; }
        public string ActivityFeedFixturePath { get; set; }
        public string ActivityFeedFixtureSHA256 { get; set; }
        public string ActivityFeedSnapshotManifestPath { get; set; }
        public string ActivityFeedSnapshotManifestSHA256 { get; set; }
    }
}
