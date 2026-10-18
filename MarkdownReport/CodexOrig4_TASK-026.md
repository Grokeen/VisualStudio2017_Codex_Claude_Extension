# TASK-026 — 요청사항 15 / 테마와 UI 통일성

- 담당: CodexOrig4
- 작업 시작: 2026-10-19 00:45 KST
- 상태: 담당 구현·자체 회귀 완료, 정식 패키지·세 IDE 통합 확인은 TASK025에 인계. 동결4와 일치하는 독립 사본의 실제 제품 DLL에서 31/31 PASS. GUI·Git 배포 미실행.
- 기준: Role.md 전체와 2026-10-18 23:45 추가 1~15, 감사 TASK019/023. Role 원문은 `artifacts/task026/Role.reviewed.start.md`에 보존.
- 사용자 추가 우선순위: UI의 통일성이 가장 중요함.

## 구현·검토 기준

1. 자동 모드에서 Visual Studio의 현재 디자인을 우선 사용한다. IDE 테마 정보가 없으면 Windows 앱 테마를 사용한다.
2. 설정에 자동 / Light / Dark 선택을 제공한다. 즉시 미리보기, 저장 후 복원, 취소·Esc·창 닫기 복구를 검증한다.
3. 밝은 모드는 흰색·회색을 기본으로 하고 옅은 유채색 강조를 사용한다. 어두운 모드는 같은 의미와 계층의 색상 체계를 유지한다.
4. 채팅·설정·안내·모델/권한 팝업·우클릭 메뉴·툴팁·목록·스크롤·Markdown/코드/비교 미리보기가 같은 팔레트를 사용한다. 창 경계를 넘는 팝업도 포함한다.
5. hover·pressed·selected·focus·disabled 상태와 일반/보조/링크/오류 텍스트 대비를 검사한다. diff 추가/삭제와 브랜드 색상의 의미를 유지한다.
6. IDE·Windows 테마 변경에 반응하고 화면 해제 시 이벤트를 해제한다. Windows/IDE 사용자 테마 자체는 변경하지 않는다.

## 협업 경계

- TASK024: 기존 Markdown·안내·hover 등 소유 기능 유지. 테마 본체 인계 완료. 해당 하위 기능의 팔레트 연결은 기존 담당자가 완료·동결했고 Orig4는 중복 편집하지 않았다.
- TASK025: 프로젝트·버전·실행기·최종 통합·감사·Git 소유 유지.
- TASK026: 인계받은 테마 기반과 `tests/task026` 및 이 보고서만 전담. 공유 문서는 덧붙이기만 수행.
- 다른 작업자 진행 중이므로 개별 GitHub 배포하지 않는다.

## 구현 결과

- `AssistantThemePolicy.cs`로 명시 선택 → Visual Studio 실제 배경 → Windows 앱 모드 우선순위를 통일했다. 저장값은 기존 초안의 `auto/light/dark`를 유지한다.
- `AssistantPalette.cs`의 공통 의미 자원을 XAML·편집 영역·설정·메뉴·팝업·툴팁에 공유한다. Light는 흰색·회색과 옅은 청색/녹색 강조, Dark는 같은 계층의 어두운 표면을 사용한다.
- 버튼·목록·탭·부채꼴의 hover/selected/pressed/focus와 기본/주요 버튼 대비를 공통 역할로 묶었다. 좁은 대화 목록의 가림막도 공통 팔레트를 사용한다.
- 설정의 선택 변경은 즉시 반영한다. `SelectionChanged` 중 `SelectedValue`가 이전 값을 주던 현상을 `AddedItems`의 새 항목 Tag를 사용해 수정했다. 기존 명시 저장 및 취소·Esc·창 닫기 경로에 모드 스냅샷/복구를 연결했다.
- 모델·권한 팝업의 고정 어두운 배경, 강조/설명/하단과 작업 도구 창의 보조 문구를 동적 자원으로 교체했다. 열린 분리 창의 같은 사전 참조를 통해 모드를 같이 바꾼다.
- Light에서는 Thinking 효과가 어두운 글자 안에서 움직여 밝은 배경에서도 읽힌다. Dark의 기존 흰빛 효과는 유지한다.
- IDE·Windows 알림은 표시 중만 구독하며 중복 알림은 병합한다. 수동 모드에서는 자동 알림으로 선택이 바뀌지 않는다. Unloaded/Dispose에서 static 구독을 해제한다. 실제 SDK 스레드 진단 후 WPF 이벤트 진입에서 Dispatcher를 검증하고 외부 스레드 알림은 초기 캡처한 Dispatcher에 게시하도록 경계를 보완했다. 비차단 게시 한 줄에만 근거를 단 VSTHRD001 pragma를 사용했다.
- 기존 Markdown 문서가 옛 코드/링크 색을 붙잡던 현상을 추가 제품 회귀에서 확인했다. 보조 자원을 먼저 교체하고 재렌더링을 유발하는 배경·본문을 마지막에 게시하도록 수정했다. 새 후보에서 검증할 4개 조건을 추가했다.
- [Microsoft VSColorTheme](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.platformui.vscolortheme?view=visualstudiosdk-2022)의 색 조회/변경 이벤트를 기존 SDK와 대조했다. 사용자 Windows·IDE 설정 자체는 변경하지 않는다.

