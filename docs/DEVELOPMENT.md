# 개발 및 수동 검증

v0.11.1은 VS2017 15.7용/15.9 이상용·VS2019·VS2022의 네 패키지를 제공한다. 공통 WPF 화면·서비스를 링크한 프로젝트를 버전별 SDK로 빌드한다. VS2017 15.7.1에서 업데이트 없이 설치하도록 구 SDK 빌드를 별도로 추가했다.

## 브랜치와 GitHub 배포

`main`을 공개 소스와 배포 안내의 기준으로 사용한다. 작업 브랜치는 기능 개발·검증을 위한 임시 경로이며, 검증을 마친 변경은 `main`에 통합한 뒤 해당 소스 커밋에 릴리스 태그를 만든다. 설치 파일을 작업 브랜치에서만 게시하고 `main`을 이전 상태로 남기지 않는다. 태그가 가리키는 소스와 배포 패키지의 제품 소스가 같은지 확인하고, 게시 후 공개 URL에서 파일을 다시 내려받아 크기와 SHA-256을 대조한다.

2026-10-28 점검에서 원격 작업 브랜치 세 개가 모두 같은 직선 이력의 과거 지점이며, 최신 작업 브랜치가 `main`보다 19개 커밋 앞선 것을 확인했다. v0.14.2-preview.1 설치 자산 14개는 이미 공개되어 있었고, 기본 브랜치의 소스·안내 통합이 누락되어 있었다. 기존 브랜치·태그 이력을 보존하면서 최신 검증 이력을 `main`에 반영한다.

Preview는 정식 릴리스와 별도로 유지한다. 현재 설치 파일은 [v0.14.2-preview.1](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.14.2-preview.1)에서 제공한다. GitHub의 `Latest`와 `/releases/latest`는 정식 v0.11.1을 가리키며 해당 버전은 과거 정리로 설치 첨부가 없는 상태다. Preview 다운로드 안내에는 버전이 지정된 릴리스 URL을 사용한다.

## 호환성 기준

| 항목 | 현재 설정 |
|---|---|
| 설치 대상 | VS2017 구버전 `[15.0.27703.0,16.0)` / 일반 `[15.0.28307.0,16.0)` / VS2019 `[16.0,17.0)` / VS2022 `[17.0,18.0)` amd64 |
| 에디션 | Community 및 상위 에디션 Professional/Enterprise |
| 프로젝트 | 공통 C# 7.0 소스 링크. VS2017/19 net462, VS2022 net472 |
| VS SDK | 2017 구버전 Shell 15.7.27703·Threading 15.6.31 / 2017 일반·2019 Shell 15.9.28307 / 2022 Shell 17.0.32112.339 |
| VSIX 빌드 도구 | 2017/19 15.9.3032 / 2022 17.0.5241 |
| 진입점 | 도구(Tools) → AI Assistant → AI Assistant 문서 탭 |

`scripts/Sync-VsixTargets.ps1`이 공통 소스 링크와 대상별 매니페스트·SDK 설정을 동기화한다. VS2022는 별도 64비트 SDK 참조를 사용한다. 네 대상은 독립 bin/obj로 빌드하되 공통 DocumentReader 프로젝트 때문에 제품 빌드를 순차 수행한다.

일반 VS2017 패키지의 최소 버전은 15.9이며, 별도 `VS2017.Legacy.AIAssistant`는 15.7 SDK와 Threading으로 컴파일한다. 구버전용도 15.9 호스트에서 검사할 수 있도록 상위 호환 범위를 선언하되, 설치 도구는 15.7·15.8에만 구버전용을 기본 선택하고 15.9 이상에는 일반 패키지를 선택한다. 두 패키지는 같은 확장 ID이므로 한 IDE에 함께 설치하지 않는다.

