using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProgressStep
    /// desc         : 공개 작업 식별자의 시작·완료를 같은 행에서 갱신하며 실제 제공된 종류·안전 요약·종료 코드만 보관한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 중복 안내 대신 요청별 세부 작업 상태 모델 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ProgressStep : INotifyPropertyChanged
    {
        public string Kind { get; private set; }
        public string Detail { get; private set; }
        public string State { get; private set; }
        public int? ExitCode { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }
        public bool HasWorkId { get; private set; }
        public int Number { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public string Category { get { return Kind == "file" ? "파일 변경" : Kind == "tool" ? (Detail == "웹 검색" ? "검색" : "조회·도구") : Detail.Contains("test") ? "테스트" : Detail.Contains("build") || Detail == "MSBuild" || Detail == "C# 컴파일" ? "빌드·컴파일" : Detail.StartsWith("Git", StringComparison.Ordinal) || Detail.StartsWith("SVN", StringComparison.Ordinal) || Detail.StartsWith("TFVC", StringComparison.Ordinal) ? "형상관리" : Detail.Contains("restore") ? "패키지 복원" : "명령 실행"; } }
        public string Glyph { get { return Kind == "file" ? "✎" : Kind == "command" ? "⌘" : "⌕"; } }
        public string StateLabel { get { return State == "started" ? "진행 중" : State == "completed" ? "완료" : State == "failed" ? "실패" : State == "cancelled" ? "중지됨" : State == "unreported" ? "종료 상태 미제공" : "상태 미제공"; } }
        public string Title { get { return Category + (HasWorkId ? " #" + Number : " · 종류별 상태"); } }
        public string StatusText { get { return StateLabel + (ExitCode.HasValue ? " · 종료 코드 " + ExitCode.Value.ToString(CultureInfo.InvariantCulture) : "") + " · 수신 " + UpdatedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture); } }
        public string Accent { get { return State == "failed" ? "#F28B82" : State == "completed" ? "#81C995" : State == "started" ? "#A9C7FF" : "#B8B8B8"; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProgressStep
        /// desc         : 새로 보고된 작업의 실제 공개 종류와 단일행 안전 요약을 고정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 작업 번호는 수신 행 구분용이며 추정 실행 횟수가 아님.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal ProgressStep(string kind, string detail, bool hasId, int number)
        { Kind = kind; Detail = detail; HasWorkId = hasId; Number = number; State = "unknown"; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Update
        /// desc         : 반복 상태와 늦게 온 시작을 버리고 같은 실제 작업의 종료 상태를 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Claude tool 완료에도 기존 command·file 종류와 안전 요약 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal bool Update(string state, string detail, int? exitCode, DateTimeOffset received)
        {
            if (HasWorkId && (State == "failed" || State == "completed") && (state == "started" || state == "unknown" || (State == "failed" && state == "completed"))) return false;
            // ID가 없는 종류별 집계는 과거 작업의 종료 코드를 새 상태로 승계하지 않는다.
            string safeDetail = HasWorkId ? (Detail.Length == 0 ? detail : Detail) : detail;
            int? code = HasWorkId ? exitCode ?? ExitCode : exitCode;
            if (state == "completed" && code.HasValue && code.Value != 0) state = "failed";
            if (State == state && Detail == safeDetail && ExitCode == code && UpdatedAt != default(DateTimeOffset)) return false;
            State = state; Detail = safeDetail; ExitCode = code; UpdatedAt = received;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("")); return true;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProgressNotice
    /// desc         : 한 요청의 공개 작업을 최대 80행과 2000자 Markdown 요약으로 유지하는 안정 알림 모델이다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 실제 작업 상태와 저장된 과거 요약을 분리하고 가짜 완료를 만들지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ProgressNotice : INotifyPropertyChanged
    {
        public const int MaximumSteps = 80, MaximumSummaryCharacters = 2000;
        public const string MarkdownMarker = "<!-- ai-progress-summary:v1 -->";
        private readonly ObservableCollection<ProgressStep> steps = new ObservableCollection<ProgressStep>();
        private readonly Dictionary<string, ProgressStep> byId = new Dictionary<string, ProgressStep>(StringComparer.Ordinal);
        private string savedSummary;
        public ReadOnlyObservableCollection<ProgressStep> Steps { get; private set; }
        public string RequestId { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsArchived { get { return savedSummary != null; } }
        public bool IsTruncated { get; private set; }
        public string Outcome { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public string Heading { get { return IsArchived ? "저장된 작업 요약" : IsActive ? "작업 진행" : "작업 기록"; } }
        public string OutcomeLabel { get { return IsArchived ? "과거 기록" : IsActive ? "진행 중" : Outcome == "completed" ? "요청 완료" : Outcome == "failed" ? "요청 실패" : Outcome == "cancelled" ? "요청 중지" : "요청 종료"; } }
        public string CurrentTask { get { if (IsArchived) return "현재 실행 상태와 구분된 저장 기록"; var step = steps.LastOrDefault(s => s.State == "started") ?? steps.OrderByDescending(s => s.UpdatedAt).FirstOrDefault(); return step == null ? "공개 작업 상태 수신 대기" : step.Category + (step.Detail.Length == 0 ? "" : " · " + step.Detail) + " · " + step.StateLabel; } }
        public string MarkdownText { get { return savedSummary ?? BuildMarkdown(); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProgressNotice
        /// desc         : 실제 요청의 공개 진행 수신을 시작하며 작업이 보고되기 전에는 실행 결과를 만들지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 모델 객체와 작업 컬렉션 참조를 요청 수명 동안 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ProgressNotice()
        { Steps = new ReadOnlyObservableCollection<ProgressStep>(steps); RequestId = Guid.NewGuid().ToString("N"); IsActive = true; Outcome = "running"; UpdatedAt = DateTimeOffset.Now; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Apply
        /// desc         : Provider가 정제한 Detail과 실제 WorkId·State·ExitCode만 작업 모델에 반영한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 원문 명령·출력·숨은 추론 필드를 받지 않고 기존 작업 갱신은 행 제한 뒤에도 허용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool Apply(string kind, string workId, string state, string sanitizedDetail = null, int? exitCode = null)
        {
            if (!IsActive || IsArchived || (kind != "command" && kind != "file" && kind != "tool")) return false;
            bool identified = !String.IsNullOrWhiteSpace(workId) && workId.Length <= 128 && !workId.Any(Char.IsControl);
            string key = identified ? "id/" + workId : "kind/" + kind;
            ProgressStep step; bool created = false;
            if (!byId.TryGetValue(key, out step))
            {
                if (steps.Count >= MaximumSteps) { if (IsTruncated) return false; IsTruncated = true; Changed(); return true; }
                step = new ProgressStep(kind, SafeSingleLine(sanitizedDetail, 120), identified, steps.Count + 1); byId.Add(key, step); created = true;
            }
            bool updated = step.Update(NormalizeState(state), SafeSingleLine(sanitizedDetail, 120), exitCode, DateTimeOffset.Now);
            if (created) steps.Add(step);
            if (!updated && !created) return false;
            UpdatedAt = step.UpdatedAt; Changed(); return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Complete
        /// desc         : 실제 요청 종료만 기록하고 종료 이벤트 없는 개별 작업을 성공으로 추정하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 취소·실패와 개별 종료 상태 미제공을 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool Complete(string outcome = "completed")
        {
            if (!IsActive || IsArchived) return false;
            Outcome = outcome == "completed" ? "completed" : outcome == "failed" ? "failed" : outcome == "cancelled" || outcome == "stopped" ? "cancelled" : "ended";
            IsActive = false; UpdatedAt = DateTimeOffset.Now;
            foreach (var step in steps.Where(s => s.State == "started" || s.State == "unknown")) step.Update(Outcome == "cancelled" ? "cancelled" : "unreported", "", null, UpdatedAt);
            Changed(); return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RestoreSummary
        /// desc         : 저장된 짧은 요약은 현재 진행 작업으로 재생하지 않고 읽기 전용 과거 기록으로 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 외부 Markdown을 명령·실시간 상태·동작으로 해석하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static ProgressNotice RestoreSummary(string text)
        {
            if (text == null || text.Length > MaximumSummaryCharacters || !text.StartsWith(MarkdownMarker + "\n", StringComparison.Ordinal)) return null;
            return new ProgressNotice { savedSummary = text, IsActive = false, Outcome = "archived" };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NormalizeState
        /// desc         : 공개 프로토콜의 한글·영문 상태만 정규화하고 알 수 없는 상태를 완료로 취급하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 현재 DTO와 새 State 계약의 호환 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string NormalizeState(string state)
        { switch (state) { case "시작": case "진행": case "started": case "running": return "started"; case "완료": case "completed": return "completed"; case "실패": case "failed": return "failed"; default: return "unknown"; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SafeSingleLine
        /// desc         : 정제된 공개 요약에도 제어문자와 길이 제한을 다시 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 원문 스크립트·여러 줄 출력으로 안전 요약을 대체하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string SafeSingleLine(string value, int maximum)
        { if (String.IsNullOrWhiteSpace(value) || value.Any(Char.IsControl)) return ""; value = value.Trim(); if (value.Length <= maximum) return value; int end = maximum - 1; if (Char.IsHighSurrogate(value[end - 1])) end--; return value.Substring(0, end) + "…"; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BuildMarkdown
        /// desc         : 실제 상태·안전 요약·수신 시각만 최대 2000자 읽을 수 있는 Markdown으로 구성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 작업 ID·원문 명령·Provider 출력은 영구 요약에 넣지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private string BuildMarkdown()
        {
            var text = new StringBuilder(MarkdownMarker + "\n**" + Heading + " · " + OutcomeLabel + "**\n");
            bool omitted = IsTruncated;
            foreach (var step in steps)
            {
                string row = "- **" + step.Category + "** · " + step.StateLabel + (step.Detail.Length == 0 ? "" : " · " + step.Detail) + (step.ExitCode.HasValue ? " · 종료 코드 " + step.ExitCode.Value.ToString(CultureInfo.InvariantCulture) : "") + " · 수신 " + step.UpdatedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "\n";
                if (text.Length + row.Length > MaximumSummaryCharacters - 70) { omitted = true; break; }
                text.Append(row);
            }
            if (omitted) text.Append("- 일부 작업은 표시·저장 한도로 생략했습니다.\n");
            return text.ToString();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Changed
        /// desc         : 카드 객체를 교체하지 않고 최신 바인딩과 저장 요약 갱신을 알린다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 열린 Dropdown의 불필요한 재생성 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Changed() { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("")); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProgressNoticeConverter
    /// desc         : 진행 메시지를 안정 모델로 분류하고 일반 사용자·AI·안내 메시지는 기존 템플릿에 남긴다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 메시지 수명에 묶인 weak 연결과 저장된 읽기 전용 요약 지원.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ProgressNoticeConverter : IValueConverter
    {
        private static readonly ConditionalWeakTable<ChatMessage, ProgressNotice> Notices = new ConditionalWeakTable<ChatMessage, ProgressNotice>();
        private static readonly object Gate = new object();

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Attach
        /// desc         : 메시지가 대화 컬렉션에 들어가기 전에 요청의 단일 진행 모델을 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 같은 메시지에 다른 모델을 덮어쓰거나 UI 객체를 공유하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void Attach(ChatMessage message, ProgressNotice notice)
        {
            if (message == null || notice == null || message.Role != "system") throw new ArgumentException("진행 안내 메시지와 모델이 올바르지 않습니다.");
            lock (Gate) { ProgressNotice prior; if (Notices.TryGetValue(message, out prior)) { if (!ReferenceEquals(prior, notice)) throw new InvalidOperationException("이미 연결된 진행 모델이 있습니다."); return; } Notices.Add(message, notice); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Convert
        /// desc         : 기본은 모델 또는 null, IsProgress 인수는 XAML 표시 분기를 위한 bool을 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : property 갱신에도 같은 모델 참조를 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var message = value as ChatMessage; ProgressNotice notice = null;
            if (message != null && message.Role == "system") lock (Gate) { if (!Notices.TryGetValue(message, out notice)) { notice = ProgressNotice.RestoreSummary(message.Text); if (notice != null) Notices.Add(message, notice); } }
            return String.Equals(parameter as string, "IsProgress", StringComparison.Ordinal) ? (object)(notice != null) : notice;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConvertBack
        /// desc         : 읽기 전용 진행 카드의 값을 원문 메시지에 역으로 쓰지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 표시 변환의 부작용 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { return Binding.DoNothing; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProgressNoticePresenter
    /// desc         : 진행 안내 한 카드에 색상·Bold·gradient 아이콘과 hover·키보드 Dropdown을 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 안정 모델 갱신과 Popup·포커스·타이머 수명을 요청 카드에 한정.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ProgressNoticePresenter : ContentControl
    {
        public static readonly DependencyProperty NoticeProperty = DependencyProperty.Register("Notice", typeof(ProgressNotice), typeof(ProgressNoticePresenter), new PropertyMetadata(null, NoticeChanged));
        public ProgressNotice Notice { get { return (ProgressNotice)GetValue(NoticeProperty); } set { SetValue(NoticeProperty, value); } }
        private readonly Button header;
        private readonly TextBlock title, summary, detailsSummary, archived, limit;
        private readonly ItemsControl rows;
        private readonly ScrollViewer detailsScroll;
        private readonly Border popupBody;
        private readonly Popup popup;
        private readonly DispatcherTimer closeTimer;
        private ProgressNotice subscribed;
        private Window owner;
        private bool pinned;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProgressNoticePresenter
        /// desc         : 원문 실행 없이 공개 상태만 보여 주는 한 카드와 고정 Dropdown 컨테이너를 구성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 마우스로 Popup에 진입할 시간을 주고 click·Escape 접근 지원.
        ///                2026-10-18 CodexCode : hover 중 native capture가 anchor 이탈을 만들지 않게 자체 수명 관리만 사용.
        ///                2026-10-19 CodexCode : TASK024 제목 고정·유한 상세 영역과 휠 우선 소비 추가.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ProgressNoticePresenter()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            var face = new Grid(); face.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(27) }); face.ColumnDefinitions.Add(new ColumnDefinition()); face.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            var icon = new TextBlock { Text = "◈", FontSize = 20, Foreground = IconBrush(), VerticalAlignment = VerticalAlignment.Center }; face.Children.Add(icon);
            var lines = new StackPanel(); Grid.SetColumn(lines, 1); face.Children.Add(lines);
            title = new TextBlock { FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            summary = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(174, 194, 225)), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 0) }; lines.Children.Add(title); lines.Children.Add(summary);
            var arrow = new TextBlock { Text = "⌄", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }; Grid.SetColumn(arrow, 2); face.Children.Add(arrow);
            header = new Button { Content = face, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 7, 8, 7), HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = "현재 요청의 공개 작업 상태 · 마우스 또는 Enter로 자세히 보기" };
            System.Windows.Automation.AutomationProperties.SetName(header, "작업 진행 상세 보기");
            Content = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(62, 68, 79)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromRgb(33, 36, 42)), Child = header };
            var body = new StackPanel(); var top = new DockPanel();
            var close = new Button { Content = "×", Width = 26, Height = 26, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.Gainsboro, ToolTip = "작업 상세 닫기 (Escape)" }; DockPanel.SetDock(close, Dock.Right); top.Children.Add(close);
            top.Children.Add(new TextBlock { Text = "공개 작업 내역", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            detailsSummary = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(184, 202, 237)), Margin = new Thickness(0, 7, 0, 7) };
            rows = new ItemsControl { ItemTemplate = StepTemplate() }; body.Children.Add(rows);
            archived = new TextBlock { TextWrapping = TextWrapping.Wrap }; body.Children.Add(archived);
            limit = new TextBlock { Text = "일부 작업은 표시 한도로 생략했습니다.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Khaki, Margin = new Thickness(0, 5, 0, 0) }; body.Children.Add(limit);
            detailsScroll = new ScrollViewer { MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = body };
            var frame = new DockPanel(); DockPanel.SetDock(top, Dock.Top); frame.Children.Add(top); DockPanel.SetDock(detailsSummary, Dock.Top); frame.Children.Add(detailsSummary); frame.Children.Add(detailsScroll);
            popupBody = new Border { Background = new SolidColorBrush(Color.FromRgb(37, 39, 44)), BorderBrush = new SolidColorBrush(Color.FromRgb(81, 88, 103)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(11), Child = frame };
            popupBody.PreviewMouseWheel += (sender, args) => HoverScrollPolicy.HandleWheel(popupBody, args, true);
            popupBody.SetValue(TextElement.ForegroundProperty, Brushes.Gainsboro);
            PopupPalette.Attach(this, this); PopupPalette.Attach(popupBody, this);
            header.SetResourceReference(Control.ForegroundProperty, "AssistantForegroundBrush"); title.SetResourceReference(TextBlock.ForegroundProperty, "AssistantForegroundBrush"); summary.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush");
            detailsSummary.SetResourceReference(TextBlock.ForegroundProperty, "AssistantLinkBrush"); archived.SetResourceReference(TextBlock.ForegroundProperty, "AssistantForegroundBrush"); limit.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush");
            close.SetResourceReference(Control.ForegroundProperty, "AssistantForegroundBrush"); popupBody.SetResourceReference(TextElement.ForegroundProperty, "AssistantForegroundBrush"); popupBody.SetResourceReference(Border.BackgroundProperty, "AssistantPopupBackgroundBrush"); popupBody.SetResourceReference(Border.BorderBrushProperty, "AssistantPopupBorderBrush");
            ((Border)Content).SetResourceReference(Border.BackgroundProperty, "AssistantSurfaceBrush"); ((Border)Content).SetResourceReference(Border.BorderBrushProperty, "AssistantBorderBrush");
            // StaysOpen=false의 native capture는 anchor의 MouseLeave/재진입을 만들어 닫히는 창에 IsOpen만 남길 수 있다.
            // 포인터 이탈은 closeTimer, 키보드는 focus/Escape, 창 전환은 OwnerDeactivated에서 닫는다.
            popup = new Popup { PlacementTarget = header, Placement = PlacementMode.Bottom, AllowsTransparency = true, StaysOpen = true, PopupAnimation = PopupAnimation.Fade, Child = popupBody };
            closeTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(180) }; closeTimer.Tick += CloseTick;
            header.MouseEnter += HeaderEntered; header.MouseLeave += PointerLeft; header.Click += HeaderClicked;
            popupBody.MouseEnter += PopupEntered; popupBody.MouseLeave += PointerLeft;
            header.PreviewKeyDown += KeyPressed; popupBody.PreviewKeyDown += KeyPressed;
            header.LostKeyboardFocus += FocusLeft; popupBody.LostKeyboardFocus += FocusLeft;
            close.Click += (sender, args) => CloseDetails(true);
            popup.Closed += (sender, args) => { pinned = false; closeTimer.Stop(); };
            Loaded += PresenterLoaded; Unloaded += PresenterUnloaded;
            Refresh();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NoticeChanged
        /// desc         : 다른 요청 모델로 바뀔 때 이전 Popup과 구독을 먼저 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 대화 전환의 이전 작업 표시와 이벤트 누수 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void NoticeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) { var view = (ProgressNoticePresenter)sender; view.CloseDetails(false); view.Subscribe(); view.Refresh(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Subscribe
        /// desc         : 로드된 카드의 현재 모델 하나만 관찰한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 재부착과 모델 교체에 중복 구독하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Subscribe() { if (subscribed != null) subscribed.PropertyChanged -= ModelChanged; subscribed = IsLoaded ? Notice : null; if (subscribed != null) subscribed.PropertyChanged += ModelChanged; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Refresh
        /// desc         : 같은 Popup·ItemsControl을 유지한 채 공개 상태와 안전 요약을 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 업데이트 중 Dropdown 재생성·깜빡임 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Refresh()
        {
            if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("진행 카드는 해당 WPF UI 스레드에서만 갱신하세요.");
            var notice = Notice; header.IsEnabled = notice != null;
            title.Text = notice == null ? "작업 진행" : notice.Heading + " · " + notice.OutcomeLabel;
            summary.Text = notice == null ? "공개 작업 상태 수신 대기" : notice.CurrentTask;
            detailsSummary.Text = summary.Text; rows.ItemsSource = notice == null ? null : notice.Steps;
            bool saved = notice != null && notice.IsArchived;
            rows.Visibility = saved ? Visibility.Collapsed : Visibility.Visible;
            archived.Visibility = saved ? Visibility.Visible : Visibility.Collapsed;
            archived.Text = saved ? notice.MarkdownText.Substring(ProgressNotice.MarkdownMarker.Length).Trim() : "";
            limit.Visibility = notice != null && notice.IsTruncated ? Visibility.Visible : Visibility.Collapsed;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IconBrush
        /// desc         : 각 안내 아이콘에 검정 계열 화면과 대비되는 실제 gradient를 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 별도 이미지·외부 리소스 없는 공개 작업 장식.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Brush IconBrush() { var brush = new LinearGradientBrush(Color.FromRgb(127, 204, 255), Color.FromRgb(190, 150, 246), 35); brush.Freeze(); return brush; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StepTemplate
        /// desc         : 종류별 gradient 아이콘·Bold 제목·색상 상태와 단일행 안전 요약을 바인딩한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 일반 안내 heading을 반복하지 않는 세부 작업 행.
        ///                2026-10-19 CodexCode : TASK024 긴 작업명은 유한 폭의 DockPanel 안에서 줄바꿈.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static DataTemplate StepTemplate()
        {
            var root = new FrameworkElementFactory(typeof(StackPanel)); root.SetValue(MarginProperty, new Thickness(0, 5, 0, 7));
            var heading = new FrameworkElementFactory(typeof(DockPanel));
            var icon = new FrameworkElementFactory(typeof(TextBlock)); icon.SetBinding(TextBlock.TextProperty, new Binding("Glyph")); icon.SetValue(TextBlock.ForegroundProperty, IconBrush()); icon.SetValue(MarginProperty, new Thickness(0, 0, 7, 0)); icon.SetValue(DockPanel.DockProperty, Dock.Left); heading.AppendChild(icon);
            var title = new FrameworkElementFactory(typeof(TextBlock)); title.SetBinding(TextBlock.TextProperty, new Binding("Title")); title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); title.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); title.SetValue(StyleProperty, StepAccentStyle()); heading.AppendChild(title); root.AppendChild(heading);
            var detail = new FrameworkElementFactory(typeof(TextBlock)); detail.SetBinding(TextBlock.TextProperty, new Binding("Detail")); detail.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); detail.SetValue(MarginProperty, new Thickness(21, 3, 0, 0)); root.AppendChild(detail);
            var state = new FrameworkElementFactory(typeof(TextBlock)); state.SetBinding(TextBlock.TextProperty, new Binding("StatusText")); state.SetValue(StyleProperty, StepAccentStyle()); state.SetValue(TextBlock.FontSizeProperty, 11.0); state.SetValue(MarginProperty, new Thickness(21, 3, 0, 0)); root.AppendChild(state);
            return new DataTemplate(typeof(ProgressStep)) { VisualTree = root };
        }

        /// <summary>
        /// name         : StepAccentStyle
        /// desc         : 상태 의미는 유지하며 각 Light/Dark semantic brush를 동적으로 조회한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 저장 모델의 고정 Accent 문자열을 실제 Light 렌더에 강제하지 않음.
        /// </summary>
        private static Style StepAccentStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("AssistantMutedForegroundBrush")));
            string[] states = { "started", "completed", "failed" }, keys = { "AssistantLinkBrush", "AssistantAddedBrush", "AssistantRemovedBrush" };
            for (int index = 0; index < states.Length; index++)
            { var trigger = new DataTrigger { Binding = new Binding("State"), Value = states[index] }; trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension(keys[index]))); style.Triggers.Add(trigger); }
            return style;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenDetails
        /// desc         : 마우스 hover는 초점을 빼앗지 않고 click·키보드는 같은 Dropdown을 열어 탐색 가능하게 한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 로드되지 않았거나 모델 없는 과거 컨테이너에서는 열지 않음.
        ///                2026-10-19 CodexCode : TASK024 작업영역 이내의 폭·상세 높이 적용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OpenDetails(bool keyboard)
        {
            if (!IsLoaded || Notice == null) return;
            PopupPalette.Attach(this, this); PopupPalette.Attach(popupBody, this);
            closeTimer.Stop(); popupBody.Width = Math.Max(160, Math.Min(420, Math.Min(SystemParameters.WorkArea.Width - 24, Math.Max(280, ActualWidth))));
            detailsScroll.MaxHeight = Math.Max(80, Math.Min(320, SystemParameters.WorkArea.Height - 100));
            popup.IsOpen = true;
            if (keyboard) { pinned = true; popupBody.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseDetails
        /// desc         : 요청된 경우에만 초점을 헤더로 돌려주고 Popup과 지연 닫기 타이머를 정리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 대화 전환·종료가 사용자 초점을 되가져가지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void CloseDetails(bool restoreFocus = false) { closeTimer.Stop(); pinned = false; popup.IsOpen = false; if (restoreFocus && IsLoaded) header.Focus(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseWithin
        /// desc         : 지정한 대화 컨테이너 아래 진행 Dropdown만 닫는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 다른 창·다른 작업의 Popup은 건드리지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void CloseWithin(DependencyObject root)
        { if (root == null) return; var view = root as ProgressNoticePresenter; if (view != null) view.CloseDetails(); if (!(root is Visual)) return; for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++) CloseWithin(VisualTreeHelper.GetChild(root, index)); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PresenterLoaded
        /// desc         : 표시된 카드만 모델과 창 비활성화 이벤트를 구독한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 창 전환 시 떠 있는 Dropdown 닫기.
        ///                2026-10-18 CodexCode : native capture 없이 소유 창의 바깥 클릭도 닫고 재로드 중복 구독 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PresenterLoaded(object sender, RoutedEventArgs args)
        {
            PopupPalette.Attach(this, this); PopupPalette.Attach(popupBody, this);
            Subscribe();
            if (owner != null) { owner.Deactivated -= OwnerDeactivated; owner.PreviewMouseDown -= OwnerPointerPressed; }
            owner = Window.GetWindow(this);
            if (owner != null) { owner.Deactivated += OwnerDeactivated; owner.PreviewMouseDown += OwnerPointerPressed; }
            Refresh();
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PresenterUnloaded
        /// desc         : 화면에서 제거된 카드의 구독·타이머·Popup을 모두 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 대화 전환 후 이전 작업 상세 잔류 방지.
        ///                2026-10-18 CodexCode : 소유 창의 바깥 클릭 구독도 함께 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PresenterUnloaded(object sender, RoutedEventArgs args) { CloseDetails(); if (subscribed != null) subscribed.PropertyChanged -= ModelChanged; subscribed = null; if (owner != null) { owner.Deactivated -= OwnerDeactivated; owner.PreviewMouseDown -= OwnerPointerPressed; } owner = null; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ModelChanged
        /// desc         : 현재 카드의 공개 상태만 같은 컨테이너에 다시 바인딩한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : stable VM 변경 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ModelChanged(object sender, PropertyChangedEventArgs args) { Refresh(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HeaderEntered
        /// desc         : hover로 현재 작업을 초점 이동 없이 펼친다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 안내 위 마우스 진입 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void HeaderEntered(object sender, MouseEventArgs args) { OpenDetails(false); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PopupEntered
        /// desc         : 작업 상세 위에 있는 동안 지연 닫기를 취소한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : hover 경계 깜빡임 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PopupEntered(object sender, MouseEventArgs args) { closeTimer.Stop(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PointerLeft
        /// desc         : 마우스가 anchor에서 Popup으로 이동할 짧은 시간을 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : click으로 고정한 상세는 outside click·Escape로 닫음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PointerLeft(object sender, MouseEventArgs args) { if (!pinned) closeTimer.Start(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseTick
        /// desc         : anchor·Popup·키보드 초점이 모두 떠난 경우에만 상세를 닫는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 진행 이벤트 갱신은 Popup 수명을 바꾸지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CloseTick(object sender, EventArgs args) { closeTimer.Stop(); if (!pinned && !header.IsMouseOver && !popupBody.IsMouseOver && !header.IsKeyboardFocusWithin && !popupBody.IsKeyboardFocusWithin) CloseDetails(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HeaderClicked
        /// desc         : Enter·Space·마우스 click을 같은 펼침·고정·접기 동작으로 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : hover 이후 click은 먼저 상세를 고정.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void HeaderClicked(object sender, RoutedEventArgs args) { if (popup.IsOpen && pinned) CloseDetails(true); else OpenDetails(true); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : KeyPressed
        /// desc         : Escape는 상세를 닫고 키보드 사용자를 anchor로 돌려보낸다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 숨은 작업이나 명령 실행 단축키는 만들지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void KeyPressed(object sender, KeyEventArgs args) { if (args.Key == Key.Escape && popup.IsOpen) { CloseDetails(true); args.Handled = true; } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FocusLeft
        /// desc         : 키보드가 카드와 상세를 떠날 때 지연 검사를 예약한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Tab으로 다른 조작 항목에 이동할 수 있게 함.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FocusLeft(object sender, KeyboardFocusChangedEventArgs args) { if (!popupBody.IsKeyboardFocusWithin && !header.IsKeyboardFocusWithin) { pinned = false; closeTimer.Start(); } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OwnerDeactivated
        /// desc         : 다른 창으로 이동한 경우 상세를 즉시 접는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 포커스를 되가져오지 않고 Popup 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OwnerDeactivated(object sender, EventArgs args) { CloseDetails(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OwnerPointerPressed
        /// desc         : 포커스를 받지 않는 소유 창의 빈 영역을 클릭해도 고정된 작업 상세를 닫는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : hover native capture를 제거하면서 기존 바깥 클릭 닫힘 동작 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OwnerPointerPressed(object sender, MouseButtonEventArgs args)
        {
            if (popup.IsOpen && !header.IsMouseOver && !popupBody.IsMouseOver) CloseDetails();
        }
    }
}
