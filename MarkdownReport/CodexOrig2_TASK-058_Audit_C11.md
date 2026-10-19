# CodexOrig2 Role.md 2차 감사 — C11 최종 후보 판정

대상은 `artifacts/task054/candidate11`(0.12.1)이다. [C1](CodexOrig2_TASK-058_Audit_C1.md)·[C2](CodexOrig2_TASK-058_Audit_C2.md)·[C6](CodexOrig2_TASK-058_Audit_C6.md)·[C8](CodexOrig2_TASK-058_Audit_C8.md)·[C9](CodexOrig2_TASK-058_Audit_C9.md)·[C10](CodexOrig2_TASK-058_Audit_C10.md)의 원본 결과와 실패는 보존한다. **판정: C11은 배포 불가. 새 P1 결함 R58-05로 설치된 확장의 PDF 안내서 열기가 실패한다.** R58-01·02·04의 DLL 직접 회귀와 R58-03의 세 IDE 테마 자동 적용은 통과했고, 네 SKU 실제 IDE 통합은 각 145/145, 총 580/580이다. 그 검사에는 설치된 PDF 열기 경로가 없었다. 원본 설정 export 완전 일치 실패 4건은 보존하며, 기존 명시 설정값 보존을 별도 근거로 인정한다. Claude 로그인·실제 답변은 사용자 지시에 따라 이번 검사에서 제외한다.

## 고정 후보·직접 검사

C11 해시 목록 132개를 소스 스냅샷에서 직접 재계산해 불일치 0건이었다. C10과 달라진 입력은 `AssistantControl.Fan.cs` 한 파일이며 실제 diff는 class 주석의 `update date` 들여쓰기 한 줄이다. 실행문은 같다. 네 SKU Release 빌드가 모두 종료코드 0, 오류 0, 경고 0이다. 빌드 DLL 해시는 C10과 달라 C11 실제 바이너리로 핵심 회귀를 다시 실행했다. [C11 해시 목록](../artifacts/task054/candidate11/source-hashes.json) · [빌드](../artifacts/task054/candidate11/builds.json).

| C11 DLL 직접 회귀 | VS2017 | VS2017 Legacy | VS2019 | VS2022 |
|---|---:|---:|---:|---:|
| 테마 XML·파일 경계 | 28/28 | 28/28 | 28/28 | 28/28 |
| 알림 개인정보·안전한 작업 요약 | 30/30 | 30/30 | 30/30 | 30/30 |

VS2017 DLL의 FIFO·승인·오류 상태 통합 검사는 **58/58**이다. 즉 R58-01은 사용자 원문 노출 경로의 네 SKU 직접 검사 기준 해결, R58-02는 구조화한 답변·수정안·파일 작업 요약의 네 SKU 직접 검사 기준 해결, R58-04는 원래 CLI 오류와 대기 취소 수 보존의 C11 VS2017 직접 검사 기준 해결이다. OS 화면 픽셀·장기 알림 중복률·실계정 응답은 별도 범위다. [2017 알림](../artifacts/audit-codexorig2-task058/candidate11-notification-2017/result.json) · [2017 테마 경계](../artifacts/audit-codexorig2-task058/candidate11-theme-boundary-2017/result.json) · [큐](../artifacts/audit-codexorig2-task058/candidate11-queue/result.json).

