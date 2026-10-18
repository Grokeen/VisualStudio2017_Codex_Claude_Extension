using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VS2017.AIAssistant.Providers
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : NativeCliDiscovery
    /// desc         : 프로세스·사용자 PATH와 알려진 네이티브 설치 위치에서 CLI를 자동 발견한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 20:25 추가6, 이미 실행 중인 IDE의 오래된 PATH도 보완.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class NativeCliDiscovery
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Find
        /// desc         : 설치나 로그인을 수행하지 않고 선택한 제공자의 exe 경로만 찾는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : Claude 데스크톱 앱을 CLI 설치로 오인하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string Find(string provider)
        {
            ProviderExecutionOptions.ValidateProvider(provider);
            var paths = new List<string>();
            foreach (var target in new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
                try { paths.AddRange((Environment.GetEnvironmentVariable("PATH", target) ?? "").Split(Path.PathSeparator)); } catch (System.Security.SecurityException) { }
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string file = provider == "Codex" ? "codex.exe" : "claude.exe";
            var native = new List<string> { Path.Combine(profile, ".local", "bin", file), Path.Combine(local, "Microsoft", "WinGet", "Links", file) };
            if (provider == "Codex") {
                native.Insert(0, Path.Combine(local, "Programs", "OpenAI", "Codex", "bin", file));
                string desktop = Path.Combine(local, "OpenAI", "Codex", "bin"); native.Add(Path.Combine(desktop, file));
                try { if (Directory.Exists(desktop)) native.AddRange(Directory.EnumerateDirectories(desktop).Take(64).OrderByDescending(Directory.GetLastWriteTimeUtc).Select(directory => Path.Combine(directory, file))); }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            return FindIn(provider, paths, native);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FindIn
        /// desc         : 절대 로컬 디렉터리만 탐색하고 상대경로·배치 래퍼·다른 제공자를 제외한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 고정 위치 및 PATH 탐색 순서를 격리 검증 가능하게 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string FindIn(string provider, IEnumerable<string> directories, IEnumerable<string> nativeFiles)
        {
            ProviderExecutionOptions.ValidateProvider(provider);
            string file = provider == "Codex" ? "codex.exe" : "claude.exe";
            foreach (string directory in (directories ?? Enumerable.Empty<string>()).Where(value => !String.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Take(512)) {
                string path;
                try { path = Path.Combine(directory.Trim().Trim('"'), file); } catch (ArgumentException) { continue; }
                string candidate = ExistingLocalExecutable(path, file); if (candidate != null) return candidate;
            }
            foreach (string path in (nativeFiles ?? Enumerable.Empty<string>()).Take(128)) { string candidate = ExistingLocalExecutable(path, file); if (candidate != null) return candidate; }
            return null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ExistingLocalExecutable
        /// desc         : 자동 조회는 현재 폴더나 네트워크 공유의 임의 실행 파일을 선택하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 누락된 설치는 실행하지 않고 null로 반환.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ExistingLocalExecutable(string path, string name)
        {
            try {
                if (String.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || Path.GetPathRoot(path).Length < 3 || path.StartsWith("\\", StringComparison.Ordinal) || !String.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase)) return null;
                string full = Path.GetFullPath(path);
                var drive = new DriveInfo(Path.GetPathRoot(full)); if (drive.DriveType == DriveType.Network) return null;
                if (!File.Exists(full)) return null;
                return full;
            } catch (Exception error) when (error is ArgumentException || error is IOException || error is UnauthorizedAccessException || error is System.Security.SecurityException) { return null; }
        }
    }
}
