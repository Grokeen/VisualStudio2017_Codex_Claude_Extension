using System;
using System.IO;
using System.Globalization;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownRendering
    /// desc         : Markdown에 포함된 정적 HTML 구조를 같은 제한의 native WPF 문서로 표시한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : R3-03 표·문단·제목·목록·코드 미리보기 차이를 스크립트 실행 없이 보완.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed partial class MarkdownRendering
    {
        private static readonly Regex HtmlStart=new Regex(@"^\s{0,3}(?:<!doctype\s+html\s*>\s*)?<(table|div|p|h[1-6]|ul|ol|pre|blockquote|html|body)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,RegexLimit);
        private static readonly Regex HtmlVoid=new Regex(@"<(br|hr|img|meta|link|input)(\s[^<>]*?)?(?<!/)>",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,RegexLimit);
        private static readonly Regex HtmlDoctype=new Regex(@"^\s*<!doctype\s+html\s*>",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,RegexLimit);
        private static readonly Regex HtmlTags=new Regex(@"<\s*(/?)\s*([A-Za-z][A-Za-z0-9]{0,15})\b[^>]*>",RegexOptions.CultureInvariant,RegexLimit);
        private static readonly Regex HtmlEntities=new Regex(@"&[A-Za-z][A-Za-z0-9]{0,31};",RegexOptions.CultureInvariant,RegexLimit);

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryHtmlBlock
        /// desc         : 64Ki 문자·512행 이하 정적 블록만 DTD·외부 resolver 없이 파싱한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 알 수 없는 HTML은 원문 표시를 유지하고 파일·네트워크를 읽지 않음.
        ///                2026-10-19 CodexOrig : 후속 감사의 중첩·다중 루트·기본 엔터티·정적 전체 문서 사례 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool TryHtmlBlock(string[] lines,int index,out Block block,out int end)
        {
            block=null;end=index;var match=HtmlStart.Match(lines[index]);if(!match.Success)return false;
            string rootTag=match.Groups[1].Value;int balance=0;var text=new StringBuilder();
            for(int next=index;next<lines.Length&&next-index<512;next++)
            {
                if(text.Length+lines[next].Length+1>65536)return false;
                ChargeScan((long)lines[next].Length+1);
                text.AppendLine(lines[next]);
                foreach(Match tag in HtmlTags.Matches(lines[next]))if(tag.Groups[2].Value.Equals(rootTag,StringComparison.OrdinalIgnoreCase)&&!tag.Value.EndsWith("/>",StringComparison.Ordinal))balance+=tag.Groups[1].Length==0?1:-1;
                if(balance>0)continue;
                try
                {
                    ChargeScan(text.Length);
                    string xml=HtmlDoctype.Replace(text.ToString(),"",1);
                    xml="<div>"+HtmlEntities.Replace(HtmlVoid.Replace(xml,"<$1$2 />"),EncodeHtmlEntity)+"</div>";
                    var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536,MaxCharactersFromEntities=1024 };
                    var document=new XmlDocument { XmlResolver=null,PreserveWhitespace=true };
                    using(var input=new StringReader(xml))using(var reader=XmlReader.Create(input,settings))document.Load(reader);
                    block=HtmlBlock(document.DocumentElement,0);end=next+1;return block!=null;
                }
                catch(XmlException){continue;}
            }
            return false;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EncodeHtmlEntity
        /// desc         : 기본 HTML 엔터티를 XML 숫자 문자 참조로 바꾸며 선언·태그를 삽입하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : DTD를 허용하지 않고 copy/nbsp/amp 등의 정적 텍스트만 지원.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string EncodeHtmlEntity(Match match)
        {
            string decoded=WebUtility.HtmlDecode(match.Value);if(decoded==match.Value)return "&amp;"+match.Value.Substring(1);
            var result=new StringBuilder();for(int index=0;index<decoded.Length;index++){int code=Char.ConvertToUtf32(decoded,index);if(Char.IsHighSurrogate(decoded[index]))index++;result.Append("&#").Append(code.ToString(CultureInfo.InvariantCulture)).Append(';');}return result.ToString();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HtmlBlock
        /// desc         : 허용한 구조 태그를 제목·목록·코드·표로 만들고 브라우저 실행 기능은 만들지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 기존 깊이·노드·구문 강조 한도를 공유.
        /// </summary>
        /// ---------------------------------------------------------------------
        private Block HtmlBlock(XmlElement element,int depth)
        {
            Consume();string tag=element.LocalName.ToLowerInvariant();
            if(depth>=MaximumDepth)return new Paragraph(new Run(element.InnerText));
            if(tag=="table")return HtmlTable(element,depth+1);
            if(tag=="ul"||tag=="ol")
            {
                var list=new System.Windows.Documents.List { MarkerStyle=tag=="ol"?TextMarkerStyle.Decimal:TextMarkerStyle.Disc };
                var items=element.ChildNodes.OfType<XmlElement>().Where(x=>x.LocalName.Equals("li",StringComparison.OrdinalIgnoreCase)).Take(257).ToArray();if(items.Length>256)omitted=true;
                foreach(XmlElement item in items.Take(256)){Consume();var paragraph=new Paragraph();foreach(XmlNode node in item.ChildNodes)HtmlInline(paragraph.Inlines,node,depth+1);list.ListItems.Add(new ListItem(paragraph));}
                return list;
            }
            if(tag=="pre")
            {
                var paragraph=new Paragraph { FontFamily=new FontFamily("Consolas"),Background=codeBrush,Foreground=codeForeground,Padding=new Thickness(10) };
                var code=element.ChildNodes.OfType<XmlElement>().FirstOrDefault(x=>x.LocalName.Equals("code",StringComparison.OrdinalIgnoreCase));
                string language=code==null?"":code.GetAttribute("class").Replace("language-","");AddCode(paragraph.Inlines,element.InnerText,language);return paragraph;
            }
            if(tag=="div"||tag=="blockquote"||tag=="html"||tag=="body")
            {
                var section=new Section { Margin=new Thickness(0,4,0,8) };
                if(tag=="blockquote"){section.BorderBrush=accent;section.BorderThickness=new Thickness(3,0,0,0);section.Padding=new Thickness(10,4,4,4);}
                Paragraph inline=null;
                foreach(XmlNode node in element.ChildNodes)
                {
                    var child=node as XmlElement;string childTag=child==null?"":child.LocalName.ToLowerInvariant();
                    if(childTag=="head"||childTag=="script"||childTag=="style")continue;
                    if(child!=null&&(childTag=="table"||childTag=="p"||childTag=="div"||childTag=="ul"||childTag=="ol"||childTag=="pre"||childTag=="blockquote"||childTag=="html"||childTag=="body"||Regex.IsMatch(childTag,@"^h[1-6]$"))){inline=null;section.Blocks.Add(HtmlBlock(child,depth+1));}
                    else{if(inline==null){Consume();inline=new Paragraph();section.Blocks.Add(inline);}HtmlInline(inline.Inlines,node,depth+1);}
                }
                return section;
            }
            var result=new Paragraph { Margin=new Thickness(0,4,0,9) };
            if(tag.Length==2&&tag[0]=='h'&&tag[1]>='1'&&tag[1]<='6'){result.FontSize=fontSize+(7-(tag[1]-'0'))*2;result.FontWeight=FontWeights.Bold;}
            foreach(XmlNode node in element.ChildNodes)HtmlInline(result.Inlines,node,depth+1);return result;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HtmlTable
        /// desc         : HTML 표의 실제 셀을 128행·16열 이내의 WPF 표로 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 셀의 링크·이미지도 기존 명시 클릭·신뢰 폴더 경계를 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private Table HtmlTable(XmlElement element,int depth)
        {
            var table=new Table { CellSpacing=0,Margin=new Thickness(0,5,0,12) };var group=new TableRowGroup();table.RowGroups.Add(group);
            var rows=element.GetElementsByTagName("*").OfType<XmlElement>().Where(x=>x.LocalName.Equals("tr",StringComparison.OrdinalIgnoreCase)).Take(129).ToArray();if(rows.Length>128)omitted=true;
            foreach(XmlElement sourceRow in rows.Take(128))
            {
                Consume();var row=new TableRow();group.Rows.Add(row);int columns=0;
                var cells=sourceRow.ChildNodes.OfType<XmlElement>().Where(x=>x.LocalName.Equals("td",StringComparison.OrdinalIgnoreCase)||x.LocalName.Equals("th",StringComparison.OrdinalIgnoreCase)).Take(17).ToArray();if(cells.Length>16)omitted=true;
                foreach(XmlElement sourceCell in cells.Take(16))
                {
                    Consume();bool header=sourceCell.LocalName.Equals("th",StringComparison.OrdinalIgnoreCase);var paragraph=new Paragraph();foreach(XmlNode node in sourceCell.ChildNodes)HtmlInline(paragraph.Inlines,node,depth+1);
                    row.Cells.Add(new TableCell(paragraph) { BorderBrush=tableBorder,BorderThickness=new Thickness(1),Padding=new Thickness(7,5,7,5),FontWeight=header?FontWeights.Bold:FontWeights.Normal,Background=header?codeBrush:Brushes.Transparent });
                    if(++columns>table.Columns.Count)table.Columns.Add(new TableColumn());
                }
            }
            return table;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HtmlInline
        /// desc         : 정적 강조·줄바꿈·링크·이미지만 표시하고 스크립트·스타일·임베드는 무시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 이벤트 속성·CSS·웹 프로세스 실행 경로 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void HtmlInline(InlineCollection target,XmlNode node,int depth)
        {
            ChargeScan(1);var element=node as XmlElement;
            if(element==null){if(node.NodeType==XmlNodeType.Text||node.NodeType==XmlNodeType.CDATA||node.NodeType==XmlNodeType.SignificantWhitespace||node.NodeType==XmlNodeType.Whitespace)AddRun(target,node.Value??"");return;}
            string tag=element.LocalName.ToLowerInvariant();if(tag=="script"||tag=="style"||tag=="iframe"||tag=="object"||tag=="embed")return;
            if(depth>=MaximumDepth){AddRun(target,element.InnerText);return;}
            if(tag=="a"){AddLink(target,element.InnerText,element.GetAttribute("href"),depth+1);return;}
            if(tag=="img"){AddImage(target,element.GetAttribute("alt"),element.GetAttribute("src"));return;}
            if(tag=="br"){Consume();target.Add(new LineBreak());return;}
            Span span=null;
            if(tag=="b"||tag=="strong")span=new Bold();else if(tag=="i"||tag=="em")span=new Italic();else if(tag=="s"||tag=="del")span=new Span { TextDecorations=TextDecorations.Strikethrough };else if(tag=="code")span=new Span { FontFamily=new FontFamily("Consolas"),Background=codeBrush,Foreground=inlineCodeForeground };
            if(span!=null){Consume();target.Add(span);target=span.Inlines;}
            foreach(XmlNode child in element.ChildNodes)HtmlInline(target,child,depth+1);
        }
    }
}
