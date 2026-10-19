# TASK-058 / CodexOrig2 / Role 01:40 독립 감사 기준과 실행 계획

작성: 2026-10-20 KST  
범위: [Role.md 01:40 최신 요청](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/Role.md)의 #1–8·#11–14 및 image56–65. 준비 상태이며 TASK054의 새 고정 후보를 수령한 뒤 실행한다. Role에는 #9·#10이 없다. #13은 파일 생성이고 Claude 검증은 #14다. **사용자의 후속 지시대로 Claude는 로그인 없이 검사한다.**

## 이번 계획의 판정 원칙

화면은 WPF 스타일이나 XAML에 공통 리소스가 존재하는지만으로 통과시키지 않는다. 고정 후보의 실제 DLL을 사용해 **실제 픽셀 렌더 결과, 크기·간격, 키 입력, 상태 유지, 이벤트 순서**를 확인한다. 실제 IDE 창을 띄우거나 네이티브 포인터를 움직이는 검증은 Root가 예약을 연 뒤에만 수행한다. 무창 WPF 결과, Root의 실제 IDE 결과, Claude CLI의 비인증 실행·합성 프로토콜 결과를 각각 표시하고 서로 대신 쓰지 않는다.

먼저 후보 경로·버전·입력 목록·4개 대상 Release DLL의 SHA-256을 기록한다. 화면/상태 fixture는 해당 고정 DLL만 로드한다. 테스트 출력은 빈 개인 폴더를 쓰며 설정·작업공간 경로와 CLI 발견을 분리한다. 개인 정보 없는 합성 제목·대화·프로젝트로 캡처한다. 최종 자료에는 각 항목의 시각 결과, 측정값, PASS/FAIL/NOT RUN, 캡처·JSON의 SHA-256을 남긴다.

화면 캡처는 요청 image의 목적을 재현하되 그 픽셀 색을 기준값으로 복사하지 않는다. 기능별 다른 크기·역할의 컨트롤은 의미상 같은 그룹끼리 비교한다. 기본/주 동작, 보조, 위험, icon-only, checkbox/toggle은 그룹을 나누고 각 그룹 안에서 크기·색·상태·여백을 비교한다. 표준 텍스트 대비 목표는 일반 크기 4.5:1, 큰 글씨 3:1이며 OS High Contrast는 실제 OS 색과 모드로 따로 확인한다.

표의 수치 기준은 감사자의 사전 제안이다. Research057의 모션 근거와 Root의 실행 환경을 대조해 **고정 후보를 열기 전에** 최종 기준·측정 환경을 기록한다. 후보 실패 뒤 임계값을 바꾸어 PASS로 만들지 않는다. 환경 탓인 측정은 원본 시간·영상과 조건을 보존하고 같은 후보에서 재측정 사유를 남긴다.

## 요구별 합격 기준

