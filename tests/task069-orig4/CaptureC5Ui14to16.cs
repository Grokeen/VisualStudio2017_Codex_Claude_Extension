using System;
using System.Collections;
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
using VS2017.AIAssistant.Views;

/// <summary>Offscreen UI acceptance captures for frozen TASK069 C5, with anonymous synthetic conversation data.</summary>
internal static class CaptureC5Ui14to16
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string output;
    private static readonly List<object> checks = new List<object>();
    private static int failures;

    [STAThread]
    private static int Main(string[] args)
    {
        string productPath = Path.GetFullPath(args[0]);
        string dependencyPath = Path.GetFullPath(args[1]);
        output = Path.GetFullPath(args[2]);
        string expectedIdentity = "사용자 · Codex";
        var dependencies = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(dependencyPath));
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
        var control = new AssistantControl();
        SetPrivateText(control, "CurrentAgentLabel", expectedIdentity);
        SetPrivateText(control, "AgentButton", expectedIdentity);
        SeedConversation(control);
        var source = new
        {
            ProductCandidate = "TASK069 C5 frozen VS2017 Release exact DLL",
            DisplayIdentity = expectedIdentity,
            SyntheticConversation = true,
            ProductSendOrAi = false,
            WindowShown = false,
            PopupOpened = false,
            OsInputUsed = false,
            NetworkUsed = false
        };
        try
        {
            VerifySendStopStates(control);
            var themeCases = new[]
            {
                new ThemeCase("vs-dark", "dark", "vs"),
                new ThemeCase("vs-light", "light", "vs"),
                new ThemeCase("kakao-dark", "dark", "kakao"),
                new ThemeCase("kakao-light", "light", "kakao"),
                new ThemeCase("dracula-dark", "dark", "dracula")
            };
            foreach (var theme in themeCases)
            {
                SetTheme(control, theme.Mode, theme.Preset);
                RecordPalette(control, theme);
                SetState(control, false, null, false);
                Capture(control, theme.Name + "-wide.png", 620, 760);
                Capture(control, theme.Name + "-narrow.png", 390, 760);
            }
            SetTheme(control, "dark", "vs");
            SetState(control, false, null, false);
            Capture(control, "vs-dark-state-idle-generate-only.png", 620, 760);
            SetState(control, true, null, false);
            Capture(control, "vs-dark-state-running-stop-only.png", 620, 760);
            SetState(control, false, null, true);
            Capture(control, "vs-dark-state-other-room-running-generate-only.png", 620, 760);
            SetState(control, false, "current-wait", false);
            Capture(control, "vs-dark-state-current-room-wait-stop-only.png", 620, 760);
            SetState(control, false, null, false);
            var scroll = (ScrollViewer)GetField(control, "ConversationScroll");
            var header = (FrameworkElement)GetField(control, "ConversationHeader");
            control.Measure(new Size(390, 760)); control.Arrange(new Rect(0, 0, 390, 760)); control.UpdateLayout();
            scroll.ScrollToVerticalOffset(0); control.UpdateLayout();
            Point before = header.TranslatePoint(new Point(0, 0), control);
            scroll.ScrollToVerticalOffset(scroll.ScrollableHeight); control.UpdateLayout();
            Point after = header.TranslatePoint(new Point(0, 0), control);
            bool moved = Math.Abs(scroll.VerticalOffset) > 1;
            bool fixedHeader = Math.Abs(before.Y - after.Y) < .5;
            Check("message viewport scrolls while #16 header remains fixed", moved && fixedHeader, new { ScrollableHeight = scroll.ScrollableHeight, Offset = scroll.VerticalOffset, HeaderYBefore = before.Y, HeaderYAfter = after.Y });
            Capture(control, "vs-dark-narrow-header-at-bottom.png", 390, 760);
            RecordWatermarkVisibility(control);
            var result = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(new
            {
                Status = failures == 0 ? "PASS" : "FAIL",
                Source = source,
                Captures = new[] { "vs-dark-wide.png", "vs-dark-narrow.png", "vs-light-wide.png", "vs-light-narrow.png", "kakao-dark-wide.png", "kakao-dark-narrow.png", "kakao-light-wide.png", "kakao-light-narrow.png", "dracula-dark-wide.png", "dracula-dark-narrow.png", "vs-dark-state-idle-generate-only.png", "vs-dark-state-running-stop-only.png", "vs-dark-state-other-room-running-generate-only.png", "vs-dark-state-current-room-wait-stop-only.png", "vs-dark-narrow-header-at-bottom.png" },
                Checks = checks,
                Failures = failures
            });
            File.WriteAllText(Path.Combine(output, "capture-result.json"), result, new UTF8Encoding(false));
            Console.WriteLine(result);
        }
        finally { control.Dispose(); }
        return failures == 0 ? 0 : 1;
    }

    private static void SeedConversation(AssistantControl control)
    {
        object chat = control.GetType().GetProperty("ActiveChat", Private).GetValue(control, null);
        MethodInfo add = chat.GetType().GetMethod("Add", new[] { typeof(string), typeof(string) });
        for (int i = 0; i < 10; i++)
        {
            add.Invoke(chat, new object[] { "user", "요청 예시 " + (i + 1) + ": 변경 전후를 비교하고 화면의 접근성과 대화 내용을 확인해 주세요." });
            add.Invoke(chat, new object[] { "assistant", "답변 예시 " + (i + 1) + ": 색상과 레이아웃을 함께 검토했습니다.\n\n- 같은 surface 색상\n- 읽기 대비 유지\n- 대화 위치 고정" });
        }
        var prompt = (TextBox)GetField(control, "Instruction");
        prompt.Text = "첨부 없이 작성한 합성 요청입니다. 실제 전송하지 않습니다.";
        SetPrivateText(control, "CurrentAgentLabel", "사용자 · Codex");
        var start = GetField(control, "ConversationStartedLabel") as TextBlock;
        if (start != null) start.ToolTip = "대화 시작 · 합성 캡처 fixture";
    }

    private static void VerifySendStopStates(AssistantControl control)
    {
        var generate = (Button)GetField(control, "GenerateButton");
        var stop = (Button)GetField(control, "StopButton");
        SetState(control, false, null, false);
        Check("idle selected conversation shows only Generate", generate.Visibility == Visibility.Visible && stop.Visibility == Visibility.Collapsed, new { Generate = generate.Visibility.ToString(), Stop = stop.Visibility.ToString() });
        SetState(control, false, null, true);
        Check("other conversation running keeps idle selection on Generate", generate.Visibility == Visibility.Visible && stop.Visibility == Visibility.Collapsed, new { Generate = generate.Visibility.ToString(), Stop = stop.Visibility.ToString() });
        SetState(control, true, null, false);
        Check("selected legacy FIFO run shows only Stop", generate.Visibility == Visibility.Collapsed && stop.Visibility == Visibility.Visible, new { Generate = generate.Visibility.ToString(), Stop = stop.Visibility.ToString() });
        SetState(control, false, "current-run", false);
        Check("selected room run shows only Stop", generate.Visibility == Visibility.Collapsed && stop.Visibility == Visibility.Visible, new { Generate = generate.Visibility.ToString(), Stop = stop.Visibility.ToString() });
        SetState(control, false, "current-wait", false);
        Check("selected room wait queue shows only Stop", generate.Visibility == Visibility.Collapsed && stop.Visibility == Visibility.Visible, new { Generate = generate.Visibility.ToString(), Stop = stop.Visibility.ToString() });
        SetState(control, false, "global-wait", false);
        Check("legacy FIFO wait queue shows only Stop", generate.Visibility == Visibility.Collapsed && stop.Visibility == Visibility.Visible, new { Generate = generate.Visibility.ToString(), Stop = stop.Visibility.ToString() });
        SetState(control, false, null, false);
    }

    private static void SetState(AssistantControl control, bool legacyBusy, string roomState, bool otherRoomRunning)
    {
        FieldInfo busy = control.GetType().GetField("busy", Private);
        busy.SetValue(control, legacyBusy);
        IDictionary roomRuns = (IDictionary)GetField(control, "roomRuns");
        IDictionary roomQueues = (IDictionary)GetField(control, "roomQueues");
        roomRuns.Clear(); roomQueues.Clear();
        object legacyQueue = GetField(control, "queuedRequests"); legacyQueue.GetType().GetMethod("Clear").Invoke(legacyQueue, null);
        if (otherRoomRunning) AddRoomRun(roomRuns, "synthetic-other-room");
        object record = null;
        if (roomState == "current-run" || roomState == "current-wait")
        {
            IDictionary records = (IDictionary)GetField(control, "activeRecords");
            string provider = Convert.ToString(control.GetType().GetField("selectedProvider", Private).GetValue(control));
            record = records[provider];
            string id = Convert.ToString(record.GetType().GetProperty("Id").GetValue(record, null));
            if (roomState == "current-run") AddRoomRun(roomRuns, id);
            else AddQueue(roomQueues, id);
        }
        if (roomState == "global-wait") legacyQueue.GetType().GetMethod("Enqueue").Invoke(legacyQueue, new object[] { null });
        control.GetType().GetMethod("UpdateButtons", Private).Invoke(control, null);
        control.UpdateLayout();
    }

    private static void AddRoomRun(IDictionary roomRuns, string id)
    {
        Type runType = roomRuns.GetType().GetGenericArguments()[1];
        roomRuns.Add(id, Activator.CreateInstance(runType, true));
    }

    private static void AddQueue(IDictionary queues, string id)
    {
        Type queueType = queues.GetType().GetGenericArguments()[1];
        object queue = Activator.CreateInstance(queueType);
        queue.GetType().GetMethod("Enqueue").Invoke(queue, new object[] { null });
        queues.Add(id, queue);
    }

    private static void SetTheme(AssistantControl control, string mode, string preset)
    {
        SelectTag((ComboBox)GetField(control, "ThemeModeChoice"), mode);
        SelectTag((ComboBox)GetField(control, "VisualThemePresetChoice"), preset);
        control.UpdateLayout();
    }

    private static void SelectTag(ComboBox combo, string tag)
    {
        var item = combo.Items.Cast<ComboBoxItem>().First(value => String.Equals(Convert.ToString(value.Tag), tag, StringComparison.OrdinalIgnoreCase));
        combo.SelectedItem = item;
    }

    private static void RecordPalette(AssistantControl control, ThemeCase theme)
    {
        var surface = control.FindResource("AssistantSurfaceBrush") as SolidColorBrush;
        var reply = control.FindResource("AssistantReplyBackgroundBrush") as SolidColorBrush;
        var text = control.FindResource("AssistantForegroundBrush") as SolidColorBrush;
        if (surface == null || reply == null || text == null) { Check(theme.Name + " palette resources exist", false, null); return; }
        double contrast = Contrast(text.Color, reply.Color);
        bool darker = Luminance(reply.Color) < Luminance(surface.Color);
        Check(theme.Name + " reply is darker with >=4.5:1 text contrast", darker && contrast >= 4.5, new { Surface = surface.Color.ToString(), Reply = reply.Color.ToString(), Text = text.Color.ToString(), Contrast = contrast });
        RecordMessageSurfaces(control, theme, surface.Color, reply.Color);
    }

    private static void RecordMessageSurfaces(AssistantControl control, ThemeCase theme, Color expectedUser, Color expectedAssistant)
    {
        control.Measure(new Size(620, 760)); control.Arrange(new Rect(0, 0, 620, 760)); control.UpdateLayout();
        var bubbles = new List<Border>();
        FindBubbles((DependencyObject)GetField(control, "ConversationMessages"), bubbles);
        Border user = null, assistant = null;
        foreach (var bubble in bubbles)
        {
            object data = bubble.DataContext;
            var roleProperty = data == null ? null : data.GetType().GetProperty("Role");
            string role = roleProperty == null ? "" : Convert.ToString(roleProperty.GetValue(data, null));
            if (role == "user" && user == null) user = bubble;
            if (role == "assistant" && assistant == null) assistant = bubble;
        }
        var userBrush = user == null ? null : user.Background as SolidColorBrush;
        var assistantBrush = assistant == null ? null : assistant.Background as SolidColorBrush;
        Check(theme.Name + " live user bubble equals composer surface", userBrush != null && userBrush.Color == expectedUser, new { Count = bubbles.Count, UserBackground = userBrush == null ? "missing" : userBrush.Color.ToString(), Expected = expectedUser.ToString() });
        Check(theme.Name + " live AI bubble uses darker reply surface", assistantBrush != null && assistantBrush.Color == expectedAssistant, new { Count = bubbles.Count, AssistantBackground = assistantBrush == null ? "missing" : assistantBrush.Color.ToString(), Expected = expectedAssistant.ToString() });
    }

    private static void FindBubbles(DependencyObject node, List<Border> result)
    {
        if (node == null) return;
        var element = node as FrameworkElement;
        if (element != null && element.Name == "MessageBubble")
        {
            var border = element as Border;
            if (border != null) result.Add(border);
        }
        int children = VisualTreeHelper.GetChildrenCount(node);
        for (int index = 0; index < children; index++) FindBubbles(VisualTreeHelper.GetChild(node, index), result);
    }

    private static void RecordWatermarkVisibility(AssistantControl control)
    {
        var watermark = (Image)GetField(control, "BrandWatermark");
        bool configured = watermark.Source != null && watermark.Opacity > 0 && watermark.ActualWidth > 0 && watermark.ActualHeight > 0;
        var withWatermark = RenderBitmap(control);
        Visibility oldVisibility = watermark.Visibility;
        watermark.Visibility = Visibility.Collapsed; control.UpdateLayout();
        var withoutWatermark = RenderBitmap(control);
        watermark.Visibility = oldVisibility; control.UpdateLayout();
        int visiblePixels = CountPixelDifferences(withWatermark, withoutWatermark);
        Check("brand watermark remains configured in conversation", configured, new { Source = watermark.Source == null ? "none" : watermark.Source.GetType().Name, Opacity = watermark.Opacity, Width = watermark.ActualWidth, Height = watermark.ActualHeight });
        Check("brand watermark contributes visible pixels over actual message surfaces", visiblePixels > 0, new { WatermarkVisiblePixels = visiblePixels, CapturedWithMessages = true, ZOrder = "Existing image remains under scroll content; this count detects fully opaque occlusion." });
    }

    private static double Contrast(Color a, Color b)
    { double x = Luminance(a), y = Luminance(b); if (x < y) { double t = x; x = y; y = t; } return (x + .05) / (y + .05); }
    private static double Luminance(Color c)
    { return .2126 * Linear(c.R / 255.0) + .7152 * Linear(c.G / 255.0) + .0722 * Linear(c.B / 255.0); }
    private static double Linear(double value)
    { return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }

    private static void Capture(AssistantControl control, string name, int width, int height)
    {
        var bitmap = RenderBitmap(control, width, height);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, name))) encoder.Save(stream);
    }

    private static RenderTargetBitmap RenderBitmap(FrameworkElement element)
    { return RenderBitmap(element, Math.Max(1, (int)element.ActualWidth), Math.Max(1, (int)element.ActualHeight)); }

    private static RenderTargetBitmap RenderBitmap(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element); return bitmap;
    }

    private static int CountPixelDifferences(BitmapSource left, BitmapSource right)
    {
        int stride = left.PixelWidth * 4;
        var a = new byte[stride * left.PixelHeight]; var b = new byte[stride * right.PixelHeight];
        left.CopyPixels(a, stride, 0); right.CopyPixels(b, stride, 0);
        int count = 0;
        for (int i = 0; i < a.Length; i += 4)
            if (Math.Abs(a[i] - b[i]) + Math.Abs(a[i + 1] - b[i + 1]) + Math.Abs(a[i + 2] - b[i + 2]) > 6) count++;
        return count;
    }

    private static object GetField(AssistantControl control, string name)
    { var field = control.GetType().GetField(name, Private); return field == null ? null : field.GetValue(control); }
    private static void SetPrivateText(AssistantControl control, string name, string value)
    {
        object target = GetField(control, name);
        var text = target as TextBlock; if (text != null) { text.Text = value; return; }
        var button = target as Button; if (button != null) button.ToolTip = value;
        var dependencyObject = target as DependencyObject; if (dependencyObject != null) ToolTipService.SetToolTip(dependencyObject, value);
    }
    private static void Check(string name, bool passed, object details)
    {
        if (!passed) failures++;
        checks.Add(new { Name = name, Pass = passed, Details = details });
    }

    private sealed class ThemeCase
    {
        internal readonly string Name, Mode, Preset;
        internal ThemeCase(string name, string mode, string preset) { Name = name; Mode = mode; Preset = preset; }
    }
}
