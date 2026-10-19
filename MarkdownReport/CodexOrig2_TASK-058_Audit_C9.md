# CodexOrig2 2차 감사 C9 후속 보고 — 실제 IDE 재검증 반영

대상: `artifacts/task054/candidate9` 버전 0.12.1. [C1](CodexOrig2_TASK-058_Audit_C1.md)·[C2](CodexOrig2_TASK-058_Audit_C2.md)·[C6](CodexOrig2_TASK-058_Audit_C6.md)·[C8 중간 보고](CodexOrig2_TASK-058_Audit_C8.md)는 보존한다. C9는 C8 대비 `AssistantControl.IdeThemes.ReadBoundedThemeFile`의 버퍼를 4KB 이하로 바꾼 고정 후보다. 허용 문자 수와 제품의 테마 XML 구조·큐·알림 소스는 같다.

**현재 판정:** 새 132개 입력과 네 SKU 빌드·패키지, 무창 경계 검사와 FIFO는 통과했다. C9를 실제 VS2017·VS2019 전용 프로필에서 적용해 두 테마의 GUID·대표색·pending 소비·임시 정리를 확인했다. 두 IDE의 원본 비테마 export 완전 일치 검사는 각각 2건씩 계속 실패한다. 다만 기존 설정값의 변경·삭제는 0건이고, 별도로 심은 비기본값도 두 테마 뒤에 보존됐다. VS2019의 새 아홉 행은 설치된 기본 프로필의 값과 모두 일치한다. C9의 VS2022 실제 재실행이 없어 #16 전체 완료로 판정하지 않는다. 최종 PDF·설치·공개 배포도 아직 최종 검증 전이다.

## 고정 및 직접 검증

[C9 고정 해시 기록](../artifacts/audit-codexorig2-task058/candidate9-freeze.json)의 입력132개를 개인 복사본에서 다시 대조해 불일치0개였다. 네 Release 빌드 기록은 각각 오류0·경고0이다. 직접 검사에 사용한 VS2017 DLL SHA-256은 `9811B6CCF89A393E444B92E81329922FD5F15E16CC8D9925C5DC9726C2DA6D17`이다.

- **테마 파일 경계:** 네 SKU 실제 DLL에서 최소 XML의 `Environment_Group` 계층·두 테마 GUID·세 IDE 버전·허용하지 않은 버전/GUID 거절·UTF-8 정확한 상한과 한 글자 초과 거절을 각 28개, 총 **112/112** 통과했다. 시험 길이에는 0, 1, 63, 64, 4095, 4096, 8193자가 포함된다. 이는 4KB 버퍼 변경이 문자 상한을 약화시키지 않았음을 보인다. [VS2017 결과](../artifacts/audit-codexorig2-task058/candidate9-theme-boundary-2017.json)와 [실행기](../tests/task054/Test-IdeThemeBoundaries.ps1).
- **큐 오류 상태:** C6에서 실패했던 2개를 포함한 C9 실제 VS2017 DLL의 FIFO 통합 검사가 **58/58** 통과했다. 대기0에서는 원래 CLI 오류를 보존하고, 대기1에서는 원래 오류 뒤에 취소 건수를 표시한다. [C9 결과](../artifacts/audit-codexorig2-task058/candidate9-queue-result.json).
- **완료 알림:** 네 SKU 실제 DLL의 질문 답변/수정안/파일 작업 유형을 구분하고 합성 인명·금액·경로를 배제했다. 별도 native 창의 합성 요청에서 Codex x64·Claude x86 각각 `BalloonTipShown` 이벤트 1회, 브랜드 아이콘·타이머·중복 완료 시 아이콘 유지·정리를 확인했다. 이는 실제 IDE나 실계정 요청이 아니며 화면 픽셀·장기 중복률은 미검증이다. [직접 호출](../artifacts/audit-codexorig2-task058/candidate9-notification-2017.json) · [Codex native](../artifacts/task054/branded-candidate9-codex-x64/result.json) · [Claude native](../artifacts/task054/branded-candidate9-claude-x86/result.json).
- **테마 VSIX:** 네 C9 패키지에 현재 SKU용 독립 테마 VSIX가 정확히 하나씩 있고 ZIP 내부와 Release 파일 SHA-256이 일치한다. [패키지 근거](../artifacts/audit-codexorig2-task058/candidate9-themes-integrity.json).

## C9 실제 IDE 및 다른 설정 보존 판정

