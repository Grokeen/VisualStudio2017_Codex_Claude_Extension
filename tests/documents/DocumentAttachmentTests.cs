using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using VS2017.AIAssistant.Chat;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : DocumentAttachmentTests
/// desc         : 합성 문서와 Apache POI fixture로 본문 추출·문서 경계·원본 보존을 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 실제 Windows PDF·DOC·PPT 필터와 격리 도우미 오류 검증.
/// </summary>
/// ---------------------------------------------------------------------
internal static class DocumentAttachmentTests
{
    private static string root; private static int passed;
    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 명시한 테스트 폴더와 fixture만 읽고 검사 결과·최초 실패를 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 원본 사용자 문서·공유 제품 출력은 변경하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try { root = args[0]; Run(args[1], args[2]); Console.WriteLine("PASS " + passed + " document attachment checks"); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 한글·순서·외부 참조·크기·취소·도우미 실패를 실제 추출 API로 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 빈 스캔 PDF와 UTF8 바이트 제한 사례 포함.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Run(string fixtures, string mockWorker)
    {
        Check(DocumentAttachmentReader.CanRead(".PDF") && DocumentAttachmentReader.CanRead("a.docx") && DocumentAttachmentReader.CanRead("a.ppt") && !DocumentAttachmentReader.CanRead("a.exe"), "Format allow list");
        Check(DocumentAttachmentReader.GetFilter().Contains("*.pdf") && DocumentAttachmentReader.GetFormatSupport(".doc").Contains("IFilter"), "Chooser and platform support descriptions");
        string txt = Text("unicode.txt", "한글 UTF8 TEXT_MARKER"); Check(DocumentAttachmentReader.Extract(txt).Contains("한글"), "UTF8 Korean text");
        string utf16 = Path.Combine(root, "unicode16.txt"); File.WriteAllText(utf16, "한글 UTF16_MARKER", Encoding.Unicode); Check(DocumentAttachmentReader.Extract(utf16).Contains("UTF16_MARKER"), "UTF16 BOM text");
        Check(DocumentAttachmentReader.Extract(Text("sample.csv", "Name,Count\nAlpha,7")).Contains("Alpha,7"), "CSV content");
        Check(DocumentAttachmentReader.Extract(Text("sample.html", "<html><head><script>DO_NOT_SEND</script></head><body><p>A &amp; 한글</p><script src='https://example.invalid/missing.js'>DO_NOT_SEND</script><p title='x>y'>HTML_MARKER</p></body></html>")).Contains("A & 한글") && !DocumentAttachmentReader.Extract(Path.Combine(root, "sample.html")).Contains("DO_NOT_SEND"), "HTML strips executable content and decodes entities");
        string rtf = Text("sample.rtf", "{\\rtf1\\ansi{\\fonttbl{\\f0 Arial;}} RTF_MARKER \\u54620?\\u44544?\\par Next {\\object\\objdata DO_NOT_SEND} End}");
        string result = DocumentAttachmentReader.Extract(rtf); Check(result.Contains("한글") && result.Contains("RTF_MARKER") && !result.Contains("DO_NOT_SEND") && !result.Contains("Arial"), "RTF Unicode and ignored OLE/font groups");
        Check(DocumentAttachmentReader.Extract(Text("hex.rtf", "{\\rtf1\\ansi\\ansicpg949 \\'c7\\'d1\\'b1\\'db}")).Contains("한글"), "RTF multibyte codepage hex");

        string wordXml = "<w:document xmlns:w='" + W + "'><w:body><w:p><w:r><w:t>DOCX_MARKER 한글</w:t><w:t>_ADJACENT</w:t></w:r></w:p></w:body></w:document>";
        string docx = Zip("sample.docx", new Dictionary<string,string> { { "word/document.xml", wordXml }, { "word/_rels/document.xml.rels", "<Relationships><Relationship Target='https://example.invalid/' TargetMode='External'/></Relationships>" } });
        string docxHash = Hash(docx); result = DocumentAttachmentReader.Extract(docx);
        Check(result.Contains("DOCX_MARKER 한글_ADJACENT"), "DOCX adjacent runs and Korean"); Check(Hash(docx) == docxHash, "DOCX source bytes unchanged");
        Check(DocumentAttachmentReader.Extract(Zip("strict.docx", new Dictionary<string,string> { { "word/document.xml", wordXml.Replace(W, "http://purl.oclc.org/ooxml/wordprocessingml/main") } })).Contains("DOCX_MARKER"), "Strict OOXML Word namespace");
        string main = "<p:presentation xmlns:p='p' xmlns:r='" + R + "'><p:sldIdLst><p:sldId r:id='r2'/><p:sldId r:id='r1'/></p:sldIdLst></p:presentation>";
        var pptParts = new Dictionary<string,string> { { "ppt/presentation.xml", main }, { "ppt/_rels/presentation.xml.rels", "<Relationships><Relationship Id='r1' Target='slides/slide1.xml'/><Relationship Id='r2' Target='slides/slide2.xml'/></Relationships>" }, { "ppt/slides/slide1.xml", "<a:p xmlns:a='" + A + "'><a:r><a:t>FIRST</a:t></a:r></a:p>" }, { "ppt/slides/slide2.xml", "<a:p xmlns:a='" + A + "'><a:r><a:t>SECOND 한글</a:t></a:r></a:p>" } };
        result = DocumentAttachmentReader.Extract(Zip("ordered.pptx", pptParts)); Check(result.IndexOf("SECOND", StringComparison.Ordinal) < result.IndexOf("FIRST", StringComparison.Ordinal), "PPTX presentation order rather than filename order");
        pptParts["ppt/_rels/presentation.xml.rels"] = "<Relationships><Relationship Id='r1' Target='https://example.invalid/' TargetMode='External'/><Relationship Id='r2' Target='slides/slide2.xml'/></Relationships>";
        Reject(() => DocumentAttachmentReader.Extract(Zip("external.pptx", pptParts)), "External selected slide is not fetched");
        string xlsx = Zip("sample.xlsx", new Dictionary<string,string> { { "xl/workbook.xml", "<workbook xmlns:r='" + R + "'><sheets><sheet r:id='s1'/></sheets></workbook>" }, { "xl/_rels/workbook.xml.rels", "<Relationships><Relationship Id='s1' Target='/xl/worksheets/sheet1.xml'/></Relationships>" }, { "xl/sharedStrings.xml", "<sst><si><r><t>XLSX_</t></r><r><t>한글</t></r></si></sst>" }, { "xl/worksheets/sheet1.xml", "<worksheet><sheetData><row><c t='s'><v>0</v></c><c><f>WEBSERVICE(https://example.invalid)</f><v>42</v></c><c t='inlineStr'><is><t>INLINE</t></is></c></row></sheetData></worksheet>" } });
        result = DocumentAttachmentReader.Extract(xlsx); Check(result.Contains("XLSX_한글") && result.Contains("42") && result.Contains("INLINE") && !result.Contains("WEBSERVICE"), "XLSX shared rich strings inline text and cached values without formula execution");
        foreach (string ext in new[] { "odt", "odp", "ods" })
        { string odf = Zip("sample." + ext, new Dictionary<string,string> { { "content.xml", "<office xmlns:text='urn:oasis:names:tc:opendocument:xmlns:text:1.0'><text:p>ODF_한글<text:s text:c='2'/>MARKER</text:p></office>" } }); Check(DocumentAttachmentReader.Extract(odf).Contains("ODF_한글  MARKER"), ext + " text and repeated spaces"); }

        string pdf = Path.Combine(root, "synthetic.pdf"); File.WriteAllBytes(pdf, Pdf(false)); string pdfHash = Hash(pdf);
        Check(DocumentAttachmentReader.Extract(pdf).Contains("DOCUMENT_PDF_MARKER"), "Real Windows PDF filter body extraction"); Check(Hash(pdf) == pdfHash, "PDF source bytes unchanged");
        string scan = Path.Combine(root, "image-only.pdf"); File.WriteAllBytes(scan, Pdf(true)); Reject(() => DocumentAttachmentReader.Extract(scan), "Image-only PDF asks for OCR", "OCR");
        foreach (string legacy in new[] { "simple.doc", "basic_test_ppt_file.ppt", "Simple.xls" })
        {
            string file = Path.Combine(fixtures, legacy), before = Hash(file); result = DocumentAttachmentReader.Extract(file);
            Check(result.IndexOf(legacy.EndsWith(".doc", StringComparison.Ordinal) ? "simple file created with Word" : legacy.EndsWith(".xls", StringComparison.Ordinal) ? "replaceMe" : "test title", StringComparison.OrdinalIgnoreCase) >= 0, "Real Windows legacy filter " + legacy);
            Check(before == Hash(file), "Legacy source unchanged " + legacy);
        }

        Reject(() => DocumentAttachmentReader.Extract(Text("korean-limit.txt", new string('가', 40000))), "UTF8 byte bound catches Korean below character cap", "100KB");
        string huge = Path.Combine(root, "too-large.txt"); using (var stream = File.Create(huge)) stream.SetLength(DocumentAttachmentReader.MaxFileBytes + 1L);
        Reject(() => DocumentAttachmentReader.Extract(huge), "Physical file bound");
        Reject(() => DocumentAttachmentReader.Extract(Text("binary.txt", "A\0B\0C")), "Binary masquerading as text rejected");
        Reject(() => DocumentAttachmentReader.Extract(Text("wrong.pdf", "not a pdf")), "Damaged PDF rejected");
        Reject(() => DocumentAttachmentReader.Extract(Text("wrong.docx", "not a zip")), "Damaged or encrypted OOXML rejected");
        Reject(() => DocumentAttachmentReader.Extract(Zip("dtd.docx", new Dictionary<string,string> { { "word/document.xml", "<!DOCTYPE x [<!ENTITY secret SYSTEM 'file:///C:/SHOULD_NOT_READ'>]><w:document xmlns:w='" + W + "'><w:t>&secret;</w:t></w:document>" } })), "DTD external entity rejected");
        Reject(() => DocumentAttachmentReader.Extract(Zip("zip-slip.docx", new Dictionary<string,string> { { "word/document.xml", wordXml }, { "../outside.xml", "DO_NOT_WRITE" } })), "ZIP parent traversal entry rejected");
        Reject(() => DocumentAttachmentReader.Extract(Zip("inflation.docx", new Dictionary<string,string> { { "word/document.xml", new string('A', 4 * 1024 * 1024 + 1) } })), "ZIP XML expansion bound");
        var crowded = new Dictionary<string,string>(); for (int i = 0; i < 2049; i++) crowded.Add("part" + i, "");
        Reject(() => DocumentAttachmentReader.Extract(Zip("entries.docx", crowded)), "ZIP entry-count bound");
        Reject(() => DocumentAttachmentReader.Extract(Text("broken.rtf", "{\\rtf1 unclosed")), "Malformed RTF groups rejected");
        using (var cancelled = new CancellationTokenSource()) { cancelled.Cancel(); Reject(() => DocumentAttachmentReader.Extract(txt, cancelled.Token), "Cancelled before parsing", null, typeof(OperationCanceledException)); }
        Check(DocumentAttachmentReader.ExtractAsync(txt, CancellationToken.None).GetAwaiter().GetResult().Contains("TEXT_MARKER"), "Asynchronous extraction API");
        Check(Encoding.UTF8.GetByteCount(DocumentAttachmentReader.Extract(docx)) <= DocumentAttachmentReader.MaxTextBytes, "Output satisfies ChatAttachment UTF8 byte contract");
        MockChecks(mockWorker, pdf);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MockChecks
    /// desc         : 자기 출력 폴더의 도우미 복사본만 교체해 지원 없음·상한·시간·취소를 재현한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 등록 필터와 다른 IDE·도우미 파일은 수정하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void MockChecks(string mock, string pdf)
    {
        string worker = Path.Combine(Path.GetDirectoryName(typeof(DocumentAttachmentReader).Assembly.Location), "VS2017.AIAssistant.DocumentReader.exe"), backup = worker + ".own-backup";
        File.Move(worker, backup);
        try
        {
            Reject(() => DocumentAttachmentReader.Extract(pdf), "Missing worker reported", "도우미"); File.Copy(mock, worker);
            Environment.SetEnvironmentVariable("DOC_READER_FIXTURE", "unsupported"); Reject(() => DocumentAttachmentReader.Extract(pdf), "Unregistered filter reported", "IFilter");
            Environment.SetEnvironmentVariable("DOC_READER_FIXTURE", "password"); Reject(() => DocumentAttachmentReader.Extract(pdf), "Password protected document reported", "암호");
            Environment.SetEnvironmentVariable("DOC_READER_FIXTURE", "oversize"); Reject(() => DocumentAttachmentReader.Extract(pdf), "Worker output byte bound");
            Environment.SetEnvironmentVariable("DOC_READER_FIXTURE", "hang"); string marker = Path.Combine(root, "worker-pid.txt"); Environment.SetEnvironmentVariable("DOC_READER_MARKER", marker);
            Reject(() => InvokeWorker(Pdf(false), CancellationToken.None, TimeSpan.FromMilliseconds(400)), "Worker timeout enforced", null, typeof(TimeoutException)); Check(Gone(marker), "Timed out worker process exited");
            File.Delete(marker);
            using (var cancellation = new CancellationTokenSource(400)) Reject(() => InvokeWorker(Pdf(false), cancellation.Token, TimeSpan.FromSeconds(15)), "Worker cancellation enforced", null, typeof(OperationCanceledException));
            Check(Gone(marker), "Cancelled worker process exited");
        }
        finally { Environment.SetEnvironmentVariable("DOC_READER_FIXTURE", null); Environment.SetEnvironmentVariable("DOC_READER_MARKER", null); if (File.Exists(worker)) File.Delete(worker); File.Move(backup, worker); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : InvokeWorker
    /// desc         : 내부 시간 제한 경계를 제품 DLL과 독립 소스 모두에서 동일하게 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 공개 제품 API에 테스트 전용 옵션을 추가하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void InvokeWorker(byte[] bytes, CancellationToken token, TimeSpan timeout)
    { try { typeof(DocumentAttachmentReader).Assembly.GetType("VS2017.AIAssistant.Chat.DocumentWorkerLauncher").GetMethod("Extract", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { bytes, ".pdf", token, timeout }); } catch (TargetInvocationException error) { throw error.InnerException; } }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Gone
    /// desc         : 자기 fixture PID와 임시 입력 디렉터리의 정리 여부를 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 종료 명령 없이 상태만 조회.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool Gone(string marker)
    {
        if (!File.Exists(marker)) return false; string[] state = File.ReadAllLines(marker); bool gone;
        try { using (var process = Process.GetProcessById(int.Parse(state[0]))) gone = process.WaitForExit(2000); } catch (ArgumentException) { gone = true; }
        return gone && !Directory.Exists(Path.GetDirectoryName(state[1]));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Text
    /// desc         : 고유 테스트 폴더에만 UTF8 합성 문서를 작성한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 사용자 경로와 구분한 fixture 생성.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Text(string name, string value) { string path = Path.Combine(root, name); File.WriteAllText(path, value, new UTF8Encoding(false)); return path; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Zip
    /// desc         : 지정한 XML만 포함하는 재현 가능한 문서 컨테이너를 생성한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 네트워크·Office 앱 없이 경계 문서 생성.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Zip(string name, IDictionary<string,string> parts)
    { string path = Path.Combine(root, name); using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) foreach (var part in parts) using (var writer = new StreamWriter(zip.CreateEntry(part.Key).Open(), new UTF8Encoding(false))) writer.Write(part.Value); return path; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pdf
    /// desc         : 실제 xref를 가진 한 페이지 텍스트 PDF 또는 이미지 전용 PDF를 합성한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : OCR이 없는 필터의 빈 텍스트 오류를 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static byte[] Pdf(bool image)
    {
        string commands = image ? "q 100 0 0 100 30 600 cm /Im1 Do Q" : "BT /F1 18 Tf 50 720 Td (DOCUMENT_PDF_MARKER) Tj ET";
        var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> " + (image ? "/XObject << /Im1 6 0 R >> " : "") + ">> /Contents 5 0 R >>", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", "<< /Length " + commands.Length + " >>\nstream\n" + commands + "\nendstream" };
        if (image) objects.Add("<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode /Length 7 >>\nstream\n000000>\nendstream");
        var pdf = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int>();
        for (int i = 0; i < objects.Count; i++) { offsets.Add(pdf.Length); pdf.Append((i + 1) + " 0 obj\n" + objects[i] + "\nendobj\n"); }
        int xref = pdf.Length; pdf.Append("xref\n0 " + (objects.Count + 1) + "\n0000000000 65535 f \n");
        foreach (int offset in offsets) pdf.Append(offset.ToString("D10") + " 00000 n \n");
        pdf.Append("trailer\n<< /Root 1 0 R /Size " + (objects.Count + 1) + " >>\nstartxref\n" + xref + "\n%%EOF\n"); return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Hash
    /// desc         : 문서 원본의 변경 여부를 SHA256으로 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 읽기 전후 바이트 동일성 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(stream)); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 검사 이름과 통과 수를 기록하며 잘못된 결과는 즉시 실패한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 개별 경계 결과 집계.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Reject
    /// desc         : 실패가 빈 성공 본문으로 바뀌지 않고 명확한 오류로 전달되는지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 사용자 안내 문구를 필요한 사례에서 함께 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Reject(Action action, string name, string message = null, Type expected = null) { bool rejected = false; try { action(); } catch (Exception error) { rejected = (message == null || error.Message.Contains(message)) && (expected == null || expected.IsInstanceOfType(error)); } Check(rejected, name); }
}
