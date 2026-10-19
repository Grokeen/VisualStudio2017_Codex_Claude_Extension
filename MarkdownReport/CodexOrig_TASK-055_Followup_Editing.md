# TASK055 후속 — 편집 회귀 5개 실패 진단

2026-10-20 03:10~03:13 KST, 약 4분, CodexOrig 보조 개발. Root의 최신 통합 회귀 중 editing 실패를 맡아 `tests/editing/EditingTests.cs`와 `Test-SourceEditing.ps1`만 변경했다. 제품 5개 소유 파일과 다른 테스트는 수정하지 않았다. 검사 종료 즉시 GUI 예약을 Root에 반납했다.

## 동일 고정 제품에서 재현

Root candidate2 VS2017 DLL SHA256은 `618CE370C6551E8E24ABB1DB8D60A885F84B9096DBC0A81C8757890976489BB4`다. 해당 후보의 `project.assets.json`도 직접 지정했다(SHA256 `5DCD09E84AC3D4BCC68E1122D06EF88BCF73D3CEE4F1E13F47B0C2D044F1A658`). 제품 재빌드나 교체 없이 기존 검사로 **87/92**, Root의 동일한 WPF 5개 실패를 재현했다. 원문은 `artifacts/task055/editing-c2-original/results.log`와 launcher.log, 기존 소스/드라이버는 `editing-followup-original/`에 보존했다. 이전 보고서·Root 실행 결과는 변경하지 않았다.

## 확인된 원인과 수정

기존 검사는 `GenerateButton.IsEnabled`가 요청 시작에 false, 종료에 true라고 가정했다. 새 요구는 실행 중에도 전송 버튼을 활성으로 두어 FIFO에 추가해야 한다. 따라서 첫 검사는 시작 직후 실패했고, 나머지는 전송 후 `PumpUntil(generate.IsEnabled)`가 이미 true인 상태에서 즉시 반환해 아직 승인할 수정안이 도착하지 않았는데 Apply 버튼을 검사했다. Stop 후에도 실제 취소 정리가 끝나기 전에 새 요청을 검사했다.

최신 입력 요구를 `generate.IsEnabled && Instruction.IsEnabled && IsRequestRunning(control)`로 명시 검사한다. 완료 대기는 실제 `busy`와 FIFO 소비자 `drainingRequests`가 모두 false인지 확인한다. 완료/실패 때 실행·전송·승인 상태와 StatusLabel을 원문 로그에 남긴다. 이 대기를 통해 기존 승인·적용·저장·복구·외부 편집 충돌·Provider 전환 승인 초기화·캡처한 파일 적용·Stop 후 재실행 검사를 유지한다.

반복 전송 검사는 첫 미승인 수정안을 두 번째 전송이 덮을 수 있다는 이전 가정을 없앴다. 두 번째 요청 제출 직후 기존 수정안 참조가 유지되고 실행이 대기하는지 확인한 뒤, 실제 **이 수정안 건너뛰고 다음 요청** 버튼을 클릭한다. 이후 기존 fixture가 규칙/모델/첨부/이전 turn 전달을 그대로 검사한다. 사용자·AI 4개 메시지를 검사하고 새로 필요한 건너뛰기 시스템 기록은 별도로 구분한다. 새 대화 생성 후 기록·첨부·적용 초기화도 유지했다.

드라이버에는 정확한 후보 assets와 새 출력 폴더를 지정하는 선택 매개변수와 실행 해시 요약을 추가했다. 출력 폴더가 있으면 덮어쓰지 않는다. 기존 기본 호출도 유지한다.

## 결과와 인계

동일 DLL/동일 assets로 수정한 검사 **92/92 PASS**, 종료 0. `artifacts/task055/editing-c2-revised/results.log`, `summary.json`, `editing-c2-revised-launcher.log`에 결과를 보존했다. 이 진단에서 제품 결함을 재현하지 않았고 제품 수정은 하지 않았다. 실제 IDE/유료 AI 실행이 아닌 fixture CLI·WPF 검사 범위다. 최신 최종 후보에서도 Root가 같은 회귀를 실행하고 전체 검사·빌드/컴파일·최신 Release 제작·공개 배포·익명 다운로드 해시 검증까지 마감해야 한다.
