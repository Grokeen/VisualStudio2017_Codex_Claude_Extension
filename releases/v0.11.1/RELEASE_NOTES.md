# v0.11.1 — VS2017 15.7 전용 패키지와 설치 대상 자동 선택

Visual Studio를 업데이트할 수 없는 VS2017 15.7.1 환경을 위해 당시 SDK로 컴파일한 전용 패키지를 추가했습니다.

- **VS2017.15.7.AIAssistant-0.11.1.vsix**: VS2017 15.7·15.8용. VS2017을 업데이트하지 않고 설치합니다.
- **VS2017.AIAssistant-0.11.1.vsix**: VS2017 15.9 이상용.
- **VS2019.AIAssistant-0.11.1.vsix**: VS2019용.
- **VS2022.AIAssistant-0.11.1.vsix**: VS2022 x64용.

설치 묶음 ZIP을 모두 풀고 **InstallAIAssistant.bat**를 실행하세요. IDE가 여러 개면 설치 대상을 선택합니다. 버전·아키텍처·핵심 편집기·파일 무결성을 검사하고 해당 IDE의 공식 설치기를 엽니다. PowerShell 스크립트의 기본 호출은 진단만 수행합니다. 구형 vswhere와 한글 경로를 지원하며 Visual Studio 자체를 업데이트하지 않습니다. Community·Professional·Enterprise에서 사용할 수 있습니다. VS2017 두 패키지를 한 IDE에 함께 설치하지 마세요.

구버전 SDK 빌드의 필수 AvalonEdit/WebView2 DLL 동봉을 보완했습니다. 네 대상 Debug/Release 8빌드 오류·경고0, 설치 진단35, 구 SDK WPF31, 실행기15를 통과했습니다. 네 패키지를 실제 설치해 각각145개 IDE 검사를 통과했으며, **15.7용의 실제 IDE 검사는 개발 PC의 VS2017 Community 15.9에서 수행했습니다. 고객의 15.7.1 Professional PC 설치·실행 성공은 아직 확인하지 않았습니다.** 기본16묶음은 전체15성공 뒤 검사 도구의 구버전 매니페스트 선택을 수정하고 foundation45 재실행 성공으로 마감했습니다.

독립 감사와 상세 근거: [TASK-036 검증 보고](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/blob/main/MarkdownReport/CodexOrig_TASK-036.md). 설치 오류가 계속되면 [진단 안내](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/blob/main/docs/INSTALL_TROUBLESHOOTING.md)를 확인하세요.

기존 v0.11.0 자산은 보존합니다. ZIP31항목과 다운로드 파일의 SHA256 목록을 제공합니다. Codex·Claude CLI 설치 및 계정 로그인은 별도입니다. VS2017 15.7 미만과 VS2022 ARM64는 이번 배포 대상에 포함되지 않습니다.
