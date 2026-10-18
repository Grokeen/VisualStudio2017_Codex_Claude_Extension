# 개발 및 수동 검증

v0.5.0은 AI별 Markdown 대화 복원, 프로젝트 공통 instruction, 입력 영역의 모델·모드 선택, 첨부 미리보기, 변경 파일 링크와 권한별 프로젝트 작업을 추가했다. 공통 AssistantTheme.xaml은 IDE의 밝은/어두운 테마에 관계없이 검정 계열 팔레트를 유지한다. 설정·첫 사용 안내는 [UX 검증](UX_TESTS.md), 요청·승인·저장은 [소스 편집](SOURCE_EDITING.md), CLI 모드 전달은 [CLI 옵션](CLI_OPTIONS.md)을 참고한다.

## 호환성 기준

| 항목 | 현재 설정 |
|---|---|
| 설치 대상 | Visual Studio 2017 15.9 이상, `[15.0.28307.0,16.0)` |
| 에디션 | Community 및 상위 에디션 Professional/Enterprise |
| 프로젝트 | 기존 형식 C# VSIX, .NET Framework 4.6.2, C# 7.0 |
| VS SDK | Microsoft.VisualStudio.Shell.15.0 15.9.28307 |
| VSIX 빌드 도구 | Microsoft.VSSDK.BuildTools 15.9.3032 |
| 진입점 | 도구(Tools) → AI Assistant → Chat 문서 탭 |

VS2019 및 VS2022는 향후 목표이다. 현재 매니페스트는 두 버전의 설치를 허용하지 않는다. VS2022는 64비트 IDE이므로 적합한 SDK 및 아키텍처 대상으로 별도 빌드·실행 검증해야 한다.

SDK 패키지와 IDE 의존성의 기준을 맞추기 위해 VS2017 15.9를 최소 버전으로 삼는다. VSIX 버전 범위의 minor는 Microsoft 스키마 규칙에 따라 `15.0.28307.0`으로 표기한다. Threading 및 Json의 참조 버전은 SDK 의존성 충돌을 피하도록 고정하며, IDE에서 제공하는 SDK와 해당 종속 DLL은 패키지에 포함하지 않는다.

## 개발 PC 준비

1. Visual Studio 2017 15.9의 Visual Studio extension development 워크로드를 설치한다.
2. .NET Framework 4.6.2 targeting pack 및 개발 도구를 선택한다. 명령줄 빌드에서는 NuGet ReferenceAssemblies 패키지도 참조 어셈블리를 공급한다.
3. `VS2017.AIAssistant.sln`을 열고 NuGet 패키지를 복원한다.
4. Debug 구성을 빌드하고 F5를 누른다. 시작 프로그램은 `$(DevEnvDir)devenv.exe`, 인수는 `/RootSuffix Exp`이다.

이 확장은 별도 솔루션으로 개발하며 사용자가 연 기존 개발 프로젝트는 변경하지 않는다.

## 명령줄 빌드

Windows PowerShell에서 저장소 루트 기준으로 실행한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

MSBuild를 자동으로 찾지 못하면 전체 Framework MSBuild.exe 경로를 `-MSBuildPath`로 지정한다. NuGet.exe로 미리 복원해야 하는 독립 도구 환경은 `-NuGetPath`도 지원한다. `dotnet build`는 이 legacy WPF/VSSDK 프로젝트의 빌드 경로가 아니다.

빌드 결과: `src/VS2017.AIAssistant/bin/Release/VS2017.AIAssistant.vsix`. 스크립트는 CLI 빌드 시 IDE에 확장을 자동 설치하지 않는다.

## 자동 검증

빌드 후 Windows PowerShell에서 실행한다.

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -STA -File .\tests\foundation\Test-FoundationPackage.ps1
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\editing\Test-SourceEditing.ps1
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\compatibility\Test-VsixCompatibility.Tests.ps1
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\scripts\Test-VsixCompatibility.ps1 `
    -ManifestPath .\src\VS2017.AIAssistant\source.extension.vsixmanifest `
    -ProjectPath .\src\VS2017.AIAssistant\VS2017.AIAssistant.csproj
