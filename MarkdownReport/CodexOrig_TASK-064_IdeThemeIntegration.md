# TASK-064: 설정의 IDE 테마 설치·적용 통합 검증

- 담당: CodexOrig 보조 개발 / Root TASK-054 통합 검증 지원
- 기준: Role.md 추가 요청 #16, 2026-10-20
- 상태: C8 실제 3IDE 및 C9 실제 2017·2019 sentinel 검사 완료. 선택 GUID·실제색·비기본 설정 보존을 확인했다. 원본 행수 equality FAIL은 보존하고 직렬화 추가와 값 변경을 별도 보고한다.
- 소유: `tests/task064`, `artifacts/task064`, 이 새 보고서만. 제품 소스·기존 감사·공유 빌드·Git·Release는 수정하지 않는다.

## 검증 설계

`Test-IdeThemeWorkflow.ps1`는 정확한 VS 설치 인스턴스를 vswhere로 확인하고 매 실행 새 `ThemeApply64` RootSuffix를 만든다. 선택한 C2 AI VSIX에 올바른 연도 독립 테마 VSIX가 포함되었는지 압축 항목과 SHA256으로 먼저 확인한다. 정식 VSIXInstaller의 `/quiet /rootSuffix /instanceIds`로 테마·제품·검사 helper만 설치하고 전용 프로필 구성을 갱신한다.

목적별 helper는 실제 DTE.RegistryRoot가 실행 설정의 예상 전체 경로와 정확히 같고 전용 suffix와 일치할 때만 설정 명령 또는 pending 파일 작성에 진입한다. 등록 제품 메뉴를 API로 실행해 실제 설치 제품을 로드하고 DLL 해시를 확인한다. 실제 마우스·키보드·NativeWindow·데스크톱 이미지 접근은 사용하지 않는다.

총 22개 수락 검사:

1. 정확한 자기 IDE 프로필/연도 및 설치된 제품 DLL SHA256
2. 설정 화면의 두 설치·적용 버튼, 기본 승인 권한에서 enabled 상태
3. 현재 지원 연도와 미지원 연도, 설치된 확장 옆 `Themes` 경로, bundled VSIX 해시
4. 의미 있는 비테마 설정 10개 이상의 실제 export 기준값
5. 잘못된 pending 요청의 예외 비전파·수동 복구 안내·기존 설정 보존
6. Kakao와 Dracula 각각 제품 적용 성공 상태, pending 소비, 실제 재export GUID, 비테마 설정 보존, 신규 임시 파일 미유출

검사는 제품의 비공개 `ApplyPendingIdeThemeOnLoad`를 실제 IDE에서 새 제품 control에 호출한다. 호출의 DTE는 실제 전용 IDE이며 import/export 명령은 교체하지 않는다. 설치 관리자 버튼의 실제 UI 클릭·설치 재시작·데스크톱 시각 변경은 이 하네스의 검사 결과로 주장하지 않는다. 설치·적용 파이프라인을 목적별 API로 검증한다.

