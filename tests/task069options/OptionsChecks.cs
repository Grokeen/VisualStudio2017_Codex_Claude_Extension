using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : OptionsChecks
/// desc         : 무창 실제 UI 선택과 제출 문맥을 실제 제품 native wire까지 관측한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK069 이미지69 모델·강도와 Codex/Claude 속도·권한 계약.
/// </summary>
/// ---------------------------------------------------------------------
public static class OptionsChecks
{
    public static readonly List<string> Passed = new List<string>(), Errors = new List<string>();
    public static readonly List<object> Observations = new List<object>();
    public static int CaptureCount, NativeCalls, NativeWireRows;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 8000000 };

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 제공자·권한·속도 조합을 별도 개인 UI와 native 자식으로 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : Window.Show·Popup·OS 입력·실계정 사용 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run(string root, string fixture)
    {
        foreach (string provider in new[] { "Codex", "Claude" })
            foreach (bool full in new[] { true, false })
                foreach (string speed in new[] { "standard", "fast" })
                {
                    string name = provider + "-" + (full ? "full" : "proposal") + "-" + speed;
                    try { Scenario(Path.Combine(root, name), fixture, provider, full, speed); }
                    catch (Exception error) { Errors.Add(name + ": " + error); }
                }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Scenario
    /// desc         : UI 이벤트로 정한 값과 대기 캡처를 CLI JSON/argv의 실제 값과 대조한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 합성 문서의 실제 파일/저장 상태만 사용한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Scenario(string root, string fixture, string provider, bool full, string speed)
    {
        Directory.CreateDirectory(root);
        string wire = Path.Combine(root, "wire"); Directory.CreateDirectory(wire);
        Environment.SetEnvironmentVariable("TASK069_OPTIONS_WIRE", wire);
        Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", Path.Combine(root, "settings.json"));
        Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", Path.Combine(root, "workspace"));
        new AssistantSettingsStore().Save(new AssistantSettings { HideWelcome = true, ShowHistory = false, EnableCompletionNotifications = false });
        var documents = new Documents(Path.Combine(root, "project"));
        var control = new AssistantControl();
        try
        {
            Set(control, "scmLookupStarted", true);
            control.Initialize(documents, new RuntimeEditProvider());
            var tabs = (TabControl)control.FindName("ProviderChoice");
            tabs.SelectedItem = tabs.Items.Cast<TabItem>().Single(item => Convert.ToString(item.Tag) == provider);
            Check(root, "actual provider tab event selects requested provider", Convert.ToString(Field(control, "selectedProvider")) == provider);
            ((TextBox)control.FindName("ExecutablePath")).Text = fixture;
            Set(control, "agentFullAccess", full);
            string model = provider == "Codex" ? "gpt-6-luna" : "opus";
            ((TextBox)control.FindName("ModelName")).Text = model;
            var mode = (ComboBox)control.FindName("ModeChoice");
            mode.SelectedItem = mode.Items.Cast<ComboBoxItem>().Single(item => Convert.ToString(item.Tag) == "high");
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            Observations.Add(new { Scenario = Path.GetFileName(root), Phase = "ui-selection", Model = ((TextBox)control.FindName("ModelName")).Text, ModeValue = mode.SelectedValue, ModeItem = Convert.ToString((mode.SelectedItem as ComboBoxItem)?.Tag), Syncing = Field(control, "syncingModel"), ExperienceReady = Field(control, "experienceReady"), Effort = ((Dictionary<string, string>)Field(control, "efforts"))[provider] });
            ((ToggleButton)control.FindName("FastToggle")).IsChecked = speed == "fast";
            Invoke(control, "FastChanged", control.FindName("FastToggle"), new RoutedEventArgs());
            string label = Convert.ToString(((Button)control.FindName("ModelOptionsButton")).Content);
            Check(root, "image model and High are shown by actual UI events", label.Contains(model) && label.Contains("High"));
            Check(root, "actual UI effort and speed state match selection", ((Dictionary<string, string>)Field(control, "efforts"))[provider] == "high" && ((Dictionary<string, string>)Field(control, "speeds"))[provider] == speed);
            ((TextBox)control.FindName("Instruction")).Text = "TASK069 isolated option verification";
            object captured = Invoke(control, "CaptureCurrentRequest", false);
            CaptureCount++;
            var original = (ChatRequestContext)Field(captured, "Context");
            Check(root, "submission captures exact selected options", original.Model == model && original.ReasoningEffort == "high" && original.Speed == speed && (bool)Field(captured, "FullAccess") == full);
            ((TextBox)control.FindName("ModelName")).Text = "different-next-request-model";
            ((ComboBox)control.FindName("ModeChoice")).SelectedValue = "auto";
            ((ToggleButton)control.FindName("FastToggle")).IsChecked = false;
            var context = (ChatRequestContext)Invoke(control, "BuildQueuedExecutionContext", captured);
            Check(root, "later UI changes do not replace queued model effort or speed", context.Model == model && context.ReasoningEffort == "high" && context.Speed == speed);
            var snapshot = documents.GetActiveDocument().Capture();
            string before = File.ReadAllText(snapshot.FilePath);
            NativeCalls++;
            if (full)
            {
                AgentExecutionResult result = provider == "Codex"
                    ? Wait(new CodexRuntimeRunner { Timeout = TimeSpan.FromSeconds(20) }.RunAgentAsync(fixture, documents.Project, "TASK069 isolated option verification", context, null, null, CancellationToken.None))
                    : Wait(new ClaudeRuntimeRunner { Timeout = TimeSpan.FromSeconds(20) }.RunAgentAsync(fixture, documents.Project, "TASK069 isolated option verification", context, null, null, CancellationToken.None));
                Check(root, "actual full-access native protocol completes", result.Text == "isolated options completed");
            }
            else
            {
                var runtime = new RuntimeEditProvider();
                var result = Wait(runtime.GenerateWithContextAsync(provider, fixture, snapshot, "TASK069 isolated option verification", context, CancellationToken.None));
                Check(root, "actual approval protocol returns an unapplied proposal", result.ProposedText == "return 2;");
            }
            var messages = Directory.GetFiles(wire, "*.wire.jsonl").SelectMany(path => File.ReadAllLines(path)).Select(line => Json.Deserialize<Dictionary<string, object>>(line)).ToArray();
            NativeWireRows += messages.Length;
            var argvRows = messages.Where(row => Text(row, "Kind") == "argv").ToArray();
            var invocation = argvRows.Single(row => !((IEnumerable)row["Arguments"]).Cast<string>().Contains("--version"));
            string[] argv = ((IEnumerable)invocation["Arguments"]).Cast<string>().ToArray();
            if (provider == "Codex" && full)
            {
                var thread = Parameters(messages, "thread/start"); var turn = Parameters(messages, "turn/start");
                Check(root, "Codex model is explicit on thread/start", Text(thread, "model") == model);
                Check(root, "Codex turn/start receives exact high and selected tier", Text(turn, "effort") == "high" && (speed == "fast" ? Text(turn, "serviceTier") == "fast" : turn.ContainsKey("serviceTier") && turn["serviceTier"] == null));
                Check(root, "Codex full-access approval sandbox and ephemeral stay explicit", Text(thread, "approvalPolicy") == "never" && Text(thread, "sandbox") == "danger-full-access" && Equals(thread["ephemeral"], true) && Text(turn, "approvalPolicy") == "never");
                Check(root, "Codex turn targets the thread that selected the model", Text(turn, "threadId") == "options-thread" && argv.SequenceEqual(new[] { "app-server" }));
                Observations.Add(new { Scenario = Path.GetFileName(root), Label = label, ThreadStart = thread, TurnStart = turn, Arguments = argv });
            }
            else
            {
                Check(root, "native argv receives exact selected model", After(argv, "--model") == model);
                Check(root, "native argv receives exact High effort", provider == "Codex" ? argv.Contains("model_reasoning_effort=\"high\"") : After(argv, "--effort") == "high");
                if (provider == "Codex")
                    Check(root, "Codex proposal speed and read-only scope remain separate", argv.Contains("service_tier=\"fast\"") == (speed == "fast") && After(argv, "--sandbox") == "read-only" && argv.Contains("--ignore-user-config") && argv.Contains("--ephemeral"));
                else
                {
                    var settings = Json.Deserialize<Dictionary<string, object>>(After(argv, "--settings"));
                    Check(root, "Claude fastMode is explicitly true or false", settings.ContainsKey("fastMode") && Equals(settings["fastMode"], speed == "fast"));
                    Check(root, "Claude selected mode keeps correct tool permission boundary", argv.Contains("--no-session-persistence") && (full ? After(argv, "--permission-mode") == "bypassPermissions" && After(argv, "--input-format") == "stream-json" : After(argv, "--tools") == "" && After(argv, "--disallowedTools") == "mcp__*"));
                }
                Observations.Add(new { Scenario = Path.GetFileName(root), Label = label, Arguments = argv, CapturedModel = context.Model, CapturedEffort = context.ReasoningEffort, CapturedSpeed = context.Speed });
            }
            Check(root, "local fixture does not apply proposal or modify source", File.ReadAllText(snapshot.FilePath) == before);
        }
        finally { control.Dispose(); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Parameters
    /// desc         : 실제 wire에서 요청한 JSON-RPC 메소드의 원문 params를 반환한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 기대값으로 wire를 재구성하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static Dictionary<string, object> Parameters(Dictionary<string, object>[] rows, string method)
    { return (Dictionary<string, object>)rows.Where(row => Text(row, "Kind") == "wire").Select(row => (Dictionary<string, object>)row["Message"]).Single(row => Text(row, "method") == method)["params"]; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Wait
    /// desc         : 자기 WPF dispatcher를 펌프하며 제품 비동기 작업의 실제 완료를 기다린다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 25초 상한·Task 실패를 숨기지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static T Wait<T>(Task<T> task)
    {
        DateTime limit = DateTime.UtcNow.AddSeconds(25);
        while (!task.IsCompleted) { if (DateTime.UtcNow > limit) throw new TimeoutException("Options test exceeded its bound."); Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { })); Thread.Sleep(5); }
        return task.GetAwaiter().GetResult();
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 시나리오별 실제 성공·실패 assertion을 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실패를 성공 개수에 넣지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string root, string name, bool passed) { string label = Path.GetFileName(root) + ": " + name; if (passed) Passed.Add(label); else Errors.Add(label); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Field
    /// desc         : 검사 대상 제품 인스턴스의 기존 상태만 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 제품 코드를 대체하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Field(object target, string name) { return target.GetType().GetField(name, Private | BindingFlags.Public).GetValue(target); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Set
    /// desc         : 자기 UI의 비시각 검사 조건만 설정한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 모델/강도는 실제 컨트롤 이벤트로만 바꾼다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Invoke
    /// desc         : 자기 실제 컨트롤의 옵션 캡처·재구성 메소드를 호출한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 외부 UI/운영 프로필에 접근하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Invoke(object target, string method, params object[] args) { return target.GetType().GetMethod(method, Private).Invoke(target, args); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Text
    /// desc         : 관측 JSON의 문자열을 안전하게 비교한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : null을 빈 문자열로만 처리한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Text(Dictionary<string, object> row, string key) { return row.ContainsKey(key) ? Convert.ToString(row[key]) : ""; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : After
    /// desc         : 실제 argv에서 옵션 다음 값을 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 셸 문자열의 재해석 없이 비교한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string After(string[] argv, string key) { int index = Array.IndexOf(argv, key); return index < 0 || index + 1 == argv.Length ? null : argv[index + 1]; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Documents
    /// desc         : 자기 개인 프로젝트만 반환하는 합성 IDE 문서 경계다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실제 사용자 IDE의 문서를 읽지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class Documents : IActiveDocumentService, ISourceNavigationService, IFullAccessDocumentService, IEditorDocument
    {
        internal readonly string Project;
        private readonly string path;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Documents
        /// desc         : 자기 검사 폴더에만 단일 합성 소스를 생성한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 운영 소스는 접근하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal Documents(string project) { Project = project; Directory.CreateDirectory(project); path = Path.Combine(project, "Sample.txt"); File.WriteAllText(path, "return 1;"); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetActiveDocument
        /// desc         : 자기 합성 활성 문서만 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 실제 IDE 호출 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public IEditorDocument GetActiveDocument() { return this; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetProjectDirectory
        /// desc         : 고정 개인 프로젝트 폴더만 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : CLI cwd 경계 검증에 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string GetProjectDirectory() { return Project; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureFullAccessContext
        /// desc         : 합성 현재 파일의 명시적 스냅샷을 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 미저장/읽기전용 상태를 가장하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot CaptureFullAccessContext() { return Capture(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Capture
        /// desc         : 자기 실제 파일의 고정 콘텐츠를 읽는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 승인 미적용 상태를 검증한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot Capture() { return new DocumentSnapshot(path, File.ReadAllText(path), "fixture-disk", "fixture options", false, false); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigateTo
        /// desc         : 옵션 검사에서 요청하지 않은 탐색을 실패 처리한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 외부 IDE UI를 열지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void NavigateTo(string file, int line) { throw new InvalidOperationException("Unexpected navigation."); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Replace
        /// desc         : 옵션 관측 중 자동 소스 교체를 허용하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 호출되면 회귀 실패다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Replace(DocumentSnapshot expected, string content) { throw new InvalidOperationException("Unexpected source replacement."); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Save
        /// desc         : 옵션 관측 중 자동 소스 저장을 허용하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 호출되면 회귀 실패다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Save(DocumentSnapshot expected) { throw new InvalidOperationException("Unexpected source save."); }
    }
}
