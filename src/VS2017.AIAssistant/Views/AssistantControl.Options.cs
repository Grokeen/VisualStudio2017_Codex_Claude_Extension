using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Services;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 모델 능력에 맞는 상세 설정과 대화 AI의 이름·역할·실제 협업 요청을 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-016 모델·속도·담당자 설정과 순차 검토 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private readonly Dictionary<string, string> speeds = new Dictionary<string, string>();
        private ConversationCollaborationStore collaborationStore;
        private AssistantDialog optionsDialog;
        private bool refreshingOptions;
        private string verifiedScmAccount, scmDescription = "형상관리 로그인 확인 전 · Windows 사용자 이름 사용";
        private CancellationTokenSource scmCancellation;
        private bool scmLookupStarted, scmLookupCompleted;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadScmIdentity
        /// desc         : 확인한 형상관리 출처에 따라 사용자명·AI·문자 형식의 이름을 갱신한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : Git 작성자 설정과 인증 계정 구분·수동 이름 보존.
        /// update date  : 2026-10-18 CodexCode : TASK-017 이미지 분석 기반 드롭다운·안내·대화 지정 흐름 개선.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async void ReadScmIdentity(object sender, RoutedEventArgs e)
        {
            await RefreshScmIdentityAsync(false);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshScmIdentityAsync
        /// desc         : 소스 선택 전 실패는 재시도하고 명시적 계정 재확인에서 이름을 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-017 최초 Chat 실행 순서와 로그인 이후 갱신 지원.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task RefreshScmIdentityAsync(bool force)
        {
            if (disposed || scmLookupStarted || (!force && scmLookupCompleted) || !(documents is ISourceNavigationService)) return;
            scmLookupStarted = true; var lookup = new CancellationTokenSource(); scmCancellation = lookup;
            try {
                var identity = await new ScmIdentityService().ReadAsync(((ISourceNavigationService)documents).GetProjectDirectory(), lookup.Token);
                if (disposed) return;
                scmDescription = identity.Description; verifiedScmAccount = (identity.AuthenticationVerified || identity.HasStoredAccount) ? identity.AccountName : null;
                foreach (string providerName in new[] { "Claude", "Codex" }) collaborationStore.ConfigureNaming(providerName, verifiedScmAccount ?? Environment.UserName);
                scmLookupCompleted = true; RefreshAgentIdentity();
            } catch (OperationCanceledException) { }
            catch (Exception) { if (!disposed) { scmDescription = "형상관리 로그인 확인 불가 · Windows 사용자 이름 사용"; RefreshAgentIdentity(); } }
            finally { lookup.Dispose(); if (scmCancellation == lookup) scmCancellation = null; scmLookupStarted = false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EffortLabel
        /// desc         : CLI 토큰은 보존하고 모델이 지원하는 강도를 사용자가 읽기 쉽게 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 기본·고급 강도 표시 추가.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string EffortLabel(string value)
        {
            switch (value) { case "auto": return "자동"; case "low": return "Low"; case "medium": return "Medium"; case "high": return "High"; case "xhigh": return "Extra High"; case "max": return "Max"; case "ultra": return "Ultra"; default: return value; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshModelChoices
        /// desc         : 실제 CLI 카탈로그의 모델·생각 강도를 표시하고 지원되지 않는 속도를 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 캐시 부재 시 기본·직접 입력을 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshModelChoices(bool updateModels = true)
        {
            var catalog = ProviderModelCatalog.ForProvider(selectedProvider);
            var capability = catalog.Find(ModelName.Text);
            string effort = efforts[selectedProvider];
            string[] levels = new[] { "auto" }.Concat(capability == null ? new[] { "low", "medium", "high" } : capability.Efforts.ToArray()).ToArray();
            if (!levels.Contains(effort)) effort = efforts[selectedProvider] = "auto";
            if (updateModels) {
                RequestModel.Items.Clear(); RequestModel.Items.Add("CLI 기본");
                foreach (var model in catalog.Models) RequestModel.Items.Add(model.Id);
                RequestModel.Text = string.IsNullOrEmpty(ModelName.Text) ? "CLI 기본" : ModelName.Text;
            }
            ModeChoice.Items.Clear();
            foreach (string level in levels) ModeChoice.Items.Add(new ComboBoxItem { Content = EffortLabel(level), Tag = level });
            ModeChoice.SelectedValue = effort;
            refreshingOptions = true;
            try
            {
                if (capability == null || !capability.SupportsFast) speeds[selectedProvider] = "standard";
                FastToggle.IsChecked = speeds[selectedProvider] == "fast";
                FastToggle.IsEnabled = !busy && capability != null && capability.SupportsFast;
                FastToggle.ToolTip = capability != null && capability.SupportsFast ? "Fast · 빠른 응답, 사용량 증가 · " + capability.SpeedDescription : "선택한 모델의 Fast 지원이 확인되지 않았습니다.";
                ModelOptionsButton.Content = "☷ " + EffortLabel(effort) + " ⌄";
            }
            finally { refreshingOptions = false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FastChanged
        /// desc         : 사용자가 켠 Fast를 실제 지원 모델에서만 다음 요청에 적용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 제공자별 속도 분리와 오류 시 원상 복구.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FastChanged(object sender, RoutedEventArgs e)
        {
            if (!experienceReady || refreshingOptions || busy) return;
            try { new ProviderExecutionOptions(selectedProvider, ModelName.Text, efforts[selectedProvider], FastToggle.IsChecked == true ? "fast" : "standard"); speeds[selectedProvider] = FastToggle.IsChecked == true ? "fast" : "standard"; }
            catch (Exception error) { StatusLabel.Text = error.Message; syncingModel = true; try { RefreshModelChoices(); } finally { syncingModel = false; } }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ModelOptionsClicked
        /// desc         : 모델 목록·생각 강도 슬라이더·Fast 제약을 한 팝업에서 선택한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 지원 목록을 따르는 고급 옵션과 명시적 설정 저장.
        /// update date  : 2026-10-18 CodexCode : TASK-017 이미지 분석 기반 드롭다운·안내·대화 지정 흐름 개선.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ModelOptionsClicked(object sender, RoutedEventArgs e)
        {
            ShowModelDropdown(ModelOptionsButton);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OptionSection
        /// desc         : 설정 입력을 주제별 검정 계열 구역과 일정한 간격으로 묶는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 모델·역할·권한 공통 구역 구성.
        /// update date  : 2026-10-18 CodexCode : TASK-017 이미지 분석 기반 드롭다운·안내·대화 지정 흐름 개선.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static Border OptionSection(string title, UIElement content)
        {
            return AssistantDialog.CreateSection("▣", title, content);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshAgentIdentity
        /// desc         : 현재 대화의 고유 담당자와 선택한 협업 인원을 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 별도 협업 저장소를 이름·역할의 단일 원본으로 사용.
        /// update date  : 2026-10-18 CodexCode : TASK-017 이미지 분석 기반 드롭다운·안내·대화 지정 흐름 개선.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshAgentIdentity()
        {
            if (!experienceReady || collaborationStore == null) return;
            try {
                var record = activeRecords[selectedProvider]; var agent = collaborationStore.EnsureAgent(record, verifiedScmAccount);
                AgentButton.Content = "♙ AI 지정하여 대화 ⌄";
                CurrentAgentLabel.Text = agent.Name; AgentButton.ToolTip = agent.Name + "\n" + agent.Team + "\n" + (agent.Role.Length == 0 ? "이름·역할·협업 참여자를 설정하세요." : agent.Role) + "\n" + scmDescription;
            } catch (Exception error) { AgentButton.Content = "♙ AI 지정하여 대화 ⌄"; AgentButton.ToolTip = "담당자 저장 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AgentClicked
        /// desc         : 대화 AI 이름·역할·팀과 같은 팀의 실제 검토 참여자를 지정한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 최대 3명 순차 협업과 Markdown 보고 열기 제공.
        /// update date  : 2026-10-18 CodexCode : TASK-017 이미지 분석 기반 드롭다운·안내·대화 지정 흐름 개선.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AgentClicked(object sender, RoutedEventArgs e)
        {
            if (busy || disposed) return;
            try {
                var record = activeRecords[selectedProvider]; var agent = collaborationStore.EnsureAgent(record);
                var dialog = new AssistantDialog(this, "♙ 대화 AI와 협업", "자동 지정된 이름을 확인하고 역할·팀과 함께 검토할 AI를 고르세요.") { Height = 680 };
                optionsDialog = dialog;
                var name = new TextBox { Text = agent.Name, MaxLength = 40, IsReadOnly = true, ToolTip = "사용자명 + AI 이름 + A~Z · 자동 지정" };
                var role = new TextBox { Text = agent.Role, MaxLength = 1000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 75, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                var team = new TextBox { Text = agent.Team, MaxLength = 80 };
                var panel = new StackPanel(); panel.Children.Add(new TextBlock { Text = scmDescription, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Silver, Margin = new Thickness(0, 0, 0, 12) }); panel.Children.Add(OptionSection("AI 이름 · 모든 대화에서 고유", name)); panel.Children.Add(OptionSection("담당 역할", role)); panel.Children.Add(OptionSection("협업팀 · 팀 변경은 먼저 저장하세요", team));
                var participants = new StackPanel(); var selections = new Dictionary<CheckBox, ConversationRecord>();
                foreach (var peer in conversationRecords[selectedProvider].Where(r => r.Id != record.Id && r.IsOpen)) {
                    var identity = collaborationStore.EnsureAgent(peer); if (identity.IsLegacyArchive || identity.Team != agent.Team) continue;
                    var check = new CheckBox { Content = identity.Name + " · " + peer.Title, IsChecked = record.CollaboratorIds.Contains(peer.Id), Margin = new Thickness(0, 4, 0, 6), ToolTip = identity.Role };
                    selections[check] = peer; participants.Children.Add(check);
                }
                participants.Children.Add(new TextBlock { Text = "최대 3명 · 같은 AI 제공자의 열린 대화. 보내기를 누르면 참여자별 실제 AI 호출 후 현재 AI가 결과를 종합합니다. 호출 횟수와 사용량이 늘어납니다. 선택을 모두 해제하면 단독 요청입니다.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Silver, Margin = new Thickness(0, 10, 0, 0) });
                panel.Children.Add(OptionSection("함께 검토할 AI", participants));
                var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; dialog.Body.Content = scroll;
                dialog.SetSections(scroll, new DialogSection("♙", "이름", name), new DialogSection("✎", "역할", role), new DialogSection("▣", "팀", team), new DialogSection("♧", "협업", participants));
                var save = new Button { Content = "✓ AI 설정 저장", IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
                save.Click += (s, a) => { try {
                    var chosen = selections.Where(p => p.Key.IsChecked == true).Select(p => p.Value.Id).ToArray(); if (chosen.Length > 3) throw new InvalidOperationException("협업 참여자는 최대 3명입니다.");
                    if (team.Text.Trim() != agent.Team && chosen.Length > 0) throw new InvalidOperationException("참여자를 해제한 뒤 새 팀을 저장하고 다시 선택하세요.");
                    agent = SaveAgentConfiguration(record, agent, name.Text, role.Text, team.Text, chosen);
                    RefreshAgentIdentity(); dialog.DialogResult = true;
                } catch (Exception error) { TryCollaborationCleanup(() => { agent = collaborationStore.GetAgent(record.Id, record.Provider); name.Text = agent.Name; }, "AI 설정 다시 읽기 실패"); dialog.Message.Text = error.Message; } };
                var report = new Button { Content = "▤ 협업 기록", Margin = new Thickness(0, 0, 8, 0) };
                report.Click += (s, a) => { try { collaborationStore.ExportMarkdown(agent.Team); var navigation = documents as ISourceNavigationService; if (navigation == null) throw new InvalidOperationException("편집기 연결 후 기록을 열 수 있습니다."); navigation.NavigateTo(collaborationStore.ReportPath, 1); } catch (Exception error) { dialog.Message.Text = error.Message; } };
                dialog.Actions.Children.Insert(0, report); dialog.Actions.Children.Insert(0, save);
                try { dialog.ShowDialog(); } finally { optionsDialog = null; }
            } catch (Exception error) { StatusLabel.Text = "AI 설정 실패: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunCollaboratorsAsync
        /// desc         : 고정 원본으로 참여 AI를 실제 호출하고 전달된 검토 결과만 최종 요청에 넣는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 중간 수정안 미적용·상태 기록·취소·오류 정리 추가.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task<ChatRequestContext> RunCollaboratorsAsync(ConversationRecord record, DocumentSnapshot source, string prompt, ChatRequestContext context, CancellationToken token)
        {
            var own = collaborationStore.EnsureAgent(record);
            string identity = collaborationStore.BuildPromptContext(record.Id, record.Provider);
            var replies = new List<string>();
            if (record.CollaboratorIds.Length > 0 && !(provider is IAiContextEditProvider)) throw new InvalidOperationException("이 Provider는 협업 컨텍스트를 지원하지 않습니다.");
            foreach (string id in record.CollaboratorIds) {
                token.ThrowIfCancellationRequested();
                var peer = conversationRecords[record.Provider].FirstOrDefault(r => r.Id == id && r.IsOpen);
                if (peer == null) throw new InvalidOperationException("닫힌 협업 대화가 있습니다. AI 설정에서 참여자를 다시 선택하세요.");
                var agent = collaborationStore.EnsureAgent(peer);
                if (agent.Team != own.Team) throw new InvalidOperationException("협업 참여자의 팀이 변경되었습니다. 참여자를 다시 선택하세요.");
                string handoffText = prompt.Length <= 5000 ? prompt : prompt.Substring(0, 5000) + "\n[공유 요청 일부 생략 · 전체 요청은 주 담당자가 처리]";
                var handoff = collaborationStore.Send(record.Id, record.Provider, peer.Id, peer.Provider, handoffText);
                ChatSession chat;
                if (!chats.TryGetValue(id, out chat)) { chat = new ChatSession { Draft = peer.Draft }; foreach (var message in peer.Messages) chat.Messages.Add(message); chats[id] = chat; }
                try {
                    collaborationStore.Receive(handoff.Id, peer.Id, peer.Provider);
                    peer.State = ConversationRunState.Running; peer.HasUnread = true;
                    chat.Add("user", own.Name + "의 협업 검토 요청:\n" + handoffText); SavePeerConversation(peer, chat);
                    StatusLabel.Text = agent.Name + " 검토 중…";
                    var peerContext = new ChatRequestContext(context.Instructions, context.Model, chat.Messages.ToArray(), context.Attachments, context.ReasoningEffort, context.Speed, collaborationStore.BuildPromptContext(peer.Id, peer.Provider, handoff.Id));
                    var result = await ((IAiContextEditProvider)provider).GenerateWithContextAsync(peer.Provider, ExecutablePath.Text, source, "공유 요청을 담당 역할에 따라 검토하여 수정안을 제안하세요.\n" + handoffText, peerContext, token);
                    token.ThrowIfCancellationRequested(); if (disposed) throw new OperationCanceledException();
                    source.RequireUnchanged(pendingDocument.Capture());
                    string reply = result.Summary + "\n" + result.ChangePreview; if (reply.Length > 10000) reply = reply.Substring(0, 10000) + "\n[검토 결과 일부 생략]";
                    collaborationStore.Reply(handoff.Id, peer.Id, peer.Provider, reply); collaborationStore.Deliver(handoff.Id, record.Id, record.Provider);
                    chat.Add("assistant", reply); peer.State = ConversationRunState.Completed; SavePeerConversation(peer, chat);
                    replies.Add(agent.Name + " (" + agent.Role + "):\n" + reply);
                } catch (OperationCanceledException) {
                    peer.State = ConversationRunState.Stopped; chat.Add("system", "협업 요청이 중지되었습니다.");
                    CleanupCollaboration(handoff, record, peer, chat, true, "협업 요청이 중지되었습니다.");
                    throw;
                } catch (Exception error) {
                    peer.State = ConversationRunState.Failed; chat.Add("system", "협업 실패: " + error.Message);
                    CleanupCollaboration(handoff, record, peer, chat, false, error.Message);
                    throw;
                } finally { TryCollaborationCleanup(() => collaborationStore.ExportMarkdown(own.Team), "협업 보고서 저장 실패"); }
            }
            if (replies.Count > 0) {
                int allowance = Math.Max(0, 7800 - identity.Length); int each = allowance / replies.Count;
                identity += "\n다음은 이번 요청에서 실제 전달된 동료 검토입니다. 원본과 대조하여 판단하고 최종 수정안을 작성하세요.\n" + string.Join("\n\n", replies.Select(r => r.Length <= each ? r : r.Substring(0, each) + " [일부 생략]"));
                StatusLabel.Text = own.Name + " 최종 수정안 종합 중…";
            }
            return new ChatRequestContext(context.Instructions, context.Model, context.History, context.Attachments, context.ReasoningEffort, context.Speed, identity);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SavePeerConversation
        /// desc         : 화면 선택을 바꾸지 않고 참여 대화의 실제 결과·읽지 않음 상태를 저장한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 협업 기록을 기존 Markdown과 같은 형식으로 보관.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SavePeerConversation(ConversationRecord record, ChatSession chat)
        {
            record.Messages = chat.Messages.Select(m => new ChatMessage { Role = m.Role, Text = m.Text }).ToList(); record.Draft = chat.Draft; record.HasUnread = true; workspaceStore.Save(record);
            if (!disposed) RefreshConversationList();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CleanupCollaboration
        /// desc         : 전달 완료 기록을 보존하고 보조 저장 실패가 실제 요청 오류를 덮지 않게 한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 협업 상태별 실패·취소 정리 및 원인 예외 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CleanupCollaboration(CollaborationHandoff handoff, ConversationRecord sender, ConversationRecord peer, ChatSession chat, bool cancelled, string error)
        {
            TryCollaborationCleanup(() => {
                var state = collaborationStore.GetHandoff(handoff.Id).State;
                if (state == CollaborationHandoffState.Delivered || state == CollaborationHandoffState.Replied) peer.State = ConversationRunState.Completed;
                else if (cancelled && (state == CollaborationHandoffState.Pending || state == CollaborationHandoffState.Received)) collaborationStore.Cancel(handoff.Id, sender.Id, sender.Provider);
                else if (state == CollaborationHandoffState.Received) collaborationStore.MarkFailed(handoff.Id, peer.Id, peer.Provider, error.Length > 2000 ? error.Substring(0, 2000) : error);
                else if (state == CollaborationHandoffState.Pending) collaborationStore.Cancel(handoff.Id, sender.Id, sender.Provider);
            }, "협업 종료 상태 저장 실패");
            TryCollaborationCleanup(() => SavePeerConversation(peer, chat), "협업 대화 저장 실패");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryCollaborationCleanup
        /// desc         : 보조 정리 오류를 최근 알림에 남기고 주 요청의 결과·취소 원인을 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 보고서·상태·대화 파일 잠금 오류 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void TryCollaborationCleanup(Action action, string label)
        {
            try { action(); } catch (Exception error) { notifications.Add(label + ": " + error.Message); while (notifications.Count > 20) notifications.RemoveAt(0); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SaveAgentConfiguration
        /// desc         : 참여자 저장 실패 시 이름·역할·팀을 이전 값으로 복구하며 동시 편집은 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 협업 JSON과 대화 Markdown 사이 부분 저장 복구.
        /// </summary>
        /// ---------------------------------------------------------------------
        private ConversationAgentIdentity SaveAgentConfiguration(ConversationRecord record, ConversationAgentIdentity previous, string name, string role, string team, string[] collaborators)
        {
            string[] oldParticipants = record.CollaboratorIds;
            var changed = collaborationStore.UpdateAgent(record.Id, record.Provider, name, role, team, previous.Revision);
            try { record.CollaboratorIds = collaborators; workspaceStore.Save(record); return changed; }
            catch (Exception error) {
                record.CollaboratorIds = oldParticipants;
                try { collaborationStore.UpdateAgent(record.Id, record.Provider, previous.Name, previous.Role, previous.Team, changed.Revision); }
                catch (Exception rollback) { throw new InvalidOperationException("참여자 저장 실패: " + error.Message + " AI 정보의 일부가 저장되었습니다. 복구 충돌: " + rollback.Message + " 다시 열어 확인하세요.", error); }
                throw;
            }
        }
    }
}
