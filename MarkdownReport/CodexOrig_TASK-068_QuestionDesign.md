# TASK068 질문과 선택지 기능 읽기 조사 및 설계

Role 2026-10-20 12:47 #4는 AI가 필요한 정보를 질문하고 사용자가 보기 또는 직접 입력으로 답하는 기능이다. Code TASK067의 전체 제품 소유권을 존중해 이번 작업은 읽기 조사·설계·검사 데이터 준비만 했다. 제품 파일, 기존 사용자 문서, 공유 bin/obj를 수정하지 않았고 컴파일·GUI·로그인·원격 AI 추론도 실행하지 않았다. 아래 신규 구조화 계약은 **제안이며 승인된 구현이나 완료 기능이 아니다**.

## 현재 코드에서 확인한 경계

`CodexRuntimeRunner`는 app-server의 `item/completed` agentMessage와 turn 완료를 받아 최종 문자열을 만든다. id+method가 있는 미지원 서버 요청은 오류로 처리한다. `ClaudeRuntimeRunner`는 stream-json의 최종 result 문자열을 받고, control_request는 추가 도구 승인 요구로 실패 처리한다. `NativeJsonSession.Route`는 일반 RPC 응답과 notification을 구분하지만 사용자 질문에 대한 별도의 응답 API를 제공하지 않는다. 이 동작만으로 native 질문 지원 여부나 프로토콜의 최신 상세 규격을 주장하지 않는다. 원격 문서 조회나 CLI 로그인/추론은 수행하지 않았다.

`ChatRequestContext`는 최근 대화 10개·24000자와 첨부를 복사한다. 요청 지침은 EffectiveInstructions에서 필수 규칙과 합친다. 단일 파일 제안 모드는 `EditProposal.JsonSchema`의 summary/edits 두 필드만 허용하며 summary 최대 4000자, edits 최대 64개다. 질문 필드를 JSON에 임의로 추가하면 기존 파서에서 거부된다.

`ChatMessage`는 Role/Text를 기본으로 하고, 대화 상태 enum에는 Idle/Running/Completed/Failed/Stopped/Interrupted가 있다. 별도의 질문 batch/답변 제출 상태는 확인하지 못했다. 요청 큐는 제출 시점의 record/chat/provider/context를 고정한다. 질문 답변 역시 현재 화면의 전역 active chat을 따라가면 안 되고 소유 대화에 결합해야 한다. 이 영역은 TASK067의 대화 독립 실행·시간별 history 작업과 직접 겹친다.

**조사 도중 Code의 신규 작업을 추가로 관측했다.** `ChatMessage.Choices`가 최종 번호 목록을 읽고, MessageChoices가 버튼을 표시하며, MessageChoiceClicked가 선택 문장을 입력란 초안에 추가한다. 버튼 자체가 직접 전송하거나 도구를 승인하지 않는 점은 유지할 가치가 있다. 이 코드는 작업 중인 트리이므로 완료·동결 후보로 평가하지 않는다. 번호 목록 parser는 질문별 ID나 fence/인용/예제 경계를 구분하지 않고 전체 본문의 선택/choose/option 단어를 조건으로 삼으므로, 일반 작업 절차와 코드 예제를 선택 질문으로 오인할 가능성을 수락 검사에 포함해야 한다. 실제 제품 호출로 이 가능성을 검증한 것은 아니다.

## 제안하는 단계

현재 번호 선택 MVP를 먼저 마감한다면 버튼의 뜻은 **선택을 초안에 넣기**로 유지하고, 사용자가 별도 보내기를 눌러야 요청하게 한다. fence/인용 안 번호 목록을 제외하고 명시적인 질문과 인접한 목록만 인식하는 규칙, 소유 대화·provider 확인, 기존 초안 보존, 재클릭·history 재로드 동작을 Code와 합의한다. 구조화 parser를 추가하기 전까지 이를 여러 질문을 관리하는 양식이나 동일 CLI 세션의 중단/재개 기능으로 설명하지 않는다.

더 명확한 구조화 질문이 필요하면 **최종 assistant 메시지 전체가 단일 `ask_user` fenced JSON**인 경우만 카드로 만든다. 다른 문장의 일부·인용·일반 json/code fence·첨부/도구 결과·부분 스트림은 대상이 아니다. 잘못된 값은 원래 Markdown을 그대로 보여주고 자동 보정·선택·실행하지 않는다.

````text
```ask_user
{"version":1,"questions":[{"id":"environment","prompt":"어떤 테스트 환경을 사용할까요?","options":[{"id":"local","label":"로컬 시험 환경","description":"현재 PC의 격리된 데이터로 확인합니다."},{"id":"customer","label":"별도 시험 PC","description":"대상 PC에서 설치 결과를 확인합니다."}]}]}
```
````

현재 schema와 호환하려면 제안 모드는 `edits=[]`와 summary 안의 단일 질문 블록을 사용한다. 질문 JSON은 최대 3500자로 제한해 summary 4000자 안에 들어가게 한다. full-access 답변도 같은 형식을 사용할 수 있다. 모델 지침에는 정보가 부족할 때 추측하거나 의존 작업을 진행하는 대신 질문을 반환하도록 명시하되, 이를 강제 도구 승인 장치로 취급하지 않는다.

## 파서와 모델 계약 제안

