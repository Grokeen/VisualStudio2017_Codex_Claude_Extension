# CodexOrig2 2차 감사 C6 후속 보고

감사 시각: 2026-10-20 KST  
대상: `artifacts/task054/candidate6` 버전 0.12.1. [C1](CodexOrig2_TASK-058_Audit_C1.md)·[C2](CodexOrig2_TASK-058_Audit_C2.md) 감사 원문은 보존한다. C6는 알림 R58-02와 IDE 테마 적용 R58-03을 수정하려는 고정 후보이며 이후 C7의 큐 상태 수정·최종 문서 재빌드가 예정돼 있다.  
판정: **알림 R58-02 무창 해결, #16 실제 VS2017 적용 실패, 큐 상태 표시 회귀 2건 확인.** C6는 최종 릴리스 합격 후보가 아니다.

## 고정 확인

`source-hashes.json` 입력132개를 개인 복사본에서 다시 SHA-256으로 대조했고 불일치0개였다. 네 SKU Release 빌드는 기록상 오류0·경고0이며 [DLL/VSIX 해시 기록](../artifacts/audit-codexorig2-task058/candidate6-freeze.json)과 테스트 입력이 일치한다. C2 대비 변경 입력은 `AssistantControl.IdeThemes.cs`, `AssistantControl.ProgressPresentation.cs`, `AssistantControl.xaml.cs`, `CompletionNotification.cs`, 미리보기 PDF 다섯 파일이다. 이 후보의 PDF SHA-256은 `C382A0049B6BF76795885F950C8CC3C5D6DEEEF5C2A40FDF76E4E5C4CFA499DE`이며 실제 IDE 결과 후 최종 PDF 재빌드가 예고돼 있어 출판 검증으로 사용하지 않는다.

## 요구·지적 재판정

**R58-01 개인정보 / PASS(무창 유지). R58-02 작업 요약 / PASS(무창), 실제 OS PARTIAL.** 네 SKU 실제 C6 DLL을 각각 로드했다. `answer`는 `질문 답변 작성 완료`, 변경 있는 `proposal`은 `소스 수정안 준비 완료`, 완료된 파일 단계 하나는 `파일 작업 완료 보고`와 `파일 1`로 구분된다. 결과 유형에 프롬프트 원문을 직접 넣어도 알려진 `proposal` 외 문자열은 일반 답변 유형으로 처리한다. 합성 인명·급여·경로는 제목/본문 어디에도 나오지 않았다. [실제 호출 경로](../artifacts/task054/candidate6/source/src/VS2017.AIAssistant/Views/AssistantControl.ProgressPresentation.cs)는 실행 제공자와 `completionResultKind`만 OS 알림에 넘기고, 요청 원문은 전달하지 않는다. `completionResultKind`는 요청 시작마다 `answer`로 초기화하고 실제 수정안 `HasChanges`에서만 `proposal`로 설정된다. 실제 Windows 알림 화면·접근성·중복 표시 횟수는 이번 GUI 금지 범위에서 확인하지 않았다. [네 SKU 알림 결과](../artifacts/audit-codexorig2-task058/candidate6-notification-2017.json).

**R58-03 #16 테마 적용 / FAIL(실제 VS2017), 원인 미확정.** 네 SKU C6 패키지에는 현재 연도용 독립 테마 VSIX가 정확히 하나씩 있고, ZIP 내부와 Release 옆 파일의 SHA-256이 일치한다. 무창 설정 UI는 네 실제 DLL 모두 Kakao·Dracula 두 버튼과 접근성 이름을 확인했다. C6의 개인 잠금 파일 시험은 완료 XML 읽기 대기 458ms, 잠금 해제 뒤 임시 파일 삭제 445ms로 통과했다. 이는 DTE import/export 전체의 성공을 증명하지 않는다. [TASK064 실제 VS2017 전용 프로필 원자료](../artifacts/task064/candidate6-2017-v1/result.json)는 22개 중 16개 통과·6개 실패다. 이전 C2의 잘못된 pending 복구, 비테마 설정 보존, 임시 파일 정리는 개선됐으나 Kakao·Dracula 모두 성공 상태·pending 소비·재export GUID 변경에 실패했다. 공유 잠금이나 비동기 반환을 **확정 원인으로 판정하지 않는다**. VS2019/2022 실제 IDE 적용은 이 시점 자료가 없다. Root/TASK064의 후속 probe·수정 후보가 필요하다.

