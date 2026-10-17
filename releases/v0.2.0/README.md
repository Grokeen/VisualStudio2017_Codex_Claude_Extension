# v0.2.0 VS2017 검증 패키지

[VS2017.AIAssistant-0.2.0.vsix](VS2017.AIAssistant-0.2.0.vsix)는 실제 IDE 통합 테스트에 사용한 고정 패키지다.

- 설치 대상: VS2017 15.9 이상. 실제 검증: Community 15.9.83.
- 기능: 현재 파일 수정안 생성 → 비교 → 명시적 승인 → 편집기 적용 → 별도 저장·Undo/복구.
- 검증: 기존 회귀 128개 + 실제 VS2017/Codex 통합 25개 PASS. 새 인스턴스 fixture 재현 22개 PASS.
- 실제 Codex: 합성 소스로 생성·승인·적용·저장 확인.
- Claude: CLI fixture의 실제 IDE 적용까지 확인. 실제 Claude 계정 호출은 미검증.
- VS2019/2022 패키지 지원, 다중 파일·무승인 자동 적용은 포함하지 않는다. 일부 IDE 특수 문서·도킹/테마/재시작/제거 시나리오는 추가 검증이 필요하다.

VS2017을 닫고 VSIX를 실행해 설치한 뒤 도구 → AI Assistant를 연다. Codex/Claude 네이티브 CLI 설치·터미널 로그인이 별도로 필요하다. 현재 패키지는 개발 검증 후보이며 Marketplace에는 등록하지 않았다.

SHA-256: `0B7FD44509A6CA02344E645924378EB544966A0831B942A9F7D4B6CDAB872D94`. [SHA256SUMS.txt](SHA256SUMS.txt)와 다운로드 파일의 해시를 비교할 수 있다.

```powershell
Get-FileHash .\VS2017.AIAssistant-0.2.0.vsix -Algorithm SHA256
```

상세 사용법: [SOURCE_EDITING.md](../../docs/SOURCE_EDITING.md). 테스트 케이스와 재현: [IDE_TESTS.md](../../docs/IDE_TESTS.md). 결과: [TASK-005](../../MarkdownReport/CodexCode_TASK-005.md).
