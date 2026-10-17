# v0.3.0 — Visual Studio 안에서 이어가는 AI 대화

**소스를 열고, 참고 이미지·파일을 붙이고, Codex 또는 Claude에게 요청하세요.** Chat 탭에서 대화를 이어 가며 수정안을 비교하고, 승인한 내용만 편집기에 적용할 수 있습니다.

[VSIX 다운로드](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/raw/refs/heads/main/releases/v0.3.0/VS2017.AIAssistant-0.3.0.vsix) · [전체 사용 안내](../../docs/USER_GUIDE.md) · [프로젝트 소개](../../README.md)

## 설치하고 시작하기

1. **Visual Studio 2017 15.9 이상**과 사용할 AI의 Windows 네이티브 CLI `.exe`를 준비합니다. VS2019/2022용 패키지는 아직 제공하지 않습니다.
2. Visual Studio를 닫고 다운로드한 `VS2017.AIAssistant-0.3.0.vsix`를 실행해 VS2017에 설치합니다.
3. Visual Studio를 다시 열어 **도구 → AI Assistant**에서 Chat 창을 엽니다.
4. Codex 또는 Claude Code 탭을 선택하고 **설정 · 계정 · instruction**을 펼칩니다. CLI 경로를 선택한 뒤 **계정 연결**에서 공식 CLI/브라우저로 로그인하고 **연결 확인**을 누릅니다. 이미 CLI에 로그인했다면 바로 연결 확인을 사용합니다.
5. 소스 파일을 열고 프롬프트를 입력합니다. 필요하면 파일을 Chat으로 드롭하거나 **＋ 첨부**, **붙여넣기**로 참고 자료를 추가합니다.
6. **보내기** 후 응답과 변경 비교를 읽습니다. 소스 변경은 승인 체크 → **승인 후 적용** → **적용 내용 저장** 순서로 진행합니다. 후속 요청은 같은 대화에서 계속 보낼 수 있습니다.

instruction·AI별 경로/모델·글자 크기는 **설정 저장**으로 다음 실행에도 유지합니다. Chat은 문서 탭으로 시작하고 도킹·분리할 수 있습니다. **새 대화**는 현재 AI 탭의 대화와 첨부를 지웁니다.

## 확인된 범위

- VS2017 표준 Debug/Release 빌드: 경고·오류 0건.
- 편집·Chat 71/71, 패키지·화면 33/33, 호환성 44/44 검사 통과.
- 이 고정 VSIX의 실제 VS2017 통합 검사 35/35 통과: Chat 탭·도킹·분리·설정 복원 및 실제 Codex 수정안 생성·승인·적용·저장.
- 실제 Codex에 합성 이미지·텍스트·이전 대화·instruction을 함께 전달하는 독립 검사도 통과했습니다.

Claude는 테스트 CLI로 텍스트/이미지 전달과 수정안 처리를 검사했으며 **실제 Claude 계정 호출은 미검증**입니다. 첨부는 최대4개, PNG/JPEG는 각2MB·1600만 픽셀, 텍스트는 각100KB입니다. 현재 코드 수정 대상은 이미 존재하는 텍스트 파일 한 개입니다. 대화·첨부는 창 수명 동안 유지하며 디스크 대화 복원은 제공하지 않습니다.

파일 SHA-256:

```text
A7F1C597BA0CCF7B18AC52DEB29BACE2D6B66E300B9A90C6A55EB5A3E7A88DF2
```

[해시 파일](SHA256SUMS.txt) · [제품 작업 보고](../../MarkdownReport/CodexOrig_TASK-008.md) · [독립 문서·검증 보고](../../MarkdownReport/CodexCode_TASK-009.md)
