# TASK-001 개발 및 검증 보고 — CodexCode

- 작업명: VS2017 VSIX Foundation 구현 및 패키지·WPF 자동 검증
- 작업자: CodexCode
- 작업 날짜: 2026-10-17 (Asia/Seoul, KST)
- 시작: 2026-10-17 22:10:21
- 종료: 2026-10-17 22:25:46 KST
- 소요: 15분 25초
- 상태: 소스 구현과 로컬 빌드·자동 검증 완료. 실제 VS2017 설치·IDE 실행은 환경 부재로 대기 중이며 Phase 1 전체 완료는 아니다.

## 구현 내용

독립 솔루션 `VS2017.AIAssistant.sln`과 기존 형식의 C# VSIX 프로젝트를 만들었다. .NET Framework 4.6.2 / C# 7.0 / AnyCPU를 명시하고 VS2017용 SDK와 BuildTools를 사용한다.

`AsyncPackage`가 메뉴 서비스를 비동기로 구한 후 UI 스레드에서 도구 메뉴의 `AI Assistant` 명령을 등록한다. 명령은 ID 0의 도구 창을 재사용하도록 구현했다. 창 생성·표시 실패는 ActivityLog에 남기고 안내한다. 반복 메뉴 호출, 창 닫기·재열기 동작의 실제 IDE 검증은 아직 수행하지 않았다.

WPF 안내 화면은 IDE의 동적 배경·글자 리소스를 사용하고, 좁은 창의 줄바꿈과 세로 스크롤을 지원한다. AI 실행, 인증, 채팅, 소스 수정 기능은 이 단계에 포함하지 않았다.

매니페스트 설치 대상은 VS2017 15.9 이상인 `[15.0.28307.0,16.0)`이다. Community 대상은 상위 에디션에도 적용된다. VS2019/2022 설치를 허용하지 않았으며 향후 별도 호환성 작업이 필요하다. 최소 IDE와 버전 표기 이유는 [개발 가이드](../docs/DEVELOPMENT.md)에 공식 근거와 함께 정리했다.

## 변경 파일

| 경로 | 내용 |
|---|---|
| `VS2017.AIAssistant.sln` | VS2017용 독립 솔루션 |
| `src/VS2017.AIAssistant/VS2017.AIAssistant.csproj` | legacy VSIX/WPF 빌드, 프레임워크·언어·SDK 참조 |
| `src/VS2017.AIAssistant/AiAssistantPackage.cs` | 패키지·명령·도구 창 등록 |
| `src/VS2017.AIAssistant/Commands/OpenAssistantCommand.cs` | 도구 창 열기와 오류 처리 |
| `src/VS2017.AIAssistant/Commands/AssistantCommands.vsct` | 도구 메뉴 명령 테이블 |
| `src/VS2017.AIAssistant/ToolWindows/AssistantToolWindow.cs` | WPF 도구 창 호스트 |
| `src/VS2017.AIAssistant/Views/AssistantControl.xaml` 및 `.xaml.cs` | 안내 화면과 동적 테마 리소스 |
| `src/VS2017.AIAssistant/Properties/AssemblyInfo.cs` | 어셈블리 정보 |
| `src/VS2017.AIAssistant/VSPackage.resx` | 컴파일된 메뉴를 포함하는 중립 리소스 |
| `src/VS2017.AIAssistant/source.extension.vsixmanifest` | 지원 IDE·프레임워크·패키지 asset |
| `scripts/build.ps1` | MSBuild 탐색, 복원, Debug/Release 빌드 및 종료 코드 검사 |
| `tests/foundation/Test-FoundationPackage.ps1` | 실제 VSIX 내용 및 WPF 컨트롤 생성·테마·렌더링 검사 |
| `.gitignore`, `README.md`, `docs/DEVELOPMENT.md` | 빌드 출력 제외, 사용·개발·테스트 절차 |
| `MarkdownReport/WORK_STATUS.md`, `Update_History.md`, `OWERORDER.md`, 이 문서 | 협업 기록 및 개발 보고 |

소스의 클래스·메서드 위에 Role.md 형식의 작성자·생성일·변경일·설명 주석을 작성했다. 기존 Role.md와 Project_Plan.md 본문 및 CodexOrig의 작업 기록은 보존했다.

## 빌드 환경과 검증 범위

이 PC에는 dotnet 호스트와 런타임만 있으며 .NET SDK, Visual Studio, VS2017 개발 워크로드 및 기본 위치의 targeting pack이 없다. Visual Studio나 시스템 SDK를 설치하지 않고 `%TEMP%/CodexCode-VsixFoundation`에 다음 도구를 구성했다.

- 전체 .NET Framework MSBuild: Microsoft.Build.Runtime 17.11.4와 해당 종속 패키지
- C# 컴파일러: Microsoft.Net.Compilers.Toolset 4.11.0, 프로젝트의 `/langversion:7.0` 적용
- NuGet CLI: 6.11.1
- VSIX 도구: Microsoft.VSSDK.BuildTools 15.9.3032
- 참조 어셈블리: Microsoft.NETFramework.ReferenceAssemblies.net462 1.0.3

임시 MSBuild에는 Visual Studio의 NuGet 컴파일 참조 해석 구성 요소가 없으므로, 복원한 `obj/project.assets.json`의 compile 목록으로 임시 `PortableNugetReferences.targets`를 생성했다. 이 파일을 `CustomBeforeMicrosoftCommonTargets`로 주입하고 `Private=false`로 참조했다. 실제 프로젝트의 C#·WPF·VSCT 컴파일, pkgdef 생성 및 VSSDK VSIX 패키징은 정상 실행했다. 임시 참조 목록은 `artifacts/phase1/PortableNugetReferences.targets`에도 보관했다.

