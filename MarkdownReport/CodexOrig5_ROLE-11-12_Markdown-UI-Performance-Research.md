# Markdown·UI 경험 및 도구 창 첫 열기 지연 조사

작성: CodexOrig5 · 기준일: 2026-10-20 · 대상: Role.md #11, #12 및 후속 UI/모션 요청

## 조사 범위와 결론

제품 UI가 아닌 연구 산출물이다. source, build, VSIX, release, Git index/commit에는 손대지 않았다. 통합 담당의 이미지 및 수명 코드 검토에 따라 `image-75.png`는 새 도구 창 생성이 아니라 이미 만들어진 Visual Studio 우측 자동 숨김 도구 창(`Claude · Codex`)의 warm 재활성화로 본다. 최초 생성과 숨겨진 창의 재활성화는 다른 경로이므로 구분하되, 이 캡처의 우선 진단 지점은 재표시 때 발생하는 `Loaded` 작업이다.

새 창을 처음 생성하는 별도 코드 경로의 의심 지점은 `OpenAssistantCommand.Execute`의 UI 스레드 `FindToolWindow(..., true)` → `frame.Show()` → `AssistantControl` 생성자에서 `InitializeChat()` → `InitializeExperience()`이다. 이 초기화는 설정/필수 instruction 처리, Claude·Codex 각각의 `LoadOpen()` 호출, 기존 기록의 Markdown 읽기와 정렬, 없을 때 대화 파일 생성, UI 바인딩 및 일부 런타임 초기화를 함께 수행한다. `LoadOpen()`은 제공자당 디렉터리에서 최대 500개 Markdown 파일을 순회한다. 대화 화면은 최근 20개 표시 및 이전 페이지 비동기 로드를 이미 사용하므로, 무제한 메시지 렌더링을 원인으로 단정하거나 기존 최적화를 중복 구현하지 않는다. 실제 파일 수·크기 및 UI에서 재현된 시간은 이번 조사에서 측정하지 않았다.

따라서 screenshot에서 우선 확인할 후보는 warm 재활성화 때 다시 수행되는 렌더/테마 이벤트 비용이다. 전체 시작 및 기록 복원의 비동기 재설계는 별도 cold-open 증거가 모일 때까지 백로그로 둔다. VS2017 ToolWindow API, WPF Dispatcher 접근, 대화 순서/읽지 않음/저장 무결성, 취소·재활성화 경계를 확인하지 않은 스레드 이동은 금지한다.

## 시장 비교와 현재 Markdown 기능

현재 `MarkdownRendering`은 제한을 둔 자체 WPF FlowDocument 렌더러다. 제목, 목록, 인용, fenced code 및 언어별 색상, 표, 구분선, 인라인 서식, 링크/참조 링크, bare HTTP(S), 이미지, 앵커 및 일부 비실행 HTML을 지원한다. 입력/노드/깊이/이미지 크기와 정규식 실행 시간 제한이 있고 HTML을 브라우저에서 실행하지 않는다. `MarkdownEditor`는 AvalonEdit 기반 원문 편집, 찾기, 확대, 저장/열기, 포맷 삽입, 체크박스 삽입/토글, 분할/원문/미리보기 모드를 제공한다. 파일 읽기와 렌더링 크기에 제한도 있다. 따라서 우선순위는 “기능 수 늘리기”보다 호환 가능한 문법 명세·오류 복구·편집 UX 검증이다.

| 비교 기준 | 이 프로젝트 | 대표 프로그램/표준에서 기대되는 점 | 권장 판단 |
|---|---|---|---|
| VS Code Markdown | IDE 내부 원문과 미리보기 전환/나란히 보기, GFM 호환을 문서화하고 확장으로 렌더링 스타일을 보완 | 익숙한 단축키와 편집기 맥락, 미리보기 확장성 | 단축키/미리보기 상태 기억, 표준 예제의 렌더 일치부터 확인 |
| GitHub Flavored Markdown | 일부 서식이 겹치나 완전한 GFM 준수는 확인되지 않음 | 체크리스트, 취소선, 표, 자동 링크 등을 명시적 확장으로 정의 | CommonMark/GFM 핵심 호환 테스트 추가. GFM task list 미리보기는 코드에서 확인하지 못했으므로 우선 검증 |
| Obsidian Markdown | 표준 링크·목록·인용 등은 지원, 지식 관리 전용 문법(위키 링크, embed, callout, 속성)은 제품 범위 밖 | 문서 간 연결과 컨텍스트 임베드가 핵심 | 일반 Markdown 호환을 우선. 위키 링크/임베드/콜아웃은 사용자 수요·안전 비용 확인 전 보류 |
| VS 확장 UI 기준 | 별도 도구 창 및 테마 연동이 제품 장점 | VS의 익숙한 tool-window, 설정·키보드·테마 패턴과의 일관성 | 주 도구 모음/상태 피드백/설정 위치를 IDE 패턴으로 통일 |

