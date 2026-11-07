using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using CommentCodePreview.Poc;
using VS2017.AIAssistant.Providers;

// These are deliberately fake host metadata/discovery boundaries. The actual
// CliEditProvider/ProcessJob/ProviderExecutionOptions/adapter run against a local
// synthetic EXE, never an AI CLI, authenticated service, or Visual Studio.
namespace VS2017.AIAssistant.Chat
{
    public sealed class ChatAttachment { public bool IsImage; public string MediaType, Text, Name, ImageBase64; }
    public sealed class ChatMessage { public string Role, Text; }
    public sealed class ChatRequestContext
    {
        public string Model, ReasoningEffort = "auto", Speed = "standard", EffectiveInstructions = "synthetic safety", AgentContext = "";
        public ChatAttachment[] Attachments = new ChatAttachment[0]; public ChatMessage[] History = new ChatMessage[0];
        public ChatRequestContext(string model = "") { Model = model; }
    }
}
namespace VS2017.AIAssistant.Providers
{
    internal static class NativeCliDiscovery { internal static string Find(string provider) { throw new Exception("Native discovery/fallback forbidden by fixture."); } }
}
internal static class NativeWireFixture
{
    private static int count;
    private static void Check(bool pass, string name) { if (!pass) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    private static bool Pair(string[] args, string key, string value) { for(int i=0;i<args.Length-1;i++) if(args[i]==key && args[i+1]==value)return true; return false; }
    private static int Main(string[] args)
    {
        Console.InputEncoding = new System.Text.UTF8Encoding(false); Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        if(args.Length != 0 && args[0] == "fixture-parent") return Parent(args[1]);
        // Synthetic child emulates only stdin/envelope, not vendor inference or security implementation.
        try
        {
            Thread.Sleep(100);
            string capture = Environment.GetEnvironmentVariable("COMMENTCODE_SYNTHETIC_CAPTURE");
            if(String.IsNullOrEmpty(capture)) throw new Exception("Missing synthetic capture root");
            var serializer = new JavaScriptSerializer { MaxJsonLength=16000000 };
            if(args.Contains("--version")) { Console.WriteLine("synthetic 2.1.302"); return 0; }
            File.WriteAllText(Path.Combine(capture,"args.json"),serializer.Serialize(args));
            File.WriteAllText(Path.Combine(capture,"cwd.txt"),Environment.CurrentDirectory);
            string input=Console.In.ReadToEnd();
            File.WriteAllText(Path.Combine(capture,"stdin.txt"),input);
            if(Environment.GetEnvironmentVariable("COMMENTCODE_SYNTHETIC_FAIL")=="1") return 27;
            int json=input.IndexOf("\n{",StringComparison.Ordinal);
            var root=serializer.Deserialize<Dictionary<string,object>>(input.Substring(json+1));
            string source=(string)root["source_text"];
            string proposed=source.Replace("/*INSERT*/","int generated = 7;/*INSERT*/");
            var proposal=new { summary="Synthetic response, not actual AI", edits=new[]{new{old_text=source,new_text=proposed}} };
            Console.WriteLine(args.Contains("--print") ? serializer.Serialize(new{is_error=false,structured_output=proposal}) : serializer.Serialize(proposal));
            return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error.GetType().Name); return 55; }
    }
    private static int Parent(string output)
    {
        try
        {
            Directory.CreateDirectory(output);
            Environment.SetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH",Path.Combine(output,"absent-cache.json"));
            string executable=System.Reflection.Assembly.GetExecutingAssembly().Location;
            foreach(string brand in new[]{"Codex","Claude"})
            {
                string folder=Path.Combine(output,brand);Directory.CreateDirectory(folder);
                Environment.SetEnvironmentVariable("COMMENTCODE_SYNTHETIC_CAPTURE",folder);
                string text="// 합성 주석\r\nclass C { /*INSERT*/ }\r\n";
                int cursor=text.IndexOf("/*INSERT*/",StringComparison.Ordinal);
                var snapshot=new EditorSnapshot("opaque",3,2,text,cursor,cursor,0,false,false,false,false);
                var provider=new CommentCodeProviderAdapter(brand,executable,"C:/synthetic/Example.cs","explicit-model");
                string result=provider.GenerateAsync(new CommentCodeRequest(1,"// 상수 생성",snapshot),CancellationToken.None).GetAwaiter().GetResult();
                Check(result=="int generated = 7;",brand+" actual source adapter/CLI runner synthetic child result");
                string[] wire=new JavaScriptSerializer().Deserialize<string[]>(File.ReadAllText(Path.Combine(folder,"args.json")));
                Check(Pair(wire,"--model","explicit-model"),brand+" exact requested model wire");
                Check(!wire.Contains("--dangerously-bypass-approvals-and-sandbox")&&!wire.Contains("--dangerously-skip-permissions"),brand+" no FullAccess flags");
                Check(!String.Equals(File.ReadAllText(Path.Combine(folder,"cwd.txt")),Environment.CurrentDirectory,StringComparison.OrdinalIgnoreCase),brand+" isolated cwd");
                string prompt=File.ReadAllText(Path.Combine(folder,"stdin.txt"));
                Check(prompt.Contains("Example.cs")&&prompt.Contains("source_text")&&!prompt.Contains("C:/synthetic"),brand+" basename only and synthetic captured source on stdin");
                if(brand=="Codex")
                {
                    Check(Pair(wire,"--sandbox","read-only")&&wire.Contains("--ignore-user-config")&&wire.Contains("--ignore-rules"),"Codex readonly/user/rules controls");
                    foreach(string feature in new[]{"shell_tool","unified_exec","shell_snapshot","hooks","plugins","apps"})Check(Pair(wire,"--disable",feature),"Codex disable "+feature);
                    Check(Pair(wire,"--config","web_search=\"disabled\"")&&Pair(wire,"--config","mcp_servers={}"),"Codex explicit web/MCP settings");
                }
                else
                {
                    Check(Pair(wire,"--tools","")&&Pair(wire,"--disallowedTools","mcp__*"),"Claude built-in and MCP tools restricted");
                    Check(wire.Contains("--strict-mcp-config")&&Pair(wire,"--setting-sources","")&&wire.Contains("--disable-slash-commands"),"Claude strict MCP/settings/skills controls");
                    int index=Array.IndexOf(wire,"--settings");var setting=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(wire[index+1]);
                    Check(wire.Count(x=>x=="--settings")==1&&Equals(setting["disableAllHooks"],true)&&Equals(setting["fastMode"],false),"Claude single settings retains standard fastMode");
                }
                Environment.SetEnvironmentVariable("COMMENTCODE_SYNTHETIC_FAIL","1");
                bool failed=false;try{provider.GenerateAsync(new CommentCodeRequest(2,"// 거절 응답",snapshot),CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException){failed=true;}
                finally {Environment.SetEnvironmentVariable("COMMENTCODE_SYNTHETIC_FAIL",null);}
                Check(failed,brand+" CLI request rejection is failure");
                Check(Pair(new JavaScriptSerializer().Deserialize<string[]>(File.ReadAllText(Path.Combine(folder,"args.json"))),"--model","explicit-model"),brand+" failure kept requested model/provider");
            }
            var fast=new List<string>{"--settings","{\"fastMode\":true}","--effort","high"};CliEditProvider.RestrictCommentCodeArguments("Claude",fast);
            var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(fast[fast.IndexOf("--settings")+1]);
            Check(Equals(data["fastMode"],true)&&Equals(data["disableAllHooks"],true)&&fast.Contains("high"),"Claude fast and effort retained under hook setting merge");
            var field=typeof(CliEditProvider).GetField("commentCodePreviewOnly",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            Check(Equals(field.GetValue(new CliEditProvider()),false)&&Equals(field.GetValue(new CliEditProvider(TimeSpan.FromSeconds(1))),false),"legacy constructors keep default false");
            Check(Equals(field.GetValue(new CliEditProvider(TimeSpan.FromSeconds(1),true)),true),"preview explicit opt-in true");
            Console.WriteLine("RESULT "+count+"/"+count+" PASS; SyntheticSubprocess=true; VendorCLIExecuted=false; ActualAI=false; ActualIDE=false");return 0;
        }
        catch(Exception error){Console.Error.WriteLine("FAIL "+error);return 1;}
    }
}
