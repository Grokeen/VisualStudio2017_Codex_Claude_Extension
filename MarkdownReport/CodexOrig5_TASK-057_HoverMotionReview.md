# TASK-057 — Role #12 hover·모션 UI 권고

작성: CodexOrig5 / 2026-10-20 KST  
범위: Role의 2026-10-20 01:40 추가 요청 #12와 image56~65에 대한 UI 연구. 제품 코드·Role·기존 보고서·배포는 변경하지 않았다.

## 빠른 결론

팬 메뉴가 `OptionsHovered`의 `MouseEnter`에서 `OpenFanMenu()`를 즉시 호출하고, 영역 밖 이동 뒤 닫기 유예는 175ms다. 마우스가 화면을 가로지르다 의도치 않게 메뉴를 열고, trigger와 fan 사이를 건너는 중 짧은 유예 때문에 닫힐 수 있다. 포인터 hover에만 **200ms 전후의 열기 대기(시험 범위 180–250ms)**를 두고, 이 시간 내 이탈하면 취소한다. 열기 대기값은 표준의 강제 수치가 아니라 사용성 시험용 시작값이다. 닫힘은 포인터가 trigger와 fan 어디에 있는지 확인하면서 **250–300ms 유예**를 시험한다. 클릭이나 키보드 포커스·Enter/Space 열기는 기다리지 않게 한다.

현재 제품 소스에는 CodexOrig4의 `AssistantMotionTokens`가 이미 있다: 주요 진입 220ms EaseOut, 미세 반응 150ms, `SystemParameters.ClientAreaAnimation` 확인. 따라서 연구 요청은 공통 속도를 다시 바꾸지 않는다. **남은 설계 공백은 fan hover-intent 대기와 포인터 경계 이동의 지속성**이다.

## 이미지 범위별 짧은 권고

