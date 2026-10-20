using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : AttachmentWireChecks
/// desc         : 실제 첨부 UI·대기 캡처·네 Codex/Claude 실행 경로에서 네 파일의 정확한 본문을 대조한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK069 #13 실제 계정·OS입력·Window.Show 없음.
/// </summary>
/// ---------------------------------------------------------------------
internal static class AttachmentWireChecks
{
    internal static readonly List<string> Checks = new List<string>(), Errors = new List<string>();
    internal static readonly List<object> Observations = new List<object>();
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string root, fixture;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 일반 한글 파일과 합계400KB 및 JSON escape 경계, 일괄 실패를 격리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 각 실패를 원문 보존하고 다른 경로 검사는 계속한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static void Run(string directory)
    {
        root = directory; fixture = Environment.GetEnvironmentVariable("TASK069_ATTACHMENTS_FIXTURE");
        foreach (string provider in new[] { "Codex", "Claude" }) foreach (bool full in new[] { true, false }) foreach (string size in new[] { "normal", "cap", "escaped-cap" })
        {
            string name = provider + (full ? "-full-" : "-approval-") + size;
            try { Wire(name, provider, full, size); } catch (Exception error) { Errors.Add(name + ": " + error); }
        }
        try { Bounds(); } catch (Exception error) { Errors.Add("bounds: " + error); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Wire
    /// desc         : 실제 첨부 후 원본 파일·현재 목록을 바꿔도 캡처된 네 본문이 wire에 보존되는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 원문 byte hash·문자수·UTF8합계를 CLI가 실제 받은 JSON에서 비교한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Wire(string name, string provider, bool full, string size)
    {
        string path = Path.Combine(root,name); Directory.CreateDirectory(path); string wire = Path.Combine(path,"wire"); Directory.CreateDirectory(wire);
        Environment.SetEnvironmentVariable("TASK069_ATTACHMENTS_WIRE",wire);
        var documents = new Documents(Path.Combine(path,"project")); var control = Create(path,documents,provider);
        try
        {
            Set(control,"agentFullAccess",full); ((TextBox)control.FindName("ExecutablePath")).Text = fixture;
            string[] names = { "SaveZeroTaxDetailedStatement.xaml.txt", "SaveZeroTaxDetailedStatement.xaml.cs.txt", "SaveZeroTaxDetailedStatement.xaml.Behavior.cd.txt", "SaveZeroTaxDetailedStatement.xaml.Data.cd.txt" };
            string[] contents = new string[4], files = new string[4];
            for (int i=0;i<4;i++)
            {
                string head = "BEGIN_ATTACHMENT_"+i+"\r\n한글 고유 SENTINEL_"+i+"_"+name+"\n";
                contents[i] = size=="normal" ? head+"<원문> 인용\"문자\\ 및 줄바꿈\r\nEND_ATTACHMENT_"+i : head+new string(size=="escaped-cap"?'<':'x',102400-Encoding.UTF8.GetByteCount(head));
                files[i] = Path.Combine(path,names[i]); File.WriteAllText(files[i],contents[i],new UTF8Encoding(false));
            }
            Await((Task)Invoke(control,"AttachFilesFromUiAsync",(object)files)); var chat = Active(control);
            Check(name+": actual UI reads all four names and exact text bodies", chat.Attachments.Count==4 && chat.Attachments.Select(a=>a.Name).SequenceEqual(names) && chat.Attachments.Select(a=>a.Text).SequenceEqual(contents));
            ((TextBox)control.FindName("Instruction")).Text="ATTACHMENT_WIRE_"+name;
            object captured = Invoke(control,"CaptureCurrentRequest",true); var context = (ChatRequestContext)Invoke(control,"BuildQueuedExecutionContext",captured);
            Check(name+": submitted chat lists every attachment and chips remain intentionally available", names.All(n=>chat.Messages.Last().Text.Contains(n)) && chat.Attachments.Count==4);
            string bubble = chat.Messages.Last().Text;
            Check(name+": submitted attachment receipt includes per-file decoded sizes and retention explanation", bubble.Contains("첨부 본문 포함 요청") && bubble.Contains("UTF-8") && bubble.Contains("다음 요청에도 유지") && contents.All(c=>!bubble.Contains(c)));
            Check(name+": queue snapshot includes all four exact bodies", context.Attachments.Length==4 && context.Attachments.Select(a=>a.Text).SequenceEqual(contents));
            foreach(string file in files)File.WriteAllText(file,"MUTATED_AFTER_CAPTURE"); chat.Attachments.Clear();
            Check(name+": later file and UI attachment changes do not replace queued data", context.Attachments.Length==4 && context.Attachments.Select(a=>a.Text).SequenceEqual(contents));
            if(full)
            {
                var result=provider=="Codex"?Await(new CodexRuntimeRunner{Timeout=TimeSpan.FromSeconds(15)}.RunAgentAsync(fixture,documents.Project,"ATTACHMENT_WIRE_"+name,context,null,null,CancellationToken.None)):
                    Await(new ClaudeRuntimeRunner{Timeout=TimeSpan.FromSeconds(15)}.RunAgentAsync(fixture,documents.Project,"ATTACHMENT_WIRE_"+name,context,null,null,CancellationToken.None));
                Check(name+": actual full protocol completes",result.Text=="isolated attachments completed");
            }
            else
            {
                var result=Await(new RuntimeEditProvider().GenerateWithContextAsync(provider,fixture,documents.Document.Capture(),"ATTACHMENT_WIRE_"+name,context,CancellationToken.None));
                Check(name+": actual approval protocol completes without applying changes",!result.HasChanges && result.Summary=="isolated attachment fixture");
            }
            var rows=Directory.GetFiles(wire,"*.wire.jsonl").SelectMany(File.ReadAllLines).Select(line=>Json.Deserialize<Dictionary<string,object>>(line)).ToArray();
            string raw;
            if(!full)raw=Convert.ToString(rows.Single(r=>Convert.ToString(r["Kind"])=="stdin")["Text"]);
            else if(provider=="Codex")
            {
                var turn=rows.Where(r=>Convert.ToString(r["Kind"])=="wire").Select(r=>(Dictionary<string,object>)r["Message"]).Single(m=>m.ContainsKey("method")&&Convert.ToString(m["method"])=="turn/start");
                var parameters=(Dictionary<string,object>)turn["params"]; raw=Convert.ToString(((Dictionary<string,object>)((IList)parameters["input"])[0])["text"]);
            }
            else
            {
                var user=rows.Where(r=>Convert.ToString(r["Kind"])=="wire").Select(r=>(Dictionary<string,object>)r["Message"]).Single(m=>m.ContainsKey("type")&&Convert.ToString(m["type"])=="user");
                var message=(Dictionary<string,object>)user["message"]; raw=Convert.ToString(((Dictionary<string,object>)((IList)message["content"])[0])["text"]);
            }
            var payload=Json.Deserialize<Dictionary<string,object>>(raw.Substring(raw.IndexOf('\n')+1));
            var actual=((IList)payload["attachments"]).Cast<Dictionary<string,object>>().ToArray();
            Check(name+": native wire receives four exact filenames",actual.Length==4 && actual.Select(a=>Convert.ToString(a["name"])).SequenceEqual(names));
            Check(name+": native wire receives exact four decoded bodies including all sentinels",actual.Select(a=>Convert.ToString(a["text"])).SequenceEqual(contents));
            Check(name+": aggregate content byte and character counts are preserved",actual.Sum(a=>Encoding.UTF8.GetByteCount(Convert.ToString(a["text"])))==contents.Sum(Encoding.UTF8.GetByteCount) && actual.Sum(a=>Convert.ToString(a["text"]).Length)==contents.Sum(c=>c.Length));
            Observations.Add(new{Scenario=name,Count=actual.Length,Utf8Bytes=contents.Sum(Encoding.UTF8.GetByteCount),Characters=contents.Sum(c=>c.Length),PayloadCharacters=raw.Length,Names=names,Hashes=contents.Select(Hash).ToArray(),ChipRetentionBeforeClear=4,SourceUnchanged=documents.Document.Capture().Content=="alpha"});
        }
        finally{control.Dispose();Pump();}
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Bounds
    /// desc         : 5개·초과 파일·잘못된 인코딩·바이너리의 실패가 부분 첨부나 조용한 누락을 만들지 않는지 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 사용자 입력을 보존하고 100KB 실제 경계를 확인한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Bounds()
    {
        string path=Path.Combine(root,"bounds");Directory.CreateDirectory(path);var control=Create(path,new Documents(Path.Combine(path,"project")),"Claude");
        try
        {
            var input=(TextBox)control.FindName("Instruction");input.Text="PRESERVED_INPUT";
            string[] files=Enumerable.Range(0,5).Select(i=>Path.Combine(path,"bound"+i+".txt")).ToArray();foreach(string file in files)File.WriteAllText(file,"valid");
            Await((Task)Invoke(control,"AttachFilesFromUiAsync",(object)files));
            Check("bounds: fifth file rejection attaches none and preserves input",Active(control).Attachments.Count==0&&input.Text=="PRESERVED_INPUT");
            File.WriteAllText(files[3],new string('x',102401),new UTF8Encoding(false));
            Await((Task)Invoke(control,"AttachFilesFromUiAsync",(object)files.Take(4).ToArray()));
            Check("bounds: over-100KB fourth file fails atomically without first-three partial attachment",Active(control).Attachments.Count==0&&((TextBlock)control.FindName("StatusLabel")).Text.Contains("첨부 실패"));
            File.WriteAllBytes(files[3],new byte[]{0xc3,0x28});Await((Task)Invoke(control,"AttachFilesFromUiAsync",(object)files.Take(4).ToArray()));
            Check("bounds: invalid UTF8 rejects whole batch with visible error",Active(control).Attachments.Count==0&&((TextBlock)control.FindName("StatusLabel")).Text.Contains("첨부 실패"));
            File.WriteAllBytes(files[3],new byte[]{0x61,0,0x62});Await((Task)Invoke(control,"AttachFilesFromUiAsync",(object)files.Take(4).ToArray()));
            Check("bounds: binary NUL rejects whole batch with visible error",Active(control).Attachments.Count==0&&((TextBlock)control.FindName("StatusLabel")).Text.Contains("첨부 실패"));
            File.WriteAllText(files[3],new string('x',102400),new UTF8Encoding(false));Await((Task)Invoke(control,"AttachFilesFromUiAsync",(object)new[]{files[3]}));
            Check("bounds: exact 100KB text is accepted without truncation",Active(control).Attachments.Count==1&&Active(control).Attachments[0].Text.Length==102400);
            Check("bounds: all attachment failures preserve original prompt",input.Text=="PRESERVED_INPUT");
            var attachment=Active(control).Attachments[0];
            Check("display: TXT badge and complete filename-size tooltip are present",Convert.ToString(attachment.GetType().GetProperty("IconText").GetValue(attachment,null))=="TXT"&&Convert.ToString(attachment.GetType().GetProperty("DisplayInfo").GetValue(attachment,null)).Contains(attachment.Name)&&Convert.ToString(attachment.GetType().GetProperty("DisplayInfo").GetValue(attachment,null)).Contains("UTF-8"));
            var list=(ListBox)control.FindName("AttachmentList");var tile=(FrameworkElement)list.ItemTemplate.LoadContent();tile.DataContext=attachment;
            tile.Measure(new Size(138,500));tile.Arrange(new Rect(0,0,138,tile.DesiredSize.Height));tile.UpdateLayout();Pump();
            var visual=Descendants(tile).ToArray();
            Check("display: actual XAML template binds TXT and full accessible filename",visual.OfType<TextBlock>().Any(t=>t.Text=="TXT")&&System.Windows.Automation.AutomationProperties.GetName(tile)==attachment.Name&&System.Windows.Automation.AutomationProperties.GetHelpText(tile).Contains("UTF-8"));
            Check("display: narrow attachment panel keeps wrapping layout",list.ItemsPanel.LoadContent() is WrapPanel&&tile.ActualWidth<=138);
            var remove=visual.OfType<Button>().Single(b=>Convert.ToString(b.Content)=="×");
            Check("display: delete button retains exact attachment Tag",Object.ReferenceEquals(remove.Tag,attachment));
            remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("display: actual XAML delete handler removes chip without deleting original file",Active(control).Attachments.Count==0&&File.Exists(files[3]));
            Await((Task)Invoke(control,"AttachFilesFromUiAsync",(object)files.Take(4).ToArray()));input.Text=new string('P',16000);
            object captured=Invoke(control,"CaptureCurrentRequest",true);var context=(ChatRequestContext)Invoke(control,"BuildQueuedExecutionContext",captured);var message=Active(control).Messages.Last();
            Check("display: full 16000-char prompt plus four metadata rows captures without draft loss or body duplication",context.Attachments.Length==4&&message.Text.StartsWith(new string('P',16000),StringComparison.Ordinal)&&message.Text.Length<2000000&&!message.Text.Contains(new string('x',64))&&input.Text.Length==0);
        }
        finally{control.Dispose();Pump();}
    }
    private static AssistantControl Create(string path,Documents documents,string provider)
    {
        Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH",Path.Combine(path,"settings.json"));Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH",Path.Combine(path,"workspace"));
        new AssistantSettingsStore().Save(new AssistantSettings{HideWelcome=true,ShowHistory=false,EnableCompletionNotifications=false});var c=new AssistantControl();Set(c,"scmLookupStarted",true);c.Initialize(documents,new RuntimeEditProvider());
        var tabs=(TabControl)c.FindName("ProviderChoice");tabs.SelectedItem=tabs.Items.Cast<TabItem>().Single(t=>Convert.ToString(t.Tag)==provider);return c;
    }
    private static ChatSession Active(AssistantControl control){return(ChatSession)control.GetType().GetProperty("ActiveChat",Private).GetValue(control,null);}
    private static void Check(string name,bool value){if(value)Checks.Add(name);else Errors.Add(name);}
    private static object Invoke(object target,string name,params object[] args){return target.GetType().GetMethod(name,Private).Invoke(target,args);}
    private static void Set(object target,string name,object value){target.GetType().GetField(name,Private).SetValue(target,value);}
    private static void Await(Task task){Until(()=>task.IsCompleted);task.GetAwaiter().GetResult();}
    private static T Await<T>(Task<T> task){Await((Task)task);return task.GetAwaiter().GetResult();}
    private static string Hash(string value){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject item){yield return item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(item);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(item,i)))yield return child;}
    private static void Until(Func<bool> condition){var watch=Stopwatch.StartNew();while(!condition()){if(watch.ElapsedMilliseconds>25000)throw new TimeoutException("Attachment fixture condition timed out.");Pump();}}
    private static void Pump(){var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(15)};timer.Tick+=(s,a)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    private sealed class Documents:IActiveDocumentService,ISourceNavigationService,IFullAccessDocumentService
    {
        internal readonly string Project;internal readonly Editor Document;
        internal Documents(string root){Project=root;Directory.CreateDirectory(root);Document=new Editor(Path.Combine(root,"source.cs"));}
        public IEditorDocument GetActiveDocument(){return Document;}
        public DocumentSnapshot CaptureFullAccessContext(){return Document.Capture();}
        public string GetProjectDirectory(){return Project;}
        public void NavigateTo(string path,int line){throw new InvalidOperationException("No UI navigation.");}
    }
    private sealed class Editor:IEditorDocument
    {
        private readonly string path;internal Editor(string file){path=file;File.WriteAllText(path,"alpha");}
        public DocumentSnapshot Capture(){return new DocumentSnapshot(path,File.ReadAllText(path),"fixture-disk", "private attachment project",false,false);}
        public void Replace(DocumentSnapshot expected,string text){throw new InvalidOperationException("Do not apply fixture proposals.");}
        public void Save(DocumentSnapshot expected){throw new InvalidOperationException("Do not save fixture proposals.");}
    }
}
