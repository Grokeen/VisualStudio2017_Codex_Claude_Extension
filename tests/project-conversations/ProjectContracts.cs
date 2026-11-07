using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Services;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ProjectContracts
/// desc         : 실제 저장 소스와 순수 전환 게이트의 격리 계약만 검증한다.
/// author       : CodexOrig
/// create date  : 2026-11-08
/// </summary>
/// ---------------------------------------------------------------------
internal static class ProjectContracts
{
    static readonly List<object> Results = new List<object>();
    static void Check(string name, bool value) { Results.Add(new { Name = name, Passed = value }); if (!value) throw new Exception(name); }
    static bool Throws<T>(Action action) where T : Exception { try { action(); return false; } catch (T) { return true; } }
    public static int Main(string[] args)
    {
        int exit = 0;
        try { Run(args[0]); } catch (Exception error) { exit = 1; Results.Add(new { Name = "Unhandled", Passed = false, Type = error.GetType().FullName }); }
        File.WriteAllText(Path.Combine(args[0], "contract-result.json"), new JavaScriptSerializer().Serialize(new { ActualDte = false, ActualWpf = false, Gui = false, Failed = exit, Count = Results.Count, Cases = Results }));
        return exit;
    }
    static void Run(string root)
    {
        var a = ProjectConversationIdentity.ForSolution(@"C:\synthetic\A\demo.sln");
        var same = ProjectConversationIdentity.ForSolution(@"c:/synthetic/A/../A/DEMO.sln");
        var b = ProjectConversationIdentity.ForSolution(@"C:\synthetic\B\demo.sln");
        Check("canonical-case-and-dot", a.Key == same.Key);
        Check("same-name-different-path", a.Key != b.Key);
        Check("full-binding-hash", a.PathHash.Length == 64);
        Check("relative-rejected", Throws<ArgumentException>(() => ProjectConversationIdentity.ForSolution("demo.sln")));
        Check("drive-relative-rejected", Throws<ArgumentException>(() => ProjectConversationIdentity.ForSolution("C:demo.sln")));
        Check("none-and-legacy-distinct", ProjectConversationIdentity.NoSolution().Key != ProjectConversationIdentity.Legacy().Key);
        var gate = new ProjectConversationSwitchGate(a);
        Check("matching-send", gate.CanSend(same));
        gate.Request(b); Check("mismatch-send-rejected", !gate.CanSend(b) && !gate.CanSend(a));
        Check("busy-deferred", gate.Propose(true) == null && gate.Loaded.Key == a.Key);
        Check("idle-proposes-latest", gate.Propose(false).Key == b.Key);
        gate.Request(ProjectConversationIdentity.NoSolution()); Check("stale-commit-rejected", !gate.Commit(b));
        Check("none-commit", gate.Commit(ProjectConversationIdentity.NoSolution()));
        Check("none-send", gate.CanSend(ProjectConversationIdentity.NoSolution()));
        gate.Request(ProjectConversationIdentity.Unavailable()); Check("unavailable-failclosed", gate.Propose(false) == null && !gate.CanSend(a));
        gate.Request(ProjectConversationIdentity.Legacy()); gate.Commit(ProjectConversationIdentity.Legacy()); Check("legacy-readonly-send", !gate.CanSend(a));
        var globalRoot = Path.Combine(root, "w");
        var global = new MarkdownWorkspaceStore(globalRoot);
        global.SaveInstructions("synthetic common rule");
        var legacy = global.Create("Codex"); legacy.Draft = "legacy draft"; global.Save(legacy);
        var legacyBytes = File.ReadAllBytes(global.GetPath(legacy));
        var rulesBytes = File.ReadAllBytes(global.InstructionsPath);
        var ar = a.EnsureStoreRoot(globalRoot); var br = b.EnsureStoreRoot(globalRoot);
        var nr = ProjectConversationIdentity.NoSolution().EnsureStoreRoot(globalRoot);
        Check("three-scoped-roots", ar != br && br != nr && nr != globalRoot);
        Check("stable-store-root", ar == same.EnsureStoreRoot(globalRoot));
        Check("legacy-root-unmoved", ProjectConversationIdentity.Legacy().EnsureStoreRoot(globalRoot) == globalRoot);
        Check("unavailable-root-rejected", Throws<InvalidOperationException>(() => ProjectConversationIdentity.Unavailable().EnsureStoreRoot(globalRoot)));
        var sa = new MarkdownWorkspaceStore(ar); var sb = new MarkdownWorkspaceStore(br); var sn = new MarkdownWorkspaceStore(nr);
        var ra = sa.Create("Codex"); ra.Draft = "A draft \uD55C\uAE00\r\n"; ra.Messages.Add(new ChatMessage { Role = "user", Text = "synthetic A" }); sa.Save(ra);
        var rb = sb.Create("Codex"); rb.Draft = "B draft"; sb.Save(rb);
        var rn = sn.Create("Claude"); rn.Draft = "no solution"; sn.Save(rn);
        var beforeB = File.ReadAllBytes(sb.GetPath(rb));
        var transition = new ProjectConversationSwitchGate(a); transition.Request(b);
        Check("running-keeps-A", transition.Propose(true) == null);
        ra.Messages.Add(new ChatMessage { Role = "assistant", Text = "late synthetic A result" }); sa.Save(ra);
        Check("late-A-does-not-write-B", beforeB.SequenceEqual(File.ReadAllBytes(sb.GetPath(rb))));
        Check("late-A-result-present", sa.Load(sa.GetPath(ra), "Codex").Messages.Any(m => m.Text == "late synthetic A result"));
        Check("A-draft-before-switch", sa.Load(sa.GetPath(ra), "Codex").Draft == ra.Draft);
        Check("safe-commit-B", transition.Commit(transition.Propose(false)) && transition.CanSend(b));
        Check("B-isolated-records", sb.LoadOpen("Codex").Length == 1 && sb.LoadOpen("Codex")[0].Id == rb.Id);
        Check("none-isolated-records", sn.LoadOpen("Claude").Length == 1 && sa.LoadOpen("Claude").Length == 0);
        Check("global-record-bytes-preserved", legacyBytes.SequenceEqual(File.ReadAllBytes(global.GetPath(legacy))));
        Check("global-rules-bytes-preserved", rulesBytes.SequenceEqual(File.ReadAllBytes(global.InstructionsPath)));
        Check("no-copied-scope-rules", !File.Exists(sa.InstructionsPath) && !File.Exists(sb.InstructionsPath));
        var stale = sa.Load(sa.GetPath(ra), "Codex"); var writer = sa.Load(sa.GetPath(ra), "Codex");
        writer.Draft = "external writer"; sa.Save(writer); var committed = File.ReadAllBytes(sa.GetPath(writer)); stale.Draft = "unsaved local draft";
        Check("actual-CAS-rejects-stale", Throws<StaleConversationWriteException>(() => sa.Save(stale)));
        Check("CAS-keeps-external-bytes", committed.SequenceEqual(File.ReadAllBytes(sa.GetPath(writer))));
        Check("CAS-keeps-local-draft", stale.Draft == "unsaved local draft");
        var binding = Path.Combine(br, "scope-id.txt"); File.WriteAllText(binding, "synthetic wrong identity"); var bad = File.ReadAllBytes(binding);
        Check("binding-collision-rejected", Throws<InvalidDataException>(() => b.EnsureStoreRoot(globalRoot)));
        Check("binding-not-overwritten", bad.SequenceEqual(File.ReadAllBytes(binding)));
        WorkChecks();
    }
    static void WorkChecks()
    {
        var w = new WorkBoundary(); Check("idle-no-work", !w.HasWork());
        w.busy = true; Check("legacy-busy", w.HasWork()); w.busy = false;
        w.drainingRequests = true; Check("draining", w.HasWork()); w.drainingRequests = false;
        w.executingRequest = new object(); Check("executing", w.HasWork()); w.executingRequest = null;
        w.queuedRequests.Enqueue(new object()); Check("global-queue", w.HasWork()); w.queuedRequests.Clear();
        w.roomQueues["A"] = new Queue<object>(); w.roomQueues["A"].Enqueue(new object()); Check("room-queue", w.HasWork()); w.roomQueues.Clear();
        w.projectCaptureDepth = 1; Check("capture", w.HasWork()); w.projectCaptureDepth = 0;
        ComponentDispatcher.IsThreadModal = true; Check("modal", w.HasWork()); ComponentDispatcher.IsThreadModal = false;
        w.projectTransitionApplying = true; Check("transition-reentry", w.HasWork()); w.projectTransitionApplying = false;
        var pending = new TaskCompletionSource<bool>(); w.roomRuns["A"] = new FakeRun { Task = pending.Task };
        Check("native-run", w.HasWork()); w.roomRuns.Clear(); Check("removed-run-task-still-blocks", w.HasWork()); pending.SetResult(true); Check("completed-task-clears", !w.HasWork());
        pending = new TaskCompletionSource<bool>(); w.collaboratingRooms["A"] = new FakeRun { Task = pending.Task }; Check("collaboration", w.HasWork()); w.collaboratingRooms.Clear(); Check("removed-collaboration-task-blocks", w.HasWork()); pending.SetResult(true); Check("collaboration-completed", !w.HasWork());
        pending = new TaskCompletionSource<bool>(); w.requestQueueTask = pending.Task; Check("queue-task", w.HasWork()); pending.SetResult(true); Check("queue-task-completed", !w.HasWork());
        var d = new Dictionary<string, object>(); var a = new object(); d["same-id"] = a;
        var cached = WorkBoundary.Copy(d); var b = new object(); d["same-id"] = b; WorkBoundary.Restore(d, cached);
        Check("same-id-scope-cache-reference", Object.ReferenceEquals(d["same-id"], a));
    }
}
internal static class ComponentDispatcher { internal static bool IsThreadModal; }
internal sealed class FakeRun { internal Task Task; }
internal sealed class WorkBoundary
{
    internal bool busy, drainingRequests, projectTransitionApplying;
    internal int projectCaptureDepth;
    internal object executingRequest;
    internal Task requestQueueTask;
    internal readonly Dictionary<string, FakeRun> roomRuns = new Dictionary<string, FakeRun>(), collaboratingRooms = new Dictionary<string, FakeRun>();
    internal readonly Dictionary<string, Queue<object>> roomQueues = new Dictionary<string, Queue<object>>();
    internal readonly Queue<object> queuedRequests = new Queue<object>();
    readonly HashSet<Task> projectObservedTasks = new HashSet<Task>();
    /* EXACT_WORK_METHOD */
    /* EXACT_MAP_METHODS */
    internal bool HasWork() { return HasProjectConversationWork(); }
    internal static Dictionary<TKey, TValue> Copy<TKey, TValue>(Dictionary<TKey, TValue> d) { return CloneProjectMap(d); }
    internal static void Restore<TKey, TValue>(Dictionary<TKey, TValue> d, Dictionary<TKey, TValue> c) { RestoreProjectMap(d, c); }
}
