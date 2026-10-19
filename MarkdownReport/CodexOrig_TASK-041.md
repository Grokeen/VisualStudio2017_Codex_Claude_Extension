# TASK-041 — 실제 통신 차단 검사와 오프라인 대상 PC 검증 준비

사용자는 오프라인 환경에서도 동일하게 설치되는지 확인한 다음 배포하도록 요청했다. 이번에는 이전 프록시 검사와 구분하여 Windows가 검사 프로세스의 통신을 거부하도록 하고 설치를 실행했다. 사용자에게 실제 오프라인 테스트 PC가 있다는 회신도 받았다. 준비 시점에는 실제 PC 결과를 받기 전 추가 공개 배포를 보류했다. 이후 사용자가 다운로드해 테스트할 수 있도록 직접 배포하라고 요청하여, 완성 ZIP을 별도 **테스트용 사전 릴리스 v0.11.1-offline-test.2**로 게시했다. 실제 PC의 VSIX 설치와 AI Assistant 창 확인 결과는 여전히 대기 중이다. 아래 테스트 배포 후속 기록과 배포 JSON을 현재 상태로 본다.

## 확인한 범위

Windows AppContainer에 네트워크 capability를 주지 않고 임시 프로필·전용 작업 폴더·종료 시 정리되는 자식 프로세스 job으로 검사를 실행했다. 호스트의 동일 IP:443 TCP 연결은 성공했고, 검사 프로세스의 실제 토큰은 AppContainer=1/capability=0이며 설치 전후 연결은 WSAEACCES 10013으로 거부됐다. 호스트의 방화벽 규칙이나 네트워크 설정은 변경하지 않았다. 케이블을 분리한 시험이나 고객 PC 시험을 뜻하지 않는다.

기존 C4는 46개 파일을 복사한 뒤 PowerShell `Move-Item`의 상위 경로 정규화 과정에서 접근 거부로 실패했다. 같은 경로의 직접 `.NET Directory.Move`는 성공했다. `scripts/Install-OfflineCli.ps1`은 기존 동일 부모·대상 미존재·경로 검사를 유지하고 stage의 연결 경로를 다시 검사한 뒤 `[IO.Directory]::Move`를 사용하도록 보완했다. 이 AppContainer 권한 제한을 고객 PC의 인터넷 장애 원인으로 단정하지 않는다.

새 완성 후보 C6 **원문 ZIP 그대로** 실행한 최종 네트워크 격리 검사는 6/6 통과했다. `UseWorkingInstaller`를 사용하지 않았다. CLI 46개 사전 검증·설치·재설치 및 Codex 0.163.0/Claude 2.1.302 버전 실행을 확인했다. AppContainer 프로필 삭제 결과는 0, 임시 폴더 DACL 복원은 true다. 실제 계정 인증·AI 요청은 수행하지 않았다.

공식 VSIXInstaller는 같은 AppContainer에서 0x80070005로 종료됐다. 그 제한은 정상 Windows 사용자 환경과 다르므로 실제 오프라인 VSIX 설치 실패나 성공으로 판정하지 않는다. 별도 오프라인 PC의 실제 결과를 기다린다. 이전 C17의 실제 IDE 성공을 이번 오프라인 설치 성공으로 대체하지 않았다.

## 대상 PC에 제공하는 후보

`VerifyOfflineSetup.bat`와 `scripts/Test-OfflineSetupOnTarget.ps1`를 추가하고 준비 도구가 함께 동봉하도록 했다. PS1 기본은 설치 없는 사전 검사다. BAT는 설치 모드이며 사용자가 설치 다운로드가 차단된 PC인지 Y/N로 확인하고, 모든 Visual Studio 창을 닫은 다음 설치 대상을 선택한다. CLI 설치·임시 빈 프로필의 버전 실행·공식 VSIX 설치 UI·설치된 제품 ID/버전/DLL 해시 확인 후 결과를 기록한다.

결과는 `%TEMP%\OfflineSetupVerify-<GUID>\RESULT.json`과 같은 폴더의 CLI/VSIX 로그다. `InstalledPendingIdeCheck`는 설치 파일 확인까지 성공한 상태이며 사용자가 IDE에서 **도구 → AI Assistant** 창을 열어 확인해야 한다. `InstallerSucceededPendingInstalledFileCheck`는 설치기 반환값만 성공이고 설치 파일 확인은 남은 상태다. `ReadyDiagnosticOnly`는 사전 검사만 성공한 것이며 실제 설치 성공이 아니다.

