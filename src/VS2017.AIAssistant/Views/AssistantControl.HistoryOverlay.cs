using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 좁은 창의 대화 목록을 본문과 구별되는 overlay로 관리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK024 backdrop·Escape·초점·접근성·정리 경계.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private bool historyOverlayActive, historyOverlayHooks, historyOverlayFocusPending;
        private FrameworkElement historyOverlaySurface, historyOverlayBackdrop;
        private FrameworkElement historyOverlayComposer;
        private object historyComposerEnabledLocal;
        private bool historyComposerEnabled;
        private object historySurfaceEnabledLocal;
        private bool historySurfaceEnabled;
        private object historyPanelNavigationLocal;
        private KeyboardNavigationMode historyPanelNavigation;

        /// <summary>
        /// name         : UpdateHistoryOverlay
        /// desc         : 기존 목록 너비 계산 뒤 좁은 overlay에서만 본문을 차단하고 어두운 backdrop을 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 넓은 창의 side-by-side 목록에는 dim이나 초점 강제를 적용하지 않음.
        /// </summary>
        private void UpdateHistoryOverlay()
        {
            if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("대화 목록 overlay는 WPF UI 스레드에서만 변경하세요.");
            if (disposed || conversationViewportDisposed) return;
            historyOverlaySurface = FindName("ConversationSurface") as FrameworkElement;
            historyOverlayBackdrop = FindName("HistoryBackdrop") as FrameworkElement;
            historyOverlayComposer = FindName("ComposerSurface") as FrameworkElement;
            if (historyOverlaySurface == null || historyOverlayBackdrop == null || HistoryPanel == null) return;
            if (!historyOverlayHooks) { PreviewKeyDown += HistoryOverlayKeyDown; historyOverlayHooks = true; }
            double width = LayoutRoot.ActualWidth > 0 ? LayoutRoot.ActualWidth : Math.Max(200, ActualWidth - 20);
            bool active = width < 460 && HistoryToggle.IsChecked == true && HistoryPanel.Visibility == Visibility.Visible;
            if (active && !historyOverlayActive)
            {
                ChangeDiffPopup.CloseWithin(this); ProgressNoticePresenter.CloseWithin(this);
                historySurfaceEnabledLocal = historyOverlaySurface.ReadLocalValue(IsEnabledProperty);
                historySurfaceEnabled = historyOverlaySurface.IsEnabled;
                historyPanelNavigationLocal = HistoryPanel.ReadLocalValue(KeyboardNavigation.TabNavigationProperty);
                historyPanelNavigation = KeyboardNavigation.GetTabNavigation(HistoryPanel);
                historyOverlaySurface.SetCurrentValue(IsEnabledProperty, false);
                if (historyOverlayComposer != null)
                {
                    historyComposerEnabledLocal = historyOverlayComposer.ReadLocalValue(IsEnabledProperty); historyComposerEnabled = historyOverlayComposer.IsEnabled;
                    historyOverlayComposer.SetCurrentValue(IsEnabledProperty, false);
                }
                HistoryPanel.SetCurrentValue(KeyboardNavigation.TabNavigationProperty, KeyboardNavigationMode.Cycle);
                historyOverlayActive = true;
                historyOverlayFocusPending = true; LayoutUpdated += FocusHistoryOverlay;
            }
            else if (!active && historyOverlayActive) ReleaseHistoryOverlay(false);
            historyOverlayBackdrop.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            System.Windows.Automation.AutomationProperties.SetHelpText(HistoryPanel, active ? "대화 목록 펼침 · Escape 또는 어두운 배경 클릭으로 닫기" : "대화 기록 목록");
        }

        /// <summary>
        /// name         : FocusHistoryOverlay
        /// desc         : 표시 레이아웃 후에만 목록으로 초점을 한 번 옮긴다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : popup이나 Dispatcher 예약 없이 좁은 창의 숨은 본문 초점 방지.
        /// </summary>
        private void FocusHistoryOverlay(object sender, EventArgs args)
        {
            if (!historyOverlayFocusPending) return;
            if (historyOverlayActive && !HistoryPanel.IsVisible) return;
            LayoutUpdated -= FocusHistoryOverlay; historyOverlayFocusPending = false;
            if (historyOverlayActive && HistoryPanel.IsVisible && !HistoryPanel.IsKeyboardFocusWithin) ConversationList.Focus();
        }

        /// <summary>
        /// name         : HistoryOverlayBackdropPressed
        /// desc         : 본문 위 backdrop 클릭을 소비하고 목록만 닫아 실제 대화 클릭으로 전달하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 기존 목록 숨김 저장 경로를 재사용.
        /// </summary>
        private void HistoryOverlayBackdropPressed(object sender, MouseButtonEventArgs args)
        { if (!historyOverlayActive) return; args.Handled = true; DismissHistoryOverlay(); }

        /// <summary>
        /// name         : HistoryOverlayKeyDown
        /// desc         : 좁은 목록 overlay에서 Escape를 누르면 목록을 접고 토글로 초점을 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 넓은 목록과 일반 입력의 키는 가로채지 않음.
        /// </summary>
        private void HistoryOverlayKeyDown(object sender, KeyEventArgs args)
        { if (historyOverlayActive && args.Key == Key.Escape) { args.Handled = true; DismissHistoryOverlay(); } }

        /// <summary>
        /// name         : DismissHistoryOverlay
        /// desc         : 목록 토글 상태·배치 저장을 기존 이벤트와 동일하게 처리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 초점 반환은 사용자 명시 닫기에서만 수행.
        /// </summary>
        private void DismissHistoryOverlay()
        {
            HistoryToggle.IsChecked = false;
            HistoryToggled(this, new RoutedEventArgs());
            ReleaseHistoryOverlay(true);
        }

        /// <summary>
        /// name         : ReleaseHistoryOverlay
        /// desc         : backdrop과 임시 본문 비활성화·Tab 순환을 원래 값으로 돌리고 예약을 제거한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : inherited 값은 ClearValue, binding은 SetCurrentValue로 보존.
        /// </summary>
        private void ReleaseHistoryOverlay(bool restoreFocus)
        {
            LayoutUpdated -= FocusHistoryOverlay; historyOverlayFocusPending = false;
            if (historyOverlayActive)
            {
                if (historySurfaceEnabledLocal == DependencyProperty.UnsetValue) historyOverlaySurface.ClearValue(IsEnabledProperty);
                else historyOverlaySurface.SetCurrentValue(IsEnabledProperty, historySurfaceEnabled);
                if (historyOverlayComposer != null)
                {
                    if (historyComposerEnabledLocal == DependencyProperty.UnsetValue) historyOverlayComposer.ClearValue(IsEnabledProperty);
                    else historyOverlayComposer.SetCurrentValue(IsEnabledProperty, historyComposerEnabled);
                }
                if (historyPanelNavigationLocal == DependencyProperty.UnsetValue) HistoryPanel.ClearValue(KeyboardNavigation.TabNavigationProperty);
                else HistoryPanel.SetCurrentValue(KeyboardNavigation.TabNavigationProperty, historyPanelNavigation);
            }
            historyOverlayActive = false;
            if (historyOverlayBackdrop != null) historyOverlayBackdrop.Visibility = Visibility.Collapsed;
            if (restoreFocus && HistoryToggle.IsVisible) HistoryToggle.Focus();
        }

        /// <summary>
        /// name         : DisposeHistoryOverlay
        /// desc         : 도킹 해제·뷰 종료에서 등록한 입력과 레이아웃 이벤트를 제거한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 목록의 사용자 저장 상태 자체는 변경하지 않음.
        /// </summary>
        private void DisposeHistoryOverlay()
        { ReleaseHistoryOverlay(false); if (historyOverlayHooks) PreviewKeyDown -= HistoryOverlayKeyDown; historyOverlayHooks = false; }
    }
}
