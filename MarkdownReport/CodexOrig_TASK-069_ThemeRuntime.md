# TASK069 #6/#7 실제 IDE 테마 검사

담당: CodexOrig 보조 개발 055 · 2026-10-20

공식 테마 VSIX와 C3 제품 VSIX를 새 `ThemeApply69…` 전용 RootSuffix에 설치하고, 실제 제품의 pending 적용과 IDE export 및 공개 테마 색 API를 검사한다. 사용 중인 IDE·프로젝트·기본 프로필을 변경하지 않고, 테스트가 시작한 정확한 PID만 종료한다. OS 포인터·데스크톱 캡처·사용자 계정 로그인은 사용하지 않는다.

검사 소유는 `tests/task069theme`의 새 세 파일뿐이다. 기존 `tests/task064`와 그 결과는 수정하지 않는다. 제품 소스·공유 빌드 출력·Git/index는 변경하지 않는다.

## 검사 범위

- 공식 VSIX 설치, 실제 로드된 제품 DLL SHA, 제품에 동봉된 테마 VSIX SHA, 두 테마 선택 버튼의 정확한 label/tag를 확인한다. TASK069에서 바뀐 버튼 문구만 기존 검사 기대와 맞추었다.
- 실제 제품이 테마 선택 pending을 소비하고 성공을 보고하는지, 실제 IDE export의 선택 GUID가 맞는지, 비기본 sentinel과 다른 모든 설정이 유지되는지 확인한다. 잘못된 pending의 거부와 임시 파일 정리도 기존 assertion 그대로 보존한다.
- 설치된 호스트의 공개 `EnvironmentColors.*ColorKey`를 reflection으로 읽어 고정 컴파일 정의와 정확히 비교한다. 키 이름은 VS2017 SDK XML에서 확인했다.
- Dracula의 실제 track/thumb 일반·hover·pressed 팔레트 대비를 각각 3 이상으로 검사한다. Kakao의 FileTabHot 및 ToolWindowTabMouseOver 글자는 위·아래 배경에 대해 각각 4.5 이상으로 검사한다. 실제 editor 배경과 tool window 배경이 구분되는지도 확인한다.
- 이는 **실제 호스트 팔레트와 적용 상태 검사**다. 물리적으로 마우스를 올리거나, 드래그하거나, 화면의 모든 픽셀이 보이는지 검사한 것은 아니다. 결과의 `VisualSwitchVerified`와 `PhysicalVisibleStateVerified`는 false로 남긴다.

## 최초 실패와 비교 기준 준비

VS2017 첫 실행 `actual-2017-v1`은 49/51이었다. 새 팔레트·대비·영역 검사와 실제 적용·GUID·해시·명시 sentinel은 모두 통과했고, 기존의 다른 설정 strict 비교 두 개만 실패했다. 정확한 diff는 원래 값의 변경·삭제 없이 XAML_Miscellaneous 기본 True 6개가 추가된 것이다. 개인 XAML 문서를 먼저 열어 초기화한 v3도 같은 49/51과 같은 6개 추가를 보였다. 두 원문과 diff를 보존한다.

최종 하네스는 제품 검사 **이전**에 공식 DTE 최소 설정 import로 테마를 한 번 바꾸고 원래 GUID로 복구하여 첫 전환의 schema를 준비한다. 준비 단계에서도 원래 GUID 복구와 기존 모든 설정 값 보존을 별도 두 assertion으로 확인하고, 추가된 기본값 diff를 보존한다. 그 뒤 제품 두 테마 검사는 기존의 **설정 개수 동일 + 모든 값 동일** 비교를 유지한다. 키 제외·assertion 삭제·기대값 완화는 하지 않았다. v5 두 제품 적용의 다른 설정 diff는 각각 0개다.

컴파일 보조 코드의 모호한 Constants 및 Task alias 오류(v2/v4), VS2019 제품 파일명을 잘못 지정한 v1도 삭제하지 않고 남겼다. 이들은 제품 실패나 실제 테마 실패로 계산하지 않는다.

## 실제 결과

| 실제 호스트 | 결과 | 증거 |
|---|---:|---|
| VS2017 | 53/53 PASS | `artifacts/task069theme/actual-2017-v5/result.json` |
| VS2019 | 53/53 PASS | `artifacts/task069theme/actual-2019-v2/result.json` |
| VS2022 | 53/53 PASS | `artifacts/task069theme/actual-2022-v1/result.json` |

총 **159/159 PASS**. 동일한 최종 하네스를 사용했고 세 호스트 모두 입력 불변, 컴파일된 helper 소스 일치, 제품·테마 패키지 입력 불변, helper 컴파일 경고/오류 0을 확인했다. 자기 PID 70324/76060/127256은 모두 정상 종료했으며 강제 종료는 없었다. 소유 GUI 예약을 Root에 반납했다. 해시와 범위는 `CodexOrig_TASK-069_ThemeRuntime.json`에 함께 기록했다.

측정된 Dracula 대비는 일반 **3.025968**, hover **5.903096**, pressed **5.969029**다. Kakao의 두 탭 종류와 각 배경 끝점 네 쌍은 모두 **12.105301**이다. Kakao editor는 `FFFFFFFF`, 도구 창은 `FFD9DDE3`으로 고정 정의 및 실제 호스트가 일치한다.

담당한 세 호스트 검사와 소유 PID 정리는 끝났다. Root는 네 SKU 최종 빌드·전체 IDE 회귀·감사·최신 Release·실제 게시·익명 다운로드 SHA 검증까지 이어가야 한다.
