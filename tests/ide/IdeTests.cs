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
using VS2017.AIAssistant.Services;

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
    ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
    ///                2026-10-19 CodexOrig : TASK-025 실제 IDE의 Markdown·테마·좁은 목록 검사 분리 연결.
    ///                2026-10-20 CodexOrig : TASK-054 제출 전 검증과 FIFO 입력의 실제 대화 역할·초안 보존 확인.
    ///                2026-10-20 CodexOrig : 실제 설치된 PDF의 서비스 경로와 선택 VSIX 해시 회귀 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static partial class IdeTests
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
        ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
        ///                2026-10-18 CodexOrig : 실제 IDE의 진행 카드와 소유 WPF Window 연결 상태를 관찰 기록.
        ///                2026-10-19 CodexOrig : 새 Markdown 링크·공통 테마·목록 overlay·돋보기 실제 호스트 검증.
        ///                2026-10-20 CodexOrig : TASK068 미승인 수정안을 보존한 SQL 대기를 명시적 건너뛰기로 실행.
        ///                2026-10-23 CodexOrig : TASK072 읽음 검사 전후의 실제 선택·열린 기록·읽기 경고를 진단하며 기존 조건은 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static async Task RunAsync(Package product, DTE dte, TestConfig config)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            progressPath = Path.Combine(config.OutputDirectory, "progress.log");
            string loadedHash;
            using (var input = File.OpenRead(product.GetType().Assembly.Location))
            using (var sha = System.Security.Cryptography.SHA256.Create()) loadedHash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
            Check("IDE-00 actual loaded product matches selected VSIX SHA256", loadedHash == config.ExpectedProductSha256);
            var installedGuide = new CliGuideService();
            string installedGuidePath = installedGuide.GetPdfPath();
            string installedGuideHash;
            using (var input = File.OpenRead(installedGuidePath))
            using (var sha = System.Security.Cryptography.SHA256.Create()) installedGuideHash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
            Check("GUIDE-01 actual installed PDF resolves through product service and matches selected VSIX",
                Path.GetFileName(installedGuidePath) == CliGuideService.PdfFileName && installedGuideHash == config.ExpectedGuideSha256);
            File.WriteAllText(Path.Combine(config.OutputDirectory, "installed-guide.json"), new JavaScriptSerializer().Serialize(new { Path = installedGuidePath, Sha256 = installedGuideHash, ExpectedSha256 = config.ExpectedGuideSha256, PdfApplicationLaunched = false }));
            var pane = product.FindToolWindow(typeof(AssistantToolWindow), 0, false);
            Check("IDE-01 product package loaded by registered menu", product.GetType().FullName == "VS2017.AIAssistant.AiAssistantPackage");
            Check("IDE-02 actual configured Visual Studio host " + config.ExpectedIdeMajor, dte.Version.StartsWith(config.ExpectedIdeMajor + "."));
            Check("IDE-03 tool window created", pane != null && pane.Frame != null);
            var view = (AssistantControl)pane.Content;
            Check("WORK-01 first provider tab is Claude", Equals(((TabItem)((TabControl)view.FindName("ProviderChoice")).Items[0]).Tag, "Claude"));
            var frame = (IVsWindowFrame)pane.Frame;
            object frameMode;
            frame.GetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, out frameMode);
            File.AppendAllText(progressPath, "Actual caption=" + pane.Caption + "; FrameMode=" + frameMode + "\r\n");
            Check("CHAT-01 IDE caption removes Chat and retains document tab", pane.Caption == "Claude · Codex" && Convert.ToInt32(frameMode) == (int)VSFRAMEMODE.VSFM_MdiChild);
            frameMode = await ChangeFrameModeAsync(frame, VSFRAMEMODE.VSFM_Float, dte);
            Check("CHAT-02 Chat can float", Convert.ToInt32(frameMode) == (int)VSFRAMEMODE.VSFM_Float);
            frameMode = await ChangeFrameModeAsync(frame, VSFRAMEMODE.VSFM_Dock, dte);
            Check("CHAT-03 Chat can dock", Convert.ToInt32(frameMode) == (int)VSFRAMEMODE.VSFM_Dock);
            frameMode = await ChangeFrameModeAsync(frame, VSFRAMEMODE.VSFM_MdiChild, dte);
            Check("CHAT-04 Chat returns to document tab", Convert.ToInt32(frameMode) == (int)VSFRAMEMODE.VSFM_MdiChild);
            Check("IDE-04 real document service connected", Button(view, "GenerateButton").IsEnabled);
            var mainWindow = Application.Current == null ? null : Application.Current.MainWindow;
            var licenseTexts = mainWindow == null ? new string[0] : Visuals(mainWindow).OfType<TextBlock>().Where(block => block.IsVisible).Select(block => block.Text).ToArray();
            bool licensePrompt = licenseTexts.Any(text => System.Text.RegularExpressions.Regex.IsMatch(text ?? "", "evaluation period|trial period|license (?:has|is|expired|stale)|평가 기간|라이선스.*(?:만료|갱신)|평가판.*(?:남|만료)", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            File.WriteAllText(Path.Combine(config.OutputDirectory, "license-observation.json"), new JavaScriptSerializer().Serialize(new { IdeMajor = config.ExpectedIdeMajor, MainWindowInspected = licenseTexts.Length > 0, BlockingLicenseTextObserved = licensePrompt, CheckedAt = DateTimeOffset.Now, Scope = "현재 테스트 인스턴스의 표시된 WPF 텍스트 관찰. 계정의 라이선스 유효성이나 미래 배너 부재를 인증하지 않음." }));
            if (licenseTexts.Length > 0) Check("LICENSE-01 no licensing warning observed in actual IDE session", !licensePrompt);
            var service = (IActiveDocumentService)typeof(AssistantControl).GetField("documents", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            Check("IDE-05 production EnvDTE adapter", service.GetType().FullName == "VS2017.AIAssistant.Services.VisualStudioDocumentService");
            DialogChecks.Run(Path.Combine(config.OutputDirectory, "ux"), Check);
            ProductUi.SelectProvider(view, "Codex");
            Text(view, "ExecutablePath").Text = config.FixturePath;
            Text(view, "Instruction").Text = "fixture:ok";
            await GenerateAsync(view);
            Check("IDE-06 request without source rejected before enqueue and preserves draft",
                ((TextBlock)view.FindName("StatusLabel")).Text == "요청 대기 실패: 프로젝트 소스 파일을 선택하거나 저장된 솔루션을 열어 주세요."
                && Text(view, "Instruction").Text == "fixture:ok" && !Button(view, "StopButton").IsEnabled);

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
            int selectionOffset = originalDisk.IndexOf("return 1;", StringComparison.Ordinal);
            int selectionLine = 1 + originalDisk.Substring(0, selectionOffset).Count(character => character == '\n');
            int selectionColumn = selectionOffset - originalDisk.LastIndexOf('\n', selectionOffset);
            TextSelection selection = textDoc.Selection;
            selection.MoveToLineAndOffset(selectionLine, selectionColumn, false);
            selection.MoveToLineAndOffset(selectionLine, selectionColumn + 9, true);
            var selected = target.Capture();
            Check("SELECT-01 actual IDE selected text separately captured", selected.HasSelection && selected.Selection.Text == "return 1;" && selected.Selection.StartOffset == selectionOffset);
            Check("SELECT-02 selection retains complete source and disk conflict hash", selected.Content == initial.Content && selected.DiskVersion == initial.DiskVersion);
            selection.MoveToLineAndOffset(selectionLine, selectionColumn + 9, false);
            selection.MoveToLineAndOffset(selectionLine, selectionColumn, true);
            Check("SELECT-03 reversed actual IDE selection normalized", target.Capture().Selection.Text == "return 1;");
            selection.MoveToLineAndOffset(1, 1, false);
            Check("SELECT-04 cleared selection leaves full source context", !target.Capture().HasSelection && target.Capture().Content == initial.Content);
            var directDocuments = service as IFullAccessDocumentService;
            Check("FULL-01 actual saved editor context supports direct execution", directDocuments != null && directDocuments.CaptureFullAccessContext().FilePath == sourcePath);
            dte.ItemOperations.OpenFile(otherPath); Document otherDoc = dte.ActiveDocument;
            ((TextDocument)otherDoc.Object("TextDocument")).EndPoint.CreateEditPoint().Insert("// isolated unsaved buffer\r\n");
            sourceDoc.Activate(); bool unsavedBlocked = false;
            try { directDocuments.CaptureFullAccessContext(); } catch (InvalidOperationException) { unsavedBlocked = true; }
            Check("FULL-02 another open unsaved file blocks native direct writes", unsavedBlocked);
            bool unsavedScopeBlocked = false;
            try { using (((IExternalEditingDocumentService)service).BeginExternalEditing(null)) { } } catch (InvalidOperationException) { unsavedScopeBlocked = true; }
            Check("SYNC-01 unsaved open buffer blocks external editing scope", unsavedScopeBlocked && !otherDoc.Saved);
            otherDoc.Close(vsSaveChanges.vsSaveChangesNo); sourceDoc.Activate();
            var editingService = (IExternalEditingDocumentService)service;
            var solutionService = (IVsSolution)Package.GetGlobalService(typeof(SVsSolution));
            IVsHierarchy diagnosticHierarchy; solutionService.GetProjectOfUniqueName(dte.Solution.Projects.Item(1).UniqueName, out diagnosticHierarchy);
            var diagnosticPersist = diagnosticHierarchy as IVsPersistHierarchyItem2;
            int diagnosticDirty = -1, diagnosticReloadable = -1;
            int diagnosticDirtyHr = diagnosticPersist == null ? -1 : diagnosticPersist.IsItemDirty(Microsoft.VisualStudio.VSConstants.VSITEMID_ROOT, IntPtr.Zero, out diagnosticDirty);
            int diagnosticReloadHr = diagnosticPersist == null ? -1 : diagnosticPersist.IsItemReloadable(Microsoft.VisualStudio.VSConstants.VSITEMID_ROOT, out diagnosticReloadable);
            File.WriteAllText(Path.Combine(config.OutputDirectory, "project-interface-diagnostic.json"), new JavaScriptSerializer().Serialize(new { HasPersist2 = diagnosticPersist != null, DirtyHr = diagnosticDirtyHr, Dirty = diagnosticDirty, ReloadHr = diagnosticReloadHr, Reloadable = diagnosticReloadable, Saved = dte.Solution.Projects.Item(1).Saved, HasPersistFile = diagnosticHierarchy is IPersistFileFormat, HasPersistDoc = diagnosticHierarchy is IVsPersistDocData }));
            string changedDisk = originalDisk + "\r\n// isolated native reload check\r\n";
            using (var editing = editingService.BeginExternalEditing(null)) {
                Check("SYNC-02 saved editor temporarily protects user edits", target.Capture().IsReadOnly);
                File.WriteAllText(sourcePath, changedDisk, new UTF8Encoding(true));
                await Task.Delay(250);
                Check("SYNC-03 external change does not replace protected buffer mid-run", target.Capture().Content == originalDisk && sourceDoc.Saved);
            }
            Check("SYNC-04 completed external change reloads without modal or dirty buffer", target.Capture().Content == changedDisk && sourceDoc.Saved);
            Check("SYNC-05 temporary editor protection restored", !target.Capture().IsReadOnly);
            using (var editing = editingService.BeginExternalEditing(null)) File.WriteAllBytes(sourcePath, originalBytes);
            Check("SYNC-06 baseline restored through same synchronization path", target.Capture().Content == originalDisk && sourceDoc.Saved);
            await CheckSharedRoomEditingAsync(view, editingService, target, sourceDoc, sourcePath, originalBytes, originalDisk, changedDisk);
            bool closeStopped = false;
            using (var editing = editingService.BeginExternalEditing(() => closeStopped = true)) {
                dte.ItemOperations.OpenFile(otherPath); var reopened = dte.ActiveDocument;
                Check("SYNC-07 newly opened editor protected during native run", service.GetActiveDocument().Capture().IsReadOnly);
                reopened.Close(vsSaveChanges.vsSaveChangesNo); dte.ItemOperations.OpenFile(otherPath);
                Check("SYNC-08 close requests cancellation and reopened editor gets new protection", closeStopped && service.GetActiveDocument().Capture().IsReadOnly);
            }
            dte.ActiveDocument.Close(vsSaveChanges.vsSaveChangesNo); sourceDoc.Activate();
            Check("SYNC-09 closing and reopening restores original editor state", !target.Capture().IsReadOnly && sourceDoc.Saved);
            frame.Show();
            await Task.Delay(100);
            Check("CHAT-05 Chat tab retains last source target", service.GetActiveDocument().Capture().FilePath == sourcePath);
            ProductUi.SelectProvider(view, "Codex");
            Text(view, "ExecutablePath").Text = config.FixturePath;
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
            Text(view, "Instruction").Text = "fixture:ok";

            await GenerateAsync(view);
            Check("IDE-10 Codex fixture proposal displayed", Text(view, "ProposedPreview").Text == unsaved.Replace("return 1;", "return 2;"));
            Check("WORK-02 change card lists the modified source", ((TextBlock)view.FindName("ChangeSummary")).Text.Contains("1"));
            var submittedTurns = ((ItemsControl)view.FindName("ConversationMessages")).Items.OfType<ChatMessage>().ToArray();
            Check("CHAT-09 conversation stacks submitted user and AI turns",
                submittedTurns.Length >= 2 && submittedTurns.Any(message => message.Role == "user" &&
                    message.Text.StartsWith("fixture:ok\n첨부 본문 포함 요청 · 1개\n", StringComparison.Ordinal) &&
                    message.Text.Contains("참고.txt — 본문 9자 · UTF-8 23바이트") &&
                    message.Text.Contains("첨부는 다음 요청에도 유지됩니다.") && !message.Text.Contains("테스트 참고 조건"))
                && submittedTurns.Any(message => message.Role == "assistant" && message.Text == "테스트 수정안"));
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
            if (!liveRan)
            {
                string retainedPreview = Text(view, "ProposedPreview").Text;
                Click(view, "GenerateButton");
                Check("ROOM-QUEUE-01 unapproved proposal is retained before explicit skip", Text(view, "ProposedPreview").Text == retainedPreview && Button(view, "DiscardProposalButton").IsEnabled && File.ReadAllText(queryPath) == "");
                Click(view, "DiscardProposalButton");
                await GenerateAsync(view, 15000, false);
                Check("ROOM-QUEUE-02 explicit skip runs queued SQL without applying it", Text(view, "ProposedPreview").Text.Trim() == "SELECT Id FROM dbo.Sample;" && File.ReadAllText(queryPath) == "");
            }
            else await GenerateAsync(view, 120000);
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
            WriteUnreadDiagnostic(view, selectedRecord, config.OutputDirectory, "before");
            selectedRecord.HasUnread = true;
            ((ScrollViewer)view.FindName("ConversationScroll")).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            var unreadStore = new MarkdownWorkspaceStore();
            var unreadRecords = unreadStore.LoadOpen("Codex");
            WriteUnreadDiagnostic(view, selectedRecord, config.OutputDirectory, "after", unreadStore, unreadRecords);
            Check("SIDEBAR-06 viewing the IDE conversation clears only its persisted unread marker", !selectedRecord.HasUnread && !unreadRecords.Single(r => r.Id == selectedRecord.Id).HasUnread);
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
            Check("ADVANCED-07 model details expose Fast and usage through one visible control", Button(view, "ModelOptionsButton").IsVisible && ((ToggleButton)view.FindName("FastToggle")).Visibility == Visibility.Collapsed && Button(view, "TokenUsageButton").Visibility == Visibility.Collapsed && Button(view, "ModelOptionsButton").ToolTip is ToolTip);
            using (var restoredAdvanced = new AssistantControl()) {
                ProductUi.SelectProvider(restoredAdvanced, "Codex");
                var restoredRecords = ((ListBox)restoredAdvanced.FindName("ConversationList")).Items.OfType<ConversationRecord>().ToArray();
                var restoredRecord = restoredRecords.SingleOrDefault(r => r.Id == coordinator.Id);
                if (restoredRecord == null) throw new InvalidOperationException("Restored conversation missing; provider=" + Convert.ToString(((TabItem)((TabControl)restoredAdvanced.FindName("ProviderChoice")).SelectedItem).Tag) + "; records=" + restoredRecords.Length + "; status=" + ((TextBlock)restoredAdvanced.FindName("StatusLabel")).Text);
                var restoredIdentity = collaboration.GetAgent(coordinator.Id, "Codex");
                Check("ADVANCED-08 recreated IDE controls restore AI identity and selected collaborators", restoredRecord.CollaboratorIds.Contains(colleague.Id) && restoredIdentity.Letter == identity.Letter && restoredIdentity.Role == "최종 검토" && restoredIdentity.Name.EndsWith("코덱스" + identity.Letter));
            }
            await CheckImageUiAsync(view);
            await CheckLatestUiAsync(view, dte, frame, sourcePath, otherPath, config.OutputDirectory);
            await CheckProjectExternalEditingAsync(service, dte, config.OutputDirectory);
            dte.Documents.SaveAll();
            ProductUi.SelectProvider(view, "Codex");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(AssistantControl).GetMethod("SetAgentFullAccess", flags).Invoke(view, new object[] { true });
            coordinator.CollaboratorIds = new string[0]; new MarkdownWorkspaceStore().Save(coordinator);
            Text(view, "ExecutablePath").Text = config.RuntimeFixturePath;
            Text(view, "Instruction").Text = "Run the isolated native fixture command and report actual files and usage.";
            string retainedCollaborationPreview = Text(view, "ProposedPreview").Text;
            object retainedCollaborationProposal = typeof(AssistantControl).GetField("pendingProposal", flags).GetValue(view);
            Click(view, "GenerateButton");
            File.AppendAllText(progressPath, "ROOM-QUEUE-03 observation: " + new JavaScriptSerializer().Serialize(new {
                Status = ((TextBlock)view.FindName("StatusLabel")).Text,
                PreviewPreserved = Text(view, "ProposedPreview").Text == retainedCollaborationPreview,
                ProposalBefore = retainedCollaborationProposal != null,
                ProposalPreserved = ReferenceEquals(retainedCollaborationProposal, typeof(AssistantControl).GetField("pendingProposal", flags).GetValue(view)),
                DiscardEnabled = Button(view, "DiscardProposalButton").IsEnabled,
                MarkerExists = File.Exists(Path.Combine(config.OutputDirectory, "actual-command-passed.txt")),
                ActiveDocument = dte.ActiveDocument == null ? "" : dte.ActiveDocument.FullName,
                Provider = Convert.ToString(((TabItem)((TabControl)view.FindName("ProviderChoice")).SelectedItem).Tag)
            }) + "\r\n");
            Check("ROOM-QUEUE-03 collaboration proposal blocks direct writes until explicit skip", Text(view, "ProposedPreview").Text == retainedCollaborationPreview && Button(view, "DiscardProposalButton").IsEnabled && !File.Exists(Path.Combine(config.OutputDirectory, "actual-command-passed.txt")));
            Click(view, "DiscardProposalButton");
            await GenerateAsync(view, 30000, false);
            Check("FULL-04 actual IDE full runner executes file writes and test command", File.Exists(Path.Combine(config.OutputDirectory, "actual-command-passed.txt")) && File.ReadAllText(Path.Combine(config.OutputDirectory, "NativeFixture.txt")) == "new");
            string immediateUsage = Convert.ToString(((ToolTip)((Button)view.FindName("ModelOptionsButton")).ToolTip).Content);
            Check("FULL-08 completed request immediately exposes its final reported usage", immediateUsage.Contains("24 tokens"));
            File.AppendAllText(progressPath, "Native UI provider=" + Convert.ToString(((TabItem)((TabControl)view.FindName("ProviderChoice")).SelectedItem).Tag) + "; immediate usage=" + immediateUsage.Replace("\n", " | ") + "\r\n");
            await Task.Delay(200);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            File.AppendAllText(progressPath, "Native UI settled usage=" + Convert.ToString(((ToolTip)((Button)view.FindName("ModelOptionsButton")).ToolTip).Content).Replace("\n", " | ") + "\r\n");
            Check("FULL-05 actual IDE visible model tooltip displays reported tokens", Convert.ToString(((ToolTip)((Button)view.FindName("ModelOptionsButton")).ToolTip).Content).Contains("24") && Convert.ToString(((ToolTip)((Button)view.FindName("ModelOptionsButton")).ToolTip).Content).Contains("tokens"));
            Check("FULL-06 actual IDE records public progress and completed state", ((ItemsControl)view.FindName("ConversationMessages")).Items.Cast<ChatMessage>().Any(message => message.Role == "system" && message.Text.Contains("명령 실행")) && ((ConversationRecord)((ListBox)view.FindName("ConversationList")).SelectedItem).State == ConversationRunState.Completed);
            view.UpdateLayout();
            var actualProgressViews = Visuals(view).OfType<FrameworkElement>().Where(element => element.GetType().Name == "ProgressNoticePresenter").ToArray();
            File.AppendAllText(progressPath, "Native progress owner observation: cards=" + actualProgressViews.Length + "; owners=" + string.Join(",", actualProgressViews.Select(element => System.Windows.Window.GetWindow(element) == null ? "null" : System.Windows.Window.GetWindow(element).GetType().FullName)) + "\r\n");
            Check("FULL-07 actual IDE executed change card preserves direct mode", ((TextBlock)view.FindName("ChangeSummary")).Text.Length > 0 && Convert.ToString(Button(view, "PermissionsButton").Content).Contains("전체 액세스"));
            typeof(AssistantControl).GetMethod("SetAgentFullAccess", flags).Invoke(view, new object[] { false });
            dte.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
            Check("FULL-03 a solution without an active source permits project context", directDocuments.CaptureFullAccessContext() == null && ((ISourceNavigationService)service).GetProjectDirectory() == Path.GetDirectoryName(config.SolutionPath));
            await CheckDebuggingHelperAsync(view, dte, config.OutputDirectory);
            var json = new JavaScriptSerializer();
            File.WriteAllText(Path.Combine(config.OutputDirectory, "result.json"), json.Serialize(new {
                Succeeded = failed == 0, Passed = passed, Failed = failed, IdeRuntimeTest = true,
                HostVersion = dte.Version, RegistryRoot = dte.RegistryRoot,
                ProductAssembly = product.GetType().Assembly.Location, ProductVersion = product.GetType().Assembly.GetName().Version.ToString(),
                ProductSha256 = loadedHash,
                LiveCodexTest = liveRan, LiveSqlDraftTest = liveRan, LiveClaudeTest = false, UserProjectSourceSent = false, Tests = results
            }), new UTF8Encoding(false));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChangeFrameModeAsync
        /// desc         : 실제 프레임 전환의 반환 코드와 완료 상태를 제한 시간 동안 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 IDE별 지연 전환과 실제 실패를 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async System.Threading.Tasks.Task<int> ChangeFrameModeAsync(IVsWindowFrame frame, VSFRAMEMODE requested, DTE dte)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            int code = frame.SetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, (int)requested);
            File.AppendAllText(progressPath, "Frame transition requested=" + requested + "; HRESULT=0x" + code.ToString("X8") + "\r\n");
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(code);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
            object actual = null;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                await Task.Delay(50);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.GetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, out actual));
                if (Convert.ToInt32(actual) == (int)requested) break;
            }
            if (requested == VSFRAMEMODE.VSFM_Dock && Convert.ToInt32(actual) != (int)requested)
            {
                object automation;
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.GetProperty((int)__VSFPROPID.VSFPROPID_ExtWindowObject, out automation));
                var window = automation as EnvDTE.Window;
                if (window == null) throw new InvalidOperationException("Tool window automation unavailable");
                File.AppendAllText(progressPath, "Dock automation before: kind=" + window.Kind + "; linkable=" + window.Linkable + "; floating=" + window.IsFloating + "\r\n");
                window.Linkable = true; window.IsFloating = false;
                await Task.Delay(200);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.GetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, out actual));
                File.AppendAllText(progressPath, "Dock automation actual=" + actual + "\r\n");
                if (Convert.ToInt32(actual) != (int)requested)
                {
                    var target = dte.Windows.Item(EnvDTE.Constants.vsWindowKindSolutionExplorer);
                    target.Visible = true;
                    var linked = dte.Windows.CreateLinkedWindowFrame(window, target, vsLinkedWindowType.vsLinkedWindowTypeDocked);
                    linked.Visible = true;
                    await Task.Delay(200);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.GetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, out actual));
                    File.AppendAllText(progressPath, "Dock explicit Solution Explorer target: children=" + linked.LinkedWindows.Count + "; actual=" + actual + "\r\n");
                }
            }
            File.AppendAllText(progressPath, "Frame transition actual=" + actual + "\r\n");
            return Convert.ToInt32(actual);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WriteUnreadDiagnostic
        /// desc         : 읽음 검사 선택과 저장소 상태를 익명 테스트 경로에 기록하며 제품 상태는 변경하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void WriteUnreadDiagnostic(AssistantControl view, ConversationRecord selected, string output, string phase, MarkdownWorkspaceStore observedStore = null, ConversationRecord[] observedRecords = null)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var provider = (string)typeof(AssistantControl).GetField("selectedProvider", flags).GetValue(view);
            var active = (System.Collections.IDictionary)typeof(AssistantControl).GetField("activeRecords", flags).GetValue(view);
            var current = (ConversationRecord)active[provider];
            var store = observedStore ?? new MarkdownWorkspaceStore();
            var open = observedRecords ?? new ConversationRecord[0];
            var uiStore = (MarkdownWorkspaceStore)typeof(AssistantControl).GetField("workspaceStore", flags).GetValue(view);
            var observation = new {
                Phase = phase, SelectedProvider = provider,
                TabProvider = Convert.ToString(((TabItem)((TabControl)view.FindName("ProviderChoice")).SelectedItem).Tag),
                UiWorkspaceRoot = uiStore.RootPath, ObservedWorkspaceRoot = store.RootPath,
                EnvironmentRoot = Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH"),
                ActualAssertionRecordsCaptured = observedRecords != null,
                Selected = new { selected.Id, selected.Provider, selected.IsOpen, selected.HasUnread },
                Active = new { current.Id, current.Provider, current.IsOpen, current.HasUnread },
                Open = open.Select(r => new { r.Id, r.Provider, r.IsOpen, r.HasUnread }).ToArray(),
                Warnings = store.LastLoadWarnings,
                Status = ((TextBlock)view.FindName("StatusLabel")).Text
            };
            File.WriteAllText(Path.Combine(output, "sidebar-unread-" + phase + ".json"), new JavaScriptSerializer().Serialize(observation), new UTF8Encoding(false));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckImageUiAsync
        /// desc         : 실제 VS2017 도구 창에서 하단 배치와 anchor 팝업·선택·닫기를 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-018 이미지7~16의 실제 IDE 회귀 추가.
        ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
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
            Check("IMAGES-04 actual IDE permission dropdown exposes three presets and the separate action planner", labels.Any(t => t.Contains("승인 요청")) && labels.Any(t => t.Contains("작업 도구 허용")) && labels.Any(t => t.Contains("전체 액세스")) && labels.Any(t => t.Contains("DB·형상관리·배포")));
            typeof(AssistantControl).GetMethod("CloseDropdown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            Click(view, "AgentButton"); await Task.Delay(150);
            popup = (Popup)typeof(AssistantControl).GetField("dropdownPopup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            var selected = (ConversationRecord)((ListBox)view.FindName("ConversationList")).SelectedItem;
            var agent = new ConversationCollaborationStore(new MarkdownWorkspaceStore().RootPath).GetAgent(selected.Id, "Codex");
            var choice = Visuals(popup.Child).OfType<Button>().Single(b => Convert.ToString(b.Content).EndsWith(agent.Name));
            choice.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); await Task.Delay(150);
            Check("IMAGES-05 the actual IDE AI dropdown routes its selected name to the original conversation", !popup.IsOpen && ((ConversationRecord)((ListBox)view.FindName("ConversationList")).SelectedItem).Id == selected.Id && agent.Name.EndsWith("코덱스" + agent.Letter));
            var trigger = (ToggleButton)view.FindName("OptionsToggle");
            typeof(AssistantControl).GetMethod("CloseFan", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            int before = (int)typeof(AssistantControl).GetField("fanOpenCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            for (int i = 0; i < 10; i++) trigger.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            var hoverTimer = (System.Windows.Threading.DispatcherTimer)typeof(AssistantControl).GetField("fanOpenTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            bool intentScheduled = hoverTimer.IsEnabled && hoverTimer.Interval.TotalMilliseconds == 220
                && (int)typeof(AssistantControl).GetField("fanOpenCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view) == before;
            typeof(AssistantControl).GetMethod("OpenFanMenu", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            for (int i = 0; i < 10; i++) trigger.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            Check("IMAGES-06 synthetic hover schedules intent and explicit opening runs once in the actual IDE",
                intentScheduled && !hoverTimer.IsEnabled && (int)typeof(AssistantControl).GetField("fanOpenCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view) == before + 1);
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
        ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
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
        /// name         : CheckSharedRoomEditingAsync
        /// desc         : 실제 IDE의 두 대화 임대가 같은 보호 scope를 사용하고 마지막 종료에만 버퍼를 복원하는지 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 전용 호스트의 실제 문서 보호·외부 변경·참조계수 복원 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task CheckSharedRoomEditingAsync(AssistantControl view, IExternalEditingDocumentService editingService,
            IEditorDocument target, Document sourceDoc, string sourcePath, byte[] originalBytes, string originalDisk, string changedDisk)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var acquire = typeof(AssistantControl).GetMethod("AcquireRoomExternalEditing", flags);
            var release = typeof(AssistantControl).GetMethod("ReleaseRoomExternalEditing", flags);
            var scope = typeof(AssistantControl).GetField("sharedExternalEditing", flags);
            var count = typeof(AssistantControl).GetField("sharedExternalEditingUsers", flags);
            var chat = new ChatSession();
            bool first = false, second = false;
            try
            {
                first = (bool)acquire.Invoke(view, null);
                object identity = scope.GetValue(view);
                Check("ROOM-SYNC-01 first room protects actual saved IDE buffer", first && identity != null && target.Capture().IsReadOnly);
                second = (bool)acquire.Invoke(view, null);
                Check("ROOM-SYNC-02 second room shares one actual IDE scope", second && ReferenceEquals(identity, scope.GetValue(view)) && (int)count.GetValue(view) == 2 && ((IExternalEditingScopeState)identity).IsTemporarilyReadOnly(sourcePath));
                File.WriteAllText(sourcePath, changedDisk, new UTF8Encoding(true));
                await Task.Delay(150);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                release.Invoke(view, new object[] { chat }); second = false;
                Check("ROOM-SYNC-03 first completion keeps remaining room protected", (int)count.GetValue(view) == 1 && target.Capture().IsReadOnly && target.Capture().Content == originalDisk);
                release.Invoke(view, new object[] { chat }); first = false;
                Check("ROOM-SYNC-04 last completion restores buffer and editability", (int)count.GetValue(view) == 0 && scope.GetValue(view) == null && !target.Capture().IsReadOnly && target.Capture().Content == changedDisk && sourceDoc.Saved);
            }
            finally
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (second) release.Invoke(view, new object[] { chat });
                if (first) release.Invoke(view, new object[] { chat });
                using (var editing = editingService.BeginExternalEditing(null)) File.WriteAllBytes(sourcePath, originalBytes);
            }
            Check("ROOM-SYNC-05 original fixture content and state restored", target.Capture().Content == originalDisk && !target.Capture().IsReadOnly && sourceDoc.Saved);
            bool originallyReadOnly = sourceDoc.ReadOnly;
            bool readOnlyLease = false;
            try
            {
                sourceDoc.ReadOnly = true;
                readOnlyLease = (bool)acquire.Invoke(view, null);
                Check("ROOM-SYNC-06 baseline read-only is excluded from temporary protection", target.Capture().IsReadOnly && !((IExternalEditingScopeState)scope.GetValue(view)).IsTemporarilyReadOnly(sourcePath));
                var requestType = typeof(AssistantControl).GetNestedType("QueuedUiRequest", BindingFlags.NonPublic);
                object request = Activator.CreateInstance(requestType, true);
                requestType.GetField("Document", flags | BindingFlags.Public).SetValue(request, target);
                requestType.GetField("Source", flags | BindingFlags.Public).SetValue(request, target.Capture());
                bool rejected = false;
                try { typeof(AssistantControl).GetMethod("RequireRoomSourceUnchanged", flags).Invoke(view, new[] { request }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
                Check("ROOM-SYNC-07 another room cannot bypass baseline read-only", rejected && target.Capture().Content == originalDisk);
            }
            finally
            {
                if (readOnlyLease) release.Invoke(view, new object[] { chat });
                sourceDoc.ReadOnly = originallyReadOnly;
            }
            Check("ROOM-SYNC-08 baseline fixture protection is restored", !target.Capture().IsReadOnly && sourceDoc.Saved && (int)count.GetValue(view) == 0);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GenerateAsync
        /// desc         : 실제 생성 버튼을 누른 후 Dispatcher를 막지 않고 제품의 비동기 처리를 기다린다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 타임아웃 시 상태 텍스트를 진단에 포함.
        ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task GenerateAsync(AssistantControl view, int timeoutMs = 15000, bool submit = true)
        {
            if (submit) Click(view, "GenerateButton");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var busyField = typeof(AssistantControl).GetField("busy", flags);
            var drainingField = typeof(AssistantControl).GetField("drainingRequests", flags);
            Func<bool> active = () => Button(view, "StopButton").IsEnabled || (bool)busyField.GetValue(view) || (bool)drainingField.GetValue(view);
            while (active() && clock.ElapsedMilliseconds < timeoutMs) await Task.Delay(50);
            if (active()) throw new TimeoutException("Generate timed out: " + ((TextBlock)view.FindName("StatusLabel")).Text);
            File.AppendAllText(progressPath, "Generate: " + ((TextBlock)view.FindName("StatusLabel")).Text + "\r\n");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApproveAndApply
        /// desc         : 명시적 승인 체크 후 실제 적용 이벤트를 호출한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 테스트 전용 승인 절차.
        ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
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
        ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
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
        ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
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
        ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
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
        ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
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
