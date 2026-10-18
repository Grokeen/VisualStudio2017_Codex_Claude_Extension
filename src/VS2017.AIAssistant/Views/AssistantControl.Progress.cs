using System;
using System.Collections.Generic;
using System.Windows.Threading;
using VS2017.AIAssistant.Providers;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 작업별 진행을 갱신하고 같은 안내·최종 답변이 반복해서 쌓이지 않게 한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 20:25 추가 요청의 진행 구분·중복 제거.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private readonly object progressQueueGate = new object();
        private readonly Queue<PendingProgress> pendingProgress = new Queue<PendingProgress>();
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PendingProgress
        /// desc         : 수신 당시 연결·요청 번호를 공개 진행과 함께 보관한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 완료 직전 게시 이벤트도 순서대로 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class PendingProgress
        { internal int Connection, Request; internal AgentProgress Progress; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResetRuntimeProgress
        /// desc         : 이전 요청의 진행 게시와 중복 판정만 초기화하고 대화 원문은 유지한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 대화·연결 간 진행 혼합 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ResetRuntimeProgress()
        {
            Dispatcher.VerifyAccess();
            lock (progressQueueGate) pendingProgress.Clear();
            BeginProgressRequest();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : QueueRuntimeProgress
        /// desc         : 백그라운드 진행을 제한된 큐에 넣고 UI에서 처리하도록 예약한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 비공개 명령 출력 없이 실제 진행 게시 순서 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void QueueRuntimeProgress(AgentProgress progress)
        {
            if (disposed || progress == null || String.IsNullOrWhiteSpace(progress.Text)) return;
            var pending = new PendingProgress { Connection = runtimeGeneration, Request = runtimeRequestVersion, Progress = progress };
            lock (progressQueueGate) { if (pendingProgress.Count >= 512) return; pendingProgress.Enqueue(pending); }
            {
#pragma warning disable VSTHRD001 // 큐의 WPF 표시를 예약하며 COM 호출이나 동기 대기를 수행하지 않는다.
                var posted = Dispatcher.InvokeAsync(() => { Dispatcher.VerifyAccess(); FlushRuntimeProgress(); }, DispatcherPriority.Input);
#pragma warning restore VSTHRD001
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FlushRuntimeProgress
        /// desc         : 현재 요청의 게시를 순서대로 반영하고 한번에 Markdown을 저장한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 최종 답변보다 늦게 진행이 나타나는 순서 역전 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FlushRuntimeProgress()
        {
            Dispatcher.VerifyAccess();
            PendingProgress[] batch;
            lock (progressQueueGate) { batch = pendingProgress.ToArray(); pendingProgress.Clear(); }
            bool changed = false;
            foreach (var item in batch)
                if (!disposed && item.Connection == runtimeGeneration && item.Request == runtimeRequestVersion) changed |= ApplyRuntimeProgress(item.Progress);
            if (changed) { FollowConversation(false); PersistCurrentConversation(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApplyRuntimeProgress
        /// desc         : 공개 답변은 한번만 표시하고 명령·파일·도구는 상태별 집계를 갱신한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 반복 안내를 작업 이름·진행·완료·실패로 세분화.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool ApplyRuntimeProgress(AgentProgress progress)
        {
            Dispatcher.VerifyAccess();
            return PresentProgressUpdate(progress);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddRuntimeFinalReply
        /// desc         : 공개 중간 답변과 완전히 같은 최종 답변을 다시 쌓지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 최종 진행을 먼저 비우고 실제 AI 답변을 한번만 기록.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddRuntimeFinalReply(string text)
        {
            Dispatcher.VerifyAccess(); FlushRuntimeProgress();
            string key = (text ?? "").Trim();
            if (key.Length != 0 && !HasProgressReply(key)) ActiveChat.Add("assistant", key);
            FollowConversation(false);
        }
    }
}
