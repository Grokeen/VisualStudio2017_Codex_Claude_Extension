using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Web.Script.Serialization;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Views;
using Task = System.Threading.Tasks.Task;
using TextRange = System.Windows.Documents.TextRange;

namespace CodexCode.IdeIntegration
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IdeTests
    /// desc         : 실제 세 IDE 호스트에서 새 Markdown·테마·좁은 목록·입력 아이콘을 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-025 이미지28~37의 실제 제품 연결 회귀 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static partial class IdeTests
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckLatestUiAsync
        /// desc         : 등록된 제품의 실제 답변 링크를 클릭하고 표시 값과 좁은 목록의 차단·복원을 관찰한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 개인 소스·네트워크·실제 CLI 호출 없이 격리된 합성 파일만 사용.
        ///                2026-10-19 CodexOrig : 투명 입력 배경은 실제 불투명 composer 표면과 합성해 팔레트 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task CheckLatestUiAsync(AssistantControl view, DTE dte, IVsWindowFrame frame, string sourcePath, string otherPath, string output)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            view.UpdateLayout();
            var palette = new List<object>();
            foreach (string name in new[] { "ConversationList", "Instruction", "TextSizeChoice" })
            {
                var control = (Control)view.FindName(name);
                var background = control.Background as SolidColorBrush;
                var foreground = control.Foreground as SolidColorBrush;
                var surface = background != null && background.Color.A == 0 && name == "Instruction" ? ((Border)view.FindName("ComposerSurface")).Background as SolidColorBrush : background;
                Check("LATEST-PALETTE " + name + " resolves explicit dark surface in actual host", surface != null && surface.Color.A == 255 && surface.Color.R < 75 && surface.Color.G < 75 && surface.Color.B < 75 && foreground != null && foreground.Color.R > 170);
                palette.Add(new { Control = name, Background = background == null ? null : background.Color.ToString(), EffectiveBackground = surface.Color.ToString(), Foreground = foreground.Color.ToString(), BackgroundSource = DependencyPropertyHelper.GetValueSource(control, Control.BackgroundProperty).BaseValueSource.ToString() });
            }
            var send = Button(view, "GenerateButton");
            var icon = Visuals(send).OfType<System.Windows.Shapes.Path>().SingleOrDefault();
            var iconHost = Visuals(send).OfType<Viewbox>().SingleOrDefault();
            Point iconCenter = iconHost == null ? new Point(Double.NaN, Double.NaN) : iconHost.TranslatePoint(new Point(iconHost.ActualWidth / 2, iconHost.ActualHeight / 2), send);
            Check("LATEST-ICON actual send button has a bold centered vector magnifier", icon != null && icon.StrokeThickness >= 2.5 && icon.Data != null && send.FontWeight == FontWeights.Bold && Math.Abs(iconCenter.X - send.ActualWidth / 2) < 1 && Math.Abs(iconCenter.Y - send.ActualHeight / 2) < 1);
            await CheckLatestThemeModesAsync(view, dte, output);
            CheckLatestMarkdownEditor(view);

            var chat = (ChatSession)typeof(AssistantControl).GetProperty("ActiveChat", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view, null);
            string safeFile = Path.Combine(output, "Markdown_Link_Test.cmd");
            string marker = Path.Combine(output, "markdown-command-must-never-run.txt");
            File.WriteAllText(safeFile, "@echo off\r\necho forbidden>\"" + marker + "\"\r\n");
            string sourceLink = new Uri(sourcePath).AbsoluteUri + "#L2";
            string driveLink = sourcePath.Replace('\\', '/') + "#L2";
            string relativeLink = "Probe.cs#L2";
            string safeLink = new Uri(safeFile).AbsoluteUri;
            var message = new ChatMessage { Role = "assistant", Text = "# TASK025 실제 IDE Markdown\n\n**강조된 안내**\n\n[소스 행 열기](" + sourceLink + ")\n\n[드라이브 경로](" + driveLink + ")\n\n[상대 경로](" + relativeLink + ")\n\n[실행 없는 텍스트 보기](" + safeLink + ")\n\n[웹 도움말](https://example.com/help)\n\n```csharp\nreturn 2;\n```" };
            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            chat.Messages.Add(message);
            try
            {
                await Task.Delay(150);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                view.UpdateLayout();
                var preview = Visuals(view).OfType<FrameworkElement>().SingleOrDefault(element => element.GetType().Name == "MarkdownPreview" && Convert.ToString(element.GetType().GetProperty("Markdown").GetValue(element, null)) == message.Text);
                Check("LATEST-MD answer binding creates the real Markdown preview", preview != null);
                FlowDocument document = FindMarkdownDocument(preview);
                Check("LATEST-MD headings and emphasis become WPF elements", document != null && LogicalObjects(document).OfType<Paragraph>().Any(p => p.FontSize > view.FontSize) && LogicalObjects(document).OfType<Bold>().Any());
                var links = LogicalObjects(document).OfType<Hyperlink>().ToArray();
                Check("LATEST-MD local and web links carry visible icons", links.Length == 5 && links.Any(link => new TextRange(link.ContentStart, link.ContentEnd).Text.StartsWith("▣ ", StringComparison.Ordinal)) && links.Any(link => new TextRange(link.ContentStart, link.ContentEnd).Text.StartsWith("↗ ", StringComparison.Ordinal)));
                Check("LATEST-MD rendering alone leaves active document and disk unchanged", !File.Exists(marker) && sourceBytes.SequenceEqual(File.ReadAllBytes(sourcePath)));
                dte.ItemOperations.OpenFile(otherPath);
                links.Single(link => Convert.ToString(link.Tag) == sourceLink).RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
                Check("LATEST-LINK actual Markdown click opens requested source line in IDE", dte.ActiveDocument != null && dte.ActiveDocument.FullName == sourcePath && ((TextDocument)dte.ActiveDocument.Object("TextDocument")).Selection.CurrentLine == 2);
                foreach (string format in new[] { driveLink, relativeLink })
                {
                    dte.ItemOperations.OpenFile(otherPath);
                    links.Single(link => Convert.ToString(link.Tag) == format).RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
                    Check("LATEST-LINK parsed line suffix works for " + (format == driveLink ? "drive" : "relative") + " path", dte.ActiveDocument != null && dte.ActiveDocument.FullName == sourcePath && ((TextDocument)dte.ActiveDocument.Object("TextDocument")).Selection.CurrentLine == 2);
                }
                links.Single(link => Convert.ToString(link.Tag) == safeLink).RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
                Check("LATEST-LINK command suffix opens as IDE text without executing", dte.ActiveDocument != null && dte.ActiveDocument.FullName == safeFile && dte.ActiveDocument.Object("TextDocument") is TextDocument && !File.Exists(marker));
                dte.ActiveDocument.Close(vsSaveChanges.vsSaveChangesNo);
                dte.ItemOperations.OpenFile(sourcePath);
                Check("LATEST-LINK navigation preserves source bytes", sourceBytes.SequenceEqual(File.ReadAllBytes(sourcePath)));
            }
            finally { chat.Messages.Remove(message); }

            frame.Show();
            await Task.Delay(100);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            view.UpdateLayout();

            double oldWidth = view.Width;
            var history = (ToggleButton)view.FindName("HistoryToggle");
            bool? oldHistory = history.IsChecked;
            try
            {
                view.Width = 380;
                history.IsChecked = true;
                typeof(AssistantControl).GetMethod("HistoryToggled", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { view, new RoutedEventArgs() });
                await Task.Delay(100);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                view.UpdateLayout();
                var backdrop = (Border)view.FindName("HistoryBackdrop");
                var surface = (FrameworkElement)view.FindName("ConversationSurface");
                Check("LATEST-OVERLAY narrow actual IDE dims and disables obscured conversation", backdrop.IsVisible && ((SolidColorBrush)backdrop.Background).Color.A >= 160 && !surface.IsEnabled);
                typeof(AssistantControl).GetMethod("DismissHistoryOverlay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
                Check("LATEST-OVERLAY dismiss restores conversation input and hides scrim", !backdrop.IsVisible && surface.IsEnabled && history.IsChecked == false);
            }
            finally
            {
                view.Width = oldWidth; history.IsChecked = oldHistory;
                typeof(AssistantControl).GetMethod("HistoryToggled", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { view, new RoutedEventArgs() });
                view.UpdateLayout();
            }
            File.WriteAllText(Path.Combine(output, "latest-ui-observation.json"), new JavaScriptSerializer().Serialize(new { HostVersion = dte.Version, Palette = palette, NativePointerUsed = false, WebLinksOpened = false, AiInvoked = false, OriginalSourceUnchanged = sourceBytes.SequenceEqual(File.ReadAllBytes(sourcePath)), CommandExecuted = File.Exists(marker), CheckedAt = DateTimeOffset.Now }));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FindMarkdownDocument
        /// desc         : 실제 미리보기의 문서를 찾아 원시 문자열 대신 표시 구조를 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 일반 WPF 문서 호스트 세 종류만 조회.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static FlowDocument FindMarkdownDocument(DependencyObject root)
        {
            foreach (var element in Visuals(root))
            {
                var rich = element as RichTextBox; if (rich != null) return rich.Document;
                var scroll = element as FlowDocumentScrollViewer; if (scroll != null) return scroll.Document;
                var reader = element as FlowDocumentReader; if (reader != null) return reader.Document;
            }
            return null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckLatestThemeModesAsync
        /// desc         : 세 실제 IDE 안에서 명시 Light·Dark 전환과 VS 우선 자동 모드를 관찰한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : IDE·Windows의 전역 테마를 바꾸지 않고 확장 선택만 되돌려 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task CheckLatestThemeModesAsync(AssistantControl view, DTE dte, string output)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var restore = typeof(AssistantControl).GetMethod("RestoreThemeMode", flags);
            var choice = (ComboBox)view.FindName("ThemeModeChoice");
            Check("LATEST-THEME explicit theme choice is connected in actual IDE", restore != null && choice != null);
            object prior = choice.SelectedValue;
            var observations = new List<object>();
            try
            {
                Click(view, "ModelOptionsButton");
                await Task.Delay(100);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var popup = (Popup)typeof(AssistantControl).GetField("dropdownPopup", flags).GetValue(view);
                Check("LATEST-THEME real model popup opens before palette changes", popup != null && popup.IsOpen && popup.Child is Border);
                var popupSurface = (Border)popup.Child;
                foreach (string mode in new[] { "light", "dark", "auto" })
                {
                    restore.Invoke(view, new object[] { mode });
                    await Task.Delay(80);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    view.UpdateLayout();
                    var background = (SolidColorBrush)view.Background;
                    var foreground = (SolidColorBrush)view.Foreground;
                    var listBackground = (SolidColorBrush)((ListBox)view.FindName("ConversationList")).Background;
                    var popupBackground = (SolidColorBrush)popupSurface.Background;
                    var popupForeground = (SolidColorBrush)popupSurface.GetValue(Control.ForegroundProperty);
                    observations.Add(new { Mode = mode, Background = background.Color.ToString(), Foreground = foreground.Color.ToString(), ListBackground = listBackground.Color.ToString(), PopupBackground = popupBackground.Color.ToString(), PopupForeground = popupForeground.Color.ToString(), SelectedValue = Convert.ToString(choice.SelectedValue) });
                    Check("LATEST-THEME " + mode + " selects the matching option", String.Equals(Convert.ToString(choice.SelectedValue), mode, StringComparison.OrdinalIgnoreCase));
                    Check("LATEST-THEME open model popup retains readable contrast in " + mode, popup.IsOpen && ThemeContrast(popupBackground.Color, popupForeground.Color) >= 4.5);
                    if (mode == "light") Check("LATEST-THEME Light updates the real surface and text contrast", background.Color.R >= 200 && background.Color.G >= 200 && foreground.Color.R <= 100 && listBackground.Color.R >= 200);
                    if (mode == "light") Check("LATEST-THEME open popup also changes to a Light surface", popupBackground.Color.R >= 180 && popupBackground.Color.G >= 180);
                    if (mode == "dark") Check("LATEST-THEME Dark restores the real dark surface and text contrast", background.Color.R < 75 && foreground.Color.R > 170 && listBackground.Color.R < 75);
                    if (mode == "auto")
                    {
                        var native = System.Windows.Application.Current.TryFindResource(VsBrushes.ToolWindowBackgroundKey) as SolidColorBrush;
                        Color expected;
                        if (native != null && native.Color.A == 255) expected = native.Color;
                        else
                        {
                            var color = Microsoft.VisualStudio.PlatformUI.VSColorTheme.GetThemedColor(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowBackgroundColorKey);
                            expected = Color.FromArgb(color.A, color.R, color.G, color.B);
                        }
                        Check("LATEST-THEME Auto uses the actual Visual Studio background before Windows", background.Color == expected);
                        observations.Add(new { NativeHostBackground = expected.ToString(), Mode = mode });
                    }
                }
            }
            finally { typeof(AssistantControl).GetMethod("CloseDropdown", flags).Invoke(view, null); restore.Invoke(view, new[] { prior ?? "dark" }); view.UpdateLayout(); }
            File.WriteAllText(Path.Combine(output, "theme-mode-observation.json"), new JavaScriptSerializer().Serialize(new { HostVersion = dte.Version, Observations = observations, GlobalIdeThemeChanged = false, GlobalWindowsThemeChanged = false, CheckedAt = DateTimeOffset.Now }));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ThemeContrast
        /// desc         : 실제 표시한 불투명 색 두 개의 상대 휘도로 글자 대비를 계산한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 열린 popup의 밝은 글자·배경 전환 누락을 검출.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static double ThemeContrast(Color first, Color second)
        {
            Func<byte, double> linear = value => { double channel = value / 255.0; return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4); };
            Func<Color, double> luminance = color => 0.2126 * linear(color.R) + 0.7152 * linear(color.G) + 0.0722 * linear(color.B);
            double a = luminance(first), b = luminance(second);
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckLatestMarkdownEditor
        /// desc         : 실제 입력란 우클릭 메뉴에서 작성 창을 열어 설치된 AvalonEdit과 미리보기를 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 합성 초안만 편집하고 입력란에 적용하지 않은 채 자기 창만 닫음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckLatestMarkdownEditor(AssistantControl view)
        {
            string original = Text(view, "Instruction").Text;
            var menu = Text(view, "Instruction").ContextMenu;
            var entry = menu == null ? null : menu.Items.OfType<MenuItem>().SingleOrDefault(item => Convert.ToString(item.Header) == "Markdown 작성 · 미리보기");
            Check("LATEST-EDITOR actual prompt context menu exposes authoring", entry != null);
            Exception error = null;
            bool inspected = false;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Action inspect = () =>
            {
                System.Windows.Window dialog = null;
                FrameworkElement editor = null;
                try
                {
                    dialog = (System.Windows.Window)typeof(AssistantControl).GetField("activeMarkdownDialog", flags).GetValue(view);
                    Check("LATEST-EDITOR context action opens an owned visible IDE dialog", dialog != null && dialog.IsVisible && (dialog.Owner != null || new System.Windows.Interop.WindowInteropHelper(dialog).Owner != IntPtr.Zero));
                    dialog.UpdateLayout();
                    editor = Visuals(dialog).OfType<FrameworkElement>().Single(element => element.GetType().Name == "MarkdownEditor");
                    var nativeEditor = editor.GetType().GetProperty("Editor").GetValue(editor, null);
                    string dependency = Path.Combine(Path.GetDirectoryName(typeof(AssistantControl).Assembly.Location), "ICSharpCode.AvalonEdit.dll");
                    string actual = nativeEditor.GetType().Assembly.Location;
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                    {
                        byte[] packaged = sha.ComputeHash(File.ReadAllBytes(dependency));
                        byte[] loaded = sha.ComputeHash(File.ReadAllBytes(actual));
                        Check("LATEST-EDITOR actual IDE loads the packaged AvalonEdit bytes", packaged.SequenceEqual(loaded));
                    }
                    Check("LATEST-EDITOR actual code editor displays line numbers", (bool)nativeEditor.GetType().GetProperty("ShowLineNumbers").GetValue(nativeEditor, null));
                    editor.GetType().GetProperty("Text").SetValue(editor, "# IDE 작성 확인\n\n**표시 예제**\n\n- [ ] 확인 항목", null);
                    editor.GetType().GetMethod("RefreshPreview").Invoke(editor, null);
                    var preview = (DependencyObject)editor.GetType().GetProperty("Preview").GetValue(editor, null);
                    var document = FindMarkdownDocument(preview);
                    Check("LATEST-EDITOR actual editor refreshes a formatted native preview", document != null && LogicalObjects(document).OfType<Bold>().Any());
                    inspected = true;
                }
                catch (Exception failure) { error = failure; }
                finally
                {
                    if (editor != null) editor.GetType().GetMethod("MarkSaved").Invoke(editor, null);
                    if (dialog != null) dialog.Close();
                }
            };
            // 자기 모달의 중첩 Dispatcher가 시작된 뒤 검사하므로 Background에 비차단으로 한 번 게시한다.
#pragma warning disable VSTHRD001
            var operation = view.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, inspect);
#pragma warning restore VSTHRD001
            entry.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            if (!inspected) operation.Abort();
            if (error != null) throw new InvalidOperationException("Actual IDE Markdown editor inspection failed.", error);
            Check("LATEST-EDITOR closing an unapplied draft preserves the original prompt", inspected && Text(view, "Instruction").Text == original);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LogicalObjects
        /// desc         : FlowDocument의 단락과 링크를 논리 트리로 읽기만 한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 문서 소유권과 이벤트 연결을 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<DependencyObject> LogicalObjects(DependencyObject root)
        {
            if (root == null) yield break;
            yield return root;
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                var item = child as DependencyObject;
                if (item != null) foreach (var descendant in LogicalObjects(item)) yield return descendant;
            }
        }
    }
}
