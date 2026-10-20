# TASK069 Role17 final4 실제 호스트 팔레트 검사

담당: CodexOrig 보조 개발 055 · 2026-10-20

VS2017·2019·2022의 전용 프로필에서 각각 **118/118 PASS**, 합계 **354/354 PASS**다. 검사 중 입력 불변, 컴파일된 helper와 고정 소스 일치, 자기 PID 정상 종료를 확인했다. 테스트 전용 프로필 외 실사용 IDE·프로젝트·기본 프로필·OS 포인터를 조작하지 않았다.

| 호스트 | 증거 경로 | 통과 | 자기 PID / 정상 종료 |
|---|---|---:|---|
| VS2017 | `artifacts/task069theme/menu/actual-2017-v1` | 118 | 99084 / true |
| VS2019 | `artifacts/task069theme/menu/actual-2019-v1` | 118 | 41916 / true |
| VS2022 | `artifacts/task069theme/menu/actual-2022-v2` | 118 | 48516 / true |

실행 결과·입력 사본 SHA·설정 export·설치 결과·실제 팔레트·정리 증거를 각 경로에 보존했다. 세 private helper 빌드는 성공했고 경고/오류 진단이 없었다. 집계 스크립트 `artifacts/task069theme/menu/aggregate-final.py`는 결과, 입력 사본 해시, 실제 컴파일 소스 해시, 패키지 원본 불변과 자기 PID 정상 정리를 다시 검사한다. 상세 해시는 `CodexOrig_TASK-069_MenuRuntime.json`에 기록했다.

## 정확한 후보 범위

Root 승인 범위의 **C5 원본 DLL + final4 테마를 넣은 개인 probe VSIX**다. C5 정식 VSIX 자체나 최종 C8 배포 후보를 검사했다고 주장하지 않는다. C5 제품 패키지의 모든 ZIP entry 중 `Themes/<해당 연도 테마 VSIX>` 하나만 final4로 교체한 private 사본이며, DLL과 나머지 모든 entry body 및 원본 패키지 SHA 불변을 확인했다. `artifacts/task069theme/menu/package-probe/probe-manifest.json`이 원본/사본/제품 DLL 해시와 변경 entry를 기록한다. 이 manifest의 생성 시점 `ActualIdeTestStarted=false`는 생성 시점 정보이며, 후속 실제 실행은 위 세 경로가 증명한다. 정식 Release 후보는 아니다.

기존 TASK069 159개 final1 검사의 세 소스와 증거는 변경하지 않았다. 새 `tests/task069theme/menu` 세 소스에서 기존 53조건을 유지하고 메뉴 15조건, 확대 raw token 35조건, 추가 대비 15조건을 더했다. 메뉴 글자와 glyph hover 대비는 **5.903096**, 메뉴 경계와 IDE 배경 대비는 **3.955889**, 메뉴 내부 배경과 경계 대비는 **3.275630**으로 세 호스트 모두 일치했다.

확대 검사는 실제 `VSColorTheme.GetThemedColor`에서 Environment/CommonControls/Text Editor/ColorizedSignatureHelp/Text Marker의 정확한 category·name·channel을 읽고 고정 final4 정의와 비교한다. toolbar hover, hot/auto-hide 탭, 버튼/콤보, indicator margin, overview 배경, breakpoint marker, error/warning 토큰이 포함된다. 기존 스크롤 thumb 대비, Kakao hot 탭 글자 대비, editor/tool 영역 구분, pending 적용·선택 GUID·다른 설정 보존 검사도 유지했다.

## 보존한 실패

VS2022 첫 실행 `actual-2022-v1`은 실제 assertion 118개를 통과했으나 실행 중 공용 `tests/ide/IdeIntegration.csproj`가 Root의 디버그 helper Compile 등록으로 변경되어 드라이버가 실패했다. 강한 입력 불변 검사를 완화하지 않았고 원래 결과·변경 해시·정상 정리 증거를 보존했다. Root가 공용 템플릿을 동결한 뒤 동일한 118개 소스로 새 `actual-2022-v2`를 실행해 드라이버 성공과 입력 불변을 확인했다. 이 최초 실행은 최종 354개에 합산하지 않는다. 확대 helper 준비 중 지역 변수 이름 충돌 컴파일 실패도 삭제하지 않았다.

## 검증 한계와 후속 인계

실제 설치된 호스트의 테마 리소스 팔레트와 제품 적용 상태를 검사했다. 물리적 메뉴 hover·스크롤 조작·화면 픽셀 가시성은 검사하지 않았다. 특히 editor의 `IEditorFormatMap` 및 `OverviewErrorMargin`/`OverviewMarkMargin`이 이 토큰을 소비하는지, 실제 오류/중단점 태그가 화면에 보이는지는 **미검증**이다. `EditorMarkerConsumptionVerified=false`를 유지하며 테마 token 읽기 성공을 화면 표시 성공으로 바꾸지 않는다. 확인되지 않은 가상 `ErrorInScrollBar` 키를 만들지 않았다.

소유 GUI 예약은 반납했고 추가 IDE는 시작하지 않는다. 세 menu 검사 소스는 해시와 함께 동결한다. Root는 최종 네 SKU 빌드·실제 IDE/전체 회귀·감사·최신 Release 제작·실제 게시·익명 다운로드 SHA 검증을 이어가야 한다. 개인 probe 검사가 이 최종 배포 게이트를 대신하지 않는다.
