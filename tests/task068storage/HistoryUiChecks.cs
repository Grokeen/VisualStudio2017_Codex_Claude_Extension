using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Views;

namespace VS2017.AIAssistant.Chat { public sealed class ChatAttachment { } }
namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ControlledHistoryReader
    /// desc         : 실제 저장 조회 앞에 테스트 전용 gate를 두어 UI 반응·전환·취소의 순서를 재현한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 실제 WPF 메서드 사본의 동시 수명 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ControlledHistoryReader
    {
        internal readonly MarkdownWorkspaceStore Inner;
        internal readonly ConcurrentDictionary<string, ManualResetEventSlim> Gates = new ConcurrentDictionary<string, ManualResetEventSlim>();
        internal int Calls, FailNext, WorkerThread;
        internal ConversationRecord Captured;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ControlledHistoryReader
        /// desc         : 실제 파일 조회 앞에 제어 gate를 연결해 비동기 순서를 재현한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal ControlledHistoryReader(MarkdownWorkspaceStore inner) { Inner = inner; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LoadOlder
        /// desc         : gate의 지연·실패 조건 뒤 실제 저장소의 이전 페이지 API를 실행한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ChatMessage[] LoadOlder(ConversationRecord record, ChatMessage before, int count)
        {
            WorkerThread = Thread.CurrentThread.ManagedThreadId; Captured = record; Interlocked.Increment(ref Calls);
            ManualResetEventSlim gate;
            if (Gates.TryGetValue(record.Id, out gate) && !gate.Wait(TimeSpan.FromSeconds(8))) throw new TimeoutException("Fixture gate was not released.");
            if (Interlocked.Exchange(ref FailNext, 0) == 1) throw new IOException("Controlled retryable history failure.");
            return Inner.LoadOlder(record, before, count);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : Window.Show 없이 실제 기록 메서드를 실행하는 WPF 껍데기이며 CLI와 다른 제품 기능은 포함하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 메서드 본문은 Prepare-HistoryUi가 제품에서 그대로 추출.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl : UserControl
    {
        private readonly Dictionary<string, ConversationRecord> activeRecords = new Dictionary<string, ConversationRecord>();
        private string selectedProvider = "Codex";
        private readonly ObservableCollection<ChatMessage> visibleConversationMessages = new ObservableCollection<ChatMessage>();
        private ChatSession visibleConversationChat;
        private int visibleConversationStart;
        private const int ConversationPageSize = 20;
        private CancellationTokenSource olderConversationLoadCancellation;
        private int olderConversationLoadGeneration;
        private string olderConversationExhaustedCursor;
        private bool disposed, conversationViewportDisposed, conversationFollowingEnd = true, conversationPrepending;
        private Func<Task<bool>> conversationTryLoadOlder;
        private double conversationPrependOffset, conversationPrependExtent, conversationPrependAnchorY;
        private DispatcherOperation conversationPrependOperation;
        private Task conversationPrependTask;
        private int conversationPrependGeneration;
        private FrameworkElement conversationPrependAnchor;
        private readonly ControlledHistoryReader workspaceStore;
        internal readonly ItemsControl ConversationMessages = new ItemsControl();
        internal readonly ScrollViewer ConversationScroll = new ScrollViewer();
        internal readonly TextBlock StatusLabel = new TextBlock();
        internal bool CliBusy;
        internal string Draft = "unrelated-next-draft";
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantControl
        /// desc         : 창을 표시하지 않는 실제 WPF 트리를 구성해 추출한 제품 메서드를 실행한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal AssistantControl(ControlledHistoryReader store)
        {
            workspaceStore = store;
            var panel = new FrameworkElementFactory(typeof(Border)); panel.SetValue(FrameworkElement.HeightProperty, 32.0);
            var text = new FrameworkElementFactory(typeof(TextBlock)); text.SetBinding(TextBlock.TextProperty, new Binding("Text")); panel.AppendChild(text);
            ConversationMessages.ItemTemplate = new DataTemplate(typeof(ChatMessage)) { VisualTree = panel };
            ConversationScroll.Content = ConversationMessages; ConversationScroll.CanContentScroll = false;
            ConversationScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            Content = ConversationScroll; Width = 400; Height = 240;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Select
        /// desc         : 격리된 실제 저장·WPF 소스에서 Select 단계의 원문·소유권·수명 경계를 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Select(ConversationRecord record, ChatSession chat)
        {
            selectedProvider = record.Provider; activeRecords[selectedProvider] = record; BindVisibleConversation(chat); Layout();
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Layout
        /// desc         : 격리된 실제 저장·WPF 소스에서 Layout 단계의 원문·소유권·수명 경계를 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Layout() { Measure(new Size(400, 240)); Arrange(new Rect(0, 0, 400, 240)); UpdateLayout(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Start
        /// desc         : 격리된 실제 저장·WPF 소스에서 Start 단계의 원문·소유권·수명 경계를 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal bool Start() { return TryPrependConversationPage(); }
        internal bool Loading { get { return conversationPrepending; } }
        internal bool FollowingEnd { get { return conversationFollowingEnd; } }
        internal Task Work { get { return conversationPrependTask; } }
        internal ChatMessage[] Visible { get { return visibleConversationMessages.ToArray(); } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RemoveCursor
        /// desc         : 격리된 실제 저장·WPF 소스에서 RemoveCursor 단계의 원문·소유권·수명 경계를 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void RemoveCursor() { visibleConversationMessages.RemoveAt(0); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MessageY
        /// desc         : 격리된 실제 저장·WPF 소스에서 MessageY 단계의 원문·소유권·수명 경계를 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal double MessageY(ChatMessage message)
        {
            Layout(); var item = ConversationMessages.ItemContainerGenerator.ContainerFromItem(message) as FrameworkElement;
            return item == null ? Double.NaN : item.TranslatePoint(new Point(), ConversationScroll).Y;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseFixture
        /// desc         : 실제 viewport Dispose를 호출하고 fixture의 메시지 구독을 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void CloseFixture()
        {
            disposed = true; DisposeConversationViewport();
            if (visibleConversationChat != null) visibleConversationChat.Messages.CollectionChanged -= VisibleConversationChanged;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CancelConversationFollow
        /// desc         : 격리된 실제 저장·WPF 소스에서 CancelConversationFollow 단계의 원문·소유권·수명 경계를 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CancelConversationFollow() { }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateThinkingPlacement
        /// desc         : 격리된 실제 저장·WPF 소스에서 UpdateThinkingPlacement 단계의 원문·소유권·수명 경계를 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateThinkingPlacement() { }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateThinkingAnimation
        /// desc         : 격리된 실제 저장·WPF 소스에서 UpdateThinkingAnimation 단계의 원문·소유권·수명 경계를 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateThinkingAnimation(bool value) { }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeHistoryOverlay
        /// desc         : 격리된 실제 저장·WPF 소스에서 DisposeHistoryOverlay 단계의 원문·소유권·수명 경계를 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeHistoryOverlay() { }
    }
    internal static class ChangeDiffPopup { internal static void CloseWithin(FrameworkElement element) { } }
}

/// ---------------------------------------------------------------------
/// <summary>
/// name         : HistoryUiChecks
/// desc         : 실제 WPF 기록 메서드 사본과 실제 파일 저장을 gate로 연결해 비동기 조회 수명을 검사한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK068 창 표시·클립보드·OS 입력·공유 빌드 없이 재현.
/// </summary>
/// ---------------------------------------------------------------------
internal static class HistoryUiChecks
{
    private static string root, fixtures;
    private static int number, passed, failed;
    private static readonly List<object> results = new List<object>();
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 개인 fixture에서 모든 경계 검사를 실행하고 실패 원문과 범위를 JSON으로 남긴다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        root = args[0]; fixtures = args[1];
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        Test("Delayed_disk_runs_off_UI_and_dispatcher_remains_responsive", Responsive);
        Test("Repeated_scroll_has_exactly_one_inflight_and_one_prepend", SingleFlight);
        Test("Delayed_A_cannot_change_B_or_cancel_B_inflight", Switch);
        Test("Switch_A_B_A_discards_old_A_generation_and_allows_new_A", SwitchBack);
        Test("Dispose_discards_late_page_without_touching_CLI_or_draft", Dispose);
        Test("Failed_page_preserves_cursor_and_can_retry", Retry);
        Test("Exhausted_cursor_stops_repeated_disk_reads", Exhausted);
        Test("Cursor_change_during_IO_discards_late_prefix", ChangedCursor);
        Test("Prepend_preserves_anchor_even_when_reply_appended_during_IO", Anchor);
        Test("User_scroll_during_IO_remains_at_new_reading_position", UserScroll);
        File.WriteAllText(Path.Combine(root, "history-ui-result.json"), new JavaScriptSerializer().Serialize(new {
            ActualProductMethodSubstrings = true, FullProductDllTested = false, ReaderGateSubstituted = true, ActualWpf = true, WindowShown = false, HostSettingsChanged = false,
            Passed = passed, Failed = failed, Checks = results }), new System.Text.UTF8Encoding(false));
        Console.WriteLine("History UI checks " + passed + "/" + (passed + failed)); return failed == 0 ? 0 : 1;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Test
    /// desc         : 한 비동기·경계 시나리오를 실행하고 최초 오류의 전체 근거를 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Test(string name, Action action)
    {
        try { action(); passed++; results.Add(new { Name = name, Pass = true, Error = "" }); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; results.Add(new { Name = name, Pass = false, Error = e.ToString() }); Console.WriteLine("FAIL " + name + ": " + e); }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Must
    /// desc         : 관찰 가능한 저장·화면 결과가 기대와 다르면 원문을 보존하고 검사를 실패시킨다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Must(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PumpUntil
    /// desc         : 정확한 완료 조건까지 해당 fixture dispatcher만 진행하고 제한 시간 뒤 명시 실패한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void PumpUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(6)) throw new TimeoutException("History UI fixture did not complete.");
            var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); Thread.Sleep(1);
        }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Store
    /// desc         : 개인 출력 내부의 충돌 없는 저장 루트를 할당해 사용자 기록을 분리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static MarkdownWorkspaceStore Store() { return new MarkdownWorkspaceStore(Path.Combine(fixtures, (++number).ToString("D2"))); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Seed
    /// desc         : 실제 저장소에 독립 대화를 만들어 시간과 원문이 고정된 검사 기록을 준비한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ConversationRecord Seed(MarkdownWorkspaceStore s, int count)
    {
        var r = s.Create("Codex");
        for (int i = 0; i < count; i++) r.Messages.Add(new ChatMessage { Id = Guid.NewGuid().ToString("N"), Role = i % 2 == 0 ? "user" : "assistant", Text = "record-" + r.Id + "-message-" + i, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i) });
        s.Save(r); return s.LoadOpen("Codex").Single(item => item.Id == r.Id);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Chat
    /// desc         : 격리된 실제 저장·WPF 소스에서 Chat 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ChatSession Chat(ConversationRecord r) { var chat = new ChatSession(); foreach (var message in r.Messages) chat.Messages.Add(message); return chat; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Release
    /// desc         : 격리된 실제 저장·WPF 소스에서 Release 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Release(ManualResetEventSlim gate, Task work, AssistantControl control)
    { gate.Set(); PumpUntil(() => work.IsCompleted); PumpUntil(() => !control.Loading); control.Layout(); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Responsive
    /// desc         : 격리된 실제 저장·WPF 소스에서 Responsive 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Responsive()
    {
        var s = Store(); var r = Seed(s, 60); var reader = new ControlledHistoryReader(s); var gate = reader.Gates[r.Id] = new ManualResetEventSlim();
        var control = new AssistantControl(reader); control.Select(r, Chat(r)); int ui = Thread.CurrentThread.ManagedThreadId;
        var clock = Stopwatch.StartNew(); Must(control.Start(), "Page did not start."); clock.Stop(); Task work = control.Work;
        Must(clock.Elapsed < TimeSpan.FromMilliseconds(300), "Page start blocked UI on disk gate.");
        bool sentinel = false; Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => sentinel = true)); PumpUntil(() => sentinel && reader.Calls == 1);
        Must(reader.WorkerThread != ui && control.Visible.Length == 20 && !ReferenceEquals(reader.Captured, r), "Disk did not run on independent captured record.");
        Must(!ReferenceEquals(reader.Captured.Messages[0], r.Messages[0]), "Snapshot reused mutable message objects.");
        Release(gate, work, control); Must(control.Visible.Length == 40, "Completed page missing."); control.CloseFixture();
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SingleFlight
    /// desc         : 격리된 실제 저장·WPF 소스에서 SingleFlight 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void SingleFlight()
    {
        var s = Store(); var r = Seed(s, 60); var reader = new ControlledHistoryReader(s); var gate = reader.Gates[r.Id] = new ManualResetEventSlim();
        var control = new AssistantControl(reader); control.Select(r, Chat(r)); control.Start(); Task work = control.Work; PumpUntil(() => reader.Calls == 1);
        for (int i = 0; i < 15; i++) Must(!control.Start(), "Repeated scroll started duplicate IO.");
        Release(gate, work, control); Must(reader.Calls == 1 && control.Visible.Length == 40 && control.Visible.Select(m => m.Id).Distinct().Count() == 40, "Duplicate/omitted prepend."); control.CloseFixture();
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Switch
    /// desc         : 격리된 실제 저장·WPF 소스에서 Switch 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Switch()
    {
        var s = Store(); var a = Seed(s, 60); var b = Seed(s, 60); var reader = new ControlledHistoryReader(s);
        var gateA = reader.Gates[a.Id] = new ManualResetEventSlim(); var gateB = reader.Gates[b.Id] = new ManualResetEventSlim();
        var control = new AssistantControl(reader); control.Select(a, Chat(a)); control.Start(); Task workA = control.Work; PumpUntil(() => reader.Calls == 1);
        control.Select(b, Chat(b)); control.StatusLabel.Text = "B running"; control.Start(); Task workB = control.Work; PumpUntil(() => reader.Calls == 2);
        gateA.Set(); PumpUntil(() => workA.IsCompleted);
        Must(control.Loading && control.Visible.Length == 20 && control.StatusLabel.Text == "B running" && control.Visible.All(m => m.Text.Contains(b.Id)), "Late A changed/cancelled B.");
        Release(gateB, workB, control); Must(control.Visible.Length == 40 && control.Visible.All(m => m.Text.Contains(b.Id)), "B page not independent."); control.CloseFixture();
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SwitchBack
    /// desc         : 격리된 실제 저장·WPF 소스에서 SwitchBack 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void SwitchBack()
    {
        var s = Store(); var a = Seed(s, 60); var b = Seed(s, 60); var reader = new ControlledHistoryReader(s); var gate = reader.Gates[a.Id] = new ManualResetEventSlim();
        var ca = Chat(a); var control = new AssistantControl(reader); control.Select(a, ca); control.Start(); Task old = control.Work; PumpUntil(() => reader.Calls == 1);
        control.Select(b, Chat(b)); control.Select(a, ca); gate.Set(); PumpUntil(() => old.IsCompleted);
        Must(control.Visible.Length == 20, "Returned A accepted old generation.");
        Must(control.Start(), "Fresh A load could not start."); Task next = control.Work; PumpUntil(() => next.IsCompleted); PumpUntil(() => !control.Loading);
        Must(control.Visible.Length == 40 && reader.Calls == 2, "New A generation did not load once."); control.CloseFixture();
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Dispose
    /// desc         : 격리된 실제 저장·WPF 소스에서 Dispose 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Dispose()
    {
        var s = Store(); var r = Seed(s, 60); var reader = new ControlledHistoryReader(s); var gate = reader.Gates[r.Id] = new ManualResetEventSlim();
        var control = new AssistantControl(reader); control.Select(r, Chat(r)); control.CliBusy = true; control.Start(); Task work = control.Work; PumpUntil(() => reader.Calls == 1);
        control.CloseFixture(); gate.Set(); PumpUntil(() => work.IsCompleted);
        Must(control.Visible.Length == 20 && !control.Loading && control.CliBusy && control.Draft == "unrelated-next-draft" && !control.Start(), "Dispose changed page/CLI/draft or allowed new IO.");
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Retry
    /// desc         : 격리된 실제 저장·WPF 소스에서 Retry 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Retry()
    {
        var s = Store(); var r = Seed(s, 60); var reader = new ControlledHistoryReader(s) { FailNext = 1 };
        var control = new AssistantControl(reader); control.Select(r, Chat(r)); string first = control.Visible[0].Id; control.Start(); Task failure = control.Work;
        PumpUntil(() => failure.IsCompleted); PumpUntil(() => !control.Loading);
        Must(control.Visible.Length == 20 && control.Visible[0].Id == first && control.StatusLabel.Text.Contains("다시 시도"), "Failed read changed cursor or hid retry.");
        control.Start(); Task retry = control.Work; PumpUntil(() => retry.IsCompleted); PumpUntil(() => !control.Loading);
        Must(reader.Calls == 2 && control.Visible.Length == 40 && !control.StatusLabel.Text.Contains("읽지 못했습니다"), "Retry did not recover."); control.CloseFixture();
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Exhausted
    /// desc         : 격리된 실제 저장·WPF 소스에서 Exhausted 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Exhausted()
    {
        var s = Store(); var r = Seed(s, 20); var reader = new ControlledHistoryReader(s); var control = new AssistantControl(reader); control.Select(r, Chat(r));
        control.Start(); Task work = control.Work; PumpUntil(() => work.IsCompleted); PumpUntil(() => !control.Loading);
        for (int i = 0; i < 8; i++) Must(!control.Start(), "Known exhausted cursor still handled load as pending.");
        Must(reader.Calls == 1 && control.Visible.Length == 20, "Exhausted page repeated disk reads."); control.CloseFixture();
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ChangedCursor
    /// desc         : 격리된 실제 저장·WPF 소스에서 ChangedCursor 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ChangedCursor()
    {
        var s = Store(); var r = Seed(s, 60); var reader = new ControlledHistoryReader(s); var gate = reader.Gates[r.Id] = new ManualResetEventSlim();
        var control = new AssistantControl(reader); control.Select(r, Chat(r)); control.Start(); Task work = control.Work; PumpUntil(() => reader.Calls == 1);
        control.RemoveCursor(); Release(gate, work, control); Must(control.Visible.Length == 19, "Old cursor result was applied."); control.CloseFixture();
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Anchor
    /// desc         : 격리된 실제 저장·WPF 소스에서 Anchor 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Anchor()
    {
        var s = Store(); var r = Seed(s, 60); var reader = new ControlledHistoryReader(s); var gate = reader.Gates[r.Id] = new ManualResetEventSlim(); var chat = Chat(r);
        var control = new AssistantControl(reader); control.Select(r, chat); var first = control.Visible[0]; double before = control.MessageY(first);
        Must(!Double.IsNaN(before), "WPF anchor container not generated."); control.Start(); Task work = control.Work; PumpUntil(() => reader.Calls == 1);
        chat.Messages.Add(new ChatMessage { Role = "assistant", Text = "reply appended during older IO" }); control.Layout();
        Release(gate, work, control); double after = control.MessageY(first);
        Must(control.Visible.Length == 41 && Math.Abs(after - before) <= 1.0 && !control.FollowingEnd,
            "Anchor moved by " + (after - before) + "px after prepend/concurrent bottom reply."); control.CloseFixture();
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : UserScroll
    /// desc         : 격리된 실제 저장·WPF 소스에서 UserScroll 단계의 원문·소유권·수명 경계를 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 제품 저장·비동기 기록 회귀 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void UserScroll()
    {
        var s = Store(); var r = Seed(s, 60); var reader = new ControlledHistoryReader(s); var gate = reader.Gates[r.Id] = new ManualResetEventSlim();
        var control = new AssistantControl(reader); control.Select(r, Chat(r)); var first = control.Visible[0];
        control.Start(); Task work = control.Work; PumpUntil(() => reader.Calls == 1);
        control.ConversationScroll.ScrollToVerticalOffset(100); control.Layout();
        double before = control.MessageY(first); Must(before < -50, "Fixture did not move user's reading position while IO was pending.");
        Release(gate, work, control); double after = control.MessageY(first);
        Must(control.Visible.Length == 40 && Math.Abs(after - before) <= 1.0, "Delayed prepend jumped user from new reading position by " + (after - before) + "px."); control.CloseFixture();
    }
}
