# TASK069 Role17 final5 실제 MEF 경로 검증

담당: CodexOrig 보조 개발 055 · 2026-10-20

VS2017 전용 실제 호스트에서 **146/146 PASS**, 예외 없음·드라이버 exit 0이다. 원래 118개 적용·팔레트·설정 보존 조건과 primary MEF 경로에 따른 최초 core 28조건을 모두 통과했다. 최초부터 성공해야 하는 조건을 유지했으며 뒤늦은 회복으로 초기 실패를 지우지 않았다.

증거는 `artifacts/task069theme/timing/actual-2017-final5-v1`이다. 집계는 원래 118개 이름/통과 여부, 모든 고정 원본·사본 및 실제 컴파일 소스 SHA 일치, 자기 PID 정상 종료를 재확인했다. helper 빌드는 오류 0이지만 아래의 경고 1개가 있다. 해시는 `CodexOrig_TASK-069_MefRouteValidation.json`에 기록했다.

## 정확한 후보와 색상 경로

Root가 승인한 **C10 원본과 동일한 DLL + final5 테마를 넣은 개인 probe VSIX**다. C10 원본의 DLL `6B97908EF35C474A351FE3FFE45A78D7630DF7E145F77EC651C567E9E677BDAA` 및 나머지 모든 ZIP entry body는 그대로이고 `Themes/Grokeen-VisualStudio-Themes-0.14.0-VS2017.vsix` 하나만 교체했다. 원본 패키지는 변경하지 않았다. `artifacts/task069theme/timing/package-probe/probe-manifest.json`이 원본/사본/테마 해시와 유일 변경 entry를 보존한다. 정식 C11 최종 후보 검사라고 주장하지 않는다.

064의 primary SDK 연구에 따라 actual MEF category `{75A05685-00A8-4DED-BAE5-E7A50BFA929A}`의 Foreground/Background와 비교했다. legacy `{FF349800-EA43-46C1-8C98-878E78F46501}` 값으로 자동 fallback하지 않는다. core 오류 4종과 breakpoint만 정의·opaque 채널·SolidColorBrush·대비를 엄격히 검사한다.

| 실제 키 / 채널 | 최초 실제 ARGB = final5 MEF raw 정의 | 실제 editor 배경 대비 |
|---|---|---:|
| syntax error / Foreground | FFFF79C6 | 5.969029 |
| compiler error / Foreground | FFFF79C6 | 5.969029 |
| other error / Foreground | FFFF79C6 | 5.969029 |
| compiler warning / Foreground | FFFFB86C | 8.357583 |
| MarkerFormatDefinition/BreakpointInScrollBar / Background | FFBD93F9 | 5.903096 |

core 채널은 모두 SolidColorBrush, Opacity=1, Transform/RelativeTransform identity다. suggestion=`00FFFFFF`, hinted suggestion=`FFA5A5A5`는 관찰만 했으며 기본 의도를 추정해 강제 비투명화하지 않았다. 초기 C10/final4 실행의 143 PASS/10 FAIL 원문과 그 legacy 비교·suggestion 가정 한계는 별도 보고/증거에 그대로 남겼다.

## 지연 경계와 옵션

첫 snapshot은 약 42ms에 이미 core 조건을 만족했다. ApplicationIdle barrier도 완료했고 약 51ms, 571ms, 2095ms, 7117ms의 추가 네 snapshot에서 같은 core 색을 유지했다. 추가 관찰 28개씩 112개는 진단 데이터로 분리했고 최초 146개 통과 수에 중복 합산하지 않았다. 초기 snapshot과 각 관찰의 기준별 통과/실패를 `dracula-format-map-timing.json`에 보존했다.

timing 전용 helper의 명시 `DispatcherPriority.ApplicationIdle` 관찰 한 줄에 **VSTHRD001 경고 1개**가 있다. 일반 UI 전환 대신 관찰할 queue 우선순위를 지정한 테스트 코드이며 제품 소스나 제품 DLL의 경고가 아니다. 원본 `helper-build.log`를 보존하고 경고를 숨기지 않았다. 초기 PrepareOnly 결과를 0 warning으로 안내한 것은 로그 tail만 확인한 오류였으며 이 보고와 WORK_STATUS에서 정정한다. 런타임 검사 성공과 helper 무경고 빌드는 별도이며 후자는 달성하지 않았다. 실제 실행 뒤 검사 소스를 바꾸지 않았다.

ShowErrors·ShowMarks는 true, ErrorMarginWidth·MarkMarginWidth는 6이다. 실제 OverviewErrorMargin·OverviewMarkMargin은 모두 등록·Enabled=true·Visible·MarginSize/ActualWidth=6이다. API로 옵션·format map을 설정하거나 cache를 강제로 clear한 일은 없다.

## 안전 경계와 최종 인계

실제 설치된 view별 editor format map/옵션/margin 조회를 검증했다. native 오류·breakpoint 태그를 생성하거나 물리 hover·화면 픽셀을 검사하지 않았다. `ActualErrorTagsCreated=false`, `ActualBreakpointTagsCreated=false`, `PhysicalMarkerVisibilityVerified=false`를 유지한다. 특히 lookup 성공을 사용자 프로젝트의 실제 태그 표시 성공으로 바꾸지 않는다.

전용 `ThemeApply69T44fe8c4b`의 자기 PID **56316**은 정상 종료했고 강제 cleanup은 없었다. 입력 불변 true, 실사용 IDE·기본 프로필·프로젝트·OS 포인터 변경 0이다. core 성공과 정리를 Root에 즉시 알리고 GUI 예약을 반납했다. 추가 IDE·소스 변경 없이 네 timing 소스를 동결한다. 이전 menu 354개와 effective 실패 증거/소스를 수정하지 않았다.

Root는 final5 테마를 정식 최종 후보에 통합한 뒤 네 SKU/설치기·전체 회귀·감사·최신 Release·실제 게시·익명 다운로드 SHA 검증을 마감해야 한다. 개인 probe의 성공은 최종 배포 후보의 검증을 대신하지 않는다.
