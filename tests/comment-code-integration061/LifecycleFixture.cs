using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommentCodePreview.Poc;
using VS2017.AIAssistant.Providers;

// Real adapter/runner/job with controlled owned EXE children only. No vendor CLI,
// Visual Studio, user buffer, credentials, or remote inference is executed.
internal static class LifecycleFixture
{
    private static int passed;
    private static void Check(bool value, string name) { if(!value)throw new Exception(name); passed++; Console.WriteLine("PASS "+name); }
    private static int Main(string[] args)
    {
        Console.InputEncoding=new System.Text.UTF8Encoding(false);Console.OutputEncoding=new System.Text.UTF8Encoding(false);
        if(args.Length>0&&args[0]=="fixture-parent") return Parent(args[1]);
        string folder=Environment.GetEnvironmentVariable("COMMENTCODE_LIFECYCLE_CAPTURE");
        if(String.IsNullOrWhiteSpace(folder)) return 91;
        if(args.Length>0&&args[0]=="--owned-grandchild")
        { File.WriteAllText(Path.Combine(folder,"grandchild-ready.txt"),"ready"); Thread.Sleep(30000); return 0; }
        Thread.Sleep(150); // allow the actual runner to attach its Job before the subtree is created
        string input = Console.In.ReadToEnd();
        File.WriteAllText(Path.Combine(folder,"child.pid"),Process.GetCurrentProcess().Id.ToString());
        string exe=System.Reflection.Assembly.GetExecutingAssembly().Location;
        using(var child=Process.Start(new ProcessStartInfo(exe,"--owned-grandchild"){UseShellExecute=false,CreateNoWindow=true}))
        {
            File.WriteAllText(Path.Combine(folder,"grandchild.pid"),child.Id.ToString()); Thread.Sleep(30000);
            // If Job cancellation fails, this intentionally attempts a late valid response.
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            int json = input.IndexOf("\n{",StringComparison.Ordinal);
            var payload = serializer.Deserialize<System.Collections.Generic.Dictionary<string,object>>(input.Substring(json+1));
            string before = (string)payload["source_text"];
            var proposal = new { summary="Synthetic late response", edits=new[]{new{old_text=before,new_text=before+"int late = 1;"}} };
            File.WriteAllText(Path.Combine(folder,"late-response.txt"),"attempted");
            Console.WriteLine(Array.IndexOf(args,"--print")>=0 ? serializer.Serialize(new{is_error=false,structured_output=proposal}) : serializer.Serialize(proposal));
        }
        return 0;
    }
    private static bool Alive(int pid)
    { try { using(var process=Process.GetProcessById(pid))return !process.HasExited; }catch(ArgumentException){return false;} }
    private static bool WaitFor(Func<bool> ready,int milliseconds)
    {var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<milliseconds){if(ready())return true;Thread.Sleep(20);}return ready();}
    private static void CleanupKnown(string folder,string exe)
    {
        foreach(string name in new[]{"child.pid","grandchild.pid"})
        {
            string path=Path.Combine(folder,name);int pid;if(!File.Exists(path)||!Int32.TryParse(File.ReadAllText(path),out pid))continue;
            try { using(var process=Process.GetProcessById(pid)) { if(!process.HasExited&&String.Equals(process.MainModule.FileName,exe,StringComparison.OrdinalIgnoreCase))process.Kill(); } }
            catch(ArgumentException){}catch(InvalidOperationException){}
        }
    }
    private static int Parent(string output)
    {
        string exe=System.Reflection.Assembly.GetExecutingAssembly().Location;
        try
        {
            Environment.SetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH",Path.Combine(output,"absent-cache.json"));
            foreach(string brand in new[]{"Codex","Claude"})foreach(string mode in new[]{"cancel","timeout"})
            {
                string folder=Path.Combine(output,brand+"-"+mode);Directory.CreateDirectory(folder);
                Environment.SetEnvironmentVariable("COMMENTCODE_LIFECYCLE_CAPTURE",folder);
                try
                {
                    using(var cancellation=new CancellationTokenSource())
                    {
                        var runner=new CliEditProvider(mode=="timeout"?TimeSpan.FromSeconds(3):TimeSpan.FromSeconds(10),true);
                        var adapter=new CommentCodeProviderAdapter(runner,brand,exe,"C:/synthetic/Unsaved.cs","explicit-model");
                        string text="// 합성\r\n";var snapshot=new EditorSnapshot("opaque",3,0,text,text.Length,text.Length,0,false,false,false,false);
                        Task<string> request=adapter.GenerateAsync(new CommentCodeRequest(1,"// 합성",snapshot),cancellation.Token);
                        bool ready=WaitFor(()=>File.Exists(Path.Combine(folder,"grandchild-ready.txt")),2000);
                        Check(ready,brand+" "+mode+" actual runner owned child/grandchild started");
                        int child=Int32.Parse(File.ReadAllText(Path.Combine(folder,"child.pid"))),grandchild=Int32.Parse(File.ReadAllText(Path.Combine(folder,"grandchild.pid")));
                        if(mode=="cancel")cancellation.Cancel();
                        bool finished=WaitFor(()=>request.IsCompleted,6000);
                        Check(finished,brand+" "+mode+" request completes within bounded wait");
                        bool expected=false;try{request.GetAwaiter().GetResult();}catch(OperationCanceledException){expected=mode=="cancel";}catch(TimeoutException){expected=mode=="timeout";}
                        Check(expected,brand+" "+mode+" exact cancellation/timeout failure propagated");
                        Check(WaitFor(()=>!Alive(child)&&!Alive(grandchild),3000),brand+" "+mode+" owned Job terminated child and grandchild");
                        Check(!File.Exists(Path.Combine(folder,"late-response.txt")),brand+" "+mode+" owned producer stopped before planned late output");
                        File.WriteAllText(Path.Combine(folder,"observed.txt"),"child="+child+";grandchild="+grandchild+";bothExited=true;mode="+mode);
                    }
                }
                finally { CleanupKnown(folder,exe); }
            }
            Console.WriteLine("RESULT "+passed+"/"+passed+" PASS; ActualCLIProductRunner=true; SyntheticOwnedProcesses=true; ActualAI=false; ActualIDE=false; ControllerUIExecuted=false");return 0;
        }
        catch(Exception error){Console.Error.WriteLine("FAIL "+error);return 1;}
    }
}
