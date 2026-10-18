using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using VS2017.AIAssistant.Chat;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 대화 목록의 폭·숨김과 실제 요청 결과·읽음 상태를 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-014 드래그 경계와 대화별 상태 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private double historyWidth = 220;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeConversationTracking
        /// desc         : 이전 목록 배치를 복원하고 실제 본문 확인 동작에 읽음 처리를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 자동 스크롤을 사용자 읽음 동작으로 오인하지 않도록 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InitializeConversationTracking(AssistantSettings settings)
        {
            historyWidth = settings.HistoryWidth;
            HistoryToggle.IsChecked = settings.ShowHistory;
            ConversationScroll.PreviewMouseDown += ConversationViewed;
            ConversationScroll.AddHandler(Mouse.PreviewMouseWheelEvent, new MouseWheelEventHandler(ConversationViewed), true);
            ConversationScroll.GotKeyboardFocus += ConversationViewed;
            ConversationList.PreviewMouseUp += ConversationViewed;
            IsKeyboardFocusWithinChanged += (sender, args) => { if (IsKeyboardFocusWithin && IsConversationInView()) MarkCurrentConversationRead(); };
            ApplyHistoryLayout();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApplyHistoryLayout
        /// desc         : 넓은 창에는 전체 높이 경계를, 좁은 창에는 입력란을 가리지 않는 목록을 배치한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 창 크기 변경으로 사용자가 저장한 목록 폭을 덮어쓰지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ApplyHistoryLayout()
        {
            if (!experienceReady || HistorySplitter == null) return;
            bool visible = HistoryToggle.IsChecked == true;
            double width = LayoutRoot.ActualWidth > 0 ? LayoutRoot.ActualWidth : Math.Max(200, ActualWidth - 20);
            bool narrow = width < 460;
            HistoryColumn.MaxWidth = Math.Max(140, Math.Min(420, width - 200));
            HistoryColumn.Width = new GridLength(visible && !narrow ? Math.Min(historyWidth, HistoryColumn.MaxWidth) : 0);
            HistorySplitterColumn.Width = new GridLength(visible && !narrow ? 7 : 0);
            HistoryPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            HistoryPanel.Width = narrow ? Math.Min(historyWidth, Math.Max(140, width - 65)) : double.NaN;
            HistoryPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            System.Windows.Controls.Grid.SetColumnSpan(HistoryPanel, visible && narrow ? 3 : 1);
            System.Windows.Controls.Grid.SetRowSpan(HistoryPanel, narrow ? 1 : 3);
            if (narrow) HistoryPanel.HorizontalAlignment = HorizontalAlignment.Left;
            HistorySplitter.Visibility = visible && !narrow ? Visibility.Visible : Visibility.Collapsed;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HistoryResizeCompleted
        /// desc         : 실제 경계 드래그가 끝나면 폭을 보관하거나 왼쪽 끝에서 목록을 접는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 취소된 드래그는 이전 설정을 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void HistoryResizeCompleted(object sender, DragCompletedEventArgs e)
        {
            if (!e.Canceled) FinishHistoryResize();
            else ApplyHistoryLayout();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HistoryResizeKeyUp
        /// desc         : 키보드 방향키로 조절한 폭도 드래그와 같은 기준으로 저장한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 마우스 없이 목록 폭과 숨김을 조절.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void HistoryResizeKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.Right) FinishHistoryResize();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FinishHistoryResize
        /// desc         : 96픽셀 미만이면 숨기고 다시 열 때 마지막 유효 폭을 복원한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 임시 축소 폭 대신 사용 가능한 140~420픽셀 범위 보관.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FinishHistoryResize()
        {
            if (!experienceReady || disposed) return;
            double width = HistoryColumn.ActualWidth;
            if (width < 96) HistoryToggle.IsChecked = false;
            else historyWidth = Math.Max(140, Math.Min(420, width));
            ApplyHistoryLayout(); SaveHistoryLayout();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SaveHistoryLayout
        /// desc         : 디스크의 다른 설정을 읽어 목록 폭·숨김 두 값만 병합한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 미저장 CLI·규칙 입력을 배치 저장에 포함하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SaveHistoryLayout()
        {
            if (!experienceReady || disposed) return;
            try { var saved = settingsStore.Load(); saved.HistoryWidth = historyWidth; saved.ShowHistory = HistoryToggle.IsChecked == true; settingsStore.Save(saved); }
            catch (Exception error) { StatusLabel.Text = "대화 목록 배치 저장 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsConversationInView
        /// desc         : 표시된 AI 창에 실제 입력 포커스가 있는 경우만 현재 응답 확인으로 간주한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 다른 소스 탭·앱에서 작업 중 도착한 응답의 읽지 않음 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool IsConversationInView()
        {
            var window = Window.GetWindow(this);
            return IsVisible && IsKeyboardFocusWithin && (window == null || window.IsActive && window.WindowState != WindowState.Minimized);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConversationViewed
        /// desc         : 목록 선택 또는 본문 클릭·스크롤·키보드 확인으로 읽지 않음 표시를 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 읽음 저장 실패 시 점을 복원하여 기록과 표시 일치.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ConversationViewed(object sender, RoutedEventArgs e) { MarkCurrentConversationRead(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkCurrentConversationRead
        /// desc         : 현재 대화의 읽음만 저장하고 다른 대화의 알림·종료 상태는 유지한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 복원만으로 읽지 않은 기록이 사라지지 않도록 명시적 확인 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void MarkCurrentConversationRead()
        {
            if (!experienceReady || disposed) return;
            var record = activeRecords[selectedProvider];
            if (!record.HasUnread) return;
            record.HasUnread = false;
            if (!PersistCurrentConversation()) record.HasUnread = true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SetConversationState
        /// desc         : 요청 이벤트의 실제 결과와 현재 확인 여부를 해당 대화에 반영한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 진행 상태는 결과로 덮이고 읽지 않은 응답은 별도 보관.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SetConversationState(ConversationRecord record, ConversationRunState state)
        {
            record.State = state;
            record.HasUnread = state != ConversationRunState.Running && !IsConversationInView();
        }
    }
}
