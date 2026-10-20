using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using VS2017.AIAssistant.Providers;

namespace VS2017.AIAssistant.Providers
{
#if !PRODUCT
    // Standalone compile boundary: the real tracker source is compiled unchanged.
    public sealed class AgentFileChange { public string Path, Kind, Diff; public bool HasLineCounts; }
#endif
}

/// ---------------------------------------------------------------------
/// <summary>
/// name         : LineEndingChecks
/// desc         : 실제 tracker의 원자 정규화·인코딩·비변경·읽기 전용·취소 경계를 검사한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// </summary>
/// ---------------------------------------------------------------------
internal static class LineEndingChecks
{
    private static int passed;
    private static object Tracker(string root)
    {
        Type type = typeof(AgentFileChange).Assembly.GetType("VS2017.AIAssistant.Providers.AgentFileTracker", true);
        var task = (Task)type.GetMethod("CreateAsync", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { root, CancellationToken.None });
        task.GetAwaiter().GetResult(); return task.GetType().GetProperty("Result").GetValue(task, null);
    }
    private static void Reconcile(object tracker, IDictionary<string, AgentFileChange> changes, CancellationToken cancellation)
    {
        try { tracker.GetType().GetMethod("Reconcile", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tracker, new object[] { changes, cancellation, true }); }
        catch (TargetInvocationException error) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    private static void Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        var encodings = new Encoding[] { new UTF8Encoding(false, true), new UTF8Encoding(true, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true), new UTF32Encoding(false, true, true), new UTF32Encoding(true, true, true) };
        for (int i = 0; i < encodings.Length; i++) {
            string path = Path.Combine(root, "Encoding" + i + ".cs"); var encoding = encodings[i];
            File.WriteAllText(path, "한글\r\nold\r\n", encoding);
            var tracker = Tracker(root);
            File.WriteAllText(path, "한글\r\nnew\n", encoding);
            Reconcile(tracker, new Dictionary<string, AgentFileChange>(), CancellationToken.None);
            Check(File.ReadAllBytes(path).SequenceEqual(encoding.GetPreamble().Concat(encoding.GetBytes("한글\r\nnew\r\n"))), "encoding/BOM preserved " + i);
        }
        string unix = Path.Combine(root, "Unix.py"), untouched = Path.Combine(root, "Untouched.cs"), readOnly = Path.Combine(root, "ReadOnly.cs"), binary = Path.Combine(root, "Binary.cs");
        File.WriteAllText(unix, "a\nb\n"); File.WriteAllText(untouched, "a\r\nb\n");
        File.WriteAllText(readOnly, "old\r\n"); File.WriteAllBytes(binary, new byte[] { 0xff, 0x80, 0x0a });
        byte[] originalUntouched = File.ReadAllBytes(untouched), originalBinary = File.ReadAllBytes(binary);
        var observed = Tracker(root);
        File.WriteAllText(unix, "A\r\nb\n"); File.WriteAllText(readOnly, "new\nline\r\n"); File.SetAttributes(readOnly, FileAttributes.ReadOnly);
        string added = Path.Combine(root, "New.cs"); File.WriteAllText(added, "new\r\nline\n");
        var changes = new Dictionary<string, AgentFileChange>(); Reconcile(observed, changes, CancellationToken.None);
        Check(File.ReadAllText(unix) == "A\nb\n", "existing LF remains LF");
        Check(File.ReadAllBytes(untouched).SequenceEqual(originalUntouched), "untouched mixed file remains byte exact");
        Check(File.ReadAllText(readOnly) == "new\nline\r\n", "read-only file remains unchanged");
        Check(File.ReadAllBytes(binary).SequenceEqual(originalBinary), "invalid text remains byte exact");
        Check(File.ReadAllText(added) == "new\r\nline\r\n", "new mixed file uses dominant line ending");
        Check(changes.ContainsKey(unix) && changes[unix].Diff.Contains("+A"), "final diff describes normalized actual source");
        Check(!changes.ContainsKey(untouched), "normalizer does not create unrelated changes");
        Check(Directory.GetFiles(root, ".ai-eol-*.tmp").Length == 0, "no temporary files remain");
        using (var stop = new CancellationTokenSource()) { stop.Cancel(); bool canceled = false; try { Reconcile(observed, changes, stop.Token); } catch (OperationCanceledException) { canceled = true; } Check(canceled, "cancellation is honored before writes"); }
        File.SetAttributes(readOnly, FileAttributes.Normal);
        File.WriteAllText(Path.Combine(root, "result.json"), "{\"Passed\":" + passed + ",\"Failed\":0,\"Assembly\":\"" + typeof(AgentFileChange).Assembly.Location.Replace("\\", "\\\\") + "\"}");
    }
}
