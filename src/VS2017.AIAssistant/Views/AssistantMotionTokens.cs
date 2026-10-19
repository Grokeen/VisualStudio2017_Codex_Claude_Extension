using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantMotionTokens
    /// desc         : 주요 패널 진입 모션의 공통 시간·감속 곡선과 Windows 애니메이션 선호를 제공한다.
    /// author       : CodexOrig4
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig4 : fan·안내·패널 진입에만 적용하고 반복 입력 반응은 변경하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class AssistantMotionTokens
    {
        private static readonly CubicEase EntryEase = CreateEntryEase();
        public static readonly TimeSpan PrimaryEntry = TimeSpan.FromMilliseconds(220);
        public static readonly TimeSpan MicroInteraction = TimeSpan.FromMilliseconds(150);

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ClientAreaAnimationsEnabled
        /// desc         : Windows가 클라이언트 영역 애니메이션을 허용하는 현재 설정을 읽는다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 움직임을 끈 경우 호출부에서 목표 상태를 즉시 표시한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static bool ClientAreaAnimationsEnabled { get { return SystemParameters.ClientAreaAnimation; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateEntryEase
        /// desc         : 공유해도 변경되지 않도록 고정된 EaseOut 곡선을 준비한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 주요 진입의 감속 곡선을 통일한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static CubicEase CreateEntryEase()
        {
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            easing.Freeze();
            return easing;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreatePrimaryEntry
        /// desc         : OS 선호와 무관하게 모션 켬/끔의 시간·곡선 사양을 검사할 표본을 만든다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 제품 호출부는 끔 상태에서 애니메이션 대신 목표값을 직접 적용.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static DoubleAnimation CreatePrimaryEntry(double from, double to, int delay, bool clientAreaAnimationsEnabled)
        {
            if (delay < 0) throw new ArgumentOutOfRangeException("delay");
            var duration = clientAreaAnimationsEnabled ? PrimaryEntry : TimeSpan.Zero;
            return new DoubleAnimation(from, to, duration)
            {
                BeginTime = TimeSpan.FromMilliseconds(delay),
                EasingFunction = clientAreaAnimationsEnabled ? EntryEase : null
            };
        }

        internal static DoubleAnimation CreateMicroInteraction(double from, double to, bool clientAreaAnimationsEnabled)
        {
            return new DoubleAnimation(from, to, clientAreaAnimationsEnabled ? MicroInteraction : TimeSpan.Zero)
            {
                EasingFunction = clientAreaAnimationsEnabled ? EntryEase : null
            };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApplyPrimaryEntry
        /// desc         : OS 모션 설정에 따라 eased animation 또는 즉시 목표 상태를 적용한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 두 정책 경로를 STA fixture에서 별도로 검증하도록 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static bool ApplyPrimaryEntry(UIElement target, DependencyProperty property, double from, double to, int delay, bool clientAreaAnimationsEnabled)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (property == null) throw new ArgumentNullException("property");
            if (!clientAreaAnimationsEnabled)
            {
                target.BeginAnimation(property, null);
                target.SetCurrentValue(property, to);
                return false;
            }
            target.BeginAnimation(property, CreatePrimaryEntry(from, to, delay, true));
            return true;
        }

        internal static bool ApplyPrimaryEntry(Animatable target, DependencyProperty property, double from, double to, int delay, bool clientAreaAnimationsEnabled)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (property == null) throw new ArgumentNullException("property");
            if (!clientAreaAnimationsEnabled)
            {
                target.BeginAnimation(property, null);
                target.SetCurrentValue(property, to);
                return false;
            }
            target.BeginAnimation(property, CreatePrimaryEntry(from, to, delay, true));
            return true;
        }

        internal static bool ApplyMicroInteraction(UIElement target, DependencyProperty property, double from, double to, bool clientAreaAnimationsEnabled)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (property == null) throw new ArgumentNullException("property");
            if (!clientAreaAnimationsEnabled)
            {
                target.BeginAnimation(property, null);
                target.SetCurrentValue(property, to);
                return false;
            }
            target.BeginAnimation(property, CreateMicroInteraction(from, to, true));
            return true;
        }

        internal static bool ApplyMicroInteraction(Animatable target, DependencyProperty property, double from, double to, bool clientAreaAnimationsEnabled)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (property == null) throw new ArgumentNullException("property");
            if (!clientAreaAnimationsEnabled)
            {
                target.BeginAnimation(property, null);
                target.SetCurrentValue(property, to);
                return false;
            }
            target.BeginAnimation(property, CreateMicroInteraction(from, to, true));
            return true;
        }

        internal static bool ApplyPrimaryEntry(Animatable target, DependencyProperty property, double from, double to, int delay)
        {
            return ApplyPrimaryEntry(target, property, from, to, delay, ClientAreaAnimationsEnabled);
        }

        internal static bool ApplyPrimaryEntry(UIElement target, DependencyProperty property, double from, double to, int delay)
        {
            return ApplyPrimaryEntry(target, property, from, to, delay, ClientAreaAnimationsEnabled);
        }
    }

    public partial class AssistantControl
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MotionPreferenceLoaded
        /// desc         : 표시 중인 컨트롤만 Windows reduced-motion 변경을 수신하도록 연결한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 컨트롤 Unloaded/Dispose 수명에서 구독을 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void MotionPreferenceLoaded(object sender, RoutedEventArgs args)
        {
            SystemParameters.StaticPropertyChanged -= MotionPreferenceChanged;
            SystemParameters.StaticPropertyChanged += MotionPreferenceChanged;
            RefreshActiveMotion();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MotionPreferenceUnloaded
        /// desc         : WPF 컨트롤 수명 밖에서 설정 변경 이벤트가 인스턴스를 붙잡지 않도록 해제한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : Unloaded와 명시적 Dispose 양쪽에서 중복 호출 가능.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void MotionPreferenceUnloaded(object sender, RoutedEventArgs args)
        {
            SystemParameters.StaticPropertyChanged -= MotionPreferenceChanged;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MotionPreferenceChanged
        /// desc         : Windows 애니메이션 설정이 바뀌면 활성 Thinking 표시를 정적 또는 모션 상태로 갱신한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 다른 시스템 설정 변경에는 WPF 트리를 갱신하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void MotionPreferenceChanged(object sender, PropertyChangedEventArgs args)
        {
            if (!String.IsNullOrEmpty(args.PropertyName) && args.PropertyName != "ClientAreaAnimation") return;
            if (Dispatcher.CheckAccess()) { RefreshActiveMotion(); return; }
            if (!disposed)
            {
                var dispatcherContext = new System.Windows.Threading.DispatcherSynchronizationContext(Dispatcher);
                dispatcherContext.Post(delegate { RefreshActiveMotion(); }, null);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshActiveMotion
        /// desc         : 활성 상태 문자열은 유지하면서 현재 OS 선호에 맞춰 반복 표시를 갱신한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 종료·unload 경쟁에서는 UI 갱신을 생략.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshActiveMotion()
        {
            if (!disposed && thinkingAnimationActive) UpdateThinkingAnimation(true);
        }
    }
}