### 기능 격차 및 우선순위

1. **P0 — 문법 호환 테스트**: CommonMark/GFM 예제를 fixture로 정하고 지원/미지원 항목을 명시한다. 중첩 목록, URL 및 괄호/공백 링크, 각주, 자동 링크, 표 정렬, 이미지 크기/오류, 체크박스 렌더를 조사한다. 현재 구현에 없는 각주·GFM 자동 링크 등은 추정으로 확정하지 않고 회귀 테스트로 증명한다.
2. **P1 — 편집 기본기**: 문서 목차(outline), 제목/링크/이미지로 빠른 이동, 표 편집 삽입, 편집/미리보기 동기 스크롤, 최근 폴더/파일, 저장되지 않은 변경 상태, 키보드 접근, 검색 결과 강조를 검토한다. 저장·원문은 Markdown 파일 그대로 유지한다.
3. **P1 — 긴 문서 안정성**: 디바운스 이후 최신 문서만 렌더, 이전 렌더 취소/무시, 이미지 실패 placeholder, 표 가로 overflow, 긴 코드의 가로 스크롤, 큰 문서 한도 안내를 사용성 테스트한다. 기존 debounce와 제한을 존중한다.
4. **P2 — 표현 확장**: GFM task checkbox 상호작용, 알림형 callout, Mermaid/수식, HTML 확장, 위키 링크/임베드, PDF 내보내기는 보안·의존성·호환성 비용에 비해 수요가 입증된 뒤 분리 검토한다. 특히 외부 실행 엔진/원격 이미지 자동 호출은 기본 기능으로 넣지 않는다.

**차별화 기회**: 별도의 Markdown 노트 앱이 되려 하기보다, Visual Studio 안에서 “대화에서 생성된 코드·파일 제안·원문 Markdown 기록·승인된 IDE 편집”을 연결하는 개발 문맥 기반 기록 도구로 차별화한다. 로컬 우선, 인터넷 없는 환경, 안전한 링크/HTML 처리, 대화-프로젝트 경로 문맥, CLI 연결 상태 및 프라이버시 투명성을 제품 약속으로 내세운다. 경쟁 앱의 그래프/확장 생태계 전체를 복제하면 범위와 유지비만 증가한다.

## UI/브랜드 사이트와 디자인 시스템 비교

Visual Studio UX 지침은 IDE 패턴에 맞는 일관성, 접근성 및 키보드 흐름을 우선하고, VS Code 확장 UX는 Command Palette/Quick Pick/상태 표시/문맥 내 진행/온보딩 walkthrough 등 익숙한 탐색 요소를 제시한다. Fluent의 모션 지침은 목적 있는 짧고 자연스러운 움직임, 계층감과 easing, 시스템의 motion 제한 존중을 강조한다. Windows 접근성 지침은 텍스트 대비, 고대비, DPI 배율, 색상만으로 의미 전달 금지를 요구한다.

프로젝트에서 볼 개선 방향은 넓은 화면/좁은 tool-window에서도 동작하는 반응형 밀도, primary/secondary action 구분, 단어와 아이콘을 함께 쓰는 명확성, 대화/설정/기록의 탐색 분리, 선택/hover/focus/disabled/loading/error 상태 통일, 고대비/키보드/스크린리더 점검, 도움말/첫 실행 경로 정리다. 붉은 강조 박스가 가리키는 auto-hide 탭 자체는 Visual Studio host UI라 제품이 재디자인하지 않는다. 도구 창 내부의 제목·빈 상태·로딩 표시는 host 경험과 조화를 맞춰 개선한다.