네 C11 VSIX는 각각 새 26쪽 PDF(SHA-256 `F853789CE823A1EABE8B4E98226064A9E86F808389CA77033DF83925744F2372`) 하나와 현재 SKU의 테마 VSIX 하나를 포함한다. PDF는 C10과 바이트 동일하며 그때 표지·19·26쪽을 렌더해 확인했다. 최종 오프라인 입력 검사는 AI VSIX 4개·테마 VSIX 3개·배포 archive 2개·자산 13개를 기록하고 각 DLL/VSIX/PDF 해시를 고정했다. 앞선 패키지에서 달라진 입력은 `OFFLINE_INSTALL.md` 한 파일뿐이다. 현재 v0.12.1 미리보기 링크와 완성 ZIP의 바로 설치 절차를 안내하도록 수정했다. 완성 FullOffline ZIP은 **311,148,298바이트**, SHA-256 `EE7056184395E0188D5F0BA87460CA5D89DCBDC3B96BF0E2DB2144DFD8106FB6`이며 감사자가 재해시했다. 새 설치 경계 35/35는 합성 SKU와 로컬 재고 조건에서 통과했다. 이 정확한 ZIP의 AppContainer 재실행은 네트워크 capability 0·설치 반복·두 CLI 시작·전후 TCP 차단 **6/6**, 프로필·ACL 정리 성공이다. 계정 인증은 하지 않았다. 공개 배포와 고객 VS2017 Professional 15.7.1 PC 설치는 확인하지 않았다. [최종 패키지 입력](../artifacts/task054/package-inputs-final3/package-validation.json) · [설치 경계](../artifacts/task054/installer-final2/results.json) · [최종 ZIP 격리 설치](../artifacts/task041/run-dae38a6ef0e04c739b3742c3df4b17f4/worker-results.json).

## 실제 IDE·다른 설정의 판정 기준

C9 [TASK064 동결](CodexOrig_TASK-064_FinalValidation.json)은 실제 VS2017 24/26·VS2019 26/28이었다. 네 원본 실패는 비테마 export **완전 일치** 검사이며 보존한다. 두 IDE·두 테마에서 기존 비테마 값 변경·삭제는 0건, 명시한 비기본값 `AutoUpdateTags=False`와 `DefaultZoomSetting=0`은 4/4 유지됐다. VS2017 새 여섯 행은 설치된 vendor getter 기본 `True`, VS2019 새 아홉 행은 설치된 기본 프로필 값과 일치한다. 이것은 검사한 프로필의 **기존 명시값 보존** 근거이지 모든 PC의 export 바이트 동일성 또는 모든 설정의 동작 동일성 보증은 아니다.

C11 테마 첫 실제 VS2017 전용 프로필은 [원본](../artifacts/task064/candidate11-2017-sentinel1/result.json) **26/28**이다. Kakao·Dracula의 제품 성공, 선택 GUID, 대표 환경색, pending 소비, 임시 정리와 비기본 `AutoUpdateTags=False` 유지가 통과했다. 두 실패는 각 테마의 raw 비테마 export 완전 일치다. 감사자가 원본 export XML을 독립 비교하자 테마별 기존 1,396개 비테마 값의 변경·삭제는 0건, `True` 행 추가만 6건이었다. 적용 후 읽기 전용 UserSettings에도 `False`가 남았다. [독립 비교](../artifacts/audit-codexorig2-task058/candidate11-2017-settings-independent.json).

VS2019 전용 프로필도 [원본](../artifacts/task064/candidate11-2019-sentinel1/result.json) **26/28**이다. 두 테마의 제품 성공·GUID·대표색·pending 소비·임시 정리 및 비기본 `DefaultZoomSetting=0` 보존은 통과하고, raw 완전 일치만 각 1건 실패했다. 감사자가 export XML을 독립 비교하자 테마별 기존 1,541개 값의 변경·삭제는 0건, XAML Designer General 행 추가만 9건이었다. 추가된 18개 값(두 테마 × 9개)은 설치된 VS2019 Community 기본 프로필과 모두 같다. 이 비교는 기본 프로필과 일치한다는 근거이며, 각 속성의 모든 런타임 fallback을 입증하지 않는다. [독립 비교](../artifacts/audit-codexorig2-task058/candidate11-2019-settings-independent.json) · [설치 프로필 대조](../artifacts/audit-codexorig2-task058/candidate11-2019-profile-comparison.json).

VS2022 전용 프로필은 [원본](../artifacts/task064/candidate11-2022-basic1/result.json) **25/25**다. 두 테마의 성공·GUID·대표색·pending 소비·임시 정리뿐 아니라 raw 비테마 설정 완전 일치도 통과했다. 세 호스트 모두 C11 해당 SKU의 DLL SHA-256을 검사하고 실제 IDE의 import/export를 실행했으며 기본 사용자 프로필은 변경하지 않았다. 따라서 **R58-03의 실제 테마 자동 적용 실패는 검사한 VS2017·2019·2022 전용 프로필에서 해결**로 종료한다. 다만 2017·2019 raw 완전 일치 네 실패를 통과로 바꾸지 않는다. 그 차이는 위와 같이 기존 명시값 변경·삭제 0, 비기본값 보존 4/4, 기본 행의 직렬화로 분리한다. 실제 고객 VS2017 Professional 15.7.1 PC에서의 설치·적용은 미검증이다.

