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
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class VisualStudioExternalEditingScope : IExternalEditingScope
    {
        private readonly IVsRunningDocumentTable table;
        private readonly IVsFileChangeEx fileChanges;
        private readonly DocumentEvents events;
        private readonly Action stopExecution;
        private readonly Dictionary<string, ProtectedDocument> documents = new Dictionary<string, ProtectedDocument>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> notices = new List<string>();
        private long protectedBytes;
        private bool disposed;
        public string Notice { get { return String.Join("\n", notices); } }

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
            internal bool Ignored, StateProtected;
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
            events = dte.Events.DocumentEvents;
            try {
                foreach (Document document in dte.Documents) if (!document.Saved) throw new InvalidOperationException("직접 수정 전에 열린 미저장 문서를 모두 저장하세요.");
                foreach (Document document in dte.Documents) Protect(document);
                events.DocumentOpened += DocumentOpened;
                events.DocumentClosing += DocumentClosing;
            } catch { Dispose(); throw; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Protect
        /// desc         : 텍스트 문서의 알림을 중지하고 원래 상태를 보존한 임시 읽기 전용을 설정한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : RDT·DocData의 중첩 알림 중지를 균형 있게 관리.
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
