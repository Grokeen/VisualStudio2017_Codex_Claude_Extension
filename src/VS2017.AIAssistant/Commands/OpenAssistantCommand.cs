using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VS2017.AIAssistant.ToolWindows;

namespace VS2017.AIAssistant.Commands
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : OpenAssistantCommand
    /// desc         : 도구 메뉴에서 하나의 AI Assistant 도구 창을 열거나 다시 활성화한다.
    /// author       : CodexCode
    /// create date  : 2026-10-17
    /// update date  : 2026-10-17 CodexCode : 명령 등록, 창 재사용 및 오류 안내 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class OpenAssistantCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("c9201782-e997-4591-a509-7dabd90e97ce");
        private readonly AsyncPackage package;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenAssistantCommand
        /// desc         : 패키지와 도구 창 명령의 수명을 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-17
        /// update date  : 2026-10-17 CodexCode : 패키지 참조 보관.
        /// </summary>
        /// ---------------------------------------------------------------------
        private OpenAssistantCommand(AsyncPackage package)
        {
            this.package = package;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Register
        /// desc         : VSCT와 같은 GUID 및 ID를 사용하여 메뉴 콜백을 등록한다.
        /// author       : CodexCode
        /// create date  : 2026-10-17
        /// update date  : 2026-10-17 CodexCode : UI 스레드에서 메뉴 등록.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static void Register(AsyncPackage package, OleMenuCommandService commandService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (commandService == null) throw new ArgumentNullException(nameof(commandService));

            var command = new OpenAssistantCommand(package);
            commandService.AddCommand(new MenuCommand(command.Execute, new CommandID(CommandSet, CommandId)));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Execute
        /// desc         : VS2017 초기 버전의 동기 창 API로 가벼운 WPF 창을 생성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-17
        /// update date  : 2026-10-17 CodexCode : 프레임 확인 및 생성 실패 안내 추가.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                // ID 0을 재사용하여 메뉴를 여러 번 눌러도 창이 중복 생성되지 않는다.
                ToolWindowPane window = package.FindToolWindow(typeof(AssistantToolWindow), 0, true);
                var frame = window == null ? null : window.Frame as IVsWindowFrame;
                if (frame == null)
                {
                    throw new InvalidOperationException("AI Assistant 도구 창을 생성하지 못했습니다.");
                }

                ErrorHandler.ThrowOnFailure(frame.Show());
            }
            catch (Exception exception)
            {
                ActivityLog.LogError(nameof(OpenAssistantCommand), exception.ToString());
                VsShellUtilities.ShowMessageBox(package,
                    "AI Assistant 창을 열 수 없습니다. Visual Studio ActivityLog.xml을 확인하세요.",
                    "VS2017 AI Assistant", OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }
}
