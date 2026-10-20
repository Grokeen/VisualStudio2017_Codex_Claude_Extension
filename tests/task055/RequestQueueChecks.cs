using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : RequestQueueChecks
/// desc         : 실제 WPF 입력·버튼과 제어 가능한 Provider로 FIFO·승인·취소·문맥 소유권을 검사한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 격리 제품 DLL 계약 검사.
/// </summary>
/// ---------------------------------------------------------------------
public static class RequestQueueChecks
{
    public static readonly List<string> Passed = new List<string>();
    public static readonly List<string> Failed = new List<string>();
    public static readonly List<string> StatusObservations = new List<string>();
    public static long SwitchElapsedMilliseconds;
    public static int SwitchDiskWrites;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string root;
    private static int scenario;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 격리된 제품 UI의 모든 요청 큐 시나리오를 실행한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run()
    {
        root = Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH");
        Scenario("FIFO input and immutable context", Fifo);
        Scenario("pending proposal approval gate", Review);
        Scenario("explicitly skip pending proposal", Discard);
        Scenario("stop cancels waiting and rejects late result", Stop);
        Scenario("failure does not launch successors", Failure);
        Scenario("dispose prevents later execution", Disposed);
        Scenario("bounded waiting queue", Bounded);
        Scenario("source change rejects queued request", ChangedSource);
        Scenario("conversation ownership changes reject execution", Ownership);
        Scenario("full-access uses same FIFO and immutable mode", FullAccessMode);
        Scenario("full-access actual child process FIFO", FullAccessProcesses);
        Scenario("full-access failure cancels successors", FullAccessFailure);
        Scenario("full-access Stop kills owned child and pending queue", FullAccessStop);
        Scenario("acceptance save failure preserves draft and history", SaveFailure);
        Scenario("unchanged tab switch persistence", Performance);
        Scenario("independent rooms run concurrently", ConcurrentRooms);
        Scenario("independent edit rooms run concurrently", ConcurrentEditRooms);
        Scenario("room stop leaves other request running", ConcurrentRoomStop);
        Scenario("room proposals stay with their owners", ConcurrentRoomProposals);
        RunStatusOnly(); // R58-04: keep both primary-error regressions in the default suite.
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Scenario
    /// desc         : 시나리오마다 새 WPF 컨트롤과 작업 폴더를 사용하고 실패를 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Scenario(string name, Action<TestHost> action)
    {
        TestHost host = null;
        try { host = new TestHost(++scenario); action(host); }
        catch (Exception error) { Failed.Add(name + ": " + error); }
        finally { if (host != null) host.Dispose(); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Fifo
    /// desc         : 실행 중 입력·제출 순서·고정 옵션·후속 답변 문맥을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Fifo(TestHost h)
    {
        h.Text("ModelName").Text = "claude-sonnet-4-6";
        h.Text("ExecutablePath").Text = "frozen-cli.exe";
        h.Chat.Attachments.Add(ChatAttachment.FromText("attachment-one", "one.txt"));
        h.Send("FIRST");
        Check("first request starts once and clears submitted text", h.Provider.Calls.Count == 1 && h.Input.Text == "");
        Check("input and send remain enabled while running: input="+h.Input.IsEnabled+", send="+((Button)h.Control.FindName("GenerateButton")).IsEnabled+", local="+h.Input.ReadLocalValue(UIElement.IsEnabledProperty), h.Input.IsEnabled && ((Button)h.Control.FindName("GenerateButton")).IsEnabled);
        h.Send("SECOND");
        h.Send("THIRD FUTURE");
        Check("second Enter enqueues without concurrent provider call", h.Count == 2 && h.Provider.Calls.Count == 1);
        h.Input.Text = "NEXT DRAFT";
        h.Key(ModifierKeys.Control);
        Check("Ctrl Enter adds newline while request is running", h.Input.Text.Contains(Environment.NewLine));
        string draft = h.Input.Text;
        Check("completion notification source remains executing request", (string)Property(h.Control,"CurrentRequestTitle") == "FIRST" && (string)Property(h.Control,"CurrentRequestProvider") == "Claude");
        var tabs = (TabControl)h.Control.FindName("ProviderChoice"); tabs.SelectedIndex = 1;
        Check("provider tab cannot switch while executing", (string)Field(h.Control,"selectedProvider") == "Claude");
        h.Text("ModelName").Text = "claude-opus-4-6";
        h.Text("ExecutablePath").Text = "later-cli.exe";
        h.Chat.Attachments.Clear();
        h.Provider.Complete(0, false); Until(() => h.Provider.Calls.Count == 2);
        var second = h.Provider.Calls[1];
        Check("FIFO executes second with original instruction", second.Instruction == "SECOND" && second.Provider == "Claude");
        Check("queued model and executable are immutable", second.Context.Model == "claude-sonnet-4-6" && second.Executable == "frozen-cli.exe");
        Check("queued attachments remain captured after later mutation", second.Context.Attachments.Length == 1 && second.Context.Attachments[0].Text == "attachment-one");
        Check("queued original source and selection are captured", second.Source.Content == "original" && second.Source.Selection.Text == "orig");
        Check("second receives completed first response and excludes future user", second.Context.History.Any(m => m.Role=="assistant" && m.Text=="done") && !second.Context.History.Any(m => m.Text=="THIRD FUTURE" || m.Text=="SECOND"));
        Check("draft survives first completion and queued start", h.Input.Text == draft);
        Check("notification source advances only when second starts", (string)Property(h.Control,"CurrentRequestTitle") == "SECOND");
        h.Provider.Complete(1, false); Until(() => h.Provider.Calls.Count==3);
        var third=h.Provider.Calls[2];
        Check("third history groups completed turns in submission order",third.Context.History.Select(m=>m.Role+":"+m.Text.Split('\n')[0]).SequenceEqual(new[]{"user:FIRST","assistant:done","user:SECOND","assistant:done"}));
        h.Provider.Complete(2,false);Until(()=>!h.Busy);
        Check("both requests run exactly once without editor writes", h.Provider.Calls.Count == 3 && h.Document.Writes == 0 && h.Count == 0);
        Check("latest draft survives all completions", h.Input.Text == draft);
        Check("accepted queued user text is recorded exactly once", h.Chat.Messages.Count(m => m.Role == "user" && m.Text.StartsWith("SECOND")) == 1);
        Check("finished request releases notification context", (string)Property(h.Control,"CurrentRequestTitle") == "");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Review
    /// desc         : 미승인 수정안 보존과 명시 승인·원본 변경 경계를 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Review(TestHost h)
    {
        h.Send("EDIT"); h.Send("FOLLOWUP"); h.Provider.Complete(0, true); Until(() => !h.Busy);
        object original = Field(h.Control,"pendingProposal");
        Check("pending edit pauses FIFO before next CLI", h.Count == 1 && h.Provider.Calls.Count == 1 && original != null);
        h.Send("THIRD");
        Check("additional input can enqueue at approval gate", h.Count == 2 && h.Provider.Calls.Count == 1 && ReferenceEquals(original,Field(h.Control,"pendingProposal")));
        h.Click("ApplyClicked");
        Check("unapproved apply cannot run or overwrite pending proposal", h.Document.Writes == 0 && h.Provider.Calls.Count == 1 && ReferenceEquals(original,Field(h.Control,"pendingProposal")));
        ((CheckBox)h.Control.FindName("ReviewApproval")).IsChecked = true;
        h.Click("ApplyClicked"); Until(() => !h.Busy && h.Count == 0);
        Check("explicit approval applies only first proposal", h.Document.Writes == 1 && h.Document.Content == "changed");
        Check("queued pre-apply source is rejected after explicit edit", h.Provider.Calls.Count == 1 && h.Chat.Messages.Any(m => m.Text.Contains("편집 내용이 변경")));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Stop
    /// desc         : 중지 뒤 대기 요청과 늦은 결과의 재실행을 차단하는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Discard
    /// desc         : 명시적 건너뛰기만 수정안을 버리고 소스 변경 없이 다음 요청을 실행하는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 승인 대기 건너뛰기 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Discard(TestHost h)
    {
        h.Send("EDIT");h.Send("FOLLOWUP");h.Provider.Complete(0,true);Until(()=>!h.Busy);
        var button=h.Control.FindName("DiscardProposalButton") as Button;
        Check("skip control is visible and enabled only at review queue gate",button!=null && button.Visibility==Visibility.Visible && button.IsEnabled);
        h.Input.Text="SKIP NEXT DRAFT";h.Click("DiscardProposalAndContinueClicked");Until(()=>h.Provider.Calls.Count==2);
        Check("explicit skip launches successor exactly once without editor change",h.Provider.Calls.Count==2 && h.Document.Writes==0 && h.Document.Content=="original");
        Check("explicit skip clears proposal and approval and records user action",Field(h.Control,"pendingProposal")==null && ((CheckBox)h.Control.FindName("ReviewApproval")).IsChecked==false && h.Chat.Messages.Any(m=>m.Role=="system" && m.Text.Contains("건너뛰기")));
        Check("skip button hides again while successor executes",button.Visibility==Visibility.Collapsed && !button.IsEnabled);
        h.Provider.Complete(1,false);Until(()=>!h.Busy);
        Check("skip completion preserves latest draft",h.Input.Text=="SKIP NEXT DRAFT");
    }

    /// ---------------------------------------------------------------------
    private static void Stop(TestHost h)
    {
        h.Provider.IgnoreCancellation = true; h.Send("STOP ME"); h.Send("NEVER RUN");
        h.Input.Text = "KEEP DRAFT"; h.Click("StopClicked");
        Check("stop immediately cancels queued requests", h.Count == 0 && h.Provider.Calls[0].Token.IsCancellationRequested);
        h.Provider.Complete(0, true); Until(() => !h.Busy);
        Check("late result after stop is discarded", Field(h.Control,"pendingProposal") == null && h.Document.Writes == 0 && h.Provider.Calls.Count == 1);
        Check("stop preserves next draft and unexecuted text", h.Input.Text == "KEEP DRAFT" && h.Chat.Messages.Any(m => m.Text.Contains("NEVER RUN")));
        h.Send("FRESH");
        Check("cancelled queued instruction never enters later execution history",!h.Provider.Calls[1].Context.History.Any(m=>m.Text=="NEVER RUN"));
        h.Provider.Complete(1,false);Until(()=>!h.Busy);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Failure
    /// desc         : Provider 실패 이후 후속 요청을 실행하지 않는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Failure(TestHost h)
    {
        h.Send("FAIL"); h.Send("SUCCESSOR"); h.Input.Text="DRAFT";
        h.Provider.Calls[0].Completion.SetException(new InvalidOperationException("controlled failure")); Until(() => !h.Busy);
        Check("failure prevents successor and clears queue", h.Provider.Calls.Count == 1 && h.Count == 0 && h.Document.Writes == 0);
        Check("failure records diagnostic and preserves draft", h.Chat.Messages.Any(m => m.Text.Contains("controlled failure")) && h.Input.Text=="DRAFT");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Disposed
    /// desc         : 화면 해제 이후 비동기 결과와 대기 실행을 차단하는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Disposed(TestHost h)
    {
        h.Provider.IgnoreCancellation=true; h.Send("DISPOSE"); h.Send("NEVER"); h.Control.Dispose(); h.IsDisposed=true;
        h.Provider.Complete(0, true); Until(() => !h.Busy);
        Check("dispose cancels active and releases pending FIFO", h.Count == 0 && h.Provider.Calls[0].Token.IsCancellationRequested && h.Provider.Calls.Count == 1);
        Check("dispose completion cannot write editor", h.Document.Writes==0 && Field(h.Control,"pendingProposal")==null);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Bounded
    /// desc         : 대기 상한과 초과 입력 보존을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Bounded(TestHost h)
    {
        h.Send("ACTIVE"); for(int index=0;index<8;index++) h.Send("WAIT "+index);
        h.Send("OVERFLOW");
        Check("queue bounds waiting requests to eight", h.Count==8 && h.Provider.Calls.Count==1);
        Check("overflow retains input for later resend", h.Input.Text=="OVERFLOW" && ((TextBlock)h.Control.FindName("StatusLabel")).Text.Contains("최대 8"));
        h.Click("StopClicked"); Until(()=>!h.Busy);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ChangedSource
    /// desc         : 외부 편집 뒤 결과 적용과 후속 실행을 차단하는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ChangedSource(TestHost h)
    {
        h.Send("A"); h.Send("B"); h.Document.Content="external edit"; h.Provider.Complete(0,false); Until(()=>!h.Busy);
        Check("external buffer change rejects result and successors", h.Provider.Calls.Count==1 && h.Count==0 && h.Document.Writes==0 && Field(h.Control,"pendingProposal")==null);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Ownership
    /// desc         : 다른 대화에 대기 요청이나 응답이 섞이지 않는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Ownership(TestHost h)
    {
        h.Send("A"); h.Send("B");
        var records=(Dictionary<string,ConversationRecord>)Field(h.Control,"activeRecords");
        var original=records["Claude"]; records["Claude"]=new MarkdownWorkspaceStore().Create("Claude");
        h.Provider.Complete(0,false); Until(()=>!h.Busy);
        Check("changed conversation cannot consume queued request", h.Provider.Calls.Count==1 && h.Count==0);
        Check("foreign record does not receive queued user text", !records["Claude"].Messages.Any(m=>m.Text=="B"));
        records["Claude"]=original;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FullAccessMode
    /// desc         : 제출한 권한 모드를 실행 직전에 재확인하는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void FullAccessMode(TestHost h)
    {
        h.Send("EDIT MODE"); Set(h.Control,"agentFullAccess",true); h.Send("FULL MODE"); Set(h.Control,"agentFullAccess",false);
        h.Provider.Complete(0,false); Until(()=>!h.Busy);
        Check("full-access request shares bounded FIFO with edit request", h.Provider.Calls.Count==1 && h.Count==0);
        Check("changed permission mode cannot promote queued request", h.Chat.Messages.Any(m=>m.Text.Contains("대화·권한·참여자")) && h.Document.Writes==0);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FullAccessProcesses
    /// desc         : 제어 가능한 실제 자식 프로세스로 전체 액세스 FIFO를 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void FullAccessProcesses(TestHost h)
    {
        Set(h.Control,"agentFullAccess",true);
        h.Text("ExecutablePath").Text=Environment.GetEnvironmentVariable("TASK055_FIXTURE_EXE");
        h.Send("FULL ONE");
        string directory=h.Document.GetProjectDirectory();string trace=Path.Combine(directory,"queue-process-events.txt");
        Until(()=>File.Exists(trace));
        h.Send("FULL TWO"); h.Input.Text="FULL NEXT DRAFT";
        Check("full-access accepts another prompt during child process",h.Input.IsEnabled && h.Count==1 && File.ReadAllLines(trace).SequenceEqual(new[]{"start1"}));
        Check("full-access notification holds executing prompt",(string)Property(h.Control,"CurrentRequestTitle")=="FULL ONE");
        File.WriteAllText(Path.Combine(directory,"release-1"),"");
        Until(()=>File.Exists(Path.Combine(directory,"request-2.json")));
        Check("full-access FIFO starts second only after first completion",File.ReadAllLines(trace).SequenceEqual(new[]{"start1","end1","start2"}));
        Check("full-access second child receives captured prompt",File.ReadAllText(Path.Combine(directory,"request-2.json")).Contains("FULL TWO"));
        File.WriteAllText(Path.Combine(directory,"release-2"),"");Until(()=>!h.Busy);
        Check("full-access completion preserves next draft and avoids edit-provider calls",h.Input.Text=="FULL NEXT DRAFT" && h.Count==0 && h.Provider.Calls.Count==0);
        Check("full-access never auto applies review edits",h.Document.Writes==0 && Field(h.Control,"pendingProposal")==null);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConcurrentRooms
    /// desc         : 실제 두 CLI 프로세스가 각 대화방에서 동시에 대기하고 응답을 소유 방에 남긴다.
    /// author       : CodexCode
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ConcurrentRooms(TestHost h)
    {
        h.Control.Initialize(h.Document,new RuntimeEditProvider());
        Set(h.Control,"agentFullAccess",true);
        h.Text("ExecutablePath").Text=Environment.GetEnvironmentVariable("TASK055_FIXTURE_EXE");
        var first=(ConversationRecord)((Dictionary<string,ConversationRecord>)Field(h.Control,"activeRecords"))["Claude"];
        h.Send("ROOM-A");
        string directory=h.Document.GetProjectDirectory();
        Until(()=>File.Exists(Path.Combine(directory,"start-A")));
        Invoke(h.Control,"CreateConversation");
        var second=(ConversationRecord)((Dictionary<string,ConversationRecord>)Field(h.Control,"activeRecords"))["Claude"];
        Check("running conversation can open another room",first.Id!=second.Id && first.State==ConversationRunState.Running);
        h.Send("ROOM-B");
        Until(()=>File.Exists(Path.Combine(directory,"start-B")));
        Check("two distinct AI processes run before either completes",first.State==ConversationRunState.Running && second.State==ConversationRunState.Running && !File.Exists(Path.Combine(directory,"release-A")) && !File.Exists(Path.Combine(directory,"release-B")));
        File.WriteAllText(Path.Combine(directory,"release-B"),"");
        Until(()=>second.State==ConversationRunState.Completed);
        Check("second room can finish while first is still running",first.State==ConversationRunState.Running && h.Chat.Messages.Any(message=>message.Text.Contains("fixture room B completed")));
        File.WriteAllText(Path.Combine(directory,"release-A"),"");
        Until(()=>first.State==ConversationRunState.Completed);
        Check("each room retains its own final answer",new MarkdownWorkspaceStore().LoadOpen("Claude").Single(record=>record.Id==first.Id).Messages.Any(message=>message.Text.Contains("fixture room A completed")) && new MarkdownWorkspaceStore().LoadOpen("Claude").Single(record=>record.Id==second.Id).Messages.Any(message=>message.Text.Contains("fixture room B completed")));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConcurrentEditRooms
    /// desc         : 수정안 검토 모드에서도 서로 다른 방의 CLI가 독립적으로 완료된다.
    /// author       : CodexCode
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ConcurrentEditRooms(TestHost h)
    {
        h.Control.Initialize(h.Document,new RuntimeEditProvider());
        Set(h.Control,"agentFullAccess",false);
        h.Text("ExecutablePath").Text=Environment.GetEnvironmentVariable("TASK055_FIXTURE_EXE");
        string directory=h.Document.GetProjectDirectory();
        Environment.SetEnvironmentVariable("TASK055_ROOM_DIR",directory);
        try
        {
            var first=(ConversationRecord)((Dictionary<string,ConversationRecord>)Field(h.Control,"activeRecords"))["Claude"];
            h.Send("EDIT-A");
            Until(()=>File.Exists(Path.Combine(directory,"start-edit-A")));
            Invoke(h.Control,"CreateConversation");
            var second=(ConversationRecord)((Dictionary<string,ConversationRecord>)Field(h.Control,"activeRecords"))["Claude"];
            h.Send("EDIT-B");
            Until(()=>File.Exists(Path.Combine(directory,"start-edit-B")));
            Check("two edit CLI requests overlap in different rooms",first.State==ConversationRunState.Running && second.State==ConversationRunState.Running);
            File.WriteAllText(Path.Combine(directory,"release-edit-B"),"");
            Until(()=>second.State==ConversationRunState.Completed);
            Check("second edit room completes independently",first.State==ConversationRunState.Running && h.Chat.Messages.Any(message=>message.Text.Contains("fixture edit B completed")));
            File.WriteAllText(Path.Combine(directory,"release-edit-A"),"");
            Until(()=>first.State==ConversationRunState.Completed);
            Check("edit replies persist to their owner rooms",new MarkdownWorkspaceStore().LoadOpen("Claude").Single(record=>record.Id==first.Id).Messages.Any(message=>message.Text.Contains("fixture edit A completed")) && new MarkdownWorkspaceStore().LoadOpen("Claude").Single(record=>record.Id==second.Id).Messages.Any(message=>message.Text.Contains("fixture edit B completed")));
        }
        finally { Environment.SetEnvironmentVariable("TASK055_ROOM_DIR",null); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConcurrentRoomStop
    /// desc         : 한 대화방의 중지만 해당 CLI 프로세스를 취소한다.
    /// author       : CodexCode
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ConcurrentRoomStop(TestHost h)
    {
        h.Control.Initialize(h.Document,new RuntimeEditProvider());Set(h.Control,"agentFullAccess",true);
        h.Text("ExecutablePath").Text=Environment.GetEnvironmentVariable("TASK055_FIXTURE_EXE");
        var first=(ConversationRecord)((Dictionary<string,ConversationRecord>)Field(h.Control,"activeRecords"))["Claude"];
        h.Send("ROOM-A");string directory=h.Document.GetProjectDirectory();Until(()=>File.Exists(Path.Combine(directory,"start-A")));
        Invoke(h.Control,"CreateConversation");
        var second=(ConversationRecord)((Dictionary<string,ConversationRecord>)Field(h.Control,"activeRecords"))["Claude"];
        h.Send("ROOM-B");Until(()=>File.Exists(Path.Combine(directory,"start-B")));
        ((ListBox)h.Control.FindName("ConversationList")).SelectedItem=first;
        h.Click("StopClicked");Until(()=>first.State==ConversationRunState.Stopped);
        Check("stopping first room keeps second AI running",second.State==ConversationRunState.Running);
        File.WriteAllText(Path.Combine(directory,"release-B"),"");Until(()=>second.State==ConversationRunState.Completed);
        Check("second room still completes after first cancellation",second.Messages.Any(message=>message.Text.Contains("fixture room B completed")));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConcurrentRoomProposals
    /// desc         : 다른 방으로 돌아와도 두 수정안과 승인 상태가 섞이지 않는지 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ConcurrentRoomProposals(TestHost h)
    {
        h.Control.Initialize(h.Document,new RuntimeEditProvider());Set(h.Control,"agentFullAccess",false);
        h.Text("ExecutablePath").Text=Environment.GetEnvironmentVariable("TASK055_FIXTURE_EXE");
        string directory=h.Document.GetProjectDirectory();Environment.SetEnvironmentVariable("TASK055_ROOM_DIR",directory);
        try
        {
            var first=(ConversationRecord)((Dictionary<string,ConversationRecord>)Field(h.Control,"activeRecords"))["Claude"];
            h.Send("EDIT-A PROPOSAL");Until(()=>File.Exists(Path.Combine(directory,"start-edit-A")));
            Invoke(h.Control,"CreateConversation");
            var second=(ConversationRecord)((Dictionary<string,ConversationRecord>)Field(h.Control,"activeRecords"))["Claude"];
            h.Send("EDIT-B PROPOSAL");Until(()=>File.Exists(Path.Combine(directory,"start-edit-B")));
            File.WriteAllText(Path.Combine(directory,"release-edit-B"),"");Until(()=>second.State==ConversationRunState.Completed);
            File.WriteAllText(Path.Combine(directory,"release-edit-A"),"");Until(()=>first.State==ConversationRunState.Completed);
            var list=(ListBox)h.Control.FindName("ConversationList");
            Check("second room shows its own unapproved proposal",((EditProposal)Field(h.Control,"pendingProposal")).Summary.Contains("fixture edit B"));
            list.SelectedItem=first;
            var firstProposal=(EditProposal)Field(h.Control,"pendingProposal");
            Check("switching restores first room proposal without approval",firstProposal!=null && firstProposal.Summary.Contains("fixture edit A") && ((CheckBox)h.Control.FindName("ReviewApproval")).IsChecked!=true);
            list.SelectedItem=second;
            Check("switching back restores second room proposal",((EditProposal)Field(h.Control,"pendingProposal")).Summary.Contains("fixture edit B") && h.Document.Writes==0);
        }
        finally { Environment.SetEnvironmentVariable("TASK055_ROOM_DIR",null); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FullAccessFailure
    /// desc         : 전체 액세스 실패 이후 후속 CLI를 실행하지 않는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void FullAccessFailure(TestHost h)
    {
        Set(h.Control,"agentFullAccess",true);h.Text("ExecutablePath").Text=Environment.GetEnvironmentVariable("TASK055_FIXTURE_EXE");
        h.Send("FAIL FULL");string directory=h.Document.GetProjectDirectory();string trace=Path.Combine(directory,"queue-process-events.txt");
        Until(()=>File.Exists(trace));h.Send("NEVER FULL");
        File.WriteAllText(Path.Combine(directory,"release-1"),"");Until(()=>!h.Busy);
        Check("full-access failure stops same FIFO before successor",h.Count==0 && File.ReadAllLines(trace).SequenceEqual(new[]{"start1","end1"}));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FullAccessStop
    /// desc         : 전체 액세스 중지 시 소유 자식 종료와 입력 보존을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void FullAccessStop(TestHost h)
    {
        Set(h.Control,"agentFullAccess",true);h.Text("ExecutablePath").Text=Environment.GetEnvironmentVariable("TASK055_FIXTURE_EXE");
        h.Send("STOP FULL");string directory=h.Document.GetProjectDirectory();string trace=Path.Combine(directory,"queue-process-events.txt");
        string pidFile=Path.Combine(directory,"process-1.pid");Until(()=>File.Exists(pidFile));
        int pid=int.Parse(File.ReadAllText(pidFile));h.Send("NEVER FULL");h.Input.Text="FULL DRAFT";h.Click("StopClicked");Until(()=>!h.Busy);
        bool exited;try{exited=Process.GetProcessById(pid).HasExited;}catch(ArgumentException){exited=true;}
        Check("full-access stop cancels queue and terminates owned CLI",h.Count==0 && exited && File.ReadAllLines(trace).SequenceEqual(new[]{"start1"}));
        Check("full-access stop preserves following draft",h.Input.Text=="FULL DRAFT");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SaveFailure
    /// desc         : 저장 오류 시 제출 입력과 기존 100개 메시지의 복구를 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void SaveFailure(TestHost h)
    {
        for(int index=0;index<100;index++)h.Chat.Add("assistant","prior "+index);
        Invoke(h.Control,"PersistCurrentConversation");
        var records=(Dictionary<string,ConversationRecord>)Field(h.Control,"activeRecords");
        string file=Directory.GetFiles(new MarkdownWorkspaceStore().RootPath,records["Claude"].Id+".md",SearchOption.AllDirectories).Single();
        var before=h.Chat.Messages.ToArray();
        using(var locked=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.None)){h.Send("SAVE FAILURE");}
        Check("failed queue acceptance never launches CLI and preserves input",h.Count==0 && h.Provider.Calls.Count==0 && h.Input.Text=="SAVE FAILURE");
        Check("failed queue acceptance restores all hundred original messages",h.Chat.Messages.SequenceEqual(before));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunStatusOnly
    /// desc         : 표시 창 없이 실제 실패 경로의 원래 오류와 대기 취소 안내 보존을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK055 후속, queued 0/1 오류 표시 회귀.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void RunStatusOnly()
    {
        root=Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH");
        Scenario("failure without queued requests retains primary error",h=>{
            h.Send("FAIL ACTIVE");
            h.Provider.Calls[0].Completion.SetException(new InvalidOperationException("controlled primary status error"));Until(()=>!h.Busy && !(bool)Field(h.Control,"drainingRequests"));
            string status=((TextBlock)h.Control.FindName("StatusLabel")).Text;
            StatusObservations.Add("queued=0; status="+status);
            Check("zero queued requests retain the primary CLI error without unrelated queue notice",status.Contains("controlled primary status error") && !status.Contains("대기 요청") && h.Count==0);
        });
        Scenario("failure with queued request appends without masking primary error",h=>{
            h.Send("FAIL ACTIVE");h.Send("NEVER RUN");h.Input.Text="NEXT DRAFT";
            h.Provider.Calls[0].Completion.SetException(new InvalidOperationException("controlled primary status error"));Until(()=>!h.Busy && !(bool)Field(h.Control,"drainingRequests"));
            string status=((TextBlock)h.Control.FindName("StatusLabel")).Text;
            StatusObservations.Add("queued=1; status="+status);
            Check("queued cancellation preserves primary error then appends count while retaining draft",status.Contains("controlled primary status error") && status.IndexOf("controlled primary status error",StringComparison.Ordinal)<status.IndexOf("대기 요청",StringComparison.Ordinal) && status.Contains("1개 취소") && h.Count==0 && h.Provider.Calls.Count==1 && h.Input.Text=="NEXT DRAFT");
        });
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunPerformanceOnly
    /// desc         : 이전 배포와 같은 호스트 조건으로 탭 전환 시간을 측정한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void RunPerformanceOnly()
    {
        root=Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH"); Scenario("tab timing",Performance);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Performance
    /// desc         : 변경 없는 탭 전환의 저장 중복과 변경한 초안 저장을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Performance(TestHost h)
    {
        Invoke(h.Control,"PersistCurrentConversation");
        var tabs=(TabControl)h.Control.FindName("ProviderChoice");
        tabs.SelectedIndex=1; tabs.SelectedIndex=0;
        var store=new MarkdownWorkspaceStore(); var paths=Directory.GetFiles(store.RootPath,"*.md",SearchOption.AllDirectories).Where(p=>Path.GetFileName(p)!="instructions.md").ToArray();
        var stamps=paths.ToDictionary(p=>p,p=>File.GetLastWriteTimeUtc(p));
        var watch=Stopwatch.StartNew();
        for(int index=0;index<40;index++) { tabs.SelectedIndex=index%2==0?1:0; }
        watch.Stop(); SwitchElapsedMilliseconds=watch.ElapsedMilliseconds;
        SwitchDiskWrites=paths.Count(p=>File.GetLastWriteTimeUtc(p)!=stamps[p]);
        Check("40 unchanged provider switches avoid repeated record writes",SwitchDiskWrites==0);
        Invoke(h.Control,"PersistCurrentConversation");
        h.Input.Text="changed draft"; Invoke(h.Control,"PersistCurrentConversation");
        Check("changed draft still persists immediately",new MarkdownWorkspaceStore().LoadOpen("Claude").Any(r=>r.Draft=="changed draft"));
    }

    private sealed class TestHost : IDisposable
    {
        internal readonly AssistantControl Control; internal readonly FakeProvider Provider=new FakeProvider();
        internal readonly FakeDocument Document=new FakeDocument(); internal bool IsDisposed;
        internal TextBox Input { get{return Text("Instruction");} }
        internal ChatSession Chat { get{return (ChatSession)Property(Control,"ActiveChat");} }
        internal bool Busy {get{return (bool)Field(Control,"busy");}}
        internal int Count {get{return (int)Field(Control,"queuedRequests").GetType().GetProperty("Count").GetValue(Field(Control,"queuedRequests"),null);}}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TestHost
        /// desc         : 격리된 요청 큐 검사에서 TestHost 경로를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal TestHost(int number) {
            Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH",Path.Combine(root,"scenario-"+number));
            new AssistantSettingsStore().Save(new AssistantSettings{HideWelcome=true,ShowHistory=false,EnableCompletionNotifications=false});
            Control=new AssistantControl();Set(Control,"scmLookupStarted",true);Control.Initialize(Document,Provider);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Text
        /// desc         : 격리된 요청 큐 검사에서 Text 경로를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal TextBox Text(string name){return (TextBox)Control.FindName(name);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Send
        /// desc         : 격리된 요청 큐 검사에서 Send 경로를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Send(string text){Input.Text=text;Key(ModifierKeys.None);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Key
        /// desc         : 격리된 요청 큐 검사에서 Key 경로를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Key(ModifierKeys modifiers){Invoke(Control,"HandlePromptKey",System.Windows.Input.Key.Enter,modifiers);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Click
        /// desc         : 격리된 요청 큐 검사에서 Click 경로를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Click(string method){Invoke(Control,method,Control,new RoutedEventArgs());}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 격리된 요청 큐 검사에서 Dispose 경로를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose(){if(!IsDisposed)Control.Dispose();}
    }

    private sealed class FakeDocument : IEditorDocument,IActiveDocumentService,ISourceNavigationService
    {
        internal string Content="original";internal int Writes;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetActiveDocument
        /// desc         : 격리된 요청 큐 검사에서 GetActiveDocument 경로를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public IEditorDocument GetActiveDocument(){return this;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetProjectDirectory
        /// desc         : 격리된 요청 큐 검사에서 GetProjectDirectory 경로를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string GetProjectDirectory(){return Path.GetFullPath(Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH"));}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigateTo
        /// desc         : 격리된 요청 큐 검사에서 NavigateTo 경로를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void NavigateTo(string path,int line){}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Capture
        /// desc         : 문서와 선택 영역을 불변 스냅샷으로 제공한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot Capture(){return new DocumentSnapshot(Path.Combine(GetProjectDirectory(),"sample.cs"),Content,"disk-one","project",false,false,DocumentSelection.FromOffsets(Content,0,Math.Min(4,Content.Length)));}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Replace
        /// desc         : 원본 일치와 명시 적용 뒤에만 합성 문서를 변경한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Replace(DocumentSnapshot expected,string content){expected.RequireUnchanged(Capture());Content=content;Writes++;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Save
        /// desc         : 격리된 요청 큐 검사에서 Save 경로를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Save(DocumentSnapshot expected){expected.RequireUnchanged(Capture());}
    }

    private sealed class Call
    {
        internal string Provider,Executable,Instruction;internal DocumentSnapshot Source;internal ChatRequestContext Context;
        internal CancellationToken Token;internal readonly TaskCompletionSource<EditProposal> Completion=new TaskCompletionSource<EditProposal>();
    }
    private sealed class FakeProvider : IAiContextEditProvider
    {
        internal bool IgnoreCancellation;internal readonly List<Call> Calls=new List<Call>();
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GenerateAsync
        /// desc         : 격리된 요청 큐 검사에서 GenerateAsync 경로를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public Task<EditProposal> GenerateAsync(string provider,string executable,DocumentSnapshot source,string instruction,CancellationToken token){return GenerateWithContextAsync(provider,executable,source,instruction,null,token);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GenerateWithContextAsync
        /// desc         : 요청 매개변수를 보관하고 검사자가 완료·실패·취소 시점을 제어한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public Task<EditProposal> GenerateWithContextAsync(string provider,string executable,DocumentSnapshot source,string instruction,ChatRequestContext context,CancellationToken token){var call=new Call{Provider=provider,Executable=executable,Source=source,Instruction=instruction,Context=context,Token=token};Calls.Add(call);if(!IgnoreCancellation)token.Register(()=>call.Completion.TrySetCanceled());return call.Completion.Task;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Complete
        /// desc         : 고정 원본으로 만든 검증 가능한 수정안 또는 변경 없는 답변을 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Complete(int index,bool changes){var call=Calls[index];call.Completion.SetResult(EditProposal.Parse(call.Source,changes?"{\"summary\":\"edited\",\"edits\":[{\"old_text\":\"original\",\"new_text\":\"changed\"}]}":"{\"summary\":\"done\",\"edits\":[]}"));}
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Field
    /// desc         : 격리된 요청 큐 검사에서 Field 경로를 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Field(object target,string name){return target.GetType().GetField(name,Private).GetValue(target);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Set
    /// desc         : 격리된 요청 큐 검사에서 Set 경로를 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Set(object target,string name,object value){target.GetType().GetField(name,Private).SetValue(target,value);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Property
    /// desc         : 격리된 요청 큐 검사에서 Property 경로를 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Property(object target,string name){return target.GetType().GetProperty(name,Private).GetValue(target,null);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Invoke
    /// desc         : 격리된 요청 큐 검사에서 Invoke 경로를 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Invoke(object target,string name,params object[] args){return target.GetType().GetMethod(name,Private).Invoke(target,args);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 각 기대 조건의 성공·실패 이름을 결과에 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name,bool result){if(result)Passed.Add(name);else Failed.Add(name);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Until
    /// desc         : WPF Dispatcher를 처리하며 조건을 제한 시간 동안 기다린다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Until(Func<bool> done){var watch=Stopwatch.StartNew();while(!done()&&watch.ElapsedMilliseconds<5000){var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(5)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}if(!done())throw new TimeoutException("WPF request did not reach expected state");}
}