TASK064가 C9의 설치 DLL SHA-256을 대조한 전용 프로필에서 실행했다. VS2017은 **24/26**, VS2019는 **26/28**이며 각 2건의 원본 실패를 그대로 보존한다. 실패 이름은 두 테마의 `unrelated settings retained`로, 원본 비테마 export의 **완전 일치**가 기준이었다. 그 밖에 두 IDE 모두 Kakao·Dracula의 성공 상태, 적용 후 GUID, 실제 호스트의 대표 ToolWindowBackground 색, pending 소비, 임시 파일 정리가 통과했다. 이는 실제 호스트 상태 근거이지 전체 화면의 픽셀·가독성 검사는 아니다. [VS2017 원본](../artifacts/task064/candidate9-2017-sentinel1/result.json) · [VS2019 원본](../artifacts/task064/candidate9-2019-sentinel1/result.json).

| 구분 | VS2017 C9 | VS2019 C9 |
|---|---|---|
| 기존 비테마 export 값 변경·삭제 | 두 테마 모두 0·0 | 두 테마 모두 0·0 |
| 새 export 행 | 테마별 XAML Miscellaneous 6개, 모두 `True` | 테마별 XAML Designer General 9개 |
| 실제 IDE에 미리 심은 비기본값 | `AutoUpdateTags=False` | `DefaultZoomSetting=0` |
| 두 테마 적용 뒤 | export에서 `False` 유지, 읽기 전용 UserSettings도 `False` | export에서 `0` 유지; 이 속성의 읽기 전용 store 직접값은 해당 근거에 없음 |
| 원본 완전 일치 | 24/26 중 2건 FAIL 유지 | 26/28 중 2건 FAIL 유지 |

VS2017의 새 여섯 행은 설치된 Microsoft XAML package의 각 getter가 값 부재 시 사용하는 기본 `True`와 일치한다. 따라서 검사한 프로필에서는 기존 명시값 `False`를 유지하면서 기본값 행을 직렬화한 결과로 판정한다. VS2019의 `DefaultZoomSetting=0`은 vendor 기본 `1`과 다른 명시값이며 실제 export에 남았다. 새 아홉 행은 설치된 VS2019 Community `General.vssettings`의 XAML Designer General 항목과 18/18(두 테마 × 9개) 일치한다. 이는 기본 **프로필 값과의 일치**이며 아홉 속성 각각의 런타임 fallback 또는 모든 사용자 프로필의 상태를 증명하지 않는다. **R58-03은 시험한 기존 명시 설정의 보존은 통과, 비테마 export 완전 일치는 실패, 일반 사용자 PC에서의 포괄적 동작 동일성은 미검증**으로 나눠 기록한다. [2017 비교](../artifacts/task064/candidate9-2017-sentinel1/settings-analysis.json) · [2019 비교](../artifacts/task064/candidate9-2019-sentinel1/settings-analysis.json) · [설치 프로필 대조](../artifacts/audit-codexorig2-task058/candidate9-2019-vendor-profile-defaults.json) · [vendor IL 조사](CodexOrig5_TASK-064_AutoUpdateTagsResearch.md).

C8의 VS2022 전용 프로필 25/25는 앞선 후보의 근거다. C9에는 VS2022 실제 IDE 재실행 기록이 없으므로 C8 결과를 C9 통과로 옮기지 않는다. 실제 사용자 VS2017 15.7.1 PC, Windows 알림 픽셀, 전체 테마 화면, 릴리스 설치·익명 다운로드도 이 감사 범위에서 확인되지 않았다.

[TASK064 동결 요약](CodexOrig_TASK-064_FinalValidation.json)의 원본 수치는 C8 **71/75**, C9 **50/54**다. 원본 실패 4건씩은 모두 그대로 남고, C9의 두 호스트·두 테마 비기본값 보존은 **4/4**다. TASK064는 전용 프로필을 정상 종료했고 기본 사용자 프로필을 바꾸지 않았다. 이 수치에 C8과 C9의 검사를 합쳐 하나의 성공률로 표시하지 않는다.

#15 사용자 말풍선, #13 파일 생성, #14 로그인 없는 Claude 경계의 입력은 C8과 같다. 이전 무창 근거를 유지하되 실제 IDE·사용자 PC·실계정 성공으로 확대하지 않는다. 현 PDF는 C6 때의 미리보기 파일이며 최종 문서 재빌드가 예정돼 있다. 감사자는 GUI 예약·제품 소스·공유 빌드·릴리스·기존 보고서를 변경하지 않았다. [C9 검증 JSON](CodexOrig2_TASK-058_Audit_C9_Evidence.json)에 현재 증거를 기록했다.
