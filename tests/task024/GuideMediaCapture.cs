using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VS2017.AIAssistant.Services;
using VS2017.AIAssistant.Views;

namespace CodexCode.Task024
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GuideMediaCapture
    /// desc         : 실제 제품 WPF 요소에 합성 문구만 넣어 안내용 PNG 프레임을 렌더한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 마우스·인증·네트워크·설치·Window.Show 호출 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class GuideMediaCapture
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Run
        /// desc         : 예약된 STA 호스트에서만 다섯 시나리오의 실제 UI 요소를 새 폴더에 캡처한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 개인 설정과 분리하며 안내용 합성을 실제 작업 결과로 표시하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void Run(string outputDir)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) throw new InvalidOperationException("안내 캡처는 예약된 STA에서 실행하세요.");
            string root = Path.GetFullPath(outputDir);
            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new IOException("빈 캡처 폴더를 지정하세요.");
            Directory.CreateDirectory(root);
            var context = new CaptureContext(root);
            string priorSettings = Environment.GetEnvironmentVariable("VS2017_AI_SETTINGS_PATH");
            string priorWorkspace = Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH");
            string priorDiscovery = Environment.GetEnvironmentVariable("VS2017_AI_DISABLE_RUNTIME_DISCOVERY");
            AssistantControl host = null; GettingStartedDialog guide = null; CliGuideDialog cli = null; Window confirmation = null; MarkdownEditor editor = null;
            try
            {
                string settings = Path.Combine(root, "fixture-settings.json");
                File.WriteAllText(settings, "{\"HideWelcome\":true,\"FontSize\":14,\"ThemeMode\":\"dark\"}", new UTF8Encoding(false));
                Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", settings);
                Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", Path.Combine(root, "fixture-workspace"));
                Environment.SetEnvironmentVariable("VS2017_AI_DISABLE_RUNTIME_DISCOVERY", "1");
                host = new AssistantControl();
                context.Resources = host.Resources;

                var account = new AssistantGuideDetails { Summary = AssistantHelpContent.AccountSummary, Markdown = AssistantHelpContent.Account, MediaKey = "", Resources = host.Resources };
                var accountExpander = ((StackPanel)account.Content).Children.OfType<Expander>().Single();
                context.Capture("01-connect", 1, "1  AI와 실행 파일을 먼저 준비", account, "아직 로그인하거나 연결을 확인하지 않은 합성 예제입니다.");
                accountExpander.IsExpanded = true;
                context.Capture("01-connect", 2, "2  상세 설명을 펼쳐 순서 확인", account, "설치 → 로그인 → 연결 확인 → 설정 저장은 서로 다른 단계입니다.");
                var connectSteps = new AssistantGuideDetails { Summary = "본인의 공식 CLI에서 계정 인증을 마친 뒤 확장으로 돌아옵니다.", Markdown = "## 계정 연결 순서\n\n1. **계정 연결**을 선택합니다.\n2. 공식 CLI 또는 브라우저에서 본인 계정으로 로그인합니다.\n3. 확장으로 돌아와 **연결 확인**을 선택합니다.\n\n> 이 화면은 인증 창을 실행하지 않는 합성 안내입니다.", Resources = host.Resources };
                ((StackPanel)connectSteps.Content).Children.OfType<Expander>().Single().IsExpanded = true;
                context.Capture("01-connect", 3, "3  인증은 공식 창에서 직접 진행", connectSteps, "암호·API 키·토큰을 공통 규칙이나 대화에 넣지 않습니다.");
                connectSteps.Markdown = "## 연결 결과와 저장\n\n1. **연결 확인**의 실제 결과를 읽습니다.\n2. 성공을 확인한 경우 **설정 저장**을 선택합니다.\n3. 경로·로그인·네트워크 오류는 실제 원인부터 해결합니다.\n\n> 안내를 읽거나 로그인 창을 여는 것만으로 연결 성공이 되지는 않습니다.";
                context.Capture("01-connect", 4, "4  확인한 다음 설정 저장", connectSteps, "합성 예제이며 계정 상태나 이용 권한을 판정하지 않습니다.");

                var composer = Find(host, "ComposerSurface");
                var request = (TextBox)Find(host, "Instruction");
                context.Capture("02-request", 1, "1  현재 파일과 요청 범위 확인", composer, "실제 파일·첨부·AI 호출 없이 입력 영역만 보여 줍니다.");
                request.Text = "선택한 메서드의 빈 입력 오류를 수정하고 공개 API는 유지해 주세요.\n변경 이유와 실행한 테스트도 설명해 주세요.";
                context.Capture("02-request", 2, "2  원하는 결과와 유지 조건 입력", composer, "Enter 전송 · Ctrl+Enter 줄바꿈 — 여기서는 전송하지 않습니다.");
                editor = new MarkdownEditor { Text = "# 요청 예제\n\n- **대상**: 선택한 메서드\n- **변경**: 빈 입력 처리\n- **유지**: 공개 API\n\n```csharp\nif (value == null) return;\n```", Resources = host.Resources };
                editor.ViewMode = 1;
                context.Capture("02-request", 3, "3  Markdown으로 요청을 정리", editor, "원문을 편집하고 미리보기로 서식을 확인할 수 있습니다.");
                editor.ViewMode = 2; editor.RefreshPreview();
                context.Capture("02-request", 4, "4  미리보기 확인 후 사용자가 요청", editor, "합성 입력만 사용하며 실제 AI 요청은 실행하지 않습니다.");

                var reviewTabs = (TabControl)Find(host, "ReviewTabs");
                ((TextBox)Find(host, "OriginalPreview")).Text = "// 합성 소스\npublic string Format(string value)\n{\n    return value.Trim();\n}";
                ((TextBox)Find(host, "ProposedPreview")).Text = "// 합성 수정안\npublic string Format(string value)\n{\n    return (value ?? string.Empty).Trim();\n}";
                var colored = (RichTextBox)Find(host, "ColoredDiffPreview");
                typeof(AssistantControl).GetMethod("RenderColoredDiff", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(host, new object[] {
                    "--- a/Sample.cs\n+++ b/Sample.cs\n@@ -3 +3 @@\n-    return value.Trim();\n+    return (value ?? string.Empty).Trim();", "합성 예제 · 실제 변경 기록이 아닙니다." });
                reviewTabs.SelectedIndex = 0;
                context.Capture("03-review", 1, "1  원본과 수정안의 차이 확인", reviewTabs, "합성 코드입니다. 실제 프로젝트 파일이나 편집기 변경은 없습니다.");
                reviewTabs.SelectedIndex = 1;
                context.Capture("03-review", 2, "2  원본 탭에서 기존 동작 읽기", reviewTabs, "승인하기 전에 제거되는 동작과 오류 처리를 함께 확인합니다.");
                reviewTabs.SelectedIndex = 2;
                context.Capture("03-review", 3, "3  수정안 탭에서 변경 내용 읽기", reviewTabs, "이 예제는 승인·적용·저장 버튼을 실행하지 않습니다.");
                var reviewSummary = new AssistantGuideDetails { Summary = "비교 → 사용자 승인 → 편집기에 적용 → 직접 저장 순서입니다.", Markdown = "## 검토 뒤 확인할 것\n\n- 변경 범위와 공개 API를 읽습니다.\n- 필요한 빌드·테스트의 실제 결과를 확인합니다.\n- 동의한 수정안만 **승인 후 적용**합니다.\n- 편집기 내용을 확인한 다음 **적용 내용 저장**을 선택합니다.\n\n> 전체 access의 CLI 직접 변경은 별도 실행 흐름입니다.", Resources = host.Resources };
                ((StackPanel)reviewSummary.Content).Children.OfType<Expander>().Single().IsExpanded = true;
                context.Capture("03-review", 4, "4  확인 뒤 승인과 저장은 직접 선택", reviewSummary, "저장·적용 성공을 흉내 내지 않는 절차 안내입니다.");

                guide = new GettingStartedDialog(host, false);
                var gallery = (ContentControl)guide.Body.Content; guide.Body.Content = null;
                var overview = (ScrollViewer)gallery.Content;
                context.Capture("04-guide", 1, "1  사용 안내에서 필요한 주제 선택", gallery, "실제 안내 카드입니다. 카드의 상세 보기를 선택할 수 있습니다.");
                var card = ((Panel)overview.Content).Children.OfType<Button>().First(); card.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var page = (Grid)gallery.Content; var guideScroll = page.Children.OfType<ScrollViewer>().Single();
                context.Capture("04-guide", 2, "2  상세 설명과 단계 그림 읽기", gallery, "실제 제품 안내의 우측 상단 복귀 버튼은 스크롤 밖에 있습니다.");
                guideScroll.ScrollToVerticalOffset(230);
                context.Capture("04-guide", 3, "3  본문을 스크롤해도 복귀는 고정", gallery, "이 합성 프레임은 상세 본문만 아래로 이동한 모습입니다.");
                page.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                context.Capture("04-guide", 4, "4  우측 상단에서 안내 목록으로 복귀", gallery, "목록으로 돌아온 뒤 다른 주제의 설명을 열 수 있습니다.");

                string installRoot = Path.Combine(root, "fixture-cli"); string installAssets = Path.Combine(installRoot, "Resources", "CliGuide"); Directory.CreateDirectory(installAssets);
                File.WriteAllText(Path.Combine(installAssets, CliGuideService.MarkdownFileName), "# CLI 설치와 시작\n\n1. 동봉된 **PDF 설명서** 또는 **텍스트 안내**를 읽습니다.\n2. 필요한 경우 **CLI 설치**를 선택합니다.\n3. 설치 확인 화면의 내용을 읽고 설치 또는 취소를 직접 선택합니다.\n\n> 이 화면은 합성 예제이며 설치기를 실행하지 않습니다.", new UTF8Encoding(false));
                cli = new CliGuideDialog(host, new CliGuideService(installRoot));
                cli.GetType().GetMethod("ShowFallback", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cli, new object[] { "합성 예제 · 실제 설치는 실행하지 않습니다." });
                var cliBody = (FrameworkElement)cli.Body.Content; cli.Body.Content = null;
                context.Capture("05-install", 1, "1  PDF 또는 Markdown 설명서 읽기", cliBody, "실제 CliGuideDialog의 오프라인 Markdown 대체 화면입니다.");
                var cliActions = cli.Actions; Detach(cliActions);
                context.Capture("05-install", 2, "2  안내 버튼과 설치 버튼 구분", cliActions, "설치 버튼은 별도 색상이며 사용자 확인 없이는 실행하지 않습니다.");
                var confirmType = typeof(CliGuideDialog).GetNestedType("InstallationConfirmationDialog", BindingFlags.NonPublic);
                confirmation = (Window)Activator.CreateInstance(confirmType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { host }, null);
                var confirmationDialog = (AssistantDialog)confirmation;
                var confirmBody = (FrameworkElement)confirmationDialog.Body.Content; confirmationDialog.Body.Content = null;
                context.Capture("05-install", 3, "3  실행 전에 변경 내용과 조건 확인", confirmBody, "다운로드·사용자 PATH 변경·별도 로그인을 먼저 읽습니다.");
                var confirmActions = confirmationDialog.Actions; Detach(confirmActions);
                context.Capture("05-install", 4, "4  기본 선택은 취소 · 설치는 명시 선택", confirmActions, "실제 확인 UI의 기본 취소 상태입니다. 버튼을 실행하지 않았습니다.");
                context.WriteManifest();
            }
            finally
            {
                if (confirmation != null) confirmation.Close(); if (cli != null) cli.Close(); if (guide != null) guide.Close();
                if (editor != null) editor.Dispose();
                if (host != null) { var disposable = (object)host as IDisposable; if (disposable != null) disposable.Dispose(); }
                Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", priorSettings);
                Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", priorWorkspace);
                Environment.SetEnvironmentVariable("VS2017_AI_DISABLE_RUNTIME_DISCOVERY", priorDiscovery);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Find
        /// desc         : 실제 제품의 명명된 WPF 요소를 찾고 누락 시 캡처를 명확히 중단한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 유사 화면을 임의로 만들어 성공 처리하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static FrameworkElement Find(AssistantControl host, string name)
        { var element = host.FindName(name) as FrameworkElement; if (element == null) throw new InvalidOperationException("제품 요소 누락: " + name); return element; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Detach
        /// desc         : 캡처 전용으로 생성한 요소만 기존 메모리 부모에서 분리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 열린 사용자 창이나 원본 파일 접근 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Detach(FrameworkElement element)
        {
            var parent = element.Parent;
            var panel = parent as Panel; if (panel != null) { panel.Children.Remove(element); return; }
            var decorator = parent as Decorator; if (decorator != null) { decorator.Child = null; return; }
            var content = parent as ContentControl; if (content != null && ReferenceEquals(content.Content, element)) { content.Content = null; return; }
            if (parent != null) throw new InvalidOperationException("지원하지 않는 캡처 부모: " + parent.GetType().Name);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureContext
        /// desc         : 공통 캡션·범위 제한·메타데이터를 유지하며 PNG만 새로 생성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 실제 화면 캡처나 개인 정보를 포함하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class CaptureContext
        {
            private readonly string output;
            private readonly List<object> frames = new List<object>();
            internal ResourceDictionary Resources { get; set; }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : CaptureContext
            /// desc         : 확인된 새 캡처 경로를 보관한다.
            /// author       : CodexCode
            /// create date  : 2026-10-19
            /// update date  : 2026-10-19 CodexCode : TASK-024 고정 다섯 하위 폴더만 생성.
            /// </summary>
            /// ---------------------------------------------------------------------
            internal CaptureContext(string output) { this.output = output; }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : Capture
            /// desc         : 실제 WPF 요소를 고정 크기에 렌더하고 프레임의 합성 범위를 명시한다.
            /// author       : CodexCode
            /// create date  : 2026-10-19
            /// update date  : 2026-10-19 CodexCode : TASK-024 임의 사용자 화면·Window.Show·명령 실행 없음.
            /// </summary>
            /// ---------------------------------------------------------------------
            internal void Capture(string scenario, int index, string title, FrameworkElement content, string note)
            {
                Detach(content);
                // 반복 GIF 안에 같은 GIF나 자산 누락 안내를 중첩하지 않는 합성 설명 예제이다.
                var compact = content as AssistantGuideDetails;
                if (compact != null && string.IsNullOrEmpty(compact.MediaKey))
                {
                    var placeholder = typeof(AssistantGuideDetails).GetField("illustration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(compact) as FrameworkElement;
                    if (placeholder != null) placeholder.Visibility = Visibility.Collapsed;
                }
                var page = new Grid { Width = 600, Height = 420, Background = new SolidColorBrush(Color.FromRgb(23, 23, 23)), ClipToBounds = true, Resources = Resources ?? content.Resources };
                page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
                page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
                page.RowDefinitions.Add(new RowDefinition());
                page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
                page.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(37, 62, 70)), Child = new TextBlock { Text = "합성 예제 · 클릭하지 않고 절차 확인", Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 16, 0) } });
                var heading = new TextBlock { Text = title, Foreground = Brushes.Gainsboro, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(16, 8, 16, 6), TextWrapping = TextWrapping.Wrap }; Grid.SetRow(heading, 1); page.Children.Add(heading);
                var viewport = new Border { Child = content, Padding = new Thickness(14, 8, 14, 8), ClipToBounds = true, Background = new SolidColorBrush(Color.FromRgb(29, 29, 29)) }; Grid.SetRow(viewport, 2); page.Children.Add(viewport);
                var footer = new TextBlock { Text = note, Foreground = Brushes.Silver, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 10, 16, 6) }; Grid.SetRow(footer, 3); page.Children.Add(footer);
                page.SetValue(TextElement.FontFamilyProperty, new FontFamily("Malgun Gothic")); page.SetValue(TextElement.FontSizeProperty, 13.0); page.SetValue(TextElement.ForegroundProperty, Brushes.Gainsboro);
                page.Measure(new Size(600, 420)); page.Arrange(new Rect(0, 0, 600, 420)); page.UpdateLayout();
                var bitmap = new RenderTargetBitmap(600, 420, 96, 96, PixelFormats.Pbgra32); bitmap.Render(page);
                string directory = Path.Combine(output, scenario); Directory.CreateDirectory(directory);
                string file = "frame-" + index.ToString("00") + ".png"; string path = Path.Combine(directory, file);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) encoder.Save(stream);
                frames.Add(new { RelativePath = scenario + "/" + file, Scenario = scenario, Frame = index, Caption = title, UiType = content.GetType().FullName, Width = 600, Height = 420, LiveAccount = false, InstallerStarted = false, Network = false });
                viewport.Child = null;
            }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : WriteManifest
            /// desc         : 사용한 제품 assembly와 합성 프레임의 한계를 JSON에 기록한다.
            /// author       : CodexCode
            /// create date  : 2026-10-19
            /// update date  : 2026-10-19 CodexCode : TASK-024 실제 인증·설치·AI 결과 증거와 명확히 구분.
            /// </summary>
            /// ---------------------------------------------------------------------
            internal void WriteManifest()
            {
                var manifest = new { ProductAssemblyPath = typeof(AssistantControl).Assembly.Location, Synthetic = true, WindowShown = false, NativePointerUsed = false, AiInvoked = false, InstallerStarted = false, Network = false, Frames = frames };
                File.WriteAllText(Path.Combine(output, "capture-manifest.json"), new JavaScriptSerializer().Serialize(manifest), new UTF8Encoding(false));
            }
        }
    }
}
