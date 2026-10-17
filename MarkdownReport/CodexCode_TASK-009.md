# TASK-009 — 사용자 README 개편 및 v0.3.0 독립 검토

- 작업자: CodexCode
- 시작: 2026-10-18 02:31:42 KST
- 종료: 2026-10-18 02:57:20 KST
- 소요: 25분 38초
- 상태: 사용자 문서 작성·최종 고정 DLL 독립 검증 완료. GitHub 최종 반영은 TASK-008 담당자에게 인계.

## 요구사항과 담당 범위

Role.md 기본 규칙 7번을 확인했다. README는 소스 빌드 설명 중심에서 **사용자가 얻는 가치·설치·계정 연결·첫 요청·첨부·대화·승인·저장** 중심으로 전면 개편했다. 상세 사용 방법은 docs/USER_GUIDE.md에 작성했다.

추가 프로젝트 3~8번 요구와 제공된 image.png도 검토했다. 같은 시점 CodexOrig가 제품 구현을 진행하고 있어 WORK_STATUS.md에서 범위를 분리했다. 제품 Chat UI·Provider·기존 회귀/IDE 테스트는 TASK-008이 담당하고, CodexCode는 README·사용 가이드·독립 검토/검증을 맡았다. 초안 모델 4개는 작성자·경로를 확인한 뒤 artifacts/task009로 이동했으며 제품에 포함하지 않았다. 고정 v0.2.0 릴리스와 다른 작업자의 파일은 보존했다.

## 변경 파일

| 파일 | 변경 목적 |
|---|---|
| README.md | 사용자 가치·실제 화면·설치·계정 연결·요청·첨부·규칙·승인/저장 안내 |
| docs/USER_GUIDE.md | 창 이동, AI 탭, 누적 대화, 첨부 제거·재전송, instruction·기본 설정·문제 해결 |
| docs/assets/chat-0.3-preview.png | 실제 제품 WPF UI를 합성 예제 대화로 렌더한 미리보기 |
| tests/review/Test-LiveChatContext.ps1 | 실제 Codex 이미지·텍스트·이전 대화·instruction 및 연결 확인의 독립 검증 |
| 자기 보고·WORK_STATUS·Update_History·OWERORDER | 검토·검증·소요 시간 및 협업 기록 |

## 독립 검토와 검증

README/가이드는 제품 코드 및 TASK-008 인계의 실제 버튼명·제한과 맞췄다. 첨부 4개, 이미지 PNG/JPEG 2 MB·1,600만 픽셀, 텍스트 100 KB, instruction 8,000자, 화면 최근 100개 메시지·요청 최근 10개/24,000자, 글자 크기 10~28을 구분했다. 첨부가 다음 요청에도 유지되는 동작, 메모리 대화와 영속 기본 설정의 차이, 적용과 저장의 차이도 설명했다.

독립 실제 Codex 검사에서는 합성 숫자 이미지, 텍스트 참고 자료, 이전 대화, 현재 instruction을 함께 전송했다. 이미지 숫자 7을 읽어 `return 1;` 한 곳만 `return 7;`로 바꿨고, 응답에 RULES_OK·HISTORY_OK·NOTE_OK 세 조건을 모두 포함했다. 공식 로그인 상태 확인도 성공했고 승인 전 합성 원본 디스크 파일은 유지했다. 사용자 프로젝트·제공된 참고 스크린샷은 AI에 보내지 않았다.

초기 검증 근거는 artifacts/task009/live-context-58eb96a4b0a74076afcb82d6dd251b86/result.json이며, 해당 스냅샷 DLL의 SHA-256은 E6804E1E1D65D920D7C5238F7289B9AF2A1C2BE4B4D5DD8C06A05031B41EE00C다. 최종 고정 DLL 결과는 아래 완료 기록에서 구분한다. 이 검사는 실제 CLI 검증이며 EnvDTE/IDE 저장 검사를 대체하지 않는다.

