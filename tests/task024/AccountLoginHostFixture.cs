using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 새 계정 화면의 소유 partial 계약에 필요한 합성 상태만 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 실제 계정·설정·문서에 접근하지 않는 메모리 호스트.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed partial class AssistantControl
    {
        private bool busy = false, disposed = false;
        private string selectedProvider = "Codex";
        private readonly Dictionary<string, string> executablePaths = new Dictionary<string, string>();
        private readonly TextBox ExecutablePath = new TextBox();
        private readonly TextBlock AccountLabel = new TextBlock();
        private AssistantDialog activeSettingsDialog = null;
    }
}
namespace VS2017.AIAssistant.Providers
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CliEditProvider
    /// desc         : 기존 계정 서비스 소스의 컴파일 계약은 제공하되 실제 CLI 실행은 차단한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 로그인·상태 확인 검사는 주입 callback만 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class CliEditProvider
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResolveExecutable
        /// desc         : 실 CLI 검색이나 인증 프로세스 시작을 fixture에서 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 운영 인증 접근 금지.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string ResolveExecutable(string executable, string fallback) { throw new InvalidOperationException("Real CLI is disabled in account source fixture."); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadBoundedAsync
        /// desc         : 미사용인 기존 계정 서비스의 출력 읽기 계약만 컴파일한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 실제 프로세스·출력 스트림 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static Task<string> ReadBoundedAsync(StreamReader reader, int limit, Action stop) { throw new InvalidOperationException("No process streams in account fixture."); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryKill
        /// desc         : 실제 프로세스를 만들지 않는 fixture 계약이다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 운영 프로세스 접근 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static void TryKill(Process process) { }
    }
}
