using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VS2017.AIAssistant.Views;

/// <summary>Anonymous offscreen acceptance of C10 conversation gutters using synthetic chat content.</summary>
internal static class CaptureC10ConversationGutter
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<object> checks = new List<object>();
    private static readonly List<string> captures = new List<string>();
    private static int failures;
    private static string output;
    private static AssistantControl control;

    [STAThread]
    private static int Main(string[] args)
    {
        string productPath = Path.GetFullPath(args[0]);
        var dependencies = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(args[1]));
        output = Path.GetFullPath(args[2]);
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            string name = new AssemblyName(eventArgs.Name).Name;
            string sibling = Path.Combine(Path.GetDirectoryName(productPath), name + ".dll");
            if (File.Exists(sibling)) return Assembly.LoadFrom(sibling);
            string mapped;
            return dependencies.TryGetValue(name, out mapped) && File.Exists(mapped) ? Assembly.LoadFrom(mapped) : null;
        };
        Assembly.LoadFrom(productPath);
        Directory.CreateDirectory(output);
        control = new AssistantControl();
        try { RunCapture(); }
        finally { if (control != null) control.Dispose(); }
        var result = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(new
        {
            Status = failures == 0 ? "PASS" : "FAIL",
            ProductCandidate = "TASK069 C10 exact VS2017 Release DLL",
            Identity = "사용자 · Codex",
            Fixture = "synthetic user and assistant messages with a long fenced code line; no real project or debugger",
            WindowShown = false, PopupOpened = false, OsInputUsed = false, NetworkUsed = false, SendOrAi = false,
            Checks = checks, Failures = failures, Captures = captures
        });
        File.WriteAllText(Path.Combine(output, "capture-result.json"), result, new UTF8Encoding(false));
        Console.WriteLine(result);
        return failures == 0 ? 0 : 1;
    }

    private static void RunCapture()
    {
        SetText("CurrentAgentLabel", "사용자 · Codex");
        object chat = control.GetType().GetProperty("ActiveChat", Private).GetValue(control, null);
        chat.GetType().GetMethod("Add", new[] { typeof(string), typeof(string) }).Invoke(chat, new object[] { "user", "대화 본문 여백 합성 요청입니다." });
        string longCode = "string longLine = \"" + new string('x', 180) + "\";";
        var body = new StringBuilder("합성 응답입니다. 여백 바깥으로 본문이 나가지 않는지 확인합니다.\n\n```csharp\n");
        body.Append(longCode).Append("\nConsole.WriteLine(longLine);\n```\n\n");
        for (int line = 1; line <= 18; line++) body.Append("추가 합성 문장 ").Append(line).Append(" — 스크롤과 줄바꿈 확인용입니다.\n");
        chat.GetType().GetMethod("Add", new[] { typeof(string), typeof(string) }).Invoke(chat, new object[] { "assistant", body.ToString() });
        control.UpdateLayout();

        var surface = (FrameworkElement)GetField("ConversationSurface");
        var header = (FrameworkElement)GetField("ConversationHeader");
        var workspace = (FrameworkElement)GetField("WorkspaceGrid");
        var composer = (FrameworkElement)GetField("ComposerSurface");
        var scroll = (ScrollViewer)GetField("ConversationScroll");
        var messages = (ItemsControl)GetField("ConversationMessages");
        var contentGrid = scroll.Content as Grid;
        Check("C10 conversation body has responsive grid gutters", contentGrid != null && contentGrid.ColumnDefinitions.Count == 3, contentGrid == null ? null : new { Columns = contentGrid.ColumnDefinitions.Count });
        Check("fixed header stays outside scroll", ReferenceEquals(header.Parent, surface) && ReferenceEquals(scroll.Parent, surface), new { HeaderParent = header.Parent == null ? "missing" : header.Parent.GetType().Name, ScrollParent = scroll.Parent == null ? "missing" : scroll.Parent.GetType().Name });
        Check("synthetic Markdown code message is present", messages.Items.Count == 2, new { MessageCount = messages.Items.Count });

        SetTheme("dark", "vs");
        foreach (double viewport in new[] { 343.0, 520.0, 640.0 })
        {
            LayoutAtViewport(scroll, viewport);
            double left = contentGrid.ColumnDefinitions[0].ActualWidth;
            double center = contentGrid.ColumnDefinitions[1].ActualWidth;
            double right = contentGrid.ColumnDefinitions[2].ActualWidth;
            Check("actual ConversationScroll viewport matches " + viewport + " px", Math.Abs(scroll.ViewportWidth - viewport) < 0.75, new { Requested = viewport, Actual = Math.Round(scroll.ViewportWidth, 2), ControlWidth = Math.Round(control.ActualWidth, 2) });
            Check("actual gutter bounds and symmetry at " + viewport + " px", left >= 12 - 0.1 && left <= 20 + 0.1 && right >= 12 - 0.1 && right <= 20 + 0.1 && Math.Abs(left - right) < 0.1 && center > 0, new { Left = Math.Round(left, 2), Center = Math.Round(center, 2), Right = Math.Round(right, 2) });
            Check("header and composer align to full conversation surface at " + viewport + " px", Math.Abs(header.ActualWidth - surface.ActualWidth) < 1 && Math.Abs(composer.ActualWidth - workspace.ActualWidth) < 1, new { Header = Math.Round(header.ActualWidth, 2), Surface = Math.Round(surface.ActualWidth, 2), Composer = Math.Round(composer.ActualWidth, 2), Workspace = Math.Round(workspace.ActualWidth, 2) });
            Check("outer body remains vertically scrollable without horizontal scrolling at " + viewport + " px", scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled && scroll.ScrollableWidth < 1 && scroll.ScrollableHeight >= 0, new { Horizontal = scroll.HorizontalScrollBarVisibility.ToString(), ScrollableWidth = Math.Round(scroll.ScrollableWidth, 2), ScrollableHeight = Math.Round(scroll.ScrollableHeight, 2) });
            CheckBubbles(messages, viewport);
            Capture("vs-dark-viewport-" + ((int)viewport) + ".png");
        }

        foreach (var theme in new[] { new ThemeCase("vs-light", "light", "vs"), new ThemeCase("kakao-dark", "dark", "kakao"), new ThemeCase("kakao-light", "light", "kakao"), new ThemeCase("dracula-dark", "dark", "dracula") })
        {
            SetTheme(theme.Mode, theme.Preset);
            LayoutAtViewport(scroll, 520);
            double left = contentGrid.ColumnDefinitions[0].ActualWidth, right = contentGrid.ColumnDefinitions[2].ActualWidth;
            Check(theme.Name + " keeps both gutters within range", left >= 12 && left <= 20 && right >= 12 && right <= 20 && Math.Abs(left - right) < 0.1, new { Left = Math.Round(left, 2), Right = Math.Round(right, 2) });
            Capture(theme.Name + "-viewport-520.png");
        }

        SetTheme("dark", "vs"); LayoutAtViewport(scroll, 520);
        var assistantContainer = messages.ItemContainerGenerator.ContainerFromIndex(1) as ContentPresenter;
        Border assistantBubble = assistantContainer == null ? null : assistantContainer.ContentTemplate.FindName("MessageBubble", assistantContainer) as Border;
        RichTextBox codeView = FindVisual<RichTextBox>(assistantContainer);
        ScrollViewer codeHost = codeView == null ? null : FindNamedScrollViewer(codeView);
        Check("message bubble limits and existing role offsets remain intact", assistantBubble != null && assistantBubble.MaxWidth == 760 && assistantBubble.Margin.Left == 0 && assistantBubble.Margin.Right == 24, assistantBubble == null ? null : new { assistantBubble.MaxWidth, assistantBubble.Margin });
        Check("Markdown code view remains present inside the guttered body", codeView != null && codeView.Document != null && codeView.Document.Blocks.Count > 0, new { HasCodeView = codeView != null, Blocks = codeView == null || codeView.Document == null ? 0 : codeView.Document.Blocks.Count });
        Check("long code stays within the body without outer horizontal overflow", scroll.ScrollableWidth < 1 && codeHost != null && codeHost.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled, new { OuterScrollableWidth = Math.Round(scroll.ScrollableWidth, 2), CodeHostHorizontal = codeHost == null ? "missing" : codeHost.HorizontalScrollBarVisibility.ToString(), CodeHostScrollableWidth = codeHost == null ? -1 : Math.Round(codeHost.ScrollableWidth, 2) });
    }

    private static void LayoutAtViewport(ScrollViewer scroll, double target)
    {
        control.Height = 820;
        control.Width = target + 48;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            control.Measure(new Size(control.Width, control.Height));
            control.Arrange(new Rect(0, 0, control.Width, control.Height));
            control.UpdateLayout();
            double current = scroll.ViewportWidth;
            if (Math.Abs(current - target) < 0.5) break;
            control.Width += target - current;
        }
        control.Measure(new Size(control.Width, control.Height));
        control.Arrange(new Rect(0, 0, control.Width, control.Height));
        control.UpdateLayout();
    }

    private static void CheckBubbles(ItemsControl messages, double width)
    {
        var userPresenter = messages.ItemContainerGenerator.ContainerFromIndex(0) as ContentPresenter;
        var assistantPresenter = messages.ItemContainerGenerator.ContainerFromIndex(1) as ContentPresenter;
        Border user = userPresenter == null ? null : userPresenter.ContentTemplate.FindName("MessageBubble", userPresenter) as Border;
        Border assistant = assistantPresenter == null ? null : assistantPresenter.ContentTemplate.FindName("MessageBubble", assistantPresenter) as Border;
        Check("user and assistant bubbles stay inside the body at " + width + " px", user != null && assistant != null && user.ActualWidth <= messages.ActualWidth && assistant.ActualWidth <= messages.ActualWidth, new { Body = Math.Round(messages.ActualWidth, 2), User = user == null ? -1 : Math.Round(user.ActualWidth, 2), Assistant = assistant == null ? -1 : Math.Round(assistant.ActualWidth, 2) });
    }

    private static void Capture(string filename)
    {
        ((ScrollViewer)GetField("ConversationScroll")).ScrollToTop();
        control.UpdateLayout();
        int width = Math.Max(1, (int)Math.Ceiling(control.ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(control.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(control);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, filename))) encoder.Save(stream);
        captures.Add(filename);
    }

    private static T FindVisual<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;
        if (parent is T) return (T)parent;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { T value = FindVisual<T>(VisualTreeHelper.GetChild(parent, i)); if (value != null) return value; }
        return null;
    }

    private static ScrollViewer FindNamedScrollViewer(DependencyObject parent)
    {
        if (parent == null) return null;
        var viewer = parent as ScrollViewer; if (viewer != null && viewer.Name == "PART_ContentHost") return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var value = FindNamedScrollViewer(VisualTreeHelper.GetChild(parent, i)); if (value != null) return value; }
        return null;
    }

    private static void SetTheme(string mode, string preset)
    {
        SelectTag((ComboBox)GetField("ThemeModeChoice"), mode);
        SelectTag((ComboBox)GetField("VisualThemePresetChoice"), preset);
        control.UpdateLayout();
    }
    private static void SelectTag(ComboBox combo, string tag)
    { var item = combo.Items.Cast<ComboBoxItem>().First(value => String.Equals(Convert.ToString(value.Tag), tag, StringComparison.OrdinalIgnoreCase)); combo.SelectedItem = item; }
    private static object GetField(string name)
    { var field = control.GetType().GetField(name, Private); return field == null ? null : field.GetValue(control); }
    private static void SetText(string name, string value)
    { var element = GetField(name) as TextBlock; if (element != null) element.Text = value; }
    private static void Check(string name, bool pass, object details)
    { if (!pass) failures++; checks.Add(new { Name = name, Pass = pass, Details = details }); }

    private sealed class ThemeCase
    { internal readonly string Name, Mode, Preset; internal ThemeCase(string name, string mode, string preset) { Name = name; Mode = mode; Preset = preset; } }
}
