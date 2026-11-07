using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using CommentCodePreview.Poc;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;

// Only test helpers are compiled here; core/provider implementations come from the exact product DLL.
internal static class ProductProviderContracts
{
    private sealed class Row { public string Name; public bool Passed; public string ErrorType; }
    private static readonly List<Row> rows = new List<Row>();
    private static readonly Type adapterType = typeof(PreviewSession).Assembly.GetType("VS2017.AIAssistant.Providers.CommentCodeProviderAdapter", true);
    private sealed class Fake : IAiContextEditProvider
    {
        internal string Provider, Executable, Model, Instruction;
        internal DocumentSnapshot Source;
        internal int Calls, Cursor;
        internal bool Fail, Foreign, Outside;
        public Task<EditProposal> GenerateAsync(string p,string e,DocumentSnapshot s,string i,CancellationToken t) { throw new Exception("Unexpected fallback API"); }
        public Task<EditProposal> GenerateWithContextAsync(string p,string e,DocumentSnapshot s,string i,ChatRequestContext c,CancellationToken t)
        {
            Calls++; Provider=p; Executable=e; Model=c.Model; Instruction=i; Source=s;
            if(Fail) throw new InvalidOperationException("synthetic unsupported/account response");
            var origin=Foreign?new DocumentSnapshot(Path.Combine(Path.GetTempPath(),"Other.cs"),s.Content,"","",true,false):s;
            string proposed=Outside?"X"+s.Content:s.Content.Insert(Cursor,"int generated = 1;");
            return Task.FromResult(EditProposal.Parse(origin,new JavaScriptSerializer().Serialize(new{summary="synthetic",edits=new[]{new{old_text=s.Content,new_text=proposed}}})));
        }
    }
    private static void Check(string name,Func<bool> assertion)
    {
        try { rows.Add(new Row{Name=name,Passed=assertion()}); }
        catch(Exception e) { rows.Add(new Row{Name=name,Passed=false,ErrorType=e.GetType().FullName}); }
    }
    private static bool Reject(Action action,Type exception)
    {try{action();return false;}catch(Exception e){return exception.IsInstanceOfType(e);}}
    private static ICommentCodeProvider Adapter(Fake fake,string brand)
    {
        return (ICommentCodeProvider)Activator.CreateInstance(adapterType,BindingFlags.Instance|BindingFlags.NonPublic,null,
            new object[]{fake,brand,"explicit-unexecuted.exe","C:/synthetic/Unsaved.cs","explicit-model"},null);
    }
    private static CommentCodeRequest Request(EditorSnapshot snapshot)
    {return (CommentCodeRequest)Activator.CreateInstance(typeof(CommentCodeRequest),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{1L,"// 명시 주석",snapshot},null);}
    private static EditorSnapshot Snapshot(string text,int cursor,int start,int length,bool readOnly=false)
    {return new EditorSnapshot("opaque-document",7,3,text,cursor,start,length,readOnly,false,false,false);}
    private static string Generate(ICommentCodeProvider adapter,EditorSnapshot snapshot,CancellationToken token)
    {return adapter.GenerateAsync(Request(snapshot),token).GetAwaiter().GetResult();}
    private static bool Pair(List<string> values,string key,string value)
    {for(int i=0;i<values.Count-1;i++)if(values[i]==key&&values[i+1]==value)return true;return false;}
    private static int Main(string[] args)
    {
        Check("provider/core loaded from exact supplied assembly",()=>String.Equals(Path.GetFullPath(typeof(PreviewSession).Assembly.Location),Path.GetFullPath(args[1]),StringComparison.OrdinalIgnoreCase));
        foreach(string brand in new[]{"Codex","Claude"})
        {
            string text="// 한글\r\nclass C { /*HERE*/ }\r\n";int cursor=text.IndexOf("/*HERE*/",StringComparison.Ordinal);
            var source=Snapshot(text,cursor,cursor,0);var fake=new Fake{Cursor=cursor};var adapter=Adapter(fake,brand);
            Check(brand+" exact cursor insertion from actual adapter",()=>Generate(adapter,source,CancellationToken.None)=="int generated = 1;");
            Check(brand+" unsaved original and basename preserved",()=>fake.Source.Content==text&&fake.Source.HasUnsavedChanges&&Path.GetFileName(fake.Source.FilePath)=="Unsaved.cs");
            Check(brand+" exact provider/model and executable no fallback",()=>fake.Calls==1&&fake.Provider==brand&&fake.Model=="explicit-model"&&fake.Executable=="explicit-unexecuted.exe");
            fake.Foreign=true;Check(brand+" different file response rejected",()=>Reject(()=>Generate(adapter,source,CancellationToken.None),typeof(InvalidOperationException)));fake.Foreign=false;
            fake.Outside=true;Check(brand+" outside cursor change rejected",()=>Reject(()=>Generate(adapter,source,CancellationToken.None),typeof(InvalidOperationException)));fake.Outside=false;
            int calls=fake.Calls;
            Check(brand+" readonly rejected before provider",()=>Reject(()=>Generate(adapter,Snapshot(text,cursor,cursor,0,true),CancellationToken.None),typeof(InvalidOperationException))&&fake.Calls==calls);
            using(var cancel=new CancellationTokenSource()){cancel.Cancel();Check(brand+" precancelled actual adapter does not invoke provider",()=>Reject(()=>Generate(adapter,source,cancel.Token),typeof(OperationCanceledException))&&fake.Calls==calls);}
            fake.Fail=true;Check(brand+" unsupported/account response remains failure",()=>Reject(()=>Generate(adapter,source,CancellationToken.None),typeof(InvalidOperationException)));fake.Fail=false;
            Check(brand+" failure has one request and no fallback",()=>fake.Calls==calls+1&&fake.Provider==brand&&fake.Model=="explicit-model");
            string selected="// 계획 \uD83D\uDE00\r\n", prefix="// 한글 \uD83D\uDE80\r\n";
            string body=prefix+selected+"0123456789abcdefgh";int start=prefix.Length,end=start+selected.Length;
            fake=new Fake{Cursor=end};adapter=Adapter(fake,brand);
            var forward=Snapshot(body,end,start,selected.Length);
            Check(brand+" forward selection preserves comment and inserts at cursor",()=>Generate(adapter,forward,CancellationToken.None)=="int generated = 1;");
            Check(brand+" forward selection exact reference context",()=>fake.Source!=null&&fake.Source.Selection!=null&&fake.Source.Selection.StartOffset==start&&fake.Source.Selection.Text==selected);
            fake=new Fake{Cursor=start};adapter=Adapter(fake,brand);
            Check(brand+" reverse selection exact context and active cursor insertion",()=>Generate(adapter,Snapshot(body,start,start,selected.Length),CancellationToken.None)=="int generated = 1;"&&fake.Source.Selection.StartOffset==start&&fake.Source.Selection.Text==selected);
            string eof=prefix+selected;fake=new Fake{Cursor=eof.Length};adapter=Adapter(fake,brand);
            Check(brand+" selection ending at EOF can request without out-of-range",()=>Generate(adapter,Snapshot(eof,eof.Length,start,selected.Length),CancellationToken.None)=="int generated = 1;"&&fake.Source.Selection.Text==selected);
        }
        Type cli=typeof(CliEditProvider);var restrict=cli.GetMethod("RestrictCommentCodeArguments",BindingFlags.Static|BindingFlags.NonPublic);
        var codex=new List<string>{"exec","--sandbox","read-only","--ignore-user-config"};restrict.Invoke(null,new object[]{"Codex",codex});
        Check("actual DLL Codex readonly retained and shell/unified disabled",()=>Pair(codex,"--sandbox","read-only")&&Pair(codex,"--disable","shell_tool")&&Pair(codex,"--disable","unified_exec"));
        Check("actual DLL Codex hook/plugin/app restriction",()=>Pair(codex,"--disable","hooks")&&Pair(codex,"--disable","plugins")&&Pair(codex,"--disable","apps"));
        var claude=new List<string>{"--settings","{\"fastMode\":true}","--effort","high"};restrict.Invoke(null,new object[]{"Claude",claude});
        var settings=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(claude[claude.IndexOf("--settings")+1]);
        Check("actual DLL Claude settings preserve fast/effort and suppress hooks",()=>claude.Count(x=>x=="--settings")==1&&Equals(settings["fastMode"],true)&&Equals(settings["disableAllHooks"],true)&&Pair(claude,"--effort","high"));
        Check("actual DLL Claude explicit skills restriction",()=>claude.Contains("--disable-slash-commands"));
        var flag=cli.GetField("commentCodePreviewOnly",BindingFlags.Instance|BindingFlags.NonPublic);
        Check("existing product constructors keep preview opt-in false",()=>Equals(flag.GetValue(new CliEditProvider()),false)&&Equals(flag.GetValue(new CliEditProvider(TimeSpan.FromSeconds(1))),false));
        bool sourceOnly=args.Length>2&&args[2]=="SourceOnly";
        var result=new{Passed=rows.Count(x=>x.Passed),Failed=rows.Count(x=>!x.Passed),Checks=rows,ProductAssembly=typeof(PreviewSession).Assembly.Location,CoreSourceCompiled=sourceOnly,ProviderSourceCompiled=sourceOnly,ActualProductDll=!sourceOnly,FakeProviderOnly=true,ActualIde=false,ActualAI=false,NativeCLI=false,WindowShown=false,ShippingApproved=false};
        File.WriteAllText(args[0],new JavaScriptSerializer().Serialize(result),new System.Text.UTF8Encoding(false));
        Console.WriteLine("Passed="+result.Passed+" Failed="+result.Failed);return result.Failed==0?0:1;
    }
}
