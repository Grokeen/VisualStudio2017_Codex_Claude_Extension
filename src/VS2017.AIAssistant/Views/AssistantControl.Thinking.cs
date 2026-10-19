using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 기존 생각 중 텍스트 위로 흰빛이 왼쪽에서 오른쪽으로 흐르는 표시를 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 추가9의 그라데이션 진행 표시와 중복·종료 수명 관리.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private LinearGradientBrush thinkingSweepBrush;
        private TranslateTransform thinkingSweepTransform;
        private Brush thinkingOriginalForeground;
        private bool thinkingForegroundHasTemporaryLocalValue;
        private Brush thinkingAboveOriginalForeground;
        private bool thinkingAboveHasTemporaryLocalValue;
        private bool thinkingAnimationActive;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateThinkingAnimation
        /// desc         : UI에서 생각 중 sweep를 한 번만 시작하고 중지 시 시계와 기존 색을 복원한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 타이머·외부 입력 없이 상대 좌표 애니메이션을 정리.
        /// update date  : 2026-10-18 CodexCode : SDK17에서 WPF 스레드 검사와 VS 전용 UI 요구를 분리.
        /// update date  : 2026-10-18 CodexCode : 상속 색에는 임시 로컬 브러시를 연결하고 종료 시 원래 값 출처 복원.
        /// update date  : 2026-10-18 CodexCode : TASK-022 바닥 footer와 위쪽 읽기 표시가 단일 sweep 시계를 공유.
        ///                2026-10-19 CodexOrig4 : Light에서도 진행 표시의 전체 그라데이션이 읽히도록 보조/본문 팔레트 대비 사용.
        ///                2026-10-19 CodexOrig4 : Windows 모션 끔이면 텍스트는 유지하고 반복 shimmer만 멈춤.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateThinkingAnimation(bool active)
        {
            if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("생각 중 표시는 해당 WPF UI 스레드에서만 갱신할 수 있습니다.");
            if (ThinkingIndicator == null) return;
            active = active && !disposed;
            thinkingAnimationActive = active;
            UpdateThinkingPlacement();
            if (active && AssistantMotionTokens.ClientAreaAnimationsEnabled)
            {
                if (thinkingSweepBrush != null) return;
                thinkingOriginalForeground = ThinkingIndicator.Foreground;
                thinkingForegroundHasTemporaryLocalValue = ThinkingIndicator.ReadLocalValue(TextBlock.ForegroundProperty) == DependencyProperty.UnsetValue;
                if (ThinkingAboveIndicator != null)
                {
                    thinkingAboveOriginalForeground = ThinkingAboveIndicator.Foreground;
                    thinkingAboveHasTemporaryLocalValue = ThinkingAboveIndicator.ReadLocalValue(TextBlock.ForegroundProperty) == DependencyProperty.UnsetValue;
                }
                thinkingSweepTransform = new TranslateTransform(-1, 0);
                thinkingSweepBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, .5), EndPoint = new Point(1, .5),
                    MappingMode = BrushMappingMode.RelativeToBoundingBox,
                    RelativeTransform = thinkingSweepTransform, SpreadMethod = GradientSpreadMethod.Pad
                };
                var muted = TryFindResource("AssistantMutedForegroundBrush") as SolidColorBrush;
                var foreground = TryFindResource("AssistantForegroundBrush") as SolidColorBrush;
                var surface = TryFindResource("AssistantBackgroundBrush") as SolidColorBrush;
                bool light = surface != null && AssistantPalette.IsLight(surface.Color);
                var gray = muted == null ? Color.FromRgb(132, 132, 132) : muted.Color;
                var highlight = light ? (foreground == null ? Colors.Black : foreground.Color) : Colors.White;
                thinkingSweepBrush.GradientStops.Add(new GradientStop(gray, 0));
                thinkingSweepBrush.GradientStops.Add(new GradientStop(gray, .32));
                thinkingSweepBrush.GradientStops.Add(new GradientStop(highlight, .5));
                thinkingSweepBrush.GradientStops.Add(new GradientStop(gray, .68));
                thinkingSweepBrush.GradientStops.Add(new GradientStop(gray, 1));
                thinkingSweepTransform.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(-1, 1, TimeSpan.FromMilliseconds(1600)) { RepeatBehavior = RepeatBehavior.Forever },
                    HandoffBehavior.SnapshotAndReplace);
                // 상속·스타일 값의 SetCurrentValue는 mutable Freezable 변경 시 원래 값으로 돌아갈 수 있다.
                // 그 경우만 임시 로컬 값을 만들고 ClearValue로 복원하며 기존 로컬 바인딩·리소스는 유지한다.
                if (thinkingForegroundHasTemporaryLocalValue) ThinkingIndicator.SetValue(TextBlock.ForegroundProperty, thinkingSweepBrush);
                else ThinkingIndicator.SetCurrentValue(TextBlock.ForegroundProperty, thinkingSweepBrush);
                if (ThinkingAboveIndicator != null)
                {
                    if (thinkingAboveHasTemporaryLocalValue) ThinkingAboveIndicator.SetValue(TextBlock.ForegroundProperty, thinkingSweepBrush);
                    else ThinkingAboveIndicator.SetCurrentValue(TextBlock.ForegroundProperty, thinkingSweepBrush);
                }
                return;
            }
            if (thinkingSweepTransform != null)
            {
                thinkingSweepTransform.BeginAnimation(TranslateTransform.XProperty, null);
                thinkingSweepTransform.X = -1;
            }
            if (thinkingSweepBrush != null)
            {
                if (thinkingForegroundHasTemporaryLocalValue) ThinkingIndicator.ClearValue(TextBlock.ForegroundProperty);
                else ThinkingIndicator.SetCurrentValue(TextBlock.ForegroundProperty, thinkingOriginalForeground);
                if (ThinkingAboveIndicator != null)
                {
                    if (thinkingAboveHasTemporaryLocalValue) ThinkingAboveIndicator.ClearValue(TextBlock.ForegroundProperty);
                    else ThinkingAboveIndicator.SetCurrentValue(TextBlock.ForegroundProperty, thinkingAboveOriginalForeground);
                }
            }
            thinkingSweepBrush = null; thinkingSweepTransform = null; thinkingOriginalForeground = null;
            thinkingAboveOriginalForeground = null;
            thinkingForegroundHasTemporaryLocalValue = thinkingAboveHasTemporaryLocalValue = false;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateThinkingPlacement
        /// desc         : 바닥에서는 footer만 보이고 이전 내용을 읽을 때만 상단에 진행 상태를 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 숨긴 footer의 공간을 유지하여 스크롤 높이·위치 피드백 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateThinkingPlacement()
        {
            bool active = thinkingAnimationActive && !disposed;
            bool above = active && ConversationScroll != null && !IsConversationAtEnd(ConversationScroll.VerticalOffset, ConversationScroll.ScrollableHeight);
            if (ThinkingIndicator != null) ThinkingIndicator.Visibility = !active ? Visibility.Collapsed : above ? Visibility.Hidden : Visibility.Visible;
            if (ThinkingAboveIndicator != null) ThinkingAboveIndicator.Visibility = above ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
