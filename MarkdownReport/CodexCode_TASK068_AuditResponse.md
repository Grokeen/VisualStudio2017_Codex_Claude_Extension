# TASK068 CodexCode 독립 감사 대응

2026-10-20 13:49 KST. 독립 감사 AI가 신규 동시 실행·기록·선택지·필수 규칙을 읽기 전용으로 검사했다. 기록 저장 P1 결함은 별도 보조055의 immutable hour/manifest/head 저장으로 인계하고 제품 통합 검사 19+13 및 기존 workspace 59를 확인했다. 비동기 스크롤은 현재 055가 통합 중이다.

| 감사 지적 | 대응 | 확인 |
| --- | --- | --- |
| 전체 액세스 두 방의 편집기 보호 scope 중첩 | 단일 scope+참조계수, 마지막 방 종료 시 복원, 외부변경시 관련 실행 모두 취소 | 로컬 실제 CLI 두 PID+가짜 IDE scope 71/71. 실제 IDE COM 복원은 Root 최종 검사 필요 |
| 대기 요청의 프로젝트·권한·참여자·원본 상태 재검사 누락 | 각 실행 직전 및 협업 후 CLI 직전에 검사, 조건 변경 시 후속 큐 취소 | 실제 CLI fixture 64/64, stale 원본 거부 |
| 방 전환시 다른 방 초안 혼입 | 새 방 초안 표시 후 읽음/저장 | 실제 CLI fixture 전환 검사 통과 |
| 진행·사용량·완료 알림이 전역 상태에 섞임 | 방별 ProgressNotice·토큰 스냅샷·소유 제공자 완료 알림 | 방별 토큰/역할/진행 격리 fixture 통과. OS 알림 표시는 최종 IDE 검사 필요 |
| 번호 목록을 선택지로 오인, 긴 한국어 버튼 잘림 | 질문 직후 연속 번호만 선택지 처리, 코드 fence/중복번호 거부; 줄바꿈·표면·경계 추가 | 실제 getter 8/8, offscreen WPF 8조건 독립 UI 감사 통과 |
| 기존 공식 숨김 규칙 파일 1~2 유지 | 정확한 구버전 공식 UTF-8 바이트만 원자 교체, 사용자 수정본 보존 | 필수 규칙 36/36 |

2017 Release 빌드, task055 70/70, workspace 59/59, 선택지 8/8, 필수규칙 36/36을 확인했다. 최종 4SKU·실제 IDE·#2 비동기 스크롤·배포는 CodexOrig 통합 게이트에서 다시 검증한다. 원격 AI 로그인/실답변은 이 검사에 포함하지 않았다.

## C4 독립 감사 후속 (2026-10-20)

CodexOrig2의 C4 감사에서 A68-01(방별 역할 문맥)과 A68-02(영어 `selected` 완료 설명의 선택지 오인)는 각각 고정 DLL 검사 82/82와 8/8로 해결 확인됐다. A68-02에 대해서는 `choose`·`select`·`which option`을 단어 경계에서만 인식하도록 바꾸고 감사 원문과 정상 영어 선택 질문을 포함한 실제 getter 회귀 10/10을 통과했다 (`artifacts/task067/choice-check-31f5f13070c94aacbe13bff9c5e13a72`).

C4 감사의 A68-03은 원래 읽기 전용 문서를 실행 직전에 거절할 때 초안이 이미 비워지는 경계였다. `CaptureCurrentRequest`에서 사용자 메시지 추가와 입력 삭제 이전에 읽기 전용 여부를 판정한다. 동시에 진행 중인 다른 방이 설정한 임시 보호는 공유 scope의 `IExternalEditingScopeState.IsTemporarilyReadOnly`가 정확한 문서 경로에 대해 확인한 경우에만 허용한다. 별도 방의 보호 범위 수명, 원래 읽기 전용·알 수 없는 scope·다른 경로 거절, 입력 보존을 포함한 실제 RuntimeEditProvider/로컬 Claude EXE fixture **115/115**가 통과했다 (`artifacts/task067/official-concurrent-readonly-202610201404/result.json`). 기존 FIFO 회귀는 70/70이다. 이 수정은 C4 동결 이후 제품 소스에 반영됐으며 Root의 C5 최종 빌드·실제 IDE 검사 대상이다.

## C5 실제 IDE 실패와 C6 수정 (2026-10-20)

C5 실제 IDE 검사에서 협업 수정안을 받은 후 담당 AI를 다시 선택하면 `ActivateAssignedAgent`가 화면의 `pendingProposal`을 지우고 방별 `roomProposals`만 남겼다. 후속 요청은 대기했지만 미승인안을 건너뛸 버튼이 비활성이라 진행되지 않았다. 첫 IDE 실행은 대기 상태 시간 초과, 두 번째는 `ROOM-QUEUE-03` 실패, 세 번째 진단 실행은 `ProposalBefore=false`, `DiscardEnabled=false`를 기록했다. 실패 원본은 각각 `artifacts/ide/run-558ae705c6f94e3bab93052563dd8e3b`, `run-948b745918e841b690ff6c3d541da6a7`, `run-ee398a3430034af1a4b9d8fc08f039e7`에 보존했다.

`ActivateAssignedAgent`에서 현재 방을 다시 표시한 뒤 `RestoreRoomPresentation()`을 호출하도록 수정했다. C6 고정 Legacy DLL의 실제 RuntimeEditProvider/로컬 Claude fixture는 기존 115개와 담당 AI 재선택 5개를 합쳐 **120/120** 통과했다 (`artifacts/task068concurrent/candidate6-agent-reselect-1/result.json`). 새 검사는 같은 AI 재선택 후 미승인안·원본/비교 화면·건너뛰기 복원, 다음 요청의 대기, 명시적 건너뛰기 후 실제 CLI 시작과 미승인 편집 미적용을 확인한다. C6 네 SKU 빌드는 모두 오류·경고 0이다. 실제 IDE 전체 검사와 C6 재패키징·공개 검증은 Root의 후속 게이트다.

C6 실제 IDE 재검사에서 VS2017.Legacy, VS2017, VS2019, VS2022가 **각 157/157, 총 628/628** 통과했다. 첫 VS2017.Legacy 실행의 `ROOM-QUEUE-03` 관찰은 `ProposalBefore=true`, `DiscardEnabled=true`이고, 명시적 건너뛰기 후 실제 전체 액세스 명령 및 파일 변경 검사도 통과했다. 결과 원본은 `artifacts/ide/run-195ab4163d27492688c003ad2d7cb4f0`, `run-b930f22dd3e241f0b4a6f00d49f875db`, `run-86b61db5063f4febb18fffa1ce704a36`, `run-fe680fd0907447cf9db1537326b425c7`에 보존했다. 고객 PC의 VS2017 15.7.1·제한망 실측과 실계정 AI 답변은 이 검사에 포함되지 않았다.
