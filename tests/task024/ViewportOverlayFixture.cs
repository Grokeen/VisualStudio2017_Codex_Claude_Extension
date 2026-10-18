using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// SOURCE_ONLY: 실제 제품 생성자·저장·계정·문서 기능을 대신하지 않는 최소 WPF host다.
namespace VS2017.AIAssistant.Chat
{
    /// <summary>
    /// name         : ChatMessage
    /// desc         : 공개 진행 변환 source 검사용 DTO.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : source 모드 전용.
    /// </summary>
    public sealed class ChatMessage { public string Role { get; set; } public string Text { get; set; } }
}
namespace VS2017.AIAssistant.Views
{
    /// <summary>
    /// name         : ChangeCardFile
    /// desc         : diff renderer source 검사에 필요한 불변 값 계약 fixture.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 실제 Changes.cs 통합 증명으로 집계하지 않음.
    /// </summary>
    public sealed class ChangeCardFile
    {
        public string FilePath { get; private set; }
        public int AddedLines { get; private set; }
        public int RemovedLines { get; private set; }
        public string DiffText { get; private set; }
        public int FirstChangedLine { get; private set; }
        public bool HasLineCounts { get; private set; }
        /// <summary>
        /// name         : ChangeCardFile
        /// desc         : source renderer에 실제 입력 fixture 값을 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 추정 계수는 만들지 않음.
        /// </summary>
        public ChangeCardFile(string path, int added, int removed, string diff, int line = 1, bool known = true)
        { FilePath = path; AddedLines = added; RemovedLines = removed; DiffText = diff; FirstChangedLine = line; HasLineCounts = known; }
    }
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 소유 Viewport/Thinking/Overlay 소스를 창 표시 없이 컴파일·측정하는 source host.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : fake 계정·설정·문서·AI 프로세스 기능 없음.
    /// </summary>
    public partial class AssistantControl : UserControl, IDisposable
    {
        private bool disposed;
        private readonly TextBlock ThinkingIndicator = new TextBlock(), ThinkingAboveIndicator = new TextBlock();
        private readonly ScrollViewer ConversationScroll = new ScrollViewer();
        private readonly TextBox Instruction = new TextBox();
        private readonly ListBox AttachmentList = new ListBox(), ConversationList = new ListBox();
        private readonly Grid LayoutRoot = new Grid();
        private readonly Border HistoryPanel = new Border();
        private readonly ToggleButton HistoryToggle = new ToggleButton();
        /// <summary>
        /// name         : AssistantControl
        /// desc         : 실제 overlay 이름·연결을 가진 최소 메모리 트리를 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 실제 XAML 화면과 별도 fixture임을 결과에 명시.
        /// </summary>
        public AssistantControl()
        {
            NameScope.SetNameScope(this, new NameScope());
            var surface = new Grid(); surface.Children.Add(ConversationScroll); LayoutRoot.Children.Add(surface); RegisterName("ConversationSurface", surface);
            var backdrop = new Border { Visibility = Visibility.Collapsed }; LayoutRoot.Children.Add(backdrop); RegisterName("HistoryBackdrop", backdrop);
            var composer = new Border(); LayoutRoot.Children.Add(composer); RegisterName("ComposerSurface", composer);
            HistoryPanel.Child = ConversationList; LayoutRoot.Children.Add(HistoryPanel); Content = LayoutRoot;
        }
        /// <summary>
        /// name         : HistoryToggled
        /// desc         : source host의 목록 표시 상태만 변경하고 overlay를 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 디스크 저장 기능을 호출하지 않음.
        /// </summary>
        private void HistoryToggled(object sender, RoutedEventArgs args) { HistoryPanel.Visibility = HistoryToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed; UpdateHistoryOverlay(); }
        /// <summary>
        /// name         : Dispose
        /// desc         : 실제 소유 수명 helper를 종료한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 열린 창이나 실제 프로세스 없음.
        /// </summary>
        public void Dispose() { disposed = true; DisposeConversationViewport(); }
    }
}
