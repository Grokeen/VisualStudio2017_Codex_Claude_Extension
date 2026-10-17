using System;
using System.IO;
using System.Security.Cryptography;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VS2017.AIAssistant.Editing;

namespace VS2017.AIAssistant.Services
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : VisualStudioDocumentService
    /// desc         : VS2017 활성 텍스트 문서를 현재 파일 전용 편집 어댑터로 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : EnvDTE 문서 컨텍스트 연결.
    /// update date  : 2026-10-18 CodexOrig : Chat 문서 탭 활성화 시 마지막 소스 문서 유지.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class VisualStudioDocumentService : IActiveDocumentService, IDisposable
    {
        private readonly DTE dte;
        private readonly WindowEvents windowEvents;
        private readonly DocumentEvents documentEvents;
        private Document lastDocument;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : VisualStudioDocumentService
        /// desc         : 도구 창의 패키지 서비스 공급자를 보관한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 서비스 초기화.
        /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal VisualStudioDocumentService(IServiceProvider services)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            dte = services.GetService(typeof(DTE)) as DTE;
            if (dte != null)
            {
                lastDocument = dte.ActiveDocument;
                windowEvents = dte.Events.WindowEvents; documentEvents = dte.Events.DocumentEvents;
                windowEvents.WindowActivated += WindowActivated;
                documentEvents.DocumentClosing += DocumentClosing;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WindowActivated
        /// desc         : Chat 탭과 전환하기 직전의 실제 소스 문서를 기억한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 문서 없는 도구 창은 대상으로 삼지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void WindowActivated(Window gotFocus, Window lostFocus)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (gotFocus != null && gotFocus.Document != null) lastDocument = gotFocus.Document;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DocumentClosing
        /// desc         : 닫힌 문서를 다음 요청의 대상으로 재사용하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : COM 문서 수명에 맞춰 캐시 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DocumentClosing(Document closing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (lastDocument == closing) lastDocument = null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : Chat 창 해제 시 DTE 이벤트 구독을 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : IDE 종료 후 참조 누수 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (windowEvents != null) windowEvents.WindowActivated -= WindowActivated;
            if (documentEvents != null) documentEvents.DocumentClosing -= DocumentClosing;
            lastDocument = null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetActiveDocument
        /// desc         : 실제 파일이 있는 활성 텍스트 문서만 선택하고 미저장 편집 내용은 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 솔루션 부재·디자이너·새 문서 오류 안내.
        /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
        /// </summary>
        /// ---------------------------------------------------------------------
        public IEditorDocument GetActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (dte == null) throw new InvalidOperationException("Visual Studio 문서 서비스를 사용할 수 없습니다.");
            Document active = dte.ActiveDocument ?? lastDocument;
            if (active == null || string.IsNullOrWhiteSpace(active.FullName) || !File.Exists(active.FullName))
                throw new InvalidOperationException("수정할 소스 파일을 편집기에서 열어 주세요. 새 문서는 먼저 저장하세요.");
            var text = active.Object("TextDocument") as TextDocument;
            if (text == null) throw new InvalidOperationException("현재 문서는 텍스트 편집 문서가 아닙니다.");
            if (dte.UndoContext.IsOpen) throw new InvalidOperationException("다른 편집 작업이 진행 중입니다. 완료 후 다시 시도하세요.");
            return new VisualStudioEditorDocument(dte, active, text);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : VisualStudioEditorDocument
    /// desc         : 디스크 쓰기 대신 IDE 버퍼·UndoContext·저장 기능으로 한 문서를 수정한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 파일 경로 고정 및 편집·디스크 충돌 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class VisualStudioEditorDocument : IEditorDocument
    {
        private readonly DTE dte;
        private readonly Document document;
        private readonly TextDocument text;
        private readonly string filePath;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : VisualStudioEditorDocument
        /// desc         : 요청 시점의 문서 COM 객체와 경로를 고정하여 다른 파일 수정을 막는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 문서 어댑터 초기화.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal VisualStudioEditorDocument(DTE dte, Document document, TextDocument text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.dte = dte;
            this.document = document;
            this.text = text;
            filePath = Path.GetFullPath(document.FullName);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Capture
        /// desc         : 저장되지 않은 버퍼와 디스크 바이트 해시, 제공 가능한 프로젝트 조건을 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 외부 변경 감지 및 읽기 전용 영역 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot Capture()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!string.Equals(filePath, Path.GetFullPath(document.FullName), StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
                throw new InvalidOperationException("대상 파일의 이름이나 경로가 변경되었습니다.");
            EditPoint start = text.StartPoint.CreateEditPoint();
            if (text.EndPoint.AbsoluteCharOffset > EditProposal.MaxDocumentLength * 2)
                throw new InvalidOperationException("문서가 너무 큽니다. 더 작은 소스 파일로 작업하세요.");
            string content = start.GetText(text.EndPoint);
            string diskVersion;
            using (var file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var hash = SHA256.Create()) { diskVersion = Convert.ToBase64String(hash.ComputeHash(file)); }
            string context = "Language: " + text.Language + "; File type: " + Path.GetExtension(filePath);
            try
            {
                Project project = document.ProjectItem == null ? null : document.ProjectItem.ContainingProject;
                if (project != null)
                {
                    context += "; Project: " + project.Name;
                    context += "; Framework: " + Convert.ToString(project.Properties.Item("TargetFrameworkMoniker").Value);
                }
            }
            catch (Exception) { context += "; Framework: unknown (preserve existing syntax and APIs)"; }
            bool readOnly = document.ReadOnly || start.ReadOnly(text.EndPoint) ||
                (File.GetAttributes(filePath) & FileAttributes.ReadOnly) != 0;
            return new DocumentSnapshot(filePath, content, diskVersion, context, !document.Saved, readOnly);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Replace
        /// desc         : 승인된 원본 일치를 재확인하고 한 Undo 그룹으로 버퍼를 교체한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 편집 실패 시 UndoContext 중단 및 파일 자동 저장 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Replace(DocumentSnapshot expected, string content)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            expected.RequireUnchanged(Capture());
            if (dte.UndoContext.IsOpen) throw new InvalidOperationException("다른 편집 작업이 진행 중입니다.");
            dte.UndoContext.Open("AI Assistant 소스 수정", false);
            try
            {
                text.StartPoint.CreateEditPoint().ReplaceText(text.EndPoint, content,
                    (int)vsEPReplaceTextOptions.vsEPReplaceTextKeepMarkers);
            }
            catch
            {
                dte.UndoContext.SetAborted();
                throw;
            }
            finally { if (dte.UndoContext.IsOpen) dte.UndoContext.Close(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Save
        /// desc         : 별도 저장 요청에 한해 문서 상태를 확인하고 IDE가 기존 인코딩으로 저장한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 저장 취소·실패를 성공으로 보고하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Save(DocumentSnapshot expected)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            expected.RequireUnchanged(Capture());
            if (document.Save(filePath) != vsSaveStatus.vsSaveSucceeded)
                throw new InvalidOperationException("문서 저장이 취소되었거나 실패했습니다. 편집기 내용을 확인하세요.");
        }
    }
}
