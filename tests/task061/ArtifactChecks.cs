using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Views;

namespace VS2017.AIAssistant.Chat
{
    public sealed class ChatMessage { public string Role { get; set; } }
}
namespace VS2017.AIAssistant.Views
{
    public sealed class MarkdownPreview : FrameworkElement { public string Markdown { get; set; } }
    public partial class AssistantControl : UserControl
    {
        private bool disposed;
        private readonly TextBlock StatusLabel = new TextBlock();
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Menu
        /// desc         : 실제 partial 메뉴 연결을 창 표시 없이 호출하는 fixture 진입점이다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 제품 호스트를 대체하는 최소 UI 전용 fixture.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ContextMenu Menu(MarkdownPreview preview)
        { var menu = new ContextMenu(); menu.Items.Add(new MenuItem { Header = "copy" }); AddArtifactActions(menu, preview); return menu; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : End
        /// desc         : 종료 후 메뉴 차단을 검사하기 위해 호스트 수명만 종료한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 실제 IDE나 사용자 창을 닫지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void End() { disposed = true; }
    }
}

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ArtifactChecks
/// desc         : 합성 답변으로 실제 파일 형식·보존·경쟁·UI 메뉴 계약을 검사한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK061 독립 source snapshot 검사, 실제 AI·사용자 문서·외부 프로그램 없음.
/// </summary>
/// ---------------------------------------------------------------------
internal static class ArtifactChecks
{
    private static readonly List<object> Results = new List<object>();
    private static string output;
    private static int failed;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 새 evidence 폴더에 유효 문서와 보안 실패·경쟁 결과를 남긴다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : Windows STA·출력 경계 확인, 과거 결과 덮어쓰기 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        output = Path.GetFullPath(args[0]);
        string documents = Path.Combine(output, "documents"); Directory.CreateDirectory(documents);
        string source = "# 파일 생성 검증\r\n한글과 English <tag> & 따옴표 \" ' 😀\r\n첫째\t둘째\r\n  앞뒤 공백  \r\n";
        Check("Markdown exact UTF8", () => { string p = Path.Combine(documents, "sample.md"); Save(p, source); Assert(File.ReadAllBytes(p).SequenceEqual(Utf8.GetBytes(source))); });
        Check("Text exact UTF8", () => { string p = Path.Combine(documents, "sample.txt"); Save(p, source); Assert(File.ReadAllText(p, Utf8) == source); });
        Check("Empty answer creates empty text", () => { string p = Path.Combine(documents, "empty.txt"); Save(p, null); Assert(new FileInfo(p).Length == 0); });
        Check("Cancel no IO even invalid path and content", () => { int before = Directory.GetFileSystemEntries(documents).Length; Assert(!AssistantArtifactWriter.TryCreateNew(false, @"\\server\no\a.exe", new string('x', AssistantArtifactWriter.MaxCharacters + 1))); Assert(before == Directory.GetFileSystemEntries(documents).Length); });
        Check("Existing file preserved", () => { string p = Path.Combine(documents, "sample.txt"); byte[] original = File.ReadAllBytes(p); Reject(() => Save(p, "replacement")); Assert(original.SequenceEqual(File.ReadAllBytes(p))); });
        Check("Directory target preserved", () => { string p = Path.Combine(documents, "directory.md"); Directory.CreateDirectory(p); Reject(() => Save(p, "replacement")); Assert(Directory.Exists(p)); });
        Check("Unsupported formats cannot masquerade as text", () => { foreach (string ext in new[] { ".pdf", ".xlsx", ".exe", ".dll", ".doc", ".pptx", ".zip", ".rtf" }) { string p = Path.Combine(documents, "blocked" + ext); Reject(() => Save(p, "data")); Assert(!File.Exists(p)); } });
        Check("All allowed code and structured text preserve bytes", () => { foreach (string ext in ".cs .js .ts .py .java .c .cpp .h .hpp .css .sql .ps1 .sh .bat .cmd .json .yaml .yml .xml .csv .tsv .ini .config .toml .vb .fs .go .rs .rb .php .swift .kt .r .jsx .tsx .log .markdown".Split(' ')) { string p = Path.Combine(documents, "exact" + ext); Save(p, source); Assert(File.ReadAllBytes(p).SequenceEqual(Utf8.GetBytes(source))); } });
        Check("Relative UNC device ADS drive-relative paths refused", () => { foreach (string path in new[] { "answer.md", @"C:answer.md", @"\\server\share\answer.md", @"\\?\C:\answer.md", @"\\.\NUL", documents + "\\answer.md:other", documents + "\\a\n.md" }) Reject(() => Save(path, "data")); });
        Check("Reserved names and ambiguous trailing spaces refused", () => { foreach (string name in new[] { "CON.md", "nul.txt", "COM1.cs", "LPT9.txt", "COM¹.md", "a.md.", "a.md ", "CONIN$.txt" }) Reject(() => Save(Path.Combine(documents, name), "data")); });
        Check("Missing parent not created", () => { string folder = Path.Combine(documents, "missing"); Reject(() => Save(Path.Combine(folder, "a.md"), "data")); Assert(!Directory.Exists(folder)); });
        Check("Large answer rejected before file creation", () => { string p = Path.Combine(documents, "large.md"); Reject(() => Save(p, new string('a', AssistantArtifactWriter.MaxCharacters + 1))); Assert(!File.Exists(p)); });
        Check("Invalid UTF16 not silently changed", () => { string p = Path.Combine(documents, "bad.md"); Reject(() => Save(p, "\ud800")); Assert(!File.Exists(p)); });
        Check("HTML literal script images and URLs are inert", () => { string attack = "</pre><script>alert(1)</script><img src='https://example.invalid/pixel'><a href='javascript:x'>x</a>"; string p = Path.Combine(documents, "sample.html"); Save(p, attack); string html = File.ReadAllText(p, Utf8); Assert(html.Contains("&lt;script&gt;") && !html.Contains("<script") && !html.Contains("<img") && !html.Contains("<a ") && html.Contains("default-src 'none'") && html.Contains("form-action 'none'")); });
        Check("DOCX real package and XML Unicode paragraphs tabs", () =>
        {
            string p = Path.Combine(documents, "sample.docx"); Save(p, source);
            using (var zip = ZipFile.OpenRead(p))
            {
                Assert(zip.Entries.Count == 3 && zip.GetEntry("word/document.xml") != null);
                var document = new XmlDocument { XmlResolver = null }; using (var stream = zip.GetEntry("word/document.xml").Open()) document.Load(stream);
                var ns = new XmlNamespaceManager(document.NameTable); ns.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                Assert(document.SelectNodes("//w:p", ns).Count == 5 && document.SelectNodes("//w:tab", ns).Count == 1);
                Assert(document.InnerText.Contains("한글과 English <tag> & 따옴표 \" ' 😀") && document.InnerText.Contains("  앞뒤 공백  "));
                foreach (var entry in zip.Entries) using (var reader = new StreamReader(entry.Open(), Utf8)) { string text = reader.ReadToEnd(); Assert(!text.Contains("TargetMode=\"External\"") && !text.Contains("macroEnabled") && !text.Contains("vbaProject")); }
            }
        });
        Check("DOCX invalid XML character leaves no file", () => { string p = Path.Combine(documents, "invalid.docx"); Reject(() => Save(p, "a\u0001b")); Assert(!File.Exists(p)); });
        Check("Single fence extracts only exact code", () => { string code, ext; Assert(AssistantArtifactWriter.TryExtractSingleCodeBlock("설명\r\n```csharp\r\n// 한글\r\nvar x = 1;\r\n```\r\n끝", out code, out ext)); Assert(code == "// 한글\r\nvar x = 1;\r\n" && ext == ".cs"); });
        Check("Tilde longer fence CRLF and unknown language", () => { string code, ext; Assert(AssistantArtifactWriter.TryExtractSingleCodeBlock(" ~~~~unknown\nhello\n ~~~~", out code, out ext) && code == "hello\n" && ext == ".txt"); });
        Check("Incomplete multiple and unfenced answers not guessed", () => { foreach (string text in new[] { "hello", "```cs\na", "```\na\n```\n```\nb\n```", "    ```cs\na\n    ```" }) { string code, ext; Assert(!AssistantArtifactWriter.TryExtractSingleCodeBlock(text, out code, out ext)); } });
        Check("Fence info cannot supply path or executable extension", () => { string code, ext; Assert(AssistantArtifactWriter.TryExtractSingleCodeBlock("```../../evil.exe\na\n```", out code, out ext) && ext == ".txt"); });
        Check("Exactly one concurrent creator wins and no originals replaced", () =>
        {
            string p = Path.Combine(documents, "race.txt"); int success = 0; string winner = null;
            Parallel.For(0, 24, i => { string value = "writer-" + i + "\n" + new string((char)('a' + i), 65536); try { Save(p, value); Interlocked.Increment(ref success); winner = value; } catch (IOException) { } });
            Assert(success == 1 && File.ReadAllText(p, Utf8) == winner);
        });
        Check("Reparse intermediate directory cannot redirect output", () => { string p = Path.Combine(output, "junction", "redirect.md"); Reject(() => Save(p, "data")); Assert(!File.Exists(Path.Combine(output, "junction-target", "redirect.md"))); });
        Check("UI menu offers AI answer and only single code block", () =>
        {
            var control = new AssistantControl(); var preview = new MarkdownPreview { DataContext = new ChatMessage { Role = "assistant" }, Markdown = "```py\nprint('한글')\n```" };
            var menu = control.Menu(preview); menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            var all = (MenuItem)menu.Items[1]; var code = (MenuItem)menu.Items[2]; Assert(all.IsEnabled && code.IsEnabled && all.Visibility == Visibility.Visible);
            preview.Markdown = "답변 원문"; menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent)); Assert(all.IsEnabled && !code.IsEnabled);
            preview.DataContext = new ChatMessage { Role = "user" }; menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent)); Assert(all.Visibility == Visibility.Collapsed && code.Visibility == Visibility.Collapsed);
            preview.DataContext = new ChatMessage { Role = "assistant" }; control.End(); menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent)); Assert(!all.IsEnabled && all.Visibility == Visibility.Collapsed);
        });
        Check("Failure paths leave no temporary files", () => Assert(Directory.GetFiles(documents, ".ai-artifact-*.tmp").Length == 0));
        var result = new { Succeeded = failed == 0, Passed = Results.Count - failed, Failed = failed, PointerBytes = IntPtr.Size, Tests = Results, ExternalAiUsed = false, ActualNativeSaveDialogUsed = false, ExistingFileOverwriteSupported = false, GeneratedDocx = Path.Combine(documents, "sample.docx") };
        File.WriteAllText(Path.Combine(output, "result.json"), new JavaScriptSerializer().Serialize(result), Utf8);
        Console.WriteLine("RESULT " + (Results.Count - failed) + "/" + Results.Count);
        return failed == 0 ? 0 : 1;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Save
    /// desc         : 명시적으로 승인한 새 파일 생성 경로를 호출한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : fixture 폴더 밖의 사용자 파일은 선택하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Save(string path, string value) { Assert(AssistantArtifactWriter.TryCreateNew(true, path, value)); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Reject
    /// desc         : 지원하지 않는 입력은 파일 생성 전 예외로 실패해야 한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실패를 성공으로 삼키지 않도록 별도 assertion 유지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Reject(Action action) { bool rejected = false; try { action(); } catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is UnauthorizedAccessException || e is XmlException) { rejected = true; } Assert(rejected); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Assert
    /// desc         : 조건 불일치를 해당 검사의 실패로 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : fixture 상태를 조용히 보정하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Assert(bool value) { if (!value) throw new InvalidOperationException("Assertion failed."); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 모든 독립 검사 결과와 원래 오류를 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실패 후에도 나머지 검사를 실행한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, Action action)
    { try { action(); Results.Add(new { Name = name, Passed = true }); Console.WriteLine("PASS " + name); } catch (Exception error) { failed++; Results.Add(new { Name = name, Passed = false, Error = error.ToString() }); Console.WriteLine("FAIL " + name + " " + error); } }
}