### 이벤트 애니메이션 속도 판단

현재 `AssistantMotionTokens`의 주요 진입 모션은 220ms, 미세 상호작용은 150ms다. 토스트가 3초 뒤 시작하는 fade는 800ms이며, fan 메뉴는 단계 지연 220/280ms, thinking sweep는 1600ms 반복이다. 이 값은 애니메이션마다 목적이 달라 단순히 전체를 늦추면 입력 반응이나 작업 피드백이 답답해질 수 있다. 체감 의견을 반영해 **짧은 진입/상태 변화만** 더 여유롭게 조정하는 실험을 권한다: primary entry 약 280ms 전후, micro 약 180ms 전후를 비교 후보로 삼고, stagger/지연은 중복 합산을 피한다. 이는 디자인 가설이며 실제 UI A/B 결과가 아니다.

CodexOrig4 개발 handoff: 중앙 token으로 주요 진입과 미세 반응을 조금 완만하게 조정하는 비교를 요청한다. `PrimaryEntry`는 `ToastHost` 진입에도 공유되므로 함께 바뀐다. 토스트의 자동 사라짐 시간과 별도 fade-out, 반복 thinking sweep는 이 변경과 구분한다. ToastHost 진입을 제외해야 한다면 별도 token/callsite가 필요한지 Root가 다음 후보 범위에서 결정한다. OS의 ClientAreaAnimation/Reduced Motion 설정을 계속 존중하고, 입력이 늦게 느껴지거나 움직임이 산만해지면 원복한다. focus/상태 전달은 모션 없이도 이해되어야 한다.

## 자동 숨김 창 재활성화 및 최소 개발 후보

CodexOrig의 image-75 수명 확인과 최신 소스 검토로, 이 캡처는 이미 만들어진 auto-hide 창의 재활성화 경로다. 따라서 첫 구현 후보는 대규모 startup/storage 재설계가 아니라, 같은 입력에 반복되는 무거운 재표시 작업을 안전하게 생략하는 두 지점이다.

1. **Markdown 미리보기 재사용**: `MarkdownPreview.PreviewLoaded`가 `Refresh()`를 호출하고, `Refresh()`가 매번 새 `MarkdownRendering`을 만들어 전체 Markdown을 `FlowDocument`로 렌더한 뒤 기존 renderer를 dispose한다. 동일한 Markdown·asset base directory·글꼴·유효 팔레트 값으로 다시 `Loaded`되는 경우에 한해 기존 문서와 GIF frame을 보존할 수 있는지 검토한다. `SetDocument`/DependencyProperty 변경, 명시적 자산 재검사 `Refresh()`, 실제 글꼴/팔레트/asset directory 변경, Dispose는 기존 의미를 유지한다. 숨김/재열기만으로 local image/GIF 자원을 놓치지 않는지 함께 검증한다.
2. **테마 재표시 중복 작업 확인**: `AssistantThemeLoaded`는 event subscription을 재연결하고 `RefreshAssistantTheme()`을 호출한다. 현재 함수에는 effective mode/preset/high contrast/host color/resource 조건이 같으면 빠져나오는 equality guard가 있다. 그 guard가 재활성화에도 동작하는지 확인하고, 동일한 결과 팔레트의 재할당·불필요한 registry fallback lookup이 남아 있으면 최소 수정으로 생략한다. 명시적 light/dark mode에서는 Windows/VS mode fallback 조회가 일어나지 않도록 보존한다. 실제 시스템/VS 테마 변경 시에는 다시 적용되어야 한다.
3. 첫 생성 로딩의 계측 및 `AsyncPackage`/metadata-only history 같은 아키텍처 변경은 screenshot 경로와 분리된 후속 backlog다. 실제 cold-open 지연이 확인되면 별도 proposal을 만든다. 로그에는 단계 시간과 크기 범주만 남기고 prompt/body/경로를 넣지 않는다.
4. 회귀 점검은 같은 pane 숨김/재활성화 반복, Preview text/asset/font/theme 각각 변경, animated image 시작/중지 및 dispose, invalidated renderer, VS/system theme 변경, high contrast, 대화 draft/history 보존을 포함한다. 코드 단위 테스트만으로 실제 사용자 체감 개선을 주장하지 않는다.

