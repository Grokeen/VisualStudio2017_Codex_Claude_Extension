# TASK-065 — UI 디자인 벤치마크와 이벤트 모션 후속 권고

작성: CodexOrig5 / 2026-10-20 KST  
범위: 제품 코드 읽기, 최근 UI 연구·검증 문서와 합성 화면 검토, 공식 1차 디자인 가이드 비교. 코드/릴리스는 수정하지 않았다.

## 결론

확장 프로그램은 이미 Claude/Codex 선택, 대화 입력, 승인·변경 검토, Markdown 및 설정 화면을 VS 도킹 패널 안에 제공하고, 다크/라이트 팔레트·좁은 폭 fixture·공통 MotionTokens도 갖췄다. 지금 필요한 방향은 새 장식이나 큰 재배치보다 **작은 IDE 패널에서도 핵심 동작이 즉시 보이는 위계**, **모든 UI 상태의 테마·대비 일관성**, **사용자가 빠르다고 느끼는 주요 진입 모션의 선택적 완화**다.

현재 `AssistantMotionTokens.PrimaryEntry`는 220ms EaseOut, `MicroInteraction`은 150ms EaseOut이다. Windows Fluent 기준의 일반 지속시간은 250ms, 빠른 지속시간은 167ms, 더 빠른 지속시간은 83ms다. 그러므로 220ms가 비정상적으로 짧다고 할 수는 없지만, 사용자의 체감이 빠르다면 팬/패널/대화 기록처럼 큰 영역이 들어오는 **주요 진입만 250–270ms로 시각 비교**할 근거가 있다. 버튼 hover·focus처럼 반복되는 마이크로 반응은 빠르게 둔다. 전역으로 늦추지 않는다.

## 대표 제품·디자인 가이드 비교

| 기준 | 공식 문서/제품에서 확인한 원칙 | 현재 UI에 적용할 점 |
|---|---|---|
| Visual Studio Code 확장 UX | 패널은 보조 기능에 쓰고, toolbar action을 과도하게 늘리지 않으며 기본 접기/닫기와 중복되는 아이콘은 피한다. 뷰는 적게 유지하고, 폭을 옮기거나 줄여도 reflow하며, 도구 설명·키보드·테마 접근성을 보장한다. | AI 확장 패널에서 주 동작(전송/중지, 변경 검토·승인)은 전면에 두고, 빈도가 낮은 설정·부가 도구는 menu/명확한 그룹으로 밀도를 조절한다. VS의 기본 창 버튼/명령을 다시 만들지 않는다. 320px 안팎에서 아이콘 겹침, 라벨 잘림, 옮긴 패널의 폭 변화를 확인한다. |
| JetBrains IntelliJ IDEA | tool window 크기 조절, UI 폰트 크기 조정, Compact mode를 제공하고, 화면 reader 설정 시 키보드 중심 동작도 맞춘다. | 현재의 좁은 폭 검증을 계속 유지하면서 125/150% DPI, 긴 한국어 라벨, 키보드 포커스 상태를 포함한다. 별도 “콤팩트 모드” 설정을 새로 추가하기 전에 IDE pane 크기 변화에서 자연히 재배치되는지 확인한다. |
| Microsoft Fluent 2 / Windows | 의미 기반 디자인 토큰으로 색·글꼴·간격·모션을 공유한다. 기본 모션은 빠르고 문맥에 맞게 하며, 진입/이동/퇴장을 서로 다른 목적과 시간으로 구분한다. WinUI의 표준 지속시간 250/167/83ms는 참고 기준이다. | 기존 팔레트와 220/150ms 토큰을 재사용하고, 숫자를 여러 파일에 다시 박지 않는다. 넓은 패널 진입만 250–270ms 실험, 미세 반응은 150–167ms 범위, 닫힘은 빠르게 유지한다. |
| Apple Human Interface Guidelines | 모션은 목적을 전달해야 하고, 자주 쓰는 상호작용에는 불필요한 모션을 피하며, 짧고 정확하고 취소 가능한 피드백을 권한다. 모션을 유일한 상태 표시로 쓰지 않는다. | 열린/선택/실행 상태는 모션이 없어도 색·레이블·아이콘으로 이해돼야 한다. 기존 Windows 애니메이션 설정을 존중하고, 재생 중에도 클릭·취소를 막지 않는다. |

## 현재 UI에서 확인된 개선 우선순위

1. **작은 pane에서 동작 위계** — 화면 하단의 입력·첨부·모드·모델·권한·전송 제어는 모두 중요하지만 한 줄에 함께 있을 때 시각적 무게가 비슷해질 수 있다. 최소 폭에서 전송/중지와 변경 승인 흐름을 우선 인지할 수 있는지 확인한다. 보조 제어를 이동하거나 숨기려면 기존 Role 요구와 접근 경로를 보존한다.
2. **상태별 시각 일관성** — 코드에는 공용 AssistantPalette/테마 리소스, 공용 액션 버튼, 라이트·다크·커스텀 테마가 이미 존재한다. 부족한 부분은 새로운 색 추가보다 비활성, focus, selected, warning, disabled 상태가 각 팝업·대화·메뉴에서 같은 의미색과 대비를 유지하는지 전체 상태표로 검증하는 것이다. 기존 TASK056은 footer·팝업 표면·버튼의 주요 정합성 수정을 통과시켰으므로 그 결과를 되돌리지 않는다.
3. **기능 목록과 도구 아이콘 수 절제** — VS Code는 View와 toolbar action 과잉, 기존 IDE 동작 반복을 피하도록 권한다. 우리 확장도 현재 보여야 하는 주 동작과 메뉴로 들어갈 보조 동작을 분리하되, 새 도구/통계/장식 아이콘을 추가하기 전 사용 빈도와 실제 수요를 확인한다.
4. **접근 가능한 입력 흐름** — 키보드 Tab 순서, focus ring, tooltip/AutomationName, Escape·Enter 동작을 버튼/팬 메뉴/대화 도구에서 화면별로 확인한다. JetBrains가 화면 reader 감지와 키보드 최적화를 안내하는 점을 참고한다.
5. **모션 체감** — 220ms는 이미 공통화되어 있고 OS 애니메이션 비활성도 검사한다. 남은 개선은 token 재설계가 아니라 사용자 체감이 걸린 이벤트 하나를 특정해 220ms와 250/270ms를 동일 화면·동일 하드웨어에서 비교하는 것이다. Fan hover의 열기 대기(180–250ms)와 열기 애니메이션 길이는 다른 문제로 기록하며, 포인터 의도 지연을 애니메이션 시간에 합치지 않는다.

