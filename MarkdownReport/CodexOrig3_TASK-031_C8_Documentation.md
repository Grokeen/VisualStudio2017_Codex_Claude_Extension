# CodexOrig3 TASK-031 — candidate8 문서 문구 정정

완료 시각: 2026-10-19 03:14:25 KST

candidate8 고정 소스에서 일반 Markdown 표의 실제 표시 열은 최대 16개이며, 17번째 이후 열이 감지되면 공통 생략 안내(`미리보기 한도로 일부 내용을 생략했습니다. 편집 탭과 원문은 변경되지 않았습니다.`)가 추가됨을 확인했다. 근거는 `artifacts/task025/candidate8/source/src/VS2017.AIAssistant/Views/MarkdownRendering.cs`의 Table/Cells/Render 및 `artifacts/task025/markdown-f4-source-fixed.log`(111 passed, 0 failed)이다.

README와 USER_GUIDE에서 기존의 HTML 표 제한 설명을 일반 Markdown 표와 구분했다. README는 일반 Markdown 표 16열, 정적 HTML 표 128행·16열 및 한도 초과 시 안내로 설명한다. USER_GUIDE도 같은 범위를 안내한다. README의 소유자 갱신 버전·다운로드 경로는 보존했다. C7 설명서 원문/PDF/그림은 재생성하거나 수정하지 않았다.

## 상태 경계

- candidate8 readiness: 120 입력, 6개 빌드 모두 오류 0·경고 0, Markdown source checks 111/111.
- 실제 UI 확인은 readiness에서 `ActualUiExpected=231`, `ActualUiVerified=false`로 남아 있다. 최종 IDE 감사 완료로 표현하지 않는다.
- candidate8 Manifest SHA-256: `08AE13DE57637766A994868ACBDEB27A8F2FC587CD1786895EDCAD47FF7B0787`
- VS2017 candidate8 DLL SHA-256: `A2A10A3922CC3FD47F5AA10E15FFF7FCB70A44494D4B26BEE22610B3C4C2172E`

## 파일 해시 (SHA-256)

| 파일 | SHA-256 |
|---|---|
| `README.md` | `6661161DF0EAA018FAA792C7C5C25C518E9F06E67B8300A8E1DA075D5F4460A0` |
| `docs/USER_GUIDE.md` | `70F50AA9D9187EB04A2207598D4F0D368F7FC24B832F99E6858D817FA1E00C48` |
| 보존 확인: C7 PDF | `EF6C8A3B5BE274F6FDAB77FBA6E07221F9E33B13E85B574380625EB0E3AF5453` |
| 보존 확인: C7 Markdown | `168895E4350FA973465537B966A8E1695F58B7E80901D4AB578B12B7A39D130C` |
