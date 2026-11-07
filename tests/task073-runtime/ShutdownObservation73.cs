using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Process = System.Diagnostics.Process;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ShutdownObservation73
/// desc         : 전용 실제 IDE의 종료 API 경계만 기록하고 종료·프로세스 제어를 직접 하지 않는다.
/// author       : CodexOrig 보조 개발 055
/// create date  : 2026-11-03
/// update date  : 2026-11-03 CodexOrig 보조 개발 055 : private old-reference 컴파일의 Process alias와 실제 UI 진입 경계를 명시.
/// </summary>
/// ---------------------------------------------------------------------
public static class ShutdownObservation73
{
    private static int verifiedOwnedProcessId;
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Mark
    /// desc         : Documents.CloseAll·Solution.Close·Quit 앞뒤에 시각과 문서 개수만 남긴다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Mark(DTE dte, string outputDirectory, string phase, Exception error = null)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        int processId = Process.GetCurrentProcess().Id;
        if (verifiedOwnedProcessId != processId)
        {
            if (dte == null || !dte.RegistryRoot.Contains("CodexCodeTest"))
                throw new InvalidOperationException("Shutdown observations require the dedicated test IDE profile.");
            verifiedOwnedProcessId = processId;
        }
        string path = Path.GetFullPath(outputDirectory);
        if (!path.Contains(Path.Combine("artifacts", "ide"))) throw new InvalidOperationException("An owned IDE evidence path is required.");
        int? documentCount = null; bool? solutionOpen = null; string observationError = null;
        bool skipCom = phase.IndexOf("quit", StringComparison.OrdinalIgnoreCase) >= 0;
        try { if (!skipCom) { documentCount = dte.Documents.Count; solutionOpen = dte.Solution.IsOpen; } }
        catch (Exception readError) { observationError = readError.GetType().FullName; }
        File.AppendAllText(Path.Combine(path, "task073-shutdown-observations.jsonl"), new JavaScriptSerializer().Serialize(new {
            Phase = phase, At = DateTimeOffset.UtcNow.ToString("o"), MonotonicTimestamp = Stopwatch.GetTimestamp(),
            ProcessId = processId, DocumentCount = documentCount, SolutionOpen = solutionOpen,
            ErrorType = error == null ? null : error.GetType().FullName, ObservationError = observationError,
            ComObservationSkippedNearQuit = skipCom,
            ProcessControlIssuedByProbe = false, RootOwnsFinalProcessCleanup = true
        }) + "\n", new UTF8Encoding(false));
    }
}
