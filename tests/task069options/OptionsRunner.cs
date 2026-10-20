using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Threading;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : OptionsRunner
/// desc         : 고정 실제 DLL과 개인 dependency map을 로드해 옵션 검사를 무창 실행한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK069 실제/미검증 범위를 결과 JSON에 분리한다.
/// </summary>
/// ---------------------------------------------------------------------
internal static class OptionsRunner
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 정확한 후보 DLL을 STA에 로드하고 native fixture 검사 결과를 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : API 키·계정 파일·실 AI 요청을 읽거나 실행하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        var json = new JavaScriptSerializer { MaxJsonLength = 16000000 };
        var map = json.Deserialize<Dictionary<string, string>>(File.ReadAllText(args[1]));
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) => {
            string name = new AssemblyName(eventArgs.Name).Name;
            return map.ContainsKey(name) && File.Exists(map[name]) ? Assembly.LoadFrom(map[name]) : null;
        };
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => File.WriteAllText(args[2] + ".unhandled.txt", Convert.ToString(eventArgs.ExceptionObject));
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        Assembly.LoadFrom(args[0]);
        return Execute(args, json);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Execute
    /// desc         : 실제 관측·실패 원문과 성공으로 확대할 수 없는 범위를 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 이미지69 모델 권한/서버 실제 추론은 미검증이다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Execute(string[] args, JavaScriptSerializer json)
    {
        try { OptionsChecks.Run(args[3], args[4]); } catch (Exception error) { OptionsChecks.Errors.Add("Runner/product exception: " + error); }
        var result = new {
            Passed = OptionsChecks.Passed.Count, Failed = OptionsChecks.Errors.Count,
            Checks = OptionsChecks.Passed, Errors = OptionsChecks.Errors, Observations = OptionsChecks.Observations,
            ProductAssemblySha256 = args[5], ActualProductUiEventsUsed = OptionsChecks.Passed.Count > 0, ActualRequestCaptureUsed = OptionsChecks.CaptureCount > 0,
            ActualProductRuntimeRunnerUsed = OptionsChecks.NativeCalls > 0, ActualNativeWireFixtureUsed = OptionsChecks.NativeWireRows > 0,
            CaptureCount = OptionsChecks.CaptureCount, NativeCalls = OptionsChecks.NativeCalls, NativeWireRows = OptionsChecks.NativeWireRows,
            PopupOpened = false, WindowShown = false, OsInputInjected = false, LoginAttempted = false, LiveAiUsed = false, NetworkRequested = false,
            Limitation = "Control API events and captured queued context; the captured context is passed to actual product native runners. Scheduler ordering, dropdown popup appearance, physical pointer, IDE interaction, remote model access and billed inference are not exercised.",
            CheckedAt = DateTimeOffset.Now.ToString("o")
        };
        string text = json.Serialize(result); File.WriteAllText(args[2], text, new System.Text.UTF8Encoding(false)); Console.WriteLine(text);
        return result.Failed == 0 ? 0 : 1;
    }
}
