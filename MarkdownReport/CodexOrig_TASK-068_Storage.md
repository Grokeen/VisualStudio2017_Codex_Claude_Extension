# TASK068 #2 시간별 저장·비동기 이전 조회 인계

CodexOrig 보조 개발 / backend055. 2026-10-20 13:11 검토 시작,13:31 제품 인수,14:01 인계. 전체 배포 완료 보고가 아니라 Root의 통합·감사·실 IDE·Release 작업을 위한 인계다.

## 변경

메인 대화 Markdown은 최신20개만 보관하고 이전 원문은 UTC 시간별 불변 Markdown에 저장한다. MD 표식을 포함한 UTF-8 바이트를 계산해 파일4MB 이전에 분할한다. 별도 인덱스의 수신 순서로 조회하므로 시계 역행·시간대 변경이 대화 순서를 바꾸지 않는다. 기존 ID 없는 v1 원문에는 대화ID/원문 위치 기반 안정적인 ID를 부여한다.

시간 파일과 불변 인덱스를 먼저 준비하고 메인 MD의 manifest/revision/hash 참조를 마지막 원자 커밋으로 교체한다. 실패·프로세스 중단 뒤 미참조 orphan은 읽지 않으며 실패한 사용자 요청이나 성공 메타데이터를 전진시키지 않는다. 동일 대화 저장을 파일 거래 잠금으로 직렬화하고 오래된 저장자는 `StaleConversationWriteException : IOException`으로 명시 거절한다. 확정 원문과 미전송 입력은 모두 보존한다. 메인 MD의 외부 수동 편집도 원문 해시로 확인한다. 임시 파일은 같은 폴더의21자 sibling 이름을 독점 생성해 MAX_PATH 문제를 해결한다.

위쪽 스크롤은 Task.Run에서 파일을 읽고 소유 WPF dispatcher에서만 결과를 삽입한다. provider/record/chat/cursor/manifest/generation을 고정하며 전환/Dispose/커서 변경 후 결과를 버린다. 중복 스크롤은 한 건만 진행한다. 실패 시 커서를 보존해 재시도하며 끝 커서는 반복 I/O를 멈춘다. 실제 삽입 직전 현재 문단의 Y/offset을 고정해 조회 중 사용자 스크롤과 아래 새 답변을 섞지 않고 읽던 위치를 유지한다. 이 취소는 CLI 중지·초안과 독립적이다.

## API와 소유 범위

Create/LoadOpen/Save/LoadOlder/Rename/Close/Load/LoadInstructions/SaveInstructions/GetPath API를 유지했다. ConversationRecord에 StorageRevision, StorageDocumentHash, HistoryManifest, HistoryManifestHash를 추가했다. 성공 커밋 뒤만 갱신한다. 기존 Persist는 record 자체를 유지하고 Messages만 ID/CreatedAt과 함께 복사하므로 clone 수정0이다. 새로운 record clone은 네 필드를 함께 보존해야 한다.

새 Compile은 Chat/MarkdownWorkspaceStore.Committed.cs이며 Root가4 SKU에 등록했다. Chat.cs는 history 필드·BindVisibleConversation·이전 조회/소유권/취소만, Viewport.cs는 이전 조회·취소·레이아웃/앵커만 수정했다. CodexCode의 선택지/MessageChoiceClicked·동시 실행·권한·CLI는 편집하지 않았다. callback은 `Func<Task<bool>>`, 메서드는 `PrependOlderConversationMessagesAsync`다.

비동기 off-thread 진입은 실제 소유 dispatcher로 전환한다. 동기 수명 함수는 CheckAccess 기반 runtime guard를 사용해 IDE 전역 주 스레드 단언을 다른 화면으로 전파하지 않는다. VSTHRD001은 필요한 WPF dispatcher 메서드,002는 이미 완료한 Task 결과 조회,003은 같은 컨트롤이 직접 시작·추적한 파일 Task를 기다리는 CompleteConversationPrependAsync 한 메서드에만 근거를 붙였다. 프로젝트/파일 전역 suppression0이다.

## 검증

