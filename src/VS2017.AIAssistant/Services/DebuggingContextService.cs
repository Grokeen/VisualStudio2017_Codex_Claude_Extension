using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using EnvDTE;

namespace VS2017.AIAssistant.Services
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DebuggingFrameData
    /// desc         : 명시적으로 읽은 중단 위치·제한된 소스/함수명만 보관한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 변수·식·자동창 값을 담는 필드는 없다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class DebuggingFrameData
    {
        public bool IsBreakMode { get; set; }
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public string FunctionName { get; set; }
        public int SourceStartLine { get; set; }
        public string[] SourceLines { get; set; } = new string[0];
        public string[] StackFunctions { get; set; } = new string[0];
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IDebuggingContextReader
    /// desc         : UI dispatcher에서 중단 위치와 제한된 읽기 전용 자료만 제공하는 경계다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실제 DTE와 격리 검사 어댑터가 동일 계약을 사용한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface IDebuggingContextReader : IDisposable
    {
        event EventHandler StateChanged;
        DebuggingFrameData ReadCurrent(bool includeDetails);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IDebuggingContextProvider
    /// desc         : 초안 추가용 bounded 중단 정보를 제공하며 전송이나 디버거 제어는 하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 사용 가능 상태 조회에는 소스/스택 본문을 읽지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface IDebuggingContextProvider : IDisposable
    {
        event EventHandler StateChanged;
        bool CanCapture { get; }
        Task<string> CaptureAsync(CancellationToken cancellation);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DebuggingContextService
    /// desc         : 현재 break frame과 이미 열린 소스의 앞뒤3줄·호출 함수3개를 안전한 초안으로 만든다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 #18 COM은 UI 전용·추가 파일 I/O/식 평가/실행 제어 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class DebuggingContextService : IDebuggingContextProvider
    {
        public const int MaximumContextCharacters = 5000;
        private readonly IDebuggingContextReader reader;
        private readonly Dispatcher dispatcher;
        private bool disposed;
        public event EventHandler StateChanged;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DebuggingContextService
        /// desc         : 현재 IDE의 DTE만 읽기 전용 어댑터로 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : VS2017~2022의 기존 EnvDTE 참조만 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DebuggingContextService(IServiceProvider services, Dispatcher owner)
        {
            if (owner == null) throw new ArgumentNullException("owner");
            owner.VerifyAccess(); dispatcher = owner; reader = new DteReader(services, owner); reader.StateChanged += ReaderStateChanged;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DebuggingContextService
        /// desc         : 동일 UI 수명 계약의 읽기 어댑터를 주입한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 격리 검사는 실제 형식화·상태 검사 경로를 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DebuggingContextService(IDebuggingContextReader source, Dispatcher owner)
        {
            if (source == null || owner == null) throw new ArgumentNullException(source == null ? "source" : "owner");
            owner.VerifyAccess(); reader = source; dispatcher = owner; reader.StateChanged += ReaderStateChanged;
        }

        public bool CanCapture
        {
            get { dispatcher.VerifyAccess(); if (disposed) return false; try { return ValidLocation(reader.ReadCurrent(false)); } catch (Exception) { return false; } }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureAsync
        /// desc         : 명시 클릭에서만 상세를 읽고 위치가 그대로인지 확인해 최대5000자 정보를 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 열린 편집기 COM 읽기만 하므로 디스크 I/O나 background COM 호출이 없다.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD109", Justification = "중단 frame과 열린 편집기 COM은 WPF dispatcher 전용이며 비동기 전환 없이 즉시 읽어 완료 Task로 반환한다.")]
        public Task<string> CaptureAsync(CancellationToken cancellation)
        {
            dispatcher.VerifyAccess(); cancellation.ThrowIfCancellationRequested();
            if (disposed) throw new ObjectDisposedException("DebuggingContextService");
            var frame = reader.ReadCurrent(true);
            if (!ValidLocation(frame)) throw new InvalidOperationException("디버거가 중단되어 있고 읽을 수 있는 현재 소스 위치가 있어야 합니다.");
            string file = frame.FilePath, function = frame.FunctionName; int line = frame.LineNumber, first = frame.SourceStartLine;
            string[] source = (frame.SourceLines ?? new string[0]).ToArray(), stack = (frame.StackFunctions ?? new string[0]).Take(3).ToArray();
            if (source.Length == 0 || source.Length > 7 || first < Math.Max(1, line - 3) || first > line ||
                line >= (long)first + source.Length || (long)first + source.Length - 1 > (long)line + 3)
                throw new InvalidOperationException("현재 중단 줄 주변의 열린 소스를 안전한 범위 안에서 읽지 못했습니다.");
            cancellation.ThrowIfCancellationRequested(); var current = reader.ReadCurrent(false);
            if (!ValidLocation(current) || current.LineNumber != line || !String.Equals(current.FilePath, file, StringComparison.OrdinalIgnoreCase) || current.FunctionName != function)
                throw new InvalidOperationException("디버깅 중단 위치가 바뀌었습니다. 다시 선택하세요.");
            var text = new StringBuilder("[디버깅 중단 정보 · 사용자 선택으로 추가]\n");
            text.Append("상태: 디버거 중단 중 · 중단 원인/예외 정보는 자동 수집하지 않음\n파일: ").Append(OneLine(Path.GetFileName(file), 255)).Append("\n줄: ").Append(line)
                .Append("\n현재 함수: ").Append(OneLine(function, 240)).Append("\n열린 편집기 소스(앞뒤 최대3줄, 행별 최대400자):\n");
            for (int index = 0; index < source.Length; index++) text.Append("    ").Append(first + index).Append(first + index == line ? " > " : " : ").Append(OneLine(source[index], 400)).Append('\n');
            text.Append("호출 스택(함수명 최대3개, 값 제외):\n");
            if (stack.Length == 0) text.Append("    읽을 수 있는 함수명 없음\n");
            for (int index = 0; index < stack.Length; index++) text.Append("    ").Append(index + 1).Append(". ").Append(OneLine(stack[index], 240)).Append('\n');
            text.Append("변수 값·식 평가·디버그 제어는 수행하지 않았습니다. 전송 전 내용을 확인하세요.");
            if (text.Length > MaximumContextCharacters) throw new InvalidOperationException("디버깅 정보가 안전한 초안 크기를 넘습니다.");
            return Task.FromResult(text.ToString());
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidLocation
        /// desc         : 중단 모드와 현재 파일·줄·함수명의 유효성만 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool ValidLocation(DebuggingFrameData frame)
        { return frame != null && frame.IsBreakMode && !String.IsNullOrWhiteSpace(frame.FilePath) && frame.FilePath.Length <= 4096 && frame.LineNumber > 0 && !String.IsNullOrWhiteSpace(frame.FunctionName); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OneLine
        /// desc         : 제어문자를 제거하고 출력 한 줄의 최대 길이를 제한한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string OneLine(string value, int limit)
        { return new string((value ?? "").Select(c => Char.IsControl(c) ? ' ' : c).Take(limit).ToArray()); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReaderStateChanged
        /// desc         : 자기 dispatcher에서 상태 변경만 구독자에게 알린다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ReaderStateChanged(object sender, EventArgs args)
        { dispatcher.VerifyAccess(); if (!disposed) { var handler = StateChanged; if (handler != null) handler(this, EventArgs.Empty); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 이벤트를 해제하고 자기 DTE 읽기 어댑터의 수명을 종료한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 중복 해제는 무시하고 디버거 실행 상태는 바꾸지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        { dispatcher.VerifyAccess(); if (disposed) return; disposed = true; reader.StateChanged -= ReaderStateChanged; reader.Dispose(); }

        // VS2017 SDK EnvDTE90a.StackFrame2의 실제 IID/DispId. IDIspatch 속성 읽기만 사용해 새 SDK 참조를 요구하지 않는다.
        [ComImport, Guid("FD76BAF1-20DF-4659-864F-E8AEB46139FB"), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IFrameLocation
        {
            [DispId(110)] uint LineNumber { get; }
            [DispId(111)] string FileName { get; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DteReader
        /// desc         : 현재 IDE의 디버거와 열린 문서를 읽기 전용으로 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class DteReader : IDebuggingContextReader
        {
            private readonly DTE dte;
            private readonly Dispatcher dispatcher;
            private DebuggerEvents events;
            private bool disposed;
            public event EventHandler StateChanged;

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : DteReader
            /// desc         : UI에서 DTE와 모드·컨텍스트 이벤트의 수명을 연결한다.
            /// author       : CodexOrig
            /// create date  : 2026-10-20
            /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
            /// </summary>
            /// ---------------------------------------------------------------------
            internal DteReader(IServiceProvider services, Dispatcher owner)
            {
                if (owner == null) throw new ArgumentNullException("owner"); owner.VerifyAccess(); dispatcher = owner;
                dte = services == null ? null : services.GetService(typeof(DTE)) as DTE;
                if (dte == null) return;
                try { events = dte.Events.DebuggerEvents; events.OnEnterBreakMode += EnterBreak; events.OnEnterRunMode += EnterMode; events.OnEnterDesignMode += EnterMode; events.OnContextChanged += ContextChanged; }
                catch { Dispose(); throw; }
            }

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : ReadCurrent
            /// desc         : 현재 break frame 위치와 선택 시에만 열린 문서·스택 함수명3개를 읽는다.
            /// author       : CodexOrig
            /// create date  : 2026-10-20
            /// update date  : 2026-10-20 CodexOrig : 디버거 값/평가/실행 API는 호출하지 않는다.
            /// </summary>
            /// ---------------------------------------------------------------------
            public DebuggingFrameData ReadCurrent(bool includeDetails)
            {
                dispatcher.VerifyAccess();
                if (disposed || dte == null || dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode) return new DebuggingFrameData();
                var frame = dte.Debugger.CurrentStackFrame; var location = frame as IFrameLocation;
                if (frame == null || location == null || location.LineNumber == 0 || location.LineNumber > Int32.MaxValue) return new DebuggingFrameData();
                var data = new DebuggingFrameData { IsBreakMode = true, FilePath = location.FileName, LineNumber = (int)location.LineNumber, FunctionName = frame.FunctionName };
                if (!includeDetails || !ValidLocation(data)) return data;
                TextDocument text = null; int inspected = 0;
                foreach (Document document in dte.Documents)
                {
                    if (++inspected > 128) break;
                    if (String.Equals(document.FullName, data.FilePath, StringComparison.OrdinalIgnoreCase)) { text = document.Object("TextDocument") as TextDocument; break; }
                }
                if (text == null) throw new InvalidOperationException("현재 중단 위치의 소스 파일이 편집기에 열려 있어야 합니다.");
                data.SourceStartLine = Math.Max(1, data.LineNumber - 3);
                var point = text.StartPoint.CreateEditPoint(); point.MoveToLineAndOffset(data.SourceStartLine, 1);
                int count = Math.Min(3000, Math.Max(0, text.EndPoint.AbsoluteCharOffset - point.AbsoluteCharOffset));
                data.SourceLines = point.GetText(count).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Take(7).ToArray();
                var names = new System.Collections.Generic.List<string>(); var thread = dte.Debugger.CurrentThread;
                if (thread != null) for (int index = 1; index <= 3; index++)
                {
                    try { var item = thread.StackFrames.Item(index); if (item == null) break; names.Add(OneLine(item.FunctionName, 240)); }
                    catch (COMException) { break; }
                    catch (ArgumentException) { break; }
                }
                data.StackFunctions = names.ToArray(); return data;
            }

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : EnterBreak
            /// desc         : 중단 진입을 알리고 실행 동작 참조값은 변경하지 않는다.
            /// author       : CodexOrig
            /// create date  : 2026-10-20
            /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
            /// </summary>
            /// ---------------------------------------------------------------------
            private void EnterBreak(dbgEventReason reason, ref dbgExecutionAction action) { Notify(); }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : EnterMode
            /// desc         : 실행·디자인 모드 변경을 알린다.
            /// author       : CodexOrig
            /// create date  : 2026-10-20
            /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
            /// </summary>
            /// ---------------------------------------------------------------------
            private void EnterMode(dbgEventReason reason) { Notify(); }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : ContextChanged
            /// desc         : 현재 중단 frame 변경을 알린다.
            /// author       : CodexOrig
            /// create date  : 2026-10-20
            /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
            /// </summary>
            /// ---------------------------------------------------------------------
            private void ContextChanged(EnvDTE.Process process, Program program, EnvDTE.Thread thread, StackFrame frame) { Notify(); }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : Notify
            /// desc         : 상태 알림을 소유 WPF dispatcher로 제한한다.
            /// author       : CodexOrig
            /// create date  : 2026-10-20
            /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
            /// </summary>
            /// ---------------------------------------------------------------------
            private void Notify()
            {
                if (disposed) return;
                if (!dispatcher.CheckAccess())
                {
#pragma warning disable VSTHRD001
                    var pendingNotification = dispatcher.BeginInvoke(new Action(Notify), DispatcherPriority.Background);
#pragma warning restore VSTHRD001
                    return;
                }
                var handler = StateChanged; if (handler != null) handler(this, EventArgs.Empty);
            }

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : Dispose
            /// desc         : DTE 이벤트 구독을 해제하고 디버거 상태는 보존한다.
            /// author       : CodexOrig
            /// create date  : 2026-10-20
            /// update date  : 2026-10-20 CodexOrig : TASK069 #18 명시 클릭·제한된 읽기·자기 수명 계약.
            /// </summary>
            /// ---------------------------------------------------------------------
            public void Dispose()
            {
                dispatcher.VerifyAccess(); if (disposed) return; disposed = true; if (events == null) return;
                try { events.OnEnterBreakMode -= EnterBreak; } catch (Exception error) when (error is COMException || error is InvalidComObjectException) { }
                try { events.OnEnterRunMode -= EnterMode; } catch (Exception error) when (error is COMException || error is InvalidComObjectException) { }
                try { events.OnEnterDesignMode -= EnterMode; } catch (Exception error) when (error is COMException || error is InvalidComObjectException) { }
                try { events.OnContextChanged -= ContextChanged; } catch (Exception error) when (error is COMException || error is InvalidComObjectException) { }
                events = null;
            }
        }
    }
}
