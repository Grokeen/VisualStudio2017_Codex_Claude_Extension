using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace VS2017.AIAssistant.Chat
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DocumentWorkerLauncher
    /// desc         : 파일 스냅샷만 문서 도우미에 전달하고 시간·메모리·취소·임시 파일을 관리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : Windows 필터 오류가 IDE 프로세스에 영향을 주지 않도록 격리.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class DocumentWorkerLauncher
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Extract
        /// desc         : DLL 옆의 고정 도우미를 셸 없이 실행하고 최대 15초 안에 본문을 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 동기 API는 검사·호환용이며 UI 호출은 ExtractAsync로 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string Extract(byte[] bytes, string extension, CancellationToken cancellation, TimeSpan? timeout = null)
        {
            string worker = Path.Combine(Path.GetDirectoryName(typeof(DocumentAttachmentReader).Assembly.Location), "VS2017.AIAssistant.DocumentReader.exe");
            if (!File.Exists(worker)) throw new FileNotFoundException("문서 읽기 도우미가 없습니다. AI Assistant VSIX를 다시 설치하세요.");
            cancellation.ThrowIfCancellationRequested();
            string folder = Path.Combine(Path.GetTempPath(), "VS2017AIDoc-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(folder, "document" + extension), output = Path.Combine(folder, "content.txt");
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllBytes(source, bytes); File.SetAttributes(source, FileAttributes.ReadOnly);
                var start = new ProcessStartInfo(worker, Quote(source) + " " + Quote(output)) { WorkingDirectory = folder, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var process = new Process { StartInfo = start }) using (var job = new DocumentReaderJob())
                {
                    try
                    {
                        if (!process.Start()) throw new InvalidOperationException("문서 읽기 도우미를 시작하지 못했습니다.");
                        job.Attach(process); process.StandardInput.Close();
                        process.OutputDataReceived += (sender, args) => { }; process.ErrorDataReceived += (sender, args) => { };
                        process.BeginOutputReadLine(); process.BeginErrorReadLine();
                        var clock = Stopwatch.StartNew(); TimeSpan limit = timeout ?? TimeSpan.FromSeconds(15);
                        using (cancellation.Register(job.Dispose))
                        {
                            while (!process.WaitForExit(50))
                            { cancellation.ThrowIfCancellationRequested(); if (clock.Elapsed > limit) throw new TimeoutException("문서 읽기 제한 시간 15초를 초과했습니다. 문서를 작은 구역으로 나누어 첨부하세요."); }
                            cancellation.ThrowIfCancellationRequested();
                        }
                        if (process.ExitCode == 3) throw new NotSupportedException("Windows IFilter가 없거나 암호·손상으로 이 문서를 열지 못했습니다. 암호를 해제한 DOCX·PPTX·XLSX 또는 텍스트 사본으로 첨부하세요.");
                        if (process.ExitCode == 4) throw new InvalidDataException("암호로 보호된 문서는 읽을 수 없습니다. 암호를 해제한 사본을 첨부하세요.");
                        if (process.ExitCode == 5) throw new InvalidDataException(extension == ".pdf" ? "PDF에 추출 가능한 텍스트가 없습니다. 스캔 PDF는 OCR 후 텍스트가 포함된 PDF로 저장하세요." : "문서에 추출 가능한 본문이 없습니다. 최신 Office 형식 또는 텍스트로 저장해 첨부하세요.");
                        if (process.ExitCode == 6) throw new InvalidDataException("추출한 문서 본문이 UTF8 100KB 제한을 넘습니다. 필요한 페이지나 구역으로 나누어 첨부하세요.");
                        if (process.ExitCode != 0 || !File.Exists(output)) throw new InvalidDataException("문서 본문을 읽지 못했습니다. 손상·암호·지원 형식을 확인하거나 최신 Office 형식으로 저장하세요.");
                        byte[] text; using (var input = File.OpenRead(output)) text = DocumentAttachmentReader.ReadBounded(input, DocumentAttachmentReader.MaxTextBytes);
                        return new UTF8Encoding(false, true).GetString(text);
                    }
                    finally
                    {
                        job.Dispose();
                        try { if (!process.HasExited) process.Kill(); process.WaitForExit(2000); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                    }
                }
            }
            finally
            {
                try { if (File.Exists(source)) { File.SetAttributes(source, FileAttributes.Normal); File.Delete(source); } } catch (IOException) { } catch (UnauthorizedAccessException) { }
                try { if (File.Exists(output)) File.Delete(output); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                try { Directory.Delete(folder, false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Quote
        /// desc         : 파일 경로의 공백·따옴표·역슬래시를 Windows argv 규칙대로 인코딩한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 사용자 경로를 셸 명령으로 해석하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Quote(string value)
        {
            var result = new StringBuilder("\""); int slash = 0;
            foreach (char ch in value) { if (ch == '\\') { slash++; continue; } result.Append('\\', ch == '"' ? slash * 2 + 1 : slash); result.Append(ch); slash = 0; }
            return result.Append('\\', slash * 2).Append('"').ToString();
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DocumentReaderJob
    /// desc         : 문서 필터 프로세스의 메모리를 256MB로 제한하고 종료 시 자식도 정리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 원본 문서나 다른 IDE 프로세스에 종료 명령을 보내지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class DocumentReaderJob : IDisposable
    {
        private readonly SafeFileHandle handle;
        [StructLayout(LayoutKind.Sequential)] private struct Basic { internal long ProcessTime, JobTime; internal uint Flags; internal UIntPtr Minimum, Maximum; internal uint Active; internal UIntPtr Affinity; internal uint Priority, Scheduling; }
        [StructLayout(LayoutKind.Sequential)] private struct Io { internal ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] private struct Extended { internal Basic Basic; internal Io Io; internal UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(SafeFileHandle job, int kind, ref Extended data, uint length);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DocumentReaderJob
        /// desc         : 하드 메모리 제한과 KILL_ON_JOB_CLOSE를 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 설정 실패 시 필터 실행을 중단.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal DocumentReaderJob()
        {
            handle = CreateJobObject(IntPtr.Zero, null); if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            var data = new Extended(); data.Basic.Flags = 0x2000 | 0x100; data.ProcessMemory = new UIntPtr(256 * 1024 * 1024);
            if (!SetInformationJobObject(handle, 9, ref data, (uint)Marshal.SizeOf(data))) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new System.ComponentModel.Win32Exception(error); }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Attach
        /// desc         : 이번 요청에서 시작한 도우미만 Job에 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 취소 범위를 자기 문서 추출 프로세스로 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Attach(Process process) { if (!AssignProcessToJobObject(handle, process.Handle)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : Job을 닫아 문서 필터와 하위 프로세스를 정리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : SafeHandle로 중복 취소·완료를 안전하게 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose() { handle.Dispose(); }
    }
}
