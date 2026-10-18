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
            control.Dispose(); int afterDispose = documents.Count;
            Click(control, source, true, source, 1);
            Assert(documents.Count == afterDispose, "Disposed assistant controls cannot navigate from a stale Markdown event");
        }
        catch (Exception error) { Errors.Add("Unexpected test failure: " + error); }
        finally { if (control != null) control.Dispose(); }
    }

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
