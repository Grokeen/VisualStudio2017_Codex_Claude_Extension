# CodexOrig2 2차 감사 C2 후속 보고

감사 시각: 2026-10-20 03:08 KST  
대상: `artifacts/task054/candidate2` 버전 0.12.1. [C1 감사 원문](CodexOrig2_TASK-058_Audit_C1.md)은 보존한다. C2 변경 범위는 알림 R58-01, Role #15 사용자 메시지, #16 Visual Studio 테마 설치·적용 및 관련 회귀다.  
판정: **R58-01 해결. #15 무창 통과. #16 실제 VS2017 적용 실패. #8 작업 요약은 부분 충족.** C2는 최종 제품 합격 후보가 아니다.

## 고정 확인

C2의 `source-hashes.json` 132개를 개인 복사본에서 재해시해 불일치 0개였다. 네 SKU Release DLL/VSIX는 빌드 기록상 오류0·경고0이고, [고정 SHA 기록](../artifacts/audit-codexorig2-task058/candidate2-freeze.json)의 값과 테스트 입력이 일치한다. 실제 직접 호출 기준 VS2017 DLL SHA-256은 `618CE370C6551E8E24ABB1DB8D60A885F84B9096DBC0A81C8757890976489BB4`다. C1 대비 새 파일 5개, 변경 8개, 이전 0.12.0 미리보기 PDF 제거 1개다. 새 파일에는 v0.12.1 PDF, 테마 VSIX 3개, `AssistantControl.IdeThemes.cs`가 있다. PDF SHA-256은 `94FDD30CA5F95CFDFB4F7E48A1F36E4D84D0244E893A2145EEE9CC65EE53BE31`이다.

## 주요 판정

**R58-01 / 해결 / #8 알림 개인정보.** 네 SKU C2 DLL의 `SafeRequestTitle`과 `Body`를 각각 직접 호출했다. 합성 인명·급여·경로·비밀번호·일반 요청 네 사례 모두 제목 `AI 요청`, 진행 단계가 없을 때 본문 `Visual Studio에서 답변을 확인하세요.`였다. 16개 결과 모두 요청 원문이 없었다. 고정 소스의 [실제 알림 호출](../artifacts/task054/candidate2/source/src/VS2017.AIAssistant/Views/AssistantControl.ProgressPresentation.cs)은 `Body(null, progressNotice)`를 전달하고, `completionRequestProvider`만 실행 제공자에서 고정한다. `Body`는 진행 단계의 `Kind/State`별 완료 수만 표시한다. 따라서 C1의 OS 알림 원문 전달 경로는 제거됐다. 실제 OS 알림 픽셀·노출 횟수는 이번 무창 검사 범위 밖이다. [4 SKU 독립 결과](../artifacts/audit-codexorig2-task058/candidate2-notification-2017.json).

**R58-02 / 보통(P2) / #8 짧은 작업 요약은 여전히 부분 충족.** 같은 실제 DLL에서 `보고서 검토`, `테스트 작성`처럼 다른 요청도 완료 단계가 없으면 동일한 일반 안내 문구를 반환했다(`DistinctBodies=1`). 이는 특정 작업을 짧게 보여 달라는 Role #8에 대한 답이 아니다. 단계가 있더라도 현재 본문은 `파일 N · 명령 N · 조회·도구 N`만 표시해 작업 목적이나 결과를 구분하지 않는다. 원문을 다시 노출하지 말고, 앱이 알고 있는 작업 모드/결과에서 안전한 유형(`질문 답변`, `수정안 준비`, `파일 생성`)을 구성하는 방식으로 보완할 수 있다. 실제 요청 원문에서 제목을 추출하라는 뜻이 아니다.

**#15 / 무창 PASS, 실제 IDE PARTIAL.** 실제 C2 DLL에서 컴파일된 사용자 `DataTemplate` 트리거를 검사했다. 사용자 메시지는 오른쪽 정렬, 테두리1, 모서리12, 패딩 왼쪽12, 최대 폭680이다. [실제 C2 DLL 무창 렌더](../docs/assets/ui-guide-v0.12.1/candidate2/user-bubble.png)에는 회색 둥근 말풍선 안에 Markdown 목록·링크가 유지되고 텍스트 대비가 12.37:1로 기록됐다. 이 렌더는 실제 IDE 주변 배경·물리 스크롤·키보드 조작을 대신하지 않는다. [Designer 5테마 25속성 증거](../artifacts/task056/UserBubbleEvidence.json)의 XAML SHA가 C2 입력과 일치한다.

