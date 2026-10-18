using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : DocumentWorkerFixture
/// desc         : 문서 도우미의 미등록·초과·무응답을 자기 테스트 복사본에서만 재현한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 제품 필터 등록·실제 문서와 분리된 네이티브 경계 fixture.
/// </summary>
/// ---------------------------------------------------------------------
internal static class DocumentWorkerFixture
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 테스트 환경변수에 따라 고정된 실패 또는 출력만 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : PID·임시 입력 경로로 종료와 정리를 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        string mode = Environment.GetEnvironmentVariable("DOC_READER_FIXTURE");
        if (mode == "unsupported") return 3;
        if (mode == "password") return 4;
        if (mode == "oversize") { File.WriteAllText(args[1], new string('A', 102401), new UTF8Encoding(false)); return 0; }
        if (mode == "hang") { File.WriteAllLines(Environment.GetEnvironmentVariable("DOC_READER_MARKER"), new[] { Process.GetCurrentProcess().Id.ToString(), args[0] }); Thread.Sleep(30000); return 0; }
        return 2;
    }
}
