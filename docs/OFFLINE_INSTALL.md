# 오프라인 설치 안내

이 안내는 **Visual Studio AI Assistant 확장과 Windows용 Codex·Claude CLI를 인터넷이 없는 PC에 설치**하는 절차입니다. AI 모델 요청과 계정 인증까지 오프라인으로 처리하지는 않습니다. 설치 후 네트워크 연결이 허용되면 본인 계정으로 공식 로그인을 하고 연결을 확인하세요.

## v0.14.2-preview.1 현재 설치·Dracula 편집기 안내

현재 안내 대상은 v0.14.2-preview.1입니다. Preview 다운로드 링크는 Release 게시가 끝나면 활성화됩니다. 직전 v0.14.1-preview.1 Release는 보존하고, Preview 게시·익명 다운로드 검증 이후에는 v0.14.0 이하의 오래된 Release 첨부를 별도 정리할 수 있습니다. 이전 v0.14.0 안내 PDF는 [저장소 보존본](../output/pdf/VisualStudio_AI_Assistant_%EC%84%A4%EC%B9%98_%EC%82%AC%EC%9A%A9%EC%84%A4%EB%AA%85%EC%84%9C_v0.14.0_%EB%AF%B8%EB%A6%AC%EB%B3%B4%EA%B8%B0.pdf)으로 유지합니다.

1. [v0.14.2-preview.1 Release](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.14.2-preview.1)에서 CLI 포함 전체 오프라인 ZIP `VisualStudio_AI_Assistant-0.14.2-Preview1-FullOffline-win-x64.zip`을 받습니다. IDE 한 개를 수동 설치할 때는 같은 릴리스의 해당 연도 VSIX를 사용합니다.
2. ZIP을 새 폴더에 완전히 압축 해제하고 manifest·체크섬을 확인합니다. 실행 중인 Visual Studio 창은 모두 닫습니다.
3. `Grokeen-AI-Theme-Offline-Installer-0.14.2.exe`를 실행하면 동봉 확장과 IDE 테마 VSIX 설치를 진행합니다. 확장만 설치할 때는 `InstallAIAssistant.bat` 또는 해당 IDE 버전 VSIX를 사용합니다. 설치는 로컬 패키지를 사용하며 인터넷에서 파일을 받지 않습니다.
4. 설치가 끝나면 같은 Visual Studio IDE를 다시 엽니다.
5. **도구 → 옵션 → 환경 → 일반 → 색 테마**에서 **Grokeen Dracula Inspired**를 선택합니다. 편집기 변경 표시를 보려면 diff 보기와 overview 영역을 확인합니다.

별도 선택 경로: 테마 VSIX 없이 AI Assistant VSIX만 수동 설치한 경우에 한해, **도구 → AI Assistant → 설정 → Dracula 테마 설치·적용**으로 IDE 테마 VSIX를 추가할 수 있습니다. 설치 관리자 완료 후 같은 IDE를 다시 열고 위 색 테마 메뉴에서 **Grokeen Dracula Inspired**를 선택하세요. 전체 오프라인 설치기 사용자는 이 추가 설치 단계를 건너뜁니다.

독립 테마 VSIX에는 작은 `Grokeen.VisualStudioThemes.dll` MEF 구성 요소 하나가 포함되어 C# block comment와 오류 span을 IDE 편집기에서 읽기 전용으로 보강합니다. Visual Studio Editor SDK DLL/PDB는 테마 패키지에 넣지 않습니다. 이는 IDE가 제공하는 SDK 서비스에 연결되며, 테마 색상 표시를 위해 CLI 설치·로그인이나 AI 서비스 연결은 필요하지 않습니다. 서비스 요청을 사용하려면 별도의 CLI 설치·로그인이 필요합니다.

