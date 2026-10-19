using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexCode.Task024
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GuideReadabilityResult
    /// desc         : 표시하지 않은 안내 구조 검사의 성공·실패와 한계를 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 GUI·실제 GIF 재생 결과로 확대하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class GuideReadabilityResult
    {
        public List<string> Passed { get; } = new List<string>();
        public List<string> Failures { get; } = new List<string>();
        public bool Succeeded { get { return Failures.Count == 0; } }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GuideReadabilityChecks
    /// desc         : 실제 안내 제품 타입의 접이 설명·고정 복귀·원문·설치 기본 취소 구조를 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 Window.Show·브라우저·설치·계정 호출 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class GuideReadabilityChecks
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Run
        /// desc         : 주어진 고정 assembly의 제품 안내를 메모리에서만 만들고 각각 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 실패는 항목별 보존하고 생성한 창만 닫는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static GuideReadabilityResult Run(Assembly assembly, string fixtureRoot)
        {
            var result = new GuideReadabilityResult();
            var host = (FrameworkElement)Activator.CreateInstance(Type(assembly, "AssistantControl"));
            Window dialog = null, cli = null, confirmation = null;
            try
            {
                var help = Type(assembly, "AssistantHelpContent");
                foreach (string name in new[] { "Account", "Model", "Instructions", "Display" })
                {
                    string item = name;
                    Check(result, item + " keeps full details separately from a brief summary", () => {
                        string full = (string)help.GetProperty(item).GetValue(null);
                        string brief = (string)help.GetProperty(item + "Summary").GetValue(null);
                        Assert(full.Length > 400 && full.Contains("\n\n") && brief.Length > 15 && brief.Length <= 160 && !brief.Contains("\n"));
                    });
                }
                var details = (ContentControl)Activator.CreateInstance(Type(assembly, "AssistantGuideDetails"));
                const string fullMarkdown = "# 상세 설명\n\n**보존할 원문**\n\n1. 계정 연결\n2. 확인 후 저장";
                Set(details, "Summary", "연결하고 확인한 뒤 저장하세요."); Set(details, "Markdown", fullMarkdown); Set(details, "MediaKey", "connect");
                var detailBody = (StackPanel)details.Content;
                var expander = detailBody.Children.OfType<Expander>().Single();
                var expandedBody = (StackPanel)expander.Content;
                Check(result, "Settings show a brief summary and start with details collapsed", () => Assert(!expander.IsExpanded && detailBody.Children.OfType<TextBlock>().Single().Text == "연결하고 확인한 뒤 저장하세요."));
                var detailPreview = expandedBody.Children.OfType<ContentControl>().Single();
                Check(result, "Settings preserve exact Markdown text and give scrolling to the parent", () => Assert((string)Get(detailPreview, "Markdown") == fullMarkdown && !(bool)Get(detailPreview, "UseInternalScrolling")));
                Check(result, "Collapsed settings defer their illustration", () => Assert(expandedBody.Children.Count == 1));
                expander.IsExpanded = true;
                Check(result, "Expanding creates an illustration and retains the same full body", () => Assert(expandedBody.Children.Count == 2 && ReferenceEquals(expandedBody.Children[1], detailPreview)));
                var initialImage = expandedBody.Children[0]; expander.IsExpanded = false; expander.IsExpanded = true;
                Check(result, "Repeated expand keeps the existing illustration", () => Assert(ReferenceEquals(initialImage, expandedBody.Children[0])));
                Set(details, "MediaKey", "../../outside");
                Check(result, "Unknown media gives a readable fallback without an image path", () => Assert(!LogicalDescendants(expandedBody.Children[0]).OfType<Image>().Any() && LogicalDescendants(expandedBody.Children[0]).OfType<TextBlock>().Any(x => x.Text.Contains("그림 안내가 없는"))));

                dialog = (Window)Activator.CreateInstance(Type(assembly, "GettingStartedDialog"), new object[] { host, false });
                Check(result, "Common dialog follows semantic colors without showing an HWND", () => Assert(!dialog.IsVisible && ((SolidColorBrush)dialog.Background).Color == ((SolidColorBrush)host.FindResource("AssistantBackgroundBrush")).Color && dialog.ReadLocalValue(Control.BackgroundProperty) != DependencyProperty.UnsetValue));
                var body = (ContentControl)Get(dialog, "Body"); var gallery = (ContentControl)body.Content;
                var overview = (ScrollViewer)gallery.Content;
                var firstCard = ((Panel)overview.Content).Children.OfType<Button>().First();
                object step = firstCard.DataContext;
                string title = (string)Get(step, "Title");
                string originalDetails = String.Join("\n\n", (IEnumerable<string>)Get(step, "Details"));
                Check(result, "Guide card retains its automation name and readable left aligned content", () => Assert(AutomationProperties.GetName(firstCard) == title + " 상세 설명" && firstCard.HorizontalContentAlignment == HorizontalAlignment.Stretch));
                firstCard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var page = gallery.Content as Grid;
                Check(result, "Guide details use a fixed toolbar outside the scroll content", () => Assert(page != null && page.RowDefinitions.Count == 2));
                var back = page.Children.OfType<Button>().Single(); var scroll = page.Children.OfType<ScrollViewer>().Single();
                Check(result, "Back stays in the upper left browser-style row with a clear destination", () => Assert(Grid.GetRow(back) == 0 && back.HorizontalAlignment == HorizontalAlignment.Left && (string)back.Content == "← 뒤로" && (string)back.ToolTip == "안내 목록으로 돌아가기" && back.HorizontalContentAlignment == HorizontalAlignment.Center));
                Check(result, "Only detail content scrolls below the fixed back button", () => Assert(Grid.GetRow(scroll) == 1 && !LogicalDescendants(scroll).Contains(back) && scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled));
                var pagePreview = ((StackPanel)scroll.Content).Children.OfType<ContentControl>().Single(x => x.GetType().Name == "MarkdownPreview");
                Check(result, "Guide full paragraphs are preserved verbatim in Markdown", () => Assert((string)Get(pagePreview, "Markdown") == originalDetails && !(bool)Get(pagePreview, "UseInternalScrolling")));
                Check(result, "Guide detail includes the original procedure image automation name", () => Assert(LogicalDescendants(scroll).OfType<Image>().Any(x => AutomationProperties.GetName(x) == title + " 절차 그림")));
                back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(result, "Back returns to the original guide list", () => Assert(ReferenceEquals(gallery.Content, overview)));
                Check(result, "Existing window cards and footer follow a palette change without recreation", () => {
                    string[] keys = { "AssistantBackgroundBrush", "AssistantForegroundBrush", "AssistantSurfaceBrush", "AssistantBorderBrush" };
                    object[] saved = keys.Select(host.FindResource).ToArray();
                    try
                    {
                        host.Resources[keys[0]] = Brushes.White; host.Resources[keys[1]] = Brushes.Black; host.Resources[keys[2]] = Brushes.WhiteSmoke; host.Resources[keys[3]] = Brushes.DarkGray;
                        Assert(ReferenceEquals(dialog.Background, Brushes.White) && ReferenceEquals(dialog.Foreground, Brushes.Black) && ReferenceEquals(((Border)firstCard.Content).Background, Brushes.WhiteSmoke) && ReferenceEquals(firstCard.Foreground, Brushes.Black) && ReferenceEquals(((Border)Get(dialog, "Footer")).Background, Brushes.WhiteSmoke));
                    }
                    finally { for (int i = 0; i < keys.Length; i++) host.Resources[keys[i]] = saved[i]; }
                });

                var media = Type(assembly, "AssistantGuideMedia");
                var resolve = media.GetMethod("Resolve", BindingFlags.Static | BindingFlags.NonPublic);
                Check(result, "Media resolver rejects paths URLs and unknown keys", () => { foreach (string key in new[] { "../../secret", @"C:\secret", "https://example.invalid/a.gif", "file:///c:/x", "connect.gif", "" }) Assert(resolve.Invoke(null, new object[] { key }) == null); });
                Check(result, "Known media resolves only beneath the assembly guide directory", () => {
                    string basePath = Path.Combine(Path.GetDirectoryName(assembly.Location), "Resources", "GuideMedia") + Path.DirectorySeparatorChar;
                    foreach (string key in new[] { "connect", "request", "review", "guide", "install" })
                    { string path = (string)resolve.Invoke(null, new object[] { key }); if (path != null) Assert(path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase) && File.Exists(path)); }
                });
                Check(result, "Guide path validation rejects a linked directory", () => Assert(!(bool)media.GetMethod("IsPlainPath", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { Path.Combine(fixtureRoot, "linked") })));
                Check(result, "Corrupt GIF retries the guarded PNG as a static illustration with an accurate caption", () => CheckCorruptGifFallback(assembly, fixtureRoot, media));

                var serviceType = assembly.GetType("VS2017.AIAssistant.Services.CliGuideService", true);
                var service = Activator.CreateInstance(serviceType, new object[] { fixtureRoot, null, null });
                cli = (Window)Activator.CreateInstance(Type(assembly, "CliGuideDialog"), new object[] { host, service });
                string raw = File.ReadAllText(Path.Combine(fixtureRoot, "Resources", "CliGuide", "HowToInstallCLI.md"));
                var rawBox = (TextBox)Read(cli, "markdown"); var cliPreview = Read(cli, "markdownPreview"); var cliScroll = (ScrollViewer)Read(cli, "markdownScroll");
                Check(result, "CLI keeps literal source hidden and presents rendered Markdown", () => Assert(rawBox.IsReadOnly && rawBox.Text == raw && rawBox.Visibility == Visibility.Collapsed && (string)Get(cliPreview, "Markdown") == raw && ReferenceEquals(cliScroll.Content, cliPreview)));
                Check(result, "CLI Markdown only permits images under its existing guide resource base", () => Assert((string)Get(cliPreview, "AssetBaseDirectory") == (string)Get(service, "AssetsDirectory") && !(bool)Get(cliPreview, "UseInternalScrolling")));
                Check(result, "CLI fallback and its surrounding surface follow palette changes", () => {
                    object background = host.FindResource("AssistantBackgroundBrush"), foreground = host.FindResource("AssistantForegroundBrush");
                    try
                    { host.Resources["AssistantBackgroundBrush"] = Brushes.White; host.Resources["AssistantForegroundBrush"] = Brushes.Black; Assert(ReferenceEquals(((Grid)Read(cli, "viewerHost")).Background, Brushes.White) && ReferenceEquals(rawBox.Background, Brushes.White) && ReferenceEquals(rawBox.Foreground, Brushes.Black) && ReferenceEquals(cli.Background, Brushes.White)); }
                    finally { host.Resources["AssistantBackgroundBrush"] = background; host.Resources["AssistantForegroundBrush"] = foreground; }
                });
                cli.GetType().GetMethod("ShowFallback", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cli, new object[] { "fixture fallback" });
                Check(result, "Text fallback opens no browser and retains exact source", () => Assert(Read(cli, "browser") == null && cliScroll.Visibility == Visibility.Visible && (string)Get(cliPreview, "Markdown") == raw));
                var footer = (ContentControl)Get(cli, "FooterContent");
                Check(result, "CLI overview is collapsed while its full original text remains available", () => Assert(footer.Content is Expander && !((Expander)footer.Content).IsExpanded && ((TextBlock)((Expander)footer.Content).Content).Text == (string)serviceType.GetField("Overview").GetRawConstantValue()));
                var actions = ((WrapPanel)Get(cli, "Actions")).Children.OfType<Button>().ToArray();
                var install = actions.Single(x => (string)x.Content == "⇩ CLI 설치"); var read = actions.Single(x => (string)x.Content == "↻ PDF 보기");
                Check(result, "Guide and installer use different action styles with original labels", () => Assert(install.Style != null && read.Style != null && !ReferenceEquals(install.Style, read.Style) && actions.Any(x => (string)x.Content == "¶ 텍스트 안내")));
                var confirmationType = cli.GetType().GetNestedType("InstallationConfirmationDialog", BindingFlags.NonPublic);
                confirmation = (Window)Activator.CreateInstance(confirmationType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { host }, null);
                var choices = ((WrapPanel)Get(confirmation, "Actions")).Children.OfType<Button>().ToArray();
                Check(result, "Installation still requires explicit approval and defaults to cancel", () => Assert(choices.Length == 2 && choices.Single(x => x.IsDefault).IsCancel && choices.Single(x => !x.IsCancel).Content.ToString() == "⇩ 설치" && !confirmation.IsVisible));
                Check(result, "No memory inspection starts a browser or installer", () => Assert(Read(cli, "browser") == null && !(bool)Get(service, "IsInstallationRunning") && !cli.IsVisible));
            }
            catch (Exception error) { result.Failures.Add("Setup/completion: " + error.GetBaseException().Message); }
            finally
            {
                if (confirmation != null) confirmation.Close(); if (cli != null) cli.Close(); if (dialog != null) dialog.Close();
                var disposable = host as IDisposable; if (disposable != null) disposable.Dispose();
            }
            return result;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckCorruptGifFallback
        /// desc         : 격리된 제품 사본의 손상 GIF와 정상 PNG를 조합해 정지 그림 대체를 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 원본 제품 자산을 변경하지 않고 fixture 파일도 finally에서 복원.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckCorruptGifFallback(Assembly assembly, string fixtureRoot, Type media)
        {
            string product = Path.GetDirectoryName(assembly.Location);
            Assert(String.Equals(product, Path.Combine(Path.GetDirectoryName(fixtureRoot), "product"), StringComparison.OrdinalIgnoreCase));
            string directory = (string)media.GetMethod("GetAssetDirectory", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Assert(directory != null);
            string gifPath = Path.Combine(directory, "01-connect.gif"), pngPath = Path.Combine(directory, "01-connect.png");
            byte[] originalGif = File.Exists(gifPath) ? File.ReadAllBytes(gifPath) : null;
            byte[] originalPng = File.Exists(pngPath) ? File.ReadAllBytes(pngPath) : null;
            FrameworkElement illustration = null;
            try
            {
                File.WriteAllBytes(gifPath, new byte[] { 71, 73, 70, 56, 57, 97, 0 });
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 32, 64, 128, 255 }, 4)));
                using (var stream = new MemoryStream()) { encoder.Save(stream); File.WriteAllBytes(pngPath, stream.ToArray()); }
                Assert((string)media.GetMethod("Resolve", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "connect" }) == gifPath);
                illustration = (FrameworkElement)media.GetMethod("CreateIllustration", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "connect", "fixture", null });
                var image = LogicalDescendants(illustration).OfType<Image>().SingleOrDefault();
                Assert(image != null && image.Source != null && image.GetType().Name == "MarkdownAnimatedImage");
                Assert(String.Equals((string)Get(image, "SourcePath"), pngPath, StringComparison.OrdinalIgnoreCase));
                Assert((int)Get(image, "FrameCount") == 1 && !(bool)Get(image, "IsAnimationRunning") && !image.IsLoaded);
                Assert(LogicalDescendants(illustration).OfType<TextBlock>().Any(x => x.Text == "제품 UI 합성 예시 · 실제 로그인·설치 결과가 아닙니다."));
            }
            finally
            {
                if (illustration != null)
                    foreach (var image in LogicalDescendants(illustration).OfType<IDisposable>()) image.Dispose();
                if (originalGif == null) File.Delete(gifPath); else File.WriteAllBytes(gifPath, originalGif);
                if (originalPng == null) File.Delete(pngPath); else File.WriteAllBytes(pngPath, originalPng);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LogicalDescendants
        /// desc         : 표시나 메시지 루프 없이 논리 트리의 안내 자식을 열거한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 ScrollViewer 내부까지 구조 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
        {
            yield return root;
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            { var item = child as DependencyObject; if (item != null) foreach (var descendant in LogicalDescendants(item)) yield return descendant; }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Type
        /// desc         : 지정된 assembly 안의 제품 안내 타입만 찾는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 공유 bin 자동 탐색 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Type Type(Assembly assembly, string name) { return assembly.GetType("VS2017.AIAssistant.Views." + name, true); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Get
        /// desc         : 제품의 공개 읽기 속성을 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 상태 변경 없이 값 조회.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static object Get(object value, string name) { return value.GetType().GetProperty(name).GetValue(value); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Set
        /// desc         : 안내 표시용 의존 속성만 synthetic 값으로 지정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 파일이나 설정 저장 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Set(object value, string name, object content) { value.GetType().GetProperty(name).SetValue(value, content); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Read
        /// desc         : 안내창의 수명 경계를 private 필드로 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 실제 실행 메서드 호출 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static object Read(object value, string name) { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Assert
        /// desc         : 구조 계약 위반을 검사 실패로 남긴다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 원문 실패 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Assert(bool condition) { if (!condition) throw new InvalidOperationException("Guide contract assertion failed."); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : 각각의 의미 있는 회귀 검사 결과를 수집한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 하나의 실패로 나머지 결과를 숨기지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Check(GuideReadabilityResult result, string name, Action action)
        { try { action(); result.Passed.Add(name); } catch (Exception error) { result.Failures.Add(name + ": " + error.GetBaseException().Message); } }
    }
}
