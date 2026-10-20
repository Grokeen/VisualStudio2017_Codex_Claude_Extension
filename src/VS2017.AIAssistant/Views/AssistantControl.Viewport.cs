using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 도킹 호스트의 무한 측정을 제한하고 대화 스크롤만 입력란 위에서 움직이게 한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 고정 입력란·사용자 읽기 위치·생각 중 표시 위치 관리.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private bool conversationFollowingEnd = true;
        private bool conversationFollowScheduled;
        private bool conversationViewportDisposed;
        private bool conversationMeasuredInfinitely;
        private double lastConversationViewportHeight = 600;
        // TASK049 CodexCode : 화면에 보이지 않는 저장 기록은 ChatSession에 남겨 두고 상단 접근 시에만 펼친다.
        private Func<Task<bool>> conversationTryLoadOlder;
        private bool conversationPrepending;
        private double conversationPrependOffset, conversationPrependExtent;
        private DispatcherOperation conversationPrependOperation;
        private Task conversationPrependTask;
        private int conversationPrependGeneration;
        private FrameworkElement conversationPrependAnchor;
        private double conversationPrependAnchorY;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MeasureOverride
        /// desc         : 외부 ScrollViewer가 무한 높이를 주더라도 실제 viewport 안에서 자식 Grid를 측정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 전체 내용 높이가 IDE 바깥 스크롤을 만드는 경계 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override Size MeasureOverride(Size constraint)
        {
            conversationMeasuredInfinitely = double.IsInfinity(constraint.Height) || double.IsNaN(constraint.Height);
            Size host = conversationMeasuredInfinitely || double.IsInfinity(constraint.Width) ? FindConversationHostViewport() : constraint;
            double height = ConstrainViewportLength(constraint.Height, host.Height, lastConversationViewportHeight);
            double width = ConstrainViewportLength(constraint.Width, host.Width, 400);
            if (height > 0) lastConversationViewportHeight = height;
            ApplyConversationInputBudget(height);
            Size desired = base.MeasureOverride(new Size(width, height));
            return new Size(Math.Min(width, desired.Width), Math.Min(height, desired.Height));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OnRenderSizeChanged
        /// desc         : 초기 측정 뒤 호스트 viewport가 확정되거나 도킹 크기가 바뀌면 유한 경계를 다시 측정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 첫 레이아웃의 0 viewport와 이후 리사이즈를 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (conversationMeasuredInfinitely && sizeInfo.HeightChanged && !conversationViewportDisposed) InvalidateMeasure();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConstrainViewportLength
        /// desc         : 유한 제약은 그대로 쓰고 무한·NaN만 호스트 또는 유한 초기값으로 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : UI 없는 독립 수학 검사로 경계값을 검증할 수 있게 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static double ConstrainViewportLength(double available, double viewport, double fallback)
        {
            if (!double.IsNaN(available) && !double.IsInfinity(available)) return Math.Max(0, available);
            if (!double.IsNaN(viewport) && !double.IsInfinity(viewport) && viewport > 0) return viewport;
            return !double.IsNaN(fallback) && !double.IsInfinity(fallback) && fallback > 0 ? fallback : 300;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FindConversationHostViewport
        /// desc         : 대화 내부가 아닌 이 컨트롤 바깥의 가장 가까운 스크롤 호스트 크기만 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 운영체제 창 크기 추정 대신 실제 ScrollContentPresenter 경계 우선.
        /// </summary>
        /// ---------------------------------------------------------------------
        private Size FindConversationHostViewport()
        {
            double horizontalInsets = Margin.Left + Margin.Right, verticalInsets = Margin.Top + Margin.Bottom;
            for (DependencyObject node = VisualTreeHelper.GetParent(this); node != null; node = VisualTreeHelper.GetParent(node))
            {
                var presenter = node as ScrollContentPresenter;
                if (presenter != null && presenter.ActualHeight > 0)
                    return new Size(Math.Max(0, presenter.ActualWidth - horizontalInsets), Math.Max(0, presenter.ActualHeight - verticalInsets));
                var scroll = node as ScrollViewer;
                if (scroll != null)
                {
                    double height = scroll.ViewportHeight > 0 ? scroll.ViewportHeight : scroll.ActualHeight - scroll.BorderThickness.Top - scroll.BorderThickness.Bottom - scroll.Padding.Top - scroll.Padding.Bottom;
                    double width = scroll.ViewportWidth > 0 ? scroll.ViewportWidth : scroll.ActualWidth - scroll.BorderThickness.Left - scroll.BorderThickness.Right - scroll.Padding.Left - scroll.Padding.Right;
                    return new Size(Math.Max(0, width - horizontalInsets), Math.Max(0, height - verticalInsets));
                }
                var element = node as FrameworkElement;
                if (element != null) { horizontalInsets += element.Margin.Left + element.Margin.Right; verticalInsets += element.Margin.Top + element.Margin.Bottom; }
                var border = node as Border;
                if (border != null)
                {
                    horizontalInsets += border.Padding.Left + border.Padding.Right + border.BorderThickness.Left + border.BorderThickness.Right;
                    verticalInsets += border.Padding.Top + border.Padding.Bottom + border.BorderThickness.Top + border.BorderThickness.Bottom;
                }
            }
            return new Size(0, 0);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApplyConversationInputBudget
        /// desc         : 작은 창에서는 입력과 첨부 높이만 줄여 전송 도구가 대화 아래 별도 행에 남게 한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 변경 비교 확장이 입력란 높이를 밀어내지 않게 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ApplyConversationInputBudget(double height)
        {
            if (Instruction == null || AttachmentList == null) return;
            double minimum = Math.Min(65, Math.Max(28, height * .14));
            Instruction.MinHeight = minimum;
            Instruction.MaxHeight = Math.Max(minimum, Math.Min(150, height * .22));
            AttachmentList.MaxHeight = Math.Min(150, Math.Max(36, height * .15));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConversationViewportLoaded
        /// desc         : 실제 도킹 호스트가 연결된 뒤 viewport를 재측정하고 초기 최신 위치를 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : XAML 이벤트로 초기화하여 기존 생성자·다른 소유 파일 수정 불필요.
        ///                2026-10-19 CodexCode : TASK024 목록 overlay도 실제 호스트 연결 뒤 복원.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ConversationViewportLoaded(object sender, RoutedEventArgs args)
        { if (!conversationViewportDisposed) { InvalidateMeasure(); FollowConversation(false); UpdateHistoryOverlay(); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FollowConversation
        /// desc         : 전송·대화 전환만 강제 최신 위치로 가고 새 응답은 사용자가 바닥을 읽는 동안만 따라간다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 직접 ScrollToEnd 호출의 통합 진입점.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FollowConversation(bool force)
        {
            if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("대화 스크롤은 해당 WPF UI 스레드에서만 변경할 수 있습니다.");
            if (disposed || conversationViewportDisposed || ConversationScroll == null) return;
            if (force) conversationFollowingEnd = true;
            if (conversationFollowingEnd)
            {
                ConversationScroll.ScrollToEnd();
                if (!conversationFollowScheduled) { conversationFollowScheduled = true; LayoutUpdated += FinishConversationFollow; }
            }
            UpdateThinkingPlacement();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FinishConversationFollow
        /// desc         : 새 메시지의 측정이 끝난 다음 최신 위치를 한 번 더 적용하고 예약 이벤트를 제거한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Dispatcher Task나 타이머 없이 레이아웃 후 추종 완료.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FinishConversationFollow(object sender, EventArgs args)
        {
            CancelConversationFollow();
            if (!disposed && !conversationViewportDisposed && conversationFollowingEnd && ConversationScroll != null) ConversationScroll.ScrollToEnd();
            UpdateThinkingPlacement();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConversationViewportChanged
        /// desc         : 사용자 이동과 본문 높이 변경을 구분하고 바닥에 있을 때만 새 진행 메시지를 추종한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 중첩 diff 스크롤 이벤트가 대화의 읽기 위치를 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ConversationViewportChanged(object sender, ScrollChangedEventArgs args)
        {
            if (disposed || conversationViewportDisposed || ConversationScroll == null || !ReferenceEquals(args.OriginalSource, ConversationScroll)) return;
            if (conversationPrepending) return;
            if (args.ExtentHeightChange == 0 && args.ViewportHeightChange == 0 && args.VerticalChange != 0)
            {
                conversationFollowingEnd = IsConversationAtEnd(ConversationScroll.VerticalOffset, ConversationScroll.ScrollableHeight);
                if (args.VerticalChange < 0 && ConversationScroll.VerticalOffset <= 2) TryPrependConversationPage();
            }
            else if (conversationFollowingEnd && (args.ExtentHeightChange != 0 || args.ViewportHeightChange != 0)) FollowConversation(false);
            UpdateThinkingPlacement();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryPrependConversationPage
        /// desc         : 상단에 이전 메시지를 추가하고 확장된 높이만큼 읽던 위치를 복원한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK049 위쪽 휠·키·스크롤바의 공통 진입점.
        ///                2026-10-20 CodexOrig : TASK068 비동기 조회 한 건만 진행하고 완료 후 현재 세대의 읽기 앵커를 복원.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "Schedules the owned WPF ScrollViewer layout pass at Loaded priority; this is not a switch to the Visual Studio main thread.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Reads only a task whose status is already RanToCompletion; pending file IO is always awaited asynchronously.")]
        private bool TryPrependConversationPage()
        {
            if (conversationPrepending || conversationTryLoadOlder == null || ConversationScroll == null || disposed || conversationViewportDisposed) return false;
            conversationFollowingEnd = false;
            CancelConversationFollow();
            conversationPrepending = true;
            CaptureConversationPrependAnchor();
            int generation = ++conversationPrependGeneration;
            Task<bool> load;
            try { load = conversationTryLoadOlder(); }
            catch { CancelConversationPrepend(); throw; }
            if (load.Status == TaskStatus.RanToCompletion && !load.GetAwaiter().GetResult()) { CancelConversationPrepend(); return false; }
            conversationPrependTask = CompleteConversationPrependAsync(load, generation);
            return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureConversationPrependAnchor
        /// desc         : 조회 중 사용자 스크롤을 존중하도록 실제 삽입 직전의 문단 위치와 offset을 고정한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 지연 조회가 시작 시점의 읽기 위치로 되돌리지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CaptureConversationPrependAnchor()
        {
            VerifyConversationHistoryAccess();
            if (!conversationPrepending || ConversationScroll == null) return;
            conversationPrependOffset = ConversationScroll.VerticalOffset;
            conversationPrependExtent = ConversationScroll.ExtentHeight;
            conversationPrependAnchor = ConversationMessages.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
            conversationPrependAnchorY = conversationPrependAnchor == null ? 0 : conversationPrependAnchor.TranslatePoint(new Point(), ConversationScroll).Y;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CompleteConversationPrependAsync
        /// desc         : 파일 조회를 기다리는 동안 UI를 비워 두고 같은 대화 세대의 완료만 레이아웃에 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 조회 실패·취소·전환 뒤 이전 작업이 새 페이지 상태를 해제하지 못하게 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "Returns to this owned WPF control only to apply the completed history layout; no Visual Studio service is accessed.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "The parameter is started and tracked by this control's TryPrependConversationPage callback, runs bounded file IO on Task.Run, and never joins Visual Studio service work.")]
        private async Task CompleteConversationPrependAsync(Task<bool> load, int generation)
        {
            bool added = false;
            Exception failure = null;
            try { added = await load.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception error) { failure = error; }
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    VerifyConversationHistoryAccess();
                    if (generation != conversationPrependGeneration || disposed || conversationViewportDisposed) return;
                    if (failure != null) StatusLabel.Text = "이전 기록을 읽지 못했습니다. 위로 스크롤하면 다시 시도합니다: " + failure.Message;
                    if (!added) { CancelConversationPrepend(); return; }
                    ArrangeConversationPrepend(generation);
                }).Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ArrangeConversationPrepend
        /// desc         : 실제 삽입이 끝난 페이지의 높이 변경만 측정하고 취소된 세대의 예약을 폐기한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 비동기 결과 이후 Loaded 레이아웃 예약과 앵커 복원.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "Schedules only this owned ScrollViewer's Loaded layout pass.")]
        private void ArrangeConversationPrepend(int generation)
        {
            VerifyConversationHistoryAccess();
            LayoutUpdated += FinishConversationPrepend;
            ConversationScroll.InvalidateMeasure();
            // 첫 LayoutUpdated가 ScrollViewer extent 갱신보다 빠를 수 있으므로 다음 레이아웃 배치에서 재확인한다.
            conversationPrependOperation = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                conversationPrependOperation = null;
                if (!conversationPrepending || ConversationScroll == null || generation != conversationPrependGeneration) return;
                ConversationScroll.UpdateLayout();
                FinishConversationPrepend(this, EventArgs.Empty);
                if (conversationPrepending) CancelConversationPrepend();
            }));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FinishConversationPrepend
        /// desc         : 새 문단의 실제 높이를 측정한 뒤 기존 첫 문단이 보이던 지점을 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK049 프로그램 이동을 사용자 스크롤로 오인하지 않음.
        ///                2026-10-20 CodexOrig : TASK068 조회 중 아래에 추가된 답변 높이를 제외하고 기존 문단의 실제 위치 차이로 복원.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FinishConversationPrepend(object sender, EventArgs args)
        {
            if (ConversationScroll != null && !conversationViewportDisposed && !disposed)
            {
                bool anchored = conversationPrependAnchor != null && conversationPrependAnchor.IsDescendantOf(ConversationScroll);
                double growth = anchored ? conversationPrependAnchor.TranslatePoint(new Point(), ConversationScroll).Y - conversationPrependAnchorY : ConversationScroll.ExtentHeight - conversationPrependExtent;
                if (growth <= 0) return;
                ConversationScroll.ScrollToVerticalOffset((anchored ? ConversationScroll.VerticalOffset : conversationPrependOffset) + growth);
            }
            CancelConversationPrepend();
            UpdateThinkingPlacement();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CancelConversationPrepend
        /// desc         : 대화 전환·도킹 해제·종료 시 이전 페이지의 레이아웃 예약을 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK049 다른 대화로 앵커가 넘어가지 않게 보호.
        ///                2026-10-20 CodexOrig : 예약된 DispatcherOperation도 해제해 종료·전환 뒤 실행을 막는다.
        ///                2026-10-20 CodexOrig : TASK068 진행 중 파일 조회와 레이아웃 세대도 대화 입력·CLI 중지와 독립 취소.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CancelConversationPrepend()
        {
            VerifyConversationHistoryAccess();
            conversationPrependGeneration++;
            CancelOlderConversationLoad();
            LayoutUpdated -= FinishConversationPrepend;
            if (conversationPrependOperation != null && conversationPrependOperation.Status == DispatcherOperationStatus.Pending) conversationPrependOperation.Abort();
            conversationPrependOperation = null;
            conversationPrepending = false;
            conversationPrependAnchor = null;
            if (conversationPrependTask != null && conversationPrependTask.IsCompleted) conversationPrependTask = null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : VerifyConversationHistoryAccess
        /// desc         : IDE 전역 주 스레드 계약을 다른 화면으로 전파하지 않고 이 WPF 컨트롤의 실제 dispatcher 소유권을 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 비동기 진입은 dispatcher로 전환하고 동기 수명 메서드는 소유 스레드에서만 허용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void VerifyConversationHistoryAccess()
        {
            if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("대화 기록 화면은 해당 WPF dispatcher에서만 변경할 수 있습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsConversationAtEnd
        /// desc         : 픽셀 반올림 차이를 허용해 대화가 실제 마지막 위치인지 판단한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 빈 대화와 작은 스크롤 높이도 바닥으로 취급.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsConversationAtEnd(double offset, double scrollableHeight)
        { return !double.IsNaN(offset) && !double.IsNaN(scrollableHeight) && !double.IsInfinity(offset) && !double.IsInfinity(scrollableHeight) && scrollableHeight - offset <= 2; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConversationViewportMouseWheel
        /// desc         : 위로 읽기 시작하면 추종을 멈추고 내부 스크롤이 없는 본문 휠은 대화만 이동한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 대화 맨 위의 휠이 VS 바깥 전체 스크롤로 전파되지 않게 처리.
        ///                2026-10-19 CodexCode : TASK024 내부 비교가 휠을 먼저 소비하고 끝에서도 배경 추종을 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ConversationViewportMouseWheel(object sender, MouseWheelEventArgs args)
        {
            if (ConversationScroll == null || disposed || conversationViewportDisposed) return;
            if (HoverScrollPolicy.HandleNestedWheel(ConversationScroll, args)) return;
            if (args.Delta > 0) { conversationFollowingEnd = false; CancelConversationFollow(); }
            double lines = SystemParameters.WheelScrollLines;
            double distance = lines < 0 ? ConversationScroll.ViewportHeight : Math.Max(1, lines) * Math.Max(14, FontSize * 1.3);
            double destination = ConversationScroll.VerticalOffset - args.Delta / 120.0 * distance;
            ConversationScroll.ScrollToVerticalOffset(destination);
            if (args.Delta > 0 && destination <= 2) TryPrependConversationPage();
            args.Handled = true;
            UpdateThinkingPlacement();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConversationViewportKeyDown
        /// desc         : 대화를 위로 읽는 탐색 키가 대기 중인 자동 추종을 취소하게 한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 읽기 전용 본문 선택·복사 단축키는 처리하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ConversationViewportKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Up && args.Key != Key.PageUp && args.Key != Key.Home) return;
            conversationFollowingEnd = false; CancelConversationFollow();
            if ((args.Key == Key.Home || ConversationScroll != null && ConversationScroll.VerticalOffset <= 2) && TryPrependConversationPage()) args.Handled = true;
            UpdateThinkingPlacement();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CancelConversationFollow
        /// desc         : 단발 레이아웃 예약을 제거해 사용자 이동과 종료 후 강제 스크롤을 막는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 반복 호출과 Unloaded·Dispose 경합을 안전하게 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CancelConversationFollow()
        { if (conversationFollowScheduled) { LayoutUpdated -= FinishConversationFollow; conversationFollowScheduled = false; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConversationViewportUnloaded
        /// desc         : 도킹 이동으로 분리된 컨트롤의 단발 레이아웃 예약만 제거한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 다시 도킹될 때 진행 상태를 이어받도록 영구 Dispose와 구별.
        ///                2026-10-19 CodexCode : TASK024 목록 overlay와 파일 비교 popup의 임시 수명 정리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ConversationViewportUnloaded(object sender, RoutedEventArgs args)
        { CancelConversationFollow(); CancelConversationPrepend(); DisposeHistoryOverlay(); ChangeDiffPopup.CloseWithin(this); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeConversationViewport
        /// desc         : 뷰 종료 시 스크롤 예약과 두 생각 중 표시의 공유 애니메이션을 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 기존 DisposeExperience에서 호출하는 단일 수명 정리 API.
        ///                2026-10-19 CodexCode : TASK024 overlay 입력·레이아웃 구독과 파일 비교 popup도 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeConversationViewport()
        { conversationViewportDisposed = true; CancelConversationFollow(); CancelConversationPrepend(); conversationTryLoadOlder = null; UpdateThinkingAnimation(false); DisposeHistoryOverlay(); ChangeDiffPopup.CloseWithin(this); }
    }
}
