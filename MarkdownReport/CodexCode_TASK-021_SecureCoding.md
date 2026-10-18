# TASK-021 저장·첨부·소스 적용 시큐어 코딩 검토

- 작성: CodexCode
- 확인 시각: 2026-10-18 19:02 KST
- 근거: Role.md 기본 규칙 9번 및 부모 에이전트가 지정한 검토·수정 범위
- 현재 상태: 확인한 두 경계의 수정 및 독립 소스 fixture 검증 완료. 후속 실제 후보4 DLL의 SecureCoding 22/22도 통과했다. IDE와 운영 서버의 검증 범위는 별도로 구분한다.

## 범위와 증거의 구분

`MarkdownWorkspaceStore`, `AssistantSettingsTransaction`, `DocumentAttachmentReader`, `DocumentWorkerLauncher`, `ConversationCollaboration`, `DocumentSnapshot`, `EditProposal`, `SourceEditSession`, `VisualStudioDocumentService`의 실제 소스를 읽었다. 설정 거래를 확인하기 위해 `ChatWorkspace.cs`의 `AssistantSettingsStore.Load/Save`도 포함했다.

기존 workspace·transaction·collaboration·document·editing 검사의 경계 사례를 읽고 대조했다. 기존 검사를 읽은 사실을 이번 실행 통과로 계산하지 않았다. 이번에 실제 실행한 검사는 아래의 22건과 37건이다. 대규모 전체 검사·공유 제품 빌드·Git·실계정 조회·네트워크·실제 IDE 대형 파일 검사는 수행하지 않았다. Role.md 및 image-23을 포함한 사용자 원문은 수정하지 않았다.

사용자가 명시적으로 선택하는 전체 액세스 기능 자체는 취약점으로 분류하지 않았다.

## 확인한 문제와 수정

### SC-021-S01 — 디스크 해시의 무제한 읽기

- 중요도: 중간, IDE 가용성.
- 실제 경로: `VisualStudioEditorDocument.Capture`는 UI 스레드에서 편집기 버퍼의 길이를 확인한 다음 디스크 스트림 전체에 `SHA256.ComputeHash`를 호출했다. 디스크 바이트 수에는 상한이 없었다.
- 재현 조건: 작은 파일을 편집기에 연 뒤 외부 작업이 같은 경로의 디스크 파일을 크게 바꾸거나 계속 추가하는 동안 Generate·Apply·Save 등의 캡처가 실행된다. 버퍼 길이 검사는 새 디스크 크기를 제한하지 못한다.
- 영향: 해시 계산의 읽기 양이 통제되지 않아 UI 응답이 지연될 수 있다. 실제 IDE에 큰 파일을 주입하거나 장시간 정지시킨 실행은 하지 않았다. 기존 코드의 경로와 상한 누락은 정적으로 확인했다.
- 수정: `ComputeBoundedDiskVersion(Stream)`를 추가했다. 초기 길이가 2MiB를 넘으면 읽기 전에 거부하고, 길이를 조회할 수 없거나 읽는 도중 커지는 스트림도 최대 `2MiB + 1`바이트만 읽은 뒤 거부한다. 정상 파일의 Base64 SHA-256 계약을 유지하며 원문에 쓰거나 호출자 스트림을 닫지 않는다.
- 호환 범위: 200,000자 소스의 정상 UTF-8·UTF-16·UTF-32와 적용 결과 400,000자에 여유가 있는 바이트 상한이다.
- 검증: 실제 생산 helper를 호출한 합성 스트림 12건 통과. 정상 인코딩, 정확한 경계, 초기 초과, 성장 스트림, 비탐색 무한 스트림, 짧은 읽기, 읽기 오류, 빈 입력과 소유권을 확인했다.

### SC-021-S02 — 같은 바이트의 후속 설정 저장을 되돌리는 ABA 경쟁

- 중요도: 중간, 로컬 설정 무결성.
- 실제 경로: `AssistantSettingsTransaction.SaveCore`의 JSON 스냅샷 → JSON 저장 → Markdown 저장 실패 → JSON 롤백. 롤백 전 바이트 비교는 있었으나 거래 전체를 직렬화하지 않았다.
- 재현 순서:
  1. 첫 프로세스가 새 설정 A의 JSON을 저장한다.
  2. 두 번째 프로세스가 동일한 A로 공개 `Save`를 호출해 JSON과 Markdown 저장에 성공한다.
  3. 첫 거래의 Markdown 단계에서 IO 오류가 발생한다.
  4. 현재 JSON 바이트는 여전히 A이므로 첫 거래가 이전 JSON으로 롤백한다. 두 번째 거래가 성공했다고 반환했어도 JSON은 이전 값, Markdown은 새 값이 된다.
