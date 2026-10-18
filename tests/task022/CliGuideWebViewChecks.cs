using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Wpf;
using VS2017.AIAssistant.Services;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : CliGuideWebViewChecks
/// desc         : 예약된 선택 검사에서만 실제 WebView PDF 탐색·원본 Source·Runtime·네이티브 화면을 기록한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : installer callback은 실행 없는 거부 fixture이며 기본 검사에서는 호출하지 않음.
/// </summary>
/// ---------------------------------------------------------------------
public static class CliGuideWebViewChecks
{
    public static readonly List<string> Checks=new List<string>(),Errors=new List<string>();
    public static string RuntimeVersion="",Source="",ScreenshotPath="";
    [StructLayout(LayoutKind.Sequential)] private struct WindowRect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] private struct ScreenPoint{public int X,Y;}
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle,out WindowRect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(ScreenPoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window,uint flag);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int metric);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool GetCursorPos(out ScreenPoint point);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle,uint message,IntPtr word,IntPtr data);
    private static bool nativeActivationUsed;

    /// <summary>
    /// name         : Run
    /// desc         : 실제 child HWND가 포함된 창 화면을 PNG로 보관하고 모든 자식 UI를 종료한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 사용자 포인터는 움직이지 않고 다른 사람이 옮긴 전경 창은 복원하지 않음.
    ///                2026-10-18 CodexOrig : 전경이 거부될 때만 검증한 자기 창 제목을 클릭하고 포인터를 복원.
    /// </summary>
    public static void Run(string packageRoot,string outputDirectory)
    {
        AssistantControl host=null;CliGuideDialog dialog=null;IntPtr prior=GetForegroundWindow();int installerAttempts=0;bool priorTopmost=false,dialogClosed=false;
        string captureDiagnostic="not attempted";bool capturePerformed=false;
        nativeActivationUsed=false;ScreenPoint originalCursor;bool hasOriginalCursor=GetCursorPos(out originalCursor);
        var service=new CliGuideService(packageRoot,delegate(ProcessStartInfo info){installerAttempts++;throw new InvalidOperationException("Installer is disabled in the WebView test.");},delegate(ProcessStartInfo info){throw new InvalidOperationException("External PDF app is disabled in the WebView test.");});
        try
        {
            host=new AssistantControl();dialog=new CliGuideDialog(host,service);priorTopmost=dialog.Topmost;dialog.Topmost=true;dialog.Show();dialog.Activate();
            var watch=Stopwatch.StartNew();WebView2 view=null;
            while(watch.ElapsedMilliseconds<20000)
            {
                Pump(100);view=Read(dialog,"browser") as WebView2;
                if(view!=null&&view.CoreWebView2!=null){RuntimeVersion=view.CoreWebView2.Environment.BrowserVersionString;Source=view.CoreWebView2.Source;}
                if((bool)Read(dialog,"pdfLoaded"))break;
                if(!(bool)Read(dialog,"loading")&&view==null)break;
            }
            Check("Actual WebView reports successful local PDF NavigationCompleted",(bool)Read(dialog,"pdfLoaded"));
            Check("Actual WebView Source is exactly the installed encoded Korean PDF",service.IsAllowedNavigation(Source)&&new Uri(Source).LocalPath==service.GetPdfPath());
            Check("Actual WebView runtime version is reported",!String.IsNullOrWhiteSpace(RuntimeVersion));
            Check("Actual WebView uses the isolated dedicated profile directory",Directory.Exists(service.GetBrowserDataDirectory()));
            // NavigationCompleted 이후 실제 PDF 렌더에 유한한 2초를 주고 자기 창만 캡처한다.
            dialog.Activate();ActivateOwnCaption(dialog);Pump(2000);
            ScreenshotPath=Path.Combine(outputDirectory,"cli-guide-native-window.png");
            WindowRect rect;IntPtr handle=new WindowInteropHelper(dialog).Handle;
            if(!GetWindowRect(handle,out rect)||rect.Right<=rect.Left||rect.Bottom<=rect.Top)throw new InvalidOperationException("Native guide window rectangle is unavailable.");
            uint activeProcess;IntPtr activeWindow=GetForegroundWindow();GetWindowThreadProcessId(activeWindow,out activeProcess);
            bool ownedForeground=activeProcess==(uint)Process.GetCurrentProcess().Id;
            bool validRect=IsWindow(handle)&&InsideVirtualScreen(rect)&&(long)(rect.Right-rect.Left)*(rect.Bottom-rect.Top)<=20000000;
            int cleanProbes=validRect?CountOwnWindowProbes(handle,rect):0;
            captureDiagnostic="Topmost="+dialog.Topmost+"; OwnerActive="+dialog.IsActive+"; ForegroundHwnd="+activeWindow.ToInt64()+"; ForegroundProcess="+activeProcess+"; OwnProcess="+Process.GetCurrentProcess().Id+"; NativeRect="+rect.Left+","+rect.Top+","+rect.Right+","+rect.Bottom+"; InsideVirtualScreen="+validRect+"; UnoccludedProbes="+cleanProbes+"/9; AdditionalRenderWaitMs=2000";
            Check("Capture foreground belongs to the actual test process",ownedForeground&&dialog.IsActive);
            Check("Native capture rectangle is finite bounded and inside the virtual screen",validRect);
            Check("Native window probes show the guide above foreign application windows",cleanProbes==9);
            if(!ownedForeground||!dialog.IsActive||!validRect||cleanProbes!=9)throw new InvalidOperationException("Guide screenshot withheld because foreground rectangle or occlusion checks failed. "+captureDiagnostic);
            using(var image=new Bitmap(rect.Right-rect.Left,rect.Bottom-rect.Top))
            {using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(rect.Left,rect.Top,0,0,image.Size,CopyPixelOperation.SourceCopy);image.Save(ScreenshotPath,ImageFormat.Png);}
            capturePerformed=true;
            Check("Native screen PNG includes the real child HWND rather than WPF-only rendering",File.Exists(ScreenshotPath)&&new FileInfo(ScreenshotPath).Length>1000);
            Check("Actual WebView display never invoked an installer",installerAttempts==0&&!service.IsInstallationRunning);
            dialog.Topmost=priorTopmost;dialog.Close();dialogClosed=true;Check("Closing actual guide releases its WebView instance",Read(dialog,"browser")==null);
        }
        finally
        {
            uint currentProcess;GetWindowThreadProcessId(GetForegroundWindow(),out currentProcess);bool owned=currentProcess==(uint)Process.GetCurrentProcess().Id;
            try{if(dialog!=null&&!dialogClosed){try{dialog.Topmost=priorTopmost;}finally{dialog.Close();}}}finally{var disposable=host as IDisposable;if(disposable!=null)disposable.Dispose();}
            if(nativeActivationUsed&&hasOriginalCursor)SetCursorPos(originalCursor.X,originalCursor.Y);
            File.WriteAllText(Path.Combine(outputDirectory,"cli-guide-webview-capture-diagnostics.json"),"{\"CapturePerformed\":"+(capturePerformed?"true":"false")+",\"AdditionalRenderWaitMs\":2000,\"Details\":"+Json(captureDiagnostic)+",\"Source\":"+Json(Source)+",\"RuntimeVersion\":"+Json(RuntimeVersion)+",\"ScreenshotPath\":"+Json(ScreenshotPath)+"}",new UTF8Encoding(true));
            if(owned&&prior!=IntPtr.Zero&&IsWindow(prior)){uint remaining;GetWindowThreadProcessId(GetForegroundWindow(),out remaining);if(remaining==(uint)Process.GetCurrentProcess().Id)SetForegroundWindow(prior);}
        }
    }
    /// <summary>
    /// name         : Pump
    /// desc         : 비동기 WebView 이벤트를 유한한 WPF dispatcher 프레임으로 처리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 선택 GUI 검사에만 사용.
    /// </summary>
    private static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    /// <summary>
    /// name         : Read
    /// desc         : 실제 Dialog의 완료 상태·브라우저 참조만 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : private field 변경 없음.
    /// </summary>
    private static object Read(object value,string name){return value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    /// <summary>
    /// name         : Check
    /// desc         : 실제 선택 GUI 검증 결과를 기본 구조 검사와 별도 목록으로 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : NavigationCompleted와 시각 검토용 PNG를 각각 기록.
    /// </summary>
    private static void Check(string name,bool passed){if(passed)Checks.Add(name);else Errors.Add(name);}

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : InsideVirtualScreen
    /// desc         : DPI 문맥이 같은 Win32 virtual-screen 좌표와 캡처 사각형을 비교한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 화면 밖 영역이나 잘못된 크기를 캡처하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool InsideVirtualScreen(WindowRect rect){int left=GetSystemMetrics(76),top=GetSystemMetrics(77),width=GetSystemMetrics(78),height=GetSystemMetrics(79);return width>0&&height>0&&rect.Right>rect.Left&&rect.Bottom>rect.Top&&rect.Left>=left&&rect.Top>=top&&rect.Right<=left+width&&rect.Bottom<=top+height;}

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CountOwnWindowProbes
    /// desc         : WebView 자식 HWND도 최상위 guide HWND로 판별하여 다른 앱의 가림을 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : browser child 프로세스 PID를 호스트 PID와 혼동하지 않고 포인터도 움직이지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int CountOwnWindowProbes(IntPtr guide,WindowRect rect)
    {
        int count=0;int[] xs={rect.Left+15,(rect.Left+rect.Right)/2,rect.Right-15};int[] ys={rect.Top+15,(rect.Top+rect.Bottom)/2,rect.Bottom-15};
        foreach(int x in xs)foreach(int y in ys){IntPtr target=WindowFromPoint(new ScreenPoint{X=x,Y=y});if(target==guide||GetAncestor(target,2)==guide)count++;}return count;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ActivateOwnCaption
    /// desc         : 자기 창의 비동작 제목 영역을 native hit-test로 확인한 뒤 필요한 경우에만 클릭한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 전경 차단 환경에서도 설치 버튼·외부 앱을 누르지 않고 캡처 전경 조건을 충족.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ActivateOwnCaption(Window window)
    {
        IntPtr handle=new WindowInteropHelper(window).Handle;
        SetForegroundWindow(handle);Pump(100);
        if(GetForegroundWindow()==handle)return;
        WindowRect rect;if(!GetWindowRect(handle,out rect)||!InsideVirtualScreen(rect))throw new InvalidOperationException("Own caption rectangle unavailable.");
        var point=new ScreenPoint{X=(rect.Left+rect.Right)/2,Y=rect.Top+12};
        var packed=new IntPtr(((point.Y&65535)<<16)|(point.X&65535));
        IntPtr target=WindowFromPoint(point);uint process;GetWindowThreadProcessId(handle,out process);
        if(process!=(uint)Process.GetCurrentProcess().Id||(target!=handle&&GetAncestor(target,2)!=handle)||SendMessage(handle,0x0084,IntPtr.Zero,packed).ToInt64()!=2)throw new InvalidOperationException("Own neutral caption target was not verified; activation withheld.");
        if(!SetCursorPos(point.X,point.Y))throw new InvalidOperationException("Native caption activation cursor unavailable.");nativeActivationUsed=true;
        target=WindowFromPoint(point);
        if((target!=handle&&GetAncestor(target,2)!=handle)||SendMessage(handle,0x0084,IntPtr.Zero,packed).ToInt64()!=2)throw new InvalidOperationException("Own caption target changed before activation click.");
        mouse_event(0x0002,0,0,0,UIntPtr.Zero);mouse_event(0x0004,0,0,0,UIntPtr.Zero);Pump(150);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Json
    /// desc         : 캡처 진단 경로와 Source를 독립 JSON 문자열로 안전하게 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 원본 성공 JSON·가려진 PNG는 덮어쓰지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Json(string value){return "\""+(value??"").Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n").Replace("\t","\\t")+"\"";}
}
