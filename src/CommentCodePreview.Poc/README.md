# 수동 주석 → 코드 미리보기: 독립 core PoC

이 폴더는 최종 기능의 **검증 기반**이다. 최종 목표는 실제 Visual Studio의 열린 소스에서 주석을 요청하고, 실제 AI 응답을 받은 뒤, 사용자가 수락하면 그 편집 버퍼에 삽입하고 Undo하는 것이다. 이 core의 자체 검사는 고정 모의 공급자와 가짜 원자 에디터만 사용한다. 실제 VS·실제 AI·편집기 표시 기능의 완성을 뜻하지 않는다.

기존 제품 프로젝트/manifest/배포 경로에 이 구현자가 연결하지 않았다. 실제 editor/provider/UI adapter는 별도 담당자가 연결한다. 기준 연구는 `MarkdownReport/CodexOrig6_CommentToCode_Autocomplete_Research_20261107.md`이며 원본 보고서는 main 작업 트리에 있다.

## 파일과 공개 계약

namespace는 `CommentCodePreview.Poc`이다. 프로젝트/NuGet/VS SDK 의존성 없이 .NET Framework 4.6.2 및 C# 5로 private 컴파일한다.

| 파일/형식 | 계약 |
|---|---|
| `EditorContract.cs` / `EditorSnapshot` | 문서 ID·버전·ContextEpoch·UTF-16 커서/선택·내용 및 SHA-256·readonly/diff/closed/IME의 불변 캡처 |
| `IAtomicPreviewEditor` | `Capture()`, `CompareAndApply(expected,insertion,token)`, `CompareAndUndo(receipt)` |
| `IUndoReceipt` | 에디터가 소유하는 opaque receipt. 다른 에디터나 다른 편집에 재사용 금지 |
| `AtomicApplyResult` | `Success(nonNullReceipt)` 또는 `Rejected(typedReason)` |
| `AtomicUndoResult` | `Success()` 또는 `Rejected(typedReason)` |
| `CommentCodeRequest` | core가 생성한다. public `RequestId`, `Instruction`, `Snapshot`만 제공 |
| `ICommentCodeProvider` | `Task<string> GenerateAsync(CommentCodeRequest,CancellationToken)` |
| `MockCommentCodeProvider.cs` | 생성자에 넣은 고정 문자열만 반환한다. 실제 공급자 연결 없음 |
| `PreviewSession.cs` | `RequestAsync`, `GetStatus`, `GetPreview`, `Accept(long requestId)`, `Undo(long acceptedRequestId)`, `Cancel`, `NotifyContextChanged`, `Dispose` |

`RequestId`는 한 `PreviewSession` 안에서만 식별자다. UI는 해당 session 인스턴스와 ID를 함께 보관해야 한다. 이전 session의 버튼을 새 session으로 연결하지 않는다. `CodePreview.Text`는 명시 미리보기용 생성 문자열이고 `PreviewStatus`는 원문 없는 enum 상태다.

## 상태와 수동 동작

1. 생성자와 상태 조회는 공급자를 호출하지 않는다. 사용자 명령 하나가 `RequestAsync(editor, explicitComment, token)` 하나를 만든다.
2. 새 요청은 이전 제안/수락 receipt를 폐기하고 이전 공급자 토큰을 취소한다. 미완료 요청은 최대 4개로 제한한다.
3. 공급자 응답 후 문서를 다시 캡처해 모든 경계와 guard를 확인한다. 늦은 응답·새 요청·취소·변경된 문맥은 표시하지 않는다.
4. `Ready`에서도 본문은 불변이다. `Accept(preview.RequestId)`라는 별도 명시 호출만 삽입할 수 있다.
5. 삽입 위치는 캡처한 **Cursor**다. 선택은 고정된 요청 문맥이며 이 단계는 선택 내용을 교체/삭제하지 않는다.
6. 성공한 Accept는 한 번 소비되고, 동일 ID 재수락은 에디터를 다시 호출하지 않는다. 동일 session의 성공 receipt만 한 번 Undo할 수 있다.
7. 외부 편집 후에는 전체 preimage를 무조건 복원하지 않는다. FakeEditor는 정확한 postimage/버전/세대가 달라지면 Undo를 거절해 외부 변경을 보존한다.