- 수정 전 실행 증거: [secure-aba-77e056c894/result.json](../artifacts/task021/secure-aba-77e056c894/result.json). 실제 후보 DLL과 파일, PowerShell 두 프로세스를 사용했다. 첫 거래의 Markdown 경계에서 순서와 `IOException`을 제어한 fault injection이다. 임의의 실제 디스크 장애를 자연 발생시킨 검증으로 표현하지 않는다.
- 수정 전 사용 DLL SHA-256: `18AE15D0A8F6F9604E3E18A4DB81B1C813A0C37E744EF77A6D49FD65901C7570`.
- 관찰 결과: 두 번째 저장 성공 표식이 있는데 최종 JSON은 `old.exe`와 이전 규칙, Markdown은 새 규칙이었다.
- 수정: 설정 파일의 정규화한 절대 경로를 SHA-256으로 구분하는 `AcquireSettingsLock`을 추가했다. 같은 Windows 세션의 협력 프로세스가 snapshot부터 Markdown·롤백까지 같은 named Mutex를 사용한다. `Store.Load/Save`도 같은 잠금을 사용해 중간 상태 읽기와 직접 저장의 끼어들기를 막는다. 같은 스레드의 중첩은 허용하며 5초 대기, abandoned 획득, 정상·예외 해제를 처리한다.
- 기존 보호 유지: 외부 비협력 작성자의 다른 바이트를 발견하면 복구를 중단하는 기존 검사를 유지했다. `Store.Save`의 `MarkdownWorkspaceStore.CommitTemporaryFile(temp, FilePath)` 호출도 보존했다.
- 관련 상한 보강: `Store.Load`는 실제 스트림에서 UTF-8 최대 64KiB만 읽고 BOM을 해석한다. `Store.Save`는 직렬화 이후 UTF-8 바이트가 64KiB를 넘으면 임시 파일 쓰기 전에 거부한다. 손상된 UTF-8·JSON 오류의 표시 메시지에 원문 조각을 넣지 않는다.
- 수정 후 검증: 실제 두 프로세스의 동일 바이트 저장이 첫 거래 종료까지 대기하고, 그 후 성공한 JSON·Markdown이 함께 남는 것을 확인했다. 중첩·5초 대기·abandoned·경로 독립성·읽기 대기 및 크기·BOM·표시 오류 검사도 통과했다.

## 나머지 범위에서 확인한 보호

| 영역 | 읽고 확인한 보호 | 이번 검토의 실행 범위 |
|---|---|---|
| 대화 Markdown | ID 32자리 검증, 정해진 Provider 하위 경로, reparse 거부, 4MiB 파일/100개 메시지/500개 파일 상한, 길이 기반 본문 경계, 같은 디렉터리 임시 교체 | 관련 소스와 기존 control 사례 읽기. 이번에 52건 전체를 재실행하지 않음 |
| 첨부 본문 | 파일 16MiB, 본문 UTF-8 100KiB, ZIP 항목·해제 크기·경로 검증, DTD 금지·XmlResolver 없음, 외부 관계를 가져오지 않음 | 관련 소스와 XXE/ZIP/외부 관계/IFilter timeout·취소 control 읽기. 실제 필터 전체 재실행 안 함 |
| 네이티브 문서 worker | 고정 배포 위치 exe, 쉘 없이 인수 인용, GUID 임시 디렉터리, 원본 사본 read-only, 15초 대기·job 메모리/종료, finally 정리 | 소스·기존 worker fixture 읽기. native 필터 메모리 안전성 fuzzing을 수행하지 않음 |
| 협업 | 주소와 수신자·상태 검증, 팀·열림 상태 검사, 명시 선택 텍스트만 공유, 4MiB 및 요청·회신·프롬프트 상한, named Mutex와 원자 교체 | 소스와 기존 collaboration control 읽기. 협업 전체 재실행 안 함 |
| 수정안·적용 | 엄격한 변경 JSON, 파일·응답·교체 개수 상한, 고유 앵커와 겹침 검사, 승인 필수, 전체 버퍼·경로·디스크 버전·읽기 전용 재검사, 별도 저장 | 소스 및 기존 editing/selection control 읽기. 이번 신규 실행은 해시 helper와 설정 경계에 한정 |

위 경로에서는 이번 검토 범위 안에서 추가로 확정한 승인 우회·경로 이탈·외부 URI 실행 경로를 찾지 못했다. 사용자 입력 메시지·명시 공유 텍스트는 저장 대상이며, 저장 모델에 별도 자격증명 필드나 첨부 바이너리를 추가하지 않았다.

## 실제 실행 결과

| 검사 | 결과 | 근거 |
|---|---|---|
| 신규 SecureCoding | 22/22 | [result.json](../artifacts/task021/secure-2108c20e82/result.json), [컴파일 입력](../artifacts/task021/secure-2108c20e82/inputs.json) |
| 기존 SettingsTransaction | 37/37 | [result.json](../artifacts/task012/settings-transaction-5b0e80fe8dd24883895e03519ed69150/result.json), [실행 어셈블리 근거](../artifacts/task012/settings-transaction-5b0e80fe8dd24883895e03519ed69150/assembly-evidence.json) |

