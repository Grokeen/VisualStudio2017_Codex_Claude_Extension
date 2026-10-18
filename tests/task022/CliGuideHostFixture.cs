using System.Windows;
using System.Windows.Controls;

namespace VS2017.AIAssistant.Views
{
    /// <summary>
    /// name         : AssistantControl
    /// desc         : source-only CLI 안내 구조 검사에만 사용하는 빈 WPF host fixture.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 제품 DLL 검사에서는 제외.
    /// </summary>
    public sealed class AssistantControl : UserControl, System.IDisposable
    {
        /// <summary>
        /// name         : Dispose
        /// desc         : source-only 빈 host 수명을 종료한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 제품 모드에서는 원래 Dispose를 사용.
        /// </summary>
        public void Dispose() { }
    }
    /// <summary>
    /// name         : AssistantDialog
    /// desc         : source-only 새 안내창 검사에 필요한 최소 공통창 계약 fixture.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 제품 공통 테마·VS 소유 창 통합을 입증하지 않음.
    /// </summary>
    public class AssistantDialog : Window
    {
        public WrapPanel Actions { get; private set; }
        public TextBlock Message { get; private set; }
        public ContentControl Body { get; private set; }
        public ContentControl FooterContent { get; private set; }
        /// <summary>
        /// name         : AssistantDialog
        /// desc         : 새 Dialog가 사용하는 공개 컨테이너만 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 표시·브라우저 초기화·설치 실행 없음.
        /// </summary>
        public AssistantDialog(AssistantControl source,string title,string description)
        {
            Title=title;MaxWidth=1200;MaxHeight=900;
            Actions=new WrapPanel();Actions.Children.Add(new Button{Content="닫기"});Message=new TextBlock();Body=new ContentControl();FooterContent=new ContentControl();
            var panel=new StackPanel();panel.Children.Add(Body);panel.Children.Add(FooterContent);panel.Children.Add(Message);panel.Children.Add(Actions);Content=panel;
        }
    }
}
