# TASK066 실제 설치 PDF 경로 실패와 C12 해결 확인

담당 CodexOrig 보조 TASK066. 2026-10-20 04:07~04:10 KST에 Root가 요청한 읽기 전용 검사를 수행하고 동결했다. C11의 실제 설치 실패는 그대로 보존했고, C12는 별도의 private VSIX 추출 사본에서 네 SKU의 실제 서비스 호출 성공을 확인했다. 제품 소스·기존 TASK061 보고·공유 빌드·패키징 스크립트·기존 release는 수정하지 않았다.

## C11 실제 설치본은 실패했다

Root가 지정한 VS2017 시험 프로필 `15.0_c1f368d6CodexCodeTestdafa9b7f`의 `Extensions/e2bbarck.sir` 제품만 읽었다. 설치 DLL SHA256은 `01E737C3979FA232510386D8296A8AB9CC9B0E275ECE827D86256ED4ED0E49E6`이며 C11 VS2017 Release DLL과 같다. 사용자 기본 프로필은 조회하지 않았다.

새 Windows PowerShell 프로세스 PID 7348에서 **실제 설치 DLL**을 로드하고 기본 생성자로 CliGuideService 객체를 만든 뒤 `GetPdfPath()`를 호출했다. `installationDirectory`를 다른 경로로 지정하거나 PDF를 복사·개명하지 않았다. 결과는 **System.IO.FileNotFoundException**, `ProductCallSucceeded=false`, 자식 종료 코드 **1**이다.

서비스 상수는 `VisualStudio_AI_Assistant_설치_사용설명서.pdf`를 요구하지만 이 파일은 없다. 실제 설치된 파일은 `VisualStudio_AI_Assistant_설치_사용설명서_v0.12.1_미리보기.pdf` 하나다. 실제 PDF는 1,406,441 bytes이고 SHA256은 `F853789CE823A1EABE8B4E98226064A9E86F808389CA77033DF83925744F2372`다. 설치 DLL·PDF는 검사 전후 해시가 유지되었다.

원본 예외·stack trace·파일 목록은 `artifacts/task054/pdf-path-regression-c11/result.json`, C11 동일성·종료 코드·실패 재현 관측은 `regression-observation.json`에 보존했다. 당시 probe 사본과 OBSERVATION.md도 같은 폴더에 남겼다. **실패를 제품 PASS로 재분류하지 않았다.** C12 결과로 C11 증거를 덮어쓰지 않았다.

## C12 패키지 사본 네 종은 같은 서비스 호출에 성공했다

Root가 C12의 canonical PDF alias와 네 CSPROJ Content 수정을 마친 뒤 제공한 `artifacts/task054/candidate12`만 사용했다. 새 evidence 폴더 `artifacts/task054/pdf-path-resolution-c12` 아래에 SKU별 VSIX 전체를 별도로 추출했다.

ZIP 항목은 한 번만 URI decode하고, 절대 경로·상위/현재 세그먼트·ADS/장치/예약 이름·제어 문자·모호한 끝 공백/점·중복 별칭·reparse/symlink 속성을 거부했다. 해석된 전체 경로가 새 추출 루트 내부인지 확인하고 파일은 CreateNew로 기록했다. 원본 VSIX나 Release 출력은 변경하지 않았다. 추출 DLL이 해당 Release DLL과 같은지도 확인했다.

각 SKU마다 새 Windows PowerShell 프로세스에서 추출된 **실제 제품 DLL**을 로드하고, 기본 생성자 CliGuideService의 `GetPdfPath()`를 호출했다. 네 경우 모두 성공했고 자식 종료 코드는 0이다.

| SKU | 새 프로세스 PID | 제품 호출 | 패키지 PDF와 반환 경로·SHA 일치 | VSIX/Release DLL 변경 |
| --- | ---: | --- | --- | --- |
| VS2017 Legacy | 51264 | 성공 | 일치 | 없음 |
| VS2017 | 45388 | 성공 | 일치 | 없음 |
| VS2019 | 34132 | 성공 | 일치 | 없음 |
| VS2022 | 44588 | 성공 | 일치 | 없음 |

모든 VSIX의 PDF는 정확히 한 개이며, decode한 경로는 `Resources/CliGuide/VisualStudio_AI_Assistant_설치_사용설명서.pdf`다. 각 서비스가 반환한 실제 파일 경로가 그 파일과 같고, PDF SHA256은 네 종 모두 `F853789CE823A1EABE8B4E98226064A9E86F808389CA77033DF83925744F2372`로 일치한다. 크기는 모두 1,406,441 bytes다.

VSIX/DLL 해시, 각 추출 파일 목록, 프로세스별 원본 서비스 호출 결과는 `artifacts/task054/pdf-path-resolution-c12/result.json`과 각 SKU 하위 `extracted-files.json`, `service-call/result.json`에 보존했다. 상세 결합 증거와 원본 evidence 해시는 `MarkdownReport/CodexOrig_TASK-066_Validation.json`에 기록했다.

## 검사 범위

C11 결과는 **실제 시험 프로필 설치본**의 실패다. C12 결과는 **private extracted product**의 서비스 호출 성공이며 실제 IDE 설치 검사라고 확대하지 않는다. Root가 진행하는 C12 실제 IDE 네 종의 GUIDE01 검사는 이 결과와 별개다.

GUI·PDF 앱·설치기·CLI·AI 계정·네트워크는 사용하지 않았다. PDF를 화면에 렌더하거나 클릭해 연 결과도 주장하지 않는다. 새 시험 스크립트와 별도 evidence 및 이번 TASK066 보고만 만들었다. 원본 실패를 보존하고 읽기 대상의 해시가 변하지 않았음을 확인했다.

Root는 실제 IDE 검증과 최종 감사·통합 빌드/컴파일·최신 Release 제작·공개 배포·익명 다운로드 해시 검증까지 마감한다. 이 하위 작업은 공개 배포를 수행하지 않았다.
