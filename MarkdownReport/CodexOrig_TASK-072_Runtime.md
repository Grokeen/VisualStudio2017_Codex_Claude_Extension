# TASK072 실제 편집기 검증 결과

CodexOrig 보조 개발 055, 2026-10-23. **최종 C5/themes8을 실제 VS2017·2019·2022에서 각각 273개, 총 819/819 통과했다.** 세 테스트용 IDE 모두 정상 종료했고 강제 종료는 없었다. 동일 V11 도우미 소스 6개, 제품·테마 패키지, 정의·SDK 고정 입력과 C5 제품 입력 160개가 모두 불변이다. 이 결과는 네이티브 편집기 검사 범위의 완료이며 전체 제품 게시 완료를 뜻하지 않는다.

| 실제 호스트 | 통과/실패 | 소유 PID | 정상 종료 | 입력·패키지 불변 |
| --- | ---: | ---: | --- | --- |
| VS2017 | 273/0 | 45996 | true | true |
| VS2019 | 273/0 | 84724 | true | true |
| VS2022 | 273/0 | 63404 | true | true |

실행 순서는 VS2022→2017→2019였다. Root 예약 안에서 전용 `ThemeApply72` 프로필만 사용했고 세 PID가 모두 사라진 뒤 GUI 예약을 반환했다. [최종 집계](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task072/runtime-c5-aggregate.json), [제품 입력 160개 검증](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task072/c5-product-inputs-after-native055.json), [프로세스 종료 확인](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task072/c5-native-owned-process-final055.json).

실제 제품은 `artifacts/task072/candidate5/{year}/Release/VS2017.AIAssistant.vsix`, 테마는 `artifacts/task072/themes8/Grokeen-VisualStudio-Themes-0.14.2-VS{year}.vsix`다. 각 실행은 설치 DLL SHA와 제품에 동봉된 테마 SHA까지 확인했다. 세 제품·세 테마 패키지의 실행 전/후 SHA는 [패키지 기준 스냅샷](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task072/c5-package-inputs-before-runtime.json)과 최종 집계에 있다. Root가 제공한 C5의 최종 PDF SHA는 `155DF0B5DD2C2C4AB958FE09958853C73A7C8EE1DBED0AB7022E21E18FF5B0DE`이며 PDF 시각 검사는 이 하네스 범위가 아니다.

실제 diff는 익명 120행의 상단·중간·하단 변경을 native IDifferenceBuffer/IWpfDifferenceViewer로 만들었다. SideBySide의 정확 RightHost에서 원래 구현이 사용하는 추가·삭제 word 전경 brush, overview 배경, viewport pen과 실제 Drawing을 읽었다. 추가 초록 `50FA7B`·삭제 분홍 `FFB3D9`, viewport 보라 `BD93F9`, 텍스트 대비 4.5 이상·변경 표식 대비 3 이상을 통과했다. 실제 변경 Y는 VS2017/2019 `[19,268,550]`, VS2022 `[19,261,535]`였다. 가짜 도형을 그리거나 native OnRender를 직접 호출하지 않았다. [설치된 세 구현의 소비 채널 IL](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task072/diff-primary-contracts.json), [버전별 manager 필드 IL](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task072/diff-manager-host-primary.json).

실제 C# 언어 서비스가 `broken = ;`에 만든 오류 태그와 custom error marker의 span 겹침을 확인했다. marker는 분홍 전경·1px 분홍 테두리이며 불투명 Background/Fill이 없어야 통과한다. 세 버전 모두 실제 editor Drawing에서 분홍 pen을 확인했고 자기 익명 VisualElement PNG를 저장했다. 테두리 뒤 bounded ApplicationIdle/Render를 거쳐 읽었으며 format map 쓰기·수동 adornment·OS 캡처는 없었다. VS2022 코드와 diff PNG도 직접 확인했다. [VS2022 코드 PNG](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task072/runtime/actual-c5-2022-v1/native-csharp-owned-view.png), [VS2022 diff PNG](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task072/runtime/actual-c5-2022-v1/native-difference-owned-view.png).

주석은 `//`, XML 문서 본문·태그, 한 줄·여러 줄·빈 `/* */`, XML `/** */`, 주석처럼 보이는 문자열을 실제 native 태그와 최종 view 분류로 검사했다. 일반 `A0A6B5`, 블록 `B5BDD4`, XML 본문 `C8D5F2`, XML 태그 `B9C8E8`를 통과했고 블록 overlay의 XML/string 누출은 없었다. multiline 내부 query도 native full span의 원래 블록 시작을 유지했다. VS2017/2019는 native-buffer-classifier, VS2022는 own Block 타입을 제외한 raw native view tags가 원천이었다. VS2022의 실제 buffer 분류 0은 숨기지 않고 보존했으며 최종 combined classifier에서 native 정책을 역추론하지 않았다.

