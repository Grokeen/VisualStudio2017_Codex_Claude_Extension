using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
#if !PROJECT_CONVERSATION_CORE_TEST
using EnvDTE;
using Microsoft.VisualStudio.Shell;
#endif

namespace VS2017.AIAssistant.Services
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProjectConversationIdentity
    /// desc         : 솔루션의 정규화 경로를 안정 hash로 구분하고 기존 전역 기록을 별도 보존한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ProjectConversationIdentity
    {
        public string Key { get; private set; }
        public string PathHash { get; private set; }
        public bool IsLegacy { get; private set; }
        public bool IsUnavailable { get; private set; }
        public bool HasSolution { get; private set; }
        private ProjectConversationIdentity() { }
        public static ProjectConversationIdentity Legacy() { return new ProjectConversationIdentity { Key = "legacy", IsLegacy = true }; }
        public static ProjectConversationIdentity NoSolution() { return new ProjectConversationIdentity { Key = "no-solution" }; }
        public static ProjectConversationIdentity Unavailable() { return new ProjectConversationIdentity { Key = "unavailable", IsUnavailable = true }; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ForSolution
        /// desc         : 절대 경로의 대소문자·구분자·점 경로를 정규화하며 디스크나 원본 솔루션은 수정하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public static ProjectConversationIdentity ForSolution(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return NoSolution();
            string root = Path.GetPathRoot(path);
            if (String.IsNullOrEmpty(root) || root.Length < 3 || !Path.IsPathRooted(path)) throw new ArgumentException("An absolute solution path is required.");
            string canonical = Path.GetFullPath(path).Replace('/', '\\').TrimEnd('\\').ToUpperInvariant();
            if (String.IsNullOrEmpty(Path.GetFileName(canonical))) throw new ArgumentException("A solution file identity is required.");
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "").ToLowerInvariant();
            return new ProjectConversationIdentity { Key = "solution:" + hash, PathHash = hash, HasSolution = true };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EnsureStoreRoot
        /// desc         : 짧은 hash 디렉터리의 전체 identity 바인딩을 확인하고 전역 원문을 이동·삭제하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public string EnsureStoreRoot(string globalRoot)
        {
            if (IsUnavailable) throw new InvalidOperationException("Solution identity is not available.");
            string root = Path.GetFullPath(globalRoot);
            if (IsLegacy) return root;
            string directory = Path.Combine(root, "p", HasSolution ? PathHash.Substring(0, 32) : "none");
            RejectLinks(directory); Directory.CreateDirectory(directory); RejectLinks(directory);
            string binding = Path.Combine(directory, "scope-id.txt"); RejectLinks(binding);
            string expected = "project-conversation-v1\n" + Key + "\n";
            try
            {
                using (var stream = new FileStream(binding, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                { var bytes = new UTF8Encoding(false).GetBytes(expected); stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            }
            catch (IOException) { if (!File.Exists(binding)) throw; }
            RejectLinks(binding);
            if (new FileInfo(binding).Length > 256 || File.ReadAllText(binding, Encoding.UTF8) != expected) throw new InvalidDataException("Project conversation identity binding differs.");
            return directory;
        }
        private static void RejectLinks(string path)
        {
            for (string part = Path.GetFullPath(path); !String.IsNullOrEmpty(part); part = Path.GetDirectoryName(part))
                if ((Directory.Exists(part) || File.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked conversation paths are not allowed.");
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProjectConversationSwitchGate
    /// desc         : 실제 선택과 활성 대화 범위가 일치할 때만 전송하고 작업 중 전환은 보류한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ProjectConversationSwitchGate
    {
        public ProjectConversationIdentity Loaded { get; private set; }
        public ProjectConversationIdentity Requested { get; private set; }
        public bool Pending { get { return Loaded.Key != Requested.Key; } }
        public ProjectConversationSwitchGate(ProjectConversationIdentity initial) { if (initial == null) throw new ArgumentNullException("initial"); Loaded = Requested = initial; }
        public void Request(ProjectConversationIdentity identity) { if (identity == null) throw new ArgumentNullException("identity"); Requested = identity; }
        public ProjectConversationIdentity Propose(bool hasWork) { return !hasWork && Pending && !Requested.IsUnavailable ? Requested : null; }
        public bool Commit(ProjectConversationIdentity identity) { if (identity == null || identity.Key != Requested.Key) return false; Loaded = identity; return true; }
        public bool CanSend(ProjectConversationIdentity current) { return current != null && !Loaded.IsLegacy && !Loaded.IsUnavailable && !Pending && current.Key == Loaded.Key; }
    }

#if !PROJECT_CONVERSATION_CORE_TEST
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProjectConversationContext
    /// desc         : 실제 DTE SolutionEvents의 열기·닫기와 현재 FullName을 UI 스레드에서 추적한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class ProjectConversationContext : IDisposable
    {
        private readonly DTE dte;
        private readonly SolutionEvents solutionEvents;
        private bool disposed;
        internal ProjectConversationIdentity Current { get; private set; }
        internal long Generation { get; private set; }
        internal event EventHandler Changed;
        internal ProjectConversationContext(IServiceProvider services)
        {
            ThreadHelper.ThrowIfNotOnUIThread(); Current = ProjectConversationIdentity.Unavailable();
            try
            {
                if (services == null) return;
                object automation = services.GetService(typeof(DTE));
                if (automation == null) return;
                dte = automation as DTE;
                if (dte != null) { var events = dte.Events; if (events != null) solutionEvents = events.SolutionEvents; if (solutionEvents != null) { solutionEvents.Opened += Opened; solutionEvents.AfterClosing += Closed; Refresh(); } }
            }
            catch (System.Runtime.InteropServices.COMException) { Update(ProjectConversationIdentity.Unavailable()); }
        }
        internal void Refresh()
        {
            ThreadHelper.ThrowIfNotOnUIThread(); if (disposed) return;
            try { Update(dte == null ? ProjectConversationIdentity.Unavailable() : dte.Solution == null || !dte.Solution.IsOpen ? ProjectConversationIdentity.NoSolution() : ProjectConversationIdentity.ForSolution(dte.Solution.FullName)); }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException || error is ArgumentException || error is IOException || error is NotSupportedException) { Update(ProjectConversationIdentity.Unavailable()); }
        }
        private void Opened() { ThreadHelper.ThrowIfNotOnUIThread(); Refresh(); }
        private void Closed() { ThreadHelper.ThrowIfNotOnUIThread(); if (!disposed) Update(ProjectConversationIdentity.NoSolution()); }
        private void Update(ProjectConversationIdentity identity)
        { ThreadHelper.ThrowIfNotOnUIThread(); if (Current.Key == identity.Key) return; Current = identity; Generation++; var changed = Changed; if (changed != null) changed(this, EventArgs.Empty); }
        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread(); if (disposed) return; disposed = true;
            if (solutionEvents != null) { solutionEvents.Opened -= Opened; solutionEvents.AfterClosing -= Closed; }
            Changed = null;
        }
    }
#endif
}
