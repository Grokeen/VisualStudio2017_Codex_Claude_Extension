using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VS2017.AIAssistant.Providers
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : NativeJsonSession
    /// desc         : 프로세스 수명과 크기 제한을 가진 stdin/stdout JSON RPC 연결이다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 네이티브 CLI 프로토콜·취소·크기 경계.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class NativeJsonSession : IDisposable
    {
        private readonly Process process = new Process();
        private readonly ProcessJob job = new ProcessJob();
        private readonly SemaphoreSlim writes = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IDictionary<string, object>>> pending = new ConcurrentDictionary<string, TaskCompletionSource<IDictionary<string, object>>>();
        private int sequence;
        private volatile bool disposed;
        private Task outputTask, errorTask;
        internal event Action<IDictionary<string, object>> Notification;
        internal Task Completion { get { return Task.WhenAll(outputTask ?? Task.CompletedTask, errorTask ?? Task.CompletedTask); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NativeJsonSession
        /// desc         : 선택한 네이티브 CLI만 숨김으로 실행하고 자식 프로세스도 같은 Job에 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 네이티브 CLI 프로토콜·취소·크기 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal NativeJsonSession(string executable, IEnumerable<string> arguments, string directory)
        {
            process.StartInfo = new ProcessStartInfo(executable, String.Join(" ", arguments.Select(Quote)))
            {
                WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false)
            };
            try {
                process.Start(); job.Attach(process);
                outputTask = ReadAsync();
                errorTask = DrainErrorsAsync();
            }
            catch { Dispose(); throw; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Quote
        /// desc         : Windows CreateProcess 인수를 따옴표와 후행 역슬래시까지 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 네이티브 CLI 프로토콜·취소·크기 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Quote(string value)
        {
            if (value == null) value = "";
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value) {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { result.Append('\\', slashes * 2 + 1).Append(c); slashes = 0; continue; }
                result.Append('\\', slashes).Append(c); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SendAsync
        /// desc         : 한 JSON 요청 전체를 stdin으로 보내 프롬프트를 명령줄에 노출하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 네이티브 CLI 프로토콜·취소·크기 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal async Task SendAsync(object message, CancellationToken cancellation)
        {
            await writes.WaitAsync(cancellation).ConfigureAwait(false);
            try {
                if (disposed) throw new ObjectDisposedException("NativeJsonSession");
                string json = RuntimeJson.Serialize(message) + "\n";
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                await process.StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length, cancellation).ConfigureAwait(false);
                await process.StandardInput.BaseStream.FlushAsync(cancellation).ConfigureAwait(false);
            }
            finally { writes.Release(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CallAsync
        /// desc         : 응답 ID를 연결하고 취소·종료 시 대기 요청을 남기지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 네이티브 CLI 프로토콜·취소·크기 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal Task<IDictionary<string, object>> CallAsync(string method, object parameters, CancellationToken cancellation)
        { return RequestAsync(method, parameters, false, cancellation); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ControlAsync
        /// desc         : Claude SDK의 제어 요청 envelope를 같은 수명 관리로 전달한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 네이티브 CLI 프로토콜·취소·크기 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal Task<IDictionary<string, object>> ControlAsync(string subtype, CancellationToken cancellation)
        { return RequestAsync(subtype, null, true, cancellation); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequestAsync
        /// desc         : 프로토콜별 요청 ID와 응답을 정확히 일대일로 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 네이티브 CLI 프로토콜·취소·크기 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task<IDictionary<string, object>> RequestAsync(string method, object parameters, bool control, CancellationToken cancellation)
        {
            string id = Interlocked.Increment(ref sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var completion = new TaskCompletionSource<IDictionary<string, object>>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[id] = completion;
            using (cancellation.Register(() => completion.TrySetCanceled())) {
                try {
                    if (control) await SendAsync(new { type = "control_request", request_id = id, request = new { subtype = method } }, cancellation).ConfigureAwait(false);
                    else await SendAsync(new { id, method, @params = parameters ?? new { } }, cancellation).ConfigureAwait(false);
                    return await completion.Task.ConfigureAwait(false);
                }
                finally { TaskCompletionSource<IDictionary<string, object>> removed; pending.TryRemove(id, out removed); }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadAsync
        /// desc         : 줄 길이와 누적 출력 제한을 적용하고 완성된 JSON만 처리한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 네이티브 CLI 프로토콜·취소·크기 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task ReadAsync()
        {
            Exception failure = null;
            try {
                char[] buffer = new char[4096]; var line = new StringBuilder(); int total = 0, count;
                while ((count = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0) {
                    total += count; if (total > 32 * 1024 * 1024) throw new InvalidDataException("CLI 출력이 32MB 제한을 초과했습니다.");
                    for (int i = 0; i < count; i++) {
                        if (buffer[i] == '\n') { if (line.Length > 0) Route(line.ToString().TrimEnd('\r')); line.Clear(); }
                        else { line.Append(buffer[i]); if (line.Length > 2 * 1024 * 1024) throw new InvalidDataException("CLI JSON 한 줄이 제한을 초과했습니다."); }
                    }
                }
                if (line.Length > 0) Route(line.ToString().TrimEnd('\r'));
                failure = new IOException("CLI 프로토콜 연결이 종료되었습니다.");
            }
            catch (Exception error) { failure = error; }
            finally {
                foreach (var request in pending.Values) request.TrySetException(failure ?? new IOException("CLI 연결 종료"));
                if (!disposed) { var callback = Notification; if (callback != null) callback(new Dictionary<string, object> { { "method", "connection/closed" } }); }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DrainErrorsAsync
        /// desc         : 표준 오류를 제한 크기 안에서 소모하며 인증 출력은 UI와 기록에 복제하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 네이티브 CLI 프로토콜·취소·크기 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task DrainErrorsAsync()
        {
            try { char[] buffer = new char[4096]; int total = 0, count;
                while ((count = await process.StandardError.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    if ((total += count) > 2 * 1024 * 1024) { job.Dispose(); break; }
            } catch (IOException) { } catch (ObjectDisposedException) { }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Route
        /// desc         : RPC 오류와 Claude 제어 오류를 성공 응답과 구분하고 이벤트는 별도로 전달한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 네이티브 CLI 프로토콜·취소·크기 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Route(string json)
        {
            var message = RuntimeJson.Parse(json);
            string id = Convert.ToString(RuntimeJson.Get(message, "id"), System.Globalization.CultureInfo.InvariantCulture);
            bool control = RuntimeJson.Text(message, "type") == "control_response";
            var response = control ? RuntimeJson.Child(message, "response") : message;
            if (control) id = RuntimeJson.Text(response, "request_id");
            TaskCompletionSource<IDictionary<string, object>> completion;
            bool isResponse = control || (RuntimeJson.Get(message, "method") == null && (message.ContainsKey("result") || message.ContainsKey("error")));
            if (isResponse && id.Length > 0 && pending.TryGetValue(id, out completion)) {
                object error = RuntimeJson.Get(response, "error");
                if (error != null || RuntimeJson.Text(response, "subtype") == "error")
                    completion.TrySetException(new InvalidOperationException("CLI가 요청을 거부했습니다. 계정·모델·지원 버전을 확인하세요. 서버 오류 원문은 대화에 저장하지 않습니다."));
                else completion.TrySetResult(RuntimeJson.Object(RuntimeJson.Get(response, control ? "response" : "result")));
                return;
            }
            if (!disposed) { var callback = Notification; if (callback != null) callback(message); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 이 연결의 Job과 요청만 종료하며 다른 CLI·IDE 프로세스는 건드리지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 네이티브 CLI 프로토콜·취소·크기 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var request in pending.Values) request.TrySetException(new ObjectDisposedException("NativeJsonSession"));
            job.Dispose();
            try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
            process.Dispose();
            // 실행 중 write finally의 Release와 경쟁하지 않도록 Semaphore는 GC가 해제한다.
        }
    }
}

