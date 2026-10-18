using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : MarkdownnHelperChecks
/// desc         : 실제 이식 source의 순수 편집·토큰·구문 정의·fence cache 경계를 독립 검사한다.
/// author       : CodexCode
/// create date  : 2026-10-19
/// update date  : 2026-10-19 CodexCode : 창 표시·사용자 문서·네트워크·AI·공유 빌드 없음.
/// </summary>
/// ---------------------------------------------------------------------
public static class MarkdownnHelperChecks
{
    public static readonly List<string> Checks=new List<string>(),Errors=new List<string>();
    /// <summary>
    /// name         : Run
    /// desc         : 입력 보존과 시간·크기 제한의 의미 있는 회귀를 각각 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 실계정·실제 파일 편집 없음.
    /// </summary>
    public static void Run()
    {
        Check("Original list recognition retains unordered numbered and indented markers",()=>MarkdownnEditing.IsListLine("  12) 항목") && MarkdownnEditing.IsListLine("\t- 항목") && !MarkdownnEditing.IsListLine("# heading"));
        Check("Empty checklist recognition distinguishes existing content",()=>MarkdownnEditing.IsEmptyListLine("  - [x] ") && !MarkdownnEditing.IsEmptyListLine("- [ ] 본문"));
        Check("Task conversion preserves indentation numbering and Korean body",()=>MarkdownnEditing.TaskLine("  7) 본문",false)=="  7) [ ] 본문");
        Check("Converting an existing checked item preserves its original state and spacing",()=>MarkdownnEditing.TaskLine("\t* [X]  done",false)=="\t* [X]  done");
        Check("Task toggle changes only the actual checkbox character",()=>MarkdownnEditing.TaskLine("- [X] done",true)=="- [ ] done" && MarkdownnEditing.TaskLine("- [ ] done",true)=="- [x] done");
        Check("Toggle leaves ordinary and non-checkbox list lines untouched",()=>MarkdownnEditing.TaskLine("그냥 본문",true)=="그냥 본문" && MarkdownnEditing.TaskLine("- body",true)=="- body");
        Check("Plain-line checkbox creation preserves leading whitespace",()=>MarkdownnEditing.TaskLine(" \t본문😀",false)==" \t- [ ] 본문😀");
        Check("Numbered continuation advances and preserves the original punctuation",()=>Continue("009. done")=="10. " && Continue(" 9) done")==" 10) ");
        Check("Checklist continuation always begins a new unchecked item",()=>Continue("  - [x] done")=="  - [ ] ");
        bool remove;string next=MarkdownnEditing.Continuation("  + [ ] ",out remove);
        Check("Enter on an empty checklist removes only its marker",()=>remove && next=="  ");
        Check("Quote continuation retains depth and leading indentation",()=>Continue(" >> quoted")==" >> ");
        bool quoteRemove;string emptyQuote=MarkdownnEditing.Continuation("\t>> ",out quoteRemove);
        Check("An empty quote ends without deleting leading indentation",()=>quoteRemove && emptyQuote=="\t");
        Check("Maximum or oversized list numbers never overflow or throw",()=>Continue("2147483647. done")=="2147483647. " && Continue(new String('9',200)+") body")==new String('9',200)+") ");
        string enormous="- "+new String('한',1000000);bool hugeRemove;string hugePrefix=MarkdownnEditing.Continuation(enormous,out hugeRemove);
        Check("Oversized editing input is preserved and receives no invented automatic marker",()=>ReferenceEquals(MarkdownnEditing.TaskLine(enormous,false),enormous) && !MarkdownnEditing.IsListLine(enormous) && hugePrefix==null && !hugeRemove);
        Check("All editing regular expressions have an explicit finite timeout",()=>Regexes(typeof(MarkdownnEditing)).All(r=>r.MatchTimeout==TimeSpan.FromMilliseconds(100)));
        Check("Original SQL aliases and source/plain distinctions are retained",()=>new[]{"sql","tsql","postgresql","mssql","query","{.sqlite}"}.All(s=>MarkdownnCodeHighlighting.Language(s)==MarkdownnCodeKind.Sql) && MarkdownnCodeHighlighting.Language("cs")==MarkdownnCodeKind.Source && MarkdownnCodeHighlighting.Language("text")==MarkdownnCodeKind.Plain);
        Check("Language classification examines only a bounded info prefix",()=>MarkdownnCodeHighlighting.Language("sql "+new String('x',1000000))==MarkdownnCodeKind.Sql);
        var sql=MarkdownnCodeHighlighting.Tokens("SELECT 'it''s select', 42 FROM sample -- where 9",MarkdownnCodeKind.Sql).ToArray();
        Check("SQL strings comments numbers and keywords retain distinct original token classes",()=>sql.Any(t=>t.Kind==MarkdownnTokenKind.String) && sql.Any(t=>t.Kind==MarkdownnTokenKind.Comment) && sql.Any(t=>t.Kind==MarkdownnTokenKind.Number) && sql.Count(t=>t.Kind==MarkdownnTokenKind.Keyword)==2);
        string source="한글😀 public string x = \"class\"; // return 123";var tokens=MarkdownnCodeHighlighting.Tokens(source,MarkdownnCodeKind.Source).ToArray();
        Check("Source token offsets remain valid UTF16 positions and never overlap",()=>tokens.Length>0 && tokens.All(t=>t.Start>=0 && t.Length>0 && t.Start+t.Length<=source.Length) && tokens.Zip(tokens.Skip(1),(a,b)=>a.Start+a.Length<=b.Start).All(x=>x));
        Check("A comment suppresses later keyword-looking content on the same line",()=>tokens.Last().Kind==MarkdownnTokenKind.Comment && !tokens.Any(t=>t.Kind==MarkdownnTokenKind.Number));
        Check("Markup type and plain-code behavior preserve source feature coverage",()=>MarkdownnCodeHighlighting.Tokens("<Button x=\"a\">",MarkdownnCodeKind.Source).Any(t=>t.Kind==MarkdownnTokenKind.Type) && !MarkdownnCodeHighlighting.Tokens("public 3",MarkdownnCodeKind.Plain).Any());
        Check("Token count and individual token length are bounded",()=>MarkdownnCodeHighlighting.Tokens(String.Join(" ",Enumerable.Repeat("1",10000)),MarkdownnCodeKind.Source).Count()<=2048 && MarkdownnCodeHighlighting.Tokens("\""+new String('a',5000)+"\"",MarkdownnCodeKind.Source).All(t=>t.Length<=4096));
        Check("An oversized code line receives no partial guessed highlight",()=>!MarkdownnCodeHighlighting.Tokens("public "+new String('x',100000),MarkdownnCodeKind.Source).Any());
        Check("All source regexes and original palette brushes are finite/frozen",()=>Regexes(typeof(MarkdownnCodeHighlighting)).All(r=>r.MatchTimeout==TimeSpan.FromMilliseconds(100)) && Enum.GetValues(typeof(MarkdownnCodeKind)).Cast<MarkdownnCodeKind>().All(k=>MarkdownnCodeHighlighting.Base(k).IsFrozen));
        IHighlightingDefinition definition;
        using(var stream=typeof(MarkdownnHelperChecks).Assembly.GetManifestResourceStream("VS2017.AIAssistant.MarkdownnHighlighting.xshd")) definition=MarkdownnCodeHighlighting.LoadDefinition(stream);
        Check("Original XSHD is embedded with the agreed logical name and full Markdown palette",()=>definition.GetNamedColor("Heading")!=null && definition.GetNamedColor("Strong")!=null && definition.MainRuleSet.Spans.Count==2);
        Check("Actual AvalonEdit rule and span regex instances all receive finite timeout",()=>definition.MainRuleSet.Rules.All(r=>r.Regex.MatchTimeout==TimeSpan.FromMilliseconds(100)) && definition.MainRuleSet.Spans.All(s=>s.StartExpression.MatchTimeout==TimeSpan.FromMilliseconds(100) && s.EndExpression.MatchTimeout==TimeSpan.FromMilliseconds(100)));
        Check("DTD and oversized syntax definitions are rejected before external XML resolution",()=>RejectXml("<!DOCTYPE x [<!ENTITY p SYSTEM 'file:///never-read'>]><x>&p;</x>") && RejectXml("<x>"+new String('a',140000)+"</x>"));
        var document=new TextDocument("```sql\nselect 1\n```\noutside");
        Check("Normal bounded documents are eligible for full original syntax",()=>MarkdownnCodeHighlighting.CanHighlight(document));
        Check("Oversized document line count and line length disable decoration without mutating text",()=>!MarkdownnCodeHighlighting.CanHighlight(new TextDocument(new String('a',32769))) && !MarkdownnCodeHighlighting.CanHighlight(new TextDocument(new String('\n',50001))) && !MarkdownnCodeHighlighting.CanHighlight(new TextDocument(new String('x',2*1024*1024+1))));
        var editor=new TextEditor{Document=document};var fence=new MarkdownnFenceColorizer(editor);var syntax=new MarkdownnSyntaxColorizer(editor,definition);
        Prepare(fence,document);var kinds=(MarkdownnCodeKind[])Field(fence,"kinds");var delimiters=(bool[])Field(fence,"delimiters");
        Check("Fence cache distinguishes SQL body delimiters and outside content",()=>kinds[1]==MarkdownnCodeKind.Sql && kinds[3]==MarkdownnCodeKind.Plain && delimiters[0] && delimiters[2]);
        Prepare(fence,document);
        Check("Repeated highlighting of the same immutable version reuses the state array",()=>ReferenceEquals(kinds,Field(fence,"kinds")));
        document.Replace(3,3,"cs ");Prepare(fence,document);
        Check("A same-length text edit invalidates cached fence language by version",()=>!ReferenceEquals(kinds,Field(fence,"kinds")) && ((MarkdownnCodeKind[])Field(fence,"kinds"))[1]==MarkdownnCodeKind.Source);
        var unmatched=new TextDocument("````sql\nselect 1\n```\nselect 2\n````\nplain");editor.Document=unmatched;Prepare(fence,unmatched);
        Check("Shorter closing fences do not incorrectly end a longer original fence",()=>((MarkdownnCodeKind[])Field(fence,"kinds"))[3]==MarkdownnCodeKind.Sql && ((MarkdownnCodeKind[])Field(fence,"kinds"))[5]==MarkdownnCodeKind.Plain);
        Check("Fence matching itself also has an explicit timeout",()=>Regexes(typeof(MarkdownnFenceColorizer)).All(r=>r.MatchTimeout==TimeSpan.FromMilliseconds(100)));
        fence.Dispose();fence.Dispose();
        Check("Fence disposal clears cached document versions and state arrays idempotently",()=>Field(fence,"cachedDocument")==null && ((MarkdownnCodeKind[])Field(fence,"kinds")).Length==0 && (bool)Field(fence,"disposed"));
        Check("A safe syntax transformer is available without opening any editor window",()=>syntax!=null && !editor.IsLoaded);
        Check("Light code palette is selected from the actual foreground rather than a fixed application mode",()=>MarkdownnCodeHighlighting.IsLightTheme(System.Windows.Media.Brushes.Black) && !MarkdownnCodeHighlighting.IsLightTheme(System.Windows.Media.Brushes.White) && !ReferenceEquals(MarkdownnCodeHighlighting.Token(MarkdownnTokenKind.Keyword,true),MarkdownnCodeHighlighting.Token(MarkdownnTokenKind.Keyword,false)));
        var one=new MarkdownnThemeDefinition(definition);var two=new MarkdownnThemeDefinition(definition);var original=definition.GetNamedColor("Heading").Foreground.GetColor(null);one.SetLight(true);
        Check("Light XSHD colors are editor-local copies and never mutate the original or another editor",()=>one.GetNamedColor("Heading").Foreground.GetColor(null)!=original && two.GetNamedColor("Heading").Foreground.GetColor(null)==original && definition.GetNamedColor("Heading").Foreground.GetColor(null)==original && !ReferenceEquals(one.GetNamedColor("Heading"),definition.GetNamedColor("Heading")));
        one.SetLight(false);
        Check("Switching the runtime clone back to Dark restores the original color and bold emphasis",()=>one.GetNamedColor("Heading").Foreground.GetColor(null)==original && one.GetNamedColor("Heading").FontWeight==definition.GetNamedColor("Heading").FontWeight);
        string unchanged=editor.Text;editor.Foreground=System.Windows.Media.Brushes.Black;syntax.RefreshTheme();var theme=(MarkdownnThemeDefinition)Field(syntax,"theme");var lightHeading=theme.GetNamedColor("Heading").Foreground.GetColor(null);editor.Foreground=System.Windows.Media.Brushes.White;syntax.RefreshTheme();
        Check("Actual syntax transformer refresh changes existing palette without changing editor text",()=>lightHeading!=original && theme.GetNamedColor("Heading").Foreground.GetColor(null)==original && editor.Text==unchanged);
    }
    /// <summary>
    /// name         : Continue
    /// desc         : 목록·인용 helper의 직접 반환값을 검사에 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 원문 편집 없음.
    /// </summary>
    private static string Continue(string text){bool remove;return MarkdownnEditing.Continuation(text,out remove);}
    /// <summary>
    /// name         : Regexes
    /// desc         : 실제 static Regex 인스턴스의 timeout 속성을 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 소스 문자열 검사 대신 실제 객체 확인.
    /// </summary>
    private static IEnumerable<Regex> Regexes(Type type){return type.GetFields(BindingFlags.Static|BindingFlags.NonPublic).Where(f=>f.FieldType==typeof(Regex)).Select(f=>(Regex)f.GetValue(null));}
    /// <summary>
    /// name         : RejectXml
    /// desc         : 유한 XML loader가 외부 접근 없는 부정 fixture를 거부하는지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 네트워크·파일 URI를 열지 않음.
    /// </summary>
    private static bool RejectXml(string text){try{using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(text)))MarkdownnCodeHighlighting.LoadDefinition(stream);return false;}catch(Exception e){return e is System.Xml.XmlException || e is InvalidDataException || e is ICSharpCode.AvalonEdit.Highlighting.HighlightingDefinitionInvalidException;}}
    /// <summary>
    /// name         : Prepare
    /// desc         : 실제 bounded cache builder를 창 없이 호출한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 실제 native 렌더 검증으로 집계하지 않음.
    /// </summary>
    private static void Prepare(MarkdownnFenceColorizer value,TextDocument document){typeof(MarkdownnFenceColorizer).GetMethod("PrepareCache",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,new object[]{document});}
    /// <summary>
    /// name         : Field
    /// desc         : cache와 수명 상태를 읽기만 한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 실제 source 객체 확인.
    /// </summary>
    private static object Field(object value,string name){return value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    /// <summary>
    /// name         : Check
    /// desc         : 각 독립 검사의 성공·실패를 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 실패를 숨기거나 expected pass로 치환하지 않음.
    /// </summary>
    private static void Check(string name,Func<bool> body){try{if(!body())throw new InvalidOperationException(name);Checks.Add(name);}catch(Exception error){Errors.Add(name+": "+error.Message);}}
}
