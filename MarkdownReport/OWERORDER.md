# 개발·검증 보고

## TASK-003 — CodexOrig / 2026-10-18 01:33:36 KST

VS2017 Community 15.9.83, VS2019 Community 16.11.60, VS2022 Community 17.14.42를 각각 설치했습니다. 한국어와 WPF/.NET 데스크톱·확장 개발 SDK·.NET Framework 4.6.2 Targeting Pack/SDK를 포함했습니다. 세 설치 모두 종료 코드 0이며 재부팅이 필요하지 않습니다.

세 버전의 설치 상태와 필수 구성 요소를 확인했고, 각 MSBuild 및 IDE 자체의 명령행으로 .NET 4.6.2 WPF 테스트 솔루션 빌드를 모두 통과했습니다. 각 IDE 빌드 결과는 성공 1건·실패 0건·종료 코드 0입니다.

재검증은 저장소 루트에서 실행합니다.

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\scripts\Test-VsixTestEnvironment.ps1 -BuildSmokeTest
```

IDE 명령행 검증 절차와 경로는 `docs/TEST_ENVIRONMENT.md`, 최종 결과는 `MarkdownReport/CodexOrig_TASK-003_Environment.json`과 `CodexOrig_TASK-003.md`에 기록했습니다.

GUI 도구에서 접근 거부(0x80070005)가 발생해 실제 메뉴·확장 도구 창·EnvDTE 편집 화면은 미검증입니다. VS2017은 첫 실행 명령행에 30일 라이선스 안내가 표시되어 사용자 계정으로 로그인·라이선스 상태 확인이 필요합니다. 이 작업에서 GitHub 배포는 수행하지 않았습니다.


## TASK-002 — CodexOrig / 2026-10-17 22:19:55 KST

VSIX 원본의 버전 범위·아키텍처·SDK 선언을 확인하는 검사 도구를 추가했습니다. CodexCode가 진행 중인 VSIX 구현 소스에는 손대지 않았습니다.

Windows PowerShell 5.1과 PowerShell 7에서 각각 44개 테스트가 모두 통과했습니다. 실제 Phase 1 소스는 VS2017 정적 검사를 통과했으며, 최종 확인 중 담당자의 추가 변경을 감지하여 최신 소스로 재검사했습니다. VS2019·VS2022는 현재 미선언입니다. 정적 검사 결과를 실제 설치·실행 성공으로 보고하지 않았습니다.

테스트는 저장소 루트에서 아래 명령으로 재현할 수 있습니다.

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\compatibility\Test-VsixCompatibility.Tests.ps1
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\scripts\Test-VsixCompatibility.ps1 `
    -ManifestPath .\src\VS2017.AIAssistant\source.extension.vsixmanifest `
    -ProjectPath .\src\VS2017.AIAssistant\VS2017.AIAssistant.csproj
```

VSIX 빌드는 TASK-001 담당자가 진행 중이며 실제 IDE 설치·도구 창 실행은 해당 환경에서 추가 확인해야 합니다. CodexCode가 작업 중이므로 `Role.md` 규칙에 따라 GitHub 배포는 수행하지 않았습니다.

상세 결과: `MarkdownReport/CodexOrig_TASK-002.md`, 사용법: `docs/VSIX_COMPATIBILITY.md`.

## TASK-001 — CodexCode / 2026-10-17 22:25:46 KST

VS2017 15.9용 독립 VSIX 솔루션과 도구 → AI Assistant 메뉴, WPF 도구 창을 구현했습니다. .NET Framework 4.6.2 / C# 7.0 / AnyCPU를 사용하며, Codex·Claude 실제 연결은 다음 단계입니다.

현재 PC에 Visual Studio와 .NET SDK가 없어 임시 전체 Framework MSBuild 및 NuGet 참조 목록 주입으로 컴파일·패키징했습니다. Debug/Release 빌드가 모두 성공했고 패키지·WPF 검사는 각각 22개, CodexOrig 호환성 회귀 검사는 44개 모두 통과했습니다. 밝은/어두운 화면과 320/200픽셀 폭 렌더링도 확인했습니다.

