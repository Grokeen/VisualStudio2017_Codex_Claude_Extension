using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommentCodePreview.Poc;
using CommentCodePreview.Poc.Tests;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;

// Product classes are loaded from the built extension DLL; the editor and provider are fakes.
internal static class PauseProductContracts
{
    private sealed class FakeProvider : IAiContextEditProvider
    {
        internal int Calls;
        public Task<EditProposal> GenerateAsync(string p, string e, DocumentSnapshot s, string i, CancellationToken token)
        { Calls++; throw new InvalidOperationException("Unexpected provider call."); }
        public Task<EditProposal> GenerateWithContextAsync(string p, string e, DocumentSnapshot s, string i, ChatRequestContext c, CancellationToken token)
        { Calls++; throw new InvalidOperationException("Unexpected provider call."); }
    }

    private static int Main()
    {
        var assembly = typeof(PreviewSession).Assembly;
        var pauseType = assembly.GetType("VS2017.AIAssistant.Providers.CommentCodeSafetyPause", true);
        var paused = pauseType.GetMethod("IsPaused", BindingFlags.Static | BindingFlags.NonPublic);
        if (paused == null || !(bool)paused.Invoke(null, null)) throw new InvalidOperationException("Pause guard is not active.");
        var adapterType = assembly.GetType("VS2017.AIAssistant.Providers.CommentCodeProviderAdapter", true);
        foreach (string brand in new[] { "Codex", "Claude" })
        {
            var fake = new FakeProvider();
            var adapter = (ICommentCodeProvider)Activator.CreateInstance(adapterType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { fake, brand, "not-executed.exe", "C:/synthetic/file.cs", "test-model" }, null);
            var editor = new FakeEditorFixture("// synthetic comment\n");
            var before = editor.Capture();
            using (var session = new PreviewSession(adapter))
            {
                var result = session.RequestAsync(editor, "// synthetic comment", CancellationToken.None).GetAwaiter().GetResult();
                if (result.Outcome != RequestOutcome.Failed || result.Reason != PreviewReason.ProviderError || fake.Calls != 0 ||
                    session.GetPreview() != null || session.GetStatus().CanUndo || editor.Insertions != 0 || !before.Matches(editor.Capture()))
                    throw new InvalidOperationException("Paused request reached the provider or changed the editor: " + brand);
            }
        }
        Console.WriteLine("PASS paused product provider: Codex and Claude fake calls 0; source unchanged.");
        return 0;
    }
}
