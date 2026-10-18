using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodexCode.Task022
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CenterResult
    /// desc         : 표시되지 않은 메모리 컨트롤의 중앙 배치 결과를 보관한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-19 CodexOrig : TASK025 Markdown 본문·고정 복귀·명시 Dark 팔레트의 최신 UI 계약 검증.
    /// update date  : 2026-10-18 CodexCode : TASK-022 실제 GUI 실행 없이 좌표 증거 기록.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class CenterResult
    {
        public bool Succeeded;
        public List<string> Passed = new List<string>();
        public List<string> Failures = new List<string>();
        public List<object> Evidence = new List<object>();
        public string Error;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CenterChecks
    /// desc         : 실제 Fan·공통 대화상자 소스의 정렬을 독립 메모리에서 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 창 표시·렌더·이벤트 루프·계정 접근 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class CenterChecks
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : 병렬 의미가 없는 독립 배치 판정을 누적한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 검사 결과 수집.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Check(CenterResult result, bool passed, string label)
        { if (passed) result.Passed.Add(label); else result.Failures.Add(label); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Descendants
        /// desc         : 화면 표시 없이 적용된 템플릿의 실제 시각 자식만 열거한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 TextBlock 실제 배치 좌표 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i); yield return child;
                foreach (var descendant in Descendants(child)) yield return descendant;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Layout
        /// desc         : HWND나 Window 표시 없이 유한 컨트롤 트리를 측정하고 배치한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 출력 이미지나 Dispatcher loop 사용 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Layout(FrameworkElement element, double width, double height)
        { element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckCommand
        /// desc         : 명령 버튼 내부 글자와 아이콘의 두 축 중앙 정렬 계약을 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 버튼 자체의 외부 배치와 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckCommand(CenterResult result, Button button, string label)
        {
            Layout(button, 240, 48); button.ApplyTemplate(); Layout(button, 240, 48);
            var presenter = Descendants(button).OfType<ContentPresenter>().FirstOrDefault();
            bool aligned = button.HorizontalContentAlignment == HorizontalAlignment.Center && button.VerticalContentAlignment == VerticalAlignment.Center;
            if (presenter != null)
            {
                Rect bounds = presenter.TransformToAncestor(button).TransformBounds(new Rect(0, 0, presenter.ActualWidth, presenter.ActualHeight));
                aligned = aligned && Math.Abs(bounds.X + bounds.Width / 2 - button.ActualWidth / 2) < 0.1 && Math.Abs(bounds.Y + bounds.Height / 2 - button.ActualHeight / 2) < 0.1;
            }
            Check(result, aligned && presenter != null, label);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Property
        /// desc         : 소스 대역 또는 실제 제품의 동일한 공개 컨트롤 속성을 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 제품 DLL을 재컴파일하지 않는 reflection 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static T Property<T>(object value, string name) { return (T)value.GetType().GetProperty(name).GetValue(value, null); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Run
        /// desc         : 200·500 폭과 네 글자 크기로 중심을 확인하고 긴 안내 카드의 읽기 정렬을 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 순수 메모리 배치 회귀 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static CenterResult Run(FrameworkElement source)
        {
            var result = new CenterResult(); Type controlType = source.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                controlType.GetMethod("InitializeFanMenu", flags).Invoke(source, null);
                var fan = (Canvas)controlType.GetField("FanPanel", flags).GetValue(source); fan.Visibility = Visibility.Visible;
                foreach (double width in new[] { 200.0, 500.0 }) foreach (double font in new[] { 12.0, 14.0, 20.0, 24.0 })
                {
                    source.Width = width; ((Control)source).FontSize = font; Layout(source, width, 400);
                    controlType.GetMethod("UpdateFanScale", flags).Invoke(source, null); Layout(source, width, 400);
                    foreach (string name in new[] { "HelpButton", "SettingsButton" })
                    {
                        var button = (Button)controlType.GetField(name, flags).GetValue(source); button.ApplyTemplate(); Layout(button, 260, 200);
                        var caption = (FrameworkElement)button.Template.FindName("FanCaption", button);
                        Layout(button, 260, 200);
                        var text = Descendants(caption).OfType<TextBlock>().ToArray();
                        double degrees = name == "HelpButton" ? (123 + 158) / 2.0 : (90 + 121) / 2.0;
                        var expected = new Point(240 + 126 * Math.Cos(degrees * Math.PI / 180), 20 + 126 * Math.Sin(degrees * Math.PI / 180));
                        Rect bounds = Rect.Empty; bool textInside = true;
                        foreach (var block in text)
                        {
                            Rect textBounds = block.TransformToAncestor(button).TransformBounds(new Rect(0, 0, block.ActualWidth, block.ActualHeight)); bounds.Union(textBounds);
                            textInside = textInside && new[] { textBounds.TopLeft, textBounds.TopRight, textBounds.BottomLeft, textBounds.BottomRight }.All(point => button.Clip.FillContains(point));
                        }
                        var actual = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                        string label = name + " width=" + width + " font=" + font;
                        Check(result, text.Length == 2 && (actual - expected).Length < 0.1, label + " actual icon/label union is at sector midpoint");
                        Check(result, text.All(t => t.TextAlignment == TextAlignment.Center) && button.Clip.FillContains(expected), label + " label is centered inside active sector");
                        Check(result, textInside, label + " complete icon/label bounds remain inside clipped sector");
                        Check(result, button.HorizontalContentAlignment == HorizontalAlignment.Center && button.VerticalContentAlignment == VerticalAlignment.Center, label + " content alignment is centered");
                        result.Evidence.Add(new { Button = name, Width = width, Font = font, ExpectedX = expected.X, ExpectedY = expected.Y, ActualX = actual.X, ActualY = actual.Y, CaptionWidth = bounds.Width, CaptionHeight = bounds.Height });
                    }
                }
                Type dialogType = controlType.Assembly.GetType("VS2017.AIAssistant.Views.AssistantDialog", true);
                var dialog = (Window)Activator.CreateInstance(dialogType, source, "MEMORY ONLY", "No Show or ShowDialog");
                CheckCommand(result, Property<WrapPanel>(dialog, "Actions").Children.OfType<Button>().Single(), "Common close command content is centered");
                var section = new StackPanel(); var scroll = new ScrollViewer { Content = section };
                Type sectionType = controlType.Assembly.GetType("VS2017.AIAssistant.Views.DialogSection", true);
                Array sections = Array.CreateInstance(sectionType, 1); sections.SetValue(Activator.CreateInstance(sectionType, "⚙", "설정", section), 0);
                dialogType.GetMethod("SetSections").Invoke(dialog, new object[] { scroll, sections });
                CheckCommand(result, Property<WrapPanel>(dialog, "Navigation").Children.OfType<Button>().Single(), "Section navigation command content is centered");
                Type guideType = controlType.Assembly.GetType("VS2017.AIAssistant.Views.GettingStartedDialog", true);
                var guide = (Window)Activator.CreateInstance(guideType, source, false);
                CheckCommand(result, Property<Button>(guide, "StartButton"), "Start command content is centered");
                CheckCommand(result, Property<Button>(guide, "ConnectButton"), "Connect command content is centered");
                var gallery = (ContentControl)Property<ContentControl>(guide, "Body").Content;
                var cards = (List<Button>)gallery.GetType().GetField("cards", flags).GetValue(gallery);
                Check(result, cards.Count == 4 && cards.All(card => card.HorizontalContentAlignment == HorizontalAlignment.Stretch && card.VerticalContentAlignment == VerticalAlignment.Stretch), "Long help cards retain full-width reading surface");
                var surface = (Border)cards[0].Content; var paragraphs = (StackPanel)surface.Child;
                Check(result, paragraphs.Children.OfType<TextBlock>().All(text => text.TextAlignment == TextAlignment.Left), "Long help paragraphs retain reading alignment");
                gallery.GetType().GetMethod("ShowDetail", flags).Invoke(gallery, new[] { cards[0].DataContext, cards[0] });
                var detail = (Grid)gallery.Content;
                var back = detail.Children.OfType<Button>().Single();
                CheckCommand(result, back, "Back-to-guide command content is centered");
                Check(result, !detail.Children.OfType<ScrollViewer>().Single().IsAncestorOf(back), "Back-to-guide command stays outside the detail scroll surface");
                Check(result, !source.IsLoaded && System.Windows.PresentationSource.FromVisual(source) == null && !dialog.IsLoaded && !guide.IsLoaded, "No source/dialog is loaded or attached to a presentation surface");
                result.Succeeded = result.Failures.Count == 0;
            }
            catch (Exception error) { result.Error = error.ToString(); }
            finally { var disposable = source as IDisposable; if (disposable != null) disposable.Dispose(); else controlType.GetMethod("DisposeFanMenu", flags).Invoke(source, null); }
            return result;
        }
    }
}
