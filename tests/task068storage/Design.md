# TASK068 storage — Role 2026-10-20 12:47 #2 설계·검사 준비

2026-10-20 14:01 갱신: 아래 본문은 제품 인수 전 설계 제안의 원문이다. Code가13:31 제품 소유권을 인계한 이후의 구현·검사는 MarkdownReport/CodexOrig_TASK-068_Storage.md를 참고한다. 실행 가능한 검사는 Test-StorageBoundaries.ps1(실제 저장 소스19+13)와 Test-HistoryUi.ps1(실제 메서드 사본·무창 WPF10)이다. 아래 제안 API/Cases.json/fixture generator 이름은 채택 완료나 파일 존재를 뜻하지 않는다.

상태: **제안/검사 준비이며 제품 구현·제품 검사 성공 아님**. Code TASK067이 제품을 소유하므로 Root의 명시적 인수 전 제품 파일은 수정하지 않는다. 이 폴더만 준비한다. 공유 bin/obj·GUI·빌드·배포를 실행하지 않았다.

## 현재 소스에서 확인한 계약

| 위치 | 현재 동작 | #2 반영 때 필요한 구분 |
|---|---|---|
| MarkdownWorkspaceStore.cs:84~86,120 | 파일4MiB, provider당500파일, record당100메시지. LoadOpen이 각각의 본문 전체를 동기 ReadRecord | 목록 metadata만 조회. 상세 본문은 현재 대화 최근 page만 읽기 |
| MarkdownWorkspaceStore.cs:173~203 | Save가 record.Messages 전부를 한 md로 원자 교체 | window의 일부 메시지를 전체 역사로 오인해 삭제하면 안 됨 |
| MarkdownWorkspaceStore.cs:252~263 | 외부 md import는 새 GUID; provider 혼합 거절 | legacy import와 archive import를 구분하고 원본/다른 대화 덮어쓰기 차단 |
| MarkdownWorkspaceStore.cs:459~534 | UTF8 strict bounded read, reparse point 차단, temp→Replace/Move 및 제한 재시도 | chunk/manifest에도 같은 검증 유지. 여러 파일의 단일 commit 지점 추가 |
| ChatWorkspace.cs:169,202 | 메시지는 Role/Text뿐, Add가 오래된 것을100개 상한에서 제거 | stable id/time/sequence 추가. UI 상한이 디스크 보관 삭제로 이어지지 않게 분리 |
| ChatWorkspace.cs:259 | 요청 history는 user/assistant 최근10개/24000자 | 스크롤로 공개한 옛 페이지를 AI 요청으로 자동 확장하지 않기 |
| AssistantControl.Chat.cs:40,140,199 | ActiveChat이 record 본문을 모두 메모리에 복사. 화면20개, prepend20은 이미 읽은100개에서 처리 | 실제 async 디스크 page와 bounded visible window로 변경 |
| AssistantControl.Experience.cs:210 | Role/Text만 복사한 record를 Save, 전체 JSON SHA256 fingerprint 비교 | id/time/sequence를 보존하고 append/upsert와 metadata/draft 저장을 분리 |
| AssistantControl.Viewport.cs:221,278 | Func<bool> 동기 prepend; 레이아웃 높이 증가만큼 anchor 복원; 전환 시 예약 취소 | async read + cancellation + conversation/provider/generation 검증 후 같은 anchor 복원 |

현재는 UI paging만 있으며 파일 지연조회가 아니다. 500개 × 4MiB 허용 기록은 목록 복원만으로도 본문 최대 약2GiB를 읽을 수 있고, 본문 문자열과 ChatSession 복사/렌더 할당이 추가된다. 실제 peak 수치는 아직 측정하지 않았다.

## 제안 API (구현 담당과 합의 전 이름/형식 미확정)

