using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ProgressPresentationChecks
/// desc         : 표시 창·CLI 실행 없이 공개 진행 모델·Presenter 구조·실제 helper 동작을 검사한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : source fixture와 고정 제품 DLL 결과를 실행기가 명확히 구분.
/// </summary>
/// ---------------------------------------------------------------------
public static class ProgressPresentationChecks
{
    public static readonly List<string> Checks = new List<string>(), Errors = new List<string>();

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 공개 상태의 중복 제거·진실한 종료·저장·접근성 구조와 실제 메시지 갱신을 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 네트워크·개인 계정·원문 명령 실행을 사용하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run()
    {
        Check("STA and runtime discovery isolation are required", delegate { Assert(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA && Environment.GetEnvironmentVariable("VS2017_AI_DISABLE_RUNTIME_DISCOVERY") == "1"); });
        Check("One actual work ID updates one stable row through start and completion", delegate { var n = new ProgressNotice(); n.Apply("command", "one", "시작", "dotnet test"); var row = n.Steps[0]; Assert(n.Apply("command", "one", "완료", "dotnet test", 0) && n.Steps.Count == 1 && Object.ReferenceEquals(row,n.Steps[0]) && row.State == "completed"); });
        Check("Duplicate work state does not mutate timestamps or notify bindings", delegate { var n = new ProgressNotice(); n.Apply("command","id","started","dotnet test"); var when=n.UpdatedAt; int notices=0; n.PropertyChanged += delegate { notices++; }; Assert(!n.Apply("command","id","started","dotnet test") && when==n.UpdatedAt && notices==0); });
        Check("Claude generic tool completion preserves original command kind and safe detail", delegate { var n = new ProgressNotice(); n.Apply("command","claude-1","started","Git status"); n.Apply("tool","claude-1","completed","연결된 도구",0); Assert(n.Steps.Count==1 && n.Steps[0].Kind=="command" && n.Steps[0].Detail=="Git status"); });
        Check("Claude generic tool completion preserves original file classification", delegate { var n = new ProgressNotice(); n.Apply("file","file-1","시작","Program.cs"); n.Apply("tool","file-1","완료","연결된 도구"); Assert(n.Steps.Count==1 && n.Steps[0].Kind=="file" && n.Steps[0].Detail=="Program.cs"); });
        Check("Late start cannot reopen a finished actual work item", delegate { var n = new ProgressNotice(); n.Apply("command","id","completed","MSBuild",0); Assert(!n.Apply("command","id","started","MSBuild") && n.Steps[0].State=="completed"); });
        Check("Nonzero actual exit code reports failure without inventing completion", delegate { var n = new ProgressNotice(); n.Apply("command","id","completed","dotnet test",7); Assert(n.Steps[0].State=="failed" && n.Steps[0].ExitCode==7 && n.MarkdownText.Contains("종료 코드 7")); });
        Check("Failed actual work cannot be silently changed into success", delegate { var n = new ProgressNotice(); n.Apply("tool","id","failed","연결된 도구"); Assert(!n.Apply("tool","id","completed","연결된 도구") && n.Steps[0].State=="failed"); });
        Check("Unknown states remain unknown instead of guessed completion", delegate { var n = new ProgressNotice(); n.Apply("tool","id","90 percent","연결된 도구"); Assert(n.Steps[0].State=="unknown" && !n.Steps[0].StateLabel.Contains("완료")); });
        Check("Anonymous events update a category row without fictional work counts", delegate { var n = new ProgressNotice(); n.Apply("command",null,"시작","MSBuild"); n.Apply("command",null,"완료","MSBuild"); n.Apply("command",null,"시작","MSBuild"); Assert(n.Steps.Count==1 && !n.Steps[0].HasWorkId && !n.Steps[0].Title.Contains("#") && n.Steps[0].State=="started"); });
        Check("Anonymous category restart clears stale failure detail and exit code", delegate { var n=new ProgressNotice();n.Apply("command",null,"failed","MSBuild",1);n.Apply("command",null,"started",".NET build");Assert(n.Steps[0].State=="started" && n.Steps[0].ExitCode==null && n.Steps[0].Detail==".NET build");n.Apply("command",null,"completed",".NET build");Assert(n.Steps.Count==1 && n.Steps[0].State=="completed" && n.Steps[0].ExitCode==null); });
        Check("Separate actual IDs are kept even when safe details match", delegate { var n = new ProgressNotice(); n.Apply("file","a","started","same.cs"); n.Apply("file","b","started","same.cs"); Assert(n.Steps.Count==2 && n.Steps[0].Number!=n.Steps[1].Number); });
        Check("Bounded safe single-line details reject multiline and control content", delegate { var n = new ProgressNotice(); n.Apply("command","one","started","MSBuild\nprivate-output"); n.Apply("tool","two","started","label\u001b[31m"); Assert(n.Steps.All(s=>s.Detail.Length==0) && !n.MarkdownText.Contains("private-output")); });
        Check("Safe detail length and surrogate boundary are bounded", delegate { var n = new ProgressNotice(); n.Apply("file","one","started",new String('a',118)+"😀"+new String('b',80)); string text=n.Steps[0].Detail; Assert(text.Length<=120 && !Char.IsHighSurrogate(text[text.Length-2]) && text.EndsWith("…",StringComparison.Ordinal)); });
        Check("Unknown kinds and hidden reasoning do not become public work rows", delegate { var n = new ProgressNotice(); Assert(!n.Apply("thinking","id","started","private") && !n.Apply("reasoning","id","completed","private") && !n.Apply("message","id","completed","answer") && n.Steps.Count==0); });
        Check("Bounded row count still accepts terminal updates for admitted work", delegate { var n = new ProgressNotice(); for(int i=0;i<80;i++) n.Apply("command","id"+i,"started","dotnet test"); Assert(n.Apply("tool","overflow","started","연결된 도구") && n.IsTruncated && n.Steps.Count==80); Assert(n.Apply("tool","id0","completed","연결된 도구",0) && n.Steps[0].State=="completed" && !n.Apply("tool","overflow-again","started","연결된 도구")); });
        Check("Durable summary remains readable and at most 2000 characters", delegate { var n = new ProgressNotice(); for(int i=0;i<80;i++) n.Apply("file","id"+i,"started",new String('한',120)); Assert(n.MarkdownText.Length<=2000 && n.MarkdownText.StartsWith(ProgressNotice.MarkdownMarker+"\n",StringComparison.Ordinal) && n.MarkdownText.Contains("생략") && !n.MarkdownText.Contains("id79")); });
        Check("Normal request completion does not manufacture work success", delegate { var n = new ProgressNotice(); n.Apply("command","one","started","MSBuild"); n.Complete("completed"); Assert(n.Outcome=="completed" && n.Steps[0].State=="unreported" && !n.IsActive); });
        Check("Cancelled request marks unfinished work stopped and preserves finished work", delegate { var n = new ProgressNotice(); n.Apply("command","done","completed","MSBuild",0); n.Apply("file","pending","started","a.cs"); n.Complete("cancelled"); Assert(n.Outcome=="cancelled" && n.Steps[0].State=="completed" && n.Steps[1].State=="cancelled"); });
        Check("Failed request keeps unfinished work termination explicitly unreported", delegate { var n = new ProgressNotice(); n.Apply("tool","one","started","연결된 도구"); n.Complete("failed"); Assert(n.Outcome=="failed" && n.Steps[0].State=="unreported"); });
        Check("Completed request rejects stale progress and repeated completion", delegate { var n = new ProgressNotice(); n.Apply("tool","one","started","연결된 도구"); n.Complete(); string saved=n.MarkdownText; Assert(!n.Apply("file","late","started","late.cs") && !n.Complete("failed") && saved==n.MarkdownText); });
        Check("Desktop notification policy accepts only visible enabled successful requests", delegate { var method=typeof(AssistantControl).GetMethod("ShouldShowCompletionNotification",BindingFlags.NonPublic|BindingFlags.Static);Assert(method!=null && (bool)method.Invoke(null,new object[]{"completed",true,true}) && !(bool)method.Invoke(null,new object[]{"failed",true,true}) && !(bool)method.Invoke(null,new object[]{"cancelled",true,true}) && !(bool)method.Invoke(null,new object[]{"completed",false,true}) && !(bool)method.Invoke(null,new object[]{"completed",true,false})); });
        Check("Saved Markdown reopens as immutable historical summary rather than live tasks", delegate { var n=new ProgressNotice(); n.Apply("file","one","completed","a.cs"); var old=ProgressNotice.RestoreSummary(n.MarkdownText); Assert(old!=null && old.IsArchived && !old.IsActive && old.Steps.Count==0 && !old.Apply("file","new","started","new.cs") && old.MarkdownText==n.MarkdownText); });
        Check("Arbitrary or oversized imported messages do not masquerade as progress cards", delegate { Assert(ProgressNotice.RestoreSummary("**작업 진행**")==null && ProgressNotice.RestoreSummary(ProgressNotice.MarkdownMarker+"\n"+new String('a',2001))==null); });
        Check("Converter keeps one attached VM and exposes the IsProgress boolean", delegate { var c=new ProgressNoticeConverter();var row=new ChatMessage{Role="system",Text="plain"};var n=new ProgressNotice();ProgressNoticeConverter.Attach(row,n);Assert(Object.ReferenceEquals(c.Convert(row,typeof(object),null,CultureInfo.InvariantCulture),n) && (bool)c.Convert(row,typeof(bool),"IsProgress",CultureInfo.InvariantCulture)); });
        Check("Converter leaves ordinary user assistant and system messages unchanged", delegate { var c=new ProgressNoticeConverter(); foreach(string role in new[]{"user","assistant","system"}) { var row=new ChatMessage{Role=role,Text="normal"};Assert(c.Convert(row,typeof(object),null,CultureInfo.InvariantCulture)==null && !(bool)c.Convert(row,typeof(bool),"IsProgress",CultureInfo.InvariantCulture)); } });
        Check("Only system messages with an explicit marker become historical cards", delegate { var n=new ProgressNotice(); n.Apply("file","one","completed","a.cs");var c=new ProgressNoticeConverter();var saved=new ChatMessage{Role="system",Text=n.MarkdownText};Assert(((ProgressNotice)c.Convert(saved,typeof(object),null,CultureInfo.InvariantCulture)).IsArchived && c.Convert(new ChatMessage{Role="assistant",Text=n.MarkdownText},typeof(object),null,CultureInfo.InvariantCulture)==null); });
        Check("A message cannot be rebound to a different live notice", delegate { var row=new ChatMessage{Role="system"};ProgressNoticeConverter.Attach(row,new ProgressNotice());bool refused=false;try{ProgressNoticeConverter.Attach(row,new ProgressNotice());}catch(InvalidOperationException){refused=true;}Assert(refused); });
        Check("Two presenters can share one model without sharing FrameworkElements", delegate { var n=new ProgressNotice();var one=new ProgressNoticePresenter{Notice=n};var two=new ProgressNoticePresenter{Notice=n};Assert(!Object.ReferenceEquals(one.Content,two.Content) && Object.ReferenceEquals(one.Notice,two.Notice)); });
        Check("Progress card uses a bold title and real gradient icon", delegate { var p=new ProgressNoticePresenter{Notice=new ProgressNotice()};var blocks=Descendants((DependencyObject)p.Content).OfType<TextBlock>().ToArray();Assert(blocks.Any(t=>t.FontWeight==FontWeights.SemiBold) && blocks.Any(t=>t.Foreground is LinearGradientBrush)); });
        Check("Stable popup and item containers survive model rebinding without native display", delegate { var p=new ProgressNoticePresenter();var pop=Field(p,"popup");var rows=Field(p,"rows");var n=new ProgressNotice();p.Notice=n;n.Apply("file","one","started","a.cs");p.Notice=new ProgressNotice();Assert(Object.ReferenceEquals(pop,Field(p,"popup")) && Object.ReferenceEquals(rows,Field(p,"rows")) && !((Popup)pop).IsOpen); });
        Check("Closing and unloading an unshown presenter releases popup and timer", delegate { var p=new ProgressNoticePresenter{Notice=new ProgressNotice()};p.CloseDetails();p.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));Assert(!((Popup)Field(p,"popup")).IsOpen && !((System.Windows.Threading.DispatcherTimer)Field(p,"closeTimer")).IsEnabled); });
        Check("Historical details are displayed as plain text and expose no live rows", delegate { var n=new ProgressNotice(); n.Apply("file","one","completed","a.cs");var p=new ProgressNoticePresenter{Notice=ProgressNotice.RestoreSummary(n.MarkdownText)};Assert(((ItemsControl)Field(p,"rows")).Visibility==Visibility.Collapsed && ((TextBlock)Field(p,"archived")).Text.Contains("a.cs")); });
        Check("Progress kinds expose distinct status colors and useful safe subcategories", delegate { var n=new ProgressNotice();n.Apply("command","test","started","dotnet test");n.Apply("command","build","completed","MSBuild",0);n.Apply("file","file","failed","a.cs");Assert(n.Steps[0].Category.Contains("테스트") && n.Steps[1].Category.Contains("빌드") && n.Steps[2].Category.Contains("파일") && n.Steps.Select(s=>s.Accent).Distinct().Count()==3); });
        CheckControl();
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CheckControl
    /// desc         : 실제 제품 helper를 반사 호출하고 실제 ItemsSource의 메시지 변경을 관찰한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : private field 수정·Window.Show·CLI 실행 없는 요청 흐름 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void CheckControl()
    {
        AssistantControl control=null;
        Check("Actual control construction and progress helper contracts are available",delegate { control=new AssistantControl();Assert(new[]{"BeginProgressRequest","PresentProgressUpdate","HasProgressReply","CompleteProgressRequest","CloseProgressNotices"}.All(n=>control.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic)!=null)); });
        if(control==null)return;
        try
        {
            var messages=(ItemsControl)control.FindName("ConversationMessages");
            Check("Actual conversation ItemsSource is observable and remains available while unshown",delegate { Assert(messages!=null && messages.ItemsSource!=null && !control.IsVisible); });
            Invoke(control,"BeginProgressRequest");
            Check("Progress helper creates one actual system card for first work",delegate { Assert((bool)Invoke(control,"PresentProgressUpdate",new AgentProgress{Kind="command",WorkId="check1",State="started",Detail="dotnet test",Text="RAW_COMMAND_MUST_NOT_ENTER_HISTORY"}));Assert(Cards(messages).Length==1 && !Cards(messages)[0].Text.Contains("RAW_COMMAND")); });
            ChatMessage row=Cards(messages).FirstOrDefault();
            Check("Same WorkId completion updates the existing actual ChatMessage and durable Text",delegate { Assert((bool)Invoke(control,"PresentProgressUpdate",new AgentProgress{Kind="tool",WorkId="check1",State="completed",Detail="연결된 도구",ExitCode=7,Text="raw"}));Assert(Cards(messages).Length==1 && Object.ReferenceEquals(row,Cards(messages)[0]) && row.Text.Contains("종료 코드 7") && row.Text.Contains("테스트")); });
            Check("Repeated actual work event cannot append duplicate notices",delegate { Assert(!(bool)Invoke(control,"PresentProgressUpdate",new AgentProgress{Kind="tool",WorkId="check1",State="completed",Detail="연결된 도구",ExitCode=7,Text="raw"}) && Cards(messages).Length==1); });
            Check("Public commentary becomes one assistant body and final-reply dedup recognizes it",delegate { var p=new AgentProgress{Kind="message",Text="실제 공개 답변"};Assert((bool)Invoke(control,"PresentProgressUpdate",p) && !(bool)Invoke(control,"PresentProgressUpdate",p));Assert(Messages(messages).Count(m=>m.Role=="assistant" && m.Text==p.Text)==1 && (bool)Invoke(control,"HasProgressReply","  실제 공개 답변  ")); });
            Check("Final reply dedup requires full text rather than a prefix or truncated match",delegate { Assert(!(bool)Invoke(control,"HasProgressReply","실제 공개 답변 추가") && !(bool)Invoke(control,"HasProgressReply","실제"));var p=new AgentProgress{Kind="message",Text=new String('가',2001)};Invoke(control,"PresentProgressUpdate",p);Assert(!(bool)Invoke(control,"HasProgressReply",p.Text)); });
            Check("Command and file events remain one request card while public answer remains separate",delegate { Invoke(control,"PresentProgressUpdate",new AgentProgress{Kind="file",WorkId="check2",State="started",Detail="Program.cs",Text="raw"});Assert(Cards(messages).Length==1 && Cards(messages)[0].Text.Contains("Program.cs") && Messages(messages).Any(m=>m.Role=="assistant"&&m.Text=="실제 공개 답변")); });
            Check("Actual request cancellation records stopped work without false success",delegate { Invoke(control,"CompleteProgressRequest","cancelled");Assert(Cards(messages)[0].Text.Contains("요청 중지") && Cards(messages)[0].Text.Contains("중지") && !((ProgressNotice)new ProgressNoticeConverter().Convert(Cards(messages)[0],typeof(object),null,CultureInfo.InvariantCulture)).IsActive); });
            Check("Actual helper rejects late events after completion",delegate { string saved=row.Text;Assert(!(bool)Invoke(control,"PresentProgressUpdate",new AgentProgress{Kind="file",WorkId="late",State="completed",Detail="late.cs",Text="raw"}) && row.Text==saved); });
            Check("New request creates a different card and clears previous final reply ownership",delegate { Invoke(control,"BeginProgressRequest");Assert(!(bool)Invoke(control,"HasProgressReply","실제 공개 답변"));Invoke(control,"PresentProgressUpdate",new AgentProgress{Kind="tool",WorkId="newrequest",State="started",Detail="웹 검색",Text="raw"});Assert(Cards(messages).Length==2 && !Object.ReferenceEquals(Cards(messages)[0],Cards(messages)[1])); });
            var switchFixture=control.GetType().GetMethod("SwitchFixtureConversation");
            if(switchFixture!=null) Check("SOURCE FIXTURE ONLY owner switch rejects events for a different conversation",delegate { switchFixture.Invoke(control,null);Assert(!(bool)Invoke(control,"PresentProgressUpdate",new AgentProgress{Kind="file",WorkId="wrongowner",State="started",Detail="private.cs",Text="raw"}) && !(bool)Invoke(control,"HasProgressReply","실제 공개 답변")); });
            Check("Helper refuses a worker-thread mutation before touching actual WPF collections",delegate { bool rejected=false;var worker=new Thread(delegate(){try{Invoke(control,"PresentProgressUpdate",new AgentProgress{Kind="file",WorkId="thread",State="started",Detail="a.cs",Text="raw"});}catch(InvalidOperationException){rejected=true;}});worker.Start();worker.Join();Assert(rejected); });
            Invoke(control,"CompleteProgressRequest","failed");
            Invoke(control,"CloseProgressNotices");
        }
        catch(Exception e){Errors.Add("Control flow could not continue: "+e.GetType().Name+": "+e.Message);}
        finally { var disposable=control as IDisposable;if(disposable!=null)disposable.Dispose(); }
    }

    /// <summary>
    /// name         : Cards
    /// desc         : 실제 ItemsSource에서 요청 카드 메시지만 찾는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 화면 표시·private field 변경 없음.
    /// </summary>
    private static ChatMessage[] Cards(ItemsControl view){return Messages(view).Where(m=>m.Role=="system"&&(m.Text??"").StartsWith(ProgressNotice.MarkdownMarker,StringComparison.Ordinal)).ToArray();}
    /// <summary>
    /// name         : Messages
    /// desc         : 화면의 공개 ItemsSource를 통해 메시지를 관찰한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 내부 대화 사전을 테스트가 덮어쓰지 않음.
    /// </summary>
    private static IEnumerable<ChatMessage> Messages(ItemsControl view){return ((IEnumerable)view.ItemsSource).Cast<ChatMessage>();}
    /// <summary>
    /// name         : Invoke
    /// desc         : 제품 partial helper 메서드만 호출하고 실제 내부 예외를 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : private field 주입·변경 없음.
    /// </summary>
    private static object Invoke(object target,string method,params object[] args){try{return target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    /// <summary>
    /// name         : Field
    /// desc         : Presenter의 안정 컨테이너 참조와 타이머 상태를 읽기만 한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : Window나 Popup은 열지 않음.
    /// </summary>
    private static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);}
    /// <summary>
    /// name         : Descendants
    /// desc         : 논리 트리에서 공개 카드의 장식 구조를 관찰한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 스타일 객체를 검사.
    /// </summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var nested in Descendants(child))yield return nested;}
    /// <summary>
    /// name         : Check
    /// desc         : 개별 검증의 실제 성공·실패를 분리 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실패 뒤 후속 독립 경계도 검사.
    /// </summary>
    private static void Check(string name,Action action){try{action();Checks.Add(name);}catch(Exception e){Errors.Add(name+": "+e.GetType().Name+": "+e.Message);}}
    /// <summary>
    /// name         : Assert
    /// desc         : 의미 있는 상태 불일치를 실패로 만든다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 관찰값만 검증.
    /// </summary>
    private static void Assert(bool value){if(!value)throw new InvalidOperationException("Observed progress contract did not match.");}
}
