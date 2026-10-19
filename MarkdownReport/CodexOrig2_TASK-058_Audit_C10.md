# CodexOrig2 2차 감사 C10 중간 보고

대상: `artifacts/task054/candidate10`, 제품 버전 0.12.1. [C1](CodexOrig2_TASK-058_Audit_C1.md)·[C2](CodexOrig2_TASK-058_Audit_C2.md)·[C6](CodexOrig2_TASK-058_Audit_C6.md)·[C8](CodexOrig2_TASK-058_Audit_C8.md)·[C9](CodexOrig2_TASK-058_Audit_C9.md) 기록과 원본 실패는 보존한다. **현재 판정은 조건부 진행 중이다.** C10 실제 IDE 4종 재실행과 릴리스 설치·배포 확인이 끝나지 않았다.

## 고정 후보와 독립 회귀

C10 `source-hashes.json`의 132개 항목을 C10 소스 스냅샷과 직접 SHA-256 대조해 불일치 0건이었다. C9의 132개 입력과 비교해 바뀐 항목은 26쪽 사용자 안내 PDF 한 파일뿐이며 제품 소스 입력은 전부 같다. 네 SKU Release 빌드는 각각 종료코드 0, 오류 0, 경고 0이다. DLL 바이너리 해시는 C9와 달라 C9 DLL의 검사 성공을 그대로 C10의 직접 검사로 옮기지 않았다. [C9→C10 변경](../artifacts/task054/candidate9-to10.json) · [C10 빌드](../artifacts/task054/candidate10/builds.json).

| C10 직접 실행 | VS2017 | VS2017 Legacy | VS2019 | VS2022 |
|---|---:|---:|---:|---:|
| 테마 XML·크기 경계 | 28/28 | 28/28 | 28/28 | 28/28 |
| 알림의 개인정보·안전한 작업 요약 | 30/30 | 30/30 | 30/30 | 30/30 |

VS2017 C10 DLL의 FIFO·실패 상태 통합 시험은 **58/58**이다. 따라서 **R58-01**(요청 원문 노출)은 네 SKU 직접 호출 기준 해결, **R58-02**(안전한 작업 유형 요약)는 네 SKU 직접 호출 기준 해결, **R58-04**(큐 실패 상태가 원래 오류를 가림)는 C10 VS2017 직접 실행 기준 해결로 판정한다. 실제 IDE·OS 픽셀·실계정 요청까지 뜻하지 않는다. [테마 경계 2017](../artifacts/audit-codexorig2-task058/candidate10-theme-boundary-2017/result.json) · [알림 2017](../artifacts/audit-codexorig2-task058/candidate10-notification-2017/result.json) · [큐 58건](../artifacts/audit-codexorig2-task058/candidate10-queue/result.json).

## 테마와 비테마 설정

C9의 [TASK064 동결 결과](CodexOrig_TASK-064_FinalValidation.json)는 실제 VS2017 24/26, VS2019 26/28이었다. 두 테마 모두 실제 선택 GUID·대표색·pending 소비·임시 정리를 확인했고, 기존 비테마 export 값의 변경·삭제는 0건, 미리 심은 비기본값 보존은 4/4였다. 원본 export 완전 일치 실패 4건은 그대로 유지한다. VS2017에서 새로 직렬화된 여섯 `True` 행은 설치된 package의 getter 기본값과 일치한다. VS2019의 새 아홉 행은 설치된 기본 프로필 값과 18/18 일치한다. **R58-03은 C9 전용 프로필에서 기존 명시 설정값 보존을 확인한 상태이나, C10 실제 IDE 재실행 전에는 C10 최종 종료로 판정하지 않는다.** 일반 사용자 PC 전반의 설정 보존과 raw export 완전 일치도 주장하지 않는다.