실패 상태에서 자동 수락·재전송·저장·명령 실행은 없다. `SaveAll();` 같은 응답도 FakeEditor에는 문자열로만 삽입된다. 에디터 인터페이스 자체에 저장/명령 실행 API가 없다. 코드 생성물의 의미·컴파일·안전성은 이 PoC가 판정하지 않는다.

## 실제 editor adapter 연결 계약

- `DocumentId`는 경로로 사용하지 않는 opaque 식별자다. 원문이나 ID를 상태 문자열/통계/오류에 복사하지 않는다.
- `Version`은 실제 버퍼의 변경 버전을 사용한다. `ContextEpoch`는 커서/선택, 탭 전환(같은 문서로 돌아오는 경우 포함), readonly/diff/닫힘/IME 등의 관측 변화마다 단조 증가시킨다. 원래 값으로 복구해도 세대가 되돌아가면 안 된다.
- adapter는 외부 문맥 변경을 **변경 시점**에 `NotifyContextChanged()`로 전달한다. core가 수행한 자체 apply/undo 이벤트는 외부 변경으로 재전달하지 않는다. 알림을 놓쳐도 응답 재검증/atomic 경계가 불일치를 막지만, 이미 표시한 미리보기의 즉시 숨김은 알림 연결이 필요하다.
- `RequestAsync`는 `ConfigureAwait(false)` 뒤에도 `Capture()`를 호출한다. adapter의 Capture는 호출 스레드와 무관하게 UI thread에 안전하게 연결해야 한다. UI thread에서 `.Wait()`/`.Result`로 요청을 막지 않는다.
- **CompareAndApply는 한 원자 경계**에서 현재 스냅샷/guard/취소를 재검사하고 한 번만 삽입해야 한다. 읽기와 쓰기를 분리해 사이에 다른 편집이 끼게 하지 않는다. mutation 전에 postimage·receipt·삽입 범위가 유효한지 확인해야 한다. mutation 후 예외를 던지고 receipt 없이 반환하는 adapter는 계약 위반이다.
- core는 Accept/Undo 동안 session lock을 보유한다. atomic 호출 안에서 dispatcher pump, session 메서드의 동기 재진입, 다른 lock과의 역순 대기를 하지 않는다. editor 문맥 알림은 자체 변경의 origin을 구분해 바깥에서 처리한다.
- Cancel과 Accept가 경쟁하면 원자 경계에서 먼저 선형화된 동작을 따른다. atomic 직전 취소 토큰도 검사해야 한다. commit 후 도착한 취소가 자동으로 코드를 삭제하지는 않는다.
- 현재 Undo는 **가짜 fixture의 receipt 계약**만 검증됐다. 실제 VS의 ITextUndoTransaction, 실제 Ctrl+Z/Redo 및 다른 편집기 확장과의 상호작용은 별도 실제 검증이 필요하다.
- 공급자가 취소를 무시하면 session의 preview는 즉시 폐기되지만 그 Task의 종료까지 강제하지 않는다. 늦은 결과는 격리하고 미완료 수를 제한한다. 실제 provider adapter는 자체 timeout/취소/프로세스 수명과 데이터 전송 정책을 구현해야 한다.

## 문맥과 텍스트 경계

| 대상 | UTF-16 문자 상한 | UTF-8 byte 상한 |
|---|---:|---:|
| Snapshot 전체 Content | 65,536 | 131,072 |
| 명시 주석 Instruction | 2,048 | 8,192 |
| 생성 문자열 | 8,192 | 16,384 |

