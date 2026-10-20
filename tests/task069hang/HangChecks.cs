using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Controls;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using Task069HangFixture;

namespace Task069HangFixture
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 원문 표시 복구 메서드에 개인 WPF 필드만 제공한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private bool experienceReady = true, disposed;
        private readonly ConversationCollaborationStore collaborationStore;
        private readonly Dictionary<string, ConversationRecord> activeRecords = new Dictionary<string, ConversationRecord>();
        private string selectedProvider = "Codex", verifiedScmAccount = "Fixture", scmDescription = "fixture";
        private DispatcherTimer agentIdentityRetryTimer;
        private int agentIdentityRetries;
        private readonly Button AgentButton = new Button();
        private readonly TextBlock CurrentAgentLabel = new TextBlock();
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantControl
        /// desc         : 원문 표시 복구 메서드에 개인 WPF 필드만 제공한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal AssistantControl(ConversationCollaborationStore store, ConversationRecord record) { collaborationStore = store; activeRecords["Codex"] = record; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Refresh
        /// desc         : 현재 담당자 표시의 제품 메서드를 호출한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Refresh() { RefreshAgentIdentity(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Select
        /// desc         : 선택 대화 변경 뒤 제품 표시 복구를 호출한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Select(ConversationRecord record) { activeRecords["Codex"] = record; RefreshAgentIdentity(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeFixture
        /// desc         : 종료 상태와 제품 타이머 정리를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void DisposeFixture() { disposed = true; StopAgentIdentityRetry(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Tick
        /// desc         : 대기 시간 없이 제품 재시도 한 회를 실행한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Tick() { AgentIdentityRetryTick(this, EventArgs.Empty); }
        internal object Retry { get { return agentIdentityRetryTimer; } }
        internal string Label { get { return CurrentAgentLabel.Text; } }
    }
}

