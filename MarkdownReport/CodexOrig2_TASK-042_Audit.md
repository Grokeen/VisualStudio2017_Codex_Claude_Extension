# TASK-042 / CodexOrig2 / 오프라인 격리·설치 이동 후속 감사

감사자 **CodexOrig2**, 2026-10-19 KST. 사용자 요청은 오프라인에서도 동일하게 설치되는지 확인 후 배포하는 것이다.

**판정: R42-01/P2 1건을 발견하여 수정 확인했고 이번 감사 범위의 미해소 확정 결함은 0건이다. C5 설치 도구·로컬 OS 네트워크 격리 검사는 확인했으나, 실제 대상 PC의 VSIX 설치·IDE 확인 결과는 미수집이다. 정식 추가 배포 조건은 아직 충족되지 않아 보류를 유지한다.**

## R42-01 / P2 / 임시 AppContainer SID의 ACL이 복원되지 않음 — 수정 확인

고정 `OfflineNetworkIsolation.cs` SHA256 `6B2754E049798E0B7C61C6E6DD582DEE418F2194D0C3B0245BB5D2E48EA59514`를 개인 marked `task041/run-GUID` 폴더에서 직접 실행했다. 성공·exit7·시간 초과의 세 종료 경로 모두 root와 기존 seed 하위 폴더 ACL이 원래대로 돌아오지 않았다. 삭제된 AppContainer SID의 명시적 **Modify Allow / ObjectInherit·ContainerInherit** ACE와 하위 상속 ACE가 남았다.

세 경로 모두 token `[1,0]`, 외부 TCP 10013 거부, 합성 부모 환경 표식 미상속, 전용 설정 경로, 자식 job 종료, 임시 프로필 삭제 HRESULT 0 및 저장소 제거는 확인했다. ACL 복원 조건 때문에 수명 검사 결과는 **0/3**이었다. 영향은 전용 검사 폴더의 하네스 정리이며 제품 설치기가 기존 사용자 설치의 권한을 바꾸는 결함으로 확대하지 않는다.

원인은 `finally`에서 변경되지 않은 `originalAcl` 객체를 `SetAccessControl`에 그대로 넘기는 구현이다. [Microsoft 공식 문서](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.setaccesscontrol?view=netframework-4.8.1)는 생성 후 수정되지 않은 DirectorySecurity는 저장되지 않으며, 새 객체에 원본 SDDL/바이너리를 적용해 복원하도록 설명한다.

TASK-041이 새 DirectorySecurity의 Access 섹션에 원본 바이너리를 적용한 뒤 복원하고, 전후 Access SDDL 동일성과 `LastAclRestored`를 확인하도록 보완했다. 수정 코드에서 **동일 세 경로 모두 3/3 통과**했다. 감사자는 root와 기존 child의 전체 SDDL도 정확히 일치하는 것을 직접 확인했다. 프로필 삭제·저장소 제거·자식 프로세스 종료·토큰·TCP 거부·설정 경로·가짜 부모 비밀 미상속도 유지됐다. 따라서 R42-01을 검사한 수정 하네스에서 종결한다.

처음 남은 개인 전용 SID 4개는 최초 증거를 보존한 뒤 marker·절대 경로를 확인하여 해당 명시 ACE만 제거했다. 다른 담당자 폴더 권한은 변경하지 않았다. 성공/실패 시 만든 총 7개 개인 AppContainer 저장소의 최종 잔존은 0개다. 작업 폴더·실패 원문은 보존했다.

최초 증거: [수명 검사 JSON](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task042/isolation-cleanup-result.json), [잔존 ACE 증거](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task042/acl-remnant-proof.json), [독립 fixture](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task042/Verify-IsolationCleanup42.ps1).

수정·정리 근거: [수정 후 세 수명 경로](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task042/lifecycle-fixed/isolation-cleanup-result.json), [자기 ACE 정리 기록](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task042/owned-acl-cleanup.json).

## 설치 이동·대상 PC 도구

