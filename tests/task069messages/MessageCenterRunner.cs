using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Threading;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : MessageCenterRunner
/// desc         : 정확한 C1 DLL과 dependency map을 새 STA 프로세스에서 로드해 무창 검사한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK069 개인 실행기, 실패/범위·해시를 JSON에 보존.
/// </summary>
/// ---------------------------------------------------------------------
internal static class MessageCenterRunner
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 의존성/dispatcher를 초기화하고 실제 동시경로의 검사 결과를 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 네트워크/로그인/실AI·Window.Show 사용 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        var json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
        var map = json.Deserialize<Dictionary<string, string>>(File.ReadAllText(args[1]));
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) => {
            string name = new AssemblyName(eventArgs.Name).Name;
            string sibling = Path.Combine(Path.GetDirectoryName(args[0]), name + ".dll");
            if (File.Exists(sibling)) return Assembly.LoadFrom(sibling);
            return map.ContainsKey(name) && File.Exists(map[name]) ? Assembly.LoadFrom(map[name]) : null;
        };
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => { File.WriteAllText(args[2] + ".unhandled.txt", Convert.ToString(eventArgs.ExceptionObject)); };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        Assembly.LoadFrom(args[0]);
        return Execute(args, json);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Execute
    /// desc         : 실제 통과/실패와 미검증 확대 범위를 분리해 결과를 저장한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : fake provider의 이전FIFO 결과를 포함하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Execute(string[] args, JavaScriptSerializer json)
    {
        try { MessageCenterChecks.Run(args[3]); } catch (Exception error) { MessageCenterChecks.Errors.Add("Runner/product exception: " + error); }
        var result = new {
            Succeeded = MessageCenterChecks.Errors.Count == 0, Passed = MessageCenterChecks.Checks.Count, Failed = MessageCenterChecks.Errors.Count,
            Checks = MessageCenterChecks.Checks, Errors = MessageCenterChecks.Errors, Observations = MessageCenterChecks.Observations,
            ProductAssemblySha256 = args[4], ActualMessageCenterUiUsed = true, ActualCliEditProviderUsed = true, ActualLocalCliExeUsed = true,
            WindowShown = false, PopupOpened = false, OsInputInjected = false, GuiUsed = false, LiveAiUsed = false, LoginAttempted = false, NetworkRequested = false,
            Limitation = "Actual product message UI/state and readonly Claude/Codex protocol subprocesses; no native UI, actual AI, account or real server delivery.",
            CheckedAt = DateTimeOffset.Now.ToString("o")
        };
        string text = json.Serialize(result); File.WriteAllText(args[2], text, new System.Text.UTF8Encoding(false)); Console.WriteLine(text);
        return result.Succeeded ? 0 : 1;
    }
}
