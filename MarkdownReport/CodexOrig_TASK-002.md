# TASK-002 완료 보고 — CodexOrig

## 작업 정보

- 작업명: Phase 1 지원용 VSIX 정적 호환성 검사 및 독립 회귀 테스트
- 작업자: CodexOrig
- 작업 날짜: 2026-10-17, Asia/Seoul (KST)
- 시작: 22:11:32 / 완료: 22:19:55 / 소요: 8분 23초
- 상태: 독립 검사 도구 구현·로컬 검증 완료. VSIX 설치·실행 검증과 프로젝트 전체 완료는 별도이다.
- 협업: TASK-001의 CodexCode가 VSIX 소스·솔루션·빌드를 담당하므로 해당 파일을 변경하지 않았다.

## 변경 파일

| 경로 | 내용 |
|---|---|
| `scripts/Test-VsixCompatibility.ps1` | VSIX 매니페스트·프로젝트 선언 검사, 버전별 행렬, JSON 보고서, CLI 종료 코드 |
| `tests/compatibility/Test-VsixCompatibility.Tests.ps1` | 외부 모듈 없는 44개 회귀 시나리오 |
| `docs/VSIX_COMPATIBILITY.md` | 공식 근거, 저장소의 SDK 정책, 실행법, 판정 한계와 실제 IDE 테스트 절차 |
| `MarkdownReport/CodexOrig_TASK-002_Compatibility.json` | TASK-001 실제 원본 프로젝트의 VS2017 기본 정적 검사 결과 |
| `MarkdownReport/CodexOrig_TASK-002.md` | 이 완료 보고서 |
| `MarkdownReport/WORK_STATUS.md` | 자기 작업 범위·상태 및 CodexCode와의 파일 기반 협업 기록 |
| `MarkdownReport/Update_History.md` | 자기 작업의 날짜·시간·소요 시간·변경 내용 추가 |
| `MarkdownReport/OWERORDER.md` | 상관 보고용 개발 내역·검증 방법 추가 |

## 구현 내용

- VS2017을 기본 필수 대상으로 검사하고, VS2019·VS2022는 사용자가 필수 대상으로 지정할 수 있게 했다.
- 정확한 버전, 열린 구간과 포함·제외 경계, VS2017 15.9 등 하위 버전 한정 선언을 처리한다. 원래 범위를 결과에 보존한다.
- VS2022 `amd64`/`arm64`와 레거시 x86 대상, 명시된 `PlatformTarget`, SDK·BuildTools 패키지 주 버전을 검사한다.
- 이 실행 코드 확장의 레거시/VS2022 프로젝트 분리 정책을 검사한다. 매니페스트 범위 확장만으로 모든 IDE 호환을 인정하지 않는다.
- .NET Framework 4.x, VS2017의 명시적 C# 버전, VsPackage asset을 검사한다.
- `PackageReference`의 속성·자식 버전과 `packages.config`를 지원한다. SDK의 변수·부동 버전이나 해석하지 못하는 참조는 검증 완료로 간주하지 않는다.
- XML DTD/외부 엔터티를 금지하고, 검사 입력 파일은 보존한다. JSON 출력은 별도 파일만 허용한다.
- Windows PowerShell 5.1의 한글 인코딩 문제를 수정하여 `.ps1`을 UTF-8 BOM으로 저장했다.

## 검증 결과

| 항목 | 결과 |
|---|---|
| Windows PowerShell 5.1.26100.9711 | 44/44 PASS, 테스트 프로세스 종료 코드 0 |
| PowerShell 7.6.5 | 44/44 PASS, 테스트 프로세스 종료 코드 0 |
| 두 스크립트 구문 분석 | 오류 0건 |
| CLI 정상·실패 종료 코드 | 각각 0 / 1 확인 |
| 한글·공백 경로, XML 오류·DTD, 입력 파일 보존, JSON 저장 | PASS |
| 실제 Phase 1 매니페스트·csproj 기본 검사 | VS2017 정적 검사 PASS, 종료 코드 0 |
| VS2019·VS2022 실제 지원 | 매니페스트 미선언. 구현·설치 검증 완료로 보고하지 않음 |
| 실제 VSIX 빌드·설치·Experimental Instance | TASK-002에서 미수행. TASK-001의 빌드 작업 및 실제 IDE 환경 검증 필요 |

검사 시점 원본의 프레임워크는 `v4.6.2`, 언어는 `7.0`, 매니페스트는 `[15.0.28307.0,16.0)`이다. SDK는 Shell.15.0 `15.9.28307`, BuildTools는 `15.9.3032`이다. 명시적인 `PlatformTarget`이 없으므로 유효 컴파일 아키텍처는 MSBuild로 확인해야 한다는 경고를 남겼다. 모든 실행에는 정적 검사임을 알리는 경고도 포함된다.

최종 확인 중 TASK-001의 원본 변경을 감지하여 22:19:55 KST에 최신 소스로 정적 검사를 다시 실행했고 PASS를 확인했다. JSON도 이 결과로 갱신했다.

검사 대상 스냅샷의 SHA-256:

- `source.extension.vsixmanifest`: `2A1DE711B27575EB64C9EA89CCFB7A727F58E9814F21985E326A1CC8643D1B00`
- `VS2017.AIAssistant.csproj`: `62C0B54FF8AA9B0DC0459B6CDD982397A3D3D33D827795E5529D7DAE85E59809`

이후 담당 작업자가 소스를 변경하면 검사를 다시 실행해야 한다.

## 재현 방법

저장소 루트에서 실행한다.

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\compatibility\Test-VsixCompatibility.Tests.ps1
pwsh -NoProfile -NonInteractive -File .\tests\compatibility\Test-VsixCompatibility.Tests.ps1
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\scripts\Test-VsixCompatibility.ps1 `
    -ManifestPath .\src\VS2017.AIAssistant\source.extension.vsixmanifest `
    -ProjectPath .\src\VS2017.AIAssistant\VS2017.AIAssistant.csproj `
    -JsonReportPath .\MarkdownReport\CodexOrig_TASK-002_Compatibility.json
```

## 미완료 및 다음 작업

- 현재 환경의 `dotnet --info`에서는 SDK가 없고 런타임만 확인되었다. Visual Studio/VS SDK도 기본 설치 경로에서 확인되지 않았다.
- VSIX 빌드 검증은 TASK-001 담당자가 진행 중이다. TASK-002의 PowerShell 구문 분석·실행 검증을 VSIX 컴파일 성공으로 대체하지 않았다.
- 실제 VS2017 설치 후 도구 창을 열고 닫는 검증이 필요하다. VS2019·VS2022는 각각 대상 선언·참조 프로젝트·실제 IDE 검증이 필요하다.
- CodexCode가 작업 중이므로 `Role.md` 6번에 따라 이 작업에서는 GitHub push·릴리스 배포를 하지 않았다. 사용자 승인 대기는 없다.
