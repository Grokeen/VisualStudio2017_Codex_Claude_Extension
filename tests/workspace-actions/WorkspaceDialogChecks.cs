using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VS2017.AIAssistant.Services;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : WorkspaceDialogChecks
/// desc         : 실제 제품 DLL의 작업 팝업에서 명령 미리보기·실행 승인 무효화를 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 외부 명령을 실행하지 않고 UI 행동과 다크 렌더링 검사.
/// </summary>
/// ---------------------------------------------------------------------
internal static class WorkspaceDialogChecks
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 권한·대상 변경 후 재승인 요구와 DB 연결 정보 비전달을 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 팝업 열기만으로 작업이 발생하지 않음을 확인.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static void Run(string root, string git, Action<bool, string> check)
    {
        string shared = null;
        using (var view = new AssistantControl())
        {
            view.Resources[Microsoft.VisualStudio.Shell.VsBrushes.ToolWindowBackgroundKey] = new SolidColorBrush(Color.FromRgb(24, 24, 24));
            view.Resources[Microsoft.VisualStudio.Shell.VsBrushes.ToolWindowTextKey] = Brushes.White;
            var dialog = new WorkspaceActionsDialog(view, root, new WorkspacePermissionPolicy(), text => shared = text);
            var run = Field<Button>(dialog, "run"); var preview = Field<TextBox>(dialog, "preview");
            check(!run.IsEnabled && preview.Text.Length == 0 && shared == null, "Dialog open does not execute or share");
            Field<TextBox>(dialog, "executable").Text = git;
            Invoke(dialog, "ReviewClicked");
            check(!run.IsEnabled && dialog.Message.Text.Contains("권한"), "Dialog refuses review without permission");
            Field<CheckBox>(dialog, "sourcePermission").IsChecked = true; Invoke(dialog, "ReviewClicked");
            check(preview.Text.Contains(git) && preview.Text.Contains(root) && !run.IsEnabled, "Dialog exact target and command shown before approval");
            Field<CheckBox>(dialog, "approval").IsChecked = true;
            check(run.IsEnabled, "Dialog explicit approval enables execution");
            Field<TextBox>(dialog, "argument").Text = "origin";
            check(!run.IsEnabled && preview.Text.Length == 0, "Dialog target argument edit invalidates approval");
            Invoke(dialog, "ReviewClicked"); Field<CheckBox>(dialog, "approval").IsChecked = true;
            Field<CheckBox>(dialog, "sourcePermission").IsChecked = false;
            check(!run.IsEnabled && preview.Text.Length == 0, "Dialog permission removal invalidates approval");
            var options = Field<ComboBox>(dialog, "operation");
            options.SelectedItem = options.Items.Cast<ComboBoxItem>().First(i => (WorkspaceOperation)i.Tag == WorkspaceOperation.Deploy);
            check(!run.IsEnabled && Field<TextBox>(dialog, "executable").Text.Length == 0, "Dialog switching operation resets executable and approval");
            Field<CheckBox>(dialog, "databasePermission").IsChecked = true;
            Field<TextBox>(dialog, "connection").Text = "Server=unused;Database=unused;Password=NEVER_SHARE;User ID=fixture";
            Field<TextBox>(dialog, "schema").Text = "dbo | Synthetic | Id | int | NO";
            Field<TextBox>(dialog, "queryRequest").Text = "작성해 주세요";
            Invoke(dialog, "ShareSchemaClicked");
            check(shared != null && shared.Contains("Synthetic") && !shared.Contains("NEVER_SHARE") && !shared.Contains("Server=unused"), "Dialog schema callback excludes credentials and connection string");
            check(Field<TextBox>(dialog, "connection").Text.Length == 0, "Dialog close clears connection input");
            var render = new WorkspaceActionsDialog(view, root, new WorkspacePermissionPolicy(), null);
            render.Width = 540; render.Height = 760;
            var content = (FrameworkElement)render.Content;
            content.Measure(new Size(540, 760)); content.Arrange(new Rect(0, 0, 540, 760)); content.UpdateLayout();
            var bitmap = new RenderTargetBitmap(540, 760, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(root, "workspace-dialog-dark.png"))) encoder.Save(stream);
            byte[] pixels = new byte[540 * 760 * 4]; bitmap.CopyPixels(pixels, 540 * 4, 0);
            check(pixels.Count(value => value > 128) > 5000, "Dialog dark compact content rendering created"); render.Close();
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Field
    /// desc         : 실제 제품 컨트롤의 비공개 입력을 회귀 검사에서 찾아 사용한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 테스트 전용 공개 API 추가 없이 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static T Field<T>(object value, string name) { return (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Invoke
    /// desc         : 해당 버튼의 실제 이벤트 핸들러를 입력 상태에 따라 호출한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 네트워크 없이 검토·컨텍스트 전달 동작 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Invoke(object value, string name) { value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, new object[] { null, new RoutedEventArgs() }); }
}
