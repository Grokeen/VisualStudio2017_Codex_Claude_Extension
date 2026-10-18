using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VS2017.AIAssistant.Views;
namespace Task024Checks
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownControlsChecks
    /// desc         : 실제 공통 Markdown 객체의 보기·편집·파일·매체 경계를 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
    ///                2026-10-19 CodexOrig : R3-03F4 Markdown 표의 한도 아래·동일·초과 및 코드·escape 회귀 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class MarkdownControlsChecks
    {
        private static readonly List<object> checks=new List<object>();
        private static int passed,failed;
        private static string work;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : 한 검사의 성공·실패를 독립적으로 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Check(string name,Action action){try{action();passed++;checks.Add(new{name,passed=true});}catch(Exception error){failed++;checks.Add(new{name,passed=false,error=error.GetType().Name+": "+error.Message});}}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Assert
        /// desc         : 의도한 의미가 맞지 않으면 검사 실패로 처리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Assert(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Plain
        /// desc         : 문서 전체의 실제 native 표시 텍스트를 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Plain(MarkdownPreview preview){return new TextRange(preview.TextView.Document.ContentStart,preview.TextView.Document.ContentEnd).Text;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Nodes
        /// desc         : 블록·리스트·표·인라인을 실제 WPF 트리에서 열거한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<TextElement> Nodes(TextElement value){yield return value;var section=value as Section;if(section!=null){foreach(Block b in section.Blocks)foreach(var n in Nodes(b))yield return n;}var p=value as Paragraph;if(p!=null){foreach(Inline i in p.Inlines)foreach(var n in Nodes(i))yield return n;}var span=value as Span;if(span!=null){foreach(Inline i in span.Inlines)foreach(var n in Nodes(i))yield return n;}var list=value as System.Windows.Documents.List;if(list!=null){foreach(ListItem item in list.ListItems)foreach(var n in Nodes(item))yield return n;}var li=value as ListItem;if(li!=null){foreach(Block b in li.Blocks)foreach(var n in Nodes(b))yield return n;}var table=value as Table;if(table!=null){foreach(TableRowGroup g in table.RowGroups)foreach(var n in Nodes(g))yield return n;}var group=value as TableRowGroup;if(group!=null){foreach(TableRow row in group.Rows)foreach(var n in Nodes(row))yield return n;}var rowValue=value as TableRow;if(rowValue!=null){foreach(TableCell cell in rowValue.Cells)foreach(var n in Nodes(cell))yield return n;}var cellValue=value as TableCell;if(cellValue!=null){foreach(Block b in cellValue.Blocks)foreach(var n in Nodes(b))yield return n;}}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Nodes
        /// desc         : FlowDocument의 블록에서 실제 텍스트 노드 열거를 시작한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 문서 타입과 TextElement 타입을 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<TextElement> Nodes(FlowDocument document){foreach(Block block in document.Blocks)foreach(var node in Nodes(block))yield return node;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Links
        /// desc         : 직접 실행 URI가 없는 실제 Hyperlink를 가져온다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Hyperlink[] Links(MarkdownPreview p){return Nodes(p.TextView.Document).OfType<Hyperlink>().ToArray();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Click
        /// desc         : 창·포인터 없이 실제 Hyperlink 클릭 이벤트만 발생시킨다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Click(Hyperlink link){link.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Image
        /// desc         : 격리한 폴더를 명시한 실제 이미지 컨트롤을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static MarkdownAnimatedImage Image(string name){return new MarkdownAnimatedImage{AssetBaseDirectory=work,SourcePath=name};}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Reject
        /// desc         : 예외가 발생하고 원문이 보존됐는지 검사하는 보조 API다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Reject(Action action){bool rejected=false;try{action();}catch(Exception error)when(error is ArgumentException||error is InvalidOperationException||error is InvalidDataException||error is IOException||error is DecoderFallbackException){rejected=true;}Assert(rejected,"Invalid input was admitted");}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Main
        /// desc         : 격리된 실제 WPF 컨트롤 검사를 실행하고 결과 JSON을 남긴다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        [STAThread] public static int Main(string[] args)
        {
            work=Path.Combine(args[0],"fixtures");Directory.CreateDirectory(work);
            try{RenderChecks();MediaChecks();EditorChecks();DocumentChecks();}
            catch(Exception error){failed++;checks.Add(new{name="suite setup",passed=false,error=error.ToString()});}
            var assembly=typeof(MarkdownPreview).Assembly;string hash;using(var sha=SHA256.Create())using(var input=File.OpenRead(assembly.Location))hash=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");
            var result=new{Passed=passed,Failed=failed,Succeeded=failed==0,SourceOnly=args[1]=="true",AssemblySha256=hash,ProductAssemblySha256=args[1]=="true"?null:hash,WindowShown=false,NativePointerUsed=false,NetworkUsed=false,RealAccountUsed=false,Checks=checks};
            File.WriteAllText(Path.Combine(args[0],"result.json"),new JavaScriptSerializer{MaxJsonLength=2*1024*1024}.Serialize(result),new UTF8Encoding(false));
            Console.WriteLine(passed+" passed, "+failed+" failed");return failed==0?0:1;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DocumentChecks
        /// desc         : 실제 격리 파일로 목록·현재 저장·이름 변경·삭제의 데이터 보존과 경계를 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : R3-03 작성 기능 누락 회귀 및 확인 거부·충돌·폴더 이탈 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void DocumentChecks()
        {
            string folder=Path.Combine(work,"documents");Directory.CreateDirectory(folder);
            string first=Path.Combine(folder,"first.md"),other=Path.Combine(folder,"other.markdown");
            File.WriteAllText(first,"first",new UTF8Encoding(false));File.WriteAllText(other,"other",new UTF8Encoding(false));File.WriteAllText(Path.Combine(folder,"ignored.txt"),"ignored");Directory.CreateDirectory(Path.Combine(folder,"nested"));File.WriteAllText(Path.Combine(folder,"nested/hidden.md"),"nested");
            using(var editor=new MarkdownEditor())
            {
                Check("new draft save requires explicit destination",()=>{editor.Text="draft";Assert(!editor.SaveCurrentFile()&&editor.CurrentPath==null,"new draft invented a save path");});
                Check("document folder lists only top-level Markdown",()=>{editor.SetDocumentDirectory(folder);Assert(editor.ListedDocuments.Length==2&&editor.DocumentDirectory==folder&&!editor.ListedDocuments.Any(x=>x.EndsWith("hidden.md")),"folder enumeration scope");});
                Check("current file save preserves its path",()=>{editor.TryLoadFile(first,()=>true);editor.Text="saved 한글";Assert(editor.SaveCurrentFile()&&File.ReadAllText(first)==editor.Text&&editor.CurrentPath==first&&!editor.IsDirty,"current save or dirty state");});
                Check("rename retains unsaved draft and disk bytes",()=>{editor.Text="unsaved draft";Assert(editor.RenameCurrentDocument("renamed.md")&&!File.Exists(first)&&editor.IsDirty&&editor.Text=="unsaved draft"&&File.ReadAllText(editor.CurrentPath)=="saved 한글","rename lost draft or saved bytes");});
                Check("rename refuses collision and path escape",()=>{string before=editor.CurrentPath;Reject(()=>editor.RenameCurrentDocument("other.markdown"));Reject(()=>editor.RenameCurrentDocument("../escape.md"));Reject(()=>editor.RenameCurrentDocument("unsafe.exe"));Assert(editor.CurrentPath==before&&File.ReadAllText(other)=="other","invalid rename changed files");});
                Check("declined delete keeps current file and draft",()=>{string before=editor.CurrentPath;Assert(!editor.DeleteCurrentDocument(()=>false)&&File.Exists(before)&&editor.Text=="unsaved draft"&&editor.IsDirty,"declined delete changed document");});
                Check("confirmed delete affects only current file",()=>{string before=editor.CurrentPath;Assert(editor.DeleteCurrentDocument(()=>true)&&!File.Exists(before)&&File.Exists(other)&&editor.CurrentPath==null&&editor.Text==""&&!editor.IsDirty&&editor.AssetBaseDirectory==null,"delete scope or reset");});
                Check("document folder rejects network and missing paths",()=>{string before=editor.DocumentDirectory;Reject(()=>editor.SetDocumentDirectory(@"\\server\share"));Reject(()=>editor.SetDocumentDirectory(Path.Combine(folder,"missing")));Assert(editor.DocumentDirectory==before,"failed folder changed state");});
                Check("document transitions clear old deletion confirmation",()=>{editor.TryLoadFile(other,()=>true);var row=(FrameworkElement)typeof(MarkdownEditor).GetField("deleteConfirmation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);row.Visibility=Visibility.Visible;editor.TryNewDocument(()=>true);Assert(row.Visibility==Visibility.Collapsed,"new document kept delete approval");editor.TryLoadFile(other,()=>true);row.Visibility=Visibility.Visible;editor.ExportUtf8(Path.Combine(folder,"saved-as.md"));Assert(row.Visibility==Visibility.Collapsed,"SaveAs kept delete approval");row.Visibility=Visibility.Visible;editor.TryLoadFile(other,()=>true);Assert(row.Visibility==Visibility.Collapsed&&File.Exists(other),"load kept delete approval");});
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RenderChecks
        /// desc         : 원문 보존·강조·표·목록·안전 링크·파서 한도를 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        ///                2026-10-19 CodexOrig : SQL·소스의 실제 미리보기 토큰 색과 전체 계산·노드 상한 회귀.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RenderChecks()
        {
            using(var p=new MarkdownPreview())
            {
                Check("readonly selectable enabled document",()=>Assert(p.TextView.IsReadOnly&&p.TextView.IsDocumentEnabled,"readonly/link selection contract"));
                Check("identifier underscores remain literal",()=>{p.Markdown="VIEWPORT_NORMAL_MESSAGE and file_name_test.cs";Assert(Plain(p).Contains("VIEWPORT_NORMAL_MESSAGE")&&Plain(p).Contains("file_name_test.cs")&&!Nodes(p.TextView.Document).OfType<Italic>().Any(),"intraword underscore changed identifier");});
                Check("underscore emphasis keeps internal identifiers",()=>{p.Markdown="_part_one_";Assert(Nodes(p.TextView.Document).OfType<Italic>().Count()==1&&Plain(p).Trim()=="part_one","underscore close delimiter");});
                Check("heading bold inline code",()=>{p.Markdown="# 제목\n\n**굵게** 및 `x < y`";Assert(Nodes(p.TextView.Document).OfType<Bold>().Any()&&Plain(p).Contains("x < y")&&p.TextView.Document.Blocks.FirstBlock.FontSize>p.FontSize,"format missing");});
                Check("fenced source remains literal",()=>{p.Markdown="```csharp\n**literal** <script>\n```";Assert(Plain(p).Contains("**literal** <script>")&&!Nodes(p.TextView.Document).OfType<Bold>().Any(),"fence interpreted");});
                Check("reference link forms and hidden definitions",()=>{p.Markdown="[Documentation][ref] [ref][] [ref]\n\n[ref]: https://example.test/docs \"title\"";Assert(Links(p).Length==3&&Links(p).All(x=>Equals(x.Tag,"https://example.test/docs"))&&!Plain(p).Contains("[ref]:"),"reference targets or definition display");});
                Check("reference definitions inside code remain literal",()=>{p.Markdown="```text\n[ref]: https://example.test/hidden\n```\n\n[ref]";Assert(Links(p).Length==0&&Plain(p).Contains("[ref]:"),"code changed reference state");});
                Check("unsafe reference targets never become hyperlinks",()=>{p.Markdown="[bad][ref]\n\n[ref]: javascript:alert(1)";Assert(Links(p).Length==0&&Plain(p).Contains("bad"),"unsafe reference link");});
                foreach(bool light in new[]{false,true})
                {
                    bool currentLight=light;
                    Check("SQL preview token colors "+(light?"light":"dark"),()=>{p.Foreground=currentLight?Brushes.Black:Brushes.Gainsboro;p.Background=currentLight?Brushes.White:Brushes.Black;p.Markdown="```sql\nSELECT name FROM Users WHERE Id=1;\n```";var runs=Nodes(p.TextView.Document).OfType<Run>().ToArray();var keyword=runs.Single(r=>r.Text=="SELECT");var number=runs.Single(r=>r.Text=="1");Assert(!Equals(keyword.Foreground,number.Foreground)&&((SolidColorBrush)keyword.Foreground).Color!=((SolidColorBrush)number.Foreground).Color&&Plain(p).Contains("SELECT name FROM Users WHERE Id=1;"),"SQL token colors or source lost");});
                }
                Check("source preview token colors",()=>{p.Markdown="```csharp\nreturn \"hello\"; // comment\n```";var runs=Nodes(p.TextView.Document).OfType<Run>().ToArray();Assert(runs.Any(r=>r.Text=="return")&&runs.Any(r=>r.Text=="\"hello\"")&&runs.Select(r=>((SolidColorBrush)r.Foreground).Color).Distinct().Count()>=3,"source highlighting missing");});
                Check("highlighted fence work and nodes bounded",()=>{string source="```sql\n"+String.Join("\n",Enumerable.Repeat("SELECT name FROM Users WHERE Id=1;",1200))+"\n```";var watch=Stopwatch.StartNew();p.Markdown=source;Assert(watch.ElapsedMilliseconds<3000&&Nodes(p.TextView.Document).Count()<6100&&p.Markdown==source,"highlight budget or source preservation");});
                Check("unordered nested and ordered lists",()=>{p.Markdown="- first\n  - child\n- second\n\n3. third";var lists=Nodes(p.TextView.Document).OfType<System.Windows.Documents.List>().ToArray();Assert(lists.Length==3&&lists.Any(x=>x.StartIndex==3),"native lists missing");});
                Check("checkbox display",()=>{p.Markdown="- [ ] todo\n- [x] done";Assert(Plain(p).Contains("☐ todo")&&Plain(p).Contains("☑ done"),"checkbox text");});
                Check("table escaped pipe and code",()=>{p.Markdown="| A | B |\n|---|:---:|\n| a\\|b | `x|y` |";var table=Nodes(p.TextView.Document).OfType<Table>().Single();Assert(table.Columns.Count==2&&Plain(p).Contains("a|b")&&Plain(p).Contains("x|y"),"pipe corruption");});
                for(int count=15;count<=17;count++)
                {
                    int width=count;
                    Check("Markdown table column boundary "+width,()=>{string source="|"+String.Join("|",Enumerable.Range(1,width).Select(x=>"H"+x))+"|\n|"+String.Join("|",Enumerable.Repeat("---",width))+"|\n|"+String.Join("|",Enumerable.Range(1,width).Select(x=>"V"+x))+"|";p.Markdown=source;var table=Nodes(p.TextView.Document).OfType<Table>().Single();Assert(table.Columns.Count==Math.Min(16,width)&&Plain(p).Contains("생략")== (width>16)&&p.Markdown==source,"column cap notice or source changed");});
                }
                Check("Markdown table body-only overflow announces omission",()=>{string row="|"+String.Join("|",Enumerable.Range(1,16).Select(x=>"H"+x))+"|";p.Markdown=row+"\n|"+String.Join("|",Enumerable.Repeat("---",16))+"|\n"+row+"MD_TAIL_SENTINEL|";Assert(Nodes(p.TextView.Document).OfType<Table>().Single().Columns.Count==16&&Plain(p).Contains("생략")&&!Plain(p).Contains("MD_TAIL_SENTINEL"),"body-only column overflow silently omitted");});
                Check("Markdown exact column cap preserves escaped and code pipes",()=>{string prefix="|"+String.Join("|",Enumerable.Repeat("cell",14))+"|";string source=prefix+"a\\|b|`x|y`|\n|"+String.Join("|",Enumerable.Repeat("---",16))+"|\n"+prefix+"a\\|b|`x|y`|";p.Markdown=source;Assert(Nodes(p.TextView.Document).OfType<Table>().Single().Columns.Count==16&&Plain(p).Contains("a|b")&&Plain(p).Contains("x|y")&&!Plain(p).Contains("생략")&&p.Markdown==source,"literal pipes treated as extra columns");});
                Check("quote rendered native",()=>{p.Markdown="> 첫째\n> 둘째";Assert(p.TextView.Document.Blocks.OfType<Paragraph>().Any(x=>x.BorderThickness.Left==3)&&Plain(p).Contains("둘째"),"quote missing");});
                Check("web click delegated only",()=>{p.Markdown="[docs](https://example.test/doc#part)";MarkdownLinkEventArgs received=null;p.LinkRequested+=(s,e)=>received=e;var link=Links(p).Single();Assert(link.NavigateUri==null,"browser URI assigned");Click(link);Assert(received!=null&&!received.IsLocalFile&&received.Target=="https://example.test/doc#part","event target");});
                Check("absolute source and line",()=>{p.Markdown="[code](C:/src/한글.cs#L37)";MarkdownLinkEventArgs received=null;EventHandler<MarkdownLinkEventArgs> handler=(s,e)=>received=e;p.LinkRequested+=handler;Click(Links(p).Single());p.LinkRequested-=handler;Assert(received.IsLocalFile&&received.FilePath=="C:/src/한글.cs"&&received.Line==37&&received.Target.EndsWith("#L37"),"path/line contract");});
                Check("relative source colon line",()=>{p.Markdown="[code](relative.cs:37)";MarkdownLinkEventArgs received=null;EventHandler<MarkdownLinkEventArgs> handler=(s,e)=>received=e;p.LinkRequested+=handler;Assert(Links(p).Length==1,"relative source rejected");Click(Links(p)[0]);p.LinkRequested-=handler;Assert(received.FilePath=="relative.cs"&&received.Line==37,"relative line");});
                Check("file URI decoded source",()=>{p.Markdown="[code](file:///C:/src/%ED%95%9C%EA%B8%80.cs#L5)";MarkdownLinkEventArgs received=null;EventHandler<MarkdownLinkEventArgs> handler=(s,e)=>received=e;p.LinkRequested+=handler;Click(Links(p).Single());p.LinkRequested-=handler;Assert(received.FilePath=="C:\\src\\한글.cs"&&received.Line==5,"file URI path");});
                Check("unsafe URI schemes blocked",()=>{p.Markdown="[a](javascript:alert(1)) [b](data:text/html,abc) [c](powershell:run) [d](https://user:pass@example.test/) [e](//remote/a.cs)";Assert(Links(p).Length==0,"unsafe links");});
                Check("control characters and ADS blocked",()=>{p.Markdown="[a](https://example.test/%0Asecret) [b](C:/src/a.cs:secret)";Assert(Links(p).Length==0,"control/ADS");});
                Check("nested hyperlink label does not throw",()=>{p.Markdown="[[inner](https://inner.test)](https://outer.test)";Assert(Links(p).Length==1&&Plain(p).Contains("inner"),"nested links");});
                Check("HTML remains inert text",()=>{p.Markdown="<script>alert(1)</script>\n<iframe src='https://bad.test'/>";Assert(Plain(p).Contains("<script>")&&Links(p).Length==0,"HTML execution route");});
                Check("static HTML table renders native cells",()=>{p.Markdown="<table><tr><th>Title</th><td><strong>Cell</strong> <a href='https://example.test/docs'>Docs</a></td></tr></table>";Assert(Nodes(p.TextView.Document).OfType<TableCell>().Count()==2&&Nodes(p.TextView.Document).OfType<Bold>().Any()&&Links(p).Length==1&&Plain(p).Contains("Cell")&&!Plain(p).Contains("<table>"),"HTML table or inline formatting");});
                Check("nested HTML blocks keep matching outer close",()=>{p.Markdown="<div>\n<div><p>Nested content</p></div>\n</div>";Assert(Plain(p).Contains("Nested content")&&!Plain(p).Contains("<div>"),"nested HTML prematurely ended");});
                Check("HTML sibling roots render as separate paragraphs",()=>{p.Markdown="<p>One</p><p>Two</p>";Assert(Plain(p).Contains("One")&&Plain(p).Contains("Two")&&!Plain(p).Contains("<p>"),"multiple HTML roots");});
                Check("HTML block starts after ordinary paragraph",()=>{p.Markdown="Intro\n<table><tr><td>Cell</td></tr></table>";Assert(Nodes(p.TextView.Document).OfType<Table>().Count()==1&&Plain(p).Contains("Intro")&&!Plain(p).Contains("<table>"),"HTML block absorbed into paragraph");});
                Check("HTML named entities become literal text",()=>{p.Markdown="<p>Copyright &copy; 2026 &amp; text &lt;script&gt;</p>";Assert(Plain(p).Contains("© 2026 & text <script>")&&!Plain(p).Contains("<p>"),"HTML entity text");});
                Check("static full HTML document renders body only",()=>{p.Markdown="<!doctype html>\n<html><head><title>Hidden title</title></head><body><p>Body</p></body></html>";Assert(Plain(p).Contains("Body")&&!Plain(p).Contains("<html>")&&!Plain(p).Contains("<!doctype")&&!Plain(p).Contains("Hidden title"),"static full document");});
                Check("HTML column cap announces omitted content",()=>{p.Markdown="<table><tr>"+String.Concat(Enumerable.Range(0,17).Select(x=>"<td>Cell"+x+"</td>"))+"</tr></table>";Assert(Nodes(p.TextView.Document).OfType<TableCell>().Count()==16&&Plain(p).Contains("생략"),"silent HTML column omission");});
                Check("HTML row cap announces omitted content",()=>{p.Markdown="<table>"+String.Concat(Enumerable.Range(0,129).Select(x=>"<tr><td>Row"+x+"</td></tr>"))+"</table>";Assert(Nodes(p.TextView.Document).OfType<TableRow>().Count()==128&&Plain(p).Contains("생략"),"silent HTML row omission");});
                Check("static HTML blocks never activate scripts or remote images",()=>{p.AssetBaseDirectory=null;p.Markdown="<div><h2>Heading</h2><p><b>Bold</b> <em>Italic</em><img src='https://example.test/a.gif' alt='remote'/></p><script>forbidden-script</script><iframe src='https://example.test'/></div>";Assert(Nodes(p.TextView.Document).OfType<Bold>().Any()&&Nodes(p.TextView.Document).OfType<Italic>().Any()&&!Nodes(p.TextView.Document).OfType<InlineUIContainer>().Any()&&!Plain(p).Contains("forbidden-script")&&Plain(p).Contains("remote"),"HTML execution/media boundary");});
                Check("unclosed HTML scan remains bounded",()=>{string source=String.Join("\n",Enumerable.Repeat("<p>",10000));var watch=Stopwatch.StartNew();p.Markdown=source;Assert(watch.ElapsedMilliseconds<3000&&ScanWork(p)<=128L*1024*64+1024&&p.Markdown==source,"HTML scan budget");});
                Check("normal chat never opens images",()=>{p.AssetBaseDirectory=null;p.Markdown="![remote](https://example.test/a.gif) ![local](C:/does-not-exist.gif)";Assert(!Nodes(p.TextView.Document).OfType<InlineUIContainer>().Any()&&Plain(p).Contains("[이미지:"),"image attempted");});
                Check("input limit preserves source",()=>{string text=new string('한',140000);p.Markdown=text;Assert(p.Markdown==text&&Plain(p).Contains("생략"),"source truncated");});
                Check("syntax bomb bounded",()=>{var timer=Stopwatch.StartNew();p.Markdown=new string('[',120000);Assert(timer.ElapsedMilliseconds<3000&&p.Markdown.Length==120000,"parse unbounded");});
                Check("unclosed backtick run consumes bounded scan work",()=>{string source="a "+new string('`',4096)+"\n\n**kept**";p.Markdown=source;Assert(p.Markdown==source&&Plain(p).Contains(new string('`',4096))&&Nodes(p.TextView.Document).OfType<Bold>().Any()&&!Plain(p).Contains("생략"),"failed run changed visible text");Assert(ScanWork(p)<=5L*source.Length,"failed run repeatedly rescanned its suffix");});
                Check("nested brackets preserve source with bounded fallback",()=>{string brackets=new string('[',2048)+new string(']',2048);string source="a "+brackets+"\n\n[kept](https://example.test)";p.Markdown=source;Assert(p.Markdown==source&&Plain(p).Contains(brackets)&&Links(p).Length==1&&!Plain(p).Contains("생략"),"nested bracket fallback lost content");Assert(ScanWork(p)<=5L*source.Length,"nested fallback repeated unbounded scanning");});
                Check("unclosed angles exhaust document shared scan budget",()=>{string source=String.Join("\n\n",Enumerable.Repeat("a "+new string('<',512),80));p.Markdown=source;Assert(p.Markdown==source&&source.Length<128*1024&&Plain(p).Contains("미리보기 한도로")&&p.TextView.Document.Blocks.Count>1&&p.TextView.Document.Blocks.Count<81,"shared work guard did not explain bounded preview");Assert(ScanWork(p)<=128L*1024*64&&Nodes(p.TextView.Document).Count()<6000,"work guard used the node limit instead");});
                Check("node budget bounded",()=>{p.Markdown=String.Join("\n\n",Enumerable.Repeat("**a** **b**",5000));Assert(Nodes(p.TextView.Document).Count()<6100&&Plain(p).Contains("생략"),"node limit");});
                Check("light palette link and code contrast",()=>{p.Foreground=Brushes.Black;p.Background=Brushes.White;p.Markdown="[link](https://example.test)\n\n```\nx\n```";var link=(SolidColorBrush)Links(p).Single().Foreground;Assert(link.Color.B<210&&link.Color.R<60,"light link contrast");var code=p.TextView.Document.Blocks.OfType<Paragraph>().Last();Assert(((SolidColorBrush)code.Background).Color.R>200&&((SolidColorBrush)code.Foreground).Color.R<70,"light code contrast");});
                Check("dark palette readability",()=>{p.Foreground=Brushes.Gainsboro;p.Background=Brushes.Black;p.Markdown="```\nx\n```";var code=p.TextView.Document.Blocks.OfType<Paragraph>().Single();Assert(((SolidColorBrush)code.Foreground).Color.R>180&&((SolidColorBrush)code.Background).Color.R<70,"dark code contrast");});
                Check("outer scrolling contract",()=>{p.UseInternalScrolling=false;Assert(p.TextView.VerticalScrollBarVisibility==ScrollBarVisibility.Disabled,"internal scrolling");});
            }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MediaChecks
        /// desc         : 로컬 이미지 경로·byte/pixel/frame 제한과 GIF 부분 프레임 합성을 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void MediaChecks()
        {
            byte[] png;using(var output=new MemoryStream()){var pixels=new byte[]{0,0,255,255,0,255,0,255};var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,1,96,96,PixelFormats.Bgra32,null,pixels,8)));encoder.Save(output);png=output.ToArray();}File.WriteAllBytes(Path.Combine(work,"한글.png"),png);
            Check("trusted PNG actual decode",()=>{using(var image=Image("한글.png"))Assert(image.FrameCount==1&&image.Source!=null&&image.DecodedPixelCount==2,"PNG decode");});
            Check("absolute path inside root",()=>{using(var image=Image(Path.Combine(work,"한글.png")))Assert(image.FrameCount==1,"absolute trusted path");});
            Check("unloaded media timer stays stopped",()=>{File.WriteAllBytes(Path.Combine(work,"sample.gif"),Gif(1,3));using(var image=Image("sample.gif"))Assert(image.FrameCount==3&&!image.IsAnimationRunning,"unloaded animation");});
            Check("GIF restore background disposal",()=>{File.WriteAllBytes(Path.Combine(work,"bg.gif"),Gif(2,3));using(var image=Image("bg.gif")){Assert(image.FrameCount==3,"GIF decode");var frames=Frames(image);Assert(Pixel(frames[1],0)==0xff00ff00&&Pixel(frames[1],1)==0xffff0000,"partial frame combine");Assert(Pixel(frames[2],0)==0xff000000&&Pixel(frames[2],1)==0xff0000ff,"disposal2");}});
            Check("GIF restore previous disposal",()=>{File.WriteAllBytes(Path.Combine(work,"previous.gif"),Gif(3,3));using(var image=Image("previous.gif")){Assert(image.FrameCount==3,"GIF decode");Assert(Pixel(Frames(image)[2],0)==0xffff0000&&Pixel(Frames(image)[2],1)==0xff0000ff,"disposal3");}});
            Check("GIF frame count cap before decode",()=>{File.WriteAllBytes(Path.Combine(work,"many.gif"),Gif(1,61));using(var image=Image("many.gif"))Assert(image.FrameCount==0&&image.Source==null,"frames cap");});
            Check("GIF canvas pixel bomb rejected",()=>{byte[] bomb=Gif(1,3);bomb[6]=255;bomb[7]=127;bomb[8]=255;bomb[9]=127;File.WriteAllBytes(Path.Combine(work,"bomb.gif"),bomb);using(var image=Image("bomb.gif"))Assert(image.FrameCount==0,"pixel bomb");});
            Check("oversized image byte cap",()=>{File.WriteAllBytes(Path.Combine(work,"large.png"),new byte[2*1024*1024+1]);using(var image=Image("large.png"))Assert(image.FrameCount==0,"byte cap");});
            Check("unknown image encoding rejected",()=>{File.WriteAllText(Path.Combine(work,"unknown.svg"),"<svg/>");using(var image=Image("unknown.svg"))Assert(image.FrameCount==0,"SVG accepted");});
            Check("path escape encoded and ordinary rejected",()=>{using(var a=Image("../escape.png"))using(var b=Image("%2e%2e/escape.png"))using(var d=Image("https://example.test/a.png"))Assert(a.FrameCount==0&&b.FrameCount==0&&d.FrameCount==0,"unsafe path");});
            Check("device UNC and ADS rejected",()=>{using(var a=Image("\\\\remote\\a.png"))using(var b=Image(Path.Combine(work,"한글.png")+":data"))Assert(a.FrameCount==0&&b.FrameCount==0,"device/ADS");});
            Check("reparse directory refused before image read",()=>{string outside=Path.Combine(Path.GetDirectoryName(work),"outside");Directory.CreateDirectory(outside);File.WriteAllBytes(Path.Combine(outside,"outside.png"),png);File.WriteAllText(Path.Combine(outside,"outside.md"),"outside");string junction=Path.Combine(work,"junction");var start=new ProcessStartInfo("cmd.exe","/d /c mklink /J \""+junction+"\" \""+outside+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};using(var process=Process.Start(start)){Assert(process.WaitForExit(5000)&&process.ExitCode==0,"fixture junction creation failed");}using(var image=Image("junction/outside.png"))Assert(image.FrameCount==0,"reparse image read");});
            Check("media absent root performs no load",()=>{using(var image=new MarkdownAnimatedImage{SourcePath=Path.Combine(work,"한글.png")})Assert(image.FrameCount==0,"root missing");});
            Check("preview media actual native image",()=>{using(var p=new MarkdownPreview{AssetBaseDirectory=work,Markdown="![sample](한글.png)"})Assert(Nodes(p.TextView.Document).OfType<InlineUIContainer>().Single().Child is MarkdownAnimatedImage,"native image not rendered");});
            Check("preview replacement disposes media",()=>{using(var p=new MarkdownPreview{AssetBaseDirectory=work,Markdown="![sample](sample.gif)"}){var image=(MarkdownAnimatedImage)Nodes(p.TextView.Document).OfType<InlineUIContainer>().Single().Child;p.Markdown="replaced";Assert(image.Source==null&&!image.IsAnimationRunning,"old frames retained");}});
            Check("document image count cap",()=>{using(var p=new MarkdownPreview{AssetBaseDirectory=work,Markdown=String.Join(" ",Enumerable.Repeat("![sample](한글.png)",30))})Assert(Nodes(p.TextView.Document).OfType<InlineUIContainer>().Count()==24&&Plain(p).Contains("[이미지:"),"image count unbounded");});
            Check("dispose is idempotent",()=>{var image=Image("sample.gif");image.Dispose();image.Dispose();Assert(image.Source==null&&!image.IsAnimationRunning,"dispose");});
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Frames
        /// desc         : 검증 목적의 읽기 전용 reflection으로 실제 디코딩 결과를 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static List<BitmapSource> Frames(MarkdownAnimatedImage image){object frames=typeof(MarkdownAnimatedImage).GetField("frames",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(image);return (List<BitmapSource>)frames.GetType().GetField("Images",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(frames);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ScanWork
        /// desc         : 시간 임계값 대신 실제 제품 계산 예산의 사용량을 읽기 전용으로 관찰한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : SC024-01 회귀는 원문 보존·실패 run 선형 비용·문서 공유 한도를 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static long ScanWork(MarkdownPreview preview){object rendering=typeof(MarkdownPreview).GetField("rendering",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(preview);return (long)rendering.GetType().GetField("scanWork",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(rendering);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Pixel
        /// desc         : native 합성 결과의 한 픽셀을 비교한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static uint Pixel(BitmapSource frame,int x){var bytes=new uint[frame.PixelWidth*frame.PixelHeight];frame.CopyPixels(bytes,frame.PixelWidth*4,0);return bytes[x];}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Gif
        /// desc         : 외부 설치·이미지 도구 없이 작은 부분 프레임 GIF fixture를 생성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static byte[] Gif(int disposal,int count){using(var output=new MemoryStream())using(var w=new BinaryWriter(output)){w.Write(Encoding.ASCII.GetBytes("GIF89a"));w.Write((ushort)2);w.Write((ushort)1);w.Write((byte)0x81);w.Write((byte)0);w.Write((byte)0);w.Write(new byte[]{0,0,0,255,0,0,0,255,0,0,0,255});for(int i=0;i<count;i++){w.Write(new byte[]{0x21,0xf9,4,(byte)((i==1?disposal:1)<<2),5,0,0,0,0x2c});w.Write((ushort)(i==2?1:0));w.Write((ushort)0);w.Write((ushort)(i==0?2:1));w.Write((ushort)1);w.Write((byte)0);w.Write((byte)2);var codes=i==0?new[]{4,1,4,1,5}:new[]{4,i==1?2:3,5};int bits=0,used=0;var packed=new List<byte>();foreach(int code in codes){bits|=code<<used;used+=3;while(used>=8){packed.Add((byte)bits);bits>>=8;used-=8;}}if(used>0)packed.Add((byte)bits);w.Write((byte)packed.Count);w.Write(packed.ToArray());w.Write((byte)0);}w.Write((byte)0x3b);return output.ToArray();}}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EditorChecks
        /// desc         : 실제 AvalonEdit 작성·Undo·목록·바인딩·문서 전환·UTF-8 파일 경계를 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 독립 WPF 회귀; 창 표시·네트워크·실계정 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void EditorChecks()
        {
            using(var e=new MarkdownEditor{MaxLength=16000})
            {
                Check("real AvalonEdit and line numbers",()=>Assert(e.Editor.GetType().FullName=="ICSharpCode.AvalonEdit.TextEditor"&&e.Editor.ShowLineNumbers&&e.Editor.Options.HighlightCurrentLine,"AvalonEdit missing"));
                Check("dynamic native editor palette light and dark",()=>{e.Resources["AssistantForegroundBrush"]=Brushes.Black;e.Resources["AssistantBackgroundBrush"]=Brushes.White;e.Resources["AssistantMutedForegroundBrush"]=Brushes.DimGray;e.Resources["AssistantSurfaceBrush"]=Brushes.WhiteSmoke;e.Resources["AssistantBorderBrush"]=Brushes.Gray;e.Resources["AssistantSelectionBrush"]=Brushes.SteelBlue;Assert(e.Editor.Foreground==Brushes.Black&&e.Editor.Background==Brushes.White&&e.Editor.LineNumbersForeground==Brushes.DimGray,"light editor resource");e.Resources["AssistantForegroundBrush"]=Brushes.White;e.Resources["AssistantBackgroundBrush"]=Brushes.Black;Assert(e.Editor.Foreground==Brushes.White&&e.Editor.Background==Brushes.Black,"dark editor resource");});
                Check("source roundtrip preserves CRLF tabs Unicode",()=>{string source="# 한글\r\n\t- [ ] task\r\n";e.Text=source;e.RefreshPreview();Assert(e.Text==source&&e.Editor.Text==source&&e.Preview.Markdown==source,"source modified");});
                Check("external source keeps caret",()=>{e.Text="abcdef";e.Editor.Select(2,2);e.Text="abcdefghi";Assert(e.Editor.SelectionStart==2&&e.Editor.SelectionLength==2,"caret lost");});
                Check("native edit updates DP",()=>{e.Editor.Document.Insert(0,"x");Assert(e.Text==e.Editor.Text&&e.Text.StartsWith("x"),"DP stale");});
                Check("find text case and whole words",()=>{e.Text="Cat cats cat";e.SearchPattern="cat";e.SearchWholeWords=true;e.SearchMatchCase=true;e.Editor.Select(0,0);Assert(e.FindNext(false)&&e.Editor.SelectionStart==9,"case whole word");e.SearchMatchCase=false;Assert(e.FindNext(false)&&e.Editor.SelectionStart==0,"search wrap");});
                Check("regex find safe options",()=>{e.Text="a12 b345";e.SearchWholeWords=false;e.SearchUseRegex=true;e.SearchPattern=@"\d+";e.Editor.Select(0,0);Assert(e.FindNext(false)&&e.Editor.SelectedText=="12","regex find");e.SearchPattern="[";Assert(!e.FindNext(false),"invalid regex propagated");e.SearchUseRegex=false;});
                Check("regex timeout leaves source untouched",()=>{e.Text=new string('a',12000)+"!";e.SearchPattern="^(a+)+$";e.SearchUseRegex=true;e.Editor.Select(0,0);var timer=Stopwatch.StartNew();Assert(!e.FindNext(false)&&timer.ElapsedMilliseconds<1500&&e.Text.EndsWith("!"),"search timeout");e.SearchUseRegex=false;});
                Check("bold and undo redo",()=>{e.Text="hello";e.Editor.Select(0,5);e.WrapSelection("**","**","");Assert(e.Text=="**hello**","bold edit");e.Editor.Undo();Assert(e.Text=="hello","undo");e.Editor.Redo();Assert(e.Text=="**hello**","redo");});
                Check("checkbox multi-line boundary",()=>{e.Text="a\r\nb\r\nc";e.Editor.Select(0,6);e.EditTaskLines(false);Assert(e.Text=="- [ ] a\r\n- [ ] b\r\nc","selection line boundary");});
                Check("checkbox completion preserves indentation",()=>{e.Text="  - [ ] item";e.Editor.Select(0,e.Text.Length);e.EditTaskLines(true);Assert(e.Text=="  - [x] item","task toggle");});
                Check("number list continuation",()=>{e.Text="9. text";e.Editor.Select(e.Text.Length,0);Assert(e.ContinueMarkdownLine()&&e.Text=="9. text\n10. ","continuation");});
                Check("empty list exits",()=>{e.Text="- ";e.Editor.Select(2,0);Assert(e.ContinueMarkdownLine()&&e.Text=="\n","empty list");});
                Check("quote CRLF continuation",()=>{e.Text="first\r\n> quote";e.Editor.Select(e.Text.Length,0);Assert(e.ContinueMarkdownLine()&&e.Text.EndsWith("\r\n> "),"quote continuation");});
                Check("indent outdent backspace",()=>{e.Text="- item";e.Editor.Select(0,0);Assert(e.IndentMarkdownListLine(false)&&e.Text=="    - item","indent");e.Editor.Select(2,0);Assert(e.OutdentMarkdownListOnBackspace()&&e.Text=="- item","outdent");});
                Check("three view modes preserve source",()=>{string source=e.Text;e.ViewMode=2;Assert(e.Preview.Markdown==source&&e.Editor.Visibility==Visibility.Collapsed,"preview");e.ViewMode=1;Assert(e.Preview.Visibility==Visibility.Collapsed,"edit");e.ViewMode=0;Assert(e.Editor.Visibility==Visibility.Visible&&e.Preview.Visibility==Visibility.Visible&&e.Text==source,"split");});
                Check("zoom clamp source unchanged",()=>{string source=e.Text;e.ZoomPercent=20;Assert(e.ZoomPercent==60,"minzoom");e.ZoomPercent=500;Assert(e.ZoomPercent==200&&e.Text==source,"maxzoom");e.ZoomPercent=100;});
                Check("format edit over input limit rejected",()=>{e.MaxLength=5;e.Text="abcde";e.Editor.Select(0,5);e.WrapSelection("**","**","");Assert(e.Text=="abcde","partial edit");e.MaxLength=16000;});
                Check("hard DP limit fails preserving source",()=>{e.Text="keep";Reject(()=>e.Text=new string('a',128*1024+1));Assert(e.Text=="keep"&&e.Editor.Text=="keep","source lost");});
                Check("direct document hard limit before mutation",()=>{e.Text="keep";Reject(()=>e.Editor.Document.Insert(0,new string('a',128*1024)));Assert(e.Text=="keep"&&e.Editor.Text=="keep","direct source lost");});
                Check("dirty transition cancel preserves document",()=>{e.Text="unsaved";string path=Path.Combine(work,"next.md");File.WriteAllText(path,"next",new UTF8Encoding(false));Assert(!e.TryLoadFile(path,()=>false)&&e.Text=="unsaved"&&e.IsDirty,"cancel transition");});
                Check("dirty close requires explicit confirmation",()=>{bool closed=false;Assert(!e.ConfirmClose(()=>closed=true)&&!closed&&e.Text=="unsaved","close bypass");});
                Check("UTF8 import accepted and clean",()=>{Assert(e.TryLoadFile(Path.Combine(work,"next.md"),()=>true)&&e.Text=="next"&&!e.IsDirty,"import");});
                Check("selected Markdown sibling PNG and GIF preview",()=>{string path=Path.Combine(work,"media.md");File.WriteAllText(path,"![png](한글.png)\n\n![gif](sample.gif)",new UTF8Encoding(false));Assert(e.TryLoadFile(path,()=>true)&&e.AssetBaseDirectory==work,"loaded asset origin");var images=Nodes(e.Preview.TextView.Document).OfType<InlineUIContainer>().Select(x=>x.Child).OfType<MarkdownAnimatedImage>().ToArray();Assert(images.Length==2&&images[0].FrameCount==1&&images[1].FrameCount==3,"sibling PNG/GIF not rendered");});
                Check("cancelled load keeps trusted image origin",()=>{e.Editor.Document.Insert(0,"changed");string before=e.AssetBaseDirectory;Assert(!e.TryLoadFile(Path.Combine(work,"next.md"),()=>false)&&e.AssetBaseDirectory==before&&e.Preview.AssetBaseDirectory==before,"cancel changed origin");});
                Check("invalid UTF8 in new folder keeps old origin",()=>{string other=Path.Combine(work,"other");Directory.CreateDirectory(other);string path=Path.Combine(other,"bad.md");File.WriteAllBytes(path,new byte[]{255});string before=e.Text,origin=e.AssetBaseDirectory;Reject(()=>e.TryLoadFile(path,()=>true));Assert(e.Text==before&&e.AssetBaseDirectory==origin&&e.Preview.AssetBaseDirectory==origin,"invalid load changed origin");});
                Check("SaveAs success changes origin to actual document folder",()=>{string destination=Path.Combine(work,"saved");Directory.CreateDirectory(destination);string original=e.Text;e.ExportUtf8(Path.Combine(destination,"copy.md"));Assert(e.AssetBaseDirectory==destination&&e.Preview.AssetBaseDirectory==destination&&e.Text==original,"SaveAs origin policy");});
                Check("UTF8 BOM Korean roundtrip",()=>{string path=Path.Combine(work,"한글.md");File.WriteAllText(path,"# 한글\r\n",new UTF8Encoding(true));Assert(e.TryLoadFile(path,()=>true)&&e.Text=="# 한글\r\n","BOM import");e.Editor.Document.Insert(e.Text.Length,"more");e.ExportUtf8(path);Assert(File.ReadAllText(path)==e.Text&&!e.IsDirty&&!Directory.GetFiles(work,".markdown-*.tmp").Any(),"export");});
                Check("literal percent filename import",()=>{string path=Path.Combine(work,"a%20b.md");File.WriteAllText(path,"literal-percent",new UTF8Encoding(false));Assert(e.TryLoadFile(path,()=>true)&&e.Text=="literal-percent","selected filename decoded incorrectly");});
                Check("locked atomic export preserves original and dirty",()=>{string folder=Path.Combine(work,"locked-folder");Directory.CreateDirectory(folder);string path=Path.Combine(folder,"locked.md");File.WriteAllText(path,"original");e.Text="changed";string origin=e.AssetBaseDirectory,current=e.CurrentPath;using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){Reject(()=>e.ExportUtf8(path));}Assert(File.ReadAllText(path)=="original"&&e.Text=="changed"&&e.IsDirty&&e.AssetBaseDirectory==origin&&e.CurrentPath==current&&!Directory.GetFiles(folder,".markdown-*.tmp").Any(),"atomic export lost data or origin");});
                Check("reparse import and export refused",()=>{string before=e.Text;Reject(()=>e.TryLoadFile(Path.Combine(work,"junction/outside.md"),()=>true));Reject(()=>e.ExportUtf8(Path.Combine(work,"junction/new.md")));Assert(e.Text==before&&!File.Exists(Path.Combine(Path.GetDirectoryName(work),"outside/new.md")),"reparse IO bypass");});
                Check("invalid UTF8 leaves document unchanged",()=>{string before=e.Text;File.WriteAllBytes(Path.Combine(work,"bad.md"),new byte[]{0xff,0xff});Reject(()=>e.TryLoadFile(Path.Combine(work,"bad.md"),()=>true));Assert(e.Text==before,"invalid UTF8 mutation");});
                Check("oversized file rejected unchanged",()=>{string before=e.Text;File.WriteAllBytes(Path.Combine(work,"huge.md"),new byte[512*1024+1]);Reject(()=>e.TryLoadFile(Path.Combine(work,"huge.md"),()=>true));Assert(e.Text==before,"huge source mutation");});
                Check("untrusted export extension rejected",()=>{string before=e.Text;Reject(()=>e.ExportUtf8(Path.Combine(work,"bad.exe")));Assert(!File.Exists(Path.Combine(work,"bad.exe"))&&e.Text==before,"export extension");});
                Check("new document clean after approval",()=>{e.Text="changed";Assert(!e.TryNewDocument(null)&&e.Text=="changed","implicit discard");Assert(e.TryNewDocument(()=>true)&&e.Text==""&&!e.IsDirty&&e.CurrentPath==null&&e.AssetBaseDirectory==null&&e.Preview.AssetBaseDirectory==null,"new document");});
                Check("empty UTF8 file supported",()=>{string path=Path.Combine(work,"empty.md");File.WriteAllBytes(path,new byte[0]);Assert(e.TryLoadFile(path,()=>true)&&e.Text=="","empty read");});
            }
        }
    }
}
