using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : RuntimeChecks
/// desc         : 운영 계정 없이 네이티브 프로토콜과 파일 수정·중지 결과를 검증한다.
/// author       : CodexOrig
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
/// </summary>
/// ---------------------------------------------------------------------
internal static class RuntimeChecks
{
    private static readonly List<object> Results = new List<object>();
    private static string Fixture, Root;
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 성공·실패 근거를 기록하고 실패를 즉시 보고한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, bool passed) { Results.Add(new { name, passed }); Console.WriteLine((passed ? "PASS " : "FAIL ") + name); if (!passed) throw new Exception(name); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Case
    /// desc         : 고유한 테스트 폴더와 합성 실행 모드를 만든다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Case(string mode)
    {
        string path = Path.Combine(Root, mode + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)); Directory.CreateDirectory(path);
        Environment.SetEnvironmentVariable("TASK020_FIXTURE_MODE", mode); Environment.SetEnvironmentVariable("TASK020_FIXTURE_TRACE", Path.Combine(path, "protocol.log")); return path;
    }
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
        Fixture = Path.GetFullPath(args[0]); Root = Path.GetFullPath(args[1]);
        try { RunAsync().GetAwaiter().GetResult(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); Results.Add(new { name = "unexpected", passed = false, error = error.ToString() }); return 1; }
        finally { File.WriteAllText(Path.Combine(Root, "result.json"), new JavaScriptSerializer().Serialize(new { checks = Results })); }
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
    private static async Task RunAsync()
    {
        CheckAutomaticDiscovery();
        string path = Case("success");
        var store = new MarkdownWorkspaceStore(Path.Combine(path, "store"));
        var record = store.Create("Codex");
        string recordFile = Directory.GetFiles(Path.Combine(path, "store"), "*.md", SearchOption.AllDirectories).Single();
        using (var locked = new FileStream(recordFile, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            var release = Task.Run(async () => { await Task.Delay(70); locked.Dispose(); });
            record.Draft = "recovered after transient lock"; store.Save(record); await release;
        }
        Check("real transient file lock recovers atomic save", store.LoadOpen("Codex").Single().Draft == record.Draft);
        string original = File.ReadAllText(recordFile);
        using (var locked = new FileStream(recordFile, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            try { store.Close(record); Check("persistent lock refuses close", false); }
            catch (IOException error) { Check("persistent lock preserves open state and reports Win32 code", record.IsOpen && error.Message.Contains("Win32")); }
        }
        Check("failed replacement preserves original and cleans temporary", File.ReadAllText(recordFile) == original && Directory.GetFiles(Path.GetDirectoryName(recordFile), "*.tmp").Length == 0);
        Check("diff source navigation uses actual changed line", AgentProtocolEvents.FirstChangedLine("@@ -54,2 +56,2 @@\n-old\n+new") == 56);
        path = Case("tracker");
        File.WriteAllText(Path.Combine(path, "Middle.cs"), "a\nb\nc\nd\ne\n");
        File.WriteAllText(Path.Combine(path, "DeleteEmpty.cs"), "");
        File.WriteAllText(Path.Combine(path, "LargeChange.cs"), String.Join("\n", Enumerable.Repeat("old", 300)));
        var trackerType = typeof(CodexRuntimeRunner).Assembly.GetType("VS2017.AIAssistant.Providers.AgentFileTracker", true);
        var initialization = (Task)trackerType.GetMethod("CreateAsync", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { path, CancellationToken.None });
        await initialization;
        object tracker = initialization.GetType().GetProperty("Result").GetValue(initialization, null);
        File.WriteAllText(Path.Combine(path, "Middle.cs"), "A\nb\nc\nd\nE\n");
        File.Delete(Path.Combine(path, "DeleteEmpty.cs")); File.WriteAllText(Path.Combine(path, "NewEmpty.cs"), "");
        File.WriteAllText(Path.Combine(path, "LargeChange.cs"), String.Join("\n", Enumerable.Repeat("new", 300)));
        var actualChanges = (AgentFileChange[])trackerType.GetMethod("DetectChanges", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(tracker, null);
        int trackerAdded, trackerRemoved;
        AgentProtocolEvents.CountDiff(actualChanges.Single(file => file.Path.EndsWith("Middle.cs")).Diff, out trackerAdded, out trackerRemoved);
        Check("tracker keeps unchanged middle lines and precise two-line counts", trackerAdded == 2 && trackerRemoved == 2);
        Check("tracker reports creation and deletion of zero-byte sources", actualChanges.Any(file => file.Path.EndsWith("NewEmpty.cs")) && actualChanges.Any(file => file.Path.EndsWith("DeleteEmpty.cs")));
        Check("large nonminimal fallback explicitly has unknown line counts", !actualChanges.Single(file => file.Path.EndsWith("LargeChange.cs")).HasLineCounts);
        File.WriteAllText(Path.Combine(path, "Middle.cs"), "a\nb\nc\nd\ne\n");
        actualChanges = (AgentFileChange[])trackerType.GetMethod("DetectChanges", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(tracker, null);
        Check("reverted file absent from final net changes", !actualChanges.Any(file => file.Path.EndsWith("Middle.cs")));
        path = Case("success");
        var codex = await new ProviderRuntimeService().ReadAsync("Codex", Fixture, CancellationToken.None);
        Check("metadata reads both model pages and actual plan", codex.Catalog.Models.Count == 2 && codex.Plan == "fixture-plan" && codex.Authenticated == true);
        Check("new model and effort discovered without hardcoded tier", codex.Catalog.Find("future-two").Efforts.Contains("xhigh") && codex.Catalog.Find("future-one").SupportsFast);
        Check("quota is percentage separate from unknown tokens", codex.Usage.QuotaUsedPercent == 42.5 && !codex.Usage.TotalTokens.HasValue);
        Check("metadata starts no thread or inference", !File.ReadAllText(Path.Combine(path, "protocol.log")).Contains("thread/start"));
        path = Case("collision");
        codex = await new ProviderRuntimeService().ReadAsync("Codex", Fixture, CancellationToken.None);
        Check("server request with matching pending id is not response", codex.Authenticated == true && codex.Plan == "fixture-plan");
        path = Case("success");
        var claude = await new ProviderRuntimeService().ReadAsync("Claude", Fixture, CancellationToken.None);
        Check("Claude control initialize discovers model capabilities", claude.Catalog.Find("claude-future").Efforts.Contains("max") && claude.Plan == "fixture-account");
        Check("Claude model metadata alone never claims an authenticated account", !claude.Authenticated.HasValue);
        var usage = ProviderRuntimeService.ParseUsage("Claude", "{\"type\":\"stream_event\",\"event\":{\"usage\":{\"output_tokens\":4}}}");
        Check("partial Claude usage stays unknown for missing input", usage.OutputTokens == 4 && usage.InputTokens == null && usage.TotalTokens == null);
        usage = ProviderRuntimeService.ParseUsage("Claude", "{\"usage\":{\"input_tokens\":10,\"output_tokens\":3}}");
        Check("missing cache fields are not estimated zero", usage.InputTokens == null && usage.TotalTokens == null);
        usage = ProviderRuntimeService.ParseUsage("Claude", "{\"usage\":{\"input_tokens\":9223372036854775807,\"output_tokens\":3,\"cache_read_input_tokens\":2,\"cache_creation_input_tokens\":0}}");
        Check("token overflow stays unknown", usage.InputTokens == null && usage.TotalTokens == null);
        usage = ProviderRuntimeService.ParseUsage("Codex", "{\"usage\":{\"input_tokens\":-1,\"output_tokens\":3}}");
        Check("negative token count rejected", usage.InputTokens == null && usage.OutputTokens == 3);
        int added, removed;
        Check("unprovided diff has unknown counts", !AgentProtocolEvents.CountDiff("", out added, out removed));
        AgentProtocolEvents.CountDiff("--- a/a.cs\n+++ b/a.cs\n@@ -1 +1 @@\n---actualCode\n+++actualCode\n", out added, out removed);
        Check("actual ++ and -- source lines count", added == 1 && removed == 1);
        string repeatedHeaders = "--- a/a.cs\n+++ b/a.cs\n@@ -1 +1 @@\n-old\n+new\n--- a/b.cs\n+++ b/b.cs\n@@ -1 +1 @@\n-old2\n+new2\n";
        AgentProtocolEvents.CountDiff(repeatedHeaders, out added, out removed);
        Check("standard repeated headers do not inflate changed line counts", added == 2 && removed == 2);
        Check("aggregate diff without git headers retains distinct files", AgentProtocolEvents.ParseAggregatedDiff(path, repeatedHeaders).Length == 2);
        var secret = AgentProtocolEvents.ParseProgress("Claude", "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"thinking\",\"thinking\":\"SECRET\"}]}}");
        Check("private reasoning not exposed as progress", secret == null);
        foreach (string provider in new[] { "Codex", "Claude" }) {
            path = Case("success"); File.WriteAllText(Path.Combine(path, "NativeFixture.txt"), "old");
            var tokens = new List<ProviderUsageSnapshot>(); var activities = new List<AgentProgress>(); var updates = new List<AgentFileChange[]>();
            var context = new ChatRequestContext();
            var result = provider == "Codex"
                ? await new CodexRuntimeRunner().RunAgentAsync(Fixture, path, "Perform fixture edit and actual assertion", context, tokens.Add, updates.Add, CancellationToken.None, activities.Add)
                : await new ClaudeRuntimeRunner().RunAgentAsync(Fixture, path, "Perform fixture edit and actual assertion", context, tokens.Add, updates.Add, CancellationToken.None, activities.Add);
            Check(provider + " full runner performs actual isolated file write", File.ReadAllText(Path.Combine(path, "NativeFixture.txt")) == "new");
            Check(provider + " full runner transmits mandatory instructions", File.ReadAllText(Path.Combine(path, "protocol.log.prompt")).Contains(new JavaScriptSerializer().Serialize(context.EffectiveInstructions)));
            Check(provider + " actual subprocess assertion completed", File.ReadAllText(Path.Combine(path, "actual-command-passed.txt")) == "PASS");
            Check(provider + " emits actual token and intermediate events", tokens.Count >= 2 && activities.Count >= 3 && result.Usage.TotalTokens > 0 && updates.Count >= 2);
            if (provider == "Codex") {
                Check("Codex aggregate diff keeps both files", result.Changes.Any(file => file.Path.EndsWith("NativeFixture.txt")) && result.Changes.Any(file => file.Path.EndsWith("Second.txt")));
                string diff = result.Changes.Single(file => file.Path.EndsWith("NativeFixture.txt")).Diff;
                Check("Codex repeated patch shows cumulative final diff", diff.Contains("-old") && diff.Contains("+new") && !diff.Contains("intermediate"));
            } else {
                Check("Claude repeated Edit preserves both tool changes in updates", updates.Any(files => files.Any(file => file.Diff.Contains("-old") && file.Diff.Contains("-intermediate"))));
                Check("Claude final project diff reflects actual net change", result.Changes.Single(file => file.Path.EndsWith("NativeFixture.txt")).Diff.Contains("-old") && !result.Changes.Single(file => file.Path.EndsWith("NativeFixture.txt")).Diff.Contains("intermediate"));
            }
            path = Case("failed");
            try {
                if (provider == "Codex") await new CodexRuntimeRunner().RunAgentAsync(Fixture, path, "fail", context, null, null, CancellationToken.None);
                else await new ClaudeRuntimeRunner().RunAgentAsync(Fixture, path, "fail", context, null, null, CancellationToken.None);
                Check(provider + " failure rejected", false);
            } catch (InvalidOperationException error) { Check(provider + " failure not success and raw error not exposed", !error.Message.Contains("SECRET")); }
            path = Case("stall-initialize");
            using (var cancellation = new CancellationTokenSource(700)) {
                try {
                    if (provider == "Codex") await new CodexRuntimeRunner().RunAgentAsync(Fixture, path, "cancel", context, null, null, cancellation.Token);
                    else await new ClaudeRuntimeRunner().RunAgentAsync(Fixture, path, "cancel", context, null, null, cancellation.Token);
                    Check(provider + " initialize cancellation rejected", false);
                } catch (OperationCanceledException) { Check(provider + " initialize cancellation normalized", true); }
            }
            await Task.Delay(200);
            string pidFile = Path.Combine(path, "protocol.log.pid");
            int pid = Int32.Parse(File.ReadAllText(pidFile)); bool running;
            try { using (var child = Process.GetProcessById(pid)) running = !child.HasExited; } catch (ArgumentException) { running = false; }
            Check(provider + " cancellation kills child process tree", !running);
            path = Case("stall-initialize");
            try {
                if (provider == "Codex") await new CodexRuntimeRunner { Timeout = TimeSpan.FromMilliseconds(600) }.RunAgentAsync(Fixture, path, "timeout", context, null, null, CancellationToken.None);
                else await new ClaudeRuntimeRunner { Timeout = TimeSpan.FromMilliseconds(600) }.RunAgentAsync(Fixture, path, "timeout", context, null, null, CancellationToken.None);
                Check(provider + " deadline rejected", false);
            } catch (TimeoutException) { Check(provider + " initialize deadline distinct from stop", true); }
            path = Case("success");
            string sourcePath = Path.Combine(path, "Proposal.cs"); File.WriteAllText(sourcePath, "class Test { int oldValue; }");
            var source = new DocumentSnapshot(sourcePath, File.ReadAllText(sourcePath), "disk", "CSharp", false, false);
            var normal = new RuntimeEditProvider(); int reported = 0; normal.UsageUpdated += value => reported++;
            var proposal = await normal.GenerateAsync(provider, Fixture, source, "rename oldValue", CancellationToken.None);
            Check(provider + " normal mode returns proposal without direct write", proposal.ProposedText.Contains("newValue") && File.ReadAllText(sourcePath).Contains("oldValue") && reported == 1);
            Check(provider + " normal proposal transmits mandatory instructions", File.ReadAllText(Path.Combine(path, "protocol.log")).Contains(new JavaScriptSerializer().Serialize(new ChatRequestContext().EffectiveInstructions)));
            path = Case("commands-only"); File.WriteAllText(Path.Combine(path, "NativeFixture.txt"), "old");
            var commandResult = provider == "Codex"
                ? await new CodexRuntimeRunner().RunAgentAsync(Fixture, path, "command-only edit", context, null, null, CancellationToken.None)
                : await new ClaudeRuntimeRunner().RunAgentAsync(Fixture, path, "command-only edit", context, null, null, CancellationToken.None);
            var observed = commandResult.Changes.Single(file => file.Path.EndsWith("NativeFixture.txt"));
            Check(provider + " command-only write detected without native fileChange event", observed.Kind == "프로젝트 전후 비교" && observed.Diff.Contains("-old") && observed.Diff.Contains("+new"));
        }
        path = Case("server-request");
        try { await new CodexRuntimeRunner().RunAgentAsync(Fixture, path, "request", new ChatRequestContext(), null, null, CancellationToken.None); Check("unsupported server approval rejected", false); }
        catch (InvalidOperationException) { Check("unsupported server approval rejected", true); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CheckAutomaticDiscovery
    /// desc         : PATH 우선·오래된 PATH의 설치 후보·명시 경로 실패·안전한 공개 작업을 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 설치·로그인·추론 없이 발견 경계와 다중 진행 회귀 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void CheckAutomaticDiscovery()
    {
        string folder = Case("discovery"), path = Path.Combine(folder, "path"), known = Path.Combine(folder, "known");
        Directory.CreateDirectory(path); Directory.CreateDirectory(known);
        foreach (string name in new[] { "codex.exe", "claude.exe" }) { File.WriteAllText(Path.Combine(path, name), "fixture"); File.WriteAllText(Path.Combine(known, name), "fixture"); }
        var find = typeof(NativeCliDiscovery).GetMethod("FindIn", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Func<string, string[], string[], string> lookup = (provider, paths, files) => (string)find.Invoke(null, new object[] { provider, paths, files });
        foreach (string provider in new[] { "Codex", "Claude" }) {
            string file = provider.ToLowerInvariant() + ".exe", expected = Path.Combine(known, file);
            Check(provider + " PATH native exe takes precedence over known install", lookup(provider, new[] { path }, new[] { expected }) == Path.Combine(path, file));
            Check(provider + " known native install found when IDE PATH is stale", lookup(provider, new[] { Path.Combine(folder, "missing") }, new[] { expected }) == expected);
            Check(provider + " discovery rejects current directory and network shares", lookup(provider, new[] { ".", "relative", @"\\invalid\share" }, new string[0]) == null);
            Check(provider + " discovery rejects wrapper and other provider", lookup(provider, new string[0], new[] { Path.Combine(known, "setup.cmd"), Path.Combine(known, provider == "Codex" ? "claude.exe" : "codex.exe") }) == null);
            Check(provider + " missing installations return unknown", lookup(provider, new string[0], new[] { Path.Combine(folder, "absent", file) }) == null);
        }
        var resolve = typeof(CliEditProvider).GetMethod("ResolveExecutable", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        string working = Environment.CurrentDirectory;
        try {
            Environment.CurrentDirectory = folder;
            Check("drive-relative PATH never discovers a current-directory executable", lookup("Codex", new[] { Path.GetPathRoot(folder).TrimEnd('\\') + "path" }, new string[0]) == null);
        } finally { Environment.CurrentDirectory = working; }
        bool explicitMissingRejected = false;
        try { resolve.Invoke(null, new object[] { Path.Combine(folder, "missing.exe"), "codex.exe" }); }
        catch (System.Reflection.TargetInvocationException) { explicitMissingRejected = true; }
        Check("explicit missing CLI never silently chooses installed fallback", explicitMissingRejected);
        Check("explicit native CLI path remains authoritative", (string)resolve.Invoke(null, new object[] { Path.Combine(known, "codex.exe"), "codex.exe" }) == Path.Combine(known, "codex.exe"));
        var events = AgentProtocolEvents.ParseProgressEvents("Claude", "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"thinking\",\"thinking\":\"private\"},{\"type\":\"tool_use\",\"id\":\"one\",\"name\":\"Bash\",\"input\":{\"command\":\"dotnet test --secret=private\"}},{\"type\":\"tool_use\",\"id\":\"two\",\"name\":\"Write\",\"input\":{\"file_path\":\"C:/private/Probe.cs\"}}]}}");
        Check("all Claude public work blocks retain separate IDs without private arguments", events.Length == 2 && events[0].WorkId == "one" && events[1].WorkId == "two" && events[0].Detail == ".NET test" && !events.Any(value => (value.Text + value.Detail).Contains("private")));
        var ended = AgentProtocolEvents.ParseProgressEvents("Claude", "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"one\"},{\"type\":\"tool_result\",\"tool_use_id\":\"two\",\"is_error\":true}]}}");
        Check("all Claude tool results retain actual completion or failure", ended.Length == 2 && ended[0].WorkId == "one" && ended[0].State == "완료" && ended[1].State == "실패");
        var userContext = new ChatRequestContext(new String('u', 8000));
        var collaboratorContext = new ChatRequestContext(userContext.Instructions, agentContext: "separate collaboration");
        Check("mandatory rules preserve complete user limit and compose once across collaboration", userContext.Instructions.Length == 8000 && userContext.EffectiveInstructions == collaboratorContext.EffectiveInstructions && userContext.EffectiveInstructions == MandatoryInstructionStore.DefaultInstructions + "\n\n" + userContext.Instructions);
    }
}
