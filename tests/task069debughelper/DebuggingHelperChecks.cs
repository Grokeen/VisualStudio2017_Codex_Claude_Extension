using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Services;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : DebuggingHelperChecks
/// desc         : 실제 서비스의 bounded 형식화와 제품 WPF click/초안/수명을 합성 읽기 어댑터로 검사한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 SDK 위치 IID도 검사하며 native debuggee 실측과 구분한다.
/// </summary>
/// ---------------------------------------------------------------------
internal static class DebuggingHelperChecks
{
    internal static readonly List<string> Checks = new List<string>(), Errors = new List<string>();
    internal static readonly List<object> Observations = new List<object>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 원격AI·디버거 실행·OS입력 없이 실제 DLL 서비스/컨트롤 경로를 실행한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실패는 원문 저장하고 후속 독립 검사를 계속한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static void Run(string directory)
    {
        Scenario("service",Service);
        Scenario("source-boundary",()=>SourceBoundary(directory));
        Scenario("actual-control",()=>ActualControl(directory));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Service
    /// desc         : 실제 서비스가 중단 위치·source±3줄·stack3·취소·상태 변화를 검사하는지 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 합성 adapter만 주입하며 형식화나 승인 결과를 대체하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Service()
    {
        var reader=new Reader();var service=new DebuggingContextService(reader,Dispatcher.CurrentDispatcher);
        Check("service: initial nonbreak state cannot capture or read details",!service.CanCapture&&reader.Details==0);
        reader.Data.IsBreakMode=true;
        Check("service: break/frame availability does not read source or stack",service.CanCapture&&reader.Details==0);
        string body=service.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult();
        Check("service: actual format contains current file line function and bounded source",body.Contains("DebugFixture.cs")&&body.Contains("줄: 180")&&body.Contains("PausedMethod")&&body.Contains("180 > current_statement")&&body.Contains("177 : before_3")&&body.Contains("183 : after_3"));
        Check("service: only first three actual function names are included",body.Contains("FrameOne")&&body.Contains("FrameTwo")&&body.Contains("FrameThree")&&!body.Contains("FrameFour"));
        Check("service: unknown reason is not falsely labeled exception or error",body.Contains("중단 원인/예외 정보는 자동 수집하지 않음")&&!body.Contains("예외 발생")&&!body.Contains("오류 발생"));
        Check("service: full source path and unrequested variable sentinel are absent",!body.Contains("PRIVATE_USER_PATH")&&!body.Contains(reader.UnrequestedVariableValue)&&body.Length<=DebuggingContextService.MaximumContextCharacters);
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();int before=reader.Details;Expect("service: cancelled capture reads no detailed state",()=>service.CaptureAsync(cancelled.Token));Check("service: cancelled token causes zero additional source reads",reader.Details==before);}
        reader.ThrowDetails=true;Expect("service: COM/read failure propagates instead of guessed context",()=>service.CaptureAsync(CancellationToken.None));reader.ThrowDetails=false;
        var original=reader.Data.SourceLines;reader.Data.SourceLines=new string[0];Expect("service: missing source cannot produce partial context",()=>service.CaptureAsync(CancellationToken.None));reader.Data.SourceLines=original;
        reader.Data.SourceLines=Enumerable.Repeat(new string('x',1000),7).ToArray();
        string bounded=service.CaptureAsync(CancellationToken.None).Result;
        Check("service: long source lines are bounded without oversized output",bounded.Length<=5000&&!bounded.Contains(new string('x',401)));
        reader.Data.SourceLines=original;reader.ChangeOnVerify=true;Expect("service: changed frame during read is rejected",()=>service.CaptureAsync(CancellationToken.None));reader.ChangeOnVerify=false;reader.Data.LineNumber=180;
        service.Dispose();service.Dispose();Check("service: disposal unsubscribes reader and disposes once",reader.Subscribers==0&&reader.Disposals==1&&!service.CanCapture);
        Expect("service: disposed service cannot read debugger",()=>service.CaptureAsync(CancellationToken.None));
        Observations.Add(new{Scenario="service",ContextCharacters=body.Length,SourceReads=reader.Details,StateReads=reader.States,NativeDebuggeeUsed=false});
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SourceBoundary
    /// desc         : 실행 후보의 실제 소스와 SDK interop 메타데이터에서 금지 API와 위치 계약을 대조한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : metadata 검증은 native frame QI 성공으로 확대해석하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void SourceBoundary(string root)
    {
        string source=File.ReadAllText(Path.Combine(root,"Product.DebuggingContextService.cs"));
        string[] forbidden={".Locals",".Arguments","GetExpression(","GetExpression2(",".ExecuteStatement(",".SetNextStatement(",".Go(",".Break(",".Stop(",".StepInto(",".StepOver(",".StepOut(","File.Read","File.Write","OpenFile("};
        foreach(string token in forbidden)Check("source: no "+token+" call/member",!source.Contains(token));
        Check("source: source buffer request is bounded and only matching open document is used",source.Contains("Math.Min(3000")&&source.Contains("point.GetText(count)")&&source.Contains("String.Equals(document.FullName, data.FilePath"));
        Type contract=typeof(DebuggingContextService).GetNestedType("IFrameLocation",BindingFlags.NonPublic);
        string sdk="C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/Common7/IDE/PublicAssemblies/envdte90a.dll";
        Type actual=Assembly.LoadFrom(sdk).GetType("EnvDTE90a.StackFrame2");
        Check("interop: location contract matches installed VS2017 StackFrame2 IID",contract.GUID==actual.GUID);
        foreach(string name in new[]{"LineNumber","FileName"})
        {
            int left=((DispIdAttribute)contract.GetProperty(name).GetCustomAttributes(typeof(DispIdAttribute),false).Single()).Value;
            int right=((DispIdAttribute)actual.GetProperty(name).GetCustomAttributes(typeof(DispIdAttribute),false).Single()).Value;
            Check("interop: "+name+" readonly DispId/type matches SDK",left==right&&contract.GetProperty(name).PropertyType==actual.GetProperty(name).PropertyType&&!contract.GetProperty(name).CanWrite);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ActualControl
    /// desc         : 실제 DBG click handler의 상태 표시·원문/선택 보존·16k atomic 실패·수명을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 합성 source 제공자를 주입하며 실제 디버거 실행을 제어하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ActualControl(string root)
    {
        Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH",Path.Combine(root,"settings.json"));Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH",Path.Combine(root,"workspace"));
        new AssistantSettingsStore().Save(new AssistantSettings{HideWelcome=true,ShowHistory=false,EnableCompletionNotifications=false});
        var documents=new Documents();var control=new AssistantControl();Set(control,"scmLookupStarted",true);
        try
        {
            control.Initialize(documents,new RuntimeEditProvider());var button=(Button)control.FindName("DebuggingInfoButton");var input=(TextBox)control.FindName("Instruction");
            Check("UI: idle helper is absent and source details remain unread",button.Visibility==Visibility.Collapsed&&documents.Reader.Details==0);
            documents.Reader.Data.IsBreakMode=false;documents.Reader.Raise();Check("UI: running helper remains absent without details",button.Visibility==Visibility.Collapsed&&documents.Reader.Details==0);
            documents.Reader.Data.IsBreakMode=true;documents.Reader.Raise();
            Check("UI: break frame reveals one accessible DBG action beside composer controls",button.Visibility==Visibility.Visible&&Convert.ToString(button.Content)=="DBG"&&System.Windows.Automation.AutomationProperties.GetName(button)=="디버깅 정보 초안 추가"&&button.Style==control.FindResource("SurfaceButton")&&documents.Reader.Details==0);
            input.Text="기존 요청\n한글 유지";input.Select(2,3);string before=input.Text;int count=Chat(control).Messages.Count;
            Click(button);Pump();
            Check("UI: explicit click appends bounded actual service context preserving original text",input.Text.StartsWith(before+"\n\n",StringComparison.Ordinal)&&input.Text.Contains("current_statement")&&input.Text.Length<=16000);
            Check("UI: original selection is preserved after append",input.SelectionStart==2&&input.SelectionLength==3);
            Check("UI: helper never submits chat or starts queued/CLI work",Chat(control).Messages.Count==count&&((IDictionary)Field(control,"roomRuns")).Count==0&&((ICollection)Field(control,"queuedRequests")).Count==0);
            Check("UI: helper reads no active editor service outside bounded debugger adapter",documents.EditorReads==0&&documents.Reader.Details==1);
            input.Text=new string('L',15990);input.Select(3,4);string longDraft=input.Text;Click(button);Pump();
            Check("UI: 16k overflow preserves complete draft and selection atomically",input.Text==longDraft&&input.SelectionStart==3&&input.SelectionLength==4&&Status(control).Contains("16,000"));
            input.Text="KEEP_ON_READ_FAILURE";documents.Reader.ThrowDetails=true;Click(button);Pump();documents.Reader.ThrowDetails=false;
            Check("UI: read failure preserves draft with a responsive explanation",input.Text=="KEEP_ON_READ_FAILURE"&&Status(control).Contains("읽지 못"));
            documents.Reader.Data.FunctionName="";documents.Reader.Raise();int details=documents.Reader.Details;Click(button);Pump();
            Check("UI: unavailable frame hides action and click-race reads no details",button.Visibility==Visibility.Collapsed&&input.Text=="KEEP_ON_READ_FAILURE"&&documents.Reader.Details==details);
            documents.Reader.Data.FunctionName="PausedMethod";documents.Reader.Raise();documents.PendingMode=true;
            input.Text="WAITING_ORIGINAL";Click(button);input.Text="USER_EDITED_DRAFT";documents.Complete();PumpUntil(()=>Field(control,"debuggingCapture")==null);
            Check("UI: editing draft while context is pending never overwrites new user input",input.Text=="USER_EDITED_DRAFT");
            input.Text="ROOM_A_DRAFT";Click(button);Invoke(control,"CreateConversation");input.Text="ROOM_B_DRAFT";documents.Complete();PumpUntil(()=>Field(control,"debuggingCapture")==null);
            Check("UI: switching conversation while capture is pending never inserts in wrong room",input.Text=="ROOM_B_DRAFT"&&!input.Text.Contains("디버깅 중단 정보"));
            Click(button);control.Dispose();PumpUntil(()=>Field(control,"debuggingCapture")==null);
            Check("UI: Dispose cancels pending capture and removes debugger subscriptions once",documents.Disposals==1&&documents.Reader.Subscribers==0&&documents.Reader.Disposals==1&&input.Text=="ROOM_B_DRAFT");
            Check("UI: disposed helper is absent and no automatic user message was created",button.Visibility==Visibility.Collapsed&&Chat(control).Messages.Count==0);
            Observations.Add(new{Scenario="actual-control",DebuggerControlled=false,EditorReads=documents.EditorReads,ReaderDetails=documents.Reader.Details,SubscriptionsAfterDispose=documents.Reader.Subscribers,FinalDraft=input.Text});
        }
        finally{control.Dispose();Pump();}
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Reader
    /// desc         : 값 수집 API 없이 제한된 위치·소스·스택 읽기를 합성한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class Reader:IDebuggingContextReader
    {
        internal DebuggingFrameData Data=new DebuggingFrameData{FilePath="C:/PRIVATE_USER_PATH/DebugFixture.cs",FunctionName="PausedMethod",LineNumber=180,SourceStartLine=177,SourceLines=new[]{"before_3","before_2","before_1","current_statement","after_1","after_2","after_3"},StackFunctions=new[]{"FrameOne","FrameTwo","FrameThree","FrameFour"}};
        internal int Details,States,Subscribers,Disposals;internal bool ThrowDetails,ChangeOnVerify;internal readonly string UnrequestedVariableValue="PRIVATE_LOCALS_SENTINEL";private EventHandler changed;
        public event EventHandler StateChanged{add{changed+=value;Subscribers++;}remove{changed-=value;Subscribers--;}}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadCurrent
        /// desc         : 요청된 상세 여부와 실패·위치변경을 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DebuggingFrameData ReadCurrent(bool details){if(details){Details++;if(ThrowDetails)throw new COMException("synthetic read denied");}else{States++;if(ChangeOnVerify)Data.LineNumber++;}return Data;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Raise
        /// desc         : 자기 상태 이벤트를 발생시킨다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Raise(){var handler=changed;if(handler!=null)handler(this,EventArgs.Empty);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 자기 합성 서비스의 해제 횟수를 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose(){Disposals++;}
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Documents
    /// desc         : 실제 서비스를 어댑터로 주입하고 대기·취소를 제어한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class Documents:IActiveDocumentService,IDebuggingContextProvider
    {
        internal readonly Reader Reader=new Reader();private readonly DebuggingContextService service;internal bool PendingMode;internal int EditorReads,Disposals;private TaskCompletionSource<string> pending;private CancellationTokenRegistration registration;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Documents
        /// desc         : 실제 서비스를 어댑터로 주입하고 대기·취소를 제어한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal Documents(){service=new DebuggingContextService(Reader,Dispatcher.CurrentDispatcher);}
        public event EventHandler StateChanged{add{service.StateChanged+=value;}remove{service.StateChanged-=value;}}
        public bool CanCapture{get{return service.CanCapture;}}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureAsync
        /// desc         : 실제 서비스를 사용하거나 취소 가능한 대기를 제공한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public Task<string> CaptureAsync(CancellationToken token){if(!PendingMode)return service.CaptureAsync(token);pending=new TaskCompletionSource<string>();registration=token.Register(()=>pending.TrySetCanceled());return pending.Task;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Complete
        /// desc         : 대기 읽기를 실제 서비스의 결과로 완료한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Complete(){registration.Dispose();pending.TrySetResult(service.CaptureAsync(CancellationToken.None).Result);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetActiveDocument
        /// desc         : 관련 없는 활성문서 읽기가 발생하면 실패한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public IEditorDocument GetActiveDocument(){EditorReads++;throw new InvalidOperationException("No unrelated editor reads.");}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 자기 합성 서비스의 해제 횟수를 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose(){if(Disposals++==0){registration.Dispose();service.Dispose();}}
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Scenario
    /// desc         : 시나리오 예외를 원문 기록하고 다른 검사를 계속한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Scenario(string name,Action action){try{action();}catch(Exception error){Errors.Add(name+": "+error);}}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 명명한 계약의 실제 통과와 실패를 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name,bool result){if(result)Checks.Add(name);else Errors.Add(name);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Expect
    /// desc         : 계약에 따른 명시 거절을 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Expect(string name,Action action){try{action();Check(name,false);}catch(Exception){Check(name,true);}}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Field
    /// desc         : 실제 컨트롤의 내부 상태를 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Field(object target,string name){return target.GetType().GetField(name,Private).GetValue(target);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Set
    /// desc         : 격리 fixture의 초기 상태만 설정한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Set(object target,string name,object value){target.GetType().GetField(name,Private).SetValue(target,value);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Invoke
    /// desc         : 실제 제품의 내부 사용자 동작을 호출한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Invoke(object target,string name,params object[] args){return target.GetType().GetMethod(name,Private).Invoke(target,args);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Chat
    /// desc         : 현재 제품 대화의 메시지를 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ChatSession Chat(AssistantControl control){return(ChatSession)control.GetType().GetProperty("ActiveChat",Private).GetValue(control,null);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Status
    /// desc         : 실제 상태 안내문을 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Status(AssistantControl control){return((TextBlock)control.FindName("StatusLabel")).Text;}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Click
    /// desc         : 실제 WPF Click 이벤트를 발생시킨다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PumpUntil
    /// desc         : 제한된 시간 내 dispatcher 완료를 기다린다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void PumpUntil(Func<bool> condition){var end=DateTime.UtcNow.AddSeconds(5);while(!condition()){if(DateTime.UtcNow>end)throw new TimeoutException("Debug helper continuation timed out.");Pump();}}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pump
    /// desc         : 무창 dispatcher의 대기 연속 작업을 처리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 실제 제품 경계와 합성 입력을 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Pump(){var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(20)};timer.Tick+=(s,a)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
}