| 검사 | 결과 | 증거 |
|---|---:|---|
| 실제 제품 저장 소스 + 실제 ChatMessage 클래스 사본 | 19/19 | artifacts/task068storage/run-b6c7b2251af149e48d0252f301b775cf |
| 4MB·세 commit 실패·orphan·snapshot·손상·긴 경로·수동 MD 충돌 | 13/13 | 같은 실행 boundary-result.json |
| 기존 workspace 전체 source 회귀 | 59/59 | artifacts/task012/workspace-5fc906cd3cd244e8a526fb6acfe188c7 |
| 실제 history13메서드 사본 + WPF + 제어 gate + 실제 저장소,Window.Show0 | 10/10 | artifacts/task068storage/ui-46645546598d4ad3b2361d60f3fa3c3e |
| C3 전체 소스 개인 사본 + 두 UI 파일 overlay,Legacy Release analyzer | 오류0/경고0 | artifacts/task068storage/c4-ead8/build-legacy.log |
| 같은 개인 사본,2022 Release analyzer | 오류0/경고0 | artifacts/task068storage/c4-ead8/build-2022.log |
| Task049 갱신 검사 실제 C2 DLL 참조 library compile | 오류0 | artifacts/task068storage/HistoryPagingChecks-current.dll |
| C5 실제 DLL만 참조,동일19+13 전체 저장 검사 | 32/32 | artifacts/task068storage/assembly-151a81dfe1154b8c84f21f396fcb9d6e/assembly-summary.json |

19개 기대는 Root가 허용한 stale 두 조건만 명시 거절·입력/원문 보존으로 바꿨다. 병렬 저장은 같은 revision에서 정확히 한 성공과 성공 원문 누락0을 요구한다. workspace는 최신 Draft를 실제 변경한 뒤 낡은 Close의 예외/초안/IsOpen/확정 파일 보존과 최신 record Close를 검사한다. Task049는 초기 model20/main20과 전체66개 디스크 원문 순서/고유ID 왕복을 검사하며 기존 anchor assertion을 유지하고 실제 async 완료를 기다린다. IdeTests.cs/LatestUi 편집0이다.

최초10/18·11/19 실패와 긴 fixture 경로2/18, 별도 시제품 최종29/30 실패를 보존했다. 시제품의 serializer 크기 측정 전 상한 오류를 제품에 채택하지 않았고 실제 MD 바이트 분할로 검증했다. Root C3의 VSTHRD109 오류1/010 경고21도 보존하며 후속 개인 Release0/0과 구분한다.

task022 SourceOnly 수학 driver는 기존 overlay/popup/palette/motion/hover 의존성 누락8종으로 컴파일 실패가 남는다. 새 async 필드/취소 stub만 추가했다. artifacts/task022/viewport-a7c0faac77ea43a8b8f582e871df1d2b를 보존하고 제품 오류/성공으로 합치지 않았다. 무창10개는 실제 메서드 본문 추출 + fixture 껍데기/reader gate이며 **전체 VSIX DLL이나 실 IDE 검사라고 주장하지 않는다**. 전체 Task049/viewport/편집 GUI 회귀는 Root의 고정 후보에서 실행해야 한다.

## 최종 동결

| 파일 | SHA256 |
|---|---|
| MarkdownWorkspaceStore.cs | 4DF915F98CFFC7FCB36190960DC5CF02CC784D76DCBE18A0E9FBE8D34DF39103 |
| MarkdownWorkspaceStore.Committed.cs | 86116B5B08EFAFC5EB8D34F15F37BEF28E1B0A9CF7BB67A952DAC25C2AA27282 |
| AssistantControl.Chat.cs | E82AF68F44474EF88F84711AA64AB91A5BBDB5944724AE624A028A35156BAC1A |
| AssistantControl.Viewport.cs | FD858978052EBAD76BAD1EB087D057974DC095D75FF09544B3118CF8A7E1A1B9 |