VSIX 범위의 minor는 Microsoft 스키마 규칙에 따라 0으로 표기한다. IDE 제공 SDK DLL은 동봉하지 않는다. BuildTools는 고객 IDE에서 실행되는 SDK가 아닌 빌드 도구이며 구버전용에도 15.9.3032 패커를 사용한다. 15.7 패커에서 NuGet 런타임 DLL이 누락됐던 초기 C16 실패를 보존하고 C17에서 AvalonEdit/WebView2 DLL 포함을 재검증했다. 구 Threading의 중복 AsyncUsageAnalyzers 이벤트 진단은 기존 VSTHRD100 주석을 인식하는 현대 분석기로 검사하며, `AvoidAsyncVoid`만 구버전 프로젝트에서 제외한다.

고객 15.7.1 Professional 실환경은 이 개발 PC에 없다. 15.7 SDK 컴파일·SDK DLL을 사용한 독립 검사·15.9 호스트 통합 검사와 고객 PC의 설치/실행 성공은 구분해야 한다.

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

# VS2017 15.7 전용 (일반 프로젝트를 구 SDK 설정으로 링크 생성)
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Sync-VsixTargets.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -VisualStudioVersion 2017.Legacy
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
.\VisualStudio_AIChat_ExtentionTest.exe --run editing --vsix .\releases\v0.7.0\VS2017.AIAssistant-0.7.0.vsix
```

기본13개는 compatibility·build-entry·markdown·settings·workspace-actions·editing·foundation·experience·documents·collaboration·provider-options·provider-cli·scm-identity다. `ide`와 `live-codex`는 직접 선택할 때만 실행한다. `live-codex`에는 로그인된 네이티브 CLI를 `--codex C:\path\to\codex.exe`로 지정한다. 기본 전체 검사는 유료 AI 호출과 실제 IDE 설치·실행을 포함하지 않는다. 실제 IDE70개 fixture 검사는 `--run ide`, 실제 Codex를 함께 쓰는 IDE73개는 [IDE_TESTS.md](IDE_TESTS.md)의 PowerShell 명령을 사용한다.

제품 DLL·VSIX 경로는 저장소 안의 파일로 지정한다. 비워 두면 배포 v0.7.0 VSIX를 우선 선택하고 DLL을 실행별 폴더로 추출한다. 배포본이 없으면 기존 Release 산출물을 사용하며 런처가 제품을 자동 빌드하지는 않는다. WPF·SDK 검사에는 VS2017 개발 도구·4.6.2 targeting pack·복원된 SDK 참조가 필요하므로 새 체크아웃은 위 개발 PC 준비/빌드를 먼저 수행한다. 제품 VSIX를 설치해 쓰는 사용자에게 이 개발 도구가 필요한 것은 아니다.

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

## v0.7 문서·모델·협업 검증

문서 도우미는 net462 독립 exe이며 제품 빌드가 먼저 빌드해 DLL 옆과 VSIX 루트에 exe/config를 포함한다. tests/documents는 지정 DLL과 포장된 도우미로 실제 PDF/DOC/PPT 및 OpenXML·경계·취소를 검사한다. 협업60개, 모델65개, CLI15개, SCM 출처별 경계는 각 신규 검사 폴더에 있다. experience는 기존79개에 실제 모달·비동기 파일 붙여넣기·두 AI 호출·JSON/Markdown 롤백·원인 오류 보존 등을 추가했다. WPF 독립 검사는 실제 IDE와 같은 DispatcherSynchronizationContext로 비동기 UI 호출을 검사한다.

루트 --all은 지정 VSIX의 DLL과 문서 도우미를 함께 추출한다. PDF/구형 Office의 지원은 설치된 Windows IFilter에 따르며 AnyCPU/x86 제품 경로 검증을 구분한다. 별도 SCM 검사는 작성자 설정과 서버 로그인 확인·GCM 저장 계정을 혼동하지 않는지 검사한다.

## v0.8 이미지 요구와 실행기

모델·생각 강도·Fast·권한·AI 지정은 입력 버튼에 연결된 WPF Popup이다. 외부 클릭/Escape로 닫히며 동시에 하나만 열린다. 대화 액션은 HistoryPanel 하단의 가로 스크롤 안에 있고 AgentButton은 고정 문구를 사용한다. 이름 서비스는 제공자마다 영구 A~Z 주소를 유지하며 27번째 새 요청은 기존 A의 대화·초안을 재사용한다. 닫기·재열기와 이전 초과 기록의 보관도 검사한다.

안내는 hover 시 선택 카드만 강조하고 클릭 가능한 영역은 고정한다. 상세 절차 그림과 목록 복귀는 같은 본문 안에서 이루어진다. 설정·모델 창은 상단 목차, 주제 간 간격, 스크롤 밖 별도 색상의 동작 하단을 제공한다. 옵션 부채꼴은 공통 포인터 경계와 지연 닫기를 사용하며 반복 MouseEnter가 애니메이션을 재시작하지 않는다.

루트 실행기는 기본14/선택2개 묶음이다. 신규 image-requirements는 tests/review/Test-ImageRequirements.ps1에서 명명40개와 안내·목차23개를 실행한다. tests/review/DropdownExperienceChecks.cs는 실제 WPF 버튼·PopupRoot·선택 결과·A 라우팅·hover/Escape/해제를 검사한다. 고립 UI 검사는 실제 SCM 조회를 비활성화하여 명명 시점 변동을 제거하고, 실제 SCM 동작은 별도63개 검사에서 확인한다. 제품 빌드는 단일 소유자가 수행하며 고정 VSIX/DLL을 이용해 후속 검사를 실행한다.

## v0.9 재현 경로

```powershell
# 공통 Compile/Page 추가 또는 버전 변경 뒤 대상 동기화
powershell -NoProfile -File .\scripts\Sync-VsixTargets.ps1
# 버전별 MSBuild 자동 선택과 해당 프로젝트 빌드
.\scripts\Build-Vsix.cmd -VisualStudioVersion 2017 -Configuration Release
.\scripts\Build-Vsix.cmd -VisualStudioVersion 2019 -Configuration Release
.\scripts\Build-Vsix.cmd -VisualStudioVersion 2022 -Configuration Release
# 고정 제품으로 기본 검사16묶음 (실제 AI·운영 서버 자동 접속 없음)
.\VisualStudio_AIChat_ExtentionTest.exe --all --vsix .\releases\v0.9.0\VS2017.AIAssistant-0.9.0.vsix
```

기본 검사에는 실제 자식 프로세스가 합성 파일을 바꾸고 명령을 실행하는 NativeRuntime, 이미지·선택·보안 8단계 검사가 포함된다. 실제 계정 Codex 전체 액세스는 `tests/task020/Test-LiveNativeRuntime.ps1`, 실제 루프백 SVN·격리 배포는 `tests/task020/Test-LocalServerIntegration.ps1`의 명시적 선택 실행이다. 실제 DB·TFVC 및 Claude 인증 검증과 로컬 fixture를 혼동하지 않는다.
CLI 설명서 화면은 Microsoft.Web.WebView2 1.0.4191.47의 .NET Framework WPF/Core 및 native loader를 패키징한다. 공식 패키지의 IncludeInVsix 메타데이터를 사용하며 x86/x64 호스트에서 맞는 loader를 선택한다. 고객용 MD·배치·PDF·그림은 원본 경로에서 Content/Link로 동봉하므로 별도 사본을 수동 관리하지 않는다. 최종 후보에는 제품 DLL뿐 아니라 Resources와 runtimes를 함께 고정해야 한다. 루트 실행기는 OPC로 인코딩된 한글 파일명을 복호화하고 지정한 검사 폴더 경계·중복·수량·용량을 검사하여 추출한다. 실제 실행되는 설치 배치는 동봉 원본 SHA-256 검증과 사용자 설치 확인을 모두 통과해야 한다.