**R58-03 / 높음(P1) / #16 설치 패키징은 통과했지만 실제 IDE 자동 적용은 실패.** C2 네 VSIX ZIP마다 현재 SKU용 독립 테마 VSIX가 정확히 1개 있고 Release 옆 `Themes/` 파일과 SHA-256이 일치한다. 내장 테마 VSIX의 Identity와 설치 대상도 각각 VS2017 `[15.0,16.0)`, VS2019 `[16.0,17.0)`, VS2022 `[17.0,18.0)`에 맞는다. 네 SKU 실제 DLL을 창 없이 생성한 설정 검사에서 두 설치 버튼·공통 스타일·접근성 이름은 모두 통과했다. 그러나 [TASK064 실제 VS2017 전용 프로필 실행](../artifacts/task064/candidate2-2017-v4/result.json)은 22개 중 11개 통과·11개 실패했다. Kakao와 Dracula 모두 제품 성공 상태가 없고 pending이 소비되지 않았으며, 재export한 테마 GUID가 초기값으로 유지됐다. 검사자는 기본 사용자 프로필을 변경하지 않았고, 다른 설정 3개 비교 실패는 테스트 하네스의 export 경로 정규화 영향으로 분리했다. **자동 적용 실패 자체는 실제 IDE 결과로 확인**되지만 DTE 명령의 비동기성 등 정확한 원인은 이 증거만으로 확정하지 않는다. C3 수정 후 동일한 IDE 검증을 다시 해야 한다.

## 관련 회귀와 제한

| 항목 | C2 결과 | 경계 |
|---|---|---|
| FIFO/연속 입력 | C2 VS2017 DLL 무창 WPF·합성 CLI 56/56 PASS. 변경 없는 전환40회 총859ms, 디스크 쓰기0. | 실제 IDE 입력·화면 미검증. |
| 긴 대화 탭 전환 | 제공자마다 100개×8,000자, warm-up10회 후 50회: p95 39.0981ms, p99/max 43.8390ms. | 무창 WPF 측정. 실제 IDE click→화면 반응 아님. |
| #13 파일 만들기 | C1에서 직접 API 5형식/경계가 통과했고 C2의 Writer 및 Artifacts 소스 해시가 불변이다. | C2 저장창 물리 동작·Word 시각 렌더 미검증. |
| #14 Claude | C1 비로그인 합성 경계 소스가 C2에서 불변이다. | C2 DLL의 CLI 계정 기반 실답변은 사용자 지시로 범위 밖. |
| #1–5, #11–12 | 변경 없는 항목의 C1 무창/소유자 자료를 유지한다. | 실제 IDE 시각·포인터·DPI 최종 검증은 별도. |

감사자는 GUI 예약을 점유하거나 설치 관리자·실제 계정을 실행하지 않았다. 테마 적용 실패 판정은 TASK064의 실제 IDE 원자료를 감사자가 독립 대조한 것이며 직접 IDE를 조작한 결과로 쓰지 않는다. C2 제품 소스·공유 빌드·Release·이전 보고서/JSON을 수정하지 않았다. C3가 고정되면 새 4 SKU SHA에서 R58-03 및 #8 잔여 요약을 재검사해야 한다.

## 증거

- [C2 감사 JSON](CodexOrig2_TASK-058_Audit_C2_Evidence.json): 대상 해시·판정·원자료 경로.
- [알림 직접 호출 실행기](../artifacts/audit-codexorig2-task058/Test-Candidate2Notification.ps1)와 [VS2017](../artifacts/audit-codexorig2-task058/candidate2-notification-2017.json)·[Legacy](../artifacts/audit-codexorig2-task058/candidate2-notification-2017.Legacy.json)·[2019](../artifacts/audit-codexorig2-task058/candidate2-notification-2019.json)·[2022](../artifacts/audit-codexorig2-task058/candidate2-notification-2022.json) 결과.
- [C2 말풍선 실제 DLL 무창 결과](../artifacts/audit-codexorig2-task058/candidate2-bubble-result.json), [설정 UI 네 SKU 결과](../artifacts/audit-codexorig2-task058/candidate2-theme-ui-2017.json), [테마 패키지 바이트 검증](../artifacts/audit-codexorig2-task058/candidate2-themes-integrity.json).
- [C2 FIFO 56개 검사](../artifacts/audit-codexorig2-task058/candidate2-queue-result.json), [긴 대화 50회 CSV](../artifacts/audit-codexorig2-task058/candidate2-long-tab-latency.csv), [요약](../artifacts/audit-codexorig2-task058/candidate2-long-tab-summary.txt).
