# TASK072 — Dracula 편집기 가독성 개선

담당: CodexOrig. 시작: 2026-10-23 16:21 KST. 새 Preview의 실제 게시·익명 다운로드 검증과 승인된 구버전 정리를 완료했다. 정식 및 모든 고객 환경의 검증 완료를 의미하지 않는다.

사용자 요청은 Dracula에서 기록 보기의 변경 영역과 스크롤 위치 표시를 읽기 쉽게 하고, C# 일반·블록·XML 문서 주석을 비슷한 계열의 서로 다른 색으로 표시하며, 문법 오류에 추가 표시를 제공하는 것이다. 이번 범위의 기록 보기는 Visual Studio의 native 변경 비교로 해석했다.

## 구현

- 추가 줄 면 `#243B38`, 추가 단어 전경/면 `#50FA7B`/`#294D44`; 삭제 줄 면 `#452D40`, 삭제 단어 전경/면 `#FFB3D9`/`#63384C`를 사용한다. 변경 단어 대비는 각각 6.83:1, 5.77:1이다. overview 변경 표식은 초록·분홍, viewport 윤곽은 보라 `#BD93F9`로 구분한다.
- C# 일반 주석은 `#A0A6B5`, 블록 주석은 `#B5BDD4`, XML 문서 본문은 `#C8D5F2`, XML 이름·구분자는 `#B9C8E8`이다. 기존 Roslyn 분류를 읽고 원래 comment 범위의 시작 두 문자만 확인한다. 문자열과 XML 문서 분류를 다시 파싱하지 않는다.
- 실제 언어 서비스의 오류 범위에 분홍 `#FF79C6` 1px 테두리를 추가한다. 배경을 채우지 않으며 기존 오류 squiggle을 유지한다. warning·hint·suggestion을 오류로 승격하지 않는다.
- Dracula에서 오류·변경 추적 보기 옵션을 뷰 범위로 활성화한다. VS2019/2022의 별도 `Diff/View/ShowDiffOverviewMargin`은 정의된 경우에만 활성화한다. 다른 테마로 전환하거나 뷰를 닫을 때 원래 로컬 값·상속 상태를 복원하고, 사용자의 중간 변경은 보존한다.
- 작은 독립 MEF DLL 한 개를 테마 VSIX에 포함한다. Editor SDK DLL/PDB, 프로젝트 소스, AI/CLI/계정 의존성을 추가하지 않는다.

## 호스트 차이와 수정 근거

초기 후보의 실제 IDE 실패를 보존했다. C1 VS2017 187/6, C2 VS2017 192/2, C3 VS2019 210/2, C4 VS2022 191/21을 성공으로 계산하지 않는다. 테마 GUID 조회 경로, native comment 형식 우선순위, 배경 alpha 손실, 공유 IDisposable 태거 수명, 별도 diff 옵션과 VS2022의 view 전용 Roslyn 분류 경로를 각각 확인해 보완했다.

VS2022는 buffer의 일반 분류 조회가 비어 있고 native view 분류를 사용한다. 버퍼 우선 경로를 유지하면서 빈 결과일 때만 공개 뷰 집계기를 사용한다. 세션 범위 query guard를 집계기 생성 전부터 지연 열거의 완료까지 유지하고 finally로 복원한다. 자체 주석 태거의 재진입과 자체 분류를 제외하고, 즉시 이벤트의 자체 sender 및 동기 발행 중 되돌아오는 알림을 차단한다. 테마 전환·종료 때 두 원천을 모두 해제한다. 소비자별 facade는 내부 태거를 해제할 권한이 없고 뷰 세션이 수명을 소유한다.

native 차이 보기 검사도 호스트별 private field 이름과 정확한 RightHost를 구분하도록 보완했다. 이는 테스트의 읽기 전용 관측이며 제품은 내부 구현 reflection을 사용하지 않는다. 큰 파일 검사는 비동기 native 분류 준비 관찰과 네 번의 계측을 분리하고 최초 빈 결과·query 수를 기록한다. 준비 15초, 각 동기 query 10초의 상한과 실제 native/custom/최종색 조건을 유지한다. process cold start 또는 이전 버전 대비 성능 개선을 주장하지 않는다.

## 현재 검증

최종 후보는 `artifacts/task072/candidate5`, 테마는 `themes8`이다. 제품 입력 160개, 제품 네 SKU와 독립 테마 세 SKU의 MSBuild 종료코드·경고·오류는 모두 0이다. MEF 소스 SHA-256은 `3EDCA6D67FE57FA1E6DF59B361F0F0B415CC82F61A62EC6BF67935758680ED41`이다.

