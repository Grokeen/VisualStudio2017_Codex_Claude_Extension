# CodexOrig2 — Role.md 2026-10-20 12:47 추가 요구 1~5, C2 독립 중간 감사

대상은 `artifacts/task068/candidate2`의 v0.13.0-preview.1 **배포 전 후보**다. Role.md의 [image-68](image-68.png)은 실행 중인 대화 A를 둔 채 목록의 다른 대화 B로 이동해 각 방에서 작업하는 요구로 해석했다. 제품·공유 빌드·IDE·서버는 수정하거나 조작하지 않았다. 고정 소스 137개가 `source-hashes.json`과 **137/137 일치**했다. 작업 트리의 `AssistantControl.ConcurrentRooms.cs`, `AssistantControl.xaml.cs` 두 파일은 이후 수정돼 C2 증거로 혼합하지 않았다.

**중간 판정: C2 배포 게이트 미통과.** 두 확정 지적이 있으며, 2022 빌드 경고 2개와 이전 PDF도 최종 후보에서 해소해야 한다. 이 판정은 뒤따르는 C3/최종 후보의 판정이 아니다.

## 확정 지적

| ID | 심각도 | 실증 및 영향 | 소스 위치 |
|---|---|---|---|
| A68-01 | P1, 배포 차단 | 고정 C2 실제 DLL의 무창 `RuntimeEditProvider`/로컬 Claude 프로토콜 검사 **61 통과·3 실패**. 배정한 역할이 제안 경로와 전체 액세스 A/B 각각의 실제 CLI 요청 문맥에 없다. 같은 실행에서 방별 동시 PID·완료/취소·초안·대기열·승인 가드·사용량 라우팅은 통과했다. | [요청 문맥 생성](../artifacts/task068/candidate2/source/src/VS2017.AIAssistant/Views/AssistantControl.RequestQueue.cs#L158), [협업 유무 분기](../artifacts/task068/candidate2/source/src/VS2017.AIAssistant/Views/AssistantControl.ConcurrentRooms.cs#L189). [실행 원본](../artifacts/task068concurrent/candidate2-extended-2/result.json) |
| A68-02 | P2, 선택지 오인 | 고정 C2 DLL의 `ChatMessage.Choices`를 직접 호출한 8개 입력 중 7개가 기대와 일치했다. `The selected file has been checked.` 다음의 `1. build / 2. test`는 선택 요청이 아닌 완료 설명인데도 두 버튼 후보를 반환했다. 질문 판정의 영어 `select` 부분 문자열이 `selected`에도 걸린다. 클릭은 입력 초안만 작성하며 자동 전송·승인은 확인되지 않았다. | [질문 판정 정규식](../artifacts/task068/candidate2/source/src/VS2017.AIAssistant/Chat/ChatWorkspace.cs#L212), [클릭 핸들러](../artifacts/task068/candidate2/source/src/VS2017.AIAssistant/Views/AssistantControl.Chat.cs#L229). [실행 원본](../artifacts/task068-audit/candidate2-choices.json) |

Root가 `WORK_STATUS.md`에 두 지적의 파일·행·실증과 수정/재검사 상태를 반영해야 한다. 이 감사자는 위임된 새 보고서와 `artifacts/task068-audit` 이외의 공유 문서를 수정하지 않았다.

## 요구별 진행 판정

| Role.md 요구 | C2 관측 | 남은 검증 |
|---|---|---|
| 1. 대화방별 동시 작업 | 실제 C2 DLL 검사에서 A/B 독립 실행·전환·취소·큐·승인·토큰 분리 **61/64**. A68-01 역할 문맥 누락. | 수정 후보 재검사, Root의 실제 IDE/UI 경로. |
| 2. 시간별 저장·위로 스크롤 | C2의 `MarkdownWorkspaceStore`·`ChatWorkspace`·Committed 파일과 같은 해시의 실제 소스 무창 저장 검사 **19/19**. UTC 시간 청크, 최근 20개와 LoadOlder, 실패 전 메인 커밋, CAS/구버전 식별 등을 검사했다. | C2 DLL 자체에 대한 같은 검사, 사용자 IDE 스크롤 체감, 고객 저장 데이터. |
| 3. 좁은 화면 더블클릭 시 목록 접기 | 목록 항목 더블클릭·폭 460 미만·현재 선택 방 확인 코드와 XAML 이벤트 연결을 확인했다. | 실제 좁은 IDE에서 포인터/레이아웃 검사는 Root 담당. |
| 4. AI 선택지 제시·선택 요청 | 목록 감지·버튼 바인딩·선택 후 수동 전송 초안 흐름 확인. C2 DLL 7/8, A68-02. | 오인 수정과 실제 버튼/키보드 UI 검사. |
| 5. 필수 숨김 지침 보강 | 고정 C2 DLL 파일시스템 검사 **36/36**. 공식 이전 1·2번 사본은 정확한 바이트일 때만 1~5번으로 갱신하고 사용자 수정본은 보존한다. 내장 Compose의 사용자 원문 보존과 중복 제거도 확인. | 실제 설치 후 생성·IDE 요청 연결은 Root 담당. |

고정 네 Release 빌드는 오류 0이다. 2017 Legacy·2017·2019는 경고 0, **2022는 VSTHRD110 경고 2개**(C2 동시 방 소스 462, 497행)다. 네 C2 VSIX의 PDF는 모두 SHA-256 `F853789CE823A1EABE8B4E98226064A9E86F808389CA77033DF83925744F2372`인 **이전 0.12.1 문서**다. 새 문서·무경고 최종 후보를 기다린다.

`Prepare-ReleasePackages.ps1`은 최종 PDF와 네 VSIX 내부 PDF 해시 일치·네 무경고 빌드·7개 VSIX를 요구한다. `Prepare-UserDistribution.ps1`은 최종 ZIP SHA와 항목별 체크섬을 확인하고 허용 목록만 새 버전 디렉터리에 추출하며, VSIX 안의 소스/PDB/계정 항목을 거절하도록 작성돼 있다. `Publish-UiPreview.ps1`은 13자산 해시·이전 배포 보존·공개 후 익명 재다운로드 확인을 포함한다. **이 세 스크립트는 정적 검토만 했고 최종 ZIP·UserDistribution·게시를 실행하지 않았다.** 실제 사용자는 설치 필수 전달 트리만 받아야 하며 개발 소스/PDB/증거의 부재는 최종 산출물 검사로 확인해야 한다.

실제 IDE 네 SKU 584+ 검사, 신규 PDF, 설치·오프라인 경계, 사용자 전달 트리, 공개 게시·익명 13자산 해시, 이전 첨부 정리는 모두 후속 범위다. Claude 로그인·실계정 답변은 사용자 지시로 제외했다. [기계 판정 JSON](CodexOrig2_TASK-068_C2_InterimEvidence.json).
