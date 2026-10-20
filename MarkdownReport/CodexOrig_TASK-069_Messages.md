# TASK069 · AI 간 메시지 전달

담당: CodexOrig 보조061. 기준: Role.md 마지막 추가 요청 #1, image-67.png 직접 확인. 제품 수정은 이 기능 소유 범위에 한정했으며 Git/공유 bin·obj 빌드/로그인/원격 AI 호출을 수행하지 않았다. Root가 통합 빌드·설치·감사·Release·실제 배포를 담당한다.

이미지에서 AI는 현재 세션에 다른 담당자에게 보내는 수신자·메시징 경로가 없어 공유 문안만 작성했다고 답했다. 기존 `ConversationCollaborationStore`의 실제 Send/Receive/Reply/Deliver 상태 머신은 있었지만 사용자가 직접 주소와 본문을 선택해 전달하고 상대 AI로 응답받는 일반 메시지 UI와 runner 연결이 없었다.

실제 같은 팀의 열린 AI 주소 목록과 `ai_message` JSON 초안 형식을 프롬프트에 추가했다. AI가 수신자 ID·제공자·본문을 작성하면 답변 우클릭의 **AI 메시지 초안 전송 검토…**가 주소와 본문을 미리 채운다. 사용자는 검토 창에서 **이 수신자에게 전송 승인**을 누른다. 일반 답변도 **다른 AI에게 메시지 보내기…**에서 실제 수신자를 직접 선택할 수 있다. 렌더링·메뉴 열기·초안 파싱만으로 전송하지 않는다.

사용 순서는 다음과 같다.

1. 서로 메시지를 주고받을 AI 대화를 열고 역할·협업 설정에서 같은 팀인지 확인한다.
2. 발신 AI 답변에서 메시지 초안 전송 검토를 선택하거나, **AI 지정하여 대화 → 역할·협업 설정 → AI 메시지센터**를 연다. 실제 수신자와 본문을 확인하고 전송을 승인한다. 이때 상태는 **Pending**, 실제 수신함에 저장됐으며 상대 AI가 실행된 상태는 아니다.
3. 수신 AI 대화를 선택하고 메시지센터의 받은 메시지를 고른 뒤 **수신하고 읽기 전용 AI 답변 실행**을 누른다. 해당 AI의 설정된 CLI·모델·생각 강도·속도로 빈 격리 폴더에서 응답한다. Claude는 tools-off/strict empty MCP, Codex는 read-only exec를 사용한다. 프로젝트 원문·다른 대화·첨부는 자동 공유하지 않는다.
4. 실제 CLI summary가 성공하고 회신 저장까지 완료돼야 **Replied**가 된다. 발신 AI에서 보낸 메시지를 선택하고 **발신 AI에서 회신 읽기**를 누르면 **Delivered**로 기록한다. 실제 수신·회신은 각 대화에 고정 ID로 기록돼 다음 요청 문맥에 포함된다. 재읽기·저장 재시도로 같은 메시지를 중복 삽입하지 않는다.

공유 메시지는 도구 실행 권한이 아니다. 응답에 수정안이 반환되면 적용하거나 회신으로 성공 처리하지 않는다. 답변을 다른 AI에게 자동으로 연쇄 전송하지 않는다. 승인 취소는 Store/CLI 무변경이며, 실제 저장 시점에 자기 자신·다른 팀·닫힌 수신자를 다시 검증한다. 동시에 같은 메시지를 응답하는 두 번째 CLI는 고정 workspace의 메시지별 file lease로 배제한다. 오류·중지·창 종료/Dispose는 실제 프로세스를 종료하고 실패를 보존하며 허위 회신을 만들지 않는다. 내부 협업 파일 경로를 UI 링크로 제공하지 않는다.

제품 소유 파일은 `Chat/CollaborationMessageRequest.cs`, `Providers/CollaborationMessageRunner.cs`, `Views/AssistantControl.Messages.cs`, `ConversationCollaboration.cs`의 BuildPromptContext, Markdown 메뉴 1개 연결, Options 메시지센터 버튼이다. Root가 새 Compile 등록/4SKU 동기화와 Dispose 연결을 처리했다. 빌드 분석기의 최초 WPF 경고/오류는 좁은 실제 Dispatcher 검증 및 해당 WPF 핸들러만의 근거 주석으로 수정했다. Root 요청으로 Dropdowns의 역할 설정 이벤트와 계정 조회 후 UI 갱신에도 VerifyAccess를 제공했다. Store constructor/Access/EnsureAgent 성능 보완은 보조055 소유로 분리했다.

고정 C3 DLL을 실제 로드한 새 STA 프로세스에서 최초 46/46, 메뉴·수명·수신자 경계 추가 뒤 58/58을 통과했다. 근거는 `artifacts/task069messages/candidate3-messages-2/result.json`, `inputs.json`, gate 원문과 실제 workspace JSON/대화 파일이다. C3 DLL SHA256은 `EDB57654F5D455C73DF2BCBBDC191206D06252A663A2D7DE059BE6184D94751E`다. 실제 Claude/Codex 두 subprocess 프로토콜, 실제 UI Send/Run/Read handler, 실제 수신/회신 상태 재로드, 초안 보존, 다음 queued context, 중복 실행/재읽기, 오류/명시 retry, 비공유 history·attachment 제외, 실제 PID 취소/Dispose를 검사했다. 가짜 IAiProvider 성공으로 대체하지 않았다.

남은 범위는 실제 AI가 초안 프로토콜을 따르는지, 계정 로그인/원격 실제 답변, 설치된 IDE에서의 물리 UI 조작·화면 배치와 최종 통합 후보 회귀다. 이번 fixture는 Window.Show/Popup/OS 입력/네트워크/실 AI를 사용하지 않았다. 따라서 원격 모델 간 전달까지 실측했다고 확대하지 않는다. Root가 최종 후보에 최상위 기본 메시지 검사를 연결하고 4SKU·독립 감사·최신 Release 생성·실제 공개·다운로드 해시 검증까지 마감해야 한다.
