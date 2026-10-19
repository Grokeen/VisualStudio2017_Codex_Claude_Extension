using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
/// ---------------------------------------------------------------------
/// <summary>
/// name         : RequestQueueRunner
/// desc         : 백그라운드 CLI 콜백이 PowerShell runspace에 의존하지 않는 STA 검사 진입점.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 실제 프로세스 검사와 실패 원문 저장.
/// </summary>
/// ---------------------------------------------------------------------
internal static class RequestQueueRunner
{
    [STAThread]
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 격리된 요청 큐 검사에서 Main 경로를 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException+=(sender,eventArgs)=>{
            var exception=eventArgs.ExceptionObject as Exception;
            try { File.WriteAllText(args[2]+".unhandled.txt",exception==null?"non exception":exception.GetType().FullName+"\n"+exception.Message+"\n"+exception.StackTrace); } catch {}
        };
        var json=new JavaScriptSerializer{MaxJsonLength=16000000};
        var map=json.Deserialize<Dictionary<string,string>>(File.ReadAllText(args[1]));
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,eventArgs)=>{
            string name=new AssemblyName(eventArgs.Name).Name;
            string sibling=Path.Combine(Path.GetDirectoryName(args[0]),name+".dll");
            if(File.Exists(sibling))return Assembly.LoadFrom(sibling);
            return map.ContainsKey(name)?Assembly.LoadFrom(map[name]):null;
        };
        System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
        Assembly.LoadFrom(args[0]);
        return Execute(args,json);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Execute
    /// desc         : 검사 결과·해시·시간과 종료 코드를 같은 결과 파일에 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Execute(string[] args,JavaScriptSerializer json)
    {
        if(args[3]=="performance")RequestQueueChecks.RunPerformanceOnly();else if(args[3]=="status")RequestQueueChecks.RunStatusOnly();else RequestQueueChecks.Run();
        var result=new{Succeeded=RequestQueueChecks.Failed.Count==0,Passed=RequestQueueChecks.Passed.Count,Failed=RequestQueueChecks.Failed.Count,Checks=RequestQueueChecks.Passed,Errors=RequestQueueChecks.Failed,StatusObservations=RequestQueueChecks.StatusObservations,SwitchElapsedMilliseconds=RequestQueueChecks.SwitchElapsedMilliseconds,SwitchDiskWrites=RequestQueueChecks.SwitchDiskWrites,ProductAssemblySha256=args[4],NetworkRequested=false,LiveAiUsed=false};
        string payload=json.Serialize(result);File.WriteAllText(args[2],payload,new System.Text.UTF8Encoding(true));Console.WriteLine(payload);
        return result.Succeeded?0:1;
    }
}
