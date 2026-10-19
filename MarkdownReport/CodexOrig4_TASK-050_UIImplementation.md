# TASK-050 · Role 23:09 UI implementation

2026-10-19 KST · CodexOrig4

## Scope and design

I reviewed Role.md and opened images 44–55. The main requirement is visual consistency, so the work keeps existing app-wide controls and color resources as the shared baseline. The common `AssistantDialog` now has one surface header, body spacing, footer divider and action-button sizing. Settings use a left page navigator and show one settings section at a time. Closing the modal restores every original section control. The left navigation supports arrow, Home and End keys. Guide detail returns from a button placed at the upper left, outside the scrolling article.

The Markdown editor opens within the available work area, with a larger 1040×780 ceiling. Its toolbar has six labeled groups—document, edit, formatting, insert, task list and view—and search checkboxes use the shared `AssistantTheme` style. Claude/Codex provider tabs retain their native WPF `TabControl`, add directional keyboard navigation, larger spacing and a visible keyboard-focus cue.

The existing `auto` / `light` / `dark` mode setting remains intact. A separate `VisualThemePreset` choice adds `vs`, `kakao` and `dracula`, without changing Visual Studio’s own global theme. Kakao and Dracula presets update only the extension resource dictionary. High Contrast uses the host palette in place of these presets. Existing save/cancel transactions include the palette and completion-notification preference; Root is adding the corresponding notification getters/restoration and successful-save commit hook.

## Palette references

Kakao’s #FEE500 button surface and #191919 label come from the [Kakao Developers login design guide](https://developers.kakao.com/docs/en/kakaologin/design-guide). This is a Kakao-inspired extension palette, not a claim that Kakao publishes an application-wide editor theme. Dracula’s UI colors follow the [official Dracula specification](https://draculatheme.com/spec), including #282A36 background, #F8F8F2 foreground, #44475A selection, #8BE9FD cyan, #50FA7B green, #FFB86C orange, #FF5555 red and #BD93F9 purple. A [separate Visual Studio package](https://draculatheme.com/visual-studio) is owned by Root. Built-in and user-installed VS themes continue through the extension’s existing automatic host-color detection described in [Microsoft’s Visual Studio personalization guide](https://learn.microsoft.com/en-us/visualstudio/ide/personalizing-the-visual-studio-ide?view=visualstudio).

## Verification and remaining integration

`tests/task050/Test-ThemePresets.ps1` compiled `AssistantPalette.cs` and `AssistantThemePolicy.cs` with the VS2017 C# 7 compiler in an isolated source-only fixture. Kakao/Dracula light/dark role contrasts and UI wiring checks passed **55/55**. Evidence: `artifacts/task050/theme-presets-4/result.json`. Initial harness checks that used a Korean string literal failed under Windows PowerShell 5.1 script decoding; those checks were replaced with an ASCII group-count assertion. Their original results remain in `theme-presets-1/` and `theme-presets-2/`; corrected `theme-presets-3/` passed 51/51, then the expanded suite passed 55/55.

The XAML files parse as XML and the changed source paths pass `git diff --check`. A private VS2017 Release copy build found and fixed one designer compile omission: `System.Windows.Controls.Primitives` was missing for the settings navigation’s `ToggleButton` handler. After Root/Code landed notification and URL-list integration, the isolated VS2017 project built with no errors and produced a DLL and VSIX. The DLL SHA256 is `81775A7A32024CB2D4B13C2A5EF94768F9EEC050C9C2CA37A080C8B650FC03AA`. Five analyzer warnings remain in this pre-final build: four `VSTHRD010` UI-affinity warnings at notification/settings callers and one `VSTHRD001` in the existing Viewport path.

`tests/task050/Test-UiLayout.ps1` and `UiLayoutChecks.cs` ran against that isolated DLL; actual WPF objects verified provider keyboard navigation/accessibility, popup chrome, left-side page switching and restoration, six Markdown toolbar groups, common CheckBox styles, and notification choice load/restore **14/14**. Evidence: `artifacts/task050/ui-layout-4/result.json`; WindowShown=false, no IDE theme changed, no profile credentials or network request. This validates a personal source copy, not an installed customer build. Actual Visual Studio IDE use, balloon display, multi-SKU final candidates, independent audit, VS theme packages and publishing remain with Root.

