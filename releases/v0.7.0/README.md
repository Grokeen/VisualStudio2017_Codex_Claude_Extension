# v0.7.0 — 문서를 참고하고, AI마다 역할을 나눠 검토하세요

PDF·Word·PowerPoint·Excel 문서를 첨부해 본문을 참고시키고, 지원 모델의 생각 강도·Fast를 선택할 수 있습니다. 대화마다 AI 이름·역할·팀을 지정하고 같은 팀의 다른 대화 AI를 최대 3개 골라 순차 검토한 뒤 현재 AI가 최종 수정안을 만듭니다. 최종 코드는 비교·승인 후 적용하고 별도로 저장합니다.

[VSIX 다운로드](VS2017.AIAssistant-0.7.0.vsix?raw=true) · [상세 사용법](../../docs/USER_GUIDE.md) · [화면](../../docs/assets/chat-0.7-preview.png) · [검증 보고](../../MarkdownReport/CodexOrig_TASK-016.md)

1. VS2017 15.9 이상을 종료한 뒤 VSIX를 설치합니다.
2. 도구 → AI Assistant에서 Claude 또는 Codex와 네이티브 CLI 계정을 연결합니다.
3. ＋ 또는 파일 복사 후 Ctrl+V로 문서를 첨부합니다. 본문 추출이 끝나면 요청을 보냅니다.
4. 모델과 ☷ 생각 강도를 고릅니다. ϟ Fast는 지원 모델에서만 사용하며 사용량이 증가합니다.
5. ♙ AI 이름에서 역할·팀·참여자를 저장합니다. 선택한 동료와 현재 AI 각각에 실제 호출과 사용량이 발생합니다. 협업 기록에서 실제 요청·회신·전달을 확인합니다.
6. 기존 대화목록 폭 조절·숨김·복원과 대화별 상태·읽지 않은 응답 표시도 사용할 수 있습니다.

DOCX/PPTX/XLSX·OpenDocument·RTF·HTML은 기본 본문 추출을 제공합니다. PDF·DOC/PPT/XLS는 PC에 해당 Windows IFilter가 필요합니다. 원본 16MB·본문 UTF-8 100KB, 첨부 최대 4개입니다. 스캔 PDF는 OCR 후 텍스트가 필요합니다. 원본 그림·서식은 본문 추출에 포함되지 않습니다.

권한 프리셋은 확장의 DB·형상관리·배포 허용 범위이며 실제 실행 계획과 개별 동의는 유지됩니다. 협업·Fast가 코드 적용을 자동 승인하지 않습니다.

저장소 전체의 최상위 VisualStudio_AIChat_ExtentionTest.exe에서 기본13개 검사 묶음을 실행할 수 있습니다. 실제 IDE·AI 계정 검사는 직접 선택합니다. 현재 패키지는 VS2017용입니다. VS2019/2022 패키지, 실제 Claude 계정·Fast 과금 호출, 외부 SVN/TFVC·운영 DB/배포는 완료 범위에 포함되지 않습니다. 이전 v0.2~v0.6 패키지는 보존합니다.

패키지 SHA-256은 [SHA256SUMS.txt](SHA256SUMS.txt)에 있습니다. 문서 읽기 도우미 exe/config는 VSIX에 포함되어 함께 설치됩니다.