- 개인 복제본의 변경 설치기로 기존 오프라인 회귀 **31/31**을 직접 통과했다. `Directory.Move` 전에 루트·staging·목적지의 경로 검사와 대상 존재 검사가 유지된다. [Directory.Move 공식 계약](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.move?view=netframework-4.8.1)에 따르면 기존 목적지는 덮어쓰지 않고 오류가 발생한다.
- TASK-041의 최초 격리 6/6은 FullC4에 working installer를 덮어쓴 검사(`WorkingInstallerUsed=true`)다. 최종 C5 원본 검사 `run-75bfdff830614fb98697cc23072a09ea`는 **`WorkingInstallerUsed=false`**, 6/6, ACL 복원 true·프로필 cleanup 0이며 control의 ZIP SHA가 아래 C5와 같다. 두 기록 모두 통합 담당자 자료 확인이며, 원래 공개 C4가 수정 전부터 통과한 것으로 기록하지 않는다.
- 감사자의 독립 격리 fixture는 실제 CLI 대신 PowerShell 작업과 잠든 자식 프로세스를 사용했다. 호스트 TCP 성공과 동일 외부 주소의 격리 작업 TCP 10013을 확인했다. 물리 케이블 차단·전체 패킷 캡처·실제 고객 PC 성공과는 다르다.
- VSIXInstaller의 AppContainer 접근 거부 `0x80070005`는 실제 오프라인 VSIX 설치 성공 증거가 아니다. AppContainer의 capability는 할당된 네트워크 자원을 제한하는 [Windows 보안 경계](https://learn.microsoft.com/en-us/windows/win32/secauthz/appcontainer-isolation)이며, 프로필 삭제의 성공/실패는 [공식 DeleteAppContainerProfile 계약](https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-deleteappcontainerprofile)과 구분해 확인했다.

대상 검증 도구는 개인 합성 bundle·대역 설치기에서 **5/5** 통과했다. 기본 진단 모드는 설치하지 않고, 기존 결과 폴더는 보존하며, 오프라인 질문에 N이면 실제 복사·VSIX 실행 전에 취소됐다. 설치 파일 확인 함수는 제품 ID·버전·DLL SHA가 모두 맞는 경우만 수용하고 DTD·다른 ID/버전/hash·junction은 제외했다. 별도 합성 version 프로그램에서 지정 인증 환경 변수 6개가 제거되고 Codex/Claude 설정 경로가 새 폴더인지 확인했다. 실제 CLI·실계정은 감사자가 실행하지 않았다.

`UserConfirmedInstallDownloadNetworkBlocked`는 사용자 확인이고 **`NetworkIsolationEnforcedByTool=false`**다. 도구는 물리 연결이나 방화벽을 바꾸지 않는다. 설치 파일이 발견돼도 `InstalledPendingIdeCheck`로 남고 `IdeToolWindowConfirmed=false`다. 실제 IDE 도구창·로그인·AI 요청 성공을 자동으로 단정하지 않는다. BAT는 설치 진입점이고 PS 기본은 진단 전용임을 구분했다.

## 고정 C5와 Tools2 후보

| 후보 | SHA256 | 직접 대조 |
|---|---|---|
| FullOffline C5, 308,372,418 bytes | `33915BF44387155EC95A3823F7107AF50712323D728D2AF2E0B6B3F8F2FFD177` | ZIP 61/61, 내부 checksum 60/60, 외부 checksum 일치, CLI 46, C17 VSIX 4개 동일 |
| OfflineTools2, 16,946,091 bytes | `34DF74DBCFA8D3C049BE2324BD11F7E865285EEBB1FCD516A8C687EB337885B6` | ZIP 39/39, 내부 checksum 38/38, 외부 checksum 일치, CLI payload 0 |

동일하게 검사한 설치기·Common·대상 PS·BAT의 SHA가 두 ZIP에 일치한다. 안내서 최종 SHA는 `4C387A0A5B3959184D1AF8A3868E64A3FA1986BE728B43F91BF69CAE71227255`다. 네이티브 하네스는 ZIP에 포함되지 않으며 감사자가 고정한 수정 CS와 현재 CS의 SHA를 따로 연결했다. 상세 [C5 연결 근거](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task042/candidate5-linkage.json).

보호 원본 **72/72**, 제품 C17 입력 **122/122**의 SHA가 유지됐다. 기존 OfflineTools1·FullC4·공개 VSIX·이전 감사·Role·온라인 원본은 변경하지 않았다. C5/Tools2는 고객 검증용 후보이며, 실제 테스트 PC의 `RESULT.json` 및 IDE 확인이 오기 전 정식 추가 게시 성공으로 기록하지 않는다.

초기 개인 fixture에서 자식 PowerShell에 ExecutionPolicy Bypass를 빠뜨린 1회 실행 실패는 하네스 오류로 보존하고 제품 결함과 구분했다. 보정 후 ACL 미복원 0/3을 재현했고, 수정 소스에서 동일 3경로를 다시 확인했다.

감사자는 제품·공유 GUI·실계정·기존 사용자 프로필·CLI·배포를 변경하지 않고 개인 fixture와 새 보고만 작성했다. 이 보고는 고객 실제 설치나 정식 배포 완료를 승인하는 결과가 아니다. 코드 통합·고객 결과 수집·배포 판단은 TASK-041이 담당한다. 상세 [TASK-042 Evidence](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-042_Evidence.json).

## 감사 종료

시작 2026-10-19 12:43:24 +09:00, 종료 2026-10-19 12:56:21 +09:00, 경과 00:12:57. TASK042 독립 감사·수정 확인·보고를 완료하고 담당 소유를 반납한다. 정식 추가 배포 보류 및 실제 대상 PC 결과 수집은 TASK041이 이어서 담당한다.