## 새 결함 R58-05 — 설치된 PDF 안내서 열기 실패 (P1)

C11 소스의 `CliGuideService.GetPdfPath()`는 고정 이름 `VisualStudio_AI_Assistant_설치_사용설명서.pdf`를 찾는다. 그러나 네 VSIX 내부와 실제 VS2017 전용 IDE에 설치된 확장에는 `VisualStudio_AI_Assistant_설치_사용설명서_v0.12.1_미리보기.pdf`라는 이름으로 PDF가 들어 있다. C11 VS2017 설치 DLL(해시 `01E737C3979FA232510386D8296A8AB9CC9B0E275ECE827D86256ED4ED0E49E6`)에서 서비스를 직접 호출하자 `FileNotFoundException: PDF 설명서를 찾지 못했습니다…`가 재현됐다. 빌드 출력 폴더에는 MSBuild `Link`가 지정한 기본 이름으로 파일이 있지만 VSIX 압축/설치 단계는 원본 파일명을 사용했다. 앞선 PDF 해시·포함 여부 검사와 IDE 145건은 이 **설치 후 경로**를 확인하지 않아 놓쳤다. [직접 재현 JSON](../artifacts/audit-codexorig2-task058/candidate11-installed-pdf-path-failure.json) · [C11 서비스](../artifacts/task054/candidate11/source/src/VS2017.AIAssistant/Services/CliGuideService.cs) · [C11 VSIX](../artifacts/task054/candidate11/2017/Release/VS2017.AIAssistant.vsix).

R58-05가 해결된 새 후보에서 네 SKU 실제 VSIX 설치 뒤 `GetPdfPath()` 성공, 반환 파일명·SHA-256, 실제 IDE 안내 화면 경로를 재검사해야 한다. **C11 공개 게이트는 닫는다.** 이 결함의 발견은 앞선 원본 성공·실패 기록을 변경하지 않는다.

C10 실제 IDE 초기 재실행은 오래된 검사 전제 때문에 IDE-06·CHAT-09·IMAGES-06에서 차례로 중단됐고, 각 원본을 [C10 중간 보고](CodexOrig2_TASK-058_Audit_C10.md)에 보존했다. IDE-06은 실제 preflight 문구와 입력 초안·Stop 상태로, CHAT-09는 사용자·AI 역할 및 첨부 안내를 포함한 실제 본문으로 갱신했다. IMAGES-06은 합성 `MouseEnter` 직후 바로 열림을 기대하는 대신 지연 예약·즉시 열리지 않음·명시적 열기 1회와 예약 소비를 확인한다. 이 검사는 물리 포인터 이동을 입증하지 않으며, 그 결과를 실제 GUI hover의 대체로 주장하지 않는다. C11 제품 소스는 이 검사 수정 과정에서 실행문 변경이 없다. 실제 IDE의 최종 결과가 아직 필요하다.

C11 VS2017 Legacy 전용 프로필에서 실행한 첫 통합 검사는 [원본](../artifacts/ide/run-cb5f05a756d342aa859f3eecd4e86b1b/result.json) **145/145**, 실패 0건이다. 검사 입력 DLL SHA-256 `A09DF16CF6E766413622DE06ED96B3ED9D06B9399C168CCE3332BF734295D4B6`가 C11 Legacy Release와 일치한다. 이는 새 IDE-06·CHAT-09·IMAGES-06 계약을 포함한 실제 호스트·합성 CLI 통합 결과지만, 실계정 AI 요청이나 별도 Kakao/Dracula 테마 설치·적용 검사의 결과는 아니다.

