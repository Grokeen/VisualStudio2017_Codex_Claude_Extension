# Visual Studio AI Assistant v0.11.0

Visual Studio 2017·2019·2022에서 Claude와 Codex를 사용하고, 수정안을 검토하거나 직접 파일 수정과 테스트를 맡길 수 있습니다.

## 다운로드

| 파일 | 용도 |
|---|---|
| [VS2017 설치 파일](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.11.0/VS2017.AIAssistant-0.11.0.vsix) | VS2017 15.9 이상 · x86 IDE |
| [VS2019 설치 파일](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.11.0/VS2019.AIAssistant-0.11.0.vsix) | VS2019 16.11 · x86 IDE |
| [VS2022 설치 파일](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.11.0/VS2022.AIAssistant-0.11.0.vsix) | VS2022 17.x · x64 IDE |
| [전체 설치 묶음 ZIP](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.11.0/VisualStudio_AI_Assistant-0.11.0.zip) | 세 VSIX와 기존 CLI 설치 Markdown·배치·PDF·그림 |
| [검사 실행기](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.11.0/VisualStudio_AIChat_ExtentionTest.exe) | [테스트 환경](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/blob/main/docs/TEST_ENVIRONMENT.md)을 준비하고 저장소/소스 코드 ZIP의 루트에서 사용 |
| [SHA256 체크섬](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.11.0/RELEASE_SHA256SUMS.txt) | 다섯 다운로드 파일의 무결성 확인 |

Visual Studio를 닫고 사용 중인 버전에 맞는 VSIX를 설치한 뒤 **도구 → AI Assistant**를 여세요. 공식 CLI 설치와 로그인이 필요합니다. ZIP을 풀거나 확장을 설치하는 것만으로 CLI 설치 배치가 실행되지는 않습니다.

## v0.11.0 변경 사항

- 같은 팀의 Claude와 Codex가 순서대로 검토하고 주 담당 AI가 종합합니다. 주 담당 외 최대3명이며 각 제공자의 CLI·모델·생각 강도·속도를 사용합니다.
- 사용자 메시지는 배경 없는 회색 글자로 표시하고, 답변 바탕이 배경 로고를 가리는 문제를 수정했습니다.
- 직접 실행 중 프로젝트 파일 알림을 모았다가 저장된 변경을 다시 읽습니다. 미저장 변경을 보존하고 프로젝트 재로딩 뒤 닫힌 문서 참조를 재사용하지 않습니다.
- VS2017·2019·2022의 확장 제거·비활성화와 데이터 보존 방법을 안내합니다.

## 소스·패키지 검증

이번 배포에서 소스120개가 이전 독립 감사를 마친 v0.11.0과 동일함을 다시 확인했습니다. 세 버전 Debug/Release 6개를 오류·경고 없이 재빌드하고, 배포할 VSIX로 전체16묶음과 실행기15개를 다시 검사했습니다. 최종 세 VSIX는 기존에 실제 각 IDE에서145개씩 통과한 바이트를 유지합니다. 설치 ZIP의26항목도 다시 읽어 입력 파일과 비교했습니다.

현재 버전 소스는 변경되지 않아 v0.11.0을 유지했습니다. GitHub의 자동 소스 코드 ZIP/TAR는 이 릴리스 태그의 소스입니다. [배포 재검증 기록](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/blob/main/MarkdownReport/CodexOrig_TASK-035.md)과 [기존 독립 감사](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/blob/main/MarkdownReport/CodexOrig2_TASK-034_Audit.md)를 참고하세요.

선택한 요청·소스·첨부는 협업에 참여한 AI 서비스로 전달되고 호출별 사용량이 발생합니다. 실제 Claude 계정·운영 DB·외부 TFVC·운영 배포의 검증과 격리 검사는 구분합니다. 브라우저·Claude 앱 로그인과 CLI 로그인은 별도입니다.
