using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 설정·안내 메뉴의 부채꼴 배치와 포인터·키보드 공통 열림 상태를 관리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-017 원점 위 버튼 이동과 반복 hover 애니메이션 제거.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private DispatcherTimer fanCloseTimer;
        private bool fanMenuInitialized, fanMenuOpen, fanMenuDisposed;
        private int fanOpenCount;
        private Path fanBackdrop;
        private Window fanOwnerWindow;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeFanMenu
        /// desc         : 원점 아래의 고정 버튼과 부채꼴 배경 및 지연 닫기 이벤트를 한 번 준비한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 상단 40픽셀의 원점에는 메뉴 hit 영역을 생성하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InitializeFanMenu()
        {
            if (fanMenuInitialized || fanMenuDisposed) return;
            fanMenuInitialized = true;
            FanPanel.Width = 260; FanPanel.Height = 200;
            FanPanel.Background = null;
            FanPanel.Focusable = false;
            FanPanel.Visibility = Visibility.Collapsed;
            for (int index = FanPanel.Children.Count - 1; index >= 0; index--)
                if (FanPanel.Children[index] is Border) FanPanel.Children.RemoveAt(index);

            // 원점 (240,24)에서 아래·왼쪽으로 펼치되 위쪽 40픽셀은 잘라 트리거를 가리지 않는다.
            fanBackdrop = new Path {
                Data = Geometry.Parse("M 240,24 L 240,190 A 166,166 0 0 1 74,24 Z"),
                Fill = new SolidColorBrush(Color.FromArgb(245, 39, 39, 39)),
                Stroke = new SolidColorBrush(Color.FromRgb(83, 83, 83)),
                StrokeThickness = 1,
                Clip = new RectangleGeometry(new Rect(0, 40, 260, 160)),
                IsHitTestVisible = true
            };
            FanPanel.Children.Insert(0, fanBackdrop);
            ConfigureFanButton(HelpButton, 62, 64, 124, 0);
            ConfigureFanButton(SettingsButton, 161, 122, 94, 1);
            fanCloseTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher) { Interval = TimeSpan.FromMilliseconds(175) };
            fanCloseTimer.Tick += FanCloseTick;
            OptionsToggle.MouseEnter += FanRegionEntered;
            OptionsToggle.MouseLeave += FanRegionLeft;
            FanPanel.MouseEnter += FanRegionEntered;
            FanPanel.MouseLeave += FanRegionLeft;
            OptionsToggle.GotKeyboardFocus += FanFocusEntered;
            FanPanel.GotKeyboardFocus += FanFocusEntered;
            OptionsToggle.LostKeyboardFocus += FanFocusLeft;
            FanPanel.LostKeyboardFocus += FanFocusLeft;
            Loaded += FanMenuLoaded;
            Unloaded += FanMenuUnloaded;
            AttachFanOwner();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConfigureFanButton
        /// desc         : 서로 다른 각도의 고정 버튼 영역을 배치하고 기존 이동 효과를 제거한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 버튼 자체는 움직이지 않아 포인터 아래로 날아들지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ConfigureFanButton(Button button, double left, double top, double width, int tabIndex)
        {
            Canvas.SetLeft(button, left); Canvas.SetTop(button, top);
            button.Width = width; button.MinHeight = 36;
            button.Margin = new Thickness(0); button.Padding = new Thickness(7, 6, 7, 6);
            button.RenderTransform = Transform.Identity;
            button.TabIndex = tabIndex;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenFanMenu
        /// desc         : 닫힌 메뉴만 한 번 펼치고 열린 영역 재진입에서는 닫기 예약만 취소한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : fanOpenCount로 실제 열림과 hover 재진입을 구분해 검증 가능.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OpenFanMenu()
        {
            if (fanMenuDisposed || disposed || busy) { CloseFanMenu(); return; }
            InitializeFanMenu();
            fanCloseTimer.Stop(); OptionsToggle.IsChecked = true;
            if (fanMenuOpen) return;
            fanMenuOpen = true; fanOpenCount++;
            FanPanel.Visibility = Visibility.Visible;
            fanBackdrop.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            AnimateFanContent(HelpButton, -5, 4, 0);
            AnimateFanContent(SettingsButton, -2, 6, 55);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AnimateFanContent
        /// desc         : 고정 hit 영역 안의 글자만 짧게 이동·확대하고 버튼을 서서히 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 두 메뉴의 다른 방향과 시간차로 펼침을 표현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void AnimateFanContent(Button button, double x, double y, int delay)
        {
            var duration = TimeSpan.FromMilliseconds(175);
            var fade = new DoubleAnimation(0, 1, duration) { BeginTime = TimeSpan.FromMilliseconds(delay) };
            button.BeginAnimation(OpacityProperty, fade);
            button.ApplyTemplate();
            var content = FindFanContent(button);
            if (content == null) return;
            var scale = new ScaleTransform(); var move = new TranslateTransform();
            var transforms = new TransformGroup(); transforms.Children.Add(scale); transforms.Children.Add(move);
            content.RenderTransformOrigin = new Point(0.5, 0.5); content.RenderTransform = transforms;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, duration));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, duration));
            move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, 0, duration));
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, 0, duration));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FindFanContent
        /// desc         : 버튼 템플릿에서 표시 내용만 찾아 버튼의 고정 포인터 영역을 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 템플릿에 내용 표시자가 없으면 이동 효과를 생략.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static ContentPresenter FindFanContent(DependencyObject element)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
            {
                var child = VisualTreeHelper.GetChild(element, index);
                var content = child as ContentPresenter;
                if (content != null) return content;
                content = FindFanContent(child); if (content != null) return content;
            }
            return null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseFanMenu
        /// desc         : 지연 닫기와 메뉴 표시를 함께 정리해 Escape·취소 뒤 다시 열리지 않게 한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 반복 호출해도 같은 닫힘 상태를 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CloseFanMenu()
        {
            if (fanCloseTimer != null) fanCloseTimer.Stop();
            bool restoreFocus = FanPanel.IsKeyboardFocusWithin && OptionsToggle.IsVisible &&
                !disposed && (fanOwnerWindow == null || fanOwnerWindow.IsActive);
            fanMenuOpen = false;
            FanPanel.Visibility = Visibility.Collapsed;
            OptionsToggle.IsChecked = false;
            if (restoreFocus) OptionsToggle.Focus();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ScheduleFanClose
        /// desc         : 원점과 부채꼴 사이 이동을 허용하고 175밀리초 후 실제 영역·포커스를 다시 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 화면 경계의 즉시 닫힘과 반복 열림 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ScheduleFanClose()
        {
            if (!fanMenuOpen || fanMenuDisposed || fanCloseTimer == null) return;
            fanCloseTimer.Stop(); fanCloseTimer.Start();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FanCloseTick
        /// desc         : 실제 hit 영역이나 내부 키보드 포커스가 없을 때만 메뉴를 접는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 전체 사각 Canvas 대신 표시된 자식의 실제 마우스 범위를 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FanCloseTick(object sender, EventArgs args)
        {
            fanCloseTimer.Stop();
            if (!fanMenuOpen || fanMenuDisposed) return;
            if (OptionsToggle.IsMouseOver || FanPanel.IsMouseOver || OptionsToggle.IsKeyboardFocusWithin || FanPanel.IsKeyboardFocusWithin) return;
            CloseFanMenu();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FanRegionEntered
        /// desc         : 트리거나 메뉴에 들어온 실제 포인터의 닫기 예약을 취소한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 이 이벤트에서는 펼침 애니메이션을 시작하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FanRegionEntered(object sender, MouseEventArgs args)
        { if (fanCloseTimer != null) fanCloseTimer.Stop(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FanRegionLeft
        /// desc         : 트리거와 메뉴 사이의 포인터 이동에 공통 닫기 유예 시간을 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 원점과 부채꼴을 하나의 hover 흐름으로 취급.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FanRegionLeft(object sender, MouseEventArgs args) { ScheduleFanClose(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FanFocusEntered
        /// desc         : 키보드로 메뉴를 탐색하는 동안 닫기 예약을 취소한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 포인터가 밖에 있어도 Tab 탐색을 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FanFocusEntered(object sender, KeyboardFocusChangedEventArgs args)
        { if (fanCloseTimer != null) fanCloseTimer.Stop(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FanFocusLeft
        /// desc         : 포커스 이동이 완료된 뒤 내부 유지 여부를 지연 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 메뉴 내부 이동과 외부 탈출을 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FanFocusLeft(object sender, KeyboardFocusChangedEventArgs args) { ScheduleFanClose(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FanMenuLoaded
        /// desc         : 도킹·재표시된 화면의 실제 소유 창에 비활성 닫기 동작을 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 창 이동 후 이전 창 이벤트 참조를 남기지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FanMenuLoaded(object sender, RoutedEventArgs args) { AttachFanOwner(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AttachFanOwner
        /// desc         : 현재 소유 창이 바뀐 경우에만 비활성 이벤트를 교체한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : VS 도킹 해제와 독립 테스트 창을 동일하게 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AttachFanOwner()
        {
            if (fanMenuDisposed) return;
            var owner = Window.GetWindow(this); if (owner == fanOwnerWindow) return;
            if (fanOwnerWindow != null) fanOwnerWindow.Deactivated -= FanOwnerDeactivated;
            fanOwnerWindow = owner;
            if (fanOwnerWindow != null) fanOwnerWindow.Deactivated += FanOwnerDeactivated;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FanOwnerDeactivated
        /// desc         : 다른 창으로 전환될 때 내부 키보드 포커스가 남아 있어도 메뉴를 닫는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 창 비활성 상태에서 메뉴 잔류 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FanOwnerDeactivated(object sender, EventArgs args) { CloseFanMenu(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FanMenuUnloaded
        /// desc         : 숨겨진 도구 창의 닫기 예약과 이전 소유 창 참조를 제거한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 다시 Loaded될 경우 정상적으로 재연결 가능.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FanMenuUnloaded(object sender, RoutedEventArgs args)
        {
            CloseFanMenu();
            if (fanOwnerWindow != null) fanOwnerWindow.Deactivated -= FanOwnerDeactivated;
            fanOwnerWindow = null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeFanMenu
        /// desc         : 종료된 컨트롤의 타이머와 입력·소유 창 이벤트를 모두 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 열린 메뉴 종료와 중복 Dispose를 안전하게 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeFanMenu()
        {
            if (fanMenuDisposed) return;
            CloseFanMenu(); fanMenuDisposed = true;
            if (fanCloseTimer != null) { fanCloseTimer.Tick -= FanCloseTick; fanCloseTimer = null; }
            OptionsToggle.MouseEnter -= FanRegionEntered; OptionsToggle.MouseLeave -= FanRegionLeft;
            FanPanel.MouseEnter -= FanRegionEntered; FanPanel.MouseLeave -= FanRegionLeft;
            OptionsToggle.GotKeyboardFocus -= FanFocusEntered; FanPanel.GotKeyboardFocus -= FanFocusEntered;
            OptionsToggle.LostKeyboardFocus -= FanFocusLeft; FanPanel.LostKeyboardFocus -= FanFocusLeft;
            Loaded -= FanMenuLoaded; Unloaded -= FanMenuUnloaded;
            if (fanOwnerWindow != null) fanOwnerWindow.Deactivated -= FanOwnerDeactivated;
            fanOwnerWindow = null;
        }
    }
}
