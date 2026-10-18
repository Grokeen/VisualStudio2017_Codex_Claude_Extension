# TASK-014 — 대화 목록 크기 조절·숨김 및 대화별 상태

- 담당 CodexOrig. 시작2026-10-18 13:46:29 KST. 상태: 구현/검증 완료, TASK-015/016 종료 후 공동 배포 및 원격 해시 확인 완료.
- 사용자 첨부의 대화 목록 경계와 파란 상태점 요구를 반영했다. 기존 v0.5.0은 보존하고 v0.6.0을 별도로 준비했다.

목록 경계는 메시지·변경 검토·입력란에 걸쳐 세로로 이어진다. 드래그와 키보드 좌우 방향키로 폭을 바꾸고96픽셀 미만으로 접으면 숨긴다. ☰는 마지막 유효 폭을 복원한다. 폭/표시는 설정 JSON에 병합해 다른 설정을 보존하고 좁은 창에서는 입력란을 가리지 않는 목록을 제공한다.

대화마다 대기·작업 중·완료·실패·중지·이전 실행 중단을 실제 요청 이벤트로 표시한다. 보지 않은 응답은 파란 점이고 선택/본문 클릭·스크롤·키보드 확인으로 해당 대화만 읽음 처리한다. 상태와 읽지 않음은 선택 front matter로 저장해 구버전 Markdown을 계속 읽는다. 재시작 때 남은 Running은 Interrupted로 복원한다. 코드 적용·승인/저장과 응답 완료는 구분한다.

## 구현 경로

- src/VS2017.AIAssistant/Views/AssistantControl.Conversations.cs·xaml·Experience.cs·Chat.cs·Dialogs.cs·xaml.cs
- Chat/MarkdownWorkspaceStore.cs·ChatWorkspace.cs, csproj·AssemblyInfo·VSIX manifest
- tests/review/Test-WorkspaceExperience.ps1의 실제 WPF drag/상태23개 추가, tests/ide/IdeTests.cs의 실제 IDE7개 추가
- tests/launcher/TestLauncher.cs·Test-TestLauncher.ps1:0.6 기본 대상, PowerShell7에서 Windows PowerShell5.1을 실행할 때 PSModulePath 재구성
- README·USER_GUIDE·DEVELOPMENT·UX_TESTS·0.6 사용자 화면5개, releases/v0.6.0

## 최종 검증

Debug/Release 경고·오류0, 실제 제품 DLL SHA54F59CB9B06546A78E0D43C93F8AF08F21F8696B45C332A7EA76C89A9102320A.

| 검사 | 결과 |
|---|---|
| 실제 WPF 입력·대화·경계·상태 | 79/79 PASS |
| 실제 VS2017/Codex·SQL·목록 조절 | 65/65 PASS |
| 실행기 기본8묶음 | 8/8 PASS |
| 실행기 GUI/잘못된 인수/실패 전달/하위 프로세스 | 9/9 PASS |

기본8묶음은 호환44·빌드진입16·기록52·설정37·업무48·편집92·Foundation33·UI79를 재사용한다. 실제 IDE에는 원래58개에 경계 드래그·접기·복원·실제 완료 상태·읽음 저장·재생성7개를 추가했다. 직접 WPF 검사는 자동 스크롤이 읽음이 되지 않는지, 실제 숨긴 창에서 도착한 응답과 오래된 파일/중단 복구를 포함한다.

초기 실제 IDE의58개는 통과했으나 추가 경계 검사는 소스 탭이 활성 상태여서 멈췄다. 테스트 소유 AI 프레임을 다시 표시한 뒤 전체65개 통과를 확인했다. 실행기에서 PowerShell7 모듈 경로가 Windows PowerShell5.1에 전달되는 문제는 자식의 기본 경로를 다시 구성하여 해결했고8묶음 재실행에 통과했다. 제품 동작을 테스트에 맞춰 완화하지 않았다.

[검증 JSON](CodexOrig_TASK-014_Validation.json) · [실제 IDE](CodexOrig_TASK-014_IDE.json) · [UI 사례](CodexOrig_TASK-014_Experience.json) · [배포 파일](../releases/v0.6.0/README.md)

작업 중 사용자가 Role.md에 문서 첨부·새 CLI 선택·영역 구분·이름/협업을 추가하고 진행을 요청했다. 이 후속 요구는 TASK-015 CodexCode 서비스/Provider, TASK-016 CodexOrig UI/모델/검증/문서와0.7로 분담한다. 현재0.6 DLL/VSIX는 덮어쓰지 않으며 다른 작업자 종료 전에는 push하지 않는다. 현재 VSIX는 VS2017용이다.

배포 완료 2026-10-18 14:38:46 KST: 로컬고정 a9c66b4의 v0.6.0 VSIX를 v0.7.0과 함께 da33703으로 origin/main에 반영했다. 원격184770바이트·5528C0A1… 일치 확인. 상세 근거는 [공동 배포 JSON](CodexOrig_TASK-016_Deployment.json)이다. 0.6 고정 패키지는 변경하지 않았다.
