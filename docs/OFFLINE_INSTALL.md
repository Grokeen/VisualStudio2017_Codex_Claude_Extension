# 오프라인 설치 안내

이 안내는 **Visual Studio AI Assistant 확장과 Windows용 Codex·Claude CLI를 인터넷이 없는 PC에 설치**하는 절차입니다. AI 모델 요청과 계정 인증까지 오프라인으로 처리하지는 않습니다. 설치 후 네트워크 연결이 허용되면 본인 계정으로 공식 로그인을 하고 연결을 확인하세요.

> 준비 PC에는 인터넷 연결이 필요합니다. 오프라인 PC에는 확장 설치 파일과 CLI 실행 파일을 USB 등으로 옮깁니다. CLI 프로필, 로그인 정보, 토큰은 복사하지 않습니다.

## 1. 준비할 환경

- 인터넷에 연결된 준비 PC와 파일을 옮길 USB 또는 승인된 이동 매체
- 대상 PC에 설치된 지원 Visual Studio
- 공급받은 **PreparedOffline-*.zip(FullOffline 설치 묶음)**. 공개된 VSIX 설치 ZIP만으로는 Codex·Claude CLI 런타임이 포함되지 않습니다.
- Windows x64, Windows 10 1809/Server 2019 이상, Windows PowerShell 5.1 이상. 이 조건은 동봉 Claude CLI 기준이며 Visual Studio 패키지 버전 조건과는 별개입니다. `winver`로 Windows 버전을 확인하세요.

v0.11.1 설치 묶음은 Visual Studio 버전에 맞춰 아래 VSIX를 포함합니다.

| 설치된 IDE | 설치 파일 |
|---|---|
| VS2017 15.7·15.8 | `VS2017.15.7.AIAssistant-0.11.1.vsix` |
| VS2017 15.9 이상 | `VS2017.AIAssistant-0.11.1.vsix` |
| VS2019 16.11 | `VS2019.AIAssistant-0.11.1.vsix` |
| VS2022 17.x x64 | `VS2022.AIAssistant-0.11.1.vsix` |

설치 대상과 맞는 VSIX를 사용하세요. VS2017 15.7 전용 패키지는 .NET Framework 4.6.2 이상 등 해당 Visual Studio 구성 요소가 필요합니다.

## 2. 인터넷 PC에서 오프라인 묶음 준비

1. 제공받은 준비 도구를 새 폴더에 모두 압축 해제합니다.
2. 인터넷이 연결된 준비 PC에 공식 Windows x64 Codex와 Claude CLI를 설치합니다. 기본적으로 Codex 전체 패키지는 `%USERPROFILE%\.codex\packages\standalone\current`, Claude 실행 파일은 `%USERPROFILE%\.local\bin\claude.exe`에서 찾습니다. 다른 위치라면 `Prepare-OfflineSetup.ps1`의 `-CodexPackageDirectory`와 `-ClaudeExecutable` 매개 변수로 지정합니다. Codex는 `bin` 폴더만이 아니라 standalone 패키지 전체가 필요합니다. CLI를 다시 설치하거나 계정 로그인까지 할 필요는 없습니다.
3. 저장소의 `PrepareOfflineSetup.bat`을 실행합니다. 이 배치는 `Prepare-OfflineSetup.ps1 -CreateZip`을 시작해 CLI 게시자 서명과 파일 구성을 확인한 뒤 VSIX, 설치 도구, CLI 런타임을 준비합니다. 기본으로 현재 폴더에 `PreparedOffline-날짜-시간` 폴더와 같은 이름의 `.zip` 파일, `.zip.sha256` 체크섬 파일을 만듭니다.
4. 완료 화면의 ZIP 경로와 CLI 버전·파일 검사 결과를 확인합니다. 이동 전 PowerShell에서 `Get-ChildItem .\PreparedOffline-*.zip | Get-FileHash -Algorithm SHA256`을 실행하고, 출력한 `Hash`를 `.zip.sha256` 파일의 값과 비교합니다. ZIP과 같은 경로의 `.zip.sha256` 파일을 함께 USB에 복사합니다. 또는 준비 폴더 전체와 `OFFLINE_SHA256SUMS.txt`를 함께 복사할 수 있습니다.