검증 방법은 `docs/DEVELOPMENT.md`를 따르시면 됩니다. 빌드 후 아래 명령으로 패키지·WPF 검사를 수행할 수 있습니다.

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -STA -File .\tests\foundation\Test-FoundationPackage.ps1
```

설치 후보 파일은 `src/VS2017.AIAssistant/bin/Release/VS2017.AIAssistant.vsix`입니다. VS2017에서 F5·설치·메뉴 실행·창 재열기·도킹·IDE 종료/복원·제거 검증은 아직 필요합니다. 해당 항목을 완료한 것으로 보고하지 않았으며, Role.md의 검증 완료 조건에 따라 GitHub 배포도 보류했습니다. 협업 기록과 CodexOrig의 보고는 보존했습니다.

상세 변경 파일, 빌드 환경의 한계, 로그 및 재현 절차는 `MarkdownReport/CodexCode_TASK-001.md`에 정리했습니다. 다음 작업은 실제 VS2017 환경 검증입니다.
## TASK-004 — CodexCode / 2026-10-18 01:26:19 KST

추가 Role.md의 소스 수정 요구를 반영하여 v0.2.0을 구현했습니다. 현재 파일의 내용을 Codex/Claude CLI에 전달해 수정안을 받고, 변경 비교와 사용자 승인 후 편집기에 적용하며 별도로 저장·되돌리기할 수 있습니다. 원본과 맞지 않는 교체, 중복·겹치는 수정안, 사용자 편집·외부 파일 변경·읽기 전용 상태는 거부합니다.

설치된 VS2017의 MSBuild 15.9와 Roslyn으로 Debug/Release 빌드가 경고·오류 없이 성공했습니다. 편집/CLI/WPF 55개, 패키지·렌더링 29개, 기존 호환성 44개 검사가 모두 통과했습니다. 실제 Codex 계정으로 합성 코드만 전송하여 return 1 → return 2의 정확한 수정안 반환과 승인 전 원본 파일 보존도 확인했습니다.

사용 방법: 기존 소스 파일을 열고 도구 → AI Assistant에서 AI 및 네이티브 CLI exe를 선택합니다. 수정 요청을 입력한 후 비교 탭을 검토하고 승인 체크 → 적용 → 별도 저장 순서로 진행합니다. CLI는 사전에 터미널에서 로그인해야 합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 `
    -MSBuildPath 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe'
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\editing\Test-SourceEditing.ps1
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -STA -File .\tests\foundation\Test-FoundationPackage.ps1
```

고정 설치 후보: `artifacts/source-editing/handoff/VS2017.AIAssistant-0.2.0.vsix`. 이 파일과 해시를 IDE 환경 담당자 CodexOrig에게 인계했습니다. 실제 IDE의 EnvDTE 적용·저장·Undo와 실제 Claude 계정 호출은 별도 검증이 필요합니다. 현재 대상은 VS2017 15.9의 기존 파일 한 개이며 무승인 자동 적용·다중 파일·VS2019/2022 지원은 후속 범위입니다.

세부 변경 파일·로그·재현 절차·검증 한계는 `MarkdownReport/CodexCode_TASK-004.md`, 사용법은 `docs/SOURCE_EDITING.md`에 기록했습니다. 다른 Codex의 TASK-003이 진행 중이므로 Role.md에 따라 GitHub 배포는 하지 않았습니다.

## TASK-006 — CodexOrig / 2026-10-18 02:02:51 KST

Role.md의 추가 요구를 검토하고, IDE를 시작하지 않고 MSBuild로 VSIX를 생성하는 배치 파일을 추가했습니다. `scripts/Build-Vsix.cmd`를 실행하면 기본 VS2017 Release 빌드를 수행하며, `-VisualStudioVersion 2019` 등으로 빌드 엔진을 지정할 수 있습니다. `-PlanOnly`로 실행 없이 경로를 확인할 수 있습니다. 사용법과 로그인 절차는 `docs/BUILD_AND_LICENSE.md`에 정리했습니다.

PS5.1과 PS7에서 한글/공백/특수문자 경로·선택 오류·배치 출력/종료 코드를 각각 16개 검사하여 모두 통과했습니다. 다른 작업자와 충돌하지 않도록 별도 소스 복사본에서 설치된 MSBuild 15/16/17로 실제 Release VSIX를 각각 생성했고 모두 성공했습니다. 세 빌드 로그에는 IDE 라이선스 배너가 나타나지 않았습니다.

재현: `scripts\Build-Vsix.cmd -VisualStudioVersion 2017 -PlanOnly`, `powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\build-entry\Test-BuildEntry.ps1`. 현재 패키지는 VS2017용이며, 세 엔진 빌드 성공을 VS2019/2022 IDE 지원으로 보고하지 않았습니다.

IDE 자체의 안내 해소에는 사용자의 계정 로그인·정상 라이선스 상태 확인이 필요합니다. 배치는 활성화를 대신하지 않습니다. 소스 수정 기능은 v0.2.0에 구현되어 있으며 실제 EnvDTE 검증은 CodexCode TASK-005가 진행 중입니다. 상세 결과는 `MarkdownReport/CodexOrig_TASK-006.md`와 `_Validation.json`에 기록했습니다. 작업 소요 7분 11초, 다른 작업자 진행 중이므로 GitHub 배포는 수행하지 않았습니다.

## TASK-005 — CodexCode / 2026-10-18 02:04:14 KST

구축된 환경에서 실제 VS2017 테스트를 완료했습니다. 고정 v0.2.0을 별도 인스턴스에 설치하고 실제 도구 창의 Codex 호출 → 수정안 비교 → 승인 → 편집기 적용 → 저장을 확인했습니다. 미저장 주석 보존, 다른 파일 전환 후 원래 파일만 적용, 한 번의 Undo, 전용 복구, 적용 전 사용자 편집 충돌도 통과했습니다.

실제 IDE 25개와 기존 편집 55개·패키지/화면 29개·호환성 44개로 고유 검사 153/153 PASS입니다. 최종 테스트 런처를 새 인스턴스에서 재실행한 22개도 통과했습니다. 제품 결함이 발견되지 않아 src/ 및 고정 VSIX는 수정하지 않았습니다.

재현: powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\ide\Test-IdeIntegration.ps1
실제 AI 포함 시 -LiveCodexPath에 로그인된 codex.exe를 지정합니다. 사용자 프로젝트 대신 테스트 스크립트가 만든 합성 소스만 사용합니다. 상세 테스트 케이스는 docs/IDE_TESTS.md, 근거는 CodexCode_TASK-005.md 및 IDE/Repro JSON입니다.

실제 Claude 계정 호출과 VS2019/2022 패키지 지원은 남아 있습니다. 다른 Codex의 TASK-006이 진행 중이므로 Role.md에 따라 GitHub 배포하지 않았습니다. 작업 시간은 2026-10-18 01:49:18 ~ 2026-10-18 02:04:14 KST, 14분 56초 입니다.

## TASK-007 — CodexCode / 2026-10-18 02:07:23 KST

다른 Codex의 TASK-006 완료를 재확인하여 Role.md 6번에 따라 GitHub main에 검증 소스·테스트·보고서와 고정 v0.2.0 VSIX를 반영했습니다. 구현 커밋은 b074155이며 releases/v0.2.0/에 패키지·체크섬·설치 안내가 있습니다. GitHub에서 다시 다운로드한 파일의 크기(39,860바이트)와 SHA-256이 실제 IDE 테스트 파일과 일치했습니다.

실제 Claude 계정, VS2019/2022 지원과 남은 IDE 검증 범위를 명시했습니다. 일반 IDE 설치나 Marketplace 등록은 수행하지 않았습니다. 소요 1분 42초, 세부 근거는 CodexCode_TASK-007.md입니다.

## TASK-009 — CodexCode / 2026-10-18 02:57:20 KST

추가 Role.md의 README 규칙과 프로젝트3~8 요구를 확인했습니다. README는 일반 사용자가 가치를 이해하고 설치부터 사용할 수 있도록 전면 개편했습니다. 실제 제품 화면의 예제 대화 이미지와 상세 사용 안내, 계정 연결·첨부·누적 대화·instruction·승인/저장·되돌리기·문제 해결 절차를 포함했습니다.

제품 구현은 TASK-008과 분담하고, 최종 DLL로 실제Codex 컨텍스트6개·편집71개·화면33개를 독립 재실행해 모두 통과했습니다. 실제 IDE35개 결과도 확인했습니다. 어두운 화면 헤더 및 Ctrl+V 검토 의견은 제품에 반영됐습니다. 다운로드 파일 해시와 문서 로컬 참조 16개 모두 정상입니다.

README.md와 docs/USER_GUIDE.md에서 사용 방법을 확인할 수 있습니다. 최종 검증은 tests/review/Test-LiveChatContext.ps1로 재현하며 합성 데이터만 전송합니다. 실제 Claude 계정·VS2019/2022 지원 한계는 명시했습니다. 소요 25분 38초, 상세 CodexCode_TASK-009.md. CodexCode의 작업은 완료하여 최종 GitHub 배포는 합의한 CodexOrig에 인계했습니다.

## TASK-008 — CodexOrig / 2026-10-18 02:58:58 KST

Role.md의 추가 기능을 v0.3.0에 반영했습니다. Chat을 일반 문서 탭처럼 열고 도킹/분리할 수 있으며, Codex/Claude별로 대화·작성 중 입력·첨부를 보존합니다. 이미지/텍스트 파일을 놓거나 붙여넣고 후속 요청을 보낼 수 있습니다. instruction, AI별 CLI 경로/모델 및 글자 크기는 설정 저장 후 복원됩니다. 계정 연결은 공식 CLI 인증을 사용합니다.

추가 지시에 따라 제가 생성한 C# 주석을 기존 작성자의 name/desc/author/create date/update date 줄바꿈과 콜론 열로 통일했고 생성용 C# 코드까지 확인했습니다. 기존 작성자의 주석 기록은 보존했습니다. README는 TASK-009가 개발자 대상 설명을 사용자 효용·설치·연결·첫 요청·반복 대화·첨부·승인/저장/복구 안내로 전면 개편했습니다.

Debug/Release 빌드는 경고 없이 성공했고 회귀148개와 최종 패키지의 실제 VS2017 검사35개가 모두 통과했습니다. 실제 Codex가 합성 이미지와 규칙·이전 대화·텍스트를 반영하여 정확한 수정안을 생성했고 승인 전 원본도 보존했습니다. 같은 최종 DLL에 대한 독립 컨텍스트6개 및 편집71개/화면33개도 통과했습니다.

실행은 releases/v0.3.0/README.md의 설치 안내를 따르시면 됩니다. 개발 재현은 tests/editing/Test-SourceEditing.ps1, tests/foundation/Test-FoundationPackage.ps1, tests/ide/Test-IdeIntegration.ps1을 사용합니다. 상세 변경/로그/한계/해시는 MarkdownReport/CodexOrig_TASK-008.md와 _Validation.json에 기록했습니다. 실제 Claude 계정과 VS2019/2022 지원은 남아 있습니다. 로컬 검증까지 26분 3초 소요됐으며 GitHub 반영 결과는 후속 완료 기록에 남깁니다.


### TASK-008 최종 배포 완료 — 2026-10-18 03:00:29 KST

전체 소요 27분 34초. TASK-009 종료 및 다른 활성 편집 없음 확인 후 9b3d930 커밋을 origin/main에 반영했습니다. 원격 다운로드 VSIX 66,118바이트 및 SHA-256 A7F1C597BA0CCF7B18AC52DEB29BACE2D6B66E300B9A90C6A55EB5A3E7A88DF2 일치를 확인했습니다. 사용자 요구 기능·README·C# 주석 형식 보정 및 회귀148/148·실제IDE35/35 검증 결과를 함께 배포했습니다.

## TASK-010 — CodexOrig / 2026-10-18 11:45:08 KST

Role.md 추가8~11번을 v0.4.0에 반영했습니다. 설정은 상단/우클릭에서 화면 중앙 팝업으로 열고, 저장·취소를 구분합니다. Chat·팝업·메뉴의 색상/아이콘을 통일했으며 처음 사용에는 네 단계 안내와 다시 보지 않기를 제공합니다. 안내 선택만 저장할 때 기존 계정 경로·모델·규칙은 보존합니다. 텍스트 편집 메뉴와 이미지 Ctrl+V도 유지했습니다.

Debug/Release 빌드는 경고·오류 없이 완료했습니다. 회귀163개, 실제 VS2017에서 실제 Codex를 포함한50개 및 독립 첫 사용 여정28개를 모두 통과했습니다. 세 IDE가 설치된 기존 테스트 환경을 유지하며 현재 배포 패키지는 VS2017용입니다. Claude 실제 계정과 VS2019/2022 확장 지원은 남아 있습니다.

설치 파일은 releases/v0.4.0/에 준비했습니다. 테스트 재현은 tests/editing/Test-SourceEditing.ps1 및 tests/ide/Test-IdeIntegration.ps1, 상세 근거는 CodexOrig_TASK-010.md와 검증 JSON에 있습니다. README·상세 사용 안내·제품 예제 화면은 TASK-011과 분담했습니다. 로컬 검증까지 16분 33초 소요됐으며 TASK-011 완료 확인 후 GitHub 반영 결과를 이어 기록합니다.

## TASK-011 — CodexCode / 2026-10-18 11:45:52 KST

Role 추가 8~11에 맞춰 README와 상세 사용 안내를 v0.4.0으로 갱신했습니다. 우클릭/상단 설정, 화면 중앙 팝업, 저장 전 닫기·Esc 취소, 첫 사용 안내와 다시 보지 않기·수동 재표시를 실제 버튼명으로 설명했습니다. 최종 제품 WPF의 Chat 예제와 단계별 안내 이미지를 넣었고 개발자 설명보다 사용자 설치·사용 순서를 유지했습니다.

제품 구현은 TASK-010과 분담했습니다. 동적 대화 TextBox의 Loaded 연결 검토 의견이 반영됐고, 최종 DLL의 별도 WPF 사용 흐름28개가 모두 통과했습니다. 첫 안내→숨김저장→재생성 생략→수동 재열기·복원, 기존 설정 보존, 저장/닫기, 최소 폭300 배치를 확인했습니다. 실제 IDE50개 및 제품 회귀163개 통과 인계도 확인했습니다.

재현은 Windows PowerShell5.1 -STA로 tests/review/Test-OnboardingJourney.ps1 -AssemblyPath <빌드된 제품DLL>를 실행합니다. 최종 문서참조17개와 패키지 내장DLL/해시가 정상이며 상세 근거는 CodexCode_TASK-011.md·_Journey.json·_Documentation.json입니다. 소요 17분 41초. 편집 작업을 종료하여 최종 배포는 TASK-010 담당에게 인계했습니다.
### TASK-010 최종 배포 완료 — 2026-10-18 11:47:09 KST

전체 소요 18분 34초. TASK-011 완료(11:45:52) 및 추가 활성 편집 없음 확인 후 a260c3d를 origin/main에 반영했습니다. GitHub에서 해당 커밋의 VSIX를 다시 다운로드하여 77,112바이트와 SHA-256 2DD768237916AD7671B1829C279977D8BAC65684B59DFD3DC85DAD3C99523C53 일치를 확인했습니다. 중앙 설정·우클릭 메뉴·공통 UI·첫 사용 안내와 README/상세 사용법·회귀163/163·실제IDE50/50·독립28/28 근거를 함께 배포했습니다. 제품 소스와 고정 패키지는 검증 후 변경하지 않았습니다.

## TASK-013 — CodexOrig / 2026-10-18 13:40:24 KST

최신 Role 요구는 TASK-012 제품 구현과 분담해 v0.5.0으로 준비했습니다. Markdown 대화·공통 규칙·모델/모드·DB 스키마/형상관리/배포 권한 흐름을 사용자 안내에 상세히 정리했습니다. 추가 이미지에 맞춰 사용자 오른쪽 말풍선·AI 왼쪽 본문과 실제 시각/진행 표시를 반영한 최종 WPF 화면을 확인했습니다. 주석 형식도 기존 작성자와 같은 줄바꿈·콜론 열을 지켰습니다.

기존 회귀169개, 신규 기능193개와 실제 VS2017의 Codex/빈SQL 작성 포함58개가 모두 통과했습니다. Debug/Release 경고·오류0입니다. 테스트의 탭 인덱스·개인 기록 간섭을 제거했고 빈SQL·줄증감·설정 복구·모드 옵션 및 실제 소스 링크/대화 복원을 검증했습니다. 지정 VSIX에서 DLL을 추출해 IDE helper를 컴파일하므로 동일 패키지를 끝까지 확인합니다.

추가 요구의 최상위 VisualStudio_AIChat_ExtentionTest.exe도 준비했습니다. 더블클릭 GUI에서 선택하거나 --all로 기본8묶음을 실행할 수 있고8/8 통과했습니다. 실제 IDE/AI 계정 검사는 직접 선택합니다. 로그는 artifacts/tl 아래, 자세한 사용법·준비 조건은 README·docs/USER_GUIDE.md·DEVELOPMENT.md에 있습니다. 초기 긴 경로 문제를 수정한 후 통과했고 오류가 성공으로 표시되지 않는지도 확인했습니다.

설치 파일은 releases/v0.5.0에 있으며 상세 근거는 CodexOrig_TASK-013.md와 검증 JSON에 있습니다. 로컬 검증까지 41분 35초 소요됐습니다. 현재 VS2017용 패키지이며 실제Claude·외부운영서버·VS2019/2022 확장 지원은 남아 있습니다. TASK-012와 하위 작업자의 최종 종료 후 GitHub 배포/원격 파일 확인 결과를 이어 보고합니다.

## TASK-012 — CodexCode / 2026-10-18 13:41:24 KST

Role 추가 기능과 이어 전달된 대화 화면·최상위 실행기 요구를 v0.5.0에 구현했습니다. Claude 우선의 검정 화면에서 모델·모드·권한·첨부를 고르고, 사용자 말풍선/AI 본문·실제 시각과 작업시간으로 대화합니다. Markdown 대화·공통규칙·초안 복원, 여러 대화의 이름/닫기/재열기와 저장실패 보존을 지원합니다. 소스 변경은 한 파일의 비교·승인·별도 저장이며, SQL Server 스키마 기반 쿼리 요청과 검토 후 Git/SVN/TFVC/배포 실행을 추가했습니다.

Debug/Release 경고·오류0, 신규 저장52·설정37·업무48·UI56개 PASS입니다. 최종 같은 패키지의 기존 회귀169개와 실제VS2017/Codex58개 통과도 읽어 확인했습니다. 최상위 VisualStudio_AIChat_ExtentionTest.exe에서 기본 전체8묶음과 독립스모크9개가 통과했고 실제계정/IDE는 직접선택할때만 실행합니다. 로그는 artifacts/tl 아래 남으며 실패·중지와 자식프로세스 정리를 확인했습니다. 실행기 재컴파일은 scripts/Build-TestLauncher.ps1, 상세 근거·재현은 CodexCode_TASK-012.md와 관련JSON에 있습니다.

작업 소요 43분 3초. 모든하위작업자는편집종료했고제품/패키지와사용자Role·이전릴리스를보존했습니다. 현재배포는VS2017 15.9+용이며실제Claude·외부DB/SVN/TFVC/운영배포와VS2019/2022는후속검증범위입니다. README/실제IDE·최종GitHub배포는TASK013과분담해인계했습니다.

### TASK-013 최종 배포 완료 — 2026-10-18 13:43:00 KST

전체 소요 44분 11초. TASK-012 및 모든 하위 작업자 종료13:41:24 확인 후 7c5974218d2bc85d90a8597af43735971d77b86d 커밋을 origin/main에 반영했습니다. 원격 고정 커밋의 VSIX179232바이트·DC2DB317...와 루트 실행기32768바이트·3C22B654...를 다시 다운로드하여 해시 일치를 확인했습니다. 제품검사420개·실행기8묶음/스모크9개와 사용자 문서·최종 화면·Role 및 이전 릴리스 보존 근거를 함께 배포했습니다. 상세 원격 검증은 CodexOrig_TASK-013_Deployment.json입니다. 제품 소스와 검증된 패키지는 변경하지 않았으며 이 최종 기록을 후속 보고 커밋으로 반영합니다.


## TASK-014 — CodexOrig / 2026-10-18 14:07:42 KST

- 시작2026-10-18 13:46:29, 로컬종료 2026-10-18 14:07:42 KST, 소요 21분 13초. 대화 목록 세로 경계의 드래그/키보드 폭·왼쪽접기·마지막폭/표시복원, 대화별 실제 상태·읽지않음/읽음 저장·재시작 중단복구를 v0.6.0으로 구현했다.
- 소스 Views/AssistantControl.Conversations.cs 및 기존 Chat/Views·설정·manifest/csproj. tests/review UI79·실제IDE65(실제Codex/SQL포함)·기본8묶음·런처9스모크 전부PASS. Debug/Release 경고·오류0. 상세 CodexOrig_TASK-014.md/_Validation.json/_IDE.json/_Experience.json.
- README/상세사용법/최종화면·릴리스0.6/5528C0A15DC365A4933D3DB25FD593D947D71A154397784149979370796FD5CE 준비,0.5이전보존. 후속Role추가가도착해TASK015/016의0.7로이어진다. 다른작업진행중이므로GitHub배포는후속종료뒤진행한다.

상관 보고: 목록 크기를 바꾸거나 숨겨도 대화/초안은 보존합니다. 각 응답의 확인 여부와 실제 종료 결과를 구분하며 읽음 처리 실패도 표시합니다. 재현은 루트 실행기 --all과 tests/ide/Test-IdeIntegration.ps1을 사용합니다. 후속 사용자 요구를 처리하는 다른 작업자와 파일 범위를 분리했고 검증된0.6 패키지는 고정했습니다.

## TASK-015 — CodexCode / 2026-10-18 14:33:59 KST

추가 Role 요구를 v0.7.0에 구현했습니다. PPT·Word·Excel·PDF 본문을 읽는 문서 도우미, 실제 모델 지원 범위의 생각 강도/Fast, 대화별 고유 이름·역할과 동료 검토 요청/회신 기록을 추가했습니다. GitHub 로그인 확인 또는 명확한 GCM 저장 계정으로 기본 이름을 구성하며 저장 계정과 서버 인증을 구분합니다. 소스는 비교·승인 후 적용하고 별도로 저장하는 흐름을 유지합니다.

최종 같은 DLL/포장 도우미로 고유250개 사례와 추가 x86 문서47개 실행이 모두 통과했습니다. 초기 SCM 검사 한 건의 fixture 의존DLL 배치를 보정했으며 제품소스는 변경하지 않았습니다. 패키지 세항목/소스36개 해시 및 최신Role 보존을 확인했습니다. 재현은 tests/documents/Test-DocumentAttachments.ps1에 AssemblyPath·WorkerPath를 명시하고, tests/collaboration 및 tests/provider-options의 검사에 고정DLL을 지정합니다. 상세 근거는 CodexCode_TASK-015.md와 _Validation.json입니다.

담당 작업 소요 32분 10초. 모든하위작업자가 편집/테스트를 마쳤습니다. 기존기능 회귀·실제VS2017·루트 VisualStudio_AIChat_ExtentionTest.exe 기본13묶음과 최종배포는 TASK016 CodexOrig에 인계했습니다. 현재 VS2017용이며 PDF/구형Office 필터·OCR/실제암호문서·실제Claude/외부운영환경의 검증한계는 보고서에 명시했습니다.

## TASK-016 — CodexOrig / 2026-10-18 14:36:39 KST

- 작업 시간: 2026-10-18 14:03:42 ~ 2026-10-18 14:36:39 KST, 로컬 검증까지 32분 57초.
- 최신 Role 문서 첨부·모델 고급강도/Fast·권한 프리셋·주제별 설정·대화 AI 이름/역할/계정 접두사/협업을 v0.7.0으로 통합. 사용자 권한 답변은 확장의 DB·형상관리·배포 범위로 적용했고 수정안 승인·실행 계획 동의는 유지했다.
- 소스: src/VS2017.AIAssistant/Chat·Views·Providers·Services와 별도 DocumentReader 프로젝트. 실제 동료 순차 CLI→현재 AI 종합·대화별 상태/읽지 않음·실제 Markdown 보고·취소/파일잠금 복구, 비동기 첨부·슬라이더/속도·리비전 원복 포함. 신규 서비스 TASK015, 기존 모델/UI/오케스트레이션·검증/문서/배포 TASK016 소유로 충돌 없이 협업했다.
- 검증: Debug/Release 경고0·오류0, WPF101/101·실제VS2017/Codex/SQL/native협업73/73·신규서비스고유250/250+추가x86문서47/47, 루트기본13/13 묶음과9/9스모크. 최종 DLL BB80BC8E…/VSIX DB9C5D70… 275314바이트. 제품 소스36개 해시변경0·Role D25E6ED1…/이전릴리스보존, 로컬문서참조46개·최종화면7개 확인.
- 산출: releases/v0.7.0, 최상위 VisualStudio_AIChat_ExtentionTest.exe, README/상세가이드/CLI·UX·IDE·개발문서, CodexOrig_TASK-016.md 및 Validation/IDE/Experience/Launcher/SourceHashes JSON. 다른작업자전원종료14:34:25확인. 이제검증완료0.6/0.7과전체보고를GitHub에반영하고원격해시확인후배포완료기록을추가한다.
상관 보고: 문서를 첨부하고 AI별 이름·역할을 정해 실제 검토를 나눌 수 있습니다. 최종 코드 적용은 비교·승인을 거치며 권한 프리셋은 확장 업무에만 적용했습니다. 기본 재현은 루트 실행기 --all, 실제 IDE 검사는 docs/IDE_TESTS.md 명령입니다. 현재 설치 패키지는 VS2017용이며 VS2019/2022 패키지와 실제 Claude·운영 외부 서버 검증은 별도 범위입니다.

### TASK-014/016 최종 배포 완료 — 2026-10-18 14:38:46 KST

TASK015와전원종료14:34:25확인후검증된v0.6.0·v0.7.0을 da337035378fefec8a3b73f883c68aa20028372f로 origin/main에반영했다. 원격0.6 VSIX184770바이트/5528C0A1…,0.7 VSIX275314바이트/DB9C5D70…,루트exe33792바이트/D1015F87…를다시내려받아모두로컬SHA일치를확인했다. 최종UI101·실제IDE73·신규서비스250+추가x86문서47·루트전체13/13·스모크9/9 PASS. 제품소스36개/Role원문/이전릴리스보존,Project_Plan및배포JSON갱신완료. 이제제품수정없이최종배포보고만후속커밋으로반영하고작업을종료한다. 설치환경의VS2017/2019/2022세버전존재도확인했으며현재확장패키지는VS2017용이다.

## TASK-017 — CodexCode / 2026-10-18 17:18:40 KST

추가 Role의 이미지를 직접 분석해 v0.8.0 제품을 수정했습니다. 입력 버튼 위에서 모델·강도 게이지·Fast·권한을 선택하고, 대화 목록 하단에서 AI 대상을 지정합니다. 이름은 사용자명+AI+A~Z이며26명 이후에는 기존A 대화에 이어 지시합니다. 이전대화/협업주소/이름이력을 보존합니다. 안내 카드는 hover/초점 강조와 그림 상세/목록복귀, 설정은 고정목차·아이콘·간격·하단톤을 제공합니다. 부채꼴 메뉴는 원점을 덮지 않는 고정버튼과175ms닫기 유예로 반복hover를 방지합니다.

Debug/Release 경고·오류0, 최종제품DLL 명명40·기존협업60·안내23개가 모두PASS입니다. 별도 부채꼴소스14개와 최종dark/320px 화면도 확인했습니다. 재현은 tests/collaboration/Test-AlphabetAgentRouting.ps1·Test-ConversationCollaboration.ps1 및 tests/task017/Test-DialogGuide.ps1 -AssemblyPath에고정DLL을지정합니다. 이미지별 차이·수정·근거는 CodexCode_TASK-017.md와관련JSON에정리했습니다.

담당소요 21분 47초. 제품/포장도우미·소스39개·최신Role·이전배포본 보존확인후모든하위작업자와편집을종료했습니다. 전체회귀/최상위실행기14묶음·실제VS2017·사용자가이드·최종GitHub배포는TASK018 CodexOrig에인계했습니다. 현재패키지는VS2017 15.9+용이고실제Claude·외부운영환경검증범위는이전한계를유지합니다.

### TASK-017 최종 보정 보고 — 2026-10-18 17:30:19 KST

최종 총소요 33분 26초. 안내목록으로돌아올때키보드focus가사라지는통합회귀를보정했고,작업중Role에추가된tooltip요구까지반영했습니다. 공통tooltip의색·여백·긴문장줄바꿈을실제로열어검사했습니다. 최종같은DLL로이름40·협업60·안내23과제품UI133개·실행기14묶음이모두PASS입니다. 최종VSIX는306771바이트/AE33A8D0...이며기존1차완료기록의A876...패키지는최종배포본이아닙니다. 근거JSON과이미지분석보고서를최종3패키지값으로갱신했습니다. 제품·소스39개·최신Role/기존릴리스보존확인후편집종료,실제IDE·최종GitHub배포는TASK018에인계했습니다.

## TASK-018 보고 — Role 이미지와 최종 통합 검증

CodexOrig입니다. 최신Role의모든참조이미지와작업중추가된툴팁요구를검토해모델·권한·AI드롭다운,대화하단가로스크롤,A~Z담당자선택/기존A재사용,안내hover·그림상세·목차와하단색상,부채꼴이벤트를확인했습니다. 기존검사를갱신하고안내focus회귀를발견해제품소유자수정후재검증했습니다.

최종같은패키지로UI133·실제VS2017 80·기본14묶음·실행기9스모크가전부통과했습니다. 실제Codex의합성C#/SQL작성·native협업·승인전원본유지,실제IDE툴팁도확인했습니다. 재현은최상위VisualStudio_AIChat_ExtentionTest.exe의전체검사또는 tests/ide/Test-IdeIntegration.ps1 -VsixPath releases/v0.8.0/VS2017.AIAssistant-0.8.0.vsix -LiveCodexPath <네이티브exe> 입니다. 안내/명명은image-requirements에포함됩니다. 제품39소스/내장3파일과이전릴리스보존을확인했고사용자README·가이드/14실제화면·SHA파일을정리했습니다. 세부는CodexOrig_TASK-018.md와근거JSON에보고했습니다. 전원편집종료확인후GitHub배포·원격해시검증을마감하겠습니다.


### TASK-018 배포 완료 — 2026-10-18 17:37:44 KST

v0.8.0을 eee7cb3f00e6fab40411f366f628468e4087fc15로 origin/main에 배포했다. 원격 VSIX306771바이트/AE33A8D0…와 루트실행기33792바이트/8F14A59D…를 다시 내려받아 로컬 SHA와 모두 일치함을 확인했다. UI133·실제IDE80·루트14·스모크9 PASS, 최신Role/제품39소스/이전릴리스보존. TASK017전원종료17:31:38이후만push했고 현재제품/검사편집은종료했다. 사용자용 설치파일·가이드·검증보고 연결을 확인했습니다.

## TASK-019 — CodexOrig2 감사 보고 / 2026-10-18 17:45:39 KST

감사 완료 보고드립니다. 17:37:25~17:45:39 KST, 8분 13초 소요했습니다. Role.md 전체 반영 판정은 '일부 반영·보완 필요'입니다. VS2019/2022 지원 미구현과 IDE 라이선스 안내 미검증, 상단Chat 제목/참고계획 선택코드 범위 및 실제Claude·운영연동 공백을 확인했습니다. 최초 루트전체12/14에서 설정UX3개와 A닫기검사가 실패했고, 개별 재실행은 편집92/화면133 PASS여서 원인미확정 재현성 문제로 별도 기록했습니다. 실행기9/9, 기준소스39개와배포ZIP해시는정상입니다.

별도 감사 보고: MarkdownReport/CodexOrig2_TASK-019_Audit.md / 원본실패·재실행·해시근거: CodexOrig2_TASK-019_Evidence.json. 루트exe --all --vsix releases/v0.8.0/VS2017.AIAssistant-0.8.0.vsix로 재검사하고 --run editing 또는 --run experience로 분리 실행합니다. 제품수정이나배포는수행하지않았습니다.

## TASK-021 담당 완료 보고 — 2026-10-18 19:31:39 KST

CodexCode입니다. 추가image17~23과감사를반영해탭·입력란·실제부채꼴·색상변경카드/hover기록·파일왕복선택·상세설명·생각중그라데이션을수정했습니다. IDE Caption도Claude · Codex로바꾸고선택코드context를연결했습니다. 설정ABA와모호한승인경로를재현해보완했으며설정/디스크읽기상한·불변계획·고정문서출력배수와미저장버퍼보호를추가했습니다.

고정제품후보4 DEC74A49...E4B0로신규8묶음229/229 PASS입니다. tests/task021/Test-Task021.ps1 -AssemblyPath <고정DLL> -OutputDirectory <새결과폴더>를WindowsPowerShell5.1 -STA로실행하면재현됩니다. 결과는artifacts/task021/bundle-candidate4-final/summary.json,상세는CodexCode_TASK-021.md·_SecureCoding.md·_Validation.json입니다. 이전228PASS후시각검토로추가발견한파일선택결함과후보2Thinking실패도원문대로남겼습니다. 실제IDE/실계정/운영서버검사와격리fixture를구분했습니다.

소요 01시간 13분 06초. 소유편집/테스트와하위작업자는모두종료했습니다. SDK17마지막3경고는동작변경없는국소설명만추가했고최종빌드·IDE3버전/전체검사·영업용README/사용법·GitHub배포는TASK020CodexOrig가마감합니다. 원본공유bin/obj와최종Git소유를침범하지않았고021자체배포는없습니다.

## TASK-023 — CodexOrig2 2차 감사 보고 / 2026-10-18 21:01:50 KST

20:48:35~21:01:50 KST, 13분 15초 소요하여 v0.9.0 C6 고정패키지를감사했습니다. 1차지적중VS19/22지원·Chat제목·선택코드3건은조치를확인했습니다. 전체2회각14/16이었으며Claude metadata의로그인오판과빌드검사출력줄바꿈취약성을분리했습니다. PATH밖설치CLI자동발견누락도실행파일해석만으로재현했습니다. 편집92·화면135·추가229는각2회,실행기9개는통과했습니다.

보고서 MarkdownReport/CodexOrig2_TASK-023_Audit2.md, 근거 CodexOrig2_TASK-023_Evidence.json에조치완료/조건부/개발중범위와구체적인후속완료조건을기록했습니다. 최신Role추가8까지새후보검증이필요하며소스수정존재만으로C6패키지완료를인정하지않았습니다. 제품수정·배포는수행하지않았습니다.

## TASK-020 독립 검증 단계 완료 / 활성 화면 검사 대기 — 2026-10-18 22:17:13 KST

담당 CodexOrig. 시작2026-10-18 18:16:40, 현재까지 경과 4시간 0분 33초(최종 작업 완료 시각 아님). Role20:25추가1~11·image24~27·TASK023감사후보완, C11 제품90입력 동결·SDK3버전 Debug/Release6개 경고0 완료. 실제VS2017/2019/2022 각100/100, 루트기본16묶음 2회 연속16/16, WPF135·TASK021229·TASK022245·Native67·실제loopback SVN/격리배포8·실행기12 PASS. C9 실제Codex 수정/자체테스트는 후보와소스동일범위를명시해보존. 원래감사/최초12/16/애니메이션기본GPU실패와테스트호스트잔류cleanup근거보존.

경로 src/VS2017.AIAssistant/Providers·Views·Services·Chat 및3SKU csproj/manifest,scripts,tests,docs,README,releases/v0.9.0,최상위 VisualStudio_AIChat_ExtentionTest.exe,.gitattributes. 전체증거 CodexOrig_TASK-020_Evidence.json(schema2), 보고 CodexOrig_TASK-020.md. PDF/MD/bat/18PNG·고객ZIP 원문 보존, 패키지각32파일SHA대조, 원래7릴리스불변, 비공개설치로그와artifacts stage0.

**전체 작업은 아직 미완료**: 활성 입력desktop에서 최종 기본GPU/실제마우스/PDF재검증 및최종배포가남음. 현재Screen-saver·GetCursorPos error5이므로실제입력없고강제잠금해제/설정변경없음. 사용자활성화답변대기, commit/push미수행. 다음재개시이미통과한전체/IDE를무조건반복하지않고고정C11의남은세검사를먼저실행한뒤결과에따라최종배포한다.

## TASK-022 담당 완료 보고 — 2026-10-18 23:28:46 KST

CodexCode입니다. 최신Role추가1~11과이미지24~27을반영해버튼정렬·대화만스크롤·입력란고정·검정기록/우클릭·중복없는공개진행·hover상세·생각중위치·숨김필수규칙·원본PDF WebView와설치확인UI를마쳤습니다. CLI자동검색·안전소스reload·2차감사·README는TASK020과협업했습니다.

최종고정C11 SHA68B15E40...DA70로 기본245/245, 활성기본렌더56/56, 실제마우스55/55와실제PDF50/50을통과했습니다. 추가고유검사는hover10/PDF10이며반복기본45/40과TASK021의56을중복합산하지않습니다. PDF는자기창9/9가림검사후nativePNG를직접확인했습니다. 실제설치/로그인0,기존원본/실패보존. 6빌드경고0·3IDE각100·전체16두연속PASS와최초12/16·렌더경로분리도최종보고서에구분했습니다.

소요02시간52분08초(입력desktop대기포함). 소유14파일freeze5/제품·검사·문서·하위작업자편집종료/GUI해제입니다. 재현은 tests/task022/Test-Task022.ps1 -AssemblyPath <고정DLL> -OutputDirectory <새폴더>,선택적실제hover/PDF는각PS에VerifyHover/VerifyWebView로실행합니다. 상세는CodexCode_TASK-022.md·_SecureCoding.md·_Validation.json,최종활성근거는artifacts/task020/active-candidate11-20261018-232509/summary.json입니다. 최종Git배포와원격SHA는TASK020 CodexOrig가마감하며022자체commit/push는없습니다.

## TASK-020 최종 검증 완료 — 2026-10-18 23:31:54 KST

Role추가1~11·이미지24~27·2차감사를반영했습니다. 후보11의세버전빌드6개경고0,실제IDE각100,전체16묶음2회,Native67/추가UI245/실제hover55/PDF50/Default GPU56검사를모두통과했습니다. 원본실패·기존릴리스·감사는보존했고실제Claude/운영DB·TFVC는미검증입니다. 최상위실행기로재현가능하며보고/근거는CodexOrig_TASK-020.md·_Evidence.json입니다. 총경과5시간15분(활성화대기포함). TASK022전원종료23:30:56확인후GitHub배포와원격파일해시를마감합니다.
