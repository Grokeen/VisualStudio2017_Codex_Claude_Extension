using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : MenuChecks
/// desc         : 실제 메뉴 template·fallback·명령 대상과 고정 제품의 Popup·본문 메뉴 통합을 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-19 CodexOrig : TASK025 Markdown 본문·고정 복귀·명시 Dark 팔레트의 최신 UI 계약 검증.
/// update date  : 2026-10-18 CodexCode : 원본 문서·클립보드 변경·CLI·실계정 없이 우클릭 흰 배경 회귀 검사.
/// </summary>
/// ---------------------------------------------------------------------
public static class MenuChecks
{
    public static readonly List<string> Checks = new List<string>(), Errors = new List<string>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : source-only에서는 숨은 객체만 측정하고 실제 DLL에서는 예약된 검증 창과 Popup을 표시한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 열린 Popup 증거를 source-only 결과와 구별.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run(ResourceDictionary resources, UserControl product, bool showWindows, string directory)
    {
        Window window = null;
        ContextMenu sample = null;
        try
        {
            var menuStyle = (Style)resources["AssistantContextMenu"];
            var itemStyle = (Style)resources["AssistantMenuItem"];
            var separatorStyle = (Style)resources["AssistantMenuSeparator"];
            Check(menuStyle.TargetType == typeof(ContextMenu) && itemStyle.TargetType == typeof(MenuItem) && separatorStyle.TargetType == typeof(Separator), "Named styles expose the exact menu, item and separator contracts");
            Check(((Style)resources[typeof(ContextMenu)]).BasedOn == menuStyle && ((Style)resources[typeof(MenuItem)]).BasedOn == itemStyle, "Implicit menu styles reuse the explicit dark contracts");
            Check(((Style)resources[typeof(Separator)]).BasedOn == separatorStyle && ((Style)resources[MenuItem.SeparatorStyleKey]).BasedOn == separatorStyle, "Both ordinary and menu-owned separator keys use the same dark line");
            sample = new ContextMenu { Style = menuStyle }; sample.Resources.MergedDictionaries.Add(resources);
            var commandItem = new MenuItem { Header = "메뉴 검증", Icon = "⚙", InputGestureText = "Ctrl+M", Style = itemStyle };
            var submenu = new MenuItem { Header = "하위 메뉴", Style = itemStyle };
            var disabled = new MenuItem { Header = "비활성 항목", IsEnabled = false, Style = itemStyle };
            var checkable = new MenuItem { Header = "선택 항목", IsCheckable = true, IsChecked = true, Style = itemStyle };
            submenu.Items.Add(disabled); submenu.Items.Add(checkable); sample.Items.Add(commandItem); sample.Items.Add(new Separator { Style = separatorStyle }); sample.Items.Add(submenu);
            MeasureMenu(sample); commandItem.ApplyTemplate(); submenu.ApplyTemplate(); disabled.ApplyTemplate(); checkable.ApplyTemplate();
            var menuSurface = (Border)sample.Template.FindName("ContextMenuSurface", sample);
            Check(menuSurface != null && Dark(menuSurface.Background) && Dark(sample.Background) && sample.OverridesDefaultStyle, "The entire menu surface replaces the native light template and icon gutter");
            Check(menuSurface.BorderThickness == new Thickness(1) && menuSurface.Padding == new Thickness(4), "The custom menu draws its own dark border and padding");
            var icon = (ContentPresenter)commandItem.Template.FindName("MenuIcon", commandItem);
            Check(icon != null && Convert.ToString(icon.Content) == "⚙" && commandItem.InputGestureText == "Ctrl+M", "Menu templates preserve icons and input-gesture labels");
            var popup = (Popup)submenu.Template.FindName("PART_Popup", submenu);
            var submenuSurface = (Border)submenu.Template.FindName("SubmenuSurface", submenu);
            Check(popup != null && popup.Placement == PlacementMode.Right && popup.AllowsTransparency && submenuSurface != null && Dark(submenuSurface.Background), "Submenus have a real PART_Popup with their own dark background");
            Check(((TextBlock)submenu.Template.FindName("SubmenuArrow", submenu)).Visibility == Visibility.Visible, "Submenu affordances remain visible for hierarchical items");
            Check(((TextBlock)checkable.Template.FindName("MenuCheck", checkable)).Visibility == Visibility.Visible && ((ContentPresenter)checkable.Template.FindName("MenuIcon", checkable)).Visibility == Visibility.Collapsed, "Checkable menu items retain their checked-state glyph");
            Check(((Border)disabled.Template.FindName("MenuItemSurface", disabled)).Opacity < 1 && disabled.Foreground.ToString() == "#FF8E8E8E", "Disabled menu items remain readable and visibly distinct");
            var separator = (Separator)sample.Items[1]; separator.ApplyTemplate();
            Check(Dark(((Border)separator.Template.FindName("SeparatorLine", separator)).Background), "Separators cannot reintroduce a light native gutter");
            var first = new TextBox { Text = "FIRST_SYNTHETIC_EDIT_TARGET" }; first.Resources.MergedDictionaries.Add(resources); first.ApplyTemplate(); first.Measure(new Size(400, 100));
            var second = new TextBox { Text = "SECOND_SYNTHETIC_EDIT_TARGET" }; second.Resources.MergedDictionaries.Add(resources); second.ApplyTemplate(); second.Measure(new Size(400, 100));
            var readOnly = new TextBox { Text = "READONLY_SYNTHETIC_TEXT", IsReadOnly = true }; readOnly.Resources.MergedDictionaries.Add(resources); readOnly.ApplyTemplate(); readOnly.Measure(new Size(400, 100));
            var rich = new RichTextBox { IsReadOnly = true }; rich.Resources.MergedDictionaries.Add(resources); rich.ApplyTemplate(); rich.Measure(new Size(400, 100));
            Check(first.ContextMenu != null && first.ContextMenu.Style == menuStyle && first.ReadLocalValue(FrameworkElement.ContextMenuProperty) == DependencyProperty.UnsetValue, "New TextBoxes receive a styled fallback without replacing their local-value precedence");
            Check(rich.ContextMenu != null && rich.ContextMenu.Style == menuStyle, "RichTextBox fallback avoids the private stock editor context menu");
            var editable = new ComboBox { IsEditable = true }; editable.Resources.MergedDictionaries.Add(resources); editable.ApplyTemplate(); editable.Measure(new Size(400, 80));
            var editPart = (TextBox)editable.Template.FindName("PART_EditableTextBox", editable);
            Check(editPart != null && editPart.ContextMenu != null && editPart.ContextMenu.Style == menuStyle, "Editable ComboBox text parts inherit the same dark text menu");
            var explicitMenu = new ContextMenu { Style = menuStyle }; first.ContextMenu = explicitMenu; first.InvalidateProperty(FrameworkElement.ContextMenuProperty);
            Check(ReferenceEquals(first.ContextMenu, explicitMenu), "An explicitly supplied local context menu remains authoritative"); first.ClearValue(FrameworkElement.ContextMenuProperty);
            ContextMenu textMenu = first.ContextMenu; textMenu.PlacementTarget = first; MeasureMenu(textMenu); Pump();
            var textItems = textMenu.Items.OfType<MenuItem>().ToArray();
            Check(textItems.All(item => ReferenceEquals(item.CommandTarget, first)), "Fallback edit commands initially target their actual placement TextBox [" + string.Join(";", textItems.Select(item => Convert.ToString(item.Header) + ":" + (item.CommandTarget == null ? "null" : item.CommandTarget.GetType().Name) + ":" + (item.Parent == null ? "no-parent" : item.Parent.GetType().Name) + ":" + item.GetBindingExpression(MenuItem.CommandTargetProperty).Status)) + "]");
            Check(textItems.Any(item => item.Command == ApplicationCommands.Undo) && textItems.Any(item => item.Command == ApplicationCommands.Redo) && textItems.Any(item => item.Command == ApplicationCommands.Cut) && textItems.Any(item => item.Command == ApplicationCommands.Copy) && textItems.Any(item => item.Command == ApplicationCommands.Paste) && textItems.Any(item => item.Command == ApplicationCommands.SelectAll), "Fallback menus retain standard editing commands and shortcuts");
            ContextMenu secondMenu = second.ContextMenu; secondMenu.PlacementTarget = second; MeasureMenu(secondMenu); Pump();
            Check(secondMenu.Items.OfType<MenuItem>().All(item => ReferenceEquals(item.CommandTarget, second)), "A reused resource menu retargets every command when another editor opens it");
            first.Select(0, 0); second.Select(0, 0);
            ApplicationCommands.SelectAll.Execute(null, second);
            Check(first.SelectionLength == 0 && second.SelectionLength == second.Text.Length, "A safe editing command selects only the current synthetic target without touching clipboard contents");
            Check(!ApplicationCommands.Cut.CanExecute(null, readOnly) && !ApplicationCommands.Delete.CanExecute(null, readOnly), "Read-only input keeps the built-in prohibition on destructive edit commands");
            if (product != null)
            {
                var rootMenu = product.ContextMenu;
                Check(rootMenu != null && rootMenu.Style == menuStyle && HasHeader(rootMenu, "설정") && HasHeader(rootMenu, "사용 안내"), "The actual main context menu explicitly uses the dark contract and preserves settings/help");
                var actualRich = (RichTextBox)product.FindName("ColoredDiffPreview");
                Check(actualRich.ContextMenu != null && HasHeader(actualRich.ContextMenu, "설정") && actualRich.ContextMenu.Items.OfType<MenuItem>().Any(item => item.Command == ApplicationCommands.Copy && ReferenceEquals(item.CommandTarget, actualRich)) && !actualRich.ContextMenu.Items.OfType<MenuItem>().Any(item => item.Command == ApplicationCommands.Cut || item.Command == ApplicationCommands.Paste), "The actual read-only colored diff retains copy/settings/help and rejects cut/paste menu entries");
                var property = product.GetType().GetProperty("ActiveChat", Private); object chat = property.GetValue(product, null);
                var list = (IList)chat.GetType().GetProperty("Messages").GetValue(chat, null); list.Clear();
                var messageType = product.GetType().Assembly.GetType("VS2017.AIAssistant.Chat.ChatMessage", true); object message = Activator.CreateInstance(messageType); messageType.GetProperty("Role").SetValue(message, "assistant", null); messageType.GetProperty("Text").SetValue(message, "SYNTHETIC_MENU_MESSAGE", null); list.Add(message);
                var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.Children.Add(product); var targetBox = new TextBox { Text = "MENU_POPUP_TARGET" }; Grid.SetRow(targetBox, 1); layout.Children.Add(targetBox);
                window = new Window { Content = layout, Width = 720, Height = 560, ShowInTaskbar = false, ShowActivated = false, Background = Brushes.White, Title = "Isolated dark context menu review" };
                var lightMenu = new Style(typeof(ContextMenu)); lightMenu.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White)); window.Resources[typeof(ContextMenu)] = lightMenu;
                if (showWindows) { window.Show(); Pump(); }
                var actualMessages = (ItemsControl)product.FindName("ConversationMessages"); actualMessages.ApplyTemplate(); actualMessages.UpdateLayout();
                var messagePreview = Descendants<ContentControl>(actualMessages).FirstOrDefault(box => box.Name == "MessageBody" && box.GetType().Name == "MarkdownPreview");
                var messageBox = messagePreview == null ? null : messagePreview.GetType().GetProperty("TextView").GetValue(messagePreview,null) as RichTextBox;
                Check(messageBox != null && messageBox.ContextMenu != null && HasHeader(messageBox.ContextMenu, "설정") && HasHeader(messageBox.ContextMenu, "사용 안내"), "Loaded message text replaces its style fallback with the full main context menu");
                if (showWindows)
                {
                    rootMenu.PlacementTarget = targetBox; rootMenu.Placement = PlacementMode.Top; rootMenu.IsOpen = true; Pump();
                    Check(rootMenu.IsOpen && PresentationSource.FromVisual(rootMenu) != null && Dark(((Border)rootMenu.Template.FindName("ContextMenuSurface", rootMenu)).Background), "An actual opened main Popup stays dark under the light host theme");
                    Snapshot(rootMenu, Path.Combine(directory, "context-main.png")); rootMenu.IsOpen = false; Pump();
                    sample.PlacementTarget = targetBox; sample.Placement = PlacementMode.Top; sample.IsOpen = true; Pump(); submenu.IsSubmenuOpen = true; Pump();
                    Check(popup.IsOpen && PresentationSource.FromVisual(submenuSurface) != null && Dark(submenuSurface.Background), "The actual child Popup opens through the MenuItem contract and paints a dark surface");
                    Snapshot(sample, Path.Combine(directory, "context-template.png")); Snapshot(submenuSurface, Path.Combine(directory, "context-submenu.png"));
                    submenu.IsSubmenuOpen = false; sample.IsOpen = false; Pump();
                    Check(!popup.IsOpen && !sample.IsOpen, "Closing submenu and menu releases both Popup surfaces");
                }
            }
        }
        catch (Exception error) { Errors.Add(error.ToString()); }
        finally { if (sample != null) sample.IsOpen = false; if (product != null) { if (product.ContextMenu != null) product.ContextMenu.IsOpen = false; ((IDisposable)product).Dispose(); } if (window != null) window.Close(); if (showWindows) Pump(); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MeasureMenu
    /// desc         : 창을 열지 않고 menu와 자식의 실제 template을 적용한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : source-only 검증에서 창이나 Popup을 열지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void MeasureMenu(ContextMenu menu) { menu.ApplyTemplate(); menu.Measure(new Size(600, 480)); foreach (MenuItem item in menu.Items.OfType<MenuItem>()) { item.ApplyTemplate(); var binding = item.GetBindingExpression(MenuItem.CommandTargetProperty); if (binding != null) binding.UpdateTarget(); } }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Dark / HasHeader
    /// desc         : 실제 메뉴 색과 기존 한국어 명령 항목을 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 밝은 텍스트를 흰 배경 결함으로 오인하지 않고 표면 브러시만 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool Dark(Brush value) { var brush = value as SolidColorBrush; return brush != null && brush.Color.A == 255 && brush.Color.R < 100 && brush.Color.G < 100 && brush.Color.B < 100; }
    private static bool HasHeader(ContextMenu menu, string text) { return menu.Items.OfType<MenuItem>().Any(item => Convert.ToString(item.Header) == text); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Descendants
    /// desc         : 실제 메시지 template에서 생성된 읽기 전용 입력란만 찾는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 IDE 문서를 탐색하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { DependencyObject child = VisualTreeHelper.GetChild(root, i); if (child is T) yield return (T)child; foreach (T item in Descendants<T>(child)) yield return item; } }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pump
    /// desc         : 메모리 바인딩 또는 예약된 실제 GUI의 dispatcher 처리를 유한 시간만 진행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : source-only는 바인딩 연결만 처리하고 창·Popup은 열지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Pump() { var frame = new DispatcherFrame(); var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) }; timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Snapshot
    /// desc         : 검증 메뉴 표면만 PNG로 저장하여 사용자 데스크톱을 캡처하지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 Popup이 열린 fixed DLL 모드에서만 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Snapshot(FrameworkElement element, string path) { var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var output = File.Create(path)) encoder.Save(output); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 실제 통과한 검사만 누적하고 최초 실패를 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : source-only와 실제 UI 성공 수는 별도 결과로 기록.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); Checks.Add(name); }
}
