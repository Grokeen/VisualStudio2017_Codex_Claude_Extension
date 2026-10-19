# TASK-043 — 오프라인 설치 문서 후속

완료 시각: 2026-10-19 12:37:31 KST

요청 범위에 따라 `docs/OFFLINE_INSTALL.md`만 수정했다.

- 운영체제 범위를 Windows 10 x64 버전 1809 이상 또는 Windows 11 x64로 명시하고, 이 조건이 CLI 런타임 기준이며 Visual Studio 패키지 호환성과 별도임을 설명했다.
- 인터넷 준비 PC에서 사용하는 준비 도구 ZIP과 그 도구로 생성하여 오프라인 대상 PC로 옮기는 완성 설치 ZIP을 구분했다. 준비 도구 ZIP만으로는 오프라인 CLI 런타임 설치가 완료되지 않는다고 덧붙였다.
- 네트워크 격리 시험은 제한된 Windows AppContainer의 외부 TCP 거부 관측이며, 물리 네트워크 케이블 차단이나 고객 PC에서의 실제 Visual Studio/VSIX 설치 검증이 아님을 명시했다. 진행 중인 검사 숫자는 문서에 기재하지 않았다.

검증: `git diff --check -- docs/OFFLINE_INSTALL.md` 통과. 문서 SHA-256 `B16F856768F485B67225D23CA7B04E6015208DF83902D23336FB29D040C96D68`. 제품 소스·README·설치 스크립트·기존 릴리스·PDF는 변경하지 않았다. `WORK_STATUS.md`에 완료 및 소유권 반환을 기록했으며, 문서만 CodexOrig에 반납한다.