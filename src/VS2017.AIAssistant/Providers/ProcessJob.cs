using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VS2017.AIAssistant.Providers
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProcessJob
    /// desc         : CLI 프로세스와 하위 프로세스를 요청 수명에 묶는 Windows Job Object.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 취소·타임아웃·IDE 종료 시 프로세스 정리 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class ProcessJob : IDisposable
    {
        private readonly SafeFileHandle handle;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProcessJob
        /// desc         : 핸들을 닫으면 연결된 프로세스가 종료되도록 Job 제한을 설정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Job 생성 실패 시 실행 중단.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ProcessJob()
        {
            handle = CreateJobObject(IntPtr.Zero, null);
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            var information = new ExtendedLimitInformation();
            information.BasicLimitInformation.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            if (!SetInformationJobObject(handle, 9, ref information, (uint)Marshal.SizeOf(information)))
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Attach
        /// desc         : 실행 직후 CLI를 Job에 연결하며 실패하면 호출자가 프로세스를 종료한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Windows 중첩 Job 연결 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Attach(Process process)
        {
            if (!AssignProcessToJobObject(handle, process.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : Job 핸들을 해제하여 남은 CLI 하위 프로세스도 종료한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 중복 해제에 안전한 SafeHandle 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose() { handle.Dispose(); }

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimitInformation
        {
            public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimitInformation
        {
            public BasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref ExtendedLimitInformation information, uint length);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    }
}
