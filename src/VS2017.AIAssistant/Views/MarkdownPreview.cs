using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownPreview
    /// desc         : 원문 바인딩과 선택·복사가 가능한 native Markdown 미리보기를 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 공통 보기; HTML·외부 이미지 자동 요청 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class MarkdownPreview : ContentControl, IDisposable
    {
        public static readonly DependencyProperty MarkdownProperty=DependencyProperty.Register("Markdown",typeof(string),typeof(MarkdownPreview),new PropertyMetadata("",PreviewChanged));
        public static readonly DependencyProperty AssetBaseDirectoryProperty=DependencyProperty.Register("AssetBaseDirectory",typeof(string),typeof(MarkdownPreview),new PropertyMetadata(null,PreviewChanged));
        public static readonly DependencyProperty UseInternalScrollingProperty=DependencyProperty.Register("UseInternalScrolling",typeof(bool),typeof(MarkdownPreview),new PropertyMetadata(true,ScrollingChanged));
        public static readonly RoutedEvent LinkRequestedEvent=EventManager.RegisterRoutedEvent("LinkRequested",RoutingStrategy.Bubble,typeof(EventHandler<MarkdownLinkEventArgs>),typeof(MarkdownPreview));
        public string Markdown { get{return (string)GetValue(MarkdownProperty);} set{SetValue(MarkdownProperty,value);} }
        public string AssetBaseDirectory { get{return (string)GetValue(AssetBaseDirectoryProperty);} set{SetValue(AssetBaseDirectoryProperty,value);} }
        public bool UseInternalScrolling { get{return (bool)GetValue(UseInternalScrollingProperty);} set{SetValue(UseInternalScrollingProperty,value);} }
        public RichTextBox TextView { get; private set; }
        public event EventHandler<MarkdownLinkEventArgs> LinkRequested { add{AddHandler(LinkRequestedEvent,value);} remove{RemoveHandler(LinkRequestedEvent,value);} }
        private MarkdownRendering rendering;
        private bool disposed,refreshing;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkdownPreview
        /// desc         : 일반 WPF 읽기 전용 편집기에 선택·기본 복사 기능을 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 기존 호스트 메뉴·테마·탐색 처리는 호출자에게 위임.
        /// </summary>
        /// ---------------------------------------------------------------------
        public MarkdownPreview()
        {
            SetResourceReference(ForegroundProperty,"AssistantForegroundBrush");SetResourceReference(BackgroundProperty,"AssistantBackgroundBrush");
            TextView=new RichTextBox{IsReadOnly=true,IsDocumentEnabled=true,AcceptsTab=false,BorderThickness=new Thickness(0),Padding=new Thickness(0),Background=Brushes.Transparent,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            TextView.SetResourceReference(RichTextBox.SelectionBrushProperty,"AssistantSelectionBrush");Content=TextView;HorizontalContentAlignment=HorizontalAlignment.Stretch;VerticalContentAlignment=VerticalAlignment.Stretch;
            Loaded+=PreviewLoaded;Refresh();
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PreviewChanged
        /// desc         : 원문·신뢰 자산 폴더 변경을 새 제한된 문서에 반영한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 원문 바인딩을 잘라내거나 다시 쓰지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void PreviewChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args){((MarkdownPreview)sender).Refresh();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ScrollingChanged
        /// desc         : 채팅·안내 호스트의 바깥 스크롤을 사용하도록 내부 막대를 제어한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 부모 컨테이너의 휠 이벤트를 강제 차단하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ScrollingChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args){var view=(MarkdownPreview)sender;if(view.TextView!=null)view.TextView.VerticalScrollBarVisibility=view.UseInternalScrolling?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PreviewLoaded
        /// desc         : 호스트 테마 상속이 완료된 뒤 미리보기 색상을 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 창을 생성하거나 포커스·클립보드를 자동 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PreviewLoaded(object sender,RoutedEventArgs args){Refresh();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SetDocument
        /// desc         : 문서와 신뢰 자산 폴더를 함께 바꾼 뒤 한 번만 렌더링한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 이전 원문이 새 문서 폴더의 자산을 잠깐 읽는 경로 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void SetDocument(string markdown,string assetBaseDirectory)
        {if(disposed)return;if(!Dispatcher.CheckAccess())throw new InvalidOperationException("Markdown 문서는 UI 스레드에서 변경해야 합니다.");refreshing=true;try{SetCurrentValue(MarkdownProperty,markdown);SetCurrentValue(AssetBaseDirectoryProperty,assetBaseDirectory);}finally{refreshing=false;}Refresh();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OnPropertyChanged
        /// desc         : 상속된 글꼴·색상 변경을 현재 native 문서에 반영한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 전역 리소스나 기존 컨트롤 스타일을 수정하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs args)
        {base.OnPropertyChanged(args);if(args.Property==ForegroundProperty||args.Property==BackgroundProperty||args.Property==FontSizeProperty)Refresh();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Refresh
        /// desc         : 이전 GIF 자원을 해제하고 현재 원문을 한 번만 렌더링한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : UI 스레드 전용이며 재진입과 종료 후 갱신을 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Refresh()
        {
            if(TextView==null||disposed||refreshing)return;if(!Dispatcher.CheckAccess())throw new InvalidOperationException("Markdown 미리보기는 UI 스레드에서 갱신해야 합니다.");
            refreshing=true;MarkdownRendering next=null;
            try
            {
                next=new MarkdownRendering(AssetBaseDirectory,ForwardLink,Foreground,Background,FontSize,TryFindResource("AssistantLinkBrush") as Brush,TryFindResource("AssistantCodeBackgroundBrush") as Brush,TryFindResource("AssistantMutedForegroundBrush") as Brush,TryFindResource("AssistantBorderBrush") as Brush);
                var document=next.Render(Markdown);var previous=rendering;TextView.Document=document;rendering=next;next=null;if(previous!=null)previous.Dispose();
                TextView.Foreground=Foreground;TextView.FontSize=FontSize;
            }
            finally{if(next!=null)next.Dispose();refreshing=false;}
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ForwardLink
        /// desc         : 사용자가 누른 허용 링크만 routed event로 호스트에 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 대상 앱 실행·네트워크·문서 열기 책임은 호스트에 있음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ForwardLink(object sender,MarkdownLinkEventArgs args){args.RoutedEvent=LinkRequestedEvent;args.Source=this;RaiseEvent(args);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 호출자가 영구히 닫은 미리보기의 이미지와 이벤트를 정리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Unloaded만으로 원문을 삭제하지 않아 탭 재사용을 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose(){if(disposed)return;disposed=true;Loaded-=PreviewLoaded;if(rendering!=null){rendering.Dispose();rendering=null;}TextView.Document=new System.Windows.Documents.FlowDocument();}
    }
}