## 통합 candidate2 UI/테마 재검증 / 2026-10-20 KST
Root의 동결된 candidate2 123입력 후보 DLL을 대상으로 2017, 2017 Legacy, 2019, 2022 실제 제품 assembly를 각각 별도 WPF fixture에 로드했다. 공통 팝업·설정 좌측 탭과 전환·복원·제공자 키보드 탐색·Markdown toolbar/checkbox 검사 각 14/14, 총56/56 통과했다. assembly SHA 및 원문 근거는 [검증 JSON](../artifacts/task050/candidate2-ui-theme-review.json)과 `artifacts/task050/ui-candidate2-{sku}/result.json`에 보존했다. WindowShown=false, IDE 전역 테마 변경=false, 계정 자격증명 접근=false, 네트워크 요청=false.

VS2017 통합 DLL에서 저장 설정을 격리해 Kakao light/dark와 Dracula light/dark 팔레트 적용을 확인했다. 실제 배경·전경·강조색·강조 위 글자색이 기대값과 일치해 4/4 통과했다. 개별 근거는 `artifacts/task050/runtime-candidate2-{kakao|dracula}-{light|dark}/result.json`이다.

Visual Studio 테마 XML은 Kakao의 노랑 #FEE500와 검정 #191919, Dracula의 표준 어두운 표면과 보라/청록/녹색 강조가 각자 일관된 색상 계열을 이룬다. 다만 후보3 정의 토큰 범위가 Kakao 65 categories/2696 color definitions, Dracula 80/2470이며 Kakao에만 있는 category는 14개, Dracula에만 있는 category는 29개다. 한쪽 테마에서 정의되지 않은 IDE 기능은 host/default 색을 상속할 수 있으므로 출시 전 두 정의에 같은 category/token 기준선을 적용하거나 실제 fallback을 IDE에서 검증할 필요가 있음을 Root에 보고했다. 세 개 SKU VSIX는 모두 compiler warning/error 0, 실제 IDE 설치는 하지 않았다.

추가 문서 정합성: package 내 `THIRD-PARTY-NOTICES.md`는 PlainDark.xml/Tan.xml 원본이 `Baselines/`에 포함됐다고 말하지만 candidate3 패키지 트리에는 그 경로가 없고 파일은 task051/theme-research 아래에만 있다. MIT license 파일은 패키지에 포함되어 있다. 보존 위치 문구를 수정하거나 고지대로 원본을 추가하도록 Root에게 전달했다.
## Visual Studio 테마 candidate4 재검토 / 2026-10-20 KST
Root가 candidate3 category mismatch를 반영해 준비한 candidate4를 재검토했다. Kakao와 Dracula XML은 이제 동일한 PlainDark 원본 기반이며 category 80개와 색 역할 정의 2,470개가 정확히 같고 누락/추가 key가 없다. Kakao는 #FEE500/#191919와 연한 중립면을, Dracula는 #282A36/#F8F8F2와 보라·청록 계열 강조를 사용한다. `CT_SYSCOLOR` 6개는 두 정의에서 유지되어 원본의 ARGB/system index 정규화 메모와 일치한다. 컴파일 결과 JSON에서 VS2017/2019/2022 세 VSIX 모두 오류/경고 0이다. 라이선스 고지는 원본 XML 보존 위치를 source repository로 고쳐 candidate3에서 발견된 문서/패키지 경로 차이를 해소했다.

후속 evidence: [candidate4 테마 검사](../artifacts/task050/themes-candidate4-review.json). IDE에서 실제 설치·전환한 화면은 아직 확인하지 않았다. Visual Studio 기본 테마 host fallback 및 설치 결과 검증과 구분한다.
## 통합 candidate3 UI 최종 재검증 / 2026-10-20 KST
C2 대비 UI 소스 변경이 없다고 전달됐지만, 고정 candidate3의 네 DLL에 해당 SKU의 정확한 `obj/project.assets.json`을 연결해 fixture를 재실행했다. VS2017, VS2017 Legacy, VS2019, VS2022 모두 14/14 통과(총56/56, 실패0). Assembly SHA, AssetsPath, 개별 근거는 [candidate3 결과 요약](../artifacts/task050/candidate3-ui-layout-review.json)에 기록했다. WindowShown=false, VisualStudioThemeChanged=false, PersonalCredentialsRead=false, NetworkRequested=false. 이 검사는 제품 UI 객체를 로드한 숨김 WPF fixture이며 실제 IDE에서 띄운 시각 검증은 아니다.
## 최종 candidate4 UI 확인 및 TASK-050 소유권 인계 / 2026-10-20 KST
고정 candidate4의 123입력 제품 후보에 대해 요청받은 정확한 SKU별 `obj/project.assets.json`을 연결해 WPF fixture를 실행했다. 2017/2017 Legacy/2019/2022가 각각 14/14(총56/56, 실패0)이다. 각 DLL SHA, SDK assets path, 빌드 경고·오류와 개별 결과는 [최종 candidate4 인계 JSON](../artifacts/task050/candidate4-final-ui-handoff.json)에 보존했다. window/IDE 전역 테마/network/credential 사용은 없었다.

