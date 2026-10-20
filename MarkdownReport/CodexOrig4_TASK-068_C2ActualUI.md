# TASK068 C2 실제 컨트롤 UI 재검증

검토 입력은 2026-10-20 C2 candidate2의 VS2017 실제 `AssistantControl` DLL이다. CodexCode가 #3/#4 제품 파일 소유를 유지하고 있어 읽기 전용으로 검증했다.

## 고정 입력

- DLL: `artifacts/task068/candidate2/2017/Release/VS2017.AIAssistant.dll`
- DLL SHA256: `804DF1D5D33775468D7D2FAA461C5E84028F32D255AE13052C20B5B31116962F`
- 137개 입력 파일 해시 목록 SHA256: `B2D4FBE08E24364F848E4C54987DD9432D284637F3F35D3772E93208194B023D`
- VS2017 exact `project.assets.json` SHA256: `82B1DAA2BF406E020A58E58DDE34E6FAE63DF8EFC8CB9A81CAB6F3378E4E9FE0`
- Candidate 4 SKU Release: 오류 0. 경고는 VS2017=0, VS2017.Legacy=0, VS2019=0, VS2022=2로 기록됐다. C2는 전체 최종 후보가 아니다.

[복사 고정한 C2 검사 코드](../tests/task068-design/c2/C2AssistantUiChecks.cs)와 [실행기](../tests/task068-design/c2/Run-C2AssistantUiChecks.ps1)는 별도 폴더에 두었다. 실제 C2 DLL을 로드해 320, 380, 459, 460px `LayoutRoot` 폭 × 다크/라이트 총 8개 조건에서 XAML 배치와 WPF 요소를 측정했다. 실제 Window 또는 `Show` 호출, OS 포인터/키 입력, 실제 IDE, CLI, 네트워크 사용은 없었다. 사용자 이름이 아닌 고정 예시 대화 라벨을 렌더 입력에 사용했다.

## 결과

8개 조건 모두 검사 통과, 실패 0이다.

- #3 실제 목록 선택 컨테이너에 더블클릭 이벤트를 전달했을 때 320/380/459px에서 목록이 접히고, 460px에서는 열린 상태를 유지했다.
- #4 긴 한국어 선택지는 C1의 단일 행 잘림에서 개선되어 실제 `TextBlock` `TextWrapping=Wrap`, 버튼 높이 69.86px로 보인다. 320px 시안에서도 전체 문장이 읽힌다.
- 선택지는 다크 `#282828`, 라이트 `#F0F2F5` 공용 표면색과 테두리 1px을 사용한다. 선택지 글자 대비는 다크 12.37:1, 라이트 13.76:1이다.
- WPF AutomationPeer 이름이 전체 선택 문구를 반환했고, 버튼 포커스 가능성, 선택 버튼 이벤트의 입력 초안 삽입, 자동 제출 없음도 8개 조건에서 확인했다.
- 포커스 복귀는 PresentationSource가 없는 오프스크린 환경에서 `item.Focus()`가 false여서 검증하지 못했다. 실제 IDE에서 키보드로 목록 항목을 더블클릭한 경우의 포커스 경로는 별도 UI 검사에 남긴다.

케이스별 값과 모든 PNG SHA256은 [C2 manifest](../artifacts/task068-design/c2-actual/manifest.json) 및 [상세 결과](../artifacts/task068-design/c2-actual/actual-ui-results.json)에 있다. 문서 담당자가 재사용할 성공 장면만 [docs/assets/ui-guide-v0.13.0/candidate2](../docs/assets/ui-guide-v0.13.0/candidate2)에 복사하고 별도 manifest로 원본 입력·렌더 경계를 남겼다. 공개 문서 본문은 수정하지 않았다.

제품 DLL·소스·공유 `bin/obj`·IDE는 변경하지 않았다. C2 후속 통합 후보가 고정되면 같은 폭/테마 검사를 재실행하고, 실제 IDE focus 검증과 별도 Root GUI 회귀를 완료할 때까지 전체 UI 완료로 표시하지 않는다.
