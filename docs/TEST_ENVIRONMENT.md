# Visual Studio 실제 테스트 환경

작성: CodexOrig / 2026-10-18 KST / TASK-003

이 구성은 VS2017·VS2019·VS2022에서 VSIX를 실제 설치·실행해 검증하기 위한 환경이다. 설치 상태와 빌드 성공 여부는 검증 스크립트의 최신 JSON 결과로 확인한다.

## 설치 구성

| 버전 | 에디션 | 신규 설치 기본 경로 |
|---|---|---|
| VS2017 | Community | `C:\Program Files (x86)\Microsoft Visual Studio\2017\Community` |
| VS2019 | Community | `C:\Program Files (x86)\Microsoft Visual Studio\2019\Community` |
| VS2022 | Community | `C:\Program Files\Microsoft Visual Studio\2022\Community` |

기존 인스턴스가 발견되면 해당 경로를 대상으로 구성 요소를 추가한다. 같은 주 버전의 인스턴스가 여러 개면 자동 수정하지 않고 중단한다. [Microsoft의 버전 병렬 설치 안내](https://learn.microsoft.com/en-us/visualstudio/install/install-visual-studio-versions-side-by-side?view=vs-2022)를 기준으로 한다.

세 버전에 아래 구성 요소를 명시적으로 추가하며 UI 언어는 한국어로 설정한다.

- `Microsoft.VisualStudio.Workload.ManagedDesktop`: C#/WPF/.NET 데스크톱 개발
- `Microsoft.VisualStudio.Workload.VisualStudioExtension`: Visual Studio 확장 개발
- `Microsoft.VisualStudio.Component.VSSDK`: Visual Studio SDK
- `Microsoft.Net.Component.4.6.2.TargetingPack`: 기존 프로젝트 대상 참조 어셈블리
- `Microsoft.Net.Component.4.6.2.SDK`: .NET Framework 4.6.2 SDK 도구

근거: [공식 구성 요소 ID](https://learn.microsoft.com/en-us/visualstudio/install/workload-component-id-vs-community?view=vs-2022). 권장·선택 구성 요소 전체와 C++ 워크로드는 일괄 설치하지 않는다.

## 설치와 재실행

관리자 권한 없이 설치 계획을 확인할 수 있다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-VsixTestEnvironment.ps1 -PlanOnly
```

실제 설치는 Windows 관리자 권한이 필요하다. 승인된 관리자 PowerShell에서 실행한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-VsixTestEnvironment.ps1
```

스크립트는 공식 Microsoft bootstrapper를 사용한다. 실행 전 Authenticode 서명, 주 버전과 SHA-256을 검사하며, 2017 → 2019 → 2022 순으로 설치한다. 다운로드 URL이 갱신되어 고정 해시와 달라지면 파일을 다시 확인해야 한다.

`--passive --norestart --wait`로 설치하고 자동 재부팅을 하지 않는다. 설치 프로그램이 `3010`을 반환하면 재부팅 필요 상태를 기록하고 다음 버전 설치를 멈춘다. 작업을 저장하고 사용자가 재부팅한 뒤 재실행한다. [공식 명령행 인수 안내](https://learn.microsoft.com/en-us/visualstudio/install/use-command-line-parameters-to-install-visual-studio?view=vs-2022).

기본 설치 파일·로그 위치:

```text
%LOCALAPPDATA%\CodexOrig\VSIX-TestEnvironment\installers\
%LOCALAPPDATA%\CodexOrig\VSIX-TestEnvironment\logs\install-status.json
%LOCALAPPDATA%\CodexOrig\VSIX-TestEnvironment\logs\install-*.txt
%TEMP%\dd_bootstrapper_*.log
%TEMP%\dd_setup_*.log
```

설치 파일과 로그는 저장소의 소스 디렉터리에 넣지 않는다.

## 환경 검증

설치 완료 후 저장소 루트에서 실행한다.

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\scripts\Test-VsixTestEnvironment.ps1 -BuildSmokeTest
```

검사 대상은 다음과 같다.

1. vswhere에 각 IDE가 등록되고 `isComplete`/`isLaunchable` 상태인지 확인한다.
2. 필수 워크로드/SDK 구성 요소, `devenv.exe`, `VSIXInstaller.exe`, 버전별 MSBuild와 .NET 4.6.2 참조 어셈블리를 확인한다.
3. 버전별로 분리된 로그 디렉터리에서 .NET Framework 4.6.2/C# 7.0 WPF 프로젝트를 실제 컴파일한다. XAML에서 생성한 `InitializeComponent` 호출도 컴파일한다.

기본 결과: `%LOCALAPPDATA%\CodexOrig\VSIX-TestEnvironment\logs\environment-report.json`. 스크립트는 통과 시 종료 코드 0, 미설치·불완전 구성·빌드 실패 시 1을 반환한다. `-PassThru`를 쓰면 종료 코드를 변경하는 대신 결과 객체를 반환한다.

이 검사는 IDE 화면이나 VSIX 런타임 동작까지 자동 통과로 간주하지 않는다. 해당 결과는 실제 실행 후 별도로 보고한다.

### IDE 자체의 명령행 빌드

`-BuildSmokeTest`는 각 버전의 `logs\smoke\<버전>\WpfProbe.sln`도 생성한다. MSBuild 검사 후 IDE 실행 호스트로 별도 검증할 수 있다.

```powershell
$probeRoot = Join-Path $env:LOCALAPPDATA 'CodexOrig\VSIX-TestEnvironment\logs\smoke'
& 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE\devenv.com' `
    (Join-Path $probeRoot '2017\WpfProbe.sln') /Build 'Release|Any CPU'
& 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\devenv.com' `
    (Join-Path $probeRoot '2019\WpfProbe.sln') /Build 'Release|Any CPU'
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.com' `
    (Join-Path $probeRoot '2022\WpfProbe.sln') /Build 'Release|Any CPU'
```

TASK-003에서는 세 명령 모두 성공 1건·실패 0건과 종료 코드 0을 확인했다. `/Build`는 IDE의 솔루션 빌드 명령과 대응한다. 근거: [Microsoft의 /Build 안내](https://learn.microsoft.com/en-us/visualstudio/ide/reference/build-devenv-exe?view=vs-2022). 이 검증으로 메뉴 클릭·도킹·확장 도구 창의 실제 화면까지 확인했다고 간주하지 않는다.

최종 설치·빌드 결과는 `MarkdownReport/CodexOrig_TASK-003_Environment.json`, 설치 실행 결과는 `MarkdownReport/CodexOrig_TASK-003_InstallResult.json`에 보관한다.

## 확장 빌드와 Experimental Instance

VS2017 확장은 설치된 VS2017 MSBuild 경로를 명시하여 빌드한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Configuration Release `
    -MSBuildPath 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe'
```

실험용 IDE는 설치 경로의 `Common7\IDE\devenv.exe`에 `/RootSuffix Exp`를 붙여 실행한다. [공식 Experimental Instance 안내](https://learn.microsoft.com/en-us/visualstudio/extensibility/the-experimental-instance?view=vs-2022). 정상 실행 확인 후 확장을 실험용 인스턴스에 등록하고 도구 메뉴에서 AI Assistant 창을 연다.

현재 저장소 VSIX의 매니페스트는 VS2017을 대상으로 한다. VS2019와 VS2022를 설치했다고 기존 VSIX의 지원 범위가 바뀌지는 않는다. 각 IDE용 패키지가 준비되면 해당 버전의 VSIXInstaller와 실험용 인스턴스에서 검증한다. VS2022는 별도 SDK 참조와 아키텍처 구성이 필요하며 관련 기준은 `docs/VSIX_COMPATIBILITY.md`를 따른다.

계정 로그인·활성화 또는 Windows 권한 입력이 필요하면 사용자가 직접 처리한다. 사용자 계정 정보를 자동 입력하거나 기존 개발 설정을 초기화하지 않는다.

2026-10-18 검증 시 세 버전의 설치·SDK·MSBuild 빌드와 IDE 명령행 빌드는 통과했다. 화면 확인 도구는 `GetCursorPos failed: 액세스가 거부되었습니다. (0x80070005)`를 반환하여 대화형 화면 및 확장 도구 창 검증은 미검증으로 남겼다. VS2017 명령행에는 라이선스가 30일 후 만료된다는 안내가 표시되었으므로 사용자가 로그인·라이선스 상태를 확인해야 한다.
