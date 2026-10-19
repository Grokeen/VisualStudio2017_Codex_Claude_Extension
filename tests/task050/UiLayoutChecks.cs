using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VS2017.AIAssistant.Views;

public static class UiLayoutChecks
{
    public static readonly List<string> Checks = new List<string>();
    public static readonly List<string> Errors = new List<string>();

    public static void Run()
    {
        AssistantControl source = null; AssistantDialog dialog = null; MarkdownEditor editor = null;
        try
        {
            source = new AssistantControl();
            var getNotifications = typeof(AssistantControl).GetMethod("GetCompletionNotificationsEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
            var restoreNotifications = typeof(AssistantControl).GetMethod("RestoreCompletionNotifications", BindingFlags.Instance | BindingFlags.NonPublic);
            if (getNotifications == null || restoreNotifications == null) throw new InvalidOperationException("Notification settings bridge methods were not found.");
            Assert(getNotifications != null && restoreNotifications != null && (bool)getNotifications.Invoke(source, null),
                "Completion notification choice loads from the isolated settings file");
            restoreNotifications.Invoke(source, new object[] { false });
            Assert(!(bool)getNotifications.Invoke(source, null), "Completion notification choice can be restored to off");
            restoreNotifications.Invoke(source, new object[] { true });
            var provider = source.FindName("ProviderChoice") as TabControl;
            Assert(provider != null && provider.Items.Count == 2 &&
                KeyboardNavigation.GetDirectionalNavigation(provider) == KeyboardNavigationMode.Contained,
                "Claude and Codex remain arrow-key navigable provider tabs");
            if (provider != null && provider.Items.Count == 2)
                Assert(!String.IsNullOrWhiteSpace(AutomationProperties.GetName(provider.Items[0] as DependencyObject)) &&
                    !String.IsNullOrWhiteSpace(AutomationProperties.GetName(provider.Items[1] as DependencyObject)) &&
                    AutomationProperties.GetName(provider.Items[0] as DependencyObject) != AutomationProperties.GetName(provider.Items[1] as DependencyObject),
                    "Provider tabs expose distinct accessible names");
            dialog = new AssistantDialog(source, "설정 UI 검사", "좌측 목록에서 설정 화면을 선택합니다.") { Width = 900, Height = 740 };
            var dialogGrid = dialog.Content as Grid;
            var headerBand = dialogGrid == null ? null : dialogGrid.Children.OfType<Border>().FirstOrDefault(item => Grid.GetRow(item) == 0);
            var sharedActionStyle = dialog.Actions.Resources[typeof(Button)] as Style;
            Assert(headerBand != null && headerBand.BorderThickness.Bottom == 1 && dialog.Footer.BorderThickness.Top == 1,
                "Popups share a surface header and divided footer chrome");
            Assert(sharedActionStyle != null && sharedActionStyle.Setters.OfType<Setter>().Any(setter => setter.Property == FrameworkElement.MinHeightProperty) &&
                sharedActionStyle.Setters.OfType<Setter>().Any(setter => setter.Property == FrameworkElement.MinWidthProperty),
                "Popup action buttons share minimum size and padding resources");
            var host = new StackPanel();
            var sections = new[]
            {
                new Border { Child = new TextBlock { Text = "계정 본문" } },
                new Border { Child = new TextBlock { Text = "모델 본문" } },
                new Border { Child = new TextBlock { Text = "규칙 본문" } },
                new Border { Child = new TextBlock { Text = "화면 본문" } }
            };
            foreach (var section in sections) host.Children.Add(section);
            var scroll = new ScrollViewer { Content = host };
            dialog.Body.Content = scroll;
            dialog.SetSectionPages(host,
                new DialogSection("⚙", "계정", sections[0]), new DialogSection("☷", "모델", sections[1]),
                new DialogSection("✎", "규칙", sections[2]), new DialogSection("◐", "화면", sections[3]));
            Assert(dialog.SidebarNavigation.Visibility == Visibility.Visible && dialog.SidebarNavigation.Children.Count == 4,
                "Settings sections appear in a four-item left navigation");
            Assert(host.Children.Count == 1 && ReferenceEquals(host.Children[0], sections[0]),
                "Only the selected settings page is visible");
            var modelTab = (ToggleButton)dialog.SidebarNavigation.Children[1];
            modelTab.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert(modelTab.IsChecked == true && host.Children.Count == 1 && ReferenceEquals(host.Children[0], sections[1]),
                "Selecting a side tab switches the settings page");
            dialog.RestoreSectionPages();
            Assert(dialog.SidebarNavigation.Visibility == Visibility.Collapsed && host.Children.Count == 4 &&
                sections.All(section => ReferenceEquals(section.Parent, host)),
                "Closing settings restores all original section controls");

            dialog.SetSections(scroll, new DialogSection("⚙", "계정", sections[0]), new DialogSection("✎", "규칙", sections[2]));
            Assert(dialog.Navigation.Visibility == Visibility.Visible && dialog.SidebarNavigation.Visibility == Visibility.Collapsed,
                "Other dialog contents retain the shared horizontal navigation contract");

            editor = new MarkdownEditor();
            editor.Resources.MergedDictionaries.Add(source.Resources);
            var editorGrid = editor.Content as Grid;
            var toolbar = editorGrid == null ? null : editorGrid.Children.OfType<WrapPanel>().FirstOrDefault();
            var groups = toolbar == null ? new Border[0] : toolbar.Children.OfType<Border>().ToArray();
            Assert(groups.Length == 6 && groups.All(group => group.Child is StackPanel),
                "Markdown actions are grouped into six bounded functional surfaces");
            Assert(editor.SearchMatchCase == false && editor.SearchWholeWords == false && editor.SearchUseRegex == false,
                "Markdown search checkbox defaults remain unchecked");
            var checkboxes = Descendants<CheckBox>(editor).ToArray();
            Assert(checkboxes.Length == 3 && checkboxes.All(box => box.Style != null),
                "All Markdown search checkboxes resolve the shared checkbox theme style");
        }
        catch (Exception error) { Errors.Add(error.ToString()); }
        finally
        {
            if (editor != null) editor.Dispose();
            if (dialog != null) dialog.Close();
            if (source != null) { var disposable = source as IDisposable; if (disposable != null) disposable.Dispose(); }
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
