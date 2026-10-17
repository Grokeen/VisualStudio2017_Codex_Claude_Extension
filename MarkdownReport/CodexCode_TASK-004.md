# TASK-004 — v0.2.0 소스 수정 기능 개발·검증 보고

- 작업자: CodexCode
- 날짜/시간대: 2026-10-18 / Asia/Seoul (KST)
- 시작: 2026-10-18 00:55:01
- 종료: 2026-10-18 01:26:19 KST
- 소요: 31분 18초
- 상태: 현재 파일 수정 기능 구현, VS2017 표준 빌드 및 자동 회귀 검증 완료. 실제 Codex 수정안 생성도 확인. 실제 IDE 적용·저장 및 Claude 계정 응답은 별도 검증 대상이다.

## 요구사항 검토와 결과

Role.md에 추가된 “비주얼스튜디오에서 확장자로 실행된 코덱스나 클로드가 소스수정이 가능해야 한다”는 요구를 확인했다. 기존 v0.1.0에는 안내용 도구 창만 있었고 AI 연결·편집 기능이 없었다.

v0.2.0에 **현재 파일 수정 요청 → Codex/Claude CLI 수정안 → 변경 비교 → 사용자 승인 → 편집기 버퍼 적용 → 별도 저장·직전 적용 복구**를 구현했다. 원본 Project_Plan.md의 MVP 수정 제외 문구는 추가 Role.md 지시에 맞춰 변경했다. 원래 명세의 사용자 승인·충돌 검사·미저장 내용 보존 원칙은 유지했다.

## 변경 파일

| 경로 | 구현 내용 |
|---|---|
| `src/VS2017.AIAssistant/Editing/DocumentSnapshot.cs` | 경로·버퍼·디스크 해시·읽기 전용 상태와 IDE 편집 인터페이스 |
| `src/VS2017.AIAssistant/Editing/EditProposal.cs` | 수정 JSON 검증, 고유 교체 일치·겹침·개행·크기 검사와 변경 위치 비교 |
| `src/VS2017.AIAssistant/Editing/SourceEditSession.cs` | 승인 후 적용, 별도 저장, 직전 적용 전 버퍼 복구 |
| `src/VS2017.AIAssistant/Services/VisualStudioDocumentService.cs` | EnvDTE 활성 파일·미저장 버퍼·프로젝트 조건, Undo 그룹과 IDE 저장 |
| `src/VS2017.AIAssistant/Providers/CliEditProvider.cs` | Codex exec/Claude print 프로토콜, UTF-8 파이프, 구조화 출력, 오류·취소·타임아웃 |
| `src/VS2017.AIAssistant/Providers/ProcessJob.cs` | Windows Job Object로 CLI 및 하위 프로세스 수명 관리 |
| `src/VS2017.AIAssistant/Views/AssistantControl.xaml` 및 `.xaml.cs` | Provider·실행 파일·수정 요청·비교 탭·승인·적용·저장·복구·중지 UI |
| `src/VS2017.AIAssistant/ToolWindows/AssistantToolWindow.cs` | 패키지가 연결된 이후 문서/AI 서비스 주입, 종료 시 요청 취소 |
| `src/VS2017.AIAssistant/VS2017.AIAssistant.csproj` | EnvDTE 8.0.2, Framework System.Web.Extensions 및 새 소스 포함 |
| `src/VS2017.AIAssistant/Properties/AssemblyInfo.cs`, `source.extension.vsixmanifest` | 버전 0.2.0 및 기능 설명 갱신 |
| `tests/editing/EditingTests.cs`, `Test-SourceEditing.ps1` | 실제 빌드 DLL·가짜 CLI exe·실제 WPF 이벤트를 사용한 55개 회귀 시나리오 |
| `tests/editing/Test-LiveCodex.ps1` | 기존 로그인 Codex 계정으로 합성 코드만 사용하는 명시적 실제 CLI 검증 |
| `tests/foundation/Test-FoundationPackage.ps1` | 편집 UI의 기본 승인 상태, 테마별 승인 문구와 전체 화면 렌더링 검증 |
| `README.md`, `docs/DEVELOPMENT.md`, `docs/SOURCE_EDITING.md` | 사용 순서·빌드/테스트 방법·동작 범위·한계·공식 CLI 근거 |
| `MarkdownReport/Project_Plan.md` | 추가 Role 지시와 충돌하던 초기 MVP 범위 갱신 |
| 협업 현황·작업 이력·OWERORDER·이 문서 | 협업·작업 시간·테스트·산출물 보고 |

작업한 클래스와 메서드에는 Role.md 형식의 설명·작성자·생성일·변경일 주석을 작성했다. 다른 작업자의 설치 스크립트·환경 구성·결과 기록은 수정하지 않았다.

## 편집 동작

