using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VS2017.AIAssistant.Providers;

namespace VS2017.AIAssistant.Services
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WorkspaceOperation
    /// desc         : 사용자가 실행 계획을 확인한 뒤 선택할 수 있는 형상 관리와 배포 작업.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : Git·SVN·TFVC의 명시적 작업 종류 정의.
    /// </summary>
    /// ---------------------------------------------------------------------
    public enum WorkspaceOperation { GitFetch, GitPull, GitCheckout, GitCommit, GitPush, SvnUpdate, SvnCheckout, SvnCommit, TfsGet, TfsCheckout, TfsCheckin, Deploy }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WorkspacePermissionPolicy
    /// desc         : 형상 관리·DB 스키마 읽기·배포의 별도 허용 여부. 기본은 모두 해제.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : AI의 읽기 전용 실행과 별개인 명시적 작업 권한.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class WorkspacePermissionPolicy
    {
        public bool AllowSourceControl { get; set; }
        public bool AllowDatabaseSchema { get; set; }
        public bool AllowDeployment { get; set; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WorkspaceActionRequest
    /// desc         : 프로젝트 경로·실행 파일·브랜치·메시지·명시적 파일 목록을 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 배포 인수는 문자열 배열로 받아 셸 해석을 배제.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class WorkspaceActionRequest
    {
        public WorkspaceOperation Operation { get; set; }
        public string ProjectDirectory { get; set; }
        public string ExecutablePath { get; set; }
        public string Argument { get; set; }
        public string Message { get; set; }
        public string DestinationDirectory { get; set; }
        public string[] Files { get; set; }
        public string[] DeploymentArguments { get; set; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WorkspaceActionPlan
    /// desc         : 검증을 마친 정확한 작업 디렉터리·실행 파일·인수를 불변 스냅샷으로 보관한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실행 직전 확인 화면과 실제 프로세스 입력 일치.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class WorkspaceActionPlan
    {
        public string TargetDirectory { get; private set; }
        public string ExecutablePath { get; private set; }
        public string Arguments { get; private set; }
        public string DisplayCommand { get { return QuoteArgument(ExecutablePath) + " " + Arguments; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WorkspaceActionPlan
        /// desc         : 검증한 실행 계획을 생성하며 호출자가 인수를 다시 바꾸지 못하게 한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 미리보기와 실행의 공통 모델.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal WorkspaceActionPlan(string directory, string executable, IEnumerable<string> arguments)
        {
            TargetDirectory = directory; ExecutablePath = executable; Arguments = string.Join(" ", arguments.Select(QuoteArgument));
            if (Arguments.Length + ExecutablePath.Length > 30000) throw new ArgumentException("명령 전체 길이가 제한을 초과했습니다. 파일 또는 인수 개수를 줄이세요.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : QuoteArgument
        /// desc         : Windows argv 규칙에 따라 공백·따옴표·끝 역슬래시를 손실 없이 인코딩한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 셸 구문을 실행하지 않는 네이티브 인수 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string QuoteArgument(string value)
        {
            if (value == null || value.IndexOf('\0') >= 0) throw new ArgumentException("인수에 NUL 문자를 사용할 수 없습니다.");
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char ch in value)
            {
                if (ch == '\\') { slashes++; continue; }
                if (ch == '"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                result.Append(ch); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WorkspaceActionResult
    /// desc         : 작업 종료 코드와 크기가 제한된 표준 출력·오류를 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 성공은 종료 코드 0으로 판단.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class WorkspaceActionResult
    {
        public int ExitCode { get; internal set; }
        public string Output { get; internal set; }
        public string Error { get; internal set; }
        public bool Succeeded { get { return ExitCode == 0; } }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WorkspaceActionService
    /// desc         : 명시적 권한과 검토를 거친 CLI 작업·SQL Server 스키마 읽기만 수행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 프로젝트 경로 검사·출력 제한·취소·시간 제한 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class WorkspaceActionService
    {
        private readonly TimeSpan timeout;
        private const int OutputLimit = 131072;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WorkspaceActionService
        /// desc         : 네트워크 작업을 포함한 단일 작업의 기본 제한 시간을 2분으로 지정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 테스트에서 더 짧은 제한 시간 주입 지원.
        /// </summary>
        /// ---------------------------------------------------------------------
        public WorkspaceActionService(TimeSpan? timeout = null)
        {
            this.timeout = timeout ?? TimeSpan.FromMinutes(2);
            if (this.timeout <= TimeSpan.Zero || this.timeout > TimeSpan.FromMinutes(30)) throw new ArgumentOutOfRangeException("timeout");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BuildPlan
        /// desc         : 권한·실행 파일·대상 경로를 검사하여 실행하지 않고 검토용 명령을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 커밋은 지정된 추적 파일에 한정하며 강제 갱신 금지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public WorkspaceActionPlan BuildPlan(WorkspaceActionRequest request, WorkspacePermissionPolicy policy)
        {
            if (request == null || policy == null) throw new ArgumentNullException(request == null ? "request" : "policy");
            if (!Enum.IsDefined(typeof(WorkspaceOperation), request.Operation)) throw new ArgumentException("지원하지 않는 작업입니다.");
            if (request.Operation == WorkspaceOperation.Deploy ? !policy.AllowDeployment : !policy.AllowSourceControl)
                throw new InvalidOperationException("먼저 이 작업의 권한을 허용하세요.");
            string directory = Path.GetFullPath(Required(request.ProjectDirectory, "프로젝트 폴더", 32700));
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("프로젝트 폴더가 없습니다.");
            RejectReparsePoints(directory);
            bool isTfs = request.Operation == WorkspaceOperation.TfsGet || request.Operation == WorkspaceOperation.TfsCheckout || request.Operation == WorkspaceOperation.TfsCheckin;
            if (isTfs && (directory.Contains(";") || (request.Files != null && request.Files.Any(f => f != null && f.Contains(";")))))
                throw new ArgumentException("TFVC 파일 경로에는 버전 구분자인 세미콜론을 사용할 수 없습니다.");
            var args = new List<string>(); string tool;
            switch (request.Operation)
            {
                case WorkspaceOperation.GitFetch: tool = "git.exe"; args.Add("fetch"); AddRemote(args, request.Argument); break;
                case WorkspaceOperation.GitPull: tool = "git.exe"; args.AddRange(new[] { "pull", "--ff-only" }); AddRemote(args, request.Argument); break;
                case WorkspaceOperation.GitCheckout:
                    tool = "git.exe"; string branch = Required(request.Argument, "브랜치 이름", 200);
                    if (branch.StartsWith("-", StringComparison.Ordinal) || branch.Any(char.IsWhiteSpace) || branch.IndexOfAny(new[] { '*', '?', '[', '\\', '~', '^', ':', '@' }) >= 0 || branch.Contains("..") || branch.EndsWith("/", StringComparison.Ordinal))
                        throw new ArgumentException("명시적인 기존 브랜치 이름을 입력하세요.");
                    args.AddRange(new[] { "checkout", branch, "--" }); break;
                case WorkspaceOperation.GitCommit:
                    tool = "git.exe"; args.AddRange(new[] { "--literal-pathspecs", "commit", "--only", "-m", Required(request.Message, "커밋 메시지", 4000), "--" });
                    args.AddRange(ValidateFiles(directory, request.Files)); break;
                case WorkspaceOperation.GitPush: tool = "git.exe"; args.Add("push"); AddRemote(args, request.Argument); break;
                case WorkspaceOperation.SvnUpdate: tool = "svn.exe"; args.AddRange(new[] { "update", "--non-interactive", "--no-auth-cache", "--", directory.Contains("@") ? directory + "@" : directory }); break;
                case WorkspaceOperation.SvnCheckout:
                    tool = "svn.exe"; Uri uri;
                    if (!Uri.TryCreate(Required(request.Argument, "SVN 저장소 URL", 2048), UriKind.Absolute, out uri) || !(new[] { "https", "http", "svn", "svn+ssh", "file" }).Contains(uri.Scheme) || !string.IsNullOrEmpty(uri.UserInfo))
                        throw new ArgumentException("자격 증명을 제외한 SVN 저장소 URL을 입력하세요.");
                    string destination = ResolveChild(directory, Required(request.DestinationDirectory, "새 체크아웃 폴더", 1000));
                    if (Directory.Exists(destination) || File.Exists(destination)) throw new ArgumentException("체크아웃 대상은 아직 존재하지 않는 하위 폴더여야 합니다.");
                    args.AddRange(new[] { "checkout", "--non-interactive", "--no-auth-cache", "--", uri.AbsoluteUri.Contains("@") ? uri.AbsoluteUri + "@" : uri.AbsoluteUri, destination }); break;
                case WorkspaceOperation.SvnCommit:
                    tool = "svn.exe"; args.AddRange(new[] { "commit", "--non-interactive", "--no-auth-cache", "--message", Required(request.Message, "체크인 메시지", 4000), "--" });
                    args.AddRange(ValidateFiles(directory, request.Files).Select(p => p.Contains("@") ? p + "@" : p)); break;
                case WorkspaceOperation.TfsGet: tool = "tf.exe"; args.AddRange(new[] { "get", directory, "/recursive", "/noautoresolve", "/noprompt" }); break;
                case WorkspaceOperation.TfsCheckout:
                    tool = "tf.exe"; args.Add("checkout"); args.AddRange(ValidateFiles(directory, request.Files)); break;
                case WorkspaceOperation.TfsCheckin:
                    tool = "tf.exe"; args.Add("checkin"); args.AddRange(ValidateFiles(directory, request.Files));
                    string comment = Required(request.Message, "체크인 메시지", 4000);
                    if (comment.StartsWith("@", StringComparison.Ordinal)) throw new ArgumentException("TFVC 메시지는 파일 참조 기호인 @로 시작할 수 없습니다.");
                    args.AddRange(new[] { "/comment:" + comment, "/noautoresolve", "/noprompt" }); break;
                case WorkspaceOperation.Deploy:
                    tool = Required(request.ExecutablePath, "배포 실행 파일", 32700);
                    if (request.DeploymentArguments != null)
                    {
                        if (request.DeploymentArguments.Length > 64) throw new ArgumentException("배포 인수는 최대 64개입니다.");
                        foreach (string arg in request.DeploymentArguments)
                        {
                            if (arg == null || arg.Length > 8000 || arg.Any(c => c == '\0' || c == '\r' || c == '\n')) throw new ArgumentException("배포 인수는 각 줄에 한 개씩 입력하세요.");
                            args.Add(arg);
                        }
                    }
                    break;
                default: throw new ArgumentException("지원하지 않는 작업입니다.");
            }
            return new WorkspaceActionPlan(directory, ResolveExecutable(string.IsNullOrWhiteSpace(request.ExecutablePath) ? tool : request.ExecutablePath), args);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunAsync
        /// desc         : 입력을 다시 검증하고 확인된 네이티브 작업을 취소 가능한 Job 안에서 실행한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 셸·대화형 암호 입력 금지, 프로세스 트리 종료.
        /// </summary>
        /// ---------------------------------------------------------------------
        public async Task<WorkspaceActionResult> RunAsync(WorkspaceActionRequest request, WorkspacePermissionPolicy policy, CancellationToken cancellationToken)
        {
            var plan = BuildPlan(request, policy); cancellationToken.ThrowIfCancellationRequested();
            var info = new ProcessStartInfo(plan.ExecutablePath, plan.Arguments) {
                WorkingDirectory = plan.TargetDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            foreach (string name in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES" }) info.EnvironmentVariables.Remove(name);
            info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0"; info.EnvironmentVariables["GCM_INTERACTIVE"] = "Never";
            using (var process = new Process { StartInfo = info })
            using (var job = new ProcessJob())
            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutSource.CancelAfter(timeout);
                try
                {
                    if (!process.Start()) throw new InvalidOperationException("작업 프로세스를 시작하지 못했습니다.");
                    job.Attach(process); process.StandardInput.Close();
                    Task<string> output = ReadBoundedAsync(process.StandardOutput); Task<string> error = ReadBoundedAsync(process.StandardError);
                    while (!process.HasExited)
                    {
                        timeoutSource.Token.ThrowIfCancellationRequested();
                        if (output.IsFaulted) await output.ConfigureAwait(false);
                        if (error.IsFaulted) await error.ConfigureAwait(false);
                        await Task.Delay(50, timeoutSource.Token).ConfigureAwait(false);
                    }
                    Task completed = Task.WhenAll(output, error);
                    if (await Task.WhenAny(completed, Task.Delay(Timeout.Infinite, timeoutSource.Token)).ConfigureAwait(false) != completed)
                        timeoutSource.Token.ThrowIfCancellationRequested();
                    await completed.ConfigureAwait(false);
                    return new WorkspaceActionResult { ExitCode = process.ExitCode, Output = await output.ConfigureAwait(false), Error = await error.ConfigureAwait(false) };
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested) throw;
                    throw new TimeoutException("작업 제한 시간을 초과했습니다. 원격 작업 상태를 확인한 뒤 다시 시도하세요.");
                }
                finally { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadSqlSchemaAsync
        /// desc         : 별도 DB 권한을 확인하고 SQL Server의 테이블·열 정의만 제한적으로 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 데이터·SQL 실행 없이 쿼리 작성용 스키마 반환, 암호 저장 안 함.
        /// </summary>
        /// ---------------------------------------------------------------------
        public async Task<string> ReadSqlSchemaAsync(string connectionString, WorkspacePermissionPolicy policy, CancellationToken cancellationToken)
        {
            if (policy == null || !policy.AllowDatabaseSchema) throw new InvalidOperationException("먼저 DB 스키마 읽기 권한을 허용하세요.");
            var builder = new SqlConnectionStringBuilder(Required(connectionString, "SQL Server 연결 문자열", 8000));
            builder.ConnectTimeout = 10; builder.PersistSecurityInfo = false; builder.ApplicationName = "VS2017 AI Schema Reader"; builder.Enlist = false;
            if (string.IsNullOrWhiteSpace(builder.DataSource) || string.IsNullOrWhiteSpace(builder.InitialCatalog)) throw new ArgumentException("서버와 데이터베이스를 지정하세요.");
            if (!string.IsNullOrEmpty(builder.AttachDBFilename) || builder.UserInstance || builder.ContextConnection) throw new ArgumentException("기존 SQL Server 데이터베이스 연결을 사용하세요. 파일 연결·사용자 인스턴스는 지원하지 않습니다.");
            const string query = "SELECT TOP (512) TABLE_SCHEMA,TABLE_NAME,COLUMN_NAME,DATA_TYPE,IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS ORDER BY TABLE_SCHEMA,TABLE_NAME,ORDINAL_POSITION";
            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = new SqlCommand(query, connection) { CommandTimeout = 15 })
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (cancellationToken.Register(command.Cancel))
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    var schema = new StringBuilder("SQL Server schema (metadata only; up to 512 columns):\n");
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        string row = string.Join(" | ", Enumerable.Range(0, 5).Select(i => reader.GetString(i).Replace("\r", " ").Replace("\n", " "))) + "\n";
                        if (schema.Length + row.Length > 12000) { schema.Append("[schema truncated; narrow the database or provide a focused schema]\n"); break; }
                        schema.Append(row);
                    }
                    return schema.ToString();
                }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadBoundedAsync
        /// desc         : 프로세스 출력이 메모리 상한을 넘으면 작업을 중단한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : stdout·stderr 각각 128K 문자 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task<string> ReadBoundedAsync(StreamReader reader)
        {
            var text = new StringBuilder(); var buffer = new char[2048]; int count;
            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            { if (text.Length + count > OutputLimit) throw new InvalidOperationException("작업 출력이 제한을 초과했습니다."); text.Append(buffer, 0, count); }
            return text.ToString();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResolveExecutable
        /// desc         : 기존 네이티브 exe만 절대 경로로 확인하며 배치·셸 실행은 허용하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : PATH의 현재 폴더 우선 탐색을 배제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ResolveExecutable(string value)
        {
            value = Required(value, "실행 파일", 32700);
            if (!string.Equals(Path.GetExtension(value), ".exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("네이티브 .exe 실행 파일만 선택하세요.");
            if (Path.IsPathRooted(value)) { string full = Path.GetFullPath(value); if (File.Exists(full)) return full; }
            else if (value == Path.GetFileName(value))
                foreach (string entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                {
                    if (!Path.IsPathRooted(entry.Trim('"'))) continue;
                    string candidate = Path.Combine(entry.Trim('"'), value); if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
            throw new FileNotFoundException("실행 파일을 찾지 못했습니다. 설치된 .exe의 전체 경로를 지정하세요.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidateFiles
        /// desc         : 커밋·체크아웃 파일을 프로젝트 안의 명시적인 경로로 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 전체 스테이징·와일드카드·상위 경로 접근 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string[] ValidateFiles(string directory, string[] files)
        {
            if (files == null || files.Length == 0 || files.Length > 100) throw new ArgumentException("작업할 파일을 한 줄에 하나씩 지정하세요. 최대 100개입니다.");
            return files.Select(file => {
                string path = ResolveChild(directory, Required(file, "파일 경로", 2000));
                if (Directory.Exists(path)) throw new ArgumentException("폴더 대신 개별 파일을 지정하세요.");
                return path;
            }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResolveChild
        /// desc         : 경로 정규화 후 프로젝트 바깥과 와일드카드·재분석 지점을 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 명령 옵션 대신 절대 파일 경로 전달.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ResolveChild(string root, string value)
        {
            if (value.IndexOfAny(new[] { '*', '?', '\0', '\r', '\n' }) >= 0) throw new ArgumentException("파일 경로에 와일드카드를 사용할 수 없습니다.");
            string full = Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(root, value));
            if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("프로젝트 폴더 안의 파일 또는 새 하위 폴더를 지정하세요.");
            RejectReparsePoints(full); return full;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RejectReparsePoints
        /// desc         : 심볼릭 링크·정션을 통한 검토 대상 경로 바꾸기를 방지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 존재하는 조상 경로도 함께 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RejectReparsePoints(string path)
        {
            for (string item = path; !string.IsNullOrEmpty(item); item = Path.GetDirectoryName(item))
                if ((Directory.Exists(item) || File.Exists(item)) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("정션·심볼릭 링크 대신 실제 프로젝트 경로를 선택하세요.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Required
        /// desc         : 필수 입력의 길이와 제어 문자를 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 줄바꿈을 통한 숨은 명령 미리보기 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Required(string value, string label, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl)) throw new ArgumentException(label + " 입력을 확인하세요.");
            return value;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddRemote
        /// desc         : 선택한 기존 Git 원격 이름만 단일 인수로 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 원격 미지정 시 저장소의 기본 upstream 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void AddRemote(List<string> args, string remote)
        {
            if (string.IsNullOrWhiteSpace(remote)) return;
            remote = Required(remote, "원격 이름", 100);
            if (!remote.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.') || remote.StartsWith("-", StringComparison.Ordinal)) throw new ArgumentException("origin 같은 기존 원격 이름을 입력하세요.");
            args.Add("--"); args.Add(remote);
        }
    }
}
