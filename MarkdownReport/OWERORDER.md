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
