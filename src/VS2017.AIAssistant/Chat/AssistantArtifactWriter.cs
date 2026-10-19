using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Microsoft.Win32.SafeHandles;

namespace VS2017.AIAssistant.Chat
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantArtifactWriter
    /// desc         : 사용자가 선택한 새 로컬 파일에 답변을 실제 형식으로 생성하며 실행하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK061 새 파일만 생성·UTF-8·비활성 HTML·OpenXML Word 지원.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class AssistantArtifactWriter
    {
        public const int MaxCharacters = 1024 * 1024;
        private const string Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly HashSet<string> TextExtensions = new HashSet<string>(
            (".md .markdown .txt .log .json .xml .csv .tsv .yaml .yml .toml .ini .config " +
             ".cs .vb .fs .js .jsx .ts .tsx .py .java .c .cpp .h .hpp .css .sql .ps1 .sh .bat .cmd .go .rs .rb .php .swift .kt .r").Split(' '), StringComparer.OrdinalIgnoreCase);
        public const string SaveFilter = "Markdown (*.md)|*.md|텍스트 (*.txt)|*.txt|Word 문서 (*.docx)|*.docx|안전한 HTML 문서 (*.html)|*.html|코드·구조화 텍스트|*.cs;*.vb;*.fs;*.js;*.jsx;*.ts;*.tsx;*.py;*.java;*.c;*.cpp;*.h;*.hpp;*.css;*.sql;*.ps1;*.sh;*.bat;*.cmd;*.go;*.rs;*.rb;*.php;*.swift;*.kt;*.r;*.json;*.xml;*.csv;*.tsv;*.yaml;*.yml;*.toml;*.ini;*.config;*.log;*.markdown";

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryCreateNew
        /// desc         : 취소 시 검증·쓰기 없이 반환하며 승인 시 기존 파일을 덮어쓰지 않고 새 파일만 만든다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 부모 디렉터리 이동 잠금과 열린 임시 파일의 원자적 비덮어쓰기 이름 변경.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static bool TryCreateNew(bool userConfirmed, string path, string content)
        {
            if (!userConfirmed) return false;
            string full = ValidatePath(path);
            byte[] bytes = Encode(Path.GetExtension(full), content ?? "");
            var parents = new List<SafeFileHandle>();
            try
            {
                LockParents(Path.GetDirectoryName(full), parents);
                if (File.Exists(full) || Directory.Exists(full))
                    throw new IOException("기존 파일은 보존됩니다. 다른 이름으로 새 파일을 만드세요.");
                string temporary = Path.Combine(Path.GetDirectoryName(full), ".ai-artifact-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (SafeFileHandle handle = CreateFile(temporary, 0x40000000u | 0x00010000u, 0, IntPtr.Zero, 1, 0x00200000u, IntPtr.Zero))
                {
                    if (handle.IsInvalid) throw NativeError("임시 파일을 만들 수 없습니다.");
                    bool renamed = false;
                    using (var output = new FileStream(handle, FileAccess.Write))
                    {
                        try
                        {
                            output.Write(bytes, 0, bytes.Length);
                            output.Flush(true);
                            // 핸들이 열린 동안 이름을 바꾼다. 임시 경로 교체나 대상 생성 경쟁은 덮어쓰지 않는다.
                            RenameNew(handle, full);
                            renamed = true;
                        }
                        finally
                        {
                            if (!renamed)
                            {
                                int delete = 1;
                                SetFileInformationByHandle(handle, 4, ref delete, 4);
                            }
                        }
                    }
                }
                return true;
            }
            finally { for (int i = parents.Count - 1; i >= 0; i--) parents[i].Dispose(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidatePath
        /// desc         : 로컬 드라이브의 명확한 파일 이름과 지원 확장자만 허용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : UNC·장치·ADS·예약 이름·연결 경로 및 형식 위장 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string ValidatePath(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || path.Length > 240 || path.Length < 4 || !Char.IsLetter(path[0]) ||
                path[1] != ':' || (path[2] != '\\' && path[2] != '/') || path.Any(Char.IsControl) || path.IndexOf(':', 2) >= 0)
                throw new InvalidDataException("로컬 드라이브의 새 파일을 선택하세요. 네트워크·장치 경로는 지원하지 않습니다.");
            foreach (string part in path.Substring(3).Replace('/', '\\').Split('\\'))
            {
                if (part.Length == 0 || part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal) ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || IsDeviceName(part))
                    throw new InvalidDataException("파일과 폴더 이름을 확인하세요.");
            }
            string full = Path.GetFullPath(path);
            string extension = Path.GetExtension(full);
            if (!TextExtensions.Contains(extension) && !extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".html", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Markdown·텍스트·HTML·DOCX 또는 목록의 코드 형식을 선택하세요. PDF·XLSX·실행 파일 생성은 지원하지 않습니다.");
            var drive = new DriveInfo(Path.GetPathRoot(full));
            if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable)
                throw new InvalidDataException("로컬 디스크의 새 파일을 선택하세요.");
            if (Directory.Exists(full)) throw new InvalidDataException("폴더 대신 새 파일 이름을 지정하세요.");
            return full;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsDeviceName
        /// desc         : Win32 예약 장치 이름을 확장자 유무와 관계없이 거부한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 장치 별칭으로 새 파일 동작이 바뀌는 경우 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsDeviceName(string name)
        {
            string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            return stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CONIN$" || stem == "CONOUT$" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                ("123456789¹²³".IndexOf(stem[3]) >= 0));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LockParents
        /// desc         : 루트부터 부모까지 연결점을 따라가지 않고 열어 저장 중 경로 변경을 막는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : FILE_SHARE_DELETE 제외·OPEN_REPARSE_POINT·실제 핸들 속성 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void LockParents(string parent, List<SafeFileHandle> locks)
        {
            string current = Path.GetPathRoot(parent);
            var paths = new List<string> { current };
            foreach (string segment in parent.Substring(current.Length).Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
            { current = Path.Combine(current, segment); paths.Add(current); }
            foreach (string directory in paths)
            {
                var handle = CreateFile(directory, 0x80u, 3, IntPtr.Zero, 3, 0x02000000u | 0x00200000u, IntPtr.Zero);
                if (handle.IsInvalid) { handle.Dispose(); throw NativeError("저장 폴더를 안전하게 열 수 없습니다."); }
                locks.Add(handle);
                FileInformation info;
                if (!GetFileInformationByHandle(handle, out info)) throw NativeError("저장 폴더를 확인할 수 없습니다.");
                if ((info.Attributes & 0x400u) != 0 || (info.Attributes & 0x10u) == 0)
                    throw new InvalidDataException("연결 폴더·정션·심볼릭 링크에는 저장할 수 없습니다. 실제 로컬 폴더를 선택하세요.");
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RenameNew
        /// desc         : 완성된 열린 파일 핸들을 새 이름으로 원자적으로 이동하며 대상이 생기면 실패한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : ReplaceIfExists=false로 확인 이후 대상 생성 경쟁에서도 기존 파일 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RenameNew(SafeFileHandle file, string path)
        {
            byte[] name = Encoding.Unicode.GetBytes(path);
            int lengthOffset = IntPtr.Size * 2;
            int nameOffset = lengthOffset + 4;
            IntPtr buffer = Marshal.AllocHGlobal(nameOffset + name.Length + 2);
            try
            {
                for (int i = 0; i < nameOffset; i++) Marshal.WriteByte(buffer, i, 0);
                Marshal.WriteInt32(buffer, lengthOffset, name.Length);
                Marshal.Copy(name, 0, IntPtr.Add(buffer, nameOffset), name.Length);
                Marshal.WriteInt16(buffer, nameOffset + name.Length, 0);
                if (!SetFileInformationByHandle(file, 3, buffer, (uint)(nameOffset + name.Length + 2)))
                    throw NativeError("새 파일을 만들지 못했습니다. 같은 이름의 파일이 생겼다면 새 이름을 선택하세요.");
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Encode
        /// desc         : 텍스트 원문·비활성 HTML·Word OpenXML을 확장자에 맞춰 메모리에서 완성한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 원문을 Markdown 렌더링이나 AI 명령으로 해석하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static byte[] Encode(string extension, string content)
        {
            if (content.Length > MaxCharacters) throw new InvalidDataException("파일 생성은 답변 1Mi 문자까지 지원합니다.");
            // 엄격한 UTF-8 인코딩으로 잘못된 surrogate를 묵시적으로 바꾸지 않는다.
            Utf8.GetByteCount(content);
            if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)) return CreateDocx(content);
            if (extension.Equals(".html", StringComparison.OrdinalIgnoreCase) || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
            {
                string escaped = content.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
                return Utf8.GetBytes("<!doctype html><html lang=\"ko\"><head><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"><title>AI 답변</title><style>body{margin:2em;font:16px system-ui,sans-serif;color:#202020;background:#fff}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:inherit}</style></head><body><pre>" + escaped + "</pre></body></html>");
            }
            return Utf8.GetBytes(content);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateDocx
        /// desc         : 매크로·외부 관계·링크 실행 없이 한글과 줄바꿈·탭이 보존되는 실제 Word 문서를 만든다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : Markdown은 원문으로 기록하며 복잡한 표·수식·스타일 변환은 하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static byte[] CreateDocx(string content)
        {
            XmlConvert.VerifyXmlChars(content);
            using (var result = new MemoryStream())
            {
                using (var zip = new ZipArchive(result, ZipArchiveMode.Create, true))
                {
                    WriteEntry(zip, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"utf-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
                    WriteEntry(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"utf-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
                    using (var stream = zip.CreateEntry("word/document.xml").Open())
                    using (var xml = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = Utf8, CloseOutput = false }))
                    {
                        xml.WriteStartDocument(); xml.WriteStartElement("w", "document", Word); xml.WriteStartElement("w", "body", Word);
                        foreach (string line in content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                        {
                            xml.WriteStartElement("w", "p", Word); xml.WriteStartElement("w", "r", Word);
                            xml.WriteStartElement("w", "rPr", Word);
                            xml.WriteStartElement("w", "rFonts", Word); xml.WriteAttributeString("w", "ascii", Word, "Calibri"); xml.WriteAttributeString("w", "hAnsi", Word, "Calibri"); xml.WriteAttributeString("w", "eastAsia", Word, "Malgun Gothic"); xml.WriteEndElement();
                            xml.WriteStartElement("w", "sz", Word); xml.WriteAttributeString("w", "val", Word, "22"); xml.WriteEndElement(); xml.WriteEndElement();
                            string[] pieces = line.Split('\t');
                            for (int i = 0; i < pieces.Length; i++)
                            {
                                if (i > 0) { xml.WriteStartElement("w", "tab", Word); xml.WriteEndElement(); }
                                xml.WriteStartElement("w", "t", Word); xml.WriteAttributeString("xml", "space", null, "preserve"); xml.WriteString(pieces[i]); xml.WriteEndElement();
                            }
                            xml.WriteEndElement(); xml.WriteEndElement();
                        }
                        xml.WriteStartElement("w", "sectPr", Word);
                        xml.WriteStartElement("w", "pgSz", Word); xml.WriteAttributeString("w", "w", Word, "12240"); xml.WriteAttributeString("w", "h", Word, "15840"); xml.WriteEndElement();
                        xml.WriteStartElement("w", "pgMar", Word); foreach (string side in new[] { "top", "bottom", "left", "right" }) xml.WriteAttributeString("w", side, Word, "1440"); xml.WriteEndElement();
                        xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndDocument();
                    }
                }
                return result.ToArray();
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WriteEntry
        /// desc         : 고정 OpenXML 패키지 메타데이터를 UTF-8로 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 사용자 원문은 XML 문자열 조합에 사용하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void WriteEntry(ZipArchive zip, string name, string value)
        { using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), Utf8)) writer.Write(value); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryExtractSingleCodeBlock
        /// desc         : 완결된 fenced code block이 하나일 때만 본문 그대로를 별도 저장 대상으로 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 다중·미완결·중첩 추정 없음, 언어 정보는 고정 확장자 제안에만 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static bool TryExtractSingleCodeBlock(string markdown, out string code, out string extension)
        {
            code = null; extension = ".txt";
            if (String.IsNullOrEmpty(markdown) || markdown.Length > MaxCharacters) return false;
            int bodyStart = -1, blocks = 0, fenceSize = 0; char marker = '\0'; string language = "";
            for (int start = 0; start < markdown.Length;)
            {
                int end = markdown.IndexOf('\n', start); if (end < 0) end = markdown.Length;
                string line = markdown.Substring(start, end - start).TrimEnd('\r');
                int spaces = 0; while (spaces < line.Length && line[spaces] == ' ') spaces++;
                if (spaces <= 3 && spaces < line.Length && (line[spaces] == '`' || line[spaces] == '~'))
                {
                    char nextMarker = line[spaces]; int count = 0;
                    while (spaces + count < line.Length && line[spaces + count] == nextMarker) count++;
                    if (count >= 3)
                    {
                        string tail = line.Substring(spaces + count).Trim();
                        if (bodyStart < 0)
                        {
                            if (++blocks > 1 || (nextMarker == '`' && tail.IndexOf('`') >= 0)) return false;
                            marker = nextMarker; fenceSize = count; language = tail; bodyStart = end < markdown.Length ? end + 1 : end;
                        }
                        else if (nextMarker == marker && count >= fenceSize && tail.Length == 0)
                        { code = markdown.Substring(bodyStart, start - bodyStart); bodyStart = -1; }
                    }
                }
                start = end < markdown.Length ? end + 1 : markdown.Length;
            }
            if (blocks != 1 || bodyStart >= 0 || code == null) { code = null; return false; }
            switch (language.Trim().ToLowerInvariant())
            {
                case "csharp": case "cs": extension = ".cs"; break;
                case "javascript": case "js": extension = ".js"; break;
                case "typescript": case "ts": extension = ".ts"; break;
                case "python": case "py": extension = ".py"; break;
                case "powershell": case "ps1": extension = ".ps1"; break;
                case "html": extension = ".html"; break;
                case "bash": case "shell": case "sh": extension = ".sh"; break;
                default: if (TextExtensions.Contains("." + language)) extension = "." + language; break;
            }
            return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NativeError
        /// desc         : 저장 실패를 호출자에게 전달하되 답변이나 인증 정보를 오류에 포함하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 원래 Win32 코드는 내부 예외로 보존한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IOException NativeError(string message)
        { return new IOException(message, new Win32Exception(Marshal.GetLastWin32Error())); }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
            public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle file, int informationClass, IntPtr information, uint bytes);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle file, int informationClass, ref int information, uint bytes);
    }
}
