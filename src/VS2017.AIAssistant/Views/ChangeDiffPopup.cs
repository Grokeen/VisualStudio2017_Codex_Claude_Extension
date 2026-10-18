using System;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace VS2017.AIAssistant.Views
{
    /// <summary>
    /// name         : PopupPalette
    /// desc         : 별도 native popup에도 실제 AssistantControl의 동일 리소스 사전·동적 palette를 공유한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : bounded 부모 탐색과 standalone Theme fallback, 복사한 고정색 없음.
    /// </summary>
    internal static class PopupPalette
    {
        private static readonly ConditionalWeakTable<FrameworkElement, Scope> Scopes = new ConditionalWeakTable<FrameworkElement, Scope>();
        /// <summary>
        /// name         : Scope
        /// desc         : 소유 root의 변경 가능한 사전 참조만 중복 없이 추적한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 약한 key로 분리된 popup을 전역에 보존하지 않음.
        /// </summary>
        private sealed class Scope { internal ResourceDictionary Host; }
        /// <summary>
        /// name         : Attach
        /// desc         : root.Resources 동일 인스턴스를 마지막 merge에 놓아 열린 popup도 테마 변경을 받는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : fixture에는 명시적인 dark semantic 기본값만 제공.
        /// </summary>
        internal static void Attach(FrameworkElement target, FrameworkElement anchor)
        {
            var scope = Scopes.GetValue(target, key =>
            {
                var fallback = new ResourceDictionary();
                // source-only 독립 compile에는 BAML이 없다. 실제 제품은 반드시 동일 AssistantTheme 템플릿을 사용한다.
                if (typeof(PopupPalette).Assembly.GetManifestResourceNames().Any(name => name.EndsWith(".g.resources", StringComparison.Ordinal)))
                    fallback.Source = new Uri("/" + typeof(PopupPalette).Assembly.GetName().Name + ";component/Views/AssistantTheme.xaml", UriKind.Relative);
                else
                {
                    string[] names = { "AssistantPopupBackgroundBrush", "AssistantPopupBorderBrush", "AssistantForegroundBrush", "AssistantMutedForegroundBrush", "AssistantCodeBackgroundBrush", "AssistantSurfaceBrush", "AssistantAddedBrush", "AssistantRemovedBrush", "AssistantLinkBrush", "AssistantBorderBrush", "AssistantBackgroundBrush", "AssistantSelectionBrush" };
                    string[] colors = { "#25272C", "#515867", "#D3D3D3", "#808080", "#1D1E20", "#242424", "#57CC8A", "#FF7373", "#79C5F5", "#424242", "#1E1E1E", "#385E88" };
                    for (int index = 0; index < names.Length; index++) fallback[names[index]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[index]));
                }
                target.Resources.MergedDictionaries.Add(fallback); return new Scope();
            });
            ResourceDictionary host = null; int depth = 0;
            for (DependencyObject node = anchor; node != null && depth++ < 256; node = HoverScrollPolicy.Parent(node))
            { var control = node as AssistantControl; if (control != null) { host = control.Resources; break; } }
            // native PopupRoot의 Loaded 경로에서는 anchor 쪽 logical tree가 보이지 않는다. 기존 host 공유는 유지한다.
            if (host == null || ReferenceEquals(host, scope.Host)) return;
            if (scope.Host != null) target.Resources.MergedDictionaries.Remove(scope.Host);
            scope.Host = host;
            if (host != null && !ReferenceEquals(host, target.Resources)) target.Resources.MergedDictionaries.Add(host);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ChangeDiffPopup
    /// desc         : 파일 링크의 기존 클릭을 보존하면서 실제 비교를 읽을 수 있는 hover Dropdown으로 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK024 native capture 없는 고정·Escape·바깥 클릭 수명 관리.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ChangeDiffPopup
    {
        private static readonly ConditionalWeakTable<FrameworkElement, ChangeDiffPopup> Attached = new ConditionalWeakTable<FrameworkElement, ChangeDiffPopup>();
        private readonly FrameworkElement anchor;
        private readonly Popup popup;
        private readonly Border body;
        private readonly DiffPreviewPresenter preview;
        private readonly DispatcherTimer closeTimer;
        private readonly Button pin;
        private ChangeCardFile file;
        private string history;
        private bool pinned;
        private Window owner;

        /// <summary>
        /// name         : ChangeDiffPopup
        /// desc         : 실제 사용자 동작 전에는 창을 열지 않고 고정된 경량 컨테이너만 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 링크 클릭은 원래 소스 이동 또는 파일 선택 처리에 남김.
        /// </summary>
        private ChangeDiffPopup(FrameworkElement target)
        {
            anchor = target; preview = new DiffPreviewPresenter();
            var surface = new Grid(); surface.Children.Add(preview);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 4, 0) };
            pin = new Button { Content = "◇", Width = 24, Height = 25, ToolTip = "상세 고정 / 해제", Background = Brushes.Transparent, Foreground = Brushes.Gainsboro, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
            var close = new Button { Content = "×", Width = 24, Height = 25, ToolTip = "비교 닫기 (Escape)", Background = Brushes.Transparent, Foreground = Brushes.Gainsboro, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
            actions.Children.Add(pin); actions.Children.Add(close); surface.Children.Add(actions);
            body = new Border { Child = surface, BorderBrush = new SolidColorBrush(Color.FromRgb(82, 85, 92)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromRgb(29, 30, 32)), ClipToBounds = true };
            PopupPalette.Attach(body, anchor);
            body.SetResourceReference(Border.BackgroundProperty, "AssistantPopupBackgroundBrush"); body.SetResourceReference(Border.BorderBrushProperty, "AssistantPopupBorderBrush");
            pin.SetResourceReference(Control.ForegroundProperty, "AssistantForegroundBrush"); close.SetResourceReference(Control.ForegroundProperty, "AssistantForegroundBrush");
            popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = true, AllowsTransparency = true, PopupAnimation = PopupAnimation.Fade, Child = body };
            closeTimer = new DispatcherTimer(DispatcherPriority.Background, anchor.Dispatcher) { Interval = TimeSpan.FromMilliseconds(240) };
            closeTimer.Tick += CloseTick;
            anchor.MouseEnter += Entered; anchor.MouseLeave += Left; anchor.PreviewKeyDown += KeyPressed; anchor.Unloaded += Unloaded;
            body.MouseEnter += Entered; body.MouseLeave += Left; body.PreviewKeyDown += KeyPressed; body.LostKeyboardFocus += FocusLeft;
            pin.Click += (sender, args) => { pinned = !pinned; pin.Content = pinned ? "◆" : "◇"; if (!pinned) closeTimer.Start(); };
            close.Click += (sender, args) => Close(true);
            popup.Closed += (sender, args) => { closeTimer.Stop(); pinned = false; pin.Content = "◇"; DetachOwner(); };
            System.Windows.Automation.AutomationProperties.SetName(pin, "변경 비교 고정 또는 해제");
            System.Windows.Automation.AutomationProperties.SetName(close, "변경 비교 닫기");
        }

        /// <summary>
        /// name         : Attach
        /// desc         : 같은 링크에는 한 controller만 두고 현재 대화의 실제 파일·기록으로 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Tooltip 장문 대신 유한 상세, 컨테이너 중복 구독 방지.
        /// </summary>
        public static void Attach(FrameworkElement target, ChangeCardFile file, string history)
        {
            if (target == null || file == null) return;
            if (!target.Dispatcher.CheckAccess()) throw new InvalidOperationException("변경 Dropdown은 WPF UI 스레드에서 연결하세요.");
            var controller = Attached.GetValue(target, key => new ChangeDiffPopup(key));
            controller.Close(); controller.file = file; controller.history = history ?? "";
            ToolTipService.SetIsEnabled(target, false);
            System.Windows.Automation.AutomationProperties.SetHelpText(target, "변경 비교: 마우스를 올리거나 Alt+아래 · 기존 클릭 동작 유지");
        }

        /// <summary>
        /// name         : CloseWithin
        /// desc         : 대화 전환·뷰 종료에서 해당 시각 트리의 Dropdown만 정리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 다른 창이나 원본 소스에는 접근하지 않음.
        /// </summary>
        public static void CloseWithin(DependencyObject root)
        {
            var element = root as FrameworkElement; ChangeDiffPopup controller;
            if (element != null && Attached.TryGetValue(element, out controller)) controller.Close();
            if (!(root is Visual)) return;
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++) CloseWithin(VisualTreeHelper.GetChild(root, index));
        }

        /// <summary>
        /// name         : Open
        /// desc         : 화면 작업영역 이내의 유한 비교를 열며 hover는 현재 키보드 초점을 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : native capture 없이 C11 바깥 클릭 보호 유지.
        /// </summary>
        private void Open(bool keyboard)
        {
            if (!anchor.IsLoaded || file == null) return;
            closeTimer.Stop();
            if (!popup.IsOpen)
            {
                PopupPalette.Attach(body, anchor); PopupPalette.Attach(preview, anchor);
                preview.Present(file, history);
                body.Width = Math.Max(170, Math.Min(680, SystemParameters.WorkArea.Width - 24));
                body.Height = Math.Max(120, Math.Min(420, SystemParameters.WorkArea.Height - 40));
                owner = Window.GetWindow(anchor);
                if (owner != null) { owner.Deactivated += OwnerDeactivated; owner.PreviewMouseDown += OwnerPressed; }
                popup.IsOpen = true;
            }
            if (keyboard) { pinned = true; pin.Content = "◆"; pin.Focus(); }
        }

        /// <summary>
        /// name         : Close
        /// desc         : 닫힘·타이머·소유 창 구독을 정리하고 요청한 경우에만 링크로 초점을 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Unloaded 시 초점을 되가져오지 않음.
        /// </summary>
        private void Close(bool restoreFocus = false)
        { closeTimer.Stop(); pinned = false; pin.Content = "◇"; popup.IsOpen = false; DetachOwner(); if (restoreFocus && anchor.IsLoaded && anchor.Focusable) anchor.Focus(); }

        /// <summary>
        /// name         : DetachOwner
        /// desc         : 같은 owner 이벤트를 반복 구독하거나 분리된 카드에 남기지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 지연 닫기와 창 종료의 중복 정리 허용.
        /// </summary>
        private void DetachOwner() { if (owner != null) { owner.Deactivated -= OwnerDeactivated; owner.PreviewMouseDown -= OwnerPressed; owner = null; } }
        /// <summary>
        /// name         : Entered
        /// desc         : 링크와 popup 사이 진입 유예를 취소하고 공개 비교만 연다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : popup 본문 재진입은 scroll 위치를 초기화하지 않음.
        /// </summary>
        private void Entered(object sender, MouseEventArgs args) { if (ReferenceEquals(sender, anchor)) Open(false); else closeTimer.Stop(); }
        /// <summary>
        /// name         : Left
        /// desc         : 고정되지 않은 상세의 포인터 이동에 짧은 grace를 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 스크롤바 이동 중 깜빡임 방지.
        /// </summary>
        private void Left(object sender, MouseEventArgs args) { if (!pinned) closeTimer.Start(); }
        /// <summary>
        /// name         : CloseTick
        /// desc         : 본문·anchor·키보드 초점 모두 떠났을 때만 닫는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 끝 위치 휠은 팝업 수명을 바꾸지 않음.
        /// </summary>
        private void CloseTick(object sender, EventArgs args) { closeTimer.Stop(); if (!pinned && !anchor.IsMouseOver && !body.IsMouseOver && !body.IsKeyboardFocusWithin) Close(); }
        /// <summary>
        /// name         : KeyPressed
        /// desc         : Alt+아래는 원래 링크 동작을 보존하며 고정 상세를 열고 Escape는 닫는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Enter 소스 이동·행 선택 계약을 변경하지 않음.
        /// </summary>
        private void KeyPressed(object sender, KeyEventArgs args) { if (args.Key == Key.Escape && popup.IsOpen) { Close(true); args.Handled = true; } else if ((args.Key == Key.Down || args.SystemKey == Key.Down) && Keyboard.Modifiers == ModifierKeys.Alt) { Open(true); args.Handled = true; } }
        /// <summary>
        /// name         : FocusLeft
        /// desc         : 키보드가 상세를 벗어나면 고정을 풀고 지연 닫기를 예약한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Tab 탐색이 popup에 갇히지 않게 함.
        /// </summary>
        private void FocusLeft(object sender, KeyboardFocusChangedEventArgs args) { if (!body.IsKeyboardFocusWithin && !anchor.IsKeyboardFocusWithin) { pinned = false; closeTimer.Start(); } }
        /// <summary>
        /// name         : Unloaded
        /// desc         : 행 제거·대화 전환 시 해당 상세만 닫는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 실제 소스 파일과 저장 기록은 보존.
        /// </summary>
        private void Unloaded(object sender, RoutedEventArgs args) { Close(); }
        /// <summary>
        /// name         : OwnerDeactivated
        /// desc         : 다른 창으로 이동할 때 떠 있는 상세를 닫는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 다른 앱으로 포커스를 강제 이동하지 않음.
        /// </summary>
        private void OwnerDeactivated(object sender, EventArgs args) { Close(); }
        /// <summary>
        /// name         : OwnerPressed
        /// desc         : 소유 창의 빈 영역 클릭도 고정 상세를 닫게 한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : StaysOpen=false native capture 경합 재도입 금지.
        /// </summary>
        private void OwnerPressed(object sender, MouseButtonEventArgs args) { if (!anchor.IsMouseOver && !body.IsMouseOver) Close(); }
    }
}
