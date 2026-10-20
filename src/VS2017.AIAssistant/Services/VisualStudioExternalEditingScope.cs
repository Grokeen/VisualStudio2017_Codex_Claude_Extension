using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using VS2017.AIAssistant.Editing;

namespace VS2017.AIAssistant.Services
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : VisualStudioExternalEditingScope
    /// desc         : 저장된 편집기만 일시 보호하고 직접 수정 결과를 모달 없이 다시 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 문서별 알림 중지·읽기 전용 보호·동기화·복원.
    ///                2026-10-19 CodexOrig : TASK032 열린 프로젝트 파일도 제한된 실행 범위에서 동기화.
    ///                2026-10-20 CodexOrig : TASK068 대화 간 공유 보호와 원래 읽기 전용 상태 구분.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class VisualStudioExternalEditingScope : IExternalEditingScope, IExternalEditingScopeState
    {
        private readonly IVsRunningDocumentTable table;
        private readonly IVsFileChangeEx fileChanges;
        private readonly DocumentEvents events;
        private readonly Action stopExecution;
        private readonly DTE automation;
        private IVsSolution4 projectSolution;
        private readonly Dictionary<string, ProtectedDocument> documents = new Dictionary<string, ProtectedDocument>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> notices = new List<string>();
        private readonly List<ProtectedProject> projects = new List<ProtectedProject>();
        private long protectedBytes;
        private bool disposed;
        public string Notice { get { return String.Join("\n", notices); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsTemporarilyReadOnly
        /// desc         : 이 범위가 편집 가능한 문서에 설정한 임시 읽기 전용 상태만 인정한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 기존 버퍼·파일 읽기 전용과 종료된 보호 범위를 제외.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool IsTemporarilyReadOnly(string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ProtectedDocument entry;
            if (disposed || String.IsNullOrWhiteSpace(filePath) || !documents.TryGetValue(filePath, out entry) ||
                !entry.StateProtected || entry.OriginalReadOnly ||
                (entry.OriginalState & (uint)(BUFFERSTATEFLAGS.BSF_USER_READONLY | BUFFERSTATEFLAGS.BSF_FILESYS_READONLY)) != 0)
                return false;
            try { return File.Exists(filePath) && (File.GetAttributes(filePath) & FileAttributes.ReadOnly) == 0; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProtectedDocument
        /// desc         : 자신이 변경한 알림·읽기 전용 상태와 파일 기준을 복원용으로 보관한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 기존 문서 상태를 덮어쓰지 않는 수명 관리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class ProtectedDocument
        {
            internal string Path, Fingerprint;
            internal long Size;
            internal uint Cookie, OriginalState;
            internal IVsDocDataFileChangeControl ChangeControl;
            internal IVsPersistDocData Persist;
            internal IVsTextLines Buffer;
            internal bool Ignored, StateProtected, OriginalReadOnly;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProtectedProject
        /// desc         : 프로젝트 루트의 저장 상태와 자신이 중지한 파일 알림만 보관한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 편집기 문서와 프로젝트 계층의 별도 알림 수명 관리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class ProtectedProject
        {
            internal string Path, Fingerprint;
            internal IVsPersistHierarchyItem2 Persist;
            internal IPersistFileFormat FilePersist;
            internal Guid Id;
            internal bool Ignored, HierarchyIgnored;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : VisualStudioExternalEditingScope
        /// desc         : 쓰기 전에 모든 열린 문서의 저장 상태를 확인하고 자기 세션의 보호만 설정한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 파일 변경 전 보호 실패는 CLI 실행 전에 중단.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal VisualStudioExternalEditingScope(IServiceProvider services, DTE dte, Action stopExecution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (services == null || dte == null) throw new InvalidOperationException("편집기 동기화 서비스를 사용할 수 없습니다.");
            var runningTable = services.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
            if (runningTable == null) throw new InvalidOperationException("열린 편집기 서비스를 사용할 수 없습니다.");
            table = runningTable;
            var changeService = services.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;
            if (changeService == null) throw new InvalidOperationException("편집기 파일 변경 서비스를 사용할 수 없습니다.");
            fileChanges = changeService;
            this.stopExecution = stopExecution;
            automation = dte;
            events = dte.Events.DocumentEvents;
            try {
                foreach (Document document in dte.Documents) if (!document.Saved) throw new InvalidOperationException("직접 수정 전에 열린 미저장 문서를 모두 저장하세요.");
                foreach (Document document in dte.Documents) Protect(document);
                ProtectProjects(services);
                events.DocumentOpened += DocumentOpened;
                events.DocumentClosing += DocumentClosing;
            } catch { Dispose(); throw; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProtectProjects
        /// desc         : 저장된 로드 프로젝트의 파일 알림을 잠시 중지하고 안전한 재로딩 가능 여부를 먼저 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : image39의 csproj 변경 모달을 문서 보호와 별도로 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ProtectProjects(IServiceProvider services)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var solution = services.GetService(typeof(SVsSolution)) as IVsSolution;
            if (solution == null) throw new InvalidOperationException("프로젝트 동기화 서비스를 사용할 수 없습니다.");
            projectSolution = solution as IVsSolution4;
            Guid kind = Guid.Empty; IEnumHierarchies enumerator;
            ErrorHandler.ThrowOnFailure(solution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION, ref kind, out enumerator));
            var next = new IVsHierarchy[1]; uint fetched; int count = 0;
            while (enumerator.Next(1, next, out fetched) == VSConstants.S_OK && fetched == 1)
            {
                if (++count > 256) throw new InvalidOperationException("로드된 프로젝트가 많아 직접 수정을 시작하지 않았습니다.");
                var project = next[0] as IVsProject; string path;
                if (project == null || ErrorHandler.Failed(project.GetMkDocument(VSConstants.VSITEMID_ROOT, out path)) || String.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
                path = System.IO.Path.GetFullPath(path);
                if (projects.Exists(existing => String.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
                var persist = next[0] as IVsPersistHierarchyItem2;
                var filePersist = next[0] as IPersistFileFormat;
                int dirty, reloadable = 0;
                if (filePersist == null) throw new InvalidOperationException("프로젝트 저장 상태를 확인할 수 없어 직접 수정을 시작하지 않았습니다: " + System.IO.Path.GetFileName(path));
                ErrorHandler.ThrowOnFailure(filePersist.IsDirty(out dirty));
                if (dirty != 0)
                    throw new InvalidOperationException("직접 수정 전에 프로젝트의 미저장 변경을 저장하세요: " + System.IO.Path.GetFileName(path));
                if (persist != null && ErrorHandler.Failed(persist.IsItemReloadable(VSConstants.VSITEMID_ROOT, out reloadable))) reloadable = 0;
                if (reloadable == 0 && projectSolution == null)
                    throw new InvalidOperationException("이 프로젝트는 자동 동기화를 지원하지 않아 직접 수정을 시작하지 않았습니다: " + System.IO.Path.GetFileName(path));
                Guid id; ErrorHandler.ThrowOnFailure(solution.GetGuidOfProject(next[0], out id));
                var entry = new ProtectedProject { Path = path, Fingerprint = Fingerprint(path), Persist = reloadable == 0 ? null : persist, FilePersist = filePersist, Id = id };
                projects.Add(entry);
                ErrorHandler.ThrowOnFailure(fileChanges.IgnoreFile(0, path, 1)); entry.Ignored = true;
                if (entry.Persist != null) { ErrorHandler.ThrowOnFailure(entry.Persist.IgnoreItemFileChanges(VSConstants.VSITEMID_ROOT, 1)); entry.HierarchyIgnored = true; }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Protect
        /// desc         : 텍스트 문서의 알림을 중지하고 원래 상태를 보존한 임시 읽기 전용을 설정한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : RDT·DocData의 중첩 알림 중지를 균형 있게 관리.
        ///                2026-10-20 CodexOrig : 원래 문서·영역 읽기 전용 상태를 임시 보호 전에 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Protect(Document document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!document.Saved) throw new InvalidOperationException("실행 중 새 미저장 문서가 열려 직접 수정을 중지합니다.");
            string path = document.FullName;
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path) || documents.ContainsKey(path)) return;
            if (!String.Equals(document.Kind, EnvDTE.Constants.vsDocumentKindText, StringComparison.OrdinalIgnoreCase)) return;
            long size = new FileInfo(path).Length;
            if (documents.Count >= 64 || protectedBytes + size > 16L * 1024 * 1024) throw new InvalidOperationException("열린 텍스트 문서가 많습니다. 사용하지 않는 문서를 닫은 뒤 직접 수정을 요청하세요.");
            IVsHierarchy hierarchy; uint item, cookie; IntPtr data;
            ErrorHandler.ThrowOnFailure(table.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, path, out hierarchy, out item, out data, out cookie));
            if (data == IntPtr.Zero) throw new InvalidOperationException("열린 문서를 보호하지 못했습니다. 문서를 닫거나 다시 연 뒤 요청하세요.");
            object docData;
            try { docData = Marshal.GetObjectForIUnknown(data); } finally { Marshal.Release(data); }
            var entry = new ProtectedDocument { Path = path, Size = size, Cookie = cookie, ChangeControl = docData as IVsDocDataFileChangeControl, Persist = docData as IVsPersistDocData, Buffer = docData as IVsTextLines };
            if (entry.Buffer == null) { var provider = docData as IVsTextBufferProvider; if (provider != null) ErrorHandler.ThrowOnFailure(provider.GetTextBuffer(out entry.Buffer)); }
            if (entry.ChangeControl == null || entry.Persist == null || entry.Buffer == null) throw new InvalidOperationException("현재 편집기는 직접 수정 동기화를 지원하지 않습니다. 해당 문서를 닫은 뒤 요청하세요.");
            var text = document.Object("TextDocument") as TextDocument;
            entry.OriginalReadOnly = document.ReadOnly || text == null || text.StartPoint.CreateEditPoint().ReadOnly(text.EndPoint);
            entry.Fingerprint = Fingerprint(path);
            documents.Add(path, entry); protectedBytes += size;
            ErrorHandler.ThrowOnFailure(entry.ChangeControl.IgnoreFileChanges(1)); entry.Ignored = true;
            ErrorHandler.ThrowOnFailure(entry.Buffer.GetStateFlags(out entry.OriginalState));
            ErrorHandler.ThrowOnFailure(entry.Buffer.SetStateFlags(entry.OriginalState | (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY)); entry.StateProtected = true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DocumentOpened
        /// desc         : 실행 중 새로 열린 소스도 보호하고 미저장 문서는 실행을 중지해 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 실행 도중 편집기 문서 집합 변경 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DocumentOpened(Document document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (disposed) return;
            try { Protect(document); }
            catch (Exception) { notices.Add("새 문서를 보호하지 못해 실행을 중지했습니다. 편집 내용은 그대로 보존했습니다."); StopExecution(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DocumentClosing
        /// desc         : 문서를 닫으면 실행을 중지하고 기존 DocData를 해제해 재열림도 새로 보호한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 닫힌 COM 버퍼와 재열린 버퍼의 수명 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DocumentClosing(Document document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (disposed) return;
            ProtectedDocument entry;
            try {
                if (!documents.TryGetValue(document.FullName, out entry)) return;
                notices.Add("문서를 닫아 직접 수정을 중지했습니다. 수행된 변경은 디스크에 남을 수 있습니다.");
                StopExecution(); Release(entry); documents.Remove(entry.Path); protectedBytes -= entry.Size;
            } catch (Exception) { notices.Add("닫는 문서의 동기화 상태를 확인하세요."); StopExecution(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StopExecution
        /// desc         : 종료된 요청의 취소 콜백도 문서 이벤트로 예외를 전파하지 않게 한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 문서 정리와 요청 취소의 오류 독립.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void StopExecution() { try { if (stopExecution != null) stopExecution(); } catch (Exception) { notices.Add("실행 중지 상태를 확인하세요."); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Fingerprint
        /// desc         : 최대 2MiB 파일만 공유 읽기로 해시해 직접 변경 여부를 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 읽는 동안 커지는 파일도 크기 제한 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Fingerprint(string path)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var hash = SHA256.Create()) {
                var buffer = new byte[8192]; int read, total = 0;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0) { total += read; if (total > VisualStudioEditorDocument.MaximumDiskHashBytes) throw new InvalidOperationException("큰 편집 문서는 닫은 뒤 직접 수정을 요청하세요."); hash.TransformBlock(buffer, 0, read, buffer, 0); }
                hash.TransformFinalBlock(new byte[0], 0, 0); return Convert.ToBase64String(hash.Hash);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 저장된 변경은 다시 읽고 모든 알림·읽기 전용 상태를 중지·실패에도 복원한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 예외가 원래 실행 오류를 가리지 않게 안내로 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (disposed) return; disposed = true;
            try { if (events != null) events.DocumentOpened -= DocumentOpened; } catch (Exception) { notices.Add("문서 열림 이벤트 해제를 확인하세요."); }
            try { if (events != null) events.DocumentClosing -= DocumentClosing; } catch (Exception) { notices.Add("문서 닫힘 이벤트 해제를 확인하세요."); }
            foreach (var entry in documents.Values) Release(entry);
            documents.Clear();
            foreach (var entry in projects) ReleaseProject(entry);
            projects.Clear();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReleaseProject
        /// desc         : 변경된 깨끗한 프로젝트만 다시 읽고 실패·삭제·미저장 상태에도 알림을 복원한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 전역 자동 다시 읽기 설정과 사용자 미저장 프로젝트는 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ReleaseProject(ProtectedProject entry)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (!File.Exists(entry.Path)) notices.Add(System.IO.Path.GetFileName(entry.Path) + ": 프로젝트 파일이 삭제되어 자동으로 다시 읽지 않았습니다.");
                else if (entry.Ignored && Fingerprint(entry.Path) != entry.Fingerprint)
                {
                    int dirty; ErrorHandler.ThrowOnFailure(entry.FilePersist.IsDirty(out dirty));
                    if (dirty == 0) ReloadProject(entry);
                    else notices.Add(System.IO.Path.GetFileName(entry.Path) + ": 미저장 프로젝트 변경을 보존했습니다. 디스크와 비교한 뒤 다시 읽으세요.");
                }
                if (entry.Ignored) ErrorHandler.ThrowOnFailure(fileChanges.SyncFile(entry.Path));
            }
            catch (Exception) { notices.Add(System.IO.Path.GetFileName(entry.Path) + ": 프로젝트 자동 동기화를 완료하지 못했습니다. 디스크와 프로젝트 내용을 확인하세요."); }
            finally
            {
                try { if (entry.HierarchyIgnored) ErrorHandler.ThrowOnFailure(entry.Persist.IgnoreItemFileChanges(VSConstants.VSITEMID_ROOT, 0)); }
                catch (Exception) { notices.Add("프로젝트 계층 변경 알림 복원을 확인하세요."); }
                try { if (entry.Ignored) ErrorHandler.ThrowOnFailure(fileChanges.IgnoreFile(0, entry.Path, 0)); }
                catch (Exception) { notices.Add("프로젝트 파일 변경 알림 복원을 확인하세요."); }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReloadProject
        /// desc         : 계층 재로딩이 없는 기본 C# 프로젝트는 솔루션 API로 다시 읽고 닫힌 텍스트 탭을 복원한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 실제 VS2017의 PersistHierarchyItem2 미지원 관측에 따른 호환 경로.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ReloadProject(ProtectedProject entry)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (entry.Persist != null) { ErrorHandler.ThrowOnFailure(entry.Persist.ReloadItem(VSConstants.VSITEMID_ROOT, 0)); return; }
            var paths = new List<string>();
            foreach (Document document in automation.Documents)
            {
                if (!document.Saved) { notices.Add("미저장 문서를 보존하기 위해 프로젝트 다시 읽기를 보류했습니다."); return; }
                if (String.Equals(document.Kind, EnvDTE.Constants.vsDocumentKindText, StringComparison.OrdinalIgnoreCase) && File.Exists(document.FullName)) paths.Add(document.FullName);
            }
            var activeWindow = automation.ActiveWindow;
            // Loaded legacy C# projects expose IPersistFileFormat but not IVsPersistHierarchyItem2.
            // The solution API requires unload before reload; always attempt reload after a successful unload.
            ErrorHandler.ThrowOnFailure(projectSolution.UnloadProject(ref entry.Id, (uint)_VSProjectUnloadStatus.UNLOADSTATUS_UnloadedByUser));
            try { ErrorHandler.ThrowOnFailure(projectSolution.ReloadProject(ref entry.Id)); }
            finally
            {
                foreach (string path in paths)
                {
                    bool open = false;
                    foreach (Document document in automation.Documents) if (String.Equals(document.FullName, path, StringComparison.OrdinalIgnoreCase)) { open = true; break; }
                    if (!open && File.Exists(path)) try { automation.ItemOperations.OpenFile(path, EnvDTE.Constants.vsViewKindTextView); } catch (Exception) { notices.Add(System.IO.Path.GetFileName(path) + ": 편집기 탭을 다시 열어 주세요."); }
                }
                try { if (activeWindow != null) activeWindow.Activate(); } catch (Exception) { }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Release
        /// desc         : 한 문서의 재로딩 실패와 독립적으로 알림·읽기 전용 상태를 반드시 복원한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 닫기·일반 완료·취소에서 같은 정리 경로 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Release(ProtectedDocument entry)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
                try {
                    if (entry.Ignored && File.Exists(entry.Path) && Fingerprint(entry.Path) != entry.Fingerprint) {
                        int dirty; ErrorHandler.ThrowOnFailure(entry.Persist.IsDocDataDirty(out dirty));
                        if (dirty == 0) ErrorHandler.ThrowOnFailure(entry.Persist.ReloadDocData(0));
                        else notices.Add(Path.GetFileName(entry.Path) + ": 실행 중 발생한 미저장 편집을 보존했습니다. 디스크의 AI 변경과 비교하세요.");
                    } else if (!File.Exists(entry.Path)) notices.Add(Path.GetFileName(entry.Path) + ": 디스크에서 삭제되어 열린 편집기 원문을 보존했습니다.");
                    if (entry.Ignored) ErrorHandler.ThrowOnFailure(fileChanges.SyncFile(entry.Path));
                } catch (Exception) { notices.Add(Path.GetFileName(entry.Path) + ": 편집기 자동 동기화를 완료하지 못했습니다. 저장 전에 디스크와 비교하세요."); }
                finally {
                    try { if (entry.Ignored) ErrorHandler.ThrowOnFailure(entry.ChangeControl.IgnoreFileChanges(0)); } catch (Exception) { notices.Add("문서 변경 알림 복원을 확인하세요."); }
                    try { if (entry.StateProtected) { uint state; ErrorHandler.ThrowOnFailure(entry.Buffer.GetStateFlags(out state)); ErrorHandler.ThrowOnFailure(entry.Buffer.SetStateFlags((state & ~(uint)BUFFERSTATEFLAGS.BSF_USER_READONLY) | (entry.OriginalState & (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY))); } } catch (Exception) { notices.Add("문서의 편집 가능 상태 복원을 확인하세요."); }
                }
        }
    }
}
