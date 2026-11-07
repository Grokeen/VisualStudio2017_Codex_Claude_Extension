using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace VS2017.AIAssistant.Views
{
    /// <summary>
    /// UI-only immutable presentation data for a comment-to-code proposal.
    /// Coordinates are one-based. Text is displayed verbatim and is never applied by this class.
    /// </summary>
    public sealed class CommentCodePreviewUiPresentation
    {
        public string TargetFilePath { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }
        public string ContextBefore { get; private set; }
        public string ExistingText { get; private set; }
        public string ProposedCode { get; private set; }
        public string ContextAfter { get; private set; }

        public CommentCodePreviewUiPresentation(
            string targetFilePath,
            int line,
            int column,
            string contextBefore,
            string existingText,
            string proposedCode,
            string contextAfter)
        {
            if (String.IsNullOrWhiteSpace(targetFilePath)) throw new ArgumentException("대상 파일 경로가 필요합니다.", nameof(targetFilePath));
            if (line < 1) throw new ArgumentOutOfRangeException(nameof(line), "줄 번호는 1부터 시작합니다.");
            if (column < 1) throw new ArgumentOutOfRangeException(nameof(column), "열 번호는 1부터 시작합니다.");
            if (String.IsNullOrWhiteSpace(proposedCode)) throw new ArgumentException("미리볼 코드가 필요합니다.", nameof(proposedCode));

            TargetFilePath = targetFilePath;
            Line = line;
            Column = column;
            ContextBefore = contextBefore ?? String.Empty;
            ExistingText = existingText ?? String.Empty;
            ProposedCode = proposedCode;
            ContextAfter = contextAfter ?? String.Empty;
        }
    }

    public enum CommentCodePreviewUiAcceptStatus
    {
        Accepted,
        Stale,
        Rejected
    }

    /// <summary>
    /// Result from the adapter's atomic, current-buffer validation and apply callback.
    /// </summary>
    public sealed class CommentCodePreviewUiAcceptResult
    {
        public CommentCodePreviewUiAcceptStatus Status { get; private set; }
        public string Reason { get; private set; }

        private CommentCodePreviewUiAcceptResult(CommentCodePreviewUiAcceptStatus status, string reason)
        {
            Status = status;
            Reason = reason ?? String.Empty;
        }

        public static CommentCodePreviewUiAcceptResult Applied()
        {
            return new CommentCodePreviewUiAcceptResult(CommentCodePreviewUiAcceptStatus.Accepted, String.Empty);
        }

        public static CommentCodePreviewUiAcceptResult Stale(string reason)
        {
            return new CommentCodePreviewUiAcceptResult(CommentCodePreviewUiAcceptStatus.Stale, reason);
        }

        public static CommentCodePreviewUiAcceptResult Rejected(string reason)
        {
            return new CommentCodePreviewUiAcceptResult(CommentCodePreviewUiAcceptStatus.Rejected, reason);
        }
    }

    /// <summary>
    /// Shows a proposed edit at a named source location. The dialog never edits or saves a buffer.
    /// The accept callback must revalidate the captured document, version, selection, caret, IME,
    /// and editor mode atomically before changing the in-memory editor buffer.
    /// </summary>
    public sealed class CommentCodePreviewDialog : AssistantDialog
    {
        private readonly Func<CommentCodePreviewUiAcceptResult> acceptCallback;
        private readonly Action cancelCallback;
        private readonly Action requestAgainCallback;
        private readonly Button acceptButton;
        private readonly Button requestAgainButton;
        private bool isStale;
        private bool accepted;
        private bool cancelDelivered;
        private bool requestAgainDelivered;

        public bool IsStale { get { return isStale; } }
        public bool IsAcceptEnabled { get { return acceptButton.IsEnabled; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CommentCodePreviewDialog
        /// desc         : 공통 WPF 창의 스레드 검사와 조건부 VS 소유자 검사를 그대로 사용한다.
        /// author       : CodexOrig4
        /// create date  : 2026-11-07
        /// update date  : 2026-11-07 CodexOrig : 다른 공통 창과 동일하게 base 초기화 한 줄의 분석기 오탐 범위만 명시.
        /// </summary>
        /// ---------------------------------------------------------------------
        public CommentCodePreviewDialog(
            AssistantControl source,
            CommentCodePreviewUiPresentation presentation,
            Func<CommentCodePreviewUiAcceptResult> accept,
            Action cancel,
            Action requestAgain = null)
#pragma warning disable VSTHRD010 // base는 WPF Dispatcher를 검사하며 실제 VS 주 스레드인 분기에서만 COM 소유자를 조회한다.
            : base(source, "✦ 코드 제안 검토", "제안을 확인하고 수락해야 현재 Visual Studio 편집 버퍼에 반영됩니다. 파일 저장이나 코드 실행은 하지 않습니다.")
#pragma warning restore VSTHRD010
        {
            Dispatcher.VerifyAccess();
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (accept == null) throw new ArgumentNullException(nameof(accept));

            acceptCallback = accept;
            cancelCallback = cancel;
            requestAgainCallback = requestAgain;
            Width = Math.Min(760, MaxWidth);
            Height = Math.Min(680, MaxHeight);

            FooterContent.Content = BuildTarget(presentation);
            Body.Content = BuildPreview(presentation);
            Message.Text = "제안 코드는 아직 편집기에 들어가지 않았습니다.";
            Message.Focusable = true;
            AutomationProperties.SetName(Message, "제안 적용 상태");
            AutomationProperties.SetHelpText(Message, "수락 전에는 실제 편집 버퍼가 바뀌지 않습니다.");

            Actions.Children.Clear();
            requestAgainButton = CreateActionButton("↻ 다시 요청", "다시 요청", false);
            requestAgainButton.Visibility = Visibility.Collapsed;
            requestAgainButton.Click += RequestAgainClicked;
            Actions.Children.Add(requestAgainButton);

            var cancelButton = CreateActionButton("취소", "제안 취소", false);
            cancelButton.IsCancel = true;
            cancelButton.Click += (sender, args) => Close();
            Actions.Children.Add(cancelButton);

            acceptButton = CreateActionButton("✓ 수락", "코드 제안 수락", true);
            acceptButton.IsDefault = false;
            acceptButton.Click += AcceptClicked;
            Actions.Children.Add(acceptButton);

            Closing += PreviewClosing;
        }

        /// <summary>
        /// Invalidates the preview after an editor/document guard changes.
        /// Call on this dialog's dispatcher. The accept callback still performs its own final check.
        /// </summary>
        public void SetStale(string reason)
        {
            Dispatcher.VerifyAccess();
            if (accepted) return;

            isStale = true;
            acceptButton.IsEnabled = false;
            var detail = String.IsNullOrWhiteSpace(reason) ? "편집기 상태가 바뀌어 제안을 적용할 수 없습니다." : reason.Trim();
            Message.Text = "제안이 현재 편집기 상태와 맞지 않아 수락할 수 없습니다. " + detail;
            AutomationProperties.SetName(Message, "제안이 오래되어 수락할 수 없음");
            AutomationProperties.SetHelpText(Message, detail + " 다시 요청하거나 취소하세요.");
            AutomationProperties.SetHelpText(acceptButton, "제안이 오래되어 사용할 수 없습니다. 다시 요청하세요.");
            requestAgainButton.Visibility = requestAgainCallback == null ? Visibility.Collapsed : Visibility.Visible;
            Message.Focus();
        }

        private UIElement BuildTarget(CommentCodePreviewUiPresentation presentation)
        {
            var target = new TextBlock
            {
                Text = String.Format("대상 파일: {0}\n삽입 위치: {1}행 {2}열", presentation.TargetFilePath, presentation.Line, presentation.Column),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            target.ToolTip = presentation.TargetFilePath;
            AutomationProperties.SetName(target, "제안 대상 파일과 삽입 위치");
            AutomationProperties.SetHelpText(target, String.Format("{0}, {1}행 {2}열", presentation.TargetFilePath, presentation.Line, presentation.Column));
            return target;
        }

        private UIElement BuildPreview(CommentCodePreviewUiPresentation presentation)
        {
            var sections = new StackPanel { Margin = new Thickness(0, 2, 0, 4) };
            sections.Children.Add(CreateHeading("현재 문맥"));
            sections.Children.Add(CreateCodeBox("삽입 위치 앞 문맥", presentation.ContextBefore, 68));
            if (!String.IsNullOrEmpty(presentation.ExistingText))
            {
                sections.Children.Add(CreateHeading("대체될 기존 선택 영역"));
                sections.Children.Add(CreateCodeBox("대체될 기존 코드", presentation.ExistingText, 72, "AssistantRemovedBrush"));
            }
            sections.Children.Add(CreateCodeBox("삽입 위치 뒤 문맥", presentation.ContextAfter, 68));
            sections.Children.Add(CreateHeading("제안 코드 · 아직 적용되지 않음"));
            sections.Children.Add(CreateCodeBox("편집기에 삽입할 제안 코드", presentation.ProposedCode, 160, "AssistantAddedBrush"));

            var scroll = new ScrollViewer
            {
                Content = sections,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = true
            };
            AutomationProperties.SetName(scroll, "코드 제안 미리보기");
            AutomationProperties.SetHelpText(scroll, "현재 편집 문맥, 대체 대상, 제안 코드를 구분해 읽습니다.");
            return scroll;
        }

        private TextBlock CreateHeading(string text)
        {
            var heading = new TextBlock
            {
                Text = String.IsNullOrEmpty(text) ? "(문맥 없음)" : text,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 4)
            };
            AutomationProperties.SetName(heading, text);
            return heading;
        }

        private TextBox CreateCodeBox(string accessibleName, string text, double maxHeight, string borderBrushKey = "AssistantBorderBrush")
        {
            var box = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                MinHeight = Math.Min(48, maxHeight),
                MaxHeight = maxHeight,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Padding = new Thickness(9, 7, 9, 7),
                Margin = new Thickness(0, 0, 0, 5)
            };
            box.SetResourceReference(Control.BackgroundProperty, "AssistantCodeBackgroundBrush");
            box.SetResourceReference(Control.ForegroundProperty, "AssistantForegroundBrush");
            box.SetResourceReference(Control.BorderBrushProperty, borderBrushKey);
            AutomationProperties.SetName(box, accessibleName);
            AutomationProperties.SetHelpText(box, "읽기 전용 미리보기입니다. 이 내용은 아직 편집기 버퍼에 반영되지 않았습니다.");
            return box;
        }

        private Button CreateActionButton(string label, string accessibleName, bool primary)
        {
            var button = new Button
            {
                Content = label,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, primary ? "PrimaryButton" : "AssistantActionButton");
            AutomationProperties.SetName(button, accessibleName);
            AutomationProperties.SetHelpText(button, accessibleName + ". Tab으로 이동하고 Space로 선택할 수 있습니다.");
            return button;
        }

        private void AcceptClicked(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess();
            if (isStale || accepted || !acceptButton.IsEnabled) return;

            CommentCodePreviewUiAcceptResult result;
            try { result = acceptCallback(); }
            catch
            {
                SetStale("편집기 상태를 확인하지 못해 적용을 중단했습니다. 다시 요청해 주세요.");
                return;
            }

            if (result == null)
            {
                ShowRejected("편집기에서 수락 결과를 받지 못했습니다. 내용을 다시 확인한 뒤 시도하세요.");
                return;
            }

            if (result.Status == CommentCodePreviewUiAcceptStatus.Accepted)
            {
                accepted = true;
                Close();
                return;
            }

            if (result.Status == CommentCodePreviewUiAcceptStatus.Stale)
            {
                SetStale(result.Reason);
                return;
            }

            if (result.Status == CommentCodePreviewUiAcceptStatus.Rejected)
            {
                ShowRejected(result.Reason);
                return;
            }

            ShowRejected("수락 결과를 인식하지 못해 제안을 적용하지 않았습니다.");
        }

        private void ShowRejected(string reason)
        {
            Message.Text = String.IsNullOrWhiteSpace(reason)
                ? "제안을 적용하지 못했습니다. 편집기 내용을 확인하세요."
                : "제안을 적용하지 못했습니다. " + reason.Trim();
            AutomationProperties.SetName(Message, "제안 적용 오류");
            AutomationProperties.SetHelpText(Message, "제안 미리보기는 열려 있습니다. 내용을 다시 확인하거나 취소하세요.");
            Message.Focus();
        }

        private void RequestAgainClicked(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess();
            if (!isStale || requestAgainCallback == null || requestAgainDelivered) return;

            requestAgainDelivered = true;
            try { requestAgainCallback(); }
            catch
            {
                requestAgainDelivered = false;
                Message.Text = "다시 요청을 시작하지 못했습니다. 창을 닫고 다시 시도하세요.";
                AutomationProperties.SetName(Message, "다시 요청 실패");
                AutomationProperties.SetHelpText(Message, "현재 미리보기는 오래되어 수락할 수 없습니다.");
                return;
            }
            Close();
        }

        private void PreviewClosing(object sender, System.ComponentModel.CancelEventArgs args)
        {
            if (accepted || requestAgainDelivered || cancelDelivered) return;
            cancelDelivered = true;
            if (cancelCallback != null) cancelCallback();
        }
    }
}