| ID / Role | 보이는 실패 조건 | 독립 합격 기준 | 실행 증거 |
|---|---|---|---|
| V01 / #1 image56–57 팝업과 IDE 구분 | 팝업 경계·제목·본문이 주변 Visual Studio와 섞여 계층이 불분명하다. | 고정 후보의 실제 IDE에서 창 네 변과 caption이 host 배경과 눈에 띄게 분리되고, 내용은 선택된 팔레트로 읽힌다. Dialog가 owner 위에 있으며, 작업영역 밖에 잘리지 않고 닫기/Esc/Alt+F4/resize가 유지된다. VS light/dark 및 Kakao/Dracula를 지원하는 구성에서는 각각 확인한다. 실제 IDE screenshot 1장 이상/구성, 기계 측정한 caption/본문/인접 host의 RGB·텍스트 대비 기록. 이미지를 겹쳐 색상만 강제로 통일하거나 뒤쪽 IDE를 완전히 가리지 않는지 함께 본다. | 실제 창 캡처·caption/본문/host sample·owner/종료 입력 결과·SKU SHA. 캡처 없는 무창 render만 있으면 PARTIAL. |
| V02 / #2 image58 Markdown UI | “완료 전환” checkbox가 주변 toolbar와 다른 흰 기본 버튼처럼 보이거나 Apply/Close·맨 아래 입력 영역이 부풀어 보인다. | 편집 명령, 선택/toggle, checkbox, Apply, Close를 의미별로 분류한다. 같은 역할끼리는 baseline 높이·padding·border·색·hover/focus/disabled가 일관되고 기본 WPF 흰색이 튀어나오지 않는다. Editor/status/input/footer는 겹치지 않고 화면 높이에 맞춰 줄어든다. 1024×768 및 1440×900의 실제 렌더를 비교한다. 고정 footer 수직 여백은 버튼 행 높이+24 DIP 이내를 목표로 하되 두 줄 이상 wrap 시 자연스럽게 늘어난다. | 두 크기 캡처, 버튼 그룹별 bounding box·brush·상태 matrix, 요소 겹침 검사. |
| V03 / #3 image59 전체 버튼 일관성 | 설정 Save/Close, 본문 동작, Markdown 도구 버튼의 모양이 의도 없이 다르다. | 앱 전체에서 같은 의미의 동작은 같은 style-token·높이·padding·radius·hover/focus/disabled 상태를 렌더한다. 역할별 차이는 왜 다른지 설명할 수 있어야 하며 button/ToggleButton/CheckBox의 의미 차이를 없애지는 않는다. UI screenshot의 대표 표본과 컨트롤 트리의 수치 비교를 함께 통과한다. | 대표 화면 6개 이상, 역할별 분류·크기/색 차이표·접근성 이름·키보드 focus screenshot. |
| V04 / #4 image60 안내 footer | 하단 “다시 보지 않기”와 버튼이 주황색 띠에 겹치거나, 마지막 카드 뒤에 목적 없는 큰 빈 띠가 남는다. | 안내 본문은 스크롤되고 하단 checkbox/footer/actions는 고정 영역에 완전히 표시된다. 여러 DPI/작은 작업영역에서 교차·잘림·가림이 없고 footer 여백만 내용에 따라 쓴다. 본문 높이가 늘어날 때 footer를 body 안으로 밀지 않는다. | 1024×768/1440×900 실제 Window screenshot, checkbox/actions/content layout rectangles 및 교차 면적0. |
| V05 / #5 image61 provider 탭 간격 | Claude와 Codex 사이가 캡처처럼 과도하게 벌어지거나 포커스 표시가 없어진다. | 실제 렌더에서 시각 공백은 8–16 DIP 범위, 키보드 hit/focus target은 각 최소32×32 DIP로 둔다. Claude↔Codex 클릭 및 좌우 화살표가 모두 맞는 탭을 선택하고 선택색·focus cue를 보인다. 100%/125% 환경에서 다시 잰다. | 캡처와 header child bounding box 거리, click·Left/Right 결과, DPI/SKU 기록. |
| V06 / #6 image62 탭 전환 지연·초기화 | 탭을 누르면 작업면이 비거나 멈추고 작성 중 입력·대화 스크롤·실행상태가 초기화된다. | 50회 양방향 빠른 전환의 click→대상 tab selected를 기록하고 click→composer 사용 가능 지연은 워밍업 후 p95≤200 ms, p99≤350 ms를 제안 기준으로 둔다. 각 10회 캡처에서 임의 loading flash/빈 본문이 보이지 않으며 입력·draft·대화/스크롤·실행 indicator가 약정한 provider state로 복원된다. 성능은 로컬 합성 응답 조건에서 재고 CLI 네트워크 시간은 제외한다. | Stopwatch CSV·탭 전후 state snapshot·10회 캡처. 플랫폼/하드웨어·창 크기·DPI 명시. |
| V07 / #7 image63 실행 중 연속 입력/FIFO | 실행 중 입력창이 막혀 다음 요청을 타이핑하지 못하거나, 요청이 누락·중복·역순·다른 provider 대화로 이동한다. | fake CLI A를 2초 block한 동안 B/C를 입력·전송할 수 있다. UI는 현재/대기 상태 및 개수를 명확히 하고 editor 내용을 덮어쓰지 않는다. unblock 뒤 실제 실행 인자는 A→B→C, 각1회, queue별 동시실행 최대1이다. 탭 전환 중 각 provider 입력·큐·응답은 소속 대화에만 붙는다. 실패/취소/앱 닫기 때 대기 항목 처리 결과도 알 수 있다. 전역 또는 provider별 queue 계약을 코드/기록에서 하나로 정의하고 동일 순서를 검증한다. | 결정적 fake CLI의 timestamped argv/stdin/start-end log, 50개 순서 시험, typing/input snapshot, provider switch/pasteback 결과. 실계정 미사용. |
| V08 / #8 image64 완료 알림 | Visual Studio 로고와 일반 문구만 보여 어떤 AI가 무엇을 했는지 알 수 없다. | 완료 주체의 Claude/Codex 브랜드가 서로 바뀌지 않고 짧은 작업 요약을 보여 준다. 긴 요약은 잘라내고 화면읽기용 이름은 유지한다. 원문 프롬프트·전체 응답·경로·토큰·인증 정보는 알림에 넣지 않는다. 성공/실패/취소/연속 완료에서 알림 수·상태가 중복되거나 다른 작업과 뒤섞이지 않는다. | 격리 fake response 2 provider×3 outcome×짧음/긴 요약, Windows notification event count·텍스트·accessibility name. 실 OS 화면은 Root 예약 시만. |
| V09 / #11 image65 fan 메뉴 간격·색상 | fan wedge가 가장자리 탭/설정/안내 hover와 겹치거나 선택색이 바탕에 묻힌다. | 각 fan label이 host와 인접 hover target 위에 실제로 표시되며 채팅 본문을 가리지 않는다. anchor 및 neighbor와의 거리를 재고 실제 pointer path 3회에서 wedge로 진입·선택 가능하다. 항목 글자≥4.5:1, 선택 경계/포커스 cue≥3:1 대비를 통과한다. 배치 폭의 최소/중간/최대 창에서 clip/intersection이 없다. | 실제IDE 캡처, 화면좌표 box·intersection·contrast 계산, pointer path 기록(또는 Root의 예약된 입력 run). |
| V10 / #12 image65 hover 등장 지연·모션 | 마우스가 이동 중일 때 메뉴/툴팁이 너무 빨리 나타나 목표를 가린다. 진입·퇴장이 튀거나 닫히지 않고 뚝뚝 끊긴다. | Research057 제안과 fixed implementation의 정확한 delay/duration을 먼저 대조한다. 초기 감사 target은 hover open delay 150–200 ms, leave grace≥250 ms, 진입·퇴장 각180–240 ms로 제안한다. 20 ms 이하 간격 pointer sampling으로 anchor→fan item 경로를 3방향씩 반복하고 중간에 닫히지 않는다. 빠르게 100 ms hover는 열리지 않고, 실제 항목에 머물면 한 번 열려 선택까지 남는다. 60 fps 화면기록의 프레임별 opacity/transform은 단조롭게 진행되고 되튐·끝점 점프·텍스트 번짐이 없다. 고정 시험 PC의 idle 기준에서 연속 두 프레임 사이 50 ms 초과 정지를 실패 후보로 검토하고 GPU/IDE 부하를 기록한다. OS reduced-motion/high-contrast에선 규정된 단순/정적 대체로 동작한다. timer·handler는 owner unload/close/dispose 뒤 남지 않는다. | motion elapsed/frame timestamp log·pointer trace/video·reduced-motion run·post-close timer/handler count. 실제 입력은 예약된 IDE 환경에서만. |
| V11 / #13 사용자 요청 문서·특정 파일 생성 | 답변은 “저장했다”고 하지만 파일이 없거나, 다른 경로/형식으로 만들고 기존 파일을 조용히 덮거나, 모델 답변 속 경로가 사용자 지시로 승격된다. | 파일 생성 결정과 목적 경로는 사용자 명시 요청 또는 제품에 명시된 기본 출력 위치에서만 온다. 모델 출력·첨부·파일 내용 속 경로는 경로 권한이 아니다. 명시한 `.md`/`.txt`/`.html`/`.docx`/코드 확장자마다 **디스크에 실제 파일이 생기고** 열어 읽은 내용·형식·인코딩·파일명/위치·반환 링크가 요청과 맞는다. DOCX는 유효한 문서 패키지이며 본문을 렌더/추출해 대조한다. HTML은 의도한 내용으로 열리며 외부 자원·스크립트가 예기치 않게 실행되지 않는다. 코드 파일은 요청한 언어/확장자와 내용이 일치하고 생성만으로 실행되지 않는다. 기존 파일은 명시적 교체 요청이 없으면 보존되고, 허용된 교체는 실패/취소에도 이전 내용이 온전하다. 상대경로 정규화, `..`, 절대/UNC, 장치명, ADS, reparse/symlink 및 대소문자 충돌은 실제 사용자 지정 경로의 허용 범위와 격리 루트를 기준으로 판정한다. 유효하지 않은 요청이나 출력 실패는 성공 메시지와 부분 파일을 남기지 않는다. | 개인 임시 workspace에서 형식5종의 파일·bytes·SHA·본문/미리보기·반환링크 대조. 기존 파일/명시적 덮어쓰기/실패 복구와 공격성 경로 10종은 전후 트리 SHA/경로로 검증. 실제 사용자 문서는 건드리지 않음. |
| V12 / #14 Claude 로그인 없는 동작 확인 | Codex 경로만 확인하고 Claude를 같은 것으로 가정하거나, 인증되지 않은 CLI 시작 실패를 실제 AI 성공으로 보고한다. | 설치된 Claude CLI의 경로·실행 가능성·버전과 **인증 없는 초기화 경계**를 확인한다. 로그인 화면/계정 연결이 필요하다는 상태는 숨기거나 자동 진행하지 않고 정확히 기록한다. 별도 fake Claude CLI로 요청 인수 전송·응답 조각 parsing·UI 표시·nonzero exit/stderr·cancel·timeout을 결정적으로 확인한다. 이 fixture의 성공을 실계정 AI 응답 성공으로 표현하지 않는다. CLI 프로세스가 로그인/브라우저를 열거나 자격정보를 요구하면 중지하고 상태를 기록한다. | 실제 CLI path/version/startup·로그인필요 여부·exit summary와 합성 프로토콜 원문, 고정 후보 DLL SHA. 실제 계정 로그인·실제 AI 답변은 `OUT OF SCOPE — USER NO LOGIN`. |

