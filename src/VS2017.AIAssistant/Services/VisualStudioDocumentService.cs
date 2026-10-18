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
    /// update date  : 2026-10-18 CodexCode : 전체 액세스 전에 모든 열린 미저장 문서를 차단하고 소스 문맥은 선택 사항으로 제공.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class VisualStudioDocumentService : IActiveDocumentService, ISourceNavigationService, IFullAccessDocumentService, IExternalEditingDocumentService, IDisposable
    {
        private readonly DTE dte;
        private readonly IServiceProvider services;
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal VisualStudioDocumentService(IServiceProvider services)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.services = services;
            object automation = services.GetService(typeof(DTE));
            if (automation == null) return;
            dte = automation as DTE;
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
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
        /// name         : ResolveOpenDocument
        /// desc         : 활성·최근 문서가 현재 열린 집합에 남아 있는지 확인하고 닫힌 COM 참조를 버린다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 프로젝트 재로딩·CloseAll 뒤 VS2022의 오래된 문서 참조 재사용 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private Document ResolveOpenDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (dte == null) return null;
            try
            {
                var candidate = dte.ActiveDocument ?? lastDocument;
                if (candidate == null) return null;
                string path = candidate.FullName;
                foreach (Document opened in dte.Documents)
                    if (Object.ReferenceEquals(opened, candidate) || (!String.IsNullOrWhiteSpace(path) && String.Equals(opened.FullName, path, StringComparison.OrdinalIgnoreCase))) return opened;
            }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException || error is ArgumentException) { }
            lastDocument = null;
            return null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetActiveDocument
        /// desc         : 실제 파일이 있는 활성 텍스트 문서만 선택하고 미저장 편집 내용은 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 솔루션 부재·디자이너·새 문서 오류 안내.
        /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        ///                2026-10-19 CodexOrig : 현재 열린 문서에서만 활성 대상을 확인한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        public IEditorDocument GetActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (dte == null) throw new InvalidOperationException("Visual Studio 문서 서비스를 사용할 수 없습니다.");
            Document active = ResolveOpenDocument();
            if (active == null || string.IsNullOrWhiteSpace(active.FullName) || !File.Exists(active.FullName))
                throw new InvalidOperationException("수정할 소스 파일을 편집기에서 열어 주세요. 새 문서는 먼저 저장하세요.");
            var text = active.Object("TextDocument") as TextDocument;
            if (text == null) throw new InvalidOperationException("현재 문서는 텍스트 편집 문서가 아닙니다.");
            if (dte.UndoContext.IsOpen) throw new InvalidOperationException("다른 편집 작업이 진행 중입니다. 완료 후 다시 시도하세요.");
            return new VisualStudioEditorDocument(dte, active, text);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureFullAccessContext
        /// desc         : 직접 파일 쓰기 전에 열린 모든 미저장 문서를 거부하고 가능한 활성 텍스트 문맥만 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-020 전체 폴더 작업의 소스 선택 의무를 없애되 다른 버퍼의 미저장 편집을 보호.
        ///                2026-10-19 CodexOrig : 재로딩 뒤 닫힌 COM 문서는 소스 없는 프로젝트 문맥으로 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot CaptureFullAccessContext()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (dte == null || dte.Documents == null) throw new InvalidOperationException("열린 문서의 저장 상태를 확인할 수 없어 전체 액세스를 실행하지 않았습니다.");
            foreach (Document opened in dte.Documents)
                if (opened != null && !opened.Saved)
                    throw new InvalidOperationException("전체 액세스를 실행하기 전에 열려 있는 모든 파일의 편집 내용을 저장하세요. 새 문서는 먼저 파일로 저장하세요.");
            Document active = ResolveOpenDocument();
            if (active == null || string.IsNullOrWhiteSpace(active.FullName) || !File.Exists(active.FullName)) return null;
            TextDocument selected;
            try { selected = active.Object("TextDocument") as TextDocument; }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException || error is ArgumentException) { return null; }
            if (selected == null) return null;
            if (dte.UndoContext.IsOpen) throw new InvalidOperationException("다른 편집 작업이 진행 중입니다. 완료 후 다시 시도하세요.");
            return new VisualStudioEditorDocument(dte, active, selected).Capture();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BeginExternalEditing
        /// desc         : 직접 수정 전에 문서별 보호·알림 중지 세션을 생성한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 20:25 외부 변경 모달 없이 저장된 문서를 동기화.
        /// </summary>
        /// ---------------------------------------------------------------------
        public IExternalEditingScope BeginExternalEditing(Action stopExecution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return new VisualStudioExternalEditingScope(services, dte, stopExecution);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigateTo
        /// desc         : 비교 카드의 대상 소스 파일과 변경 줄을 실제 편집기에서 연다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 사용자 클릭에 의한 파일 탐색 구현.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        ///                2026-10-19 CodexCode : Markdown 파일 링크를 항상 텍스트 뷰로 열어 OS 실행 연결 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void NavigateTo(string filePath, int line)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (dte == null || !File.Exists(filePath)) throw new InvalidOperationException("소스 파일을 찾을 수 없습니다.");
            // TASK-024 : Markdown의 파일 링크도 실행 파일 연결 대신 항상 IDE 텍스트 편집기로 탐색한다.
            var window = dte.ItemOperations.OpenFile(Path.GetFullPath(filePath), EnvDTE.Constants.vsViewKindTextView); window.Activate();
            var selection = dte.ActiveDocument.Selection as TextSelection;
            if (selection != null) selection.GotoLine(Math.Max(1, line), true);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetProjectDirectory
        /// desc         : 선택 소스의 프로젝트 또는 파일 폴더를 작업 대상으로 제안한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 솔루션 없는 단일 파일도 지원.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// update date  : 2026-10-18 CodexCode : 열린 소스가 없어도 저장된 솔루션의 폴더를 작업 대상으로 사용.
        ///                2026-10-19 CodexOrig : 닫힌 최근 문서를 재사용하지 않고 솔루션 경로로 복귀.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string GetProjectDirectory()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var active = ResolveOpenDocument();
            if (active == null)
            {
                string solutionPath = dte == null || dte.Solution == null ? null : dte.Solution.FullName;
                if (!String.IsNullOrWhiteSpace(solutionPath) && Path.IsPathRooted(solutionPath) && File.Exists(solutionPath))
                    return Path.GetDirectoryName(solutionPath);
                throw new InvalidOperationException("프로젝트 소스 파일을 선택하거나 저장된 솔루션을 열어 주세요.");
            }
            var projectPath = active.ProjectItem == null || active.ProjectItem.ContainingProject == null ? null : active.ProjectItem.ContainingProject.FullName;
            return Path.GetDirectoryName(string.IsNullOrEmpty(projectPath) ? active.FullName : projectPath);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : VisualStudioEditorDocument
    /// desc         : 디스크 쓰기 대신 IDE 버퍼·UndoContext·저장 기능으로 한 문서를 수정한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 파일 경로 고정 및 편집·디스크 충돌 검사.
    /// update date  : 2026-10-18 CodexCode : 시큐어 코딩 검토의 디스크 SHA-256 읽기를 최대 2MiB로 제한.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class VisualStudioEditorDocument : IEditorDocument
    {
        internal const int MaximumDiskHashBytes = 2 * 1024 * 1024;
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// update date  : 2026-10-18 CodexCode : 감사 F06의 연속 선택 코드·줄 정보를 전체 버퍼와 별도 고정.
        /// update date  : 2026-10-18 CodexCode : 크거나 계속 증가하는 디스크 파일의 무제한 UI 스레드 읽기 차단.
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
            { diskVersion = ComputeBoundedDiskVersion(file); }
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
            string selectionError; DocumentSelection selection = CaptureSelection(content, out selectionError);
            return new DocumentSnapshot(filePath, content, diskVersion, context, !document.Saved, readOnly, selection, selectionError);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ComputeBoundedDiskVersion
        /// desc         : 디스크 원문을 변경하거나 스트림을 닫지 않고 최대 2MiB까지만 SHA-256 버전으로 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 시큐어 코딩 검토의 계속 커지는 파일·비탐색 스트림 무제한 읽기 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string ComputeBoundedDiskVersion(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new ArgumentException("디스크 원문을 읽을 수 없는 스트림입니다.", nameof(input));
            if (input.CanSeek && input.Length > MaximumDiskHashBytes)
                throw new InvalidOperationException("디스크 원문은 최대 2MiB까지 검사할 수 있습니다. 더 작은 소스 파일로 작업하세요.");
            using (var hash = SHA256.Create())
            {
                byte[] buffer = new byte[8192]; int total = 0, count;
                while ((count = input.Read(buffer, 0, Math.Min(buffer.Length, MaximumDiskHashBytes + 1 - total))) > 0)
                {
                    total += count;
                    if (total > MaximumDiskHashBytes)
                        throw new InvalidOperationException("디스크 원문이 읽는 중 2MiB 제한을 초과했습니다. 수정 요청을 다시 생성하세요.");
                    hash.TransformBlock(buffer, 0, count, null, 0);
                }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                return Convert.ToBase64String(hash.Hash);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureSelection
        /// desc         : 원래 문서의 연속 선택을 포커스 이동 없이 읽고 전체 버퍼와 정확히 대조한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 역방향·CRLF 위치 보존 및 사각형·가상 공백 요청의 명시 오류 기록.
        /// </summary>
        /// ---------------------------------------------------------------------
        private DocumentSelection CaptureSelection(string content, out string error)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            error = "";
            try
            {
                TextSelection selected = text.Selection;
                if (selected == null) { error = "선택한 코드를 읽을 수 없습니다. 소스 편집기를 선택한 뒤 다시 요청하세요."; return null; }
                if (selected.IsEmpty) return null;
                if (selected.Mode != vsSelectionMode.vsSelectionModeStream) { error = "사각형 선택은 지원하지 않습니다. 일반 연속 영역으로 선택한 뒤 다시 요청하세요."; return null; }
                VirtualPoint top = selected.TopPoint, bottom = selected.BottomPoint;
                if (top == null || bottom == null) throw new InvalidOperationException("선택 끝점을 읽을 수 없습니다.");
                if (top.VirtualCharOffset != top.LineCharOffset || bottom.VirtualCharOffset != bottom.LineCharOffset)
                { error = "가상 공백이 포함된 선택은 지원하지 않습니다. 실제 소스 문자만 선택하세요."; return null; }
                // DTE AbsoluteCharOffset은 1기준이고 줄바꿈을 한 문자로 센다. CRLF 원문의 UTF-16 위치에는 직접 사용하지 않는다.
                string prefix = text.StartPoint.CreateEditPoint().GetText(top);
                string selectedText = top.CreateEditPoint().GetText(bottom);
                if (prefix == null || selectedText == null) throw new InvalidOperationException("선택 원문을 읽을 수 없습니다.");
                if (prefix.Length > content.Length || string.CompareOrdinal(content, 0, prefix, 0, prefix.Length) != 0)
                    throw new InvalidOperationException("선택 시작 위치가 현재 문서와 일치하지 않습니다.");
                var selection = DocumentSelection.FromOffsets(content, prefix.Length, selectedText.Length);
                if (selection == null || !string.Equals(selection.Text, selectedText, StringComparison.Ordinal) ||
                    selection.StartLine != top.Line || selection.StartColumn != top.LineCharOffset || selection.EndLine != bottom.Line || selection.EndColumn != bottom.LineCharOffset)
                    throw new InvalidOperationException("선택 내용이나 줄 위치가 현재 문서와 일치하지 않습니다.");
                return selection;
            }
            catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException || exception is ArgumentException || exception is InvalidOperationException)
            {
                error = "선택한 코드를 정확히 읽지 못했습니다. 소스 편집기에서 다시 선택한 뒤 요청하세요.";
                return null;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Replace
        /// desc         : 승인된 원본 일치를 재확인하고 한 Undo 그룹으로 버퍼를 교체한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 편집 실패 시 UndoContext 중단 및 파일 자동 저장 차단.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
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
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
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
