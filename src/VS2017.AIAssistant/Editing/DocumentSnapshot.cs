using System;
using System.IO;

namespace VS2017.AIAssistant.Editing
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DocumentSnapshot
    /// desc         : AI에 전달한 편집기 내용과 디스크 버전을 보관하는 불변 스냅샷.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 미저장 내용과 외부 변경 구분 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class DocumentSnapshot
    {
        public string FilePath { get; private set; }
        public string Content { get; private set; }
        public string DiskVersion { get; private set; }
        public string ProjectContext { get; private set; }
        public bool HasUnsavedChanges { get; private set; }
        public bool IsReadOnly { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DocumentSnapshot
        /// desc         : 파일 식별자, 내용, 디스크 해시 및 편집 가능 상태를 고정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 문서 스냅샷 초기화.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot(string filePath, string content, string diskVersion,
            string projectContext, bool hasUnsavedChanges, bool isReadOnly)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("파일 경로가 없습니다.");
            FilePath = Path.GetFullPath(filePath);
            Content = content ?? throw new ArgumentNullException(nameof(content));
            DiskVersion = diskVersion ?? throw new ArgumentNullException(nameof(diskVersion));
            ProjectContext = projectContext ?? "프로젝트 정보 없음";
            HasUnsavedChanges = hasUnsavedChanges;
            IsReadOnly = isReadOnly;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequireUnchanged
        /// desc         : 파일 교체, 읽기 전용, 편집기 및 외부 디스크 변경을 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 적용·저장·복구 공통 충돌 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void RequireUnchanged(DocumentSnapshot current)
        {
            if (current == null || !string.Equals(FilePath, current.FilePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("대상 문서가 변경되었습니다. 수정안을 다시 생성하세요.");
            if (current.IsReadOnly) throw new InvalidOperationException("읽기 전용 문서는 수정할 수 없습니다.");
            if (!string.Equals(Content, current.Content, StringComparison.Ordinal))
                throw new InvalidOperationException("편집 내용이 변경되었습니다. 수정안을 다시 생성하세요.");
            if (!string.Equals(DiskVersion, current.DiskVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("디스크의 파일이 외부에서 변경되었습니다. 파일을 확인하세요.");
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IEditorDocument
    /// desc         : 실제 IDE 편집과 테스트 문서를 분리하는 현재 파일 편집 계약.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 예상 스냅샷을 검증한 교체·저장 계약 정의.
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface IEditorDocument
    {
        DocumentSnapshot Capture();
        void Replace(DocumentSnapshot expected, string content);
        void Save(DocumentSnapshot expected);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IActiveDocumentService
    /// desc         : 사용자가 수정 요청한 활성 텍스트 문서를 선택한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : IDE 문서 선택 계약 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface IActiveDocumentService
    {
        IEditorDocument GetActiveDocument();
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ISourceNavigationService
    /// desc         : 변경 파일 링크와 프로젝트 작업 대상 폴더를 IDE에 연결한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 기존 문서 서비스와 독립된 탐색 계약 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface ISourceNavigationService
    {
        void NavigateTo(string filePath, int line);
        string GetProjectDirectory();
    }
}
