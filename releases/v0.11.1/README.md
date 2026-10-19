# Visual Studio AI Assistant v0.11.1 설치

Visual Studio 업데이트가 불가능한 **VS2017 15.7.1**을 위해 당시 SDK로 컴파일한 전용 패키지를 추가했습니다.

| 설치된 IDE | 설치 파일 |
|---|---|
| VS2017 15.7·15.8 | [VS2017.15.7.AIAssistant-0.11.1.vsix](VS2017.15.7.AIAssistant-0.11.1.vsix?raw=true) |
| VS2017 15.9 이상 | [VS2017.AIAssistant-0.11.1.vsix](VS2017.AIAssistant-0.11.1.vsix?raw=true) |
| VS2019 16.x | [VS2019.AIAssistant-0.11.1.vsix](VS2019.AIAssistant-0.11.1.vsix?raw=true) |
| VS2022 17.x x64 | [VS2022.AIAssistant-0.11.1.vsix](VS2022.AIAssistant-0.11.1.vsix?raw=true) |

[GitHub Release](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.11.1)에서 [설치 묶음 ZIP](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.11.1/VisualStudio_AI_Assistant-0.11.1.zip)을 내려받아 모두 풀고 **InstallAIAssistant.bat**를 실행하세요. 여러 IDE가 있으면 번호를 선택합니다. 사전 검사를 통과한 대상의 공식 VSIXInstaller를 열고, 설치 화면을 따라 진행합니다. Visual Studio 자체를 업데이트하지 않습니다.

수동으로 설치할 때는 위 표의 파일 한 개를 실행하세요. Community·Professional·Enterprise를 지원합니다. VS2017 두 패키지는 같은 확장 ID를 사용하므로 한 IDE에 함께 설치하지 않습니다. VS2017 15.7 미만과 VS2022 ARM64는 현재 지원하지 않습니다.

VS2017 15.7 전용은 Shell SDK 15.7.27703·Threading 15.6.31, 15.9 이상용은 기존 SDK 계열로 별도 빌드합니다. **현재 개발 PC에는 15.7.1 Professional이 없어, 고객 PC에서의 설치·실행 성공은 아직 확인하지 않았습니다.** 같은 구버전 패키지를 VS2017 15.9 호스트에서 검사한 결과와 구분합니다.

오류가 나면 [설치 진단 안내](../../docs/INSTALL_TROUBLESHOOTING.md)를 확인하세요. 설치 없이 검사하려면 ZIP 폴더에서 다음 명령을 실행합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-AIAssistant.ps1 -PackageDirectory .
```

설치 후 Visual Studio의 **도구 → AI Assistant**를 선택하세요. Codex·Claude의 Windows 네이티브 CLI와 본인 계정의 공식 로그인이 필요합니다. [자세한 사용법](../../docs/USER_GUIDE.md), [CLI 설치 설명서](../../HowToInstallCLI.md)를 제공합니다.

파일 손상 검사는 SHA256SUMS.txt(네 VSIX), BUNDLE_SHA256SUMS.txt(ZIP 내부), RELEASE_SHA256SUMS.txt(다운로드 파일)로 확인하세요. 기존 v0.11.0 자산은 그대로 보존합니다. 별도 테스트 실행기는 저장소 소스와 개발 도구가 있는 환경에서 사용합니다.
