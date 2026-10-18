using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ViewPolicyChecks
/// desc         : UI를 만들지 않고 실제 viewport 소스의 수학·XAML 구조 경계를 검사한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : source-only 결과를 실제 제품 UI 결과와 명확히 구별.
/// </summary>
/// ---------------------------------------------------------------------
public static class ViewPolicyChecks
{
    public static readonly List<string> Checks = new List<string>();

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunMath
    /// desc         : 유한 높이·무한 높이·초기 host·바닥 판정의 실제 제품 helper를 실행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 렌더링 없이 경계값만 검증하며 제품 DLL에서도 같은 검사 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void RunMath()
    {
        Check(Length(480, 300, 600) == 480 && Length(0, 300, 600) == 0, "Finite host constraints and zero available space remain authoritative");
        Check(Length(double.PositiveInfinity, 420, 600) == 420, "Infinite measurement uses the real host viewport");
        Check(Length(double.PositiveInfinity, 0, 600) == 600, "The initial unmeasured host uses a finite fallback");
        Check(Length(double.NaN, 350, 600) == 350, "A NaN measurement cannot escape the finite host boundary");
        Check(Length(double.PositiveInfinity, double.NaN, double.PositiveInfinity) == 300, "Invalid host and fallback values still produce a finite limit");
        Check(AtEnd(0, 0) && AtEnd(248.5, 250), "Empty and pixel-rounded last positions count as the bottom");
        Check(!AtEnd(100, 250), "Reading earlier content is distinct from being at the bottom");
        Check(!AtEnd(double.NaN, 250) && !AtEnd(double.PositiveInfinity, 250), "Invalid offsets never enable automatic following");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunSchema
    /// desc         : 대화 footer·고정 입력·dark template·진행 카드 binding의 실제 XAML 구조를 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : source-only 검사를 제품 렌더링 성공으로 보고하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void RunSchema(string xamlPath, string themePath)
    {
        XDocument xaml = XDocument.Load(xamlPath), theme = XDocument.Load(themePath);
        XNamespace names = "http://schemas.microsoft.com/winfx/2006/xaml";
        Func<string, XElement> named = value => xaml.Descendants().Single(node => (string)node.Attribute(names + "Name") == value);
        XElement scroll = named("ConversationScroll"), footer = named("ThinkingIndicator"), above = named("ThinkingAboveIndicator"), composer = named("ComposerSurface");
        Check(footer.Ancestors().Contains(scroll) && !above.Ancestors().Contains(scroll) && named("ConversationStartedLabel").Ancestors().Contains(scroll), "Footer thinking and conversation metadata belong to the conversation scroll while the alternate label stays outside");
        Check(!composer.Ancestors().Contains(scroll) && (string)composer.Attribute("Grid.Row") == "5" && (string)xaml.Root.Attribute("MinHeight") == "0", "The composer occupies its own fixed row and the control cannot force a 300px outer extent");
        Check(theme.Descendants().Any(node => (string)node.Attribute(names + "Key") == "DarkHistoryListBox") && theme.Descendants().Any(node => (string)node.Attribute(names + "Key") == "DarkHistoryViewport") && (string)named("ConversationList").Attribute("Style") == "{StaticResource DarkHistoryListBox}", "History uses an explicit dark ListBox and viewport template rather than the host theme");
        XElement presenter = named("ProgressMessage");
        Check(presenter.Name.LocalName == "ProgressNoticePresenter" && ((string)presenter.Attribute("Notice")).Contains("ProgressNotice") && xaml.Descendants().Any(node => node.Name.LocalName == "DataTrigger" && ((string)node.Attribute("Binding") ?? "").Contains("IsProgress")), "Registered progress uses its dedicated presenter while ordinary message markup remains available");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 독립 컴파일 실행기의 source-only 결과 JSON을 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : WPF 객체 생성·창·실계정·외부 프로세스 없이 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        string error = null;
        try { RunMath(); RunSchema(args[0], args[1]); }
        catch (Exception failure) { error = failure.ToString(); }
        File.WriteAllText(args[2], new JavaScriptSerializer().Serialize(new { Succeeded = error == null, Passed = Checks.Count, Failed = error == null ? 0 : 1, SourceOnly = true, Checks = Checks, Errors = error == null ? new string[0] : new[] { error } }), new UTF8Encoding(false));
        Console.WriteLine(error ?? "PASS " + Checks.Count + " source-only viewport checks");
        return error == null ? 0 : 1;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Length / AtEnd
    /// desc         : 실제 private helper를 reflection으로 호출해 테스트 전용 구현을 만들지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 소스·고정 DLL 양쪽의 같은 메서드를 실행.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static double Length(double available, double host, double fallback) { return (double)typeof(AssistantControl).GetMethod("ConstrainViewportLength", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { available, host, fallback }); }
    private static bool AtEnd(double offset, double extent) { return (bool)typeof(AssistantControl).GetMethod("IsConversationAtEnd", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { offset, extent }); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 최초 실패를 보존하고 통과한 검사 이름을 누적한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 정적 검사 수를 실제 UI 검사 수와 합치지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); Checks.Add(name); }
}