```csharp
Task<ConversationSummary[]> ListOpenSummariesAsync(string provider, CancellationToken token);
Task<HistoryPage> ReadPageAsync(ConversationKey key, HistoryCursor before,
    int count, int maxUtf8Bytes, CancellationToken token);
Task<AppendResult> AppendAsync(ConversationKey key, long expectedRevision,
    StoredMessage[] messages, CancellationToken token);
Task<MetadataResult> SaveMetadataAsync(ConversationKey key, long expectedRevision,
    ConversationMetadata metadata, CancellationToken token);
Task<MigrationResult> ImportLegacyAsync(string sourcePath, string provider,
    CancellationToken token);
```

- `ConversationKey`: 검증한 provider와 GUID. 제목/사용자 문자열은 파일 경로 구성에 쓰지 않는다.
- `StoredMessage`: stable GUID id, 단조 증가 sequence, `DateTimeOffset? CreatedAt`, role, 원문 text. legacy의 알 수 없는 개별 시간은 null/명시한 inferred 표시다. 현 시각으로 과거의 실제 작성 시간을 꾸며내지 않는다.
- 시간 bucket은 `CreatedAt.UtcDateTime`을 시 경계로 내림한 `[hour,hour+1h)`. 파일 이름에는 UTC `yyyyMMddTHH`를 사용한다. 동일 instant의 offset 차이/DST 반복 시간에 충돌하지 않는다. UI 표시 시간은 기존 로컬 시간이다. 시간 역행에도 sequence가 대화 순서를 결정한다.
- `HistoryCursor`는 key, **snapshot revision/상한 sequence**, exclusive beforeSequence를 포함한다. 첫 page에서 snapshot을 고정하고 이전 page 요청에 재사용한다. 최신 append는 과거 페이지 순서를 바꾸지 않는다. 이미 받은 id/sequence 중복은 거절/제거하고 페이지 간 누락은 오류로 알린다.
- `HistoryPage`는 chronological messages, nextBefore, hasOlder, snapshot 정보와 제한/오류 정보를 반환한다. 최신20개/이전20개가 기본이며 count 외 UTF8 바이트 상한도 적용한다. 한 메시지가 바이트 예산보다 크지만 기존4MiB 파일 제한 내에 있으면 단독 page 또는 별도 bounded 본문 읽기 계약을 명시한다. 조용히 잘라 저장하지 않는다.
- append의 id 재시도는 같은 payload면 idempotent, 같은 id/다른 payload는 충돌이다. 두 writer의 stale revision이 나중 저장으로 선행 기록을 덮지 못하게 한다. 서로 다른 대화는 독립 lock/트랜잭션을 사용한다. 단순 `Save(record.Messages)`를 새 전체 이력 overwrite로 연결하지 않는다.

## 파일 구조/원자 저장과 이전 버전

시간 구획마다 message 원문 Markdown을 보관하되, 같은 시간에4MiB가 넘으면 번호/sequence로 추가 part를 둔다. 새로운 chunk는 immutable generation 파일로 먼저 완성하고 검증한다. **manifest/index 포인터의 원자 교체를 마지막 commit 지점**으로 삼는다. 실패/중단 시 이전 manifest가 계속 완전한 이전 데이터를 가리켜야 한다. 아직 manifest가 참조하지 않은 orphan/temp는 읽지 않는다. 묵시적으로 기존 원본을 지우지 않는다.

새 목록은 작은 metadata/index만 읽는다. 한 manifest가 과도하게 커지면 bounded index page/날짜 구획으로 나누고 기존 read cap을 무작정 늘리지 않는다. 500개 제한은 대화 descriptor 수에 적용하며 시간 chunk 개수로 세지 않는다. 디스크 한도/쓰기 실패는 새 저장을 성공으로 보고하기 전에 알리고 기존 자료를 보존한다. 자동 삭제/보관기간은 새로 도입하지 않는다.

