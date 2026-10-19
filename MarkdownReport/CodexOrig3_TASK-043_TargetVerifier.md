# TASK-043 후속 — 대상 PC 설치 검증 절차

완료 시각: 2026-10-19 12:46:20 KST

새 `VerifyOfflineSetup.bat` 및 `scripts/Test-OfflineSetupOnTarget.ps1`를 읽고 동작을 대조한 뒤 `docs/OFFLINE_INSTALL.md`에 사용 절차를 추가했다. 안내에는 사용자 확인만으로 오프라인을 증명하거나 방화벽을 변경하지 않는 점, 열린 Visual Studio 종료와 IDE 다중 선택, 로컬 사전검사 후 CLI 설치 및 임시 프로필의 `--version` 확인, 공식 VSIXInstaller UI 실행, 로그인/AI 요청은 하지 않는 점을 담았다. 직접 PowerShell 실행은 기본 진단 모드이며 `ReadyDiagnosticOnly`는 설치 완료가 아니라고 설명했다.

`%TEMP%\\OfflineSetupVerify-<GUID>\\RESULT.json` 및 CLI/VSIX 로그 위치, `InstalledPendingIdeCheck` 이후 사용자가 IDE를 열어 도구 창을 확인할 책임, `InstallerSucceededPendingInstalledFileCheck`와 `Failed` 상태의 의미, 지원 요청에 전달할 최소 항목(RESULT 상태, 오류 문구, 도구 창 열림 여부)과 계정·암호·토큰 비공유 안내를 추가했다.

검증: `git diff --check -- docs/OFFLINE_INSTALL.md` 통과. 문서 SHA-256 `4C387A0A5B3959184D1AF8A3868E64A3FA1986BE728B43F91BF69CAE71227255`. 이번 작업은 문서만 수정했으며 실제 대상 PC 설치/테스트나 배포는 수행하지 않았다. 제품·README·PDF·설치 스크립트·릴리스 변경 0. 상세 기록과 TASK043 문서 소유권을 CodexOrig에 반납한다.