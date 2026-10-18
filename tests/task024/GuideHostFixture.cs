using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 안내 소스 독립 검사에 필요한 리소스와 탐색 계약만 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 실제 IDE·CLI·계정·공유 설정 접근 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed partial class AssistantControl : UserControl
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantControl
        /// desc         : 정상적인 별도 스타일을 준비하되 실제 창은 열지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 안내 구조만 메모리에서 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        public AssistantControl()
        {
            foreach (string key in new[] { "PrimaryButton", "GuideActionButton", "InstallActionButton" }) Resources[key] = new Style(typeof(Button));
            Resources["AssistantExpander"] = new Style(typeof(Expander));
            Resources["AssistantBackgroundBrush"] = System.Windows.Media.Brushes.Black;
            Resources["AssistantForegroundBrush"] = System.Windows.Media.Brushes.Gainsboro;
            Resources["AssistantSurfaceBrush"] = System.Windows.Media.Brushes.DimGray;
            Resources["AssistantBorderBrush"] = System.Windows.Media.Brushes.Gray;
            Resources["AssistantMutedForegroundBrush"] = System.Windows.Media.Brushes.Silver;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigateMarkdownLink
        /// desc         : 실제 외부 탐색 없이 연결 계약만 독립 컴파일한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 자동 링크 실행 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void NavigateMarkdownLink(MarkdownLinkEventArgs args) { }
    }
}

namespace Microsoft.VisualStudio.Shell
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ThreadHelper
    /// desc         : 독립 검사에서는 VS 주 스레드와 COM 접근을 제공하지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 제품 SDK 대신 검사 호스트만 컴파일.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class ThreadHelper
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckAccess
        /// desc         : 실제 VS 조회 분기 진입을 막는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 COM 없는 메모리 호스트.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static bool CheckAccess() { return false; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ThrowIfNotOnUIThread
        /// desc         : 잘못된 COM 조회 분기 진입을 명확한 실패로 처리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 실제 IDE 연결 금지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void ThrowIfNotOnUIThread() { throw new InvalidOperationException("No IDE in guide fixture."); }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Package
    /// desc         : 미호출 SDK 서비스 계약만 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 실제 서비스 조회가 있으면 실패.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class Package
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetGlobalService
        /// desc         : 독립 검사에서 실제 IDE 서비스 호출을 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 의존성 격리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static object GetGlobalService(Type type) { throw new InvalidOperationException("No IDE service."); }
    }
}
namespace Microsoft.VisualStudio.Shell.Interop
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SVsUIShell
    /// desc         : 소유 창 조회의 타입 계약만 선언한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 구현 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class SVsUIShell { }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IVsUIShell
    /// desc         : 독립 소스 컴파일에 필요한 HWND 조회 시그니처만 선언한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 호출 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface IVsUIShell
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetDialogOwnerHwnd
        /// desc         : 실제 실행되지 않는 소유 창 계약이다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 GUI 생성 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        int GetDialogOwnerHwnd(out IntPtr handle);
    }
}
namespace Microsoft.VisualStudio.Threading
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GuideTaskExtensions
    /// desc         : 제품의 지연 초점 관찰 소스를 독립 컴파일한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 Dispatcher loop를 시작하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class GuideTaskExtensions
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Forget
        /// desc         : 메모리 검사에서 실제 초점 변경을 실행하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 SDK 대역 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void Forget(this Task task) { }
    }
}
