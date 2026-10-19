using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;

// SOURCE_ONLY 전용: 아래 host/DTO/Chat 타입은 실제 제품 통합으로 집계하지 않는다.
namespace VS2017.AIAssistant.Chat
{
    /// <summary>
    /// name         : ChatMessage
    /// desc         : source-only helper 검사를 위한 최소 메시지 계약 fixture.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 제품 DLL 모드에서는 포함하지 않음.
    /// </summary>
    public sealed class ChatMessage { public string Role { get; set; } public string Text { get; set; } }
    /// <summary>
    /// name         : ChatSession
    /// desc         : source-only 소유 대화 계약 fixture.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 Markdown 저장·제품 UI를 대체하지 않는 순수 host.
    /// </summary>
    public sealed class ChatSession { public ObservableCollection<ChatMessage> Messages { get; private set; } = new ObservableCollection<ChatMessage>(); }
}
namespace VS2017.AIAssistant.Providers
{
    /// <summary>
    /// name         : AgentProgress
    /// desc         : source-only 공개 DTO 계약 fixture.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 DLL 모드에서는 제품 DTO를 사용.
    /// </summary>
    public sealed class AgentProgress { public string Text { get; set; } public string Kind { get; set; } public string WorkId { get; set; } public string State { get; set; } public string Detail { get; set; } public int? ExitCode { get; set; } }
}
namespace VS2017.AIAssistant.Views
{
    // ProgressNotice의 화면 없는 source-only 컴파일에 필요한 외부 UI 서비스 경계.
    public static class HoverScrollPolicy { public static void HandleWheel(System.Windows.FrameworkElement owner, System.Windows.Input.MouseWheelEventArgs args, bool contained) { } }
    internal static class PopupPalette { internal static void Attach(System.Windows.FrameworkElement target, System.Windows.FrameworkElement anchor) { } }
    /// <summary>
    /// name         : AssistantControl
    /// desc         : source-only helper 소유권을 시험하는 화면 없는 최소 host.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 제품 private field 조작 없이 명시적 fixture 전환 제공.
    /// </summary>
    public partial class AssistantControl : UserControl, IDisposable
    {
        private bool disposed;
        private Chat.ChatSession chat = new Chat.ChatSession();
        private Chat.ChatSession ActiveChat { get { return chat; } }
        public ItemsControl ConversationMessages { get; private set; }
        private bool GetCompletionNotificationsEnabled() { return false; }
        /// <summary>
        /// name         : AssistantControl
        /// desc         : 테스트 컬렉션을 바인딩하고 표시하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 순수 source fixture.
        /// </summary>
        public AssistantControl() { System.Windows.NameScope.SetNameScope(this,new System.Windows.NameScope()); ConversationMessages = new ItemsControl { ItemsSource = chat.Messages }; RegisterName("ConversationMessages", ConversationMessages); Content = ConversationMessages; }
        /// <summary>
        /// name         : SwitchFixtureConversation
        /// desc         : 원래 소유 대화를 남기고 source host만 전환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 제품 UI 전환 검증으로 집계하지 않음.
        /// </summary>
        public void SwitchFixtureConversation() { chat = new Chat.ChatSession(); ConversationMessages.ItemsSource = chat.Messages; }
        /// <summary>
        /// name         : Dispose
        /// desc         : fixture 수명을 종료한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 열린 창 없음.
        /// </summary>
        public void Dispose() { disposed = true; CloseProgressNotices(); }
    }
}
