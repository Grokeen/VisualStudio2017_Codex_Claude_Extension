using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class C1AssistantUiChecks
{
    private static readonly string Repo = Environment.GetEnvironmentVariable("TASK068_ROOT");
    private static readonly string Dll = Environment.GetEnvironmentVariable("TASK068_DLL");
    private static readonly string Output = Environment.GetEnvironmentVariable("TASK068_OUTPUT");
    private static readonly string RunId = Environment.GetEnvironmentVariable("TASK068_RUN_ID");
    private static readonly string VsAssemblies = Environment.GetEnvironmentVariable("TASK068_VS_ASSEMBLIES");
    private static readonly string[] Widths = { "320", "380", "459", "460" };
    private static readonly string LongChoice = "현재 프로젝트 전체에서 한국어로 길게 설명된 선택지를 읽고 범위를 확인한 다음 변경할 파일만 검토해 주세요";
    private static Assembly product;
    private static readonly List<object> results = new List<object>();
    private static int failures;

    [STAThread]
    private static int Main()
    {
        Directory.CreateDirectory(Output);
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        product = Assembly.LoadFrom(Dll);
        Type controlType = product.GetType("VS2017.AIAssistant.Views.AssistantControl", true);
        foreach (string theme in new[] { "dark", "light" })
            foreach (string widthText in Widths)
                RunCase(controlType, theme, Double.Parse(widthText, System.Globalization.CultureInfo.InvariantCulture));
        string json = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(new
        {
            Candidate = "C1 actual VS2017.AIAssistant.dll",
            DllPath = Dll,
            Widths = Widths,
            Themes = new[] { "dark", "light" },
            WindowCreated = false,
            WindowShown = false,
            OperatingSystemInput = false,
            RealMouseOrDesktopCapture = false,
            NetworkOrCli = false,
            ProductSourceModified = false,
            Tests = results,
            Failures = failures
        });
        File.WriteAllText(Path.Combine(Output, "actual-ui-results.json"), json);
        Console.WriteLine(json);
        return failures == 0 ? 0 : 1;
    }

    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name + ".dll";
        string[] candidates = {
            Path.Combine(Path.GetDirectoryName(Dll), name),
            Path.Combine(VsAssemblies, name),
            Path.Combine(VsAssemblies, "..", "Framework", name),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "newtonsoft.json", "13.0.3", "lib", "net45", name)
        };
        foreach (string candidate in candidates)
            if (File.Exists(candidate)) return Assembly.LoadFrom(Path.GetFullPath(candidate));
        return null;
    }

    private static void RunCase(Type controlType, string theme, double width)
    {
        string id = theme + "-" + width.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        string stateRoot = Path.Combine(Output, "f", RunId.Substring(0, 6) + id);
        string workspace = Path.Combine(stateRoot, "workspace");
        Directory.CreateDirectory(workspace);
        Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", Path.Combine(stateRoot, "settings.json"));
        Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", workspace);
        Environment.SetEnvironmentVariable("VS2017_AI_DISABLE_NOTIFICATIONS", "1");
        object control = null;
        try
        {
            control = Activator.CreateInstance(controlType);
            SetTheme(control, theme);
            FrameworkElement root = (FrameworkElement)Field(control, "LayoutRoot");
            Set(control, "Width", width + 20); Set(control, "Height", 760d);
            controlType.GetMethod("Measure", BindingFlags.Instance | BindingFlags.Public).Invoke(control, new object[] { new Size(width + 20, 760) });
            controlType.GetMethod("Arrange", BindingFlags.Instance | BindingFlags.Public).Invoke(control, new object[] { new Rect(0, 0, width + 20, 760) });
            controlType.GetMethod("UpdateLayout", BindingFlags.Instance | BindingFlags.Public).Invoke(control, null);
            double actual = root.ActualWidth;
            Check(Math.Abs(actual - width) < 1.0, id + " layout root actual width");
            ToggleButton historyToggle = (ToggleButton)Field(control, "HistoryToggle");
            if (historyToggle.IsChecked == true)
            {
                historyToggle.IsChecked = false;
                Invoke(control, "HistoryToggled", new object[] { control, new RoutedEventArgs() });
                controlType.GetMethod("UpdateLayout", BindingFlags.Instance | BindingFlags.Public).Invoke(control, null);
            }

            Render(control, Path.Combine(Output, "assistant-" + id + "-initial.png"), width + 20, 760);
            var themeResult = new Dictionary<string, object> { { "theme", theme }, { "width", width }, { "rootActualWidth", actual } };
            var background = BrushColor(control, "AssistantBackgroundBrush");
            var foreground = BrushColor(control, "AssistantForegroundBrush");
            themeResult["background"] = background.ToString(); themeResult["foreground"] = foreground.ToString();
            themeResult["backgroundForegroundContrast"] = Contrast(background, foreground);
            Check(theme == "dark" ? background.R < foreground.R : background.R > foreground.R, id + " theme palette order");
            Check(Contrast(background, foreground) >= 7.0, id + " theme foreground contrast >= 7:1");

            object message = AddChoiceMessage(control, LongChoice);
            object choices = message.GetType().GetProperty("Choices").GetValue(message, null);
            themeResult["parsedChoiceCount"] = ((Array)choices).Length;
            controlType.GetMethod("UpdateLayout", BindingFlags.Instance | BindingFlags.Public).Invoke(control, null);
            List<Button> choiceButtons = Descendants((DependencyObject)control).OfType<Button>().Where(b => b.Tag is string && ((string)b.Tag).Contains("현재 프로젝트 전체에서")).ToList();
            Check(choiceButtons.Count == 1, id + " choice button generated");
            if (choiceButtons.Count == 1)
            {
                Button button = choiceButtons[0];
                themeResult["choiceButtonWidth"] = button.ActualWidth;
                themeResult["choiceButtonHeight"] = button.ActualHeight;
                themeResult["choiceButtonFocusable"] = button.Focusable;
                themeResult["choiceButtonAutomationName"] = AutomationPeer(button);
                themeResult["choiceButtonBackground"] = button.Background == null ? null : button.Background.ToString();
                themeResult["choiceButtonBorder"] = button.BorderBrush == null ? null : button.BorderBrush.ToString();
                AccessText accessText = Descendants(button).OfType<AccessText>().FirstOrDefault();
                if (accessText != null)
                {
                    themeResult["choiceLabelActualWidth"] = accessText.ActualWidth;
                    themeResult["choiceLabelDesiredWidth"] = accessText.DesiredSize.Width;
                    themeResult["choiceLabelActualHeight"] = accessText.ActualHeight;
                    themeResult["choiceLabelDesiredHeight"] = accessText.DesiredSize.Height;
                }
                Check(button.ActualWidth > 0 && button.ActualWidth <= Math.Min(width - 20, 280.5), id + " choice button bounded to narrow viewport");
                Check(button.ActualHeight >= 40, id + " long Korean choice wraps to a readable multi-line target");
                Check(button.Focusable, id + " choice button keyboard focusable");
                Render(control, Path.Combine(Output, "assistant-" + id + "-choices.png"), width + 20, 760);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
                string prompt = ((TextBox)Field(control, "Instruction")).Text;
                themeResult["choiceDraftInserted"] = prompt.Contains(LongChoice);
                Check(prompt.Contains(LongChoice), id + " choice inserts exact Korean label into draft");
                Check(!prompt.Contains("전송됨"), id + " choice selection does not report auto submission");
            }

            ListBox list = (ListBox)Field(control, "ConversationList");
            if (historyToggle.IsChecked == true)
            {
                historyToggle.IsChecked = false;
                Invoke(control, "HistoryToggled", new object[] { control, new RoutedEventArgs() });
            }
            controlType.GetMethod("UpdateLayout", BindingFlags.Instance | BindingFlags.Public).Invoke(control, null);
            Render(control, Path.Combine(Output, "assistant-" + id + "-conversation-closed.png"), width + 20, 760);
            historyToggle.IsChecked = true;
            Invoke(control, "HistoryToggled", new object[] { control, new RoutedEventArgs() });
            controlType.GetMethod("UpdateLayout", BindingFlags.Instance | BindingFlags.Public).Invoke(control, null);
            list.UpdateLayout();
            Render(control, Path.Combine(Output, "assistant-" + id + "-history-open.png"), width + 20, 760);
            int selectedIndex = list.SelectedIndex;
            var item = selectedIndex >= 0 ? list.ItemContainerGenerator.ContainerFromIndex(selectedIndex) as ListBoxItem : null;
            Check(item != null, id + " active conversation list item realized");
            var history = Field(control, "HistoryPanel") as FrameworkElement;
            if (item != null)
            {
                bool focusRequestSucceeded = item.Focus();
                bool focusedBefore = item.IsKeyboardFocusWithin || FocusManager.GetFocusedElement((DependencyObject)control) == item;
                var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent, Source = item };
                Invoke(control, "ConversationDoubleClicked", new object[] { control, args });
                controlType.GetMethod("UpdateLayout", BindingFlags.Instance | BindingFlags.Public).Invoke(control, null);
                bool shouldClose = width < 460;
                bool closed = history.Visibility != Visibility.Visible;
                themeResult["historyClosedByDoubleClick"] = closed;
                themeResult["focusProbeSucceededWithoutWindow"] = focusRequestSucceeded;
                themeResult["historyFocusBefore"] = focusedBefore;
                themeResult["historyFocusAfter"] = Keyboard.FocusedElement == item || FocusManager.GetFocusedElement((DependencyObject)control) == item;
                Check(shouldClose ? closed : !closed, id + " double-click threshold behavior");
                if (closed) Render(control, Path.Combine(Output, "assistant-" + id + "-history-closed.png"), width + 20, 760);
            }
            results.Add(themeResult);
        }
        catch (Exception error)
        {
            failures++;
            results.Add(new { theme, width, error = error.ToString() });
        }
        finally
        {
            var disposable = control as IDisposable;
            if (disposable != null) disposable.Dispose();
        }
    }

    private static void SetTheme(object control, string theme)
    {
        ComboBox choice = (ComboBox)Field(control, "ThemeModeChoice");
        ComboBoxItem item = choice.Items.Cast<ComboBoxItem>().First(i => String.Equals(Convert.ToString(i.Tag), theme, StringComparison.OrdinalIgnoreCase));
        choice.SelectedItem = item;
    }

    private static object AddChoiceMessage(object control, string label)
    {
        PropertyInfo active = control.GetType().GetProperty("ActiveChat", BindingFlags.Instance | BindingFlags.NonPublic);
        object chat = active.GetValue(control, null);
        IList messages = (IList)chat.GetType().GetProperty("Messages").GetValue(chat, null);
        Type messageType = product.GetType("VS2017.AIAssistant.Chat.ChatMessage", true);
        object message = Activator.CreateInstance(messageType);
        messageType.GetProperty("Role").SetValue(message, "assistant", null);
        messageType.GetProperty("Text").SetValue(message, "진행 범위를 선택해 주세요.\n1. " + label + "\n2. 현재 파일만", null);
        messages.Add(message);
        return message;
    }

    private static string AutomationPeer(Button button)
    {
        var peer = new ButtonAutomationPeer(button);
        return peer.GetName();
    }

    private static Color BrushColor(object control, string key)
    {
        var brush = ((FrameworkElement)control).TryFindResource(key) as SolidColorBrush;
        if (brush == null) throw new InvalidOperationException("Missing solid color resource: " + key);
        return brush.Color;
    }

    private static double Contrast(Color one, Color two)
    {
        double a = Luminance(one), b = Luminance(two);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private static double Luminance(Color c)
    {
        Func<byte, double> linear = value => { double n = value / 255.0; return n <= .04045 ? n / 12.92 : Math.Pow((n + .055) / 1.055, 2.4); };
        return .2126 * linear(c.R) + .7152 * linear(c.G) + .0722 * linear(c.B);
    }

    private static void Render(object control, string path, double width, double height)
    {
        var element = (FrameworkElement)control;
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        if (root == null) yield break;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static object Field(object instance, string name)
    { return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance); }

    private static void Set(object instance, string property, object value)
    { instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public).SetValue(instance, value, null); }

    private static void Invoke(object instance, string method, object[] args)
    { instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args); }

    private static void Check(bool value, string name)
    {
        Console.WriteLine((value ? "PASS " : "FAIL ") + name);
        if (!value) failures++;
    }
}
