using System;
using System.Text;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : TfsProtocolFixture
/// desc         : TFS 서버나 제품 DLL 없이 실제 네이티브 인수만 출력하는 독립 검사 프로세스다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// </summary>
/// ---------------------------------------------------------------------
internal static class TfsProtocolFixture
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length == 0 || (args[0] != "get" && args[0] != "checkout" && args[0] != "checkin")) return 2;
        foreach (string value in args) Console.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
        return 0;
    }
}
