# VS2017 AI Assistant

Visual Studio 2017 내부에서 Codex와 Claude Code를 사용하기 위한 독립 VSIX 확장 프로젝트입니다.

v0.2.0에는 **현재 파일의 AI 수정안 생성 → 비교 → 사용자 승인 → 편집기 적용 → 별도 저장·되돌리기**를 구현했습니다. Codex와 Claude Code의 Windows 네이티브 CLI를 선택할 수 있습니다. VS2017 실제 도구 창에서 Codex 수정안 생성·승인·적용·저장을 검증했습니다. Claude는 CLI 테스트 응답으로 검증했으며 실제 계정 호출은 남아 있습니다.

| 현재 대상 | 구성 |
|---|---|
| Visual Studio | 2017 15.9 이상, Community/Professional/Enterprise |
| 개발 언어 | C# 7.0 / WPF |
| 프레임워크 | .NET Framework 4.6.2 |
| 메뉴 | 도구 → AI Assistant |

AI CLI를 설치하고 터미널에서 로그인한 후 소스 파일을 엽니다. AI Assistant 창에서 Provider와 `.exe` 경로를 선택하고 수정 요청을 입력합니다. 수정안을 비교한 뒤 승인 체크와 적용 버튼으로 버퍼를 변경하고, 별도 저장 버튼 또는 Ctrl+S로 저장합니다. CLI 실행 파일 경로는 창 수명 동안 Provider별로 유지합니다.

현재 지원 범위는 이미 존재하는 한 텍스트 파일입니다. 미저장 버퍼를 요청에 포함하며, 요청 이후 편집 내용·디스크 파일이 바뀌면 적용을 거부합니다. 여러 파일 수정·파일 생성/삭제·무승인 자동 적용은 후속 범위입니다.

VS2019/2022 지원은 향후 단계에서 추가합니다.

VS2017의 확장 개발 워크로드를 설치한 후 `VS2017.AIAssistant.sln`을 열어 NuGet 복원 및 빌드를 수행합니다. F5는 Experimental Instance를 시작합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Release VSIX 경로는 `src/VS2017.AIAssistant/bin/Release/VS2017.AIAssistant.vsix`입니다.

직접 빌드하지 않고 테스트할 때는 [검증된 v0.2.0 패키지와 설치 안내](releases/v0.2.0/README.md)를 사용하세요.

- [개발·빌드·설치 테스트 절차](docs/DEVELOPMENT.md)
- [소스 수정 사용법과 검증 범위](docs/SOURCE_EDITING.md)
- [실제 VS2017 통합 테스트와 25개 테스트 케이스](docs/IDE_TESTS.md)
- [협업 상태](MarkdownReport/WORK_STATUS.md)
- [작업 이력](MarkdownReport/Update_History.md)
- [개발 및 테스트 보고](MarkdownReport/OWERORDER.md)
- [CodexCode Phase 1 상세 결과와 미검증 항목](MarkdownReport/CodexCode_TASK-001.md)
- [CodexCode v0.2.0 소스 수정 구현 보고](MarkdownReport/CodexCode_TASK-004.md)
