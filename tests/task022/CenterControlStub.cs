using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 창을 표시하지 않는 메모리 배치 검사에 필요한 Fan 필드만 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 계정·설정·IDE 없는 독립 fixture.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl : UserControl
    {
        private bool busy, disposed;
        private readonly Canvas FanPanel = new Canvas { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        private readonly Button HelpButton = new Button { Content = "? 사용 안내" }, SettingsButton = new Button { Content = "⚙ 설정" };
        private readonly ToggleButton OptionsToggle = new ToggleButton { Width = 32, Height = 30.62, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(29, 12, 29, 0) };

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantControl
        /// desc         : 실제 원점 여백과 같은 배치를 만들되 Window·Dispatcher loop는 열지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 순수 Measure/Arrange 검사 준비.
        /// </summary>
        /// ---------------------------------------------------------------------
        public AssistantControl()
        {
            busy = disposed = false;
            FontFamily = new FontFamily("Malgun Gothic"); FontSize = 14;
            Resources["PrimaryButton"] = new Style(typeof(Button));
            var layout = new Grid(); layout.Children.Add(OptionsToggle);
            FanPanel.Children.Add(HelpButton); FanPanel.Children.Add(SettingsButton); layout.Children.Add(FanPanel); Content = layout;
        }
    }
}

// IDE/COM 조회 분기는 실행되지 않도록 하며 제품 SDK 소스·공유 빌드를 사용하지 않는다.
namespace Microsoft.VisualStudio.Shell
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ThreadHelper
    /// desc         : 독립 검사에서 VS 주 스레드가 없음을 반환하는 SDK 대역.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 COM 접근 차단.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class ThreadHelper
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckAccess
        /// desc         : 독립 호스트에서는 실제 VS 주 스레드를 제공하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 조회 분기 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static bool CheckAccess() { return false; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ThrowIfNotOnUIThread
        /// desc         : 예기치 않은 COM 분기 진입은 즉시 검사 실패로 처리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 호출 금지 근거.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void ThrowIfNotOnUIThread() { throw new InvalidOperationException("No IDE in memory fixture."); }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Package
    /// desc         : 실 IDE 서비스 조회를 금지하는 SDK 대역.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 외부 접근 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class Package
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetGlobalService
        /// desc         : 테스트에서 실제 IDE 서비스를 호출하면 실패시킨다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 서비스 격리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static object GetGlobalService(Type type) { throw new InvalidOperationException("No IDE service in fixture."); }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : VsBrushes
    /// desc         : 제품 리소스 키만 제공하는 메모리 검사 대역.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 SDK 패키지 로드 배제.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class VsBrushes { public static readonly object ToolWindowBackgroundKey = new object(), ToolWindowTextKey = new object(); }
}
namespace Microsoft.VisualStudio.Shell.Interop
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SVsUIShell
    /// desc         : 컴파일만 위한 COM 서비스 타입 대역.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 실제 조회 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class SVsUIShell { }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IVsUIShell
    /// desc         : 독립 컴파일에 필요한 소유 창 메서드 계약만 선언한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 실제 구현 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface IVsUIShell
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetDialogOwnerHwnd
        /// desc         : 소스 컴파일을 위한 미호출 계약.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 HWND 생성 금지.
        /// </summary>
        /// ---------------------------------------------------------------------
        int GetDialogOwnerHwnd(out IntPtr handle);
    }
}
namespace Microsoft.VisualStudio.Threading
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CenterTaskExtensions
    /// desc         : 제품 async 관찰 코드를 독립 컴파일하는 최소 계약 대역.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 실제 focus 예약은 검사에서 실행하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class CenterTaskExtensions
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Forget
        /// desc         : focus 작업을 실행하지 않는 검사 호스트의 컴파일 계약.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 dispatcher loop 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void Forget(this Task task) { }
    }
}
