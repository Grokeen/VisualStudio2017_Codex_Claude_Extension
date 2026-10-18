using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace VS2017.AIAssistant.Services
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CliInstallerLaunchStatus
    /// desc         : 설치 확인 취소·실제 시작·기존 설치 실행 중 상태를 분리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 설치 성공이나 계정 로그인을 시작 결과로 추정하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public enum CliInstallerLaunchStatus { Cancelled, Started, AlreadyRunning }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ICliGuideProcess
    /// desc         : 일반 사용자 설치 창의 수명만 관찰하며 테스트에서는 실행 없는 fixture로 대체한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : Dispose는 핸들 해제이며 이미 시작한 설치를 중지하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface ICliGuideProcess : IDisposable
    {
        event EventHandler Exited;
        bool HasExited { get; }
        int? ExitCode { get; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CliGuideService
    /// desc         : 확장 설치 폴더의 고정 설명서만 읽고 승인된 원본 배치만 일반 권한으로 실행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 PDF·Markdown 대체 안내, 경로·크기·배치 hash 검증 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class CliGuideService
    {
        public const string PdfFileName = "VisualStudio_AI_Assistant_설치_사용설명서.pdf";
        public const string MarkdownFileName = "HowToInstallCLI.md";
        public const string InstallerFileName = "InstallCLI.bat";
        public const string InstallerSha256 = "81BAE40E698B7E16B4A15093248453C7A79EF5D564306FC9CE9416255D91D5CB";
        public const int MaximumMarkdownBytes = 128 * 1024, MaximumPdfBytes = 10 * 1024 * 1024, MaximumInstallerBytes = 64 * 1024;
        public const string Overview = "일반 사용자로 두 CLI 준비 → 설치 창의 SUCCESS 확인 → 본인 계정으로 별도 로그인 → Visual Studio를 다시 열어 연결 확인 순서입니다. 기본 배치는 실행 시점의 공식 최신 Windows 네이티브 CLI를 내려받고 사용자 PATH를 보완할 수 있습니다. Node.js·npm·PowerShell 7은 필수가 아니며 Visual Studio나 프로젝트 빌드 도구는 설치하지 않습니다. CLI 설치 성공과 AI 계정 로그인·이용 권한 확인은 별도 단계입니다.";
        public const string Confirmation = "Codex CLI와 Claude Code CLI 설치 배치를 실행하시겠습니까?\n\n공식 설치기를 인터넷에서 내려받으며 기존 CLI를 갱신하거나 사용자 PATH를 보완할 수 있습니다. 일반 사용자 권한의 별도 설치 창을 열고 결과를 직접 확인합니다. 관리자 권한을 요청하지 않으며 AI 계정 로그인은 별도입니다.\n\n실행 중인 Visual Studio는 설치 후 다시 열어 주세요.";
        private readonly Func<ProcessStartInfo, ICliGuideProcess> installerLauncher;
        private readonly Action<ProcessStartInfo> documentOpener;
        private ICliGuideProcess runningProcess;
        private FileStream runningInstallerFile;
        private readonly object processGate = new object();
        private static int installationGate;
        public string InstallationDirectory { get; private set; }
        public string AssetsDirectory { get; private set; }
        public bool IsInstallationRunning { get { return Volatile.Read(ref installationGate) != 0; } }
        public int? LastExitCode { get; private set; }
        public event EventHandler InstallationChanged;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CliGuideService
        /// desc         : assembly 폴더 또는 명시적 절대 설치 폴더 아래 고정 Resources/CliGuide 자산만 사용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실행 callback은 테스트 경계이며 사용자 입력 명령·옵션을 받지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public CliGuideService(string installationDirectory = null, Func<ProcessStartInfo, ICliGuideProcess> installerLauncher = null, Action<ProcessStartInfo> documentOpener = null)
        {
            InstallationDirectory = RequireLocalAbsoluteDirectory(installationDirectory ?? Path.GetDirectoryName(typeof(CliGuideService).Assembly.Location));
            AssetsDirectory = Path.Combine(InstallationDirectory, "Resources", "CliGuide");
            this.installerLauncher = installerLauncher ?? StartInstaller;
            this.documentOpener = documentOpener ?? (info => { using (var opened = Process.Start(info)) { } });
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetPdfPath
        /// desc         : PDF 서명·파일 크기·로컬 경로를 검증하고 누락된 문서는 친절한 오류로 구분한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : WebView에 임의 URL이나 다른 파일을 제공하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string GetPdfPath()
        {
            string path = ResolveAsset(PdfFileName);
            using (var stream = OpenAsset(path, MaximumPdfBytes, "PDF 설명서"))
            {
                var signature = new byte[5];
                if (stream.Read(signature, 0, signature.Length) != 5 || Encoding.ASCII.GetString(signature) != "%PDF-") throw new InvalidDataException("PDF 설명서 형식이 올바르지 않습니다. 텍스트 안내를 이용하세요.");
            }
            return path;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadMarkdownFallback
        /// desc         : WebView 사용이 불가능해도 HTML 실행 없이 원본 Markdown 본문을 오프라인으로 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 누락·손상 시 기본 절차와 패키지 복구 안내를 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string ReadMarkdownFallback()
        {
            try
            {
                using (var stream = OpenAsset(ResolveAsset(MarkdownFileName), MaximumMarkdownBytes, "텍스트 설명서"))
                {
                    byte[] bytes = ReadBounded(stream, MaximumMarkdownBytes);
                    using (var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false, true), true)) return reader.ReadToEnd();
                }
            }
            catch (Exception error) when (IsExpectedAssetError(error))
            { return Overview + "\n\n텍스트 설명서를 읽지 못했습니다. 확장 패키지의 Resources/CliGuide 문서가 포함되어 있는지 확인하거나 확장을 다시 설치하세요. 설치 배치와 PDF를 받은 폴더에서도 안내를 볼 수 있습니다."; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsAllowedNavigation
        /// desc         : 검증된 로컬 PDF의 동일 파일·쪽 fragment만 WebView 탐색 대상으로 허용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 외부 http·UNC·다른 로컬 파일·쿼리·인증정보는 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool IsAllowedNavigation(string address)
        {
            Uri uri;
            if (!Uri.TryCreate(address, UriKind.Absolute, out uri) || !uri.IsFile || uri.IsUnc || uri.Query.Length != 0 || uri.UserInfo.Length != 0) return false;
            try { return String.Equals(Path.GetFullPath(uri.LocalPath), GetPdfPath(), StringComparison.OrdinalIgnoreCase); }
            catch (Exception error) when (IsExpectedAssetError(error) || error is ArgumentException || error is NotSupportedException) { return false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenPdfExternally
        /// desc         : 사용자가 누른 경우 검증된 PDF 한 파일만 기본 PDF 앱으로 열고 인수는 전달하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 자동 실행·임의 문서·원격 URL 연결 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void OpenPdfExternally()
        { documentOpener(new ProcessStartInfo { FileName = GetPdfPath(), Arguments = "", WorkingDirectory = AssetsDirectory, UseShellExecute = true, Verb = "open" }); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LaunchInstaller
        /// desc         : 명시적 설치 승인 뒤 원본 SHA를 확인하고 한 프로세스에 설치 창 하나만 시작한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 파일 잠금을 실행 종료까지 유지하고 shell 옵션·관리자 승격을 주입하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public CliInstallerLaunchStatus LaunchInstaller(bool confirmed)
        {
            if (!confirmed) return CliInstallerLaunchStatus.Cancelled;
            if (Interlocked.CompareExchange(ref installationGate, 1, 0) != 0) return CliInstallerLaunchStatus.AlreadyRunning;
            FileStream verified = null;
            bool launched = false;
            try
            {
                string path = ResolveAsset(InstallerFileName);
                if (path.Any(c => Char.IsControl(c) || "\"%&|<>^!".IndexOf(c) >= 0)) throw new InvalidOperationException("설치 경로에 명령 해석 문자가 있어 자동 실행할 수 없습니다. PDF의 수동 설치 절차를 이용하세요.");
                verified = OpenAsset(path, MaximumInstallerBytes, "CLI 설치 배치");
                byte[] bytes = ReadBounded(verified, MaximumInstallerBytes);
                string hash;
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                if (!String.Equals(hash, InstallerSha256, StringComparison.Ordinal)) throw new InvalidDataException("CLI 설치 배치가 원본과 다릅니다. 실행하지 않았습니다. 확장을 다시 설치한 뒤 확인하세요.");
                var info = new ProcessStartInfo {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                    Arguments = "/d /v:off /s /c \"\"" + path + "\"\"", WorkingDirectory = AssetsDirectory,
                    UseShellExecute = true, Verb = "open", CreateNoWindow = false, WindowStyle = ProcessWindowStyle.Normal
                };
                lock (processGate)
                {
                    LastExitCode = null;
                    var started = installerLauncher(info);
                    if (started == null) throw new InvalidOperationException("설치 창을 시작하지 못했습니다.");
                    runningInstallerFile = verified; verified = null;
                    runningProcess = started; launched = true;
                    started.Exited += InstallerExited;
                    if (started.HasExited) InstallerExited(started, EventArgs.Empty);
                }
                NotifyInstallationChanged();
                return CliInstallerLaunchStatus.Started;
            }
            catch { if (!launched) Interlocked.Exchange(ref installationGate, 0); throw; }
            finally { if (verified != null) verified.Dispose(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetBrowserDataDirectory
        /// desc         : 브라우저 사용자 프로필과 별개인 로컬 안내 전용 WebView 저장 위치를 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 격리 검사에서는 workspace override 아래만 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string GetBrowserDataDirectory()
        {
            string workspace = Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH");
            string root = String.IsNullOrWhiteSpace(workspace) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VS2017AIAssistant") : RequireLocalAbsoluteDirectory(workspace);
            string path = Path.Combine(root, "CliGuideWebView2"); RejectReparsePoints(path); return path;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InstallerExited
        /// desc         : 실제 종료 상태만 기록하고 창 종료가 설치·로그인 성공을 뜻한다고 추정하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 종료 경쟁을 한 번만 처리하고 중복 실행 차단을 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InstallerExited(object sender, EventArgs args)
        {
            lock (processGate)
            {
                if (runningProcess == null || !ReferenceEquals(sender, runningProcess) || !runningProcess.HasExited) return;
                var process = runningProcess; runningProcess = null;
                try { LastExitCode = process.ExitCode; process.Exited -= InstallerExited; process.Dispose(); }
                finally { if (runningInstallerFile != null) { runningInstallerFile.Dispose(); runningInstallerFile = null; } Interlocked.Exchange(ref installationGate, 0); }
            }
            NotifyInstallationChanged();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NotifyInstallationChanged
        /// desc         : UI 알림 구독자의 예외가 실행 중인 설치 소유권과 중복 차단을 변경하지 못하게 한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 개별 알림 실패는 원문 기록 없이 격리하고 다른 구독자는 계속 알림.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void NotifyInstallationChanged()
        { var handlers = InstallationChanged; if (handlers == null) return; foreach (EventHandler handler in handlers.GetInvocationList()) { try { handler(this, EventArgs.Empty); } catch (Exception) { /* 알림은 설치 프로세스의 성공·실패 경계가 아니다. */ } } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResolveAsset
        /// desc         : 고정 자산 이름을 설치 디렉터리 아래로만 해석한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 외부 호출자로부터 상대 경로나 URL을 받지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private string ResolveAsset(string name)
        {
            string path = Path.GetFullPath(Path.Combine(AssetsDirectory, name));
            if (!path.StartsWith(AssetsDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("설명서 경로가 설치 폴더를 벗어났습니다.");
            RejectReparsePoints(path); return path;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenAsset
        /// desc         : 고정 설명서 파일을 공유 읽기로 열고 크기와 파일 종류를 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 설치 검증에서 열린 동안 다른 쓰기·삭제를 허용하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static FileStream OpenAsset(string path, int maximum, string label)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(label + "를 찾지 못했습니다. 확장 패키지를 다시 설치하거나 텍스트 안내를 이용하세요.");
            RejectReparsePoints(path);
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length == 0 || stream.Length > maximum) { stream.Dispose(); throw new InvalidDataException(label + "의 크기가 올바르지 않습니다."); }
            return stream;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadBounded
        /// desc         : 증가하는 스트림도 최대 한 바이트 초과 지점에서 읽기를 멈춘다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 텍스트·설치 배치 무제한 읽기 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static byte[] ReadBounded(Stream stream, int maximum)
        {
            using (var output = new MemoryStream()) { var buffer = new byte[8192]; int read; while ((read = stream.Read(buffer, 0, Math.Min(buffer.Length, maximum + 1 - (int)output.Length))) > 0) { output.Write(buffer, 0, read); if (output.Length > maximum) throw new InvalidDataException("설명서 파일이 허용 크기를 넘었습니다."); } return output.ToArray(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequireLocalAbsoluteDirectory
        /// desc         : 현재 작업 폴더·UNC·장치 경로를 사용하지 않는 절대 로컬 설치 위치만 허용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 폴더를 생성하거나 사용자 입력을 명령 인수로 확장하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string RequireLocalAbsoluteDirectory(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || path.Length < 3 || !Char.IsLetter(path[0]) || path[1] != ':' || (path[2] != '\\' && path[2] != '/') || path.Any(Char.IsControl)) throw new ArgumentException("설명서는 로컬 절대 설치 폴더에서만 열 수 있습니다.");
            string full = Path.GetFullPath(path); if (full.Length > 3) full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            RejectReparsePoints(full); return full;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RejectReparsePoints
        /// desc         : 자산·프로필 경로의 기존 모든 상위 요소에서 junction·symbolic link를 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 설치 폴더 밖의 문서·배치로 우회하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RejectReparsePoints(string path)
        {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("설명서 경로에 연결 폴더가 있어 열거나 실행하지 않았습니다. 확장 설치 위치를 확인하세요.");
                string parent = Path.GetDirectoryName(current); if (parent == current) break; current = parent;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsExpectedAssetError
        /// desc         : 원문·환경 경로를 대체 안내에 노출하지 않고 예상 가능한 문서 오류를 판별한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 사용자 본문이나 자격 증명을 로그에 복사하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsExpectedAssetError(Exception error)
        { return error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is InvalidOperationException || error is DecoderFallbackException || error is System.Security.SecurityException; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StartInstaller
        /// desc         : 검증이 끝난 명령 정보로만 일반 권한의 표시 가능한 설치 창을 시작한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실행은 사용자 확인 이후에만 호출되고 stdout·인증정보는 수집하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static ICliGuideProcess StartInstaller(ProcessStartInfo info)
        { var process = Process.Start(info); if (process == null) throw new InvalidOperationException("설치 창을 시작하지 못했습니다."); return new CliGuideProcess(process); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CliGuideProcess
        /// desc         : 설치 자식 프로세스의 실제 종료 이벤트·코드만 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 원문 출력·로그인·토큰 데이터를 수집하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class CliGuideProcess : ICliGuideProcess
        {
            private readonly Process process;
            public event EventHandler Exited;
            public bool HasExited { get { return process.HasExited; } }
            public int? ExitCode { get { return process.HasExited ? (int?)process.ExitCode : null; } }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : CliGuideProcess
            /// desc         : 이미 시작한 프로세스의 종료 감시를 연결한다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 종료가 빠른 경우도 HasExited로 확인.
            /// </summary>
            /// ---------------------------------------------------------------------
            public CliGuideProcess(Process process) { this.process = process; process.Exited += ProcessExited; process.EnableRaisingEvents = true; }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : ProcessExited
            /// desc         : 실제 설치 창 종료만 알린다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 설치 성공을 만들어 내지 않음.
            /// </summary>
            /// ---------------------------------------------------------------------
            private void ProcessExited(object sender, EventArgs args) { Exited?.Invoke(this, EventArgs.Empty); }
            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : Dispose
            /// desc         : 프로세스를 강제 종료하지 않고 이벤트·핸들만 해제한다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 안내창 종료가 설치를 중단하지 않음.
            /// </summary>
            /// ---------------------------------------------------------------------
            public void Dispose() { process.Exited -= ProcessExited; process.Dispose(); }
        }
    }
}
