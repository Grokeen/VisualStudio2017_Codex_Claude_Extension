using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using VS2017.AIAssistant.Views;

public static class DialogCaptionChecks
{
    public static readonly List<string> Checks = new List<string>();
    public static readonly List<string> Errors = new List<string>();

    public static void Run(bool expectCustomCaption)
    {
        var source = new AssistantControl();
        var owner = new Window { Content = source, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Width = 1, Height = 1, Left = -10000, Top = -10000, Opacity = 0 };
        AssistantDialog dialog = null;
        try
        {
            owner.Show(); owner.Hide();
            dialog = new AssistantDialog(source, "캡션 회귀 검사", "제목은 Window.Title에 유지하고 본문 설명은 별도로 표시합니다.");
            var chrome = WindowChrome.GetWindowChrome(dialog);
            if (!expectCustomCaption)
            {
                Assert(dialog.WindowStyle == WindowStyle.SingleBorderWindow, "C4 baseline retains the operating-system title bar");
                Assert(chrome == null, "C4 baseline has no WindowChrome override");
                Assert(dialog.Title == "캡션 회귀 검사", "C4 baseline preserves Window.Title");
                return;
            }

            Assert(dialog.WindowStyle == WindowStyle.None, "Custom caption replaces the native title bar");
            Assert(chrome != null && chrome.CaptionHeight >= 40 && !chrome.UseAeroCaptionButtons,
                "WindowChrome provides a themed caption without DWM caption buttons");
            Assert(chrome != null && chrome.ResizeBorderThickness.Left > 0 && chrome.ResizeBorderThickness.Right > 0 &&
                chrome.ResizeBorderThickness.Top > 0 && chrome.ResizeBorderThickness.Bottom > 0 && dialog.ResizeMode == ResizeMode.CanResize,
                "WindowChrome preserves resize hit-test borders on every edge");
            Assert(dialog.Title == "캡션 회귀 검사" && ReferenceEquals(dialog.Owner, owner) && !dialog.ShowInTaskbar,
                "Window.Title, owner relationship and modal-window taskbar behavior remain intact");

            var dialogContent = (DependencyObject)dialog.Content;
            var textBlocks = Descendants<TextBlock>(dialogContent).ToArray();
            var captionTitle = textBlocks.FirstOrDefault(item => item.GetBindingExpression(TextBlock.TextProperty) != null &&
                item.GetBindingExpression(TextBlock.TextProperty).ParentBinding.Path.Path == "Title");
            Assert(captionTitle != null, "Caption title is data-bound to Window.Title");
            if (captionTitle != null)
            {
                dialog.Title = "갱신된 접근성 제목";
                captionTitle.GetBindingExpression(TextBlock.TextProperty).UpdateTarget();
                var nameBinding = captionTitle.GetBindingExpression(AutomationProperties.NameProperty);
                if (nameBinding != null) nameBinding.UpdateTarget();
                Assert(captionTitle.Text == dialog.Title && AutomationProperties.GetName(captionTitle) == dialog.Title,
                    "Caption text and accessible name track Window.Title changes");
            }
            Assert(textBlocks.Any(item => item.Text == "제목은 Window.Title에 유지하고 본문 설명은 별도로 표시합니다."),
                "Description remains in the client area without a duplicate title heading");

            var captionButtons = Descendants<Button>(dialogContent)
                .Where(button => (AutomationProperties.GetAutomationId(button) ?? "").StartsWith("AssistantDialog.", StringComparison.Ordinal))
                .ToArray();
            var close = captionButtons.FirstOrDefault(button => AutomationProperties.GetAutomationId(button) == "AssistantDialog.Close");
            var minimize = captionButtons.FirstOrDefault(button => AutomationProperties.GetAutomationId(button) == "AssistantDialog.Minimize");
            var maximize = captionButtons.FirstOrDefault(button => AutomationProperties.GetAutomationId(button) == "AssistantDialog.MaximizeRestore");
            Assert(captionButtons.Length == 3 && close != null && minimize != null && maximize != null,
                "Minimize, maximize/restore and close caption buttons are present");
            Assert(captionButtons.Length == 3 && captionButtons.All(button => WindowChrome.GetIsHitTestVisibleInChrome(button)),
                "Every caption button receives pointer hit tests inside the WindowChrome area");
            Assert(close != null && AutomationProperties.GetName(close) == "닫기" && close.Command == SystemCommands.CloseWindowCommand && ReferenceEquals(close.CommandTarget, dialog),
                "Caption close button is accessible and routes the close command to its window");
            Assert(minimize != null && minimize.Command == SystemCommands.MinimizeWindowCommand && maximize != null &&
                maximize.Command == SystemCommands.MaximizeWindowCommand && ReferenceEquals(maximize.CommandTarget, dialog),
                "Caption minimize and maximize controls route system window commands");
            Assert(dialog.InputBindings.OfType<KeyBinding>().Any(binding => binding.Key == Key.F4 &&
                (binding.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && binding.Command == SystemCommands.CloseWindowCommand),
                "Alt+F4 remains explicitly bound to the standard close command");
            Assert(dialog.CommandBindings.OfType<CommandBinding>().Any(binding => binding.Command == SystemCommands.CloseWindowCommand),
                "The window handles the standard close command");
        }
        catch (Exception error) { Errors.Add(error.ToString()); }
        finally
        {
            if (dialog != null) dialog.Close();
            owner.Content = null;
            ((IDisposable)source).Dispose();
            owner.Close();
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root == null) yield break;
        foreach (object item in LogicalTreeHelper.GetChildren(root))
        {
            var child = item as DependencyObject;
            if (child == null) continue;
            if (child is T) yield return (T)child;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void Assert(bool condition, string message)
    { if (condition) Checks.Add(message); else Errors.Add(message); }
}
