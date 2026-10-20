# TASK069 C12 UX-16 실패 분리 진단

담당: CodexOrig 보조 개발 055 · 2026-10-21

C12 최초 Legacy 실제 IDE 실행 `artifacts/ide/run-42e3358f286045efb90b7b979bbd9c79`은 UX-16에서 중단됐다. 실패 뒤 보존된 JSON에는 잠금 실패 예정의 새 경로·instruction이 남고, Markdown에는 직전 공통 규칙이 유지되어 있다. 이 결과만으로 JSON 롤백 실패와 실패 이후 다른 쓰기를 구분하지 않는다. 실제 대화상자 오류 문구와 assertion 직전 네 조건이 기존 기록에 없어 원인은 미확정이다. Root가 동일 C12의 독립 기본 회귀에서는 UX-16 통과를 확인했다는 사실도 별도로 유지한다.

## 좁은 진단 변경과 검증

소유 변경은 `tests/ux/DialogChecks.cs`뿐이다. UX-16의 원래 assertion 줄·조건·기대는 그대로 두었다. 저장 클릭 직후/기존 assertion 직전에 다음을 `ux/ux16-lock-save-diagnostic.json`으로 기록한다.

- 독립 네 bool: DialogVisible, ErrorPrefix, JsonUnchanged, MarkdownUnchanged.
- ActualMessage와 StatusLabel, busy/disposed, Save 버튼 Enabled, 실제 activeSettingsDialog 일치 여부.
- JSON/Markdown의 잠금 전·판정 직전 SHA256과 읽기 오류.
- 기대 파일 경로와 실제 view의 settingsStore/workspaceStore 경로, 현재/원래 process 환경 경로.

설정 원문 내용은 artifact에 복사하지 않는다. 진단은 원문 읽기와 private artifact 쓰기만 하며 옵션·설정·제품 상태를 바꾸지 않는다. 진단 자체가 실패해도 원래 assertion을 대체하지 않는다. 잠금 전 byte SHA 두 개는 기존 baseline text 읽기 직후 캡처한다.

C12 Legacy 실제 DLL `FCDF176FFD0722F8F0B19CFD5F96E8895BDFA7E8CE7330F8EF36BE88BB56C7C7`을 참조한 private library 컴파일은 exit 0, 경고/오류 0이다. 실제 실행·WPF 창·IDE·OS 포인터는 사용하지 않았다. 컴파일 입력 사본과 결과는 `artifacts/task069/ux16-diagnostic055`에 보존했다. 변경 source SHA는 `83A693556FC6B0B608005763C6A0DB5B18E43956F23596EB4520DD36F7E43314`다.

## 환경과 저장 경계의 정적 검토

`DialogChecks.Run`은 process 환경 경로를 저장한 뒤 자기 UX 디렉터리로 바꾸고 그 상태에서 store와 view를 만든다. finally에서 원래 값을 복원한다. 두 store는 생성 시 환경 경로를 읽어 instance 경로에 고정한다. 따라서 기존 view가 환경 변수 변경만으로 자동 재지정되는 구조는 아니다. 별도 프로세스인 기본 회귀의 process 환경 변경이 이 IDE process 환경을 직접 바꾸지는 않는다. 다만 같은 IDE 안의 다른 지연 callback이나 생성된 view가 임시 환경을 읽을 가능성까지 배제하지 않아 실제 view/store 경로를 기록한다.

JSON 거래는 경로별 named mutex로 협력 저장자를 직렬화하고, 롤백 전 바이트가 이번 저장과 같은지 확인한다. 일반 commit은 일시 잠금을 최대 여섯 번 시도하지만 `AssistantSettingsTransaction.RestoreSnapshot`의 마지막 `File.Replace`는 일회성이므로 일시적인 복구 대상 잠금은 가능한 가설이다. 실제 catch 문구·Win32 오류가 없으므로 이번 원인으로 단정하지 않는다. 실패 설정 경로 길이는 126, rollback 경로 예상 길이는 172자라 이번 사례에 MAX_PATH 원인 근거는 없다.

최초 실패 원문·제품·다른 검사·PDF·Git/index를 수정하지 않았다. 진단 소스는 동결하여 Root의 다음 실제 IDE 실행에 인계했다. 추가 실제 실행과 최종 결과 판정은 Root 소유이며, 이 진단 준비를 UX-16 통과나 원인 해결로 보고하지 않는다.

## Root 첫 진단 재실행 관찰

Root의 C12 final2 Legacy `run-85abe3d038e843c39441c88bedf86b0d`에서 추가한 JSON을 읽어 확인했다. 네 bool은 모두 true, busy/disposed는 false, activeSettingsDialog와 기대/실제 store 경로도 일치했다. JSON 전후 SHA는 `F39B8F8F5E30106AA5336FE183D8920B99BDEA7D60CB04AE0C9ACF3398390CBD`, Markdown 전후 SHA는 `6BE74713396E44CD1D7F18B7D1B087718C513082B4335A17B9F1458F591FA4F5`로 각각 같았다. 실제 메시지는 예상한 Markdown Win32 32 잠금 실패(시도 6) 후 기본 설정을 원래 상태로 복구했다는 문구다. Root가 원래 UX-16 통과를 확인했다.

이는 같은 제품의 이번 재실행에서 복구 경로가 정상 동작했다는 관찰이다. 최초 실패의 원인을 확정하거나 진단 추가로 제품을 고쳤다고 주장하지 않는다. 두 실행 원문을 보존하고 검사 소스를 추가 수정하지 않는다. 전체 실제 IDE 결과와 감사 판정은 Root가 별도로 마감한다.