## 모션 실험안

| 종류 | 현재값 | 시범 목표 | 처리 |
|---|---:|---:|---|
| 큰 pane/dialog/기록 진입 | 공통 220ms EaseOut | 250ms, 필요 시 270ms 대조군 | 주 이벤트만 비교; 애니메이션 후에도 조작 가능 |
| fan 항목/순차 등장 | 공통 220ms; 2번째 항목에는 stagger | 250ms 진입 후보 | 전체 노출이 너무 느려지지 않게 stagger 55ms는 분리 관찰; hover open delay와 닫힘 timer는 별도 측정 |
| 버튼·focus·선택의 미세 피드백 | 150ms | 150–167ms 유지 | 사용자가 자주 반복하는 반응은 즉시성과 가시성 우선 |
| 퇴장·toast dismiss | 화면별 기존 timer | 유지 또는 의미에 맞게 별도 판정 | 진입 속도를 바꿔도 닫힘/재진입/취소 timer에 영향 주지 않음 |
| Thinking 반복 표시 | 1600ms 반복 shimmer | 유지/주의력 확인 후 저감 | 진행 상태 텍스트를 남기고 모션 off에서는 정지 |

**실험 판정 기준:** 최소 5회 반복의 짧은 사용성 비교(220 대 250ms)를 시각적으로 진행한다. 250ms 쪽이 상태 변화를 읽기 쉽고 기다리는 느낌을 주지 않으면 그 주요 진입에만 채택한다. 클릭·키보드 조작·Escape가 애니메이션 완료를 기다리지 않는지, ClientAreaAnimation off 시 최종 상태가 즉시 보이는지, Unload/Dispose 이후 타이머·Storyboard가 남지 않는지 검증한다. 타이밍은 표준의 의무값이 아니라 Fluent 숫자를 참고한 내부 시험값이다.

## CodexOrig4 개발 인계

CodexOrig4에 요청: 최근 UI 소유권과 현재 `AssistantMotionTokens` 구현(220ms/150ms)을 확인하고, **220ms vs 250ms(필요하면 270ms) 주요 진입만** 비교 캡처 또는 WPF fixture로 검토해 달라. 이벤트가 잦은 버튼/focus 반응은 늦추지 말고, hover 열기 대기·닫힘 유예 timer와 진입 duration은 분리할 것. UI 벤치마크의 우선순위인 작은 폭 composer 위계, Light/Dark의 selected/focus/disabled 대비, keyboard focus도 다음 디자인 검토에 포함하되, Root의 통합 작업과 충돌하면 source edit 없이 결과/요청만 회신한다. 이 인계는 특정 값의 전면 변경을 승인하는 뜻이 아니며, 구현 여부와 최종 값은 담당 개발자의 비교 결과를 따른다.

## 제한

검토 자료는 TASK056의 offscreen WPF·합성 캡처, TASK048/057 연구, 현재 source입니다. 실제 Visual Studio에서 고객이 사용하는 동안의 관찰 시험은 하지 않았고, 이 문서는 WCAG 적합성 인증을 주장하지 않습니다. 프로젝트 트리의 `MarkdownReport/Role.md`를 읽었으며, Role의 파일/코드 작업 내역 형식은 따르되 제품 소스와 기존 담당자 변경 파일은 손대지 않았습니다.

## 공식 1차 참고 자료

- [VS Code Extension UX Guidelines — Panel](https://code.visualstudio.com/api/ux-guidelines/panel)
- [VS Code Extension UX Guidelines — Views](https://code.visualstudio.com/api/ux-guidelines/views)
- [VS Code Extension UX Guidelines — Webviews](https://code.visualstudio.com/api/ux-guidelines/webviews)
- [JetBrains IntelliJ IDEA — Accessibility](https://www.jetbrains.com/help/idea/accessibility.html)
- [JetBrains IntelliJ IDEA — IDE viewing modes / compact mode](https://www.jetbrains.com/help/idea/ide-viewing-modes.html)
- [Fluent 2 — Design tokens](https://fluent2.microsoft.design/design-tokens)
- [Microsoft Learn — Timing and easing](https://learn.microsoft.com/en-us/windows/apps/design/motion/timing-and-easing)
- [Microsoft Learn — Motion in Windows](https://learn.microsoft.com/en-us/windows/apps/design/motion/)
- [Apple Human Interface Guidelines — Motion](https://developer.apple.com/design/human-interface-guidelines/motion)
- [Google Material 3 — Motion physics system](https://m3.material.io/styles/motion/overview/how-it-works)
