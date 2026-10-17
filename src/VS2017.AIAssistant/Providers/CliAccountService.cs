using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VS2017.AIAssistant.Providers
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CliAccountService
    /// desc         : 공식 CLI의 로그인과 상태 확인에 연결하고 인증 정보는 저장하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : Codex login / Claude auth 및 제한된 상태 프로세스.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class CliAccountService
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BeginLogin
        /// desc         : 사용자 클릭에 따라 인증을 직접 수행할 공식 CLI 창을 연다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 셸 명령 조합 없이 고정 인수로 실행.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void BeginLogin(string provider, string executable)
        {
            string path = Resolve(provider, executable);
            Process.Start(new ProcessStartInfo(path, provider == "Codex" ? "login" : "auth login") { UseShellExecute = true });
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckAsync
        /// desc         : 계정 상세 출력은 노출하지 않고 종료 코드로 연결 여부를 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 15초 제한·출력 상한·취소·Job 정리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public Task<bool> CheckAsync(string provider, string executable, CancellationToken cancellationToken)
        {
            string path = Resolve(provider, executable);
            return Task.Run(async () =>
            {
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                using (var process = new Process())
                using (var job = new ProcessJob())
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(15));
                    process.StartInfo = new ProcessStartInfo(path, provider == "Codex" ? "login status" : "auth status")
                    {
                        UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                    };
                    var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    process.EnableRaisingEvents = true;
                    process.Exited += (s, e) => exited.TrySetResult(true);
                    deadline.Token.ThrowIfCancellationRequested();
                    try
                    {
                        process.Start(); job.Attach(process);
                        using (deadline.Token.Register(() => job.Dispose()))
                        {
                            await Task.WhenAll(CliEditProvider.ReadBoundedAsync(process.StandardOutput, 32768, job.Dispose),
                                CliEditProvider.ReadBoundedAsync(process.StandardError, 32768, job.Dispose), exited.Task).ConfigureAwait(false);
                            deadline.Token.ThrowIfCancellationRequested();
                            if (process.ExitCode != 0 && process.ExitCode != 1) throw new InvalidOperationException("CLI가 계정 상태 확인을 지원하는지 확인하세요.");
                            return process.ExitCode == 0;
                        }
                    }
                    finally { CliEditProvider.TryKill(process); }
                }
            }, cancellationToken);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Resolve
        /// desc         : 지원 Provider와 네이티브 실행 파일만 허용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 요청 경로와 동일한 CLI 검증 재사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Resolve(string provider, string executable)
        {
            if (provider != "Codex" && provider != "Claude") throw new ArgumentException("지원하지 않는 Provider입니다.");
            return CliEditProvider.ResolveExecutable(executable, provider == "Codex" ? "codex.exe" : "claude.exe");
        }
    }
}
