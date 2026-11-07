using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantKeyboardToolTip
    /// desc         : 잘린 버튼 문구를 키보드 초점에서도 보여 주는 선택적 net462 호환 UI 동작이다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// update date  : 2026-11-03 CodexOrig : 세 문구 생략 그룹에만 적용하며 호스트 설정이나 기존 ToolTip 바인딩은 바꾸지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class AssistantKeyboardToolTip
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(AssistantKeyboardToolTip), new PropertyMetadata(false, IsEnabledChanged));

        private static readonly DependencyProperty OpenTipProperty = DependencyProperty.RegisterAttached(
            "OpenTip", typeof(ToolTip), typeof(AssistantKeyboardToolTip), new PropertyMetadata(null));

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetIsEnabled
        /// desc         : 해당 컨트롤의 키보드 문구 안내 사용 여부를 읽는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        public static bool GetIsEnabled(DependencyObject target)
        {
            if (target == null) throw new ArgumentNullException("target");
            target.Dispatcher.VerifyAccess();
            return (bool)target.GetValue(IsEnabledProperty);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SetIsEnabled
        /// desc         : XAML과 코드로 만든 버튼에 동일한 선택적 키보드 안내를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void SetIsEnabled(DependencyObject target, bool value)
        {
            if (target == null) throw new ArgumentNullException("target");
            target.Dispatcher.VerifyAccess();
            target.SetValue(IsEnabledProperty, value);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsEnabledChanged
        /// desc         : 컨트롤 자신의 이벤트만 연결·해제하고 비활성화 시 소유한 안내를 닫는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void IsEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            var element = target as FrameworkElement;
            if (element == null) return;
            element.Dispatcher.VerifyAccess();
            element.GotKeyboardFocus -= FocusEntered;
            element.LostKeyboardFocus -= FocusLeft;
            element.PreviewKeyDown -= KeyPressed;
            element.Unloaded -= Unloaded;
            Close(element);
            if (!(bool)args.NewValue) return;
            element.GotKeyboardFocus += FocusEntered;
            element.LostKeyboardFocus += FocusLeft;
            element.PreviewKeyDown += KeyPressed;
            element.Unloaded += Unloaded;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FocusEntered
        /// desc         : 실제로 보이고 초점을 받은 컨트롤의 현재 문자열만 별도 안내로 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void FocusEntered(object sender, KeyboardFocusChangedEventArgs args)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            element.Dispatcher.VerifyAccess();
            if (!element.IsLoaded || !element.IsVisible || !element.IsKeyboardFocusWithin) return;
            var text = ToolTipService.GetToolTip(element) as string;
            if (String.IsNullOrWhiteSpace(text)) return;
            Close(element);
            var words = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };
            var tip = new ToolTip
            {
                Content = words,
                PlacementTarget = element,
                Placement = PlacementMode.Bottom,
                StaysOpen = true,
                Focusable = false,
                IsHitTestVisible = false,
                MaxWidth = 450
            };
            tip.SetResourceReference(Control.BackgroundProperty, "AssistantControlBackgroundBrush");
            tip.SetResourceReference(Control.ForegroundProperty, "AssistantForegroundBrush");
            tip.SetResourceReference(Control.BorderBrushProperty, "AssistantBorderBrush");
            element.SetValue(OpenTipProperty, tip);
            tip.IsOpen = true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FocusLeft
        /// desc         : 같은 컨트롤 내부 이동은 보존하고 초점이 벗어나면 안내를 닫는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void FocusLeft(object sender, KeyboardFocusChangedEventArgs args)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            element.Dispatcher.VerifyAccess();
            if (!element.IsKeyboardFocusWithin) Close(element);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : KeyPressed
        /// desc         : Escape는 열린 키보드 안내만 먼저 닫고 다음 Escape의 상위 동작은 유지한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void KeyPressed(object sender, KeyEventArgs args)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            element.Dispatcher.VerifyAccess();
            var tip = element.GetValue(OpenTipProperty) as ToolTip;
            if (args.Key != Key.Escape || tip == null || !tip.IsOpen) return;
            Close(element);
            args.Handled = true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Unloaded
        /// desc         : 화면이나 행이 닫히면 이 동작이 만든 안내만 정리한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Unloaded(object sender, RoutedEventArgs args)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            element.Dispatcher.VerifyAccess();
            Close(element);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Close
        /// desc         : 원래 ToolTip 값·바인딩·서비스 설정을 보존하고 임시 팝업의 참조만 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Close(FrameworkElement element)
        {
            element.Dispatcher.VerifyAccess();
            var tip = element.GetValue(OpenTipProperty) as ToolTip;
            element.ClearValue(OpenTipProperty);
            if (tip == null) return;
            tip.IsOpen = false;
            tip.PlacementTarget = null;
            tip.Content = null;
        }
    }
}