**R58-04 / 보통(P2) / C6 큐 실패 상태가 본래 CLI 오류를 가린다.** C6의 기본 FIFO 56/56 검사는 통과했지만, 새 상태 전용 회귀를 같은 고정 DLL에서 직접 실행하자 0/2 실패했다. 합성 CLI가 `controlled primary status error`를 반환했을 때 대기가 0개인 경우에도 상태가 `실패·취소 이후 대기 요청은 실행하지 않았습니다`로 덮였고, 대기 1개인 경우에도 같은 일반 문구만 남아 원래 오류와 취소 건수가 사라졌다. [C6 `CancelQueuedRequests`](../artifacts/task054/candidate6/source/src/VS2017.AIAssistant/Views/AssistantControl.RequestQueue.cs)는 대기 수와 무관하게 `StatusLabel.Text = reason`을 수행한다. 실패 원문이 대화 기록에는 남을 수 있지만 사용자가 보는 즉시 상태에서 제거되는 것은 재현됐다. C7의 한 메서드 수정 후보에서 0개 대기 시 원래 상태 보존, 대기 시 원래 오류 뒤에 취소 건수 추가를 재검사한다. [독립 실행 원자료](../artifacts/audit-codexorig2-task058/candidate6-queue-status-result.json).

## 변경 없는 범위와 검증 한계

#15 사용자 말풍선 XAML과 #13 파일 생성 Writer, #14 Claude 비로그인 경계 소스는 C2와 해시가 같다. C2의 무창 검사 결과를 이관하되 C6 실제 IDE 픽셀·물리 입력 또는 로그인된 Claude 답변 성공으로 다시 표시하지 않는다. C6 FIFO 기본 56/56은 상태 회귀 2개를 포함하지 않으므로 R58-04를 상쇄하지 않는다. 실제 IDE 설치/적용은 TASK064가 전용 프로필로 수행했고, 감사자는 GUI·설치 관리자·사용자 기본 IDE 프로필에 손대지 않았다.

제품 소스·공유 빌드·C1/C2 보고서를 수정하지 않았다. C7 이후 새 고정 후보는 입력/DLL/VSIX를 다시 대조하고 R58-04와 알림 회귀를 재실행해야 한다. 테마 자동 적용은 실제 IDE의 성공 GUID·pending 소비·비테마 보존·임시 정리까지 모두 통과해야 #16을 PASS로 올릴 수 있다.

## 증거

- [C6 감사 JSON](CodexOrig2_TASK-058_Audit_C6_Evidence.json), [입력 및 네 SKU 고정 SHA](../artifacts/audit-codexorig2-task058/candidate6-freeze.json).
- [알림 직접 실행기](../artifacts/audit-codexorig2-task058/Test-Candidate6Notification.ps1)와 [2017](../artifacts/audit-codexorig2-task058/candidate6-notification-2017.json)·[Legacy](../artifacts/audit-codexorig2-task058/candidate6-notification-2017.Legacy.json)·[2019](../artifacts/audit-codexorig2-task058/candidate6-notification-2019.json)·[2022](../artifacts/audit-codexorig2-task058/candidate6-notification-2022.json) 결과.
- [테마 패키지 바이트 검증](../artifacts/audit-codexorig2-task058/candidate6-themes-integrity.json), [개인 잠금 파일 실행기](../artifacts/audit-codexorig2-task058/Test-Candidate6ThemeAsync.ps1)와 [결과](../artifacts/audit-codexorig2-task058/candidate6-theme-async-result.json).
- [C6 FIFO 기본56 결과](../artifacts/audit-codexorig2-task058/candidate6-queue-result.json), [오류 상태 전용0/2 결과](../artifacts/audit-codexorig2-task058/candidate6-queue-status-result.json), [TASK064 실제 VS2017 16/22](../artifacts/task064/candidate6-2017-v1/result.json).
