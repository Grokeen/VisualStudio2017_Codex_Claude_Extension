using System;
using System.IO;

namespace VS2017.AIAssistant.Editing
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DocumentSelection
    /// desc         : 전체 문서에서 선택한 연속 영역의 원문과 0기준 UTF-16 범위·1기준 줄과 열을 고정한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 감사 F06의 선택 영역을 전체 파일 적용 기준과 분리.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class DocumentSelection
    {
        public string Text { get; private set; }
        public int StartOffset { get; private set; }
        public int Length { get; private set; }
        public int EndOffset { get { return StartOffset + Length; } }
        public int StartLine { get; private set; }
        public int StartColumn { get; private set; }
        public int EndLine { get; private set; }
        public int EndColumn { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DocumentSelection
        /// desc         : 검증된 전체 원문에서만 선택 내용을 만들고 종료 위치는 미포함 경계로 보관한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 탭은 표시 폭이 아닌 UTF-16 문자 한 칸으로 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private DocumentSelection(string content, int start, int length)
        {
            StartOffset = start; Length = length; Text = content.Substring(start, length);
            int line, column; Position(content, start, out line, out column); StartLine = line; StartColumn = column;
            Position(content, start + length, out line, out column); EndLine = line; EndColumn = column;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FromOffsets
        /// desc         : 범위와 줄바꿈 경계를 검사하여 불변 선택 영역을 만들며 빈 영역은 null로 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 원문 밖 범위·오버플로·CRLF 및 Unicode surrogate 분리 거부.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static DocumentSelection FromOffsets(string content, int startOffset, int length)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (startOffset < 0 || startOffset > content.Length || length < 0 || length > content.Length - startOffset)
                throw new ArgumentOutOfRangeException(nameof(startOffset), "선택 영역이 문서 범위를 벗어났습니다.");
            RequireBoundary(content, startOffset); RequireBoundary(content, startOffset + length);
            return length == 0 ? null : new DocumentSelection(content, startOffset, length);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FromEndpoints
        /// desc         : 정방향과 역방향 두 선택 끝점을 같은 시작·종료 영역으로 정규화한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 음수와 큰 끝점은 뺄셈 전에 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static DocumentSelection FromEndpoints(string content, int firstOffset, int secondOffset)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (firstOffset < 0 || secondOffset < 0 || firstOffset > content.Length || secondOffset > content.Length)
                throw new ArgumentOutOfRangeException(nameof(firstOffset), "선택 끝점이 문서 범위를 벗어났습니다.");
            int start = Math.Min(firstOffset, secondOffset); return FromOffsets(content, start, Math.Max(firstOffset, secondOffset) - start);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequireMatches
        /// desc         : 다른 버퍼에서 만든 선택 영역을 현재 전체 원문에 붙이는 것을 막는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 선택 원문과 전체 파일의 같은 범위가 정확히 일치해야 허용.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void RequireMatches(string content)
        {
            if (content == null || StartOffset > content.Length || Length > content.Length - StartOffset ||
                string.CompareOrdinal(content, StartOffset, Text, 0, Text.Length) != 0)
                throw new ArgumentException("선택한 코드가 현재 문서 원문과 일치하지 않습니다.");
            RequireBoundary(content, StartOffset); RequireBoundary(content, EndOffset);
            int line, column; Position(content, StartOffset, out line, out column);
            if (line != StartLine || column != StartColumn) throw new ArgumentException("선택한 코드의 시작 줄과 열이 현재 문서와 일치하지 않습니다.");
            Position(content, EndOffset, out line, out column);
            if (line != EndLine || column != EndColumn) throw new ArgumentException("선택한 코드의 종료 줄과 열이 현재 문서와 일치하지 않습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequireBoundary
        /// desc         : 편집기 커서로 나타낼 수 없는 CRLF 중간과 Unicode 문자 중간 경계를 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 잘린 문자나 모호한 종료 줄 정보를 요청에 넣지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RequireBoundary(string content, int offset)
        {
            if (offset <= 0 || offset >= content.Length) return;
            if (content[offset - 1] == '\r' && content[offset] == '\n') throw new ArgumentException("CRLF 줄바꿈 중간을 선택 경계로 사용할 수 없습니다.");
            if (char.IsHighSurrogate(content[offset - 1]) && char.IsLowSurrogate(content[offset])) throw new ArgumentException("Unicode 문자 중간을 선택 경계로 사용할 수 없습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Position
        /// desc         : CRLF·LF·CR 줄바꿈을 한 줄로 세고 1기준 문자 열을 계산한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 한글·탭·UTF-16과 마지막 빈 줄의 위치 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Position(string content, int offset, out int line, out int column)
        {
            line = 1; column = 1;
            for (int index = 0; index < offset; index++)
            {
                if (content[index] == '\r') { if (index + 1 < offset && content[index + 1] == '\n') index++; line++; column = 1; }
                else if (content[index] == '\n') { line++; column = 1; }
                else column++;
            }
        }
    }

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
        public DocumentSelection Selection { get; private set; }
        public bool HasSelection { get { return Selection != null; } }
        public string SelectionError { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DocumentSnapshot
        /// desc         : 기존 여섯 인수 생성자의 바이너리 계약을 유지하고 선택 없는 문서를 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 이미 컴파일된 소비자도 기존 생성자 주소를 계속 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot(string filePath, string content, string diskVersion,
            string projectContext, bool hasUnsavedChanges, bool isReadOnly)
            : this(filePath, content, diskVersion, projectContext, hasUnsavedChanges, isReadOnly, null, null) { }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DocumentSnapshot
        /// desc         : 파일 식별자, 내용, 디스크 해시 및 편집 가능 상태를 고정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 문서 스냅샷 초기화.
        /// update date  : 2026-10-18 CodexCode : 전체 원문을 유지한 선택 영역·추출 오류를 별도 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot(string filePath, string content, string diskVersion,
            string projectContext, bool hasUnsavedChanges, bool isReadOnly, DocumentSelection selection = null, string selectionError = null)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("파일 경로가 없습니다.");
            FilePath = Path.GetFullPath(filePath);
            Content = content ?? throw new ArgumentNullException(nameof(content));
            DiskVersion = diskVersion ?? throw new ArgumentNullException(nameof(diskVersion));
            ProjectContext = projectContext ?? "프로젝트 정보 없음";
            HasUnsavedChanges = hasUnsavedChanges;
            IsReadOnly = isReadOnly;
            SelectionError = selectionError ?? "";
            if (SelectionError.Length > 1024 || (selection != null && SelectionError.Length > 0)) throw new ArgumentException("선택 영역 상태가 올바르지 않습니다.");
            if (selection != null) selection.RequireMatches(Content);
            Selection = selection;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequireValidSelection
        /// desc         : 요청 시작에만 선택 추출 오류를 확인하고 선택 없음은 기존 전체 파일 요청으로 허용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 커서 이동·선택 모드 변경이 적용·저장 충돌 검사에 영향을 주지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void RequireValidSelection()
        { if (SelectionError.Length > 0) throw new InvalidOperationException(SelectionError); if (Selection != null) Selection.RequireMatches(Content); }

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
