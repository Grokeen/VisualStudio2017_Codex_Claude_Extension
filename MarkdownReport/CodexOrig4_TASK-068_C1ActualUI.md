# TASK068 C1 실제 컨트롤 UI 검사

기준: `Role.md` 2026-10-20 12:47 요청 #3/#4와 UI 통일 요구. CodexCode가 제품 소스 소유를 유지하므로 검사만 수행했다.

## 입력 고정

- VS2017 C1 실제 DLL: `artifacts/task068/candidate1/2017/Release/VS2017.AIAssistant.dll`
- DLL SHA256: `2BC63F507870636BE12FB0AF86CCC67F3515E5C816BA4A645853833B843F3FF5`
- 원본 입력 해시 목록 SHA256: `20E275A01F95AE1270CB197BAA6ADAC72C35DFCDD8495490A82C64E2BE040DB4`
- 해당 VS2017 `project.assets.json` SHA256: `C96C41D1C9B548C51E1AE6DF374D863D7CAA6E147C5CF1C4FBE873BDA82B68EC`
- C1 빌드 로그는 오류 0, 경고는 VS2017=1, VS2017.Legacy=1, VS2019=1, VS2022=2다. Root가 동결 전인 4 SKU 후보라고 전달했다.

검사 실행기는 [C1AssistantUiChecks.cs](../tests/task068-design/C1AssistantUiChecks.cs)와 [Run-C1AssistantUiChecks.ps1](../tests/task068-design/Run-C1AssistantUiChecks.ps1)이다. `AssistantControl` 실제 후보 DLL을 reflection으로 로드하고 320, 380, 459, 460px의 `LayoutRoot` 폭과 다크/라이트 팔레트에서 RenderTargetBitmap을 만들었다. Window 생성/표시, OS 마우스·키 입력, 실제 IDE, 포인터, CLI/네트워크 사용은 없다. 설정/대화 fixture는 `artifacts/task068-design/c1-actual/f/` 아래 별도 경로만 사용했다. 공유 `bin/obj`, 제품 소스, 공개 자산은 수정하지 않았다.

## 결과

- #3 더블클릭 임계점은 320/380/459px에서 목록을 접고 460px에서는 유지했다. 8개 theme/width 조건 모두 예상과 같았다.
- #4 선택 파싱, 실제 선택 버튼 생성, UI Automation peer 이름 생성, 긴 한국어 선택을 공통 입력 초안에 정확히 삽입, 자동 제출 없음은 8개 조건 전부 통과했다. 버튼은 포커스 가능했다.
- 다크 본문 대비는 15.04:1, 라이트는 14.52:1이었다. 공용 palette가 양 모드에서 일관된 전경·배경으로 적용됐다.
- **실제 UI 결함:** 모든 8개 조건에서 긴 한국어 선택 버튼 높이는 30.62px 한 줄에 그쳤다. PNG에서 문구가 중간에서 잘리며 전체 선택 라벨이 보이지 않는다. 버튼 배경은 투명이고 공통 `SurfaceButton`의 기본 border 두께도 0이어서 기본 상태에서는 선택지가 일반 본문 텍스트처럼 보인다. 이 두 관찰 때문에 긴 라벨의 여러 줄 표시 검사가 8/8 실패했다.
- focus 복귀는 이 격리 오프스크린 렌더에서 PresentationSource가 없어 `item.Focus()`가 false였으므로 판정할 수 없다. 실제 창/IDE 입력 포커스 동작으로 오인하지 않도록 미검증으로 기록한다.

재현 결과와 이미지 해시는 [manifest.json](../artifacts/task068-design/c1-actual/manifest.json), 케이스별 상세는 [actual-ui-results.json](../artifacts/task068-design/c1-actual/actual-ui-results.json)에 있다. `assistant-dark-320-choices.png`와 `assistant-light-320-choices.png`는 잘림을, `assistant-dark-320-history-closed.png`는 목록 접힘 후 입력 가능한 장면을 보여준다.

## UI 일관성 수정 제안

CodexCode가 #4 제품 소유를 유지하므로 이 검토에서는 소스를 바꾸지 않았다. 다음 동결 후보에서 동일 의미색 토큰을 유지하면서 선택 버튼 ContentTemplate의 TextBlock에 줄바꿈을 주고, 좁은 창의 선택지를 명확한 surface 버튼(같은 corner radius/경계/포커스 강조)으로 보여주는 것을 권한다. 이렇게 하면 다크/라이트 모두 기존 palette와 어울리고 한 줄로 잘린 선택지 대신 읽고 고를 수 있는 버튼이 된다. 실제 IDE에서는 선택지 focus 이동 및 #3 접기 후 보이는 대화/입력으로 키보드 포커스가 돌아오는지 별도 검증해야 한다.
