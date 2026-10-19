# TASK055 — 실행 중 입력과 순차 요청, 탭 전환 저장 최적화

- 담당: CodexOrig 보조 개발 (Root TASK054의 역할 분담).
- 기준: Role.md 2026-10-20 01:40 추가 요청 #6, #7.
- 작업: 2026-10-20 02:23~02:42 KST, 약 19분. 세부 종료 시각·소스 해시는 `CodexOrig_TASK-055_Evidence.json` 참조.
- 구현/개인 검증 완료. 전체 최신 제품 후보 통합·감사·네 SKU/실제 IDE·문서·최신 Release 제작·공개 배포와 익명 다운로드 검증은 Root TASK054 담당이다. 이 보고는 전체 제품 배포 완료가 아니다.

## 구현

`AssistantControl.RequestQueue.cs`에 UI 스레드 단일 소비자 FIFO를 추가했다. 현재 실행 1건과 대기 최대 8건으로 제한하며, 실행 중에도 Instruction 입력과 Enter 제출, Ctrl+Enter 줄바꿈을 허용한다. 제출 시 입력만 비우므로 이전 요청 완료가 다음 초안을 지우지 않는다. 수락한 사용자 원문은 즉시 대화에 저장한다. 저장에 실패하면 실행하지 않고 입력과 기존 100개 메시지를 복구한다.

제출 시 provider, conversation/chat 소유권, 실행 파일, 모델/생각 강도/속도, 공통 규칙, 첨부, 선택/원본 문서, 프로젝트 및 전체 액세스 모드를 고정한다. 실행 전과 비동기 결과 반환 뒤에 소유권을 다시 검사한다. 대화별 sequence 장부로 실행 문맥을 만들기 때문에 선행 완료 답변은 후속 요청에 포함되고, 현재/미래 대기 입력·다른 대화·취소된 대기 입력은 섞이지 않는다. 장부에서도 대화 100개 보관 상한에 따라 제거된 참조를 해제한다.

미적용 수정안이 있으면 다음 CLI를 자동 실행하지 않는다. 사용자가 승인·적용하거나 Root가 추가한 `DiscardProposalButton`의 **이 수정안 건너뛰고 다음 요청**을 직접 선택해야 진행한다. 건너뛰기는 사용자 동작을 저장한 뒤 해당 미적용 수정안과 승인만 초기화하며, 소스를 적용·저장하지 않는다. 명시적 적용 뒤 기존 대기 snapshot과 문서가 달라졌으면 후속 CLI를 호출하기 전에 멈춘다. 자동 rebase·자동 적용은 없다.

Stop·실패·Dispose는 잔여 대기를 제거한다. 실행하지 않은 원문은 대화에 보존하고 재전송하도록 안내한다. 취소를 무시하고 늦게 반환한 수정안도 결과로 수락하지 않는다. 전체 액세스도 같은 큐 소비자를 사용하며, 요청별 고정 모드와 경로를 사용한다. provider/대화 전환은 실행 중과 승인 대기 큐가 있을 때 차단한다.

완료 알림용 internal `CurrentRequestProvider`, `CurrentRequestTitle`은 작성 중 draft 대신 실행 중 request만 반환한다. Root가 BeginProgressRequest에서 복사해 완료 시까지 사용할 수 있다.

`PersistCurrentConversation`은 저장할 전체 record의 SHA256 fingerprint가 같으면 디스크 쓰기/목록 재생성을 건너뛴다. 초안·메시지·상태·읽음·역할 메타데이터의 실제 변경은 저장한다. provider 전환의 동일 계정 초기화 이중 호출을 제거했고, 같은 Chat 바인딩을 다시 요청할 때 최근 20개 목록을 다시 초기화하지 않는다. Root 요청에 따라 Experience.OptionsHovered도 ScheduleFanOpen으로 연결했다.

## 파일

- `src/VS2017.AIAssistant/Views/AssistantControl.xaml.cs`
- `src/VS2017.AIAssistant/Views/AssistantControl.Chat.cs`
- `src/VS2017.AIAssistant/Views/AssistantControl.Experience.cs`
- `src/VS2017.AIAssistant/Views/AssistantControl.Runtime.cs`
- 신규 `src/VS2017.AIAssistant/Views/AssistantControl.RequestQueue.cs`
- `tests/task055/Test-RequestQueue.ps1`, `RequestQueueChecks.cs`, `RequestQueueRunner.cs`, `RequestQueueProtocolFixture.cs`