Snapshot은 전체 캡처 내용을 hash하고 정확한 문자열까지 비교한다. 현재 상한보다 큰 문서는 조용히 잘라서 같은 전체 문서인 것처럼 넘기지 말고 거절한다. 별도 제한 문맥 창을 지원하려면 전체 버퍼 식별/범위와 전송 문맥을 나눈 새 계약을 검토해야 한다.

한글·보충 평면 문자·combining/ZWJ 문자열을 보존한다. UTF-16 surrogate 쌍 또는 CRLF 중간의 커서/선택을 거절하며 unpaired surrogate/NUL/단독 CR은 거절한다. LF와 CRLF는 재정규화 없이 보존한다. 일반 grapheme cluster 전체를 원자로 취급하는 모델이나 실제 IME 상태 탐지는 구현하지 않았다. IME bool은 adapter가 제공하는 guard다.

실제 provider가 전송할 파일/문맥 범위·대상 공급자·사용자 고지·명시 동의는 adapter/UI 담당 범위다. 기존 FullAccess/셸/배포 권한을 이 모델이 암묵적으로 계승하거나 승인하지 않는다. 이 core에 새 계정·CLI·모델·상시 Tab 제안·자동 전송은 없다.

## Orig4 UI 연결 순서

`await RequestAsync` 완료 뒤에도 결과 ID가 현재 `GetPreview()`의 ID와 같은지 확인한 후 표시한다. 새 요청·취소·외부 문맥 알림에서 이전 표시를 즉시 비운다. 수락 버튼은 `(소유 session, 표시한 RequestId)`를 캡처하고 false 결과에서 자동 재시도하지 않는다. Undo도 성공한 수락 ID를 사용한다. 미리보기 표시가 실제 편집이거나 승인된 실행인 것처럼 보이게 하지 않는다. 실제 IntelliSense/Tab/Enter/Escape/IME, DPI/스크롤/접근성과의 연결은 후속 UI·SDK 작업이다.

## 자체 검사 실행

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/comment-code-preview-poc/Test-PreviewCore.ps1
```

테스트는 `tests/comment-code-preview-poc/artifacts/run-<nonce>/`에 원본/사본 SHA, private core DLL·가짜 fixture, 컴파일/실행 로그, process PID·종료/강제정리 여부, typed 결과를 남긴다. 기존 출력은 덮어쓰지 않는다. 실제 사용자 문서나 프로필을 읽고 쓰지 않는다. 실제 제품 빌드, SDK restore, VS/GUI, 계정/네트워크/CLI는 실행하지 않는다.

검사는 수동 요청 전후 본문 불변, 단일 수락/Undo, 오래된 UI ID, 각 snapshot/guard 변화, 새 요청·늦은 응답·취소, 공급자 오류/상한, Unicode·CRLF, 실제 두 thread의 중복 수락 및 외부 변경 경쟁, Undo 외부 편집 보존과 종료를 다룬다. 최초 94개 계약 결과를 보존했고 추가 경계와 정확한 private DLL 로드를 포함한 최종 검사는 **104/104 PASS, 7그룹 완주**였다(`tests/comment-code-preview-poc/artifacts/run-0399c959903d/`). 감사·연구 양쪽 검토와 실제 VS/provider 연결 완료는 별도 단계다. 고정 해시·한계는 `tests/comment-code-preview-poc/VALIDATION.md`를 참조한다.

## 작업 이력

- 2026-11-07 CodexOrig 보조064: 연구를 읽고 독립 core·mock·fake editor 및 비GUI 검증기 작성. 기존 제품 소스/csproj/manifest/배포 입력은 편집하지 않음. class/method 주석은 새 Role 정렬을 사용하며 기존 프로젝트 이력은 수정하지 않음.
- 전체 작업 소요는 별도 계측하지 않았다. 각 private compiler/runner 실행 시간과 종료 정보는 새 출력의 `*.process.json`에 기록한다. 중앙 Update_History/OWERORDER/배포 문서 반영은 소유 범위를 가진 Root에게 인계한다.
