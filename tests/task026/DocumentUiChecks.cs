using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : DocumentUiChecks
/// desc         : 감사 후 추가된 문서 목록을 실제 제품 객체로 배치하고 양 테마의 색상·키보드 표시·가시 영역을 검사한다.
/// author       : CodexOrig4
/// create date  : 2026-10-19
/// update date  : 2026-10-19 CodexOrig4 : 실제 창·GUI 입력·사용자 파일 변경 없이 별도 STA 프로세스에서 검사.
///                2026-10-19 CodexOrig : HTML 컨테이너 안의 표도 재귀 조회하여 실제 렌더 트리와 색상을 검사.
/// </summary>
/// ---------------------------------------------------------------------
internal static class DocumentUiChecks
{
    private static readonly List<object> checks = new List<object>();
    private static int failed;
    private static void Check(string name, bool passed, string detail)
    {
        checks.Add(new { Name = name, Passed = passed, Detail = detail });
        if (!passed) failed++;
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + " " + detail);
    }
    private static T Field<T>(object value, string name) where T : class
    {
        return (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    }
    private static Color ColorOf(Brush value) { return value is SolidColorBrush ? ((SolidColorBrush)value).Color : Colors.Transparent; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FindTable
    /// desc         : HTML 컨테이너의 Section을 포함한 문서 구조에서 실제 Table을 찾는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : 컨테이너 유무와 무관하게 기존 색상 검사를 유지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static Table FindTable(BlockCollection blocks)
    {
        foreach (Block block in blocks)
        {
            var table = block as Table;
            if (table != null) return table;
            var section = block as Section;
            if (section != null)
            {
                table = FindTable(section.Blocks);
                if (table != null) return table;
            }
        }
        return null;
    }
    private static string HashOf(string path)
    {
        using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    private static ScrollViewer BrowserScroll(DependencyObject element, Expander browser)
    {
        for (DependencyObject parent = VisualTreeHelper.GetParent(element); parent != null && parent != browser; parent = VisualTreeHelper.GetParent(parent))
        {
            var viewer = parent as ScrollViewer;
            if (viewer != null) return viewer;
        }
        return null;
    }
    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }
    private static void Render(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }
    private static void Reachable(string label, FrameworkElement target, ScrollViewer viewer, FrameworkElement host, double width)
    {
        target.BringIntoView();
        Layout(host, width, 600);
        Rect rect = target.TransformToAncestor(viewer).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
        Check(label, rect.Top >= -1 && rect.Bottom <= viewer.ViewportHeight + 1 && rect.Left >= -1 && rect.Right <= viewer.ViewportWidth + 1,
            "Bounds=" + rect + "; Viewport=" + viewer.ViewportWidth + "," + viewer.ViewportHeight);
    }
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        string output = args[0];
        bool sourceOnly = Boolean.Parse(args[1]);
        string themeAssembly = Path.Combine(output, "VS2017.AIAssistant.dll");
        Assembly.LoadFrom(themeAssembly);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var resources = new ResourceDictionary { Source = new Uri("/VS2017.AIAssistant;component/Views/AssistantTheme.xaml", UriKind.Relative) };
        var host = new Border();
        host.Resources.MergedDictionaries.Add(resources);
        host.SetResourceReference(Border.BackgroundProperty, "AssistantBackgroundBrush");
        host.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "AssistantForegroundBrush");
        var editor = new MarkdownEditor();
        host.Child = editor;
        var browser = Field<Expander>(editor, "documentBrowser");
        var list = Field<ListBox>(editor, "documentList");
        var name = Field<TextBox>(editor, "documentName");
        string docs = Path.Combine(output, "documents");
        Directory.CreateDirectory(docs);
        const string document = "<table><tr><th>Title</th><td><code>sample</code> <a href=\"https://example.invalid/docs\">Link</a></td></tr></table>";
        string file = Path.Combine(docs, "example.md");
        File.WriteAllText(file, document);
        editor.SetDocumentDirectory(docs);
        editor.TryLoadFile(file, () => true);
        editor.Preview.SetDocument(document, docs);
        browser.IsExpanded = true;
        list.SelectedIndex = 0;
        int step = 0;
        foreach (bool light in new[] { false, true, false })
        {
            string mode = (++step) + "-" + (light ? "light" : "dark");
            AssistantPalette.Apply(resources, light);
            Layout(host, 900, 600);
            var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
            Check(mode + " document list text follows palette", ColorOf(item.Foreground) == ColorOf((Brush)resources["AssistantForegroundBrush"]), "Actual=" + ColorOf(item.Foreground));
            Check(mode + " document browser follows palette", ColorOf(browser.Foreground) == ColorOf((Brush)resources["AssistantForegroundBrush"]), "Actual=" + ColorOf(browser.Foreground));
            var splitter = Field<GridSplitter>(editor, "splitter");
            Check(mode + " editor splitter follows palette", ColorOf(splitter.Background) == ColorOf((Brush)resources["AssistantBorderBrush"]), "Actual=" + ColorOf(splitter.Background));
            var table = FindTable(editor.Preview.TextView.Document.Blocks);
            Check(mode + " HTML table remains rendered after palette change", table != null, "");
            if (table != null)
            {
                var header = table.RowGroups[0].Rows[0].Cells[0];
                var paragraph = (Paragraph)table.RowGroups[0].Rows[0].Cells[1].Blocks.FirstBlock;
                var code = paragraph.Inlines.OfType<Span>().First(x => !(x is Hyperlink));
                var link = paragraph.Inlines.OfType<Hyperlink>().First();
                Check(mode + " existing HTML header follows code surface", ColorOf(header.Background) == ColorOf((Brush)resources["AssistantCodeBackgroundBrush"]), "Actual=" + ColorOf(header.Background));
                Check(mode + " existing HTML code follows code surface", ColorOf(code.Background) == ColorOf((Brush)resources["AssistantCodeBackgroundBrush"]), "Actual=" + ColorOf(code.Background));
                Check(mode + " existing HTML link follows link color", ColorOf(link.Foreground) == ColorOf((Brush)resources["AssistantLinkBrush"]), "Actual=" + ColorOf(link.Foreground));
            }
            Render(host, Path.Combine(output, mode + "-wide.png"));
            foreach (double width in new[] { 320.0, 480.0, 900.0 })
            {
                var actions = (FrameworkElement)name.Parent;
                var buttons = ((Panel)actions).Children.OfType<WrapPanel>().First();
                var confirmation = Field<WrapPanel>(editor, "deleteConfirmation");
                Layout(host, width, 600);
                var scroller = BrowserScroll(list, browser);
                if (scroller != null) { scroller.ScrollToTop(); Layout(host, width, 600); }
                string label = mode + " " + width + "px ";
                Rect listRect = list.TransformToAncestor(browser).TransformBounds(new Rect(0, 0, list.ActualWidth, list.ActualHeight));
                Rect nameRect = name.TransformToAncestor(browser).TransformBounds(new Rect(0, 0, name.ActualWidth, name.ActualHeight));
                Check(label + "file name does not overlap documents", listRect.Bottom <= nameRect.Top + 1, "ListBottom=" + listRect.Bottom + "; NameTop=" + nameRect.Top);
                Check(label + "browser has a content scroll viewer", scroller != null, "");
                if (scroller != null)
                {
                    if (width < 620)
                    {
                        var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
                        item.RaiseEvent(wheel);
                        Layout(host, width, 600);
                        Check(label + "wheel over file list scrolls browser", wheel.Handled && scroller.VerticalOffset > 0, "Offset=" + scroller.VerticalOffset);
                        scroller.ScrollToTop(); Layout(host, width, 600);
                    }
                    Reachable(label + "file name reachable", name, scroller, host, width);
                    foreach (var button in buttons.Children.OfType<Button>()) Reachable(label + button.Content + " reachable", button, scroller, host, width);
                    buttons.Children.OfType<Button>().First(x => (string)x.Content == "삭제").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Layout(host, width, 600);
                    Check(label + "delete requires visible confirmation", confirmation.Visibility == Visibility.Visible && File.Exists(file), "");
                    foreach (var button in confirmation.Children.OfType<Button>()) Reachable(label + button.Content + " reachable", button, scroller, host, width);
                    Render(host, Path.Combine(output, mode + "-" + width + "-confirmation.png"));
                    confirmation.Children.OfType<Button>().First(x => (string)x.Content == "취소").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(label + "cancel preserves document", confirmation.Visibility == Visibility.Collapsed && File.ReadAllText(file) == document && editor.CurrentPath == file, "");
                }
                Render(host, Path.Combine(output, mode + "-" + width + ".png"));
            }
        }
        editor.Dispose();
        string assembly = typeof(MarkdownEditor).Assembly.Location;
        string hash = HashOf(assembly);
        File.WriteAllText(Path.Combine(output, "result.json"), new JavaScriptSerializer().Serialize(new {
            Succeeded = failed == 0, Passed = checks.Count - failed, Failed = failed, Checks = checks,
            ProductAssemblySha256 = sourceOnly ? null : hash, UiAssemblySha256 = hash,
            ThemeAssemblySha256 = HashOf(themeAssembly), SourceOnly = sourceOnly, WindowShown = false, ActualIdeTested = false
        }));
        app.Shutdown();
        return failed == 0 ? 0 : 1;
    }
}