- **image56–57 팝업/Visual Studio 대비:** 두 어두운 화면 경계가 약해 popup이 IDE 위의 별도 표면이라는 인지가 어렵다. popup에 불투명도 있는 독립 surface, 또렷한 테두리 및 절제된 그림자/elevation을 주고, 실제 IDE 배경과 제목/닫기 버튼을 포함해 Light·Dark에서 비교한다. 본문 글자는 WCAG 텍스트 대비 4.5:1, 의미 있는 경계/아이콘은 3:1을 확인한다.
- **image58–60 Markdown/안내 footer·버튼:** 큰 빈 footer 영역을 콘텐츠 높이에 맞춰 줄이고 액션을 같은 바닥선에 정렬한다. 공통 버튼 스타일을 재사용해 높이·패딩·모서리·아이콘 간격을 맞추고, 실제 주요 저장/시작 동작만 primary로 강조한다. 작은 창/DPI에서도 버튼이 겹치거나 footer에 가려지지 않아야 한다.
- **image61–62 provider 탭:** 현재 두 provider 사이의 빈 폭을 줄이고 각 탭을 독립적으로 클릭·키보드 선택할 수 있는 크기는 유지한다. 선택 밑줄은 해당 탭 폭에 정렬하고 비선택·hover·focus 상태를 구분한다. Screenshot 62의 탭 전환 지연은 Backend055가 맡은 상태/재초기화 경로(#6)와 렌더 전환 모션을 분리 측정한다.
- **image65 팬 메뉴/hover:** 의도하지 않은 열기 억제, 포인터가 원점에서 실제 메뉴 항목으로 이동할 때 닫힘 취소, 메뉴 내부 hover/focus 유지, Escape 닫기 및 원점 포커스 복귀를 함께 보장한다. 레이블과 구획 경계는 hover 이외에도 대비와 선택/포커스 표시로 구분한다.

## 개발자가 사용할 합격 기준

1. 포인터 sweep에서 대기시간 안에 trigger를 통과하면 메뉴가 열리지 않는다. 포인터가 180–250ms 머물면 한 번만 열리며, click 또는 키보드 열기는 즉시 반응한다.
2. trigger→fan 항목으로 포인터를 이동해도 메뉴가 사라지지 않는다. 실제 포인터가 trigger와 fan 영역 모두를 벗어난 뒤 250–300ms 유예 후 닫히며, 다시 들어오면 닫힘 예약이 취소된다. 열린 메뉴에 포커스가 있으면 포인터 위치와 무관하게 유지한다.
3. 메뉴는 Tab/Shift+Tab으로 접근 가능하고 Enter/Space로 열 수 있다. Escape는 닫고 열기 전의 원점 버튼으로 포커스를 돌린다. 자동 툴팁과 fan 텍스트가 중복되지 않는다.
4. Windows `ClientAreaAnimation`이 꺼졌을 때 위치·선택·열림 상태는 애니메이션 없이 즉시 보인다. 상태를 motion만으로 전달하지 않는다. 열기/닫기 timer는 Unloaded/Dispose에서 정리되고 반복 재진입으로 중복 타이머·깜빡임이 없다.
5. popup 대 foreground/background의 텍스트 대비는 WCAG 2.2 AA 기준(일반 텍스트 4.5:1, 큰 텍스트 3:1), 의미 있는 컨트롤 경계는 3:1 이상이다. 버튼 hover·focus·disabled·selected 상태가 Light/Dark에서 모두 식별된다.
6. 탭 간 시각적 간격을 줄여도 hit target, tab 순서, AutomationName과 선택 지시자가 유지된다. Markdown/안내 footer는 빈 공간이 불필요하게 남지 않고 320–520px 폭·125%/150% DPI에서도 버튼 가림이 없다.
7. 검증은 부채꼴 메뉴의 실제 포인터 접근·빠른 통과·경계 이동·focus/Escape, 애니메이션 설정 on/off를 포함한다. VS2017·2019·2022 지원 DLL에서 반복 열기/닫기와 unload를 확인한다. #6 탭 전환 전체를 motion 수정 성공으로 대체 판정하지 않는다.

WCAG 2.2 SC 1.4.13은 hover/focus로 나타나는 추가 콘텐츠에 dismissible, hoverable, persistent 조건을 제시한다. 이 기준은 180/250ms 같은 수치를 규정하지 않는다. 따라서 대기·유예는 마우스 오동작을 줄이기 위한 UX 튜닝값으로 두고 사용자 관찰로 결정한다. Windows Tooltip 지침도 팁은 보충 정보로 제한하고, 대상 자체를 가리지 않으며, 팁 쪽으로 포인터를 옮길 수 있게 한다. 긴/상호작용형 내용은 tooltip 대신 메뉴/flyout/popover 성격의 UI를 사용한다.

## 전달

빠른 권고와 겹치는 담당 소유 범위를 Root 및 Designer056에게 메시지로 먼저 전달했다. Research 인계만 수행하며 `AssistantControl.Fan.cs` 등 제품 파일, Role, 기존 TASK054 문서, 브랜치, 빌드·릴리스는 수정하지 않았다.

## 공식 1차 참고 자료

- [W3C WCAG 2.2 — Content on Hover or Focus (SC 1.4.13)](https://www.w3.org/WAI/WCAG22/Understanding/content-on-hover-or-focus.html)
- [Microsoft Learn — Tooltips (Windows apps)](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/tooltips)
- [Microsoft Win32 UX Guide — Tooltips and infotips](https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-tooltips-and-infotips)
- [Microsoft Fluent 2 — Tooltip usage](https://fluent2.microsoft.design/components/web/react/core/tooltip/usage/)
- [W3C WCAG 2.2 — Contrast (Minimum), SC 1.4.3](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html)
- [W3C WCAG 2.2 — Non-text Contrast, SC 1.4.11](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html)
- [W3C WCAG 2.2 — Animation from Interactions, SC 2.3.3](https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions.html)

