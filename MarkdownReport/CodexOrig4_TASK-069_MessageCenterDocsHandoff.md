# TASK069 메시지센터 사용자 문서 인계

작성: CodexOrig4 · 2026-10-20 22:24 KST
범위: 사용자 안내 문구·검증 경계 인계. 제품, README, 사용자 가이드, PDF 및 캡처 담당자의 파일은 수정하지 않음.

## 사용자에게 안내할 흐름

1. AI 답변의 구조화 초안을 검토하거나 메시지센터를 열어, 같은 팀의 실제 수신 AI를 선택한다. 일반 답변은 사용자가 수신자를 직접 선택할 수 있다.
2. 수신자와 본문을 확인하고 **전송 승인**을 누른다. 실제 받은함에 저장되면 상태는 **Pending(응답 대기)**다. 이는 메시지가 수신함에 들어갔다는 뜻이며, 수신 AI가 답했거나 실행됐다는 뜻이 아니다.
3. 수신 AI 대화에서 메시지를 선택하고 **수신하고 읽기 전용 AI 답변 실행**을 명시적으로 선택한다. 실제 CLI 응답과 회신 저장이 성공하면 **Replied(회신 작성됨)**가 된다.
4. 발신 AI 대화의 보낸함에서 해당 메시지를 선택해 **발신 AI에서 회신 읽기**를 수행한다. 발신자가 회신을 확인하면 **Delivered(발신 대화에 전달·기록됨)**로 표시된다. 메시지와 회신은 양쪽 대화에 연결되어 다음 요청 문맥에 포함된다.

상태 요약: **Pending = 수신함 저장, 회신 없음 / Replied = 수신 AI의 실제 로컬 CLI 회신 저장 완료 / Delivered = 발신자가 회신을 읽어 대화에 기록**. 버튼을 누르지 않은 상태에서 자동 실행·자동 연쇄 전달은 일어나지 않는다.

## 검증 범위 및 표현 한계

C3 2017 실제 제품 DLL(`EDB57654F5D455C73DF2BCBBDC191206D06252A663A2D7DE059BE6184D94751E`)과 일치하는 assets를 사용한 별도 검사에서 58/58 통과했다. 근거는 [메시지센터 보고서](CodexOrig_TASK-069_Messages.md), [검증 JSON](CodexOrig_TASK-069_Messages_Validation.json), [후보 결과](../artifacts/task069messages/candidate3-messages-2/result.json)다.

검사는 실제 UI 핸들러·상태 저장/재로드와 격리된 Claude/Codex CLI fixture를 다뤘다. 로그인, 원격 AI 추론, 실제 온라인 메시지 전달, 설치된 IDE의 물리 UI 조작은 확인하지 않았다. 따라서 사용자 문서와 그림은 실제 서비스 간 실답변 또는 실제 IDE 캡처라고 주장하지 말고, CLI 설정에 따른 읽기 전용 실행 및 검증 경계를 밝혀야 한다. 일반 AI 답변은 draft 형식일 수 있으므로 수신자·본문을 사용자가 검토하고 승인한 뒤에만 전달된다.

## 문서 소유·대기 항목

- 현재 사용자 안내 준비/문서 소유는 **CodexOrig**로 WORK_STATUS에 기록되어 있다. Root가 통합 후보 동결 후 소유 담당자가 반영한다.
- 계획된 대상: `README.md`, `docs/USER_GUIDE.md`, 필요 시 `docs/INSTALL_TROUBLESHOOTING.md`; 새 미리보기 PDF `VisualStudio_AI_Assistant_설치_사용설명서_v0.14.0_미리보기.pdf`. 기존 v0.13 사용자 문서/PDF는 보존한다. 세부 범위는 [문서 계획](CodexOrig_TASK-069_DocumentationPlan.md) 참고.
- 전용 화면 자료는 캡처 담당자가 **detached WPF RenderTargetBitmap**으로 생성했다. `pass-4`는 실제 C3 dialog의 작성/받은함/보낸함 기본 화면 캡처이며, `pass-5-verified`에는 실제 제품 화면의 Pending/Replied/Delivered 상태를 별도 캡처했다. 후자는 로컬 합성 레코드를 exact product store API `Send -> Receive -> Reply -> Deliver`로 바꾼 시각화 fixture이며, 회신 문자열은 AI 응답이 아니다. 두 run 모두 DLL/assets 해시 전후 일치, Window.Show/Popup/OS input/CLI/AI/network 0이다. 합성 렌더를 실제 Visual Studio 화면 또는 실제 AI 답변으로 표기하지 않는다.
- 시각 검토에서 초안의 수신자 콤보박스가 이름 대신 CLR 타입명 `VS2017.AIAssistant.Chat.ConversationAgentIdentity`를 표시했다. 메시지센터 제품 소유는 CodexOrig 보조061이며 구현 범위는 `src/VS2017.AIAssistant/Views/AssistantControl.Messages.cs`다. 이 문제는 C3 화면의 결함으로 별도 보존했으며, 최종 사용자 문서의 대표 초안 그림으로 사용하기 전 소유자가 후속 후보에서 표시 이름을 보정할지 판단해야 한다. pass-4/pass-5 evidence는 변경하지 않는다.

## 별도 C8 디버깅 도우미 캡처 업데이트

2026-10-20 23:21 KST에 #18 디버깅 도우미용 익명 WPF 캡처가 `artifacts/task069/orig4/candidate8-debug-helper-pass3/`에 완료됐다. `capture-result.json`은 **25/25 PASS, 16 PNG**를 기록한다. 고정 후보 VS2017 DLL SHA256은 `F821132D6DD4225F98853A68F9154F692432437041AE59EE01512F7B564F9DB2`, assets SHA256은 `E4E14C7C98ECD9D2AF9A56CE9DA66A3A04482916D65E0FED484E418447C43AA2`다. 다섯 팔레트의 넓은/좁은 화면과 idle/pending/success/error/limit/concurrent-draft 상태가 포함되며, 좁은 배치도 겹침 없이 육안 확인됐다.

이 캡처는 합성 디버거 프레임을 쓰는 detached WPF 렌더다. 실제 IDE/DTE/debugger 연결, Window/Popup 표시, OS 입력, 네트워크, CLI·AI 호출은 없었다. 따라서 #18 화면 예시로 검토할 수 있지만 native IDE 검증을 대신하지 않는다. 이는 메시지센터의 C3 pass-4/pass-5 자료와 별개다. Root의 native IDE 확인과 문서 후보 동결 전에는 최종 PDF를 확정하지 않는다.

Root가 수행한 공식 native C8 검사도 완료됐다. VS2017 실제 DTE 통합 결과는 [result.json](../artifacts/ide/run-a02c6f22d94a450c894a9de282bfde4f/result.json) 기준 **172/172 PASS**이며 `DEBUG069-01..11`을 포함한다. 고정 DLL은 `6B0C98D6F61923EC119BF885663B96ACDECFC6264D6724F22872BE15C7C01625`다. 격리 합성 프로젝트의 `DebugProgram.cs` 8행에서 bounded context를 확인했고, 변수값 평가·자동 전송·사용자 소스 변경 없이 draft와 selection 보존 및 debugger 종료 후 버튼 숨김을 검사했다. 전용 테스트 IDE는 정상 종료했다. 이는 #18 native IDE 증거이며, #17 테마/에디터 통합 확인의 대체 증거가 아니다.

따라서 #18 화면·동작 근거는 offscreen 캡처와 Root 공식 native run 양쪽이 준비됐지만, #17에 남은 통합 확인과 최종 후보 동결이 끝날 때까지 README/PDF 배포본은 확정하지 않는다.
