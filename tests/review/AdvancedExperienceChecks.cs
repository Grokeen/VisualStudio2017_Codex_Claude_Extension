namespace CodexCode.WorkspaceReview
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Interop;
    using System.Windows.Threading;
    using VS2017.AIAssistant.Chat;
    using VS2017.AIAssistant.Editing;
    using VS2017.AIAssistant.Providers;
    using VS2017.AIAssistant.Views;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CollaborationProvider
    /// desc         : 실제 UI의 순차 요청·역할·결과 전달을 관찰하고 오류·취소를 주입한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-016 협업 호출 경계 fixture.
    /// update date  : 2026-10-18 CodexOrig : TASK-018 이미지 요구와 드롭다운·A~Z 이름에 맞춰 검증 갱신.
    ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class CollaborationProvider : IAiContextEditProvider
    {
        public readonly List<ChatRequestContext> Calls = new List<ChatRequestContext>();
        public int FailAt, HoldAt;
        public Action BeforeReturn;
        public TaskCompletionSource<EditProposal> Pending;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GenerateAsync
        /// desc         : 이전 Provider 계약도 같은 관찰 경로로 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 기본 컨텍스트 연결.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        public Task<EditProposal> GenerateAsync(string provider, string executable, DocumentSnapshot source, string instruction, CancellationToken token)
        { return GenerateWithContextAsync(provider, executable, source, instruction, new ChatRequestContext(), token); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GenerateWithContextAsync
        /// desc         : 고정 원본의 수정안을 반환하고 실제 호출 순서와 취소 토큰을 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 성공·CLI 실패·지연·저장 실패 재현.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        public Task<EditProposal> GenerateWithContextAsync(string provider, string executable, DocumentSnapshot source, string instruction, ChatRequestContext context, CancellationToken token)
        {
            Calls.Add(context);
            if (Calls.Count == FailAt) throw new InvalidOperationException("fixture primary CLI failure");
            if (Calls.Count == HoldAt) { Pending = new TaskCompletionSource<EditProposal>(); token.Register(() => Pending.TrySetCanceled()); return Pending.Task; }
            if (BeforeReturn != null) BeforeReturn();
            return Task.FromResult(EditProposal.Parse(source, "{\"summary\":\"real fixture review result\",\"edits\":[{\"old_text\":\"return 1;\",\"new_text\":\"return 2;\"}]}"));
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WorkspaceExperience
    /// desc         : 기존 사용자 화면 검사에 문서·모델·협업의 실제 이벤트와 오류 복구를 추가한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-016 비동기 입력과 다중 AI 호출 검증.
    ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static partial class WorkspaceExperience
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PrivateCall
        /// desc         : 공개 버튼과 같은 제품 연결부를 격리 WPF 프로세스에서 호출한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 비동기 첨부·설정 실패 검사 지원.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static object PrivateCall(AssistantControl control, string method, params object[] arguments)
        { return typeof(AssistantControl).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, arguments); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InspectModal
        /// desc         : 실제 제품 모달 화면의 컨트롤을 검사하고 항상 닫는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 팝업 슬라이더·이름 설정 사용자 경로 검사.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void InspectModal(Action open, Action<AssistantDialog> inspect)
        {
            Exception failure = null; var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) }; DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            timer.Tick += (s, a) => {
                var dialog = PresentationSource.CurrentSources.OfType<HwndSource>().Select(x => x.RootVisual).OfType<AssistantDialog>().FirstOrDefault(w => w.IsVisible);
                if (dialog == null && DateTime.UtcNow < deadline) return;
                timer.Stop();
                try { if (dialog == null) throw new TimeoutException("Modal did not open"); inspect(dialog); }
                catch (Exception error) { failure = error; }
                finally { if (dialog != null && dialog.IsVisible) dialog.Close(); }
            };
            timer.Start(); try { open(); } finally { timer.Stop(); }
            if (failure != null) throw failure;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckRuntimeUsageIsolation
        /// desc         : 연결 조회 실패와 늦은 Dispatcher 이벤트가 실제 최신 사용량을 지우지 않는지 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 최종 사용량 손실·이전 이벤트 역전 재현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckRuntimeUsageIsolation(AssistantControl control, string root)
        {
            var path = (TextBox)control.FindName("ExecutablePath"); string originalPath = path.Text;
            path.Text = Path.Combine(root, "missing-runtime.exe");
            var latest = new ProviderUsageSnapshot { InputTokens = 15, OutputTokens = 9, CachedInputTokens = 3, TotalTokens = 24, Source = "isolated actual-event fixture" };
            PrivateCall(control, "RuntimeUsageUpdated", latest);
            var metadata = (Task)PrivateCall(control, "RefreshRuntimeAsync");
            PumpUntil(() => metadata.IsCompleted, 3000); metadata.GetAwaiter().GetResult();
            var tip = (ToolTip)((Button)control.FindName("TokenUsageButton")).ToolTip;
            bool metadataPreserved = Convert.ToString(tip.Content).Contains("24 tokens") && Convert.ToString(tip.Content).Contains("조회하지 못했습니다");
            string metadataDisplay = Convert.ToString(tip.Content);
            path.Text = originalPath;
            var older = new ProviderUsageSnapshot { InputTokens = 13, OutputTokens = 7, TotalTokens = 20, Source = "isolated earlier event" };
            Task.Run(() => PrivateCall(control, "RuntimeUsageUpdated", older)).GetAwaiter().GetResult();
            PrivateCall(control, "RuntimeUsageUpdated", latest);
            bool drained = false;
            control.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => drained = true));
            PumpUntil(() => drained, 2000);
            bool newestPreserved = Convert.ToString(tip.Content).Contains("24 tokens") && !Convert.ToString(tip.Content).Contains("20 tokens");
            Check(metadataPreserved, "A metadata failure retains reported tokens with a separate connection error; out-of-order preserved=" + newestPreserved + "; metadata=" + metadataDisplay.Replace("\n", " | "));
            Check(newestPreserved, "A queued earlier usage event cannot overwrite the latest usage already applied on the UI thread");
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckAdvancedFeatures
        /// desc         : 문서·모델·협업·권한의 실제 WPF 사용자 경로와 저장 실패를 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-016 독립 UI 검토 및 오류 복구 회귀.
        ///                2026-10-18 CodexOrig : TASK-020 현재 모델·권한·색상·부채꼴 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckAdvancedFeatures(string output)
        {
            string oldSettings = Environment.GetEnvironmentVariable("VS2017_AI_SETTINGS_PATH"), oldWorkspace = Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH"), oldModels = Environment.GetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH");
            SynchronizationContext previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            string root = Path.Combine(output, "advanced"); Directory.CreateDirectory(root);
            AssistantControl control = null; Window owner = null; FileStream locked = null;
            try {
                Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", Path.Combine(root, "settings.json")); Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", Path.Combine(root, "workspace"));
                string cache = Path.Combine(root, "models.json");
                File.WriteAllText(cache, "{\"models\":[{\"slug\":\"fixture-deep\",\"display_name\":\"Fixture Deep\",\"visibility\":\"list\",\"priority\":0,\"supported_reasoning_levels\":[{\"effort\":\"low\"},{\"effort\":\"medium\"},{\"effort\":\"high\"},{\"effort\":\"xhigh\"},{\"effort\":\"max\"},{\"effort\":\"ultra\"}],\"default_reasoning_level\":\"medium\",\"additional_speed_tiers\":[\"fast\"]}]}");
                Environment.SetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH", cache);
                new AssistantSettingsStore().Save(new AssistantSettings { HideWelcome = true });
                var store = new MarkdownWorkspaceStore(); var main = store.Create("Codex"); var peer = store.Create("Codex");
                var collaboration = new ConversationCollaborationStore(store.RootPath); collaboration.EnsureAgent(main); collaboration.EnsureAgent(peer);
                collaboration.ConfigureNaming("Codex", "검증사용자");
                collaboration.UpdateAgent(main.Id, "Codex", collaboration.GetAgent(main.Id, "Codex").Name, "최종 검토", "fixture-team"); collaboration.UpdateAgent(peer.Id, "Codex", collaboration.GetAgent(peer.Id, "Codex").Name, "경계 조건 확인", "fixture-team");
                main.CollaboratorIds = new[] { peer.Id }; store.Save(main);
                var document = new SyntheticDocument(Path.Combine(root, "Original.cs")); var documents = new SyntheticDocuments { Document = document }; var provider = new CollaborationProvider();
                control = new AssistantControl(); control.Initialize(documents, provider); typeof(AssistantControl).GetField("scmLookupStarted", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(control, true); collaboration.ConfigureNaming("Codex", "검증사용자"); owner = new Window { Content = control, Width = 760, Height = 900 }; owner.Show(); owner.UpdateLayout();
                var tabs = (TabControl)control.FindName("ProviderChoice"); tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(t => Convert.ToString(t.Tag) == "Codex");
                var list = (ListBox)control.FindName("ConversationList"); list.SelectedItem = list.Items.OfType<ConversationRecord>().Single(r => r.Id == main.Id); owner.UpdateLayout();
                main = (ConversationRecord)list.SelectedItem;
                string coordinatorName = collaboration.GetAgent(main.Id, "Codex").Name, reviewerName = collaboration.GetAgent(peer.Id, "Codex").Name;
                Check(coordinatorName.EndsWith("코덱스A") && reviewerName.EndsWith("코덱스B") && Convert.ToString(((Button)control.FindName("AgentButton")).Content).Contains("AI 지정하여 대화"), "Conversation AI identities follow the user/provider/A-Z format behind one fixed selection button");
                InspectDropdown(control, "ModelOptionsButton", dialog => {
                    var model = Descendants(dialog).OfType<ComboBox>().Single(c => c.IsEditable); model.Text = "fixture-deep";
                    var slider = Descendants(dialog).OfType<Slider>().Single(); slider.Value = slider.Maximum;
                    var fast = Descendants(dialog).OfType<CheckBox>().Single(); fast.IsChecked = true;
                    Check(slider.Maximum == 6 && fast.IsEnabled, "Actual model popup exposes only advertised advanced levels and Fast support");
                    Capture(dialog, Path.Combine(output, "Model-options.png"));
                    Click(Descendants(dialog).OfType<Button>().Single(b => Convert.ToString(b.Content) == "✓ 선택 적용"));
                });
                Check(new AssistantSettingsStore().Load().CodexEffort == "ultra" && new AssistantSettingsStore().Load().CodexSpeed == "fast", "The real model popup saves provider-specific Ultra and Fast settings");
                var speedToggle = (ToggleButton)control.FindName("FastToggle"); speedToggle.IsChecked = false;
                InspectDropdown(control, "FastToggle", dialog => {
                    Check(speedToggle.IsChecked == true && Descendants(dialog).OfType<CheckBox>().Single().IsChecked == true && new AssistantSettingsStore().Load().CodexSpeed == "fast",
                        "Opening and dismissing the Fast dropdown preserves the saved speed and its visible toggle state");
                });
                ((TextBox)control.FindName("ModelName")).Text = "unknown-private"; owner.UpdateLayout();
                Check(!((ToggleButton)control.FindName("FastToggle")).IsEnabled && ((ToggleButton)control.FindName("FastToggle")).IsChecked != true, "Changing to an unadvertised model clears unavailable Fast and advanced effort");
                InspectDropdown(control, "ModelOptionsButton", dialog => {
                    Descendants(dialog).OfType<ComboBox>().Single(c => c.IsEditable).Text = "fixture-deep";
                    var slider = Descendants(dialog).OfType<Slider>().Single(); slider.Value = slider.Maximum;
                    Descendants(dialog).OfType<CheckBox>().Single().IsChecked = true;
                    Click(Descendants(dialog).OfType<Button>().Single(b => Convert.ToString(b.Content) == "✓ 선택 적용"));
                });
                string rtf = Path.Combine(root, "Reference.rtf"); File.WriteAllText(rtf, "{\\rtf1\\ansi DOCUMENT-BODY-014}");
                var data = new DataObject(DataFormats.FileDrop, new[] { rtf });
                Check((bool)PrivateCall(control, "PasteAttachmentData", data), "Clipboard file paste is intercepted as a document attachment");
                var attachmentList = (ListBox)control.FindName("AttachmentList"); PumpUntil(() => attachmentList.Items.Count == 1 && ((Button)control.FindName("GenerateButton")).IsEnabled, 5000);
                var attachment = (ChatAttachment)attachmentList.Items[0];
                Check(attachment.IsDocument && attachment.Text.Contains("DOCUMENT-BODY-014") && File.Exists(rtf), "Clipboard document extraction supplies body text and preserves the original file");
                var prompt = (TextBox)control.FindName("Instruction"); prompt.Text = "변경을 검토해 주세요"; Click((Button)control.FindName("GenerateButton"));
                PumpUntil(() => provider.Calls.Count == 2 && ((Button)control.FindName("GenerateButton")).IsEnabled, 5000);
                Check(provider.Calls[0].AgentContext.Contains(reviewerName) && provider.Calls[1].AgentContext.Contains(coordinatorName) && provider.Calls[1].AgentContext.Contains("real fixture review result"), "A selected peer is actually invoked before the coordinator receives that real review result");
                Check(provider.Calls.All(c => c.Speed == "fast" && c.ReasoningEffort == "ultra" && c.Attachments.Length == 1 && c.AgentContext.Length <= 8000), "Every collaboration call receives the selected model options and captured attachment within the context bound: " + string.Join(";", provider.Calls.Select(c => c.Model + ":" + c.ReasoningEffort + ":" + c.Speed + ":" + c.Attachments.Length + ":" + c.AgentContext.Length)));
                Check(document.ReplaceCount == 0 && document.SaveCount == 0 && File.ReadAllText(document.Path).Contains("return 1;"), "Neither peer nor coordinator proposals modify or save source before explicit approval");
                Check(collaboration.ListOutbox(main.Id, "Codex").Single().State == CollaborationHandoffState.Delivered && File.ReadAllText(collaboration.ReportPath).Contains(reviewerName), "Actual handoff states and results are persisted in the collaboration Markdown report");
                Check(store.LoadOpen("Codex").Single(r => r.Id == peer.Id).State == ConversationRunState.Completed && store.LoadOpen("Codex").Single(r => r.Id == peer.Id).HasUnread, "Peer completion is stored as unread in its own conversation");
                Check(store.LoadOpen("Codex").Single(r => r.Id == main.Id).CollaboratorIds.SequenceEqual(new[] { peer.Id }), "Selected collaborators survive a Markdown round trip");
                InspectModal(() => { var picker = OpenDropdown(control, "AgentButton"); Click(Descendants(picker).OfType<Button>().Single(b => Convert.ToString(b.Content).Contains("역할·협업 설정"))); }, dialog => {
                    var name = Descendants(dialog).OfType<TextBox>().Single(t => t.MaxLength == 40);
                    Check(name.IsReadOnly && name.Text == coordinatorName, "AI role settings keep the required automatic A-Z name read-only");
                    var role = Descendants(dialog).OfType<TextBox>().Single(t => t.MaxLength == 1000); role.Text = "새 검토 역할";
                    Capture(dialog, Path.Combine(output, "Agent-options.png"));
                    Click(Descendants(dialog).OfType<Button>().Single(b => Convert.ToString(b.Content) == "✓ AI 설정 저장"));
                });
                Check(collaboration.GetAgent(main.Id, "Codex").Name == coordinatorName && collaboration.GetAgent(main.Id, "Codex").Role == "새 검토 역할", "AI role settings save the role while preserving the automatic name and conversation title");
                var before = collaboration.GetAgent(main.Id, "Codex");
                using (var lockRecord = new FileStream(store.GetPath(main), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                    bool failed = false; try { PrivateCall(control, "SaveAgentConfiguration", main, before, before.Name, "new role", "new team", new string[0]); } catch (TargetInvocationException) { failed = true; }
                    Check(failed && collaboration.GetAgent(main.Id, "Codex").Name == before.Name && collaboration.GetAgent(main.Id, "Codex").Team == before.Team && main.CollaboratorIds.Contains(peer.Id), "A locked conversation file rolls AI identity, team and participant selection back together");
                }
                provider.FailAt = provider.Calls.Count + 1;
                locked = new FileStream(collaboration.ReportPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                prompt.Text = "실패 검사"; Click((Button)control.FindName("GenerateButton")); PumpUntil(() => ((Button)control.FindName("GenerateButton")).IsEnabled, 5000);
                Check(main.State == ConversationRunState.Failed && ((TextBlock)control.FindName("StatusLabel")).Text.Contains("fixture primary CLI failure"), "A report file lock cannot mask the primary collaboration CLI error");
                locked.Dispose(); locked = null;
                Check(collaboration.ListOutbox(main.Id, "Codex").Any(h => h.State == CollaborationHandoffState.Failed && h.ErrorText.Contains("fixture primary")), "Failed collaboration records a real failed handoff without fabricating an answer");
                provider.FailAt = 0; provider.HoldAt = provider.Calls.Count + 1; prompt.Text = "중지 검사"; Click((Button)control.FindName("GenerateButton")); PumpUntil(() => provider.Pending != null, 2000); Click((Button)control.FindName("StopButton")); PumpUntil(() => ((Button)control.FindName("GenerateButton")).IsEnabled, 3000);
                Check(main.State == ConversationRunState.Stopped && provider.Pending.Task.IsCanceled && collaboration.ListOutbox(main.Id, "Codex").Any(h => h.State == CollaborationHandoffState.Cancelled), "Stopping a peer call cancels the actual task and records stopped and cancelled states");
                provider.HoldAt = 0; bool didLock = false;
                provider.BeforeReturn = () => { if (!didLock) { didLock = true; locked = new FileStream(store.GetPath(peer), FileMode.Open, FileAccess.ReadWrite, FileShare.None); } };
                prompt.Text = "전달 후 저장 실패 검사"; Click((Button)control.FindName("GenerateButton")); PumpUntil(() => ((Button)control.FindName("GenerateButton")).IsEnabled, 5000);
                Check(main.State == ConversationRunState.Failed && !((TextBlock)control.FindName("StatusLabel")).Text.Contains("상태에서") && collaboration.ListOutbox(main.Id, "Codex").Count(h => h.State == CollaborationHandoffState.Delivered) == 2, "Post-delivery conversation save failure preserves delivered results and the original persistence error");
                locked.Dispose(); locked = null; provider.BeforeReturn = null;
                attachmentList.ItemsSource = null;
                var activeChat = (ChatSession)typeof(AssistantControl).GetProperty("ActiveChat", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(control, null); activeChat.Attachments.Clear(); attachmentList.ItemsSource = activeChat.Attachments;
                var batch = (Task)PrivateCall(control, "AttachFilesFromUiAsync", new object[] { new[] { rtf, Path.Combine(root, "missing.docx") } }); PumpUntil(() => batch.IsCompleted, 5000);
                Check(activeChat.Attachments.Count == 0 && ((TextBlock)control.FindName("StatusLabel")).Text.Contains("첨부 실패"), "A failed document batch leaves no partial attachments and re-enables the composer");
                var cancellation = (Task)PrivateCall(control, "AttachFilesFromUiAsync", new object[] { new[] { rtf } }); Click((Button)control.FindName("StopButton")); PumpUntil(() => cancellation.IsCompleted, 5000);
                Check(activeChat.Attachments.Count == 0 && ((Button)control.FindName("GenerateButton")).IsEnabled, "Stopping document extraction leaves the input usable without partial attachments");
                InspectDropdown(control, "PermissionsButton", dropdown => {
                    Click(Descendants(dropdown).OfType<Button>().Single(b => Convert.ToString(b.Content).EndsWith("작업 도구 허용")));
                });
                CheckGuideAndSettingsImages(control, output);
                CheckTooltipTheme(control, output);
                CheckFanMenuStability(control, owner);
                var permissionSettings = new AssistantSettingsStore().Load();
                Check(permissionSettings.AllowSourceControl && permissionSettings.AllowDatabaseSchema && !permissionSettings.AllowDeployment && !permissionSettings.AgentFullAccess, "The work tools preset permits SCM and DB while preserving proposal mode");
                InspectModal(() => { var dropdown = OpenDropdown(control, "PermissionsButton"); Click(Descendants(dropdown).OfType<Button>().Single(b => Convert.ToString(b.Content).Contains("DB·형상관리·배포 작업 열기"))); }, dialog => {
                    var permissions = Descendants(dialog).OfType<CheckBox>().Where(c => Convert.ToString(c.Content).EndsWith("허용")).ToArray();
                    Check(permissions.Length >= 2 && permissions.Single(c => Convert.ToString(c.Content).StartsWith("형상")).IsChecked == true && permissions.Single(c => Convert.ToString(c.Content).Contains("배포")).IsChecked != true, "Work tools preset enables the displayed SCM permission and leaves deployment unchecked");
                    Check(!Descendants(dialog).OfType<Button>().Single(b => Convert.ToString(b.Content) == "▶ 확인한 작업 실행").IsEnabled, "Full workspace permissions still require an exact plan and individual execution approval");
                });
                CheckRuntimeUsageIsolation(control, root);
                CheckAlphabetRouting(output);
            }
            finally {
                if (locked != null) locked.Dispose(); if (control != null) control.Dispose(); if (owner != null) owner.Close();
                Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", oldSettings); Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", oldWorkspace); Environment.SetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH", oldModels);
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }
    }
}
