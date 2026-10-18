namespace VS2017.AIAssistant.Editing
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IFullAccessDocumentService
    /// desc         : 직접 파일 수정 전에 열린 미저장 버퍼를 검사하고 선택 가능한 편집 문맥을 제공한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 전체 액세스의 편집기 충돌 경계.
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface IFullAccessDocumentService
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaptureFullAccessContext
        /// desc         : 미저장 문서를 검사한 뒤 선택 가능한 활성 문맥 또는 null을 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 실행·감사·보안 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        DocumentSnapshot CaptureFullAccessContext();
    }
}
