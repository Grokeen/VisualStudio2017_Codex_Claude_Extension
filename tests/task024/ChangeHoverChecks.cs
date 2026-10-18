using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ChangeHoverChecks
/// desc         : 명시 선택 때만 실제 자기 창·포인터로 변경 비교 Dropdown과 스크롤 수명을 검사한다.
/// author       : CodexCode
/// create date  : 2026-10-19
/// update date  : 2026-10-19 CodexCode : 실제 입력·합성 이벤트 분리, Desktop/전경/가림 guard와 first-failure 보존.
///                2026-10-19 CodexCode : C7 첫 클릭 계수 실패의 실제 포인터·down/up·capture 진단 추가.
/// </summary>
/// ---------------------------------------------------------------------
public static class ChangeHoverChecks
{
    public static readonly List<string> Checks=new List<string>(),Errors=new List<string>();
    public static readonly List<object> Observations=new List<object>();
    public static bool WindowShown,NativePointerUsed,HoverExecuted,NativeWheelExecuted,SyntheticWheelExecuted,PreconditionBlocked;
    public static string FailureKind="None";
    private static string directory;
    [StructLayout(LayoutKind.Sequential)]private struct POINT{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct MOUSEINPUT{public int dx,dy;public uint mouseData,dwFlags,time;public UIntPtr extra;}
    [StructLayout(LayoutKind.Sequential)]private struct KEYBDINPUT{public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
    [StructLayout(LayoutKind.Explicit)]private struct UNION{[FieldOffset(0)]public MOUSEINPUT mouse;[FieldOffset(0)]public KEYBDINPUT key;}
    [StructLayout(LayoutKind.Sequential)]private struct INPUT{public uint type;public UNION data;}
    [DllImport("user32.dll",SetLastError=true)]private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,INPUT[] input,int size);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")]private static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll")]private static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")]private static extern IntPtr GetAncestor(IntPtr handle,uint flag);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr handle,out uint process);
    [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr GetThreadDesktop(uint id);
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll",EntryPoint="GetUserObjectInformationW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool DesktopName(IntPtr handle,int index,StringBuilder value,uint size,out uint needed);

    /// <summary>
    /// name         : Run
    /// desc         : 사전 조건 통과 후에만 합성 문서의 native hover·휠·고정·닫힘을 순차 관찰한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : finally에서 자기 창을 닫고 포인터를 복원.
    /// </summary>
    public static void Run(string outputDirectory)
    {
        directory=outputDirectory;POINT original=new POINT();IntPtr previous=IntPtr.Zero;Window window=null;object controller=null;
        try
        {
            bool cursor=GetCursorPos(out original);int cursorError=cursor?0:Marshal.GetLastWin32Error();
            IntPtr input=OpenInputDesktop(0,false,1);int inputError=input==IntPtr.Zero?Marshal.GetLastWin32Error():0;string inputName;
            try{inputName=Name(input);}finally{if(input!=IntPtr.Zero)CloseDesktop(input);}
            string threadName=Name(GetThreadDesktop(GetCurrentThreadId()));
            Observe("Preflight",new{CursorAvailable=cursor,CursorError=cursorError,InputDesktop=inputName,InputDesktopWin32Error=inputError,ThreadDesktop=threadName,DesktopChanged=false});
            Guard(cursor && inputName=="Default" && threadName=="Default","Default input desktop and native cursor are required; product hover was not executed.");
            previous=GetForegroundWindow();
            var anchor=new Button{Content="sample.cs · 변경 비교",Height=38,Margin=new Thickness(16,12,16,0),HorizontalContentAlignment=HorizontalAlignment.Left};int clicks=0;
            anchor.Click+=(s,e)=>{clicks++;Observe("AnchorClickEvent",new{Clicks=clicks,SourceType=e.OriginalSource==null?null:e.OriginalSource.GetType().FullName});};
            anchor.AddHandler(Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler((s,e)=>Observe("AnchorMouseDown",new{Clicks=clicks,e.ClickCount,e.Handled,Button=e.ChangedButton.ToString(),State=e.ButtonState.ToString(),SourceType=e.OriginalSource==null?null:e.OriginalSource.GetType().FullName,Captured=Mouse.Captured==null?null:Mouse.Captured.GetType().FullName})),true);
            anchor.AddHandler(Mouse.PreviewMouseUpEvent,new MouseButtonEventHandler((s,e)=>Observe("AnchorMouseUp",new{Clicks=clicks,e.ClickCount,e.Handled,Button=e.ChangedButton.ToString(),State=e.ButtonState.ToString(),SourceType=e.OriginalSource==null?null:e.OriginalSource.GetType().FullName,Captured=Mouse.Captured==null?null:Mouse.Captured.GetType().FullName})),true);
            var content=new StackPanel();content.Children.Add(anchor);content.Children.Add(new Border{Height=1400});
            var background=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            var host=new Grid();host.Children.Add(background);
            var blank=new Border{Width=34,Height=34,Background=Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(16),Focusable=false};host.Children.Add(blank);
            string diff="--- sample.cs\n+++ sample.cs\n@@ -1,240 +1,240 @@\n"+String.Join("\n",Enumerable.Range(1,240).Select(i=>(i%3==0?"-":i%3==1?"+":" ")+"public string Value"+i+" = \"합성 비교\";"));
            ChangeDiffPopup.Attach(anchor,new ChangeCardFile(@"C:\synthetic\sample.cs",80,80,diff),"2026-10-19 · 합성 실행 변경 · +80 −80\n기록1\n기록2\n기록3");
            controller=Controller(anchor);var popup=(Popup)Read(controller,"popup");var body=(FrameworkElement)Read(controller,"body");var preview=(FrameworkElement)Read(controller,"preview");var editor=(RichTextBox)Read(preview,"editor");var pin=(Button)Read(controller,"pin");
            var work=SystemParameters.WorkArea;window=new Window{Title="TASK024_CHANGE_HOVER_TEST",Width=320,Height=Math.Min(700,work.Height-40),Left=work.Left+24,Top=work.Top+20,Content=host,ShowInTaskbar=false,WindowStyle=WindowStyle.ToolWindow,ResizeMode=ResizeMode.NoResize,Topmost=true};
            window.Show();WindowShown=true;window.Activate();Pump(180);IntPtr own=new WindowInteropHelper(window).Handle;
            Guard(Connected(anchor),"Anchor is not connected/measured.");
            if(GetForegroundWindow()!=own)SetForegroundWindow(own);Pump(80);
            if(!OwnForeground())ActivateBlank(blank,own);
            Guard(OwnForeground() && OwnPoint(anchor),"Own foreground or uncovered anchor required before physical input.");
            Check("Native: active test owner and exact anchor native surface are verified",true);
            Check("Structure: StaysOpen=true preserves capture-free popup lifetime",popup.StaysOpen);
            int opened=0,closed=0;popup.Opened+=(s,e)=>opened++;popup.Closed+=(s,e)=>closed++;
            Move(anchor);Pump(1000);HoverExecuted=true;
            Observe("AnchorDwell",new{Opened=opened,Closed=closed,Open=popup.IsOpen,Connected=Connected(body)});
            Check("Native: one second anchor dwell opens exactly once without a close",popup.IsOpen && opened==1 && closed==0);
            Check("Native: opened body has an actual PresentationSource and finite measured bounds",Connected(body));
            Check("Native: narrow 320px owner keeps popup width and height finite inside screen work area",body.ActualWidth<=Math.Min(680,work.Width-24)+1 && body.ActualHeight<=Math.Min(420,work.Height-40)+1);
            var top=body.PointToScreen(new Point());var bottom=body.PointToScreen(new Point(body.ActualWidth,body.ActualHeight));
            Check("Native: actual popup rectangle remains inside the visible work area",top.X>=work.Left-2 && top.Y>=work.Top-2 && bottom.X<=work.Right+2 && bottom.Y<=work.Bottom+2);
            Move(editor);Pump(100);
            Check("Native: pointer crosses from anchor into actual diff body before grace expires",popup.IsOpen && body.IsMouseOver);
            int nativeDelta=0;editor.AddHandler(Mouse.PreviewMouseWheelEvent,new MouseWheelEventHandler((s,e)=>nativeDelta=e.Delta),true);
            double parentOffset=background.VerticalOffset,old=editor.VerticalOffset;
            Wheel(-120);Pump(180);NativeWheelExecuted=true;
            Check("Native: SendInput negative wheel data is received with the actual negative routed sign",nativeDelta<0);
            Check("Native: physical wheel over diff scrolls its real document down",editor.VerticalOffset>old);
            Check("Native: physical diff scrolling leaves background conversation offset unchanged",background.VerticalOffset==parentOffset);
            old=editor.VerticalOffset;Wheel(120);Pump(160);
            Check("Native: SendInput positive wheel data is received with the actual positive sign",nativeDelta>0);
            Check("Native: positive physical wheel moves the diff back up",editor.VerticalOffset<old);
            editor.ScrollToEnd();Pump(100);old=editor.VerticalOffset;Wheel(-120);Pump(120);
            Check("Native: wheel at the document end does not leak to the conversation",editor.VerticalOffset==old && background.VerticalOffset==parentOffset);
            editor.ScrollToHome();Pump(80);Wheel(120);Pump(120);
            Check("Native: upward wheel at document start is also contained",editor.VerticalOffset==0 && background.VerticalOffset==parentOffset);
            Move(pin);Click();Pump(80);
            Check("Native: explicit pin click fixes the existing connected popup",(bool)Read(controller,"pinned") && popup.IsOpen);
            Move(blank);Pump(400);
            Check("Native: pinned popup stays connected when pointer leaves it",popup.IsOpen && Connected(body));
            Guard(OwnPoint(blank) && (WindowFromPoint(Center(blank))==own || GetAncestor(WindowFromPoint(Center(blank)),2)==own),"Outside click neutral target is not the own owner window.");Click();Pump(200);
            Check("Native: nonfocusable owner blank click closes pinned popup",!popup.IsOpen && PresentationSource.FromVisual(body)==null);
            Move(anchor);Pump(120);Move(pin);Click();Pump(100);Keys(0x1B);Pump(160);
            Check("Native: Escape closes pinned popup and restores keyboard focus to anchor",!popup.IsOpen && anchor.IsKeyboardFocused);
            Move(blank);Pump(280);Move(anchor);Pump(120);
            POINT clickPoint;bool cursorRead=GetCursorPos(out clickPoint);POINT expectedClick=Center(anchor);
            Observe("BeforeOriginalAnchorClick",new{Clicks=clicks,CursorRead=cursorRead,CursorX=clickPoint.X,CursorY=clickPoint.Y,ExpectedX=expectedClick.X,ExpectedY=expectedClick.Y,anchor.IsMouseOver,anchor.IsPressed,anchor.IsEnabled,anchor.IsKeyboardFocused,Captured=Mouse.Captured==null?null:Mouse.Captured.GetType().FullName,Modifiers=Keyboard.Modifiers.ToString(),NativeTarget=WindowFromPoint(clickPoint).ToInt64(),OwnHandle=own.ToInt64()});
            Click();Pump(120);
            Observe("AfterOriginalAnchorClick",new{Clicks=clicks,anchor.IsMouseOver,anchor.IsPressed,anchor.IsKeyboardFocused,Captured=Mouse.Captured==null?null:Mouse.Captured.GetType().FullName});
            Check("Native: original anchor click action still runs exactly once",clicks==1);
            Move(blank);Pump(400);Check("Native: unpinned popup closes after pointer leaves both surfaces",!popup.IsOpen);
            anchor.Focus();Keys(0x12,0x28);Pump(150);
            Check("Native: Alt+Down opens the same accessible pinned detail",popup.IsOpen && (bool)Read(controller,"pinned") && body.IsKeyboardFocusWithin);
            Keys(0x1B);Pump(140);
            Check("Native: keyboard reopen supports the same Escape cleanup",!popup.IsOpen);
            Move(blank);Pump(260);Move(anchor);Pump(150);Check("Native: a live connected detail exists before unload regression",popup.IsOpen && Connected(body));
            content.Children.Remove(anchor);Pump(150);
            Check("Native: removing the actual anchor unloads and closes its popup",!popup.IsOpen && !body.IsLoaded);
            Check("Native: anchor unload detaches owner handlers and close timer",Read(controller,"owner")==null && !((DispatcherTimer)Read(controller,"closeTimer")).IsEnabled);
            SyntheticWheel();
            window.Close();window=null;Pump(80);
            Check("Lifecycle: owner close leaves no connected popup or dispatcher timer",!popup.IsOpen && !((DispatcherTimer)Read(controller,"closeTimer")).IsEnabled);
        }
        catch(PreconditionException error){PreconditionBlocked=true;FailureKind="Precondition";Errors.Add(error.Message);}
        catch(Exception error){FailureKind="ProductAssertionOrHarness";Errors.Add(error.ToString());}
        finally
        {
            bool owned=OwnForeground();
            if(window!=null){window.Topmost=false;window.Close();}
            if(NativePointerUsed){SetCursorPos(original.X,original.Y);}
            if(owned && previous!=IntPtr.Zero && IsWindow(previous) && OwnForeground())SetForegroundWindow(previous);
            Observe("Finally",new{WindowShown,NativePointerUsed,HoverExecuted,NativeWheelExecuted,SyntheticWheelExecuted,PreconditionBlocked,FailureKind});
        }
    }
    /// <summary>
    /// name         : SyntheticWheel
    /// desc         : native 관찰과 별도로 합성 routed wheel의 끝 위치 소비만 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 합성 이벤트 성공을 물리 마우스 결과로 계산하지 않음.
    /// </summary>
    private static void SyntheticWheel()
    {
        var leaf=new Border{Height=1200};var inner=new ScrollViewer{Height=100,Content=leaf};var content=new StackPanel();content.Children.Add(inner);content.Children.Add(new Border{Height=1200});var outer=new ScrollViewer{Height=200,Width=250,Content=content};HoverScrollPolicy.SetContainMouseWheel(inner,true);HoverScrollPolicy.SetContainMouseWheel(outer,true);outer.Measure(new Size(250,200));outer.Arrange(new Rect(0,0,250,200));outer.UpdateLayout();inner.ScrollToEnd();Pump(80);var before=outer.VerticalOffset;
        var args=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=Mouse.PreviewMouseWheelEvent};leaf.RaiseEvent(args);Pump(80);SyntheticWheelExecuted=true;
        Check("Synthetic: routed wheel is consumed by closest nested viewer at its end",args.Handled);
        Check("Synthetic: nested end leaves parent offset unchanged",outer.VerticalOffset==before);
    }
    /// <summary>
    /// name         : Controller
    /// desc         : 실제 제품의 약한 anchor registry에서 해당 controller만 조회한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 제품 private 상태를 주입하지 않음.
    /// </summary>
    private static object Controller(FrameworkElement anchor){var table=typeof(ChangeDiffPopup).GetField("Attached",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);var values=new object[]{anchor,null};table.GetType().GetMethod("TryGetValue").Invoke(table,values);return values[1];}
    /// <summary>
    /// name         : ActivateBlank
    /// desc         : 활성화가 거부되면 정확히 자기 창의 무동작 빈 요소만 한 번 클릭한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 다른 앱·제목의 버튼을 클릭하지 않음.
    /// </summary>
    private static void ActivateBlank(FrameworkElement blank,IntPtr owner){Guard(!blank.Focusable && OwnPoint(blank),"Neutral activation point is not owned.");POINT point=Center(blank);IntPtr native=WindowFromPoint(point);Guard(native==owner || GetAncestor(native,2)==owner,"Activation point belongs to a popup or foreign window.");Guard(SetCursorPos(point.X,point.Y),"Activation cursor move failed.");NativePointerUsed=true;Guard(OwnPoint(blank),"Activation point changed.");SendBatch(new[]{new INPUT{type=0,data=new UNION{mouse=new MOUSEINPUT{dwFlags=2}}},new INPUT{type=0,data=new UNION{mouse=new MOUSEINPUT{dwFlags=4}}}});Pump(150);}
    /// <summary>
    /// name         : Move
    /// desc         : 자기 native surface의 유한 중앙 좌표로만 이동하고 실제 cursor 위치를 재확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 가려진 UI나 비활성 desktop에 입력하지 않음.
    /// </summary>
    private static void Move(FrameworkElement element){Guard(OwnForeground() && OwnPoint(element),"Pointer target or foreground is foreign.");POINT point=Center(element);Guard(SetCursorPos(point.X,point.Y),"SetCursorPos failed.");NativePointerUsed=true;POINT actual;Guard(GetCursorPos(out actual) && Math.Abs(actual.X-point.X)<=2 && Math.Abs(actual.Y-point.Y)<=2,"Actual pointer did not reach verified target.");}
    /// <summary>
    /// name         : Wheel
    /// desc         : 부호를 확인한 SendInput wheel을 실제 포인터 위치에만 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : native routed delta와 문서 offset을 별도로 관찰.
    /// </summary>
    private static void Wheel(int delta){GuardInput();var input=new INPUT{type=0,data=new UNION{mouse=new MOUSEINPUT{dwFlags=0x0800,mouseData=unchecked((uint)delta)}}};Guard(unchecked((int)input.data.mouse.mouseData)==delta,"Wheel data sign was corrupted.");Send(input);}
    /// <summary>
    /// name         : Click
    /// desc         : 전경과 현재 포인터의 자기 소유권을 재검사한 후 click pair를 보낸다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 소스 이동 대신 합성 anchor counter만 실행.
    /// </summary>
    private static void Click(){GuardInput();SendBatch(new[]{new INPUT{type=0,data=new UNION{mouse=new MOUSEINPUT{dwFlags=2}}},new INPUT{type=0,data=new UNION{mouse=new MOUSEINPUT{dwFlags=4}}}});}
    /// <summary>
    /// name         : Keys
    /// desc         : 자기 전경에서 Escape·Alt+아래의 전체 down/up을 단일 SendInput batch로 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : modifier 해제까지 한 번에 enqueue하여 중간 전제 실패로 키가 남지 않게 함.
    /// </summary>
    private static void Keys(params ushort[] values){Guard(OwnForeground(),"Native key withheld: foreign foreground.");var inputs=new List<INPUT>();foreach(ushort value in values)inputs.Add(new INPUT{type=1,data=new UNION{key=new KEYBDINPUT{vk=value,flags=0}}});foreach(ushort value in values.Reverse())inputs.Add(new INPUT{type=1,data=new UNION{key=new KEYBDINPUT{vk=value,flags=2}}});SendBatch(inputs.ToArray());}
    /// <summary>
    /// name         : Send
    /// desc         : 정확한 ABI 크기의 한 INPUT 전송 성공 수를 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : UIPI 차단 등을 성공으로 추정하지 않음.
    /// </summary>
    private static void Send(INPUT value){SendBatch(new[]{value});}
    /// <summary>
    /// name         : SendBatch
    /// desc         : click과 modifier의 완전한 sequence를 단일 native 요청으로 보낸다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 부분 전송은 keyup/leftup 정리 후 전제 실패로 보존.
    /// </summary>
    private static void SendBatch(INPUT[] values){uint count=SendInput((uint)values.Length,values,Marshal.SizeOf(typeof(INPUT)));if(count==(uint)values.Length)return;int error=Marshal.GetLastWin32Error();if(count>0){var releases=values.Where(v=>v.type==1).Select(v=>new INPUT{type=1,data=new UNION{key=new KEYBDINPUT{vk=v.data.key.vk,flags=2}}}).ToList();releases.Add(new INPUT{type=0,data=new UNION{mouse=new MOUSEINPUT{dwFlags=4}}});SendInput((uint)releases.Count,releases.ToArray(),Marshal.SizeOf(typeof(INPUT)));}throw new PreconditionException("SendInput partial/failure "+count+"/"+values.Length+" Win32="+error);}
    /// <summary>
    /// name         : GuardInput
    /// desc         : 실제 cursor와 native target의 프로세스 소유권을 입력 직전에 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 외부 앱 입력은 전제 실패로 중단.
    /// </summary>
    private static void GuardInput(){POINT point;Guard(GetCursorPos(out point) && OwnForeground(),"Cursor/foreground became unavailable.");uint process;GetWindowThreadProcessId(WindowFromPoint(point),out process);Guard(process==(uint)Process.GetCurrentProcess().Id,"Cursor native target is foreign.");}
    /// <summary>
    /// name         : OwnPoint
    /// desc         : 실제 HwndSource와 해당 화면 지점의 최상위 native 창을 대조한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 자기 process의 다른 popup 가림도 구별.
    /// </summary>
    private static bool OwnPoint(FrameworkElement element){if(!Connected(element))return false;var source=PresentationSource.FromVisual(element) as HwndSource;if(source==null)return false;IntPtr native=WindowFromPoint(Center(element));uint process;GetWindowThreadProcessId(native,out process);return process==(uint)Process.GetCurrentProcess().Id && (native==source.Handle || GetAncestor(native,2)==source.Handle);}
    /// <summary>
    /// name         : Center
    /// desc         : 연결된 요소의 유한 중앙 화면 좌표를 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 위치를 추측하지 않음.
    /// </summary>
    private static POINT Center(FrameworkElement element){var point=element.PointToScreen(new Point(element.ActualWidth/2,element.ActualHeight/2));return new POINT{X=(int)point.X,Y=(int)point.Y};}
    /// <summary>
    /// name         : Connected
    /// desc         : 실제 표시 연결과 유한 양수 layout을 함께 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : IsOpen만으로 실제 표시를 추정하지 않음.
    /// </summary>
    private static bool Connected(FrameworkElement element){return element.IsLoaded && element.IsVisible && PresentationSource.FromVisual(element)!=null && element.ActualWidth>0 && element.ActualHeight>0 && !Double.IsInfinity(element.ActualWidth) && !Double.IsInfinity(element.ActualHeight) && !Double.IsNaN(element.ActualWidth) && !Double.IsNaN(element.ActualHeight);}
    /// <summary>
    /// name         : OwnForeground
    /// desc         : 현재 전경 process만 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 전경을 임의로 강제하지 않음.
    /// </summary>
    private static bool OwnForeground(){uint process;GetWindowThreadProcessId(GetForegroundWindow(),out process);return process==(uint)Process.GetCurrentProcess().Id;}
    /// <summary>
    /// name         : Name
    /// desc         : 읽기 전용 desktop 이름을 260자 버퍼에만 저장한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : desktop 전환·설정 변경 없음.
    /// </summary>
    private static string Name(IntPtr desktop){if(desktop==IntPtr.Zero)return "Unavailable";var value=new StringBuilder(260);uint needed;return DesktopName(desktop,2,value,520,out needed)?value.ToString():"Unavailable";}
    /// <summary>
    /// name         : Observe
    /// desc         : preflight·수명 단계·실제 실행 flag를 sidecar에 즉시 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 내용·자격정보 대신 synthetic UI 상태만 기록.
    /// </summary>
    private static void Observe(string stage,object value){Observations.Add(new{Stage=stage,At=DateTimeOffset.Now.ToString("o"),Value=value});File.WriteAllText(Path.Combine(directory,"change-hover-diagnostics.json"),new JavaScriptSerializer().Serialize(Observations),new UTF8Encoding(false));}
    /// <summary>
    /// name         : Pump
    /// desc         : 짧고 유한한 dispatcher frame으로 실제 입력·popup 이벤트를 처리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 외부 프로세스나 사용자 응답을 무한 대기하지 않음.
    /// </summary>
    private static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    /// <summary>
    /// name         : Read
    /// desc         : 실제 제품 private field를 읽기만 한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : popup 강제 open이나 상태 주입 없음.
    /// </summary>
    private static object Read(object value,string field){return value.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    /// <summary>
    /// name         : Check
    /// desc         : 첫 실제 실패를 기록하고 후속 위험 입력을 중단한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 미실행 단계를 pass 처리하지 않음.
    /// </summary>
    private static void Check(string name,bool passed){if(!passed)throw new InvalidOperationException(name);Checks.Add(name);Observe(name,new{Passed=true});}
    /// <summary>
    /// name         : Guard
    /// desc         : 안전 전제를 제품 assertion 실패와 분리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 잠금 상태를 성공·제품 결함으로 바꾸지 않음.
    /// </summary>
    private static void Guard(bool passed,string message){if(!passed)throw new PreconditionException(message);}
    /// <summary>
    /// name         : PreconditionException
    /// desc         : 실제 제품 동작 전제 실패의 분류다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : ProductAssertion과 구별.
    /// </summary>
    private sealed class PreconditionException:Exception
    {
        /// <summary>
        /// name         : PreconditionException
        /// desc         : 전제 실패 원문을 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 예외 원문을 성공으로 덮지 않음.
        /// </summary>
        internal PreconditionException(string value):base(value){}
    }
}