### 100가지 개선 아이디어 (범위 목록; 모두 구현하라는 뜻이 아님)

분류: **안전 실험**은 계측/격리 후 검증하기 쉬움, **설계 검토**는 정확성/호스트 제약 검토 우선, **수요 확인**은 사용 데이터를 보기 전 보류. 아이디어 간 중복을 줄이기 위해 서로 다른 제어 지점을 한 줄씩 정의했다.

#### A. 계측 및 진단

| # | 아이디어 | 분류 |
|---:|---|---|
| 1 | 프로세스 최초 도구 창 생성시간과 warm 재활성화 시간을 별도 기록한다. | 안전 실험 |
| 2 | FindToolWindow, 생성자, InitializeChat, frame.Show 단계별 stopwatch를 둔다. | 안전 실험 |
| 3 | WPF first-render/첫 입력 가능 시점을 별도 정의해 단순 창 표시와 구별한다. | 안전 실험 |
| 4 | Claude/Codex LoadOpen별 파일 수와 총 byte 구간을 기록한다. | 안전 실험 |
| 5 | 최초 시작과 IDE 세션 중 재열기의 분포를 수집한다. | 안전 실험 |
| 6 | cold/warm, 비어 있음/기록 많음 fixture를 분리한다. | 안전 실험 |
| 7 | UI thread 작업별 elapsed/호출 횟수 진단 로그를 추가한다. | 안전 실험 |
| 8 | 화면이 멈춘 것처럼 보이는 동안 Dispatcher responsiveness pulse를 측정한다. | 설계 검토 |
| 9 | 기록 파일 크기별 렌더링 비용을 테스트 전용 fixture에서 측정한다. | 안전 실험 |
| 10 | 한 번의 open에서 가장 오래 걸린 초기화 단계를 익명화해 노출한다. | 설계 검토 |

#### B. Toolwindow 생성 및 수명

| # | 아이디어 | 분류 |
|---:|---|---|
| 11 | 최초 활성화 시 외곽 도구 창을 먼저 표시한다. | 설계 검토 |
| 12 | 재활성화가 같은 pane/control을 재사용하는지 확인한다. | 안전 실험 |
| 13 | auto-hide와 실제 close/Dispose의 수명 처리를 구분한다. | 설계 검토 |
| 14 | 열기 명령의 동기 생성 전에 가능한 경량 package initialization을 조사한다. | 설계 검토 |
| 15 | 무거운 컨트롤의 생성 시점을 사용자 첫 진입까지 늦춘다. | 설계 검토 |
| 16 | 안정적 재사용이 보장될 때만 마지막 표시 상태를 보존한다. | 설계 검토 |
| 17 | 동일 시간대 중복 open 요청을 단일 생성 작업으로 합친다. | 안전 실험 |
| 18 | 닫기와 숨기기에서 각각 필요한 resource dispose 범위를 정의한다. | 안전 실험 |
| 19 | IDE 종료 과정의 미완료 초기화 취소를 구현한다. | 설계 검토 |
| 20 | 도구 창이 숨겨져 있을 때 불필요한 갱신을 잠시 멈춘다. | 안전 실험 |

#### C. 사용자에게 보이는 첫 응답

| # | 아이디어 | 분류 |
|---:|---|---|
| 21 | 본문 로딩 중 제목/탐색만 먼저 보이는 skeleton을 둔다. | 안전 실험 |
| 22 | 최초 frame 표시 직후 진입 가능한 입력 영역부터 활성화한다. | 설계 검토 |
| 23 | background restore 동안 무해한 진행 표시를 보여준다. | 안전 실험 |
| 24 | 상태 표시를 “대화 불러오는 중”처럼 작업에 맞춰 구체화한다. | 안전 실험 |
| 25 | 느린 저장소에서 재시도와 취소 동작을 노출한다. | 설계 검토 |
| 26 | 전체 화면 진행 알림 대신 도구 창 안에 작은 진행 상태를 둔다. | 안전 실험 |
| 27 | 오류 파일 건너뛰기와 정상 기록 표시를 분리한다. | 안전 실험 |
| 28 | 로딩 중 키 입력/탭 전환 시 입력을 잃지 않게 한다. | 설계 검토 |
| 29 | 첫 사용에서는 빈 대화를 바로 열고 과거 기록은 뒤에서 불러온다. | 설계 검토 |
| 30 | 불러오기가 끝난 뒤 레이아웃이 크게 점프하지 않도록 공간을 확보한다. | 안전 실험 |

