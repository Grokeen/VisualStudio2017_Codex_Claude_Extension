# TASK-037 / CodexOrig2 / VS2017 15.7.1 호환성 후속 독립 감사

감사자 **CodexOrig2**. 2026-10-19 KST. 사용자의 진행 재개 요청과 TASK-036의 업데이트 불가·VS2017 Professional 15.7.1 지원 작업을 대상으로 한다. 기존 2차·3차·TASK-034 보고는 보존하며, 이번 결과를 별도 기록한다.

**판정: 고정 C17의 이번 검사 범위에서 추가 확정 제품·배포 결함은 없다. C16의 패키징·설치 선택기 보완을 확인했다. 실제 고객 Professional 15.7.1 설치·실행은 미검증이다.** ZIP README에 대한 최초 R37-01은 감사자의 입력 대조 실수로 철회했다. 아래 정정 기록과 원문을 보존한다.

## 범위와 고정 기준

- Role.md 08:52의 `NoApplicableSKUsException`, 이후 공유 작업 기록에 확인된 고객 버전 15.7.1·업데이트 불가 조건, 별도 Legacy SDK 패키지와 자동 선택기를 대조했다. 고객 빌드는 `15.7.27703.2000`이며 `15.7.27703.1`은 15.7.0이다. [Microsoft 릴리스 이력](https://learn.microsoft.com/en-us/visualstudio/releases/2017/vs2017-relnotes-history)을 확인했다.
- C16과 C17의 입력 각 **122개**를 `C:/dydgyqud/AO37_20261019/`에 복사하고 선언 SHA256과 전부 일치함을 확인했다. 두 후보 사이 입력 변경은 Legacy csproj **1개**다. C17의 4대상×Debug/Release 8빌드 0오류·0경고는 통합 담당자의 빌드 기록 확인이다. 감사자는 공유 빌드를 실행하지 않았다.
- TASK-034의 C14 대비 공통 C#/XAML 기능 구현 변경은 없고, AssemblyInfo 버전·세 기존 manifest와 신규 Legacy 프로젝트/manifest가 변경·추가됐다. 이전 기능 감사의 범위·계정 및 운영 연동 미검증 한계는 유지한다.
- 설치기 동결 SHA256: `E85D78DC5884E4E398657CF1F439A448035711F6E3226FDE0E53341CB1241226`. 최초 C16 설치기 SHA256은 `EF98FDB71771308C22B9C05CA61D0F3C5981C49C49B267AE14CB68F09274E5D6`이다.

## 직접 확인한 결과

| 검사 | 결과 | 의미·한계 |
|---|---:|---|
| C16 / C17 실제 패키지를 읽는 독립 선택기 fixture | 각 40/40 | 3에디션, 고객 정확 빌드, 27702/27703·28306/28307 경계, 2019/2022, 아키텍처·CoreEditor·불완전 설치·무결성 거부 |
| 구형 vswhere / 설치기 대역 CLI | 15/15 | C16 `-utf8` 거부 재현, C17 옵션 제거·한글/공백/& 경로·4대상 정확 인수·인코딩 복원·기존 JSON 보존·명시 폴더 고정. 실제 IDE/설치기 실행 아님 |
| C17 네 VSIX·제품 DLL ↔ 고정 Release 산출물 | 4/4 | 배포 폴더와 후보의 정확 바이트 연결 |
| Legacy ↔ 일반2017 공통 페이로드 | 43/43 | AvalonEdit·WebView2 관리 DLL 4개, native loader, worker, 라이선스, 도움말/그림 포함 바이트 일치 |
| C17 ZIP | 31/31 바이트 일치 | 16,925,451 bytes, 중복·경로 이탈 entry 0, 필수 8항목 존재, Release checksum 및 내부 checksum 30/30 일치. 실제 ZIP README 직접 확인 |
| 보호 원본 | 54/54 SHA 보존 | 기존 CodexOrig2 감사·Role·기존 릴리스. 감사자 제품·공유 bin/obj·GUI·실제 설치·계정·배포 변경 0 |

선택 규칙은 VS2017 build 28307 미만에서 Legacy, 이상에서 일반2017이다. Legacy manifest는 `[15.0.27703.0,16.0)`, 일반2017은 `[15.0.28307.0,16.0)`이며 둘의 ID·버전은 동일하다. 범위 중첩은 남아 있으나 자동 선택기와 문서가 선택 및 동일 IDE 중복 설치 불가를 설명하고, Legacy의 15.9 호스트 검사도 있다. 따라서 범위 중첩 자체를 확정 결함으로 분류하지 않았다.

Community target이 Professional/Enterprise에도 적용되는 에디션 상속, VS2017 이후 minor=0인 범위 비교, 2022 amd64 구분은 [Microsoft VSIX 스키마](https://learn.microsoft.com/en-us/visualstudio/extensibility/vsix-extension-schema-2-0-reference?view=vs-2022)와 대조했다. 기존 15.9 하한이 고객 15.7.1을 제외하는 조건을 확인했지만, 고객이 실행한 정확 VSIX SHA와 전체 설치 로그를 확보한 것은 아니다.

## C16 문제와 C17 보완 확인

1. **런타임 DLL 누락: C17 패키징 보완 확인.** C16 Legacy는 46개 entry이며 AvalonEdit·WebView2 관리 DLL 4개가 없었다. 실제 15.9 IDE의 MarkdownEditor 생성 시 AvalonEdit `FileNotFoundException` 실패는 TASK-036이 먼저 발견·인계한 결과다. 감사자가 원문을 읽고 누락을 독립 확인했다. C17은 50개 entry로 일반 패키지와 공통 43파일이 일치한다. C16 실패 원문은 `C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/ide/run-e85ebca58020478da2ec93aa8266a183/result.json`에 보존됐다.
2. **구형 vswhere: C17에서 보완 확인.** CodexCode가 먼저 알린 `-utf8` 호환 문제를 대역 프로그램으로 재현했다. 이 옵션은 [Microsoft vswhere 설명](https://github.com/microsoft/vswhere/wiki/Encoding)에 따르면 2.5 이상이다. C17은 옵션 없이 호출 동안 UTF8 console encoding을 설정하고 성공·실패 모두 복원한다. 실제 고객의 vswhere 버전은 모른다.
3. **명시 폴더의 조용한 대체: C17에서 제거 확인.** 체크섬이 없는 명시 폴더 안에 유효한 `C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/releases/v0.11.1`을 심은 경우에도 해당 폴더를 바꾸지 않고 `InvalidPackage`를 기록했다. BAT는 저장소/압축본 위치를 명시적으로 선택한다. PS 기본은 진단 전용이고 BAT는 `-Install` 진입점이다.

구버전 런타임 SDK는 Shell/Framework **15.7.27703**, Threading **15.6.31**로 유지됐다. C17에서 바뀐 **BuildTools 15.9.3032는 빌드 전용**이다. 동결 csproj, 읽기 전용으로 보관한 resolved `project.assets.json`, 실제 Legacy 제품 DLL의 Shell 15.0.0.0·Threading 15.6.0.0·EnvDTE 8.0.0.0 참조를 대조했다. VS SDK/Threading runtime DLL을 VSIX 안에 별도 넣지 않았다. 빌드 분석기 15.8 및 `AvoidAsyncVoid` 억제를 런타임 SDK 상향으로 해석하지 않았다.

## 감사 정정: R37-01 철회

최초 ZIP README 링크 지적은 감사자가 저장소용 `C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/releases/v0.11.1/README.md`를 ZIP 내부 파일로 오인해 발생했다. TASK-036의 이의 제기 후 **같은 ZIP SHA256 `62B4D4A231B8D8338FCF0D1B241B755A9F2603E602D6671DD55D0EE46C871E71`**에서 `ZipFile.GetEntry('README.md').Open()`을 직접 읽고 원시 바이트를 보존했다.

실제 entry는 **2,491 bytes**, SHA256 **`C624B36217AB68D5EED44BDB4AF065CBE1477C1B26F1987803B9CA4C4532013B`**인 전용 설치 묶음 README다. 소스 매핑도 `C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task036/bundle-README.md`다. `../../` 링크가 없고 진단·CLI는 루트에 있는 파일명을 안내하며 사용자 설명서는 정상 온라인 URL을 안내한다. **R37-01을 철회하고 미해소 결함 0건으로 집계한다. 제품·ZIP 변경 요청도 취소한다.**

잘못된 최초 분석 JSON `bundle-readme-links.json`과 `report-draft-withdrawn-R37-01.md`를 추적용으로 보존한다. 정정된 입력·판정은 `bundle-readme-correction.json`에 분리하고, 공유 WORK_STATUS와 사용자 진행 안내에도 철회를 기록했다. 최초 지적을 제품 수정으로 종결했다고 표현하지 않는다.

## 통합 담당자 실행 자료와 미검증 범위

TASK-036의 C17 Legacy 및 일반2017은 실제 VS2017 **15.9 호스트**에서 각각 145/145, 2019·2022도 각각 145/145를 통과한 JSON을 확인했다. 네 로드 DLL SHA가 감사 대상과 같다. 감사자가 해당 GUI를 실행한 결과가 아니며, Legacy의 15.9 성공은 고객 Professional 15.7.1의 성공 증거가 아니다. 전체 묶음은 첫 실행 **15/16** 후 foundation의 신규 Legacy manifest 선택 하네스를 수정하여 **foundation 45검사/1묶음 재실행 성공**으로 합산 16묶음 성공이다. 처음부터 전체16이 한 번에 통과했다고 기록하지 않으며 최초 실패 로그를 보존한다. 제품 패키지 변경은 없었다.

고객 실제 Professional 15.7.1의 설치·패키지 로드·메뉴·도구창·문서 동기화, 고객 .NET/WebView2 환경, 실제 CLI 계정 호출·운영 SQL/TFVC·배포는 이번 감사가 확인하지 않았다. `Ready`는 사전 진단이며 .NET/VSIX의 최종 의존성 검사는 공식 VSIXInstaller가 수행한다고 스크립트와 문서가 명시한다. checksum을 게시자 서명으로 설명하지 않는다.

감사용 하네스 초기에 dot-source의 동명 매개변수, 동일 어셈블리 ID의 ReflectionOnlyLoadFrom 제약, PS5의 BOM 없는 한글·native stderr·깊은 JSON 직렬화 문제를 만났다. 하네스 변수를 분리하고 어셈블리별 자식 프로세스·UTF8 BOM·예상 실패 처리·원시 문자열 기록으로 수정했다. 초기 출력 폴더·로그를 보존했으며 제품 실패로 집계하지 않았다. 재실행은 같은 고정 제품 바이트를 사용했다.

근거 묶음: [TASK-037 Evidence](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-037_Evidence.json), `C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task037/`의 독립 스크립트·결과 JSON 및 `C:/dydgyqud/AO37_20261019/`의 고정 사본. 기존 감사/사용자 원문/실패 기록은 유지한다.

## 감사 작업 종료

시작 2026-10-19 09:03:45 +09:00, 종료 2026-10-19 09:18:19 +09:00, 경과 00:14:34. TASK037의 독립 감사·보고·검증은 완료했다. 추가 확정 결함은 0건이며 고객 실제 15.7.1 성공은 미확인이다. 제품·공유 빌드·GUI·설치·배포 소유권을 취득하지 않았고 감사 담당 소유를 반납한다. 최종 배포·원격 바이트 검증은 TASK036이 담당한다.
