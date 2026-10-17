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
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override Task InitializeAsync(CancellationToken token, IProgress<ServiceProgressData> progress)
        {
            string configPath = Environment.GetEnvironmentVariable("CODEXCODE_IDE_TEST_CONFIG");
            if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
                JoinableTaskFactory.RunAsync(() => RunAsync(configPath));
            return Task.CompletedTask;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunAsync
        /// desc         : 메뉴 명령으로 제품을 로드한 뒤 실제 IDE 내 테스트를 수행하고 소유 인스턴스를 종료한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 단계별 진단과 실패 시 결과 보존.
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
                // 제품 패키지가 로드된 뒤 제품 타입을 사용하는 별도 메서드로 진입한다.
                await IdeTests.RunAsync((Package)product, dte, config);
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
                    dte.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
                    dte.Solution.Close(false);
                    dte.Quit();
                }
            }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TestConfig
    /// desc         : 테스트 런처가 별도 프로세스에 전달하는 합성 솔루션과 CLI 경로 설정.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실행별 결과 경로와 실제 AI 선택 경로 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class TestConfig
    {
        public string OutputDirectory { get; set; }
        public string SolutionPath { get; set; }
        public string FixturePath { get; set; }
        public string LiveCodexPath { get; set; }
        public bool CloseWhenDone { get; set; }
    }
}
