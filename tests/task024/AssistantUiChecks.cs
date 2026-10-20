using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : AssistantUiChecks
/// desc         : 고정 제품의 답변 Markdown·명시적 소스 탐색·작성 진입과 입력 액션을 격리 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-19
/// update date  : 2026-10-19 CodexCode : TASK024 실제 CLI·브라우저·인증·네트워크·Window.Show 호출 없음.
/// </summary>
/// ---------------------------------------------------------------------
public static class AssistantUiChecks
{
    public static readonly List<string> Checks = new List<string>(), Errors = new List<string>();
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 응답 템플릿과 실제 클릭 경계를 사용하고 IDE 문서 탐색만 합성 수신기로 대체한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 파일 URI·행 접미사·위험 링크·COM 오류·종료 후 입력 회귀 보존.
    ///                2026-10-19 CodexOrig : 감사에서 재현된 문서/프로젝트 동일 이름 소스의 상대 경로·행 이동 회귀.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run(string directory)
    {
        AssistantControl control = null;
        try
        {
            new AssistantSettingsStore().Save(new AssistantSettings { HideWelcome = true, ShowHistory = false });
            control = new AssistantControl { Width = 600, Height = 820 };
            typeof(AssistantControl).GetField("scmLookupStarted", Hidden).SetValue(control, true);
            var documents = new NavigationFixture(directory);
            typeof(AssistantControl).GetField("documents", Hidden).SetValue(control, documents);
            string source = System.IO.Path.Combine(directory, "sample source.cs");
            File.WriteAllText(source, "// synthetic local source\nclass Sample {}\n");

            var chat = (ChatSession)typeof(AssistantControl).GetProperty("ActiveChat", Hidden).GetValue(control, null);
            chat.Add("assistant", "# 표시 예제\n\n**강조**와 `코드`를 확인하세요.\n\n- [소스 위치](<" + source.Replace('\\', '/') + ":7>)\n");
            control.Measure(new Size(600, 820)); control.Arrange(new Rect(0, 0, 600, 820)); control.UpdateLayout();
            var previews = Descendants(control).OfType<MarkdownPreview>().Where(p => p.Markdown.Contains("표시 예제")).ToArray();
            Assert(previews.Length == 1, "The real assistant response template creates one MarkdownPreview");
            if (previews.Length == 1)
            {
                var preview = previews[0];
                string rendered = new TextRange(preview.TextView.Document.ContentStart, preview.TextView.Document.ContentEnd).Text;
                Assert(rendered.Contains("표시 예제") && !rendered.Contains("# 표시 예제") && !rendered.Contains("**강조**") && !rendered.Contains("]("), "Visible response text renders headings, emphasis and links rather than raw Markdown");
                var link = ContentChildren(preview.TextView.Document).OfType<Hyperlink>().Single();
                Assert(documents.Count == 0, "Rendering a local source link never navigates automatically");
                link.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
                Assert(documents.Count == 1 && documents.LastLine == 7 && string.Equals(documents.LastPath, source, StringComparison.OrdinalIgnoreCase), "Clicking a rendered source link navigates to the parsed existing file and requested line");
                preview.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, preview));
                Assert(preview.TextView.ContextMenu != null && preview.TextView.ContextMenu.Items.OfType<MenuItem>().Any(m => m.Command == System.Windows.Input.ApplicationCommands.Copy), "Rendered response text retains Copy and the assistant context menu");
                Assert(preview.TextView.ContextMenu.Items.OfType<MenuItem>().Any(m => Convert.ToString(m.Header) == "Markdown 원문 복사") && preview.Markdown.Contains("]("), "The response menu offers explicit original Markdown copy while retaining the source URL");
                Assert(!preview.UseInternalScrolling, "The response renderer delegates its outer scrolling to the conversation viewport");
            }
            int initial = documents.Count;
            var anchor = Click(control, "#heading", false, null, 1);
            Assert(!anchor.Handled && documents.Count == initial, "Document anchors remain available to the renderer and never become external navigation");
            foreach (string target in new[] { "javascript:alert(1)", "cmd:echo unsafe", "file://server/share/source.cs", "https://name:secret@example.invalid/" })
            {
                Click(control, target, false, target, 1);
                Assert(documents.Count == initial, "Rejected link causes no source navigation: " + target.Split(':')[0]);
            }
            Click(control, @"\\server\share\source.cs", true, @"\\server\share\source.cs", 1);
            Assert(documents.Count == initial, "UNC source links are rejected before IDE navigation");
            Click(control, "C:relative.cs", true, "C:relative.cs", 1);
            Assert(documents.Count == initial, "Drive-relative paths cannot inherit a hidden current-directory context");
            Click(control, "sample source.cs#L2", true, "sample source.cs", 2);
            Assert(documents.Count == initial + 1 && documents.LastPath == source && documents.LastLine == 2, "Relative file links resolve against the IDE project directory");
            string documentDirectory=System.IO.Path.Combine(directory,"documents");
            Directory.CreateDirectory(documentDirectory);
            string sibling=System.IO.Path.Combine(documentDirectory,"chapter.cs");
            File.WriteAllText(sibling,"// document sibling\n");
            File.WriteAllText(System.IO.Path.Combine(directory,"chapter.cs"),"// project shadow\n");
            using(var localPreview=new MarkdownPreview())
            {
                localPreview.LinkRequested+=(sender,args)=>typeof(AssistantControl).GetMethod("NavigateMarkdownLink",Hidden).Invoke(control,new object[]{args});
                localPreview.SetDocument("[Sibling](chapter.cs#L7)",documentDirectory);
                ContentChildren(localPreview.TextView.Document).OfType<Hyperlink>().Single().RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
                Assert(documents.LastPath==sibling&&documents.LastLine==7,"A local Markdown link uses its document folder instead of an existing same-name project file");
                localPreview.SetDocument("[Project](chapter.cs#L3)",null);
                ContentChildren(localPreview.TextView.Document).OfType<Hyperlink>().Single().RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
                Assert(documents.LastPath==System.IO.Path.Combine(directory,"chapter.cs")&&documents.LastLine==3,"A general AI Markdown link still uses the project folder after a local document was viewed");
            }
            Click(control, source, true, source, int.MaxValue);
            Assert(documents.LastLine == 1000000, "Source line navigation has a finite upper bound");
            documents.ThrowOnNavigate = true;
            Click(control, source, true, source, 1);
            Assert(Named<TextBlock>(control, "StatusLabel").Text.Contains("COMException") && !Named<TextBlock>(control, "StatusLabel").Text.Contains("secret-fixture"), "IDE COM failures produce a safe status without the original exception payload");
            documents.ThrowOnNavigate = false;

