using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VS2017.AIAssistant.Services;
using VS2017.AIAssistant.Views;
using Task = System.Threading.Tasks.Task;

namespace CodexCode.IdeIntegration
{
    internal static partial class IdeTests
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckDebuggingHelperAsync
        /// desc         : 자기 격리 IDE에서 합성 프로그램을 중단해 실제 DTE 위치와 초안 추가를 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 실제 COM frame·읽기 전용 캡처·전송 없음과 이벤트 수명 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task CheckDebuggingHelperAsync(AssistantControl view, DTE dte, string output)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!dte.RegistryRoot.Contains("CodexCodeTest")) throw new InvalidOperationException("Owned test IDE required.");
            var button = Button(view, "DebuggingInfoButton");
            Check("DEBUG069-01 idle debugger hides the diagnostic action", dte.Debugger.CurrentMode == dbgDebugMode.dbgDesignMode && button.Visibility == Visibility.Collapsed);
            string path = Path.Combine(output, "DebugProgram.cs");
            byte[] original = File.ReadAllBytes(path);
            var breakpoint = dte.Debugger.Breakpoints.Add(File: path, Line: 8).Item(1);
            string oldDraft = Text(view, "Instruction").Text;
            var active = (VS2017.AIAssistant.Chat.ChatSession)typeof(AssistantControl).GetProperty("ActiveChat", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view, null);
            int messages = active.Messages.Count;
            try
            {
                dte.ItemOperations.OpenFile(path);
                dte.ExecuteCommand("Debug.Start");
                for (int attempt = 0; attempt < 300 && dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode; attempt++)
                { await Task.Delay(100); await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(); }
                Check("DEBUG069-02 synthetic managed program reaches its owned breakpoint", dte.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode);
                var frame = dte.Debugger.CurrentStackFrame;
                Check("DEBUG069-03 actual debugger frame is the synthetic nested method", frame != null && frame.FunctionName.Contains("DebugProgram.Nested"));
                for (int attempt = 0; attempt < 30 && button.Visibility != Visibility.Visible; attempt++)
                { await Task.Delay(50); await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(); }
                Check("DEBUG069-04 actual debugger event enables the diagnostic action", button.Visibility == Visibility.Visible && button.IsEnabled);
                using (var service = new DebuggingContextService(ServiceProvider.GlobalProvider, view.Dispatcher))
                {
                    Check("DEBUG069-05 actual COM frame location is readable", service.CanCapture);
                    string context = await service.CaptureAsync(CancellationToken.None);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    Check("DEBUG069-06 capture contains actual file line and source", context.Contains("DebugProgram.cs") && context.Contains("줄: 8") && context.Contains("KeepAlive"));
                    Check("DEBUG069-07 capture contains bounded stack and no variable values", context.Contains("DebugProgram.Nested") && context.Contains("DebugProgram.Main") && !context.Contains("SECRET_VALUE_SENTINEL") && context.Length <= DebuggingContextService.MaximumContextCharacters);
                    File.WriteAllText(Path.Combine(output, "task069-debug-context.txt"), context);
                }
                var instruction = Text(view, "Instruction"); instruction.Text = "이 중단 위치를 검토해주세요."; instruction.Select(2, 3);
                Click(view, "DebuggingInfoButton");
                for (int attempt = 0; attempt < 30 && !instruction.Text.Contains("[디버깅 중단 정보"); attempt++)
                { await Task.Delay(50); await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(); }
                Check("DEBUG069-08 explicit click appends a reviewable draft and preserves selection", instruction.Text.StartsWith("이 중단 위치를 검토해주세요.\n\n[디버깅 중단 정보") && instruction.SelectionStart == 2 && instruction.SelectionLength == 3);
                Check("DEBUG069-09 helper leaves message count debugger source and mode unchanged", active.Messages.Count == messages && ReferenceEquals(active, typeof(AssistantControl).GetProperty("ActiveChat", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view, null)) && dte.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode && dte.Debugger.CurrentStackFrame.FunctionName == frame.FunctionName && original.SequenceEqual(File.ReadAllBytes(path)));
                instruction.Text = new string('x', 15900); Click(view, "DebuggingInfoButton");
                await Task.Delay(80); await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Check("DEBUG069-10 oversized combined draft is rejected atomically", instruction.Text == new string('x', 15900) && active.Messages.Count == messages);
                File.WriteAllText(Path.Combine(output, "task069-debug-observation.json"), new JavaScriptSerializer().Serialize(new { ActualDteFrame = true, ActualBreakpointLine = 8, AutomaticSend = false, VariableEvaluation = false, UserProjectUsed = false, OriginalSourcePreserved = original.SequenceEqual(File.ReadAllBytes(path)), CheckedAt = DateTimeOffset.Now }));
            }
            finally
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (dte.Debugger.CurrentMode != dbgDebugMode.dbgDesignMode) dte.Debugger.Stop(false);
                for (int attempt = 0; attempt < 100 && dte.Debugger.CurrentMode != dbgDebugMode.dbgDesignMode; attempt++)
                { await Task.Delay(50); await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(); }
                breakpoint.Delete(); Text(view, "Instruction").Text = oldDraft;
            }
            Check("DEBUG069-11 debugger exit event hides diagnostic action", dte.Debugger.CurrentMode == dbgDebugMode.dbgDesignMode && button.Visibility == Visibility.Collapsed);
            dte.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);
        }
    }
}
