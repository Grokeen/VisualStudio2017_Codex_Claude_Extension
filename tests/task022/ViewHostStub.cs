using System;
using System.Windows.Controls;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 실제 viewport·thinking 소스의 독립 컴파일에 필요한 필드만 선언하는 비실행 호스트다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 소스 모드에서는 인스턴스를 생성하지 않고 순수 수학만 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl : UserControl, IDisposable
    {
        private bool disposed;
        private readonly TextBlock ThinkingIndicator = new TextBlock();
        private readonly TextBlock ThinkingAboveIndicator = new TextBlock();
        private readonly ScrollViewer ConversationScroll = new ScrollViewer();
        private readonly TextBox Instruction = new TextBox();
        private readonly ListBox AttachmentList = new ListBox();

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 제품 수명 메서드를 참조해 실제 소스의 컴파일 계약을 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 소스 전용 수학 검사에서는 이 메서드를 실행하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose() { disposed = true; DisposeConversationViewport(); }
    }
}