C10 IDE 첫 실행은 [원본](../artifacts/ide/run-8b1b04cd30e24fb1905292e9f0665ac1/result.json)의 `IDE-06`에서 중단됐다. 옛 하네스는 소스 없는 요청에 `요청 실패:` 접두사를 요구했으나 실제 제품 상태는 enqueue 전 `요청 대기 실패: 프로젝트 소스 파일을 선택하거나 저장된 솔루션을 열어 주세요.`였다. 첫 수정 하네스도 다른 예외 경로의 문구를 잘못 기대해 [두 번째 원본](../artifacts/ide/run-b3061d3897d441748d55f656631d525f/result.json)에서 실패했다. 실제 출력으로 바로잡고 초안 보존·Stop 비활성을 함께 검사한 세 번째 실행은 IDE-06을 통과했다. 이후 `CHAT-09`는 과거의 거절 system 메시지를 포함해 3개 이상을 요구한 옛 검사에서 멈췄다. [대화 기록](../artifacts/ide/run-b9a501f51eaf4b369878a5fab705f72a/workspace/conversations/codex/4180d519c41348539959cffaaedfa734.md)에는 유효한 사용자 1건과 AI 수정안 1건이 있다. 네 번째 검사는 사용자 본문에 자동 첨부 안내가 붙는 점을 놓쳐 완전 일치 assertion에서 멈췄다. 다섯 번째 실행은 두 역할·실제 본문·첨부 안내를 확인해 `CHAT-09`까지 통과한 뒤, **IMAGES-06**의 반복 hover 즉시 fan 열림 assertion에서 중단됐다. 현재 제품은 지연 hover와 실제 포인터 위치를 조건으로 사용하므로 합성 `MouseEnter` 직후의 이 assertion은 현 계약과 다르다. 물리 입력 또는 적절한 지연을 포함한 재검사가 필요하다. Generate 버튼이 큐 수용 중 계속 활성화되는 Role #7 계약에 맞춰 종료 대기도 Stop 상태로 옮겼다. 제품 소스는 바꾸지 않았고 각 실패 원본은 보존한다. [다섯 번째 원본](../artifacts/ide/run-687bb77b4f9c4770aea832417c7327e3/result.json) · [변경된 하네스](../tests/ide/IdeTests.cs).

## PDF·패키지 및 남은 판정

네 C10 VSIX에는 SHA-256 `F853789CE823A1EABE8B4E98226064A9E86F808389CA77033DF83925744F2372`인 1,406,441바이트 PDF가 각각 정확히 포함되고, SKU별 테마 VSIX도 하나씩 있다. PDF는 26쪽 A4로 열리며 표지·새 안내 시작 19쪽·마지막 26쪽을 렌더해 글자 겹침이나 잘림을 발견하지 못했다. 앞 18쪽은 기존 안내, 19–26쪽은 v0.12.1 추가 안내다. 나머지 23쪽의 시각 상태와 실제 Word/LibreOffice 표시 결과는 독립 감사하지 않았다. [실제 포함 PDF](../artifacts/task054/candidate10/source/output/pdf/VisualStudio_AI_Assistant_설치_사용설명서_v0.12.1_미리보기.pdf).

오프라인 입력 패키지 검사는 AI VSIX 4개, 테마 VSIX 3개, 배포 archive 2개와 자산 13개를 기록했고 공개 배포·계정 인증은 하지 않았다. 설치 경계 35건은 합성 SKU/로컬 재고 조건에서 35/35다. 별도 AppContainer에서 네트워크 capability 없이 직접 TCP 차단 전후와 반복 설치, 두 CLI 버전 실행을 6/6 확인했다. 이는 실제 고객 Professional 15.7.1 IDE 설치 성공이나 Claude 로그인을 뜻하지 않는다. 네 중첩 테마 VSIX의 내부 SHA-256은 Release 옆 파일과 모두 일치한다. [패키지 입력](../artifacts/task054/package-inputs-final1/package-validation.json) · [설치 경계](../artifacts/task054/installer-final/results.json) · [격리 설치](../artifacts/task041/run-ab2f97f246e3499d9c304a12803dabb2/worker-results.json).

Claude 로그인·실제 답변은 이번 사용자 지시에 따라 검사 대상에서 제외한다. 고객 VS2017 Professional 15.7.1 및 제한망 PC, 최종 릴리스 다운로드·설치는 아직 확인되지 않았다. C10 실제 IDE 네 SKU와 릴리스 결과가 도착하면 본 보고서의 최종 판정을 갱신한다. [증거 JSON](CodexOrig2_TASK-058_Audit_C10_Evidence.json).