```

Foundation 검사는 VSIX 안의 DLL·pkgdef·메뉴·BAML을 확인하고 제품 WPF 컨트롤을 별도 프로세스에서 생성한다. 밝은/어두운 IDE 호스트 리소스와 320/200픽셀 폭에서도 검정 팔레트와 대비를 확인한다. IDE SDK DLL을 VSIX에 중복 포함하지 않는지도 검사한다. Debug 산출물 검증은 `-Configuration Debug`를 지정한다. 고정 사본 검증은 Foundation에 `-AssemblyPath`와 `-VsixPath`, 편집 검사에 `-AssemblyPath`를 지정하여 공유 bin의 재빌드와 격리한다. 개인 설정·대화 경로도 실행별로 격리한다.

편집 회귀 테스트는 VSIX DLL을 참조하는 C# 실행 파일을 컴파일하고 실제 subprocess/파이프로 가짜 CLI를 호출한다. 승인·충돌·원복, Codex/Claude 인수 및 JSON, 한글/공백 경로, 출력 제한, 취소·타임아웃·하위 프로세스 종료와 실제 WPF 버튼 흐름을 검사한다. 산출물은 `artifacts/source-editing/run-*/`이다. 실제 계정 AI 호출이나 EnvDTE 실행을 대체하는 테스트로 보고하지 않는다. `-CompilerPath`로 VS2017의 `MSBuild/15.0/Bin/Roslyn/csc.exe`를 지정할 수 있다.

호환성 도구는 CodexOrig의 독립 검사이며 상세 범위는 [VSIX_COMPATIBILITY.md](VSIX_COMPATIBILITY.md)를 참고한다. 추가 저장소·설정 트랜잭션 검사는 `tests/workspace/`, 프로젝트 작업 검사는 `tests/workspace-actions/`, 화면 흐름 검사는 `tests/review/Test-WorkspaceExperience.ps1`에 있다. 결과와 재현 명령은 [TASK-012](../MarkdownReport/CodexCode_TASK-012.md)와 [TASK-013](../MarkdownReport/CodexOrig_TASK-013.md)에 보존한다. 자동 검증은 아래 실제 IDE 검증과 함께 수행한다.

## 최상위 테스트 실행기

저장소 전체와 검사에 필요한 개발 도구를 준비한 뒤 루트의 `VisualStudio_AIChat_ExtentionTest.exe`를 더블클릭한다. 검정 GUI에서 항목을 체크하고 **선택 검사 실행**을 누르거나 **기본 전체 검사**를 선택한다. 로그를 같은 화면과 `artifacts/tl/<실행 ID>/`에서 확인하고 **중지**로 후속 검사를 중단할 수 있다.

```powershell
.\VisualStudio_AIChat_ExtentionTest.exe --list
.\VisualStudio_AIChat_ExtentionTest.exe --all
.\VisualStudio_AIChat_ExtentionTest.exe --run editing --vsix .\releases\v0.5.0\VS2017.AIAssistant-0.5.0.vsix
```

기본8개는 compatibility·build-entry·markdown·settings·workspace-actions·editing·foundation·experience다. `ide`와 `live-codex`는 직접 선택할 때만 실행한다. `live-codex`에는 로그인된 네이티브 CLI를 `--codex C:\path\to\codex.exe`로 지정한다. 기본 전체 검사는 유료 AI 호출과 실제 IDE 설치·실행을 포함하지 않는다. 실제 IDE55개 fixture 검사는 `--run ide`, 실제 Codex를 함께 쓰는 IDE58개는 [IDE_TESTS.md](IDE_TESTS.md)의 PowerShell 명령을 사용한다.

제품 DLL·VSIX 경로는 저장소 안의 파일로 지정한다. 비워 두면 배포 v0.5.0 VSIX를 우선 선택하고 DLL을 실행별 폴더로 추출한다. 배포본이 없으면 기존 Release 산출물을 사용하며 런처가 제품을 자동 빌드하지는 않는다. WPF·SDK 검사에는 VS2017 개발 도구·4.6.2 targeting pack·복원된 SDK 참조가 필요하므로 새 체크아웃은 위 개발 PC 준비/빌드를 먼저 수행한다. 제품 VSIX를 설치해 쓰는 사용자에게 이 개발 도구가 필요한 것은 아니다.

CLI 종료 코드는 성공0, 검사 실패1, 잘못된 인수·검사 ID2, 취소130이다. 실행기를 다시 컴파일하려면 `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-TestLauncher.ps1`을 실행한다. Windows의 .NET Framework 기본 C# 컴파일러로 루트 exe만 생성하며 제품 패키지를 바꾸지 않는다.

## VS2017 설치 및 실행 검증

| 순서 | 실행 | 기대 결과 |
|---|---|---|
| 1 | Debug 빌드 후 F5 | 별도 VS2017 Experimental Instance 실행 |
| 2 | 도구 → AI Assistant | Provider·수정 요청·비교·승인·저장 화면 표시 |
| 3 | 메뉴 명령을 반복 실행 | 기존 창 활성화, 중복 창 없음 |
| 4 | 창 닫기 후 다시 열기 | 오류 없이 다시 표시 |
| 5 | 창 도킹, 폭 축소, 밝은/어두운 테마 전환 | 글자와 배경이 구별되고 문구 줄바꿈 및 스크롤 정상 |
| 6 | 솔루션 없이 실행, C# 4.6.2 솔루션 열고 다시 실행 | 두 경우 모두 창이 열리고 요청·승인 전 소스 변경 없음 |
| 7 | 창을 연 상태로 VS 종료·재시작 | 오류 없이 종료 및 창 복원 |
| 8 | VS를 닫고 Release VSIX 실행 | VS2017 설치 대상으로 표시되고 설치 성공 |
| 9 | 일반 VS2017에서 메뉴 확인 후 도구 → 확장 및 업데이트에서 제거 | 메뉴/창 정상, 재시작 후 제거 완료 |

환경이 없는 항목은 미실행으로 보고한다. 빌드 성공은 설치·IDE 실행 성공을 대신하지 않는다. 실패 시 `devenv.exe /RootSuffix Exp /Log`로 실행하고 `%APPDATA%/Microsoft/VisualStudio/15.0_*Exp/ActivityLog.xml`을 확인한다.

실제 소스 편집의 요청·승인·저장·Undo·충돌 시나리오는 [SOURCE_EDITING.md](SOURCE_EDITING.md)의 실제 IDE 체크리스트를 수행한다.

TASK-005에서는 별도 VS2017 인스턴스에 고정 v0.2.0을 설치하고 실제 메뉴·도구 창·EnvDTE와 Codex 계정을 연결한 25개 검사를 통과했다. `tests/ide/Test-IdeIntegration.ps1`로 재현할 수 있으며 설치·초기 구성 옵션과 전체 사례는 [IDE_TESTS.md](IDE_TESTS.md)를 참고한다. 테스트 보조 VSIX는 제품에 포함하지 않는다.

## 작업 순서

`MarkdownReport/WORK_STATUS.md`를 확인한 후 담당 범위를 등록한다. 한 기능씩 구현하고 결과는 `MarkdownReport/Update_History.md` 및 `MarkdownReport/OWERORDER.md`에 기록한다. 다른 작업자가 진행 중이면 GitHub에 push하거나 릴리스하지 않는다. 추가 Role.md의 소스 수정 요구를 우선 반영하되 실제 IDE·계정 검증이 완료되지 않은 항목은 별도로 남긴다.

## 공식 근거

- [Microsoft: 도구 창 확장 만들기](https://learn.microsoft.com/en-us/visualstudio/extensibility/creating-an-extension-with-a-tool-window)
- [Microsoft: AsyncPackage 사용](https://learn.microsoft.com/en-us/visualstudio/extensibility/how-to-use-asyncpackage-to-load-vspackages-in-the-background)
- [Microsoft: VSIX 매니페스트 스키마와 SKU/버전 범위](https://learn.microsoft.com/en-us/visualstudio/extensibility/vsix-extension-schema-2-0-reference)
- [Microsoft: VS2022 확장 업그레이드](https://learn.microsoft.com/en-us/visualstudio/extensibility/migration/update-visual-studio-extension)
- [Microsoft Shell 15.9.28307 패키지](https://www.nuget.org/packages/Microsoft.VisualStudio.Shell.15.0/15.9.28307)
- [Microsoft VSSDK.BuildTools 15.9.3032 패키지](https://www.nuget.org/packages/Microsoft.VSSDK.BuildTools/15.9.3032)