시각 검토 중 Dark-320.png에서 설정/검토 Expander 제목이 보이지 않는 문제를 발견해 TASK-008에 전달했다. Foreground 수정 및 가독성 단언 추가 후 PNG를 다시 열어 두 제목이 보이는 것을 확인했다. README 이미지는 수정 후 제품 UI의 예제 데이터 화면이며 실제 계정 대화나 IDE 화면 전체로 표기하지 않았다.

첫 IDE 실행의 IDE-21 실패는 Claude 탭의 별도 Draft가 비어 있는 테스트 입력 문제임을 로그로 확인해 전달했다. 제품의 탭별 입력 보존 동작과 테스트의 초기 입력을 구분했다. 또한 기본 TextBox 이미지 Ctrl+V 명령 경로를 검토 항목으로 전달했고 가이드는 구현된 붙여넣기 버튼을 안내했다.

문서 로컬 링크·이미지 13개 존재와 PowerShell 테스트 스크립트 구문 오류 0건을 확인했다. 문구의 실제 기능·한계 일치, 버튼명, 이미지 내용도 직접 검토했다.

## 재현

제품 v0.3.0 이상 DLL과 로그인된 Codex가 필요하다. 독립 검사는 공유 빌드나 사용자 설정·프로젝트를 변경하지 않는다.

```powershell
powershell -NoProfile -STA -ExecutionPolicy Bypass -File .\tests\review\Test-LiveChatContext.ps1 `
    -AssemblyPath 'C:\path\to\VS2017.AIAssistant.dll' `
    -CodexPath 'C:\path\to\codex.exe'
```

실제 Claude 계정/CLI는 설치되지 않아 검증하지 않았다. 현재 VSIX는 VS2017 15.9 이상용이며 VS2019/2022 지원을 주장하지 않는다. 실제 계정 인증은 사용자가 CLI·브라우저에서 수행한다. 다중 파일·무승인 자동 적용·디스크 대화 복원은 현재 기능 범위에 포함하지 않았다.

## 최종 완료 기록 — 2026-10-18 02:57:20 KST

최종 고정 패키지 A7F1C597BA0CCF7B18AC52DEB29BACE2D6B66E300B9A90C6A55EB5A3E7A88DF2와 DLL 28ACEEBE6A83AD63941F20CE38E77682DB40F30794DB4630564AC5AB7912C34C를 사용했다. 최종 DLL로 실제 Codex 멀티모달6/6, 편집/Chat71/71, Foundation33/33을 독립 재실행하여 통과했다. 실제 IDE35/35(실제 Codex 포함) 결과도 읽어 확인했다. TASK-008의 호환성44/44 및 Debug/Release 경고0 빌드 기록과 구분해 보고한다.

최종 실제 Codex 결과는 CodexCode_TASK-009_LiveContext.json이다. 새 검사에서는 이미지 숫자7에 따른 정확한 단일 교체, 규칙·이전대화·텍스트첨부의 세 조건 및 원본 보존·계정 연결을 다시 통과했다. 저장된 버전과 DLL 해시도 최종 인계 파일과 일치한다.

최종 README/USER_GUIDE의 로컬 링크·이미지 16개가 모두 존재하고 releases/v0.3.0의 VSIX 해시도 일치한다. 검증 자료는 CodexCode_TASK-009_Documentation.json, 독립 로그는 artifacts/task009/final-editing.log·final-foundation.log다. README 이미지도 최종 DLL로 다시 렌더하고 직접 열어 확인한 파일이다.

이미지 Ctrl+V 검토 의견은 TASK-008에서 PreviewCanExecute/PreviewExecuted 처리로 보완했다. README는 Ctrl+V와 붙여넣기 버튼을 함께 안내한다. UI·CLI·SDK 이벤트 검증과 별도로 모든 실제 마우스/키보드 조합을 수동 사용성 검사한 것으로 보고하지 않는다.

README와 USER_GUIDE를 최종 사용 안내로 확정하고 v0.3.0 다운로드 링크로 교체했다. 다른 작업자의 구현·테스트·개발 문서는 보존했다. TASK-009 소스/문서 편집은 종료했으며, 최종 GitHub 배포는 협업 기록에서 합의한 CodexOrig가 수행한다.