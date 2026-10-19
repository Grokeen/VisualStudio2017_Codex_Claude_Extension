using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Forms;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Providers;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 실제 요청 소유 대화에 공개 작업 카드 하나와 공개 AI 답변만 반영한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 런타임 큐와 분리된 진행 표시 수명 관리.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private ChatSession progressNoticeOwner;
        private ProgressNotice progressNotice;
        private ChatMessage progressNoticeRow;
        private readonly List<ChatMessage> progressReplyRows = new List<ChatMessage>();
        private BrandedCompletionNotification completionNotificationIcon;
        private string completionRequestProvider;
        private string completionResultKind = "answer";
        private DispatcherTimer completionNotificationLifetime;
        private bool completionNotificationUnloadedHooked;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BeginProgressRequest
        /// desc         : 새 요청의 대화 소유권을 고정하고 이전 요청의 미보고 작업을 성공으로 만들지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 작업 이벤트가 없는 요청에는 빈 안내 카드를 추가하지 않음.
        ///                2026-10-20 CodexOrig : 실행 제공자만 고정하고 감사 R58-01에 따라 요청 제목은 알림에 전달하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void BeginProgressRequest()
        {
            RequireProgressUiThread();
            CloseProgressNotices();
            if (progressNotice != null && progressNotice.Complete("ended") && progressNoticeRow != null)
                progressNoticeRow.Text = progressNotice.MarkdownText;
            progressReplyRows.Clear();
            progressNoticeOwner = disposed ? null : ActiveChat;
            progressNotice = disposed ? null : new ProgressNotice();
            progressNoticeRow = null;
            completionRequestProvider = CurrentRequestProvider == "Codex" ? "Codex" : "Claude";
            completionResultKind = "answer";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PresentProgressUpdate
        /// desc         : 공개 답변은 AI 본문으로, 정제된 작업 정보는 같은 요청 카드에 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 숨은 추론·명령 원문 대신 WorkId·State·안전 Detail·실제 종료 코드만 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool PresentProgressUpdate(AgentProgress progress)
        {
            RequireProgressUiThread();
            if (disposed || progress == null || progressNotice == null || !progressNotice.IsActive ||
                progressNoticeOwner == null || !ReferenceEquals(progressNoticeOwner, ActiveChat)) return false;
            if (progress.Kind == "message")
            {
                if (String.IsNullOrWhiteSpace(progress.Text)) return false;
                string text = progress.Text.Trim();
                if (text.Length > 2000) text = text.Substring(0, Char.IsHighSurrogate(text[1999]) ? 1999 : 2000);
                if (HasProgressReply(text) || progressReplyRows.Count >= 80) return false;
                var reply = new ChatMessage { Role = "assistant", Text = text };
                progressReplyRows.Add(reply); AddProgressMessage(reply); return true;
            }
            if (!progressNotice.Apply(progress.Kind, progress.WorkId, progress.State, progress.Detail, progress.ExitCode)) return false;
            if (progressNoticeRow == null)
            {
                progressNoticeRow = new ChatMessage { Role = "system", Text = progressNotice.MarkdownText };
                ProgressNoticeConverter.Attach(progressNoticeRow, progressNotice);
                AddProgressMessage(progressNoticeRow);
            }
            else progressNoticeRow.Text = progressNotice.MarkdownText;
            return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HasProgressReply
        /// desc         : 현재 요청의 실제 AI 본문과 최종 답변 전체가 일치할 때만 중복으로 판정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 잘린 접두어·이전 대화·제거된 메시지로 긴 최종 답변을 숨기지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool HasProgressReply(string text)
        {
            RequireProgressUiThread();
            if (disposed || String.IsNullOrWhiteSpace(text) || progressNoticeOwner == null || !ReferenceEquals(progressNoticeOwner, ActiveChat)) return false;
            string exact = text.Trim();
            return progressReplyRows.Any(row => row.Role == "assistant" && progressNoticeOwner.Messages.Contains(row) && String.Equals((row.Text ?? "").Trim(), exact, StringComparison.Ordinal));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CompleteProgressRequest
        /// desc         : 호출자가 확인한 완료·실패·취소만 기록하고 개별 작업 성공을 추정하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 요청 종료 후 늦은 이벤트는 모델에 반영되지 않음.
        ///                2026-10-20 CodexOrig : 알림 설정을 조회하기 전에 WPF UI 스레드 접근을 명시적으로 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CompleteProgressRequest(string outcome = "completed")
        {
            Dispatcher.VerifyAccess();
            RequireProgressUiThread();
            if (progressNotice == null || !progressNotice.Complete(outcome)) return;
            if (progressNoticeRow != null) progressNoticeRow.Text = progressNotice.MarkdownText;
            // OS 알림 오류는 완료된 AI 요청·대화 저장 경로를 실패로 바꾸지 않는다.
            try
            {
                if (ShouldShowCompletionNotification(progressNotice.Outcome, GetCompletionNotificationsEnabled(), IsLoaded))
                    ShowCompletionNotification();
            }
            catch (Exception) { }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShouldShowCompletionNotification
        /// desc         : 실제로 완료된 요청만 사용자 설정과 표시된 도구 창 상태에 따라 알린다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK049 실패·취소·미표시 창 알림 제외.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool ShouldShowCompletionNotification(string outcome, bool enabled, bool loaded)
        { return enabled && loaded && String.Equals(outcome, "completed", StringComparison.Ordinal); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShowCompletionNotification
        /// desc         : 요청 원문 없이 앱이 확인한 결과 유형과 보고된 완료 수를 브랜드 알림으로 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK049 VSIX의 Start 메뉴 등록 없이 지원되는 NotifyIcon 사용.
        ///                2026-10-20 CodexOrig : TASK054 제공자 로고 hBalloonIcon·안전 요약과 이전 제공자 자원 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ShowCompletionNotification()
        {
            try
            {
                if (disposed) return;
                if (!completionNotificationUnloadedHooked) { Unloaded += CompletionNotificationUnloaded; completionNotificationUnloadedHooked = true; }
                if (completionNotificationIcon != null && completionNotificationIcon.Provider != completionRequestProvider)
                    DisposeCompletionNotification();
                if (completionNotificationIcon == null)
                    completionNotificationIcon = new BrandedCompletionNotification(completionRequestProvider);
                completionNotificationIcon.Show(completionRequestProvider + " 작업 완료", CompletionNotificationContent.Body(completionResultKind, progressNotice));
                if (completionNotificationLifetime == null)
                {
                    completionNotificationLifetime = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
                    completionNotificationLifetime.Tick += CompletionNotificationExpired;
                }
                completionNotificationLifetime.Stop();
                completionNotificationLifetime.Start();
            }
            catch (Exception) { DisposeCompletionNotification(); }
        }

        private void CompletionNotificationExpired(object sender, EventArgs args) { DisposeCompletionNotification(); }
        private void CompletionNotificationUnloaded(object sender, RoutedEventArgs args) { DisposeCompletionNotification(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeCompletionNotification
        /// desc         : 작업 표시줄 아이콘과 수명 타이머를 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK049 알림 후 및 도구 창 해제 시 자원 반환.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeCompletionNotification()
        {
            if (completionNotificationLifetime != null)
            {
                completionNotificationLifetime.Stop();
                completionNotificationLifetime.Tick -= CompletionNotificationExpired;
                completionNotificationLifetime = null;
            }
            if (completionNotificationIcon != null)
            {
                var icon = completionNotificationIcon;
                completionNotificationIcon = null;
                try { icon.Dispose(); }
                catch (Exception) { }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CloseProgressNotices
        /// desc         : 대화 전환·화면 해제 시 이 컨트롤의 작업 Dropdown만 접는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 닫기 자체는 실제 작업 상태나 대화 소유권을 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CloseProgressNotices() { RequireProgressUiThread(); ProgressNoticePresenter.CloseWithin(this); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddProgressMessage
        /// desc         : 모델 연결을 끝낸 메시지만 대화에 추가하고 기존 100개 상한을 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : FrameworkElement 공유 없이 안정된 메시지·모델 참조 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddProgressMessage(ChatMessage message)
        { progressNoticeOwner.Messages.Add(message); while (progressNoticeOwner.Messages.Count > 100) progressNoticeOwner.Messages.RemoveAt(0); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequireProgressUiThread
        /// desc         : 진행 모델과 WPF 메시지 컬렉션을 UI 스레드에서만 변경하도록 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : VS SDK 의존 없이 Dispatcher.CheckAccess로 경계 보장.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RequireProgressUiThread() { if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("작업 진행 표시는 UI 스레드에서 갱신해야 합니다."); }
    }
}
