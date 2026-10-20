# TASK068 선택지와 필수 지침 독립 관측

CodexOrig 보조가 Root의 후속 요청으로 현재 제품 소스의 일부를 개인 artifact에 추출·복사해 검사했다. Code TASK067의 제품 소유권은 유지하며 제품 수정은 하지 않았다. 33개 구조화 질문 제안 사례를 현재 기능의 실패 수로 사용하지 않았다. 이번 결과는 실제 추출 getter와 실제 신구 Ensure/Compose 호출의 관측이다.

근거 폴더: `artifacts/task068/independent-choices-rules/`. 입력 원본·추출 소스·컴파일 로그·실행 로그·개별 JSON을 모두 보존했다. 공유 bin/obj나 실제 사용자 workspace를 사용하지 않았다. 실제 WPF/GUI·버튼 클릭·로그인·원격 AI·실추론은 실행하지 않았다. 제품 프로젝트 빌드는 하지 않았으며 개인 helper DLL 3개와 관측 runner만 컴파일했다.

## A 선택지 후보가 일반 목록과 예제에도 생긴다

현재 ChatWorkspace.cs에서 **ChatMessage 클래스 본체만** 추출하고 namespace/using wrapper만 추가해 독립 컴파일했다. Choices 구현은 수정하지 않았다. 현재 XAML의 MessageChoices ItemsSource는 Choices에 바인딩되므로 getter의 반환 값이 버튼 항목 후보가 된다. 실제 UI를 띄운 것은 아니므로 버튼 픽셀/마우스 동작의 실측으로 표현하지 않는다.

| 실제 getter 입력 | 반환 값 |
| --- | --- |
| `어느 환경을 선택하시겠습니까?` + `1. 로컬` / `2. 대상 PC` | 로컬, 대상 PC |
| `선택한 파일을 검사했습니다.` + `1. 빌드` / `2. 테스트` | 빌드, 테스트 |
| `선택 예제이며 질문이 아닙니다.` + text code fence 안 `1. 빌드` / `2. 테스트` | 빌드, 테스트 |
| `다음에서 선택해 주세요.` + 번호 `1. 첫째` / `1. 둘째` | 첫째, 둘째 |
| `다음에서 선택해 주세요.` + 번호 `4. 넷째` / `2. 둘째` | 넷째, 둘째 |
| 선택 단어 없는 `검사를 완료했습니다.` + `1. 빌드` / `2. 테스트` | 없음 |
| user/system 역할의 정상 질문 목록 | 없음 |

완료 설명과 code fence 예제에 각각 두 선택지가 생기는 것은 **재현된 질문 오인 후보**다. 중복/역순 번호도 별도 구별 없이 받는 것은 확정 관측이지만, 텍스트가 서로 달라 버튼 선택 자체가 불가능한 결함이라고 단정하지 않는다. 8개 입력 관측을 8건 실패로 세지 않는다.

원인은 전체 Text의 선택/choose/option 포함 여부와 역방향 번호 regex만 확인하고, 질문의 의미·인접성·fence 경계를 구분하지 않는 점이다. 코드 fence 닫는 줄을 건너뛰다가 내부 번호 목록을 모으는 것도 실제로 확인했다. 입력별 원문과 반환 문자열은 `choices-observations.json`에 있다.

심각도는 **낮은 사용성/오인 범주**로 제안한다. 현재 MessageChoiceClicked 소스를 읽으면 선택을 요청 초안에 추가할 뿐이며 직접 전송·도구 실행은 하지 않는다. 해당 handler를 실제 호출하거나 도구 자동 실행 여부를 동적으로 시험한 것은 아니다. 이 관측으로 자동 승인·원격 명령 실행·보안 취약점을 주장하지 않는다.

수정 제안은 Code의 현재 MVP를 유지하면서 (1) fence/인용 데이터 안의 목록 제외, (2) 목록 바로 앞의 명시적인 질문·선택 요청만 수용, (3) 필요하면 1..N의 연속/고유 번호 확인, (4) 원래 Markdown 유지와 모호한 경우 버튼을 생략하는 보수적 fallback이다. 구조화 ask_user는 별도 장기안이며 이번 자료로 강제하지 않는다. 클릭 시 소유 대화/provider 결합도 TASK067과 함께 확인할 수 있지만 이번 무GUI 관측으로 대화 혼선이 재현됐다고 말하지 않는다.

