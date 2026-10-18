using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VS2017.AIAssistant.Providers;

namespace VS2017.AIAssistant.Services
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ScmIdentity
    /// desc         : 형상관리의 공개 이름과 실제 로그인 확인 여부를 구분하는 불변 결과다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 작성자·저장 계정·실제 로그인 확인을 각각 구분.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ScmIdentity
    {
        public string AccountName { get; private set; }
        public string Source { get; private set; }
        public bool AuthenticationVerified { get; private set; }
        public bool HasStoredAccount { get; private set; }
        public string Description { get; private set; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ScmIdentity
        /// desc         : 공개 이름과 고정된 출처·설명만 결과에 담는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 저장 계정 표시를 추가하고 원격 URL·토큰·CLI 오류는 보관하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal ScmIdentity(string accountName, string source, bool verified, string description, bool hasStoredAccount = false)
        { AccountName = accountName ?? ""; Source = source; AuthenticationVerified = verified; HasStoredAccount = hasStoredAccount; Description = description; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ScmIdentityService
    /// desc         : Git 작성자와 선택적인 GitHub 로그인·GCM 공개 계정 이름만 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 전체 5초·스트림별 4096자·취소·Job과 고정 읽기 명령 적용.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ScmIdentityService
    {
        private readonly string gitExecutable, ghExecutable, gcmExecutable;
        private readonly TimeSpan timeout;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ScmIdentityService
        /// desc         : Git/GH는 PATH, GCM은 Git 설치 경로에서 찾으며 빈 경로로 각 실행기를 끈다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 기존 생성자 뒤 GCM 실행기 주입 인수를 추가.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ScmIdentityService(string gitExecutable = null, string ghExecutable = null, TimeSpan? timeout = null, string gcmExecutable = null)
        {
            ValidateExecutableInput(gitExecutable); ValidateExecutableInput(ghExecutable); ValidateExecutableInput(gcmExecutable);
            this.gitExecutable = gitExecutable; this.ghExecutable = ghExecutable; this.gcmExecutable = gcmExecutable; this.timeout = timeout ?? TimeSpan.FromSeconds(5);
            if (this.timeout < TimeSpan.FromMilliseconds(20) || this.timeout > TimeSpan.FromSeconds(5)) throw new ArgumentOutOfRangeException(nameof(timeout), "계정 확인 제한 시간은 20ms~5초입니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadAsync
        /// desc         : UI를 막지 않고 프로젝트의 공개 이름을 조회하며 사용자 취소는 그대로 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : GitHub 로그인 확인 다음 GCM 공개 저장 계정만 조회하며 인증 여부를 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public Task<ScmIdentity> ReadAsync(string projectDirectory, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = ValidateDirectory(projectDirectory), author = "";
                string git = ResolveNative(gitExecutable, "git.exe");
                if (git == null) return Fallback("", "Git 실행 파일을 찾지 못했습니다.");
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    deadline.CancelAfter(timeout);
                    try
                    {
                        var repository = await RunAsync(git, new[] { "-C", directory, "rev-parse", "--is-inside-work-tree" }, directory, deadline.Token).ConfigureAwait(false);
                        if (repository.ExitCode != 0 || repository.Output.Trim() != "true") return Fallback("", "Git 작업 폴더가 아닙니다.");
                        var identity = await RunAsync(git, new[] { "-C", directory, "config", "--get", "user.name" }, directory, deadline.Token).ConfigureAwait(false);
                        if (identity.ExitCode == 0) author = ReadAuthor(identity.Output);
                        string gh = ResolveNative(ghExecutable, "gh.exe");
                        string gcm = ResolveGcmNative(git, null);
                        if (gh == null && gcm == null && gcmExecutable != null) return Fallback(author, "");
                        var origin = await RunAsync(git, new[] { "-C", directory, "remote", "get-url", "origin" }, directory, deadline.Token).ConfigureAwait(false);
                        if (origin.ExitCode != 0 || !IsPublicGitHubRemote(origin.Output)) return Fallback(author, "");
                        if (gh != null)
                        {
                            var login = await RunAsync(gh, new[] { "api", "user", "--jq", ".login", "--hostname", "github.com", "--method", "GET" }, directory, deadline.Token).ConfigureAwait(false);
                            string name = login.Output.Trim();
                            if (login.ExitCode == 0 && IsGitHubLogin(name)) return new ScmIdentity(name, "GitHub CLI", true, "GitHub CLI 로그인 확인 · github.com");
                        }
                        if (gcm == null && gcmExecutable == null)
                        {
                            var execPath = await RunAsync(git, new[] { "--exec-path" }, directory, deadline.Token).ConfigureAwait(false);
                            if (execPath.ExitCode == 0) gcm = ResolveGcmNative(git, execPath.Output.Trim());
                        }
                        if (gcm != null)
                        {
                            var stored = await RunAsync(gcm, new[] { "github", "list", "--no-ui" }, directory, deadline.Token).ConfigureAwait(false);
                            var accounts = stored.ExitCode == 0 ? ReadStoredAccounts(stored.Output) : new string[0];
                            if (accounts.Length > 0)
                            {
                                string selected = ReadRemoteUsername(origin.Output);
                                if (selected.Length == 0 && accounts.Length > 1)
                                {
                                    var configured = await RunAsync(git, new[] { "-C", directory, "config", "--get", "credential.https://github.com.username" }, directory, deadline.Token).ConfigureAwait(false);
                                    if (configured.ExitCode == 0 && IsGitHubLogin(configured.Output.Trim())) selected = configured.Output.Trim();
                                }
                                string account = selected.Length > 0 ? accounts.FirstOrDefault(x => String.Equals(x, selected, StringComparison.OrdinalIgnoreCase)) : accounts.Length == 1 ? accounts[0] : null;
                                if (account != null) return new ScmIdentity(account, "Git Credential Manager", false, "Git Credential Manager 저장 계정 · 서버 인증 미확인", true);
                                return Fallback(author, "저장된 GitHub 계정이 여러 개이거나 프로젝트 선택과 일치하지 않아 계정을 선택하지 않았습니다.");
                            }
                        }
                        return Fallback(author, "GitHub 로그인은 확인하지 못했습니다.");
                    }
                    catch (OperationCanceledException)
                    { cancellationToken.ThrowIfCancellationRequested(); return Fallback(author, "계정 확인 시간이 초과되었습니다."); }
                    catch (Exception error) when (error is IOException || error is Win32Exception || error is InvalidOperationException || error is UnauthorizedAccessException)
                    { cancellationToken.ThrowIfCancellationRequested(); return Fallback(author, "계정 이름 확인을 완료하지 못했습니다."); }
                }
            }, cancellationToken);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Fallback
        /// desc         : Git 작성자 설정은 인증을 확인한 로그인 계정과 다른 출처로 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 확인 실패에도 공개 이름을 유지하고 비밀 출력은 배제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static ScmIdentity Fallback(string author, string detail)
        {
            bool exists = !String.IsNullOrEmpty(author);
            string description = exists ? "Git 작성자 설정 · 로그인 미확인" : "형상관리 계정 이름을 확인하지 못했습니다.";
            if (!String.IsNullOrEmpty(detail)) description += " · " + detail;
            return new ScmIdentity(author, exists ? "Git 작성자 설정" : "없음", false, description);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadAuthor
        /// desc         : 단일 줄의 공개 작성자 이름만 허용하고 과도한 값과 제어 문자를 배제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 한글·공백 이름 지원과 출력 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ReadAuthor(string output)
        {
            string value = output.Trim();
            return value.Length == 0 || value.Length > 96 || value.Any(Char.IsControl) || value.Contains("://") ? "" : value;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsPublicGitHubRemote
        /// desc         : origin 문자열은 메모리에서만 해석하고 정확한 github.com 원격만 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 사용자 정보·토큰·임의 호스트를 gh 인수나 결과에 복사하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsPublicGitHubRemote(string output)
        {
            string value = output.Trim(); if (value.Length > 4096 || value.Any(Char.IsControl)) return false;
            Uri uri;
            if (Uri.TryCreate(value, UriKind.Absolute, out uri) && (uri.Scheme == "https" || uri.Scheme == "ssh"))
                return String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) && !String.IsNullOrEmpty(uri.AbsolutePath.Trim('/'));
            return Regex.IsMatch(value, @"\A(?:[^@/\s]+@)?github\.com:[^\s\\]+\z", RegexOptions.IgnoreCase);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsGitHubLogin
        /// desc         : GitHub 공개 계정 이름의 단일 토큰 형식만 허용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : URL·키 값·JSON·제어 문자와 과도한 길이를 배제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsGitHubLogin(string value)
        { return Regex.IsMatch(value, @"\A[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?\z"); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadStoredAccounts
        /// desc         : GCM list의 줄별 공개 이름만 읽고 형식이 섞이면 전체 목록을 버린다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 토큰·암호 형태 출력은 보관하거나 결과에 복사하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string[] ReadStoredAccounts(string output)
        {
            var names = new List<string>();
            foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = line.Trim(); if (name.Length == 0) continue;
                if (!IsGitHubLogin(name)) return new string[0];
                if (!names.Contains(name, StringComparer.OrdinalIgnoreCase)) names.Add(name);
            }
            return names.ToArray();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadRemoteUsername
        /// desc         : 검증된 GitHub HTTPS 원격에서 사용자 이름만 선택 힌트로 분리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 암호와 SSH 전송 사용자 git은 계정 선택에 사용하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ReadRemoteUsername(string output)
        {
            Uri uri;
            if (!Uri.TryCreate(output.Trim(), UriKind.Absolute, out uri) || uri.Scheme != "https" || uri.UserInfo.Length == 0) return "";
            string name = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]);
            return IsGitHubLogin(name) ? name : "";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResolveGcmNative
        /// desc         : Git 설치 경로 또는 Git execdir의 고정 네이티브 GCM 파일만 탐색한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Git 별칭·셸·임의 PATH의 credential-manager를 실행하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private string ResolveGcmNative(string git, string execDirectory)
        {
            if (gcmExecutable != null) return ResolveNative(gcmExecutable, "git-credential-manager.exe");
            string directory = Path.GetDirectoryName(git);
            var candidates = new List<string> { Path.Combine(directory, "git-credential-manager.exe") };
            string leaf = Path.GetFileName(directory);
            if (String.Equals(leaf, "cmd", StringComparison.OrdinalIgnoreCase) || String.Equals(leaf, "bin", StringComparison.OrdinalIgnoreCase))
            {
                string install = Path.GetDirectoryName(directory);
                candidates.Add(Path.Combine(install, "mingw64", "bin", "git-credential-manager.exe"));
                candidates.Add(Path.Combine(install, "mingw32", "bin", "git-credential-manager.exe"));
            }
            if (!String.IsNullOrEmpty(execDirectory) && !execDirectory.Any(Char.IsControl) && Path.IsPathRooted(execDirectory) && !execDirectory.StartsWith(@"\\", StringComparison.Ordinal))
                candidates.Add(Path.Combine(execDirectory, "git-credential-manager.exe"));
            return candidates.FirstOrDefault(File.Exists);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidateDirectory
        /// desc         : 명령 옵션이 될 수 없는 실제 로컬 절대 폴더만 조회 대상으로 정규화한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : UNC 조회 지연과 제어 문자를 차단하며 셸은 사용하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ValidateDirectory(string value)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Any(Char.IsControl) || !Path.IsPathRooted(value) || value.StartsWith(@"\\", StringComparison.Ordinal))
                throw new ArgumentException("로컬 Git 프로젝트의 절대 폴더 경로를 지정하세요.");
            string path = Path.GetFullPath(value);
            if (!Directory.Exists(path)) throw new ArgumentException("Git 프로젝트 폴더가 없습니다.");
            return path;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidateExecutableInput
        /// desc         : 배치·셸 래퍼와 제어 문자를 명시적으로 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 사용자 지정 테스트 실행기도 네이티브 exe로 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ValidateExecutableInput(string value)
        {
            if (String.IsNullOrEmpty(value)) return;
            if (value.Any(Char.IsControl) || !String.Equals(Path.GetExtension(value), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("네이티브 .exe 실행 파일만 사용할 수 있습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResolveNative
        /// desc         : 명시한 절대 exe 또는 PATH의 절대 폴더에서 실행 파일을 찾는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 프로젝트 폴더의 동명 실행 파일을 자동 실행하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ResolveNative(string configured, string fallback)
        {
            if (configured == "") return null;
            if (configured != null)
            {
                if (!Path.IsPathRooted(configured)) throw new ArgumentException("CLI 실행 파일은 절대 경로로 지정하세요.");
                string path = Path.GetFullPath(configured); return File.Exists(path) ? path : null;
            }
            foreach (string entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                string directory = entry.Trim().Trim('"'); if (directory.Length == 0 || !Path.IsPathRooted(directory)) continue;
                try { string path = Path.Combine(directory, fallback); if (File.Exists(path)) return Path.GetFullPath(path); }
                catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is System.Security.SecurityException) { }
            }
            return null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunAsync
        /// desc         : 고정 네이티브 명령의 출력을 제한하고 종료·취소 시 자식 프로세스를 정리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 인증 대화 비활성화·원격 원문 오류 폐기·Job 수명 적용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task<CommandResult> RunAsync(string executable, string[] arguments, string directory, CancellationToken token)
        {
            using (var process = new Process())
            using (var job = new ProcessJob())
            {
                token.ThrowIfCancellationRequested();
                process.StartInfo = new ProcessStartInfo(executable, String.Join(" ", arguments.Select(ProviderExecutionOptions.QuoteArgument)))
                {
                    WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false)
                };
                process.StartInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                process.StartInfo.EnvironmentVariables["GCM_INTERACTIVE"] = "Never";
                process.StartInfo.EnvironmentVariables["GH_PROMPT_DISABLED"] = "1";
                var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                process.EnableRaisingEvents = true; process.Exited += (s, e) => exited.TrySetResult(true);
                Action stop = () => { job.Dispose(); try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } catch (Win32Exception) { } };
                try
                {
                    process.Start(); job.Attach(process);
                    using (token.Register(stop))
                    {
                        var stdout = ReadOutputAsync(process.StandardOutput, stop);
                        var stderr = ReadOutputAsync(process.StandardError, stop);
                        await Task.WhenAll(stdout, stderr, exited.Task).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        return new CommandResult { ExitCode = process.ExitCode, Output = await stdout.ConfigureAwait(false) };
                    }
                }
                finally { stop(); }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadOutputAsync
        /// desc         : stdout와 stderr를 동시에 비우되 각 4096자 초과 시 즉시 중단한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 원문은 외부 예외나 설명 메시지에 넣지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task<string> ReadOutputAsync(StreamReader reader, Action stop)
        {
            var output = new StringBuilder(); var buffer = new char[1024]; int count;
            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                if (output.Length + count > 4096) { stop(); throw new InvalidOperationException("계정 확인 출력이 제한을 초과했습니다."); }
                output.Append(buffer, 0, count);
            }
            return output.ToString();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CommandResult
        /// desc         : 내부 명령의 종료 코드와 제한된 표준 출력만 잠시 보관한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 오류 스트림·원격 URL을 공개 결과에서 제외.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class CommandResult { public int ExitCode; public string Output; }
    }
}
