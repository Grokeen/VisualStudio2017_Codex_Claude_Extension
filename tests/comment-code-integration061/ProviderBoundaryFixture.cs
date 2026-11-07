using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using CommentCodePreview.Poc;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;

// SDK-independent boundary fixture: real adapter/EditProposal/DocumentSnapshot/core;
// IAiContextEditProvider below is deliberately fake. No native CLI, account or network is used.
namespace VS2017.AIAssistant.Chat
{
    public sealed class ChatRequestContext
    {
        public string Model;
        public ChatRequestContext(string model = "") { Model = model; }
    }
}
namespace VS2017.AIAssistant.Providers
{
    public interface IAiContextEditProvider
    {
        Task<EditProposal> GenerateWithContextAsync(string provider, string executable, DocumentSnapshot source, string instruction, VS2017.AIAssistant.Chat.ChatRequestContext context, CancellationToken token);
    }
    public sealed class CliEditProvider : IAiContextEditProvider
    {
        public CliEditProvider(TimeSpan timeout, bool previewOnly) { }
        public Task<EditProposal> GenerateWithContextAsync(string p, string e, DocumentSnapshot s, string i, VS2017.AIAssistant.Chat.ChatRequestContext c, CancellationToken t)
        { throw new Exception("The fixture must never launch a native provider."); }
    }
}
internal static class ProviderBoundaryFixture
{
    private static int passed;
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    private static void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, name); }
    private sealed class Provider : IAiContextEditProvider
    {
        internal string Response, SeenProvider, SeenExecutable, SeenModel, SeenInstruction;
        internal DocumentSnapshot SeenSource;
        internal bool Fail, ForeignSource;
        internal int Calls;
        public Task<EditProposal> GenerateWithContextAsync(string provider, string executable, DocumentSnapshot source, string instruction, VS2017.AIAssistant.Chat.ChatRequestContext context, CancellationToken token)
        {
            Calls++; SeenProvider = provider; SeenExecutable = executable; SeenModel = context.Model; SeenInstruction = instruction; SeenSource = source;
            if (Fail) throw new InvalidOperationException("unavailable account");
            var payload = new { summary = "controlled", edits = new[] { new { old_text = source.Content, new_text = Response } } };
            return Task.FromResult(EditProposal.Parse(ForeignSource ? new DocumentSnapshot(source.FilePath, source.Content, "other", "", true, false) : source, new JavaScriptSerializer().Serialize(payload)));
        }
    }
    private static EditorSnapshot Snapshot(string content, int position, int selection = 0)
    { return new EditorSnapshot("opaque", 42, 7, content, position + selection, position, selection, false, false, false, false); }
    private static int Main()
    {
        try
        {
            string text = "// 한글\r\nclass C {\r\n    \r\n}\r\n";
            int cursor = text.IndexOf("    ", StringComparison.Ordinal) + 4;
            var snapshot = Snapshot(text, cursor);
            string code = "int result = 1;";
            Check(CommentCodeProviderAdapter.ExtractInsertion(snapshot, text.Insert(cursor, code)) == code, "exact cursor insertion only");
            Reject(() => CommentCodeProviderAdapter.ExtractInsertion(snapshot, "X" + text.Insert(cursor, code)), "prefix outside insertion rejected");
            Reject(() => CommentCodeProviderAdapter.ExtractInsertion(snapshot, text.Insert(cursor, code) + "X"), "suffix outside insertion rejected");
            Reject(() => CommentCodeProviderAdapter.ExtractInsertion(snapshot, text), "unchanged response rejected");
            Reject(() => CommentCodeProviderAdapter.ExtractInsertion(snapshot, null), "null response rejected");
            Reject(() => CommentCodeProviderAdapter.ExtractInsertion(snapshot, text.Insert(cursor, new string('x', 8193))), "8193 character response rejected");
            Reject(() => CommentCodeProviderAdapter.ExtractInsertion(snapshot, text.Insert(cursor, new string('한', 6000))), "UTF8 response cap rejected");
            var selection = Snapshot("A\r\nold\r\nZ", 3, 3);
            Check(CommentCodeProviderAdapter.ExtractInsertion(selection, "A\r\noldnew\r\nZ") == "new", "selection preserved; insertion at active cursor");
            Reject(() => CommentCodeProviderAdapter.ExtractInsertion(selection, "A\r\nnew\r\nY"), "selection outside suffix change rejected");
            var empty = Snapshot("", 0);
            Check(CommentCodeProviderAdapter.ExtractInsertion(empty, "class C {}") == "class C {}", "empty source exact insertion");
            foreach (string brand in new[] { "Codex", "Claude" })
            {
                var fake = new Provider { Response = text.Insert(cursor, code) };
                var adapter = new CommentCodeProviderAdapter(fake, brand, "explicit-native.exe", "C:/private/Secret.cs", "exact-model");
                var request = new CommentCodeRequest(17, "// result 생성", snapshot);
                Check(adapter.GenerateAsync(request, CancellationToken.None).GetAwaiter().GetResult() == code, brand + " real adapter fake provider result");
                Check(fake.Calls == 1 && fake.SeenProvider == brand && fake.SeenExecutable == "explicit-native.exe" && fake.SeenModel == "exact-model", brand + " no provider/model/path fallback");
                Check(System.IO.Path.GetFileName(fake.SeenSource.FilePath) == "Secret.cs" && fake.SeenSource.Content == text && fake.SeenSource.IsReadOnly == false && fake.SeenSource.HasUnsavedChanges, brand + " captured unsaved source exact");
                Check(fake.SeenInstruction.Contains("[" + cursor + "," + cursor + ")") && fake.SeenInstruction.Contains(request.Instruction), brand + " exact UTF16 insertion and explicit comment");
                fake.Fail = true; Reject(() => adapter.GenerateAsync(request, CancellationToken.None).GetAwaiter().GetResult(), brand + " account/error is failure, no fallback");
                Check(fake.Calls == 2, brand + " no retry to another model");
                fake.Fail = false; fake.ForeignSource = true; Reject(() => adapter.GenerateAsync(request, CancellationToken.None).GetAwaiter().GetResult(), brand + " foreign response source rejected");
                int calls = fake.Calls;
                using (var cancelled = new CancellationTokenSource()) { cancelled.Cancel(); bool caught = false; try { adapter.GenerateAsync(request, cancelled.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { caught = true; } Check(caught && calls == fake.Calls, brand + " precancel prevents provider request"); }
            }
            Console.WriteLine("RESULT " + passed + "/" + passed + " PASS; FakeProviderBoundary=true; ActualAI=false; ActualIDE=false"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL " + error); return 1; }
    }
}