**이 결과는 임시 MSBuild 구성의 빌드 검증이다. VS2017의 MSBuild 15.x, 기본 개발 워크로드 구성, F5 디버깅 및 실제 IDE의 설치·실행을 검증한 결과가 아니다.** 일반 개발 PC의 빌드·설치 절차는 `docs/DEVELOPMENT.md`에 별도로 작성했다.

## 테스트 결과

| 검증 | 결과 |
|---|---|
| NuGet 복원 | PASS, 최종 복원 경고 없음 |
| 최종 Debug 및 Release 재빌드 | PASS, 두 구성 모두 DLL 및 VSIX 생성, 경고·오류 없음 |
| 패키지 및 WPF 자동 검사 | Debug 22/22 PASS, Release 22/22 PASS |
| VSIX 내부 DLL·pkgdef·메뉴·BAML | PASS |
| SDK 및 IDE 종속 DLL의 중복 패키징 방지 | PASS |
| WPF 컨트롤 생성 | PASS |
| 밝은/어두운 리소스 변경, 320/200픽셀 폭 렌더링 | PASS |
| 렌더링 이미지 육안 확인 | Light 320 / Dark 200 정상 표시 확인 |
| 생성 DLL 프레임워크·아키텍처 | `.NETFramework,Version=v4.6.2`, MSIL 확인 |
| PowerShell 빌드·Foundation 스크립트 구문 | PASS, 오류 0건 |
| CodexOrig 호환성 회귀 테스트 재실행 | 44/44 PASS (Windows PowerShell 5.1) |
| 최종 원본 매니페스트·프로젝트 정적 검사 | VS2017 PASS, VS2019/2022 미선언 |
| VS2017 실제 설치, 메뉴 실행, 창 재사용·도킹·복원, 설치 제거 | 미실행 — IDE 없음 |

재현 명령은 `docs/DEVELOPMENT.md`에 있다. 이번 PC의 임시 빌드 환경에서는 다음과 같이 실행했다.

```powershell
$toolDir = Join-Path $env:TEMP 'CodexCode-VsixFoundation'
$env:CustomBeforeMicrosoftCommonTargets = Join-Path $toolDir 'PortableNugetReferences.targets'
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\scripts\build.ps1 `
    -MSBuildPath (Join-Path $toolDir 'toolset\MSBuild.exe') `
    -NuGetPath (Join-Path $toolDir 'nuget.exe') -Configuration Release
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -STA `
    -File .\tests\foundation\Test-FoundationPackage.ps1
```

이 명령은 위에 설명한 임시 도구 및 참조 목록이 준비된 현재 PC용이다. 일반 개발 환경에서는 임시 참조 주입 없이 개발 가이드의 표준 빌드 절차를 사용한다.

## 산출물과 기록

- Release VSIX: `src/VS2017.AIAssistant/bin/Release/VS2017.AIAssistant.vsix`
- Release VSIX SHA-256: `3703BF3079F47E2D03545A67113E2F02D5539319AF5764E32089D73BF3DB35EC`
- 빌드 로그: `artifacts/phase1/build-Debug.log`, `build-Release.log`
- Foundation 로그: `artifacts/phase1/foundation-Debug.log`, `foundation-Release.log`
- WPF 이미지: `artifacts/phase1/Light-320.png`, `Light-200.png`, `Dark-320.png`, `Dark-200.png`
- 최종 정적 검사 JSON: `artifacts/phase1/compatibility.json`

`bin/`, `obj/`, `artifacts/`는 Git 제외 대상이다. CodexOrig의 과거 JSON은 해당 작업 시점의 스냅샷으로 보존했고 최신 검사 결과는 위 별도 경로에 저장했다.

## Phase 1 체크리스트

- [x] 독립 솔루션·VSIX 프로젝트 생성
- [x] VS2017 SDK 참조 및 C# 7.0 / Framework 4.6.2 빌드
- [x] WPF 도구 창 및 도구 메뉴 등록 구현
- [x] Debug/Release VSIX 생성 및 자동 패키지 검사
- [x] 독립 WPF 컨트롤 생성·테마·렌더링 검사
- [ ] VS2017 확장 개발 워크로드에서 기본 빌드 및 F5 실행
- [ ] VS2017 Experimental Instance에서 메뉴·창 열기/닫기/재열기 검증
- [ ] VSIX 설치·제거, 도킹·테마 전환·IDE 종료/복원 검증

## 협업 및 배포 상태

WORK_STATUS.md로 담당 범위를 등록하고 CodexOrig의 TASK-002 범위를 피하여 작업했다. 해당 작업자의 검사 도구를 검토하고 완료 이후 통합 검증에 사용했다. 별도 하위 에이전트는 생성하지 않았다.

작업 중 다른 Codex가 진행 중인 동안 GitHub에 push하거나 배포하지 않았다. TASK-002가 완료된 후에도 실제 IDE 검증은 끝나지 않았으므로 Role.md 6번의 검증 완료 조건을 충족했다고 판단하지 않았다. **이번 작업에서는 Git 커밋·push 및 GitHub 릴리스를 수행하지 않았다.** 사용자 확인이나 승인 대기는 없으며, 남은 조건은 실제 VS2017 환경 검증이다.

다음 작업은 VS2017 15.9 개발 환경에서 위 미실행 항목을 검증하는 것이다. Phase 1 완료를 확인한 다음 Codex/Claude Provider 연결을 진행한다.
