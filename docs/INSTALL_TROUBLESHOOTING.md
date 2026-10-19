# Visual Studio 확장 설치 오류 해결

2026-10-19 보고된 오류는 `Visual Studio Professional 2017`을 찾은 뒤 `VSIXInstaller.NoApplicableSKUsException`으로 중단된 사례입니다. 사용자가 알려 준 버전은 **15.7.1**입니다. 기존 v0.11.0의 VS2017 패키지는 **15.9 이상**을 대상으로 하므로 설치할 수 없습니다. 사용자 PC는 업데이트가 불가능한 조건이므로 **15.7 당시 SDK로 별도 빌드한 패키지**를 제공합니다.

## 업데이트 없이 VS2017 15.7.1에 설치하기

1. [v0.11.1 설치 묶음 ZIP](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.11.1/VisualStudio_AI_Assistant-0.11.1.zip)을 새 폴더에 **모두 압축 해제**합니다.
2. 작업 내용을 저장하고 Visual Studio를 닫습니다.
3. **InstallAIAssistant.bat**를 실행합니다. 설치된 IDE가 여러 개면 설치할 번호를 선택합니다.
4. VS2017 15.7·15.8에는 **VS2017.15.7.AIAssistant-0.11.1.vsix**, 15.9 이상에는 **VS2017.AIAssistant-0.11.1.vsix**를 선택해 해당 IDE의 공식 설치기를 엽니다. 설치 화면을 따라 진행합니다.
5. Visual Studio를 다시 열고 **도구 → AI Assistant**를 선택합니다.

수동 설치를 원하면 **VS2017.15.7.AIAssistant-0.11.1.vsix**를 직접 실행해도 됩니다. 15.7용과 15.9용은 같은 확장 ID를 사용하므로 한 IDE에 두 개를 함께 설치하지 않습니다. VS2019·2022용은 각 연도 전용 파일을 사용합니다. VS2022 패키지는 x64용이며 ARM64 IDE는 현재 지원하지 않습니다. VS2017 15.7 미만은 지원 범위에 포함되지 않습니다.

Professional을 Community로 바꿀 필요는 없습니다. Community 대상 확장은 Professional·Enterprise에도 적용됩니다. [Microsoft VSIX 매니페스트 규칙](https://learn.microsoft.com/en-us/visualstudio/extensibility/vsix-extension-schema-2-0-reference?view=vs-2022#installation-element)을 참고하세요.

15.7 전용 패키지는 Shell SDK 15.7.27703과 Threading 15.6.31에 맞춰 컴파일합니다. 개발 PC의 VS2017은 15.9이므로 실제 IDE 검증과 고객의 15.7.1 실행 성공은 구분합니다. **고객 15.7.1 PC에서의 설치·실행 성공은 아직 확인하지 않았습니다.**

## 설치하지 않고 검사하기

압축을 푼 폴더에서 PowerShell을 열고 다음 명령을 실행합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-AIAssistant.ps1 -PackageDirectory .
```

이 명령은 IDE 이름·설치 버전·아키텍처·해당 연도 패키지·SHA256 체크섬·핵심 편집기 존재 여부를 검사합니다. 설치·업데이트·CLI 로그인은 실행하지 않습니다. 최종 .NET/VSIX 의존성 검사는 공식 VSIXInstaller가 수행합니다. 체크섬은 묶음 파일의 손상 검사이며 게시자 서명을 대신하지 않습니다.

표시되는 진단 JSON 경로를 기록하세요. `Ready`는 사전 검사 통과이며 설치 완료를 뜻하지 않습니다. `UnsupportedVersion`이면 IDE 버전과 해당 파일을 확인하고, `InvalidPackage`이면 ZIP 전체를 새 폴더에 다시 풀어 사용하세요. `MissingCoreEditor`나 `IncompleteInstallation`이면 Visual Studio Installer에서 수정/복구와 필요한 재부팅을 완료합니다. 이 도구는 Visual Studio 자체를 업데이트하지 않습니다.

오류가 계속되면 실행한 VSIX 파일명, **도움말 → Microsoft Visual Studio 정보**의 버전, 화면에 표시된 진단 JSON과 VSIXInstaller 로그 경로를 알려주세요. 진단에는 로컬 설치 경로가 포함되며 자동 전송하지 않습니다. 이미 생성한 진단 파일은 덮어쓰지 않습니다.
