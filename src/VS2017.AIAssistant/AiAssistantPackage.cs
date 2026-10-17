using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell;
using VS2017.AIAssistant.Commands;
using VS2017.AIAssistant.ToolWindows;

namespace VS2017.AIAssistant
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AiAssistantPackage
    /// desc         : VS2017용 AI 도구 창과 도구 메뉴를 등록하는 비동기 패키지.
    /// author       : CodexCode
    /// create date  : 2026-10-17
    /// update date  : 2026-10-17 CodexCode : Phase 1 VSIX 패키지 등록 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(AssistantToolWindow))]
    public sealed class AiAssistantPackage : AsyncPackage
    {
        public const string PackageGuidString = "f31272f1-3188-4cca-b6e1-37c118cf7824";

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeAsync
        /// desc         : 메뉴 서비스를 비동기로 구한 후 UI 스레드에서 명령을 등록한다.
        /// author       : CodexCode
        /// create date  : 2026-10-17
        /// update date  : 2026-10-17 CodexCode : IDE 시작 시 강제 자동 로드 없이 초기화.
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            var commandService = await GetServiceAsync(typeof(System.ComponentModel.Design.IMenuCommandService))
                as OleMenuCommandService;
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (commandService == null)
            {
                throw new InvalidOperationException("Visual Studio 메뉴 서비스를 사용할 수 없습니다.");
            }

            OpenAssistantCommand.Register(this, commandService);
        }
    }
}
