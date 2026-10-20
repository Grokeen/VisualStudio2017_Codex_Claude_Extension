using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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
/// name         : ConcurrentRoomChecks
/// desc         : 실제 RuntimeEditProvider 분기로 들어가 방별 실제 합성 CLI 프로세스·취소·오류를 무창 검사한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK068 C1 독립 하네스, 기존 FakeProvider FIFO 근거와 분리.
/// </summary>
/// ---------------------------------------------------------------------
public static class ConcurrentRoomChecks
{
    public static readonly List<string> Checks = new List<string>(), Errors = new List<string>();
    public static readonly List<object> Observations = new List<object>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string root;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 자기 WPF dispatcher에서 실제 방별 경로의 네 핵심 시나리오만 실행한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : Window.Show·Popup·OS입력·AI로그인 호출 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run(string directory)
    {
        root = directory;
        Dispatcher.CurrentDispatcher.UnhandledException += (sender, args) => { Errors.Add("Dispatcher product exception: " + args.Exception); args.Handled = true; };
        Scenario("complete", Complete);
        Scenario("cancel", Cancel);
        Scenario("error", ErrorOwnership);
        Scenario("dispose", DisposeBoth);
        Scenario("proposal-queue", ProposalQueue);
        Scenario("proposal-pending-stop", PendingStop);
        Scenario("proposal-source-guard", SourceGuard);
        Scenario("proposal-approve", ApproveResume);
        Scenario("usage-role", UsageRole);
        Scenario("scope-reverse", ScopeReverse);
        Scenario("scope-external-cancel", ScopeExternalCancel);
        Scenario("scope-dispose", ScopeDispose);
        Scenario("readonly-initial", ReadOnlyInitial);
        Scenario("readonly-original-B", ReadOnlyOriginalB);
        Scenario("readonly-known-temporary", ReadOnlyKnownTemporary);
        Scenario("readonly-unknown-scope", ReadOnlyUnknownScope);
        Scenario("readonly-unknown-path", ReadOnlyUnknownPath);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Scenario
    /// desc         : 시나리오마다 설정·workspace·프로젝트·프로세스 게이트를 새로 격리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실패 원문을 보존하고 자기 프로세스만 정리한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Scenario(string name, Action<Host> action)
    { Host host = null; try { host = new Host(Path.Combine(root, name)); action(host); } catch (Exception error) { Errors.Add(name + ": " + error + (host == null ? "" : " | status=" + host.Status + " | runs=" + host.Runs)); } finally { if (host != null) host.Dispose(); } }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Complete
    /// desc         : A 실행 중 B를 시작해 먼저 완료하고 두 방의 답변·초안·프로젝트를 비교한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 같은 큐의 순차 완료를 동시 실행으로 오인하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Complete(Host h)
    {
        var a = h.Record; var chatA = h.Chat; h.Send("REQ_A_COMPLETE"); int pidA = h.Started("REQ_A_COMPLETE");
        h.Input.Text = "DRAFT_A"; h.NewRoom(); var b = h.Record; var chatB = h.Chat;
        Check("complete: A is still running after real conversation creation", a.Id != b.Id && a.State == ConversationRunState.Running && h.Runs == 1);
        h.Send("REQ_B_COMPLETE"); int pidB = h.Started("REQ_B_COMPLETE"); h.Input.Text = "DRAFT_B";
        Check("complete: two distinct owned CLI PIDs are live together", pidA != pidB && Alive(pidA) && Alive(pidB) && h.Runs == 2);
        h.Release("REQ_B_COMPLETE"); Until(() => b.State == ConversationRunState.Completed && h.Runs == 1);
        Check("complete: B finishes without completing A", a.State == ConversationRunState.Running && Alive(pidA) && chatB.Messages.Any(m => m.Text == "final-REQ_B_COMPLETE"));
        Check("complete: B completion preserves its current draft", h.Input.Text == "DRAFT_B");
        h.Release("REQ_A_COMPLETE"); Until(() => a.State == ConversationRunState.Completed && h.Runs == 0);
        Check("complete: late A answer is confined to A", chatA.Messages.Any(m => m.Text == "final-REQ_A_COMPLETE") && !chatB.Messages.Any(m => m.Text.Contains("REQ_A_COMPLETE")));
        Check("complete: late A does not replace shown B status or draft", h.Input.Text == "DRAFT_B" && h.Status.Contains("완료") && !h.Status.Contains("실패"));
        Observations.Add(new { Scenario = "complete-before-return-A", RoomA = a.Id, RoomB = b.Id, DraftA = chatA.Draft, DraftB = chatB.Draft, UiDraft = h.Input.Text, UnreadA = a.HasUnread });
        h.Select(a); Check("complete: switching back restores A draft", h.Record.Id == a.Id && h.Input.Text == "DRAFT_A");
        Observations.Add(new { Scenario = "complete-after-return-A", RoomA = a.Id, RoomB = b.Id, DraftA = chatA.Draft, DraftB = chatB.Draft, UiDraft = h.Input.Text, UnreadA = a.HasUnread });
        h.Observe("complete", a, b);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Cancel
    /// desc         : 선택된 B의 Stop이 B만 종료하고 A는 계속 완료할 수 있는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : PID별 실제 종료와 소유 방 상태를 함께 검사한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Cancel(Host h)
    {
        var a = h.Record; h.Send("REQ_A_CANCEL"); int pidA = h.Started("REQ_A_CANCEL"); h.NewRoom(); var b = h.Record;
        h.Send("REQ_B_CANCEL"); int pidB = h.Started("REQ_B_CANCEL"); h.Click("StopClicked");
        Until(() => b.State == ConversationRunState.Stopped && !Alive(pidB));
        Check("cancel: B Stop kills only its owned PID", Alive(pidA) && a.State == ConversationRunState.Running && h.Runs == 1);
        Check("cancel: no successful B answer is fabricated", !h.Chat.Messages.Any(m => m.Text == "final-REQ_B_CANCEL"));
        h.Release("REQ_A_CANCEL"); Until(() => a.State == ConversationRunState.Completed && h.Runs == 0);
        Check("cancel: A still completes after independent B cancellation", a.State == ConversationRunState.Completed && b.State == ConversationRunState.Stopped);
        h.Observe("cancel", a, b);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ErrorOwnership
    /// desc         : 숨겨진 A의 CLI 오류가 현재 B의 상태·대화 내용을 오염시키지 않는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 오류/성공 프로세스는 별도 게이트로 종료한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ErrorOwnership(Host h)
    {
        var a = h.Record; var chatA = h.Chat; h.Send("REQ_A_ERROR"); h.Started("REQ_A_ERROR"); h.NewRoom(); var b = h.Record; var chatB = h.Chat;
        h.Send("REQ_B_WAIT"); int pidB = h.Started("REQ_B_WAIT"); h.Input.Text = "ERROR_CASE_B_DRAFT";
        h.Release("REQ_A_ERROR"); Until(() => a.State == ConversationRunState.Failed && h.Runs == 1);
        Check("error: background A failure leaves B running and visible status intact", b.State == ConversationRunState.Running && Alive(pidB) && !h.Status.Contains("실패") && h.Input.Text == "ERROR_CASE_B_DRAFT");
        Check("error: failure text belongs only to A conversation", chatA.Messages.Any(m => m.Role == "system" && m.Text.Contains("요청 실패")) && !chatB.Messages.Any(m => m.Text.Contains("요청 실패")));
        h.Release("REQ_B_WAIT"); Until(() => b.State == ConversationRunState.Completed && h.Runs == 0);
        Check("error: unrelated B succeeds after A error", b.State == ConversationRunState.Completed && a.State == ConversationRunState.Failed);
        h.Observe("error", a, b);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DisposeBoth
    /// desc         : 두 방이 실행 중일 때 도구 창 Dispose가 두 소유 CLI를 종료하는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 다른 IDE/CLI 프로세스에는 접근하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void DisposeBoth(Host h)
    {
        var a = h.Record; h.Send("REQ_A_DISPOSE"); int pidA = h.Started("REQ_A_DISPOSE"); h.NewRoom(); var b = h.Record;
        h.Send("REQ_B_DISPOSE"); int pidB = h.Started("REQ_B_DISPOSE");
        h.Control.Dispose(); h.ProductDisposed = true;
        Until(() => !Alive(pidA) && !Alive(pidB) && h.Runs == 0);
        Check("dispose: both independently owned native children terminate", !Alive(pidA) && !Alive(pidB));
        Check("dispose: neither room fabricates completed success", a.State != ConversationRunState.Completed && b.State != ConversationRunState.Completed);
        h.Observe("dispose", a, b);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProposalQueue
    /// desc         : 실제 제안 대기 중8건 제한·다른방실행·명시skip·후속history·활성Stop을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 비활성 버튼의 handler 관측과 사용자 affordance 결과를 분리한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ProposalQueue(Host h)
    {
        h.ProposalMode(); var a = h.Record; var chatA = h.Chat;
        h.Send("REQ_A_EDIT_QUEUE"); h.Started("REQ_A_EDIT_QUEUE"); h.Release("REQ_A_EDIT_QUEUE"); Until(() => h.Proposals == 1 && h.Runs == 0);
        Check("proposal: result waits for approval without editing source", h.Document.Get().Writes == 0 && h.Document.Get().Content == "alpha");
        for (int i = 1; i <= 8; i++) h.Send("REQ_A_QUEUE_" + i);
        Check("proposal: eight pending requests are accepted per room", h.Queue(a.Id) == 8 && h.Runs == 0);
        int messageCount = chatA.Messages.Count; h.Send("REQ_A_QUEUE_9_DENIED");
        Check("proposal: ninth rejection preserves input and does not add submitted message", h.Queue(a.Id) == 8 && h.Input.Text == "REQ_A_QUEUE_9_DENIED" && chatA.Messages.Count == messageCount);
        h.NewRoom(); var b = h.Record; h.Send("REQ_B_PROPOSAL"); h.Started("REQ_B_PROPOSAL");
        Check("proposal: B starts while A retains pending proposal and eight requests", h.Runs == 1 && h.Proposals == 1 && h.Queue(a.Id) == 8);
        h.Release("REQ_B_PROPOSAL"); Until(() => b.State == ConversationRunState.Completed && h.Runs == 0);
        h.Select(a);
        Check("proposal: restored A skip button is enabled for its pending queue", ((Button)h.Control.FindName("DiscardProposalButton")).IsEnabled);
        h.Click("DiscardProposalAndContinueClicked"); h.Started("REQ_A_QUEUE_1");
        Check("proposal: explicit skip handler resumes only oldest A without source writes", h.Queue(a.Id) == 7 && h.Document.Get().Writes == 0 && h.Proposals == 0);
        string request = File.ReadAllText(Path.Combine(h.Gates, "REQ_A_QUEUE_1.start.json"));
        Check("proposal: next model context contains previous answer and excludes future queue messages", request.Contains("final-REQ_A_EDIT_QUEUE") && !request.Contains("REQ_A_QUEUE_2") && !request.Contains("REQ_A_QUEUE_8") && !request.Contains("REQ_A_QUEUE_9_DENIED"));
        h.Click("StopClicked"); Until(() => a.State == ConversationRunState.Stopped && h.Runs == 0);
        Check("proposal: active Stop cancels current and remaining same-room requests", h.Queue(a.Id) == 0 && b.State == ConversationRunState.Completed);
        h.Observe("proposal-queue", a, b);
        AgentReselectProposal(h);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AgentReselectProposal
    /// desc         : 실제 현재 AI 재선택이 대기 수정안·비교 화면과 명시적 건너뛰기 재개를 보존하는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : C5 actual IDE ROOM-QUEUE03의 pendingProposal 소실 회귀를 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void AgentReselectProposal(Host h)
    {
        var a = h.Record; h.Send("REQ_A_EDIT_RESELECT"); h.Started("REQ_A_EDIT_RESELECT"); h.Release("REQ_A_EDIT_RESELECT");
        Until(() => h.Proposals == 1 && h.Runs == 0);
        object proposal = Field(h.Control, "pendingProposal");
        string original = ((TextBox)h.Control.FindName("OriginalPreview")).Text;
        string proposed = ((TextBox)h.Control.FindName("ProposedPreview")).Text;
        string diff = ((TextBox)h.Control.FindName("DiffPreview")).Text;
        var store = (ConversationCollaborationStore)Field(h.Control, "collaborationStore");
        bool activated = (bool)Invoke(h.Control, "ActivateAssignedAgent", store.EnsureAgent(a));
        Check("agent-reselect: same assigned AI restores exact pending proposal and original comparison previews", activated && h.Record.Id == a.Id && proposal != null && ReferenceEquals(Field(h.Control, "pendingProposal"), proposal) && h.Proposals == 1 && ((TextBox)h.Control.FindName("OriginalPreview")).Text == original && ((TextBox)h.Control.FindName("ProposedPreview")).Text == proposed && ((TextBox)h.Control.FindName("DiffPreview")).Text == diff);
        h.Send("REQ_A_RESELECT_NEXT");
        Check("agent-reselect: newly queued request keeps proposal skip enabled without source writes", h.Queue(a.Id) == 1 && h.Proposals == 1 && ((Button)h.Control.FindName("DiscardProposalButton")).IsEnabled && h.Document.Get().Writes == 0);
        h.Click("DiscardProposalAndContinueClicked"); h.Started("REQ_A_RESELECT_NEXT");
        Check("agent-reselect: explicit skip resumes actual next CLI without applying pending edits", h.Queue(a.Id) == 0 && h.Proposals == 0 && h.Document.Get().Writes == 0 && h.Document.Get().Content == "alpha" && h.Runs == 1);
        h.Release("REQ_A_RESELECT_NEXT"); Until(() => a.State == ConversationRunState.Completed && h.Runs == 0);
        Observations.Add(new { Scenario = "agent-reselect", SameAgentActivated = activated, Room = a.Id, SourceWrites = h.Document.Get().Writes, Source = h.Document.Get().Content, PendingProposals = h.Proposals, Queue = h.Queue(a.Id), State = a.State.ToString() });
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PendingStop
    /// desc         : CLI가 없고 제안만 대기 중일 때도 자기 방의 대기를 취소할 수 있어야 한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : Stop UI 상태와 실제 handler의 queue 변경을 각각 기록한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void PendingStop(Host h)
    {
        h.ProposalMode(); var a = h.Record; h.Send("REQ_A_EDIT_STOP"); h.Started("REQ_A_EDIT_STOP"); h.Release("REQ_A_EDIT_STOP"); Until(() => h.Proposals == 1 && h.Runs == 0);
        h.Send("REQ_A_QUEUED_STOP");
        Check("pending-stop: stop affordance is enabled with queued work behind proposal", ((Button)h.Control.FindName("StopButton")).IsEnabled);
        h.Click("StopClicked");
        Check("pending-stop: explicit Stop cancels queued request even without active CLI", h.Queue(a.Id) == 0);
        Observations.Add(new { Scenario = "pending-stop", QueueAfterStop = h.Queue(a.Id), PendingProposals = h.Proposals, StopEnabled = ((Button)h.Control.FindName("StopButton")).IsEnabled, SourceWrites = h.Document.Get().Writes, Status = h.Status });
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SourceGuard
    /// desc         : 기다리는 동안 소스가 외부에서 바뀌면 stale 요청을 CLI로 보내지 않아야 한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 외부 변경은 합성 메모리 문서에만 적용한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void SourceGuard(Host h)
    {
        h.ProposalMode(); var a = h.Record; h.Send("REQ_A_EDIT_GUARD"); h.Started("REQ_A_EDIT_GUARD"); h.Release("REQ_A_EDIT_GUARD"); Until(() => h.Proposals == 1 && h.Runs == 0);
        h.Send("REQ_A_STALE_GUARD"); h.Document.Get().Content = "external-change";
        h.Click("DiscardProposalAndContinueClicked"); Until(() => a.State == ConversationRunState.Failed && h.Runs == 0);
        Check("source-guard: stale queued source fails before any new CLI request", !File.Exists(Path.Combine(h.Gates, "REQ_A_STALE_GUARD.start.json")) && h.Document.Get().Writes == 0 && h.Document.Get().Content == "external-change");
        Check("source-guard: failed predecessor clears remaining queued work", h.Queue(a.Id) == 0);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ApproveResume
    /// desc         : 실제 검토 승인 전 차단·승인 적용 뒤 stale 대기 요청 거부와 새 원문 재제출을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 소스 보호로 거부된 재개를 실행 성공으로 바꾸지 않는다.
    ///                2026-10-20 CodexOrig : Root 지시에 따라 TASK055 필수 원본 고정 계약으로 기대값 교정.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ApproveResume(Host h)
    {
        h.ProposalMode(); var a = h.Record; h.AssignRole("ROLE_A_PROPOSAL");
        h.Send("REQ_A_EDIT_APPROVE"); h.Started("REQ_A_EDIT_APPROVE");
        string role = Convert.ToString(h.Prompt("REQ_A_EDIT_APPROVE")["agent_context"]);
        Check("role: proposal CLI receives assigned own role", role.Contains("ROLE_A_PROPOSAL"));
        h.Release("REQ_A_EDIT_APPROVE"); Until(() => h.Proposals == 1 && h.Runs == 0);
        h.Send("REQ_A_AFTER_APPROVE"); h.Click("ApplyClicked");
        Check("approve: unchecked review never modifies source or removes pending proposal", h.Document.Get().Writes == 0 && h.Proposals == 1 && h.Queue(a.Id) == 1);
        ((CheckBox)h.Control.FindName("ReviewApproval")).IsChecked = true; h.Click("ApplyClicked");
        Until(() => a.State == ConversationRunState.Failed || File.Exists(Path.Combine(h.Gates, "REQ_A_AFTER_APPROVE.start.json")));
        Check("approve: explicit approval applies original proposal exactly once", h.Document.Get().Content == "beta" && h.Document.Get().Writes == 1 && h.Proposals == 0);
        bool started = File.Exists(Path.Combine(h.Gates, "REQ_A_AFTER_APPROVE.start.json"));
        Check("approve: stale successor is rejected before CLI after approved source change", !started && a.State == ConversationRunState.Failed && h.Queue(a.Id) == 0);
        Check("approve: stale refusal preserves approved source without additional writes", h.Document.Get().Content == "beta" && h.Document.Get().Writes == 1 && h.Runs == 0);
        Observations.Add(new { Scenario = "approve-resume", NextCliStarted = started, State = a.State.ToString(), Queue = h.Queue(a.Id), Status = h.Status, Source = h.Document.Get().Content, Writes = h.Document.Get().Writes, PendingProposals = h.Proposals });
        if (started) { h.Started("REQ_A_AFTER_APPROVE"); h.Release("REQ_A_AFTER_APPROVE"); Until(() => h.Runs == 0); }
        h.Send("REQ_A_FRESH_APPROVAL"); h.Started("REQ_A_FRESH_APPROVAL");
        Check("approve: newly submitted request uses explicitly approved new source", Convert.ToString(h.Prompt("REQ_A_FRESH_APPROVAL")["source_text"]) == "beta");
        h.Release("REQ_A_FRESH_APPROVAL"); Until(() => a.State == ConversationRunState.Completed && h.Runs == 0);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : UsageRole
    /// desc         : 다른 방의 완료 토큰과 역할을 실제 CLI wire·표시 모델로 구분한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실제 OS 알림이나 Popup을 띄우지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void UsageRole(Host h)
    {
        var a = h.Record; h.AssignRole("ROLE_A_FULL"); h.Send("REQ_A_USAGE_ROLE"); h.Started("REQ_A_USAGE_ROLE");
        string contextA = Convert.ToString(h.Prompt("REQ_A_USAGE_ROLE")["agent_context"]);
        h.NewRoom(); var b = h.Record; h.AssignRole("ROLE_B_FULL"); h.Send("REQ_B_USAGE_ROLE"); h.Started("REQ_B_USAGE_ROLE");
        string contextB = Convert.ToString(h.Prompt("REQ_B_USAGE_ROLE")["agent_context"]);
        Check("role: full-access A receives its own assigned role without B", contextA.Contains("ROLE_A_FULL") && !contextA.Contains("ROLE_B_FULL"));
        Check("role: full-access B receives its own assigned role without A", contextB.Contains("ROLE_B_FULL") && !contextB.Contains("ROLE_A_FULL"));
        h.Release("REQ_B_USAGE_ROLE"); Until(() => b.State == ConversationRunState.Completed && h.Runs == 1);
        Check("usage: shown B completion exposes B native token counts", h.Usage != null && h.Usage.InputTokens == 53 && h.Usage.OutputTokens == 5);
        Observations.Add(new { Scenario = "usage-B-completed", Usage = h.Usage, Tooltip = h.UsageTooltip });
        h.Release("REQ_A_USAGE_ROLE"); Until(() => a.State == ConversationRunState.Completed && h.Runs == 0);
        Check("usage: hidden A completion does not overwrite shown B counts", h.Usage != null && h.Usage.InputTokens == 53 && h.Usage.OutputTokens == 5);
        Observations.Add(new { Scenario = "usage-A-background", Usage = h.Usage, Tooltip = h.UsageTooltip });
        h.Select(a);
        Check("usage: selecting completed A restores A native token counts", h.Usage != null && h.Usage.InputTokens == 31 && h.Usage.OutputTokens == 3);
        Observations.Add(new { Scenario = "usage-return-A", Usage = h.Usage, Tooltip = h.UsageTooltip, AgentContextA = contextA, AgentContextB = contextB });
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ScopeReverse
    /// desc         : 합성 보호 범위를 두 실제 CLI가 공유하고 B 역순 완료 뒤에도 A를 보호하는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실제 IDE 보호 검증과 합성 수명 계약을 명확히 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ScopeReverse(Host h)
    {
        var a = h.Record; h.Send("REQ_A_SCOPE_REVERSE"); int pidA = h.Started("REQ_A_SCOPE_REVERSE");
        h.NewRoom(); var b = h.Record; h.Send("REQ_B_SCOPE_REVERSE"); h.Started("REQ_B_SCOPE_REVERSE");
        Check("scope-reverse: two native runs share exactly one active fake protection scope", h.Runs == 2 && h.Document.BeginCalls == 1 && h.Document.DisposeInvocations == 0 && h.Document.ActiveScopes == 1 && h.Document.Protected);
        h.Release("REQ_B_SCOPE_REVERSE"); Until(() => b.State == ConversationRunState.Completed && h.Runs == 1);
        Check("scope-reverse: B completion leaves A native PID and protection active without restore", Alive(pidA) && a.State == ConversationRunState.Running && h.Document.BeginCalls == 1 && h.Document.DisposeInvocations == 0 && h.Document.ActiveScopes == 1 && h.Document.Protected);
        h.Release("REQ_A_SCOPE_REVERSE"); Until(() => a.State == ConversationRunState.Completed && h.Runs == 0);
        Check("scope-reverse: last A completion restores fake protection exactly once", h.Document.BeginCalls == 1 && h.Document.DisposeInvocations == 1 && h.Document.ActiveScopes == 0 && !h.Document.Protected);
        h.ObserveScope("scope-reverse", a, b);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ScopeExternalCancel
    /// desc         : 합성 문서 서비스가 제공한 외부 변경 callback이 두 소유 전체 CLI를 취소하는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 제품 cancellation 필드를 직접 수정하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ScopeExternalCancel(Host h)
    {
        var a = h.Record; var chatA = h.Chat; h.Send("REQ_A_SCOPE_EXTERNAL"); int pidA = h.Started("REQ_A_SCOPE_EXTERNAL");
        h.NewRoom(); var b = h.Record; var chatB = h.Chat; h.Send("REQ_B_SCOPE_EXTERNAL"); int pidB = h.Started("REQ_B_SCOPE_EXTERNAL");
        Check("scope-external: two native runs register one shared fake callback", h.Document.BeginCalls == 1 && h.Document.ActiveScopes == 1 && h.Document.Protected);
        h.Document.SignalExternalChange();
        Until(() => !Alive(pidA) && !Alive(pidB) && h.Runs == 0);
        Check("scope-external: service callback cancels both owning native children", a.State == ConversationRunState.Stopped && b.State == ConversationRunState.Stopped && !Alive(pidA) && !Alive(pidB));
        Check("scope-external: cancellation restores fake protection once without fabricated success", h.Document.BeginCalls == 1 && h.Document.DisposeInvocations == 1 && !h.Document.Protected && h.Document.ActiveScopes == 0 && !chatA.Messages.Any(m => m.Text == "final-REQ_A_SCOPE_EXTERNAL") && !chatB.Messages.Any(m => m.Text == "final-REQ_B_SCOPE_EXTERNAL"));
        h.ObserveScope("scope-external", a, b);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ScopeDispose
    /// desc         : 도구 창 Dispose가 두 실제 CLI를 종료한 뒤 공유 합성 보호 상태를 한 번 복원하는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : OS·실제 IDE 문서의 읽기전용 속성은 조작하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ScopeDispose(Host h)
    {
        var a = h.Record; h.Send("REQ_A_SCOPE_DISPOSE"); int pidA = h.Started("REQ_A_SCOPE_DISPOSE");
        h.NewRoom(); var b = h.Record; h.Send("REQ_B_SCOPE_DISPOSE"); int pidB = h.Started("REQ_B_SCOPE_DISPOSE");
        Check("scope-dispose: one fake protection scope covers two owned live PIDs", Alive(pidA) && Alive(pidB) && h.Document.BeginCalls == 1 && h.Document.Protected);
        h.Control.Dispose(); h.ProductDisposed = true;
        Until(() => !Alive(pidA) && !Alive(pidB) && h.Runs == 0);
        Check("scope-dispose: product Dispose kills both native children and restores fake scope once", !Alive(pidA) && !Alive(pidB) && h.Document.BeginCalls == 1 && h.Document.DisposeInvocations == 1 && h.Document.ActiveScopes == 0 && !h.Document.Protected);
        h.Control.Dispose(); Pump();
        Check("scope-dispose: repeated product Dispose does not restore fake scope twice", h.Document.DisposeInvocations == 1 && !h.Document.Protected);
        h.ObserveScope("scope-dispose", a, b);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadOnlyInitial
    /// desc         : 처음부터 읽기 전용인 A 문서는 CLI를 시작하지 않고 입력을 보존해야 한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : capture가 readonly를 임의 throw하지 않고 실제 제품 guard를 검사한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ReadOnlyInitial(Host h)
    {
        h.Document.FullAccessDocumentEnabled = true; h.Document.Get().InitialReadOnly = true;
        AssertReadOnlyDenied(h, "REQ_A_READONLY_INITIAL", 0);
        Check("readonly-initial: rejection starts no fake protection scope and preserves original source", h.Document.BeginCalls == 0 && h.Document.Get().InitialReadOnly && h.Document.Get().Content == "alpha" && h.Document.Get().Writes == 0);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadOnlyOriginalB
    /// desc         : A의 공유 보호 범위가 기존 B 읽기 전용 문서의 실행을 허용하지 않아야 한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : A와 B를 보호 시작 전에 등록하고 B의 초기 readonly를 보존한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ReadOnlyOriginalB(Host h)
    {
        h.Document.FullAccessDocumentEnabled = true; var a = h.Record; h.Document.Get();
        h.NewRoom(); var b = h.Record; h.Document.Get().InitialReadOnly = true;
        h.Select(a); h.Send("REQ_A_READONLY_OTHER"); int pidA = h.Started("REQ_A_READONLY_OTHER"); h.Select(b);
        AssertReadOnlyDenied(h, "REQ_B_READONLY_ORIGINAL", 1);
        Check("readonly-original-B: rejection leaves A PID and one protection scope active", Alive(pidA) && a.State == ConversationRunState.Running && h.Document.BeginCalls == 1 && h.Document.DisposeInvocations == 0 && h.Document.Protected);
        Check("readonly-original-B: original B restriction is never certified as temporary", !h.Document.TemporaryProof(h.Document.Get().FilePath) && h.Document.Get().InitialReadOnly && h.Document.Get().Writes == 0);
        h.Release("REQ_A_READONLY_OTHER"); Until(() => a.State == ConversationRunState.Completed && !Alive(pidA));
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadOnlyKnownTemporary
    /// desc         : 처음 쓰기 가능했고 같은 scope가 실제 보호한 A/B 경로만 병렬 CLI를 허용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : path별 positive 계약으로 모든readonly 일괄거부를 성공으로 세지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ReadOnlyKnownTemporary(Host h)
    {
        h.Document.FullAccessDocumentEnabled = true; var a = h.Record; var docA = h.Document.Get();
        h.NewRoom(); var b = h.Record; var docB = h.Document.Get(); h.Select(a);
        h.Send("REQ_A_READONLY_KNOWN"); int pidA = h.Started("REQ_A_READONLY_KNOWN"); h.Select(b);
        Check("readonly-known: B snapshot is readonly only through the registered same-scope path", docB.Capture().IsReadOnly && !docB.InitialReadOnly && h.Document.TemporaryProof(docB.FilePath));
        h.Send("REQ_B_READONLY_KNOWN"); h.Started("REQ_B_READONLY_KNOWN");
        Check("readonly-known: certified temporary B starts alongside A with one shared scope", Alive(pidA) && h.Runs == 2 && h.Document.BeginCalls == 1);
        h.Release("REQ_B_READONLY_KNOWN"); Until(() => b.State == ConversationRunState.Completed && h.Runs == 1);
        Check("readonly-known: B completion leaves both open synthetic documents protected while A runs", docA.Capture().IsReadOnly && docB.Capture().IsReadOnly && h.Document.DisposeInvocations == 0);
        h.Release("REQ_A_READONLY_KNOWN"); Until(() => a.State == ConversationRunState.Completed && h.Runs == 0);
        Check("readonly-known: final completion restores both original writable states", !docA.Capture().IsReadOnly && !docB.Capture().IsReadOnly && h.Document.DisposeInvocations == 1 && !h.Document.TemporaryProof(docB.FilePath));
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadOnlyUnknownScope
    /// desc         : optional 상태 증명을 제공하지 않는 scope의 readonly는 안전하게 거절한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 임시 보호 모델은 같지만 인터페이스를 숨겨 unknown 계약을 검사한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ReadOnlyUnknownScope(Host h)
    {
        h.Document.FullAccessDocumentEnabled = true; h.Document.OmitScopeState = true; var a = h.Record; h.Document.Get();
        h.NewRoom(); var b = h.Record; h.Document.Get(); h.Select(a);
        h.Send("REQ_A_READONLY_UNKNOWN_SCOPE"); int pidA = h.Started("REQ_A_READONLY_UNKNOWN_SCOPE"); h.Select(b);
        Check("readonly-unknown-scope: synthetic document is protected but optional proof is absent", h.Document.Get().Capture().IsReadOnly && !h.Document.TemporaryProof(h.Document.Get().FilePath));
        AssertReadOnlyDenied(h, "REQ_B_READONLY_UNKNOWN_SCOPE", 1);
        Check("readonly-unknown-scope: rejection preserves other A native run and protection", Alive(pidA) && a.State == ConversationRunState.Running && h.Document.DisposeInvocations == 0 && h.Document.BeginCalls == 1 && h.Document.Protected);
        h.Release("REQ_A_READONLY_UNKNOWN_SCOPE"); Until(() => a.State == ConversationRunState.Completed && !Alive(pidA));
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadOnlyUnknownPath
    /// desc         : 공유 보호 시작 뒤 나타난 미등록 경로의 외부 readonly를 임시 보호로 인정하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 아무 path에나 true를 반환하는 fake를 사용하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ReadOnlyUnknownPath(Host h)
    {
        h.Document.FullAccessDocumentEnabled = true; var a = h.Record; h.Document.Get();
        h.Send("REQ_A_READONLY_UNKNOWN_PATH"); int pidA = h.Started("REQ_A_READONLY_UNKNOWN_PATH"); h.NewRoom();
        h.Document.Get().ExternalReadOnly = true;
        Check("readonly-unknown-path: unrelated readonly path has no temporary proof", h.Document.Get().Capture().IsReadOnly && !h.Document.TemporaryProof(h.Document.Get().FilePath));
        AssertReadOnlyDenied(h, "REQ_B_READONLY_UNKNOWN_PATH", 1);
        Check("readonly-unknown-path: rejection preserves A native run and original external restriction", Alive(pidA) && a.State == ConversationRunState.Running && h.Document.DisposeInvocations == 0 && h.Document.Get().ExternalReadOnly && h.Document.Get().Writes == 0);
        h.Release("REQ_A_READONLY_UNKNOWN_PATH"); Until(() => a.State == ConversationRunState.Completed && !Alive(pidA));
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssertReadOnlyDenied
    /// desc         : readonly 거절의 실제 CLI 미시작·입력 원문 보존과 소스 쓰기0을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 오작동으로 시작된 자기 fixture도 PID 증거를 수집해 Dispose 정리한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void AssertReadOnlyDenied(Host h, string id, int existingRuns)
    {
        h.Send(id); string gate = Path.Combine(h.Gates, id + ".start.json");
        Until(() => h.Runs <= existingRuns || File.Exists(gate)); Pump();
        bool started = File.Exists(gate); if (started) h.Started(id);
        Check(id + ": readonly guard rejects before local CLI starts and adds no active run", !started && h.Runs == existingRuns);
        Check(id + ": readonly rejection preserves exact user input", h.Input.Text == id);
        Check(id + ": readonly rejection preserves source without writes", h.Document.Get().Content == "alpha" && h.Document.Get().Writes == 0);
        Observations.Add(new { Scenario = id, CliStarted = started, Runs = h.Runs, ExpectedExistingRuns = existingRuns, Draft = h.Input.Text, Status = h.Status, SourceReadOnly = h.Document.Get().Capture().IsReadOnly, InitialReadOnly = h.Document.Get().InitialReadOnly, ExternalReadOnly = h.Document.Get().ExternalReadOnly, TemporaryProof = h.Document.TemporaryProof(h.Document.Get().FilePath), BeginCalls = h.Document.BeginCalls, DisposeInvocations = h.Document.DisposeInvocations });
    }

    private sealed class Host : IDisposable
    {
        internal readonly AssistantControl Control;
        internal readonly Documents Document;
        internal readonly string DirectoryPath, Gates;
        internal readonly List<int> Pids = new List<int>();
        private readonly Dictionary<string, string> projects = new Dictionary<string, string>();
        internal bool ProductDisposed;
        internal TextBox Input { get { return (TextBox)Control.FindName("Instruction"); } }
        internal ConversationRecord Record { get { return ((Dictionary<string, ConversationRecord>)Field(Control, "activeRecords"))["Claude"]; } }
        internal ChatSession Chat { get { return (ChatSession)Control.GetType().GetProperty("ActiveChat", Private).GetValue(Control, null); } }
        internal string Status { get { return ((TextBlock)Control.FindName("StatusLabel")).Text; } }
        internal int Runs { get { return ((IDictionary)Field(Control, "roomRuns")).Count; } }
        internal int Proposals { get { return ((IDictionary)Field(Control, "roomProposals")).Count; } }
        internal ProviderUsageSnapshot Usage { get { return (ProviderUsageSnapshot)Field(Control, "runtimeUsage"); } }
        internal string UsageTooltip { get { return Convert.ToString(((ToolTip)Field(Control, "runtimeToolTip")).Content); } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Host
        /// desc         : 무창 실제 AssistantControl과 RuntimeEditProvider 및 합성 문서 경계를 만든다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 협업0과 전체액세스로 새 ConcurrentRooms 분기를 실제 진입한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal Host(string path)
        {
            DirectoryPath = path; Directory.CreateDirectory(path); Gates = Path.Combine(path, "gates"); Directory.CreateDirectory(Gates);
            Environment.SetEnvironmentVariable("TASK068_CONCURRENT_GATES", Gates);
            Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", Path.Combine(path, "settings.json"));
            Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", Path.Combine(path, "workspace"));
            new AssistantSettingsStore().Save(new AssistantSettings { HideWelcome = true, ShowHistory = false, EnableCompletionNotifications = false });
            Control = new AssistantControl(); Set(Control, "scmLookupStarted", true);
            Document = new Documents(Path.Combine(path, "project-A")); Control.Initialize(Document, new RuntimeEditProvider());
            Set(Control, "agentFullAccess", true);
            projects[Record.Id] = Document.Project;
            ((TextBox)Control.FindName("ExecutablePath")).Text = Environment.GetEnvironmentVariable("TASK068_CONCURRENT_FIXTURE");
            Check(Path.GetFileName(path) + ": real provider and collaborators zero select concurrent branch", Field(Control, "provider") is RuntimeEditProvider && Record.CollaboratorIds.Length == 0);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Send
        /// desc         : 실제 GenerateClicked를 API로 호출해 입력 캡처와 실행 분기를 통과한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : OS 키/클릭 주입을 하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Send(string value) { Input.Text = value; Click("GenerateClicked"); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Click
        /// desc         : 자기 컨트롤의 실제 이벤트 handler만 명시적으로 호출한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 실제 OS입력/외부 UI를 자동화하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Click(string method) { Invoke(Control, method, Control, new RoutedEventArgs()); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NewRoom
        /// desc         : 실제 새 대화 경로를 호출하고 B의 프로젝트 경로를 따로 지정한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 동일 전역FIFO의 fake 호출로 대체하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void NewRoom() { Invoke(Control, "CreateConversation"); Document.Project = Path.Combine(DirectoryPath, "project-B"); Directory.CreateDirectory(Document.Project); projects[Record.Id] = Document.Project; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Select
        /// desc         : 자기 대화 목록의 실제 선택 이벤트로 원래 대화에 돌아간다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : record/chat 필드를 직접 갈아끼우지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Select(ConversationRecord record) { ((ListBox)Control.FindName("ConversationList")).SelectedItem = record; Document.Project = projects[record.Id]; Pump(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Started
        /// desc         : 요청 고유 원문과 실제 PID·작업 디렉터리의 gate 증거를 기다린다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 단일 start번호 파일을 서로 공유하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal int Started(string id)
        {
            string file = Path.Combine(Gates, id + ".start.json"); Until(() => File.Exists(file));
            var raw = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(file));
            int pid = Convert.ToInt32(raw["Pid"]); Pids.Add(pid);
            bool proposal = raw.ContainsKey("Mode") && Convert.ToString(raw["Mode"]) == "proposal";
            Check(id + ": request id and expected full-access/project or proposal/sandbox boundary preserved", Convert.ToString(raw["Id"]) == id && (proposal ? Convert.ToString(raw["Directory"]) != Document.Project : Convert.ToString(raw["Directory"]) == Document.Project));
            return pid;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Release
        /// desc         : 해당 요청의 합성 결과만 내보내도록 개인 gate를 연다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 실제 AI나 사용자의 파일을 건드리지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Release(string id) { File.WriteAllText(Path.Combine(Gates, id + ".release"), "release"); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProposalMode
        /// desc         : 실제 RuntimeEditProvider 분기는 유지하고 검토모드만 선택한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : FakeProvider를 주입하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void ProposalMode() { Set(Control, "agentFullAccess", false); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Queue
        /// desc         : 특정 방의 실제 pending queue 길이를 관찰한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 전역 legacy FIFO를 세지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal int Queue(string id) { var queues = (IDictionary)Field(Control, "roomQueues"); return queues.Contains(id) ? (int)queues[id].GetType().GetProperty("Count").GetValue(queues[id], null) : 0; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssignRole
        /// desc         : 공개 협업 저장 API로 자기 합성 AI의 담당 역할만 지정한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 고정 이름·팀·collaborator 선택은 보존한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void AssignRole(string role) { var store = (ConversationCollaborationStore)Field(Control, "collaborationStore"); var agent = store.EnsureAgent(Record); store.UpdateAgent(Record.Id, Record.Provider, agent.Name, role, agent.Team, agent.Revision); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Prompt
        /// desc         : 자기 합성 CLI가 실제 받은 구조화 입력을 그대로 읽는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 제품 내부 context를 임의 조립해 성공시키지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal Dictionary<string, object> Prompt(string id) { var raw = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(Gates, id + ".start.json"))); return (Dictionary<string, object>)raw["Prompt"]; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Observe
        /// desc         : 방 상태·현재UI·소유프로세스와 원본 대화 내용을 별도로 남긴다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : usage/notification/proposal은 이번 핵심 검사 밖으로 명시한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Observe(string scenario, ConversationRecord a, ConversationRecord b)
        { Observations.Add(new { Scenario = scenario, RoomA = a.Id, StateA = a.State.ToString(), RoomB = b.Id, StateB = b.State.ToString(), CurrentRoom = Record.Id, CurrentStatus = Status, CurrentDraft = Input.Text, Pids = Pids.ToArray(), Runs = Runs }); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ObserveScope
        /// desc         : 실제 자식 PID와 합성 보호 범위 수명 관측을 함께 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 실제 IDE read-only 복원 검증으로 확대해석하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void ObserveScope(string scenario, ConversationRecord a, ConversationRecord b)
        { Observations.Add(new { Scenario = scenario, ScopeType = "Fake lifetime contract with actual local CLI subprocesses", StateA = a.State.ToString(), StateB = b.State.ToString(), Pids = Pids.ToArray(), Runs = Runs, BeginCalls = Document.BeginCalls, DisposeInvocations = Document.DisposeInvocations, ActiveScopes = Document.ActiveScopes, Protected = Document.Protected, ExternalSignals = Document.ExternalSignals }); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 제품 수명을 끝내고 확인된 자기 fixture PID만 마지막으로 정리한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : fallback 정리가 필요하면 성공으로 숨기지 않고 실패를 기록한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            if (!ProductDisposed) { Control.Dispose(); ProductDisposed = true; }
            for (int i = 0; i < 40 && Pids.Any(Alive); i++) Pump();
            foreach (int pid in Pids.Where(Alive)) { Errors.Add("Owned fixture child survived product Dispose: " + pid); using (var process = Process.GetProcessById(pid)) { if (String.Equals(process.MainModule.FileName, Environment.GetEnvironmentVariable("TASK068_CONCURRENT_FIXTURE"), StringComparison.OrdinalIgnoreCase)) process.Kill(); } }
        }
    }
    private sealed class Documents : IActiveDocumentService, ISourceNavigationService, IFullAccessDocumentService, IExternalEditingDocumentService
    {
        internal string Project;
        internal int BeginCalls, DisposeInvocations, ActiveScopes, ExternalSignals;
        internal bool Protected, FullAccessDocumentEnabled, OmitScopeState;
        private readonly List<FakeScope> scopes = new List<FakeScope>();
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Documents
        /// desc         : 합성 프로젝트 루트만 준비한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 실제 VS문서/선택은 사용하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal Documents(string project) { Project = project; Directory.CreateDirectory(project); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BeginExternalEditing
        /// desc         : 원래 상태를 복원하는 합성 IDE 보호 범위를 만들고 호출 횟수를 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 디스크·실제 IDE 속성·제품 공유scope 필드를 변경하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public IExternalEditingScope BeginExternalEditing(Action stopExecution)
        { BeginCalls++; FakeScope value = OmitScopeState ? new FakeScope(this, stopExecution, Protected) : new KnownFakeScope(this, stopExecution, Protected); scopes.Add(value); ActiveScopes++; Protected = true; return value; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SignalExternalChange
        /// desc         : 살아 있는 범위에 제품이 제공한 외부 변경 중지 callback만 호출한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 실제 문서 이벤트·OS입력·취소 토큰 직접 조작 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void SignalExternalChange() { ExternalSignals++; foreach (var value in scopes.Where(s => !s.IsDisposed).ToArray()) value.Stop(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsProtectedPath
        /// desc         : 자기 scope가 실제 등록한 합성 문서 경로에만 임시 readonly를 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : scope 시작 전에 쓰기 가능했던 경로만 보관한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal bool IsProtectedPath(string path) { return scopes.Any(s => !s.IsDisposed && s.Paths.Contains(path)); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TemporaryProof
        /// desc         : 알려진 optional scope가 현재 경로를 증명하는지 읽기 관측한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 제품의 내부 sharedExternalEditing를 조작하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal bool TemporaryProof(string path) { return scopes.OfType<IExternalEditingScopeState>().Any(s => s.IsTemporarilyReadOnly(path)); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FakeScope
        /// desc         : 합성 보호 상태와 실제 IDisposable 호출만 기록하는 외부 편집 scope다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 동시 제품 경로가 1개 범위를 공유하는지 관찰한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private class FakeScope : IExternalEditingScope
        {
            protected readonly Documents Owner; private readonly bool original; internal readonly Action Stop; internal bool IsDisposed;
            internal readonly HashSet<string> Paths;
            public string Notice { get { return ""; } }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : FakeScope
            /// desc         : 합성 원래 상태와 제품이 요청한 중지 callback을 보관한다.
            /// author       : CodexOrig
            /// create date  : 2026-10-20
            /// update date  : 2026-10-20 CodexOrig : 실제 IDE 접근 없음.
            /// </summary>
            /// ---------------------------------------------------------------------
            internal FakeScope(Documents documents, Action stop, bool prior) { Owner = documents; Stop = stop; original = prior; Paths = new HashSet<string>(documents.documents.Values.Where(d => !d.InitialReadOnly && !d.ExternalReadOnly).Select(d => d.FilePath), StringComparer.OrdinalIgnoreCase); }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : Dispose
            /// desc         : 중복 호출 횟수도 보존하되 합성 상태는 한 번만 복원한다.
            /// author       : CodexOrig
            /// create date  : 2026-10-20
            /// update date  : 2026-10-20 CodexOrig : 실제 VisualStudioExternalEditingScope 동작을 가장하지 않는다.
            /// </summary>
            /// ---------------------------------------------------------------------
            public void Dispose() { Owner.DisposeInvocations++; if (IsDisposed) return; IsDisposed = true; Owner.ActiveScopes--; Owner.Protected = original; }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : KnownFakeScope
        /// desc         : 실제 등록 경로와 원래 쓰기 가능 상태가 있는 경우만 optional 임시 보호를 증명한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 초기·외부readonly 및 미등록path·폐기scope는 false다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class KnownFakeScope : FakeScope, IExternalEditingScopeState
        {
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : KnownFakeScope
            /// desc         : 원래 상태와 실제 등록 경로 집합을 base 합성 scope에 고정한다.
            /// author       : CodexOrig
            /// create date  : 2026-10-20
            /// update date  : 2026-10-20 CodexOrig : 전체 경로 wildcard 승인 없음.
            /// </summary>
            /// ---------------------------------------------------------------------
            internal KnownFakeScope(Documents documents, Action stop, bool prior) : base(documents, stop, prior) { }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : IsTemporarilyReadOnly
            /// desc         : 살아 있는 자기 보호 경로이며 초기·외부 제한이 없는 문서만 인정한다.
            /// author       : CodexOrig
            /// create date  : 2026-10-20
            /// update date  : 2026-10-20 CodexOrig : 등록하지 않은 경로·null·원래readonly 모두 거절한다.
            /// </summary>
            /// ---------------------------------------------------------------------
            public bool IsTemporarilyReadOnly(string filePath) { return !IsDisposed && !String.IsNullOrWhiteSpace(filePath) && Paths.Contains(filePath) && Owner.documents.Values.Any(d => String.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase) && !d.InitialReadOnly && !d.ExternalReadOnly); }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetProjectDirectory
        /// desc         : 제출 시 고정할 합성 프로젝트 경로를 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : A/B 프로젝트는 서로 분리한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string GetProjectDirectory() { return Project; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureFullAccessContext
        /// desc         : 선택 문서 없는 프로젝트 작업의 실제 지원 경로를 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 사용자 편집기를 읽지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot CaptureFullAccessContext() { return FullAccessDocumentEnabled ? Get().Capture() : null; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetActiveDocument
        /// desc         : 전체액세스 검사에서 기존 제안 모드가 잘못 호출되면 실패시킨다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 다른 분기 통과를 새경로 성공으로 세지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public IEditorDocument GetActiveDocument() { return Get(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigateTo
        /// desc         : 이 fixture에서 문서 탐색은 허용하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : OS/IDEUI를 호출하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void NavigateTo(string path, int line) { throw new InvalidOperationException("Navigation not allowed."); }
        private readonly Dictionary<string, MemoryDocument> documents = new Dictionary<string, MemoryDocument>();
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Get
        /// desc         : 각 프로젝트의 별도 메모리 문서 인스턴스를 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : A/B 활성 프로젝트 전환으로 캡처한 문서 경로가 바뀌지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal MemoryDocument Get() { MemoryDocument value; if (!documents.TryGetValue(Project, out value)) documents[Project] = value = new MemoryDocument(this, Project); return value; }
    }
    private sealed class MemoryDocument : IEditorDocument
    {
        private readonly string path; private readonly Documents owner; internal string Content = "alpha"; internal int Writes; internal bool InitialReadOnly, ExternalReadOnly;
        internal string FilePath { get { return path; } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MemoryDocument
        /// desc         : 합성 소스 파일의 안정적인 경로를 보관한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 실제 사용자 파일 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal MemoryDocument(Documents documents, string project) { owner = documents; path = Path.Combine(project, "sample.cs"); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Capture
        /// desc         : 현재 메모리 내용을 실제 제품 스냅샷 형식으로 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 미저장/읽기전용은 false다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot Capture() { return new DocumentSnapshot(path, Content, "disk-fixture", "fixture project", false, InitialReadOnly || ExternalReadOnly || owner.IsProtectedPath(path)); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Replace
        /// desc         : 원본과 실제 승인 경로가 맞을 때만 메모리 소스를 바꾼다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 자동 적용을 시뮬레이션하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Replace(DocumentSnapshot expected, string content) { expected.RequireUnchanged(Capture()); Content = content; Writes++; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Save
        /// desc         : 개인 메모리 문서의 원본 일치만 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 실제 디스크 소스 저장은 하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Save(DocumentSnapshot expected) { expected.RequireUnchanged(Capture()); }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Field
    /// desc         : 고정 제품의 실제 내부 상태를 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 성공을 만들기 위한 상태 변조를 하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Field(object target, string name) { return target.GetType().GetField(name, Private).GetValue(target); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Set
    /// desc         : fixture가 선택한 full-access와 외부SCM탐색차단만 설정한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 방 실행/완료 상태는 제품에 맡긴다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Invoke
    /// desc         : 자기 컨트롤의 실제 메서드만 호출한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 외부UI입력 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Invoke(object target, string method, params object[] args) { return target.GetType().GetMethod(method, Private).Invoke(target, args); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 성공과 실패를 원본 문구로 별도 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 기대치를 낮추거나 실패검사를 제거하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, bool value) { if (value) Checks.Add(name); else Errors.Add(name); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Alive
    /// desc         : gate에 기록된 자기 자식 PID의 실행 여부만 조회한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 시스템 프로세스를 열거하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool Alive(int pid) { try { using (var process = Process.GetProcessById(pid)) return !process.HasExited; } catch (ArgumentException) { return false; } }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Until
    /// desc         : 제한된 시간 동안 자기 WPF 비동기 continuation을 처리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : fixture gate timeout 전에 실패를 남긴다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Until(Func<bool> condition) { var watch = Stopwatch.StartNew(); while (!condition()) { if (watch.ElapsedMilliseconds > 7000) throw new TimeoutException("Expected concurrent product condition not reached."); Pump(); } }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pump
    /// desc         : 창을 표시하지 않고 자기 dispatcher 작업을 잠깐 처리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실제 키/마우스/Popup 호출 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Pump() { var frame = new DispatcherFrame(); var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(30) }; timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
}
