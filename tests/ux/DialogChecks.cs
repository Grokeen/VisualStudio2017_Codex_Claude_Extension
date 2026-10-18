using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : DialogChecks
/// desc         : 실제 제품 팝업의 첫 실행·저장·취소·테마·편집 메뉴를 WPF와 실제 IDE에서 공통 검증한다.
/// author       : CodexOrig
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexOrig : 테스트 자체 창과 설정 파일만 조작하는 격리 검증.
/// update date  : 2026-10-18 CodexOrig : TASK-018 이미지 요구와 드롭다운·A~Z 이름에 맞춰 검증 갱신.
/// </summary>
/// ---------------------------------------------------------------------
internal static class DialogChecks
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 개인 설정 경로를 보존하며 제품 버튼·우클릭 메뉴와 자동 안내의 실제 창을 연다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 네 단계 안내·플래그 병합·팝업 위치·저장 실패 검증.
    /// update date  : 2026-10-18 CodexOrig : 공통 Markdown 잠금 실패 시 JSON·Markdown 원문 복구와 검정 팔레트 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static void Run(string directory, Action<string, bool> check)
    {
        Directory.CreateDirectory(directory);
        string previous = Environment.GetEnvironmentVariable("VS2017_AI_SETTINGS_PATH");
        string previousWorkspace = Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH");
        Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", Path.Combine(directory, "dialog-settings.json"));
        Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", Path.Combine(directory, "workspace"));
        var store = new AssistantSettingsStore();
        var saved = new AssistantSettings { CodexPath = "codex-original.exe", ClaudePath = "claude-original.exe", CodexModel = "model-a", ClaudeModel = "model-b", Instructions = "기존 규칙 유지", FontSize = 16 };
        store.Save(saved);
        Window host = null;
        try
        {
            using (var view = new AssistantControl())
            {
                ProductUi.SelectProvider(view, "Codex");
                host = new Window { Title = "Isolated product UX test", Content = view, Width = 420, Height = 700, ShowInTaskbar = false };
                host.Resources[Microsoft.VisualStudio.Shell.VsBrushes.ToolWindowBackgroundKey] = Brushes.White;
                host.Resources[Microsoft.VisualStudio.Shell.VsBrushes.ToolWindowTextKey] = Brushes.Black;
                Exercise(() => host.Show(), dialog => {
                    var guide = dialog as GettingStartedDialog;
                    check("UX-01 first visible Chat automatically opens four-step guide", guide != null && CountText((DependencyObject)guide.Content, "AI 연결") == 1 && CountText((DependencyObject)guide.Content, "직접 저장") == 1);
                    check("UX-02 first guide is centered and checkbox starts unchecked", guide.WindowStartupLocation == WindowStartupLocation.CenterScreen && guide.HideNextTime.IsChecked == false);
                    Render(guide, Path.Combine(directory, "guide-light.png"));
                    guide.HideNextTime.IsChecked = true;
                    guide.StartButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
                var after = store.Load();
                check("UX-03 hide guide persists without changing paths models rules size", after.HideWelcome && after.CodexPath == saved.CodexPath && after.ClaudePath == saved.ClaudePath && after.CodexModel == saved.CodexModel && after.ClaudeModel == saved.ClaudeModel && after.Instructions == saved.Instructions && after.FontSize == saved.FontSize);
                var prompt = (TextBox)view.FindName("Instruction");
                var menu = prompt.ContextMenu;
                check("UX-04 prompt keeps targeted cut copy paste and select all commands", menu.Items.OfType<MenuItem>().Any(i => i.Command == ApplicationCommands.Paste && i.CommandTarget == prompt) && menu.Items.OfType<MenuItem>().Any(i => i.Command == ApplicationCommands.Copy && i.CommandTarget == prompt) && menu.Items.OfType<MenuItem>().Any(i => i.Command == ApplicationCommands.Cut) && menu.Items.OfType<MenuItem>().Any(i => i.Command == ApplicationCommands.SelectAll));
                check("UX-05 preview retains copy and app settings without write commands", ((TextBox)view.FindName("DiffPreview")).ContextMenu.Items.OfType<MenuItem>().Any(i => i.Command == ApplicationCommands.Copy) && !((TextBox)view.FindName("DiffPreview")).ContextMenu.Items.OfType<MenuItem>().Any(i => i.Command == ApplicationCommands.Paste));
                check("UX-06 all areas expose settings and manual guide", view.ContextMenu.Items.OfType<MenuItem>().Any(i => Equals(i.Header, "설정")) && menu.Items.OfType<MenuItem>().Any(i => Equals(i.Header, "사용 안내")));
                Exercise(() => Click(view, "HelpButton"), dialog => {
                    var guide = (GettingStartedDialog)dialog;
                    check("UX-07 hidden guide can be reopened manually with stored checkbox", guide.HideNextTime.IsChecked == true);
                });
                Exercise(() => menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "설정")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)), dialog => {
                    check("UX-08 right-click settings opens a centered modal settings window", !(dialog is GettingStartedDialog) && dialog.WindowStartupLocation == WindowStartupLocation.CenterScreen && Math.Abs(dialog.Left + dialog.ActualWidth / 2 - (SystemParameters.WorkArea.Left + SystemParameters.WorkArea.Width / 2)) < 5 && Math.Abs(dialog.Top + dialog.ActualHeight / 2 - (SystemParameters.WorkArea.Top + SystemParameters.WorkArea.Height / 2)) < 5);
                    ((TextBox)view.FindName("ExecutablePath")).Text = "unsaved.exe";
                    ((TextBox)view.FindName("RulesText")).Text = "취소할 변경";
                    ((ComboBox)view.FindName("TextSizeChoice")).SelectedValue = "24";
                    Render(dialog, Path.Combine(directory, "settings-light.png"));
                });
                check("UX-09 cancel restores all live settings and does not write disk", ((TextBox)view.FindName("ExecutablePath")).Text == saved.CodexPath && ((TextBox)view.FindName("RulesText")).Text == saved.Instructions && view.FontSize == 16 && store.Load().CodexPath == saved.CodexPath);
                host.Resources[Microsoft.VisualStudio.Shell.VsBrushes.ToolWindowBackgroundKey] = Brushes.Black;
                host.Resources[Microsoft.VisualStudio.Shell.VsBrushes.ToolWindowTextKey] = Brushes.White;
                Exercise(() => Click(view, "SettingsButton"), dialog => {
                    check("UX-10 dialog and text controls use the same dark palette", ((SolidColorBrush)dialog.Background).Color == ((SolidColorBrush)view.Background).Color && ((SolidColorBrush)dialog.Foreground).Color == ((SolidColorBrush)view.Foreground).Color && ((SolidColorBrush)((TextBox)view.FindName("RulesText")).Foreground).Color == ((SolidColorBrush)view.Foreground).Color);
                    ((TextBox)view.FindName("ExecutablePath")).Text = "saved-new.exe";
                    ((TextBox)view.FindName("RulesText")).Text = "저장한 공통 규칙";
                    ((ComboBox)view.FindName("TextSizeChoice")).SelectedValue = "18";
                    Render(dialog, Path.Combine(directory, "settings-dark.png"));
                    Click(view, "SaveSettingsButton");
                });
                check("UX-11 save closes dialog and preserves guide and other provider", store.Load().CodexPath == "saved-new.exe" && store.Load().ClaudePath == saved.ClaudePath && store.Load().HideWelcome && store.Load().FontSize == 18 && ((TextBlock)view.FindName("StatusLabel")).Text.StartsWith("설정을 저장"));
                string instructionsPath = new MarkdownWorkspaceStore().InstructionsPath;
                string beforeSettingsFailure = File.ReadAllText(store.FilePath);
                string beforeInstructionFailure = File.ReadAllText(instructionsPath);
                using (var locked = new FileStream(instructionsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Exercise(() => Click(view, "SettingsButton"), dialog => {
                        ((TextBox)view.FindName("ExecutablePath")).Text = "must-not-partially-save.exe";
                        ((TextBox)view.FindName("RulesText")).Text = "이 저장은 Markdown 파일 잠금으로 실패해야 함";
                        Click(view, "SaveSettingsButton");
                        check("UX-16 instruction failure preserves both JSON and Markdown", dialog.IsVisible && dialog.Message.Text.StartsWith("설정 저장 실패:") && File.ReadAllText(store.FilePath) == beforeSettingsFailure && File.ReadAllText(instructionsPath) == beforeInstructionFailure);
                    });
                }
                Exercise(() => Click(view, "HelpButton"), dialog => {
                    Render(dialog, Path.Combine(directory, "guide-dark.png"));
                });
                string disk = File.ReadAllText(store.FilePath);
                File.Move(store.FilePath, store.FilePath + ".preserved"); Directory.CreateDirectory(store.FilePath);
                Exercise(() => Click(view, "SettingsButton"), dialog => {
                    Click(view, "SaveSettingsButton");
                    check("UX-12 save failure stays visible in settings popup", dialog.IsVisible && dialog.Message.Text.StartsWith("설정 저장 실패:"));
                });
                Directory.Delete(store.FilePath); File.Move(store.FilePath + ".preserved", store.FilePath);
                check("UX-13 failed save preserves original settings file", File.ReadAllText(store.FilePath) == disk);
                host.Close(); host = null;
            }
            using (var recreated = new AssistantControl())
            {
                host = new Window { Content = recreated, Width = 400, Height = 600, ShowInTaskbar = false };
                host.Show(); Pump(150);
                check("UX-14 recreated Chat suppresses automatic guide and restores settings", !Dialogs().Any() && recreated.FontSize == 18 && ((TextBox)recreated.FindName("RulesText")).Text == "저장한 공통 규칙");
                Exercise(() => Click(recreated, "HelpButton"), dialog => ((GettingStartedDialog)dialog).HideNextTime.IsChecked = false);
                check("UX-15 manual guide can restore automatic display without other changes", !store.Load().HideWelcome && store.Load().CodexPath == "saved-new.exe" && store.Load().ClaudeModel == saved.ClaudeModel);
                host.Close(); host = null;
            }
        }
        finally
        {
            if (host != null) host.Close();
            Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", previous);
            Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", previousWorkspace);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Exercise
    /// desc         : 제품 모달 창이 열린 뒤 검증 콜백을 실행하고 테스트 소유 창만 닫는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 모달 중첩 Dispatcher와 제한 시간으로 멈춤 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Exercise(Action open, Action<AssistantDialog> inspect)
    {
        Exception failure = null; bool finished = false; var frame = new DispatcherFrame();
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += (sender, args) => {
            var dialog = Dialogs().FirstOrDefault();
            if (dialog == null && DateTime.UtcNow < deadline) return;
            timer.Stop();
            try { if (dialog == null) throw new InvalidOperationException("Product dialog did not open."); inspect(dialog); }
            catch (Exception error) { failure = error; }
            finally { if (dialog != null && dialog.IsVisible) dialog.Close(); finished = true; frame.Continue = false; }
        };
        timer.Start();
        try { open(); if (!finished) Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        if (failure != null) throw failure;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Dialogs
    /// desc         : 이 테스트 프로세스 안에서 보이는 제품 WPF 팝업만 찾는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : Application.Current가 없는 독립 WPF 프로세스 지원.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static System.Collections.Generic.IEnumerable<AssistantDialog> Dialogs()
    { return PresentationSource.CurrentSources.OfType<HwndSource>().Select(s => s.RootVisual).OfType<AssistantDialog>().Where(w => w.IsVisible); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CountText
    /// desc         : 실제 화면의 단계 제목 존재 여부를 시각 트리에서 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 테스트가 안내 문자열을 대신 생성하지 않도록 제품 트리 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int CountText(DependencyObject root, string value)
    {
        int count = root is TextBlock && ((TextBlock)root).Text.EndsWith(value, StringComparison.Ordinal) ? 1 : 0;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) count += CountText(VisualTreeHelper.GetChild(root, i), value);
        return count;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Click
    /// desc         : 제품 XAML의 실제 버튼에 Click 이벤트를 전달한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 이름으로 연결된 이벤트 처리기 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Click(AssistantControl view, string name)
    { ((Button)view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pump
    /// desc         : 저장된 선택에 따른 자동 안내 비표시를 실제 Loaded 처리 후 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 짧은 제한 시간 안에서 Dispatcher 작업 처리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Render
    /// desc         : 실제 팝업 본문을 PNG로 저장하여 밝은/어두운 테마의 시각 검토 증거를 남긴다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 합성 재현 그림 대신 실행된 제품 화면을 렌더.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Render(Window window, string path)
    {
        var body = (FrameworkElement)window.Content; body.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(body.ActualWidth), (int)Math.Ceiling(body.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(new VisualBrush(body), null, new Rect(0, 0, body.ActualWidth, body.ActualHeight));
        bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }
}
