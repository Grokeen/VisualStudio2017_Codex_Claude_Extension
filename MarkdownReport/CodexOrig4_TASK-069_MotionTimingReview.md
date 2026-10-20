# TASK069 UI 모션 타이밍 비교안

작성: CodexOrig4 · 2026-10-20 22:29 KST
범위: CodexOrig5의 Role #11/#12 연구 보고서 UI/모션 handoff 검토. C3 제품 후보는 동결 유지하며, 여기서는 코드·빌드·캡처를 수정하거나 실행하지 않았다.

## 비교 가설

현재 `AssistantMotionTokens`는 `PrimaryEntry = 220ms`, `MicroInteraction = 150ms`다. 검토 후보는 각각 `280ms`와 `180ms`다. 이는 60ms(27.3%) 및 30ms(20%) 증가다. 숫자는 사용자 체감 의견에 근거한 비교 가설이며 실제 실험 결과나 승인값이 아니다.

| Token | C3 baseline | 비교 후보 | 변화 | 관찰 대상 |
|---|---:|---:|---:|---|
| `PrimaryEntry` | 220ms | 280ms | +60ms / +27.3% | 패널·대화 기록·도구 메뉴·사용자 정의 대화상자의 진입 시간 |
| `MicroInteraction` | 150ms | 180ms | +30ms / +20% | 진행 단계 카드 hover/keyboard focus 강조 전환 |

두 값은 중앙 토큰에서만 조정하는 것이 읽기 쉽고, easing은 기존 `EaseOut`을 유지하는 것이 적절하다. Fan menu의 open/close timer와 55ms item stagger는 별도 hover 의도/순서 정책이므로 이 후보에서 바꾸지 않는다. 상호작용 입력·닫기·취소는 모션 완료를 기다리지 않아야 한다.

## 범위 충돌 및 제안

현재 `PrimaryEntry`는 Fan, HistoryPanel, 사용자 정의 대화상자뿐 아니라 `AssistantControl.Experience.cs`의 `ToastHost` 등장에도 사용된다. 따라서 값 하나를 280ms로 바꾸면 toast의 등장도 220ms에서 280ms로 바뀐다. Toast fade-out은 별도 800ms이고 toast 표시 타이머도 별도다. Root 결정에 따라 이번 통합 후보에서는 220/150ms를 그대로 유지하고 UI 성능 변경만 진행한다. 향후 사용자가 별도 모션 후보를 요청하는 경우에만, toast 등장까지 함께 늦출지 `ToastHost`를 220ms로 분리해 제외할지 범위를 먼저 확정해야 한다. 본 검토에서는 어느 쪽도 임의 적용하거나 source를 수정하지 않았다.

Thinking sweep은 별도 1600ms 반복 brush animation이며 `ClientAreaAnimation`이 꺼지면 반복 animation만 멈추고 상태 텍스트를 유지한다. 모션 제안은 sweep 주기, toast 표시 3초, 800ms 사라짐 fade, Windows preference listener와 정적 최종 상태 동작을 건드리지 않는다.

## 다음 후보에서의 비교 방법

Root가 후보 적용을 승인한 뒤 같은 고정 DLL 변형, OS 모션 선호, 테마, 도구 창 폭/배율, 시작 상태를 맞춰 baseline(220/150)과 후보(280/180)를 비교한다. 주요 진입은 처음/중간/완료 상태를 같은 캡처 시간점에서 보고, micro는 hover/focus 진입·이탈을 같은 입력 순서로 본다. 각 조건은 최소 5회 반복해 따뜻한 캐시/첫 진입 차이가 인상에 섞이지 않도록 한다. 이는 주관 체감 비교이며 사용자 연구 표본으로 해석하지 않는다.

합격 기준은 최종 상태가 더 읽기 쉽게 들어오고, 첫 조작이 늦게 느껴지지 않으며, 클릭·Enter/Tab/Escape·취소가 애니메이션 종료를 기다리지 않는 것이다. 대비·focus indicator·색 외의 상태 표식은 바뀌지 않아야 한다. Windows `ClientAreaAnimation` 비활성 시 두 duration 모두 0ms/정적 최종 상태이고, focus/선택 의미가 모션 없이도 읽혀야 한다. 속도가 여전히 급하다는 의견이 반복되면 작은 단계로 추가 비교하고, 입력이 무겁거나 toast까지 바뀌면 범위를 재검토하거나 원복한다.

## 확인한 소스 경로

- `src/VS2017.AIAssistant/Views/AssistantMotionTokens.cs`: 220/150ms 두 상수, EaseOut, `SystemParameters.ClientAreaAnimation` 기반 0ms/정적 경로.
- `src/VS2017.AIAssistant/Views/AssistantControl.Fan.cs`: PrimaryEntry와 별도인 220ms open dwell, 280ms close timer 및 55ms stagger.
- `src/VS2017.AIAssistant/Views/AssistantControl.Experience.cs`: HistoryPanel/ToastHost 진입, toast의 독립 800ms fade-out.
- `src/VS2017.AIAssistant/Views/AssistantDialogs.cs`: dialog 진입 및 MicroInteraction 기반 단계 카드 hover/focus.
- `src/VS2017.AIAssistant/Views/AssistantControl.Thinking.cs`: 별도의 반복 thinking sweep 및 OS 모션 설정 처리.

동작 가설이나 실측값을 만들기 위한 제품 source, build, Visual Studio/창 표시, OS 입력, Git/index 변경은 없다. 280/180ms는 사용자에게서 직접 승인된 요청이 아닌 연구 handoff의 backlog 가설로 남긴다. 별도 모션 후보에 대한 사용자의 요청과 Root 범위 결정 후에만 코드 실험을 시작한다.
