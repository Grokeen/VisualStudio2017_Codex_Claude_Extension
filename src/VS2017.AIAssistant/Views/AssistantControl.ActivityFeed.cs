using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
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
    /// update date  : 2026-11-03 CodexOrig : 안전한 활동 집계·복합 소유권과 명시 읽음 CAS 저장을 분리.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        // 2026-11-08 CodexOrig8: close only the old activity view and discard derived targets when a project scope changes.
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProjectConversationViewsChanging
        /// desc         : 솔루션 전환 직전에 이전 활동함을 닫고 파생 선택·읽음 대상을 초기화한다.
        /// author       : CodexOrig8
        /// create date  : 2026-11-08
        /// update date  : 2026-11-08 CodexOrig8 : 이전 대화 저장·캡처 이후 호출하며 저장 원문을 변경하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        partial void ProjectConversationViewsChanging()
        {
            Dispatcher.VerifyAccess();
            CloseActivityFeedView();
            activitySaveFailures.Clear();
            activityChoices.Clear();
            activityNavigationTarget = null;
            activityReadTarget = null;
            activityReadCasTarget = null;
            deferActivityRead = false;
        }

        // 2026-11-08 CodexOrig8: restore transient failure badges with their captured scope, including a failed transition rollback.
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureProjectConversationViewState
        /// desc         : 현재 범위의 일시적 저장 실패 표식을 캡처하여 재진입 또는 전환 실패 때 복원한다.
        /// author       : CodexOrig8
        /// create date  : 2026-11-08
        /// update date  : 2026-11-08 CodexOrig8 : 복원도 소유 Dispatcher에서 수행하고 기존 기록은 보존한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        partial void CaptureProjectConversationViewState(ref Action restore)
        {
            Dispatcher.VerifyAccess();
            string[] failures = activitySaveFailures.ToArray();
            restore = () =>
            {
                Dispatcher.VerifyAccess();
                activitySaveFailures.Clear();
                foreach (string key in failures) activitySaveFailures.Add(key);
            };
        }

        private readonly HashSet<string> activitySaveFailures = new HashSet<string>(StringComparer.Ordinal);
        private ConversationRecord activityNavigationTarget;
        private ConversationRecord activityReadTarget;
        private ConversationRecord activityReadCasTarget;
        private bool deferActivityRead;
        private string activityNavigationFailureMessage = "대화 소유 상태가 변경되어 이동하지 않았습니다. 활동함을 새로 열어 확인해 주세요.";
        private readonly Dictionary<string, ActivityChoiceState> activityChoices = new Dictionary<string, ActivityChoiceState>(StringComparer.Ordinal);

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivityChoiceState
        /// desc         : 마지막 공개 답변이 바뀔 때만 기존 선택지 판정을 재사용하는 메모리 캐시다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class ActivityChoiceState
        {
            internal ChatMessage Message;
            internal string Text;
            internal bool HasChoices;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivityOwnershipIndex
        /// desc         : 등록된 id 충돌과 정확한 열린 레코드 소유권을 한 번의 순회로 집계한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class ActivityOwnershipIndex
        {
            internal readonly Dictionary<string, int> IdCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            internal readonly Dictionary<string, int> KeyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            internal readonly HashSet<ConversationRecord> OpenRecords = new HashSet<ConversationRecord>();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BuildActivityOwnershipIndex
        /// desc         : 닫힌 레코드도 id 단독 캐시의 충돌 검사에 포함하고 유효한 열린 소유자만 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private ActivityOwnershipIndex BuildActivityOwnershipIndex()
        {
            var result = new ActivityOwnershipIndex();
            foreach (var entry in conversationRecords)
            {
                if (entry.Value == null) continue;
                foreach (var record in entry.Value)
                {
                    if (record == null || !ConversationActivityFeed.IsValidIdentity("Codex", record.Id)) continue;
                    int count; result.IdCounts.TryGetValue(record.Id, out count); result.IdCounts[record.Id] = count + 1;
                    if (record.Provider != entry.Key || !ConversationActivityFeed.IsValidIdentity(record.Provider, record.Id)) continue;
                    string key = ActivityKey(record); result.KeyCounts.TryGetValue(key, out count); result.KeyCounts[key] = count + 1;
                    if (record.IsOpen) result.OpenRecords.Add(record);
                }
            }
            return result;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryResolveActivityOwner
        /// desc         : 검증된 복합 식별자와 현재 등록된 유일 레코드만 활동함 동작에 허용한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool TryResolveActivityOwner(ConversationActivityItem item, out ConversationRecord record)
        {
            record = null;
            if (item == null || !ConversationActivityFeed.IsValidIdentity(item.Provider, item.Id)) return false;
            var index = BuildActivityOwnershipIndex(); int count;
            if (!index.IdCounts.TryGetValue(item.Id, out count) || count != 1) return false;
            record = index.OpenRecords.FirstOrDefault(candidate => candidate.Provider == item.Provider && candidate.Id == item.Id);
            return record != null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryMarkActivityConversationRead
        /// desc         : 선택을 바꾸지 않고 정확한 소유 대화의 읽음만 기존 CAS 저장하며 실패 시 미읽음을 복원한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig : 최초 소유 키에 실패를 귀속하고 CAS commit 이후 캐시 실패를 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool TryMarkActivityConversationRead(ConversationActivityItem item)
        {
            Dispatcher.VerifyAccess();
            ConversationRecord record;
            if (disposed || !experienceReady || !TryResolveActivityOwner(item, out record)) return false;
            if (ReferenceEquals(activityReadCasTarget, record)) activityReadCasTarget = null;
            if (!record.HasUnread) return true;
            string ownedKey = ActivityKey(record), ownedId = record.Id;
            try
            {
                record.HasUnread = false;
                ConversationRecord verified;
                if (!TryResolveActivityOwner(item, out verified) || !ReferenceEquals(verified, record)) throw new InvalidOperationException("Activity ownership changed.");
                ConversationStorage.Save(record);
            }
            catch (Exception)
            {
                record.HasUnread = true;
                activitySaveFailures.Add(ownedKey);
                RefreshActivityFeedBadge();
                return false;
            }
            try { persistedConversationFingerprints[ownedId] = ConversationFingerprint(record); }
            catch (Exception) { persistedConversationFingerprints.Remove(ownedId); }
            activitySaveFailures.Remove(ownedKey);
            RefreshActivityFeedBadge();
            return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivityKey
        /// desc         : 다른 제공자의 같은 id가 섞이지 않도록 복합 키를 만든다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ActivityKey(ConversationRecord record)
        { return record.Provider + "\0" + record.Id; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SetActivitySaveFailure
        /// desc         : 저장 결과만 기억하고 오류 문구나 대화 원문은 활동함에 복사하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SetActivitySaveFailure(ConversationRecord record, bool failed)
        {
            if (record == null || !ConversationActivityFeed.IsValidIdentity(record.Provider, record.Id)) return;
            if (failed) activitySaveFailures.Add(ActivityKey(record));
            else activitySaveFailures.Remove(ActivityKey(record));
            RefreshActivityFeedBadge();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshActivityFeedBadge
        /// desc         : 원문 조회 없이 열린 대화의 미읽음 개수만 상단 단추에 반영한다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig : 잘못된 소유자와 중복 복합 키를 미읽음 수에서 제외.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshActivityFeedBadge()
        {
            if (ActivityFeedButton == null) return;
            var index = BuildActivityOwnershipIndex();
            int unread = index.OpenRecords.Count(record => record.HasUnread && index.KeyCounts[ActivityKey(record)] == 1);
            ActivityFeedButton.Content = unread == 0 ? "◉ 활동" : "◉ 활동 " + unread;
            AutomationProperties.SetName(ActivityFeedButton, "대화 활동함 · 미읽음 " + unread + "개");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BuildActivityFeedSnapshot
        /// desc         : 열려 있는 두 제공자의 저장 상태와 실제 실행 소유권을 메모리에서만 집계한다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig : 중복 id·요청을 한 번 집계하고 선택지 캐시와 실제 HasChanges를 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private ConversationActivitySnapshot BuildActivityFeedSnapshot(ConversationActivityFilter filter)
        {
            Dispatcher.VerifyAccess();
            var index = BuildActivityOwnershipIndex();
            var waitingCounts = new Dictionary<ConversationRecord, int>();
            var seenRequests = new HashSet<QueuedUiRequest>();
            foreach (var entry in roomQueues)
                foreach (var request in entry.Value ?? new Queue<QueuedUiRequest>())
                    if (request != null && request.Record != null && entry.Key == request.Record.Id) CountActivityWaiting(request, index, seenRequests, waitingCounts);
            foreach (var request in queuedRequests) CountActivityWaiting(request, index, seenRequests, waitingCounts);
            var inputs = new List<ConversationActivityInput>();
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in index.OpenRecords)
            {
                string providerName = record.Provider, key = ActivityKey(record);
                if (index.KeyCounts[key] != 1) continue;
                seenKeys.Add(key);
                bool uniqueId = index.IdCounts[record.Id] == 1;
                RoomRun run, collaboration;
                bool ownedRun = roomRuns.TryGetValue(record.Id, out run) && run != null && run.Request != null &&
                    ReferenceEquals(run.Request.Record, record) && run.Request.Provider == providerName;
                bool collaborationRun = uniqueId && record.State == ConversationRunState.Running && collaboratingRooms.TryGetValue(record.Id, out collaboration) &&
                    collaboration != null && collaboration.Request != null && collaboration.Request.CollaboratorIds != null && collaboration.Request.CollaboratorIds.Contains(record.Id);
                int waiting; waitingCounts.TryGetValue(record, out waiting);
                ChatSession chat;
                IList<ChatMessage> messages = uniqueId && chats.TryGetValue(record.Id, out chat) ? (IList<ChatMessage>)chat.Messages : record.Messages;
                bool choices = HasActivityChoices(key, messages);
                bool current = IsRoomShown(record);
                RoomProposal proposal;
                bool hasProposal = uniqueId && (current && pendingProposal != null && pendingProposal.Source != null && pendingProposal.HasChanges ||
                    roomProposals.TryGetValue(record.Id, out proposal) && proposal != null && proposal.Proposal != null && proposal.Proposal.Source != null && proposal.Proposal.HasChanges);
                inputs.Add(new ConversationActivityInput {
                    Provider = providerName, Id = record.Id, IsOpen = true,
                    HasUnread = record.HasUnread, HasLiveRun = ownedRun || collaborationRun ||
                        executingRequest != null && ReferenceEquals(executingRequest.Record, record) && executingRequest.Provider == providerName && busy,
                    HasProposal = hasProposal, HasChoices = choices, SaveFailed = activitySaveFailures.Contains(key), PendingCount = waiting,
                    State = record.State, LastActivityAt = record.UpdatedAt == default(DateTimeOffset) ? record.CreatedAt : record.UpdatedAt
                });
            }
            foreach (string stale in activityChoices.Keys.Where(key => !seenKeys.Contains(key)).ToArray()) activityChoices.Remove(stale);
            return ConversationActivityFeed.Build(inputs, filter);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CountActivityWaiting
        /// desc         : 정확한 열린 소유 레코드의 동일 요청을 대기열 간에도 한 번만 센다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CountActivityWaiting(QueuedUiRequest request, ActivityOwnershipIndex index, HashSet<QueuedUiRequest> seen, Dictionary<ConversationRecord, int> counts)
        {
            if (request == null || request.Record == null || request.Provider != request.Record.Provider || !index.OpenRecords.Contains(request.Record) ||
                index.KeyCounts[ActivityKey(request.Record)] != 1 || !seen.Add(request)) return;
            int count; counts.TryGetValue(request.Record, out count); counts[request.Record] = count + 1;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HasActivityChoices
        /// desc         : 마지막 공개 답변의 참조와 불변 본문이 바뀐 경우에만 기존 선택지 파서를 호출한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool HasActivityChoices(string key, IList<ChatMessage> messages)
        {
            ChatMessage last = null;
            for (int index = messages == null ? -1 : messages.Count - 1; index >= 0; index--)
            {
                var message = messages[index];
                if (message != null && (message.Role == "user" || message.Role == "assistant")) { last = message; break; }
            }
            if (last == null || last.Role != "assistant") { activityChoices.Remove(key); return false; }
            ActivityChoiceState cached;
            if (activityChoices.TryGetValue(key, out cached) && ReferenceEquals(cached.Message, last) && ReferenceEquals(cached.Text, last.Text)) return cached.HasChoices;
            bool choices;
            try { choices = last.Choices.Length > 0; } catch (Exception) { choices = false; }
            activityChoices[key] = new ActivityChoiceState { Message = last, Text = last.Text, HasChoices = choices };
            return choices;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryOpenActivityConversation
        /// desc         : 클릭 시점에 provider+id+열림 소유권을 다시 확인하고 기존 저장/읽음 경로로 전환한다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig : 닫힌 레코드를 포함한 id 충돌과 현재 소유권을 전환 전에 확인.
        ///                2026-11-03 CodexOrig : 병렬 방간 이동과 동일 현재 방 재열기를 허용하고 전역 실행 제한을 고정 안내로 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool TryOpenActivityConversation(ConversationActivityItem item)
        {
            Dispatcher.VerifyAccess();
            activityNavigationFailureMessage = "대화 소유 상태가 변경되어 이동하지 않았습니다. 활동함을 새로 열어 확인해 주세요.";
            if (item == null || disposed || !experienceReady ||
                (item.Provider != "Claude" && item.Provider != "Codex")) return false;
            ConversationRecord target;
            if (!TryResolveActivityOwner(item, out target)) return false;
            ConversationRecord current;
            ConversationRecord verifiedCurrent;
            if (!activeRecords.TryGetValue(selectedProvider, out current) || current == null ||
                !TryResolveActivityOwner(new ConversationActivityItem { Provider = selectedProvider, Id = current.Id }, out verifiedCurrent) ||
                !ReferenceEquals(current, verifiedCurrent)) return false;
            bool alreadyShown = selectedProvider == item.Provider && ReferenceEquals(current, target);
            if (!alreadyShown && (busy || queuedRequests.Count > 0 || drainingRequests || executingRequest != null))
            {
                activityNavigationFailureMessage = "이 실행이 끝나거나 중지한 뒤 다른 대화로 이동해 주세요.";
                return false;
            }
            activityReadTarget = null;
            activityReadCasTarget = null;
            deferActivityRead = true;
            activityNavigationTarget = target;
            try
            {
                if (selectedProvider != item.Provider)
                {
                    ProviderChoice.SelectedItem = ProviderChoice.Items.Cast<TabItem>().First(tab => Convert.ToString(tab.Tag) == item.Provider);
                }
                else if (!ReferenceEquals(activeRecords[selectedProvider], target)) ConversationList.SelectedItem = target;
            }
            finally { activityNavigationTarget = null; deferActivityRead = false; }
            bool opened = selectedProvider == item.Provider && activeRecords.ContainsKey(item.Provider) && ReferenceEquals(activeRecords[item.Provider], target);
            if (opened) { activityReadTarget = target; activityReadCasTarget = target; RefreshActivityFeedBadge(); }
            else activityNavigationFailureMessage = "대화를 저장하지 못해 이동하지 않았습니다. 저장 상태를 확인한 뒤 다시 시도해 주세요.";
            return opened;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetActivityNavigationFailureMessage
        /// desc         : 소유권·전역 실행·저장 제한에 맞는 고정 안내만 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private string GetActivityNavigationFailureMessage()
        { return activityNavigationFailureMessage; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PreserveActivityPendingProposal
        /// desc         : 활동함 이동 직전의 실제 승인 대기 제안을 원래 방 소유 상태로 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PreserveActivityPendingProposal()
        {
            if (activityNavigationTarget == null || pendingProposal == null || pendingProposal.Source == null || !pendingProposal.HasChanges || pendingDocument == null) return;
            ConversationRecord record;
            if (!activeRecords.TryGetValue(selectedProvider, out record)) return;
            ConversationRecord verified;
            if (!TryResolveActivityOwner(new ConversationActivityItem { Provider = selectedProvider, Id = record.Id }, out verified) || !ReferenceEquals(record, verified)) return;
            roomProposals[record.Id] = new RoomProposal { Proposal = pendingProposal, Document = pendingDocument };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeActivityFeed
        /// desc         : 도구 창 종료 시 갱신 타이머와 모달 활동함을 닫아 수명을 정리한다.
        /// author       : CodexCode
        /// create date  : 2026-11-03
        /// update date  : 2026-11-03 CodexOrig : UI 종료 후 메모리 선택지 캐시와 제어 플래그를 함께 해제.
        ///                2026-11-03 CodexOrig : 활동함 해제 진입점에서 소유 Dispatcher 스레드를 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeActivityFeed()
        {
            Dispatcher.VerifyAccess();
            CloseActivityFeedView();
            activityNavigationTarget = null;
            activityReadTarget = null;
            activityReadCasTarget = null;
            deferActivityRead = false;
            activitySaveFailures.Clear();
            activityChoices.Clear();
        }
    }
}
