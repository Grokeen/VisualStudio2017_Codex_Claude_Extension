using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32;

namespace VS2017.AIAssistant.Chat
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DocumentFilterWorker
    /// desc         : Windows IFilter를 별도 프로세스에서 사용해 PDF·구형 Office의 본문만 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 외부 링크·내장 문서 탐색 없이 제한된 UTF8 결과 반환.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class DocumentFilterWorker
    {
        private static readonly Guid Storage = new Guid("B725F130-47EF-101A-A5F1-02608C9EEBAC");
        [StructLayout(LayoutKind.Sequential)] private struct PropertySpec { internal uint Kind; internal IntPtr Value; }
        [StructLayout(LayoutKind.Sequential)] private struct FullProperty { internal Guid Set; internal PropertySpec Property; }
        [StructLayout(LayoutKind.Sequential)] private struct Chunk { internal uint Id, Break, Flags, Locale; internal FullProperty Attribute; internal uint Source, Start, Length; }
        [ComImport, Guid("89BCB740-6119-101A-BCB7-00DD010655AF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFilter
        {
            [PreserveSig] int Init(uint flags, uint count, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] FullProperty[] attributes, out uint outputFlags);
            [PreserveSig] int GetChunk(out Chunk chunk);
            [PreserveSig] int GetText(ref uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] char[] text);
            [PreserveSig] int GetValue(out IntPtr value);
            [PreserveSig] int BindRegion(IntPtr region, ref Guid interfaceId, out IntPtr instance);
        }
        [ComImport, Guid("B824B49D-22AC-4161-AC8A-9916E8FA3F7F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IInitializeWithStream { [PreserveSig] int Initialize(IStream stream, uint mode); }
        [DllImport("query.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int LoadIFilter(string file, IntPtr outer, [MarshalAs(UnmanagedType.Interface)] out IFilter filter);
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateStreamOnFileEx(string file, uint mode, uint attributes, [MarshalAs(UnmanagedType.Bool)] bool create, IStream template, out IStream stream);

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Main
        /// desc         : 임시 복사본을 읽고 본문 결과만 저장하며 원본·Office 앱·매크로는 실행하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 성공0·읽기오류2·필터없음3·암호4·빈본문5·상한6 반환.
        /// </summary>
        /// ---------------------------------------------------------------------
        [STAThread]
        private static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            if (args.Length != 2) return 2;
            IFilter filter = null; IStream fileStream = null;
            try
            {
                string extension = Path.GetExtension(args[0]).ToLowerInvariant();
                if (extension != ".pdf" && extension != ".doc" && extension != ".ppt" && extension != ".xls") return 2;
                if (new FileInfo(args[0]).Length > 16777216) return 6;
                int code = LoadIFilter(args[0], IntPtr.Zero, out filter);
                if ((code < 0 || filter == null) && extension == ".pdf")
                {
                    if (filter != null) { Marshal.FinalReleaseComObject(filter); filter = null; }
                    code = OpenStreamFilter(args[0], out filter, out fileStream);
                }
                if (code == unchecked((int)0x8004170B)) { Console.Error.WriteLine("암호로 보호된 문서는 읽을 수 없습니다. 암호를 해제한 사본을 첨부하세요."); return 4; }
                if (code < 0 || filter == null) { Console.Error.WriteLine("이 PC에 문서를 읽는 Windows IFilter가 없거나 암호·손상으로 파일을 열 수 없습니다. 최신 Office 형식 또는 텍스트로 저장해 첨부하세요. (" + code.ToString("X8") + ")"); return 3; }
                uint flags;
                var content = new[] { new FullProperty { Set = Storage, Property = new PropertySpec { Kind = 1, Value = new IntPtr(19) } } };
                code = filter.Init(1 | 2 | 4 | 8 | 64 | 2048, 1, content, out flags);
                if (code == unchecked((int)0x8004170B)) { Console.Error.WriteLine("암호로 보호된 문서는 읽을 수 없습니다. 암호를 해제한 사본을 첨부하세요."); return 4; }
                if (code < 0) Marshal.ThrowExceptionForHR(code);
                var result = new StringBuilder(); var buffer = new char[4096]; int chunkCount = 0;
                while (true)
                {
                    Chunk chunk; code = filter.GetChunk(out chunk);
                    if (code == unchecked((int)0x80041700)) break;
                    if (code < 0) Marshal.ThrowExceptionForHR(code);
                    if (++chunkCount > 4096) throw new InvalidDataException("문서 구역 수가 제한을 초과했습니다.");
                    if ((chunk.Flags & 1) == 0 || chunk.Attribute.Set != Storage || chunk.Attribute.Property.Kind != 1 || chunk.Attribute.Property.Value.ToInt64() != 19) continue;
                    int reads = 0;
                    while (true)
                    {
                        uint count = (uint)buffer.Length; code = filter.GetText(ref count, buffer);
                        if (code == unchecked((int)0x80041701) || code == unchecked((int)0x80041705)) break;
                        if (code < 0) Marshal.ThrowExceptionForHR(code);
                        if (count > buffer.Length || ++reads > 1024) throw new InvalidDataException("문서 텍스트 버퍼가 유효하지 않습니다.");
                        if (result.Length + count > 102400) return 6;
                        result.Append(buffer, 0, (int)count);
                        if (code == 0x00041709 || count == 0) break;
                    }
                    result.Append('\n');
                }
                string text = result.ToString().Replace("\0", "").Replace('\u0001', ' ').Replace('\u2028', '\n').Replace('\u2029', '\n').Trim();
                if (string.IsNullOrWhiteSpace(text)) { Console.Error.WriteLine(extension == ".pdf" ? "PDF에 추출 가능한 텍스트가 없습니다. 스캔 PDF는 OCR 후 텍스트가 포함된 PDF로 저장하세요." : "문서에 추출 가능한 본문이 없습니다. 최신 Office 형식이나 텍스트로 저장하세요."); return 5; }
                if (Encoding.UTF8.GetByteCount(text) > 102400) return 6;
                File.WriteAllText(args[1], text, new UTF8Encoding(false)); return 0;
            }
            catch (Exception error) { Console.Error.WriteLine("문서 본문을 읽지 못했습니다. 손상·암호·지원 형식을 확인하세요. " + error.GetType().Name); return error.HResult == unchecked((int)0x8004170B) ? 4 : 2; }
            finally { if (filter != null && Marshal.IsComObject(filter)) Marshal.FinalReleaseComObject(filter); if (fileStream != null && Marshal.IsComObject(fileStream)) Marshal.FinalReleaseComObject(fileStream); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenStreamFilter
        /// desc         : IPersistFile 대신 읽기 전용 IStream을 요구하는 Windows PDF 필터를 초기화한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 운영체제에 등록된 PDF IFilter만 사용하며 파일 생성은 허용하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static int OpenStreamFilter(string path, out IFilter filter, out IStream stream)
        {
            filter = null; stream = null;
            using (var handlerKey = Registry.ClassesRoot.OpenSubKey(".pdf\\PersistentHandler"))
            {
                string handler = handlerKey == null ? null : handlerKey.GetValue("") as string;
                using (var filterKey = Registry.ClassesRoot.OpenSubKey("CLSID\\" + handler + "\\PersistentAddinsRegistered\\{89BCB740-6119-101A-BCB7-00DD010655AF}"))
                {
                    string id = filterKey == null ? null : filterKey.GetValue("") as string; Guid classId;
                    if (!Guid.TryParse(id, out classId)) return unchecked((int)0x80004002);
                    filter = Activator.CreateInstance(Type.GetTypeFromCLSID(classId)) as IFilter;
                    var initialize = filter as IInitializeWithStream;
                    if (initialize == null) return unchecked((int)0x80004002);
                    int code = SHCreateStreamOnFileEx(path, 0x20, 0, false, null, out stream);
                    return code < 0 ? code : initialize.Initialize(stream, 0);
                }
            }
        }
    }
}
