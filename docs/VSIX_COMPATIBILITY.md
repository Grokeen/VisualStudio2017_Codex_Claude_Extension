# VSIX 호환성 검사 — CodexOrig / TASK-002

작성일: 2026-10-17 (KST)

## 목적과 판정 범위

`scripts/Test-VsixCompatibility.ps1`은 WPF 패키지 확장의 **원본 선언**을 검사한다. 기본 필수 대상은 VS2017이다. VS2019·VS2022가 선언되지 않았다는 이유만으로 초기 Phase 1을 실패 처리하지 않는다. 해당 버전을 필수로 요청하면 미선언 상태를 실패로 반환한다.

`Succeeded = true`는 선택한 정적 정책에 어긋나는 선언이 발견되지 않았다는 뜻이다. 실제 VSIX 빌드·설치·도구 창 실행 성공을 의미하지 않는다. `Matrix.ManifestStatus = Declared`도 매니페스트 선언 상태만 나타낸다. 조건부 MSBuild 속성과 import, 전이 종속성, DLL API 사용, 완성된 VSIX의 내용은 평가하지 않는다.

## 프로젝트 정책

| IDE | 매니페스트 버전 구간 예시 | 프로세스/페이로드 | 이 프로젝트의 SDK 패키지 기준 |
|---|---|---|---|
| VS2017 | `[15.0,16.0)` 또는 실제 최소 버전부터 | x86 / AnyCPU 또는 x86 | SDK·Shell 및 명시된 BuildTools 15.x |
| VS2019 | `[16.0,17.0)` | x86 / AnyCPU 또는 x86 | SDK·Shell 및 명시된 BuildTools 15.x~16.x |
| VS2022 x64 | `[17.0,18.0)` + `ProductArchitecture=amd64` | x64 / AnyCPU 또는 x64 | 별도 프로젝트의 17.x 참조 |
| VS2022 ARM64 | `[17.0,18.0)` + `ProductArchitecture=arm64` | ARM64 / AnyCPU 또는 ARM64 | 별도 프로젝트의 17.x 참조 및 ARM64 검증 |

이 표의 SDK/BuildTools 제한은 저장소의 명확한 빌드 기준을 위한 보수적 정책이다. 모든 외부 확장 프로젝트에 적용되는 일반적인 Microsoft 지원 판정표는 아니다. 다른 빌드 도구 조합을 사용할 때는 실제 MSBuild 결과를 검토하여 정책을 조정해야 한다.

