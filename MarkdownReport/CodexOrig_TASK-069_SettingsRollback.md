# TASK069 R69-15 설정 복구의 유한 잠금 재시도

담당: CodexOrig 보조 개발 055 · 2026-10-21

공통 Markdown 저장 실패 뒤 JSON 원문 복구가 일시적인 파일 잠금 하나로 중단되는 경계를 보완했다. 기존 JSON이 있는 원문 교체와 처음 생성된 JSON을 취소하는 삭제를 같은 유한 재시도 경로로 묶었다. **소스 기반 전체 105/105 PASS**이며 최초 C12 UX-16 실패의 정확한 원인을 확정했다고 주장하지 않는다. 해당 실패 원문과 진단 재실행 결과는 그대로 보존했다.

## 제품 변경

변경 범위는 `AssistantSettingsTransaction.cs`의 SaveCore 복구 호출과 RestoreSnapshot뿐이다. SaveCore 비공개 reflection 서명은 바꾸지 않았다. 루프 밖의 중복 스냅샷 읽기를 제거해 JSON 읽기 자체가 잠겨도 재시도 경계 안에서 처리한다.

매 시도마다 현재 JSON 바이트가 이번에 저장한 expected와 정확히 같은지 다시 읽어 확인한다. 변경·삭제를 감지하면 복구를 거절한다. Win32 32/33/1175/1176/1224 잠금 오류만 최대 6회 시도하고, 사이의 25ms 대기는 최대 5번, 합계 125ms다. 다른 오류는 즉시 전달한다. 마지막 잠금 오류는 native 코드·시도 수와 원래 InnerException을 유지하고, SaveCore는 Markdown 원인과 복구 원인을 AggregateException으로 구분한다. 복구 temp 정리 실패가 원래 오류를 가리지 않도록 원래 오류의 Data에 추가 근거를 보존한다.

이는 감지된 외부 변경을 재시도로 덮는 방식이 아니다. 매회 원문 조건을 재확인하고, 조건을 만족하는 교체/삭제만 시도한다. 원래 JSON이 없던 경우에도 동일한 검사와 유한 File.Delete 재시도를 거쳐 없음 상태로 복구한다.

## 실제 잠금과 대조 검사

기존 37개 assertion을 수정하지 않고 새 시나리오 68개 조건을 추가했다. 최초 저장 상태 두 종류에 대해 FileShare.Read의 해제·지속·외부 writer, FileShare.None의 해제·지속을 검사해 총 10개 실제 잠금 시나리오다. 제품 test hook은 없다. 같은 거래 thread의 실제 native sharing 오류를 FirstChanceException으로 관찰한 뒤에만 별도 thread가 잠금을 해제하므로 첫 시도 이전 우연한 해제를 배제한다.

| 검사 | 결과 | 증거 |
|---|---:|---|
| 최종 소스 전체 | 105/105 PASS | `artifacts/task012/settings-transaction-444d059b75f74d498432c64c3800c5b8` |
| 수정 전 37개 assertion 원문 + 새 제품 소스 | 37/37 PASS | `artifacts/task069/rollback-retry055/original-assertions-final-product` |
| 구 제품 소스 + 새 Read/None·존재/없음 해제 검사 | 기대 실패 4/4 | `artifacts/task069/rollback-retry055/negative-old-product-four` |

37개 별도 대조를 105개 결과에 중복 합산하지 않는다. 구 제품 대조는 새 검사가 기존 단일 Replace/Delete의 복구 실패를 실제로 구분함을 보인다. 정상 해제는 첫 native 32 오류 한 번 뒤 원문/BOM 또는 없음 상태를 정확히 복구했다. 지속 잠금은 native 실패 6번, 측정 벽시계 약 162~169ms에서 종료됐다. 명시 대기 125ms에 OS scheduling·파일 I/O·관찰 비용이 포함된 값이며 벽시계 125ms를 보장한다고 보고하지 않는다. 두 외부 writer 시나리오는 대기 중 바뀐 원문을 보존하고 부분 저장/다른 작업 오류를 전달했다. 모든 경우 Markdown 원문과 temp 정리를 확인했다.

`Test-SettingsTransaction.ps1`의 소스 컴파일에는 현재 저장 partial helper와 필수 규칙/첨부 의존성만 추가했다. 기존 entry의 AssemblyPath 경로와 서명은 유지한다. 초기 source-only 컴파일의 첨부 helper 누락, 외부 writer가 쓰기 handle을 계속 보유해 읽기까지 잠갔던 테스트 준비 실패도 이전 log에 보존했다. 수정된 외부 writer 검사는 쓰기 handle을 닫고 읽기 공유 handle만 보유하여 실제 원문 비교 거부를 확인한다.

## 동결과 후속 통합

제품 SHA: `EA63F9C55D34F3F1B602B93013FECC6CB25CBDBA5CA8677414358252DFEF5B02`.
테스트 SHA: `CB19261FA58A51C763295DE610FCD5BF59533586FE717C547A425FFF7951B65E`.
진입 스크립트 SHA: `9123DB6C8B2EC9982FB444A965DED604B27E407FF6A5E9100D60F147FCCAA824`.

입력 사본·제품 diff·native 오류 횟수/시간/실패 문구는 `artifacts/task069/rollback-retry055`와 개별 retry-observation.json에 보존했고, 상세 집계는 `CodexOrig_TASK-069_SettingsRollback.json`이다. 개인 소스 컴파일/파일 I/O만 실행했으며 GUI·공유 bin/obj·Git/index·PDF·다른 제품 파일은 변경하지 않았다.

현재 결과는 실제 배포 DLL 검사가 아닌 소스 기반 검사다. Root가 C13 네 SKU 빌드/분석기 검사, 같은 105개 실제 DLL 검사, 기본 27개 회귀·실제 IDE 4개·감사·최종 설치기/Release·게시·익명 다운로드 SHA 검증을 이어가야 한다. 새 동결 이후 이 작업의 추가 제품 수정은 없다.
