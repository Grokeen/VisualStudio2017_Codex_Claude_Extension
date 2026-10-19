# CodexOrig2 Role.md 2차 감사 — C12 최종 소스·패키지 판정

대상: `artifacts/task054/candidate12`(0.12.1). [C11 감사](CodexOrig2_TASK-058_Audit_C11.md)의 배포 차단 결함 **R58-05**와 C1–C11 원본 증거를 보존한다. **최종 소스·패키지 판정: 미리보기 배포의 기술 검증 게이트 통과. R58-01~05의 확인된 제품 결함은 C12에서 해결됐고 추가 확정 결함은 0건이다.** 공개 실행과 공개 후 익명 다운로드·해시 검증은 아직 하지 않았으므로 배포 완료로 판정하지 않는다. Claude 로그인·실계정 답변은 사용자 지시에 따라 이번 검사에서 제외한다.

## 후보 변경·직접 회귀

C12의 133개 해시 입력을 실제 소스 스냅샷과 다시 대조해 불일치 0건이었다. C11과의 차이는 최종 PDF와 SHA-256이 같은 기본 이름 자산 하나를 추가하고 네 `.csproj`의 PDF `Content Include`를 그 파일로 옮긴 것뿐이다. C#·XAML·테마·큐·알림 실행 소스는 바뀌지 않았다. 네 Release 빌드는 종료코드·오류·경고 모두 0이다. 바이너리 해시는 새로 생성됐으므로 C12 DLL에 대한 무창 경계 검사를 반복했다. [C11→C12 입력 기록](../artifacts/task054/candidate11-to12.json) · [빌드](../artifacts/task054/candidate12/builds.json).

| C12 DLL 직접 검사 | VS2017 | VS2017 Legacy | VS2019 | VS2022 |
|---|---:|---:|---:|---:|
| 테마 XML·파일 경계 | 28/28 | 28/28 | 28/28 | 28/28 |
| 알림 개인정보·작업 요약 | 30/30 | 30/30 | 30/30 | 30/30 |

VS2017 C12 DLL의 FIFO·실패 상태 통합 검사는 **58/58**이다. 따라서 R58-01·02·04는 C12 직접 회귀 기준 계속 해결이다. R58-03은 C11에서 실제 VS2017·2019·2022 두 테마를 확인했고, C12에서 테마 실행 소스·중첩 테마 VSIX가 그대로이며 파일 경계 112/112가 통과했다. **C12 실제 IDE 테마 적용을 별도로 재실행한 결과는 아직 없으므로 C11 실제 호스트 결과의 이월과 C12 직접 결과를 구분한다.** [C11 테마 동결](CodexOrig_TASK-064_C11_Validation.json) · [C12 경계 2017](../artifacts/audit-codexorig2-task058/candidate12-theme-boundary-2017/result.json) · [C12 알림 2017](../artifacts/audit-codexorig2-task058/candidate12-notification-2017/result.json) · [C12 큐](../artifacts/audit-codexorig2-task058/candidate12-queue/result.json).

## R58-05 설치 후 PDF 경로

C11의 실제 설치본에서는 `CliGuideService.GetPdfPath()`가 요구하는 기본 PDF 이름과 설치된 파일명이 달라 `FileNotFoundException`이 났다. C12 네 VSIX의 `Resources/CliGuide/`에는 각각 `VisualStudio_AI_Assistant_설치_사용설명서.pdf`가 정확히 하나 있고, 내부 PDF 해시가 모두 `F853789CE823A1EABE8B4E98226064A9E86F808389CA77033DF83925744F2372`로 수정 자산과 일치한다. 중첩 테마 VSIX도 SKU별 하나씩이며 C11과 SHA-256이 같다. 네 SKU를 실제 전용 IDE에 설치한 뒤 제품 `GetPdfPath()` 호출·선택 VSIX 해시를 새 `GUIDE-01`로 각각 확인했고 감사자도 설치 DLL에서 같은 메서드를 독립 호출했다. **R58-05는 네 실제 설치 환경에서 해결**이다.

