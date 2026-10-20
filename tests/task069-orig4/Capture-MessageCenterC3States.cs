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
/// Captures actual frozen C3 message-center dialogs with explicitly synthetic store transitions.
/// No Window.Show, popup, OS input, CLI/AI process, account, or network request is used.
/// </summary>
internal static class CaptureMessageCenterC3States
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string output;
    private static readonly List<string> capturedStates = new List<string>();

    [STAThread]
    private static int Main(string[] args)
    {
        output = Path.GetFullPath(args[2]);
        var dependencyMap = new System.Web.Script.Serialization.JavaScriptSerializer()
            .Deserialize<Dictionary<string, string>>(File.ReadAllText(args[1]));
        AppDomain.CurrentDomain.AssemblyResolve += (resolveSender, eventArgs) =>
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
        var sender = Current(control);
        Invoke(control, "CreateConversation");
        var recipient = Current(control);
        var store = (ConversationCollaborationStore)Field(control, "collaborationStore");
        var handoff = store.Send(sender.Id, sender.Provider, recipient.Id, recipient.Provider,
            "합성 검증 메시지입니다. 실제 AI 답변이 아닌 저장 상태 흐름만 확인합니다.");

        var draftView = Invoke(control, "CreateMessageCenter", sender, "수신자와 본문을 확인하는 UI 예시입니다.", null);
        var selectedRecipient = (ComboBox)Field(draftView, "Recipient");
        selectedRecipient.SelectedIndex = 0;
        ((TextBox)Field(draftView, "Draft")).Text = "다음 검토 항목을 알려주세요.";
        Render((AssistantDialog)Field(draftView, "Dialog"), "recipient-display-defect.png", false);

        CaptureState(control, sender, "Pending", handoff.Id, "pending-outbox.png");
        store.Receive(handoff.Id, recipient.Id, recipient.Provider);
        store.Reply(handoff.Id, recipient.Id, recipient.Provider,
            "[합성 캡처용 회신] CLI/AI 호출 없이 Store.Reply API에 직접 기록한 테스트 문장입니다.");
        CaptureState(control, sender, "Replied", handoff.Id, "replied-outbox-synthetic.png");
        store.Deliver(handoff.Id, sender.Id, sender.Provider);
        CaptureState(control, sender, "Delivered", handoff.Id, "delivered-outbox-synthetic.png");

        string states = String.Join(",", capturedStates.ConvertAll(value => "\"" + value + "\""));
        string result = "{\"Succeeded\":true,\"ProductAssemblySha256\":\"" + args[3] +
            "\",\"HandoffId\":\"" + handoff.Id + "\",\"SyntheticFixture\":true,\"SyntheticReply\":true,\"TransitionPath\":\"Send->Receive->Reply->Deliver\",\"CapturedStates\":[" + states +
            "],\"WindowShown\":false,\"PopupOpened\":false,\"OsInputInjected\":false,\"CliOrAiStarted\":false,\"NetworkRequested\":false,\"Images\":[\"recipient-display-defect.png\",\"pending-outbox.png\",\"replied-outbox-synthetic.png\",\"delivered-outbox-synthetic.png\"],\"Limitation\":\"Replied is synthetic local fixture text submitted directly through the exact product ConversationCollaborationStore.Reply API; it is not a model response or real delivery.\"}";
        File.WriteAllText(Path.Combine(output, "capture-result.json"), result);
        control.Dispose();
        Pump();
        return capturedStates.Count == 3 ? 0 : 1;
    }

    private static void CaptureState(AssistantControl control, ConversationRecord owner, string expected, string id, string file)
    {
        var view = Invoke(control, "CreateMessageCenter", owner, "", null);
        var outbox = (ListBox)Field(view, "Outbox");
        var match = -1;
        for (int i = 0; i < outbox.Items.Count; i++)
        {
            var row = outbox.Items[i];
            var item = (CollaborationHandoff)row.GetType().GetField("Item", Private | BindingFlags.Public).GetValue(row);
            if (item.Id == id) { match = i; break; }
        }
        if (match < 0) throw new InvalidOperationException("Synthetic handoff missing from sender Outbox for " + expected + ".");
        outbox.SelectedIndex = match;
        Pump();
        var store = (ConversationCollaborationStore)Field(control, "collaborationStore");
        var actual = store.GetHandoff(id).State.ToString();
        if (actual != expected) throw new InvalidOperationException("Expected synthetic state " + expected + ", found " + actual + ".");
        capturedStates.Add(actual);
        Render((AssistantDialog)Field(view, "Dialog"), file, true);
    }

    private static void Render(AssistantDialog dialog, string name, bool scrollToDetails)
    {
        var visual = dialog.Content as FrameworkElement;
        if (visual == null) throw new InvalidOperationException("AssistantDialog content is not a FrameworkElement.");
        const double width = 780, height = 760;
        visual.Measure(new Size(width, height)); visual.Arrange(new Rect(0, 0, width, height)); visual.UpdateLayout();
        if (scrollToDetails)
        {
            var body = dialog.Body.Content as ScrollViewer;
            if (body == null) throw new InvalidOperationException("Actual message-center body lost its ScrollViewer.");
            body.ScrollToEnd(); visual.UpdateLayout();
        }
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, name))) encoder.Save(stream);
    }

    private static ConversationRecord Current(AssistantControl control) { return ((Dictionary<string, ConversationRecord>)Field(control, "activeRecords"))["Claude"]; }
    private static object Field(object target, string name) { return target.GetType().GetField(name, Private | BindingFlags.Public).GetValue(target); }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Private | BindingFlags.Public).SetValue(target, value); }
    private static object Invoke(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
    private static void Pump()
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
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