테마 패키지 계약 91/91, 실제 VS2017 테마 DLL의 fake SDK 계약 125/125가 통과했다. 이 DLL 계약은 실제 Roslyn/IDE 시험과 구분한다. 최종 기본 실행기 검사는 `artifacts/tl/aae610006a`에서 27/27 묶음 통과다. 첫 25/27 원본은 보존했다. 두 구형 UI 검사는 이전 공개 0.14.1의 사용자 말풍선·입력란 동일색 계약을 반영하지 못해 헤더 색과 비교하고 있었다. 제품 UI는 기존 공개 소스와 동일하며, 검사는 실제 두 표면끼리 비교하도록 정정했다. 배치·본문 투명·로고·복사 검사는 유지했다.

동일 C5/themes8의 실제 native 검사는 VS2017·2019·2022 각각 273/273, 합계 **819/819** 통과다. 세 프로세스 모두 정상 종료했으며 강제 정리는 0이다. 제품 160개 입력과 실행 복사본·패키지 해시가 그대로임을 확인했다. [최종 native 원본 집계](CodexOrig_TASK-072_Runtime.json)와 [상세 보고](CodexOrig_TASK-072_Runtime.md)를 참고한다. 기존 C4 집계의 731/21 실패 원문과 SHA도 별도 보존했다.

최종 14개 배포 자산은 크기·SHA가 모두 일치하고, `UserDistribution/v0.14.2`에는 프로젝트 소스·PDB·계정·개발 증거 없이 69개 파일(고정 CLI 46개, payload checksum 68개)을 생성했다. 새 기본 경로를 선택하는 실행기 `--run foundation`도 최종 0.14.2 VSIX/DLL의 해시로 45/45 통과했다. 실행기 자체 비GUI 계약은 55/55, 설치 로그 계약은 PowerShell 5.1/7에서 각각 23/23이다.

같은 최종 도우미로 네 제품 SKU의 실제 IDE 기능 assertion이 각각 172/172, 합계 **688/688** 통과했다. Legacy·VS2017은 정상 종료 2건, VS2019·VS2022는 결과 보존 뒤 테스트 전용 PID/RootSuffix만 10초 종료 유예 후 강제 정리 2건이다. 네 호스트가 모두 정상 종료했다고 주장하지 않는다. 최종 설치 EXE의 패키지·버전 선택 계약 **17/17**, 공식 격리 VSIXInstaller의 확장·테마 설치 **6/6**과 실제 TEMP 로그/보존 사본 SHA 6/6을 확인했다. EXE에 내장된 설치 스크립트·체크섬과 실제 검사의 외부 실행 입력도 바이트 동일하다. 고객 15.7.1 실제 설치 대신 합성 SKU 경계는 **35/35**로 구분한다.

최종 FullOffline ZIP의 Windows AppContainer 검사 **6/6**에서는 동봉 `Install-OfflineCli.ps1`을 그대로 사용했다. 작업 중 스크립트 교체는 하지 않았고, capability 0 토큰에서 설치 전후 TCP가 10013으로 차단되며 CLI 설치·멱등 재설치·버전 실행이 통과했다. 이 검사는 실제 VSIX 통합 EXE 실행이나 물리 케이블 차단 시험과 구분한다. 소유 AppContainer 프로필 정리와 폴더 ACL 복원도 확인했다.

첫 main VS2019 실행은 `SIDEBAR-05` 뒤 열린 대화 목록의 `Single` 조회 예외로 중단됐다. 원본 `artifacts/ide/run-322b1b77fa9d4bac8ecbd9d67dee5fea`를 보존했다. 후속 선택·provider·실제 assertion 조회 배열·읽기 경고·workspace 경로 진단에서 누락이 재현되지 않았고, 합격 조건을 유지한 VS2019 두 재실행과 최종 네 SKU가 통과했다. **최초 직접 원인은 미확정이며, 재실행 성공을 원인 해결로 적지 않는다.** 제품 Chat/Views/Services/Providers는 이전 공개 0.14.1 소스와 diff 0이고 이 사례 때문에 제품을 수정하지 않았다. 후속 두 VS2019 및 VS2022 테스트 프로세스의 종료 지연도 별도 관찰로 유지한다.

## 설명서와 배포 범위

