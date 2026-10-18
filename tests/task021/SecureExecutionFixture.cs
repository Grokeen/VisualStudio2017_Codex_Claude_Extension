using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : SecureExecutionFixture
/// desc         : 네트워크·계정·저장소 없이 받은 인수와 선택 환경만 출력하는 독립 합성 exe.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : TASK-021 실제 프로세스 경계와 취소·timeout 검증 제공.
/// </summary>
/// ---------------------------------------------------------------------
internal static class SecureExecutionFixture
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 합성 marker에 PID를 기록한 뒤 값을 base64로 출력하며 요청 시 제한 대기한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-021 운영 명령 실행과 외부 접속 없는 fixture.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        string marker = Environment.GetEnvironmentVariable("SECURE_FIXTURE_MARKER");
        if (!String.IsNullOrEmpty(marker)) File.WriteAllText(marker, Process.GetCurrentProcess().Id.ToString());
        // 합성 프로세스가 Job.Attach 이전에 먼저 끝나지 않도록 짧게 유지한다.
        Thread.Sleep(80);
        Print("CWD:", Environment.CurrentDirectory);
        foreach (string arg in args) Print("ARG:", arg);
        foreach (string name in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_COMMON_DIR", "GIT_NAMESPACE", "GIT_TERMINAL_PROMPT", "GCM_INTERACTIVE" })
            Print("ENV:" + name + ":", Environment.GetEnvironmentVariable(name) ?? "<null>");
        if (Array.IndexOf(args, "--fixture-wait") >= 0) Thread.Sleep(30000);
        return 0;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Print
    /// desc         : 빈 값·개행·한글·따옴표를 손실 없이 한 줄에 출력한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-021 실제 수신 argv와 환경 값의 비교 지원.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Print(string prefix, string value) { Console.WriteLine(prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(value))); }
}
