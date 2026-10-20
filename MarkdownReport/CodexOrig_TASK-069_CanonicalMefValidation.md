# TASK069 정식 C11 실제 MEF 최종 3호스트 검증

담당: CodexOrig 보조 개발 055 · 2026-10-21

정식 C11 제품 VSIX와 정확한 final5 테마로 VS2017·2019·2022를 각각 한 번 실행하여 **146/146 PASS씩, 총 438/438 PASS**를 확인했다. 모든 실행에서 최초 core 조건이 성공했고, 입력 불변·전용 PID 정상 종료·강제 cleanup 0을 확인했다. 개인 probe를 사용하지 않았으며 C11 제품과 테마 패키지를 변경하지 않았다.

| 호스트 | 실제 증거 경로 | 결과 | 전용 PID / 정상 종료 |
|---|---|---:|---|
| VS2017 | `artifacts/task069theme/canonical/actual-c11-2017-v1` | 146/146 | 79608 / true |
| VS2019 | `artifacts/task069theme/canonical/actual-c11-2019-v1` | 146/146 | 101020 / true |
| VS2022 | `artifacts/task069theme/canonical/actual-c11-2022-v1` | 146/146 | 117344 / true |

`artifacts/task069theme/canonical/c11-inputs/manifest.json`은 시작 전 canonical 제품·테마 SHA, 동봉 테마 정확 일치와 검사 소스를 고정한다. 각 실행에서 실제 설치 DLL SHA·패키지 SHA·동봉 테마 SHA를 다시 검사했다. 상세 입력 사본·공식 SDK reference·실제 맵·옵션·margin·상태/설정 export·종료 증거는 실행별 원문에 보존했다. 전체 해시는 `CodexOrig_TASK-069_CanonicalMefValidation.json`에 기록했다.

## 실제 색과 지연 관찰

각 호스트에서 원래 118개 적용·팔레트·설정 보존 조건과 최초 core 28개를 모두 통과했다. primary SDK의 MEF category `{75A05685-00A8-4DED-BAE5-E7A50BFA929A}`에서 core 오류 4종과 breakpoint의 정확한 Foreground/Background·opaque SolidColorBrush·대비를 검사했다. legacy category로 fallback하지 않는다.

실제 view별 `IEditorFormatMap`의 syntax/compiler/other error는 `FFFF79C6`, compiler warning은 `FFFFB86C`, breakpoint scrollbar Background는 `FFBD93F9`다. 실제 editor 배경 `FF282A36`에 대해 대비는 각각 5.969029, 8.357583, 5.903096이다. suggestion/hinted suggestion은 관찰만 했으며 의도된 기본값을 강제로 바꾸지 않았다.

각 최초 snapshot 이후 ApplicationIdle와 시간 지연을 포함한 추가 네 snapshot에서도 core 색이 안정적으로 유지됐다. 추가 관찰은 28개씩 총 336개이며 **438개 합계에 중복 합산하지 않았다**. 초기 실패를 뒤늦은 회복으로 지우는 방식은 사용하지 않았다. ShowErrors/ShowMarks true, 두 폭 6, 실제 두 overview margin의 등록·Enabled·Visible·ActualWidth 6 조건도 그대로 통과했다.

## 검사 하네스 호환 수정과 경고

기존 C10+probe 146개 및 C10/final4 143 PASS/10 FAIL 원문·소스는 보존했다. 이번 검사는 `tests/task069theme/canonical`의 별도 네 사본만 사용한다. 기존 timing 사본과의 diff는 `artifacts/task069theme/canonical/harness-compatibility/narrow-harness-diff.txt`에 기록했다.

VS2019 첫 PrepareOnly는 실제 editor16 SDK가 net472/Shell16을 요구하는데 기존 major16 템플릿이 net462/Shell15를 사용해 실패했다. Root의 좁은 수정 승인에 따라 새 사본의 major16만 net472와 설치된 공식 SDK16 Shell/Threading/Interop 참조 closure로 정렬했다. 구 Shell15 compiler 참조를 제거하되 Threading analyzer 자산은 유지했다. 특히 IDE PrivateAssemblies의 구 Threading15.8 대신 공식 VSSDK16 참조 identity를 고정했다. 제품·공용 템플릿·core assertion·경고 억제는 변경하지 않았다. PrepareOnly 실패 v1~v4와 성공 v5를 모두 보존했다.

- VS2017·2019 helper 빌드: 명시 ApplicationIdle 관찰의 VSTHRD001 경고 각 1개, 오류 0.
- VS2022 helper 빌드: 같은 VSTHRD001과 MSB3277 참조 버전 충돌 7종, 오류 0. 충돌은 Immutable·Threading·Validation·AsyncInterfaces·Memory·Buffers·Unsafe다. 로그의 MSB3277 continuation 진단 641줄은 독립 경고 641개로 해석하지 않는다.

경고를 숨기거나 무경고 빌드로 보고하지 않는다. 이 경고는 private 검사 helper의 설치된 editor SDK와 초기 NuGet SDK 참조 혼합에 관한 것이며 제품 C11 빌드 경고로 합산하지 않는다. 실제 helper 로드와 모든 런타임 검사는 세 호스트에서 완료됐다.

## 안전 경계와 최종 인계

실제 설치된 호스트·view별 editor 포맷 맵·옵션·margin을 검사했다. native 오류·breakpoint 태그 생성과 물리 hover/화면 픽셀 표시는 검사하지 않았다. `ActualErrorTagsCreated=false`, `ActualBreakpointTagsCreated=false`, `PhysicalMarkerVisibilityVerified=false`를 유지한다. lookup 성공을 실제 사용자 프로젝트의 태그 표시 성공으로 보고하지 않는다. 옵션/map 쓰기·강제 cache clear·실사용 프로젝트/IDE/기본 프로필 변경·OS 포인터 조작은 없다.

집계 스크립트는 각 연도의 기존 118개 baseline과 이름/통과를 정확히 비교하고, 고정 입력 사본·컴파일된 소스 SHA·C11/테마 원본 SHA·정상 정리·idle 관찰을 다시 검사했다. 최초 집계에서 2017 전용 sentinel명을 다른 연도와 비교한 집계 오류는 각 연도 baseline 사용으로 바로잡았으며 제품이나 실제 검사 assertion은 바꾸지 않았다.

GUI 예약을 Root에 반환했고 추가 IDE·소스 변경 없이 canonical 네 소스를 동결했다. Root는 남은 설치 EXE 실제 6회·최종 감사·최신 Release·실제 게시·익명 다운로드 SHA 검증을 마감해야 한다.

최종 JSON은 기존 ThemeRuntime의 기본 필드를 함께 제공한다. `Candidate=candidate11`, `Passed=438`, `Failed=0`, 실제 IDE/팔레트 true 및 포인터/기본 프로필 변경 false, 세 `Executions`의 성공·입력/패키지 불변·정상 종료·강제 cleanup false를 명시했다. `Write-Validation.ps1`의 해당 계약과 같은 조건으로 다시 확인했다. helper 경고 수는 analyzer 진단과 고유 MSB3277 assembly/version 충돌 그룹 기준 1/1/8이며, 2022 continuation 진단 줄 수는 별도 필드에 보존한다. 제품 빌드 무경고와 혼동하지 않는다.
