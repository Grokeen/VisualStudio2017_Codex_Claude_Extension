// 사용자 제공 RemoteControlDemo/Features/Markdown/MarkdownnRenderer.cs의
// 제목·목록·표·FlowDocument 분리 구조를 참고하여 제한·이벤트·로컬 자산 경계를 추가했다.
// 참고 원본은 변경하지 않으며 Markdig/HTML/AvalonEdit 코드를 포함하지 않는다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownLinkEventArgs
    /// desc         : 표시한 안전한 링크의 대상만 호스트에 전달하고 파일이나 앱을 직접 열지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : source 경로·행과 web/anchor를 구분하는 TASK-024 공통 계약.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class MarkdownLinkEventArgs : RoutedEventArgs
    {
        public string Target { get; private set; }
        public bool IsLocalFile { get; private set; }
        public string FilePath { get; private set; }
        public int Line { get; private set; }
        public string DocumentBaseDirectory { get; private set; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkdownLinkEventArgs
        /// desc         : 검증된 표시 대상의 불변 클릭 정보를 보관한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 자동 탐색·프로세스 실행 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal MarkdownLinkEventArgs(string target,bool local,string file,int line){Target=target;IsLocalFile=local;FilePath=file;Line=line;}

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkdownLinkEventArgs
        /// desc         : 명시적으로 연 문서의 기준 폴더를 클릭 시점까지 원래 렌더 문맥으로 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 3차 감사의 동일 이름 소스 오탐색을 막는 문서별 상대 링크 문맥 추가.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal MarkdownLinkEventArgs(string target,bool local,string file,int line,string documentBaseDirectory)
            : this(target,local,file,line) { DocumentBaseDirectory=documentBaseDirectory; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownRendering
    /// desc         : 유한한 Markdown 요소를 HTML 실행 없이 WPF 문서로 변환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 제공된 native 렌더 구조에 깊이·문자·노드·표·이미지 예산을 적용.
    ///                2026-10-19 CodexCode : SC024-01 반복 delimiter 탐색의 전체 계산 예산을 별도 제한.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed partial class MarkdownRendering : IDisposable
    {
        internal const int MaximumCharacters=128*1024,MaximumNodes=6000,MaximumDepth=8,MaximumImages=24;
        internal const int MaximumScanWork=MaximumCharacters*64;
        private static readonly TimeSpan RegexLimit=TimeSpan.FromMilliseconds(80);
        private static readonly Regex Heading=new Regex(@"^\s{0,3}(#{1,6})\s+(.+?)\s*#*\s*$",RegexOptions.CultureInvariant,RegexLimit);
        private static readonly Regex ListMarker=new Regex(@"^([ \t]*)([-+*]|[0-9]{1,9}[.)])\s+(.+)$",RegexOptions.CultureInvariant,RegexLimit);
        private static readonly Regex Setext=new Regex(@"^\s{0,3}(=+|-+)\s*$",RegexOptions.CultureInvariant,RegexLimit);
        private static readonly Regex ReferenceDefinition=new Regex(@"^\s{0,3}\[([^\]\r\n]{1,128})\]:\s*(?:<([^>\r\n]{1,2048})>|(\S{1,2048}))(?:[ \t]+[\""'(].*)?$",RegexOptions.CultureInvariant,RegexLimit);
        private readonly Dictionary<string,string> references=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> referenceLines=new HashSet<int>();
        private readonly string assetRoot;
        private readonly EventHandler<MarkdownLinkEventArgs> linkRequested;
        private readonly Brush foreground,background,accent,muted,codeBrush,codeForeground,inlineCodeForeground,tableBorder;
        private readonly double fontSize;
        private int nodes,images,linkDepth;
        private readonly List<MarkdownAnimatedImage> imageControls=new List<MarkdownAnimatedImage>();
        private long imagePixels,scanWork;
        private long codeHighlightTicks;
        private bool omitted;
        private readonly Dictionary<string,Paragraph> anchors=new Dictionary<string,Paragraph>(StringComparer.OrdinalIgnoreCase);

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkdownRendering
        /// desc         : 문서 한 번의 표시 예산과 호스트 팔레트·링크 경계만 고정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 현재 작업 폴더·전역 파일 저장소를 추정하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal MarkdownRendering(string assetRoot,EventHandler<MarkdownLinkEventArgs> handler,Brush foreground,Brush background,double fontSize,Brush link=null,Brush code=null,Brush subdued=null,Brush border=null)
        {this.assetRoot=assetRoot;linkRequested=handler;this.foreground=foreground??Brushes.Gainsboro;this.background=background??Brushes.Transparent;this.fontSize=Math.Max(6,Math.Min(40,fontSize));var solid=this.foreground as SolidColorBrush;bool light=solid!=null&&(solid.Color.R*299+solid.Color.G*587+solid.Color.B*114)<128000;accent=link??(light?Paint(0,87,153):Paint(142,199,255));muted=subdued??(light?Paint(80,91,105):Paint(175,188,205));codeBrush=code??(light?Paint(240,243,247):Paint(36,40,47));codeForeground=light?Paint(30,40,50):Paint(218,232,246);inlineCodeForeground=light?Paint(28,95,49):Paint(196,234,207);tableBorder=border??(light?Paint(183,193,205):Paint(69,76,88));}

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Render
        /// desc         : 원문을 변경하지 않고 제한된 복사본을 표시하며 생략된 경우 명시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제목·강조·코드·인용·목록·표·링크·안내 이미지 지원.
        ///                2026-10-19 CodexOrig : 제공된 구문 강조기를 코드 미리보기에도 적용하고 기존 표시 예산 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal FlowDocument Render(string markdown)
        {
            var document=new FlowDocument{PagePadding=new Thickness(4),Foreground=foreground,Background=background,FontFamily=new FontFamily("Segoe UI"),FontSize=fontSize,ColumnWidth=Double.PositiveInfinity};
            string source=markdown??"";if(source.Length>MaximumCharacters){int end=MaximumCharacters;if(Char.IsHighSurrogate(source[end-1]))end--;source=source.Substring(0,end);omitted=true;}
            var lines=source.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
            try
            {
                ReadReferences(lines);
                for(int index=0;index<lines.Length;)
                {
                    if(referenceLines.Contains(index)){index++;continue;}
                    string line=lines[index],trim=line.TrimStart();if(String.IsNullOrWhiteSpace(line)){index++;continue;}
                    if(line.Length>8192){document.Blocks.Add(Paragraph(line.Substring(0,8192),0));omitted=true;index++;continue;}
                    if(trim.StartsWith("<!--",StringComparison.Ordinal)){while(index<lines.Length){if(lines[index++].Contains("-->"))break;}continue;}
                    int fence=FenceLength(trim);if(fence>=3){char marker=trim[0];string language=trim.Substring(fence).Trim();var code=new StringBuilder();index++;while(index<lines.Length){string close=lines[index].Trim();if(FenceLength(close)>=fence&&close[0]==marker&&close.All(c=>c==marker)){index++;break;}if(code.Length>0)code.Append('\n');code.Append(lines[index++]);}Consume();var p=new Paragraph{FontFamily=new FontFamily("Consolas"),FontSize=Math.Max(10,fontSize-1),Background=codeBrush,Foreground=codeForeground,Padding=new Thickness(10),Margin=new Thickness(0,5,0,12)};document.Blocks.Add(p);if(language.Length>0){Consume();p.Inlines.Add(new Run(language+"\n"){Foreground=muted,FontSize=Math.Max(10,fontSize-3)});}AddCode(p.Inlines,code.ToString(),language);continue;}
                    var doctype=HtmlDoctype.Match(trim);if(doctype.Success&&doctype.Length==trim.Length){index++;continue;}
                    Block html;int htmlEnd;if(TryHtmlBlock(lines,index,out html,out htmlEnd)){document.Blocks.Add(html);index=htmlEnd;continue;}
                    var heading=Heading.Match(line);if(heading.Success){AddHeading(document,heading.Groups[2].Value,heading.Groups[1].Length);index++;continue;}
                    if(index+1<lines.Length&&Setext.IsMatch(lines[index+1])){AddHeading(document,line.Trim(),lines[index+1].TrimStart()[0]=='='?1:2);index+=2;continue;}
                    if(IsDivider(trim)){Consume();document.Blocks.Add(new Paragraph(new Run("────────────────────────")){Foreground=muted,Margin=new Thickness(0,6,0,10)});index++;continue;}
                    if(index+1<lines.Length&&line.Contains("|")&&IsTableDivider(lines[index+1])){document.Blocks.Add(Table(lines,ref index));continue;}
                    if(ListMarker.IsMatch(line)){document.Blocks.Add(List(lines,ref index,0));continue;}
                    if(trim.StartsWith(">",StringComparison.Ordinal)){var p=new Paragraph{BorderBrush=accent,BorderThickness=new Thickness(3,0,0,0),Padding=new Thickness(10,5,5,5),Margin=new Thickness(0,4,0,10),Background=codeBrush};Consume();while(index<lines.Length&&lines[index].TrimStart().StartsWith(">",StringComparison.Ordinal)){if(p.Inlines.Count>0)p.Inlines.Add(new LineBreak());AddInline(p.Inlines,lines[index++].TrimStart().Substring(1).TrimStart(),0);}document.Blocks.Add(p);continue;}
                    var paragraph=Paragraph(line.TrimStart(),0);index++;
                    while(index<lines.Length&&!referenceLines.Contains(index)&&!String.IsNullOrWhiteSpace(lines[index])&&!StartsBlock(lines,index)){bool hard=line.EndsWith("  ",StringComparison.Ordinal)||line.EndsWith("\\",StringComparison.Ordinal);paragraph.Inlines.Add(hard?(Inline)new LineBreak():new Run(" "));line=lines[index++];AddInline(paragraph.Inlines,line.Trim(),0);}
                    document.Blocks.Add(paragraph);
                }
            }
            catch(MarkdownBudgetException){omitted=true;}
            catch(RegexMatchTimeoutException){omitted=true;}
            if(omitted)document.Blocks.Add(new Paragraph(new Run("미리보기 한도로 일부 내용을 생략했습니다. 편집 탭과 원문은 변경되지 않았습니다.")){Foreground=muted,Margin=new Thickness(0,8,0,4)});
            if(document.Blocks.Count==0)document.Blocks.Add(new Paragraph(new Run("")));
            return document;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddInline
        /// desc         : 전진하는 유한 scanner로 인라인 강조·코드·링크·이미지를 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 실패한 중첩 문법은 평문으로 유지하고 재귀 깊이는 8로 제한.
        ///                2026-10-19 CodexCode : 실패 backtick run 전체 소비·재귀와 모든 검색의 문서 공유 계산 예산.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddInline(InlineCollection target,string text,int depth)
        {
            ChargeScan((long)text.Length+1);
            if(depth>=MaximumDepth){AddRun(target,text);return;}
            var plain=new StringBuilder();
            for(int at=0;at<text.Length;)
            {
                ChargeScan(1);char c=text[at];if(c=='\\'&&at+1<text.Length&&"\\`*_{}[]()#+-.!>~|".IndexOf(text[at+1])>=0){plain.Append(text[at+1]);at+=2;continue;}
                if(c=='`'){int count=0;while(at+count<text.Length&&text[at+count]=='`'){ChargeScan(1);count++;}ChargeScan(count);int end=ScanIndexOf(text,new string('`',count),at+count);if(end>=0){Flush(target,plain);Consume();target.Add(new Run(text.Substring(at+count,end-at-count)){FontFamily=new FontFamily("Consolas"),Background=codeBrush,Foreground=inlineCodeForeground});at=end+count;continue;}plain.Append(text,at,count);at+=count;continue;}
                bool image=c=='!'&&at+1<text.Length&&text[at+1]=='[';
                if(c=='['||image)
                {
                    int labelStart=at+(image?2:1),close=Closing(text,labelStart-1,'[',']');
                    if(close<0){plain.Append(text.Substring(at));break;}
                    if(close+1<text.Length&&text[close+1]=='('){int end=Closing(text,close+1,'(',')');if(end>=0){string label=text.Substring(labelStart,close-labelStart),destination=Destination(text.Substring(close+2,end-close-2));Flush(target,plain);if(image)AddImage(target,label,destination);else AddLink(target,label,destination,depth+1);at=end+1;continue;}}
                    string referenceLabel=text.Substring(labelStart,close-labelStart),referenceId=referenceLabel;int referenceEnd=close;
                    if(close+1<text.Length&&text[close+1]=='['){int end=Closing(text,close+1,'[',']');if(end>=0){referenceId=text.Substring(close+2,end-close-2);if(referenceId.Length==0)referenceId=referenceLabel;referenceEnd=end;}}
                    string referenceTarget;if(referenceId.Length<=128&&references.TryGetValue(ReferenceKey(referenceId),out referenceTarget)){Flush(target,plain);if(image)AddImage(target,referenceLabel,referenceTarget);else AddLink(target,referenceLabel,referenceTarget,depth+1);at=referenceEnd+1;continue;}
                }
                if(c=='<'&&at+1<text.Length){int end=ScanIndexOf(text,'>',at+1);if(end>at&&end-at<=2048){string url=text.Substring(at+1,end-at-1);MarkdownLinkEventArgs info;if(TryLink(url,out info)&&!info.IsLocalFile){Flush(target,plain);AddLink(target,url,url,depth+1);at=end+1;continue;}}}
                string marker=at+1<text.Length&&(text.Substring(at,2)=="**"||text.Substring(at,2)=="__"||text.Substring(at,2)=="~~")?text.Substring(at,2):(c=='*'||c=='_')?c.ToString():null;
                if(c=='_'&&at>0&&Char.IsLetterOrDigit(text[at-1]))marker=null;
                if(marker!=null){int end=ScanIndexOf(text,marker,at+marker.Length);while(marker[0]=='_'&&end>0&&end+marker.Length<text.Length&&Char.IsLetterOrDigit(text[end-1])&&Char.IsLetterOrDigit(text[end+marker.Length]))end=ScanIndexOf(text,marker,end+marker.Length);if(end>at+marker.Length){Flush(target,plain);Consume();Span span=marker=="~~"?new Span{TextDecorations=TextDecorations.Strikethrough}:marker.Length==2?(Span)new Bold():new Italic();AddInline(span.Inlines,text.Substring(at+marker.Length,end-at-marker.Length),depth+1);target.Add(span);at=end+marker.Length;continue;}}
                plain.Append(c);at++;
            }
            Flush(target,plain);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadReferences
        /// desc         : 코드 펜스 밖의 유한한 참조 정의만 먼저 수집하며 입력 원문은 변경하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : R3-03 전체·축약·짧은 참조 링크 및 이미지 정의 지원.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ReadReferences(string[] lines)
        {
            char marker='\0';int length=0;
            for(int index=0;index<lines.Length;index++)
            {
                string line=lines[index],trim=line.TrimStart();ChargeScan((long)line.Length+1);
                int fence=FenceLength(trim);
                if(marker!='\0'){if(fence>=length&&trim[0]==marker&&trim.Trim().All(c=>c==marker))marker='\0';continue;}
                if(fence>=3){marker=trim[0];length=fence;continue;}
                if(line.Length>8192||references.Count>=256)continue;
                var match=ReferenceDefinition.Match(line);if(!match.Success)continue;
                string key=ReferenceKey(match.Groups[1].Value);
                if(!references.ContainsKey(key))references.Add(key,match.Groups[2].Success?match.Groups[2].Value:match.Groups[3].Value);
                referenceLines.Add(index);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReferenceKey
        /// desc         : 대소문자와 연속 공백을 정규화한 짧은 참조 이름을 사용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 이름 길이는 호출자에서 128자로 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ReferenceKey(string value){return Regex.Replace(value.Trim(),@"\s+"," ",RegexOptions.CultureInvariant,RegexLimit);}

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddCode
        /// desc         : 제공 프로젝트의 SQL·소스 강조기를 미리보기에 재사용하고 문서 전체 비용을 제한한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 토큰 시간 80ms 이후는 원문 그대로 표시하고 모든 Run은 노드 예산에 포함.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddCode(InlineCollection target,string source,string language)
        {
            var kind=MarkdownnCodeHighlighting.Language(language);
            bool light=MarkdownnCodeHighlighting.IsLightTheme(foreground);
            Brush plain=MarkdownnCodeHighlighting.Base(kind,light);
            string[] lines=source.Split('\n');
            for(int lineIndex=0;lineIndex<lines.Length;lineIndex++)
            {
                string line=lines[lineIndex];int offset=0;
                if(kind!=MarkdownnCodeKind.Plain && line.Length<=8192 && codeHighlightTicks<Stopwatch.Frequency*80/1000)
                {
                    var timer=Stopwatch.StartNew();
                    var tokens=MarkdownnCodeHighlighting.Tokens(line,kind).ToArray();
                    codeHighlightTicks+=timer.ElapsedTicks;
                    foreach(var token in tokens)
                    {
                        if(token.Start>offset)AddCodeRun(target,line.Substring(offset,token.Start-offset),plain);
                        AddCodeRun(target,line.Substring(token.Start,token.Length),MarkdownnCodeHighlighting.Token(token.Kind,light));
                        offset=token.Start+token.Length;
                    }
                }
                AddCodeRun(target,line.Substring(offset)+(lineIndex+1<lines.Length?"\n":""),plain);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddCodeRun
        /// desc         : 표시하는 구문 조각마다 기존 문서 노드 한도를 적용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 코드 원문은 실행하지 않고 글자와 색상만 전달.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddCodeRun(InlineCollection target,string text,Brush brush)
        { if(text.Length==0)return;Consume();target.Add(new Run(text){Foreground=brush}); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddLink
        /// desc         : NavigateUri 없는 Hyperlink로 안전 대상만 클릭 이벤트에 위임한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 앱·브라우저·source 파일을 renderer에서 자동 실행하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddLink(InlineCollection target,string label,string destination,int depth)
        {
            MarkdownLinkEventArgs info;if(linkDepth>0||!TryLink(destination,out info)){AddInline(target,label,depth);return;}Consume();
            var link=new Hyperlink{Foreground=accent,ToolTip=info.Target,Tag=info.Target};link.Inlines.Add(new Run(info.IsLocalFile?"▣ ":info.Target.StartsWith("#",StringComparison.Ordinal)?"# ":"↗ "));linkDepth++;try{AddInline(link.Inlines,label,depth);}finally{linkDepth--;}
            link.Click+=(sender,args)=>{args.Handled=true;var clicked=new MarkdownLinkEventArgs(info.Target,info.IsLocalFile,info.FilePath,info.Line,assetRoot);linkRequested?.Invoke(sender,clicked);if(!clicked.Handled&&clicked.Target.StartsWith("#",StringComparison.Ordinal)){Paragraph p;if(anchors.TryGetValue(clicked.Target.Substring(1),out p))p.BringIntoView();}};target.Add(link);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryLink
        /// desc         : 허용 URL·anchor·source 참조만 데이터로 분류한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : javascript/data/UNC/장치/자격증명·제어문자 URI 거부.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool TryLink(string target,out MarkdownLinkEventArgs result)
        {
            result=null;if(String.IsNullOrWhiteSpace(target)||target.Length>2048||target.Any(Char.IsControl))return false;target=target.Trim();
            try{if(Uri.UnescapeDataString(target).Any(Char.IsControl))return false;}catch(UriFormatException){return false;}
            if(target.StartsWith("#",StringComparison.Ordinal)){result=new MarkdownLinkEventArgs(target,false,null,1);return true;}
            bool drive=target.Length>=3&&Char.IsLetter(target[0])&&target[1]==':'&&(target[2]=='/'||target[2]=='\\');Uri uri;
            string file=target;
            if(!drive&&Uri.TryCreate(target,UriKind.Absolute,out uri))
            {
                if((uri.Scheme=="http"||uri.Scheme=="https")&&uri.Host.Length>0&&uri.UserInfo.Length==0||uri.Scheme=="mailto"&&uri.UserInfo.Length==0){result=new MarkdownLinkEventArgs(uri.AbsoluteUri,false,null,1);return true;}
                if(uri.IsFile){if(uri.IsUnc)return false;file=uri.LocalPath+uri.Fragment;drive=file.Length>=3&&Char.IsLetter(file[0])&&file[1]==':'&&(file[2]=='/'||file[2]=='\\');}
                else {int suffix=target.LastIndexOf(':');int number;string prefix=suffix>0?target.Substring(0,suffix):"";if(suffix<1||prefix.IndexOf(':')>=0||prefix.IndexOf('.')<0||!Int32.TryParse(target.Substring(suffix+1),NumberStyles.None,CultureInfo.InvariantCulture,out number)||number<1)return false;}
            }
            if(file.StartsWith("\\\\",StringComparison.Ordinal)||file.StartsWith("//",StringComparison.Ordinal))return false;
            int line=1,cut=file.LastIndexOf("#L",StringComparison.OrdinalIgnoreCase);if(cut>=0){string number=file.Substring(cut+2).Split('-')[0];int parsed;if(Int32.TryParse(number,NumberStyles.None,CultureInfo.InvariantCulture,out parsed)&&parsed>0){line=Math.Min(parsed,10000000);file=file.Substring(0,cut);}}
            else {cut=file.LastIndexOf(':');int parsed;if(cut>1&&Int32.TryParse(file.Substring(cut+1),NumberStyles.None,CultureInfo.InvariantCulture,out parsed)&&parsed>0){line=Math.Min(parsed,10000000);file=file.Substring(0,cut);}}
            if(String.IsNullOrWhiteSpace(file)||file.IndexOf(':',drive?2:0)>=0)return false;result=new MarkdownLinkEventArgs(target,true,file,line);return true;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddImage
        /// desc         : 일반 답변은 alt만 표시하고 명시한 안내 폴더에서는 제한된 native 이미지를 추가한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 문서당 24이미지·총16MP와 이미지별 제한을 동시에 적용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddImage(InlineCollection target,string label,string path)
        {
            if(String.IsNullOrWhiteSpace(assetRoot)||images>=MaximumImages||imagePixels>=16*1024*1024){AddRun(target,"[이미지: "+label+"]");return;}
            Consume();images++;var image=new MarkdownAnimatedImage{AssetBaseDirectory=assetRoot,SourcePath=path,ToolTip=label,Margin=new Thickness(2,5,2,5)};
            if(image.FrameCount==0||imagePixels+image.DecodedPixelCount>16*1024*1024){image.Dispose();AddRun(target,"[이미지: "+label+"]");return;}
            imagePixels+=image.DecodedPixelCount;imageControls.Add(image);target.Add(new InlineUIContainer(image));
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : List
        /// desc         : 들여쓴 항목을 부모 안에 배치하되 목록 깊이를 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 renderer의 목록 구조를 깊이 제한과 함께 도입.
        /// </summary>
        /// ---------------------------------------------------------------------
        private List List(string[] lines,ref int index,int depth)
        {
            Consume();var first=ListMarker.Match(lines[index]);int indent=Indent(first.Groups[1].Value);bool ordered=Char.IsDigit(first.Groups[2].Value[0]);var list=new List{MarkerStyle=ordered?TextMarkerStyle.Decimal:TextMarkerStyle.Disc,Margin=new Thickness(16,3,0,10),Padding=new Thickness(12,0,0,0)};
            int start;if(ordered&&Int32.TryParse(first.Groups[2].Value.TrimEnd('.',')'),out start)&&start>0)list.StartIndex=start;
            while(index<lines.Length){var match=ListMarker.Match(lines[index]);if(!match.Success||Indent(match.Groups[1].Value)!=indent||Char.IsDigit(match.Groups[2].Value[0])!=ordered)break;string text=match.Groups[3].Value;if(text.StartsWith("[ ] ",StringComparison.Ordinal))text="☐ "+text.Substring(4);else if(text.StartsWith("[x] ",StringComparison.OrdinalIgnoreCase))text="☑ "+text.Substring(4);var item=new ListItem(Paragraph(text,0));Consume();list.ListItems.Add(item);index++;
                while(index<lines.Length){var next=ListMarker.Match(lines[index]);if(!next.Success||Indent(next.Groups[1].Value)<=indent)break;if(depth>=MaximumDepth){item.Blocks.Add(Paragraph(lines[index++].Trim(),0));omitted=true;}else item.Blocks.Add(List(lines,ref index,depth+1));}}
            return list;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Table
        /// desc         : escaped pipe·inline code를 보존하며 최대16열의 native 표를 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 셀마다 노드 예산을 적용하여 표 폭탄을 제한.
        ///                2026-10-19 CodexOrig : R3-03F4 실제 초과 열은 한도를 유지하며 생략 안내로 알림.
        /// </summary>
        /// ---------------------------------------------------------------------
        private Table Table(string[] lines,ref int index)
        {
            Consume();var headers=Cells(lines[index]);var dividers=Cells(lines[index+1]);int columns=Math.Min(16,Math.Min(headers.Count,dividers.Count));var table=new Table{CellSpacing=0,Margin=new Thickness(0,5,0,12)};for(int i=0;i<columns;i++)table.Columns.Add(new TableColumn());var group=new TableRowGroup();table.RowGroups.Add(group);var all=new List<List<string>>{headers};index+=2;
            while(index<lines.Length&&!String.IsNullOrWhiteSpace(lines[index])&&lines[index].Contains("|")){all.Add(Cells(lines[index++]));if(all.Count>=500){omitted=true;break;}}
            if(headers.Count>columns||dividers.Count>columns||all.Any(row=>row.Count>columns))omitted=true;
            for(int r=0;r<all.Count;r++){Consume();var row=new TableRow();group.Rows.Add(row);for(int c=0;c<columns;c++){var p=Paragraph(c<all[r].Count?all[r][c]:"",0);p.TextAlignment=dividers[c].StartsWith(":",StringComparison.Ordinal)&&dividers[c].EndsWith(":",StringComparison.Ordinal)?TextAlignment.Center:dividers[c].EndsWith(":",StringComparison.Ordinal)?TextAlignment.Right:TextAlignment.Left;Consume();row.Cells.Add(new TableCell(p){FontWeight=r==0?FontWeights.Bold:FontWeights.Normal,Background=r==0?codeBrush:Brushes.Transparent,BorderBrush=tableBorder,BorderThickness=new Thickness(1),Padding=new Thickness(7,5,7,5)});}}return table;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Cells
        /// desc         : code·escape 밖의 pipe만 분리하고 열 수를 유한하게 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 빈 끝 셀과 표 테두리를 구분.
        ///                2026-10-19 CodexOrig : 표시 한도16열의 초과 탐지용 셀 하나까지만 추가 보관.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static List<string> Cells(string line){line=line.Trim();if(line.StartsWith("|",StringComparison.Ordinal))line=line.Substring(1);if(line.EndsWith("|",StringComparison.Ordinal)&&!line.EndsWith("\\|",StringComparison.Ordinal))line=line.Substring(0,line.Length-1);var cells=new List<string>();var text=new StringBuilder();bool escaped=false;int ticks=0;for(int i=0;i<line.Length;i++){char c=line[i];if(c=='|'&&!escaped&&ticks==0){cells.Add(text.ToString().Trim());text.Clear();if(cells.Count>=17)break;continue;}text.Append(c);if(c=='`'&&!escaped){int run=1;while(i+1<line.Length&&line[i+1]=='`'){text.Append('`');i++;run++;}ticks=ticks==0?run:ticks==run?0:ticks;}escaped=c=='\\'&&!escaped;}if(cells.Count<17)cells.Add(text.ToString().Trim());return cells;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddHeading
        /// desc         : 제목에 계층과 accent를 적용하고 내부 anchor 대상을 등록한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 표시 텍스트만으로 유한 slug를 구성.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddHeading(FlowDocument document,string text,int level){var p=Paragraph(text,0);p.FontSize=Math.Min(42,fontSize+(7-level)*2);p.FontWeight=FontWeights.Bold;p.Foreground=accent;p.Margin=new Thickness(0,level==1?12:8,0,7);string slug=new string(text.ToLowerInvariant().Select(c=>Char.IsLetterOrDigit(c)?c:Char.IsWhiteSpace(c)?'-':'\0').Where(c=>c!='\0').ToArray());if(!anchors.ContainsKey(slug))anchors.Add(slug,p);document.Blocks.Add(p);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Paragraph
        /// desc         : 공통 간격과 유한 인라인 구문으로 문단을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 원문은 WPF Run으로만 표시.
        /// </summary>
        /// ---------------------------------------------------------------------
        private Paragraph Paragraph(string text,int depth){Consume();var p=new Paragraph{Margin=new Thickness(0,3,0,9),LineHeight=fontSize*1.55};AddInline(p.Inlines,text,depth);return p;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StartsBlock
        /// desc         : 다음 Markdown 블록이 문단에 합쳐지지 않도록 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제한된 행에만 정규식을 적용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool StartsBlock(string[] lines,int at){string line=lines[at];if(line.Length>8192)return true;string trim=line.TrimStart();return Heading.IsMatch(line)||ListMarker.IsMatch(line)||IsDivider(trim)||FenceLength(trim)>=3||HtmlStart.IsMatch(line)||HtmlDoctype.IsMatch(line)||trim.StartsWith(">",StringComparison.Ordinal)||trim.StartsWith("<!--",StringComparison.Ordinal)||at+1<lines.Length&&(Setext.IsMatch(lines[at+1])||line.Contains("|")&&IsTableDivider(lines[at+1]));}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsTableDivider
        /// desc         : 각 셀의 colon·최소3개 dash만 표 구분자로 인정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : HTML 표나 임의 문법 실행 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsTableDivider(string line){if(line.Length>8192||!line.Contains("|"))return false;var cells=Cells(line);return cells.Count>=2&&cells.All(c=>c.Trim(':').Length>=3&&c.Trim(':').All(v=>v=='-'));}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsDivider
        /// desc         : 수평선 marker만 판별한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 빈 문자열은 구분자가 아님.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsDivider(string text){return text.Length>=3&&(text.All(c=>c=='-')||text.All(c=>c=='*')||text.All(c=>c=='_'));}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FenceLength
        /// desc         : 실제 연속 marker 길이만 세어 닫는 fence를 찾는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : fence 안 Markdown은 해석하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static int FenceLength(string text){if(text.Length==0||text[0]!='`'&&text[0]!='~')return 0;int at=0;while(at<text.Length&&text[at]==text[0])at++;return at;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Closing
        /// desc         : escape와 유한 중첩을 고려하여 링크 괄호를 전진 탐색한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 괄호가 포함된 source 경로·URL을 잘라내지 않음.
        ///                2026-10-19 CodexCode : 실제 방문 문자를 문서 전체 계산 예산에 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private int Closing(string text,int start,char open,char close){int depth=0;for(int at=start;at<text.Length;at++){ChargeScan(1);if(text[at]=='\\'){if(at+1<text.Length)ChargeScan(1);at++;continue;}if(text[at]==open){if(++depth>16)return -1;}else if(text[at]==close&&--depth==0)return at;}return -1;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ScanIndexOf
        /// desc         : 문자열 검색의 남은 길이와 marker 길이의 곱을 검색 전에 지불한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : native ordinal 검색의 최악 비교량도 8Mi 공통 예산 안에서만 수행.
        /// </summary>
        /// ---------------------------------------------------------------------
        private int ScanIndexOf(string text,string marker,int start){ChargeScan((long)(text.Length-start)*marker.Length);return text.IndexOf(marker,start,StringComparison.Ordinal);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ScanIndexOf
        /// desc         : 문자 검색의 전체 남은 길이를 미리 계산 예산에 반영한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 닫히지 않은 angle 반복 검색의 제곱 비용을 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        private int ScanIndexOf(string text,char marker,int start){ChargeScan(text.Length-start);return text.IndexOf(marker,start);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChargeScan
        /// desc         : 문서·재귀 전체가 공유하는 8Mi 계산 예산을 넘기기 전에 표시를 중단한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 입력 최대128Ki의64배; 초과 시 기존 생략 안내만 추가하고 원문은 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ChargeScan(long amount){if(amount<0||amount>MaximumScanWork-scanWork)throw new MarkdownBudgetException();scanWork+=amount;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Destination
        /// desc         : 링크의 angle 대상과 선택 title을 분리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 전체 source 경로는 파일 읽기 없이 호스트로만 전달.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Destination(string text){text=text.Trim();if(text.StartsWith("<",StringComparison.Ordinal)){int end=text.IndexOf('>');return end>0?text.Substring(1,end-1):text;}for(int at=1;at<text.Length-1;at++)if(Char.IsWhiteSpace(text[at])&&(text[at+1]=='"'||text[at+1]=='\''))return text.Substring(0,at);return text.Replace("\\(","(").Replace("\\)",")");}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Consume
        /// desc         : 출력 노드를 문서당 최대6000개로 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 중첩 문법 폭탄에서 UI 객체 무제한 생성을 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Consume(){if(++nodes>MaximumNodes)throw new MarkdownBudgetException();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddRun
        /// desc         : 문자열을 실행되지 않는 WPF 텍스트 노드로 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : HTML 태그도 평문으로 표시.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddRun(InlineCollection target,string value){if(value.Length==0)return;Consume();target.Add(new Run(value));}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Flush
        /// desc         : 연속 평문을 한 노드로 합쳐 출력 수를 줄인다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 문자마다 Run을 만들지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Flush(InlineCollection target,StringBuilder plain){if(plain.Length==0)return;AddRun(target,plain.ToString());plain.Clear();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Indent
        /// desc         : 탭 들여쓰기를 일정한 폭으로 비교한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : source 공백 자체는 수정하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static int Indent(string text){return text.Sum(c=>c=='\t'?4:1);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Paint
        /// desc         : native 문서에 공유 가능한 frozen brush를 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 외부 테마 리소스를 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Brush Paint(byte r,byte g,byte b){var value=new SolidColorBrush(Color.FromRgb(r,g,b));value.Freeze();return value;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 교체되거나 닫힌 문서의 이미지 타이머·프레임을 모두 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 한도 중단으로 문서에 붙지 않은 이미지도 함께 정리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose(){foreach(var image in imageControls)image.Dispose();imageControls.Clear();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkdownBudgetException
        /// desc         : 표시 한도 도달을 명시적 생략 안내로 바꾸는 내부 신호다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 사용자 원문이나 편집 상태를 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class MarkdownBudgetException : Exception { }
    }
}
