# TASK-006 — Role 추가 검토 및 MSBuild 배치 진입점

- 작업자: CodexOrig
- 시작: 2026-10-18 01:55:40 KST
- 완료/소요: 아래 완료 기록 참고
- 결과: 배치 및 로컬 컴파일 검증 완료. IDE 로그인 후 라이선스 안내 해소는 미검증.

## 검토 내용과 작업 범위

Role.md에 추가된 두 요구를 확인했다.

1. **확장 내 Codex/Claude 소스 수정**: TASK-004의 v0.2.0에 현재 파일 수정안 생성 → 비교·승인 → 편집기 적용 → 별도 저장·복구가 구현되어 있다. `Editing/SourceEditSession.cs`의 승인·원본 일치 확인과 `Services/VisualStudioDocumentService.cs`의 EnvDTE 버퍼 교체·UndoContext·별도 저장 연결을 검토했다. 실제 IDE 통합 검증은 CodexCode의 TASK-005 진행 범위이므로 해당 소스와 테스트에 중복 변경하지 않았다. 실제 Claude 계정 검증 및 VS2019/VS2022 확장 지원은 여전히 후속 범위이다.
2. **Visual Studio 라이선스 안내 관련 배치**: TASK-003에서 관측한 VS2017의 안내는 IDE 로그인 관련 사항이다. Microsoft의 빌드 권장 경로인 MSBuild를 사용하는 배치 진입점을 추가했다. 이 배치는 IDE를 시작하지 않으므로 빌드 시 IDE의 라이선스 배너가 발생하지 않는다. IDE 화면의 실제 라이선스 안내는 사용자 계정 로그인·정상 활성화 후 별도로 확인해야 한다. 이 작업에서 안내 해소나 활성화가 완료되었다고 판정하지 않았다.

## 개발 내용

- `scripts/Build-Vsix.cmd`: 어느 작업 디렉터리에서든 저장소의 PowerShell 진입점을 호출하고 성공/실패 종료 코드를 전달한다. 지연 환경 변수 확장을 끄고 공백·한글·특수문자 경로를 지원한다.
- `scripts/Invoke-VsixBuild.ps1`: 지정한 VS2017/2019/2022의 MSBuild 및 VSSDK 설치를 `vswhere`로 탐색한다. 완전한 설치와 실행 파일 주 버전을 확인하고, 다른 주 버전으로 대체하지 않는다. `-PlanOnly`는 빌드·IDE 실행 없이 선택 정보를 JSON으로 출력한다.
- 기존 `scripts/build.ps1`를 호출하여 복원·Rebuild·패키지 출력 검증을 그대로 사용한다. 기존 빌드 스크립트·확장 소스·매니페스트·IDE 프로필은 변경하지 않았다.
- `vswhere -utf8` 출력 및 리다이렉트된 PowerShell 출력을 UTF-8로 처리하여 Windows PowerShell 5.1의 한글 경로 손상을 수정했다. 빌드 출력과 오류는 필터링하지 않는다.
- `tests/build-entry/Test-BuildEntry.ps1`: 파일 버전이 있는 작은 네이티브 fixture를 컴파일하고 실제 배치/PowerShell 프로세스 호출로 회귀 검증한다.
- `docs/BUILD_AND_LICENSE.md`: 실행 방법, 빌드 엔진과 VSIX 설치 대상의 차이, 현재 PC 안내의 관측 결과 및 공식 로그인 절차를 기록했다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| Windows PowerShell 5.1.26100.9711 회귀 | 16/16 PASS |
| PowerShell 7.6.5 회귀 | 16/16 PASS |
| 새 PowerShell 파일 구문 분석 | 오류 0건 |
| VS2017 MSBuild 15.9.30.58560 Release | 종료 0, VSIX 생성 |
| VS2019 MSBuild 16.11.6.22506 Release | 종료 0, VSIX 생성 |
| VS2022 MSBuild 17.14.60.43110 Release | 종료 0, VSIX 생성 |
| 위 세 실제 빌드 로그의 IDE 라이선스 배너 | 관측되지 않음 |
| 고정 0.2.0 인계 패키지 SHA-256 | 기존 값 보존 |

회귀 검사는 한글·공백·`&`·`!`·괄호 경로, 다른 현재 디렉터리, 세 주 버전 선택, 네이티브 인수 전달, PS5 빈 인벤토리, 미완성·버전 불일치 설치, 탐색 종료 오류, 잘못된 JSON, 명시적 MSBuild 경로, 버전·파일 부재 오류, 실제 배치의 구성/경로 전달·출력 보존·실패 전파, 지원하지 않는 연도를 확인했다. 초기 실패에서 출력 인코딩 문제를 찾아 수정한 후 최종 두 실행에서 모두 통과했다.

실제 컴파일은 `artifacts/build-entry/compile-e4864061127c48aa9b0cb981a3bae141/`의 소스 스냅샷과 엔진별 별도 디렉터리에서 수행했다. 원본 `bin/obj`와 다른 작업자의 고정 인계 패키지를 사용하거나 덮어쓰지 않았다. 각 원본 파일의 SHA-256을 기록했으며, 검사 시점에 스냅샷 이후 소스 변경이 없음을 확인했다. 이는 VS2017용 패키지의 세 **빌드 엔진** 검사이며 VS2019/VS2022 IDE 설치·실행 지원을 의미하지 않는다.

- 종합 결과 및 로그/패키지 경로: `CodexOrig_TASK-006_Validation.json`
- PS5 최종 회귀: `artifacts/build-entry/run-4eb994af255c4d0f92960d68bf43a952/results.json`
- PS7 최종 회귀: `artifacts/build-entry/run-a3ca5c4c189644a684d781c203dc461d/results.json`
- 소스별 해시: 위 compile 디렉터리의 `source-snapshot.json`
- 고정 인계 해시: `0B7FD44509A6CA02344E645924378EB544966A0831B942A9F7D4B6CDAB872D94`

## 실행 방법

```powershell
.\scripts\Build-Vsix.cmd -VisualStudioVersion 2017 -PlanOnly
.\scripts\Build-Vsix.cmd -VisualStudioVersion 2017 -Configuration Release
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\build-entry\Test-BuildEntry.ps1
pwsh -NoProfile -NonInteractive -File .\tests\build-entry\Test-BuildEntry.ps1
```

## 남은 범위 및 배포

현재 VSIX의 설치 대상은 VS2017 15.9이다. 실제 IDE의 소스 적용·저장·Undo는 TASK-005의 결과가 필요하다. 라이선스 상태를 자동 판정하거나 변경하지 않았으며, IDE 로그인 후 안내 해소도 이 작업에서 확인하지 않았다.

TASK-005가 진행 중이므로 Role.md 6번에 따라 GitHub push·릴리스를 수행하지 않는다. 사용자와 다른 작업자의 변경을 포함한 일괄 커밋도 수행하지 않았다.

## 공식 근거

- [Microsoft: 빌드는 devenv 대신 MSBuild 사용 권장](https://learn.microsoft.com/en-us/visualstudio/ide/reference/devenv-command-line-switches?view=vs-2022)
- [Microsoft: Community가 라이선스를 요구할 때 IDE 로그인](https://visualstudio.microsoft.com/vs/support/)
- [Microsoft: Visual Studio 로그인 방법](https://learn.microsoft.com/en-us/visualstudio/ide/signing-in-to-visual-studio?view=vs-2017)

## 완료 기록

- 완료: 2026-10-18 02:02:51 KST
- 작업 시간: 2026-10-18 01:55:40 ~ 02:02:51 KST
- 소요: 7분 11초

