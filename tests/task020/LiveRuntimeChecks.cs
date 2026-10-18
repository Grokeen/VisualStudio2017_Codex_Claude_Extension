using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Providers;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : LiveRuntimeChecks
/// desc         : 실제 Codex의 C# 수정·자체 테스트·사용량·변경 보고를 확인한다.
/// author       : CodexOrig
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
/// </summary>
/// ---------------------------------------------------------------------
internal static class LiveRuntimeChecks
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 격리 검증을 실행하고 실패를 종료 코드와 결과 파일에 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        string directory = Path.GetFullPath(args[1]);
        try { RunAsync(args[0], directory).GetAwaiter().GetResult(); return 0; }
        catch (Exception error) { File.WriteAllText(Path.Combine(directory, "failure.txt"), error.ToString()); Console.Error.WriteLine(error.Message); return 1; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunAsync
    /// desc         : 실제 API의 성공·실패·취소·전후 파일 근거를 순서대로 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static async Task RunAsync(string executable, string directory)
    {
        var info = await new ProviderRuntimeService().ReadAsync("Codex", executable, CancellationToken.None);
        if (info.Authenticated != true || !info.SupportsProtocol) throw new InvalidOperationException("Actual Codex account/protocol unavailable.");
        string model = info.Catalog.Models.Where(item => item.Id.IndexOf("mini", StringComparison.OrdinalIgnoreCase) >= 0).Select(item => item.Id).FirstOrDefault() ?? "";
        ProviderModelCatalog.PublishDiscovered("Codex", info.Catalog);
        string effort = model.Length == 0 || info.Catalog.Find(model).Efforts.Contains("low") ? "low" : "auto";
        File.WriteAllText(Path.Combine(directory, "Calculator.cs"), "public static class Calculator { public static int Add(int a, int b) { return a - b; } }\r\n");
        File.WriteAllText(Path.Combine(directory, "SelfTest.cs"), "public static class SelfTest { public static int Main() { return Calculator.Add(2,3)==5 && Calculator.Add(-2,1)==-1 ? 0 : 5; } }\r\n");
        File.WriteAllText(Path.Combine(directory, "Verify.ps1"), "$ErrorActionPreference='Stop'\r\n& \"$env:WINDIR\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe\" /nologo /out:SelfTest.exe Calculator.cs SelfTest.cs\r\nif($LASTEXITCODE -ne 0){exit $LASTEXITCODE}\r\n& .\\SelfTest.exe\r\nif($LASTEXITCODE -ne 0){exit $LASTEXITCODE}\r\n[IO.File]::WriteAllText((Join-Path $PWD 'self-test-passed.txt'),'PASS')\r\n");
        var tokens = new List<ProviderUsageSnapshot>(); var changes = new List<AgentFileChange[]>(); var activities = new List<AgentProgress>();
        string progressPath = Path.Combine(directory, "progress.log");
        var result = await new CodexRuntimeRunner { Timeout = TimeSpan.FromMinutes(4) }.RunAgentAsync(executable, directory,
            "This is an explicitly authorized isolated integration test. Work ONLY in the current directory. Calculator.Add should add numbers but currently subtracts. Change only Calculator.cs to fix it, preserve SelfTest.cs and Verify.ps1, and run powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\\Verify.ps1. Do not access network or any other directory. Return a short Korean summary of the actual edit and actual test result.",
            new ChatRequestContext(model: model, reasoningEffort: effort),
            usage => { tokens.Add(usage); File.AppendAllText(progressPath, "Actual usage update: " + usage.TotalTokens + "\n"); },
            files => { changes.Add(files); File.AppendAllText(progressPath, "Actual changed-file update: " + files.Length + "\n"); },
            CancellationToken.None,
            activity => { activities.Add(activity); File.AppendAllText(progressPath, activity.Text + "\n"); });
        bool edited = File.ReadAllText(Path.Combine(directory, "Calculator.cs")).Contains("return a + b;");
        bool tested = File.Exists(Path.Combine(directory, "self-test-passed.txt")) && File.ReadAllText(Path.Combine(directory, "self-test-passed.txt")) == "PASS";
        var start = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File .\\Verify.ps1") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true };
        int verification;
        using (var process = Process.Start(start)) { process.WaitForExit(); verification = process.ExitCode; }
        bool usageReceived = tokens.Count > 0 && result.Usage != null && result.Usage.TotalTokens > 0;
        bool reported = result.Changes.Any(file => file.Path.EndsWith("Calculator.cs", StringComparison.OrdinalIgnoreCase));
        File.WriteAllText(Path.Combine(directory, "result.json"), new JavaScriptSerializer().Serialize(new { Succeeded = edited && tested && usageReceived && reported && verification == 0, ActualNativeCodex = true, ActualFileEdit = edited, ActualSelfTest = tested, IndependentTestExitCode = verification, UsageEvents = tokens.Count, ProgressEvents = activities.Count, ChangeEvents = changes.Count, ReportedCalculator = reported, Model = model, Effort = effort, Usage = result.Usage, Reply = result.Text, Changes = result.Changes }));
        if (!edited || !tested || !usageReceived || !reported || verification != 0) throw new InvalidOperationException("Actual native full-access result did not satisfy all evidence checks.");
        Console.WriteLine("PASS actual native Codex file edit, self-test, independent assertion, token events, file diff and progress.");
    }
}