#### D. 기록 저장소 접근

| # | 아이디어 | 분류 |
|---:|---|---|
| 31 | `LoadOpen`의 전체 scan이 실제 사용자 데이터에서 병목인지 측정한다. | 안전 실험 |
| 32 | 대화별 작은 metadata index로 제목/열림 상태만 먼저 읽는다. | 설계 검토 |
| 33 | 본문 파일 읽기를 선택된 대화에 한정한다. | 설계 검토 |
| 34 | directory enumeration 결과를 열려 있는 IDE 세션 동안 재사용한다. | 설계 검토 |
| 35 | 파일 변경 감지 시 캐시를 무효화한다. | 설계 검토 |
| 36 | 기록 스캔을 일정 크기 묶음으로 나눠 UI 양보한다. | 설계 검토 |
| 37 | 최근 활성 기록을 먼저 읽고 나머지는 나중에 읽는다. | 설계 검토 |
| 38 | 대화 파일의 읽기 한도/파싱 실패 원인을 진단용으로 분류한다. | 안전 실험 |
| 39 | 동기 디스크 읽기와 Markdown 파싱 비용을 따로 측정한다. | 안전 실험 |
| 40 | 파일 경로 보안검사를 생략하지 않고 비용만 최적화한다. | 안전 실험 |

#### E. 목록/대화 렌더링

| # | 아이디어 | 분류 |
|---:|---|---|
| 41 | 대화 목록 UI virtualization 적용 가능성을 확인한다. | 설계 검토 |
| 42 | 목록에 필요한 제목/상태만 우선 표시한다. | 설계 검토 |
| 43 | 메시지 본문은 보이는 최근 페이지부터 렌더한다. | 설계 검토 |
| 44 | 개별 메시지 Markdown을 화면 진입 후 점진 렌더한다. | 설계 검토 |
| 45 | 긴 메시지의 code block 렌더 비용을 별도로 측정한다. | 안전 실험 |
| 46 | 원격 호출 없이 이미지 디코딩을 뒤로 미룬다. | 설계 검토 |
| 47 | 화면에 보이지 않는 GIF 애니메이션을 중지한다. | 안전 실험 |
| 48 | 문서 전환 시 이전 렌더 작업을 취소/무효화한다. | 설계 검토 |
| 49 | 오래 걸린 이전 메시지 로드 결과가 새 선택을 덮지 않게 한다. | 안전 실험 |
| 50 | 기존 페이지 크기 20이 실 데이터에서 적당한지 프로파일링한다. | 안전 실험 |

#### F. 비동기화/스레딩

| # | 아이디어 | 분류 |
|---:|---|---|
| 51 | 파일 IO와 파싱을 UI thread에서 분리할 수 있는 구간을 찾는다. | 설계 검토 |
| 52 | WPF 객체 생성/바인딩만 Dispatcher로 돌아오게 한다. | 설계 검토 |
| 53 | `AsyncPackage` 배경 초기화가 해당 VS2017 호출 경로에 맞는지 검토한다. | 설계 검토 |
| 54 | 비동기 결과에 generation token을 적용해 오래된 로드를 폐기한다. | 설계 검토 |
| 55 | Provider별 복원 작업을 순차/병렬 실행했을 때 실제 차이를 측정한다. | 안전 실험 |
| 56 | 경량/필수 설정과 선택형 부가 정보를 초기화 단계로 나눈다. | 설계 검토 |
| 57 | CLI 자동 발견을 초기 화면 표시 뒤로 미룬다. | 설계 검토 |
| 58 | 사용량/계정 metadata 조회는 명시적 요구 또는 idle 시에 실행한다. | 설계 검토 |
| 59 | 취소와 예외를 포함한 비동기 초기화의 완료 상태를 모델링한다. | 설계 검토 |
| 60 | 동기 대기 `.Wait()`/`.Result`가 UI 열기 경로에 있는지 검사한다. | 안전 실험 |

#### G. 캐시·파일 작업

