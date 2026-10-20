# TASK068 C5 최종 후보 UI 오프스크린 재검증

CodexCode가 전달한 C5 고정 입력으로 C2 때의 동일한 UI 검사기를 별도로 복사·고정해 실행했다. 제품 소스, README/PDF, 공개 자산, 공유 `bin/obj`는 변경하지 않았다.

## 입력과 검사 경계

- VS2017 DLL: `artifacts/task068/candidate5/2017/Release/VS2017.AIAssistant.dll`
- DLL SHA256: `AD6327BB58CEBF0E362B90134F9DFB803CDB566B457452AC1D17C7C999E66E02`
- 138개 입력 파일의 해시 목록 SHA256: `1E260BCFD5E02B0AC30D3D33A045F4E633A5C9C22E4AC82B28DF05A7F460824C`
- VS2017 exact `project.assets.json` SHA256: `773D111CA7FB4B7452EA373505C17AE633DFE436FE97381BA9B133471490440B`
- 4 SKU Release 빌드 오류/경고: 0/0, 0/0, 0/0, 0/0.

[C5 검사 harness](../tests/task068-design/c5/C5AssistantUiChecks.cs)와 [실행기](../tests/task068-design/c5/Run-C5AssistantUiChecks.ps1)는 C2 사본이며 C5 DLL과 별도 출력 `artifacts/task068-design/c5-actual/`을 고정 사용한다. 320/380/459/460px × 다크/라이트 8조건을 WPF `Measure`/`Arrange` 및 RenderTargetBitmap으로 검사했다. Window 생성·표시, GUI 회귀, OS 포인터/키보드, 실제 IDE, CLI 또는 네트워크 호출은 없었다. 검증 포커스는 `PresentationSource` 없는 컨트롤 테스트라 실제 focus 이동을 판정하지 않는다.

## 결과

8개 UI 조건 모두 실패 0으로 통과했다.

- #3 목록 항목 더블클릭 이벤트는 320/380/459px에서 목록을 접고 460px에서 목록을 유지했다.
- #4 긴 한글 라벨은 줄바꿈되어 높이 69.86px의 surface 버튼 안에 전체 문구가 표시됐다. 버튼은 선택 문구를 입력 초안에 넣고 자동 전송하지 않았다. WPF AutomationPeer 이름도 전체 문구를 반환했다.
- 버튼 배경/경계는 다크 `#282828`/1px와 라이트 `#F0F2F5`/1px로 적용됐다. 버튼 텍스트 대비는 다크 12.37:1, 라이트 13.76:1이다.
- 실제 focus 이동과 실제 IDE GUI 회귀는 이 무창 검사의 범위 밖이다. 후보에 포함된 다른 선택 규칙/readonly 변경의 전체 의미 검증으로 확대하지 않았다.

검사 입력 해시, 각 케이스 값, PNG SHA256은 [C5 manifest](../artifacts/task068-design/c5-actual/manifest.json)와 [실행 결과](../artifacts/task068-design/c5-actual/actual-ui-results.json)에 남겼다. 모든 렌더 파일 해시가 manifest와 일치한다. C2 산출물과 이전 결과는 보존했고 C5 전용 성공 이미지 출력만 새 폴더에 생성했다.
