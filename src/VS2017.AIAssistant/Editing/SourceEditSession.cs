using System;

namespace VS2017.AIAssistant.Editing
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SourceEditSession
    /// desc         : 승인된 수정안의 편집기 적용·별도 저장·직전 변경 복구를 관리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 승인 전 쓰기 차단 및 변경 충돌 검사 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class SourceEditSession
    {
        private IEditorDocument document;
        private DocumentSnapshot before;
        private DocumentSnapshot applied;
        public bool CanRestore { get { return document != null; } }
        public bool CanSave { get { return applied != null && applied.HasUnsavedChanges; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Apply
        /// desc         : 명시적 승인과 원본 일치를 확인한 뒤 편집기 버퍼를 교체한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 적용 시 디스크를 자동 저장하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Apply(IEditorDocument target, EditProposal proposal, bool approved)
        {
            if (!approved) throw new InvalidOperationException("변경 비교 후 적용 승인에 체크하세요.");
            if (target == null || proposal == null) throw new InvalidOperationException("적용할 수정안이 없습니다.");
            if (!proposal.HasChanges) throw new InvalidOperationException("적용할 변경 내용이 없습니다.");
            proposal.Source.RequireUnchanged(target.Capture());
            target.Replace(proposal.Source, proposal.ProposedText);
            document = target;
            before = proposal.Source;
            // 저장·복구 전에 적용 직후 상태와 다시 비교하므로 이후 사용자 편집을 덮어쓰지 않는다.
            applied = new DocumentSnapshot(before.FilePath, proposal.ProposedText, before.DiskVersion,
                before.ProjectContext, true, false);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Save
        /// desc         : 사용자의 별도 저장 명령에서 적용 내용과 디스크 충돌을 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 파일 인코딩을 IDE 저장 기능에 위임.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Save()
        {
            if (document == null) throw new InvalidOperationException("저장할 AI 변경이 없습니다.");
            applied.RequireUnchanged(document.Capture());
            document.Save(applied);
            applied = document.Capture();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Restore
        /// desc         : 직전 AI 적용 이후 문서가 그대로인 경우에만 원래 편집 내용을 복구한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 미저장 원본 내용까지 복구하며 저장은 별도 수행.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Restore(bool approved)
        {
            if (!approved) throw new InvalidOperationException("복구 요청이 승인되지 않았습니다.");
            if (document == null) throw new InvalidOperationException("복구할 AI 변경이 없습니다.");
            applied.RequireUnchanged(document.Capture());
            document.Replace(applied, before.Content);
            document = null;
            applied = null;
            before = null;
        }
    }
}
