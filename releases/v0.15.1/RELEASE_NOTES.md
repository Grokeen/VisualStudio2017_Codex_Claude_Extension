# Visual Studio AI Assistant 0.15.1 Preview 1

이 Preview는 **‘주석으로 코드 제안 요청’ 기능을 일시 중지**합니다. 메뉴를 선택하면 편집기 내용을 수집하거나 전송하기 전에 안내하고 요청을 중단합니다. 전용 제공자 경로에도 중지 확인을 두었습니다. 편집기 문서 대상 확인을 보완하는 동안 이 기능을 사용하지 마세요. 실제 문서 불일치나 정보 유출이 발생했다는 증거는 확인되지 않았습니다.

일반 Claude·Codex 대화 경로의 소스는 이번 변경에 포함되지 않았습니다. 실제 사용자 계정 로그인과 AI 응답은 시험하지 않았습니다.

기존 `0.15.0-preview.1` 설치본은 자동으로 중지되지 않습니다. 해당 버전을 유지하는 사용자는 [기존 릴리스의 사용 보류 안내](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.15.0-preview.1)에 따라 주석 코드 요청 기능을 실행하지 마세요. 새 `0.15.1` VSIX를 설치할 때는 함께 제공하는 사용자 안내서와 오프라인 설치 절차를 따릅니다.

전체 오프라인 ZIP의 `VerifyOfflineSetup.bat`은 설치를 실행하지 않고 파일 목록·SHA-256을 검사합니다. `InstallAIAssistant.bat`은 별도의 명시 설치 경로입니다. 경량 ZIP은 CLI를 포함하지 않으므로 전체 오프라인 검증 배치를 제공하지 않으며, 경량 묶음의 `BUNDLE_SHA256SUMS.txt`와 전체 묶음의 `OFFLINE_SHA256SUMS.txt`는 각각 해당 ZIP의 내부 파일 검사용입니다. ZIP 파일 자체의 출처와 바이트를 확인하려면 신뢰할 수 있는 외부 SHA-256 값을 별도로 대조하세요.

테마 설치 경로는 전체 ZIP의 `Themes/` 위치와 준비된 오프라인 ZIP의 루트 위치를 각각 검사합니다. 기존 `0.15.0-preview.1` FullOffline ZIP의 `-InstallTheme`/`-RequireTheme` 조건부 경로 문제는 [해당 Release의 경고](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.15.0-preview.1)에 기록했습니다. 실제 설치 성공 여부는 검증하지 않았습니다.

Visual Studio 2017 15.7/15.8, 2017 15.9 이상, 2019, 2022용 VSIX 네 개와 독립 테마 VSIX 세 개를 제공합니다. FullOffline ZIP에는 Codex·Claude CLI 오프라인 파일과 manifest를 포함합니다. Release의 `RELEASE_SHA256SUMS.txt`에서 첨부파일 해시를 확인할 수 있습니다.

이번 릴리스에서도 실제 Visual Studio 설치·GUI 조작·계정 로그인·AI 응답은 검증하지 않았습니다. 읽기 전용 검사와 설치 계획은 합성 파일로 검증했고, 최종 VSIX의 매니페스트·버전·내장 PDF 및 ZIP의 파일·체크섬은 출하 과정에서 확인했습니다. 실제 설치 결과는 별도 IDE 검증 범위입니다. 이전 `0.15.0-preview.1`과 안정판은 보존합니다.