- AI에는 요청 시점의 현재 파일 버퍼 전체를 전달한다. 미저장 편집 내용도 포함한다. 프레임워크 정보는 IDE에서 제공할 수 있는 값만 사용하며, 알 수 없는 C# 프로젝트에서는 기존 API와 C# 7.0 문법을 유지하도록 요청한다.
- 응답은 파일 경로가 없는 `summary` 및 `edits[{old_text,new_text}]` 형식이다. AI 결과가 다른 파일을 지정해 적용 범위를 바꿀 수 없다.
- 원본에서 교체 문자열이 정확히 한 번 일치해야 하며, 겹치는 교체와 무제한 출력·크기를 거부한다. 비교 화면은 원본 줄 위치, 변경 전/후 조각 및 전체 원본·수정안을 제공한다.
- 승인 체크와 적용 버튼이 있어야 편집기 버퍼를 변경한다. 컨트롤과 편집 세션 양쪽에서 승인을 확인한다. 새 요청·Provider 전환 시 이전 승인을 해제한다.
- 적용 시에는 저장하지 않는다. 별도 저장 명령에서 파일 인코딩 처리를 IDE에 맡긴다. 미저장 사용자 내용은 원본 스냅샷으로 보관하며, 적용은 하나의 IDE Undo 그룹으로 묶는다.
- 적용·저장·복구 전 파일 식별자·버퍼 내용·디스크 SHA-256·읽기 전용 상태를 확인한다. 이후 편집·외부 변경·이름 변경이 있으면 거부한다. 자동 저장이나 임의 덮어쓰기를 하지 않는다.
- 직전 적용 복구는 해당 적용 이후 편집 내용이 그대로인 경우에 한한다. 전용 저장 버튼 밖에서 Ctrl+S 등으로 디스크 버전이 바뀌면 전용 복구는 보수적으로 거부할 수 있으며 IDE의 Ctrl+Z를 사용할 수 있다.

## CLI 통합

Codex는 read-only sandbox와 구조화 출력 schema를 사용하는 `exec`를 선택했다. 현재 기능은 한 요청씩 처리하는 단일 파일 수정이므로 app-server 세션보다 단순한 비대화형 CLI를 사용한다. app-server 기반 장기 대화 세션은 별도 후속 범위이다. OpenAI Docs와 설치된 CLI의 도움말에서 옵션을 확인했다.

Claude는 print 모드, JSON schema 및 `structured_output`을 사용한다. built-in tools를 비우고 MCP 및 설정 공급원을 제한한다. 두 CLI 모두 별도 빈 디렉터리에서 실행하며 프로젝트를 작업 디렉터리로 주지 않는다. CLI 자체에 파일 수정 권한을 주지 않고, 승인 후 IDE 어댑터가 수정한다.

프롬프트와 소스는 UTF-8 stdin으로 전송한다. 확장은 소스·응답·인증 정보를 로그 파일에 저장하지 않는다. 임시 파일에는 schema와 빈 MCP 구성만 기록한다. stdout/stderr는 동시에 읽고 각각 2,000,000자 제한을 적용한다. 기본 시간 제한은 5분이며 Windows Job Object로 취소·오류·IDE 종료 시 하위 프로세스도 정리한다.

공식 근거와 실제 실행 옵션은 [SOURCE_EDITING.md](../docs/SOURCE_EDITING.md)에 링크했다.

## 빌드 환경과 결과

작업 초기에는 이전 작업의 임시 전체 Framework MSBuild로 개발을 시작했다. 작업 중 CodexOrig가 설치한 실제 VS2017 환경이 준비되어, 최종 산출물은 **설치된 VS2017의 표준 도구로 다시 빌드**했다.

- Visual Studio Community 2017: 15.9.83
- MSBuild: `15.9.30.58560`
- C# 컴파일러: VS2017의 `MSBuild/15.0/Bin/Roslyn/csc.exe`
- 대상: .NET Framework 4.6.2 / C# 7.0 / AnyCPU
- SDK: Microsoft.VisualStudio.Shell.15.0 15.9.28307, VSSDK.BuildTools 15.9.3032, EnvDTE 8.0.2
- 최종 Debug/Release 빌드: PASS, 경고·오류 0건
- 임시 NuGet 참조 목록 주입: 최종 빌드에서 사용하지 않음

이 결과는 실제 MSBuild 15.x의 복원·C#·WPF·VSCT·pkgdef·VSIX 생성 검증이다. 실제 IDE 안에서의 EnvDTE 편집 동작은 이 빌드 결과와 구분한다.

## 검증 결과

