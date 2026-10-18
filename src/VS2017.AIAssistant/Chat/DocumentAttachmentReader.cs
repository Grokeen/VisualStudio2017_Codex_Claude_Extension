using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace VS2017.AIAssistant.Chat
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DocumentAttachmentReader
    /// desc         : 원본 문서를 변경하지 않고 제한된 본문 텍스트를 첨부 입력으로 추출한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : OpenXML·OpenDocument·RTF·HTML과 격리된 PDF·구형 Office 추출.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class DocumentAttachmentReader
    {
        public const int MaxFileBytes = 16 * 1024 * 1024;
        public const int MaxTextBytes = 100 * 1024;
        public const int MaxTextCharacters = 100 * 1024;
        private const int MaxXmlBytes = 4 * 1024 * 1024;
        private const string Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private const string Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
        private const string Relation = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly string[] NativeFormats = { ".pdf", ".doc", ".ppt", ".xls" };
        private static readonly string[] ZipFormats = { ".docx", ".pptx", ".xlsx", ".odt", ".odp", ".ods" };
        private static readonly string[] TextFormats = { ".txt", ".md", ".csv", ".tsv", ".log", ".json", ".xml", ".yaml", ".yml", ".rtf", ".htm", ".html" };

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CanRead
        /// desc         : 본문 추출 경로를 제공하는 문서 확장자인지 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : PDF·구형 Office의 실제 지원은 PC의 등록 필터에 따라 결정.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static bool CanRead(string pathOrExtension)
        { string ext = Extension(pathOrExtension); return NativeFormats.Contains(ext) || ZipFormats.Contains(ext) || TextFormats.Contains(ext); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetFilter
        /// desc         : 첨부 파일 선택 대화상자의 문서 확장자 필터를 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 이미지·일반 소스 필터와 함께 사용할 수 있는 고정 문자열.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string GetFilter()
        { return "문서·텍스트|*.pdf;*.doc;*.docx;*.ppt;*.pptx;*.xls;*.xlsx;*.odt;*.odp;*.ods;*.rtf;*.htm;*.html;*.csv;*.tsv;*.txt;*.md;*.log;*.json;*.xml;*.yaml;*.yml"; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetFormatSupport
        /// desc         : 운영체제 필터가 필요한 형식과 기본 추출 형식을 구분해 안내한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 등록 여부와 실제 본문 읽기 성공을 혼동하지 않게 표시.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string GetFormatSupport(string pathOrExtension)
        { string ext = Extension(pathOrExtension); return NativeFormats.Contains(ext) ? "Windows IFilter 필요 · PC별 지원" : CanRead(ext) ? "기본 본문 추출" : "지원하지 않는 문서 형식"; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ExtractAsync
        /// desc         : UI 스레드를 막지 않고 제한된 문서 본문을 추출하며 취소를 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 비동기 붙여넣기·끌어놓기 연결용 API.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static Task<string> ExtractAsync(string path, CancellationToken cancellationToken)
        { return Task.Run(() => Extract(path, cancellationToken), cancellationToken); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Extract
        /// desc         : 최대 16MB 원본을 읽기 전용으로 스냅샷하고 UTF8 100KB 이하 본문만 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 초과·암호·손상·이미지 전용 문서는 원인을 안내하며 부분 전송하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string Extract(string path, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("첨부할 문서 파일을 찾지 못했습니다.");
            string ext = Extension(path); if (!CanRead(ext)) throw new NotSupportedException("지원하지 않는 문서 형식입니다. 텍스트 또는 최신 Office 형식으로 저장하세요.");
            var clock = Stopwatch.StartNew(); cancellationToken.ThrowIfCancellationRequested(); byte[] bytes;
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) bytes = ReadBounded(input, MaxFileBytes);
            var sink = new TextSink(cancellationToken, clock);
            if (NativeFormats.Contains(ext)) return ValidateText(DocumentWorkerLauncher.Extract(bytes, ext, cancellationToken));
            if (ZipFormats.Contains(ext))
            {
                if (bytes.Length < 4 || bytes[0] != 'P' || bytes[1] != 'K') throw new InvalidDataException("암호화되었거나 손상된 Office 문서입니다. 암호를 해제한 최신 형식 사본을 첨부하세요.");
                using (var memory = new MemoryStream(bytes, false)) using (var zip = new ZipArchive(memory, ZipArchiveMode.Read))
                {
                    ValidateZip(zip);
                    if (ext == ".docx") ReadWord(zip, sink);
                    else if (ext == ".pptx") ReadPresentation(zip, sink);
                    else if (ext == ".xlsx") ReadWorkbook(zip, sink);
                    else ReadOpenDocument(zip, sink);
                }
            }
            else
            {
                string text = Decode(bytes);
                if (ext == ".rtf") ReadRtf(text, sink);
                else if (ext == ".html" || ext == ".htm") ReadHtml(text, sink);
                else sink.Append(text);
            }
            return ValidateText(sink.ToString());
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidateText
        /// desc         : 제어 문자·빈 본문·한글 포함 UTF8 바이트 상한을 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : ChatAttachment.FromText와 동일한 100KB 본문 계약 보장.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string ValidateText(string text)
        {
            text = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("문서에 추출 가능한 텍스트가 없습니다. 스캔 PDF·이미지 문서는 OCR 후 텍스트가 포함된 문서로 첨부하세요.");
            if (text.Any(c => char.IsControl(c) && c != '\n' && c != '\t' && c != '\f')) throw new InvalidDataException("텍스트로 읽을 수 없는 문서입니다. 지원하는 형식으로 다시 저장하세요.");
            if (text.Length > MaxTextCharacters || Encoding.UTF8.GetByteCount(text) > MaxTextBytes) throw new InvalidDataException("추출한 문서 본문이 UTF8 100KB를 넘습니다. 필요한 페이지나 구역으로 나누어 첨부하세요.");
            return text;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Extension
        /// desc         : 파일 경로와 확장자 단독 입력을 동일하게 정규화한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 대소문자와 빈 경로 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Extension(string value)
        { return string.IsNullOrWhiteSpace(value) ? "" : (value[0] == '.' && value.IndexOfAny(new[] { '\\', '/' }) < 0 ? value : Path.GetExtension(value)).ToLowerInvariant(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadBounded
        /// desc         : 길이 정보와 무관하게 실제 읽은 바이트 수에도 상한을 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 원본·ZIP 항목의 과도한 메모리 사용 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static byte[] ReadBounded(Stream input, int limit)
        {
            if (input.CanSeek && input.Length > limit) throw new InvalidDataException("문서 파일 또는 압축 해제 내용이 크기 제한을 초과했습니다.");
            using (var output = new MemoryStream())
            { var buffer = new byte[8192]; int count; while ((count = input.Read(buffer, 0, buffer.Length)) > 0) { if (output.Length + count > limit) throw new InvalidDataException("문서 파일 또는 압축 해제 내용이 크기 제한을 초과했습니다."); output.Write(buffer, 0, count); } return output.ToArray(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Decode
        /// desc         : BOM UTF8·UTF16·UTF32와 엄격한 UTF8 텍스트를 읽으며 임의 바이너리를 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 문서 원본 인코딩을 수정하지 않고 읽기만 수행.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Decode(byte[] bytes)
        {
            Encoding encoding = new UTF8Encoding(false, true); int start = 0;
            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0) { encoding = new UTF32Encoding(false, false, true); start = 4; }
            else if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF) { encoding = new UTF32Encoding(true, false, true); start = 4; }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) { encoding = new UnicodeEncoding(false, false, true); start = 2; }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) { encoding = new UnicodeEncoding(true, false, true); start = 2; }
            else if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) start = 3;
            try { return encoding.GetString(bytes, start, bytes.Length - start); }
            catch (DecoderFallbackException) { throw new InvalidDataException("이 텍스트 인코딩을 읽을 수 없습니다. UTF8 또는 BOM이 포함된 Unicode로 저장해 첨부하세요."); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidateZip
        /// desc         : 항목 수·전체 해제 크기·중복 이름·경로를 검사하며 압축 파일을 디스크에 풀지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 최대 2048항목·32MB 해제 메타데이터와 경로 혼동 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ValidateZip(ZipArchive zip)
        {
            if (zip.Entries.Count > 2048) throw new InvalidDataException("문서의 압축 항목 수가 너무 많습니다.");
            long total = 0; var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries)
            {
                total += entry.Length; string name = entry.FullName.Replace('\\', '/');
                if (total > 32 * 1024 * 1024 || !names.Add(name) || name.StartsWith("/", StringComparison.Ordinal) || name.Contains(":") || name.Split('/').Contains("..")) throw new InvalidDataException("문서의 압축 크기 또는 항목 경로가 유효하지 않습니다.");
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenXml
        /// desc         : 지정한 ZIP 항목만 제한해 읽고 외부 엔터티·DTD·XML 리졸버를 차단한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 항목당 4MB·깊이 검사는 본문 순회에서 적용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static XmlReader OpenXml(ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name); if (entry == null) throw new InvalidDataException("문서의 필수 본문 구역을 찾지 못했습니다: " + name);
            byte[] bytes; using (var input = entry.Open()) bytes = ReadBounded(input, MaxXmlBytes);
            return XmlReader.Create(new MemoryStream(bytes, false), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxXmlBytes, MaxCharactersFromEntities = 0, CloseInput = true, IgnoreComments = true });
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadWord
        /// desc         : Word 본문의 글자·줄바꿈·표 셀을 읽고 외부 연결과 필드 명령은 따라가지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 문서 표면의 본문만 AI 참고 텍스트로 반환.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ReadWord(ZipArchive zip, TextSink sink)
        { using (var xml = OpenXml(zip, "word/document.xml")) ReadRuns(xml, Word, sink); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadRuns
        /// desc         : Word·DrawingML의 연속 t 노드를 빠짐없이 읽고 문단 구조를 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : XML 순회마다 취소·깊이·시간을 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool ReadRuns(XmlReader xml, string space, TextSink sink)
        {
            int textDepth = -1; bool found = false;
            while (xml.Read())
            {
                sink.Check(xml.Depth);
                bool knownSpace = xml.NamespaceURI == space || xml.NamespaceURI == (space == Word ? "http://purl.oclc.org/ooxml/wordprocessingml/main" : "http://purl.oclc.org/ooxml/drawingml/main");
                if (xml.NodeType == XmlNodeType.Element && knownSpace)
                { if (xml.LocalName == "t" && !xml.IsEmptyElement) textDepth = xml.Depth; else if (xml.LocalName == "tab") sink.Append("\t"); else if (xml.LocalName == "br" || xml.LocalName == "cr") sink.Append("\n"); }
                else if ((xml.NodeType == XmlNodeType.Text || xml.NodeType == XmlNodeType.CDATA || xml.NodeType == XmlNodeType.SignificantWhitespace) && textDepth >= 0) { sink.Append(xml.Value); if (!string.IsNullOrWhiteSpace(xml.Value)) found = true; }
                else if (xml.NodeType == XmlNodeType.EndElement)
                { if (xml.Depth == textDepth) textDepth = -1; if (knownSpace && xml.LocalName == "p") sink.Append("\n"); else if (space == Word && knownSpace && xml.LocalName == "tc") sink.Append("\t"); }
            }
            return found;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OrderedParts
        /// desc         : 실제 발표·통합문서 순서에 따라 내부 relationship 대상만 해석한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 외부 URL·루트 밖 경로·누락 relationship 거부.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static List<string> OrderedParts(ZipArchive zip, string main, string rels, string element, TextSink sink)
        {
            var targets = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var xml = OpenXml(zip, rels)) while (xml.Read())
            {
                sink.Check(xml.Depth); if (xml.NodeType != XmlNodeType.Element || xml.LocalName != "Relationship") continue;
                string id = xml.GetAttribute("Id"), target = xml.GetAttribute("Target");
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target) && !string.Equals(xml.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)) targets[id] = target;
            }
            var paths = new List<string>();
            using (var xml = OpenXml(zip, main)) while (xml.Read())
            {
                sink.Check(xml.Depth); if (xml.NodeType != XmlNodeType.Element || xml.LocalName != element) continue;
                string id = xml.GetAttribute("id", Relation) ?? xml.GetAttribute("id", "http://purl.oclc.org/ooxml/officeDocument/relationships"), target;
                if (id == null || !targets.TryGetValue(id, out target)) throw new InvalidDataException("문서 순서에 외부 또는 누락된 구역이 있습니다.");
                if (target.IndexOfAny(new[] { ':', '?', '#', '\\' }) >= 0 || target.StartsWith("//", StringComparison.Ordinal)) throw new InvalidDataException("외부 문서 연결은 읽지 않습니다.");
                var resolved = new Uri(new Uri("https://document.local/" + main), target);
                string path = Uri.UnescapeDataString(resolved.AbsolutePath.TrimStart('/'));
                if (resolved.Host != "document.local" || !path.StartsWith(main.Substring(0, main.IndexOf('/') + 1), StringComparison.Ordinal) || path.Split('/').Contains("..")) throw new InvalidDataException("문서 구역 경로가 유효하지 않습니다.");
                paths.Add(path); if (paths.Count > 200) throw new InvalidDataException("발표 또는 시트 구역은 최대 200개까지 첨부할 수 있습니다.");
            }
            if (paths.Count == 0) throw new InvalidDataException("문서에 읽을 구역이 없습니다."); return paths;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadPresentation
        /// desc         : 사용자에게 보이는 발표 순서대로 슬라이드의 글자를 추출한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 숨은 링크·매크로·첨부 파일은 실행하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ReadPresentation(ZipArchive zip, TextSink sink)
        {
            int page = 0; bool found = false;
            foreach (string path in OrderedParts(zip, "ppt/presentation.xml", "ppt/_rels/presentation.xml.rels", "sldId", sink))
            { sink.Append("\n[슬라이드 " + (++page).ToString(CultureInfo.InvariantCulture) + "]\n"); using (var xml = OpenXml(zip, path)) found |= ReadRuns(xml, Drawing, sink); }
            if (!found) throw new InvalidDataException("발표 파일에 추출 가능한 텍스트가 없습니다. 이미지 슬라이드는 OCR 또는 이미지 첨부를 사용하세요.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadWorkbook
        /// desc         : 공유 문자열과 셀의 저장된 값을 읽으며 수식·매크로·외부 데이터 연결은 실행하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 시트 순서·한글·인라인 문자열과 cached value 지원.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ReadWorkbook(ZipArchive zip, TextSink sink)
        {
            var shared = new List<string>(); int sharedLength = 0;
            if (zip.GetEntry("xl/sharedStrings.xml") != null) using (var xml = OpenXml(zip, "xl/sharedStrings.xml"))
            {
                StringBuilder item = null; bool inside = false;
                while (xml.Read())
                {
                    sink.Check(xml.Depth);
                    if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "si") item = new StringBuilder();
                    else if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "t") inside = !xml.IsEmptyElement;
                    else if ((xml.NodeType == XmlNodeType.Text || xml.NodeType == XmlNodeType.SignificantWhitespace) && inside && item != null) { item.Append(xml.Value); sharedLength += xml.Value.Length; if (sharedLength > MaxTextCharacters) throw new InvalidDataException("통합문서 공유 문자열이 너무 큽니다."); }
                    else if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "t") inside = false;
                    else if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "si") { shared.Add(item == null ? "" : item.ToString()); if (shared.Count > 10000) throw new InvalidDataException("통합문서 공유 문자열이 너무 많습니다."); item = null; }
                }
            }
            int sheet = 0; bool found = false;
            foreach (string path in OrderedParts(zip, "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "sheet", sink))
            {
                sink.Append("\n[시트 " + (++sheet).ToString(CultureInfo.InvariantCulture) + "]\n");
                using (var xml = OpenXml(zip, path))
                {
                    string type = ""; bool capture = false; var cell = new StringBuilder();
                    while (xml.Read())
                    {
                        sink.Check(xml.Depth);
                        if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "c") { type = xml.GetAttribute("t") ?? ""; cell.Clear(); }
                        else if (xml.NodeType == XmlNodeType.Element && (xml.LocalName == "v" || (type == "inlineStr" && xml.LocalName == "t"))) capture = !xml.IsEmptyElement;
                        else if ((xml.NodeType == XmlNodeType.Text || xml.NodeType == XmlNodeType.SignificantWhitespace) && capture) { cell.Append(xml.Value); if (cell.Length > MaxTextCharacters) throw new InvalidDataException("셀 내용이 너무 큽니다."); }
                        else if (xml.NodeType == XmlNodeType.EndElement)
                        {
                            if (xml.LocalName == "v" || xml.LocalName == "t") capture = false;
                            else if (xml.LocalName == "c")
                            {
                                string value = cell.ToString(); int index;
                                if (type == "s") { if (!int.TryParse(value, out index) || index < 0 || index >= shared.Count) throw new InvalidDataException("공유 문자열 참조가 유효하지 않습니다."); value = shared[index]; }
                                sink.Append(value); sink.Append("\t"); if (!string.IsNullOrWhiteSpace(value)) found = true;
                            }
                            else if (xml.LocalName == "row") sink.Append("\n");
                        }
                    }
                }
            }
            if (!found) throw new InvalidDataException("통합문서에 추출 가능한 저장된 셀 값이 없습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadOpenDocument
        /// desc         : ODT·ODP·ODS의 문단과 표를 XML 텍스트로 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 텍스트 공백·탭·줄바꿈을 보존하며 링크를 따라가지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ReadOpenDocument(ZipArchive zip, TextSink sink)
        {
            const string textSpace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0"; int paragraph = -1;
            using (var xml = OpenXml(zip, "content.xml")) while (xml.Read())
            {
                sink.Check(xml.Depth);
                if (xml.NodeType == XmlNodeType.Element && xml.NamespaceURI == textSpace)
                {
                    if ((xml.LocalName == "p" || xml.LocalName == "h") && !xml.IsEmptyElement) paragraph = xml.Depth;
                    else if (xml.LocalName == "s") { int spaces; if (!int.TryParse(xml.GetAttribute("c", textSpace), out spaces)) spaces = 1; if (spaces < 1 || spaces > 1000) throw new InvalidDataException("문서 공백 반복 수가 유효하지 않습니다."); sink.Append(new string(' ', spaces)); }
                    else if (xml.LocalName == "tab") sink.Append("\t"); else if (xml.LocalName == "line-break") sink.Append("\n");
                }
                else if ((xml.NodeType == XmlNodeType.Text || xml.NodeType == XmlNodeType.SignificantWhitespace || xml.NodeType == XmlNodeType.CDATA) && paragraph >= 0) sink.Append(xml.Value);
                else if (xml.NodeType == XmlNodeType.EndElement && xml.Depth == paragraph) { sink.Append("\n"); paragraph = -1; }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadHtml
        /// desc         : HTML을 브라우저 없이 읽으며 script·style·head와 외부 자원은 처리하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 인용된 속성·주석·엔터티를 제한된 순회로 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ReadHtml(string html, TextSink sink)
        {
            int index = 0;
            while (index < html.Length)
            {
                sink.Check(0);
                if (html[index] != '<') { int end = html.IndexOf('<', index); if (end < 0) end = html.Length; sink.Append(WebUtility.HtmlDecode(html.Substring(index, end - index))); index = end; continue; }
                if (html.IndexOf("<!--", index, StringComparison.Ordinal) == index) { int commentEnd = html.IndexOf("-->", index + 4, StringComparison.Ordinal); if (commentEnd < 0) throw new InvalidDataException("HTML 주석이 닫히지 않았습니다."); index = commentEnd + 3; continue; }
                int start = ++index; char quote = '\0';
                while (index < html.Length) { char ch = html[index++]; if (quote != '\0') { if (ch == quote) quote = '\0'; } else if (ch == '\'' || ch == '"') quote = ch; else if (ch == '>') break; }
                string tag = html.Substring(start, index - start).Trim().TrimEnd('>').Trim();
                string name = new string(tag.TrimStart('/').TakeWhile(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                if ((name == "script" || name == "style" || name == "head") && !tag.StartsWith("/", StringComparison.Ordinal)) { int end = html.IndexOf("</" + name, index, StringComparison.OrdinalIgnoreCase); index = end < 0 ? html.Length : end; continue; }
                if (new[] { "p", "div", "br", "li", "tr", "h1", "h2", "h3" }.Contains(name)) sink.Append("\n"); else if (name == "td" || name == "th") sink.Append("\t");
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadRtf
        /// desc         : RTF 그룹·Unicode·코드페이지를 읽고 글꼴·그림·OLE·매크로 데이터는 무시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : RichEdit나 Office 실행 없이 순수 텍스트만 추출.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ReadRtf(string text, TextSink sink)
        {
            if (!text.StartsWith("{\\rtf", StringComparison.Ordinal)) throw new InvalidDataException("RTF 문서 형식이 유효하지 않습니다.");
            var stack = new Stack<RtfState>(); var state = new RtfState(); int fallback = 0;
            for (int index = 0; index < text.Length; index++)
            {
                sink.Check(stack.Count); char ch = text[index];
                if (ch == '{') { stack.Push(state); state = state.Copy(); continue; }
                if (ch == '}') { if (stack.Count == 0) throw new InvalidDataException("RTF 그룹이 유효하지 않습니다."); state = stack.Pop(); continue; }
                if (ch != '\\') { if (ch != '\r' && ch != '\n') { if (fallback > 0) fallback--; else if (!state.Ignore) sink.Append(ch.ToString()); } continue; }
                if (++index >= text.Length) throw new InvalidDataException("RTF 제어 문자가 유효하지 않습니다."); ch = text[index];
                if (ch == '*' ) { state.Ignore = true; continue; }
                if (ch == '\\' || ch == '{' || ch == '}') { if (fallback > 0) fallback--; else if (!state.Ignore) sink.Append(ch.ToString()); continue; }
                if (ch == '\'')
                {
                    var bytes = new List<byte>();
                    while (index + 2 < text.Length)
                    {
                        byte value; if (!byte.TryParse(text.Substring(index + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) throw new InvalidDataException("RTF 16진수 문자가 유효하지 않습니다.");
                        if (fallback > 0) fallback--; else bytes.Add(value); index += 2;
                        if (index + 4 >= text.Length || text[index + 1] != '\\' || text[index + 2] != '\'') break; index += 2;
                    }
                    if (!state.Ignore && bytes.Count > 0) sink.Append(Encoding.GetEncoding(state.CodePage).GetString(bytes.ToArray())); continue;
                }
                if (!char.IsLetter(ch)) { if (!state.Ignore && ch == '~') sink.Append(" "); else if (!state.Ignore && ch == '_') sink.Append("-"); continue; }
                int wordStart = index; while (index < text.Length && char.IsLetter(text[index])) index++;
                string word = text.Substring(wordStart, index - wordStart); bool negative = index < text.Length && text[index] == '-'; if (negative) index++;
                int numberStart = index; while (index < text.Length && char.IsDigit(text[index])) index++;
                int number = 0; if (index > numberStart && (!int.TryParse(text.Substring(numberStart, index - numberStart), out number) || index - numberStart > 7)) throw new InvalidDataException("RTF 숫자가 유효하지 않습니다."); if (negative) number = -number;
                if (index < text.Length && text[index] == ' ') index++; index--;
                if (new[] { "fonttbl", "colortbl", "stylesheet", "info", "pict", "object", "objdata", "datastore", "themedata" }.Contains(word)) state.Ignore = true;
                else if (word == "bin") { if (number < 0 || index + number >= text.Length) throw new InvalidDataException("RTF 이진 구역이 유효하지 않습니다."); index += number; }
                else if (word == "ansicpg") { if (number < 1 || number > 65535) throw new InvalidDataException("RTF 코드페이지가 유효하지 않습니다."); state.CodePage = number; }
                else if (word == "uc") { if (number < 0 || number > 10) throw new InvalidDataException("RTF Unicode 대체 문자 수가 유효하지 않습니다."); state.Fallback = number; }
                else if (word == "u") { if (!state.Ignore) sink.Append(unchecked((char)(short)number).ToString()); fallback = state.Fallback; }
                else if (!state.Ignore && (word == "par" || word == "line")) sink.Append("\n"); else if (!state.Ignore && word == "tab") sink.Append("\t");
            }
            if (stack.Count != 0) throw new InvalidDataException("RTF 그룹이 닫히지 않았습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RtfState
        /// desc         : 중첩 RTF 그룹의 숨김·인코딩·Unicode 대체 문자 상태를 복사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 그룹 밖에 OLE·스타일 상태가 전파되지 않게 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class RtfState
        {
            internal bool Ignore; internal int CodePage = 1252, Fallback = 1;
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : Copy
            /// desc         : 새 그룹에 현재 추출 상태를 복사한다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 독립 그룹 상태 생성.
            /// </summary>
            /// ---------------------------------------------------------------------
            internal RtfState Copy() { return new RtfState { Ignore = Ignore, CodePage = CodePage, Fallback = Fallback }; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TextSink
        /// desc         : 추출 중 문자·깊이·15초 시간·취소를 공통으로 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 과도한 XML·문서 구조의 CPU·메모리 사용 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class TextSink
        {
            private readonly StringBuilder text = new StringBuilder(); private readonly CancellationToken token; private readonly Stopwatch clock;
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : TextSink
            /// desc         : 요청의 시간계와 취소 토큰을 공유한다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 문서 구역마다 공통 제한 사용.
            /// </summary>
            /// ---------------------------------------------------------------------
            internal TextSink(CancellationToken token, Stopwatch clock) { this.token = token; this.clock = clock; }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : Check
            /// desc         : 취소·시간 초과·과도한 중첩을 본문 순회에서 검사한다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : XML·RTF 깊이 128 제한.
            /// </summary>
            /// ---------------------------------------------------------------------
            internal void Check(int depth) { token.ThrowIfCancellationRequested(); if (clock.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("문서 읽기 제한 시간 15초를 초과했습니다."); if (depth > 128) throw new InvalidDataException("문서의 중첩 구조가 너무 깊습니다."); }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : Append
            /// desc         : 전체 본문 메모리 상한을 넘기 전에 추출을 중단한다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 잘린 부분을 정상 본문처럼 전송하지 않음.
            /// </summary>
            /// ---------------------------------------------------------------------
            internal void Append(string value) { Check(0); if (text.Length + value.Length > MaxTextCharacters) throw new InvalidDataException("문서 본문이 100KB 제한을 넘습니다. 필요한 구역으로 나누어 첨부하세요."); text.Append(value); }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : ToString
            /// desc         : 검증 전 추출된 문자열을 반환한다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 최종 UTF8 바이트 검사는 공통 ValidateText에서 수행.
            /// </summary>
            /// ---------------------------------------------------------------------
            public override string ToString() { return text.ToString(); }
        }
    }
}