v1은 개별 메시지 시각이 없다. legacy 파일의 원본 byte hash와 메시지 순서를 고정하고 별도 staging에서 v2를 구성→개수/순서/원문/메타데이터 검증→manifest commit 후에만 전환한다. 작업이 끊기면 v1을 계속 읽거나 검증된 v2를 읽는다. 양쪽을 합쳐 같은 메시지를 중복 표시하지 않는다. import 재시도 ID는 원본 hash+ordinal 등 결정적 매핑으로 검증하되 원본을 변경하지 않는다.

기존 `GetPath(record)`·닫기/이름 변경/협업 GUID·외부 import API 소비자는 호환 adapter가 필요하다. 예전 앱도 읽는 recent100 v1 mirror를 남길 경우, 이전 앱이 mirror를 수정했는지 hash/revision으로 검출해 자동 덮어쓰기/조용한 archive 유실을 막는다. 최초 구버전→신버전 migration과 신버전→구버전 열기의 범위를 문서에서 구분한다.

## UI/요청/메모리 연계

메모리 recent ChatSession과 디스크 archive, 현재 표시 page를 분리한다. 과거 page를 `ChatSession.Messages`에 그대로 insert하면100개 cap/Save/AI context 및 FIFO sequence 장부가 꼬일 수 있다. 표시용 window에서 page를 이동하고 요청 문맥은 최근10/24000자를 유지한다. 총 이력 전체를 chats dictionary에 계속 축적하지 않는다.

다중 대화(#1)와 함께 저장/조회에는 요청 소유 key를 명시한다. 백그라운드 완료 시 `ActiveChat`/현재 provider에 저장하지 않는다. page 결과도 key+load generation을 재검증해 전환/닫기/Dispose 후에는 적용하지 않는다. 중복 top-scroll 요청은 한 개만 in-flight, read 실패 시 cursor/기존 문단/읽기 위치를 유지하고 재시도한다.

UI는 로드 뒤 message id와 문단 내부 offset으로 anchor를 보존하고, window trim도 같은 기준을 사용한다. 사용자가 과거를 읽을 때 새 답변이 도착하면 최신으로 강제 점프하거나 과거 page 전체를 다시 렌더하지 않는다. UI 최대 메시지/문자, page 바이트, read cache와 pending append를 각각 제한한다. 상한에서 새 메시지를 먼저 메모리에서 버린 후 디스크 저장을 시도하면 안 된다. durable append 또는 bounded pending append의 성공/실패 확인 후 eviction한다.

## 테스트 준비물과 실행 범위

- `Cases.json`: 시간경계/이전형식/페이지/원자성/소유권/메모리/경로 방어 사례. 각 제품 관측과 기대를 명시한다.
- `New-StorageFixtures.ps1`: 새 격리 폴더에 legacy 원문·명시 시간 메시지 fixture와 기대 id/page 집합을 만든다. 제품이나 사용자 자료를 읽지 않는다. **현재 단계에서는 실행하지 않았다.**
- `StorageContract.Assertions.ps1`: 실제 구현 adapter가 반환할 id/sequence/UTC bucket/page 집합 검증 helper. 자체 계산만으로 제품 성공을 주장하지 않는다. 실제 API binding과 후보 DLL 고정 전 미실행이다.

실제 검증에서는 old chunk 하나를 열 수 없게 고정해도 summary/최신 page가 성공하는지 확인하고, read observer로 목록/첫 page가 전체 archive를 읽지 않았는지 측정한다. TaskCompletionSource 제어 reader로 전환/취소/역순 완료를 검증한다. 파일 잠금·실패 지점으로 commit 이전/이후 재시작을 시험한다. fixture/하네스 문제와 제품 결함을 구분해 최초 실패를 보존한다.

Root가 명시적으로 제품 소유권을 인수하기 전에는 준비된 설계/테스트만 유지한다. 최종 완료는 Code/Root 통합, 감사, 4 SKU 빌드·실제 IDE·최신 Release 제작·공개 배포·익명 다운로드 해시까지 포함한다.
