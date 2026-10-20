using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : MessageCenterChecks
/// desc         : 고정 제품 DLL의 실제 메시지 UI·영속 상태·로컬 CLI 응답 경로를 무창 검사한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK069 가짜 IAiProvider 대신 실제 CliEditProvider subprocess 사용.
/// </summary>
/// ---------------------------------------------------------------------
internal static class MessageCenterChecks
{
    internal static readonly List<string> Checks = new List<string>(), Errors = new List<string>();
    internal static readonly List<object> Observations = new List<object>();
    private static string root, gates, executable;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : parser·전달/수신·실제 응답·취소·수명과 UI 승인을 단계별 격리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 창·팝업·OS입력·계정·원격AI 호출 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static void Run(string directory)
    {
        root = directory; gates = Path.Combine(root, "gates"); Directory.CreateDirectory(gates);
        Environment.SetEnvironmentVariable("TASK069_MESSAGES_GATES", gates); executable = Environment.GetEnvironmentVariable("TASK069_MESSAGES_FIXTURE");
        Scenario("protocol", Protocol); Scenario("backend", Backend); Scenario("actual-ui", ActualUi);
    }
    private static void Scenario(string name, Action action) { try { action(); } catch (Exception error) { Errors.Add(name + ": " + error); } }
    private static void Check(string name, bool value) { if (value) Checks.Add(name); else Errors.Add(name); }
    private static object Field(object target, string name) { return target.GetType().GetField(name, Private | BindingFlags.Public).GetValue(target); }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Private | BindingFlags.Public).SetValue(target, value); }
    private static object Invoke(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
    private static string Fence(string id, string provider, string text) { return "```ai_message\n" + new JavaScriptSerializer().Serialize(new { recipient_id = id, provider, message = text }) + "\n```"; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Protocol
    /// desc         : 한 개의 완결 초안만 파싱하며 자동 전송·명령/경로 주소를 허용하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 중복 키·추가필드·미완료·복수·대용량·한글 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Protocol()
    {
        string id = new string('a', 32), valid = Fence(id, "Claude", "한글 메시지\n검토해주세요."); CollaborationMessageRequest parsed;
        Check("protocol: exact recipient and Korean message survive", CollaborationMessageRequest.TryParse(valid, out parsed) && parsed.RecipientId == id && parsed.Provider == "Claude" && parsed.Message == "한글 메시지\n검토해주세요.");
        var invalid = new Dictionary<string, string> {
            { "ordinary prose", "다른 AI에게 보내줘" }, { "unfinished", valid.Substring(0, valid.Length - 3) },
            { "multiple", valid + "\n" + valid }, { "unknown provider", Fence(id, "Shell", "x") },
            { "path address", Fence("C:\\command.exe", "Codex", "x") }, { "empty", Fence(id, "Claude", " ") },
            { "oversize", Fence(id, "Claude", new string('x', 6001)) }, { "NUL", Fence(id, "Claude", "x\0y") },
            { "duplicate key", valid.Replace("\"provider\":\"Claude\"", "\"provider\":\"Claude\",\"provider\":\"Codex\"") },
            { "extra command", valid.Replace("\"message\":", "\"command\":\"run\",\"message\":") },
            { "array", "```ai_message\n[]\n```" }, { "wrong types", valid.Replace("\"provider\":\"Claude\"", "\"provider\":3") },
            { "uppercase guid", Fence(new string('A',32), "Claude", "x") }
        };
        foreach (var item in invalid) Check("protocol rejects " + item.Key, !CollaborationMessageRequest.TryParse(item.Value, out parsed));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Backend
    /// desc         : 실제 Store와 readonly CLI bridge의 주소·승인·상태·오류·중복·취소 경계를 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 응답은 fixture EXE 출력으로만 생성하며 직접 회신 상태를 만들지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Backend()
    {
        var store = new ConversationCollaborationStore(Path.Combine(root, "backend"));
        ConversationRecord a = Record("Codex"), b = Record("Claude"), c = Record("Claude");
        store.EnsureAgent(a); var receiver = store.EnsureAgent(b); store.EnsureAgent(c);
        store.UpdateAgent(b.Id, b.Provider, receiver.Name, "ROLE_RECEIVER_ONLY", receiver.Team, receiver.Revision);
        var runner = new CollaborationMessageRunner();
        var settings = new ChatRequestContext("explicit common instruction", "", new[] { new ChatMessage { Role = "user", Text = "PRIVATE_HISTORY" } }, new[] { ChatAttachment.FromText("PRIVATE_ATTACHMENT", "secret.txt") });
        var request = store.Send(a.Id, a.Provider, b.Id, b.Provider, "MSG_BASIC");
        string original = File.ReadAllText(store.FilePath); int files = Directory.GetFiles(Path.GetDirectoryName(store.FilePath)).Length;
        Expect("approval false cancels without storage or CLI changes", () => Await(runner.ReplyAsync(store, request.Id, b.Id, b.Provider, executable, settings, false, CancellationToken.None)));
        Check("approval false preserves JSON and creates no lease file", original == File.ReadAllText(store.FilePath) && files == Directory.GetFiles(Path.GetDirectoryName(store.FilePath)).Length && Starts("MSG_BASIC").Length == 0);
        Expect("wrong recipient is rejected before receive", () => Await(runner.ReplyAsync(store, request.Id, c.Id, c.Provider, executable, settings, true, CancellationToken.None)));
        Check("wrong recipient leaves exact Pending receipt", store.GetHandoff(request.Id).State == CollaborationHandoffState.Pending && Starts("MSG_BASIC").Length == 0);
        var result = Await(runner.ReplyAsync(store, request.Id, b.Id, b.Provider, executable, settings, true, CancellationToken.None));
        Check("Claude native CLI reply is actually persisted as Replied", result.State == CollaborationHandoffState.Replied && result.Reply == "reply-MSG_BASIC" && new ConversationCollaborationStore(Path.Combine(root, "backend")).GetHandoff(request.Id).Reply == result.Reply);
        var gate = ReadGate("MSG_BASIC"); string raw = Convert.ToString(gate["Raw"]); string[] arguments = ((ArrayList)gate["Arguments"]).Cast<object>().Select(Convert.ToString).ToArray();
        Check("Claude uses disabled tools and strict empty MCP config", arguments.Contains("--tools") && arguments.Contains("--strict-mcp-config") && !arguments.Contains("--dangerously-skip-permissions"));
        Check("only explicit message and recipient role enter CLI context", raw.Contains("MSG_BASIC") && raw.Contains("ROLE_RECEIVER_ONLY") && !raw.Contains("PRIVATE_HISTORY") && !raw.Contains("PRIVATE_ATTACHMENT"));
        Check("recipient sandbox cwd is independent of user project", Convert.ToString(gate["Directory"]) != root && !Convert.ToString(gate["Directory"]).StartsWith(Path.Combine(root, "user-project"), StringComparison.OrdinalIgnoreCase));
        var read = store.Deliver(request.Id, a.Id, a.Provider);
        Check("sender explicitly reads actual reply as Delivered", read.State == CollaborationHandoffState.Delivered && read.Reply == "reply-MSG_BASIC");
        Expect("already replied message cannot invoke CLI twice", () => Await(runner.ReplyAsync(store, request.Id, b.Id, b.Provider, executable, settings, true, CancellationToken.None)));
        Check("already replied has exactly one actual CLI start", Starts("MSG_BASIC").Length == 1);

        var codex = store.Send(b.Id, b.Provider, a.Id, a.Provider, "MSG_CODEX");
        var codexReply = Await(runner.ReplyAsync(store, codex.Id, a.Id, a.Provider, executable, new ChatRequestContext(), true, CancellationToken.None));
        var codeArgs = ((ArrayList)ReadGate("MSG_CODEX")["Arguments"]).Cast<object>().Select(Convert.ToString).ToArray();
        Check("Codex cross-provider reply uses actual read-only exec", codexReply.Reply == "reply-MSG_CODEX" && codeArgs.Contains("exec") && codeArgs.Contains("read-only") && codeArgs.Contains("--ignore-user-config"));

        var edit = store.Send(a.Id, a.Provider, b.Id, b.Provider, "MSG_EDIT");
        Expect("unexpected edit proposal is never applied or published", () => Await(runner.ReplyAsync(store, edit.Id, b.Id, b.Provider, executable, settings, true, CancellationToken.None)));
        Check("unexpected edits leave Failed and empty reply", store.GetHandoff(edit.Id).State == CollaborationHandoffState.Failed && store.GetHandoff(edit.Id).Reply == "");
        var failure = store.Send(a.Id, a.Provider, b.Id, b.Provider, "MSG_ERROR");
        Expect("actual CLI error does not fabricate success", () => Await(runner.ReplyAsync(store, failure.Id, b.Id, b.Provider, executable, settings, true, CancellationToken.None)));
        Check("CLI error stores Failed with empty reply", store.GetHandoff(failure.Id).State == CollaborationHandoffState.Failed && store.GetHandoff(failure.Id).Reply == "");
        File.WriteAllText(Path.Combine(gates, "MSG_ERROR.success"), "retry");
        var retried = Await(runner.ReplyAsync(store, failure.Id, b.Id, b.Provider, executable, settings, true, CancellationToken.None));
        Check("explicit retry preserves failure event and stores real later reply", retried.State == CollaborationHandoffState.Replied && retried.Events.Any(e => e.State == CollaborationHandoffState.Failed) && Starts("MSG_ERROR").Length == 2);

        var held = store.Send(a.Id, a.Provider, b.Id, b.Provider, "MSG_HOLD");
        var first = runner.ReplyAsync(store, held.Id, b.Id, b.Provider, executable, settings, true, CancellationToken.None); Until(() => Starts("MSG_HOLD").Length == 1);
        Expect("same pending reply cannot run a second CLI concurrently", () => Await(runner.ReplyAsync(store, held.Id, b.Id, b.Provider, executable, settings, true, CancellationToken.None)));
        Check("held request has one native PID while duplicate is rejected", Starts("MSG_HOLD").Length == 1 && store.GetHandoff(held.Id).State == CollaborationHandoffState.Received);
        File.WriteAllText(Path.Combine(gates, "MSG_HOLD.release"), "release"); Await(first);

        var cancelled = store.Send(a.Id, a.Provider, b.Id, b.Provider, "MSG_CANCEL");
        using (var token = new CancellationTokenSource())
        {
            var task = runner.ReplyAsync(store, cancelled.Id, b.Id, b.Provider, executable, settings, true, token.Token); Until(() => Starts("MSG_CANCEL").Length == 1);
            int pid = Convert.ToInt32(ReadGate("MSG_CANCEL")["Pid"]); token.Cancel(); Expect("explicit cancel ends native CLI reply", () => Await(task)); Until(() => !Alive(pid));
            Check("cancel terminates exact native PID and leaves no reply", !Alive(pid) && store.GetHandoff(cancelled.Id).State == CollaborationHandoffState.Failed && store.GetHandoff(cancelled.Id).Reply == "");
        }
        var closed = store.Send(a.Id, a.Provider, b.Id, b.Provider, "MSG_CLOSED"); store.SetConversationOpen(b.Id, b.Provider, false);
        Expect("closed recipient rejects new execution", () => Await(runner.ReplyAsync(store, closed.Id, b.Id, b.Provider, executable, settings, true, CancellationToken.None)));
        Check("closed recipient stays Pending without CLI start", store.GetHandoff(closed.Id).State == CollaborationHandoffState.Pending && Starts("MSG_CLOSED").Length == 0);
        Observations.Add(new { Scenario = "backend", ActualClaudeReply = result.Reply, ActualCodexReply = codexReply.Reply, ActualErrorRetryEvents = retried.Events.Count, NativeProcessStarts = Directory.GetFiles(gates, "*.start.json").Length });
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ActualUi
    /// desc         : 실제 메시지센터 UI 전송·수신/실행·회신 읽기 버튼을 창 표시 없이 조작한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 사용자 초안·미승인 제안·대화 순서와 실제 수신 증거를 확인한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ActualUi()
    {
        string workspace = Path.Combine(root, "ui-workspace"); Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", workspace);
        Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", Path.Combine(root, "ui-settings.json"));
        new AssistantSettingsStore().Save(new AssistantSettings { HideWelcome = true, ShowHistory = false, EnableCompletionNotifications = false });
        var control = new AssistantControl(); Set(control, "scmLookupStarted", true);
        try
        {
            control.Initialize(new Documents(), new RuntimeEditProvider()); Set(control, "agentFullAccess", true);
            ((TextBox)control.FindName("ExecutablePath")).Text = executable;
            var a = Current(control); Invoke(control, "CreateConversation"); var b = Current(control);
            var store = (ConversationCollaborationStore)Field(control, "collaborationStore");
            Select(control, a); var input = (TextBox)control.FindName("Instruction"); input.Text = "ESTABLISH_CAPTURE_ORDER"; Invoke(control, "CaptureCurrentRequest", true); input.Text = "KEEP_SENDER_DRAFT";
            CollaborationMessageRequest draft; CollaborationMessageRequest.TryParse(Fence(b.Id, b.Provider, "MSG_UI"), out draft);
            string routing = store.BuildPromptContext(a.Id, a.Provider);
            Check("UI: actual AI context advertises real peer address and structured approval route", routing.Contains(b.Id) && routing.Contains("ai_message") && routing.Contains("전송 승인"));
            var preview = new MarkdownPreview { DataContext = new ChatMessage { Role = "assistant" }, Markdown = Fence(b.Id,b.Provider,"MSG_UI") };
            Invoke(control, "MarkdownPreviewLoaded", preview, new RoutedEventArgs()); var menu = preview.TextView.ContextMenu;
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            var messageAction = menu.Items.OfType<MenuItem>().Single(i => Convert.ToString(i.Header).Contains("메시지"));
            Check("UI: actual Markdown hook exposes structured draft review without sending", Convert.ToString(messageAction.Header) == "AI 메시지 초안 전송 검토…" && messageAction.Visibility == Visibility.Visible && store.ListInbox(b.Id,b.Provider).Length == 0);
            preview.DataContext = new ChatMessage { Role = "user" }; menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            Check("UI: user message cannot create AI send action", messageAction.Visibility == Visibility.Collapsed);
            preview.DataContext = new ChatMessage { Role = "system" }; menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            Check("UI: system message cannot create AI send action", messageAction.Visibility == Visibility.Collapsed);
            preview.DataContext = new ChatMessage { Role = "assistant" }; preview.Markdown = "일반 답변"; menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            Check("UI: ordinary answer uses explicit manual recipient flow", Convert.ToString(messageAction.Header) == "다른 AI에게 메시지 보내기…" && messageAction.Visibility == Visibility.Visible);
            var selfDraft = Invoke(control,"CreateMessageCenter",a,"MSG_SELF",null);
            Check("UI: no recipient is chosen silently for a manual draft", ((ComboBox)Field(selfDraft,"Recipient")).SelectedItem == null);
            var send = Invoke(control, "CreateMessageCenter", a, draft.Message, draft);
            Check("UI: structured AI draft chooses exact recipient and body without sending", ((ConversationAgentIdentity)((ComboBox)Field(send, "Recipient")).SelectedItem).ConversationId == b.Id && ((TextBox)Field(send, "Draft")).Text == "MSG_UI" && store.ListInbox(b.Id, b.Provider).Length == 0);
            string before = File.ReadAllText(store.FilePath); Invoke(control, "ApproveMessageSend", a, store.GetAgent(b.Id,b.Provider), "MSG_NO_APPROVAL", false);
            Check("UI: approval false preserves exact store", File.ReadAllText(store.FilePath) == before && Starts("MSG_NO_APPROVAL").Length == 0);
            Click((Button)Field(send, "Send"));
            var received = store.ListInbox(b.Id, b.Provider).Single();
            Check("UI: approved Send persists Pending receipt and preserves sender draft", received.Text == "MSG_UI" && received.State == CollaborationHandoffState.Pending && input.Text == "KEEP_SENDER_DRAFT" && Starts("MSG_UI").Length == 0);
            Select(control, b); input.Text = "KEEP_RECEIVER_DRAFT";
            var receive = Invoke(control, "CreateMessageCenter", b, "", null); Set(control, "messageCenter", receive);
            var inbox = (ListBox)Field(receive, "Inbox"); inbox.SelectedIndex = 0; Click((Button)Field(receive, "Run"));
            Until(() => Field(receive, "Execution") == null && store.GetHandoff(received.Id).State != CollaborationHandoffState.Pending);
            Check("UI: explicit receive executes local CLI and persists actual reply", store.GetHandoff(received.Id).State == CollaborationHandoffState.Replied && store.GetHandoff(received.Id).Reply == "reply-MSG_UI" && Starts("MSG_UI").Length == 1);
            var chatB = (ChatSession)control.GetType().GetProperty("ActiveChat", Private).GetValue(control, null);
            Check("UI: received request and actual reply enter recipient conversation with draft preserved", input.Text == "KEEP_RECEIVER_DRAFT" && chatB.Messages.Any(m => m.Text.Contains("MSG_UI") && m.Role == "user") && chatB.Messages.Any(m => m.Text == "reply-MSG_UI" && m.Role == "assistant"));
            int countB = chatB.Messages.Count; Invoke(control, "PublishMessageEvidence", b, store.GetHandoff(received.Id), true);
            Check("UI: repeated evidence publication does not duplicate recipient messages", chatB.Messages.Count == countB);
            Select(control, a); var read = Invoke(control, "CreateMessageCenter", a, "", null); ((ListBox)Field(read, "Outbox")).SelectedIndex = 0; Click((Button)Field(read, "Read"));
            var chatA = (ChatSession)control.GetType().GetProperty("ActiveChat", Private).GetValue(control, null);
            Check("UI: explicit read stores Delivered and preserves sender draft", store.GetHandoff(received.Id).State == CollaborationHandoffState.Delivered && input.Text == "KEEP_SENDER_DRAFT" && chatA.Messages.Any(m => m.Text.Contains("reply-MSG_UI")));
            int countA = chatA.Messages.Count; ((ListBox)Field(read, "Outbox")).SelectedIndex = 0; Click((Button)Field(read, "Read"));
            Check("UI: reading same stored reply twice does not duplicate sender conversation", countA == chatA.Messages.Count);
            input.Text = "VERIFY_NEXT_CONTEXT"; var request = Invoke(control, "CaptureCurrentRequest", true); var context = (ChatRequestContext)Invoke(control, "BuildQueuedExecutionContext", request);
            Check("UI: next actual queued context includes explicitly read peer reply", context.History.Any(m => m.Text.Contains("reply-MSG_UI")));
            var actual = store.GetHandoff(received.Id); Observations.Add(new { Scenario = "actual-ui", actual.State, actual.FromName, actual.ToName, actual.Text, actual.Reply, SenderMessages = chatA.Messages.Count, ReceiverMessages = chatB.Messages.Count });
            var peerBefore = store.GetAgent(b.Id,b.Provider); var changed = store.UpdateAgent(b.Id,b.Provider,peerBefore.Name,peerBefore.Role,"another-team",peerBefore.Revision);
            Expect("UI: stale selected recipient cannot bypass changed team", () => Invoke(control,"ApproveMessageSend",a,peerBefore,"MSG_TEAM_CHANGED",true));
            Check("UI: changed-team recipient receives no stored or executed message", !store.ListInbox(b.Id,b.Provider).Any(i => i.Text == "MSG_TEAM_CHANGED") && Starts("MSG_TEAM_CHANGED").Length == 0);
            store.UpdateAgent(b.Id,b.Provider,changed.Name,changed.Role,peerBefore.Team,changed.Revision); store.SetConversationOpen(b.Id,b.Provider,false);
            Expect("UI: stale selected recipient cannot bypass closed state", () => Invoke(control,"ApproveMessageSend",a,peerBefore,"MSG_CLOSED_UI",true));
            Check("UI: closed selected recipient receives no stored or executed message", !store.ListInbox(b.Id,b.Provider).Any(i => i.Text == "MSG_CLOSED_UI") && Starts("MSG_CLOSED_UI").Length == 0);
            store.SetConversationOpen(b.Id,b.Provider,true);
            var pendingDispose = store.Send(b.Id,b.Provider,a.Id,a.Provider,"MSG_UI_DISPOSE");
            var disposeView = Invoke(control,"CreateMessageCenter",a,"",null); Set(control,"messageCenter",disposeView);
            var disposeInbox = (ListBox)Field(disposeView,"Inbox"); disposeInbox.SelectedItem = disposeInbox.Items.Cast<object>().Single(i => ((CollaborationHandoff)Field(i,"Item")).Id == pendingDispose.Id);
            Click((Button)Field(disposeView,"Run")); Until(() => Starts("MSG_UI_DISPOSE").Length == 1);
            int disposePid = Convert.ToInt32(ReadGate("MSG_UI_DISPOSE")["Pid"]); control.Dispose();
            Until(() => Field(disposeView,"Execution") == null && !Alive(disposePid));
            Check("UI: product Dispose cancels actual message CLI process", !Alive(disposePid));
            Check("UI: disposal never fabricates a peer reply", store.GetHandoff(pendingDispose.Id).State == CollaborationHandoffState.Failed && store.GetHandoff(pendingDispose.Id).Reply == "");
        }
        finally { control.Dispose(); Pump(); }
    }

    private static ConversationRecord Record(string provider) { return new ConversationRecord { Id = Guid.NewGuid().ToString("N"), Provider = provider, IsOpen = true }; }
    private static ConversationRecord Current(AssistantControl control) { return ((Dictionary<string, ConversationRecord>)Field(control, "activeRecords"))["Claude"]; }
    private static void Select(AssistantControl control, ConversationRecord record) { ((ListBox)control.FindName("ConversationList")).SelectedItem = record; Pump(); }
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
    private static string[] Starts(string id) { return Directory.GetFiles(gates, id + ".*.start.json"); }
    private static Dictionary<string, object> ReadGate(string id) { Until(() => Starts(id).Length > 0); return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Starts(id)[0])); }
    private static T Await<T>(Task<T> task) { Until(() => task.IsCompleted); return task.GetAwaiter().GetResult(); }
    private static void Expect(string name, Action action) { try { action(); Check(name, false); } catch (Exception) { Check(name, true); } }
    private static bool Alive(int pid) { try { using (var p = Process.GetProcessById(pid)) return !p.HasExited; } catch (ArgumentException) { return false; } }
    private static void Until(Func<bool> condition) { var watch = Stopwatch.StartNew(); while (!condition()) { if (watch.ElapsedMilliseconds > 12000) throw new TimeoutException("Controlled message condition timed out."); Pump(); } }
    private static void Pump() { var frame = new DispatcherFrame(); var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(20) }; timer.Tick += (s,a) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    private sealed class Documents : IActiveDocumentService, ISourceNavigationService, IFullAccessDocumentService
    {
        public IEditorDocument GetActiveDocument() { throw new InvalidOperationException("Message bridge must not read editor source."); }
        public DocumentSnapshot CaptureFullAccessContext() { return null; }
        public string GetProjectDirectory() { return Path.GetDirectoryName(executable); }
        public void NavigateTo(string path, int line) { throw new InvalidOperationException("No navigation."); }
    }
}
