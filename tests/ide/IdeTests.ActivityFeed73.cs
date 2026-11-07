using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VS2017.AIAssistant.Services;
using VS2017.AIAssistant.ToolWindows;
using VS2017.AIAssistant.Views;
using Task = System.Threading.Tasks.Task;

namespace CodexCode.IdeIntegration
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IdeTests
    /// desc         : 정상 main suite와 분리한 실제 저장 snapshot 복원 전용 경계를 제공한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static partial class IdeTests
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunActivityFeedRestoreAsync
        /// desc         : 실제 DLL·PDF와 seed 증거를 확인한 뒤 요청·추가 focus 없이 restore addon만 실행한다.
        /// author       : CodexOrig 보조 개발 055
        /// create date  : 2026-11-03
        /// update date  :
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static async Task RunActivityFeedRestoreAsync(Package product, DTE dte, TestConfig config)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (config.ActivityFeedPhase != "restore") throw new InvalidDataException("Restore-only entry requires exact restore phase.");
            progressPath = Path.Combine(config.OutputDirectory, "progress.log");
            string productHash = ActivityFeedHash73(product.GetType().Assembly.Location);
            string pdfPath = new CliGuideService().GetPdfPath();
            string pdfHash = ActivityFeedHash73(pdfPath);
            Check("ACTIVITY73-RESTORE actual loaded DLL matches fixed candidate", productHash == config.ExpectedProductSha256);
            Check("ACTIVITY73-RESTORE actual installed PDF matches fixed candidate", pdfHash == config.ExpectedGuideSha256);
            if (productHash != config.ExpectedProductSha256 || pdfHash != config.ExpectedGuideSha256) throw new InvalidDataException("Actual restore product or PDF identity differs.");
            string seedPath = Path.Combine(config.OutputDirectory, "seed-files-before-launch.json");
            var seed = new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string, object>>(File.ReadAllText(seedPath));
            if (!Convert.ToBoolean(seed["MatchesSnapshot"]) || Convert.ToString(seed["SnapshotManifestSHA256"]) != config.ActivityFeedSnapshotManifestSHA256)
                throw new InvalidDataException("Phase2 seed was not verified before package initialization.");
            var pane = product.FindToolWindow(typeof(AssistantToolWindow), 0, false);
            var owner = pane == null ? null : pane.Content as AssistantControl;
            if (owner == null) throw new InvalidOperationException("The actual product tool window did not create an AssistantControl.");
            Check("ACTIVITY73-RESTORE actual IDE version matches requested host", dte.Version.StartsWith(config.ExpectedIdeMajor + ".", StringComparison.Ordinal));
            await ActivityFeedRuntime73.RunAsync(owner, dte, config.OutputDirectory, config.ActivityFeedFixturePath,
                config.ActivityFeedPhase, config.ActivityFeedSnapshotManifestPath, Check,
                config.ActivityFeedFixtureSHA256, config.ActivityFeedSnapshotManifestSHA256);
            File.WriteAllText(Path.Combine(config.OutputDirectory, "result.json"), new JavaScriptSerializer().Serialize(new {
                Succeeded = failed == 0, Passed = passed, Failed = failed, IdeRuntimeTest = true,
                ActivityFeedPhase = "restore", ExistingMainRequestSuiteExecuted = false,
                ProductSha256 = productHash, InstalledPdfSha256 = pdfHash, Tests = results
            }), new UTF8Encoding(false));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivityFeedHash73
        /// desc         : 실제 로드 파일을 읽기만 하여 후보의 SHA256과 대조한다.
        /// author       : CodexOrig 보조 개발 055
        /// create date  : 2026-11-03
        /// update date  :
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ActivityFeedHash73(string path)
        { using (var input = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", ""); }
    }
}
