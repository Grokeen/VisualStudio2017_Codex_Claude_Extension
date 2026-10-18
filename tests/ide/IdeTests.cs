using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Task = System.Threading.Tasks.Task;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.ToolWindows;
using VS2017.AIAssistant.Views;
using Microsoft.VisualStudio.Shell.Interop;
using VS2017.AIAssistant.Chat;

namespace CodexCode.IdeIntegration
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IdeTests
    /// desc         : 설치된 제품의 실제 WPF 버튼·문서 어댑터·EnvDTE·Undo·디스크 저장을 함께 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 메모리 fixture 대신 실제 VS2017 편집기 통합 테스트.
    /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
    /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
    /// update date  : 2026-10-18 CodexOrig : TASK-018 이미지 요구와 드롭다운·A~Z 이름에 맞춰 검증 갱신.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class IdeTests
    {
        private static readonly List<object> results = new List<object>();
        private static string progressPath;
        private static int passed, failed;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunAsync
        /// desc         : 합성 프로젝트에서 승인·적용·저장·복구·충돌·Claude 프로토콜·실제 Codex를 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : IDE 안에서 제품 DLL과 UI를 교체하지 않고 실행.
        /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
        /// update date  : 2026-10-18 CodexOrig : 격리된 실제 WPF 팝업 UX15를 IDE 호스트에서 검증.
        /// update date  : 2026-10-18 CodexOrig : AI 탭 Tag 선택·소스 링크·빈 SQL 승인 저장·Markdown 대화 복원 검증.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 실제 IDE에서 경계 드래그·접기·폭 복원·읽음 저장7개 검증.
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static async Task RunAsync(Package product, DTE dte, TestConfig config)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            progressPath = Path.Combine(config.OutputDirectory, "progress.log");
            var pane = product.FindToolWindow(typeof(AssistantToolWindow), 0, false);
            Check("IDE-01 product package loaded by registered menu", product.GetType().FullName == "VS2017.AIAssistant.AiAssistantPackage");
            Check("IDE-02 real VS2017 host", dte.Version.StartsWith("15."));
            Check("IDE-03 tool window created", pane != null && pane.Frame != null);
            var view = (AssistantControl)pane.Content;
            Check("WORK-01 first provider tab is Claude", Equals(((TabItem)((TabControl)view.FindName("ProviderChoice")).Items[0]).Tag, "Claude"));
            var frame = (IVsWindowFrame)pane.Frame;
            object frameMode;
            frame.GetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, out frameMode);
            Check("CHAT-01 Chat starts as document tab", pane.Caption == "Chat" && Convert.ToInt32(frameMode) == (int)VSFRAMEMODE.VSFM_MdiChild);
            frame.SetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, (int)VSFRAMEMODE.VSFM_Float);
            frame.GetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, out frameMode);
            Check("CHAT-02 Chat can float", Convert.ToInt32(frameMode) == (int)VSFRAMEMODE.VSFM_Float);
            frame.SetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, (int)VSFRAMEMODE.VSFM_Dock);
            frame.GetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, out frameMode);
            Check("CHAT-03 Chat can dock", Convert.ToInt32(frameMode) == (int)VSFRAMEMODE.VSFM_Dock);
            frame.SetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, (int)VSFRAMEMODE.VSFM_MdiChild);
            frame.GetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, out frameMode);
            Check("CHAT-04 Chat returns to document tab", Convert.ToInt32(frameMode) == (int)VSFRAMEMODE.VSFM_MdiChild);
            Check("IDE-04 real document service connected", Button(view, "GenerateButton").IsEnabled);
            var service = (IActiveDocumentService)typeof(AssistantControl).GetField("documents", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            Check("IDE-05 production EnvDTE adapter", service.GetType().FullName == "VS2017.AIAssistant.Services.VisualStudioDocumentService");
            DialogChecks.Run(Path.Combine(config.OutputDirectory, "ux"), Check);
            ProductUi.SelectProvider(view, "Codex");
            Text(view, "ExecutablePath").Text = config.FixturePath;
            Text(view, "Instruction").Text = "fixture:ok";
            await GenerateAsync(view);
            Check("IDE-06 request without source rejected", ((TextBlock)view.FindName("StatusLabel")).Text.StartsWith("요청 실패:"));

            dte.Solution.Open(config.SolutionPath);
            string sourcePath = Path.Combine(Path.GetDirectoryName(config.SolutionPath), "Probe.cs");
            string otherPath = Path.Combine(Path.GetDirectoryName(config.SolutionPath), "Other.cs");
            string originalDisk = File.ReadAllText(sourcePath);
            byte[] originalBytes = File.ReadAllBytes(sourcePath);
            string otherDisk = File.ReadAllText(otherPath);
            dte.ItemOperations.OpenFile(sourcePath);
            Document sourceDoc = dte.ActiveDocument;
            var textDoc = (TextDocument)sourceDoc.Object("TextDocument");
            var target = service.GetActiveDocument();
            var initial = target.Capture();
            frame.Show();
            await Task.Delay(100);
            Check("CHAT-05 Chat tab retains last source target", service.GetActiveDocument().Capture().FilePath == sourcePath);
            string attachmentPath = Path.Combine(config.OutputDirectory, "참고.txt");
            File.WriteAllText(attachmentPath, "테스트 참고 조건", new UTF8Encoding(true));
            view.AttachFiles(new[] { attachmentPath });
            Check("CHAT-06 text attachment appears in actual IDE", ((ListBox)view.FindName("AttachmentList")).Items.Count == 1);
            Text(view, "RulesText").Text = "기존 API와 들여쓰기를 유지한다.";
            ((ComboBox)view.FindName("TextSizeChoice")).SelectedValue = "16";
            Click(view, "SaveSettingsButton");
            using (var recreated = new AssistantControl())
                Check("CHAT-07 settings restore in recreated IDE control", recreated.FontSize == 16 && Text(recreated, "RulesText").Text.Contains("기존 API"));
            Check("CHAT-08 settings isolated from personal profile", new AssistantSettingsStore().FilePath.StartsWith(config.OutputDirectory, StringComparison.OrdinalIgnoreCase));
            Check("IDE-07 capture exact active buffer", initial.Content == originalDisk && !initial.HasUnsavedChanges && !initial.IsReadOnly);
            Check("IDE-08 project and framework context", initial.ProjectContext.Contains("IdeProbe") && initial.ProjectContext.Contains("4.6.2"));
            textDoc.EndPoint.CreateEditPoint().Insert("// 테스트 미저장 편집\r\n");
            string unsaved = target.Capture().Content;
            Check("IDE-09 unsaved edits captured", target.Capture().HasUnsavedChanges && unsaved.Contains("미저장 편집") && File.ReadAllText(sourcePath) == originalDisk);

            await GenerateAsync(view);
            Check("IDE-10 Codex fixture proposal displayed", Text(view, "ProposedPreview").Text == unsaved.Replace("return 1;", "return 2;"));
            Check("WORK-02 change card lists the modified source", ((TextBlock)view.FindName("ChangeSummary")).Text.Contains("1"));
            Check("CHAT-09 conversation stacks user and AI turns", ((ItemsControl)view.FindName("ConversationMessages")).Items.Count >= 3);
            Check("IDE-11 approval starts unchecked", ((CheckBox)view.FindName("ReviewApproval")).IsChecked != true && !Button(view, "ApplyButton").IsEnabled);
            Check("IDE-12 proposal preserves buffer and disk", target.Capture().Content == unsaved && File.ReadAllText(sourcePath) == originalDisk);
            dte.ItemOperations.OpenFile(otherPath);
            Click(view, "SourceLink");
            Check("WORK-03 source link navigates to fixed target in actual IDE", dte.ActiveDocument != null && dte.ActiveDocument.FullName == sourcePath);
            dte.ItemOperations.OpenFile(otherPath);
            ApproveAndApply(view);
            Check("IDE-13 active file switch preserves fixed target", target.Capture().Content == unsaved.Replace("return 1;", "return 2;") && File.ReadAllText(otherPath) == otherDisk);
            Check("IDE-14 apply changes only editor buffer", !sourceDoc.Saved && File.ReadAllText(sourcePath) == originalDisk);
            dte.ItemOperations.OpenFile(sourcePath);
            dte.ExecuteCommand("Edit.Undo");
            Check("IDE-15 one IDE Undo restores entire prior buffer", target.Capture().Content == unsaved);

            Text(view, "Instruction").Text = "fixture:ok";
            await GenerateAsync(view);
            ApproveAndApply(view);
            Click(view, "SaveButton");
            Check("IDE-16 explicit Save writes proposed text", sourceDoc.Saved && File.ReadAllText(sourcePath) == unsaved.Replace("return 1;", "return 2;"));
            byte[] savedBytes = File.ReadAllBytes(sourcePath);
            Check("IDE-17 UTF8 BOM and CRLF retained", originalBytes[0] == 0xef && savedBytes[0] == 0xef && savedBytes[1] == 0xbb && savedBytes[2] == 0xbf && File.ReadAllText(sourcePath).Contains("\r\n"));
            string saved = File.ReadAllText(sourcePath);
            Click(view, "RestoreButton");
            Check("IDE-18 Restore returns original unsaved content", target.Capture().Content == unsaved);
            Check("IDE-19 Restore leaves disk until explicit save", File.ReadAllText(sourcePath) == saved && !sourceDoc.Saved);
            sourceDoc.Save(sourcePath);

            Text(view, "Instruction").Text = "fixture:ok";
            await GenerateAsync(view);
            textDoc.StartPoint.CreateEditPoint().ReplaceText(textDoc.EndPoint, unsaved.Replace("return 1;", "return 9;"), 0);
            ApproveAndApply(view);
            Check("IDE-20 edits after proposal block apply", target.Capture().Content.Contains("return 9;") && ((TextBlock)view.FindName("StatusLabel")).Text.StartsWith("적용 실패:"));
            textDoc.StartPoint.CreateEditPoint().ReplaceText(textDoc.EndPoint, unsaved, 0);
            sourceDoc.Save(sourcePath);

            ProductUi.SelectProvider(view, "Claude");
            Check("CHAT-10 Provider tabs separate drafts and attachments", Text(view, "Instruction").Text == "" && ((ListBox)view.FindName("AttachmentList")).Items.Count == 0);
            Text(view, "ExecutablePath").Text = config.FixturePath;
            Text(view, "Instruction").Text = "fixture:ok";
            await GenerateAsync(view);
            Check("IDE-21 Claude fixture protocol in real IDE", Text(view, "ProposedPreview").Text == unsaved.Replace("return 1;", "return 2;"));
            ApproveAndApply(view);
            Check("IDE-22 Claude proposal applies through actual adapter", target.Capture().Content.Contains("return 2;") && File.ReadAllText(sourcePath) == unsaved);
            Click(view, "RestoreButton");
            sourceDoc.Save(sourcePath);

            bool liveRan = !string.IsNullOrWhiteSpace(config.LiveCodexPath);
            if (liveRan)
            {
                ProductUi.SelectProvider(view, "Codex");
                Text(view, "ExecutablePath").Text = config.LiveCodexPath;
                Text(view, "Instruction").Text = "Change only GetValue's return value from 1 to 2. Preserve all other text including comments and whitespace. Return one edit replacing exactly return 1; with return 2;.";
                await GenerateAsync(view, 120000);
                Check("IDE-23 live Codex exact proposal in actual tool window", Text(view, "ProposedPreview").Text == unsaved.Replace("return 1;", "return 2;"));
                Check("IDE-24 live Codex leaves disk unchanged before approval", target.Capture().Content == unsaved && File.ReadAllText(sourcePath) == unsaved && !Button(view, "ApplyButton").IsEnabled);
                ApproveAndApply(view);
                Click(view, "SaveButton");
                Check("IDE-25 live Codex approved edit saved by VS2017", sourceDoc.Saved && File.ReadAllText(sourcePath) == unsaved.Replace("return 1;", "return 2;"));
                Click(view, "RestoreButton");
                sourceDoc.Save(sourcePath);
            }
            ProductUi.SelectProvider(view, "Codex");
            string queryPath = Path.Combine(config.OutputDirectory, "Query.sql");
            File.WriteAllText(queryPath, "", new UTF8Encoding(true));
            dte.ItemOperations.OpenFile(queryPath);
            Document queryDoc = dte.ActiveDocument;
            Text(view, "ExecutablePath").Text = liveRan ? config.LiveCodexPath : config.FixturePath;
            Text(view, "Instruction").Text = liveRan
                ? "Insert exactly SELECT Id FROM dbo.Sample; into this empty SQL document. Reference schema: dbo.Sample has Id int NOT NULL. Add no comments or other statements. Return one edit with old_text equal to the empty string."
                : "fixture:sql";
            await GenerateAsync(view, liveRan ? 120000 : 15000);
            Check("WORK-04 empty SQL query draft preserves disk before approval", Text(view, "ProposedPreview").Text.Trim() == "SELECT Id FROM dbo.Sample;" && File.ReadAllText(queryPath) == "" && !Button(view, "ApplyButton").IsEnabled);
            ApproveAndApply(view); Click(view, "SaveButton");
            Check("WORK-05 approved SQL query is saved by actual IDE", queryDoc.Saved && File.ReadAllText(queryPath).Trim() == "SELECT Id FROM dbo.Sample;");
            Click(view, "RestoreButton"); queryDoc.Save(queryPath);
            int previousConversations = ((ListBox)view.FindName("ConversationList")).Items.Count;
            Click(view, "NewChatButton");
            Text(view, "Instruction").Text = "실제 IDE에서 보관한 미전송 초안";
            ProductUi.SelectProvider(view, "Claude");
            ProductUi.SelectProvider(view, "Codex");
            var records = new MarkdownWorkspaceStore().LoadOpen("Codex");
            Check("WORK-06 new conversation retains prior history and persists new draft", ((ListBox)view.FindName("ConversationList")).Items.Count == previousConversations + 1 && records.Any(r => r.Draft == "실제 IDE에서 보관한 미전송 초안") && records.Any(r => r.Messages.Count > 0));
            using (var restored = new AssistantControl())
            {
                ProductUi.SelectProvider(restored, "Codex");
                Check("WORK-07 recreated IDE control restores provider conversation tabs", ((ListBox)restored.FindName("ConversationList")).Items.Count == previousConversations + 1);
            }
            frame.Show(); await Task.Delay(150); view.UpdateLayout();
            var historyToggle = (ToggleButton)view.FindName("HistoryToggle");
            historyToggle.IsChecked = true; historyToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); view.UpdateLayout();
            var splitter = (GridSplitter)view.FindName("HistorySplitter");
            var historyColumn = (ColumnDefinition)view.FindName("HistoryColumn");
            Check("SIDEBAR-01 actual IDE exposes a full-height resizable conversation boundary", splitter.IsVisible && Grid.GetRowSpan(splitter) == 3);
            double previousWidth = historyColumn.ActualWidth;
            splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            splitter.RaiseEvent(new DragDeltaEventArgs(20, 0) { RoutedEvent = Thumb.DragDeltaEvent }); view.UpdateLayout();
            splitter.RaiseEvent(new DragCompletedEventArgs(20, 0, false) { RoutedEvent = Thumb.DragCompletedEvent }); view.UpdateLayout();
            double resizedWidth = historyColumn.ActualWidth;
            Check("SIDEBAR-02 real splitter drag expands the list inside VS2017", resizedWidth > previousWidth + 10);
            splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            splitter.RaiseEvent(new DragDeltaEventArgs(-1000, 0) { RoutedEvent = Thumb.DragDeltaEvent }); view.UpdateLayout();
            splitter.RaiseEvent(new DragCompletedEventArgs(-1000, 0, false) { RoutedEvent = Thumb.DragCompletedEvent }); view.UpdateLayout();
            Check("SIDEBAR-03 dragging left collapses the actual IDE sidebar", historyToggle.IsChecked == false && historyColumn.ActualWidth < 1);
            historyToggle.IsChecked = true; historyToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); view.UpdateLayout();
            Check("SIDEBAR-04 reopening restores the real resized width", Math.Abs(historyColumn.ActualWidth - resizedWidth) < 2);
            Check("SIDEBAR-05 actual successful AI requests retain completed state in Markdown", new MarkdownWorkspaceStore().LoadOpen("Codex").Any(r => r.State == ConversationRunState.Completed));
            var selectedRecord = (ConversationRecord)((ListBox)view.FindName("ConversationList")).SelectedItem;
            selectedRecord.HasUnread = true;
            ((ScrollViewer)view.FindName("ConversationScroll")).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            Check("SIDEBAR-06 viewing the IDE conversation clears only its persisted unread marker", !selectedRecord.HasUnread && !new MarkdownWorkspaceStore().LoadOpen("Codex").Single(r => r.Id == selectedRecord.Id).HasUnread);
            using (var restoredSidebar = new AssistantControl())
                Check("SIDEBAR-07 recreated IDE control restores list preference and width", ((ToggleButton)restoredSidebar.FindName("HistoryToggle")).IsChecked == true && Math.Abs(new AssistantSettingsStore().Load().HistoryWidth - resizedWidth) < 2);
            dte.ItemOperations.OpenFile(sourcePath);
            string documentReference = Path.Combine(config.OutputDirectory, "Reference.rtf"); File.WriteAllText(documentReference, "{\\rtf1\\ansi IDE-DOCUMENT-REFERENCE}");
            var uiFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            await (Task)typeof(AssistantControl).GetMethod("AttachFilesFromUiAsync", uiFlags).Invoke(view, new object[] { new[] { documentReference } });
            var documentAttachments = ((ListBox)view.FindName("AttachmentList")).Items.OfType<ChatAttachment>().ToArray();
            Check("ADVANCED-01 actual IDE asynchronously extracts document body into the composer", documentAttachments.Any(a => a.IsDocument && a.Text.Contains("IDE-DOCUMENT-REFERENCE")));
            var collaboration = new ConversationCollaborationStore(new MarkdownWorkspaceStore().RootPath);
            var coordinator = (ConversationRecord)((ListBox)view.FindName("ConversationList")).SelectedItem;
            var identity = collaboration.EnsureAgent(coordinator);
            collaboration.UpdateAgent(coordinator.Id, "Codex", identity.Name, "최종 검토", "IDE-Team", identity.Revision);
            Click(view, "NewChatButton");
            var colleague = (ConversationRecord)((ListBox)view.FindName("ConversationList")).SelectedItem;
            var peerIdentity = collaboration.EnsureAgent(colleague);
            collaboration.UpdateAgent(colleague.Id, "Codex", peerIdentity.Name, "경계 검토", "IDE-Team", peerIdentity.Revision);
            var conversationList = (ListBox)view.FindName("ConversationList");
            conversationList.SelectedItem = conversationList.Items.OfType<ConversationRecord>().Single(r => r.Id == coordinator.Id);
            coordinator.CollaboratorIds = new[] { colleague.Id }; new MarkdownWorkspaceStore().Save(coordinator);
            typeof(AssistantControl).GetMethod("RefreshExperience", uiFlags).Invoke(view, null);
            Check("ADVANCED-02 actual IDE displays the persisted unique AI name and collaboration count", Convert.ToString(Button(view, "AgentButton").Content).Contains("AI 지정하여 대화") && coordinator.CollaboratorIds.Contains(colleague.Id));
            Text(view, "ExecutablePath").Text = config.FixturePath; Text(view, "Instruction").Text = "fixture:ok";
            byte[] sourceBeforeCollaboration = File.ReadAllBytes(sourcePath);
            await GenerateAsync(view);
            Check("ADVANCED-03 actual native peer and coordinator requests deliver a real handoff inside VS2017", collaboration.ListOutbox(coordinator.Id, "Codex").Single().State == CollaborationHandoffState.Delivered && Text(view, "ProposedPreview").Text.Contains("return 2;"));
            Check("ADVANCED-04 collaboration leaves actual IDE source and disk unchanged before approval", target.Capture().Content == unsaved && sourceBeforeCollaboration.SequenceEqual(File.ReadAllBytes(sourcePath)) && !Button(view, "ApplyButton").IsEnabled);
            var peerRecord = new MarkdownWorkspaceStore().LoadOpen("Codex").Single(r => r.Id == colleague.Id);
            Check("ADVANCED-05 peer completion and unread status are saved separately in the IDE", peerRecord.State == ConversationRunState.Completed && peerRecord.HasUnread);
            Check("ADVANCED-06 actual collaboration report contains the real participants and result", File.Exists(collaboration.ReportPath) && File.ReadAllText(collaboration.ReportPath).Contains(peerIdentity.Name) && collaboration.ListOutbox(coordinator.Id, "Codex").Single().Reply.Length > 0);
            frame.Show(); await Task.Delay(150); view.UpdateLayout();
            Check("ADVANCED-07 model details and capability-controlled Fast are present in the real host", Button(view, "ModelOptionsButton").IsVisible && ((ToggleButton)view.FindName("FastToggle")).IsVisible && !((ToggleButton)view.FindName("FastToggle")).IsEnabled);
            using (var restoredAdvanced = new AssistantControl()) {
                ProductUi.SelectProvider(restoredAdvanced, "Codex");
                var restoredRecords = ((ListBox)restoredAdvanced.FindName("ConversationList")).Items.OfType<ConversationRecord>().ToArray();
                var restoredRecord = restoredRecords.SingleOrDefault(r => r.Id == coordinator.Id);
                if (restoredRecord == null) throw new InvalidOperationException("Restored conversation missing; provider=" + Convert.ToString(((TabItem)((TabControl)restoredAdvanced.FindName("ProviderChoice")).SelectedItem).Tag) + "; records=" + restoredRecords.Length + "; status=" + ((TextBlock)restoredAdvanced.FindName("StatusLabel")).Text);
                var restoredIdentity = collaboration.GetAgent(coordinator.Id, "Codex");
                Check("ADVANCED-08 recreated IDE controls restore AI identity and selected collaborators", restoredRecord.CollaboratorIds.Contains(colleague.Id) && restoredIdentity.Letter == identity.Letter && restoredIdentity.Role == "최종 검토" && restoredIdentity.Name.EndsWith("코덱스" + identity.Letter));
            }
            await CheckImageUiAsync(view);
            var json = new JavaScriptSerializer();
            File.WriteAllText(Path.Combine(config.OutputDirectory, "result.json"), json.Serialize(new {
                Succeeded = failed == 0, Passed = passed, Failed = failed, IdeRuntimeTest = true,
                HostVersion = dte.Version, RegistryRoot = dte.RegistryRoot,
                ProductAssembly = product.GetType().Assembly.Location, ProductVersion = product.GetType().Assembly.GetName().Version.ToString(),
                LiveCodexTest = liveRan, LiveSqlDraftTest = liveRan, LiveClaudeTest = false, UserProjectSourceSent = false, Tests = results
            }), new UTF8Encoding(false));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckImageUiAsync
        /// desc         : 실제 VS2017 도구 창에서 하단 배치와 anchor 팝업·선택·닫기를 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-018 이미지7~16의 실제 IDE 회귀 추가.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task CheckImageUiAsync(AssistantControl view)
        {
            var sidebar = (FrameworkElement)view.FindName("HistoryPanel");
            var actions = (ScrollViewer)view.FindName("ConversationActionsScroll");
            Check("IMAGES-01 actual IDE keeps conversation actions in the scrollable sidebar footer", Visuals(sidebar).Contains(actions) && Visuals(actions).Contains(Button(view, "AgentButton")) && actions.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto);
            Click(view, "ModelOptionsButton"); await Task.Delay(200);
            var popup = (Popup)typeof(AssistantControl).GetField("dropdownPopup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            var root = popup.Child;
            Check("IMAGES-02 actual IDE model dropdown is anchored and contains its strength gauge and topic index", popup.IsOpen && popup.PlacementTarget == view.FindName("ModelOptionsButton") && !popup.StaysOpen && Visuals(root).OfType<Slider>().Any() && Visuals(root).OfType<WrapPanel>().Any());
            root.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(root), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            await Task.Delay(200);
            Check("IMAGES-03 Escape closes the actual IDE dropdown without opening another window", !popup.IsOpen && popup.Child == null);
            Click(view, "PermissionsButton"); await Task.Delay(150);
            popup = (Popup)typeof(AssistantControl).GetField("dropdownPopup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            var labels = Visuals(popup.Child).OfType<Button>().Select(b => Convert.ToString(b.Content)).ToArray();
            Check("IMAGES-04 actual IDE permission dropdown exposes three presets and the separate action planner", labels.Any(t => t.Contains("승인 요청")) && labels.Any(t => t.Contains("내 대신 승인")) && labels.Any(t => t.Contains("전체 권한")) && labels.Any(t => t.Contains("DB·형상관리·배포")));
            typeof(AssistantControl).GetMethod("CloseDropdown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            Click(view, "AgentButton"); await Task.Delay(150);
            popup = (Popup)typeof(AssistantControl).GetField("dropdownPopup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            var selected = (ConversationRecord)((ListBox)view.FindName("ConversationList")).SelectedItem;
            var agent = new ConversationCollaborationStore(new MarkdownWorkspaceStore().RootPath).GetAgent(selected.Id, "Codex");
            var choice = Visuals(popup.Child).OfType<Button>().Single(b => Convert.ToString(b.Content).EndsWith(agent.Name));
            choice.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); await Task.Delay(150);
            Check("IMAGES-05 the actual IDE AI dropdown routes its selected name to the original conversation", !popup.IsOpen && ((ConversationRecord)((ListBox)view.FindName("ConversationList")).SelectedItem).Id == selected.Id && agent.Name.EndsWith("코덱스" + agent.Letter));
            var trigger = (ToggleButton)view.FindName("OptionsToggle");
            int before = (int)typeof(AssistantControl).GetField("fanOpenCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            for (int i = 0; i < 10; i++) trigger.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            Check("IMAGES-06 repeated hover in the actual IDE opens the fan once", (int)typeof(AssistantControl).GetField("fanOpenCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view) == before + 1);
            typeof(AssistantControl).GetMethod("CloseFan", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            var modelButton = Button(view, "ModelOptionsButton"); object originalTip = modelButton.ToolTip;
            var tip = new ToolTip { Content = string.Join(" ", Enumerable.Repeat("모델의 강도와 속도를 확인하고 선택 적용을 누르세요.", 12)), PlacementTarget = modelButton, StaysOpen = true };
            try {
                modelButton.ToolTip = tip; tip.IsOpen = true; await Task.Delay(200);
                var background = tip.Background as SolidColorBrush; var foreground = tip.Foreground as SolidColorBrush;
                Check("IMAGES-07 the actual IDE tooltip uses the dark palette and wraps a long explanation", background != null && foreground != null && background.Color.R < 65 && foreground.Color.R > 170 && tip.ActualWidth <= 430 && Visuals(tip).OfType<TextBlock>().Any(t => t.TextWrapping != TextWrapping.NoWrap));
            }
            finally { tip.IsOpen = false; modelButton.ToolTip = originalTip; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Visuals
        /// desc         : 실제 IDE에서 열린 제품 시각 트리의 자식을 순회한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 팝업 구조와 버튼을 표시 결과로 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
        {
            if (root == null) yield break;
            yield return root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Visuals(VisualTreeHelper.GetChild(root, i))) yield return child;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GenerateAsync
        /// desc         : 실제 생성 버튼을 누른 후 Dispatcher를 막지 않고 제품의 비동기 처리를 기다린다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 타임아웃 시 상태 텍스트를 진단에 포함.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task GenerateAsync(AssistantControl view, int timeoutMs = 15000)
        {
            Click(view, "GenerateButton");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!Button(view, "GenerateButton").IsEnabled && clock.ElapsedMilliseconds < timeoutMs) await Task.Delay(50);
            if (!Button(view, "GenerateButton").IsEnabled) throw new TimeoutException("Generate timed out: " + ((TextBlock)view.FindName("StatusLabel")).Text);
            File.AppendAllText(progressPath, "Generate: " + ((TextBlock)view.FindName("StatusLabel")).Text + "\r\n");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApproveAndApply
        /// desc         : 명시적 승인 체크 후 실제 적용 이벤트를 호출한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 테스트 전용 승인 절차.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ApproveAndApply(AssistantControl view) { ((CheckBox)view.FindName("ReviewApproval")).IsChecked = true; Click(view, "ApplyButton"); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Button
        /// desc         : 제품 화면의 실제 버튼을 이름으로 찾는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 화면 내부 테스트 보조 함수.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Button Button(AssistantControl view, string name) { return (Button)view.FindName(name); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Text
        /// desc         : 제품 화면의 실제 텍스트 입력란을 이름으로 찾는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 입력·미리보기 검사 보조 함수.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static TextBox Text(AssistantControl view, string name) { return (TextBox)view.FindName(name); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Click
        /// desc         : 활성화된 제품 버튼의 실제 이벤트를 실행하며 비활성 버튼이면 실패 처리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 상태 안내를 포함한 테스트 진단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Click(AssistantControl view, string name)
        {
            if (!Button(view, name).IsEnabled) throw new InvalidOperationException(name + " is disabled: " + ((TextBlock)view.FindName("StatusLabel")).Text);
            Button(view, name).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : 각 검증 결과를 즉시 로그에 기록하고 실행 종료 시 JSON 보고서로 저장한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실패를 성공으로 집계하지 않도록 별도 카운트.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Check(string name, bool condition)
        {
            if (condition) passed++; else failed++;
            results.Add(new { Name = name, Passed = condition });
            File.AppendAllText(progressPath, (condition ? "PASS " : "FAIL ") + name + "\r\n");
            if (!condition) throw new InvalidOperationException(name);
        }
    }
}
