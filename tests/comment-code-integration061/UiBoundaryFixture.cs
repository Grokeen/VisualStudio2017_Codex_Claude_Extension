using System;
using System.Windows;
using System.Windows.Controls;
using CommentCodePreview.Poc;
using VS2017.AIAssistant.Views;

// Real integration partial and dialog source compile against a fake product host only.
// No Window/UserControl instance is created, no Show/ShowDialog, OS input or IDE is used.
namespace VS2017.AIAssistant.Views
{
    public partial class AssistantControl : UserControl
    {
        private bool disposed = false, busy = false;
        private string selectedProvider = "Claude";
        private readonly TextBox ExecutablePath = null, ModelName = null;
        private readonly TextBlock StatusLabel = null;
    }
    public class AssistantDialog : Window
    {
        protected readonly ContentControl FooterContent = null, Body = null;
        protected readonly TextBlock Message = null;
        protected readonly StackPanel Actions = null;
        public AssistantDialog(AssistantControl owner, string title, string description) { throw new Exception("UI execution is forbidden in this fixture."); }
    }
}
internal static class UiBoundaryFixture
{
    private static int passed;
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    private static EditorSnapshot Snapshot(string text, int cursor, int start, int length)
    { return new EditorSnapshot("opaque", 9, 3, text, cursor, start, length, false, false, false, false); }
    private static int Main()
    {
        try
        {
            string text = "// 한글\r\n\t😀x\r\n선택\r\n영역\r\n";
            int start = text.IndexOf("선택", StringComparison.Ordinal);
            int length = "선택\r\n영역".Length;
            var dto = AssistantControl.BuildCommentCodePresentation(Snapshot(text, start + length, start, length), "C:/test/한글.cs", "new();");
            Check(dto.Line == 4 && dto.Column == 3, "multiline selection active cursor line and UTF16 column");
            Check(dto.ExistingText == "" && dto.ContextBefore + dto.ContextAfter == text, "selected comment remains unchanged context");
            Check(dto.TargetFilePath == "C:/test/한글.cs" && dto.ProposedCode == "new();", "target and proposal unchanged");
            int cursor = text.IndexOf('x');
            dto = AssistantControl.BuildCommentCodePresentation(Snapshot(text, cursor, cursor, 0), "C:/a.cs", "code");
            Check(dto.Line == 2 && dto.Column == 4 && dto.ExistingText.Length == 0, "tab one UTF16 and surrogate two columns");
            string lf = "한\n둘\n"; dto = AssistantControl.BuildCommentCodePresentation(Snapshot(lf, lf.Length, lf.Length, 0), "C:/b.cs", "z");
            Check(dto.Line == 3 && dto.Column == 1, "LF trailing empty line mapping");
            string longText = new string('a', 1200) + "😀\r\n" + new string('b', 1200);
            dto = AssistantControl.BuildCommentCodePresentation(Snapshot(longText, 2201, 2201, 0), "C:/c.cs", "z");
            Check(dto.ContextBefore.Length <= 1000 && dto.ContextAfter.Length <= 1000, "bounded display context");
            Check(dto.ContextBefore.Length == 0 || !Char.IsLowSurrogate(dto.ContextBefore[0]), "context never starts in surrogate pair");
            Check(dto.ContextBefore.Length == 0 || dto.ContextBefore[0] != '\n', "context never starts in CRLF midpoint");
            string comment = "// 주석\r\n    ";
            Check(AssistantControl.ExtractExplicitSourceComment(Snapshot(comment, comment.Length, comment.Length, 0)) == "// 주석", "previous source comment on blank cursor line");
            Check(AssistantControl.ExtractExplicitSourceComment(Snapshot(comment, 3, 3, 0)) == "// 주석", "current source comment");
            Check(AssistantControl.ExtractExplicitSourceComment(Snapshot("// 선택\ncode", 5, 0, 5)) == "// 선택", "explicit selected source comment preferred");
            Check(AssistantControl.ExtractExplicitSourceComment(Snapshot("/* 한글 */", 8, 0, 8)) == "/* 한글 */", "explicit block comment selection");
            Check(AssistantControl.ExtractExplicitSourceComment(Snapshot("int x;", 6, 0, 6)) == "", "code selection is not inferred as comment");
            Check(AssistantControl.ExtractExplicitSourceComment(Snapshot("// old\nint x;", 13, 13, 0)) == "", "previous comment ignored on a nonblank code line");
            Console.WriteLine("RESULT " + passed + "/" + passed + " PASS; UIInstantiated=false; ActualIDE=false"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL " + error); return 1; }
    }
}