- root 필드는 version=1과 questions만 허용한다. 질문 1~3개, 질문당 선택지 2~5개로 제한한다.
- 질문은 id/prompt/options, 선택지는 id/label 및 선택적인 description 문자열만 갖는다. prompt 최대300자, label 최대80자, description 최대160자다. IDs는 ASCII 소문자 시작의 `[a-z][a-z0-9_-]{0,63}`로 제한하고 batch 내 질문 ID 및 질문 내 option ID는 고유해야 한다.
- 모르는 필드·타입 변환·기본 선택/default·명령/action 필드를 받지 않는다. null 필수 문자열·제어 문자·과대 중첩/길이·미완결 JSON·중복 JSON key도 거부한다. Escape된 동일 key 중복도 검사한다. 기존 JavaScriptSerializer 단독 사용은 중복 key를 조용히 덮을 수 있으므로 별도의 제한된 reader/중복 검증 경계가 필요하다.
- 질문·선택지는 WPF 일반 TextBlock/TextBox 문자열로 표시한다. label 안 HTML/URL/명령을 렌더 실행하거나 자동 탐색하지 않는다.
- AI가 준 ID를 host의 실행 ID나 경로로 쓰지 않는다. host가 별도 batch GUID를 만들고 record ID/provider/원본 assistant 메시지 hash와 immutable 질문 배열에 결합한다.

제안 DTO는 `QuestionBatch`, `QuestionItem`, `QuestionOption`, `QuestionAnswer`다. 파서는 UI·디스크·CLI와 독립된 순수 함수로 두고, 실패하면 null/실패 결과와 원문을 반환한다. Code가 소유권을 양도하기 전 이 DTO나 parser를 제품에 생성하지 않는다.

## 카드와 응답 제출 흐름

질문마다 radio 선택지와 '직접 입력' 경로를 제공하며 **초기 선택은 없다**. 보기 선택이나 입력만으로 user 메시지·provider 호출·도구 실행을 만들지 않는다. 모든 필요한 답을 입력한 뒤 명시적인 '응답 보내기'를 눌러야 한다. 직접 입력은 1~2000자이며 일반 대화로 저장될 내용임을 UI에서 알 수 있게 한다. 기존 요청 입력란 초안과 질문 응답 초안은 섞거나 지우지 않는다. 표준 테마·키보드 탐색·접근성 이름·좁은 화면 줄바꿈을 재사용한다.

제출은 소유 대화와 provider에 결합한 **새 일반 user 요청**이다. 선택지 ID만 전달하지 않고 고정 질문 원문, 보기 원문, 사용자가 고른 ID/직접 입력, host batch ID를 구조화한 본문으로 전달한다. 따라서 최신10개/24000자 문맥에서 원 질문이 빠져도 독립적으로 의미가 보존된다. 문자열을 shell argument나 JSON tool approval 응답으로 변환하지 않는다. 제출 후 AI가 사용할 실행 권한은 기존 선택 모드와 승인 경계를 그대로 따르며 질문 선택이 권한을 올리지 않는다.

이 방식은 **동일 CLI 프로세스의 pause/resume가 아니다**. 질문 결과로 한 turn을 마치고 다음 사용자 요청으로 진행한다. native tool 승인/control_request와 연결하지 않아 현재 fail-closed 경계를 유지한다. 모델이 질문 전 이미 수행한 작업이 모두 되돌아가거나 기술적으로 차단됐다고도 주장할 수 없다.

제출은 중복 클릭에 대해 최대 한 번 수락한다. 큐 가득 참·저장 실패·대화 삭제·provider 전환·dispose·오래된 카드의 늦은 이벤트는 답변 초안을 보존하고 다른 대화로 보내지 않는다. 큐 수락/영속 기록이 실패했는데 Submitted로 표시하면 안 된다. 현재 화면이 바뀌어도 특정 batch를 전역 현재 대화로 재해석하지 않는다.

history 재로드/시간별 페이지 조회에서 이미 제출·보류한 질문을 새 미응답으로 다시 활성화하지 않도록 host batch 상태를 보존해야 한다. 저장 위치·message ID·시간별 분할의 경계는 Code #2와 합의할 사항이다. 사용자가 임의로 입력한 응답 marker를 신뢰해 승인 상태를 만드는 방식은 피한다. UI상 질문 대기와 CLI 작업 중 상태를 구분해야 하며 현재 enum에 새 상태를 추가할지 별도 pending 표시를 둘지도 소유자와 합의한다.

## 준비한 검사와 남은 일

`tests/task068questions/QuestionProtocolCases.json`에 제안 parser 사례 33개와 interaction 사례 14개를 준비했다. 정상/CRLF/다문항, user/system/tool, 부분·인용·중첩·일반 code fence, 복수 envelope, malformed/미지원 version/중복 key·ID/알 수 없는 command·default 필드/길이·옵션 제약, literal HTML·명령 label과 일반 번호 목록 오인을 포함한다. 상호작용 사례는 무선택·명시적 제출·직접 입력·취소·중복·잘못된 소유자·큐/저장 실패·재로드·모델 history 절단·native 승인 경계·dispose다.

`Test-QuestionFixtureData.ps1`은 데이터만 읽고 ID·유효 JSON 예시를 확인한다. 제품 parser/UI/큐는 실행하지 않으며 결과에도 `ProductParserTested=false`, `ProductUiTested=false`, `ContractAcceptedForImplementation=false`를 명시했다. 이 준비를 기능 PASS나 구현 완료로 표현하지 않는다.

Code와 #2/#4 분할·질문 형식을 합의한 뒤에만 제품 구현을 진행해야 한다. 원격 로그인·실제 AI 추론은 사용자 지시대로 금지하며 합성 응답/fake protocol과 고정 DLL로 검증한다. 최종 실제 제품 검사·독립 감사·4SKU 빌드/컴파일·최신 Release 제작·공개 배포·익명 다운로드 해시 확인은 Root 통합 책임으로 남는다.