공식 Microsoft 문서는 `Tools.ImportandExportSettings /import:filename`과 `/export:filename`을 정의하며, `/reset`은 현재 설정 초기화이다. 하네스 시작의 `/ResetSettings General`은 새 전용 RootSuffix에만 사용하고 제품 적용은 export 전후 비테마 `PropertyValue`를 비교한다. [Microsoft: Import and Export Settings command](https://learn.microsoft.com/en-us/visualstudio/ide/reference/import-and-export-settings-command?view=visualstudio)

## 준비 결과

- `artifacts/task064/prepare-2017-v3`: Release helper 빌드 오류 0 / 경고 0
- `artifacts/task064/prepare-2019-v3`: Release helper 빌드 오류 0 / 경고 0
- `artifacts/task064/prepare-2022-v3`: Release helper 빌드 오류 0 / 경고 0
- PrepareOnly는 제품·테마 설치, IDE 프로세스 시작, 사용자 테마 변경을 하지 않았다.
- 초기 v1은 `System.Xml` 참조 누락으로 컴파일 실패했고 v2는 shell 서비스 null 확인 경고 1건이었다. 둘 다 하네스 결함을 수정했으며 과거 실행 폴더는 보존했다.
- C2의 #16 실제 제품 파이프라인 검사, 실제 IDE별 수락 결과, 프로세스 정리 기록은 아직 없다. 따라서 준비 빌드 성공을 제품 기능 통과로 간주하지 않는다.

## 경계와 제한

현재 개발 PC에는 VS2017 15.9 / VS2019 / VS2022가 있다. 사용자 VS2017 15.7.1 PC의 실제 설치·적용 결과는 별도 미검증이다. 기존 기본 IDE 프로필과 현재 사용자 테마를 변경하지 않는다. 종료가 늦으면 자기 PID의 명령줄에 정확한 `/RootSuffix ThemeApply64...`가 있는지 검사한 뒤에만 해당 프로세스를 정리한다. 자기 프로필 pending 파일만 제거하며 생성된 증거와 전용 프로필은 삭제하지 않는다.

## C2 실제 VS2017 실패 재현

- `candidate2-2017-v1`: 설치 제품 및 bundled theme 경로/해시 등 8개 PASS 후 검사 하네스 baseline export 읽기가 공유 위반 IOException. DTE export 명령은 완전한 XML 작성 전에 반환할 수 있었다. 하네스를 bounded await/XML 완성 검사로 수정했다.
- v2/v3 helper 컴파일 실패는 generic Task 별칭과 async UI thread 진입 수정 중의 하네스 결함이며 제품을 실행하지 않았다. 해당 폴더는 보존했다.
- `candidate2-2017-v4`: 실제 IDE 22개 중 11 PASS / 11 FAIL, fatal exception 없음. 잘못된 pending 요청 예외 비전파·복구 안내 PASS. 두 정상 테마는 제품 성공 상태·pending 소비 실패, 실제 재export 모두 초기 `{A4D6A176-B948-4B29-8C66-53C97A1ED7D0}`로 유지. 제품 자동적용 실패가 실제 호스트에서 확인됐다. 임시 check export 2개도 남았다.
- 일반 설정 불변 3개 실패는 export 명령 자체가 AutoSaveFile의 경로 표현을 새 자기 suffix 경로로 정상화한 영향이었다. 이후 하네스에서는 warmup export 후 baseline export를 별도로 받아 모든 비테마 PropertyValue 비교를 유지한다. 설정을 통째로 제외하거나 제품 손상을 숨기는 필터는 추가하지 않았다.
- v4의 제품 status 전문은 기록하지 않아 공유 위반을 제품 실패의 직접 원인이라고 확정하지 않는다. Root에 증거를 인계했고 C3 수정 전 C2를 반복 실행하지 않는다. 후속 하네스는 제품 상태 전문과 bounded 비동기 완료 관찰을 추가했다.
- 두 실행 모두 소유 IDE 정상 종료, 강제 정리 없음. 기본 사용자 프로필은 변경하지 않았다. C3와 다음 실행 지시를 기다린다.
C2 보류·임시 파일 후속 정리: v4의 두 check export는 정확한 원래 경로·소유 run 시각 범위·실제 export와의 SHA256 일치를 확인한 뒤 recovered-temp 하위로 단일 파일 이동 보존했다. pending-temp-cleanup.json에 경로/해시/결과를 기록했으며 v1/v4 자기 pending은 이미 helper finally에서 삭제된 상태였다. 전체 temp 폴더나 다른 사용자 파일은 삭제하지 않았다. GUI 예약은 Root에 반환했다.

## C6 실패와 원인 분리

C6 실제 2017은 16/22였다. malformed·일반 설정 보존·임시 정리는 통과했지만 두 테마의 GUID는 바뀌지 않았다. 전용 hive를 KEY_READ로 확인한 결과 두 테마 모두 이름/GUID/70 categories/80,290 bytes가 정상 등록됐다. 직접 DTE 공개 General 속성에는 테마 setter가 없었고 테마 명령도 열거되지 않았다.

probe1은 제품 최소 XML 미적용과 전체 export GUID 변경 import 적용을 비교했다. 실제 export의 FontsAndColors Category는 `<Category name="Environment_Group" RegisteredName="Environment_Group">` 안에 있다. 제품 최소 XML에는 이 부모가 없었다. 전체 import는 다른 설정 초기화를 유발하므로 제품 해결책으로 사용하지 않았다.

probe2는 위 부모 Category의 속성과 FontsAndColors Category만 넣은 최소 XML을 실제 IDE로 import해 Kakao/Dracula GUID 모두 적용됨을 확인했다. 원래 테마도 같은 최소 XML로 복구됐다. 비테마 XML에는 Debugger/XAML 기본 설정의 직렬화 변화가 발생했고 원본·차이·실패는 보존했다. 이 결과를 Root에 인계해 C8 제품에 최소 wrapper를 반영했다. TASK064는 제품 코드를 수정하지 않았다.

## C8 실제 IDE 결과

| IDE | 전용 run | 원본 검사 결과 |
|---|---|---|
| 2017 15.9 | candidate8-2017-v2 | 23/25 PASS, 비테마 직렬화 비교 2 FAIL |
| 2019 16.11 | candidate8-2019-v1 | 23/25 PASS, 비테마 직렬화 비교 2 FAIL |
| 2022 17.14 | candidate8-2022-v1 | 25/25 PASS |

전체 원본 결과는 71/75이며 이를 전체 통과로 표기하지 않는다. 세 IDE 모두 실제 설치 DLL 해시/해당 연도 bundled VSIX/두 설치 버튼/지원 연도/잘못된 요청 방어/두 실제 선택 GUID/pending 소비/임시 정리가 통과했다. `VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey)`의 실제 호스트 색상도 Kakao `FFD9DDE3`, Dracula `FF282A36`로 각 컴파일 입력 XML의 해당 색과 일치했다. 이는 실제 호스트 API의 색값이며 데스크톱 시각 캡처는 아니다.

초기 export의 Debugger 기본값·XML 이름 정규화는 사용자 설정 손상으로 단정하지 않았다. 현재 테마 그대로 최소 XML을 import하는 no-op 단계를 제품 실행 전에 별도 기록하고, 정규화 후 baseline의 모든 비테마 PropertyValue를 비교한다. 특정 속성 제외 필터는 추가하지 않았다.

2017의 남은 차이는 기존값 변경·삭제 없이 XAML_Miscellaneous/AutoUpdateTags=True 행 1개 추가, 2019는 XAML Designer/General/DefaultZoomSetting=1 행 1개 추가이다(두 테마에서 같은 변화). 실제 공개 Debugging/General 속성 값은 전후 동일했다. TextEditor/XAML DTE 속성에는 AutoUpdateTags 자체가 노출되지 않았다. 따라서 원본 FAIL을 통과로 고치지 않았으며 읽기 전용 실제 store 진단을 준비했다.

2022에서는 ShellSettingsManager.GetReadOnlySettingsStore(UserSettings)의 전후 702 collections를 읽었고 탐색 상한에 도달하지 않았다. XAML AutoUpdateTags 저장 property는 전후 모두 없으며 비테마 XML도 완전히 유지됐다. 2017/2019에 같은 결과라고 추정하지 않는다. 최종 C9 2017 smoke 시 읽기 증거를 추가할 수 있다. [Microsoft: read-only settings store](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.settings.shellsettingsmanager?view=visualstudiosdk-2022)

각 실제 IDE는 정확한 RootSuffix와 PID를 확인해 정상 종료했으며 강제 종료는 없었다. 현재 사용자 기본 테마/프로필·실제 프로젝트·OS 설정은 변경하지 않았다. 제품 설치 버튼 UI 클릭, 실제 데스크톱 색상 검수, 고객 15.7.1 PC 설치는 이 결과의 범위 밖이다.
## C9 비기본값 최종 확인 및 동결

Root가 확정한 candidate9 제품을 다시 새 프로필에 정식 설치했다. C8의 원본 71/75를 수정하지 않았고 실패를 통과로 바꾸지 않았다.

| IDE | 최종 run | 원본 결과 | 비기본값 |
|---|---|---|---|
| 2017 15.9 | candidate9-2017-sentinel1 | 24/26 PASS, 행수 equality 2 FAIL | AutoUpdateTags=False |
| 2019 16.11 | candidate9-2019-sentinel1 | 26/28 PASS, 행수 equality 2 FAIL | DefaultZoomSetting=0 |

2019 helper에는 두 테마 후 sentinel 보존을 명시한 assertion이 추가되어 검사 개수가 다르다. 2017은 동일 검사를 저장된 실제 export 원본으로 별도 분석했다. 이 분석은 원본 result.json의 검사 수나 성공 여부를 수정하지 않는다.

`-SentinelSettings`는 보존된 해당 IDE의 실제 export category 구조를 복사해 대상 PropertyValue **1개만** 포함하는 최소 `.vssettings`를 만든다. 임의 registry 쓰기 없이 공식 DTE import를 자기 프로필에만 실행하고, 실제 재export에서 비기본값이 설정됐음을 먼저 검사했다. 그 뒤 제품을 실행했다. 2017의 AutoUpdateTags=False와 2019의 Zoom=0은 baseline·잘못된 요청 뒤·Kakao·Dracula에 그대로 유지됐다. 네 번의 테마 적용 후 비기본값 보존이 모두 확인됐다.

두 IDE의 모든 기존 비테마 PropertyValue를 비교한 결과 변경 0 / 삭제 0이다. 단, 새 행은 2017의 XAML_Miscellaneous 6개(True), 2019의 XAML Designer/General 9개가 각각 추가됐다. 정확한 이름과 값은 `candidate9-settings-analysis.json`과 각 run의 `settings-analysis.json`에 있다. 그러므로 직렬화 행 집합의 완전 일치라는 원본 검사는 여전히 실패한다. 추가 행을 필터에서 제외하거나 baseline의 미확인 값을 임의 기본값으로 치환하지 않았다. 추가된 모든 값의 이전 유효 상태까지 입증했다고 주장하지 않는다.

읽기 전용 UserSettings 진단은 2017에서 적용 전 141개, 적용 후 202개 이상의 collections를 읽었다(상한 미도달). 적용 뒤 `ApplicationPrivateSettings\Microsoft\VisualStudio\TextEditor\Xaml\Miscellaneous/AutoUpdateTags`의 실제 값은 `0*System.Boolean*False`였다. 적용 전에는 이 저장 property가 아직 없었고 비기본값은 export에서 확인됐으므로, 전후 store property 집합 자체가 같다고 주장하지 않는다. 2019의 Zoom property는 탐색한 XAML collection에 직접 노출되지 않았으며, 그 보존 근거는 실제 IDE export의 0이다.

두 C9 실행에서도 설치 DLL 해시·두 버튼·해당 연도 테마 패키지·지원 버전·malformed 복구·두 실제 GUID·호스트 ToolWindowBackground 색·pending 소비·임시 정리가 통과했다. 실제 OS/데스크톱 시각 검사로 확대 해석하지 않는다. 고객 15.7.1 PC는 계속 미검증이다.

연구 담당의 vendor getter/기본 metadata 증거는 `CodexOrig5_TASK-064_AutoUpdateTagsResearch.md`를 참고한다. TASK064는 실제 비기본값 보존과 기존 값 변경/삭제 없음의 독립 근거를 제공하며, 원본 strict FAIL에 대한 최종 감사 판정은 Root/감사 담당에게 인계한다.

소유 IDE는 모두 정상 종료했고 테스트 전용 pending과 임시 theme/check 파일은 정리됐다. GUI 예약을 Root에게 반환했다. 제품·공유 빌드·Git·Release는 수정/실행하지 않았다. Root가 최신 후보 빌드·전체 통합·Release 제작·배포·공개 다운로드 검증까지 이어가야 한다.