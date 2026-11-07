using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Views;
using Task = System.Threading.Tasks.Task;
using Window = System.Windows.Window;
using Process = System.Diagnostics.Process;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ActivityFeedRuntime73
/// desc         : 실제 IDE의 기존 문서 서비스·실제 fixture 프로세스·제품 활동함을 연결하는 phase별 addon이다.
/// author       : CodexOrig 보조 개발 055
/// create date  : 2026-11-03
/// update date  : 2026-11-03 CodexOrig 보조 개발 055 : private old-reference 컴파일의 Process alias와 실제 UI 진입 경계를 명시.
/// update date  : 2026-11-03 CodexOrig 보조 개발 055 : 실제 pane 표시와 자동 읽음 gate를 관측하고 명시 읽음 검사를 분리.
/// update date  : 2026-11-03 CodexOrig 보조 개발 055 : 별도 positive 사본에서 실제 IDE 활성화 rendezvous와 전체 저장 미읽음 불변 검사를 추가.
/// </summary>
/// ---------------------------------------------------------------------
public sealed class ActivityFeedRuntime73
{
    private readonly AssistantControl owner;
    private readonly DTE dte;
    private readonly string output, gates, executable;
    private readonly Action<string, bool> check;
    private readonly List<object> observations = new List<object>();
    private readonly List<int> ownedPids = new List<int>();
    private readonly List<string> completed = new List<string>();
    private string stage;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ActivityFeedRuntime73
    /// desc         : 전용 출력과 실제 호스트만 허용하고 제품 내부 상태를 만들어 넣지 않는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    ///                2026-11-03 CodexOrig : restore에서 사용하지 않는 빈 fixture 경로를 선택값으로 처리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private ActivityFeedRuntime73(AssistantControl view, DTE host, string directory, string fixture, Action<string, bool> assert)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (host == null || !host.RegistryRoot.Contains("CodexCodeTest")) throw new InvalidOperationException("A dedicated actual IDE host is required.");
        owner = view; dte = host; check = assert;
        output = Path.Combine(Path.GetFullPath(directory), "task073", "runtime");
        if (!output.Contains(Path.Combine("artifacts", "ide"))) throw new InvalidOperationException("An owned IDE output is required.");
        string workspace = ((MarkdownWorkspaceStore)ActivityFeedRuntimeProbe73.Read(owner, "workspaceStore")).RootPath;
        if (!workspace.StartsWith(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The actual product workspace must remain inside this exact owned run output.");
        Directory.CreateDirectory(output);
        gates = Path.Combine(output, "gates"); Directory.CreateDirectory(gates);
        executable = String.IsNullOrWhiteSpace(fixture) ? null : Path.GetFullPath(fixture);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunAsync
    /// desc         : 알 수 없는 phase를 거부하고 restore에서는 다른 요청이나 UI focus보다 먼저 저장 상태를 읽는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public static async Task RunAsync(AssistantControl owner, DTE dte, string outputDirectory, string fixturePath,
        string phase, string snapshotManifestPath, Action<string, bool> check, string expectedFixtureSHA256 = null, string expectedSnapshotManifestSHA256 = null)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (phase != "exercise" && phase != "restore") throw new ArgumentException("TASK073 phase must be exercise or restore.");
        var run = new ActivityFeedRuntime73(owner, dte, outputDirectory, fixturePath, check);
        string previous = Environment.GetEnvironmentVariable("TASK073_FIXTURE_GATES");
        Exception failure = null;
        try
        {
            if (phase == "restore")
            {
                if (string.IsNullOrEmpty(expectedSnapshotManifestSHA256) || !string.Equals(FileHash(snapshotManifestPath), expectedSnapshotManifestSHA256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The restore manifest differs from its phase1 fixed hash.");
                run.Restore(snapshotManifestPath);
            }
            else
            {
                if (run.executable == null || !File.Exists(run.executable)) throw new FileNotFoundException("An exact local fixture is required.");
                if (Path.GetFileName(run.executable) != "ActivityFeedProtocolFixture73.exe" || string.IsNullOrEmpty(expectedFixtureSHA256))
                    throw new InvalidOperationException("Only the fixed local TASK073 fixture with an explicit SHA256 may execute.");
                using (var input = File.OpenRead(run.executable)) using (var hash = SHA256.Create())
                    if (!string.Equals(BitConverter.ToString(hash.ComputeHash(input)).Replace("-", ""), expectedFixtureSHA256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The actual fixture differs from its fixed binary hash.");
                Environment.SetEnvironmentVariable("TASK073_FIXTURE_GATES", run.gates);
                await run.ExerciseAsync();
            }
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            Exception cleanupFailure = null;
            try { if (phase == "exercise") await run.StopRemainingAsync(); }
            catch (Exception cleanupError) { cleanupFailure = cleanupError; }
            Environment.SetEnvironmentVariable("TASK073_FIXTURE_GATES", previous);
            run.Write("result.json", new { ActualIdeRuntimeTest = true, Phase = phase, CompletedCases = run.completed,
                LastStage = run.stage, Succeeded = failure == null && cleanupFailure == null, Error = failure == null ? null : failure.ToString(), CleanupError = cleanupFailure == null ? null : cleanupFailure.ToString(),
                Observations = run.observations, FixturePids = run.ownedPids, ProcessKillIssued = false,
                CrashReproduced = false, RootOwnsFinalIdeCleanup = true });
            if (cleanupFailure != null) throw new AggregateException("TASK073 owned cleanup failed; original failure is retained.", new[] { failure, cleanupFailure }.Where(error => error != null));
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ExerciseAsync
    /// desc         : 실제 요청으로 실행·대기·실패·취소·미읽음·승인·저장 보호와 재시작 snapshot을 만든다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private async Task ExerciseAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        string sourcePath = Path.Combine(Path.GetDirectoryName(output), "ActivityFeedFixture73.cs");
        string source = "class ActivityFeedFixture73 { int ActivityFeedValue73 = 1; }\r\n";
        if (File.Exists(sourcePath)) throw new InvalidOperationException("Preserve prior fixture source; use a fresh run.");
        File.WriteAllText(sourcePath, source, new UTF8Encoding(false));
        dte.ItemOperations.OpenFile(sourcePath); dte.ActiveDocument.Save(sourcePath);
        object commandInput = null, commandOutput = null;
        dte.Commands.Raise("{c9201782-e997-4591-a509-7dabd90e97ce}", 0x0100, ref commandInput, ref commandOutput);
        Must("existing product menu shows actual assistant pane after source open", owner.IsVisible);
        byte[] sourceBytes = File.ReadAllBytes(sourcePath);
        var a = NewRoom("Claude", true);
        Send("ACT73_A_OK PRIVATE_PROMPT_73 C:\\private\\private-path-73.cs PRIVATE_COMMAND_73");
        await StartedAsync(a, "ACT73_A_OK");
        Send("ACT73_A_NEXT"); Input.Text = "DRAFT_A_PRIVATE_73";
        Must("working queue remains owned by A", RowKind(a) == "Working" && Pending(a) == 1 && !File.Exists(StartPath("ACT73_A_NEXT")));
        ActivityFeedRuntimeProbe73.RequireExactRow(owner, a.Provider, a.Id, "Running");
        var b = NewRoom("Codex", true); Send("ACT73_B_FAIL"); await StartedAsync(b, "ACT73_B_FAIL"); Input.Text = "DRAFT_B_PRIVATE_73";
        Must("different providers own two live requests", Runs.Contains(a.Id) && Runs.Contains(b.Id) && a.Provider != b.Provider);
        await NavigateAsync(a);
        Must("live A navigation preserves its draft and queued successor", ReferenceEquals(Current, a) && Input.Text == "DRAFT_A_PRIVATE_73" && Pending(a) == 1 && Runs.Contains(b.Id));
        await NavigateAsync(b);
        Must("live B navigation preserves its draft and both native owners", ReferenceEquals(Current, b) && Input.Text == "DRAFT_B_PRIVATE_73" && Runs.Contains(a.Id) && Runs.Contains(b.Id));
        observations.Add(ActivityFeedRuntimeProbe73.ObserveOwnedRequest(owner, a, StartPath("ACT73_A_OK"), executable));
        observations.Add(ActivityFeedRuntimeProbe73.ObserveOwnedRequest(owner, b, StartPath("ACT73_B_FAIL"), executable));
        Release("ACT73_B_FAIL"); await StateAsync(b, ConversationRunState.Failed);
        Must("B actual failure does not complete A or replace B draft", a.State == ConversationRunState.Running && RowKind(b) == "Failed" && Input.Text == "DRAFT_B_PRIVATE_73");
        ActivityFeedRuntimeProbe73.RequireExactRow(owner, b.Provider, b.Id, "FailedOrStopped");
        await NavigateAsync(a);
        Must("one click opens exact provider and restores A draft", ReferenceEquals(Current, a) && Provider == "Claude" && Input.Text == "DRAFT_A_PRIVATE_73");
        await NavigateAsync(b);
        Must("return navigation preserves B draft", ReferenceEquals(Current, b) && Input.Text == "DRAFT_B_PRIVATE_73");
        Release("ACT73_A_OK"); await StartedAsync(a, "ACT73_A_NEXT");
        Must("same-room FIFO starts once after first completion", File.Exists(StartPath("ACT73_A_OK")) && Pending(a) == 0 && a.HasUnread);
        await NavigateAsync(a); Click("StopButton"); await StateAsync(a, ConversationRunState.Stopped);
        Must("actual stop remains separate from B failure", RowKind(a) == "Stopped" && b.State == ConversationRunState.Failed);
        Must("actual request messages do not cross provider owners", !a.Messages.Any(message => message.Text.Contains("ACT73_B_FAIL")) && !b.Messages.Any(message => message.Text.Contains("ACT73_A_OK")));
        Must("cancelled queued request fabricates no final reply", !a.Messages.Any(message => message.Role == "assistant" && message.Text.Contains("PRIVATE_MESSAGE_73 ACT73_A_NEXT")));
        Done("native-working-waiting-failure-cancel-owner-drafts");

        var c = NewRoom("Codex", true); Send("ACT73_C_OK"); await StartedAsync(c, "ACT73_C_OK"); Input.Text = "DRAFT_C_PRIVATE_73";
        await NavigateAsync(a); Release("ACT73_C_OK"); await StateAsync(c, ConversationRunState.Completed);
        Must("nonselected actual completion is unread", c.HasUnread && RowKind(c) == "Completed");
        ActivityFeedRuntimeProbe73.RequireExactRow(owner, c.Provider, c.Id, "Completed");
        ActivityFeedRuntimeProbe73.RequireExactRow(owner, c.Provider, c.Id, "Attention");
        await WaitForPositiveActivationAsync(c, FileHash(sourcePath));
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        Must("positive activation has no outstanding request or queued successor", Runs.Count == 0 && Pending(a) == 0 && Pending(b) == 0 && Pending(c) == 0);
        var unreadBefore = CapturePersistedUnread("before-positive-navigation");
        string targetKey = c.Provider + "/" + c.Id;
        Must("positive target is persisted unread before navigation", unreadBefore.ContainsKey(targetKey) && unreadBefore[targetKey]);
        string inputBeforeHash = TextHash(Input.Text);
        string sourceBeforeHash = FileHash(sourcePath);
        ObserveReadVisibility(c, "before-navigation");
        await NavigateAsync(c);
        ObserveReadVisibility(c, "after-navigation");
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        bool automaticReadGate = ObserveReadVisibility(c, "after-product-input-dispatch");
        Must("positive actual visible focused active automatic-read gate is true", automaticReadGate);
        Must("positive navigation belongs to the actual foreground IDE process", ForegroundProcessIsOwnedIde());
        await UntilAsync(() => !c.HasUnread, "positive visible focused active automatic read acknowledgement");
        ObserveReadVisibility(c, "after-gated-automatic-read");
        var unreadAfter = CapturePersistedUnread("after-positive-navigation");
        bool identicalKeys = unreadBefore.Count == unreadAfter.Count && unreadBefore.Keys.All(unreadAfter.ContainsKey);
        bool targetOnlyRead = identicalKeys && !unreadAfter[targetKey] && unreadBefore.All(pair => pair.Key == targetKey || unreadAfter[pair.Key] == pair.Value);
        Write("positive-persisted-unread-comparison.json", new { IdenticalProviderRecordKeys = identicalKeys, TargetProvider = c.Provider, TargetId = c.Id,
            TargetBeforeUnread = unreadBefore[targetKey], TargetAfterUnread = unreadAfter.ContainsKey(targetKey) ? (bool?)unreadAfter[targetKey] : null,
            AllOtherUnreadUnchanged = targetOnlyRead, BeforeCount = unreadBefore.Count, AfterCount = unreadAfter.Count,
            InputBeforeSHA256 = inputBeforeHash, InputAfterSHA256 = TextHash(Input.Text), ExpectedTargetDraftSHA256 = TextHash("DRAFT_C_PRIVATE_73"),
            TargetDraftPreserved = Input.Text == "DRAFT_C_PRIVATE_73", SourceBeforeSHA256 = sourceBeforeHash, SourceAfterSHA256 = FileHash(sourcePath),
            SourceUnchanged = sourceBytes.SequenceEqual(File.ReadAllBytes(sourcePath)), ActualAutomaticReadGate = automaticReadGate,
            ForegroundProcessMatchesOwnedIde = ForegroundProcessIsOwnedIde(), ExplicitReadFallbackUsed = false });
        Must("positive persisted provider record keys are identical and only target unread clears", targetOnlyRead);
        Must("positive automatic read preserves target draft and source bytes", Input.Text == "DRAFT_C_PRIVATE_73" && sourceBytes.SequenceEqual(File.ReadAllBytes(sourcePath)));
        observations.Add(new { Case = "positive-navigation-read-oracle", GatedAutomaticReadExecuted = true,
            ExplicitReadActionExecuted = false, PersistedTargetOnlyRead = targetOnlyRead });
        var read = ActivityFeedRuntimeProbe73.ReadOpenedRecords(Store.RootPath, c.Provider, Path.Combine(output, "one-read-after-navigation.json"));
        Must("exact navigated record is persisted read and draft is preserved", read.Count(r => r.Id == c.Id) == 1 && !read.Single(r => r.Id == c.Id).HasUnread && Input.Text == "DRAFT_C_PRIVATE_73");
        Done("actual-completion-unread-oneclick-exact-read");

        var proposalOwner = NewRoom("Claude", false); Send("ACT73_EDIT"); await StartedAsync(proposalOwner, "ACT73_EDIT"); Release("ACT73_EDIT");
        await StateAsync(proposalOwner, ConversationRunState.Completed);
        object proposal = ActivityFeedRuntimeProbe73.Read(owner, "pendingProposal");
        Must("real edit result awaits review", proposal != null && RowKind(proposalOwner) == "Review" && !((Button)owner.FindName("ApplyButton")).IsEnabled);
        Send("ACT73_EDIT_NEXT"); Input.Text = "DRAFT_REVIEW_PRIVATE_73";
        Must("unapproved edit retains attention while successor waits", Pending(proposalOwner) == 1 && !File.Exists(StartPath("ACT73_EDIT_NEXT")) && ActivityFeedRuntimeProbe73.RequireExactRow(owner, proposalOwner.Provider, proposalOwner.Id, "Attention") != null);
        await NavigateAsync(c); await NavigateAsync(proposalOwner);
        Must("navigation preserves unapproved proposal and draft without applying", ReferenceEquals(ActivityFeedRuntimeProbe73.Read(owner, "pendingProposal"), proposal) && Input.Text == "DRAFT_REVIEW_PRIVATE_73" && sourceBytes.SequenceEqual(File.ReadAllBytes(sourcePath)));
        Click("DiscardProposalButton"); await StartedAsync(proposalOwner, "ACT73_EDIT_NEXT");
        Click("StopButton"); await StateAsync(proposalOwner, ConversationRunState.Stopped);
        Must("explicit skip never auto-applies original proposal", sourceBytes.SequenceEqual(File.ReadAllBytes(sourcePath)));
        Done("proposal-queue-attention-navigation-no-autoapply");

        var unread = NewRoom("Codex", true); Send("ACT73_UNREAD_OK"); await StartedAsync(unread, "ACT73_UNREAD_OK");
        await NavigateAsync(a); Release("ACT73_UNREAD_OK"); await StateAsync(unread, ConversationRunState.Completed);
        string path = Path.Combine(Store.RootPath, "conversations", "codex", unread.Id + ".md");
        byte[] before = File.ReadAllBytes(path);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) await ReadActionAsync(unread);
        Must("read save lock failure restores unread and preserves original bytes", unread.HasUnread && before.SequenceEqual(File.ReadAllBytes(path)) && RowKind(unread) == "SaveFailed");
        await ReadActionAsync(unread); Must("retry explicit read succeeds after lock release", !unread.HasUnread);
        Done("read-save-failure-unread-restore-and-retry");

        var cas = NewRoom("Codex", true); Send("ACT73_CAS_OK"); await StartedAsync(cas, "ACT73_CAS_OK");
        var hold = NewRoom("Codex", true); Send("ACT73_RESTART_HOLD"); await StartedAsync(hold, "ACT73_RESTART_HOLD");
        await NavigateAsync(a);
        Release("ACT73_CAS_OK"); await StateAsync(cas, ConversationRunState.Completed);

        c.Title = "PRIVATE_MANUAL_TITLE_73"; c.IsTitleAutomatic = false; Store.Save(c);
        await ActivityFeedRuntimeProbe73.InspectVisibleFeedAsync(owner, dialog => {
            ActivityFeedRuntimeProbe73.RequireNoPrivateText(dialog, new[] { "ACT73_", "PRIVATE_PROMPT_73", "PRIVATE_MESSAGE_73", "PRIVATE_MANUAL_TITLE_73", "PRIVATE_ERROR_PATH_73", "PRIVATE_COMMAND_73", "private-path-73.cs", "DRAFT_", "EXTERNAL_CAS_DRAFT_PRIVATE_73" });
            return Task.CompletedTask;
        });
        Done("actual-display-tooltip-automation-privacy");

        var burst = NewRoom("Claude", true); Send("ACT73_BURST"); await StartedAsync(burst, "ACT73_BURST");
        var pulse = new List<double>(); var clock = Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) }; timer.Tick += (s, e) => pulse.Add(clock.Elapsed.TotalMilliseconds); timer.Start();
        try
        {
            await ActivityFeedRuntimeProbe73.InspectVisibleFeedAsync(owner, async dialog => {
                var row = FindRowButton(dialog, burst, false); Click("ActivityFeedButton");
                Must("modeless reopen reuses exactly one activity window", ReferenceEquals(ActivityFeedRuntimeProbe73.Read(owner, "activityFeedDialog"), dialog));
                await Task.Delay(700); await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Must("stable working row retains its actual button through burst refresh", ReferenceEquals(row, FindRowButton(dialog, burst, false)));
                ActivityFeedRuntimeProbe73.RequireNoPrivateText(dialog, new[] { "PRIVATE_BURST_73", "PRIVATE_MESSAGE_73" });
            });
        }
        finally { timer.Stop(); }
        Must("actual dispatcher remained responsive during burst", pulse.Count > 0 && ActivityFeedRuntimeProbe73.Read(owner, "activityFeedDialog") == null && ActivityFeedRuntimeProbe73.Read(owner, "activityFeedTimer") == null);
        observations.Add(new { Case = "burst", ActualFixtureProgressFrames = 1000, Heartbeats = pulse.Count, TickCountClaimed = false });
        Click("StopButton"); await StateAsync(burst, ConversationRunState.Stopped);
        Done("burst-modeless-singleton-close-timer-cleanup");
        owner.GetType().GetMethod("CloseChatClicked", Private).Invoke(owner, new object[] { owner, new RoutedEventArgs() });
        var snapshotType = owner.GetType().Assembly.GetType("VS2017.AIAssistant.Chat.ConversationActivityFilter", true);
        object afterClose = owner.GetType().GetMethod("BuildActivityFeedSnapshot", Private).Invoke(owner, new[] { Enum.Parse(snapshotType, "All") });
        var afterCloseRows = ((IEnumerable)ActivityFeedRuntimeProbe73.Read(afterClose, "VisibleItems")).Cast<object>();
        Must("actually closed conversation is excluded from the feed", !burst.IsOpen && !afterCloseRows.Any(row => Convert.ToString(ActivityFeedRuntimeProbe73.Read(row, "Provider")) == burst.Provider && Convert.ToString(ActivityFeedRuntimeProbe73.Read(row, "Id")) == burst.Id));
        Done("actual-closed-conversation-excluded");
        await IsolatedLifecycleAsync();

        await NavigateAsync(a);
        Must("restart hold is Codex while actual view remains Claude", Provider == "Claude" && hold.State == ConversationRunState.Running);
        var externalStore = new MarkdownWorkspaceStore(Store.RootPath);
        var durable = externalStore.LoadOpen("Codex").Single(record => record.Id == cas.Id);
        durable.Draft = "EXTERNAL_CAS_DRAFT_PRIVATE_73"; externalStore.Save(durable);
        string casPath = Path.Combine(Store.RootPath, "conversations", "codex", cas.Id + ".md");
        byte[] external = File.ReadAllBytes(casPath);
        await ReadActionAsync(cas);
        Must("stale read CAS preserves external latest bytes and unread", cas.HasUnread && external.SequenceEqual(File.ReadAllBytes(casPath)) && RowKind(cas) == "SaveFailed");
        Done("external-CAS-original-protection");
        var manifest = ActivityFeedSnapshot73.CaptureStable(Store.RootPath, Path.Combine(output, "rs"), hold.Provider, hold.Id);
        Write("restart-manifest-location.json", new { ManifestPath = manifest, ManifestSHA256 = FileHash(manifest), RealInFlightSavedSnapshot = true, CrashReproduced = false });
        await NavigateAsync(hold); Click("StopButton"); await StateAsync(hold, ConversationRunState.Stopped);
        Must("original actual hold request is normally cancelled after capture", !Runs.Contains(hold.Id));
        Must("external CAS bytes survive remaining normal request cleanup", external.SequenceEqual(File.ReadAllBytes(casPath)));
        ActivityFeedSnapshot73.VerifySnapshot(manifest);
        Done("actual-running-snapshot-and-normal-owned-cancel");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Restore
    /// desc         : phase2의 첫 동작으로 snapshot·실제 loader·실제 control 상태를 focus 이전에 대조한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private void Restore(string manifestPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        stage = "restore-before-any-focus-or-request";
        var manifest = ActivityFeedSnapshot73.VerifySnapshot(manifestPath);
        Write("restore-first-control-observation.json", ActivityFeedRuntimeProbe73.Capture(owner, "All"));
        Must("phase2 starts with no native room runs", Runs.Count == 0);
        var expected = ((IEnumerable)manifest["SavedStates"]).Cast<object>().Select(value => (Dictionary<string, object>)value).ToArray();
        foreach (string provider in new[] { "Claude", "Codex" })
        {
            var records = ActivityFeedRuntimeProbe73.ReadOpenedRecords(Store.RootPath, provider, Path.Combine(output, "restore-one-read-" + provider + ".json"));
            foreach (var prior in expected.Where(item => Convert.ToString(item["Provider"]) == provider))
            {
                string id = Convert.ToString(prior["Id"]), raw = Convert.ToString(prior["RawState"]);
                var record = records.Single(item => item.Id == id);
                string expectedState = raw == "Running" ? "Interrupted" : raw;
                bool expectedUnread = raw == "Running" || Convert.ToBoolean(prior["HasUnread"]);
                Must("restore loader preserves saved state " + provider + "/" + id, record.State.ToString() == expectedState);
                if (provider == "Codex") Must("restore nonselected Codex unread survives startup " + id, record.HasUnread == expectedUnread);
                object row = ActivityFeedRuntimeProbe73.RequireExactRow(owner, provider, id, "All");
                if (raw == "Running") Must("actual restored feed shows Interrupted without live process", Convert.ToString(ActivityFeedRuntimeProbe73.Read(row, "Kind")) == "Interrupted" && Convert.ToBoolean(ActivityFeedRuntimeProbe73.Read(row, "HasUnread")));
            }
        }
        ActivityFeedSnapshot73.VerifySnapshot(manifestPath);
        Done("actual-second-IDE-snapshot-restore-before-focus");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IsolatedLifecycleAsync
    /// desc         : 실제 저장 사본을 읽는 별도 control의 factory·Close·Dispose가 main 저장을 바꾸지 않는지 검사한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private async Task IsolatedLifecycleAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        string snapshot = ActivityFeedSnapshot73.CaptureStable(Store.RootPath, Path.Combine(output, "ls"), null, null);
        string clone = Path.Combine(output, "lc-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        ActivityFeedSnapshot73.Seed(snapshot, clone);
        string oldWorkspace = Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH"), oldSettings = Environment.GetEnvironmentVariable("VS2017_AI_SETTINGS_PATH");
        AssistantControl isolated = null;
        try
        {
            try
            {
                Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", clone);
                Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", Path.Combine(output, "lifecycle-settings.json"));
                isolated = new AssistantControl();
                isolated.Initialize((IActiveDocumentService)ActivityFeedRuntimeProbe73.Read(owner, "documents"), (IAiEditProvider)ActivityFeedRuntimeProbe73.Read(owner, "provider"));
            }
            finally { Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", oldWorkspace); Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", oldSettings); }
            var dialogType = isolated.GetType().Assembly.GetType("VS2017.AIAssistant.Views.AssistantDialog", true);
            var dialog = (Window)Activator.CreateInstance(dialogType, isolated, "Activity factory fixture", "Owned factory fixture");
            object context = isolated.GetType().GetMethod("BuildActivityFeedView", Private).Invoke(isolated, new object[] { dialog });
            Must("factory returns root and actions without showing or starting timer", ActivityFeedRuntimeProbe73.Read(context, "Root") is FrameworkElement && ActivityFeedRuntimeProbe73.Read(context, "Refresh") is Action && ActivityFeedRuntimeProbe73.Read(context, "Dispose") is Action && !dialog.IsVisible && ActivityFeedRuntimeProbe73.Read(isolated, "activityFeedTimer") == null);
            ((Action)ActivityFeedRuntimeProbe73.Read(context, "Dispose"))(); dialog.Close();
            await ActivityFeedRuntimeProbe73.InspectVisibleFeedAsync(isolated, actual => { isolated.Dispose(); Must("Dispose closes only own modeless window and timer", !actual.IsVisible && ActivityFeedRuntimeProbe73.Read(isolated, "activityFeedTimer") == null && ActivityFeedRuntimeProbe73.Read(isolated, "activityFeedTickHandler") == null); return Task.CompletedTask; });
            Done("factory-close-dispose-subscription-lifetime");
        }
        finally { if (isolated != null && !(bool)ActivityFeedRuntimeProbe73.Read(isolated, "disposed")) isolated.Dispose(); }
        ActivityFeedSnapshot73.VerifySnapshot(snapshot);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : NewRoom
    /// desc         : 기존 실제 provider 탭과 새 대화 버튼으로 요청 소유자를 만든다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private ConversationRecord NewRoom(string provider, bool full)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var tabs = (TabControl)owner.FindName("ProviderChoice"); tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Convert.ToString(item.Tag) == provider);
        if (Provider != provider) throw new InvalidOperationException("Actual provider selection was refused.");
        Click("NewChatButton"); owner.GetType().GetMethod("SetAgentFullAccess", Private).Invoke(owner, new object[] { full });
        ((TextBox)owner.FindName("ExecutablePath")).Text = executable;
        return Current;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : StartedAsync
    /// desc         : 실제 원자 start 파일과 살아 있는 소유 프로세스를 함께 기다린다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private async Task StartedAsync(ConversationRecord record, string id)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await UntilAsync(() => File.Exists(StartPath(id)), "native fixture start " + id);
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var evidence = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(StartPath(id)));
        int pid = Convert.ToInt32(evidence["Pid"]); if (!ownedPids.Contains(pid)) ownedPids.Add(pid);
        observations.Add(ActivityFeedRuntimeProbe73.ObserveOwnedRequest(owner, record, StartPath(id), executable));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : StateAsync
    /// desc         : 공개 결과와 실제 run 제거를 함께 기다려 UI 버튼 상태만으로 완료를 추정하지 않는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private Task StateAsync(ConversationRecord record, ConversationRunState state)
    { return UntilAsync(() => record.State == state && !Runs.Contains(record.Id), "native state " + state); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : NavigateAsync
    /// desc         : 실제 modeless 피드의 정확 안전 식별명 행을 한 번 클릭하고 provider+record를 확인한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private async Task NavigateAsync(ConversationRecord target)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await ActivityFeedRuntimeProbe73.InspectVisibleFeedAsync(owner, async dialog => { await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(); var action = FindRowButton(dialog, target, false); if (!action.IsEnabled || !action.IsVisible) throw new InvalidOperationException("The actual open action is unavailable."); action.BringIntoView(); action.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); });
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        Must("one-click exact navigation " + target.Provider + "/" + target.Id, Provider == target.Provider && ReferenceEquals(Current, target));
        Must("one-click navigation retains actual pane visibility", owner.IsVisible);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WaitForPositiveActivationAsync
    /// desc         : Root가 자기 IDE를 실제 활성화할 동안 dispatcher를 유지하며 nonce release만 최대 120초 기다린다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private async Task WaitForPositiveActivationAsync(ConversationRecord target, string sourceHash)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        stage = "positive-activation-rendezvous";
        string release = Path.Combine(output, "positive-activation-release.json");
        if (File.Exists(release) || File.Exists(Path.Combine(output, "positive-activation-ready.json")))
            throw new InvalidOperationException("Positive activation requires fresh owned rendezvous evidence.");
        string token = Guid.NewGuid().ToString("N");
        Write("positive-activation-ready.json", new { Token = token, OwnedIdeProcessId = Process.GetCurrentProcess().Id,
            TargetProvider = target.Provider, TargetId = target.Id, CurrentProvider = Provider,
            CurrentRecordId = Current == null ? null : Current.Id, MaximumWaitMilliseconds = 120000,
            SourceBeforeSHA256 = sourceHash, HelperFocusWrites = false, HelperMarkReadCalls = 0,
            ExpectedGuiExecutor = "CodexOrig", RequiredReleaseFileName = "positive-activation-release.json" });
        ObserveReadVisibility(target, "activation-ready");
        var clock = Stopwatch.StartNew();
        int polls = 0;
        while (!File.Exists(release))
        {
            if (clock.ElapsedMilliseconds >= 120000) throw new TimeoutException("Actual owned IDE activation release was not received within 120 seconds.");
            if (Convert.ToBoolean(ActivityFeedRuntimeProbe73.Read(owner, "disposed"))) throw new ObjectDisposedException("Actual positive owner closed while waiting.");
            polls++;
            await Task.Delay(50);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        }
        if (clock.ElapsedMilliseconds > 120000) throw new TimeoutException("Actual owned IDE activation release exceeded its 120 second bound.");
        if ((File.GetAttributes(release) & FileAttributes.ReparsePoint) != 0 || new FileInfo(release).Length > 4096)
            throw new InvalidDataException("The owned activation release must be a small regular JSON file.");
        var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(release, Encoding.UTF8));
        if (!data.ContainsKey("Token") || Convert.ToString(data["Token"]) != token ||
            !data.ContainsKey("GuiExecutor") || Convert.ToString(data["GuiExecutor"]) != "CodexOrig" ||
            !data.ContainsKey("ActivatedOwnedIdeUsingCua") || !(data["ActivatedOwnedIdeUsingCua"] is bool) || !(bool)data["ActivatedOwnedIdeUsingCua"])
            throw new InvalidDataException("The activation release must match the exact nonce and Root executor acknowledgement.");
        Write("positive-activation-received.json", new { WaitMilliseconds = clock.Elapsed.TotalMilliseconds, Polls = polls,
            ReleaseSHA256 = FileHash(release), RootAcknowledgedActualCuaActivation = true,
            ForegroundProcessMatchesOwnedIde = ForegroundProcessIsOwnedIde(), HelperFocusWrites = false, HelperMarkReadCalls = 0 });
        ObserveReadVisibility(target, "activation-released");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CapturePersistedUnread
    /// desc         : 닫힌 대화도 포함해 실제 store의 읽기 전용 parser로 저장 미읽음을 읽고 관측 자체의 파일 불변을 확인한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    ///                2026-11-03 CodexOrig : ?ㅼ젣 ??μ냼???몄뒪?댁뒪 ReadBounded 怨꾩빟怨??섏떊 媛앹껜瑜??쇱튂?쒗궓??
    /// </summary>
    /// ---------------------------------------------------------------------
    private Dictionary<string, bool> CapturePersistedUnread(string phase)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var store = Store;
        var type = store.GetType();
        var read = type.GetMethod("ReadRecord", Private, null, new[] { typeof(string) }, null);
        var bounded = type.GetMethod("ReadBounded", Private, null, new[] { typeof(string) }, null);
        var header = type.GetMethod("ReadHeader", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(string).MakeByRefType() }, null);
        if (read == null || read.ReturnType != typeof(ConversationRecord) || bounded == null || bounded.ReturnType != typeof(string) ||
            header == null || header.ReturnType != typeof(Dictionary<string, string>)) throw new MissingMethodException("Actual readonly persisted metadata parser contract is unavailable.");
        var filesBefore = PersistedFileInventory();
        var values = new Dictionary<string, bool>(StringComparer.Ordinal);
        var rows = new List<object>();
        foreach (string provider in new[] { "Claude", "Codex" })
        {
            string directory = Path.Combine(store.RootPath, "conversations", provider.ToLowerInvariant());
            if (!Directory.Exists(directory)) continue;
            foreach (string path in Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
            {
                var record = (ConversationRecord)read.Invoke(store, new object[] { path });
                var metadata = (Dictionary<string, string>)header.Invoke(null, new object[] { bounded.Invoke(store, new object[] { path }), null });
                string raw;
                if (!metadata.TryGetValue("has_unread", out raw) || raw != "true" && raw != "false") throw new InvalidDataException("Persisted unread metadata is absent or invalid.");
                string key = record.Provider + "/" + record.Id;
                if (record.Provider != provider || Path.GetFileName(path) != record.Id + ".md" || values.ContainsKey(key))
                    throw new InvalidDataException("Persisted provider record identity is duplicated or misplaced.");
                bool unread = raw == "true";
                values.Add(key, unread);
                rows.Add(new { Provider = record.Provider, Id = record.Id, HasUnread = unread, IsOpen = record.IsOpen,
                    LoaderHasUnread = record.HasUnread, MainFileSHA256 = FileHash(path) });
            }
        }
        var filesAfter = PersistedFileInventory();
        bool unchanged = filesBefore.Count == filesAfter.Count && filesBefore.All(pair => filesAfter.ContainsKey(pair.Key) && filesAfter[pair.Key] == pair.Value);
        Write("positive-persisted-unread-" + phase + ".json", new { Rows = rows, IncludesClosedRecords = true,
            ActualReadOnlyStoreParser = true, RawPersistedHasUnreadObserved = true, ObservedFilesBefore = filesBefore.Count,
            ObservedFilesAfter = filesAfter.Count, ObservationFilesUnchanged = unchanged, ReadCallsOnly = true });
        Must("positive persisted metadata observer leaves every stored file unchanged " + phase, unchanged);
        return values;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PersistedFileInventory
    /// desc         : 자기 대화 저장 하위의 md/json만 제한된 개수·크기로 해시하며 링크를 거절하고 원문은 기록하지 않는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private Dictionary<string, string> PersistedFileInventory()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        long bytes = 0;
        var directories = new Stack<string>();
        string root = Path.Combine(Store.RootPath, "conversations");
        if (!Directory.Exists(root)) return values;
        directories.Push(root);
        int visited = 0;
        while (directories.Count != 0)
        {
            string directory = directories.Pop();
            if (++visited > 10000 || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Owned stored directory inventory exceeds its bound or contains a link.");
            foreach (string child in Directory.EnumerateDirectories(directory)) directories.Push(child);
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Owned stored file inventory contains a link.");
                string extension = Path.GetExtension(file);
                if (extension != ".md" && extension != ".json") continue;
                long length = new FileInfo(file).Length;
                bytes += length;
                if (length > 4 * 1024 * 1024 || bytes > 32 * 1024 * 1024 || values.Count >= 10000)
                    throw new InvalidDataException("Owned persisted inventory exceeds its byte or file bound.");
                values.Add(file, FileHash(file));
            }
        }
        return values;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ForegroundProcessIsOwnedIde
    /// desc         : foreground 창의 PID를 읽기만 하며 현재 전용 IDE 프로세스인지 비교한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool ForegroundProcessIsOwnedIde()
    {
        uint pid;
        IntPtr window = GetForegroundWindow();
        return window != IntPtr.Zero && GetWindowThreadProcessId(window, out pid) != 0 && pid == (uint)Process.GetCurrentProcess().Id;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GetForegroundWindow
    /// desc         : 현재 foreground 창 핸들을 읽는 Windows API 선언이며 활성화·입력을 수행하지 않는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GetWindowThreadProcessId
    /// desc         : 주어진 창의 소유 PID만 읽는 Windows API 선언이다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TextHash
    /// desc         : 입력 원문 대신 비교 가능한 UTF8 SHA256만 증거에 기록한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string TextHash(string text)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ObserveReadVisibility
    /// desc         : 원문·제목·경로 없이 실제 읽음 gate와 소유 화면의 focus 조건만 기록한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private bool ObserveReadVisibility(ConversationRecord target, string phase)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var method = owner.GetType().GetMethod("IsConversationInView", Private, null, Type.EmptyTypes, null);
        if (method == null || method.ReturnType != typeof(bool)) throw new MissingMethodException("The actual read visibility gate is unavailable.");
        bool gate = (bool)method.Invoke(owner, null);
        var window = Window.GetWindow(owner);
        var scroll = owner.FindName("ConversationScroll") as ScrollViewer;
        if (scroll == null) throw new MissingMemberException("The actual conversation scroll is unavailable.");
        var focused = Keyboard.FocusedElement;
        Write("read-visibility-" + phase + ".json", new { Phase = phase, ActualIsConversationInView = gate,
            OwnerLoaded = owner.IsLoaded, OwnerVisible = owner.IsVisible, OwnerKeyboardFocusWithin = owner.IsKeyboardFocusWithin,
            OwnWindowPresent = window != null, OwnWindowActive = window == null ? (bool?)null : window.IsActive,
            OwnWindowState = window == null ? null : window.WindowState.ToString(), ScrollFocusable = scroll.Focusable,
            ScrollIsFocused = scroll.IsFocused, FocusedElementType = focused == null ? null : focused.GetType().FullName,
            ProviderMatches = Provider == target.Provider, RecordMatches = ReferenceEquals(Current, target), HasUnread = target.HasUnread,
            TargetProvider = target.Provider, TargetId = target.Id, ForegroundProcessMatchesOwnedIde = ForegroundProcessIsOwnedIde() });
        return gate;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadActionAsync
    /// desc         : 선택을 바꾸지 않는 실제 읽음 단추를 통해 저장 실패·CAS 경계를 검사한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private async Task ReadActionAsync(ConversationRecord target)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await ActivityFeedRuntimeProbe73.InspectVisibleFeedAsync(owner, async dialog => { await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(); var action = FindRowButton(dialog, target, true); if (!action.IsEnabled || !action.IsVisible) throw new InvalidOperationException("The actual read action is unavailable."); action.BringIntoView(); action.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); });
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FindRowButton
    /// desc         : snapshot의 안전 식별명과 실제 접근성 action을 연결하고 동명·누락은 거절한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private Button FindRowButton(Window dialog, ConversationRecord target, bool read)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        object row = ActivityFeedRuntimeProbe73.RequireExactRow(owner, target.Provider, target.Id, "All");
        string safe = Convert.ToString(ActivityFeedRuntimeProbe73.Read(row, "SafeName"));
        string suffix = read ? "읽음 처리" : "대화 열기";
        return Visuals(dialog).OfType<Button>().Single(button => AutomationProperties.GetName(button).StartsWith(safe + " · ", StringComparison.Ordinal) && AutomationProperties.GetName(button).EndsWith(suffix, StringComparison.Ordinal));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : UntilAsync
    /// desc         : dispatcher를 막지 않고 제한 시간만 실제 소유 조건을 기다린다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private async Task UntilAsync(Func<bool> condition, string description)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        stage = description; var clock = Stopwatch.StartNew();
        while (!condition()) { if (clock.ElapsedMilliseconds >= 15000) { Write("timeout-observation.json", ActivityFeedRuntimeProbe73.Capture(owner, "All")); throw new TimeoutException(description); } await Task.Delay(50); await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : StopRemainingAsync
    /// desc         : 실패 시에도 실제 실행 중인 자기 fixture 요청만 기존 Stop 경로로 취소한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private async Task StopRemainingAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        foreach (object value in Runs.Values.Cast<object>().ToArray())
        {
            object request = ActivityFeedRuntimeProbe73.Read(value, "Request");
            if (!string.Equals(Convert.ToString(ActivityFeedRuntimeProbe73.Read(request, "Executable")), executable, StringComparison.OrdinalIgnoreCase)) continue;
            var record = (ConversationRecord)ActivityFeedRuntimeProbe73.Read(request, "Record");
            var item = ActivityFeedRuntimeProbe73.RequireExactRow(owner, record.Provider, record.Id, "All");
            if ((bool)owner.GetType().GetMethod("TryOpenActivityConversation", Private).Invoke(owner, new[] { item })) Click("StopButton");
        }
        var watch = Stopwatch.StartNew();
        bool ownedRunsRemain;
        do
        {
            foreach (string file in Directory.GetFiles(gates, "*.start.json"))
            {
                int pid = Convert.ToInt32(new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(file))["Pid"]);
                if (!ownedPids.Contains(pid)) ownedPids.Add(pid);
            }
            ownedRunsRemain = Runs.Values.Cast<object>().Any(value => string.Equals(Convert.ToString(ActivityFeedRuntimeProbe73.Read(ActivityFeedRuntimeProbe73.Read(value, "Request"), "Executable")), executable, StringComparison.OrdinalIgnoreCase));
            if (!ownedPids.Any(Alive) && !ownedRunsRemain) break;
            await Task.Delay(50); await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        } while (watch.ElapsedMilliseconds < 10000);
        Write("owned-cli-cleanup.json", new { Pids = ownedPids, StillAlive = ownedPids.Where(Alive).ToArray(), OwnedNativeRunsRemain = ownedRunsRemain, StartEvidenceFiles = Directory.GetFiles(gates, "*.start.json").Length, KillIssued = false });
        if (ownedPids.Any(Alive) || ownedRunsRemain) throw new InvalidOperationException("Owned fixture processes or runs did not stop; no kill was issued.");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Alive
    /// desc         : 관측한 정확한 자기 PID의 생존만 읽고 다른 프로세스를 제어하지 않는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool Alive(int pid) { try { using (var process = Process.GetProcessById(pid)) return !process.HasExited; } catch (ArgumentException) { return false; } }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FileHash
    /// desc         : Phase1 확정 manifest를 Phase2 인자와 대조할 SHA256을 읽는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string FileHash(string path) { using (var input = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", ""); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Visuals
    /// desc         : 자기 활동창의 실제 visual descendants만 순회한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
    { yield return root; for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++) foreach (var item in Visuals(VisualTreeHelper.GetChild(root, index))) yield return item; }

    private string Provider { get { return Convert.ToString(ActivityFeedRuntimeProbe73.Read(owner, "selectedProvider")); } }
    private ConversationRecord Current { get { return ((IDictionary)ActivityFeedRuntimeProbe73.Read(owner, "activeRecords"))[Provider] as ConversationRecord; } }
    private IDictionary Runs { get { return (IDictionary)ActivityFeedRuntimeProbe73.Read(owner, "roomRuns"); } }
    private MarkdownWorkspaceStore Store { get { return (MarkdownWorkspaceStore)ActivityFeedRuntimeProbe73.Read(owner, "workspaceStore"); } }
    private TextBox Input { get { return (TextBox)owner.FindName("Instruction"); } }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : StartPath
    /// desc         : 자기 gate의 원자 요청 시작 증거 경로만 반환한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private string StartPath(string id) { return Path.Combine(gates, id + ".start.json"); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RowKind
    /// desc         : 실제 typed snapshot에서 정확 소유 행의 상태를 읽는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private string RowKind(ConversationRecord record) { ThreadHelper.ThrowIfNotOnUIThread(); return Convert.ToString(ActivityFeedRuntimeProbe73.Read(ActivityFeedRuntimeProbe73.RequireExactRow(owner, record.Provider, record.Id, "All"), "Kind")); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pending
    /// desc         : 실제 소유 행의 후속 요청 개수를 읽는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private int Pending(ConversationRecord record) { ThreadHelper.ThrowIfNotOnUIThread(); return Convert.ToInt32(ActivityFeedRuntimeProbe73.Read(ActivityFeedRuntimeProbe73.RequireExactRow(owner, record.Provider, record.Id, "All"), "PendingCount")); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Send
    /// desc         : 실제 입력란과 생성 버튼으로 요청을 제출한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private void Send(string text) { Input.Text = text; Click("GenerateButton"); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Release
    /// desc         : 해당 합성 요청의 자기 release gate만 생성한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private void Release(string id) { File.WriteAllText(Path.Combine(gates, id + ".release"), "release", new UTF8Encoding(false)); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Click
    /// desc         : 실제 제품의 활성 단추 이벤트를 실행하고 비활성은 실패로 구분한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private void Click(string name) { var button = owner.FindName(name) as Button; if (button == null || !button.IsEnabled) throw new InvalidOperationException("Required actual product button is disabled: " + name); button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Must
    /// desc         : 실제 검사 결과를 공용 recorder에 전달하고 실패 시 후속 case를 중단한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private void Must(string name, bool condition) { check("ACTIVITY73 " + name, condition); if (!condition) throw new InvalidOperationException("ACTIVITY73 " + name); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Done
    /// desc         : 완료된 case 이름과 원문 없는 실제 snapshot을 보존한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private void Done(string name) { ThreadHelper.ThrowIfNotOnUIThread(); completed.Add(name); observations.Add(new { Case = name, Snapshot = ActivityFeedRuntimeProbe73.Capture(owner, "All") }); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Write
    /// desc         : 자기 출력의 진단 JSON만 기록한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private void Write(string name, object value) { File.WriteAllText(Path.Combine(output, name), new JavaScriptSerializer { MaxJsonLength = 16000000 }.Serialize(value), new UTF8Encoding(false)); }
}