### V11 파일 생성의 신뢰 경계

사용자의 명시적 경로는 작업 권한이다. AI 응답 본문·첨부·저장된 대화·프로젝트 파일의 텍스트는 파일 내용의 자료일 수 있지만 경로 선택이나 덮어쓰기 권한이 아니다. 경로가 없는 사용자 요청은 제품이 공개한 출력 폴더 계약에만 따른다. 사용자가 요청한 정당한 절대 경로를 무조건 거절하는 규칙으로 바꾸지 않으며, 실제 요청이 허용한 목적지와 선택된 파일명을 기준으로 최종 경로를 검증한다. 상대경로는 기준 폴더에서 canonicalize하고, reparse/symlink를 따라간 최종 대상도 같은 허용 범위를 만족해야 한다.

| 사례 | 검증할 결과 |
|---|---|
| V11-A 형식5종 | 사용자 요청한 Markdown, 평문, HTML, DOCX, 코드 파일을 개인 임시 폴더에 하나씩 생성한다. 파일 magic/구조, 확장자·내용·UTF-8 또는 DOCX package, 경로, 반환 링크를 실제 재열기로 대조한다. DOCX는 본문 추출과 렌더 확인, HTML은 의도하지 않은 외부 참조/실행 확인. |
| V11-B 경로 권한 | 사용자 요청에는 경로가 없지만 모델 출력 또는 문서 본문에 `save to ...`가 있을 때 실제 파일 생성0. 사용자 지정 안전한 절대·상대경로는 정확히 한 경로에만 생성. |
| V11-C 경로 변형 | `..`, 혼합 구분자, UNC, 장치명, ADS, 확장자 대소문자·이중확장자, symlink/reparse 탈출을 시험한다. 사용자 요청 범위 밖에 파일 생성0, 유효한 사용자 경로는 오거부하지 않음. |
| V11-D 기존 파일 | 교체 요청 없는 기존 파일은 byte/hash 불변. 명시적으로 교체한 파일은 성공 후 완전한 새 파일 1개이며, 중간 실패·취소·공간 부족에는 기존 내용을 보존한다. |
| V11-E 동시성과 오류 | 동일 경로 두 요청 경합, 권한 거절, 잘못된 형식/확장자, DOCX 생성 예외를 시험한다. 성공 메시지는 실제 파일·검증 SHA가 있을 때만, 실패 시 부분 파일과 다른 위치의 쓰기는 0. |