두 검사는 이번에 변경한 실제 생산 소스와 그 의존 소스를 격리 컴파일한 `SecureCodingChecks.exe`를 사용했다. 기존 37건도 `-AssemblyPath`로 그 동일 어셈블리 사본을 참조했다. UI를 포함한 최종 제품 DLL을 검증한 결과로 계산하지 않는다. 실계정·네트워크·공유 bin/obj·사용자 문서를 사용하지 않았다.

`Test-Task021.ps1`에는 여섯 번째 `SecureCoding` 그룹을 추가했고, 이후 별도 담당자의 `SecureExecution`과 `WorkerOutput`을 7·8번째로 연결했다. 여덟 스크립트의 AST·인수 계약만 확인했다. 이 묶음 전체와 WPF 검사는 이번 보안 작업 중 실행하지 않았다.

## 소스 고정과 한계

다음 해시는 19:02 독립 실행 시점의 컴파일 입력이다. 이후 DocumentService의 전체 액세스 문맥/솔루션 폴더 연결이 추가됐으며 해시 helper는 유지했다. 최신 소유 소스 해시는 부모 작업의 `CodexCode_TASK-021_Validation.json`을 기준으로 한다.

- `VisualStudioDocumentService.cs` 최초 보안 보강 시점: `D1D3BE49F3E31864773A874425A0E9D271EB508505A78171723F2BF27DF7B369`
- `AssistantSettingsTransaction.cs`: `56639555F425A32A747EC792F58FD0364505C0FBAA9FCBCEC31C841D3FBF7092`
- 독립 컴파일 당시 `ChatWorkspace.cs`: `BF95ACB7EAE3C512FC794C9D702EC1CC5BFB5381A61CDBD16C1EDAB78F9CC567`. 같은 공유 파일의 다른 담당자 변경으로 이후 전체 해시가 달라졌다. 보강한 Load/Save 본문은 유지됨을 읽어 확인했으며, 최종 제품 DLL에서 재검증해야 한다.

named Mutex는 같은 Windows 세션의 이 잠금 계약을 따르는 작성자 사이를 직렬화한다. 비협력 프로그램의 파일 변경을 운영체제 수준에서 완전히 배제하는 compare-and-swap은 아니다. 2MiB 해시 제한은 읽는 양의 상한이며 장치·네트워크 파일 시스템의 개별 IO 지연에 대한 시간 제한을 증명하지 않는다.

## 후속 실제 제품 검증 — 2026-10-18 19:27 KST

실제 배포 후보4 DLL `DEC74A499646CD5611CCA3C210F3B0DB9CEB428BC488F007BBD90A9BAE75E4B0`를 지정해 SecureCoding **22/22**를 재검증했다. 근거는 `artifacts/task021/bundle-candidate4-final/secure/result.json`이다. 부모 작업의 전체 wrapper는 8그룹·229/229이며 승인 계획/합성 SCM48과 문서 worker 출력12도 통과했다. 이때 실제 사용자 문서·계정·운영 서버를 사용하지 않았다. 기존 37건의 이번 독립 소스 성공은 위에 기록한 그대로 유지하며, 실제 최종 DLL에서의 기존 전체 회귀는 TASK-020 통합 결과로 확인한다.

## 후속 전체 액세스 문서 계약 보완

TASK-020과 합의한 `IFullAccessDocumentService.CaptureFullAccessContext()`를 구현했다. UI 스레드에서 모든 열린 문서의 `Saved` 상태를 확인하고 하나라도 미저장이면 파일 저장 안내와 함께 중단한다. 저장된 활성 텍스트 문서의 문맥은 선택적으로 캡처하며, 문서가 없거나 비텍스트일 때는 null을 반환해 솔루션·폴더 작업을 허용한다. 문서 서비스 상태를 확인할 수 없는 오류는 실행 허용으로 바꾸지 않는다. 기존 `GetActiveDocument` 계약은 유지했다.

- 이 보완 이후 서비스 SHA-256: `D802DD7669C3A05F50828B2FEDD5F9E3818B0A89B38122F8693D1F3BEEDA289D`.
- 새 interface를 포함한 실제 SDK 독립 컴파일 및 기존 선택 회귀: [38/38 결과](../artifacts/sel/51d20460d0/result.json).
- 같은 서비스 소스의 해시·설정 경계 재검증: [22/22 결과](../artifacts/task021/secure-4e9b26fe80/result.json).
- 위 검사는 실제 EnvDTE 문서 열림·미저장 동작을 실행한 검증이 아니다. 다른 미저장 파일 차단 및 활성 문서 없는 작업의 실제 IDE 검사는 TASK-020 담당자가 수행한다.
