using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Providers;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 실제 같은 팀 AI의 수신함·전송 승인·읽기 전용 답변과 저장된 회신 읽기를 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 AI 초안은 데이터로만 읽으며 전송·실행은 명시 버튼으로 분리.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private MessageCenterView messageCenter;
        private sealed class MessageRow
        {
            internal CollaborationHandoff Item;
            public string Label { get { return Item.FromName + " → " + Item.ToName + " · " + Item.State + " · " + Item.UpdatedAt.ToLocalTime().ToString("MM-dd HH:mm"); } }
        }
        private sealed class MessageCenterView
        {
            internal AssistantDialog Dialog;
            internal ConversationRecord Owner;
            internal ComboBox Recipient;
            internal TextBox Draft, Detail;
            internal ListBox Inbox, Outbox;
            internal Button Send, Run, Stop, Read;
            internal CancellationTokenSource Execution;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddMessageActions
        /// desc         : 답변 원문 또는 AI가 만든 정확한 주소의 초안을 사용자 검토 창에 채운다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 렌더·메뉴 열기·초안 파싱만으로 전송하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddMessageActions(ContextMenu menu, MarkdownPreview preview)
        {
            Dispatcher.VerifyAccess();
            var action = new MenuItem { Header = "다른 AI에게 메시지 보내기…" };
            menu.Items.Add(action);
            menu.Opened += (s, a) => {
                Dispatcher.VerifyAccess();
                var row = preview.DataContext as ChatMessage;
                action.Visibility = !disposed && row != null && row.Role == "assistant" ? Visibility.Visible : Visibility.Collapsed;
                CollaborationMessageRequest draft;
                action.Header = CollaborationMessageRequest.TryParse(preview.Markdown, out draft) ? "AI 메시지 초안 전송 검토…" : "다른 AI에게 메시지 보내기…";
            };
            action.Click += (s, a) => {
                Dispatcher.VerifyAccess();
                var row = preview.DataContext as ChatMessage; if (disposed || row == null || row.Role != "assistant") return;
                if (!ActiveChat.Messages.Contains(row)) { StatusLabel.Text = "현재 대화의 답변에서 메시지를 선택하세요."; return; }
                CollaborationMessageRequest draft; CollaborationMessageRequest.TryParse(preview.Markdown, out draft);
                string selected = preview.TextView.Selection.Text;
                OpenMessageCenter(draft == null ? (String.IsNullOrWhiteSpace(selected) ? preview.Markdown : selected) : draft.Message, draft);
            };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenMessageCenter
        /// desc         : 선택된 현재 AI 주소를 고정한 독립 메시지센터를 연다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 수신 AI 대화로 이동한 뒤 해당 AI의 수신함에서 응답한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OpenMessageCenter(string body, CollaborationMessageRequest request)
        {
            Dispatcher.VerifyAccess(); if (disposed || !experienceReady) return;
            if (messageCenter != null) { messageCenter.Dialog.Activate(); return; }
            try
            {
                messageCenter = CreateMessageCenter(activeRecords[selectedProvider], body, request);
                messageCenter.Dialog.ShowDialog();
            }
            catch (Exception error) { if (!disposed) StatusLabel.Text = "메시지센터: " + error.Message; }
            finally { DisposeMessageCenter(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateMessageCenter
        /// desc         : 실제 수신자·본문과 받은/보낸 상태를 검토하는 UI를 만들며 실행은 하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 테스트는 같은 제품 컨트롤을 무창으로 조작할 수 있다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private MessageCenterView CreateMessageCenter(ConversationRecord owner, string body, CollaborationMessageRequest request)
        {
            Dispatcher.VerifyAccess();
            var agent = collaborationStore.EnsureAgent(owner);
            var view = new MessageCenterView { Owner = owner, Dialog = new AssistantDialog(this, "✉ AI 메시지센터 · " + agent.Name,
                "수신자와 본문을 확인한 뒤 전송을 승인하세요. 수신함 저장과 AI 실행은 별개입니다. 상대 AI 대화를 선택하고 이 창을 열면 받은 메시지에 답할 수 있습니다.") { Width = 780, Height = 760 } };
            var panel = new StackPanel();
            var peers = collaborationStore.ListAgents(agent.Team).Where(p => p.IsOpen && p.ConversationId != owner.Id).ToArray();
            view.Recipient = new ComboBox { ItemsSource = peers, DisplayMemberPath = "Name", MinWidth = 200, SelectedIndex = -1 };
            if (request != null) view.Recipient.SelectedItem = peers.SingleOrDefault(p => p.ConversationId == request.RecipientId && p.Provider == request.Provider);
            view.Draft = new TextBox { Text = body ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 6000, MinHeight = 90, MaxHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            view.Send = new Button { Content = "✓ 이 수신자에게 전송 승인", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 12) };
            panel.Children.Add(OptionSection("수신 AI · 같은 팀의 열린 대화", view.Recipient));
            panel.Children.Add(OptionSection("전달 본문 · 프로젝트/대화/첨부 전체를 자동 공유하지 않습니다", view.Draft)); panel.Children.Add(view.Send);
            if (peers.Length == 0) panel.Children.Add(CreateThemedDescription("같은 팀의 다른 열린 AI 대화를 먼저 만들거나 역할·팀을 확인하세요.", new Thickness(0, 0, 0, 8)));
            else if (request != null && view.Recipient.SelectedItem == null) panel.Children.Add(CreateThemedDescription("AI 초안의 수신자가 현재 팀에 없거나 닫혀 있습니다. 실제 수신자를 직접 선택하세요.", new Thickness(0, 0, 0, 8)));
            view.Inbox = new ListBox { DisplayMemberPath = "Label", MinHeight = 60, MaxHeight = 110 };
            view.Outbox = new ListBox { DisplayMemberPath = "Label", MinHeight = 60, MaxHeight = 110 };
            panel.Children.Add(OptionSection("현재 AI의 받은 메시지", view.Inbox)); panel.Children.Add(OptionSection("보낸 메시지 · 실제 회신", view.Outbox));
            view.Detail = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MaxHeight = 170, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            panel.Children.Add(OptionSection("선택한 메시지와 실제 회신", view.Detail));
            view.Run = new Button { Content = "수신하고 읽기 전용 AI 답변 실행", Margin = new Thickness(0, 8, 8, 0) };
            view.Stop = new Button { Content = "AI 응답 중지", IsEnabled = false, Margin = new Thickness(0, 8, 8, 0) };
            view.Read = new Button { Content = "발신 AI에서 회신 읽기", Margin = new Thickness(0, 8, 0, 0) };
            var actions = new WrapPanel(); actions.Children.Add(view.Run); actions.Children.Add(view.Stop); actions.Children.Add(view.Read); panel.Children.Add(actions);
            panel.Children.Add(CreateThemedDescription("AI 응답은 수신 AI의 CLI·모델 설정으로 별도 빈 폴더에서 파일/도구 실행 없이 요청합니다. 사용량이 발생할 수 있습니다. 실제 회신이 저장된 뒤에만 완료로 표시합니다.", new Thickness(0, 12, 0, 0)));
            view.Dialog.Body.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            view.Send.Click += (s, a) => { try {
                var peer = view.Recipient.SelectedItem as ConversationAgentIdentity;
                var sent = ApproveMessageSend(view.Owner, peer, view.Draft.Text, true);
                RefreshMessageCenter(view); view.Dialog.Message.Text = sent.ToName + " 수신함에 저장했습니다. 아직 상대 AI를 실행하지 않았습니다.";
            } catch (Exception error) { view.Dialog.Message.Text = error.Message; } };
            view.Run.Tag = view; view.Run.Click += MessageReplyClicked;
            view.Stop.Click += (s, a) => { if (view.Execution != null) view.Execution.Cancel(); };
            view.Read.Click += (s, a) => { try {
                var row = view.Outbox.SelectedItem as MessageRow; if (row == null) throw new InvalidOperationException("보낸 메시지에서 회신을 선택하세요.");
                var reply = collaborationStore.Deliver(row.Item.Id, view.Owner.Id, view.Owner.Provider);
                ShowMessageDetail(view, reply); RefreshMessageCenter(view);
                view.Dialog.Message.Text = PublishMessageEvidence(view.Owner, reply, false) ? "실제 회신을 읽고 현재 AI 대화에 반영했습니다. 다음 요청에서 참고합니다." : "실제 회신은 저장되어 있습니다. 현재 대화 기록 반영은 다시 시도하세요.";
            } catch (Exception error) { view.Dialog.Message.Text = error.Message; } };
            view.Inbox.SelectionChanged += (s, a) => { var row = view.Inbox.SelectedItem as MessageRow; if (row != null) { view.Outbox.SelectedItem = null; ShowMessageDetail(view, row.Item); } };
            view.Outbox.SelectionChanged += (s, a) => { var row = view.Outbox.SelectedItem as MessageRow; if (row != null) { view.Inbox.SelectedItem = null; ShowMessageDetail(view, row.Item); } };
            view.Dialog.Closed += (s, a) => { if (view.Execution != null) view.Execution.Cancel(); };
            RefreshMessageCenter(view); return view;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApproveMessageSend
        /// desc         : 명시 승인된 실제 주소·본문만 기존 영속 Send에 넘긴다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 취소 무쓰기·닫힌/다른 팀/자기 주소는 Store에서 재검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private CollaborationHandoff ApproveMessageSend(ConversationRecord owner, ConversationAgentIdentity recipient, string body, bool approved)
        {
            Dispatcher.VerifyAccess(); if (!approved) return null;
            if (disposed || owner == null || recipient == null) throw new InvalidOperationException("실제 수신 AI를 선택하세요.");
            return collaborationStore.Send(owner.Id, owner.Provider, recipient.ConversationId, recipient.Provider, body);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MessageReplyClicked
        /// desc         : 실제 WPF 이벤트에서 UI 접근을 확인하고 모든 비동기 응답 오류를 처리한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 async delegate 대신 RoutedEventHandler 계약을 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100", Justification = "WPF RoutedEventHandler의 void 계약이며 전체 await 오류를 catch에서 처리한다.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD109", Justification = "WPF Click에서 DispatcherSynchronizationContext를 유지하며 진입과 await 오류 처리에서 Dispatcher.VerifyAccess로 UI 계약을 검증한다.")]
        private async void MessageReplyClicked(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess(); var button = sender as Button; var view = button == null ? null : button.Tag as MessageCenterView;
            if (view == null) return;
            try { await RunMessageReplyAsync(view); }
            catch (Exception error) { Dispatcher.VerifyAccess(); if (!disposed) view.Dialog.Message.Text = "메시지 응답 상태를 확인하세요: " + error.Message; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunMessageReplyAsync
        /// desc         : 사용자가 선택한 수신 메시지를 별도 응답 전용 runner에 전달하고 실제 회신만 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 대화 초안·수정안·편집 파일을 변경하지 않으며 창 닫기/Dispose 취소.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD109", Justification = "WPF dispatcher에서 호출하며 await 뒤 VerifyAccess로 같은 UI 계약을 확인한다.")]
        private async Task RunMessageReplyAsync(MessageCenterView view)
        {
            Dispatcher.VerifyAccess(); if (disposed || view.Execution != null) return;
            var row = view.Inbox.SelectedItem as MessageRow;
            if (row == null) { view.Dialog.Message.Text = "받은 메시지를 선택하세요."; return; }
            if (roomRuns.ContainsKey(view.Owner.Id) || collaboratingRooms.ContainsKey(view.Owner.Id)) { view.Dialog.Message.Text = "이 AI의 진행 중인 요청이 끝난 뒤 응답하세요."; return; }
            var cancellation = new CancellationTokenSource(); view.Execution = cancellation;
            view.Run.IsEnabled = view.Send.IsEnabled = false; view.Stop.IsEnabled = true;
            try
            {
                string executable, model, effort, speed;
                executablePaths.TryGetValue(view.Owner.Provider, out executable); models.TryGetValue(view.Owner.Provider, out model);
                efforts.TryGetValue(view.Owner.Provider, out effort); speeds.TryGetValue(view.Owner.Provider, out speed);
                if (selectedProvider == view.Owner.Provider) { executable = ExecutablePath.Text; model = ModelName.Text; }
                var settings = new ChatRequestContext(workspaceStore.LoadInstructions(), model ?? "", null, null, effort ?? "auto", speed ?? "standard");
                view.Dialog.Message.Text = "명시 수신한 메시지에 읽기 전용 CLI가 응답 중입니다…";
                var reply = await new CollaborationMessageRunner().ReplyAsync(collaborationStore, row.Item.Id, view.Owner.Id, view.Owner.Provider, executable, settings, true, cancellation.Token);
                Dispatcher.VerifyAccess();
                if (!disposed) { RefreshMessageCenter(view); ShowMessageDetail(view, reply); bool saved = PublishMessageEvidence(view.Owner, reply, true); view.Dialog.Message.Text = "실제 AI 회신을 발신자 회신함에 저장했습니다." + (saved ? " 현재 AI 대화에도 반영했습니다." : " 현재 대화 기록 반영은 다시 시도하세요."); }
            }
            catch (Exception error) { Dispatcher.VerifyAccess(); if (!disposed) view.Dialog.Message.Text = error is OperationCanceledException ? "AI 응답을 중지했습니다. 실제 회신은 생성하지 않았습니다." : "AI 메시지 응답 실패: " + error.Message; }
            finally { Dispatcher.VerifyAccess(); view.Execution = null; cancellation.Dispose(); if (!disposed) { view.Run.IsEnabled = view.Send.IsEnabled = true; view.Stop.IsEnabled = false; RefreshMessageCenter(view); } }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshMessageCenter
        /// desc         : 실제 저장소의 수신/회신 상태만 다시 읽고 선택 주소를 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 다른 대화 원문·첨부·내부 파일 경로를 표시하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshMessageCenter(MessageCenterView view)
        {
            Dispatcher.VerifyAccess();
            var inbox = view.Inbox.SelectedItem as MessageRow; var outbox = view.Outbox.SelectedItem as MessageRow;
            view.Inbox.ItemsSource = collaborationStore.ListInbox(view.Owner.Id, view.Owner.Provider).OrderByDescending(i => i.UpdatedAt).Select(i => new MessageRow { Item = i }).ToArray();
            view.Outbox.ItemsSource = collaborationStore.ListOutbox(view.Owner.Id, view.Owner.Provider).OrderByDescending(i => i.UpdatedAt).Select(i => new MessageRow { Item = i }).ToArray();
            if (inbox != null) view.Inbox.SelectedItem = view.Inbox.Items.Cast<MessageRow>().FirstOrDefault(i => i.Item.Id == inbox.Item.Id);
            if (outbox != null) view.Outbox.SelectedItem = view.Outbox.Items.Cast<MessageRow>().FirstOrDefault(i => i.Item.Id == outbox.Item.Id);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShowMessageDetail
        /// desc         : 선택한 실제 메시지와 저장된 회신을 읽기 전용으로 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 전송 대기/수신/회신/읽음 상태를 허위 완료와 구분한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ShowMessageDetail(MessageCenterView view, CollaborationHandoff item)
        { Dispatcher.VerifyAccess(); view.Detail.Text = item.FromName + " → " + item.ToName + " · " + item.State + "\n\n" + item.Text + (item.Reply.Length == 0 ? "" : "\n\n실제 회신:\n" + item.Reply); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PublishMessageEvidence
        /// desc         : 사용자가 실제 수신·읽은 메시지와 회신만 현재 대화 문맥에 중복 없이 남긴다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 고정 메시지 ID·원문 초안 보존으로 재읽기/저장 재시도 중복 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool PublishMessageEvidence(ConversationRecord owner, CollaborationHandoff item, bool recipient)
        {
            Dispatcher.VerifyAccess();
            if (disposed || !IsRoomShown(owner) || String.IsNullOrWhiteSpace(item.Reply)) return false;
            var chat = ActiveChat; chat.Draft = Instruction.Text;
            string[] roles = recipient ? new[] { "user", "assistant" } : new[] { "user" };
            string[] bodies = recipient ? new[] { "수신한 AI 메시지 · " + item.FromName + ":\n" + item.Text, item.Reply }
                : new[] { "사용자가 읽은 실제 AI 회신 · " + item.ToName + ":\n" + item.Reply };
            for (int index = 0; index < bodies.Length; index++)
            {
                string id;
                using (var hash = SHA256.Create()) id = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(item.Id + (recipient ? ":received:" : ":read:") + index))).Replace("-", "").Substring(0, 32).ToLowerInvariant();
                if (!chat.Messages.Any(m => m.Id == id))
                {
                    var message = new ChatMessage { Id = id, CreatedAt = item.UpdatedAt, Role = roles[index], Text = bodies[index] };
                    chat.Messages.Add(message);
                    QueueHistoryState order;
                    if (queuedHistory.TryGetValue(chat, out order)) order.Order[message] = order.NextSequence * 2 + 1;
                }
            }
            while (chat.Messages.Count > 100) chat.Messages.RemoveAt(0);
            QueueHistoryState history;
            if (queuedHistory.TryGetValue(chat, out history)) foreach (var removed in history.Order.Keys.Where(m => !chat.Messages.Contains(m)).ToArray()) history.Order.Remove(removed);
            return PersistConversation(owner, chat);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeMessageCenter
        /// desc         : 도구 창 수명이 끝나면 자기 메시지 CLI를 취소하고 메시지센터를 닫는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 취소 토큰은 실행 finally에서 해제한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeMessageCenter()
        {
            Dispatcher.VerifyAccess(); var view = messageCenter; messageCenter = null; if (view == null) return;
            if (view.Execution != null) view.Execution.Cancel(); view.Dialog.Close();
        }
    }
}