| # | 아이디어 | 분류 |
|---:|---|---|
| 61 | 이미 읽은 현재 대화의 parsed model을 재사용한다. | 설계 검토 |
| 62 | provider 설정 한 번만 읽어 도구 창 초기화 단계 간 공유한다. | 안전 실험 |
| 63 | 실행 파일 자동 발견 결과를 프로세스 세션에 캐시한다. | 설계 검토 |
| 64 | 캐시에 file timestamp/size를 붙여 외부 변경을 감지한다. | 설계 검토 |
| 65 | 정리 작업을 사용자 open 시점에서 분리한다. | 설계 검토 |
| 66 | 필수 지침 사본이 최신이면 불필요한 재쓰기 비용을 확인한다. | 안전 실험 |
| 67 | 파일 경합/잠금이 반복될 때 짧은 재시도 정책을 진단한다. | 설계 검토 |
| 68 | 원자 저장 방식을 유지하면서 불필요한 전체 serialize 중복을 줄인다. | 설계 검토 |
| 69 | 대화목록 캐시 만료 시점을 명시적으로 관리한다. | 설계 검토 |
| 70 | 네트워크 경로 workspace를 로컬 폴더와 따로 측정한다. | 안전 실험 |

#### H. Markdown 편집 UX 및 렌더

| # | 아이디어 | 분류 |
|---:|---|---|
| 71 | CommonMark/GFM 공식 예제 기반 renderer 호환 회귀군을 둔다. | 안전 실험 |
| 72 | 미지원 문법을 깨진 출력 대신 원문/힌트로 안전하게 보여준다. | 안전 실험 |
| 73 | Markdown 목차 및 제목으로 이동을 제공한다. | 설계 검토 |
| 74 | 원문/미리보기 동기 스크롤을 추가한다. | 설계 검토 |
| 75 | 표 삽입과 열/행 확장 UI를 제공한다. | 수요 확인 |
| 76 | GFM 체크리스트 미리보기 표시 및 상호작용을 지원한다. | 설계 검토 |
| 77 | 각주 문법 지원 필요를 사용자 문서 유형으로 확인한다. | 수요 확인 |
| 78 | 링크/이미지 상대경로 이동과 오류 상태를 안내한다. | 안전 실험 |
| 79 | 이미지 대체텍스트와 크기 제한 UI를 개선한다. | 안전 실험 |
| 80 | 최근 문서/폴더 목록과 저장되지 않은 변경 표시를 제공한다. | 설계 검토 |

#### I. 비주얼·접근성·모션

| # | 아이디어 | 분류 |
|---:|---|---|
| 81 | primary/secondary action 색과 배치를 테마 전반에서 맞춘다. | 안전 실험 |
| 82 | hover/focus/pressed/disabled/loading 상태 색상을 점검한다. | 안전 실험 |
| 83 | 모든 테마에서 텍스트와 컨트롤 대비를 검사한다. | 안전 실험 |
| 84 | high contrast와 system DPI scaling을 확인한다. | 안전 실험 |
| 85 | tooltip 이외에도 아이콘 버튼에 accessible name을 부여한다. | 설계 검토 |
| 86 | 키보드만으로 주요 채팅/기록/설정 흐름을 수행한다. | 안전 실험 |
| 87 | primary entry와 micro-interaction duration을 중앙 token으로 조정한다. | 안전 실험 |
| 88 | 시작/종료 easing을 움직임 종류에 맞춰 정리한다. | 안전 실험 |
| 89 | reduce-motion에서 애니메이션 없이 상태 변화 의미가 남는지 확인한다. | 안전 실험 |
| 90 | toast 자동 사라짐 시간을 늘리는 대신 지속시간 선택 필요를 조사한다. | 수요 확인 |

#### J. 검증/배포/사용자 가치