준비 단계는 인터넷 PC에서 공식 게시자 서명을 확인합니다. Codex standalone 패키지의 실행 파일·보조 도구·리소스·라이선스/버전 metadata와 Claude CLI 실행 파일을 묶지만, `%USERPROFILE%` 아래의 Codex·Claude 설정, 로그인 세션, 계정, 토큰, 대화 기록은 포함하지 않습니다. 준비 중 CLI를 실행하거나 로그인하지 않습니다.

## 3. 오프라인 PC에 CLI 설치

1. USB에서 `PreparedOffline-...zip` 전체를 대상 PC의 새 폴더로 복사한 뒤 압축을 모두 풉니다. 압축 파일 안에서 BAT를 직접 실행하지 마세요.
2. `InstallOfflineCLI.bat`을 실행합니다. 직접 PowerShell 스크립트를 실행하면 기본은 검증만 하며, 파일을 복사하려면 `-Install`을 지정해야 합니다.
3. 오프라인 설치기는 manifest의 파일 수·크기·SHA-256과 실행 파일의 x64 구조를 확인합니다. 이 단계에서 온라인 서명 조회나 CLI 실행은 하지 않습니다. 검사를 마치면 새 사용자별 폴더에 복사합니다. 기본 설치 위치는 `%LOCALAPPDATA%\Programs\VisualStudioAIAssistant\OfflineCLI\codex-<Codex버전>_claude-<Claude버전>_win-x64`입니다. 완료 화면에 표시된 `codex.exe`와 `claude.exe`의 전체 경로를 기록합니다. 실행 파일은 각각 `cli\codex\bin\codex.exe`와 `cli\claude\claude.exe`에 있습니다.

CLI 설치기는 묶음을 검증할 때 CLI를 실행하거나 네트워크에 접속하지 않습니다. 같은 묶음을 다시 실행하면 설치 파일 무결성을 재확인합니다. 기존 CLI 설치 파일을 덮어쓰거나 PATH·기존 설정·계정 정보를 자동으로 변경하지 않습니다. `InstallCLI.bat`은 온라인 다운로드용이므로 오프라인 PC에서는 실행하지 마세요. CLI 설치 결과 JSON은 기본적으로 `%TEMP%\OfflineCLI-install-<GUID>.json`에 생성되며, 콘솔에 표시되는 로그 경로를 보관하세요. VS 설정에서 CLI 경로를 지정할 때 기존 CLI와 새 전용 폴더 중 사용할 경로를 직접 선택하세요.

## 4. Visual Studio 확장 설치

같은 준비 폴더에서 `InstallAIAssistant.bat`을 실행합니다. 설치된 Visual Studio를 찾고 대상 버전에 맞는 VSIX를 선택하여 공식 VSIX 설치기를 엽니다. 설치가 끝나면 Visual Studio를 다시 시작하고 **도구 → AI Assistant**를 엽니다.

VS2017 15.7·15.8에는 `VS2017.15.7.AIAssistant-0.11.1.vsix`, VS2017 15.9 이상에는 `VS2017.AIAssistant-0.11.1.vsix`를 사용합니다. VS2019·VS2022는 각각 해당 버전의 패키지를 선택하세요. 다른 Visual Studio 창을 모두 닫아야 예약된 VSIX 설치가 완료될 수 있습니다. 포함된 설치기는 압축 묶음 안의 VSIX를 사용하며 다운로드 명령은 실행하지 않습니다.

이 묶음은 확장과 CLI 런타임만 제공합니다. 대상 PC에 해당 Visual Studio 자체나 VSIX가 요구하는 Windows·Visual Studio 구성 요소가 빠져 있으면 자동으로 추가 설치하지 않습니다. VSIX 설치기가 필수 구성 요소 부족을 보고하면 조직의 IT 담당자가 지원되는 Visual Studio 구성 요소를 먼저 설치한 뒤 다시 시도하세요.

## 5. CLI 경로 지정과 온라인 연결