/// ---------------------------------------------------------------------
/// <summary>
/// name         : HangChecks
/// desc         : 무창 협업 경합과 담당자 표시 수명 계약을 확인한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
/// </summary>
/// ---------------------------------------------------------------------
internal static class HangChecks
{
    private static int passed;
    private static string root;
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 개인 저장소에서 정상·경합·전환·종료·오류 검사를 실행한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        root = args[0]; Directory.CreateDirectory(root);
        try
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var store = new ConversationCollaborationStore(Path.Combine(root, "workspace"), () => !dispatcher.CheckAccess());
            var record = Record(); var other = Record();
            store.ConfigureNaming("Codex", "Fixture"); var identity = store.EnsureAgent(record); var second = store.EnsureAgent(other);
            DateTime sentinel = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(store.FilePath, sentinel); string bytes = File.ReadAllText(store.FilePath);
            Check(store.EnsureAgent(record).Revision == identity.Revision && File.GetLastWriteTimeUtc(store.FilePath) == sentinel && File.ReadAllText(store.FilePath) == bytes, "unchanged EnsureAgent preserves bytes, timestamp and revision");
            record.IsOpen = false; var closed = store.EnsureAgent(record);
            Check(closed.Revision > identity.Revision && !store.GetAgent(record.Id, "Codex").IsOpen, "real open-state change still commits once");
            record.IsOpen = true; store.EnsureAgent(record);
            var ui = new AssistantControl(store, record); ui.Refresh(); Check(ui.Label == identity.Name, "uncontended identity remains synchronous");
            using(var owner = new LockOwner(store))
            {
                var watch = Stopwatch.StartNew(); bool busy = false;
                try { store.EnsureAgent(record); } catch(CollaborationBusyException) { busy = true; }
                watch.Stop(); Check(busy && watch.ElapsedMilliseconds < 500, "UI lock contention returns before 500ms");
                int heartbeat = 0; dispatcher.BeginInvoke(new Action(() => heartbeat++)); Pump(15);
                Check(heartbeat == 1 && owner.Held, "dispatcher heartbeat executes while owner still holds lock");
                string beforeSend = File.ReadAllText(store.FilePath);
                bool sendBusy = false; try { store.Send(record.Id, "Codex", other.Id, "Codex", "not sent"); } catch(CollaborationBusyException) { sendBusy = true; }
                Check(sendBusy && File.ReadAllText(store.FilePath) == beforeSend, "contended Send is rejected before mutation");
                string before = File.ReadAllText(store.FilePath); ui.Refresh(); object timer = ui.Retry; ui.Refresh();
                Check(timer != null && Object.ReferenceEquals(timer, ui.Retry), "identity contention starts only one deferred timer");
                ui.Select(other); Check(Object.ReferenceEquals(timer, ui.Retry), "switching conversation retains one retry owner");
                Check(File.ReadAllText(store.FilePath) == before, "all contended identity reads preserve file bytes");
            }
            Pump(350); Check(ui.Label == second.Name && ui.Retry == null, "retry resolves current selected address after lock release");
            Check(store.ListOutbox(record.Id, "Codex").Length == 0, "deferred identity refresh never sends failed messages");
            using(var owner = new LockOwner(store))
            {
                ui.Refresh(); string label = ui.Label; ui.DisposeFixture(); Pump(300);
                Check(ui.Retry == null && ui.Label == label, "Dispose stops deferred callbacks without changing label");
            }
            var bounded = new AssistantControl(store, record);
            using(var owner = new LockOwner(store))
            {
                bounded.Refresh(); for(int i = 0; i < 21; i++) bounded.Tick();
                Check(bounded.Retry == null, "persistent contention ends after exactly twenty retry attempts");
            }
            var corrupt = new AssistantControl(store, record);
            using(var owner = new LockOwner(store)) { corrupt.Refresh(); Check(corrupt.Retry != null, "transient contention schedules retry"); }
            File.WriteAllText(store.FilePath, "{corrupt"); corrupt.Tick(); Check(corrupt.Retry == null, "permanent data failure stops retry");
            File.WriteAllText(store.FilePath, bytes);
            using(var owner = new LockOwner(store))
            {
                var worker = new Thread(() => { Thread.Sleep(160); owner.Release(); }); worker.Start();
                long elapsed = -1; Exception failure = null;
                var reader = new Thread(() => { var watch = Stopwatch.StartNew(); try { store.GetAgent(record.Id, "Codex"); } catch(Exception error) { failure = error; } elapsed = watch.ElapsedMilliseconds; });
                reader.Start(); reader.Join(); worker.Join(); Check(failure == null && elapsed >= 80 && elapsed < 2000, "background caller retains bounded wait and succeeds after release");
            }
            var legacyStore = new ConversationCollaborationStore(Path.Combine(root, "legacy-policy")); legacyStore.EnsureAgent(record);
            using(var owner = new LockOwner(legacyStore))
            {
                var worker = new Thread(() => { Thread.Sleep(160); owner.Release(); }); worker.Start();
                var watch = Stopwatch.StartNew(); legacyStore.GetAgent(record.Id, "Codex"); watch.Stop(); worker.Join();
                Check(watch.ElapsedMilliseconds >= 80 && watch.ElapsedMilliseconds < 2000, "original constructor preserves waiting contract");
            }
            WriteResult(args[0], null); return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error); WriteResult(args[0], error); return 1; }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WriteResult
    /// desc         : 검증 종류와 원문 예외를 개인 결과 파일에 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void WriteResult(string output, Exception error) { File.WriteAllText(Path.Combine(output,"result.json"),new JavaScriptSerializer().Serialize(new { Passed=passed, Failed=error==null?0:1, Error=error==null?null:error.ToString(), ActualProductDll=typeof(ConversationCollaborationStore).Assembly!=typeof(HangChecks).Assembly, ExactUiMethodSubstrings=true, WindowShown=false, IncidentCauseProved=false, Assembly=typeof(ConversationCollaborationStore).Assembly.Location })); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 관찰 결과가 계약과 일치할 때만 통과를 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool value,string name) { if(!value) throw new Exception(name); passed++; Console.WriteLine("PASS: "+name); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Record
    /// desc         : 고객 정보 없는 독립 대화 주소를 만든다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ConversationRecord Record() { return new ConversationRecord { Id=Guid.NewGuid().ToString("N"),Provider="Codex",IsOpen=true }; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pump
    /// desc         : 별도 창 없이 한정 시간의 Dispatcher 콜백을 처리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Pump(int milliseconds) { var frame=new DispatcherFrame(); var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(milliseconds) }; timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame); }
    private sealed class LockOwner : IDisposable
    {
        private readonly ManualResetEvent acquired=new ManualResetEvent(false), release=new ManualResetEvent(false);
        private readonly Thread thread;
        public bool Held { get { return !release.WaitOne(0); } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LockOwner
        /// desc         : 별도 스레드에서 개인 협업 잠금의 소유와 해제를 제어한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        public LockOwner(ConversationCollaborationStore store) { string name=(string)typeof(ConversationCollaborationStore).GetField("mutexName",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(store);thread=new Thread(()=>{using(var mutex=new Mutex(false,name)){mutex.WaitOne();acquired.Set();release.WaitOne();mutex.ReleaseMutex();}});thread.IsBackground=true;thread.Start();if(!acquired.WaitOne(5000))throw new Exception("mutex fixture failed"); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Release
        /// desc         : 잠금 소유 스레드에 해제를 요청한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Release() { release.Set(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 잠금 소유 스레드와 개인 동기화 자원을 정리한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 무창 계약 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose() { release.Set();thread.Join();acquired.Dispose();release.Dispose(); }
    }
}
