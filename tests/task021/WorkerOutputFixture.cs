using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : WorkerOutputFixture
/// desc         : 자기 테스트 폴더에서만 두 파이프에 무개행 출력을 보내고 고정 본문을 반환한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 실제 필터·문서·운영 도우미와 분리된 출력 배수 경계 fixture.
/// </summary>
/// ---------------------------------------------------------------------
internal static class WorkerOutputFixture
{
    private const int OutputBytes = 8 * 1024 * 1024;
    private const string Body = "WORKER_NO_NEWLINE_BODY 한글";

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : stdout·stderr에 각각 8MB를 쓴 뒤 args[1]의 테스트 본문과 완료 증거를 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 두 출력 모두 정상 종료해야만 성공 표식을 생성.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        if (args.Length != 2 || !File.Exists(args[0])) return 2;
        Exception outputError = null;
        var stderr = new Thread(() =>
        {
            try { WriteOutput(Console.OpenStandardError(), (byte)'E'); }
            catch (Exception error) { outputError = error; }
        });
        stderr.IsBackground = true;
        stderr.Start();
        WriteOutput(Console.OpenStandardOutput(), (byte)'O');
        stderr.Join();
        if (outputError != null) return 7;
        File.WriteAllText(args[1], Body, new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "worker-completed.txt"),
            new[] { OutputBytes.ToString(), OutputBytes.ToString(), args[0], args[1] }, new UTF8Encoding(false));
        return 0;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WriteOutput
    /// desc         : 작은 고정 버퍼를 재사용하여 줄바꿈 없는 출력만 생성한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : fixture 자체도 8MB 문자열을 만들지 않도록 구성.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void WriteOutput(Stream stream, byte value)
    {
        var buffer = new byte[4096];
        for (int i = 0; i < buffer.Length; i++) buffer[i] = value;
        for (int count = 0; count < OutputBytes; count += buffer.Length) stream.Write(buffer, 0, buffer.Length);
        stream.Flush();
    }
}