이 도구의 `UserConfirmedInstallDownloadNetworkBlocked`는 사용자 확인값이고 `NetworkIsolationEnforcedByTool=false`다. 방화벽을 바꾸거나 사용자 네트워크 차단을 자동 증명하지 않는다. 계정·암호·토큰을 요청하지 않으며 로그인/모델 요청을 수행하지 않는다. 사용자에게 받을 정보는 RESULT 상태·오류 문구·AI Assistant 창 열림 여부다.

고객 검증용 파일: `artifacts/task041/VisualStudio_AI_Assistant-0.11.1-OfflineTest2-Final-win-x64.zip`, **308,372,411 bytes**, SHA256 `6CE8091677E29BC075D5C97BE98C6F86414537B02BC33F3DEF33A8DF108DCC79`. 옆의 `.zip.sha256`과 함께 전달한다. 준비 도구만 담은 공개용 OfflineTools2 후보는 별도이며 CLI payload를 포함하지 않고 아직 게시하지 않았다. 기존 v0.11.1의 9개 공개 자산·태그·FullC4는 보존했다.

## 검증과 보존

| 검사 | 결과 |
|---|---|
| 오프라인 설치 경계 회귀 | Windows PowerShell 5.1 31/31 |
| 최종 C6 원문 네트워크 격리 | 6/6, 실제 토큰·설치 전후 TCP10013·복사/멱등·CLI2개 버전 |
| C6 ZIP | 61/61 원문·크기·SHA, 내부 checksum60/60, CLI46개 |
| OfflineTools2 후보 | 39/39 원문·크기·SHA, 내부 checksum38/38, CLI payload0 |
| 대상 검증기 함수 | 제품 ID·버전·DLL 해시·DTD 차단 5/5 |
| 대상 검증기 진단 모드 | ReadyDiagnosticOnly, 기존 설치/계정/IDE 실행 변경0 |
| 오프라인 확인 N 응답 | 설치 전 거부, CLI/VSIX 설치 보고0 |
| 제품·보호 원본 | C17 입력122·보호47·v0.11.1 원본7개 바이트 불변 |

초기 하네스의 예약 변수명, PowerShell native 경로 탐색, AppContainer profile 경로 실험 및 .NET 호스트 버전 혼용 실패와 기존 Move-Item 실패를 각각 보존했다. 프로필 경로 실험은 폐기하고 전용 작업 폴더를 사용한다. 최초 실패를 최종 성공으로 덮어쓰지 않았다.

독립 감사 TASK-042가 임시 폴더 ACL 복원 누락 R42-01을 재현했다. 원래 DirectorySecurity는 변경 표시가 없으므로 `SetAccessControl(originalAcl)`이 쓰기를 생략했다. 새 descriptor에 Access 구역을 명시 복제하여 기록한 뒤 원래/실제 DACL을 대조하도록 수정했다. 원문 실패는 보존하고 과거 전용 검사 폴더에 남은 자기 임시 SID만 정확히 대조해 제거했다. 파일 내용은 보존했고 전후 ACL은 `artifacts/task041/prior-test-acl-cleanup.json`에 기록했다. 제품 설치기가 고객 폴더 ACL을 변경한 문제와 구분한다.

