# TASK056 — UI 정합성 수정 인계

작성: CodexOrig4 / 2026-10-20 KST  
범위: Role 2026-10-20 01:40의 UI #1–5 및 #11 일부 화면 정합성. 팬 hover 동작은 Root054에, 탭 전환 성능·입력 큐는 Backend055에 인계했다.

## 변경

- 안내·설정 팝업은 공통 `AssistantPopupBackgroundBrush`와 별도 `AssistantDialogBorderBrush` 외곽선을 사용해 Visual Studio 위에 분리된 창 표면을 만든다. Light/Dark·Kakao·Dracula별 외곽선 색을 두고 테마 변경은 기존 동적 자원 흐름을 따른다.
- footer의 위쪽 간격과 패딩을 줄이고 빈 상태 메시지 줄을 접어 실제 footer를 79.62px에서 53px로 줄였다. 오류/상태 텍스트를 대입하면 해당 줄은 다시 표시된다. 액션 버튼은 하나의 `AssistantActionButton` 높이·패딩을 공유하고, 명시적 `PrimaryButton` 동작은 보조 버튼과 구분된다.
- Markdown 도구 버튼과 안내/주요 액션은 같은 34px 공통 action style을 사용한다. 검색 CheckBox는 이미 공통 테마 스타일을 쓰고 있어 유지했다.
- Claude/Codex 탭은 전체 오른쪽 간격을 이전 22px에서 8px로 줄이고 좌우 패딩을 5px로 맞췄다. 아이콘은 12px, 메뉴 칸은 32px로 조정했다. 200px 폭에서 탭 2개의 측정 너비는 75.12px + 69.87px로 두 라벨이 모두 보인다. 선택 밑줄, hover/focus 상태와 접근성 이름은 유지된다.

## 소유 경계 및 검증

개인 source snapshot은 `artifacts/task056/ui-source/Views/`에 고정했다. C6 개인 사본에 UI 소유 파일만 적용한 `artifacts/task056/candidate6-ui-final4/`를 별도로 빌드했고 네 대상(2017 Legacy, 2017, 2019, 2022)이 모두 오류0·경고0으로 끝났다. UI 회귀 fixture는 기존 TASK050의 Grid 루트를 보존하고 새 외곽 Border 하위 Grid를 읽도록 TASK056 개인 사본에서만 고쳤다. 17개 검사가 모두 통과했다. 정적 소스 검사는 18/18 PASS이며 결과는 `artifacts/task056/Validation.json`에 있다.

실제 WPF DLL을 offscreen에서 띄워 Light, Dark, Kakao Light/Dark, Dracula Dark의 팝업 및 Markdown 창, 그리고 200/300/900px provider pane을 PNG로 렌더했다. 버튼은 주요/보조 모두 높이 34px, 빈 fixture footer 53px였다. 팝업의 텍스트 대비는 12.44–17.58:1, 창 외곽선과 대표 Light/Dark host 표면 대비는 3.36–5.29:1이었다. 캡처·측정 JSON은 `artifacts/task056/ui-tests/rendered-layout-final5/`에 있다. 비교 host 배경은 Visual Studio 기본 테마를 대체하는 합성 색(`F0F0F0`/`1E1E1E`)이며 실제 IDE 위에 띄운 GUI 검증이나 고객 설치 검증은 아니다.

실행 DLL 빌드는 개인 C6 source copy에서만 했다. 공유 `bin/obj`, 공개 릴리스, 이전 TASK050 fixture/증거와 v0.12.0-preview.1은 수정하지 않았다. backend/Root 통합 후보에서 작은 창·고배율 및 실제 Visual Studio 테마를 한 차례 더 렌더해야 한다.

Root054는 `AssistantControl.Fan.cs` 구현, fan 색/위치 및 hover intent(Research057 권고)를 담당한다. Backend055는 Experience/Chat의 성능·큐 경로를 담당한다. 이 파일들은 편집하지 않았다.

## 후속 캡션 버튼 수정 / 2026-10-20

3차 감사의 dark popup 캡처에서 창의 최소화·최대화·닫기 버튼이 WPF 기본 흰 크롬으로 보이는 문제를 확인했다. 원인은 `CreateCaptionButton`이 새 `Style`을 만들면서 공통 `AssistantActionButton` 스타일 기반(`BasedOn`)을 지정하지 않은 점이다. 이를 공통 action style 기반으로 바꾸어 popup 캡션과 footer의 primary/secondary 버튼이 같은 ControlTemplate을 사용하게 했다. 기존 dark before PNG는 보존했고 수정 후 새 PNG는 `artifacts/task056/ui-tests/caption-fix1/rendered-final/vs-dark-popup-over-host.png`에 저장했다.

고정 후보 DLL을 offscreen WPF로 Light/Dark·Kakao·Dracula 테마에 렌더한 결과 UI fixture는 17/17, 렌더 오류 0이다. 실제 버튼 객체 검사에서 primary·secondary·caption 버튼의 ControlTemplate 참조와 CornerRadius가 동일하고, 공통 템플릿에 키보드 focus 및 disabled 트리거가 있으며 액션 버튼 34px, 캡션 버튼 36px임을 확인했다. 네 SKU 개인 빌드는 모두 오류0·경고0이다. 검증 상세는 `artifacts/task056/CaptionFixEvidence.json` 및 `artifacts/task056/candidate6-ui-caption-fix1/`에 있다. 실제 Visual Studio 셸 실행은 수행하지 않았다.
