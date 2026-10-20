using System;

namespace VS2017.AIAssistant.Editing
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IExternalEditingDocumentService
    /// desc         : 네이티브 직접 수정 동안 열린 편집기를 보호하고 완료 시 디스크와 동기화한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 20:25 외부 변경 모달 없이 안전하게 반영하는 세션 계약.
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface IExternalEditingDocumentService
    {
        IExternalEditingScope BeginExternalEditing(Action stopExecution);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IExternalEditingScope
    /// desc         : 세션 종료 시 알림·편집 가능 상태를 복원하고 보존한 문서의 안내를 제공한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 중지·실패에도 복원하는 외부 편집 범위.
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface IExternalEditingScope : IDisposable
    {
        string Notice { get; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IExternalEditingScopeState
    /// desc         : 원래 읽기 전용 문서와 실행 중 자신이 임시 보호한 문서를 구분한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 다른 대화의 보호 상태가 원래 쓰기 제한을 우회하지 않게 확인.
    /// </summary>
    /// ---------------------------------------------------------------------
    public interface IExternalEditingScopeState
    {
        bool IsTemporarilyReadOnly(string filePath);
    }
}
