using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Views;

/// <summary>
/// Renders the exact C3 product message-center dialog to bitmaps without showing a window.
/// All records are local capture fixtures; no CLI, account, network, or OS input is used.
/// </summary>
internal static class CaptureMessageCenterC3
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string output;

    [STAThread]
    private static int Main(string[] args)
    {
        output = Path.GetFullPath(args[2]);
        var dependencyMap = new System.Web.Script.Serialization.JavaScriptSerializer()
            .Deserialize<Dictionary<string, string>>(File.ReadAllText(args[1]));
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            string name = new AssemblyName(eventArgs.Name).Name;
            string sibling = Path.Combine(Path.GetDirectoryName(args[0]), name + ".dll");
            if (File.Exists(sibling)) return Assembly.LoadFrom(sibling);
            return dependencyMap.ContainsKey(name) && File.Exists(dependencyMap[name]) ? Assembly.LoadFrom(dependencyMap[name]) : null;
        };
        Assembly.LoadFrom(args[0]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", Path.Combine(output, "settings.json"));
        Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", Path.Combine(output, "workspace"));
        Environment.SetEnvironmentVariable("VS2017_AI_DISABLE_CLI_DISCOVERY", "1");
        Environment.SetEnvironmentVariable("VS2017_AI_DISABLE_RUNTIME_DISCOVERY", "1");
        Directory.CreateDirectory(Path.Combine(output, "workspace"));
        File.WriteAllText(Path.Combine(output, "settings.json"), "{\"HideWelcome\":true,\"ShowHistory\":false,\"EnableCompletionNotifications\":false}");
        var control = new AssistantControl();
        Set(control, "scmLookupStarted", true);
        control.Initialize(new CaptureDocuments(output), new RuntimeEditProvider());
        Set(control, "agentFullAccess", true);

        var owner = Current(control);
        Invoke(control, "CreateConversation");
        var peer = Current(control);
        var store = (ConversationCollaborationStore)Field(control, "collaborationStore");
        var pending = store.Send(peer.Id, peer.Provider, owner.Id, owner.Provider,
            "C3 캡처용 메시지입니다. 수신 AI가 확인하고 명시적으로 답변하는 흐름을 보여줍니다.");
        var replied = store.Send(owner.Id, owner.Provider, peer.Id, peer.Provider,
            "C3 캡처용 회신 예시입니다. 실제 AI 호출 없이 상태 표시만 확인합니다.");
        var view = Invoke(control, "CreateMessageCenter", owner,
            "이 메시지는 전송 전에 실제 수신자와 본문을 확인하는 예시입니다.", null);
        var dialog = (AssistantDialog)Field(view, "Dialog");
        var recipient = (ComboBox)Field(view, "Recipient");
        recipient.SelectedIndex = 0;
        var draft = (TextBox)Field(view, "Draft");
        draft.Text = "다음 단계와 검토할 항목을 간단히 정리해 주세요.";
        Render(dialog, "message-center-draft.png");

        var inbox = (ListBox)Field(view, "Inbox");
        inbox.SelectedIndex = 0;
        Pump();
        Render(dialog, "message-center-inbox.png", true);

        var outbox = (ListBox)Field(view, "Outbox");
        outbox.SelectedIndex = 0;
        Pump();
        Render(dialog, "message-center-outbox.png", true);

        var manifest = "{\"Succeeded\":true,\"ProductAssemblySha256\":\"" + args[3] +
            "\",\"PendingDemoId\":\"" + pending.Id + "\",\"RepliedDemoId\":\"" + replied.Id +
            "\",\"WindowShown\":false,\"PopupOpened\":false,\"OsInputInjected\":false,\"NetworkRequested\":false,\"ActualProductDialog\":true,\"RenderTargetBitmap\":true,\"Images\":[\"message-center-draft.png\",\"message-center-inbox.png\",\"message-center-outbox.png\"]}";
        File.WriteAllText(Path.Combine(output, "capture-result.json"), manifest);
        control.Dispose();
        Pump();
        return 0;
    }

    private static void Render(AssistantDialog dialog, string name, bool scrollToDetails = false)
    {
        var visual = dialog.Content as FrameworkElement;
        if (visual == null) throw new InvalidOperationException("AssistantDialog content is not a FrameworkElement.");
        const double width = 780, height = 760;
        visual.Measure(new Size(width, height));
        visual.Arrange(new Rect(0, 0, width, height));
        visual.UpdateLayout();
        if (scrollToDetails)
        {
            var body = dialog.Body.Content as ScrollViewer;
            if (body == null) throw new InvalidOperationException("Actual message-center body lost its ScrollViewer.");
            body.ScrollToEnd();
            visual.UpdateLayout();
        }
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, name))) encoder.Save(stream);
    }

    private static ConversationRecord Current(AssistantControl control)
    {
        return ((Dictionary<string, ConversationRecord>)Field(control, "activeRecords"))["Claude"];
    }
    private static object Field(object target, string name) { return target.GetType().GetField(name, Private | BindingFlags.Public).GetValue(target); }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Private | BindingFlags.Public).SetValue(target, value); }
    private static object Invoke(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }

    private sealed class CaptureDocuments : IActiveDocumentService, ISourceNavigationService, IFullAccessDocumentService
    {
        private readonly string root;
        internal CaptureDocuments(string directory) { root = directory; }
        public IEditorDocument GetActiveDocument() { throw new InvalidOperationException("Capture must not read editor source."); }
        public DocumentSnapshot CaptureFullAccessContext() { return null; }
        public void NavigateTo(string path, int line) { throw new InvalidOperationException("Capture does not navigate."); }
        public string GetProjectDirectory() { return root; }
    }
}
