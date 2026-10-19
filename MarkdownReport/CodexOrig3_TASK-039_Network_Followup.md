# TASK-039 후속 — 네트워크 허용·최초 인증 안내

완료 시각: 2026-10-19 11:27:57 KST

Role.md의 추가 요청 중 “AI 관련 서버만 열어준다고 함. 나머지는 사용 불가”와 관련하여 사용자 설치 문서만 보완했다. `docs/OFFLINE_INSTALL.md`는 이제 모델/API 연결 허용이 최초 로그인 완료를 보장하지 않는다고 설명하고, 조직 네트워크 담당자가 선택한 공급자·인증 방식의 공식 요구사항으로 연결을 확인하도록 안내한다. 고정 도메인 목록, 방화벽 변경/우회 지침은 추가하지 않았다. Codex CLI 공식 시작·로그인 문서와 Claude Code 공식 인증·기업 네트워크 문서를 연결했다.

확장 설치 범위도 명확히 했다. FullOffline 묶음은 VSIX와 CLI 런타임만 제공하며, 누락된 Visual Studio 또는 VSIX가 요구하는 Windows/Visual Studio 구성 요소는 설치하지 않는다. 요구 사항이 누락되면 IT 담당자가 지원 구성 요소를 준비해야 한다. 오프라인 대상 PC에서는 인터넷 다운로드형 `InstallCLI.bat` 대신 `InstallOfflineCLI.bat`을 사용한다고 재강조했다.

검증: `git diff --check -- docs/OFFLINE_INSTALL.md` 통과. 문서 SHA-256 `47BABB457DCB087F4BCBCC12EB90FF6E7C48736D8230DF831E47D1A06C10B8A7`. 코드·설치 스크립트·제품 UI·PDF/ZIP·릴리스는 변경하지 않았다. 원본 TASK-039 보고서를 덮어쓰지 않고 후속 보고서로 분리했다. 문서 소유권을 CodexOrig에 반납하며 C4 최종 패키지 생성 시 현재 문서 원문을 포함해야 한다.

공식 참고: [Codex CLI 시작 및 로그인](https://developers.openai.com/codex/cli), [Claude Code 인증](https://code.claude.com/docs/en/authentication), [Claude Code 기업 네트워크 설정](https://code.claude.com/docs/en/network-config).