using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using VS2017.AIAssistant.Views;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Services;

namespace VS2017.AIAssistant.ToolWindows
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantToolWindow
    /// desc         : Visual Studio 도킹과 창 레이아웃 복원을 지원하는 WPF 호스트.
    /// author       : CodexCode
    /// create date  : 2026-10-17
    /// update date  : 2026-10-18 CodexCode : 실제 문서·AI 수정 서비스와 종료 처리를 연결.
    /// update date  : 2026-10-18 CodexOrig : Chat 제목 및 마지막 소스 추적 서비스 해제.
    /// </summary>
    /// ---------------------------------------------------------------------
    [Guid("c0482cd3-9556-4081-9c50-79cf84667042")]
    public sealed class AssistantToolWindow : ToolWindowPane
    {
        private VisualStudioDocumentService documentService;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantToolWindow
        /// desc         : 제목과 WPF 콘텐츠를 설정하며 외부 프로세스는 실행하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-17
        /// update date  : 2026-10-17 CodexCode : 최소 화면 연결.
        /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
        /// </summary>
        /// ---------------------------------------------------------------------
        public AssistantToolWindow() : base(null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Caption = "Chat";
            Content = new AssistantControl();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OnToolWindowCreated
        /// desc         : Package가 연결된 뒤 화면에 IDE 문서와 CLI 수정 서비스를 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 생성 시 서비스 연결.
        /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
        /// </summary>
        /// ---------------------------------------------------------------------
        public override void OnToolWindowCreated()
        {
            base.OnToolWindowCreated();
            ThreadHelper.ThrowIfNotOnUIThread();
            documentService = new VisualStudioDocumentService((System.IServiceProvider)Package);
            ((AssistantControl)Content).Initialize(documentService, new CliEditProvider());
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 도구 창이 해제될 때 CLI 요청을 취소하고 기본 리소스를 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : IDE 종료 시 AI 프로세스 정리 연결.
        /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var view = Content as AssistantControl;
            if (disposing && view != null) view.Dispose();
            if (disposing && documentService != null) documentService.Dispose();
            base.Dispose(disposing);
        }
    }
}