## B 이전 공식 파일은 보존되고 실제 요청 문맥은 새 규칙을 쓴다

기준 commit은 `25e5b15d40f9df583ebc5a3ee6d5f8397a447f98`다. `git show HEAD:src/VS2017.AIAssistant/Chat/MandatoryInstructionStore.cs`의 stdout 원래 바이트를 개인 fixture 파일로 추출했다. 이전 클래스를 별도 DLL로 컴파일해 실제 이전 Ensure로 공식 숨김 파일을 만든 뒤, 현재 실제 MandatoryInstructionStore의 Ensure와 Compose를 호출했다. 임의로 이전 공식 문안과 유사한 문자열을 만든 테스트가 아니다.

| 관측 | 이전 공식 파일을 업그레이드한 fixture |
| --- | --- |
| 이전 공식 파일 | 규칙 1·2 |
| 새 built-in DefaultInstructions | 규칙 1·2·3·4·5 |
| 새 Ensure 이후 디스크 공식 파일 | 여전히 규칙 1·2 |
| 원본 바이트 SHA/수정 시각/숨김 속성 | 보존 |
| Ensure 반환 경로 | 이전 공식 파일과 동일 |
| Ensure notice | 반환됨 |
| 실제 Compose(user) 결과 | 새 규칙 1·2·3·4·5 포함 |
| 사용자 지침 원문 | 끝부분에 그대로 보존 |

반환된 안내 원문은 다음과 같다.

> 필수 규칙 관리 사본의 내용이 기본값과 다릅니다. 파일은 보존했으며 요청에는 내장 기본 규칙을 사용합니다.

빈 새 workspace의 대조 검사에서는 실제 디스크 숨김 파일에도 규칙 1~5가 생성되고 notice는 없다. 사용자 메모가 추가된 파일의 대조 검사에서는 그 원문을 보존하고 notice를 반환하며, 해당 메모를 읽어 필수 규칙으로 승격하지 않는다. 두 대조 결과도 별도 JSON으로 보존했다.

따라서 **이전 공식 관리 사본 자체가 새 5개 문안으로 갱신되지 않는 차이**는 확인됐다. 그러나 실제 AI 요청용 Compose는 새 built-in 규칙을 쓰므로 **새 필수 지침이 AI에 누락된다고 주장할 수 없다**. 실제 AI 호출은 금지되어 실행하지 않았으며, 요청 문자열을 만드는 실제 Compose 함수까지 확인한 것이다. 안내 문자열 반환도 확인했지만 실제 UI에 표시됐는지는 검사하지 않았다.

현재 원문 보존 정책은 사용자 변경 파일의 덮어쓰기를 피한다. 기존 공식 버전만 식별해 갱신하려면 알려진 공식 문안/hash와 임의 변경 파일을 구분하고 원본 보존 정책을 합의해야 한다. 소유자 동의 없이 자동 덮어쓰기를 추가하지 않는다. 대안은 이전 공식 사본임을 구분하는 정확한 안내나 원본을 보존한 별도 버전 관리 사본이다. 이 차이를 실행 차단·권한 문제로 과장하지 않으며, 필요성·마감 범위는 Root와 Code가 결정한다.

## 증거와 제한

- `source-inputs.json`: HEAD commit/blob ID와 원본 소스/추출 클래스 SHA256.
- `choices-observations.json`: 8개 실제 getter 입력과 반환 값.
- `old-official-upgrade.json`: 파일·built-in·실제 Compose 문맥과 SHA/시각/notice.
- `fresh-new-rules.json`: 신규 workspace에 1~5번 실제 생성.
- `customized-preservation-control.json`: 변경 사본 원문 보존·built-in 요청 문맥 사용.
- `result.json`, `run.log`, 각 compile.log: 원본 실행 결과와 범위.
- `MarkdownReport/CodexOrig_TASK-068_QuestionsRules_Evidence.json`: 위 증거의 SHA와 관측 요약.

제품 소스는 Code가 계속 작업 중인 snapshot이다. 이 자료는 기록한 source SHA에 해당하며 최종 동결 후보나 제품 전체 합격/불합격으로 대신하지 않는다. 기존 test·제품·공유 bin/obj·GUI·원격 AI는 변경/실행하지 않았다. Root가 Code 소유권을 존중해 수정 필요성을 판단하고 실제 제품 후보 검증·독립 감사·4SKU 컴파일·최신 Release 제작·배포·익명 다운로드 해시 확인까지 통합한다.