Root 소유 XAML의 건너뛰기 버튼과 네 프로젝트 Compile 등록은 Root가 수행했다. 공유 bin/obj, git branch/commit/push, 버전 또는 기존 Release를 변경하지 않았다.

## 검증

최종 개인 candidate11은 VS2017 Release 오류 0/경고 0, 고정 DLL SHA256 `5F686CE2E9071B6153CE6737361C30684C8643E3B1E06CFB1F5ACF0DABC53AAE`다. Root/다른 구현자의 진행 중 소스와 충돌하지 않도록 C2의 닫힌 개인 원본 snapshot에 TASK055 소유 5개 C# 최신본과 Root의 건너뛰기 XAML을 넣었다. 최신 전체 통합 후보를 뜻하지 않는다.

`artifacts/task055/tests-candidate11/result.json` **56/56** 통과. 실제 WPF TextBox와 버튼 상태/Enter 처리, TCS로 제어하는 IAiContextEditProvider의 순서·중복 실행·입력 보존·고정 문맥·선행 완료 답변·미래 입력 제외·승인 대기·명시 건너뛰기·취소·실패·해제·문서 변경·다른 대화 방어·8건 상한·저장 실패 복구를 검사했다. 로컬 합성 Claude stream-json 자식 프로세스의 성공 2건 순서(start1/end1/start2/end2), 실패 이후 잔여 미실행, Stop 시 소유 자식 종료도 확인했다. 실제 Claude 로그인이나 유료 모델을 호출하지 않았다.

동일 비표시 STA 호스트의 provider 탭 40회 처리 시간은 기존 v0.12.0 C6 1318ms, 현재 최종 1044ms였다. 기존 두 대화 파일의 시각은 갱신됐고 현재는 변경 파일 0개였다. 이전 C7/C8/C9 측정은 881/915/861ms였다. 해당 수치는 전체 화면 렌더·물리 마우스·실제 IDE 시간에 대한 주장이 아니다.

최초 실패를 지우지 않았다. C1의 UI 스레드 분석기 오류/경고는 VerifyAccess와 기존 WPF 이벤트 계약에 맞는 좁은 VSTHRD109 주석으로 보완했고 이후 개인 빌드는 오류/경고 0이다. C3~C5는 다른 작업자의 클래스 저장/Compile 등록 전 snapshot 및 작성 중 Claude 코드에 의한 빌드 실패로 보존했다. 첫 하네스의 작은 창 History overlay가 Composer 부모를 비활성화한 전제는 ShowHistory=false로 분리했다. native fixture의 initialize 응답 누락과, 표시 창 없이 호출한 검사 호스트의 DispatcherSynchronizationContext 누락도 수정했다. C7-v2 unhandled 기록은 UI 스레드 접근 예외를 직접 남겼다. C10의 중복 Compile는 개인 snapshot 스크립트의 치환 오류로 원문을 보존하고 C11에서 수정했다.

## 인계와 한계

PowerShell 5.1 `-STA`로 `tests/task055/Test-RequestQueue.ps1 -AssemblyPath <고정 DLL> -AssetsPath <해당 project.assets.json> -OutputDirectory <새 폴더>`를 실행한다. 기존 출력 폴더를 덮지 않는다. 네 SKU/최신 전체 후보에서 같은 검사를 다시 연결할 수 있다.

대기 실행 상태 자체는 메모리에 있으며 재시작 후 자동 재실행하지 않는다. 수락한 사용자 원문은 대화에 남는다. FIFO를 위해 사용자 원문이 제출 즉시 보이므로 화면의 제출 시간 순서와 CLI의 실행 문맥 그룹 순서를 구분한다. 실제 IDE/물리 입력, 로그인·유료 Claude 결과, 최신 전체 후보 UI·알림·artifact export의 통합 검증과 배포는 Root가 별도 근거로 마감한다. 감사·문서 담당에게도 이 계약을 전달해 사용자 설명에 포함해야 한다.
