# TASK069 최종 IDE 대화 바인딩 진단

담당: CodexOrig 보조 개발 055 · 2026-10-20

C4 Legacy 실제 IDE 두 실행에서 Markdown 샘플 미리보기를 찾지 못했다. early3의 `ChatContainsMessage=true`, `VisibleContainsMessage=false`, `ActiveChatMatches=false`는 샘플을 추가한 chat과 나중에 선택된 chat이 달랐음을 보여 준다. 단순 render 대기 시간 부족과는 구분해야 한다. 새 프레임 Hide/Show 네 검사는 샘플 검색 이후이므로 이 실패의 선행 원인이 아니다.

원문은 `artifacts/ide/run-d9f61005a3a3454a968edfa6fad738ca`와 `run-f41cef597e524ba084d382be649bd084`에 보존되어 있다. view는 표시 중이며 크기는 1527×896이었고, 다른 두 메시지만 표시됐다. 해당 실행에는 변경 전후 provider/record 주소와 선택 이벤트 stack이 없어 어떤 선택 동작이 원인이었는지 확정하지 못했다.

ActiveChat getter는 selectedProvider → activeRecords의 ID → chats 사전의 객체 순으로 선택한다. metadata 조회, 지연 담당자 표시 재시도, 테마 갱신은 이 선택 키를 직접 변경하지 않는다. 실제 선택 변경의 후보는 ProviderChanged, ConversationSelected, ActivateAssignedAgent 등이다. 모달 창·팝업 이후 native focus 복원과 목록 선택의 관계는 가설이며 확인된 원인으로 간주하지 않는다. 제품은 임의 수정하지 않았다.

`artifacts/task069/binding055/result.json`의 C4 Legacy 실제 DLL 무창 진단은 13조건을 통과했다. 세 대화 각각 네 테마 전환 뒤 메시지 추가·Measure/Arrange·Dispatcher 처리와 3.2초 지연에서 chat 참조와 표시 컬렉션이 유지됐다. 이 진단은 Window.Show·모달·native focus를 사용하지 않아 실제 IDE 실패를 재현한 검사가 아니다. DLL 사본과 원본 SHA 일치는 inputs.json에 기록했다.

C5 첫 실제 실행은 새 Role15의 AssistantSurfaceBrush와 옛 UI054의 AssistantControlBackgroundBrush 기대가 충돌하여 더 앞에서 중단됐다. Root가 해당 구기대만 새 요구에 맞춘 뒤 C5 Legacy 실행 `artifacts/ide/run-9cda7f283aad4f2fad9f1b687f07c11f`은 161/161 통과했다. 바인딩 관찰의 InitialAddress와 CurrentAddress가 같고 ActiveChatMatches·VisibleContainsMessage·PreviewFound가 모두 true였다. 샘플과 원래 coordinator의 첨부 표시가 함께 보였다. 프레임 숨김·복원도 문서를 재사용했고 308.746ms(고정 대기 120ms 포함)였다.

판정은 **C4 실패 원인 미확정, C5 실제 실행에서는 재현되지 않음**이다. 제품과 하네스가 함께 달라졌으므로 특정 제품 변경이나 더 긴 대기가 해결 원인이라고 단정하지 않는다. 실패 원문과 이 한계를 유지하며 Root의 최종 네 SKU 실제 IDE 검사를 이어간다.