Viewport의 최종 차이는2022 VSTHRD003 메서드 한정 속성이고 WPF10개가 검사한 동작은 동일하다. 저장 쌍은13:39 이후 그대로다. Chat.cs는 Code 선택지와 파일을 공유하므로 후속 변경 시 method/소유권을 함께 확인한다. 이 인계 이후 제품 추가 편집0이다.

## 제한과 인계

인덱스는2만 참조/파일4MB 중 먼저 도달하는 상한을 명시 거절하며 페이지 body cache는2개다. LoadOpen은 provider 최대500개 메인 MD의 최신20개를 읽는다. metadata-only 목록 API는 미구현이다. 과거 snapshot을 보존하는 immutable index/chunk GC는 아직 없어 저장 횟수에 따라 파일 수가 늘어난다. 무변경 Save는 새 파일을 만들지 않는다.

기존 v1의 메시지별 실제 시각은 없으므로 대화 CreatedAt fallback을 보존하며 실제 이벤트 시각 복원이라고 주장하지 않는다. 옛 미공개 시간 파일은 마지막 확정 ID까지 마이그레이션한다. 옛 버전에서 이미 거절 내용이 후속 성공 저장에 섞여 확정됐다면 그 과거 의미를 완전히 추론할 수 없다. 매우 큰 옛 시간 아카이브의 최초 마이그레이션은 원문을 모으는 경로라 추가 메모리/시간 검증이 필요하다.

외부 main MD만 복사해 가져오면 그 파일의 최신20개만 가져온다. 전체 기록 백업/이동에는 같은 대화 `.history` 폴더를 함께 보존해야 한다. 새 저장자들은 거래 잠금을 따르며 이 잠금을 모르는 이전 버전과 동시 저장하는 호환성은 별도 범위다.

Root는4 SKU0/0·전체 회귀·실 IDE·독립 감사·최신 Release 파일·실제 게시·익명 다운로드 크기/SHA256까지 마감한다. 사용자 문서에는 시간 보관·상향 조회·stale 거절 시 입력 보존/다시 불러오기·전체 백업 폴더를 안내한다. 새 화면 추가는 없다. 본 담당은 공유 bin/obj 빌드·Window.Show·OS 입력·Git/버전/배포 변경0이다.

## C5 실제 DLL 감사 후속

Root 요청으로 기존 저장19+경계13개를 C5 실제 VS2017 DLL에서 실행했고32/32 통과했다. `ActualProductDll=true`, `ProductSourceCompiled=false`, `InputsUnchanged=true`다. 각 검사 EXE가 실제로 로드한 Assembly.Location이 고정 개인 DLL 경로와 같은지도 확인했다. DLL SHA256은 AD6327BB58CEBF0E362B90134F9DFB803CDB566B457452AC1D17C7C999E66E02,exact assets SHA256은773D111CA7FB4B7452EA373505C17AE633DFE436FE97381BA9B133471490440B다.

새 `tests/task068storage/Test-StorageAssembly.ps1`는 제품 C# 소스를 컴파일하지 않고 같은 영구 fixture2개만 DLL 참조로 컴파일한다. fixture의 검사 기대는 변경하지 않았다. 내부 StorageFaultInjector 속성은 Instance/NonPublic reflection setter로 동일 실패를 주입하며 예외 ToString의 inner 원문을 그대로 보존한다. 결과 메타데이터만 실행 어셈블리에서 source/DLL 여부를 판정한다. 이 변경 때문에 소스 검사를 다시 실행한 것으로 주장하지 않는다.

최종 고정 실행은 `assembly-151a81dfe1154b8c84f21f396fcb9d6e`이며 fixed-inputs.json에 호출한 driver 자체·fixture·DLL·assets·정확한 assets 의존성·framework compile reference의 원본/복사/검사후 해시를 보관했다. 최초 실제 DLL 실행 `assembly-71f05e024af445b8abcb654554a75df9` 역시32/32이며 driver 자체 snapshot을 추가한 뒤 최종 실행을 분리해 보존했다. 제품4파일·GUI·공유 빌드·배포 변경0이다. 이32개는 source-only C2 잔여 감사의 실제 DLL 근거를 보완하며 실 IDE/전체 배포 완료를 대신하지 않는다.
