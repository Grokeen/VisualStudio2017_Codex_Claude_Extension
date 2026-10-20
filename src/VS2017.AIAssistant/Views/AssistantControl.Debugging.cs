using System;
using System.Threading;
using System.Windows;
using VS2017.AIAssistant.Services;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 사용자가 선택한 중단 정보만 기존 요청 초안에 추가한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 자동 전송·디버거 제어 없이 bounded context 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private IDebuggingContextProvider debuggingContext;
        private CancellationTokenSource debuggingCapture;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeDebuggingHelper
        /// desc         : 실제 IDE 서비스 또는 주입한 읽기 전용 서비스의 수명과 상태 이벤트를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : IDE 밖의 무창 검사에서는 서비스 부재를 정상 처리한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InitializeDebuggingHelper()
        {
            Dispatcher.VerifyAccess(); DisposeDebuggingHelper();
            try { SetDebuggingContextProvider(documents as IDebuggingContextProvider ?? new DebuggingContextService(Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider, Dispatcher)); }
            catch (Exception) { DebuggingInfoButton.Visibility = Visibility.Collapsed; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SetDebuggingContextProvider
        /// desc         : 자기 읽기 서비스와 상태 이벤트를 교체한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SetDebuggingContextProvider(IDebuggingContextProvider source)
        {
            Dispatcher.VerifyAccess(); DisposeDebuggingHelper(); debuggingContext = source;
            if (debuggingContext != null) debuggingContext.StateChanged += DebuggingStateChanged;
            UpdateDebuggingAvailability();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DebuggingStateChanged
        /// desc         : 상태 변경 시 DBG 버튼의 사용 가능 여부만 갱신한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DebuggingStateChanged(object sender, EventArgs args)
        { Dispatcher.VerifyAccess(); if (!disposed) UpdateDebuggingAvailability(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateDebuggingAvailability
        /// desc         : 현재 읽을 수 있는 중단 위치에서만 DBG 동작을 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateDebuggingAvailability()
        {
            Dispatcher.VerifyAccess(); bool available = !disposed && debuggingContext != null && debuggingContext.CanCapture;
            DebuggingInfoButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
            DebuggingInfoButton.IsEnabled = available && debuggingCapture == null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DebuggingInfoClicked
        /// desc         : 중단 정보 읽기 성공과 현재 대화·초안·16K 제한 확인 후 한 번에 추가한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 취소·모드전환·초안변경·실패는 원래 입력과 선택을 보존한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100", Justification = "WPF Click의 void 계약이며 await 전체 예외를 catch에서 처리한다.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD109", Justification = "WPF dispatcher에서 호출하고 await 뒤 Dispatcher.VerifyAccess로 원래 UI 문맥을 확인한다.")]
        private async void DebuggingInfoClicked(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess(); if (disposed || debuggingCapture != null) return;
            var source = debuggingContext;
            if (source == null || !source.CanCapture) { StatusLabel.Text = "디버거가 중단되어 있고 현재 소스 위치를 읽을 수 있을 때 사용할 수 있습니다."; return; }
            string original = Instruction.Text; int selection = Instruction.SelectionStart, length = Instruction.SelectionLength;
            var owner = ActiveChat; var cancellation = new CancellationTokenSource(); debuggingCapture = cancellation; UpdateDebuggingAvailability();
            try
            {
                string context = await source.CaptureAsync(cancellation.Token); Dispatcher.VerifyAccess();
                if (disposed || !ReferenceEquals(source, debuggingContext)) return;
                cancellation.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(owner, ActiveChat) || Instruction.Text != original) { StatusLabel.Text = "대화나 초안이 바뀌어 추가하지 않았습니다. 원래 입력을 보존했습니다."; return; }
                if (String.IsNullOrWhiteSpace(context) || context.Length > DebuggingContextService.MaximumContextCharacters) throw new InvalidOperationException("안전한 크기의 디버깅 정보를 읽지 못했습니다.");
                string combined = original + (original.Length == 0 ? "" : "\n\n") + context;
                int limit = Instruction.MaxLength > 0 ? Math.Min(16000, Instruction.MaxLength) : 16000;
                if (combined.Length > limit) { StatusLabel.Text = "디버깅 정보를 추가하면 요청 16,000자 제한을 넘습니다. 기존 입력을 보존했습니다."; return; }
                Instruction.Text = combined; Instruction.Select(selection, length);
                StatusLabel.Text = "디버깅 정보가 요청 초안에 추가되었습니다. 민감한 내용이 없는지 확인한 뒤 보내세요.";
            }
            catch (OperationCanceledException) { if (!disposed) StatusLabel.Text = "디버깅 정보 추가를 취소했습니다. 기존 입력을 보존했습니다."; }
            catch (Exception) { if (!disposed) StatusLabel.Text = "디버깅 정보를 읽지 못했습니다. 중단 위치와 열린 소스를 확인하세요. 기존 입력을 보존했습니다."; }
            finally { Dispatcher.VerifyAccess(); if (ReferenceEquals(debuggingCapture, cancellation)) debuggingCapture = null; cancellation.Dispose(); if (!disposed) UpdateDebuggingAvailability(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeDebuggingHelper
        /// desc         : 읽기 요청을 취소하고 디버깅 상태 이벤트와 자기 서비스 수명을 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 디버거 중지·재개·step 등 실행 상태는 바꾸지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeDebuggingHelper()
        {
            Dispatcher.VerifyAccess(); if (debuggingCapture != null) debuggingCapture.Cancel();
            var source = debuggingContext; debuggingContext = null;
            if (source != null) { source.StateChanged -= DebuggingStateChanged; source.Dispose(); }
            if (DebuggingInfoButton != null) DebuggingInfoButton.Visibility = Visibility.Collapsed;
        }
    }
}
