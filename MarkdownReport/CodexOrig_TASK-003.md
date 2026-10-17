# TASK-003 완료 보고 — Visual Studio 테스트 환경 구성

작성자: CodexOrig

## 작업 시간과 범위

- 날짜/시간대: 2026-10-18, Asia/Seoul (KST)
- 작업 시작: 00:50:54 / 설치·검증 완료: 01:33:36
- 소요 시간: 42분 42초
- 설치 실행 구간: 00:55:08 ~ 01:22:43, 약 27분 35초
- 사용자 요청: VS2017·VS2019·VS2022를 각각 설치하여 실제 확장 테스트 환경 구성.
- 결과: 세 Community IDE와 필수 SDK 설치 완료, 버전별 MSBuild 및 IDE 자체 명령행 빌드 통과. 대화형 GUI·VSIX 런타임은 미검증.

## 설치 결과

| 버전 | 설치된 버전 | 설치 경로 | 설치 결과 | 재부팅 |
|---|---|---|---|---|
| Visual Studio Community 2017 | 15.9.83 (`15.9.37627.17`) | `C:\Program Files (x86)\Microsoft Visual Studio\2017\Community` | 종료 코드 0, 01:01:23 완료 | 불필요 |
| Visual Studio Community 2019 | 16.11.60 (`16.11.37627.13`) | `C:\Program Files (x86)\Microsoft Visual Studio\2019\Community` | 종료 코드 0, 01:09:07 완료 | 불필요 |
| Visual Studio Community 2022 | 17.14.42 (`17.14.37806.7`) | `C:\Program Files\Microsoft Visual Studio\2022\Community` | 종료 코드 0, 01:22:43 완료 | 불필요 |

vswhere에서 세 버전 모두 `isComplete=true`, `isLaunchable=true`, `isRebootRequired=false`를 확인했다. 설치 전에는 VS Code만 확인되었고 Visual Studio IDE 설치 등록은 없었다. 초기 C: 여유 공간은 약 1,180GB였다.

UI 언어는 한국어로 지정하고, 세 버전 모두 다음 구성 요소를 명시적으로 설치·확인했다.

- .NET 데스크톱 개발: `Microsoft.VisualStudio.Workload.ManagedDesktop`
- 확장 개발: `Microsoft.VisualStudio.Workload.VisualStudioExtension`
- Visual Studio SDK: `Microsoft.VisualStudio.Component.VSSDK`
- .NET Framework 4.6.2 Targeting Pack: `Microsoft.Net.Component.4.6.2.TargetingPack`
- .NET Framework 4.6.2 SDK: `Microsoft.Net.Component.4.6.2.SDK`

## 설치 파일 검증과 실행

세 bootstrapper 모두 공식 Microsoft 경로에서 다운로드한 뒤 Authenticode `Valid`, Microsoft Corporation 서명 및 대상 주 버전을 확인했다. 고정한 SHA-256은 다음과 같다.

| 버전 | SHA-256 |
|---|---|
| 2017 | `1BF11FC98CF22D93498180D50794219A0CBB860A98880E29836BB66ACADC0123` |
| 2019 | `D70A79F50E37C337227A928875FCEF8D233B71056D4C967F070D92F1584D2C7E` |
| 2022 | `D52E18859B0996BAC3A89BA7D07E180508E49EB55F824D7EDA2822ADED495020` |

다운로드 URL·구성 요소·경로는 `CodexOrig_TASK-003_InstallPlan.json`에 기록했다. 관리자 실행기로 순차 설치했고 `--norestart`를 사용하여 자동 재부팅을 하지 않았다.

첫 실행에서 VS2017 설치 후 PS5 `ConvertFrom-Json`의 빈 배열 처리 오류로 전환이 중단되었다. 배열을 명시적으로 정규화하도록 수정하고 실제 설치 상태를 다시 확인했다. 재실행은 이미 구성된 VS2017을 건너뛰고 VS2019·VS2022만 설치했다. 첫 오류 상태와 두 실행의 transcript는 로컬 로그에 보존했다.

## 실제 검증 결과

| 검사 | VS2017 | VS2019 | VS2022 |
|---|---|---|---|
| IDE 완전 설치·실행 가능 등록 | PASS | PASS | PASS |
| 필수 워크로드/SDK 구성 확인 | PASS | PASS | PASS |
| devenv / VSIXInstaller / MSBuild 실행 파일 확인 | PASS | PASS | PASS |
| .NET Framework 4.6.2 참조 어셈블리 | PASS | PASS | PASS |
| 설치된 MSBuild로 WPF/.NET 4.6.2/C# 7.0 컴파일 | PASS | PASS | PASS |
| IDE 자체 `devenv.com` 솔루션 빌드 | PASS / 종료 0 | PASS / 종료 0 | PASS / 종료 0 |
| 대화형 IDE 화면·Experimental Instance 도구 창 | 미검증 | 미검증 | 미검증 |

