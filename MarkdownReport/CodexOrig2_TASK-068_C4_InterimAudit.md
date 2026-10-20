# CodexOrig2 — TASK068 C4 고정 기술 후보 재감사

대상은 `artifacts/task068/candidate4`의 v0.13.0 기술 후보이다. `source-hashes.json`의 **137개 소스가 고정 사본과 137/137 일치**했다. 네 SKU Release 빌드는 모두 종료코드·오류·경고 **0**이다. Root의 실제 IDE 검사가 별도로 진행 중이라 감사자는 창 표시·포인터·IDE 조작·원격 AI·로그인을 하지 않았다. **C4는 신규 PDF 전환 전 후보이므로 최종 배포 판정 대상이 아니다.**

## C2 지적 재검사

| 지적 | C4 관측 | 판정 |
|---|---|---|
| A68-01 방별 역할 문맥 누락 | 고정 2017 DLL SHA-256 `38D9A3C68A362C9A5499E5C80CAE282FCCA885C39FA9D9EE8927099D8B97DBEA`의 실제 `RuntimeEditProvider`/로컬 Claude 프로토콜 무창 검사 **82/82**. 제안 요청과 전체 액세스 A/B에 각자 배정한 역할이 포함된다. A/B 독립 CLI PID, 방별 완료·취소·초안·큐·승인·사용량, 가짜 IDE 보호 범위의 역순 완료·외부 신호·Dispose도 통과. | **검사한 C4 DLL에서 해결.** 실제 IDE 읽기 전용 복원은 별도. [실행 원본](../artifacts/task068-audit/candidate4-concurrent-1/result.json) |
| A68-02 완료 목록의 선택지 오인 | 고정 C4 DLL `ChatMessage.Choices` 직접 호출 **8/8**. C2에서 실패한 `The selected file has been checked.` + 번호 목록은 이제 버튼 후보 **0개**다. 선택 질문의 정상 2·5개, 코드 fence, 중복 번호, 역할, 후행 설명도 확인. | **검사한 C4 DLL에서 해결.** 실제 버튼 화면·포인터는 Root 검사 범위. [실행 원본](../artifacts/task068-audit/candidate4-choices.json) |

## 시간별 기록과 남은 입력 경계

C4 고정 `AssistantControl.Chat.cs`·`AssistantControl.Viewport.cs`의 실제 기록 메서드와 저장 소스를 추출하고 모두 C4 소스 해시와 대조했다. `Window.Show` 없이 WPF/제어된 파일 조회로 **10/10**: 디스크 조회 중 Dispatcher 응답, 중복 스크롤 단일 로드, A→B→A 전환과 늦은 결과 폐기, Dispose, 실패 재시도, 끝 커서, 읽기 위치 앵커를 확인했다. Reader gate를 대체한 추출 메서드 검사이며 **고정 DLL 전체의 실제 IDE 스크롤 실측은 아니다**. [실행 원본](../artifacts/task068-audit/candidate4-history-ui/history-ui-result.json) · [C4 추출 해시](../artifacts/task068-audit/candidate4-history-ui/source/source-hashes.json).

**C4 미해결 입력 경계(A68-03, 소스 경로상 위험):** 전체 액세스 요청 캡처는 읽기 전용 여부를 검사하지 않은 채 [사용자 메시지 추가와 초안 삭제](../artifacts/task068/candidate4/source/src/VS2017.AIAssistant/Views/AssistantControl.RequestQueue.cs#L161)를 끝낸다. 그 뒤 [실행 시작 시 문서 검증](../artifacts/task068/candidate4/source/src/VS2017.AIAssistant/Views/AssistantControl.ConcurrentRooms.cs#L189)이 사용자가 원래 읽기 전용으로 연 문서를 거절할 수 있다. 이 경우 요청 원문은 대화 기록에 남지만 입력란 초안은 복원하는 코드 경로가 없다. C4에서 별도 읽기 전용 입력 재현은 하지 않았으므로 **확정 런타임 실패 수로 세지 않고** C5의 사전 거절·초안 보존 검증을 요구한다. 반면 동시 실행 중 IDE가 임시로 만든 보호 상태는 `IExternalEditingScopeState.IsTemporarilyReadOnly(filePath)`가 정확한 경로·원래 읽기 전용 여부·파일 속성을 확인하도록 C4에 반영됐다. 가짜 보호 범위 수명은 82/82 검사에 포함됐지만 실제 IDE 원복은 Root 검사다.

네 C4 VSIX에서 PDF는 각각 1개이며 SHA-256이 모두 `F853789CE823A1EABE8B4E98226064A9E86F808389CA77033DF83925744F2372`인 **이전 0.12.1 문서**다. 신규 0.13.0 PDF, C5 전체 재빌드·소스 해시·실제 IDE, 최종 ZIP/UserDistribution의 소스·PDB 제외, 설치·오프라인 경계, 게시·익명 13자산 해시, 정리 순서를 아직 판정하지 않았다. [기계 판정](CodexOrig2_TASK-068_C4_InterimEvidence.json).