오류 span은 기존 오류 squiggle을 유지하면서 분홍 1px 테두리 `#FFFF79C6`만 표시합니다. 분홍 배경 채움은 없으므로 본문 글자를 가리지 않습니다. added/removed diff 색은 줄 면과 변경 단어 전경/배경으로 나뉘며, 초록/분홍 변경 표식과 보라 viewport outline도 별도 표시입니다. 색상표와 주석 분류 설명은 [상세 사용자 안내](USER_GUIDE.md#v0142-preview1-설치dracula-편집기-색상-안내)와 [46쪽 PDF](../output/pdf/VisualStudio_AI_Assistant_%EC%84%A4%EC%B9%98_%EC%82%AC%EC%9A%A9%EC%84%A4%EB%AA%85%EC%84%9C_v0.14.2-preview.1_%EB%AF%B8%EB%A6%AC%EB%B3%B4%EA%B8%B0.pdf)를 참고하세요.

버전별 설치·기능 검증 결과와 범위는 [TASK-072 통합 검증 보고서](../MarkdownReport/CodexOrig2_TASK-072_Audit.md)에서 확인하세요. 고객 VS2017 Professional 15.7.1·별도 오프라인 PC, 실제 AI/TFS 작업과 설치 UI의 물리 포인터 흐름은 아직 확인되지 않았습니다.

## 이전 v0.14.1-preview.1 전용 확장·IDE 테마 설치기

v0.14.1-preview.1 후보는 네 SKU 빌드와 대화 색상 화면 검사를 거쳤습니다. 고객 PC의 설치·실사용, 특히 VS2017 Professional 15.7.1 및 별도 제한망 PC 환경은 확인 범위에 포함하지 않았습니다. 이 묶음의 `Grokeen-AI-Theme-Offline-Installer-0.14.1.exe`는 설치된 Visual Studio를 감지해 선택한 IDE에 동봉 확장과 IDE 테마를 설치합니다. 설치 전 실행 중인 IDE는 사용자가 직접 닫아야 합니다. 설치기는 Codex·Claude CLI 설치나 계정 로그인·AI 요청을 수행하지 않습니다.

1. 같은 v0.14.1 묶음의 ZIP을 새 폴더에 모두 압축 해제하고 manifest/체크섬으로 파일을 확인합니다.
2. 실행 중인 모든 Visual Studio 인스턴스를 직접 닫습니다. 설치기는 IDE를 강제 종료하지 않고 닫힐 때까지 기다릴 수 있습니다.
3. `Grokeen-AI-Theme-Offline-Installer-0.14.1.exe`를 실행합니다. 발견된 Visual Studio 버전과 설치 인스턴스를 검토합니다. 여러 대상이 표시되면 사용할 항목을 고릅니다.
4. 설치 전 확인을 통과하면 묶음의 확장 VSIX와 IDE 테마 VSIX를 해당 Visual Studio 설치기로 설치합니다. 네트워크에서 설치 파일을 다운로드하지 않습니다.
5. 결과 화면의 진단 JSON 및 `VSIXInstaller-*.log` 경로를 기록하고, 설치가 끝난 뒤 Visual Studio를 다시 열어 **도구 → AI Assistant**와 선택한 색 테마를 확인합니다.

설치기가 실행 중 IDE 종료를 기다리면 해당 Visual Studio 창을 직접 닫으세요. 대상 버전을 찾지 못하거나 필수 설치 파일 검사가 실패하면 표시된 진단 경로를 보관하고 동일한 릴리스의 ZIP을 다시 확인합니다. 진단 파일에는 설치 위치가 들어갈 수 있으니 공유 전 개인 경로를 검토하세요.

설치/업데이트 과정은 구형 공식 필수 지침 1–5를 최신 공식 규칙 1–8로 갱신하고 별도 사용자 지침 및 사용자 수정 사본은 보존합니다. 협업 폴더는 SCM 연결과 두 명 이상 참여 조건을 만족할 때에만 만들며, 설치기가 협업 내용을 자동으로 SCM에 commit/push하지 않습니다.

전용 설치기 사용 후에도 CLI 런타임이 필요하면 아래 CLI 설치 절차를 별도로 진행하세요. CLI가 설치되어 있어도 서비스 로그인이나 실제 AI 요청은 네트워크와 계정 권한이 허용될 때만 가능합니다.

이전 버전의 기준 묶음은 **v0.14.1-preview.1**의 [CLI 포함 전체 오프라인 ZIP](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.14.1-preview.1/VisualStudio_AI_Assistant-0.14.1-Preview1-FullOffline-win-x64.zip)입니다. 작은 ZIP과 구분해 전체 오프라인 ZIP을 받으세요. 설치 전 manifest와 체크섬을 확인하고, 이 안내와 같은 버전의 파일을 사용하세요.

이전 버전인 v0.13.0-preview.1의 사용자 전달 폴더에는 VSIX와 테마 패키지, `cli/codex/`·`cli/claude/` 런타임 및 라이선스·고지, 설치/검증 BAT·PowerShell, 체크섬·manifest, 안내 문서와 PDF만 포함했습니다. 제품 소스·프로젝트·PDB·테스트·개발/감사 증거·Git 자료·사용자 프로필·토큰·대화 기록·인터넷 준비용 도구는 제외했습니다. 이 설명은 이전 버전의 구성 기록이며 현재 설치에는 위의 v0.14.2-preview.1 묶음을 사용하세요.

직전 v0.14.1-preview.1 Release와 v0.14.0·v0.13.0의 버전 기록은 보존합니다. 오래된 Release 첨부 정리는 v0.14.2 게시와 익명 다운로드 검증 후 별도 수행합니다. 이 문서의 v0.13.0 설명은 이전 버전 기록입니다. 설치 시에는 같은 버전의 VSIX, 테마 및 체크섬을 함께 사용하세요.

> 준비 PC에는 인터넷 연결이 필요합니다. 오프라인 PC에는 확장 설치 파일과 CLI 실행 파일을 USB 등으로 옮깁니다. CLI 프로필, 로그인 정보, 토큰은 복사하지 않습니다.

## 1. 준비할 환경

- 인터넷에 연결된 준비 PC와 파일을 옮길 USB 또는 승인된 이동 매체
- 대상 PC에 설치된 지원 Visual Studio
- 인터넷 준비 PC에서 사용할 **준비 도구 ZIP**과 **완성 오프라인 설치 ZIP**을 구분하세요. 완성 ZIP은 배포된 `FullOffline-win-x64.zip` 또는 직접 준비한 `PreparedOffline-...zip`이며 VSIX, 설치 도구, Codex·Claude CLI 런타임을 포함합니다. VSIX만 든 작은 설치 ZIP에는 CLI 런타임이 없습니다.
- Windows x64: Windows 10 버전 1809 이상 또는 Windows 11. PowerShell 5.1 이상이 필요합니다. OS 조건은 동봉 CLI 런타임 기준이며 Visual Studio 패키지 호환 조건과는 별개입니다. `winver`로 Windows 버전을 확인하세요.

공개 Release에서 설치한 IDE 버전에 맞는 VSIX를 선택하세요. 압축을 옮길 때에는 같은 Release의 CLI 포함 완성 오프라인 ZIP을 사용합니다. 서로 다른 Release의 VSIX·설치 도구·CLI 런타임을 섞지 마세요.

| 설치된 IDE | 설치 파일 |
|---|---|
| VS2017 15.7·15.8 | `VS2017.15.7.AIAssistant-<버전>.vsix` |
| VS2017 15.9 이상 | `VS2017.AIAssistant-<버전>.vsix` |
| VS2019 16.11 | `VS2019.AIAssistant-<버전>.vsix` |
| VS2022 17.x x64 | `VS2022.AIAssistant-<버전>.vsix` |

설치 대상과 맞는 VSIX를 사용하세요. VS2017 15.7 전용 패키지는 .NET Framework 4.6.2 이상 등 해당 Visual Studio 구성 요소가 필요합니다.


배포된 완성 ZIP을 받았다면 대상 PC에서 압축을 모두 푼 뒤 `VerifyOfflineSetup.bat`을 실행하세요. 별도로 CLI를 내려받아 묶음을 다시 준비할 필요가 없습니다. 번들에는 IDE 전체 색 테마용 독립 VSIX도 포함됩니다. 완성 묶음을 직접 준비하는 경우에는 동일 버전의 준비 도구 ZIP을 사용하고, 준비 완료 후 `offline-manifest.json`과 VSIX 체크섬 파일에서 모든 파일이 같은 패키지 버전인지 확인하세요. `PrepareOfflineSetup.bat`은 `PreparedOffline-날짜-시간.zip`을 생성합니다.
## 2. 인터넷 PC에서 오프라인 묶음 준비

1. 인터넷에 연결된 준비 PC에서 준비 도구 ZIP을 새 폴더에 모두 압축 해제합니다. 이 준비 도구 ZIP은 완성 설치 ZIP을 만드는 용도이며, 오프라인 대상 PC에 전달할 파일은 아래 절차로 생성되는 완성 설치 ZIP입니다.
2. 인터넷이 연결된 준비 PC에 공식 Windows x64 Codex와 Claude CLI를 설치합니다. 기본적으로 Codex 전체 패키지는 `%USERPROFILE%\.codex\packages\standalone\current`, Claude 실행 파일은 `%USERPROFILE%\.local\bin\claude.exe`에서 찾습니다. 다른 위치라면 `Prepare-OfflineSetup.ps1`의 `-CodexPackageDirectory`와 `-ClaudeExecutable` 매개 변수로 지정합니다. Codex는 `bin` 폴더만이 아니라 standalone 패키지 전체가 필요합니다. CLI를 다시 설치하거나 계정 로그인까지 할 필요는 없습니다.
3. 저장소의 `PrepareOfflineSetup.bat`을 실행합니다. 이 배치는 `Prepare-OfflineSetup.ps1 -CreateZip`을 시작해 CLI 게시자 서명과 파일 구성을 확인한 뒤 VSIX, 설치 도구, CLI 런타임을 준비합니다. 기본으로 현재 폴더에 `PreparedOffline-날짜-시간` 폴더와 같은 이름의 `.zip` 파일, `.zip.sha256` 체크섬 파일을 만듭니다.
4. 완료 화면의 **완성 설치 ZIP** 경로와 CLI 버전·파일 검사 결과를 확인합니다. 이동 전 PowerShell에서 `Get-ChildItem .\PreparedOffline-*.zip | Get-FileHash -Algorithm SHA256`을 실행하고, 출력한 `Hash`를 `.zip.sha256` 파일의 값과 비교합니다. 완성 ZIP과 같은 경로의 `.zip.sha256` 파일을 함께 USB에 복사합니다. 또는 준비 폴더 전체와 `OFFLINE_SHA256SUMS.txt`를 함께 복사할 수 있습니다. 준비 도구 ZIP만 대상 PC에 복사해서는 CLI 런타임 설치를 완료할 수 없습니다.

준비 단계는 인터넷 PC에서 공식 게시자 서명을 확인합니다. Codex standalone 패키지의 실행 파일·보조 도구·리소스·라이선스/버전 metadata와 Claude CLI 실행 파일을 묶지만, `%USERPROFILE%` 아래의 Codex·Claude 설정, 로그인 세션, 계정, 토큰, 대화 기록은 포함하지 않습니다. 준비 중 CLI를 실행하거나 로그인하지 않습니다.

## 3. 오프라인 PC에 CLI 설치

1. USB에서 `PreparedOffline-...zip` 전체를 대상 PC의 새 폴더로 복사한 뒤 압축을 모두 풉니다. 압축 파일 안에서 BAT를 직접 실행하지 마세요.
2. `InstallOfflineCLI.bat`을 실행합니다. 직접 PowerShell 스크립트를 실행하면 기본은 검증만 하며, 파일을 복사하려면 `-Install`을 지정해야 합니다.
3. 오프라인 설치기는 manifest의 파일 수·크기·SHA-256과 실행 파일의 x64 구조를 확인합니다. 이 단계에서 온라인 서명 조회나 CLI 실행은 하지 않습니다. 검사를 마치면 새 사용자별 폴더에 복사합니다. 기본 설치 위치는 `%LOCALAPPDATA%\Programs\VisualStudioAIAssistant\OfflineCLI\codex-<Codex버전>_claude-<Claude버전>_win-x64`입니다. 완료 화면에 표시된 `codex.exe`와 `claude.exe`의 전체 경로를 기록합니다. 실행 파일은 각각 `cli\codex\bin\codex.exe`와 `cli\claude\claude.exe`에 있습니다.

CLI 설치기는 묶음을 검증할 때 CLI를 실행하거나 네트워크에 접속하지 않습니다. 같은 묶음을 다시 실행하면 설치 파일 무결성을 재확인합니다. 기존 CLI 설치 파일을 덮어쓰거나 PATH·기존 설정·계정 정보를 자동으로 변경하지 않습니다. `InstallCLI.bat`은 온라인 다운로드용이므로 오프라인 PC에서는 실행하지 마세요. CLI 설치 결과 JSON은 기본적으로 `%TEMP%\OfflineCLI-install-<GUID>.json`에 생성되며, 콘솔에 표시되는 로그 경로를 보관하세요. VS 설정에서 CLI 경로를 지정할 때 기존 CLI와 새 전용 폴더 중 사용할 경로를 직접 선택하세요.

## 4. Visual Studio 확장 설치

같은 준비 폴더에서 `InstallAIAssistant.bat`을 실행합니다. 설치된 Visual Studio를 찾고 대상 버전에 맞는 VSIX를 선택하여 공식 VSIX 설치기를 엽니다. 설치가 끝나면 Visual Studio를 다시 시작하고 **도구 → AI Assistant**를 엽니다.

VS2017 15.7·15.8에는 `VS2017.15.7.AIAssistant-<버전>.vsix`, VS2017 15.9 이상에는 `VS2017.AIAssistant-<버전>.vsix`를 사용합니다. VS2019·VS2022는 각각 해당 버전의 VSIX를 선택하세요. 다른 Visual Studio 창을 모두 닫아야 예약된 VSIX 설치가 완료될 수 있습니다. 포함된 설치기는 압축 묶음 안의 VSIX를 사용하며 다운로드 명령은 실행하지 않습니다.

이 묶음은 Visual Studio 확장, CLI 런타임, IDE 테마를 제공합니다. 대상 PC에 해당 Visual Studio 자체나 VSIX가 요구하는 Windows·Visual Studio 구성 요소가 빠져 있으면 자동으로 추가 설치하지 않습니다. VSIX 설치기가 필수 구성 요소 부족을 보고하면 조직의 IT 담당자가 지원되는 Visual Studio 구성 요소를 먼저 설치한 뒤 다시 시도하세요.

## 5. CLI 경로 지정과 온라인 연결

설정에서 사용할 AI의 CLI 실행 파일 경로를 지정합니다. 앞 단계의 완료 화면에서 기록한 전체 경로를 사용하세요. 경로 지정은 설치된 실행 파일을 선택하는 작업이며 계정 로그인을 대신하지 않습니다.

네트워크와 Claude 또는 OpenAI 서비스 연결이 허용되는 시점에 각 CLI의 공식 로그인을 진행하고 **로그인 상태 확인**과 실제 요청을 별도로 확인합니다. CLI 설치와 Visual Studio에서 창을 여는 데 성공해도 AI 서비스 연결이 확인된 것은 아닙니다. 네트워크가 다시 차단되면 모델 응답을 받을 수 없습니다.

AI 모델 서비스 연결만 허용된 환경에서는 첫 로그인까지 완료된다고 단정할 수 없습니다. 최초 인증은 선택한 계정·조직·인증 방식에 따라 별도의 공식 로그인 화면이나 조직 인증 절차가 필요할 수 있고, 모델 요청에 쓰는 연결과 요구사항이 다를 수 있습니다. 회사 네트워크 담당자에게 사용할 공급자와 인증 방식을 알리고, 해당 공급자의 공식 인증·네트워크 문서를 기준으로 필요한 연결이 허용되는지 확인하세요. 이 안내는 고정 도메인 허용 목록이나 방화벽 변경 방법을 지정하지 않습니다.

Visual Studio 설정의 위치는 확장을 설치한 뒤 **도구 → AI Assistant → 설정**에서 찾을 수 있습니다. 계정 인증 방법은 네트워크 연결이 허용되는 시점에 제품의 공식 로그인 화면과 각 CLI의 공식 안내를 따르세요. 현재 CLI의 `InstallCLI.bat`은 인터넷에서 내려받는 온라인 설치 도구이므로 대상 PC에서 실행하지 마세요. 오프라인 설치에는 이 문서의 `InstallOfflineCLI.bat`을 사용합니다.

### 수동 AI-VSIX 설치 시 Visual Studio 테마 추가

AI Assistant VSIX만 수동으로 설치했고 IDE 테마 패키지가 없는 경우에만 이 선택 경로를 사용합니다. 전체 오프라인 설치기의 기본 절차는 위의 EXE 설치 후 같은 IDE를 다시 열고 **도구 → 옵션 → 환경 → 일반 → 색 테마**에서 **Grokeen Dracula Inspired**를 선택하는 것입니다.

1. **도구 → AI Assistant → 설정**에서 **Kakao 테마 설치·적용** 또는 **Dracula 테마 설치·적용**을 선택합니다. 이 버튼은 확장 내부 대화창 팔레트와 별개로 IDE 전체 테마 VSIX를 추가합니다.
2. 해당 Visual Studio 버전의 공식 VSIX 설치 관리자가 열리면 안내에 따라 설치를 완료합니다. 설치 관리자에서 완료를 확인하고, 실행 중인 Visual Studio 창을 모두 닫습니다.
3. 같은 Visual Studio 설치 인스턴스를 다시 시작합니다. 확장에 저장된 선택을 읽어 시작 시 IDE 테마 적용을 시도합니다.
4. **도구 → 옵션 → 환경 → 일반 → 색 테마**에서 선택한 테마가 활성화됐는지 확인합니다. 적용되지 않았다면 이 메뉴에서 수동 선택을 시도하고, 선택 이름과 IDE 버전을 기록해 지원 요청에 전달하세요.

실제 Visual Studio 2017 15.9, 2019 16.11, 2022 17.14 통합 시험에서 Kakao·Dracula 테마 선택 ID와 대표 환경 색상(Kakao `#FFD9DDE3`, Dracula `#FF282A36`), 보류 상태 처리 및 임시 파일 정리를 확인했습니다. 설치 버튼의 물리 클릭, 설치 관리자 완료, IDE 재시작을 포함한 고객 전체 사용 흐름은 별도 확인 범위입니다. VS2017 Professional 15.7.1 및 고객 제한망 PC의 실제 설치·적용도 아직 실측하지 않았습니다.

공급자별 인증·네트워크 요구사항은 변경될 수 있으므로 설정 시점에 공식 문서를 확인하세요: [Codex CLI 시작 및 로그인](https://developers.openai.com/codex/cli), [Claude Code 인증](https://code.claude.com/docs/en/authentication), [Claude Code 기업 네트워크 설정](https://code.claude.com/docs/en/network-config).

## 6. 대상 PC에서 실제 설치 검증

완성 오프라인 ZIP에 `VerifyOfflineSetup.bat`이 포함되어 있으면 대상 PC에서 압축을 푼 뒤 실행해 실제 설치를 확인할 수 있습니다. 이 도구는 설치 전에 해당 PC가 오프라인 대상인지 사용자에게 확인하고, 열린 Visual Studio 창이 있으면 종료를 요구합니다. 설치 가능한 Visual Studio가 여러 개면 목록에서 하나를 선택합니다. 이후 로컬 사전 검사를 하고 CLI를 설치한 다음 임시 프로필에서 두 CLI의 `--version`만 확인하고 공식 VSIX 설치기를 엽니다. 로그인과 AI 모델 요청은 수행하지 않습니다.

도구가 묻는 네트워크 차단 여부는 사용자의 확인값입니다. 도구가 방화벽을 바꾸거나 네트워크 차단을 증명하지는 않습니다. `VerifyOfflineSetup.bat`은 실제 설치 모드로 실행하며, `Test-OfflineSetupOnTarget.ps1`을 직접 실행하면 기본 동작은 설치 없는 사전 검사이고 결과 상태는 `ReadyDiagnosticOnly`입니다.

검사 결과는 `%TEMP%\OfflineSetupVerify-<GUID>\RESULT.json` 및 같은 폴더의 CLI·VSIX 로그에 저장됩니다. 콘솔에 출력된 정확한 결과 폴더 경로를 기록하세요. `InstalledPendingIdeCheck`는 설치 파일 확인까지 끝났다는 뜻이며 완료 판정은 아닙니다. Visual Studio를 직접 열어 **도구 → AI Assistant** 창이 표시되는지 확인하세요. `InstallerSucceededPendingInstalledFileCheck`이면 설치기가 성공을 반환했지만 설치 파일 확인은 끝나지 않은 상태이므로 결과 폴더의 로그와 파일을 확인해야 합니다. `Failed`이면 `RESULT.json`의 오류 문구와 관련 로그를 확인합니다.

지원 요청에는 `RESULT.json`의 상태, 오류 문구(오류가 있을 때), Visual Studio에서 AI Assistant 창이 열렸는지 여부만 전달하세요. 전체 결과 파일에는 사용자 경로가 포함될 수 있으므로 공유 전 확인하고, 계정·암호·토큰은 보내지 마세요.

## 문제 해결

| 증상 | 확인할 내용 |
|---|---|
| `NoApplicableSKUsException` 또는 설치 대상 없음 | 인터넷 상태보다 Visual Studio 연도·버전과 일치하는 VSIX인지 먼저 확인합니다. 특히 VS2017 15.7·15.8은 전용 패키지를 선택합니다. |
| 압축을 풀었는데 설치 파일을 찾지 못함 | ZIP 전체를 새 폴더에 압축 해제했는지, `InstallAIAssistant.bat`과 VSIX가 같은 설치 묶음에 있는지 확인합니다. |
| CLI 복사 또는 체크섬 검사 실패 | 오류 문구와 진단 파일 경로를 기록하고, 파일을 다시 준비해 체크섬을 확인합니다. 손상 또는 불완전한 파일은 실행하지 않습니다. |
| Visual Studio는 열리지만 AI 응답이 없음 | CLI 경로, 공식 로그인 상태, 네트워크 및 해당 AI 서비스 연결 허용 여부를 각각 확인합니다. 오프라인에서는 서비스 응답을 받을 수 없습니다. |

오류가 나면 `OfflineCLI-install-*.json`, `AI-Assistant-install-*.json`, `VSIXInstaller-*.log` 등 BAT가 표시하는 실제 진단·로그 경로와 오류 문구를 보존하세요. 보고서를 공유하기 전에는 사용자 이름 등 개인 경로가 포함되어 있는지 확인하고, 계정·토큰·비밀 정보는 보내지 마세요. 설치기 사전 검사는 고객 PC에서 실제 VSIX 설치나 AI 요청을 완료했다는 뜻이 아닙니다. 이 안내의 기준 패키지는 고객의 VS2017 Professional 15.7.1에서 실제 설치·도구 창 실행을 확인하지 않았습니다.

네트워크 격리 시험 기록은 제한된 Windows AppContainer에서 외부 TCP 접속이 거부되는지 확인한 결과입니다. 이는 물리적으로 네트워크 케이블을 분리한 시험이나 고객 PC에서의 실제 Visual Studio/VSIX 설치 검증을 뜻하지 않습니다. 실제 고객 환경의 설치 결과와 네트워크 정책은 해당 PC에서 별도로 확인해야 합니다.
