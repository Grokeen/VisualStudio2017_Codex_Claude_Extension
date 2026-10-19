# TASK-064 C11 최종 IDE 테마 재검사

담당: CodexOrig / TASK054 Root의 전용 검증 지원. 2026-10-20.

최종 `artifacts/task054/candidate11` 제품을 실제 VS2017 15.9·VS2019 16.11·VS2022 17.14의 새 격리 RootSuffix에 설치해 검사했다. **원본 결과는 77/81 PASS, 4 FAIL**이다. 실패를 삭제하거나 전체 통과로 재분류하지 않았다. 실제 테마 선택·호스트 색상·비기본 설정 보존은 모두 확인됐고, 실패한 조건은 추가 직렬화 행까지 완전히 동일해야 하는 비테마 행 집합 비교이다.

## 실제 실행 결과

| IDE | 전용 증거 폴더 | 원본 PASS/전체 | 비기본 baseline |
|---|---|---:|---|
| 2017 15.9 | `artifacts/task064/candidate11-2017-sentinel1` | 26/28 | AutoUpdateTags=False |
| 2019 16.11 | `artifacts/task064/candidate11-2019-sentinel1` | 26/28 | DefaultZoomSetting=0 |
| 2022 17.14 | `artifacts/task064/candidate11-2022-basic1` | 25/25 | 기존 기본 흐름 |

기존 `tests/task064`의 세 소스는 수정하지 않았다. C9 FinalValidation의 동결 SHA256과 현재 소스가 모두 일치하고, 각 helper 프로젝트에 복사한 C# 소스도 동일하다. SDK별 helper Release 빌드와 정식 VSIX 설치를 성공한 뒤 등록된 제품 명령을 통해 실제 설치 DLL을 로드했다.

각 새 프로필은 정확한 DTE.RegistryRoot가 예상 RootSuffix 전체 경로와 일치할 때만 설정 명령을 실행한다. `/quiet /rootSuffix /instanceIds`로 독립 테마·AI 제품·목적별 helper를 설치했다. 설치 버튼 자체의 물리 클릭이나 OS 데스크톱 자동화는 하지 않았다.

세 IDE에서 Kakao와 Dracula 각각 다음 항목을 확인했다.

- 설치 제품 DLL SHA256과 고정 C11 VSIX의 DLL 일치
- 설정의 두 버튼, 지원 연도, 설치된 확장 옆 bundled 테마 VSIX와 그 SHA256
- 실제 DTE import 후 재export된 선택 GUID: Kakao `{1D41C4A3-CF75-4F0A-B2FF-9A8C2D431B92}`, Dracula `{8EC84919-8A34-422A-ABE1-CC238AD4D4B1}`
- 실제 `VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey)` 색상: Kakao `FFD9DDE3`, Dracula `FF282A36`
- 성공 후 pending 소비, import/check 임시 파일 정리, 잘못된 pending의 예외 비전파와 복구 안내

## 기존 값 보존과 추가 직렬화 행의 구분

2017과 2019는 공식 DTE 최소 설정 import로 자기 테스트 프로필에 비기본값을 먼저 설정했다. 실제 export에서 False/0 설정 성공을 확인한 후 제품을 실행했고, 두 테마 뒤에도 같은 값이 보존됐다. **비기본값 보존은 4/4**이다. 미확인 값을 임의 기본값으로 채우지 않았다.

ordinal 경로와 값으로 모든 기존 비테마 PropertyValue를 비교한 결과 변경 0 / 삭제 0이었다. 2017은 각 테마 뒤 XAML_Miscellaneous의 6개 행, 2019는 XAML Designer/General의 9개 행이 새로 추가됐다. 2022는 행 집합도 그대로 유지됐다. 따라서 기존 값을 덮어쓴 것과 새 serializer 행이 생긴 것을 구분한다. 원본의 네 equality FAIL은 계속 보존한다.

2017 추가 행은 AutoInsertAttributeQuotes, AutoInsertCommas, AutoInsertEndBraces, AutoInsertEndTags, ShowDesignerErrors, WarnMinTargetCompatibility이며 값은 모두 True이다. 2019는 AutoNameElements=false, InitialDisplayMode=2, ZoomControlMode=1, AutoToolboxPopulate=true, InitialDisplayOrientation=2, EnableXamlDesigner=true, UnitType=1, UseQuadrantLayout=false, AutoInsertLayout=true이다. 이 행들의 이전 유효 값을 모두 직접 읽었다고 주장하지 않는다.

`artifacts/task064/candidate11-settings-analysis.json`과 각 sentinel run의 `settings-analysis.json`에는 원본 export에 근거한 정확한 전후값과 Added/Changed/Removed 구분이 있다. readonly UserSettings/XAML 관찰 원본도 각 run의 `xaml-store-before-apply.json`, `kakao-xaml-store.json`, `dracula-xaml-store.json`에 보존했다. 읽기 가능한 실제 저장값과 export만 노출하는 값을 구분하며 원본 raw 결과는 수정하지 않았다.

## 고정 DLL SHA256

| 대상 | 실제 설치 DLL과 일치한 C11 SHA256 |
|---|---|
| 2017 | `01E737C3979FA232510386D8296A8AB9CC9B0E275ECE827D86256ED4ED0E49E6` |
| 2019 | `4B2F3D0C3AC04749D62FB970B1143F97DEDEABF873EEB8E66B53CA5D19859B7F` |
| 2022 | `7137C6E16E8F080E8D82330F63BE2142D2666E21C55005492571CB936106B5F6` |

선택 VSIX·테마 VSIX·helper DLL·raw 결과·동결 소스 SHA와 소유 프로세스 정리 정보는 `MarkdownReport/CodexOrig_TASK-064_C11_Validation.json`에 있다. C9 FinalValidation은 기존 SHA256 `FD3AF2A080BEF5D596E4A7FBA9B29C7239151C28DD163D30269C132159023613` 그대로 보존했다.

## 정리와 인계

세 소유 IDE가 모두 정상 종료했고 강제 종료는 없었다. 각 자기 pending 경로는 없으며 ThemeApply64 IDE/설치기 잔존 프로세스는 0개다. GUI 예약을 Root에게 반환했다. 기본 사용자 IDE 프로필·테마·실제 프로젝트·OS 설정에는 접근하거나 쓰지 않았다.

이 검사는 실제 IDE의 목적별 SDK/DTE API 통합 검사이다. 물리 포인터, 데스크톱 시각 검사, 고객 VS2017 15.7.1, 실제 AI 답변 성공을 증명하지 않는다. CLI 로그인과 계정 입력은 하지 않았다. Legacy SKU의 15.7.1 호스트 실검증으로 확대하지 않는다.

세 기존 테스트 소스·제품·C9 보고/JSON·Git·Release는 수정하지 않았다. 새 C11 보고와 증거를 동결해 Root/감사에게 인계한다. 최신 Release 제작·실제 배포·익명 공개 자산 13개 다운로드 검증은 Root가 이어간다.
