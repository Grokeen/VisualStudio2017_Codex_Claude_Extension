using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
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
    /// update date  : 2026-10-18 CodexCode : TASK-021 두 버튼의 표시·클릭 영역을 실제 환형 부채꼴로 변경.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private DispatcherTimer fanCloseTimer;
        private bool fanMenuInitialized, fanMenuOpen, fanMenuDisposed;
        private int fanOpenCount;
        private Geometry fanHelpSector, fanSettingsSector;
        private ScaleTransform fanLayoutScale;
        private Rect fanClipTriggerBounds = Rect.Empty;
        private double fanClipScale = double.NaN;
        private Window fanOwnerWindow;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeFanMenu
        /// desc         : 원점 아래의 고정 버튼과 부채꼴 배경 및 지연 닫기 이벤트를 한 번 준비한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 상단 40픽셀의 원점에는 메뉴 hit 영역을 생성하지 않음.
        /// update date  : 2026-10-18 CodexCode : TASK-021 사각 버튼과 공용 배경을 독립된 두 환형 부채꼴 버튼으로 교체.
        /// update date  : 2026-10-18 CodexCode : 실제 트리거 경계는 레이아웃 완료 후 다시 계산해 축소 시 겹침 방지.
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
                if (FanPanel.Children[index] is Border || FanPanel.Children[index] is Path) FanPanel.Children.RemoveAt(index);

            // 두 구획은 별도 버튼이며 중앙의 빈 원과 바깥 사각형 모서리는 클릭 영역에서 제외한다.
            fanHelpSector = CreateFanSector(123, 158);
            fanSettingsSector = CreateFanSector(90, 121);
            ConfigureFanButton(HelpButton, fanHelpSector, 140.5, "?", "안내", "사용 안내", 0);
            ConfigureFanButton(SettingsButton, fanSettingsSector, 105.5, "⚙", "설정", "설정", 1);
            fanLayoutScale = new ScaleTransform(1, 1);
            FanPanel.LayoutTransform = fanLayoutScale;
            FanPanel.LayoutUpdated += FanMenuLayoutUpdated;
            UpdateFanScale();
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
            SizeChanged += FanMenuSizeChanged;
            AttachFanOwner();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConfigureFanButton
        /// desc         : 각 버튼의 Path 템플릿과 같은 부채꼴 Clip을 적용해 표시·클릭 모양을 일치시킨다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 버튼 자체는 움직이지 않아 포인터 아래로 날아들지 않음.
        /// update date  : 2026-10-18 CodexCode : TASK-021 사각 테두리 없는 환형 버튼에 명도·포커스·비활성 상태 제공.
        ///                2026-10-18 CodexOrig : 안내 구획의 중심 각도·가변 높이 글자 중앙 정렬.
        ///                2026-10-18 CodexCode : TASK-022 고정 caption 구역 안에 Grid와 양축 정렬로 아이콘·문구 중심 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ConfigureFanButton(Button button, Geometry sector, double angle, string icon, string label, string accessibleName, int tabIndex)
        {
            Canvas.SetLeft(button, 0); Canvas.SetTop(button, 0);
            button.Width = 260; button.Height = 200; button.MinHeight = 0;
            button.Margin = new Thickness(0); button.Padding = new Thickness(0);
            button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalContentAlignment = VerticalAlignment.Center;
            button.RenderTransform = Transform.Identity;
            button.Clip = sector; button.FocusVisualStyle = null;
            button.Background = new SolidColorBrush(Color.FromRgb(36, 36, 36));
            button.BorderBrush = new SolidColorBrush(Color.FromRgb(82, 82, 82));
            button.SetBinding(Control.ForegroundProperty, new Binding("Foreground") {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(AssistantControl), 1) });
            button.TabIndex = tabIndex;
            AutomationProperties.SetName(button, accessibleName);

            var root = new FrameworkElementFactory(typeof(Canvas)); root.Name = "FanSurface";
            var shape = new FrameworkElementFactory(typeof(Path)); shape.Name = "FanSector";
            shape.SetValue(Path.DataProperty, sector);
            shape.SetBinding(Shape.FillProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            shape.SetBinding(Shape.StrokeProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            shape.SetValue(Shape.StrokeThicknessProperty, 1.0); root.AppendChild(shape);
            var captionHost = new FrameworkElementFactory(typeof(Grid));
            var caption = new FrameworkElementFactory(typeof(StackPanel));
            caption.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            caption.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            var symbol = new FrameworkElementFactory(typeof(TextBlock));
            symbol.SetValue(TextBlock.TextProperty, icon); symbol.SetValue(TextBlock.FontSizeProperty, 20.0);
            symbol.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            symbol.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center); caption.AppendChild(symbol);
            var words = new FrameworkElementFactory(typeof(TextBlock));
            words.SetValue(TextBlock.TextProperty, label); words.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            words.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            words.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); words.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 3, 0, 0));
            caption.AppendChild(words);
            captionHost.AppendChild(caption);
            var center = FanPolarPoint(126, angle);
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.Name = "FanCaption";
            presenter.SetValue(FrameworkElement.WidthProperty, 72.0); presenter.SetValue(FrameworkElement.HeightProperty, 72.0);
            presenter.SetValue(Canvas.LeftProperty, center.X - 36); presenter.SetValue(Canvas.TopProperty, center.Y - 36);
            presenter.SetValue(UIElement.IsHitTestVisibleProperty, false);
            presenter.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { RelativeSource = RelativeSource.TemplatedParent });
            presenter.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
            presenter.SetValue(ContentPresenter.ContentTemplateProperty, new DataTemplate { VisualTree = captionHost });
            root.AppendChild(presenter);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = root };
            var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Shape.FillProperty, new SolidColorBrush(Color.FromRgb(57, 57, 57)), "FanSector"));
            template.Triggers.Add(hover);
            var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Shape.FillProperty, new SolidColorBrush(Color.FromRgb(75, 75, 75)), "FanSector"));
            template.Triggers.Add(pressed);
            var focus = new Trigger { Property = IsKeyboardFocusWithinProperty, Value = true };
            focus.Setters.Add(new Setter(Shape.StrokeProperty, new SolidColorBrush(Color.FromRgb(216, 224, 255)), "FanSector"));
            focus.Setters.Add(new Setter(Shape.StrokeThicknessProperty, 2.0, "FanSector")); template.Triggers.Add(focus);
            var disabled = new Trigger { Property = IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(OpacityProperty, 0.4, "FanSurface")); template.Triggers.Add(disabled);
            button.Template = template;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateFanSector
        /// desc         : 같은 원점과 안쪽·바깥쪽 반지름을 가진 환형 부채꼴을 읽기 전용 경로로 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 두 구획 사이의 2도 간격과 원점의 빈 원을 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Geometry CreateFanSector(double startAngle, double endAngle)
        {
            var geometry = new StreamGeometry();
            using (var drawing = geometry.Open())
            {
                drawing.BeginFigure(FanPolarPoint(174, startAngle), true, true);
                drawing.ArcTo(FanPolarPoint(174, endAngle), new Size(174, 174), 0, false, SweepDirection.Clockwise, true, false);
                drawing.LineTo(FanPolarPoint(64, endAngle), true, false);
                drawing.ArcTo(FanPolarPoint(64, startAngle), new Size(64, 64), 0, false, SweepDirection.Counterclockwise, true, false);
            }
            geometry.Freeze(); return geometry;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FanPolarPoint
        /// desc         : 우측 상단 메뉴 원점에서 각도와 반지름에 해당하는 위치를 계산한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 표시 경로와 설명 글자를 같은 좌표계에 배치.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Point FanPolarPoint(double radius, double degrees)
        { double radians = degrees * Math.PI / 180; return new Point(240 + radius * Math.Cos(radians), 20 + radius * Math.Sin(radians)); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FanMenuSizeChanged
        /// desc         : 도구 창 너비가 바뀔 때 원래 클릭 모양을 유지하며 메뉴 전체 크기를 조절한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 200픽셀 도킹 창에서도 메뉴가 좌측으로 잘리지 않도록 축소.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FanMenuSizeChanged(object sender, SizeChangedEventArgs args) { UpdateFanScale(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FanMenuLayoutUpdated
        /// desc         : 실제 배치·축소 변환이 확정된 뒤 메뉴 원점의 제외 영역을 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 스크롤바·본문 여백·탭 높이 차이와 무관한 클릭 영역 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FanMenuLayoutUpdated(object sender, EventArgs args)
        { if (fanMenuOpen) UpdateFanClips(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateFanScale
        /// desc         : 너비에 맞춘 균일 축소와 실제 화면 상단 40픽셀의 클릭 제외 영역을 함께 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 축소 후에도 트리거 영역과 부채꼴 클릭 영역 분리.
        /// update date  : 2026-10-18 CodexCode : 고정 40픽셀 가정 외에 실제 원점 경계도 제외하도록 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateFanScale()
        {
            if (fanLayoutScale == null || fanMenuDisposed) return;
            double available = ActualWidth > 0 ? ActualWidth : (Width > 0 && !double.IsInfinity(Width) ? Width : 268);
            double scale = Math.Min(1, Math.Max(120, available - 8) / 260);
            fanLayoutScale.ScaleX = scale; fanLayoutScale.ScaleY = scale;
            UpdateFanClips();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateFanClips
        /// desc         : 실제 트리거 경계를 메뉴 좌표로 바꾸어 여유 4픽셀과 함께 두 버튼에서 제외한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 후보 제품 200픽셀 창에서 트리거 하단 2.62픽셀 겹침 보정.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateFanClips()
        {
            if (fanLayoutScale == null || fanMenuDisposed) return;
            double scale = fanLayoutScale.ScaleX;
            Rect triggerBounds = Rect.Empty;
            if (OptionsToggle.ActualWidth > 0 && OptionsToggle.ActualHeight > 0 && FanPanel.ActualWidth > 0)
            {
                triggerBounds = OptionsToggle.TransformToVisual(FanPanel).TransformBounds(new Rect(0, 0, OptionsToggle.ActualWidth, OptionsToggle.ActualHeight));
                triggerBounds.Inflate(4 / scale, 4 / scale);
            }
            if (scale == fanClipScale && triggerBounds == fanClipTriggerBounds) return;
            fanClipScale = scale; fanClipTriggerBounds = triggerBounds;
            double top = 40 / scale;
            Geometry allowed = new RectangleGeometry(new Rect(0, top, 260, 200 - top));
            if (!triggerBounds.IsEmpty) allowed = new CombinedGeometry(GeometryCombineMode.Exclude, allowed, new RectangleGeometry(triggerBounds));
            var helpClip = new CombinedGeometry(GeometryCombineMode.Intersect, fanHelpSector, allowed);
            var settingsClip = new CombinedGeometry(GeometryCombineMode.Intersect, fanSettingsSector, allowed);
            helpClip.Freeze(); settingsClip.Freeze(); HelpButton.Clip = helpClip; SettingsButton.Clip = settingsClip;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenFanMenu
        /// desc         : 닫힌 메뉴만 한 번 펼치고 열린 영역 재진입에서는 닫기 예약만 취소한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : fanOpenCount로 실제 열림과 hover 재진입을 구분해 검증 가능.
        /// update date  : 2026-10-18 CodexCode : TASK-021 독립 부채꼴 버튼의 고정 모양 안에서만 펼침 효과 재생.
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
        /// update date  : 2026-10-18 CodexCode : TASK-021 창 크기 변경 이벤트도 종료 시 해제.
        /// update date  : 2026-10-18 CodexCode : 실제 원점 경계 추적용 레이아웃 이벤트 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeFanMenu()
        {
            if (fanMenuDisposed) return;
            CloseFanMenu(); fanMenuDisposed = true;
            if (fanCloseTimer != null) { fanCloseTimer.Tick -= FanCloseTick; fanCloseTimer = null; }
            OptionsToggle.MouseEnter -= FanRegionEntered; OptionsToggle.MouseLeave -= FanRegionLeft;
            FanPanel.MouseEnter -= FanRegionEntered; FanPanel.MouseLeave -= FanRegionLeft;
            FanPanel.LayoutUpdated -= FanMenuLayoutUpdated;
            OptionsToggle.GotKeyboardFocus -= FanFocusEntered; FanPanel.GotKeyboardFocus -= FanFocusEntered;
            OptionsToggle.LostKeyboardFocus -= FanFocusLeft; FanPanel.LostKeyboardFocus -= FanFocusLeft;
            Loaded -= FanMenuLoaded; Unloaded -= FanMenuUnloaded; SizeChanged -= FanMenuSizeChanged;
            if (fanOwnerWindow != null) fanOwnerWindow.Deactivated -= FanOwnerDeactivated;
            fanOwnerWindow = null;
        }
    }
}
