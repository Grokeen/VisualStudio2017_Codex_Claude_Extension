using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantGuideDetails
    /// desc         : 짧은 설정 요약 뒤에 접고 펴는 Markdown 상세 설명과 그림을 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 설정 입력·저장 동작과 분리된 읽기 전용 안내.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class AssistantGuideDetails : ContentControl
    {
        public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register("Summary", typeof(string), typeof(AssistantGuideDetails), new PropertyMetadata("", ContentChanged));
        public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register("Markdown", typeof(string), typeof(AssistantGuideDetails), new PropertyMetadata("", ContentChanged));
        public static readonly DependencyProperty MediaKeyProperty = DependencyProperty.Register("MediaKey", typeof(string), typeof(AssistantGuideDetails), new PropertyMetadata("", ContentChanged));
        public string Summary { get { return (string)GetValue(SummaryProperty); } set { SetValue(SummaryProperty, value); } }
        public string Markdown { get { return (string)GetValue(MarkdownProperty); } set { SetValue(MarkdownProperty, value); } }
        public string MediaKey { get { return (string)GetValue(MediaKeyProperty); } set { SetValue(MediaKeyProperty, value); } }
        private readonly TextBlock summary;
        private readonly Expander details;
        private readonly StackPanel expandedBody;
        private readonly MarkdownPreview preview;
        private FrameworkElement illustration;
        private string displayedMediaKey;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantGuideDetails
        /// desc         : 초기에는 요약만 보이며 상세를 선택하면 그림·문단을 같은 스크롤 안에서 펼친다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 외부 ScrollViewer가 휠을 처리하고 자동 외부 탐색 없음.
        ///                2026-10-19 CodexCode : Role15 요약도 선택한 테마의 보조 글자색을 동적으로 사용.
        ///                2026-10-19 CodexCode : SDK15 링크 콜백 내부에서 실제 WPF Dispatcher 진입 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        public AssistantGuideDetails()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            var body = new StackPanel();
            summary = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
            summary.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush");
            body.Children.Add(summary);
            expandedBody = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            preview = new MarkdownPreview { UseInternalScrolling = false, AssetBaseDirectory = AssistantGuideMedia.GetAssetDirectory() };
            preview.LinkRequested += (sender, args) => { Dispatcher.VerifyAccess(); AssistantGuideMedia.NavigateLink(this, args); };
            expandedBody.Children.Add(preview);
            details = new Expander { Header = "자세히 보기 · 그림 안내", IsExpanded = false, Content = expandedBody, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            details.SetResourceReference(StyleProperty, "AssistantExpander");
            AutomationProperties.SetName(details, "상세 설명 접고 펴기");
            details.Expanded += DetailsExpanded;
            body.Children.Add(details); Content = body;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ContentChanged
        /// desc         : 요약과 원문 의존 속성이 변경되면 같은 안내 표면만 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 설정값·공통 규칙을 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ContentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var view = (AssistantGuideDetails)sender;
            if (view.summary == null) return;
            view.summary.Text = view.Summary ?? ""; view.preview.Markdown = view.Markdown ?? "";
            if (args.Property == MediaKeyProperty && view.details.IsExpanded) view.UpdateIllustration();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DetailsExpanded
        /// desc         : 처음 펼칠 때에만 안내 그림을 준비한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 접힌 설정 설명의 GIF를 미리 재생하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DetailsExpanded(object sender, RoutedEventArgs args) { UpdateIllustration(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateIllustration
        /// desc         : 안내 키가 바뀐 경우에만 그림을 교체하고 기존 GIF 수명을 정리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 반복 확장 시 같은 그림을 다시 만들지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateIllustration()
        {
            if (illustration != null && displayedMediaKey == MediaKey) return;
            if (illustration != null) expandedBody.Children.Remove(illustration);
            displayedMediaKey = MediaKey;
            illustration = AssistantGuideMedia.CreateIllustration(MediaKey, "설정 안내");
            expandedBody.Children.Insert(0, illustration);
        }
    }
}
