# Visual Studio AI Assistant 0.12.0 미리보기

추가 UI 요구를 반영한 **v0.12.0-preview.1** 공개 시험 버전입니다. 기존 정식 v0.11.1과 오프라인 시험 배포 파일을 보존합니다.

인터넷이 없는 PC로 옮길 때는 **VisualStudio_AI_Assistant-0.12.0-Preview1-FullOffline-win-x64.zip** 전체를 내려받으세요. 확장 네 종류, Codex 0.163.0·Claude 2.1.302 런타임, 설치 검사 도구와 설명서가 포함됩니다. 압축을 모두 풀고 Visual Studio를 닫은 뒤 **VerifyOfflineSetup.bat**를 실행해 대상 IDE를 선택합니다. AI 로그인과 사용에는 허용된 네트워크 연결이 필요합니다. 계정·암호·토큰·개인 대화는 묶음에 포함하지 않았습니다.

**VisualStudio_AI_Assistant-0.12.0.zip**은 CLI 런타임이 없는 작은 설치 묶음입니다. 이미 CLI를 설치한 PC 또는 인터넷 준비 PC에서 사용합니다.

| 설치된 Visual Studio | 확장 파일 |
|---|---|
| 2017 15.7·15.8 | VS2017.15.7.AIAssistant-0.12.0.vsix |
| 2017 15.9 이상 | VS2017.AIAssistant-0.12.0.vsix |
| 2019 16.x | VS2019.AIAssistant-0.12.0.vsix |
| 2022 17.x x64 | VS2022.AIAssistant-0.12.0.vsix |

Community·Professional·Enterprise를 지원합니다. VS2017 두 파일은 같은 확장 ID이므로 해당 IDE 버전에 맞는 하나만 설치합니다. Visual Studio와 필요한 Windows/.NET 구성 요소는 미리 설치되어 있어야 합니다. 대상 CLI는 Windows 10 1809 이상 또는 Windows 11 x64용입니다.

Kakao Inspired·Dracula Inspired IDE 테마는 선택 사항입니다. **Grokeen-VisualStudio-Themes-0.12.0-VS2017/2019/2022.vsix** 중 IDE 연도에 맞는 파일 하나를 설치하면 두 테마가 추가됩니다. AI 확장을 설치하지 않아도 사용할 수 있습니다. Visual Studio를 다시 연 뒤 **도구 → 옵션 → 환경 → 일반 → 색 테마**에서 선택합니다. 확장 내부의 앱 팔레트 선택과 IDE 전체 테마 설치는 별개입니다. 설치 자체가 현재 선택 테마를 바꾸지는 않습니다.

설치 후 **도구 → AI Assistant**를 엽니다. 설정은 왼쪽 페이지 탐색을 사용하고, Markdown 작성기는 여섯 도구 그룹을 제공합니다. 사용자 안내 PDF에는 새 제목 표시줄·설정·Kakao/Dracula·Markdown 화면을 포함한 23쪽 설명이 있습니다.

작업 완료 알림은 Windows 작업 표시줄 NotifyIcon 방식입니다. OS 알림 정책에 따라 표시가 달라질 수 있습니다. 대화는 최근 20개부터 표시하고 위로 이동하면 이전 20개를 추가합니다. 기존 저장 상한 100개·4MB는 유지하며 무제한 기록 복구 기능을 뜻하지 않습니다.

검증: 네 대상 Release 빌드 경고·오류 0건, 기본 16개 검사 묶음 통과, VS2017·2019·2022 및 Legacy 패키지의 전용 IDE 검증 통과, 독립 감사 지적 두 건 보완. 독립 테마는 세 IDE 시험 프로필에 테마만 설치하고 구성 반영을 확인했습니다. 테마의 모든 화면/색상 전환, 실제 Claude 계정·운영 DB/형상관리 및 고객 오프라인 PC 결과는 별도 확인 범위입니다.

무결성은 SHA256SUMS.txt와 각 ZIP의 SHA256 파일로 확인합니다. 오류가 나면 VerifyOfflineSetup이 생성한 RESULT.json의 상태와 오류 문구를 알려주세요. 암호나 토큰은 보내지 마세요.