재현: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/compatibility/Test-OfflineNetworkIsolation.ps1 -BundleZip <C6 ZIP 전체 경로>`. 결과 폴더는 `artifacts/task041/latest-network-run.txt`에 기록된다. 최종 원문은 `run-55f2448d8ca04fae934bd24fe865b73d`, ZIP 검증은 `full-offline-candidate6-validation.json`, 공개 도구 후보는 `public-bundle-evidence.json`이다. 정확한 소스·해시·상태는 [검증 JSON](CodexOrig_TASK-041_Validation.json)에 남긴다. [독립 감사](CodexOrig2_TASK-042_Audit.md)와 [문서 후속](CodexOrig3_TASK-043.md)을 함께 대조한다.

Microsoft 공식 근거: [AppContainer 시작과 capability](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer), [프로필 생성](https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-createappcontainerprofile). 새로운 검사 도구 외 제품 C#·기존 DLL/VSIX/실행기를 재컴파일하지 않았다. 테스트용 C# helper는 Windows PowerShell의 .NET Framework로 컴파일·실행했고 Role 형식으로 클래스/메소드 주석을 정렬했다.


C5 원문과 처음 작성한 보고/JSON 초안도 artifacts/task041에 보존했다. 마지막 보완은 대상 검증기의 폴더·manifest 경로 검사를 존재 조회보다 앞에 배치한 두 곳이며, UNC 입력의 Test-Path 호출0과 감사자의 별도 C6 경계 검사로 확인했다. 대상 검증기 설치 파일 검색 이외 Installer/Common/CLI는 C5와 같지만 최종 C6 ZIP 자체로 네트워크 격리6개를 다시 실행해 모두 통과했다. 이 검증용 파일 전달을 정식 배포 완료로 보고하지 않는다.


검증용 준비 기록: 2026-10-19 13:03:53 KST, 착수12:24:43부터경과 00:39:10.4257023. [C6 후속 감사](CodexOrig2_TASK-042_Followup_C6.md)와 [대상 도구 안내](CodexOrig3_TASK-043_TargetVerifier.md)를 최종 연결했다. 현재 로컬 분기 codex/offline-verification-task041에 준비 상태만 저장하고 GitHub push와 새 릴리스 자산 게시를 수행하지 않는다. 실제 대상 PC의 RESULT 상태·오류·AI Assistant 창 열림 결과를 받은 후 최종 배포를 이어간다.


## TASK-041 후속 / 테스트용 사전 릴리스 게시 / CodexOrig / 2026-10-19 13:15:22 +09:00

사용자가 '너가 배포를 해줘야 다운 받고 테스트할 수 있어'라고 요청하여, 오프라인 PC 결과 전 **테스트 전달 목적의 공개 배포**를 진행했다. 이전 준비 기록의 보류는 이 요청 이전 상태다. 정식 검증 완료 주장과 구분하고 기존 형식의 정식 배포 gate는 변경하지 않았다.

- 공개 릴리스: [v0.11.1-offline-test.2](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.11.1-offline-test.2), ReleaseId 416894520, prerelease=true, 최신 정식 지정=false.
- [완성 ZIP 다운로드](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.11.1-offline-test.2/VisualStudio_AI_Assistant-0.11.1-OfflineTest2-Final-win-x64.zip): 308372411 bytes, SHA256 6CE8091677E29BC075D5C97BE98C6F86414537B02BC33F3DEF33A8DF108DCC79.
- [SHA256 파일](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.11.1-offline-test.2/VisualStudio_AI_Assistant-0.11.1-OfflineTest2-Final-win-x64.zip.sha256): 131 bytes, SHA256 68204285AFC435283578C3A6C25A80E510D0D267B6B376F378BC396F7F2AB198.
- 확장 4종과 Codex 0.163.0/Claude 2.1.302 native CLI 46개를 포함한 감사 완료 C6 원문이다. 준비 도구만 있는 OfflineTools2 후보와 구분한다. ZIP/내부 파일/기존 VSIX 재빌드0.
- 원격 소스 분기 codex/offline-verification-task041, 태그 대상 80adb9116f2ebe8055ab04f17ac78e85caacdbb8. draft에서 두 자산의 서버 digest·크기를 검사한 뒤 공개했고 인증 없는 공개 URL에서 두 파일 전체를 다시 내려받아 크기·SHA256을 대조했다.
- 게시 전 다른 프로젝트 채팅이 idle/notLoaded임을 확인했다. 기존 정식 v0.11.1 태그·9개 자산 ID/크기/SHA256과 최신 정식 지정을 보존했다. 실제 사용자 프로필·CLI·방화벽·네트워크 변경0.
- 별도 고객 오프라인 PC의 VSIX 설치/IDE 창 열림은 미확인이다. CustomerOfflineVsixInstallVerified=false, CustomerIdeToolWindowVerified=false, FormalTargetVerificationPending=true를 유지한다. OS 네트워크 격리 CLI 검사6/6과 회귀31/31은 기존 검증 범위이며 재실행했다고 표현하지 않는다.

사용자는 ZIP 전체 해제 → 모든 Visual Studio 종료 → VerifyOfflineSetup.bat 실행 → 설치 대상 선택 → Visual Studio 도구/AI Assistant 창 확인 순서로 테스트한다. RESULT 상태·오류 문구·창 열림 여부를 받는다. 시작 2026-10-19 13:10:06 +09:00, 종료 2026-10-19 13:15:22 +09:00, 경과 00:05:16.6580671. 근거: CodexOrig_TASK-041_TestDeployment.json 및 갱신 Validation.json. 테스트 배포 작업 종료, 실제 PC 결과 수집 대기.