실제 현재 테마는 public ServiceProvider의 서비스 GUID와 public ISettingsManager의 `Microsoft.VisualStudio.ColorThemeNew`/`Microsoft.VisualStudio.ColorTheme`를 읽어 정확 Dracula GUID와 비교했다. 오래된 General 경로의 부재는 진단으로만 남긴다. VS2022도 New 키의 실제 GUID가 일치했다. [공식 ServiceProvider API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.serviceprovider.getservice?view=visualstudiosdk-2022), [공식 settings getter](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.settings.isettingsmanager.getvalueordefault?view=visualstudiosdk-2022).

두 실제 view에서 private parent false·local false·상속 false를 구분했다. 실제 제품 handler로 Dracula→Kakao→Dracula를 전환하고 옵션 복원·중간 사용자 변경 보존·view 독립·consumer 2개 중 1개 해제 후 나머지 유효·재획득·view close 시 실제 session 종료를 모두 통과했다. 활성 키는 VS2017 2개, VS2019/2022 3개였다. `Diff/View/ShowDiffOverviewMargin`은 VS2017에서 미정의여서 쓰지 않았고 2019/2022에서 실제 view-local로 활성화됐다. helper 부모/view 쓰기는 2017 4/4회, 2019/2022 6/6회로 별도 기록했다. 글로벌 옵션 쓰기는 0, 최종 글로벌 값은 원문 그대로다.

큰 파일은 실제 익명 C# 프로젝트의 1,950,047 bytes/50,003행이다. 최대 15초 native/custom/최종색 readiness를 관찰하고 사전 직접 query 횟수를 보존한 뒤 2KB query 4회를 계측했다. 각 동기 query에는 독립 10초 watchdog을 유지했다. 이번 세 호스트는 readiness 각 1회였고 약 21.15/9.86/12.94ms였다. 계측 4회마다 실제 native comment·custom 태그·최종 custom 분류가 53개씩 있었고 정확 블록색, dispatcher heartbeat, 소스 불변을 통과했다. VS2022는 buffer count 0/view native count 53을 별도 기록했다. 첫 계측 phase는 `first-recorded-after-native-readiness`다. 프로세스 냉시작·첫 native query·다른 버전 대비 성능 향상이라고 주장하지 않는다.

V11 소스는 [prepare-v11-manifest.json](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task072/prepare-v11-manifest.json)에 고정했다. 세 호스트 개인 컴파일 오류는 0이었다. 도우미 VSTHRD001 경고는 버전마다 2개이며 기존/추가 ApplicationIdle 진단을 숨기지 않았다. VS2022 MSB3277 충돌 7묶음·상세 진단 641줄도 별도 보존했다. 제품 4SKU의 0 warning/0 error 빌드와 도우미 경고를 혼합하지 않는다. 공유 제품 bin/obj 빌드와 제품 파일 편집은 수행하지 않았다.

이전 실패는 덮어쓰지 않았다. C4의 731 통과·21 실패 원본 집계와 작성기는 별도 사본/SHA로 보존했다. C4 VS2022의 4개 diff consumer 실패는 실제 `_brushManager` 필드명 차이였고, 나머지는 buffer 부재·view-only native 주석 원천과 관련됐다. C5 제품의 guarded view fallback과 V11의 원 native view 관측으로 새 후보에서 통과한 결과이며 C4 원본을 성공으로 정정하지 않았다. C1/C2/C3의 solution fixture·알파 배경·native diff 비활성 실패, 그때 소스·PNG도 남겼다. [C4 보존 SHA](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task072/c4-aggregate-preserved-before-c5-final/preserved-sha256.json).

검증 한계는 현재 설치된 세 IDE 호스트와 자기 WPF VisualElement 렌더 범위다. 별도 VS2017 15.7.1 PC 실행이나 물리 모니터·포인터 검사가 아니다. 사용자 기본 프로필·계정·사용자 문서는 변경하지 않았다. 최종 main IDE 4SKU 검사·Orig2 감사·최신 Release 제작·실제 게시·익명 다운로드 해시 확인은 Root가 이어서 진행한다. 본 담당은 소스와 GUI를 동결하고 해당 후속 절차가 필요함을 인계했다.