VS2017 일반 SKU도 [실제 IDE 원본](../artifacts/ide/run-dafa9b7f8b8f43248bd3ec7035a11c70/result.json) **145/145**, 실패 0건이다. 입력 DLL SHA-256 `01E737C3979FA232510386D8296A8AB9CC9B0E275ECE827D86256ED4ED0E49E6`가 C11 2017 Release와 일치한다.

VS2019도 [실제 IDE 원본](../artifacts/ide/run-00fca72ee02c499bb241304c1741bab5/result.json) **145/145**, 실패 0건이다. 입력 DLL SHA-256 `4B2F3D0C3AC04749D62FB970B1143F97DEDEABF873EEB8E66B53CA5D19859B7F`가 C11 2019 Release와 일치한다.

VS2022도 [실제 IDE 원본](../artifacts/ide/run-fc601cc927554c69afdf4ea93c673c26/result.json) **145/145**, 실패 0건이다. 입력 DLL SHA-256 `7137C6E16E8F080E8D82330F63BE2142D2666E21C55005492571CB936106B5F6`가 C11 2022 Release와 일치한다. 네 결과 모두 `LiveCodexTest=false`, `LiveClaudeTest=false`이며 실제 호스트 안의 합성 CLI·전용 프로필 검사다.

## Role.md 최신 요청의 적용 범위

[최신 16개 항목](Role.md)의 번호 9·10은 원문에서 비어 있다. #1–8·#11–16을 이전 [C1 항목별 기준](CodexOrig2_TASK-058_Audit_C1.md)과 C11 근거로 재판정한다.

| 항목 | 현재 감사 판정 | 근거·경계 |
|---|---|---|
| #1–6 팝업·Markdown·버튼·여백·탭·전환 | 실제 호스트 기능 검사 통과, 안내서 열기는 실패 | 네 IDE의 145/145에 창·팝업·대화·Markdown·탭·설정 복원 경로가 포함된다. 설치된 PDF 안내 열기는 R58-05로 실패한다. 모든 DPI·크기·키보드·팔레트의 픽셀 품질도 전수 검사한 것은 아니다. |
| #7 연속 입력·FIFO | 확인 | C11 큐 58/58과 네 IDE 통합의 초안 보존·실행 경계를 확인했다. 긴 실사용 스트레스는 별도다. |
| #8 완료 알림 | 개인정보·요약 확인, 표시 품질은 부분 확인 | C11 네 DLL 120/120. C9 native 합성 요청은 브랜드별 `BalloonTipShown` 1회와 정리 성공. C11 실제 알림 픽셀·장기 중복률은 미검증이다. |
| #11–12 부채꼴·hover·모션 | 기능 경계 확인, 물리 포인터·시각은 부분 확인 | C11 실제 IDE의 합성 hover 예약과 명시적 1회 열기 및 tooltip 검사. 실제 포인터 이동 경로·가림·프레임 품질은 별도다. |
| #13 결과를 파일로 저장 | 지원 경로 확인 | C1 형식·경로 경계와 C11 실제 IDE의 승인 저장·원본 보존 경로. Word 화면 렌더 및 모든 저장 대화상자 클릭은 별도다. |
| #14 Claude | 비로그인 합성 경계 확인, 실계정 제외 | 네 실제 IDE의 합성 Claude 어댑터는 통과. 사용자 지시에 따라 로그인·실제 답변은 검사하지 않는다. |
| #15 사용자 요청의 대화형 표시 | 확인 | 네 IDE의 실제 사용자·AI 턴과 Light/Dark 말풍선·대비 검사가 통과했다. |
| #16 IDE 테마 설치·적용 | 검사한 세 IDE에서 적용 확인 | C11의 VS2017·2019 각각 26/28, VS2022 25/25. 두 테마의 실제 GUID·대표색·pending 소비·정리 통과. 2017/2019의 raw 비테마 export 완전 일치 실패는 기존값 보존과 구분해 보존한다. 물리 버튼 클릭·설치 관리자 완료·고객 15.7.1 PC는 별도다. |

검증 범위와 구조화 증거는 [C11 JSON](CodexOrig2_TASK-058_Audit_C11_Evidence.json)에 기록한다.