C11 실제 통합은 네 SKU 각각 145/145였으나 PDF 호출을 포함하지 않았다. C12의 VS2017 Legacy·일반 VS2017·VS2019·VS2022 실제 IDE 통합은 각 **146/146**, 총 **584/584**로 통과했다. 새 `GUIDE-01`은 설치된 제품 서비스의 `GetPdfPath()`와 선택 VSIX의 PDF 해시를 검사한다. 감사자도 네 설치 DLL에서 서비스를 독립 호출해 기본 이름 PDF와 해시 `F853789CE823A1EABE8B4E98226064A9E86F808389CA77033DF83925744F2372`를 확인했다. [Legacy 원본](../artifacts/ide/run-f295186340904a95aa154769829457e3/result.json) · [2017 원본](../artifacts/ide/run-670e5e87056d4f8b9507d91672fadd05/result.json) · [2019 원본](../artifacts/ide/run-4458cd90d6c64a38b3e14ab2f56a061a/result.json) · [2022 원본](../artifacts/ide/run-b0d16b6b8e304c57a19ab1ebf9ae4209/result.json). [독립 호출 Legacy](../artifacts/audit-codexorig2-task058/candidate12-installed-pdf-2017-legacy.json) · [2017](../artifacts/audit-codexorig2-task058/candidate12-installed-pdf-2017.json) · [2019](../artifacts/audit-codexorig2-task058/candidate12-installed-pdf-2019.json) · [2022](../artifacts/audit-codexorig2-task058/candidate12-installed-pdf-2022.json).

C12로 재조합한 FullOffline ZIP은 감사자 재해시 기준 **311,147,867바이트**, SHA-256 `89A3CDA8BBD21918795205A828657BB27F72B40B04BBD8085A90CC459E57C165`다. 이 정확한 ZIP의 네트워크 차단 AppContainer 검사는 **6/6**, profile·ACL 정리 성공이다. 최종 설치 경계도 합성 SKU/로컬 재고 기준 **35/35**다. 고객 VS2017 Professional 15.7.1 PC와 공개 다운로드는 미검증이다. [최종 패키지 입력](../artifacts/task054/package-inputs-final4/package-validation.json) · [설치 경계](../artifacts/task054/installer-final4/results.json) · [격리 설치](../artifacts/task041/run-a51ada1a8fad40519478b22d6cb0a7fb/worker-results.json).

## 결함 종료와 남은 경계

| 지적 | C12 판정 | 핵심 근거 |
|---|---|---|
| R58-01 요청 원문 알림 노출 | 해결 | 네 C12 DLL의 알림 30/30씩, 민감정보 미노출 |
| R58-02 완료 작업 요약 부족 | 해결 | 네 DLL에서 질문 답변·수정안·파일 작업을 안전한 유형으로 구분 |
| R58-03 IDE 테마 자동 적용 실패 | 해결(검사한 호스트) | C11의 실제 세 IDE·두 테마 적용, C12 실행 소스와 테마 VSIX 불변, C12 테마 경계 112/112. C12의 별도 3-IDE 테마 재실행은 하지 않았다. |
| R58-04 큐 오류 상태 덮임 | 해결 | C12 VS2017 DLL FIFO·실패 상태 58/58 |
| R58-05 설치 후 PDF 이름 불일치 | 해결 | C12 네 실제 IDE의 `GUIDE-01` 4/4 및 감사자 직접 서비스 호출 4/4 |

C11 테마 원본 검사의 VS2017·2019 raw export 완전 일치 실패 4건은 그대로 남는다. 기존 비테마 값의 변경·삭제는 0, 명시한 비기본값 보존은 4/4, 새 행은 설치된 기본값/프로필과 대조됐다. **검사한 프로필에서 기존 사용자 설정값 보존을 확인한 것이며 raw 파일 완전 일치나 모든 고객 프로필의 동일 동작을 주장하지 않는다.** Role.md의 화면 픽셀 전수 검사·실제 포인터 경로, 고객 15.7.1 PC, 로그인한 Claude 실답변은 미검증 또는 사용자 지시상 제외다. 최종 소스·패키지에 추가 확정 결함은 없다. 미리보기 공개 후에는 13개 자산을 익명으로 다시 내려받아 게시물과 로컬 고정 해시가 같은지 별도 검증해야 한다. [C12 증거 JSON](CodexOrig2_TASK-058_Audit_C12_Evidence.json).
