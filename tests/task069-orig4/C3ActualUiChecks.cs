using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

internal static class C3ActualUiChecks
{
    private static readonly string Dll = Environment.GetEnvironmentVariable("TASK069_C3_DLL");
    private static readonly string Output = Environment.GetEnvironmentVariable("TASK069_C3_OUTPUT");
    private static readonly string VsAssemblies = Environment.GetEnvironmentVariable("TASK069_VS_ASSEMBLIES");
    private static Assembly product;
    private static int failures;
    private static readonly List<object> results = new List<object>();

    [STAThread]
    private static int Main()
    {
        Directory.CreateDirectory(Output);
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        product = Assembly.LoadFrom(Dll);
        Type presenterType = product.GetType("VS2017.AIAssistant.Views.ProgressNoticePresenter", true);
        Type noticeType = product.GetType("VS2017.AIAssistant.Views.ProgressNotice", true);
        foreach (string mode in new[] { "dark", "light" }) RunCase(presenterType, noticeType, mode);
        File.WriteAllText(Path.Combine(Output, "actual-ui-results.json"), new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(new
        {
            CandidateDll = Dll,
            Modes = new[] { "dark", "light" },
            HwndSourceStyle = "0 (hidden)",
            HwndPosition = "-32000,-32000",
            WindowShown = false,
            NativePointerUsed = false,
            Tests = results,
            Failures = failures
        }));
        Console.WriteLine(File.ReadAllText(Path.Combine(Output, "actual-ui-results.json")));
        return failures == 0 ? 0 : 1;
    }

    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name + ".dll";
        string[] candidates = { Path.Combine(Path.GetDirectoryName(Dll), name), Path.Combine(VsAssemblies, name), Path.Combine(VsAssemblies, "..", "Framework", name) };
        foreach (string path in candidates) if (File.Exists(path)) return Assembly.LoadFrom(Path.GetFullPath(path));
        return null;
    }

    private static void RunCase(Type presenterType, Type noticeType, string mode)
    {
        var row = new Dictionary<string, object> { { "mode", mode } };
        HwndSource source = null;
        object presenter = null;
        try
        {
            presenter = Activator.CreateInstance(presenterType);
            var element = (FrameworkElement)presenter;
            ResourceDictionary hostPalette = AddPalette(element, mode);
            object notice = Activator.CreateInstance(noticeType);
            noticeType.GetMethod("Apply").Invoke(notice, new object[] { "file", "c3-ui", "started", "검토 대상 파일.cs", null });
            presenterType.GetProperty("Notice").SetValue(presenter, notice, null);
            presenterType.GetProperty("Width").SetValue(presenter, 320d, null);
            presenterType.GetProperty("Height").SetValue(presenter, 84d, null);

            var parameters = new HwndSourceParameters("CodexOrig4-C3-offscreen") { WindowStyle = 0, Width = 1, Height = 1, PositionX = -32000, PositionY = -32000 };
            source = new HwndSource(parameters);
            source.RootVisual = (Visual)presenter;
            Drain(element.Dispatcher);
            row["loaded"] = element.IsLoaded;

            var content = ((ContentControl)presenter).Content as Border;
            var header = content == null ? null : content.Child as Button;
            if (header == null) throw new InvalidOperationException("Progress header button not found.");
            Render(element, Path.Combine(Output, "progress-" + mode + "-card.png"), 320, 84);
            var popup = (Popup)presenterType.GetField("popup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);
            // Match the product's PopupPalette host-resource sharing for a light/dark AssistantControl.
            ((FrameworkElement)popup.Child).Resources.MergedDictionaries.Add(hostPalette);
            row["popupInitiallyClosed"] = !popup.IsOpen;

            // Raising MouseEnter proves hover alone does not open the popup; click exercises the candidate DLL's real routed handler.
            header.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent, Source = header });
            Drain(element.Dispatcher);
            row["popupAfterHover"] = popup.IsOpen;
            Check(!popup.IsOpen, mode + " hover leaves details closed");

            header.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, header));
            Drain(element.Dispatcher);
            row["popupAfterClick"] = popup.IsOpen;
            Check(popup.IsOpen, mode + " click opens details");
            var popupBody = popup.Child as FrameworkElement;
            if (popupBody != null)
            {
                Render(popupBody, Path.Combine(Output, "progress-" + mode + "-details.png"), 320, 250);
                row["detailsRendered"] = popupBody.ActualWidth > 0 && popupBody.ActualHeight > 0;
            }
            header.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, header));
            Drain(element.Dispatcher);
            row["popupAfterSecondClick"] = popup.IsOpen;
            Check(!popup.IsOpen, mode + " second click closes pinned details");
            row["headerAutomationName"] = System.Windows.Automation.AutomationProperties.GetName(header);
            row["headerAutomationHelp"] = System.Windows.Automation.AutomationProperties.GetHelpText(header);
            results.Add(row);
        }
        catch (Exception e) { failures++; row["error"] = e.ToString(); results.Add(row); }
        finally
        {
            if (presenter != null) { try { presenterType.GetMethod("CloseDetails").Invoke(presenter, new object[] { false }); } catch { } }
            if (source != null) { source.RootVisual = null; source.Dispose(); }
        }
    }

    private static ResourceDictionary AddPalette(FrameworkElement element, string mode)
    {
        Color bg = mode == "dark" ? Color.FromRgb(23, 23, 23) : Color.FromRgb(250, 250, 250);
        Color fg = mode == "dark" ? Color.FromRgb(235, 235, 235) : Color.FromRgb(32, 32, 32);
        Color surface = mode == "dark" ? Color.FromRgb(36, 36, 36) : Colors.White;
        Color border = mode == "dark" ? Color.FromRgb(66, 66, 66) : Color.FromRgb(210, 210, 210);
        Color muted = mode == "dark" ? Color.FromRgb(182, 182, 182) : Color.FromRgb(92, 92, 92);
        Color accent = mode == "dark" ? Color.FromRgb(121, 197, 245) : Color.FromRgb(0, 94, 184);
        var host = new ResourceDictionary();
        host["AssistantForegroundBrush"] = new SolidColorBrush(fg);
        host["AssistantMutedForegroundBrush"] = new SolidColorBrush(muted);
        host["AssistantLinkBrush"] = new SolidColorBrush(accent);
        host["AssistantAddedBrush"] = new SolidColorBrush(Color.FromRgb(38, 140, 84));
        host["AssistantRemovedBrush"] = new SolidColorBrush(Color.FromRgb(190, 55, 55));
        host["AssistantSurfaceBrush"] = new SolidColorBrush(surface);
        host["AssistantBorderBrush"] = new SolidColorBrush(border);
        host["AssistantPopupBackgroundBrush"] = new SolidColorBrush(bg);
        host["AssistantPopupBorderBrush"] = new SolidColorBrush(border);
        element.Resources.MergedDictionaries.Add(host);
        return host;
    }

    private static void Render(FrameworkElement element, string path, double width, double height)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }

    private static void Drain(System.Windows.Threading.Dispatcher dispatcher)
    { dispatcher.Invoke(new Action(delegate { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle); }

    private static void Check(bool ok, string label)
    { Console.WriteLine((ok ? "PASS " : "FAIL ") + label); if (!ok) failures++; }
}
