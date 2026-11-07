using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 열린 대화의 안전한 활동 상태를 한 화면에서 보고 소유 대화로 이동한다.
    /// author       : CodexCode
    /// create date  : 2026-11-03
    /// update date  : 2026-11-03 CodexOrig : 활동함 표시 메소드를 별도 partial로 분리.
    /// update date  : 2026-11-03 CodexOrig4 : 비모달 singleton, 재사용 view factory, 행·키보드 상태 보존, 개별 읽음 및 활동함 버튼 높이 통일.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private const double ActivityFeedActionHeight = 84.0;
        private AssistantDialog activityFeedDialog;
        private DispatcherTimer activityFeedTimer;
        private EventHandler activityFeedTickHandler;
        private DispatcherOperation activityFeedNavigationOperation;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivityFeedVisualRow
        /// desc         : 복합 대화 키와 재사용 가능한 활동함 행 컨트롤을 묶는다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class ActivityFeedVisualRow
        {
            internal string Key;
            internal ConversationActivityItem Item;
            internal Border Surface;
            internal Button OpenButton;
            internal Button ReadButton;
            internal TextBlock Heading;
            internal TextBlock Detail;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivityFeedViewContext
        /// desc         : 제품 활동함 root와 화면 전용 refresh·dispose 동작을 묶는다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig4 : 실제 GotKeyboardFocus 세대와 행 제거 focus 소유자를 추적.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class ActivityFeedViewContext
        {
            internal FrameworkElement Root { get; set; }
            internal Action Refresh { get; set; }
            internal Action Dispose { get; set; }
            internal AssistantDialog Dialog;
            internal ComboBox Filter;
            internal TextBlock Counts;
            internal StackPanel Items;
            internal ScrollViewer Scroll;
            internal TextBlock EmptyMessage;
            internal Dictionary<string, ActivityFeedVisualRow> Rows;
            internal string LastSignature;
            internal string SelectedRowKey;
            internal DispatcherOperation PendingFocusRestore;
            internal KeyboardFocusChangedEventHandler KeyboardFocusChanged;
            internal IInputElement FocusRemovalElement;
            internal string FocusRemovalKey;
            internal bool FocusRemovalAllowsNull;
            internal long FocusGeneration;
            internal bool IsApplyingFocusRestore;
            internal bool IsDisposed;
            internal bool ClosingForNavigation;
            internal SelectionChangedEventHandler FilterSelectionChanged;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivityFeedClicked
        /// desc         : 대화 본문을 읽지 않는 화면을 열고 필터·행 수를 짧은 주기로 병합 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig : 활동함 표시 메소드를 별도 partial로 분리.
        /// update date  : 2026-11-03 CodexOrig4 : 비모달 singleton으로 열고 factory 수명과 분리, 기존 창 재활성화 및 상태 갱신 중 행·선택·focus를 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ActivityFeedClicked(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess();
            if (disposed || !experienceReady) return;
            if (activityFeedDialog != null)
            {
                if (activityFeedDialog.WindowState == WindowState.Minimized) activityFeedDialog.WindowState = WindowState.Normal;
                activityFeedDialog.Activate();
                return;
            }

            var dialog = new AssistantDialog(this, "◉ 대화 활동함", "Claude·Codex의 열린 대화 상태입니다. 원문은 대화에서 확인하세요.");
            dialog.MinWidth = 242;
            dialog.Width = Math.Max(242, Math.Min(682, Math.Max(200, LayoutRoot.ActualWidth) + 42));
            dialog.Height = Math.Min(650, dialog.MaxHeight);
            activityFeedDialog = dialog;
            ActivityFeedViewContext view = null;
            try
            {
                view = BuildActivityFeedView(dialog);
                dialog.Body.Content = view.Root;
                EventHandler tick = (s, e) => view.Refresh();
                activityFeedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                activityFeedTickHandler = tick;
                activityFeedTimer.Tick += tick;
                dialog.Closed += (s, e) =>
                {
                    if (activityFeedTimer != null)
                    {
                        if (activityFeedTickHandler != null) activityFeedTimer.Tick -= activityFeedTickHandler;
                        activityFeedTimer.Stop();
                        activityFeedTimer = null;
                    }
                    activityFeedTickHandler = null;
                    view.Dispose();
                    dialog.Body.Content = null;
                    if (activityFeedDialog == dialog) activityFeedDialog = null;
                    if (!view.ClosingForNavigation && !disposed) ActivityFeedButton.Focus();
                };
                view.Refresh();
                activityFeedTimer.Start();
                dialog.Show();
                dialog.Activate();
            }
            catch
            {
                if (activityFeedTimer != null)
                {
                    if (activityFeedTickHandler != null) activityFeedTimer.Tick -= activityFeedTickHandler;
                    activityFeedTimer.Stop();
                    activityFeedTimer = null;
                }
                activityFeedTickHandler = null;
                if (view != null) view.Dispose();
                if (activityFeedDialog == dialog) activityFeedDialog = null;
                if (dialog.IsVisible) dialog.Close();
                throw;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BuildActivityFeedView
        /// desc         : 제품 필터·집계·목록 컨트롤을 생성해 Root/Refresh/Dispose context로 반환한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig4 : 행 제거·읽음 동작에서 대화상자 기본 focus 정리만 소유 전이로 추적.
        /// update date  : 2026-11-03 CodexOrig4 : dialog의 실제 키보드 focus 변경 이벤트를 view 수명에 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        private ActivityFeedViewContext BuildActivityFeedView(AssistantDialog dialog)
        {
            Dispatcher.VerifyAccess();
            if (dialog == null) throw new ArgumentNullException(nameof(dialog));

            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var filter = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 8),
                MinHeight = 34,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                SelectedValuePath = "Tag",
                MaxDropDownHeight = 360
            };
            foreach (var option in new[] {
                new { Name = "전체", Value = ConversationActivityFilter.All },
                new { Name = "확인 필요", Value = ConversationActivityFilter.Attention },
                new { Name = "실행 중·대기", Value = ConversationActivityFilter.Running },
                new { Name = "완료", Value = ConversationActivityFilter.Completed },
                new { Name = "실패·중지", Value = ConversationActivityFilter.FailedOrStopped }
            })
                filter.Items.Add(new ComboBoxItem { Content = option.Name, Tag = option.Value });
            filter.SelectedIndex = 0;
            AutomationProperties.SetName(filter, "활동 상태 필터");
            AutomationProperties.SetHelpText(filter, "상태별 대화 목록을 좁혀 80개 표시 제한 안의 항목을 찾습니다.");
            layout.Children.Add(filter);

            var counts = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
            counts.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush");
            AutomationProperties.SetName(counts, "활동함의 전체 수, 상태별 수와 표시 제한 안내");
            Grid.SetRow(counts, 1);
            layout.Children.Add(counts);

            var items = new StackPanel();
            var scroll = new ScrollViewer
            {
                Content = items,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false,
                Focusable = false
            };
            AutomationProperties.SetName(scroll, "활동 대화 목록");
            Grid.SetRow(scroll, 2);
            layout.Children.Add(scroll);

            var view = new ActivityFeedViewContext
            {
                Root = layout,
                Dialog = dialog,
                Filter = filter,
                Counts = counts,
                Items = items,
                Scroll = scroll,
                EmptyMessage = new TextBlock
                {
                    Text = "표시할 대화가 없습니다.",
                    Margin = new Thickness(8),
                    TextWrapping = TextWrapping.Wrap
                },
                Rows = new Dictionary<string, ActivityFeedVisualRow>(StringComparer.Ordinal)
            };
            view.EmptyMessage.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush");
            AutomationProperties.SetName(view.EmptyMessage, "표시할 대화가 없습니다.");

            view.KeyboardFocusChanged = (sender, args) =>
            {
                if (view.IsDisposed || view.IsApplyingFocusRestore) return;
                if (view.FocusRemovalKey != null &&
                    ReferenceEquals(args.OldFocus, view.FocusRemovalElement))
                {
                    if (IsActivityFeedExpectedFocusLossTarget(view, args.NewFocus) ||
                        (view.FocusRemovalAllowsNull && args.NewFocus == null)) return;
                    view.FocusRemovalElement = null;
                    view.FocusRemovalKey = null;
                    view.FocusRemovalAllowsNull = false;
                }
                view.FocusGeneration++;
            };
            dialog.AddHandler(Keyboard.GotKeyboardFocusEvent, view.KeyboardFocusChanged, true);

            view.Refresh = () => RefreshActivityFeedView(view);
            view.Dispose = () =>
            {
                if (view.IsDisposed) return;
                view.IsDisposed = true;
                CancelActivityFeedFocusRestore(view);
                if (view.KeyboardFocusChanged != null)
                    view.Dialog.RemoveHandler(Keyboard.GotKeyboardFocusEvent, view.KeyboardFocusChanged);
                view.KeyboardFocusChanged = null;
                view.FocusRemovalElement = null;
                view.FocusRemovalKey = null;
                view.FocusRemovalAllowsNull = false;
                if (view.FilterSelectionChanged != null)
                    view.Filter.SelectionChanged -= view.FilterSelectionChanged;
                view.Rows.Clear();
                view.Items.Children.Clear();
            };
            view.FilterSelectionChanged = (s, e) => view.Refresh();
            filter.SelectionChanged += view.FilterSelectionChanged;
            return view;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshActivityFeedView
        /// desc         : 실제 활동함 컨트롤을 snapshot에 맞게 갱신하고 선택·focus·scroll anchor를 보존한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig4 : snapshot 숫자 집계와 표시 제한을 UI Automation 이름·도움말에 동기화.
        /// update date  : 2026-11-03 CodexOrig4 : 사용자가 이동한 focus와 행 제거로 잃은 focus를 분리.
        /// update date  : 2026-11-03 CodexOrig4 : 미읽음 행에서 읽음 버튼이 숨겨질 때 소유 focus 손실을 추적.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshActivityFeedView(ActivityFeedViewContext view)
        {
            Dispatcher.VerifyAccess();
            if (view == null || view.IsDisposed || disposed) return;
            var focusElementBeforeRefresh = Keyboard.FocusedElement;
            var selected = (view.Filter.SelectedItem as ComboBoxItem)?.Tag;
            var chosenFilter = selected is ConversationActivityFilter
                ? (ConversationActivityFilter)selected
                : ConversationActivityFilter.All;
            var snapshot = BuildActivityFeedSnapshot(chosenFilter);
            var signature = ActivityFeedSignature(chosenFilter, snapshot);

            string focusedKey = null;
            bool focusedReadAction = false;
            foreach (var pair in view.Rows)
            {
                if (pair.Value.OpenButton.IsKeyboardFocusWithin)
                {
                    focusedKey = pair.Key;
                    view.SelectedRowKey = pair.Key;
                    break;
                }
                if (pair.Value.ReadButton.IsKeyboardFocusWithin)
                {
                    focusedKey = pair.Key;
                    focusedReadAction = true;
                    view.SelectedRowKey = pair.Key;
                    break;
                }
            }
            var anchor = CaptureActivityFeedAnchor(view.Rows, view.Scroll);
            var wanted = new HashSet<string>(snapshot.VisibleItems.Select(ActivityFeedKey), StringComparer.Ordinal);
            bool focusedRowWillBeRemoved = focusedKey != null && !wanted.Contains(focusedKey);
            var focusedSnapshotItem = focusedKey == null
                ? null
                : snapshot.VisibleItems.FirstOrDefault(item => String.Equals(ActivityFeedKey(item), focusedKey, StringComparison.Ordinal));
            bool focusedReadActionWillBeHidden = focusedReadAction && !focusedRowWillBeRemoved &&
                focusedSnapshotItem != null && !focusedSnapshotItem.HasUnread;

            var countsText = ActivityFeedCounts(snapshot);
            view.Counts.Text = countsText;
            var accessibleCounts = "활동함의 전체 수, 상태별 수와 표시 제한 안내 · " + countsText;
            AutomationProperties.SetName(view.Counts, accessibleCounts);
            AutomationProperties.SetHelpText(view.Counts, countsText);
            if (signature == view.LastSignature)
            {
                RefreshActivityFeedBadge();
                return;
            }
            CancelActivityFeedFocusRestore(view);
            view.LastSignature = signature;

            bool focusedElementWillDisappear = focusedRowWillBeRemoved || focusedReadActionWillBeHidden;
            if (focusedElementWillDisappear && focusedKey != null)
            {
                // Own only the focus transition caused by removing this action or its containing row.
                view.FocusRemovalElement = focusElementBeforeRefresh;
                view.FocusRemovalKey = focusedKey;
                view.FocusRemovalAllowsNull = focusedRowWillBeRemoved;
            }
            else
            {
                view.FocusRemovalElement = null;
                view.FocusRemovalKey = null;
                view.FocusRemovalAllowsNull = false;
            }

            foreach (var staleKey in view.Rows.Keys.Where(key => !wanted.Contains(key)).ToArray())
            {
                view.Items.Children.Remove(view.Rows[staleKey].Surface);
                view.Rows.Remove(staleKey);
                if (view.SelectedRowKey == staleKey) view.SelectedRowKey = null;
            }

            Action<string> selectRow = key =>
            {
                view.SelectedRowKey = key;
                UpdateActivityFeedSelection(view.Rows, view.SelectedRowKey);
            };
            Action closeForNavigation = () => { view.ClosingForNavigation = true; };
            for (int index = 0; index < snapshot.VisibleItems.Length; index++)
            {
                var item = snapshot.VisibleItems[index];
                string key = ActivityFeedKey(item);
                ActivityFeedVisualRow row;
                if (!view.Rows.TryGetValue(key, out row))
                {
                    row = CreateActivityRow(item, view.Dialog, view.Refresh, selectRow, closeForNavigation);
                    view.Rows.Add(key, row);
                }
                UpdateActivityRow(row, item);
                int currentIndex = view.Items.Children.IndexOf(row.Surface);
                if (currentIndex != index)
                {
                    if (currentIndex >= 0) view.Items.Children.RemoveAt(currentIndex);
                    view.Items.Children.Insert(Math.Min(index, view.Items.Children.Count), row.Surface);
                }
            }

            if (view.SelectedRowKey != null && !view.Rows.ContainsKey(view.SelectedRowKey)) view.SelectedRowKey = null;
            UpdateActivityFeedSelection(view.Rows, view.SelectedRowKey);

            if (snapshot.VisibleItems.Length == 0)
            {
                if (!view.Items.Children.Contains(view.EmptyMessage))
                {
                    view.Items.Children.Clear();
                    view.Items.Children.Add(view.EmptyMessage);
                }
            }
            else if (view.Items.Children.Contains(view.EmptyMessage))
            {
                view.Items.Children.Remove(view.EmptyMessage);
            }

            RefreshActivityFeedBadge();
            if (focusedKey != null || anchor != null)
                QueueActivityFeedFocusRestore(view, anchor, focusedKey, focusedReadAction,
                    focusedRowWillBeRemoved, focusedReadActionWillBeHidden);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : QueueActivityFeedFocusRestore
        /// desc         : 실제 키보드 focus 세대를 확인하고 행 제거에 따른 focus 손실만 연결된 대상으로 복원한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig4 : DispatcherOperation 수명·실제 focus owner·사용자 focus 변경을 추적하고 행 제거 fallback 추가.
        /// update date  : 2026-11-03 CodexOrig4 : WPF 기본 focus 정리 뒤 숨겨진 읽음 버튼을 같은 행의 열기 버튼으로 복원.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "Schedules only the owned WPF activity list layout and focus restoration at ContextIdle priority; it does not switch to the Visual Studio main thread.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "The returned DispatcherOperation is retained by the activity view and aborted on replacement or disposal; the callback is intentionally deferred until WPF finishes layout.")]
        private void QueueActivityFeedFocusRestore(
            ActivityFeedViewContext view, ActivityFeedAnchor anchor, string focusedKey, bool focusedReadAction,
            bool focusedRowWillBeRemoved, bool focusedReadActionWillBeHidden)
        {
            Dispatcher.VerifyAccess();
            if (view == null || view.IsDisposed || disposed) return;
            long focusGeneration = view.FocusGeneration;
            var focusElementAtSchedule = Keyboard.FocusedElement;
            DispatcherOperation operation = null;
            operation = Dispatcher.BeginInvoke(new Action(() =>
            {
                Dispatcher.VerifyAccess();
                if (ReferenceEquals(view.PendingFocusRestore, operation)) view.PendingFocusRestore = null;
                if (view.IsDisposed || disposed || !view.Dialog.IsVisible || !view.Dialog.IsActive) return;
                if (view.FocusRemovalKey != null && String.Equals(view.FocusRemovalKey, focusedKey, StringComparison.Ordinal))
                {
                    view.FocusRemovalKey = null;
                    view.FocusRemovalElement = null;
                    view.FocusRemovalAllowsNull = false;
                }
                if (view.FocusGeneration != focusGeneration) return;
                var currentFocus = Keyboard.FocusedElement;
                bool focusSourceWillDisappear = focusedRowWillBeRemoved || focusedReadActionWillBeHidden;
                bool expectedOwnedFocusLoss = (focusedRowWillBeRemoved && currentFocus == null) ||
                    (focusSourceWillDisappear && IsActivityFeedExpectedFocusLossTarget(view, currentFocus));
                if (!ReferenceEquals(currentFocus, focusElementAtSchedule) && !expectedOwnedFocusLoss) return;
                if (focusedKey != null && view.Rows.ContainsKey(focusedKey) &&
                    !String.Equals(view.SelectedRowKey, focusedKey, StringComparison.Ordinal)) return;
                string currentFocusedRowKey = GetActivityFeedFocusedRowKey(view, currentFocus);
                bool currentFocusIsFilter = ReferenceEquals(currentFocus, view.Filter);
                if (focusedKey != null && (currentFocusIsFilter ||
                    (currentFocusedRowKey != null && !String.Equals(currentFocusedRowKey, focusedKey, StringComparison.Ordinal)))) return;
                RestoreActivityFeedAnchor(anchor, view.Rows, view.Scroll);
                if (focusedKey == null) return;
                ActivityFeedVisualRow focusedRow;
                if (!view.Rows.TryGetValue(focusedKey, out focusedRow))
                {
                    if (focusedRowWillBeRemoved && !currentFocusIsFilter && currentFocusedRowKey == null)
                        RestoreActivityFeedFocusFallback(view);
                    return;
                }
                bool sameRowOpenButtonAlreadyFocused = ReferenceEquals(currentFocus, focusedRow.OpenButton) &&
                    focusedRow.OpenButton.IsVisible && focusedRow.OpenButton.IsKeyboardFocused;
                bool sameRowReadButtonAlreadyFocused = ReferenceEquals(currentFocus, focusedRow.ReadButton) &&
                    focusedRow.ReadButton.IsVisible && focusedRow.ReadButton.IsKeyboardFocused;
                if (sameRowOpenButtonAlreadyFocused || sameRowReadButtonAlreadyFocused) return;
                var target = focusedReadAction && focusedRow.ReadButton.IsVisible
                    ? focusedRow.ReadButton
                    : focusedRow.OpenButton;
                FocusActivityFeedElement(view, target);
            }), DispatcherPriority.ContextIdle);
            view.PendingFocusRestore = operation;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsActivityFeedExpectedFocusLossTarget
        /// desc         : WPF focus 정리에서 활동 대화상자 자체와 Body만 기대된 소유 focus 대상으로 인정한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsActivityFeedExpectedFocusLossTarget(ActivityFeedViewContext view, IInputElement focusedElement)
        {
            if (view == null) return false;
            view.Dialog.Dispatcher.VerifyAccess();
            return ReferenceEquals(focusedElement, view.Dialog) || ReferenceEquals(focusedElement, view.Dialog.Body);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetActivityFeedFocusedRowKey
        /// desc         : 현재 keyboard focus가 있는 활동 행의 복합 키를 반환한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig4 : 실제 focus owner를 복원 콜백에서 확인한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string GetActivityFeedFocusedRowKey(ActivityFeedViewContext view, IInputElement focusedElement)
        {
            if (view == null || focusedElement == null) return null;
            view.Dialog.Dispatcher.VerifyAccess();
            foreach (var pair in view.Rows)
                if (ReferenceEquals(focusedElement, pair.Value.OpenButton) || ReferenceEquals(focusedElement, pair.Value.ReadButton))
                    return pair.Key;
            return null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RestoreActivityFeedFocusFallback
        /// desc         : 제거된 focus 행 뒤에 남은 첫 열기 버튼이나 필터로 focus를 복원한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig4 : 실제 focus owner가 비어 있을 때만 연결된 fallback을 선택한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RestoreActivityFeedFocusFallback(ActivityFeedViewContext view)
        {
            if (view == null || view.IsDisposed || !view.Dialog.IsVisible || !view.Dialog.IsActive) return;
            view.Dialog.Dispatcher.VerifyAccess();
            var row = view.Rows.Values
                .OrderBy(candidate => view.Items.Children.IndexOf(candidate.Surface))
                .FirstOrDefault(candidate => candidate.OpenButton.IsVisible && candidate.OpenButton.IsEnabled && candidate.OpenButton.Focusable);
            if (row != null)
            {
                view.SelectedRowKey = row.Key;
                UpdateActivityFeedSelection(view.Rows, view.SelectedRowKey);
                FocusActivityFeedElement(view, row.OpenButton);
                return;
            }
            view.SelectedRowKey = null;
            UpdateActivityFeedSelection(view.Rows, view.SelectedRowKey);
            if (view.Filter.IsVisible && view.Filter.IsEnabled && view.Filter.Focusable)
                FocusActivityFeedElement(view, view.Filter);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FocusActivityFeedElement
        /// desc         : 활동함의 복원 중임을 표시하며 지정한 실제 WPF 요소에 focus를 둔다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig4 : focus 세대 추적에서 제품의 복원 이동을 구분한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void FocusActivityFeedElement(ActivityFeedViewContext view, IInputElement target)
        {
            if (view == null || target == null || view.IsDisposed) return;
            view.Dialog.Dispatcher.VerifyAccess();
            view.IsApplyingFocusRestore = true;
            try { Keyboard.Focus(target); }
            finally { view.IsApplyingFocusRestore = false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CancelActivityFeedFocusRestore
        /// desc         : 활동함 view가 더 이상 소유하지 않는 예약 레이아웃 복원 작업을 중단한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig4 : 세대 번호를 올려 오래된 예약 focus 복원을 무효화한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CancelActivityFeedFocusRestore(ActivityFeedViewContext view)
        {
            if (view == null) return;
            view.FocusGeneration++;
            var operation = view.PendingFocusRestore;
            view.PendingFocusRestore = null;
            if (operation != null && operation.Status == DispatcherOperationStatus.Pending) operation.Abort();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivityFeedSignature
        /// desc         : 표시 행과 상한 밖의 모든 상태 집계값 변경을 감지해 안내 문구를 갱신한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ActivityFeedSignature(ConversationActivityFilter filter, ConversationActivitySnapshot snapshot)
        {
            return Convert.ToString(filter) + "|" + snapshot.TotalCount + "|" + snapshot.UnreadCount + "|" +
                snapshot.FilteredCount + "|" + snapshot.AttentionCount + "|" + snapshot.RunningCount + "|" +
                snapshot.CompletedCount + "|" + snapshot.FailedOrStoppedCount + "|" + snapshot.HiddenAttentionCount + "|" +
                snapshot.HiddenRunningCount + "|" + string.Join(";", snapshot.VisibleItems.Select(item =>
                    ActivityFeedKey(item) + ":" + item.Kind + ":" + item.HasUnread + ":" + item.SafeName + ":" +
                    item.Status + ":" + item.Summary + ":" + item.Glyph + ":" + item.BrushKey + ":" + item.LastActivityAt.Ticks));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivityFeedCounts
        /// desc         : 전체 집계와 80개 상한 밖의 항목 수·상태를 텍스트로 알린다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ActivityFeedCounts(ConversationActivitySnapshot snapshot)
        {
            int hidden = Math.Max(0, snapshot.FilteredCount - snapshot.VisibleItems.Length);
            string text = "전체 " + snapshot.TotalCount + "개 · 미읽음 " + snapshot.UnreadCount +
                "개 · 확인 필요 " + snapshot.AttentionCount + "개 · 실행/대기 " + snapshot.RunningCount +
                "개 · 완료 " + snapshot.CompletedCount + "개 · 실패/중지 " + snapshot.FailedOrStoppedCount +
                "개 · 현재 필터 " + snapshot.FilteredCount + "개";
            if (hidden > 0)
                text += " · 필터 결과 중 " + hidden + "개 숨김(확인 필요 " + snapshot.HiddenAttentionCount +
                    "개, 실행/대기 " + snapshot.HiddenRunningCount + "개). 다른 상태 필터로 좁힐 수 있습니다.";
            return text;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivityFeedAnchor
        /// desc         : 목록 갱신 전 보이는 행의 키와 viewport 기준 위치를 보관한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class ActivityFeedAnchor
        {
            internal string Key;
            internal double RelativeY;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureActivityFeedAnchor
        /// desc         : 현재 scroll viewport에서 처음 보이는 행과 상대 offset을 캡처한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static ActivityFeedAnchor CaptureActivityFeedAnchor(
            IDictionary<string, ActivityFeedVisualRow> rows, ScrollViewer scroll)
        {
            foreach (var pair in rows)
            {
                if (!pair.Value.Surface.IsLoaded || !scroll.IsLoaded) continue;
                try
                {
                    double y = pair.Value.Surface.TransformToAncestor(scroll).Transform(new Point(0, 0)).Y;
                    if (y + pair.Value.Surface.ActualHeight > 0 && y < scroll.ViewportHeight)
                        return new ActivityFeedAnchor { Key = pair.Key, RelativeY = y };
                }
                catch (InvalidOperationException) { }
            }
            return null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RestoreActivityFeedAnchor
        /// desc         : 행 갱신·재정렬 뒤 같은 대화 행이 보이도록 scroll 위치를 복원한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RestoreActivityFeedAnchor(
            ActivityFeedAnchor anchor, IDictionary<string, ActivityFeedVisualRow> rows, ScrollViewer scroll)
        {
            if (anchor == null || !scroll.IsLoaded) return;
            ActivityFeedVisualRow row;
            if (!rows.TryGetValue(anchor.Key, out row) || !row.Surface.IsLoaded) return;
            try
            {
                double y = row.Surface.TransformToAncestor(scroll).Transform(new Point(0, 0)).Y;
                scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset + y - anchor.RelativeY));
            }
            catch (InvalidOperationException) { }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivityFeedKey
        /// desc         : 제공자와 대화 ID의 복합 키를 만든다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ActivityFeedKey(ConversationActivityItem item)
        { return item.Provider + "\0" + item.Id; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateActivityRow
        /// desc         : 고정 상태문구만 tooltip·접근성 이름에 넣고 키보드/마우스 한 번으로 이동한다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig4 : 열기/읽음 sibling button을 고정 높이로 통일하고 좁은 폭 텍스트를 두 줄로 제한.
        /// update date  : 2026-11-03 CodexOrig4 : 각 click handler의 UI dispatcher 경계를 검증하고 controller의 안전한 이동 실패 안내를 표시.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD010:Invoke single-threaded types on Main thread", Justification = "The open/read handlers are WPF routed clicks on this control's Dispatcher and call Dispatcher.VerifyAccess immediately before invoking controller APIs.")]
        private ActivityFeedVisualRow CreateActivityRow(
            ConversationActivityItem item, AssistantDialog dialog, Action refresh,
            Action<string> selectByKey, Action closeForNavigation)
        {
            var heading = new TextBlock
            {
                Height = 34,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = FontWeights.SemiBold
            };
            var detail = new TextBlock
            {
                Height = 32,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 3, 0, 0)
            };
            var content = new StackPanel();
            content.Children.Add(heading);
            content.Children.Add(detail);
            var open = new Button
            {
                Style = (Style)FindResource("SurfaceButton"),
                Content = content,
                Margin = new Thickness(0),
                Padding = new Thickness(8, 6, 8, 6),
                Height = ActivityFeedActionHeight,
                MinHeight = ActivityFeedActionHeight,
                MaxHeight = ActivityFeedActionHeight,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var read = new Button
            {
                Content = "✓ 읽음 처리",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0),
                Height = ActivityFeedActionHeight,
                MinHeight = ActivityFeedActionHeight,
                MaxHeight = ActivityFeedActionHeight,
                Padding = new Thickness(8, 5, 8, 5),
                Style = (Style)FindResource("LinkButton")
            };
            var actionGrid = new Grid();
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actionGrid.Children.Add(open);
            Grid.SetColumn(read, 1);
            actionGrid.Children.Add(read);
            var surface = new Border
            {
                Child = actionGrid,
                Margin = new Thickness(0, 0, 0, 6),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(2)
            };
            surface.SetResourceReference(Border.BackgroundProperty, "AssistantSurfaceBrush");
            surface.SetResourceReference(Border.BorderBrushProperty, "AssistantBorderBrush");
            var row = new ActivityFeedVisualRow
            {
                Key = ActivityFeedKey(item),
                Item = item,
                Surface = surface,
                OpenButton = open,
                ReadButton = read,
                Heading = heading,
                Detail = detail
            };
            KeyboardFocusChangedEventHandler selectWhenFocused = (s, e) =>
            {
                selectByKey(row.Key);
            };
            open.GotKeyboardFocus += selectWhenFocused;
            read.GotKeyboardFocus += selectWhenFocused;
            open.Click += (s, e) =>
            {
                Dispatcher.VerifyAccess();
                selectByKey(row.Key);
                if (!TryOpenActivityConversation(row.Item))
                {
                    dialog.Message.Text = GetActivityNavigationFailureMessage();
                    return;
                }
                var target = activityReadTarget;
                activityReadTarget = null;
                closeForNavigation();
                dialog.Close();
                QueueActivityNavigationFocus(target);
            };
            read.Click += (s, e) =>
            {
                Dispatcher.VerifyAccess();
                selectByKey(row.Key);
                if (!TryMarkActivityConversationRead(row.Item))
                {
                    dialog.Message.Text = "읽음 상태를 저장하지 못했습니다. 미읽음 표시를 유지합니다.";
                    refresh();
                    return;
                }
                dialog.Message.Text = "대화를 읽음 처리했습니다.";
                refresh();
            };
            AutomationProperties.SetName(surface, item.SafeName + " · " + item.Status);
            UpdateActivityRow(row, item);
            return row;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : QueueActivityNavigationFocus
        /// desc         : 이동한 대화가 여전히 선택된 경우에만 WPF 입력 우선순위에서 focus·읽음 확인을 이어간다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig4 : DispatcherOperation 추적과 target 변경/Dispose 방어 추가.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "Defers only the owned WPF conversation focus/read confirmation until after the activity dialog closes; it does not switch to the Visual Studio main thread.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "The returned DispatcherOperation is retained and aborted if replaced or the control closes; the callback is intentionally deferred to WPF input priority.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD010:Invoke single-threaded types on Main thread", Justification = "This is an owned WPF Dispatcher callback and calls VerifyAccess before accessing the current conversation or read-state APIs.")]
        private void QueueActivityNavigationFocus(ConversationRecord target)
        {
            Dispatcher.VerifyAccess();
            CancelActivityNavigationFocus();
            DispatcherOperation operation = null;
            operation = Dispatcher.BeginInvoke(new Action(() =>
            {
                Dispatcher.VerifyAccess();
                if (ReferenceEquals(activityFeedNavigationOperation, operation)) activityFeedNavigationOperation = null;
                if (disposed || target == null || selectedProvider != target.Provider ||
                    !activeRecords.ContainsKey(target.Provider) || !ReferenceEquals(activeRecords[target.Provider], target)) return;
                ConversationScroll.Focus();
                if (IsConversationInView()) MarkCurrentConversationRead();
            }), DispatcherPriority.Input);
            activityFeedNavigationOperation = operation;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CancelActivityNavigationFocus
        /// desc         : 더 이상 유효하지 않은 대화 focus 예약을 취소한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CancelActivityNavigationFocus()
        {
            var operation = activityFeedNavigationOperation;
            activityFeedNavigationOperation = null;
            if (operation != null && operation.Status == DispatcherOperationStatus.Pending) operation.Abort();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateActivityRow
        /// desc         : 식별 키가 유지되는 행만 갱신하여 상태 변화 중 기존 버튼과 포커스를 보존한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void UpdateActivityRow(ActivityFeedVisualRow row, ConversationActivityItem item)
        {
            row.Item = item;
            row.Heading.Text = item.Glyph + "  " + item.SafeName + " · " + item.Status + (item.HasUnread ? " · 미읽음" : "");
            row.Heading.SetResourceReference(TextBlock.ForegroundProperty, item.BrushKey);
            string when = item.LastActivityAt == default(DateTimeOffset)
                ? "시각 미기록"
                : item.LastActivityAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm");
            row.Detail.Text = item.Summary + "  ·  " + when;
            string accessible = item.SafeName + " · " + item.Status +
                (item.HasUnread ? " · 미읽음" : "") + " · " + item.Summary + " · 대화 열기";
            AutomationProperties.SetName(row.OpenButton, accessible);
            AutomationProperties.SetHelpText(row.OpenButton, "선택한 대화로 이동합니다. 미읽음 상태는 이동 성공 후 기존 읽음 확인 절차에서 저장합니다.");
            row.OpenButton.ToolTip = accessible;
            row.ReadButton.Visibility = item.HasUnread ? Visibility.Visible : Visibility.Collapsed;
            row.ReadButton.IsEnabled = item.HasUnread;
            AutomationProperties.SetName(row.ReadButton, item.SafeName + " · 읽음 처리");
            AutomationProperties.SetHelpText(row.ReadButton, "이 대화만 읽음 처리합니다. 저장에 실패하면 미읽음 표시를 유지합니다.");
            AutomationProperties.SetName(row.Surface, item.SafeName + " · " + item.Status);
            ToolTipService.SetIsEnabled(row.Surface, false);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateActivityFeedSelection
        /// desc         : 복합 키로 선택한 행을 기존 의미 리소스의 테두리로 표시한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void UpdateActivityFeedSelection(
            IDictionary<string, ActivityFeedVisualRow> rows, string selectedKey)
        {
            foreach (var pair in rows)
            {
                bool selected = String.Equals(pair.Key, selectedKey, StringComparison.Ordinal);
                pair.Value.Surface.BorderThickness = new Thickness(selected ? 2 : 1);
                pair.Value.Surface.SetResourceReference(Border.BorderBrushProperty,
                    selected ? "AssistantPrimaryBrush" : "AssistantBorderBrush");
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseActivityFeedView
        /// desc         : 활동함 창과 타이머만 닫고 대화 데이터는 변경하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig4 : 닫기 시 tick 구독도 해제하고 controller의 저장 실패 상태는 초기화하지 않는다.
        /// update date  : 2026-11-03 CodexOrig4 : 예약된 대화 focus callback도 중단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CloseActivityFeedView()
        {
            Dispatcher.VerifyAccess();
            CancelActivityNavigationFocus();
            if (activityFeedTimer != null)
            {
                if (activityFeedTickHandler != null) activityFeedTimer.Tick -= activityFeedTickHandler;
                activityFeedTimer.Stop();
                activityFeedTimer = null;
            }
            activityFeedTickHandler = null;
            if (activityFeedDialog != null)
            {
                var dialog = activityFeedDialog;
                activityFeedDialog = null;
                dialog.Close();
            }
        }
    }
}