이 검사는 사용자 PC의 기존 문서나 실제 계정 폴더에 시험 파일을 쓰지 않는다. 보조061은 기능을 구현하지만 독립 감사자는 별도 임시 폴더의 **전후 파일 트리/해시**로 결과를 판정한다. 요구를 막연히 거절하도록 강요하는 기준이 아니라, 명시된 생성 요청이 정확하고 안전하게 수행되는지 확인하는 기준이다.

## 담당·검사 책임

| 범위 | 구현/근거 소유 | TASK058 독립 확인 |
|---|---|---|
| 팝업·Markdown·버튼·탭 간격 #1–5 | Designer056 | 실제 후보 화면과 컨트롤 상태/간격 |
| 탭 반응·FIFO #6–7 | Backend055 | 합성 지연·순서 로그와 실제 UI 편집 반응 |
| 완료 알림·fan #8/#11/#12 | Root054; #12 연구 권고 Research057 | provider 표시·요약과 물리 hover/모션·수명 |
| 사용자 지정 파일 생성 #13 | 보조061 | 사용자 경로/모델 자료 신뢰 경계와 실제5형식 출력 |
| Claude 비인증 동작 #14 | 보조060 및 Root054 | 실제 CLI 실행/버전/비인증 초기화와 fake protocol; 실계정·실답변 제외 |