README, 사용자 안내, 오프라인 안내와 46쪽 PDF를 0.14.2-preview.1에 맞췄다. PDF SHA-256은 `155DF0B5DD2C2C4AB958FE09958853C73A7C8EE1DBED0AB7022E21E18FF5B0DE`이며 원본·제품 동봉본이 같다. 최신 페이지를 렌더해 Root가 설치 순서와 팔레트·테두리 설명, 초록/분홍 변경 표식 및 보라 viewport의 의미를 시각 확인했다. 합성 설명 그림은 실제 IDE 캡처와 구분한다.

새 Preview 14개 자산과 소스 없는 `UserDistribution/v0.14.2`를 생성·검증했다. FullOffline ZIP은 334,353,287 bytes / SHA-256 `65C4965218BE935FD5895162DAB9B55DB86E334009973186527F9C02A4FA3434`다. [v0.14.2-preview.1](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.14.2-preview.1)을 2026-10-23 23:55:14 KST에 실제 공개했고 23:56:06 KST에 인증 없는 다운로드 **14/14 크기·SHA 일치**를 확인했다. Release ID는 `421968926`, 제품 source/tag commit은 `5300fe344f18b39856d6183d9c2fc27be861753a`다. 기존 안정판 v0.11.1의 latest 상태를 유지한다. [실제 게시 기록](CodexOrig_TASK-072_Deployment.json).

새 공개 검증 뒤 Root가 별도 계획의 canonical 절대 경로·재분석 링크 0·정확한 원격 자산 ID를 확인했다. 2026-10-23 23:58:33 KST에 구0.13.0·0.14.0의 원격 첨부 **27개**, 로컬 `releases/v0.13.0`·`v0.14.0` **두 폴더 47파일**만 승인 범위대로 삭제했다. 새0.14.2와 직전0.14.1 전체 로컬 트리 및 원격 14개 첨부, 이전 폴더의 untracked FullOffline ZIP, 안정판, Release 항목·tag·Git 이력·개발 증거·UserDistribution를 보존 확인했다. [정리 계획·실행 결과](CodexOrig_TASK-072_ReleaseCleanup.json). Deployment의 직전 보존 필드는 게시 시점이며 이후 승인된 구버전 정리는 별도 journal로 구분한다.

CodexOrig2의 [독립 감사](CodexOrig2_TASK-072_Audit.md)는 위 잔여 관찰을 명시하는 조건으로 **Preview 게시 전 기술 게이트 조건부 통과**다. 정식·모든 환경의 검증 완료라는 판정은 아니다. [기계 검증 기록](CodexOrig_TASK-072_FinalValidation.json)은 게시 전 로컬 검사와 이후 실제 공개·정리 상태를 구분한다. 최종 저장소 README·사용 안내·오프라인 안내의 게시 전 조건문을 공개 완료 표현으로 갱신했고, 동봉 문서는 출하 시점의 동결 바이트를 유지했다.

고객 VS2017 Professional 15.7.1, 고객 별도 오프라인 PC, 실제 AI 계정 응답·운영 DB·SVN/TFVC, 물리 포인터·모니터 픽셀은 이번 로컬 자동 검사와 구분한다. 사용자 요청에 따라 Claude 로그인 없이 검사한다. 다른 역할의 파트 종료는 전체 완료가 아니며, 빌드·최신 Release 제작·실제 배포·공개 다운로드 확인까지 Root가 통합 마감한다.

## 완료 기록
시작 기록은 2026-10-23 16:21 KST(분 단위), 이 기록은 2026-10-24T00:02:09.6111133+09:00이며 경과는 약 461 분이다. 중단·재개 시간을 포함한 벽시계 경과이며 연속 작업 시간으로 주장하지 않는다. 사용자 지시의 빌드·최신 Release·실제 배포·공개 다운로드 해시·승인 정리까지 Root가 수행했다. Orig3·4·연구 및 CodexCode에 최종 링크와 전체 완료 기준을 직접 안내했고, Orig2에 사후 감사를 요청했다. 제품·배포 자산·PDF·전달 폴더는 동결하며, 후속 변경은 새 버전과 검증·배포 계획으로 진행한다.

CodexOrig2 사후 감사 상태: PASS_PUBLIC_PREVIEW_TASK072_WITH_DOCUMENTED_RESIDUALS. 공개14익명SHA·직전보존·승인정리까지 TASK072 Preview 범위 통과이며, 첫Single/종료지연/고객환경한계는유지한다.
