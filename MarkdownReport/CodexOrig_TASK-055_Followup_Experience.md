# TASK055 후속 — Advanced experience의 FIFO 전제와 원래 오류 표시

2026-10-20 03:15 KST부터 CodexOrig 보조 개발이 정적 진단·무창 검사를 수행했다. 실제 IDE 예약은 다른 담당자가 사용 중이므로 Window.Show·Popup·OS 입력·전체 experience 실행은 하지 않았다.

## 기존 experience 실패 전제

Root C2 `launcher-retry3-experience-candidate2.log`의 마지막 성공은 잠긴 대화 파일에서 AI identity/team/참여자를 함께 복구하는 검사였다. 첫 협업 성공의 미승인 HasChanges 수정안이 남은 상태에서 다음 실패 요청을 제출하므로 새 FIFO는 실행을 승인 대기에서 멈춘다. 기존 검사의 StopDisabled 또는 GenerateEnabled 조건은 실제 CLI 완료를 나타내지 못한다.

이관받은 `tests/review/AdvancedExperienceChecks.cs`만 수정했다. 다음 요청을 enqueue한 뒤 기존 수정안 참조와 실제 대기 상태를 확인하고, 실제 DiscardProposalButton으로 명시적으로 건너뛰게 했다. 임의 ClearProposal 호출이나 자동 승인·적용은 하지 않는다. 실행 완료는 busy와 drainingRequests를 확인한다. 원래 primary CLI 오류·report 잠금·전달 후 대화 저장 오류·취소·양방향 제공자 옵션/경로·협업 전달·주소 모호성·소스 미수정 검사는 그대로 유지했다. 기존 파일은 `artifacts/task055/AdvancedExperienceChecks-before-followup.cs`에 보존했다.

## 별도 확인한 실제 제품 결함

C6 RequestQueue의 실패 마감은 대기 요청이 없어도 CancelQueuedRequests(reason)를 호출하며 원래 StatusLabel을 일반 취소 안내로 덮었다. 이는 단순 검사 전제가 아닌 사용자 진단 손실이다. Root가 CancelQueuedRequests 한 메서드의 수정 권한을 부여한 뒤, queued=0이면 상태를 덮지 않고 실제 잔여 대기가 있을 때만 원래 메시지 아래 취소 개수/안내를 추가하도록 수정했다. 다른 제품 메서드는 변경하지 않았다. 동결 파일 SHA256은 `7580627721147E4DCDE3345F9BE118254EBC71B6B2DE625CC52662F8C6745FD3`다.

`tests/task055/Test-RequestQueue.ps1 -StatusOnly`의 두 시나리오는 표시 창 없이 실제 제품의 요청 실패 경로를 실행한다. C6 DLL SHA `BECE45F7C93D95DB9C8FB5B635DF9A080FF7388031F651DC0FF4FE09497D9DBA`에서 **0/2**로 버그를 재현했다. queued=0/1 모두 실제 메시지가 `실패·취소 이후 대기 요청은 실행하지 않았습니다. 대화의 원문을 확인해 다시 전송하세요.`였고 `controlled primary status error`가 사라졌다. 원문과 상태 값은 `artifacts/task055/status-c6-original-v2/result.json`에 보존했다. 최초 같은 0/2 검사도 별도 폴더에 보존했다.

## 인계

수정 포함 Root C7의 동일 무창 검사 **2/2 PASS**를 확인했다. DLL SHA256은 `14F2FD37A01324AFFA4634A0D89913FA06658194620E37BB9571CD5AE1D99B2E`이고 `artifacts/task055/status-c7-fixed/result.json`에 queued=0의 원래 오류 단독 표시, queued=1의 원래 오류와 추가 취소 개수 안내를 보존했다. 원래 C6 0/2는 그대로 유지한다. GUI 예약 반환 후 전체 experience 검증은 아직 대기 중이며 완료로 표시하지 않는다. Root가 최신 후보 통합·감사·전체 회귀·4 SKU 빌드/실제 IDE·Release 제작·공개 배포·익명 해시까지 이어서 마감한다.

## C9 전체 GUI 검사 1차 실행 — 2026-10-20 03:31 KST

064가 GUI 예약을 반환한 뒤 Root가 전체 experience 실행을 승인했다. Root의 현행 드라이버를 개인 사본으로 보존하고 candidate9의 정확한 assets 경로만 지정해 실행했다. C9 DLL SHA256은 `9811B6CCF89A393E444B92E81329922FD5F15E16CC8D9925C5DC9726C2DA6D17`다. 이 실행은 실제 IDE가 아닌 격리 WPF 창·모달·합성 Provider 검사다.

**120개 통과 후** Root 소유 `DropdownExperienceChecks.CheckFanMenuStability`의 `Repeated pointer-enter events open the settings fan once without restarting its animation`에서 중단했다. 정적 읽기상 이 검사는 MouseEnter 15회 후 Dispatcher 지연 없이 즉시 fan.IsVisible을 요구하며, 새 ScheduleFanOpen의 220ms 지연 요구와 대조가 필요하다. 실제 제품 회귀인지 합성 hover 전제인지를 Root가 진단하도록 전달했다. 해당 assertion·Root 파일·제품은 변경하지 않았다.

이전 timeout 지점 이후의 수정안 보존/실제 건너뛰기, report 잠금 시 primary CLI 오류, 실패 handoff, peer 취소, 전달 후 저장 오류, 문서 추출 실패/취소 검사는 이번 실행에서 모두 실제로 통과했다. 이후 양방향 제공자 협업 등은 fan 실패로 아직 도달하지 않았으므로 완료로 표시하지 않는다. 원문 `artifacts/task055/experience-c9-v1/result.json`, `experience-c9-v1-launcher.log`를 보존하고 검사 종료 즉시 GUI 예약을 Root에 반환했다. Root의 fan 검사 보완 및 재실행 지시를 기다린다.