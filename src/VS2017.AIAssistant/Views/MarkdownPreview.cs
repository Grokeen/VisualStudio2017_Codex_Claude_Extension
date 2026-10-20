using System;
using System.Windows;
using System.Collections.Generic;
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
    ///                2026-10-20 CodexOrig : TASK069 동일 입력의 Loaded 재표시에서 문서·선택·GIF를 재사용.
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
        private object[] renderedInputs;
        private System.Windows.Documents.FlowDocument renderedDocument;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkdownPreview
        /// desc         : 일반 WPF 읽기 전용 편집기에 선택·기본 복사 기능을 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 기존 호스트 메뉴·테마·탐색 처리는 호출자에게 위임.
        ///                2026-10-19 CodexOrig : VS 읽기 전용 기본 템플릿의 불투명 바탕 대신 전용 투명 본문 스타일 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public MarkdownPreview()
        {
            // 기본 팔레트는 스타일에서 받는다. 생성자 local 값은 메시지 템플릿의 투명 배경·회색 글자보다 우선한다.
            TextView=new RichTextBox{IsReadOnly=true,IsDocumentEnabled=true,AcceptsTab=false,BorderThickness=new Thickness(0),Padding=new Thickness(0),Background=Brushes.Transparent,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            TextView.SetResourceReference(StyleProperty,"MarkdownTextViewStyle");
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
        ///                2026-10-20 CodexOrig : TASK069 동등한 자동 입력 알림은 완성된 문서를 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void PreviewChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args){((MarkdownPreview)sender).RefreshCore(false);}
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
        ///                2026-10-20 CodexOrig : TASK069 탭 복원에서 같은 입력의 문서 재생성을 생략.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PreviewLoaded(object sender,RoutedEventArgs args){RefreshCore(false);}
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
        ///                2026-10-20 CodexOrig : TASK069 실제 색·글꼴 값이 달라질 때만 자동 재렌더링.
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs args)
        {base.OnPropertyChanged(args);if(args.Property==ForegroundProperty||args.Property==BackgroundProperty||args.Property==FontSizeProperty||args.Property==FontFamilyProperty||args.Property==FontStyleProperty||args.Property==FontWeightProperty||args.Property==FontStretchProperty)RefreshCore(false);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Refresh
        /// desc         : 이전 GIF 자원을 해제하고 현재 원문을 한 번만 렌더링한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : UI 스레드 전용이며 재진입과 종료 후 갱신을 차단.
        ///                2026-10-20 CodexOrig : TASK069 공개 Refresh는 외부 자산 변경까지 명시적으로 다시 읽음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Refresh(){RefreshCore(true);}

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshCore
        /// desc         : 자동 재표시는 완료된 동일 문서를 유지하고 명시 갱신은 자산까지 다시 렌더링한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 인스턴스 내 입력 스냅샷·외부 Document 교체·GIF 수명 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshCore(bool force)
        {
            if(TextView==null||disposed||refreshing)return;if(!Dispatcher.CheckAccess())throw new InvalidOperationException("Markdown 미리보기는 UI 스레드에서 갱신해야 합니다.");
            var link=TryFindResource("AssistantLinkBrush") as Brush;var code=TryFindResource("AssistantCodeBackgroundBrush") as Brush;var muted=TryFindResource("AssistantMutedForegroundBrush") as Brush;var border=TryFindResource("AssistantBorderBrush") as Brush;
            var inputs=CaptureRenderInputs(link,code,muted,border);
            if(!force&&ReferenceEquals(TextView.Document,renderedDocument)&&SameRenderInputs(inputs))return;
            refreshing=true;MarkdownRendering next=null;
            try
            {
                next=new MarkdownRendering(AssetBaseDirectory,ForwardLink,Foreground,Background,FontSize,link,code,muted,border);
                var document=next.Render(Markdown);var previous=rendering;TextView.Document=document;rendering=next;next=null;if(previous!=null)previous.Dispose();
                TextView.Foreground=Foreground;TextView.FontSize=FontSize;
                renderedDocument=document;renderedInputs=inputs;
            }
            finally{if(next!=null)next.Dispose();refreshing=false;}
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureRenderInputs
        /// desc         : 원문·기준 폴더·글꼴과 현재 팔레트 색의 값을 충돌 없는 비교용 스냅샷으로 보관한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 전역 캐시·파일 조회 없이 mutable brush의 변경도 감지.
        ///                2026-10-20 CodexOrig : WPF에서 허용되는 null 변환은 identity 값으로 비교.
        /// </summary>
        /// ---------------------------------------------------------------------
        private object[] CaptureRenderInputs(Brush link,Brush code,Brush muted,Brush border)
        {
            var values=new List<object>{Markdown??"",AssetBaseDirectory,FontSize,FontFamily.Source,FontFamily.BaseUri,FontStyle,FontWeight,FontStretch};
            foreach(var brush in new[]{Foreground,Background,link,code,muted,border})
            {
                var solid=brush as SolidColorBrush;
                // 지원하지 않는 brush는 잘못 재사용하지 않고 기존의 재렌더링 경로를 유지한다.
                values.Add(brush==null?null:solid==null?new object():typeof(SolidColorBrush));
                values.Add(solid==null?(object)null:solid.Color);values.Add(brush==null?0:brush.Opacity);
                values.Add(brush==null||brush.Transform==null?Matrix.Identity:brush.Transform.Value);values.Add(brush==null||brush.RelativeTransform==null?Matrix.Identity:brush.RelativeTransform.Value);
            }
            return values.ToArray();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SameRenderInputs
        /// desc         : 마지막으로 완성된 렌더 입력과 각 실제 값을 비교한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 해시 충돌로 다른 원문이나 테마를 재사용하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool SameRenderInputs(object[] values)
        {if(renderedInputs==null||values.Length!=renderedInputs.Length)return false;for(int index=0;index<values.Length;index++)if(!Equals(values[index],renderedInputs[index]))return false;return true;}
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
        ///                2026-10-20 CodexOrig : TASK069 종료 시 인스턴스의 입력 스냅샷과 문서 참조도 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose(){if(disposed)return;disposed=true;Loaded-=PreviewLoaded;renderedInputs=null;renderedDocument=null;if(rendering!=null){rendering.Dispose();rendering=null;}TextView.Document=new System.Windows.Documents.FlowDocument();}
    }
}
