# TASK-062: 01:40 추가 요청 독립 검토

작성: CodexCode · 2026-10-20

Role.md의 image56~65와 TASK054~061의 소유 선언을 확인했다. 제품 파일과 공유 빌드·GUI·릴리스는 변경하지 않았다. 아래는 현재 작업 트리의 정적 검토 결과이며 새 제품 DLL의 실행 결과가 아니다.

## 우선 수정: 저장 실패 시 기존 대화 1개 소실

`AssistantControl.RequestQueue.cs`의 `EnqueueCurrentRequest()`는 사용자 요청을 `request.Chat.Add(...)`로 추가한 뒤 `PersistCurrentConversation()`이 실패하면 마지막 항목만 지운다. `ChatSession.Add()`는 100개를 넘으면 맨 앞 항목을 즉시 제거한다. 따라서 기존 메시지가 정확히 100개일 때 저장이 실패하면 제출 전 맨 앞 기록이 복구되지 않고 99개만 남는다. `PersistCurrentConversation()`은 저장 실패 전 `record.Messages`도 새 요청을 포함한 사본으로 바꾸므로 두 모델의 내용까지 달라진다.

재현 조건: 메시지 100개인 대화에서 저장 경로 쓰기를 실패시키고 요청을 전송한다. 기대 결과는 입력 초안·대화 메시지 100개·`record.Messages`·디스크 내용이 전송 전과 동일한 것이다. 현재 코드에서는 입력만 복원된다. Backend055 소유 범위에서 전송 전 상태를 함께 복원하거나 저장 성공 후에만 메모리 모델을 확정하고, 100개 상한에서의 실패 회귀 검사를 추가해야 한다. `tests/task055`의 현재 FIFO 검사는 일반 실행·취소·초안은 다루지만 이 저장 실패 경계는 확인하지 않는다.

## 실행 의미 확인: 두 번째 대기 요청의 대화 문맥

02:33에 읽은 초안은 `EnqueueCurrentRequest()`에서 복사한 `ChatRequestContext`를 그대로 실행에 전달해 첫 답변이 두 번째 요청에 들어가지 않았다. Backend055가 작업 중인 최신 파일에는 제출·완료 순서 장부와 `BuildQueuedExecutionContext()`가 추가되어 이 문제를 해결하는 방향으로 바뀌었다. 아직 제품 DLL 검사가 끝나지 않았으므로, 테스트에서 **첫 답변은 두 번째 요청에 포함하고 세 번째 대기 입력은 제외하는지** 확인해야 한다. 접수 시점의 모델·첨부·문서 고정은 유지할 수 있다.

## 화면 감사 관찰

- image58~60은 같은 역할의 버튼이 다른 외형으로 보이고 Markdown·안내 footer가 크게 비는 문제를 보여 준다. Designer056의 현재 변경은 공통 `AssistantActionButton` 스타일과 footer 패딩 축소를 연결한다. 실제 렌더 크기·고 DPI·작은 창에서는 아직 검증되지 않았으므로 Auditor058 계획의 버튼 역할별 bounding box와 footer 교차 검사가 필요하다.
- image61의 provider 탭 간격은 기존 XAML의 오른쪽 margin 22 DIP에서 현재 10 DIP로 줄었다. 사용자 요청의 체감 간격은 실제 VS 창에서 다시 확인해야 한다.
- #6 탭 전환의 현재 최적화는 디스크 재쓰기만 건너뛴다. `PersistCurrentConversation()`은 전환할 때마다 전체 대화 `record`를 JSON으로 직렬화한 뒤 fingerprint를 비교한다. `tests/task055`의 40회 전환 검사는 쓰기 0회를 합격으로 보지만 측정한 시간에는 합격 기준이 없고, 메시지 100개·긴 본문 조건도 없다. Auditor058의 실제 후보 지연 측정에 이 큰 기록 조건을 넣어 CPU 지연을 확인해야 한다.
- image63의 입력 비활성 문제에 대해 현재 `UpdateButtons()`는 `Instruction.IsEnabled = !disposed`로 변경됐고 대기열은 8건으로 제한된다. 실제 WPF 입력·전송, 정지 후 초안 보존은 Backend055의 실제 DLL 검사로 판정해야 한다.
- image64의 브랜드/요약 알림은 Root054 소유 구현이 추가됐다. 요청 제목을 일부 표시하므로 잠금 화면·Windows 알림 정책에서 원문이 보일 수 있는 범위는 최종 설명에 명시해야 한다.
- #13 파일 생성은 현재 AI 답변의 컨텍스트 메뉴에서 사용자가 새 파일 경로를 선택하는 동작이다. 자연어 요청만으로 자동 저장하지 않는 현재 범위를 사용자 안내에 정확히 적어야 한다.

제품 수정·통합 합격 판정은 각 소유 담당자와 Auditor058/Root054의 고정 후보 검증에 맡긴다.
