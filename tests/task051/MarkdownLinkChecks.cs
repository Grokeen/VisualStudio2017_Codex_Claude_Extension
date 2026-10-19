using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : MarkdownLinkChecks
/// desc         : Role #6 링크의 복사·클릭 경계와 실제 좁은 WPF 열 위치를 검사한다.
/// author       : CodexOrig
/// create date  : 2026-10-19
/// update date  : 2026-10-19 CodexOrig : 창·브라우저·클립보드를 열지 않는 source/고정 DLL 검사.
/// </summary>
/// ---------------------------------------------------------------------
internal static class MarkdownLinkChecks
{
    private static readonly List<object> results = new List<object>();
    private static int failed;
    private static string output;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 실제 미리보기에서 굵기·대상 보존·정렬·안전 한도와 선택 가능성을 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : 실제 입력이나 IDE 검증과 구분하여 증거를 저장.
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        output = args[0];
        using (var preview = new MarkdownPreview { FontSize = 13, Foreground = Brushes.Gainsboro, Background = Brushes.Black })
        {
            Check("Markdown, reference, source, anchor and raw web links are bold", () => {
                preview.Markdown = "[web](https://example.test/a) [src](C:/src/a.cs:7) [ref][] [anchor](#title) <https://example.test/b> https://example.test/c.\n\n[ref]: https://example.test/r";
                var links = Nodes(preview.TextView.Document).OfType<Hyperlink>().ToArray();
                Require(links.Length == 6 && links.All(x => x.FontWeight == FontWeights.Bold && x.NavigateUri == null), "bold safe links");
                Require(Equals(links.Last().Tag, "https://example.test/c"), "sentence punctuation changed destination");
            });
            Check("link activation delegates exactly once without automatic navigation", () => {
                preview.Markdown = "- [file.cs](C:/src/file.cs:12): 설명";
                int calls = 0; string target = null;
                EventHandler<MarkdownLinkEventArgs> handler = (sender, e) => { calls++; target = e.Target; e.Handled = true; };
                preview.LinkRequested += handler;
                Require(calls == 0, "render navigated");
                Nodes(preview.TextView.Document).OfType<Hyperlink>().Single().RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
                preview.LinkRequested -= handler;
                Require(calls == 1 && target == "C:/src/file.cs:12", "click changed target/count");
            });
            Check("code and unsafe destinations stay inert", () => {
                preview.Markdown = "`https://example.test/code` [bad](javascript:alert(1)) [credentials](https://u:p@example.test)\n\n```text\nhttps://example.test/fence\n```";
                Require(!Nodes(preview.TextView.Document).OfType<Hyperlink>().Any(), "code or unsafe URL linked");
            });
            Check("raw web URL parentheses and short trailing text", () => {
                preview.Markdown = "(https://example.test/path(a)). h H https://example.test/%0Asecret";
                var links = Nodes(preview.TextView.Document).OfType<Hyperlink>().ToArray();
                Require(links.Length == 1 && Equals(links[0].Tag, "https://example.test/path(a)"), "raw URL scan");
            });
            Check("inert HTML attributes never turn into raw hyperlinks", () => {
                preview.Markdown="<script>alert(1)</script>\n<iframe src='https://bad.test'/>";
                Require(!Nodes(preview.TextView.Document).OfType<Hyperlink>().Any(),"HTML attribute activated");
            });
            foreach(string markup in new[]{"prefix <iframe title=\"x > https://example.test/hidden\"></iframe>","prefix <iframe title='x > https://example.test/hidden'></iframe>","prefix <!-- x > https://example.test/comment -->","prefix <!-- x >\nhttps://example.test/comment\n-->","prefix <iframe title='x >\nhttps://example.test/hidden'></iframe>"})
            {
                string source=markup;
                Check("quoted/comment markup stays inert: "+source,()=>{
                    preview.Markdown=source+" https://example.test/visible";
                    var links=Nodes(preview.TextView.Document).OfType<Hyperlink>().ToArray();
                    Require(links.Length==1&&Equals(links[0].Tag,"https://example.test/visible")&&preview.Markdown==source+" https://example.test/visible","literal boundary or visible tail");
                });
            }
            foreach (int width in new[] { 200, 320, 600 })
            foreach (bool light in new[] { false, true })
            {
                int actualWidth = width; bool actualLight = light;
                Check("URL1/URL1000 selectable column geometry " + width + (light ? " light" : " dark"), () => {
                    string markdown = "- URL1 : 첫 번째 설명은 여러 줄에서 설명 열을 유지합니다.\n- URL2: 두 번째 설명\n- URL1000   : 세 번째 설명";
                    Layout(preview, markdown, actualWidth, actualLight, "url-" + actualWidth + (actualLight ? "-light" : "-dark"));
                    var rows = Nodes(preview.TextView.Document).OfType<TableRow>().ToArray();
                    Require(rows.Length == 3, "description rows missing");
                    var x = rows.Select(r => r.Cells[2].ContentStart.GetCharacterRect(LogicalDirection.Forward).X).ToArray();
                    Require(x.All(v => !Double.IsNaN(v) && v > 30 && v < actualWidth - 20) && x.Max() - x.Min() < 1, "description columns differ: " + String.Join(",", x));
                    Require(!Nodes(preview.TextView.Document).OfType<InlineUIContainer>().Any(), "label cannot be selected");
                    string copied = new TextRange(preview.TextView.Document.ContentStart, preview.TextView.Document.ContentEnd).Text;
                    Require(copied.Contains("URL1") && copied.Contains("URL1000") && copied.Contains("세 번째 설명") && preview.Markdown == markdown, "copy/source lost");
                });
            }
            Check("screenshot source link labels align with descriptions at 200px", () => {
                Layout(preview, "- [MainWindow.xaml](C:/src/MainWindow.xaml), [MainWindow.xaml.cs](C:/src/MainWindow.xaml.cs:21): 뷰어 창을 여는 버튼과 연결 동작\n- [test.csproj](C:/src/test.csproj): 새 화면을 프로젝트에 등록", 200, false, "source-200");
                var bodies = Nodes(preview.TextView.Document).OfType<Paragraph>().Where(p => new TextRange(p.ContentStart,p.ContentEnd).Text.StartsWith(":" ,StringComparison.Ordinal)).ToArray();
                var x = bodies.Select(p => p.ContentStart.GetCharacterRect(LogicalDirection.Forward).X).ToArray();
                Require(bodies.Length == 2 && x.Max() - x.Min() < 1 && x[0] < 100, "source compact descriptions: " + String.Join(",",x));
                Require(Nodes(preview.TextView.Document).OfType<Hyperlink>().Count() == 3, "source links lost");
            });
            Check("source description groups retain alignment after resize to 600px", () => {
                string source=preview.Markdown;Layout(preview,source,600,false,"source-600");
                var bodies=Nodes(preview.TextView.Document).OfType<Paragraph>().Where(p=>new TextRange(p.ContentStart,p.ContentEnd).Text.StartsWith(":",StringComparison.Ordinal)).ToArray();
                Require(bodies.Length==2&&preview.Markdown==source,"source groups not retained");
                var x=bodies.Select(p=>p.ContentStart.GetCharacterRect(LogicalDirection.Forward).X).ToArray();
                Require(x.Max()-x.Min()<1&&x[0]<100,"wide source descriptions differ");
            });
            Check("ordinary colon and nested list semantics are retained", () => {
                preview.Markdown = "- Status: 설명\n  - URL1: child\n- https://example.test/raw\n\n3. URL2: ordered";
                Require(Nodes(preview.TextView.Document).OfType<System.Windows.Documents.List>().Count() == 3, "nested list changed");
                Require(Nodes(preview.TextView.Document).OfType<Table>().Count() == 2, "ordinary text/URL incorrectly split");
                Require(Nodes(preview.TextView.Document).OfType<System.Windows.Documents.List>().Any(x => x.StartIndex == 3), "ordered start lost");
            });
            Check("large and malformed labels retain bounded parsing and original source", () => {
                string source = String.Join("\n", Enumerable.Range(1, 1000).Select(x => "- URL" + x + ": 설명"));
                var watch = Stopwatch.StartNew(); preview.Markdown = source;
                string plain = new TextRange(preview.TextView.Document.ContentStart, preview.TextView.Document.ContentEnd).Text;
                Require(watch.ElapsedMilliseconds < 3000 && Nodes(preview.TextView.Document).Count() < 6100 && preview.Markdown == source && plain.Contains("생략"), "large list budget/source");
                source = "- [" + new string('(', 40000) + ": kept"; watch.Restart(); preview.Markdown = source;
                Require(watch.ElapsedMilliseconds < 3000 && preview.Markdown == source, "malformed label cost/source");
            });
        }
        File.WriteAllText(Path.Combine(output, "result.json"), new JavaScriptSerializer().Serialize(new { Succeeded = failed == 0, Passed = results.Count - failed, Failed = failed, ActualIdeTested = false, WindowShown = false, ProductAssembly = typeof(MarkdownPreview).Assembly.Location, Checks = results }), new UTF8Encoding(false));
        return failed == 0 ? 0 : 1;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Layout
    /// desc         : 창을 열지 않고 지정한 폭의 실제 WPF 텍스트 좌표와 PNG를 생성한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : 물리 모니터 DPI나 마우스 검증으로 해석하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Layout(MarkdownPreview preview, string markdown, int width, bool light, string name)
    {
        preview.Foreground = light ? Brushes.Black : Brushes.Gainsboro;
        preview.Background = light ? Brushes.White : Brushes.Black;
        preview.Width = width; preview.Height = 700; preview.Markdown = markdown;
        preview.Measure(new Size(width, 700)); preview.Arrange(new Rect(0, 0, width, 700)); preview.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, 700, 96, 96, PixelFormats.Pbgra32); bitmap.Render(preview);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, name + ".png"))) encoder.Save(stream);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 실패 원문과 독립 검사 이름을 결과에 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : 실패 후에도 다른 경계 검사를 계속한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, Action action)
    {
        try { action(); results.Add(new { Name = name, Passed = true }); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; results.Add(new { Name = name, Passed = false, Error = error.ToString() }); Console.WriteLine("FAIL " + name + ": " + error); }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Require
    /// desc         : 독립 기대 조건이 다르면 구체적 원인과 함께 실패한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실패를 성공으로 바꾸거나 원본 결과를 덮어쓰지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Nodes
    /// desc         : 실제 문서의 블록·목록·표·링크를 빠짐없이 열거한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK024의 동일한 native 문서 열거 방식 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<TextElement> Nodes(TextElement value)
    {
        yield return value;
        var section = value as Section; if (section != null) foreach (Block b in section.Blocks) foreach (var n in Nodes(b)) yield return n;
        var paragraph = value as Paragraph; if (paragraph != null) foreach (Inline i in paragraph.Inlines) foreach (var n in Nodes(i)) yield return n;
        var span = value as Span; if (span != null) foreach (Inline i in span.Inlines) foreach (var n in Nodes(i)) yield return n;
        var list = value as System.Windows.Documents.List; if (list != null) foreach (ListItem i in list.ListItems) foreach (var n in Nodes(i)) yield return n;
        var item = value as ListItem; if (item != null) foreach (Block b in item.Blocks) foreach (var n in Nodes(b)) yield return n;
        var table = value as Table; if (table != null) foreach (TableRowGroup g in table.RowGroups) foreach (var n in Nodes(g)) yield return n;
        var group = value as TableRowGroup; if (group != null) foreach (TableRow r in group.Rows) foreach (var n in Nodes(r)) yield return n;
        var row = value as TableRow; if (row != null) foreach (TableCell c in row.Cells) foreach (var n in Nodes(c)) yield return n;
        var cell = value as TableCell; if (cell != null) foreach (Block b in cell.Blocks) foreach (var n in Nodes(b)) yield return n;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Nodes
    /// desc         : 실제 FlowDocument의 최상위 블록부터 문서 노드를 열거한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 별도 UI 요소로 라벨을 바꿔 선택·복사를 잃는 회귀를 검사한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<TextElement> Nodes(FlowDocument document) { return document.Blocks.Cast<Block>().SelectMany(Nodes); }
}