            Assert(Named<Control>(control, "FastToggle").Visibility == Visibility.Collapsed && Named<Control>(control, "TokenUsageButton").Visibility == Visibility.Collapsed, "The two duplicate model-entry controls are removed from the visible toolbar");
            Assert(Named<Button>(control, "ModelOptionsButton").ToolTip != null && ReferenceEquals(Named<Button>(control, "ModelOptionsButton").ToolTip, Named<Button>(control, "TokenUsageButton").ToolTip), "The single model entry retains the real usage tooltip");
            var send = Named<Button>(control, "GenerateButton"); var magnifier = Descendants(send.Content as DependencyObject).OfType<System.Windows.Shapes.Path>().SingleOrDefault();
            Assert(send.Content is Viewbox && magnifier != null && magnifier.StrokeThickness >= 2.5, "The existing send action uses a bold vector magnifier");
            var viewbox = send.Content as Viewbox;
            Point iconCenter = viewbox == null ? new Point(-100, -100) : viewbox.TranslatePoint(new Point(viewbox.ActualWidth / 2, viewbox.ActualHeight / 2), send);
            Assert(viewbox != null && Math.Abs(iconCenter.X - send.ActualWidth / 2) < 1 && Math.Abs(iconCenter.Y - send.ActualHeight / 2) < 1 && send.Style != null, "The actual magnifier bounds align to the center of the circular send control");
            Assert(Named<Button>(control, "MarkdownDraftButton").ToolTip.ToString().Contains("Markdown") && Named<Button>(control, "RulesMarkdownButton") != null, "Both the prompt and common rules expose Markdown writing and preview");
            foreach (string boxName in new[] { "Instruction", "RulesText" })
                Assert(Named<TextBox>(control, boxName).ContextMenu.Items.OfType<MenuItem>().Any(m => Convert.ToString(m.Header) == "Markdown 작성 · 미리보기"), boxName + " offers context-menu entry to its Markdown editor");
            Assert(control.ContextMenu.Items.OfType<MenuItem>().Count(m => Convert.ToString(m.Header) == "Markdown 문서 작성") == 1, "A standalone Markdown document writer is available once in the general context menu");
            Assert(Named<Button>(control, "AccountLoginPanelButton") != null, "Settings has an explicit entry to the separate account login screen");
            Assert(ContentChildren(Named<FrameworkElement>(control, "SettingsContent")).OfType<AssistantGuideDetails>().Count() == 4, "All four settings sections use compact expandable guide content even while settings are collapsed");
            CheckMessageAppearance(control, directory);
            control.Dispose(); int afterDispose = documents.Count;
            Click(control, source, true, source, 1);
            Assert(documents.Count == afterDispose, "Disposed assistant controls cannot navigate from a stale Markdown event");
        }
        catch (Exception error) { Errors.Add("Unexpected test failure: " + error); }
        finally { if (control != null) control.Dispose(); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CheckMessageAppearance
    /// desc         : 양 제공자·양 테마의 실제 메시지 배경과 로고 노출을 픽셀 차이로 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK032 사용자 메시지 투명·회색 및 가로 띠 로고 가림 회귀.
    ///                2026-10-20 CodexOrig : 최신 Role #15 오른쪽 말풍선 계약으로 교체하고 본문 투명·로고 검사는 유지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void CheckMessageAppearance(AssistantControl control, string directory)
    {
        var scroll = Named<ScrollViewer>(control, "ConversationScroll");
        var logo = Named<Image>(control, "BrandWatermark");
        var providers = Named<TabControl>(control, "ProviderChoice");
        foreach (string provider in new[] { "Claude", "Codex" })
        {
            providers.SelectedValue = provider;
            var chat = (ChatSession)typeof(AssistantControl).GetProperty("ActiveChat", Hidden).GetValue(control, null);
            chat.Messages.Clear();
            chat.Add("user", "사용자 입력 · 오른쪽 회색 말풍선으로 표시");
            for (int line = 0; line < 18; line++) chat.Add("assistant", "본문 표시 " + line + " · 로고를 가리는 가로 바탕을 확인합니다.");
            foreach (bool light in new[] { true, false })
            {
                string label = provider + (light ? "-light" : "-dark");
                Named<ComboBox>(control, "ThemeModeChoice").SelectedValue = light ? "light" : "dark";
                Named<Border>(control, "ToastHost").Visibility = Visibility.Collapsed;
                Named<Border>(control, "EmptyHint").Visibility = Visibility.Collapsed;
                control.Measure(new Size(600, 820)); control.Arrange(new Rect(0, 0, 600, 820)); control.UpdateLayout();
                scroll.ScrollToHome(); control.UpdateLayout();
                var user = Descendants(control).OfType<MarkdownPreview>().Single(x => x.Markdown.StartsWith("사용자 입력", StringComparison.Ordinal));
                var bubble = Descendants(control).OfType<Border>().Single(x => x.Name == "MessageBubble" && Descendants(x).Contains(user));
                Assert(!IsClear(bubble.Background) && ((SolidColorBrush)bubble.Background).Color == ((SolidColorBrush)control.FindResource("AssistantSurfaceBrush")).Color && bubble.HorizontalAlignment == HorizontalAlignment.Right && bubble.CornerRadius == new CornerRadius(12) && bubble.Padding == new Thickness(12, 9, 12, 9) && IsClear(user.Background) && IsClear(user.TextView.Background) && IsClear(user.TextView.Document.Background), label + " user message uses a right aligned themed bubble with transparent inner document");
                Assert(((SolidColorBrush)user.Foreground).Color == ((SolidColorBrush)control.FindResource("AssistantForegroundBrush")).Color, label + " user message uses the theme readable foreground");
                Assert(Descendants(control).OfType<MarkdownPreview>().Where(x => x.Name == "MessageBody").All(x => IsClear(x.Background) && IsClear(x.TextView.Document.Background)), label + " every conversation document keeps a transparent background behind its text");
                var actual = RenderAppearance(control, System.IO.Path.Combine(directory, label + ".png"));
                File.WriteAllLines(System.IO.Path.Combine(directory, label + "-layout.txt"), Descendants(control).OfType<MarkdownPreview>().Where(x => x.Name == "MessageBody").Select(x => x.Markdown + " y=" + x.TransformToAncestor(control).Transform(new Point()).Y + " h=" + x.ActualHeight + " textHeight=" + x.TextView.ActualHeight + " color=" + x.Foreground + " text=" + new TextRange(x.TextView.Document.ContentStart, x.TextView.Document.ContentEnd).Text));
                logo.Visibility = Visibility.Hidden;
                var actualNoLogo = RenderAppearance(control, null);
                scroll.Visibility = Visibility.Hidden; logo.Visibility = Visibility.Visible;
                var baseline = RenderAppearance(control, null);
                logo.Visibility = Visibility.Hidden;
                var baselineNoLogo = RenderAppearance(control, null);
                logo.Visibility = Visibility.Visible; scroll.Visibility = Visibility.Visible;
                Point origin = logo.TransformToAncestor(control).Transform(new Point());
                int expected = 0, visible = 0;
                for (int y = (int)origin.Y; y < origin.Y + logo.ActualHeight; y++)
                    for (int x = (int)origin.X; x < origin.X + logo.ActualWidth; x++)
                    {
                        int offset = (y * 600 + x) * 4;
                        if (PixelDifference(baseline, baselineNoLogo, offset) < 3) continue;
                        expected++;
                        if (PixelDifference(actual, actualNoLogo, offset) >= 2) visible++;
                    }
                double coverage = expected == 0 ? 0 : (double)visible / expected;
                File.AppendAllText(System.IO.Path.Combine(directory, "appearance-pixels.txt"), label + " visible=" + visible + " expected=" + expected + " coverage=" + coverage + Environment.NewLine);
                Assert(expected > 100 && coverage >= .90, label + " watermark remains visible through message row backgrounds (coverage=" + coverage + ")");
            }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RenderAppearance
    /// desc         : 자기 검사 객체만 오프스크린 렌더하고 선택적으로 그림을 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : 실제 IDE/마우스 캡처와 구분하는 합성 메시지 렌더.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static byte[] RenderAppearance(FrameworkElement control, string path)
    {
        control.UpdateLayout();
        control.Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        control.UpdateLayout();
        var bitmap = new RenderTargetBitmap(600, 820, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(control);
        var pixels = new byte[600 * 820 * 4]; bitmap.CopyPixels(pixels, 600 * 4, 0);
        if (path != null) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) encoder.Save(stream); }
        return pixels;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IsClear
    /// desc         : 본문과 문서 바탕의 투명 브러시를 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : 문자 획만 로고를 가리는 정상 동작은 허용한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool IsClear(Brush brush) { return brush == null || brush is SolidColorBrush && ((SolidColorBrush)brush).Color.A == 0; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PixelDifference
    /// desc         : 동일 위치 픽셀의 제공자 로고 기여도를 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : 세 색상 채널의 최대 차이만 비교한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int PixelDifference(byte[] left, byte[] right, int offset) { return Math.Max(Math.Abs(left[offset] - right[offset]), Math.Max(Math.Abs(left[offset + 1] - right[offset + 1]), Math.Abs(left[offset + 2] - right[offset + 2]))); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Click
    /// desc         : 제품 렌더러의 내부 클릭 인자를 구성해 호스트의 실제 탐색 검증 경계에 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 외부 프로세스를 실행하는 정상 웹 링크는 이 검사에서 호출하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static MarkdownLinkEventArgs Click(AssistantControl control, string target, bool local, string path, int line)
    {
        var args = (MarkdownLinkEventArgs)Activator.CreateInstance(typeof(MarkdownLinkEventArgs), Hidden, null, new object[] { target, local, path, line }, null);
        // Match MarkdownPreview.ForwardLink, which assigns this routed event before invoking the host.
        args.RoutedEvent = MarkdownPreview.LinkRequestedEvent;
        typeof(AssistantControl).GetMethod("NavigateMarkdownLink", Hidden).Invoke(control, new object[] { args });
        return args;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Named
    /// desc         : 실제 XAML 이름 범위의 컨트롤을 가져온다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 존재하지 않는 기능을 합성 대체하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static T Named<T>(AssistantControl control, string name) where T : class { return control.FindName(name) as T; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Assert
    /// desc         : 첫 실패 뒤에도 독립 관찰을 계속하고 모든 실패를 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 성공 수와 실패 원문을 분리 기록.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Assert(bool success, string name) { if (success) Checks.Add(name); else Errors.Add(name); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Descendants
    /// desc         : 자기 테스트 객체의 유한한 WPF 시각 트리를 순회한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 다른 앱이나 사용자 화면을 읽지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        if (root == null) yield break; yield return root;
        if (!(root is Visual) && !(root is System.Windows.Media.Media3D.Visual3D)) yield break;
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, index))) yield return child;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ContentChildren
    /// desc         : 렌더 문서의 링크를 실제 논리 트리에서 찾는다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 원시 Markdown 문자열 유무만으로 클릭 연결을 추정하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<DependencyObject> ContentChildren(DependencyObject root)
    {
        yield return root;
        foreach (object item in LogicalTreeHelper.GetChildren(root))
            if (item is DependencyObject) foreach (var child in ContentChildren((DependencyObject)item)) yield return child;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : NavigationFixture
    /// desc         : IDE 탐색 요청만 기록하고 실제 파일 열기·실행은 하지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 기존 문서 조회는 허용하지 않는 최소 테스트 경계.
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class NavigationFixture : IActiveDocumentService, ISourceNavigationService
    {
        private readonly string directory;
        public int Count, LastLine; public string LastPath; public bool ThrowOnNavigate;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigationFixture
        /// desc         : 새 테스트 폴더만 프로젝트 루트로 고정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 개인 프로젝트·외부 경로 조회 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public NavigationFixture(string value) { directory = value; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigateTo
        /// desc         : 성공 탐색을 기록하거나 안전 오류 표시 확인용 COM 실패를 발생시킨다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 실제 IDE와 운영 파일 접근 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void NavigateTo(string filePath, int line) { if (ThrowOnNavigate) throw new COMException("secret-fixture"); Count++; LastPath = filePath; LastLine = line; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetProjectDirectory
        /// desc         : 고정 격리 폴더를 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 상대 링크의 문맥 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string GetProjectDirectory() { return directory; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetActiveDocument
        /// desc         : 링크 탐색 검사에서 문서 캡처가 발생하면 즉시 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : AI 요청과 코드 수정을 테스트가 유발하지 않게 한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public IEditorDocument GetActiveDocument() { throw new InvalidOperationException("No document generation in navigation fixture."); }
    }
}
