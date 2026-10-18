using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
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
/// name         : ProgressHoverChecks
/// desc         : 명시적 선택 실행에서만 실제 포인터로 hover·이동·고정·Escape·해제를 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 기본 headless 검사와 분리하고 포인터·전경 창을 finally에서 복원.
/// </summary>
/// ---------------------------------------------------------------------
public static class ProgressHoverChecks
{
    public static readonly List<string> Checks=new List<string>(),Errors=new List<string>();
    public static bool WindowShown{get;private set;}
    public static bool NativePointerUsed{get;private set;}
    public static bool HoverExecuted{get;private set;}
    private static readonly List<string> snapshots=new List<string>();
    private static string diagnosticPath;
    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X,Y; }
    [DllImport("user32.dll",SetLastError=true)] private static extern bool GetCursorPos(out CursorPoint point);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SetCursorPos(int x,int y);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr GetThreadDesktop(uint threadId);
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint desiredAccess);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll",EntryPoint="GetUserObjectInformationW",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool DesktopName(IntPtr desktop,int index,StringBuilder output,uint bytes,out uint needed);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(CursorPoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window,uint flag);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
    [DllImport("user32.dll")] private static extern void keybd_event(byte virtualKey,byte scanCode,uint flags,UIntPtr extra);

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 짧은 독립 창에서 anchor 유지·상세 진입·외부 이탈·실제 click과 Escape·수명 종료를 관찰한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 사용자 문서·CLI·계정에 접근하지 않는 선택적 GUI 검사.
    ///                2026-10-18 CodexOrig : 검증된 자기 빈 영역의 최초 활성화 후 기존 전경·대상 guard를 유지.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run()
    {
        snapshots.Clear();diagnosticPath=Path.Combine(Path.GetDirectoryName(typeof(ProgressNoticePresenter).Assembly.Location),"progress-hover-diagnostics.json");
        WindowShown=false;NativePointerUsed=false;HoverExecuted=false;
        CursorPoint original;
        bool cursorAvailable=GetCursorPos(out original);int cursorError=cursorAvailable?0:Marshal.GetLastWin32Error();
        DesktopPreflight(cursorAvailable,cursorError);
        if(!cursorAvailable)throw new InvalidOperationException("Native cursor is unavailable (Win32 error "+cursorError+"); hover was not tested. See progress-hover-diagnostics.json.");
        IntPtr foreground=GetForegroundWindow(); Window window=null;
        ProgressNoticePresenter p=null;Button header=null;Popup popup=null;FrameworkElement body=null;
        try
        {
            var n=new ProgressNotice();n.Apply("command","hover-test","started","dotnet test");
            p=new ProgressNoticePresenter{Notice=n,Margin=new Thickness(14),VerticalAlignment=VerticalAlignment.Top};
            var hostSurface=new Grid();hostSurface.Children.Add(p);
            var ownerBlank=new Border{Width=8,Height=28,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Background=Brushes.Transparent,Focusable=false};hostSurface.Children.Add(ownerBlank);
            var work=SystemParameters.WorkArea;
            window=new Window{Title="TASK022_PROGRESS_HOVER_TEST",Width=420,Height=185,Left=Math.Max(work.Left,work.Right-450),Top=Math.Max(work.Top,work.Top+60),Content=hostSurface,WindowStyle=WindowStyle.ToolWindow,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true};
            window.Show();WindowShown=true;window.Activate();Pump(180);
            header=(Button)Read(p,"header");popup=(Popup)Read(p,"popup");body=(FrameworkElement)Read(p,"popupBody");
            int opened=0,closedCount=0;
            popup.Opened+=delegate{opened++;Snapshot("Popup.Opened #"+opened,window,popup,body,header);};
            popup.Closed+=delegate{closedCount++;Snapshot("Popup.Closed #"+closedCount,window,popup,body,header);};
            body.Loaded+=delegate{Snapshot("Body.Loaded",window,popup,body,header);};
            body.Unloaded+=delegate{Snapshot("Body.Unloaded",window,popup,body,header);};
            Snapshot("BeforeAnchorMove",window,popup,body,header);
            if(!WaitForConnected(header,null,600))throw new InvalidOperationException("Header was not connected and measured within the bounded startup wait.");
            IntPtr ownWindow=new WindowInteropHelper(window).Handle;
            if(!window.IsActive||GetForegroundWindow()!=ownWindow)SetForegroundWindow(ownWindow);
            if(GetForegroundWindow()!=ownWindow)ActivateOwnBlank(window,ownerBlank,ownWindow);
            var foregroundWait=Stopwatch.StartNew();
            while(foregroundWait.ElapsedMilliseconds<600&&(!window.IsActive||GetForegroundWindow()!=ownWindow))Pump(25);
            Snapshot("ProtectedWindowBeforeInput",window,popup,body,header);
            if(!window.IsActive||GetForegroundWindow()!=ownWindow||!OwnsNativeForeground()||!OwnsElementPoint(header))throw new InvalidOperationException("Native hover withheld because the test window foreground or anchor native target is not owned by the test process.");
            Move(header);Pump(1000);HoverExecuted=true;
            Snapshot("AfterAnchorDwell1000ms opened="+opened+" closed="+closedCount,window,popup,body,header);
            Check("Native pointer remains on anchor for one second with one open no close and a connected body",popup.IsOpen&&opened==1&&closedCount==0&&body.IsLoaded&&PresentationSource.FromVisual(body)!=null);
            bool ready=WaitForConnected(body,popup,600);
            Snapshot("BeforeBodyMove ready="+ready,window,popup,body,header);
            Check("Open dropdown child becomes connected and measured within bounded 600ms wait",ready&&Object.ReferenceEquals(popup.Child,body));
            if(ready){Move(body);Pump(230);Snapshot("AfterBodyEntry",window,popup,body,header);Check("Native pointer crosses into dropdown before hover grace expires",popup.IsOpen&&body.IsMouseOver);}
            else Errors.Add("Dropdown body entry was not attempted because its real PresentationSource or finite measured bounds were unavailable.");
            MoveOutside(window);Pump(450);Check("Native pointer outside anchor and dropdown closes unpinned detail",!popup.IsOpen);
            Snapshot("AfterPointerOutside",window,popup,body,header);
            Move(header);Pump(80);
            if(!OwnsNativeInput())throw new InvalidOperationException("Native click/key input withheld because the foreground or pointer target belongs to another process.");
            mouse_event(0x0002,0,0,0,UIntPtr.Zero);mouse_event(0x0004,0,0,0,UIntPtr.Zero);Pump(150);
            Snapshot("AfterNativeClick",window,popup,body,header);
            MoveOutside(window);Pump(300);Check("Native click pins connected dropdown while pointer moves elsewhere",popup.IsOpen&&body.IsLoaded&&PresentationSource.FromVisual(body)!=null);
            if(!OwnsOwnerBlankPoint(ownerBlank,ownWindow))throw new InvalidOperationException("Outside click withheld because the nonfocusable blank point is not on the own native owner window.");
            Move(ownerBlank);Pump(25);
            if(!OwnsNativeInput()||!OwnsOwnerBlankPoint(ownerBlank,ownWindow))throw new InvalidOperationException("Outside click withheld because the verified owner target changed.");
            mouse_event(0x0002,0,0,0,UIntPtr.Zero);mouse_event(0x0004,0,0,0,UIntPtr.Zero);Pump(230);
            Snapshot("AfterOwnerNonfocusableBlankClick",window,popup,body,header);
            Check("Native click on verified nonfocusable owner blank closes pinned dropdown",!popup.IsOpen&&PresentationSource.FromVisual(body)==null);
            Move(header);Pump(80);
            if(!OwnsNativeInput())throw new InvalidOperationException("Header reopen click withheld because the native target changed.");
            mouse_event(0x0002,0,0,0,UIntPtr.Zero);mouse_event(0x0004,0,0,0,UIntPtr.Zero);Pump(180);
            Snapshot("AfterHeaderNativeReopen",window,popup,body,header);
            Check("Header native click reopens the connected shared dropdown after outside close",popup.IsOpen&&body.IsLoaded&&PresentationSource.FromVisual(body)!=null);
            if(!OwnsNativeForeground())throw new InvalidOperationException("Native Escape withheld because another process owns the foreground.");
            keybd_event(0x1B,0,0,UIntPtr.Zero);keybd_event(0x1B,0,2,UIntPtr.Zero);Pump(150);
            Snapshot("AfterNativeEscape",window,popup,body,header);
            Check("Native Escape closes dropdown and restores keyboard anchor",!popup.IsOpen&&header.IsKeyboardFocused);
            MoveOutside(window);Pump(230);Move(header);Pump(230);
            Check("A connected live dropdown is present before owner unload cleanup",popup.IsOpen&&body.IsLoaded&&PresentationSource.FromVisual(body)!=null);
            window.Topmost=false;window.Close();window=null;Pump(120);
            Check("Window closure releases popup and hover timer",!popup.IsOpen&&!((DispatcherTimer)Read(p,"closeTimer")).IsEnabled);
        }
        finally
        {
            Snapshot("FinallyBeforeClose",window,popup,body,header);
            uint activeProcess;GetWindowThreadProcessId(GetForegroundWindow(),out activeProcess);
            bool ownedForeground=activeProcess==(uint)Process.GetCurrentProcess().Id;
            if(window!=null){try{window.Topmost=false;}finally{window.Close();}}
            if(NativePointerUsed)SetCursorPos(original.X,original.Y);
            // 검사 중 다른 프로그램으로 사용자가 이동했으면 그 전경 창을 빼앗지 않는다.
            if(ownedForeground&&foreground!=IntPtr.Zero&&IsWindow(foreground))
            {
                uint remainingProcess;GetWindowThreadProcessId(GetForegroundWindow(),out remainingProcess);
                if(remainingProcess==(uint)Process.GetCurrentProcess().Id)SetForegroundWindow(foreground);
            }
        }
    }
    /// <summary>
    /// name         : Move
    /// desc         : 실제 화면 좌표에 있는 안전 테스트 요소의 중심으로 포인터를 이동한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 포인터 이동 실패는 검증 실패로 처리.
    /// </summary>
    private static void Move(FrameworkElement element){if(!OwnsNativeForeground()||!OwnsElementPoint(element))throw new InvalidOperationException("Native pointer move withheld because foreground or element native target is foreign.");var point=element.PointToScreen(new Point(Math.Max(2,element.ActualWidth/2),Math.Max(2,element.ActualHeight/2)));if(!SetCursorPos((int)point.X,(int)point.Y))throw new InvalidOperationException("Native cursor move failed (Win32 error "+Marshal.GetLastWin32Error()+").");NativePointerUsed=true;}

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ActivateOwnBlank
    /// desc         : 전경 전환이 거부되면 자기 테스트 창의 검증된 빈 영역만 한 번 클릭한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 다른 앱 입력 없이 실제 전경 소유 검사를 유지하는 테스트 시작 조건 보완.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ActivateOwnBlank(Window window,FrameworkElement blank,IntPtr handle)
    {
        if(blank.Focusable||!FinitePositive(blank.ActualWidth)||!FinitePositive(blank.ActualHeight)||!OwnsElementPoint(blank))throw new InvalidOperationException("Test activation withheld: neutral target is not on the own window.");
        var point=blank.PointToScreen(new Point(blank.ActualWidth/2,blank.ActualHeight/2));
        var cursorPoint=new CursorPoint{X=(int)point.X,Y=(int)point.Y};
        IntPtr target=WindowFromPoint(cursorPoint);uint process;GetWindowThreadProcessId(handle,out process);
        if(process!=(uint)Process.GetCurrentProcess().Id||(target!=handle&&GetAncestor(target,2)!=handle))throw new InvalidOperationException("Neutral activation belongs to a foreign window.");
        if(!SetCursorPos((int)point.X,(int)point.Y))throw new InvalidOperationException("Test activation cursor is unavailable.");
        NativePointerUsed=true;
        target=WindowFromPoint(cursorPoint);
        if(!OwnsElementPoint(blank)||(target!=handle&&GetAncestor(target,2)!=handle))throw new InvalidOperationException("Test activation target changed before click.");
        mouse_event(0x0002,0,0,0,UIntPtr.Zero);mouse_event(0x0004,0,0,0,UIntPtr.Zero);Pump(150);
        Snapshot("AfterOwnNeutralActivation",window,null,null,null);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DesktopPreflight
    /// desc         : 첫 포인터 조회가 실패해도 현재 thread·input desktop의 읽기 상태와 이름을 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 읽기 전용 DESKTOP_READOBJECTS만 요청하고 desktop 전환·권한 변경은 하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void DesktopPreflight(bool cursorAvailable,int cursorError)
    {
        var data=new Dictionary<string,string>();data["Stage"]="NativeInputPreflight";data["At"]=DateTimeOffset.Now.ToString("O");
        data["GetCursorPosSucceeded"]=cursorAvailable.ToString();data["GetCursorPosWin32Error"]=cursorError.ToString(CultureInfo.InvariantCulture);
        IntPtr thread=GetThreadDesktop(GetCurrentThreadId());int threadError=thread==IntPtr.Zero?Marshal.GetLastWin32Error():0;
        data["ThreadDesktopName"]=ReadDesktopName(thread);data["ThreadDesktopWin32Error"]=threadError.ToString(CultureInfo.InvariantCulture);
        IntPtr input=OpenInputDesktop(0,false,0x0001);int inputError=input==IntPtr.Zero?Marshal.GetLastWin32Error():0;
        try{data["OpenInputDesktopSucceeded"]=(input!=IntPtr.Zero).ToString();data["OpenInputDesktopWin32Error"]=inputError.ToString(CultureInfo.InvariantCulture);data["InputDesktopName"]=ReadDesktopName(input);}
        finally{if(input!=IntPtr.Zero)CloseDesktop(input);}
        data["DesktopChanged"]="False";AppendDiagnostic(data);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadDesktopName
    /// desc         : desktop 이름만 유한 버퍼로 읽고 창·계정 목록을 수집하지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : GetThreadDesktop의 빌린 핸들은 닫지 않고 OpenInputDesktop 소유 핸들만 별도 정리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string ReadDesktopName(IntPtr handle){if(handle==IntPtr.Zero)return "Unavailable";var name=new StringBuilder(260);uint needed;if(!DesktopName(handle,2,name,520,out needed))return "Unavailable (Win32 error "+Marshal.GetLastWin32Error()+")";return name.ToString();}

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AppendDiagnostic
    /// desc         : 실제 실행 여부와 진단을 sidecar에 남기며 검사 요청과 실제 GUI 실행을 구분한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : preflight 실패에도 sidecar를 보존하고 제품 결과를 성공으로 바꾸지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void AppendDiagnostic(Dictionary<string,string> data)
    {
        snapshots.Add("{"+String.Join(",",data.Select(pair=>Json(pair.Key)+":"+Json(pair.Value)))+"}");
        if(snapshots.Count<=120)File.WriteAllText(diagnosticPath,"{\"AssemblyPath\":"+Json(typeof(ProgressNoticePresenter).Assembly.Location)+",\"ProductStateModified\":false,\"WindowShown\":"+(WindowShown?"true":"false")+",\"NativePointerUsed\":"+(NativePointerUsed?"true":"false")+",\"HoverExecuted\":"+(HoverExecuted?"true":"false")+",\"Events\":["+String.Join(",",snapshots)+"]}",new UTF8Encoding(true));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WaitForConnected
    /// desc         : 실제 source·측정값이 준비될 때만 좌표를 사용하며 닫힌 Popup을 강제로 다시 열지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 최대 600ms의 dispatcher-load 대기만 허용하고 제품 상태·Measure를 조작하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool WaitForConnected(FrameworkElement element,Popup popup,int milliseconds)
    {
        var watch=Stopwatch.StartNew();
        do
        {
            if(popup!=null&&!popup.IsOpen)return false;
            if(element.IsLoaded&&PresentationSource.FromVisual(element)!=null&&element.IsMeasureValid&&element.IsArrangeValid&&FinitePositive(element.ActualWidth)&&FinitePositive(element.ActualHeight))return true;
            if(watch.ElapsedMilliseconds>=milliseconds)return false;Pump(25);
        }while(true);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Snapshot
    /// desc         : Popup 실제 child·연결·측정·초점·native 전경을 별도 JSON에 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 새 실행 디렉터리만 기록하고 최초 실패 자료·제품 파일은 수정하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Snapshot(string stage,Window window,Popup popup,FrameworkElement body,FrameworkElement header)
    {
        try
        {
        var data=new Dictionary<string,string>();data["Stage"]=stage;data["At"]=DateTimeOffset.Now.ToString("O");
        data["OwnerActive"]=window==null?"null":window.IsActive.ToString();data["OwnForeground"]=OwnsNativeForeground().ToString();
        data["OwnerTopmost"]=window==null?"null":window.Topmost.ToString();
        data["ForegroundHwnd"]=GetForegroundWindow().ToInt64().ToString(CultureInfo.InvariantCulture);
        data["PopupIsOpen"]=popup==null?"null":popup.IsOpen.ToString();data["PopupIsLoaded"]=popup==null?"null":popup.IsLoaded.ToString();
        data["PopupChildIsBody"]=popup==null?"null":Object.ReferenceEquals(popup.Child,body).ToString();data["PopupChildType"]=popup==null?"null":TypeName(popup.Child);
        data["PopupLogicalParent"]=popup==null?"null":TypeName(LogicalTreeHelper.GetParent(popup));
        data["PopupStaysOpen"]=popup==null?"null":popup.StaysOpen.ToString();
        data["Header"]=Describe(header);data["Body"]=Describe(body);data["MouseCaptured"]=TypeName(Mouse.Captured);data["MouseDirectlyOver"]=TypeName(Mouse.DirectlyOver);data["KeyboardFocused"]=TypeName(Keyboard.FocusedElement);
        CursorPoint point;data["NativeCursor"]=GetCursorPos(out point)?point.X+","+point.Y:"unavailable";
        AppendDiagnostic(data);
        Console.WriteLine("[HoverDiagnostic] "+stage+"; open="+data["PopupIsOpen"]+"; ownerActive="+data["OwnerActive"]+"; body="+data["Body"]);
        }
        catch(Exception error) { Errors.Add("Hover diagnostics could not be recorded at "+stage+": "+error.GetType().Name); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Describe
    /// desc         : 연결 전 PointToScreen 예외도 진단값으로 남기고 실측 source·bounds를 구분한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 연결되지 않은 Visual을 연결된 것으로 추정하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Describe(FrameworkElement element)
    {
        if(element==null)return "null";var source=PresentationSource.FromVisual(element);var hwnd=source as HwndSource;
        string bounds;try{var at=element.PointToScreen(new Point(0,0));bounds=at.X.ToString("R",CultureInfo.InvariantCulture)+","+at.Y.ToString("R",CultureInfo.InvariantCulture);}catch(Exception error){bounds=error.GetType().Name+":"+error.Message;}
        return "Loaded="+element.IsLoaded+",Visible="+element.IsVisible+",Source="+TypeName(source)+",Hwnd="+(hwnd==null?"null":hwnd.Handle.ToInt64().ToString(CultureInfo.InvariantCulture))+",Root="+(source==null?"null":TypeName(source.RootVisual))+",Actual="+element.ActualWidth.ToString("R",CultureInfo.InvariantCulture)+"x"+element.ActualHeight.ToString("R",CultureInfo.InvariantCulture)+",Desired="+element.DesiredSize+",Render="+element.RenderSize+",MeasureValid="+element.IsMeasureValid+",ArrangeValid="+element.IsArrangeValid+",MouseOver="+element.IsMouseOver+",KeyboardWithin="+element.IsKeyboardFocusWithin+",VisualParent="+TypeName(VisualTreeHelper.GetParent(element))+",Screen="+bounds;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : OwnsNativeInput
    /// desc         : 실제 click 전에 전경과 포인터의 native 대상이 검사 프로세스인지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 다른 프로그램으로 click·key가 전달되는 하네스 오동작 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool OwnsNativeInput(){CursorPoint point;if(!GetCursorPos(out point)||!OwnsNativeForeground())return false;uint target;GetWindowThreadProcessId(WindowFromPoint(point),out target);return target==(uint)Process.GetCurrentProcess().Id;}

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : OwnsElementPoint
    /// desc         : 포인터 이동 전에 실제 요소의 중앙 native 창이 검사 프로세스 소유인지 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 가려진 anchor를 다른 프로그램의 포인터 동작으로 대체하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool OwnsElementPoint(FrameworkElement element){if(PresentationSource.FromVisual(element)==null)return false;var point=element.PointToScreen(new Point(Math.Max(2,element.ActualWidth/2),Math.Max(2,element.ActualHeight/2)));uint target;GetWindowThreadProcessId(WindowFromPoint(new CursorPoint{X=(int)point.X,Y=(int)point.Y}),out target);return target==(uint)Process.GetCurrentProcess().Id;}

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : OwnsOwnerBlankPoint
    /// desc         : 비초점 빈 요소의 실제 native 대상이 Popup이나 다른 앱이 아닌 소유 창인지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 안전한 owner PreviewMouseDown 경로만 native click으로 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool OwnsOwnerBlankPoint(FrameworkElement blank,IntPtr ownerWindow){if(blank.Focusable||!OwnsNativeForeground()||!OwnsElementPoint(blank))return false;var point=blank.PointToScreen(new Point(blank.ActualWidth/2,blank.ActualHeight/2));IntPtr target=WindowFromPoint(new CursorPoint{X=(int)point.X,Y=(int)point.Y});return target==ownerWindow||GetAncestor(target,2)==ownerWindow;}

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : OwnsNativeForeground
    /// desc         : 현재 전경 창의 프로세스가 실제 검사 실행기인지 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 전경 소유권을 강제로 바꾸지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool OwnsNativeForeground(){uint active;GetWindowThreadProcessId(GetForegroundWindow(),out active);return active==(uint)Process.GetCurrentProcess().Id;}

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FinitePositive
    /// desc         : 실제 pointer 좌표에 사용할 수 있는 유한 양수 bounds를 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : NaN·Infinity·아직 측정되지 않은 0 크기 거부.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool FinitePositive(double value){return value>0&&!Double.IsNaN(value)&&!Double.IsInfinity(value);}

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TypeName
    /// desc         : 본문·명령·비밀 없이 UI 객체의 타입만 진단에 넣는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : null은 명시적으로 기록.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string TypeName(object value){return value==null?"null":value.GetType().FullName;}

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Json
    /// desc         : 추가 라이브러리 없이 진단 문자열의 제어 문자·따옴표를 JSON에 안전하게 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 하네스 예외 문구가 진단 JSON 구조를 깨지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Json(string text){var result=new StringBuilder("\"");foreach(char value in text??""){switch(value){case '\\':result.Append("\\\\");break;case '"':result.Append("\\\"");break;case '\r':result.Append("\\r");break;case '\n':result.Append("\\n");break;case '\t':result.Append("\\t");break;default:if(Char.IsControl(value))result.Append("\\u"+((int)value).ToString("x4",CultureInfo.InvariantCulture));else result.Append(value);break;}}return result.Append('"').ToString();}
    /// <summary>
    /// name         : MoveOutside
    /// desc         : 테스트 창 왼쪽 바깥으로 포인터를 이동한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 다른 프로그램의 버튼을 click하지 않음.
    /// </summary>
    private static void MoveOutside(Window window){if(!OwnsNativeForeground())throw new InvalidOperationException("Native outside move withheld because another process owns the foreground.");if(!SetCursorPos((int)Math.Max(SystemParameters.WorkArea.Left+2,window.Left-45),(int)(window.Top+window.Height+30)))throw new InvalidOperationException("Native cursor move failed.");NativePointerUsed=true;}
    /// <summary>
    /// name         : Pump
    /// desc         : WPF의 실제 hover·Popup·타이머를 유한 시간 처리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : GUI 검사에서만 사용하고 외부 프로세스는 기다리지 않음.
    /// </summary>
    private static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    /// <summary>
    /// name         : Read
    /// desc         : 실제 Presenter의 Popup·컨테이너 참조만 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : private field는 변경하지 않음.
    /// </summary>
    private static object Read(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
    /// <summary>
    /// name         : Check
    /// desc         : 실제 GUI 관찰 성공·실패를 분리 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실행하지 않은 단계는 성공으로 기록하지 않음.
    /// </summary>
    private static void Check(string name,bool passed){if(passed)Checks.Add(name);else Errors.Add(name);}
}
