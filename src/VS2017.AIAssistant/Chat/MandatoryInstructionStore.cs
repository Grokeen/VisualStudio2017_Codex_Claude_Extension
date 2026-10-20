using System;
using System.IO;
using System.Linq;
using System.Text;

namespace VS2017.AIAssistant.Chat
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MandatoryInstructionStore
    /// desc         : 내장 필수 규칙과 숨김 관리 사본을 분리해 사용자 규칙·기존 파일을 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-022 최초 생성·부재 복구·변조 안내·요청별 중복 없는 기본 규칙 제공.
    ///                2026-10-18 CodexCode : 최신 Role8의 협업 문장·원래 주석 예시·기존 내용 변경 금지 문안 반영.
    ///                2026-10-20 CodexCode : 운영 프로젝트의 기존 패턴·최소 수정·감사 협업 규칙 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class MandatoryInstructionStore
    {
        private const string FileName = ".mandatory-instructions.md";
        private const int MaximumCopyBytes = 16384;
        private static readonly object Gate = new object();
        private const string CanonicalInstructions =
            "# 필수 공통 작업 규칙\n" +
            "1. 다른 코덱스가 작업할 수 있으니, 특정 파일을 만들어 서로 간의 협업 진행.\n" +
            "2. 작업을 진행한 소스에는 소스관련 설명 주석 추가. 그리고 작업 내역은 클래스나 메소드 위에 아래 예시와 같은 주석을 남기고. 이미 있다면 update date에 업데이트 내역을 남겨, 다른 사용자가 참고할 수 있게 정리.\n" +
            "```cs\n" +
            "/// ---------------------------------------------------------------------\n" +
            "/// <summary>\n" +
            "/// name         : ClassOrMethodName\n" +
            "/// desc         : 메소드나 클래스에 대한 내용 정의\n" +
            "/// author       : 작업자명\n" +
            "/// create date  : 2025-11-10\n" +
            "/// update date  : 2026-10-17 코덱스Code : ~ 개발하였음.\n" +
            "///                2026-10-18 코덱스Orig : ~ 수정\n" +
            "///                2026-10-19 코덱스Orig : ~ 추가\n" +
            "/// </summary>\n" +
            "/// ---------------------------------------------------------------------\n" +
            "```\n" +
            "- 유사한 주석이 있는 경우, 해당 방식으로 구조 변경. 기존 주석 내용은 수정 불가.\n" +
            "3. 연결된 프로젝트가 이미 운영중인 프로그램이거나 작업이 많이 진행된 프로젝트인 경우, 현재 프로젝트의 UI나 소스에서 사용하는 특유의 패턴이 있는지 확인하고 작업 진행한다.\n" +
            "4. 연결된 프로젝트가 이미 운영중인 프로그램이거나 작업이 많이 진행된 프로젝트인 경우, 소스 수정을 최소화하여 작업한다.\n" +
            "5. 작업이 완료된 경우, 요구사항에 맞게 작업이 됐는지 감사 담당자 AI 모델에게 검증 요청한다. 감사 담당자 AI가 없는 경우 사용자에게 AI 모델 추가를 요청한다.";

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DefaultInstructions
        /// desc         : 관리 사본과 무관하게 컴파일에 동봉된 필수 기본 문안만 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 외부 파일 변조를 요청 규칙으로 승격하지 않는 기준값.
        ///                2026-10-20 CodexCode : 기존 규칙 뒤에 운영 프로젝트·감사 문안을 추가.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string DefaultInstructions { get { return CanonicalInstructions; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Compose
        /// desc         : 파일을 읽지 않고 내장 기본 규칙을 한 번만 앞붙이며 사용자 원문을 잘라내지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 재조합 중복 제거와 사용자 8000자·공백·개행 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string Compose(string userInstructions)
        {
            string user = userInstructions ?? "", prefix = CanonicalInstructions + "\n\n";
            while (user.StartsWith(prefix, StringComparison.Ordinal)) user = user.Substring(prefix.Length);
            if (String.Equals(user, CanonicalInstructions, StringComparison.Ordinal)) user = "";
            return user.Length == 0 ? CanonicalInstructions : prefix + user;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Ensure
        /// desc         : 공용 workspace의 관리 사본을 생성·복구하고 이전 공식 사본만 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 기본 경로 API와 상세 안내 overload 분리.
        ///                2026-10-20 CodexCode : 정확한 이전 공식 사본만 새 규칙으로 갱신.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string Ensure(string workspaceRoot)
        { string notice; return Ensure(workspaceRoot, out notice); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Ensure
        /// desc         : 이전 공식 바이트만 갱신하며 변조·과대·읽기 불가 사본은 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 CreateNew 경쟁·유한 읽기·숨김 속성·reparse 경계 보호.
        ///                2026-10-20 CodexCode : 이전 공식 바이트만 원자 교체하고 사용자 변경본 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string Ensure(string workspaceRoot, out string notice)
        {
            notice = null;
            lock (Gate)
            {
                string root = ValidateRoot(workspaceRoot);
                RejectReparsePoints(root); Directory.CreateDirectory(root); RejectReparsePoints(root);
                string path = Path.Combine(root, FileName); RejectReparsePoints(path);
                bool created = false;
                try
                {
                    using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] bytes = new UTF8Encoding(false).GetBytes(CanonicalInstructions);
                        output.Write(bytes, 0, bytes.Length); output.Flush();
                    }
                    created = true;
                }
                catch (IOException) { if (!File.Exists(path)) throw; }
                RejectReparsePoints(path);
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Directory) != 0) throw new IOException("필수 규칙 사본 경로가 파일이 아닙니다.");
                if ((attributes & FileAttributes.Hidden) == 0) File.SetAttributes(path, attributes | FileAttributes.Hidden);
                if (!created)
                {
                    try
                    {
                        if (IsPreviousOfficialCopy(path)) ReplacePreviousOfficialCopy(path);
                        if (!String.Equals(ReadCopy(path), CanonicalInstructions, StringComparison.Ordinal))
                            notice = "필수 규칙 관리 사본의 내용이 기본값과 다릅니다. 파일은 보존했으며 요청에는 내장 기본 규칙을 사용합니다.";
                    }
                    catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is DecoderFallbackException)
                    { notice = "필수 규칙 관리 사본을 확인하지 못했습니다. 파일은 보존했으며 요청에는 내장 기본 규칙을 사용합니다."; }
                }
                return path;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsPreviousOfficialCopy
        /// desc         : 규칙 1~2가 담긴 공식 UTF-8 바이트와 완전히 같은 사본만 구분한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsPreviousOfficialCopy(string path)
        {
            int boundary = CanonicalInstructions.IndexOf("\n3. ", StringComparison.Ordinal);
            if (boundary < 0) return false;
            byte[] original = new UTF8Encoding(false).GetBytes(CanonicalInstructions.Substring(0, boundary));
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (input.Length != original.Length) return false;
                var actual = new byte[original.Length]; int read = 0, count;
                while (read < actual.Length && (count = input.Read(actual, read, actual.Length - read)) > 0) read += count;
                return read == original.Length && input.ReadByte() < 0 && actual.SequenceEqual(original);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReplacePreviousOfficialCopy
        /// desc         : 공식 구버전 관리 사본을 같은 폴더의 임시 파일로 원자 교체한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ReplacePreviousOfficialCopy(string path)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, CanonicalInstructions, new UTF8Encoding(false));
                File.SetAttributes(temporary, File.GetAttributes(temporary) | FileAttributes.Hidden);
                if (!IsPreviousOfficialCopy(path)) return;
                File.Replace(temporary, path, null);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadCopy
        /// desc         : 커지는 파일도 최대 16KiB+1바이트까지만 읽고 과대한 사본은 원문을 반환하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 변조된 관리 사본의 유한 비교와 UTF 유효성 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ReadCopy(string path)
        {
            var bytes = new byte[MaximumCopyBytes + 1]; int length = 0, count;
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                while (length < bytes.Length && (count = input.Read(bytes, length, bytes.Length - length)) > 0) length += count;
            if (length > MaximumCopyBytes) return null;
            using (var memory = new MemoryStream(bytes, 0, length))
            using (var reader = new StreamReader(memory, new UTF8Encoding(false, true), true)) return reader.ReadToEnd();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidateRoot
        /// desc         : CWD에 의존하는 상대·드라이브 상대·루트 상대 및 장치 경로를 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 공용 workspace의 완전한 절대 경로만 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ValidateRoot(string value)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Any(Char.IsControl)) throw new ArgumentException("공용 workspace의 전체 경로가 필요합니다.");
            string path = value.Replace('/', '\\');
            bool drive = path.Length >= 3 && Char.IsLetter(path[0]) && path[1] == ':' && path[2] == '\\';
            int share = path.StartsWith(@"\\", StringComparison.Ordinal) ? path.IndexOf('\\', 2) : -1;
            bool unc = share > 2 && share < path.Length - 1 && path[share + 1] != '\\';
            int colon = path.IndexOf(':');
            if ((!drive && !unc) || path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal) ||
                (colon >= 0 && (!drive || colon != 1 || path.IndexOf(':', colon + 1) >= 0))) throw new ArgumentException("장치·상대 경로 대신 공용 workspace의 전체 경로를 지정하세요.");
            return Path.GetFullPath(path);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RejectReparsePoints
        /// desc         : 관리 사본 또는 조상 폴더의 링크·정션을 통한 다른 파일 변경을 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-022 존재하는 각 경로의 reparse 속성 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RejectReparsePoints(string path)
        {
            for (string current = path; !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                FileAttributes attributes;
                try { attributes = File.GetAttributes(current); }
                catch (FileNotFoundException) { continue; }
                catch (DirectoryNotFoundException) { continue; }
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("필수 규칙 사본에는 정션·심볼릭 링크 경로를 사용할 수 없습니다.");
            }
        }
    }
}