candidate3→candidate4에서는 UI 레이아웃·팔레트 동작 소스가 그대로다. AssistantControl.Dialogs.cs의 파일 hash는 Dispatcher guard 주석 1줄 추가로 달라졌고 구현 차이는 없다. candidate4 확인으로 CodexOrig4 TASK-050 UI 및 테마 소유권을 Root TASK-051에 최종 반납한다. 그 뒤 별도 제품/UI/theme 수정을 진행하지 않고 새 발견은 제안으로 전달한다. Root가 계속할 실제 전용 IDE 세 SKU 및 배포 검증과 본 fixture 결과를 혼동하지 않는다.
## R53-02 공통 테마 캡션 보완 / 2026-10-20 KST
image45의 초록 OS 제목 표시줄을 재현 원인으로 확인해 공통 `AssistantDialog`에 WindowChrome 사용자 캡션을 추가했다. 캡션은 AssistantSurface/Foreground/Border 리소스에 연결되며 기존 `Window.Title`을 화면과 UI Automation 이름에 함께 바인딩한다. 캡션에 최소화·최대화/복원·닫기 버튼을 두고 WindowChrome hit testing을 활성화했다. Alt+F4 표준 닫기 command, 표시 상태 제어, resize 경계, 기존 Owner와 modal Window 의미를 보존한다. 기존 본문 제목을 캡션 제목으로 통합하고 설명 문구는 본문 상단에 유지해 OS·본문 중복 제목을 제거했다. DWM/레지스트리와 시스템 테마는 바꾸지 않으며 제품 파일 추가가 없어 csproj Compile 등록은 필요 없다.

독립 비교 fixture는 고정 C4의 SingleBorderWindow/WindowChrome null 기준을 3/3로 확인했다. 업데이트된 `AssistantDialogs.cs`는 개인 격리 VS2017 Release copy에서 빌드됐고, custom caption fixture 13/13 및 기존 TASK-050 공통 UI fixture 14/14가 통과했다. 해시·C4 비교·모든 결과는 [R53-02 인계 JSON](../artifacts/task050/r53-02-caption-handoff.json)과 `artifacts/task050/caption-native-baseline-c4-3/`, `caption-themed-final/`, `ui-caption-final/`에 남겼다. 대화상자는 표시하지 않았고 소유자 판단 검사를 위해 투명·오프스크린 fixture owner를 잠깐 표시 후 숨겼다. 실제 IDE GUI는 열지 않았다. Candidate C5의 전체 SKU Release 재빌드·감사 및 3 IDE 확인은 Root TASK-051 소유로 인계한다.
## 최종 candidate5 캡션·기본 UI 검사 / 2026-10-20 KST
Root가 고정한 candidate5의 네 SKU Release DLL을 각각 해당 candidate5 `obj/project.assets.json`에 연결했다. 공통 캡션 fixture는 각13/13, 기존 팝업·탭·설정·Markdown UI 회귀 fixture는 각14/14 통과했다(캡션52/52, 기본 UI56/56, 실패0). 네 Release 빌드 결과도 error0/warning0다. DLL SHA와 SKU별 검증 입력·근거는 [candidate5 최종 UI 인계 JSON](../artifacts/task050/candidate5-final-caption-ui-handoff.json)에 보존했다. 캡션 대상은 표시하지 않았으며 theme/DWM, 네트워크 및 사용자 자격 증명을 건드리지 않았다. 소유자 경계 확인을 위한 투명한 1×1 오프스크린 fixture owner만 표시 후 숨겼다.

candidate5에 대한 UI/캡션 확인을 마치고 테스트/디자인 변경 소유를 Root TASK-051에 반납한다. 남은 작업은 Root의 R53-02 독립 감사 재확인, 실제 전용 IDE의 세 SKU 검사, 최신 Release 패키징과 배포다. 본 hidden WPF fixture 통과를 실제 IDE 화면 캡처나 설치 결과로 주장하지 않는다.