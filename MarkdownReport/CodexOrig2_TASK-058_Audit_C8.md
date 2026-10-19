# CodexOrig2 2차 감사 C8 후속 보고 — 실제 IDE 추가검증 진행 중

대상: `artifacts/task054/candidate8` 버전 0.12.1. [C1](CodexOrig2_TASK-058_Audit_C1.md)·[C2](CodexOrig2_TASK-058_Audit_C2.md)·[C6](CodexOrig2_TASK-058_Audit_C6.md) 감사 원문은 보존한다. C8은 C6 대비 `AssistantControl.RequestQueue.cs`의 오류 상태 보존과 `AssistantControl.IdeThemes.cs`의 `Environment_Group` XML 계층만 변경한 고정 후보다.  
**현재 판정: 큐 상태 R58-04 해결. 세 IDE에서 테마 GUID·대표색 적용을 확인했다. VS2017/2019의 비테마 설정 export 비교 각 2건은 판별 중이며 VS2022는 25/25 통과했다.** 최종 출시 적합 판정은 아직 하지 않는다.

## 고정 및 무창 재검증

[C8 입력/산출물 SHA 기록](../artifacts/audit-codexorig2-task058/candidate8-freeze.json)의 132개 입력은 개인 복사본에서 불일치0개였다. 네 SKU Release 빌드 기록은 오류0·경고0이고, 각 DLL·VSIX를 재해시했다. 실제 VS2017 DLL SHA-256은 `11A381D3F22B7980E9AE3100613F2ECBC949547F81C988B057D570A0BD287318`이다.

- **R58-04 해결:** C6에서 0/2였던 동일 상태 전용 fixture가 C8 DLL에서 2/2 통과했다. 대기0이면 `요청 실패: controlled primary status error`를 유지하고, 대기1이면 그 오류 뒤에 `대기 요청 1개 취소`를 덧붙인다. 별도 기본 FIFO 56/56도 통과했다. [C8 상태 결과](../artifacts/audit-codexorig2-task058/candidate8-queue-status-result.json) · [기본 큐 결과](../artifacts/audit-codexorig2-task058/candidate8-queue-result.json).
- **R58-01·02 유지:** C8 네 SKU 실제 DLL에서 질문 답변, 소스 수정안, 완료된 파일 작업의 알림 유형을 구분했고 합성 인명·금액·경로를 본문에서 배제했다. 실제 OS 알림 픽셀은 미실행. [네 SKU 알림 검사](../artifacts/audit-codexorig2-task058/candidate8-notification-2017.json).
- **테마 XML/패키지:** 네 SKU 실제 DLL의 `ThemeSettingsXml`은 지원 연도와 두 GUID 각각에서 `Environment_Group` 아래 `Environment_FontsAndColors/FontsAndColors/Theme`을 생성한다. 동봉 VSIX는 SKU마다 정확히 하나이며 ZIP 내부와 Release 옆 바이트 해시가 같다. 네 SKU 설정 UI의 두 버튼과 접근성 이름은 무창 검사에서 통과했다. [실제 DLL XML 결과](../artifacts/audit-codexorig2-task058/candidate8-theme-xml-2017.json) · [패키지 무결성](../artifacts/audit-codexorig2-task058/candidate8-themes-integrity.json).

## #16 실제 IDE 판정 보류

[TASK064 전용 VS2017 v2 결과](../artifacts/task064/candidate8-2017-v2/result.json)는 25개 중 23개 통과·2개 실패, [VS2019 v1](../artifacts/task064/candidate8-2019-v1/result.json)도 23/25이며 [VS2022 v1](../artifacts/task064/candidate8-2022-v1/result.json)은 25/25다. 세 IDE 모두 설치 DLL/번들 해시, Kakao·Dracula 적용 성공 상태, pending 소비, 실제 export의 선택 GUID, 호스트 ToolWindowBackground 대표색, 임시 파일 정리를 확인했다. VS2017/2019 실패 네 건은 각 테마 적용 뒤 비테마 설정 export의 완전 동일성이다. VS2017 원자료에서 `XAML_Miscellaneous/AutoUpdateTags`가 export에 `True`로 추가된 차이가 보인다. 이것이 실제 설정값 변경인지 Visual Studio export 직렬화의 변화인지 아직 확정되지 않았다. 감사자는 이를 임의로 필터링해 PASS로 바꾸지 않는다. TASK064의 공개 DTE 값·동일 테마 no-op 대조가 나온 뒤 최종 판정을 보완한다.

#15 사용자 말풍선, #13 파일 생성, #14 Claude 비로그인 경계의 제품 입력은 C6와 같다. C2/C6의 검증 경계를 유지하며 실제 사용자 IDE·계정 결과로 확장하지 않는다. 이 후보의 미리보기 PDF도 C6와 같고 최종 문서 재빌드가 예고돼 있어 배포 PDF 판정에는 쓰지 않는다. 감사자는 제품 소스·공유 빌드·GUI·설치 관리자·이전 감사 보고서를 수정하지 않았다.

[C8 근거 JSON](CodexOrig2_TASK-058_Audit_C8_Evidence.json)에 현재 상태와 증거 SHA를 기록했다. 실제 IDE 추가 결과가 나오면 **이 C8 보고서에 결과와 판정 근거를 갱신**하고, 새 제품 후보가 나오면 별도 SHA로 감사한다.
