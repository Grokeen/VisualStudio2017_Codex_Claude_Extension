using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : OfflineNetworkIsolation
/// desc         : 임시 검사 프로세스를 네트워크 capability 없는 AppContainer에서 실행한다.
/// author       : CodexOrig
/// create date  : 2026-10-19
/// update date  : 2026-10-19 CodexOrig : TASK-041 기존 사용자 연결과 설정을 보존하는 격리 검사.
/// </summary>
/// ---------------------------------------------------------------------
public static class OfflineNetworkIsolation
{
    [StructLayout(LayoutKind.Sequential)] struct SecurityCapabilities { public IntPtr Sid,Capabilities; public uint Count,Reserved; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct StartupInfo {
        public int Size; public string Reserved,Desktop,Title; public uint X,Y,XSize,YSize,XCount,YCount,Fill,Flags;public short Show,Reserved2;public IntPtr ReservedPtr,Input,Output,Error;
    }
    [StructLayout(LayoutKind.Sequential)] struct StartupInfoEx { public StartupInfo Info; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { public IntPtr Process,Thread;public uint ProcessId,ThreadId; }
    [StructLayout(LayoutKind.Sequential)] struct BasicLimits { public long ProcessTime,JobTime;public uint Flags;public UIntPtr MinWorking,MaxWorking;public uint ActiveProcesses;public UIntPtr Affinity;public uint Priority,Scheduling; }
    [StructLayout(LayoutKind.Sequential)] struct IoCounters { public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] struct JobLimits { public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CreateAppContainerProfile
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("userenv.dll",CharSet=CharSet.Unicode)] static extern int CreateAppContainerProfile(string name,string display,string description,IntPtr capabilities,uint count,out IntPtr sid);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DeleteAppContainerProfile
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("userenv.dll",CharSet=CharSet.Unicode)] static extern int DeleteAppContainerProfile(string name);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GetAppContainerFolderPath
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("userenv.dll",CharSet=CharSet.Unicode)] static extern int GetAppContainerFolderPath(string sid,out IntPtr path);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FreeSid
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("advapi32.dll")] static extern IntPtr FreeSid(IntPtr sid);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : InitializeProcThreadAttributeList
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool InitializeProcThreadAttributeList(IntPtr list,int count,int flags,ref IntPtr size);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : UpdateProcThreadAttribute
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool UpdateProcThreadAttribute(IntPtr list,uint flags,IntPtr key,IntPtr value,IntPtr size,IntPtr previous,IntPtr result);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DeleteProcThreadAttributeList
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(IntPtr list);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CreateProcess
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateProcess(string application,StringBuilder command,IntPtr pa,IntPtr ta,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfoEx startup,out ProcessInfo process);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WaitForSingleObject
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll",SetLastError=true)] static extern uint WaitForSingleObject(IntPtr handle,uint timeout);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GetExitCodeProcess
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetExitCodeProcess(IntPtr process,out uint exit);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TerminateProcess
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool TerminateProcess(IntPtr process,uint exit);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CloseHandle
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CreateJobObject
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateJobObject(IntPtr attributes,string name);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SetInformationJobObject
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(IntPtr job,int info,ref JobLimits limits,uint length);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssignProcessToJobObject
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ResumeThread
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll",SetLastError=true)] static extern uint ResumeThread(IntPtr thread);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : OpenProcessToken
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GetTokenInformation
    /// desc         : Windows 공식 API를 검사 전용 프로세스·프로필·토큰 수명 관리에 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-041 기존 시스템 설정을 변경하지 않는 검증 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool GetTokenInformation(IntPtr token,int kind,IntPtr buffer,int length,out int needed);

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TokenState
    /// desc         : 현재 프로세스의 AppContainer 여부와 capability 개수를 실제 토큰에서 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : 네트워크 차단 관측과 capability 0 전제를 연결한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static int[] TokenState()
    {
        IntPtr token=IntPtr.Zero,buffer=IntPtr.Zero;
        try {
            if(!OpenProcessToken(new IntPtr(-1),8,out token))throw new Win32Exception();
            buffer=Marshal.AllocHGlobal(4);int needed;
            if(!GetTokenInformation(token,29,buffer,4,out needed))throw new Win32Exception();int container=Marshal.ReadInt32(buffer);
            Marshal.FreeHGlobal(buffer);buffer=IntPtr.Zero;
            GetTokenInformation(token,30,IntPtr.Zero,0,out needed);buffer=Marshal.AllocHGlobal(needed);
            if(!GetTokenInformation(token,30,buffer,needed,out needed))throw new Win32Exception();return new int[]{container,Marshal.ReadInt32(buffer)};
        } finally {if(buffer!=IntPtr.Zero)Marshal.FreeHGlobal(buffer);if(token!=IntPtr.Zero)CloseHandle(token);}
    }

    public static string LastProfile;
    public static int LastCleanup;
    public static bool LastAclRestored;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 새 작업 폴더에만 임시 SID 접근권을 부여하고 실행 완료 후 프로필을 정리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : 네트워크 capability 0, 비밀 없는 임시 환경 변수, 시간 제한.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static int Run(string root,string executable,string arguments,int timeout)
    {
        root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        DirectoryInfo dedicated=new DirectoryInfo(root);Guid run;
        if(!dedicated.Exists || dedicated.Parent==null || dedicated.Parent.Name!="task041" || !dedicated.Name.StartsWith("run-") || !Guid.TryParseExact(dedicated.Name.Substring(4),"N",out run) || File.ReadAllText(Path.Combine(root,"TASK041.marker"))!=dedicated.Name || !Path.IsPathRooted(executable) || !File.Exists(executable))throw new ArgumentException("New marked task041/run-GUID folder and absolute executable required.");
        for(DirectoryInfo parent=dedicated;parent!=null;parent=parent.Parent)if((parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new ArgumentException("Linked test directories are not permitted.");
        string name="CodexOrig.Task041."+Guid.NewGuid().ToString("N");
        LastProfile=name;LastCleanup=int.MinValue;LastAclRestored=false;
        IntPtr sid=IntPtr.Zero,list=IntPtr.Zero,caps=IntPtr.Zero,environment=IntPtr.Zero,job=IntPtr.Zero,profilePath=IntPtr.Zero;ProcessInfo pi=new ProcessInfo();bool initialized=false,created=false,finished=false;DirectorySecurity originalAcl=null;
        try {
            int hr=CreateAppContainerProfile(name,name,"Temporary network-disabled verification",IntPtr.Zero,0,out sid);
            if(hr!=0)Marshal.ThrowExceptionForHR(hr);created=true;
            hr=GetAppContainerFolderPath(new SecurityIdentifier(sid).Value,out profilePath);if(hr!=0)Marshal.ThrowExceptionForHR(hr);
            string profile=Marshal.PtrToStringUni(profilePath);
            File.WriteAllText(Path.Combine(root,"created-profile.txt"),profile);
            DirectoryInfo directory=new DirectoryInfo(root);originalAcl=directory.GetAccessControl();DirectorySecurity acl=directory.GetAccessControl();
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid),FileSystemRights.Modify,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));directory.SetAccessControl(acl);
            IntPtr size=IntPtr.Zero;InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref size);
            list=Marshal.AllocHGlobal(size);
            if(!InitializeProcThreadAttributeList(list,1,0,ref size))throw new Win32Exception();initialized=true;
            SecurityCapabilities security=new SecurityCapabilities { Sid=sid,Capabilities=IntPtr.Zero,Count=0,Reserved=0 };
            caps=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SecurityCapabilities)));Marshal.StructureToPtr(security,caps,false);
            if(!UpdateProcThreadAttribute(list,0,new IntPtr(0x20009),caps,new IntPtr(Marshal.SizeOf(typeof(SecurityCapabilities))),IntPtr.Zero,IntPtr.Zero))throw new Win32Exception();
            SortedDictionary<string,string> variables=new SortedDictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(string key in new string[]{"SystemRoot","WINDIR","ComSpec","ProgramFiles","ProgramFiles(x86)","ProgramW6432","ProgramData","PUBLIC","PROCESSOR_ARCHITECTURE","PROCESSOR_IDENTIFIER","NUMBER_OF_PROCESSORS","OS","PATHEXT"}){string value=Environment.GetEnvironmentVariable(key);if(value!=null)variables[key]=value;}
            foreach(string key in new string[]{"USERPROFILE","HOME","APPDATA","LOCALAPPDATA","TEMP","TMP","CODEX_HOME","CLAUDE_CONFIG_DIR"})variables[key]=root+"\\profile";
            Directory.CreateDirectory(root+"\\profile");
            variables["PATH"]=Environment.GetFolderPath(Environment.SpecialFolder.System)+";"+Environment.GetFolderPath(Environment.SpecialFolder.System)+"\\WindowsPowerShell\\v1.0";
            variables["DISABLE_AUTOUPDATER"]="1";
            StringBuilder block=new StringBuilder();foreach(KeyValuePair<string,string> value in variables)block.Append(value.Key).Append('=').Append(value.Value).Append('\0');block.Append('\0');
            environment=Marshal.StringToHGlobalUni(block.ToString());
            StartupInfoEx startup=new StartupInfoEx();startup.Info.Size=Marshal.SizeOf(typeof(StartupInfoEx));startup.Attributes=list;
            StringBuilder command=new StringBuilder("\""+executable+"\" "+arguments);
            job=CreateJobObject(IntPtr.Zero,null);if(job==IntPtr.Zero)throw new Win32Exception();
            JobLimits limits=new JobLimits();limits.Basic.Flags=0x2000;
            if(!SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf(typeof(JobLimits))))throw new Win32Exception();
            if(!CreateProcess(executable,command,IntPtr.Zero,IntPtr.Zero,false,0x08080404,environment,root,ref startup,out pi))throw new Win32Exception();
            if(!AssignProcessToJobObject(job,pi.Process))throw new Win32Exception();
            if(ResumeThread(pi.Thread)==0xffffffff)throw new Win32Exception();
            uint wait=WaitForSingleObject(pi.Process,(uint)timeout);
            if(wait==258){TerminateProcess(pi.Process,1460);WaitForSingleObject(pi.Process,5000);throw new TimeoutException("Owned AppContainer process timed out.");}
            if(wait!=0)throw new Win32Exception();
            uint exit;if(!GetExitCodeProcess(pi.Process,out exit))throw new Win32Exception();finished=true;return unchecked((int)exit);
        } finally {
            if(pi.Process!=IntPtr.Zero && !finished){TerminateProcess(pi.Process,1460);WaitForSingleObject(pi.Process,5000);}
            if(job!=IntPtr.Zero)CloseHandle(job);
            if(pi.Thread!=IntPtr.Zero)CloseHandle(pi.Thread);if(pi.Process!=IntPtr.Zero)CloseHandle(pi.Process);
            if(initialized)DeleteProcThreadAttributeList(list);if(list!=IntPtr.Zero)Marshal.FreeHGlobal(list);if(caps!=IntPtr.Zero)Marshal.FreeHGlobal(caps);if(environment!=IntPtr.Zero)Marshal.FreeHGlobal(environment);if(sid!=IntPtr.Zero)FreeSid(sid);
            if(profilePath!=IntPtr.Zero)Marshal.FreeCoTaskMem(profilePath);
            try {
                if(originalAcl!=null) {
                    // 새 descriptor의 Access 구역을 명시적으로 변경해 복원 쓰기가 생략되지 않게 한다.
                    DirectorySecurity restored=new DirectorySecurity();
                    restored.SetSecurityDescriptorBinaryForm(originalAcl.GetSecurityDescriptorBinaryForm(),AccessControlSections.Access);
                    DirectoryInfo directory=new DirectoryInfo(root);directory.SetAccessControl(restored);
                    LastAclRestored=directory.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access)==originalAcl.GetSecurityDescriptorSddlForm(AccessControlSections.Access);
                    if(!LastAclRestored)throw new InvalidOperationException("Temporary test folder ACL restoration differs.");
                }
            }finally{if(created)LastCleanup=DeleteAppContainerProfile(name);}
        }
    }
}