VS2022는 64비트이며 실행 코드를 포함한 확장은 별도 참조 집합이 필요하다. 현재 WPF 확장에서는 레거시 프로젝트와 VS2022 프로젝트를 분리하는 구조를 기준으로 검사한다. 매니페스트 상한만 열어 한 프로젝트가 세 IDE를 모두 지원한다고 선언하면 실패한다. 근거: [Microsoft의 VS 확장 마이그레이션 안내](https://learn.microsoft.com/en-us/visualstudio/extensibility/migration/update-visual-studio-extension?view=vs-2022).

VS2022의 `ProductArchitecture`는 필수이며, 이전 대상에서는 생략 시 x86이다. ARM64를 추가할 때는 아키텍처마다 대상 항목을 둔다. 근거: [매니페스트 스키마](https://learn.microsoft.com/en-us/visualstudio/extensibility/vsix-extension-schema-2-0-reference?view=vs-2022), [ARM64 확장 대상 설정](https://learn.microsoft.com/en-us/visualstudio/extensibility/arm64/target-arm64-visual-studio-extension).

VS2017 대상으로 선언된 소스의 명시적 `LangVersion`은 7.3 이하로 제한한다. C# 7.3 빌드에는 VS2017 15.7 이상이 필요하다. 실제 최소 빌드 IDE와 대상 IDE의 API 호환성은 별도로 검증한다. 근거: [C# 언어 버전과 컴파일러 대응표](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/language).

## 실행

저장소 루트에서 Windows PowerShell 5.1 또는 PowerShell 7로 실행한다. 추가 모듈이나 Pester는 필요 없다. 한글 주석과 테스트 경로를 위해 두 `.ps1`은 UTF-8 BOM으로 저장한다.

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\scripts\Test-VsixCompatibility.ps1 `
    -ManifestPath .\src\VS2017.AIAssistant\source.extension.vsixmanifest `
    -ProjectPath .\src\VS2017.AIAssistant\VS2017.AIAssistant.csproj `
    -JsonReportPath "$env:TEMP\vsix-compatibility.json"
```

기본 CLI는 정적 검사 통과 시 종료 코드 `0`, 실패 또는 보고서 저장 오류 시 `1`을 반환한다. JSON 저장 폴더는 미리 존재해야 한다. 보고서 경로는 별도의 `.json` 파일이어야 한다.

다음은 PowerShell 세션에서 세 버전을 모두 필수로 요구하는 검사다. 초기 VS2017 전용 프로젝트에서는 VS2019·VS2022 미선언 실패가 예상된다. `-PassThru`는 프로세스 종료 코드를 변경하지 않고 결과 객체를 반환하므로 자동화에서는 `Succeeded`를 확인한다.

```powershell
$result = & .\scripts\Test-VsixCompatibility.ps1 `
    -ManifestPath .\src\VS2017.AIAssistant\source.extension.vsixmanifest `
    -ProjectPath .\src\VS2017.AIAssistant\VS2017.AIAssistant.csproj `
    -RequiredVersions @('2017', '2019', '2022') -PassThru
$result.Matrix
$result.Failures
```

VS2022 전용 프로젝트 검사에는 `-RequiredVersions @('2022')`를 지정한다. ARM64 대상으로 검사할 때는 `-Vs2022Architecture arm64`도 지정한다.

## 경고와 실패 해석

- 지원 버전은 주 버전 전체와의 교집합으로 판단한다. `[15.9,16.0)`도 VS2017 선언으로 인식하며, 출력에 원래 범위를 보존한다. 모든 VS2017 하위 버전을 지원한다는 의미가 아니다.
- 명시적인 `PlatformTarget`이 없으면 유효 빌드 아키텍처를 확인하라는 경고를 출력한다. 기본값이나 import 값을 추정하여 아키텍처가 검증되었다고 보고하지 않는다.
- 모든 리터럴 속성·패키지 선언을 검사한다. 조건별로 서로 다른 SDK/플랫폼을 사용하는 프로젝트는 이 검사만으로 판정할 수 없으므로 MSBuild 평가가 필요하다.
- `PackageReference`의 버전 속성·자식 요소와 인접한 `packages.config`를 읽는다. SDK의 변수·범위·부동 버전, 중앙 패키지 관리, 직접 어셈블리 참조만 있는 경우에는 SDK 버전을 확정할 수 없으므로 실패한다.
- BuildTools 패키지가 없으면 설치된 VSSDK targets 수동 검토 경고를 출력한다. 이는 빌드 가능성을 보장하지 않는다.
- .NET Framework 4.x 선언과 VsPackage asset 유무도 확인한다. 프레임워크 prerequisites의 실제 설치 여부와 모든 런타임 의존성은 별도 확인이 필요하다.
- XML DTD와 외부 엔터티를 금지한다. 검사 입력 파일을 변경하지 않는다.

## 회귀 테스트

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\compatibility\Test-VsixCompatibility.Tests.ps1
pwsh -NoProfile -NonInteractive -File .\tests\compatibility\Test-VsixCompatibility.Tests.ps1
```

테스트는 고유 임시 폴더에만 fixture를 생성하고 종료 시 해당 폴더를 정리한다. 버전 경계, 미선언 대상, 잘못된 아키텍처/SDK, ARM64, XML 오류, `packages.config`, 한글·공백 경로, 입력 파일 보존, JSON 저장 및 CLI 종료 코드를 검증한다.

## 실제 IDE 검증 순서

1. 담당 작업자가 제공한 빌드 절차로 Release VSIX를 생성한다. 빌드 로그와 실제 산출물 경로를 기록한다.
2. 대상 IDE별 Experimental Instance에 해당 아키텍처의 VSIX를 설치한다.
3. 도구 메뉴에서 AI 도구 창 열기·닫기·재열기, 테마 변경, 솔루션 전환과 IDE 종료를 확인한다.
4. VS2017의 .NET Framework 4.6.2 예제 솔루션과 함께 실행하고 오류/ActivityLog를 확인한다.
5. 각 IDE의 설치·제거 결과를 따로 기록한다. 정적 검사를 실제 IDE 검증 결과로 대체하지 않는다.

다른 작업자가 `진행 중`이면 `MarkdownReport/Role.md`에 따라 GitHub push·릴리스 배포를 수행하지 않는다.
