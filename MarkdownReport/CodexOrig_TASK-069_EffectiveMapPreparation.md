# TASK069 Role17 실제 편집기 포맷 맵 검사 준비

담당: CodexOrig 보조 개발 055 · 2026-10-20

기존 354개 실제 테마 팔레트 검사 원본은 동결 상태를 유지했다. 별도 `tests/task069theme/effective` 사본에서 자기 XAML 문서의 공개 editor API를 읽는 후속 검사를 준비했다. 아래 초기 준비와 후속 실제 시도를 구분하며 **effective 소비나 화면 표식 가시성을 통과했다고 주장하지 않는다.**

VS2017·2019·2022 SDK별 private helper PrepareOnly 빌드는 모두 성공했고 경고·오류 진단이 없었다. 자기 artifact 출력만 생성했으며 IDE 시작·확장 설치는 없었다. `artifacts/task069theme/effective/prepare-<year>-v2`가 준비 결과와 컴파일 입력 사본을 보존한다. 설치된 각 호스트의 public assembly를 ReflectionOnly로 읽어 필요한 6개 property signature를 확인했다(합계 18개). 검사 대상 method 호출은 없었다. 해시는 `artifacts/task069theme/effective/preparation-summary.json`에 기록했다.

후속 실제 검사 계획은 다음과 같다.

- 원래 118개 assertion을 유지하고 final Dracula 적용 뒤 자기 `private-xaml-baseline.xaml` 문서만 연다. 실제 `IVsTextManager.GetActiveView` → MEF `IVsEditorAdaptersFactoryService.GetWpfTextViewHost` → view별 `IEditorFormatMapService.GetEditorFormatMap`으로 연결한다.
- 유효한 error type 6개와 `MarkerFormatDefinition/BreakpointInScrollBar`의 실제 ResourceDictionary 채널을 읽는다. 오류는 Foreground, breakpoint는 Background다. 채널 값 타입/색/키 목록을 남기고, 명시 raw 정의가 있으면 정확히 비교한다. final4에 명시 raw 정의가 없는 `suggestion`은 실제 채널/대비만 관찰하며 가상의 정의를 만들지 않는다.
- 자기 실제 editor 배경과 채널의 대비를 3 이상으로 확인한다. ShowErrors/ShowMarks가 켜졌는지, ErrorMarginWidth/MarkMarginWidth가 양수인지, 실제 OverviewErrorMargin/OverviewMarkMargin 등록·Enabled·size를 확인하고 Visibility와 ActualWidth도 기록한다.
- 옵션이나 format map 값을 수정하지 않고, 오류·breakpoint 태그도 합성하지 않는다. 실제 태그 표시·물리 hover·화면 가시성은 이 검사에서도 별도 미검증이다. API 부재·타입 불일치·빈 채널은 예외/실패로 남긴다.

실제 검사에는 Root가 메인 IDE 게이트를 마친 후 명시한 GUI 예약과 정확한 최종 제품·테마 패키지가 필요하다. 현재 추가 IDE를 시작하지 않는다. 후속 실행하더라도 기본 사용자 프로필이나 실사용 IDE는 건드리지 않으며 기존과 같은 소유 RootSuffix/PID 정리·원본 해시 가드를 유지한다.

## 첫 VS2017 실제 시도와 보존한 실패

Root가 한 호스트 예약을 허용하여 `actual-2017-v1`을 canonical C8 VS2017 제품 VSIX와 final4 테마로 실행했다. 공식 테마·제품·helper 설치 exit code는 모두 0, 실제 적용·팔레트 assertion 116개가 통과하고 assertion 실패는 0이었다. 새 helper의 `Assembly.Load` 단순 이름 조회에서 `Microsoft.VisualStudio.Editor` FileNotFoundException이 발생하여 이후 편집기 포맷 맵과 옵션 검사는 실행되지 않았다. Dracula 뒤의 원래 설정 보존·temp 정리 두 assertion도 미실행이므로 기존 118개 완료로 계산하지 않는다. 실제 map/옵션 결함이나 제품 결함으로 판정하지 않는다.

`artifacts/task069theme/effective/actual-2017-v1/result.json`과 progress 원문·고정 입력 사본을 보존한다. 자기 PID **106532** / `ThemeApply69E47807966`은 정상 종료했고 강제 종료는 없었다. 입력 불변 true를 확인하고 Root에 예약을 즉시 반납했으며 나머지 호스트나 재실행은 시작하지 않았다.

Root의 후속 지시에 따라 effective 사본에만 공식 SDK 8개 Reference를 추가했다. 설치된 공식 DLL의 전체 identity/SHA를 확인하고 artifact 사본을 고정하며 `SpecificVersion=true`, `Private=false`, `IncludeInVSIX=false`를 유지한다. `Api`는 해당 compile-time SDK `typeof(...).Assembly`를 사용한다. 제품 dependency나 공용 테스트 템플릿은 변경하지 않았다. `prepare-2017-v4` 컴파일 성공·경고/오류 진단 0이며 `binding-preparation.json`은 실제 생성된 AssemblyRef 전체 identity와 helper VSIX 내 공식 SDK DLL 동봉 0을 기록한다. 이 수정본도 실제 실행 전 상태다.

연구 담당 064의 실제 margin IL에서 채널 값은 `Brush`로 소비됨을 확인하여, 새 후속 assertion은 Color 채널을 관찰만으로 합격시키지 않는다. SolidColorBrush·Opacity=1을 별도 검사하고 Transform/RelativeTransform identity도 기록한다. 옵션 off/width 비양수와 margin 미등록·비활성은 원래대로 실패이며 설정을 변경하지 않는다. Root 최종 후보의 명시 예약 전 추가 IDE는 시작하지 않는다.
