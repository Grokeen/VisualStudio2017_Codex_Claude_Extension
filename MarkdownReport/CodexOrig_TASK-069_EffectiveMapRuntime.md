# TASK069 Role17 실제 편집기 포맷 맵 진단 결과

담당: CodexOrig 보조 개발 055 · 2026-10-20

최종 C10 VS2017 정식 제품 VSIX와 final4 테마의 실제 편집기 검사에서 **143 PASS / 10 FAIL**, 예외 없음이다. 기존 118개 적용·팔레트·설정 보존 검사는 모두 통과했다. 추가 effective 포맷 맵 35개 중 25개 통과, 10개 실패다. 테마의 raw 리소스 값과 실제 편집기 포맷 맵의 오류 색이 달랐고 일부 대비가 부족했다. 실패를 완화하거나 원래 옵션·색을 바꾸지 않았다.

증거는 `artifacts/task069theme/effective/actual-2017-v2`다. `result.json`의 실패 이름, `dracula-effective-editor-format-map.json`의 실제 색/Brush/옵션/margin, 고정 입력 사본, 공식 SDK 참조 identity/SHA, 설치·정리 결과를 보존했다. 상세 해시는 `CodexOrig_TASK-069_EffectiveMapRuntime.json`에 기록했다. 집계에서 원래 118개 이름과 통과 여부, 입력/컴파일 사본 SHA, helper 컴파일 경고·오류 진단 0을 다시 확인했다.

## 실제 오류 포맷 색 불일치

자기 XAML 문서의 실제 WPF view에 연결한 `IEditorFormatMap`에서 읽었다. 편집기 배경은 `FF282A36`이다. ErrorMargin이 읽는 Foreground와 MarkMargin이 읽는 Background를 동일하게 조회한다.

| 실제 키 / 채널 | 실제 ARGB | final4 raw 정의 | 실제 배경 대비 |
|---|---|---|---:|
| syntax error / Foreground | FFFF0000 | FFFF79C6 | 3.561457 |
| compiler error / Foreground | FF0000FF | FFFF79C6 | **1.657312** |
| other error / Foreground | FF951795 | FFFF79C6 | **1.922085** |
| compiler warning / Foreground | FF008000 | FFFFB86C | **2.771907** |
| suggestion / Foreground | 00FFFFFF | 명시 raw 정의 없음 | **0: 투명** |
| hinted suggestion / Foreground | FFA5A5A5 | FFB9B9C4 | 5.781090 |
| MarkerFormatDefinition/BreakpointInScrollBar / Background | FFBD93F9 | FFBD93F9 | **5.903096: 일치** |

실패 10개는 raw 정의 불일치 5개, compiler error/other error/warning/suggestion 대비 부족 4개, suggestion 채널이 opaque하지 않음 1개다. suggestion에 가상의 raw 기대값을 추가하지 않았다. 나머지 실제 채널은 SolidColorBrush·Opacity=1·Transform/RelativeTransform identity이며, suggestion Brush도 Opacity는 1이지만 Color alpha가 0이다.

## 표시 옵션과 margin은 정상

- `OverviewMargin/ShowErrors` 및 `ShowMarks`: 모두 true.
- ErrorMarginWidth 및 MarkMarginWidth: 모두 6.
- 실제 OverviewErrorMargin 및 OverviewMarkMargin: 등록됨·Enabled=true·Visibility=Visible·MarginSize=6·ActualWidth=6.

따라서 이번 private 기본 프로필에서 오류 색상 문제를 단순한 표시 옵션 off나 0 폭으로 설명할 수 없다. raw 테마 리소스 검사 통과만으로 실제 editor 포맷 소비 색상까지 반영됐다고 볼 수 없음을 확인했다. raw 정의가 실제 map에 반영되지 않은 상세 원인이나 실제 오류 태그 표시 실패까지 확정한 것은 아니다.

## 안전 경계와 인계

첫 C8 실행의 하네스 Assembly.Load 이름 오류는 `actual-2017-v1`에 그대로 보존했다. 이후 effective 사본에만 공식 SDK full identity Reference를 고정했고, 이 C10 실행에서 실제 view·포맷 맵·옵션·margin 조회까지 완료했다. 제품 dependency나 공용 테스트 템플릿은 수정하지 않았다.

설정 export와 pending 적용은 기존 검사 범위이며, 추가 probe가 options 또는 format map 값을 수정한 일은 없다. 실제 오류·breakpoint 태그를 생성하거나 물리 hover·화면 픽셀을 검사하지 않았다. `ActualErrorTagsCreated=false`, `ActualBreakpointTagsCreated=false`, `PhysicalMarkerVisibilityVerified=false`를 유지한다. native 태그/화면 표시 통과로 보고하지 않는다.

전용 RootSuffix `ThemeApply69E8222aa7e`의 자기 PID **102388**은 정상 종료했고 강제 cleanup은 없었다. InputsUnchanged=true, 실사용 IDE·프로젝트·기본 프로필 변경 0, OS 포인터 0이다. Root에 실패와 구체 색/대비를 즉시 전달하고 GUI 예약을 반납했다. 이후 다른 IDE나 재실행을 시작하지 않았고 소스도 동결한다. Root가 실제 오류 formatting 개선 필요성을 판단하고 최종 감사·Release·게시 전에 이 미해결 요구를 명시적으로 처리해야 한다.

## 후속 해석 경계: category와 지연 갱신

10개 assertion 실패를 10개 제품 버그로 해석하지 않는다. 초기 helper는 공급한 legacy `Text Editor Text Marker Items` category `{FF349800-EA43-46C1-8C98-878E78F46501}`의 raw 의도색과 실제 map 값을 비교했다. 이후 064의 설치된 세 SDK primary IL 연구에서 actual MEF font/color category는 `{75A05685-00A8-4DED-BAE5-E7A50BFA929A}`임을 확인했다. raw theme category와 view별 map 사이의 자동 bridge를 초기 검사가 증명한 것은 아니다. 원래 raw 정의와 관찰값 불일치는 사실이지만 올바른 공급 경로를 수정할 근거를 별도로 검토해야 한다. 연구 증거는 `artifacts/task069theme-research/effective-mef-route-1/evidence.json`, 기존 정의 GUID 분석은 `artifacts/task069theme/effective/raw-map-guid-analysis.json`이다.

또한 suggestion은 raw 정의가 없고 투명 기본값이 의도된 동작일 수 있으므로 이 두 실패만으로 비투명화가 필요하다고 결론내리지 않는다. core compiler error/other error/warning의 3개 대비 부족은 실제 읽은 값에 대한 관찰이지만, 해당 native 태그 생성/픽셀 표시는 여전히 검사하지 않았다.

제품은 import 뒤 최소 250ms UI yield와 export Theme GUID 일치로 완료를 보고한다. helper는 완료 상태·추가 export·팔레트 조회 뒤 자기 문서를 열고 300ms UI yield 후 map을 읽었다. 명시 ApplicationIdle/FormatMappingChanged 완료나 FontsAndColors cache 갱신 계약은 없다. 따라서 지연 반영 가능성도 아직 배제하지 않는다. Root 지시에 따라 별도 `tests/task069theme/timing` 사본에 75A exact category 기준의 core 4개 오류/경고·breakpoint 검사와 초기/idle/시간별 관찰만 준비했으며, 초기 실패를 후속 회복으로 삭제하지 않는다. 옵션/포맷 쓰기·강제 cache clear는 없다. 실제 후속 실행은 Root 명시 예약과 정확한 테마 입력 전까지 시작하지 않는다.