| # | 아이디어 | 분류 |
|---:|---|---|
| 91 | UI/스토리지 병목을 재현하는 오프라인 fixture를 만든다. | 안전 실험 |
| 92 | warm reopen 성능 회귀 기준을 release 전 확인한다. | 안전 실험 |
| 93 | 지연 개선 전후의 동일 시나리오를 영상/캡처로 검토한다. | 안전 실험 |
| 94 | 정상·손상·권한 거부·느린 경로를 나눠 복원 회귀를 돌린다. | 안전 실험 |
| 95 | 로딩 중 메시지 순서/초안/읽지 않음 상태 보존을 검증한다. | 안전 실험 |
| 96 | 익명 진단 수집은 개인정보/선택 동의/기본 비활성화가 정해지기 전 도입하지 않는다. | 수요 확인 |
| 97 | 첫 사용 도움말에 “대화는 로컬 Markdown 기록으로 관리”를 설명한다. | 안전 실험 |
| 98 | Marketplace/설치 후 first-run에서 CLI 연결 상태 점검 경로를 노출한다. | 설계 검토 |
| 99 | 고객 요청에 없는 Mermaid/PDF/그래프 기능을 유지보수 부담만으로 추가하지 않는다. | 수요 확인 |
| 100 | 사용자 제보를 열린/숨김/최초실행 시나리오로 분류하고 재현 가능한 이슈만 구현한다. | 안전 실험 |

## 우선 handoff

**CodexCode (UI 응답성)**: warm auto-hide 경로에서 동일 input의 preview document 재사용 및 effective palette equality guard 검증을 최소 후보로 검토한다. 먼저 현재 candidate baseline에서 같은 control을 unload/load할 때 render/palette call 횟수와 identity를 계측하고, cache invalidation/explicit refresh/theme-change/Dispose 회귀를 지킨다. cold-open 단계 계측은 별도 follow-up으로 두고 최근 20개+비동기 이전 페이지 로딩을 보존한다. 원인 증거 없이 대형 리팩터링이나 영구 저장 구조 변경을 시작하지 않는다.

**CodexOrig4 (UI/모션)**: visual hierarchy, focus/hover 대비, 좁은 toolwindow 및 키보드/고대비에서 내부 화면을 검토한다. 220ms/150ms token을 약간 완만하게 한 독립 실험 캡처를 만들고, Fluent 모션/감소 모션 기준을 확인한다. 각기 다른 상태 애니메이션 전부를 한꺼번에 느리게 하지 않는다.

**CodexOrig5 (연구)**: Role #11/#12, 코드 경로와 시장 비교 조사를 이 문서에 완료. 다음은 개발자 handoff를 전달하고 report/소유권 등록만 완료한다.

### 근거 자료

- Microsoft Learn, [Visual Studio user experience guidelines](https://learn.microsoft.com/en-us/visualstudio/extensibility/ux-guidelines/visual-studio-user-experience-guidelines?view=visualstudio)
- Microsoft Learn, [AsyncPackage background loading and threading guidance](https://learn.microsoft.com/en-us/visualstudio/extensibility/how-to-use-asyncpackage-to-load-vspackages-in-the-background?view=visualstudio)
- Microsoft Learn, [Windows app startup performance guidance](https://learn.microsoft.com/en-us/windows/apps/develop/performance/)
- VS Code, [Markdown editing and preview](https://code.visualstudio.com/docs/languages/markdown)
- VS Code, [Extension UX guidelines](https://code.visualstudio.com/api/ux-guidelines/overview) and [progress notifications](https://code.visualstudio.com/api/ux-guidelines/notifications)
- GitHub, [GitHub Flavored Markdown specification](https://github.github.com/gfm/)
- CommonMark, [current specification](https://spec.commonmark.org/current/)
- Obsidian, [Markdown syntax](https://obsidian.md/help/syntax), [callouts](https://obsidian.md/help/callouts), [embeds](https://obsidian.md/help/embeds)
- Microsoft Fluent 2, [motion guidance](https://fluent2.microsoft.design/motion)
- Windows, [inclusive software design](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/designing-inclusive-software) and [accessible text/contrast](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessible-text-requirements)

## 변경/검증 한계

이번 결과는 소스 읽기와 공식 제품 문서 비교에 기반한 조사다. 사용자 실제 VS에서 cold/warm latency, animation 체감 A/B, 스크린리더, high contrast, DPI, Markdown corpus compatibility는 실행하지 않았다. 기록 수 한도나 비동기 이전 페이지 같은 기존 기능은 코드에 존재하지만 실제 환경 품질은 이 자료만으로 보증하지 않는다. 이 보고서는 개발 제안이며 제품 요구사항 승인을 대신하지 않는다.
