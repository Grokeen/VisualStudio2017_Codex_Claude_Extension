using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ArtifactProductChecks
/// desc         : 실제 제품의 대화 DataTemplate에서 생성된 파일 메뉴를 자기 WPF 창에서 검사한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK061 실제 DLL·격리 설정·합성 답변·복사/선택 명령과 팔레트 계약.
/// </summary>
/// ---------------------------------------------------------------------
public static class ArtifactProductChecks
{
    public static readonly List<string> Checks = new List<string>();
    public static readonly List<string> Errors = new List<string>();
    public static readonly List<string> PaletteObservations = new List<string>();
    public static int WindowsShown, MenusOpened;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 실제 제품 클래스의 메시지 렌더·메뉴·작성기 경로를 팔레트별로 관측한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : OS 저장창·클립보드·외부 AI·사용자 IDE 입력을 사용하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run(string output)
    {
        foreach (string palette in new[] { "vs-light", "vs-dark", "kakao", "dracula" })
        {
            AssistantControl control = null; Window window = null; ContextMenu openMenu = null;
            try
            {
                string folder = Path.Combine(output, palette); Directory.CreateDirectory(folder);
                Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", Path.Combine(folder, "settings.json"));
                Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", Path.Combine(folder, "workspace"));
                new AssistantSettingsStore().Save(new AssistantSettings {
                    HideWelcome = true, ShowHistory = false, EnableCompletionNotifications = false,
                    ThemeMode = palette == "vs-light" || palette == "kakao" ? "light" : "dark",
                    VisualThemePreset = palette.StartsWith("vs-", StringComparison.Ordinal) ? "vs" : palette
                });
                control = new AssistantControl();
                typeof(AssistantControl).GetField("scmLookupStarted", Private).SetValue(control, true);
                var chat = (ChatSession)typeof(AssistantControl).GetProperty("ActiveChat", Private).GetValue(control, null);
                chat.Messages.Clear();
                var single = new ChatMessage { Role = "assistant", Text = "합성 답변\n```csharp\n// 한글\nvar answer = 42;\n```\n설명 끝" };
                var multiple = new ChatMessage { Role = "assistant", Text = "```cs\nvar a = 1;\n```\n```cs\nvar b = 2;\n```" };
                var user = new ChatMessage { Role = "user", Text = "합성 사용자 입력" };
                var system = new ChatMessage { Role = "system", Text = "합성 시스템 안내" };
                foreach (var message in new[] { single, multiple, user, system }) chat.Messages.Add(message);
                window = new Window { Content = control, Title = "TASK061 isolated product menu " + palette, Width = 760, Height = 900, ShowActivated = false, ShowInTaskbar = false };
                window.Show(); WindowsShown++; Pump(); window.UpdateLayout(); Pump();
                var items = (ItemsControl)control.FindName("ConversationMessages");
                var previews = Descendants(items).OfType<MarkdownPreview>().ToList();
                Check(palette + " actual DataTemplate loaded four synthetic message previews", previews.Count == 4);
                var answerPreview = previews.Single(view => Object.ReferenceEquals(view.DataContext, single));
                var singleMenu = answerPreview.TextView.ContextMenu;
                Check(palette + " actual PreviewLoaded wired artifact menu exactly once", singleMenu != null && singleMenu.Items.OfType<MenuItem>().Count(item => Header(item).Contains("새 파일로 만들기")) == 2);
                // 실 메뉴 생성 수명도 통과시킨다. 문서 파일 만들기 항목은 클릭하지 않는다.
                Open(singleMenu, answerPreview.TextView); openMenu = singleMenu;
                var all = singleMenu.Items.OfType<MenuItem>().Single(item => Header(item).StartsWith("답변 전체", StringComparison.Ordinal));
                var code = singleMenu.Items.OfType<MenuItem>().Single(item => Header(item).StartsWith("코드 블록만", StringComparison.Ordinal));
                Check(palette + " single fence assistant exposes and enables both actions", all.Visibility == Visibility.Visible && all.IsEnabled && code.Visibility == Visibility.Visible && code.IsEnabled);
                var copy = singleMenu.Items.OfType<MenuItem>().Single(item => Header(item) == "복사");
                var select = singleMenu.Items.OfType<MenuItem>().Single(item => Header(item) == "모두 선택");
                Check(palette + " copy and select all preserve original commands and rich text target", copy.Command == ApplicationCommands.Copy && select.Command == ApplicationCommands.SelectAll && Object.ReferenceEquals(copy.CommandTarget, answerPreview.TextView) && Object.ReferenceEquals(select.CommandTarget, answerPreview.TextView));
                ApplicationCommands.SelectAll.Execute(null, answerPreview.TextView);
                Check(palette + " select all still selects rendered response and Copy can execute", !String.IsNullOrEmpty(answerPreview.TextView.Selection.Text) && ApplicationCommands.Copy.CanExecute(null, answerPreview.TextView));
                Check(palette + " Markdown source copy and settings help preserved", singleMenu.Items.OfType<MenuItem>().Any(item => Header(item) == "Markdown 원문 복사") && singleMenu.Items.OfType<MenuItem>().Any(item => Header(item) == "설정") && singleMenu.Items.OfType<MenuItem>().Any(item => Header(item) == "사용 안내"));
                var background = singleMenu.Background as SolidColorBrush;
                var foreground = all.Foreground as SolidColorBrush;
                var expectedBackground = control.TryFindResource("AssistantPopupBackgroundBrush") as SolidColorBrush;
                var expectedForeground = control.TryFindResource("AssistantToneEEEEEEBrush") as SolidColorBrush;
                Check(palette + " popup and new item inherit actual palette resources", background != null && foreground != null && expectedBackground != null && expectedForeground != null && background.Color == expectedBackground.Color && foreground.Color == expectedForeground.Color);
                Check(palette + " enabled artifact item has readable foreground contrast", background != null && foreground != null && Contrast(background.Color, foreground.Color) >= 4.5);
                Check(palette + " artifact items use shared menu template and padding", all.Template != null && code.Template != null && all.Template == copy.Template && all.Padding == copy.Padding && all.Style == copy.Style);
                PaletteObservations.Add(palette + ": popup=" + (background == null ? "null" : background.Color.ToString()) + ", item=" + (foreground == null ? "null" : foreground.Color.ToString()));
                Close(singleMenu); openMenu = null;
                foreach (ChatMessage message in new[] { multiple, user, system })
                {
                    var preview = previews.Single(view => Object.ReferenceEquals(view.DataContext, message));
                    ContextMenu menu = preview.TextView.ContextMenu;
                    Open(menu, preview.TextView); openMenu = menu;
                    var whole = menu.Items.OfType<MenuItem>().Single(item => Header(item).StartsWith("답변 전체", StringComparison.Ordinal));
                    var block = menu.Items.OfType<MenuItem>().Single(item => Header(item).StartsWith("코드 블록만", StringComparison.Ordinal));
                    if (message == multiple)
                        Check(palette + " multiple fence answer permits whole only", whole.Visibility == Visibility.Visible && whole.IsEnabled && block.Visibility == Visibility.Visible && !block.IsEnabled);
                    else
                        Check(palette + " " + message.Role + " never exposes answer artifact actions", whole.Visibility == Visibility.Collapsed && block.Visibility == Visibility.Collapsed && !whole.IsEnabled && !block.IsEnabled);
                    Close(menu); openMenu = null;
                }
                string extracted, extension;
                bool extractedOk = AssistantArtifactWriter.TryExtractSingleCodeBlock(single.Text, out extracted, out extension);
                Check(palette + " product writer extracts only exact code and safe extension", extractedOk && extracted == "// 한글\nvar answer = 42;\n" && extension == ".cs");
                string canceled = Path.Combine(folder, "canceled.md");
                Check(palette + " canceled creation API performs no write", !AssistantArtifactWriter.TryCreateNew(false, canceled, single.Text) && !File.Exists(canceled));
                string artifact = Path.Combine(folder, "approved.cs");
                bool created = AssistantArtifactWriter.TryCreateNew(true, artifact, extracted);
                Check(palette + " approved product writer creates exact requested new code file", created && File.ReadAllText(artifact, new System.Text.UTF8Encoding(false, true)) == extracted);
                bool refused = false;
                try { AssistantArtifactWriter.TryCreateNew(true, artifact, "replacement"); } catch (IOException) { refused = true; }
                Check(palette + " product writer refuses overwrite and preserves approved artifact", refused && File.ReadAllText(artifact) == extracted);
                control.Dispose();
                singleMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
                Check(palette + " disposed product host hides and disables file actions", all.Visibility == Visibility.Collapsed && code.Visibility == Visibility.Collapsed && !all.IsEnabled && !code.IsEnabled);
            }
            catch (Exception error) { Errors.Add(palette + " harness/product exception: " + error); }
            finally { if (openMenu != null) Close(openMenu); if (control != null) control.Dispose(); if (window != null) window.Close(); Pump(); }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Header
    /// desc         : 표준 메뉴의 사용자 표시 이름을 비교한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 번역된 실제 항목을 검사하며 고정 인덱스를 가정하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Header(MenuItem item) { return Convert.ToString(item.Header); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Open
    /// desc         : 자기 창의 실제 ContextMenu 수명 이벤트와 공통 템플릿을 활성화한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : OS 마우스·키보드 주입 없이 WPF API만 사용한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Open(ContextMenu menu, UIElement placement)
    { if (menu == null) throw new InvalidOperationException("Actual preview menu missing."); menu.PlacementTarget = placement; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true; MenusOpened++; Pump(); menu.ApplyTemplate(); menu.UpdateLayout(); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Close
    /// desc         : 자기 fixture 메뉴를 닫고 이벤트 처리를 마친다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 사용자 창이나 다른 작업의 메뉴는 참조하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Close(ContextMenu menu) { menu.IsOpen = false; Pump(); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Descendants
    /// desc         : 표시된 제품 대화 템플릿 아래에서 실제 MarkdownPreview를 찾는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 별도의 유사 UI나 stub을 생성하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 각 실제 제품 계약의 성공과 실패를 별도 목록에 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실패 검사를 제거하거나 성공으로 바꾸지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, bool passed) { if (passed) Checks.Add(name); else Errors.Add(name); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Contrast
    /// desc         : 불투명 메뉴 배경과 본문의 상대 휘도 대비를 계산한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실제 팔레트 리소스 값만 관찰한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static double Contrast(Color a, Color b) { double x = Luminance(a), y = Luminance(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Luminance
    /// desc         : sRGB 색의 선형 상대 휘도를 반환한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : alpha 값으로 대비를 과장하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static double Luminance(Color color) { return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Linear
    /// desc         : 하나의 sRGB 채널을 선형 값으로 변환한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 고정 WCAG 휘도 수식을 사용한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static double Linear(byte channel) { double value = channel / 255.0; return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pump
    /// desc         : 자기 WPF 창의 배치·메뉴 이벤트를 짧게 처리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 외부 UI 입력이나 임의 대기를 사용하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Pump()
    { var frame = new DispatcherFrame(); var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(80) }; timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
}