## 소스 독립 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| 우선순위·누락/잘못된 모드·Blue 배경 판별 | 11/11 PASS | `artifacts/task026/policy-freeze1/result.json` |
| 양 모드 대비·자원·중복·고정 색, 문서 변경 알림 시 완성된 코드/링크 색 게시 | 151/151 PASS | `artifacts/task026/palette-publication-final/result.json` |
| 실제 Theme/Thinking 소스 + 최소 host fixture, 모드 선택·변경·분리 메뉴/툴팁/팝업 전달·해제 | 19/19 PASS, 실제 Threading.Analyzers 15 포함 0경고 | `artifacts/task026/runtime-sdk-freeze4/result.json` |
| 검사 스크립트 4개 AST | 구문 오류 0 | `tests/task026` |
| 제품 코드 공백 검사 | PASS, CRLF 인식 | `git diff --check` |

위 수치는 소스 사본을 C# 7·.NET Framework 4.6.2로 독립 컴파일한 결과다. 151개 중 마지막 4개는 일반 WPF 소비자의 문서 갱신 시점 관찰이며 실제 Markdown 제품 회귀 4개의 대체 성공으로 세지 않는다. 실제 제품 DLL 또는 실제 IDE 검사 수치와 합산하지 않는다. 대표 WPF 표본의 Light/Dark PNG를 직접 열어 글자·입력·버튼·선택 상태를 검토했다. 표본은 제품 전체 화면이나 실제 IDE 캡처가 아니다.

최초 PowerShell UTF-8 BOM 누락과 reflection 인수 래핑 문제는 검사 도구에서 수정했다. 초기 색 매핑 중 중복 자원 및 Dark pressed 위 보조 글자 대비 4.318을 발견해 수정했다. 원래 실패 사본·출력은 artifacts/task026에 보존하며 최종 성공으로 덮어쓰지 않았다.

## 보안 및 남은 통합 검증

테마 입력은 유한한 세 가지 모드로 검증하고 코드 내부 색상만 파싱한다. 사용자 텍스트를 XAML로 실행하지 않는다. Windows 개인 설정은 읽기 전용으로 열며 인증 정보·파일 실행·네트워크 접근은 추가하지 않았다. 실제 IDE 전용 SDK 초기화는 IDE 프로세스 안에서만 수행한다. 검사 설정·작업 경로는 별도 폴더로 격리한다.

제품 동결 목록: `artifacts/task026/theme-freeze4.json`의 14개 파일. 실제 세 IDE의 모드/모델 팝업 검증 및 최종 6빌드는 TASK025가 수행한다.

## 실제 제품 검증과 후속 후보

- 첫 통합 SDK15 빌드는 오류 1·경고 35로 실패했다. 소유자별로 보완했으며 최초 실패는 `artifacts/task025/build-bootstrap1.log`와 진단 JSON에 보존했다.
- TASK025의 bootstrap2는 SDK15 Release **0오류·0경고**였다. 고정 제품 DLL SHA-256은 `FDF46F5BE8CC283EF1D49A243789898689905F849614BABCC2F2DFF4E3C6F836`이다. 안내 GIF가 없는 자산 제작용이며 최종 릴리스 적합 판정 대상은 아니다.
- 이 실제 DLL에서 기본 **23/23**, 실제 공통/작업 도구 창 전환을 추가한 **27/27**을 각각 통과했다. 설정 명시 저장 후 새 컨트롤의 Light 복원, 메뉴·툴팁·분리 자원, Thinking과 해제를 포함한다. 실제 제품의 두 모드 PNG도 직접 검토했다.
- 검사를 기존 Markdown 코드/링크 전환까지 확대하자 **27 PASS / 4 FAIL**이었다. 원래 실패는 `artifacts/task026/product-bootstrap2-markdown/runtime/result.json`에 보존했다. 기본 23/27 성공과 새 범위 실패를 구분한다.
- TASK025가 독립 소스 사본을 SDK15 Release로 빌드한 **theme-probe1**의 실제 제품 DLL에서 최신 **31/31 PASS**를 확인했다. SHA-256은 `4A200332A00CD1720AA6629730916828FBB5DBCFFB27F6D3A312D62629DB787C`이며 동결4의 테마 소스 **14/14 바이트 해시 일치**도 Orig4가 직접 대조했다. SDK15 빌드 로그에 오류·경고가 없으며 검사 실행은 TASK025, 결과·소스 해시 재대조는 Orig4가 수행했다. 근거는 `artifacts/task025/theme-probe1-runtime/result.json`과 하위 `runtime/result.json`이다.
- 이 사본 빌드는 **정식 후보·최종 VSIX·세 IDE 시험을 대신하지 않는다**. 합성 host 색으로 자동 모드를 시험하므로 JSON의 `ActualIdeTested=false`, `WindowShown=false`를 유지한다. 정식 2017/2019/2022 DLL에는 같은 `tests/task026/Test-Task026.ps1 -AssemblyPath <DLL> -OutputDirectory <새 경로>`의 31개 조건을 적용하도록 TASK025에 인계했다. DLL SDK 참조에 맞는 project.assets로 의존성을 선택한다. 현재 VS2017/2019는 SDK15를 공유하고 VS2022는 SDK17을 사용함을 실제 복원 자산에서 확인했다.
- Orig3 보완 문서 D08의 “자동 · Visual Studio 우선 / 밝게 · Light / 어둡게 · Dark”, 수동 선택 우선 및 자동 VS→Windows 설명은 현재 UI와 일치한다. 해당 설명용 도식은 실제 제품/IDE 캡처로 집계하지 않는다.

기존 감사 원문의 과거 실패와 실제 계정·운영 연동 미검증 한계는 그대로 유지한다. 이 테마 작업의 성공을 해당 범위의 검증 성공으로 확대하지 않는다.
