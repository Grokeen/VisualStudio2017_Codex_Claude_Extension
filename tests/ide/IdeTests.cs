using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Task = System.Threading.Tasks.Task;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
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
            Check("CHAT-09 conversation stacks user and AI turns", ((ItemsControl)view.FindName("ConversationMessages")).Items.Count >= 3);
            Check("IDE-11 approval starts unchecked", ((CheckBox)view.FindName("ReviewApproval")).IsChecked != true && !Button(view, "ApplyButton").IsEnabled);
            Check("IDE-12 proposal preserves buffer and disk", target.Capture().Content == unsaved && File.ReadAllText(sourcePath) == originalDisk);
            dte.ItemOperations.OpenFile(otherPath);
            ApproveAndApply(view);
            Check("IDE-13 active file switch preserves fixed target", target.Capture().Content == unsaved.Replace("return 1;", "return 2;") && File.ReadAllText(otherPath) == otherDisk);
            Check("IDE-14 apply changes only editor buffer", !sourceDoc.Saved && File.ReadAllText(sourcePath) == originalDisk);
            dte.ItemOperations.OpenFile(sourcePath);
            dte.ExecuteCommand("Edit.Undo");
            Check("IDE-15 one IDE Undo restores entire prior buffer", target.Capture().Content == unsaved);

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

            await GenerateAsync(view);
            textDoc.StartPoint.CreateEditPoint().ReplaceText(textDoc.EndPoint, unsaved.Replace("return 1;", "return 9;"), 0);
            ApproveAndApply(view);
            Check("IDE-20 edits after proposal block apply", target.Capture().Content.Contains("return 9;") && ((TextBlock)view.FindName("StatusLabel")).Text.StartsWith("적용 실패:"));
            textDoc.StartPoint.CreateEditPoint().ReplaceText(textDoc.EndPoint, unsaved, 0);
            sourceDoc.Save(sourcePath);

            ((TabControl)view.FindName("ProviderChoice")).SelectedIndex = 1;
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
                ((TabControl)view.FindName("ProviderChoice")).SelectedIndex = 0;
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
            var json = new JavaScriptSerializer();
            File.WriteAllText(Path.Combine(config.OutputDirectory, "result.json"), json.Serialize(new {
                Succeeded = failed == 0, Passed = passed, Failed = failed, IdeRuntimeTest = true,
                HostVersion = dte.Version, RegistryRoot = dte.RegistryRoot,
                ProductAssembly = product.GetType().Assembly.Location, ProductVersion = product.GetType().Assembly.GetName().Version.ToString(),
                LiveCodexTest = liveRan, LiveClaudeTest = false, UserProjectSourceSent = false, Tests = results
            }), new UTF8Encoding(false));
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