WPF probe는 XAML에서 생성되는 `InitializeComponent` 호출까지 컴파일한다. IDE 명령행은 01:30:48 ~ 01:31:04에 각자 성공 1건·실패 0건으로 완료했다. 실제 결과 JSON과 로그를 보존했다.

초기 IDE 명령행 probe는 `.csproj`와 사용자 지정 RootSuffix 조합에서 인수 오류를 반환했다. IDE가 읽을 수 있는 프로젝트 GUID·구성 및 정식 `.sln`을 생성하고, 문서화된 기본 호스트의 `/Build 'Release|Any CPU'`로 다시 검증하여 세 버전 모두 통과했다. 이 입력 보완은 환경 전용 probe에 적용했으며 확장 소스는 수정하지 않았다.

별도로 01:03 당시 Phase 1 소스는 설치된 VS2017 MSBuild로 Release VSIX 빌드 및 Foundation 22/22를 통과했다. 이후 TASK-004에서 소스가 변경되었으므로 이 결과를 최신 v0.2.0의 전체 회귀 결과로 간주하지 않는다. 최신 기능 회귀·표준 빌드는 CodexCode의 TASK-004 보고 범위이다.

## GUI 검증 한계와 사용자 작업

Windows 화면 확인에서 검은 캡처·창 활성화 실패가 발생했고, 다음 오류를 확인했다.

```text
failed to activate captured window
GetCursorPos failed: 액세스가 거부되었습니다. (0x80070005)
```

창을 다시 조회·선택해 확인했으나 정상적인 GUI 조작 상태를 확보하지 못했다. 화면 잠금/승인 창 확인을 사용자에게 요청했으며, GUI 결과를 통과로 기록하지 않았다. 도움말 조회용으로 시작한 자신의 VSIXInstaller 프로세스는 명령행이 `/?`인 것을 확인하고 정리했다.

VS2017 명령행은 “Visual Studio 라이선스가 30일 후에 만료됩니다.” 안내를 출력했다. 사용자가 실제 IDE에서 로그인·라이선스 상태를 확인해야 한다. 계정 정보를 입력하거나 인증 화면을 자동 조작하지 않았다.

설치된 개발 환경의 실제 컴파일·IDE 명령행 빌드와, 확장의 메뉴·도구 창·EnvDTE 편집 동작 검증은 구분한다. 후자는 화면 접근이 가능한 상태에서 진행해야 한다.

## 변경 파일과 산출물

| 파일 | 내용 |
|---|---|
| `scripts/Install-VsixTestEnvironment.ps1` | 서명·해시·버전 검증, 설치 계획, 관리자 순차 설치, 완료 IDE 건너뛰기, 재부팅/오류 상태 기록 |
| `scripts/Test-VsixTestEnvironment.ps1` | vswhere 구성 검사, WPF 실제 컴파일, IDE용 프로젝트·솔루션 생성 |
| `docs/TEST_ENVIRONMENT.md` | 재현 가능한 설치·검증·IDE 명령행·Experimental Instance 사용법 |
| `MarkdownReport/CodexOrig_TASK-003_InstallPlan.json` | 실행에 사용한 설치 계획 |
| `MarkdownReport/CodexOrig_TASK-003_InstallResult.json` | 세 IDE 설치 종료 코드와 시간 |
| `MarkdownReport/CodexOrig_TASK-003_Environment.json` | 세 IDE의 SDK/MSBuild 확인 및 IDE 자체 빌드 종료 코드 |
| `MarkdownReport/CodexOrig_TASK-003.md` | 이 보고서 |
| `MarkdownReport/WORK_STATUS.md`, `Update_History.md`, `OWERORDER.md` | 자기 작업 상태·완료 이력·상관 보고 |

로컬 설치 파일·원본 로그·probe 위치:

```text
C:\Users\ygree\AppData\Local\CodexOrig\VSIX-TestEnvironment\installers\
C:\Users\ygree\AppData\Local\CodexOrig\VSIX-TestEnvironment\logs\
C:\Users\ygree\AppData\Local\CodexOrig\VSIX-TestEnvironment\logs\smoke\2017\WpfProbe.sln
C:\Users\ygree\AppData\Local\CodexOrig\VSIX-TestEnvironment\logs\smoke\2019\WpfProbe.sln
C:\Users\ygree\AppData\Local\CodexOrig\VSIX-TestEnvironment\logs\smoke\2022\WpfProbe.sln
```

## 협업 인계

TASK-004의 v0.2.0 고정 인계 패키지와 같은 파일을 `artifacts/test-environment/vsix/VS2017.AIAssistant.0.2.0.vsix`에 보관했다. SHA-256은 `0B7FD44509A6CA02344E645924378EB544966A0831B942A9F7D4B6CDAB872D94`이다. 대화형 Exp 설치·실행 성공으로 보고하지 않는다.

확장 소스와 담당자의 테스트는 변경하지 않았다. 공유 bin/obj 충돌을 피하도록 이후 기능 빌드는 담당자에게 맡겼고 환경 전용 probe로 검증했다. GUI 통합 검증이 남아 있어 이 작업에서 GitHub push·릴리스 배포는 수행하지 않았다.