## 실행 단계와 증거 상태

1. **후보 수령** — Root가 약90% 상태에서 새 TASK054 제품 후보·선언된 입력 목록과 이전 고정본 대비 변경·SKU별 Release/DLL/VSIX·SHA·빌드 상태와 GUI 예약 상황을 보낸다. 해시를 다시 읽고 실행 폴더에 복사해 고정한다. 후보가 확정되지 않으면 fixture의 실행상태는 `NOT RUN`으로 유지한다.
2. **무창 실제 DLL 검사** — WPF STA private process에서 V02–V05, V07 fake CLI, V08 synthetic notification, V09 geometry, V10 time/state, V11 개인 temp 파일경계 및 V12 합성 Claude 프로토콜을 우선한다. 실제 계정, 공개 API, production DB, host registry/settings는 사용하지 않는다. 후보에 맞춘 runner가 필요하면 개인 `artifacts/audit-codexorig2-task058/` 안에만 둔다.
3. **GUI 예약 후 실제 IDE** — V01/V02/V04/V05/V06/V09/V10 화면·포인터 조건은 Root가 slot을 열고 SKU·theme·DPI·작업영역을 지정한 뒤 검사한다. 시작/끝 시각을 공유 WORK_STATUS에 남기고, 마지막 창/타이머/키보드 hook을 정리한 뒤 즉시 slot을 해제한다.
4. **로그인 없는 Claude CLI** — 보조060/Root054가 설치된 executable 실행·버전·비인증 초기화를 검증한다. 로그인/브라우저를 진행하거나 기존 계정·토큰을 읽는 시험은 하지 않는다. 인증 필요 상태가 나오면 정확히 기록한다. 감사자는 해당 CLI 경로·실행 원문·고정 후보 연결과 합성 프로토콜 결과를 별도로 대조한다. 실제 AI 답변은 사용자 지시로 제외되어 `NOT RUN`이 아니라 `OUT OF SCOPE`다.
5. **종료/보고** — 대조표의 각 ID에 PASS/FAIL/PARTIAL/NOT RUN을 넣고 Fail은 화면/요구/조작 순서/SKU DLL SHA로 최소 재현한다. 실패 자체, fixture 오류, 미실행을 각각 보존한다. 확정 제품 지적은 Root로 돌려보내고 수정 후 새 SHA에서 그 항목과 연관 회귀만 재검사한다. 최종통합·네 SKU 빌드·최신 Release·게시/익명 다운로드 해시는 Root054 소유다.

### 감사자에게 후보를 인계할 때 필요한 최소 항목

`candidateRoot`, version, fixedAt, `sourceHashManifest`, `releaseByTarget`(target/configuration/dllPath/dllSha/vsixPath/vsixSha/build warnings/errors), changed files vs prior freeze, `guiReservation`(owner/start/end), `testRunOwner`, Research057 motion recommendation link, 보조061 파일생성 출력경로/덮어쓰기 계약과 V11 개인 fixture 연결, 보조060/Root054 Claude **비인증 CLI** 실행·버전·초기화 owner/status. 이 목록이 일부 빠져도 독립 source/read-only 검토는 시작할 수 있지만, 빠진 SKU·화면·파일 생성·비인증 Claude 행은 PASS로 채우지 않는다.

## 개인 fixture 출력 형식

[TASK058 evidence skeleton](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task058/evidence-skeleton.json)은 위 12개 ID와 비밀 없는 결과 필드만 둔다. 아직 제품 코드를 로드·빌드하거나 테스트를 실행하지 않았고 모든 결과는 `NOT RUN`이다. Screenshot은 요구 image를 fixture/test result로 재사용하지 않는다.

이 계획은 Delegate TASK058의 요청 기준이다. TASK053 C4/C5/C6 감사·배포 기록을 수정하거나 재해석하지 않는다. 이번 준비 단계는 개인 기준서·빈 evidence schema·Role image contact sheet만 만들었으며 제품, 공유 빌드, GUI, 계정, 설치/배포에 손대지 않았다.
