using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : HoverScrollPolicy
    /// desc         : 포인터 아래 가장 가까운 스크롤을 먼저 움직이고 팝업 경계의 휠 누수를 막는다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK024 중첩·끝 위치·고해상도 휠의 한정된 소비 정책.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class HoverScrollPolicy
    {
        public static readonly DependencyProperty ContainMouseWheelProperty = DependencyProperty.RegisterAttached("ContainMouseWheel", typeof(bool), typeof(HoverScrollPolicy), new PropertyMetadata(false, ContainmentChanged));
        /// <summary>
        /// name         : GetContainMouseWheel
        /// desc         : 현재 스크롤의 hover 휠 우선 정책 사용 여부를 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Theme Setter에서 동일한 정책을 공유.
        /// </summary>
        public static bool GetContainMouseWheel(DependencyObject target) { return (bool)target.GetValue(ContainMouseWheelProperty); }
        /// <summary>
        /// name         : SetContainMouseWheel
        /// desc         : 같은 true 반복 설정은 중복 구독 없이 현재 스크롤 정책을 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : false는 등록한 핸들러만 해제.
        /// </summary>
        public static void SetContainMouseWheel(DependencyObject target, bool value) { target.SetValue(ContainMouseWheelProperty, value); }
        /// <summary>
        /// name         : ContainmentChanged
        /// desc         : ScrollViewer의 속성 상태가 바뀔 때만 preview 이벤트를 연결하거나 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 다른 컨트롤에 설정해도 이벤트를 가로채지 않음.
        /// </summary>
        private static void ContainmentChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            var scroll = target as ScrollViewer; if (scroll == null) return;
            scroll.PreviewMouseWheel -= ViewerMouseWheel;
            if ((bool)args.NewValue) scroll.PreviewMouseWheel += ViewerMouseWheel;
        }
        /// <summary>
        /// name         : ViewerMouseWheel
        /// desc         : 부모 preview는 더 가까운 유효 스크롤에 양보하고 해당 스크롤만 휠을 소비한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 끝 위치에서는 상위 대화·IDE로 누수시키지 않음.
        /// </summary>
        private static void ViewerMouseWheel(object sender, MouseWheelEventArgs args)
        {
            if (args.Handled) return;
            var viewer = (ScrollViewer)sender;
            ScrollViewer nearest = NearestViewer(args.OriginalSource as DependencyObject, null, false);
            if (nearest != null && !ReferenceEquals(nearest, viewer))
            {
                // Run의 논리 이벤트 경로에는 RichTextBox 내부 template viewer가 없으므로 그 경우만 직접 전달한다.
                if (args.OriginalSource is ContentElement) HandleWheel(nearest, args, false);
                return;
            }
            if (nearest == null) args.Handled = true;
            else HandleWheel(viewer, args, false);
        }

        /// <summary>
        /// name         : HandleWheel
        /// desc         : 범위 내 첫 ScrollViewer만 선택하며 이동 한계에서도 배경으로 이벤트를 보내지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 기본 텍스트 내부의 스크롤 없는 뷰는 대화로 전달 가능.
        /// </summary>
        internal static bool HandleWheel(FrameworkElement boundary, MouseWheelEventArgs args, bool consumeWithoutRange)
        {
            if (args.Handled) return true;
            ScrollViewer nearest = NearestViewer(args.OriginalSource as DependencyObject, boundary, consumeWithoutRange);
            if (nearest == null && !consumeWithoutRange) return false;
            if (nearest != null)
            {
                double lines = SystemParameters.WheelScrollLines;
                // TextBoxView의 CanContentScroll=true는 item 단위가 아니라 픽셀 offset이므로 글자 높이로 이동한다.
                bool logicalItems = nearest.CanContentScroll && !(nearest.TemplatedParent is TextBoxBase);
                double unit = lines < 0 ? nearest.ViewportHeight : Math.Max(1, lines) * (logicalItems ? 1 : Math.Max(14, TextElement.GetFontSize(nearest) * 1.3));
                nearest.ScrollToVerticalOffset(nearest.VerticalOffset - args.Delta / 120.0 * unit);
            }
            args.Handled = true;
            return true;
        }

        /// <summary>
        /// name         : HandleNestedWheel
        /// desc         : 대화보다 가까운 실제 문서 스크롤만 소비해 대화의 읽기·추종 상태를 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : FlowDocument 논리 자식에서도 template viewer를 찾음.
        /// </summary>
        internal static bool HandleNestedWheel(ScrollViewer boundary, MouseWheelEventArgs args)
        {
            if (args.Handled) return true;
            var nearest = NearestViewer(args.OriginalSource as DependencyObject, boundary, false);
            return nearest != null && !ReferenceEquals(nearest, boundary) && HandleWheel(nearest, args, false);
        }

        /// <summary>
        /// name         : NearestViewer
        /// desc         : 시각 부모와 FlowDocument 논리 부모를 따라 가장 가까운 유효 viewer를 찾는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 스크롤 없는 editor는 상위 대화에 위임.
        /// </summary>
        private static ScrollViewer NearestViewer(DependencyObject source, DependencyObject boundary, bool allowEmpty)
        {
            for (DependencyObject node = source; node != null; node = Parent(node))
            {
                var viewer = node as ScrollViewer;
                if (viewer == null && node is TextBoxBase) viewer = EditorViewer(node);
                if (viewer != null && (allowEmpty || viewer.ScrollableHeight > 0)) return viewer;
                if (ReferenceEquals(node, boundary)) break;
            }
            return null;
        }

        /// <summary>
        /// name         : EditorViewer
        /// desc         : ContentElement 경로에서 생략된 텍스트 편집기의 실제 template ScrollViewer만 찾는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 256개 시각 노드 이하로 탐색량 제한.
        /// </summary>
        private static ScrollViewer EditorViewer(DependencyObject editor)
        {
            var pending = new Stack<DependencyObject>(); pending.Push(editor); int count = 0;
            while (pending.Count > 0 && count++ < 256)
            {
                var node = pending.Pop(); var viewer = node as ScrollViewer; if (viewer != null) return viewer;
                if (!(node is Visual)) continue;
                for (int index = VisualTreeHelper.GetChildrenCount(node) - 1; index >= 0; index--) pending.Push(VisualTreeHelper.GetChild(node, index));
            }
            return null;
        }

        /// <summary>
        /// name         : Parent
        /// desc         : Run 같은 ContentElement와 시각 요소의 부모를 동일한 경계 탐색에 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : FlowDocument 원문 위의 휠도 내부 뷰에 우선 전달.
        /// </summary>
        internal static DependencyObject Parent(DependencyObject node)
        {
            var content = node as ContentElement;
            if (content != null) return ContentOperations.GetParent(content) ?? (content as FrameworkContentElement)?.Parent;
            return node is Visual ? VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DiffPreviewPresenter
    /// desc         : 파일 제목·실제 계수와 줄 번호가 있는 읽기 전용 색상 비교를 유한 높이에 표시한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK024 외부 Markdown·HTML 실행 없이 원문만 표시.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class DiffPreviewPresenter : Grid
    {
        private readonly TextBlock fileTitle, counts;
        private readonly RichTextBox editor;
        private readonly TextBlock historyText;

        /// <summary>
        /// name         : DiffPreviewPresenter
        /// desc         : 제목은 고정하고 비교와 최근 기록만 각 유한 영역에서 스크롤하게 구성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 밝은 호스트의 Background·Foreground 상속 차단.
        /// </summary>
        public DiffPreviewPresenter()
        {
            Background = new SolidColorBrush(Color.FromRgb(29, 30, 32));
            SetValue(TextElement.ForegroundProperty, Brushes.Gainsboro);
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition());
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var heading = new DockPanel { Margin = new Thickness(11, 9, 58, 9) };
            counts = new TextBlock { FontWeight = FontWeights.SemiBold, Margin = new Thickness(12, 0, 0, 0) };
            DockPanel.SetDock(counts, Dock.Right); heading.Children.Add(counts);
            fileTitle = new TextBlock { FontWeight = FontWeights.Bold, Foreground = Brushes.Gainsboro, TextTrimming = TextTrimming.CharacterEllipsis };
            heading.Children.Add(fileTitle); Children.Add(heading);
            editor = new RichTextBox { IsReadOnly = true, IsDocumentEnabled = false, Background = Background, Foreground = Brushes.Gainsboro,
                BorderThickness = new Thickness(0, 1, 0, 1), BorderBrush = new SolidColorBrush(Color.FromRgb(64, 66, 70)), Padding = new Thickness(6),
                FontFamily = new FontFamily("Consolas"), FontSize = 12, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(editor, 1); Children.Add(editor);
            historyText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Silver, FontSize = 11, Margin = new Thickness(8) };
            var historyScroll = new ScrollViewer { MaxHeight = 105, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = historyText };
            var history = new Expander { Header = "최근 변경 기록 · 최대 3건", Content = historyScroll, Foreground = Brushes.Silver, Margin = new Thickness(8, 5, 8, 5) };
            Grid.SetRow(history, 2); Children.Add(history);
            PopupPalette.Attach(this, this);
            SetResourceReference(BackgroundProperty, "AssistantCodeBackgroundBrush"); SetResourceReference(TextElement.ForegroundProperty, "AssistantForegroundBrush");
            fileTitle.SetResourceReference(TextBlock.ForegroundProperty, "AssistantForegroundBrush");
            editor.SetResourceReference(Control.BackgroundProperty, "AssistantCodeBackgroundBrush"); editor.SetResourceReference(Control.ForegroundProperty, "AssistantForegroundBrush"); editor.SetResourceReference(Control.BorderBrushProperty, "AssistantPopupBorderBrush");
            historyText.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush"); history.SetResourceReference(Control.ForegroundProperty, "AssistantMutedForegroundBrush");
            Loaded += (sender, args) => PopupPalette.Attach(this, this);
            PreviewMouseWheel += (sender, args) => HoverScrollPolicy.HandleWheel(this, args, true);
            System.Windows.Automation.AutomationProperties.SetName(this, "파일 변경 비교 · 추가 초록색 · 삭제 빨간색");
        }

        /// <summary>
        /// name         : Present
        /// desc         : 실제 파일 경로·증감·diff와 기존 최근 세 기록을 읽기 전용으로 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 표시량만 제한하며 원본 합계나 변경 상태를 추정하지 않음.
        /// </summary>
        public void Present(ChangeCardFile file, string history)
        {
            if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("비교 표시는 WPF UI 스레드에서만 갱신하세요.");
            fileTitle.Text = System.IO.Path.GetFileName(file.FilePath); fileTitle.ToolTip = file.FilePath;
            counts.Inlines.Clear();
            if (file.HasLineCounts) { var added = new Run("+" + file.AddedLines + " "); added.SetResourceReference(TextElement.ForegroundProperty, "AssistantAddedBrush"); var removed = new Run("−" + file.RemovedLines); removed.SetResourceReference(TextElement.ForegroundProperty, "AssistantRemovedBrush"); counts.Inlines.Add(added); counts.Inlines.Add(removed); }
            else counts.Text = "줄 수 미제공";
            counts.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush");
            historyText.Text = Limit(history ?? "", 12000);
            editor.Document = BuildDocument(file.DiffText);
            editor.ScrollToHome();
        }

        /// <summary>
        /// name         : BuildDocument
        /// desc         : unified hunk의 전후 줄 번호와 실제 추가·삭제를 표시하고 파일 헤더는 중립색으로 구별한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 1000행·60000자 제한 및 triple sign 본문 오판 방지.
        /// </summary>
        internal static FlowDocument BuildDocument(string diff)
        {
            var document = new FlowDocument { PagePadding = new Thickness(0), FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = Brushes.LightGray, Background = new SolidColorBrush(Color.FromRgb(29, 30, 32)), PageWidth = 1800 };
            document.SetResourceReference(TextElement.ForegroundProperty, "AssistantForegroundBrush"); document.SetResourceReference(TextElement.BackgroundProperty, "AssistantCodeBackgroundBrush");
            bool inHunk = false; int oldLine = 0, newLine = 0, bytes = 0, row = 0;
            foreach (string source in (diff ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                if (row++ >= 1000 || bytes >= 60000) { document.Blocks.Add(new Paragraph(new Run("… 미리보기 표시 한도 · 전체 내용은 실행 기록에서 확인하세요."))); break; }
                string line = Limit(source, Math.Min(2000, 60000 - bytes)); bytes += line.Length;
                if (source.StartsWith("diff --git ", StringComparison.Ordinal)) inHunk = false;
                var hunk = Regex.Match(line, @"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@", RegexOptions.CultureInvariant);
                if (hunk.Success) { inHunk = true; int.TryParse(hunk.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out oldLine); int.TryParse(hunk.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out newLine); }
                bool header = !inHunk && (source.StartsWith("--- ", StringComparison.Ordinal) || source.StartsWith("+++ ", StringComparison.Ordinal));
                bool added = !header && source.StartsWith("+", StringComparison.Ordinal), removed = !header && source.StartsWith("-", StringComparison.Ordinal);
                string before = "", after = "";
                if (inHunk && !hunk.Success && source.Length > 0 && source[0] != '\\')
                {
                    if (!added) before = (oldLine++).ToString(CultureInfo.InvariantCulture);
                    if (!removed) after = (newLine++).ToString(CultureInfo.InvariantCulture);
                }
                var paragraph = new Paragraph { Margin = new Thickness(0), LineHeight = 18 };
                var numbers = new Run(before.PadLeft(5) + " " + after.PadLeft(5) + "  "); numbers.SetResourceReference(TextElement.ForegroundProperty, "AssistantMutedForegroundBrush"); paragraph.Inlines.Add(numbers);
                var text = new Run(line); text.SetResourceReference(TextElement.ForegroundProperty, added ? "AssistantAddedBrush" : removed ? "AssistantRemovedBrush" : "AssistantForegroundBrush"); paragraph.Inlines.Add(text);
                if (added || removed) paragraph.Background = new SolidColorBrush(added ? Color.FromArgb(35, 87, 204, 138) : Color.FromArgb(35, 255, 115, 115));
                document.Blocks.Add(paragraph);
            }
            return document;
        }

        /// <summary>
        /// name         : Limit
        /// desc         : 출력 한도를 UTF16 surrogate 쌍 경계를 보존해 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 과대 diff가 UI를 무한 확장하지 않게 제한.
        /// </summary>
        private static string Limit(string text, int length)
        { if (text.Length <= length) return text; int end = Math.Max(0, length - 1); if (end > 0 && char.IsHighSurrogate(text[end - 1])) end--; return text.Substring(0, end) + "…"; }
    }
}
