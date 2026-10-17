# 배치 빌드와 Visual Studio 로그인

Role.md의 라이선스 안내 관련 배치 요구에 따라 `scripts/Build-Vsix.cmd`를 추가했다. 이 배치는 **IDE를 시작하지 않고 MSBuild로 VSIX를 빌드**하므로 `devenv.com`에서 발생하는 IDE 시작·라이선스 배너를 출력하지 않는다. 빌드 오류와 경고는 그대로 표시한다.

**IDE에서 직접 나타나는 로그인·라이선스 안내를 없애는 작업은 별도이다.** Microsoft는 Community 설치에서 라이선스를 요구하면 IDE에 로그인하도록 안내한다. 이 배치는 로그인이나 활성화를 대신하지 않으며, IDE의 라이선스 검사·레지스트리·평가 기간을 변경하지 않는다. 빌드 성공도 정품 인증 확인을 뜻하지 않는다.

## 사용 방법

PowerShell 또는 명령 프롬프트에서 실행한다. 현재 작업 디렉터리에 상관없이 배치 파일이 있는 저장소의 솔루션을 빌드한다.

```powershell
# 기본: 설치된 VS2017의 MSBuild, Release 구성
.\scripts\Build-Vsix.cmd

# IDE 시작·복원·빌드 없이 선택된 엔진 확인(JSON)
.\scripts\Build-Vsix.cmd -VisualStudioVersion 2017 -PlanOnly

# 버전 및 구성 명시
.\scripts\Build-Vsix.cmd -VisualStudioVersion 2019 -Configuration Debug
.\scripts\Build-Vsix.cmd -VisualStudioVersion 2022 -PlanOnly

# 자동 탐색을 쓸 수 없는 경우: 지정 버전과 실제 MSBuild 주 버전이 일치해야 한다.
.\scripts\Build-Vsix.cmd -VisualStudioVersion 2017 -MSBuildPath 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe'
```

자동 탐색은 `vswhere`로 해당 주 버전의 Community/Professional/Enterprise 중 MSBuild와 VSSDK를 갖춘 설치를 선택한다. 선택한 버전이 없으면 다른 버전으로 조용히 대체하지 않고 종료 코드 1을 반환한다. MSBuild 실행 파일의 버전도 다시 검사한다. 배치는 Windows PowerShell 5.1로 실행하며 PS7에서 직접 `.ps1`을 실행할 수도 있다.

기존 `scripts/build.ps1`의 NuGet 복원·Rebuild·패키지 생성 검증을 호출하며, 결과는 `src/VS2017.AIAssistant/bin/<구성>/VS2017.AIAssistant.vsix`에 생성된다. 성공은 종료 코드 0, 실패는 1이다. 빌드 로그를 보관하려면 명령 프롬프트에서 `scripts\Build-Vsix.cmd > build.log 2>&1`로 실행한다. 로그 내용은 필터링하지 않는다.

**빌드 엔진 선택은 VSIX 설치 대상 지원과 다르다.** 현재 v0.2.0은 VS2017 15.9용이며, VS2019·VS2022에서 MSBuild를 선택해도 매니페스트나 참조 SDK가 변경되지 않는다. 해당 IDE용 확장 지원은 별도 개발·설치 검증이 필요하다. 다른 작업자가 빌드/검증 중이면 공유 `bin/obj` 사용을 조율한다. 고정 인계 패키지는 이 배치가 덮어쓰지 않는다.

## 현재 PC의 IDE 안내 해소

2026-10-18 TASK-003의 실제 `devenv.com` 빌드에서 VS2017 Community에 “Visual Studio 라이선스가 30일 후에 만료됩니다.” 안내가 관측되었다. VS2019/VS2022의 같은 빌드에서는 이 문구가 관측되지 않았다. 안내가 없다는 사실만으로 활성화 상태를 판정하지 않는다.

1. 해당 버전의 일반 Visual Studio를 연다. VS2017 경로는 `C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE\devenv.exe`이다.
2. IDE의 우측 상단 계정 또는 파일 → 계정 설정에서 본인의 Microsoft 계정으로 로그인한다. 계정 인증은 사용자가 직접 수행한다.
3. 필요하면 도움말 → 제품 등록/라이선스 상태에서 표시된 상태를 확인하고, 화면이 제공하는 라이선스 갱신 작업을 수행한다. 메뉴 이름은 버전과 언어에 따라 다를 수 있다.
4. 해당 IDE를 다시 열어 안내가 해소되었는지 확인한다. 계속 나타나면 아래 Microsoft 지원 문서를 따른다. Professional/Enterprise는 해당 제품에 유효한 계정 또는 제품 키가 필요하다.

자동화된 배치로 계정 활성화가 완료되었다고 보고하지 않는다. 현재 작업은 MSBuild 배치 및 컴파일 검증이며, 로그인 후 IDE의 안내 해소는 아직 미검증이다.

## 재현 검사

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\build-entry\Test-BuildEntry.ps1
pwsh -NoProfile -NonInteractive -File .\tests\build-entry\Test-BuildEntry.ps1
```

검사는 별도 fixture 디렉터리에서 배치 호출·경로 처리·버전 선택·오류 전파를 확인한다. 실제 사용자 소스, IDE 프로필, 라이선스 상태는 변경하지 않는다. 실제 VSIX 컴파일 결과와 범위는 `MarkdownReport/CodexOrig_TASK-006.md`에 기록한다.

## 공식 근거

- [Microsoft: devenv 명령줄 옵션 — 빌드에 MSBuild 사용 권장](https://learn.microsoft.com/en-us/visualstudio/ide/reference/devenv-command-line-switches?view=vs-2022)
- [Microsoft: Community에서 라이선스를 요구할 때 IDE 로그인](https://visualstudio.microsoft.com/vs/support/)
- [Microsoft: Visual Studio 로그인](https://learn.microsoft.com/en-us/visualstudio/ide/signing-in-to-visual-studio?view=vs-2017)
- [Microsoft: vswhere 프로젝트와 인스턴스 탐색](https://github.com/microsoft/vswhere)