| 항목 | 결과 |
|---|---|
| 편집·CLI·WPF 회귀 | 55/55 PASS, 실제 빌드 DLL 및 VS2017 컴파일러 사용 |
| 패키지·테마·WPF 리소스 검사 | 29/29 PASS |
| 기존 호환성 회귀 | 44/44 PASS |
| 최종 VS2017 원본 정적 호환성 검사 | PASS, VS2019/2022 미선언 |
| 승인 전 변경 차단, 적용과 저장 분리, 미저장 버퍼 복구 | PASS (독립 문서 fixture) |
| 사용자 편집·외부 디스크 변경·다른 파일·읽기 전용·저장 실패 | PASS (독립 문서 fixture) |
| Codex/Claude 인수·JSON envelope·한글·공백 경로·UTF-8 파이프 | PASS (실제 exe fixture, 네트워크 없음) |
| stdout/stderr 동시 처리·출력 크기 제한·취소·시간 초과 | PASS |
| 취소 후 자식 프로세스 종료 | PASS (실제 Windows 프로세스/Job Object) |
| WPF 요청 → 비교 → 승인 → 적용 → 저장 → 복구 버튼 흐름 | PASS (실제 WPF와 Provider, 문서/AI 응답은 fixture) |
| 활성 파일 전환 후 원래 요청 문서만 적용, 취소 후 재요청 | PASS (WPF fixture) |
| 밝은/어두운 테마, 폭 320/200, 승인 문구 가독성 및 수정안 화면 | 렌더링·육안 확인 PASS |
| 실제 Codex 계정으로 합성 코드 수정안 생성 | PASS |
| 실제 Codex 응답 후 승인 전 원본 파일 보존 | PASS |
| 실제 Claude 계정 응답 | 미실행 — CLI 미설치 |
| 실제 VS2017 안의 EnvDTE 적용·저장·Undo | 환경 담당자와 별도 검증 대상 |
| VS2019/2022 설치·실행 지원 | 현재 VSIX 범위 아님 |

### 실제 Codex 검증

설치된 `codex-cli 0.162.0-alpha.17.2`의 기존 ChatGPT 로그인을 사용했다. 사용자 프로젝트 대신 작은 합성 C# 코드만 전송했다. 실제 빌드 DLL의 `CliEditProvider.GenerateAsync`가 `GetValue`의 `return 1;`을 `return 2;`로 바꾸는 정확한 수정안을 반환했다. 원본 디스크 파일은 그대로였다.

- 실행 기록: `artifacts/source-editing/live-codex.log`
- 결과 JSON: `artifacts/source-editing/live-b07b34c6c1ba41b5ac621edbaab782f7/result.json`
- 검사 종류: `LiveCliProposalOnly`

이 검증은 실제 Codex 호출 및 수정안 검증 경로까지이며, EnvDTE와 실제 IDE 저장 검증을 대신하지 않는다. 기본 회귀 테스트에서는 실제 AI 계정을 사용하지 않는다.

## 재현 방법

저장소 루트에서 실행한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 `
    -MSBuildPath 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe'
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\editing\Test-SourceEditing.ps1 `
    -CompilerPath 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe'
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -STA -File .\tests\foundation\Test-FoundationPackage.ps1
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\compatibility\Test-VsixCompatibility.Tests.ps1
```

선택적인 실제 Codex 합성 코드 검증은 `tests/editing/Test-LiveCodex.ps1 -CodexPath <native codex.exe>`로 실행한다. 이 테스트만 기존 로그인 AI 계정을 호출한다. 실제 IDE의 사용·검증 순서는 `docs/SOURCE_EDITING.md`에 있다.

## 고정 산출물과 인계

- 개발 빌드 출력: `src/VS2017.AIAssistant/bin/Release/VS2017.AIAssistant.vsix`
- 고정 v0.2.0 인계 파일: `artifacts/source-editing/handoff/VS2017.AIAssistant-0.2.0.vsix`
- 고정 파일 SHA-256: `0B7FD44509A6CA02344E645924378EB544966A0831B942A9F7D4B6CDAB872D94`
- 네이티브 빌드 로그: `artifacts/source-editing/build-vs2017-Debug.log`, `build-vs2017-Release.log`
- 최종 편집 테스트: `artifacts/source-editing/editing-final.log`
- fixture 실행 파일 및 수정안 렌더링: `artifacts/source-editing/run-99efb4fbaf81423b97f72f4de0a11249/`
- 패키지·렌더링 로그: `artifacts/source-editing/foundation-final.log`, `render/`
- 호환성: `artifacts/source-editing/compatibility-tests.log`, `compatibility-final.json`

고정 인계 파일을 만든 뒤 소스 DLL/VSIX는 다시 변경하지 않았다. CodexOrig의 TASK-003에 이 파일과 해시, fixture exe 경로를 인계했다. 환경 담당자가 IDE 테스트를 진행하더라도 공유 bin/obj를 다시 빌드할 필요가 없다.

## 남은 검증과 배포

기능은 단일 기존 파일에 한정한다. 다중 파일, 생성·삭제, 무승인 적용, 일반 채팅/기록, VS2019/2022용 별도 대상은 후속 범위이다.

실제 IDE에서의 패키지 로드·문서 편집·저장·Undo와 실제 Claude 계정 응답은 별도 확인이 필요하다. 다른 Codex가 TASK-003을 진행 중이므로 Role.md 6번에 따라 Git 커밋·push·GitHub 릴리스 배포는 하지 않는다. 작업자의 설치 스크립트와 기존 보고서 기록을 보존했다.