설정에서 사용할 AI의 CLI 실행 파일 경로를 지정합니다. 앞 단계의 완료 화면에서 기록한 전체 경로를 사용하세요. 경로 지정은 설치된 실행 파일을 선택하는 작업이며 계정 로그인을 대신하지 않습니다.

네트워크와 Claude 또는 OpenAI 서비스 연결이 허용되는 시점에 각 CLI의 공식 로그인을 진행하고 **로그인 상태 확인**과 실제 요청을 별도로 확인합니다. CLI 설치와 Visual Studio에서 창을 여는 데 성공해도 AI 서비스 연결이 확인된 것은 아닙니다. 네트워크가 다시 차단되면 모델 응답을 받을 수 없습니다.

AI 모델 서비스 연결만 허용된 환경에서는 첫 로그인까지 완료된다고 단정할 수 없습니다. 최초 인증은 선택한 계정·조직·인증 방식에 따라 별도의 공식 로그인 화면이나 조직 인증 절차가 필요할 수 있고, 모델 요청에 쓰는 연결과 요구사항이 다를 수 있습니다. 회사 네트워크 담당자에게 사용할 공급자와 인증 방식을 알리고, 해당 공급자의 공식 인증·네트워크 문서를 기준으로 필요한 연결이 허용되는지 확인하세요. 이 안내는 고정 도메인 허용 목록이나 방화벽 변경 방법을 지정하지 않습니다.

Visual Studio 설정의 위치는 확장을 설치한 뒤 **도구 → AI Assistant → 설정**에서 찾을 수 있습니다. 계정 인증 방법은 네트워크 연결이 허용되는 시점에 제품의 공식 로그인 화면과 각 CLI의 공식 안내를 따르세요. 현재 CLI의 `InstallCLI.bat`은 인터넷에서 내려받는 온라인 설치 도구이므로 대상 PC에서 실행하지 마세요. 오프라인 설치에는 이 문서의 `InstallOfflineCLI.bat`을 사용합니다.

공급자별 인증·네트워크 요구사항은 변경될 수 있으므로 설정 시점에 공식 문서를 확인하세요: [Codex CLI 시작 및 로그인](https://developers.openai.com/codex/cli), [Claude Code 인증](https://code.claude.com/docs/en/authentication), [Claude Code 기업 네트워크 설정](https://code.claude.com/docs/en/network-config).

## 문제 해결

| 증상 | 확인할 내용 |
|---|---|
| `NoApplicableSKUsException` 또는 설치 대상 없음 | 인터넷 상태보다 Visual Studio 연도·버전과 일치하는 VSIX인지 먼저 확인합니다. 특히 VS2017 15.7·15.8은 전용 패키지를 선택합니다. |
| 압축을 풀었는데 설치 파일을 찾지 못함 | ZIP 전체를 새 폴더에 압축 해제했는지, `InstallAIAssistant.bat`과 VSIX가 같은 설치 묶음에 있는지 확인합니다. |
| CLI 복사 또는 체크섬 검사 실패 | 오류 문구와 진단 파일 경로를 기록하고, 파일을 다시 준비해 체크섬을 확인합니다. 손상 또는 불완전한 파일은 실행하지 않습니다. |
| Visual Studio는 열리지만 AI 응답이 없음 | CLI 경로, 공식 로그인 상태, 네트워크 및 해당 AI 서비스 연결 허용 여부를 각각 확인합니다. 오프라인에서는 서비스 응답을 받을 수 없습니다. |

오류가 나면 `OfflineCLI-install-*.json`, `AI-Assistant-install-*.json`, `VSIXInstaller-*.log` 등 BAT가 표시하는 실제 진단·로그 경로와 오류 문구를 보존하세요. 보고서를 공유하기 전에는 사용자 이름 등 개인 경로가 포함되어 있는지 확인하고, 계정·토큰·비밀 정보는 보내지 마세요. 설치기 사전 검사는 고객 PC에서 실제 VSIX 설치나 AI 요청을 완료했다는 뜻이 아닙니다. 이 안내의 기준 패키지는 고객의 VS2017 Professional 15.7.1에서 실제 설치·도구 창 실행을 확인하지 않았습니다.
