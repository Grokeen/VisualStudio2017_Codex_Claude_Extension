# TASK-013 — 사용자 안내·회귀·실제 IDE 및 v0.5.0 배포

- 담당: CodexOrig
- 시작: 2026-10-18 12:58:49 KST.
- 상태: 제품·실행기 검증 완료. 협업 작업 종료 확인 및 최종 GitHub 배포 대기.
- 요청: 최신 Role.md 프로젝트2·4·5·7·8·9·10·12·13 및 image-2~6, 추가 사용자 요청의 대화 화면 변경.

TASK-012 CodexCode가 제품 구현을 먼저 시작해 파일 소유 범위를 나눴다. 제품 Views/Chat/Providers/Services와 신규 workspace/workspace-actions/review 검사는 TASK-012가 담당했다. TASK-013은 README·USER_GUIDE·사용자 화면, 기존 editing/foundation/ide/ux 검사, 버전과 패키지·최종 GitHub 배포를 담당한다. 다른 작업자가 종료하기 전에는 배포하지 않는다.

## 반영한 내용

- README를 사용자 효용·설치·첫 요청·이어 쓰는 대화 중심으로 개편하고 USER_GUIDE에 정확한 버튼·키·한도·권한·복원 방법을 작성했다.
- Claude 우선 탭에 맞춰 테스트의 고정 인덱스를 Provider Tag 선택으로 바꿨다. 각 테스트의 개인 설정과 Markdown 루트를 격리했다.
- 빈 SQL 파일의 삽입 거부, 한 줄 교체의 +0/−0 표기 문제를 발견해 제품 담당자에게 전달했다. 빈 문서 삽입과 실제 추가/삭제 줄 수 회귀를 추가했다.
- 공통 instruction 파일 잠금으로 저장 실패를 재현하고 JSON·Markdown 양쪽 원문 보존을 검증했다. 모드 기본값·잘못된 값·실제 subprocess의 Codex/Claude 옵션도 검사한다.
- 실제 IDE에 변경 파일 링크, 빈 SQL 작성·승인·저장, 이전 메시지·초안 보존, 컨트롤 재생성 후 대화 복원 7개 사례를 추가했다.
- IDE helper는 지정 VSIX에서 추출한 DLL을 직접 참조하므로 공유 Release 재빌드와 무관하게 같은 제품을 검사한다. VSIX·참조 DLL 해시를 launch.json에 남긴다.
- 추가 [사용자 참고 이미지](assets/task013-chat-reference.png)의 오른쪽 사용자 말풍선·왼쪽 AI 본문·시간/진행 구분을 TASK-012 제품 담당자에게 인계했다. 실제 제공하지 않는 도구 이벤트는 만들어 표시하지 않는다.
- 제품 DLL의 실제 WPF를 합성 데이터로 렌더하는 도구를 추가했다. 개인 계정 대화·소스·설정은 사용하지 않는다.
- CodexOrig가 수정한 C# 메소드에는 기존 name/desc/author/create date/update date 줄바꿈과 콜론 열을 지키고 기존 작성자 기록을 보존했다.

## 검증 기록

중간 후보 SHA06035B8A…로 편집/UX92, Foundation33, 호환성44 전부 통과했다. 초기 후보의 빈 SQL 삽입 실패는 제품 수정 후 통과했다. 기존 handoff448C85DE… 실제 IDE 검사는 메뉴·EnvDTE·UX16·소스 링크·승인/Undo까지 통과했으나, 반복 생성 시 입력란을 재사용하던 기존 테스트에서 중단됐다. 전송 후 비워진 입력란에 요청을 다시 지정하도록 테스트를 수정하고 최종 대화 화면 패키지로 전체를 재실행하여58개 모두 통과했다. 이 중간 실행을 최종 통과 결과로 집계하지 않는다.

최종 패키지 해시·검사 결과·화면은 아래에 기록했으며 GitHub 배포 후 원격 파일 확인을 덧붙인다.

## 재현

저장소 루트에서 Windows PowerShell 5.1로 실행한다.

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\editing\Test-SourceEditing.ps1 -AssemblyPath <고정 DLL>
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -STA -File .\tests\foundation\Test-FoundationPackage.ps1 -AssemblyPath <고정 DLL> -VsixPath <같은 VSIX>
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\ide\Test-IdeIntegration.ps1 -VsixPath <고정 VSIX> -LiveCodexPath <로그인된 Codex.exe>
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -STA -File .\tests\ux\Render-UserExperience.ps1 -AssemblyPath <고정 DLL> -OutputDirectory .\docs\assets
```

호환성44개는 tests/compatibility/Test-VsixCompatibility.Tests.ps1로 재현한다. 신규 저장·설정 트랜잭션·프로젝트 작업·사용 흐름은 [TASK-012](CodexCode_TASK-012.md)의 명령과 결과를 참고한다.

## 지원 범위

현재 패키지는 VS2017 15.9 이상용이며 VS2019/2022 확장 지원은 후속 범위다. 실제 Codex 호출은 합성 코드·SQL 스키마만 사용한다. 실제 Claude 계정·운영 DB·외부 SVN/TFVC 서버·운영 배포는 실제 연결로 검증하지 않았다. DB는 SQL Server 메타데이터 읽기와 쿼리 작성 요청을 지원하며 SQL 자체를 DB에서 실행하지 않는다. 소스 적용은 기존 한 파일을 대상으로 하며 새로운 다중 파일 자동 수정과 구분한다.

## 최종 제품 검증

고정 대화 화면 DLL과 동일한 VSIX를 사용했으며 빌드 담당자의 Debug/Release는 경고·오류0이다.

| 검사 | 결과 | 근거 |
|---|---|---|
| 편집·Chat·설정 UX | 92/92 PASS | _Validation.json의 전체 사례 |
| WPF·VSIX 기본 등록 | 33/33 PASS | _Validation.json의 전체 사례 |
| 호환성 도구 회귀 | 44/44 PASS | _Validation.json의 전체 사례 |
| 실제 VS2017·실제 Codex·빈 SQL 작성 | 58/58 PASS | [_IDE.json](CodexOrig_TASK-013_IDE.json) |
| 제품 소스 동결 해시 | 30개 모두 일치 | [_SourceHashes.json](CodexOrig_TASK-013_SourceHashes.json) |
| 최종 WPF 문서 화면 | Chat·검토·설정·안내·CLI안내 직접 열람 | [_Render.json](CodexOrig_TASK-013_Render.json) |

최종 제품 DLL: `AE807FFD7CABA335DCC9A61BD2BB931BCF77CA692B079E8E29D0930A78D55AE6`.
최종 VSIX: [v0.5.0 패키지](../releases/v0.5.0/VS2017.AIAssistant-0.5.0.vsix), 179,232바이트.
SHA-256: `DC2DB3178CD6490CCE529EA95E31133CD7B8997F9B146B656C54EC0FA474C768`.

실제 IDE 실행은 `artifacts/ide/run-425985c8836f4df1b486f37793869548`이며 [_IDELaunch.json](CodexOrig_TASK-013_IDELaunch.json)에 설치 VSIX와 helper 참조 DLL 해시를 남겼다. 실제 Codex와 SQL 호출 모두 true, 실제 Claude 호출 false이며 사용자 프로젝트를 전송하지 않았다. 검증 후 제품 소스와 패키지를 변경하지 않는다. 이후 추가된 최상위 테스트 실행기도 별도로 검증했다.

[최종 검증 JSON](CodexOrig_TASK-013_Validation.json) · [최종 대화 화면](../docs/assets/chat-0.5-preview.png) · [변경 비교 화면](../docs/assets/review-0.5.png)

## 최상위 테스트 실행기

작업 중 Role 기본6에 추가된 요구에 따라 [VisualStudio_AIChat_ExtentionTest.exe](../VisualStudio_AIChat_ExtentionTest.exe)를 루트에 준비했다. TASK-012가 신규 tests/launcher와 scripts/Build-TestLauncher.ps1을 구현했고 TASK-013은 문서·독립 실행 확인·배포를 담당한다.

최종 실행기 SHA-256: `3C22B6545035A508452E8886EB37B94D1A0BA90D763A48D8943EE52E7B598EFB`. 기본 --all은 8/8 묶음 통과(`artifacts/tl/e61dd70ec3/summary.txt`). --help/--list, 실제 compatibility44개 종료0 및 잘못된 ID 종료2도 별도로 확인했다. [실행기 독립 근거](CodexOrig_TASK-013_Launcher.json)에 기록했다. 처음 전체 경로의 MAX_PATH 실패는 짧은 artifacts/tl 출력 경로로 수정한 뒤 통과했으며, 실패를 성공으로 덮지 않는다.

인수 없이 실행하면 검정 GUI에서 항목을 선택한다. 기본 전체 검사는 실제 IDE·AI 계정을 사용하지 않고 ide/live-codex는 직접 선택한다. 배포 VSIX를 우선 사용하고 제품을 자동 빌드하지 않는다. 로그·종료코드·준비 조건과 CLI 명령은 [개발 문서](../docs/DEVELOPMENT.md)에 작성했다. [문서 참조 검증](CodexOrig_TASK-013_Documentation.json)도 모두 통과했다.


로컬 검증·문서·패키지 준비 종료: 2026-10-18 13:40:24 KST. 시작부터 41분 35초. 배포 결과는 협업 최종 종료 후 기록한다.
