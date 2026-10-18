# TASK-021 — 추가 이미지·시큐어 코딩 요구와 감사 보완

- 담당: CodexCode / 시작: 2026-10-18 18:18:33 KST
- 기준: [Role.md](Role.md)의 18:00 추가 1~9, 기본 규칙 9의 시큐어 코딩, [독립 감사 TASK-019](CodexOrig2_TASK-019_Audit.md).
- 최신 검토 원문 SHA-256: `5760D468A79F3A4478B8548333E30BF1397F661E02B5F2B4F6594879C12007EF`. 작업 중 추가된 image-23과 시큐어 코딩 요청, 다중 작업자의 update date 누적 예시까지 확인했다. 초기 `0758C189…BC05E`, image-23 추가 시점 `B7CE3145…439A3`와 최신 검토 사본을 각각 보존했다.
- 분담: TASK-021은 화면 요구 1~5·9의 생각 중 표시·감사 F-04/F-06와 협의한 저장/실행/문서 출력 보안을 담당한다. TASK-020 CodexOrig는 모델/계정/실시간 사용량/전체 액세스·공개 진행 이벤트·다중 IDE·전체 검사·외부 연동·문서/배포를 담당한다.
- 상태: 담당 구현·시큐어 코딩 보완 및 **후보4 실제 제품 8/8묶음·229/229·exit 0 검증 완료**. 최종 SDK17 재빌드·IDE 3버전·전체 실행기·문서/배포는 TASK-020에서 통합한다. 독립 소스 검사와 반복 실행을 실제 제품/IDE 성공 건수에 합산하지 않는다.

## 이미지 분석과 구현

| 요구 | 참조에서 확인한 내용 | 반영 |
|---|---|---|
| 1 | image-17의 Claude·Codex 탭 글자가 붙어 있음 | 탭 내부 여백을 늘리고 탭 사이 14px 여백 추가. 선택 밑줄 유지 |
| 2 | image-18/19의 총 변경량·파일별 `+` 초록/`−` 빨강, 수정 기록 확인 | 총계/파일별 계수에 다른 색상 적용. 실제 변경 줄의 색상 비교와 파일·줄 hover 기록, 실행한 여러 파일의 개별 비교 선택 추가 |
| 3 | image-20/21은 요청 글과 조작부가 한 면이며 첨부·권한은 왼쪽, 모델·전송은 오른쪽 | 테두리 없는 조작 버튼과 원형 전송 버튼, 한 개 모델·강도 선택 버튼으로 통합. 좁은 창은 조작 그룹이 다음 줄로 이동 |
| 4 | image-22와 본문은 메뉴 버튼 자체의 부채꼴과 상단/목록 버튼의 배경 통합을 요구 | 두 버튼 각각 환형 Geometry와 같은 Clip 사용. 사각 모서리·안쪽 구멍은 클릭되지 않음. 상단/목록 토글은 전용 투명 스타일 사용 |
| 5 | 설정과 첫 사용 설명이 짧음 | 계정·모델·공통 규칙·표시 24문단, 첫 사용 4카드 상세 37문단, 제공자별 CLI 절차 24문단. 기존 목차·그림·상세 이동·키보드 초점 복귀 유지 |
| 9 | image-23 빨간 영역은 실제 중간 작업, 노란 영역은 흰빛이 왼쪽에서 오른쪽으로 이동하는 Thinking | 기존 텍스트 위에 회색→흰색→회색 상대 그라데이션을 반복 이동. 기존 표시 API와 종료 복구를 유지. 공개 명령/파일/도구/메시지 이벤트는 TASK-020 런타임에서 연결하며 내부 추론은 표시하지 않음 |

색상은 실제 수정안에서 계산한 줄을 사용한다. 고유 앵커를 찾기 위해 포함한 공통 문맥은 회색으로 남긴다. `---`/`+++`로 시작하는 실제 소스도 hunk 안에서는 변경 줄로 판별한다. 빈 파일·전체 삭제·마지막 개행의 줄 계산은 같은 함수를 사용한다.

hover 기록은 **현재 창에서 발생한 최근 세 건**이며 대화와 파일을 구분해 최대 100개 파일 키를 보관한다. 원문 경로, 기록 시각, 검토안/실제 파일 변경 구분, 줄 증감과 실제 비교 조각을 표시한다. 재시작 뒤에도 유지되는 영구 변경 이력으로 표시하지 않는다. 긴 비교는 표시량을 제한하고 전체 원본·수정안 또는 실행 기록을 확인하도록 안내한다.

## 감사 항목과 상태 정확성

- **F-04:** IDE 도구 창 Caption도 `Claude · Codex`로 바꿔 본문과 IDE 탭 모두 `Chat` 문구를 제거했다. 실제 IDE 검사 기준도 이 제목에 맞춘다.
- **F-06:** 전체 원문/디스크 해시를 유지하면서 선택 원문과 줄·열을 별도 캡처한다. DTE의 줄바꿈 문자 계수와 CRLF 원문의 UTF-16 오프셋 차이를 처리한다. 연속 선택·역방향을 지원하며 사각형/가상 공백·불일치 선택은 요청 시 명시 오류로 처리한다. 커서 이동만으로 적용·저장·복구가 실패하지 않도록 전체 원문의 충돌 검사와 분리했다.
- **F-01/F-02/F-03/F-05:** TASK-020 통합 범위다. VS2019·2022 설치 지원, 최초 전체 검사 실패 원인, 실제 IDE 라이선스 상태, 실제 Claude 및 외부 서버 검증은 각각 실행 근거로 판정한다. 감사의 첫 `12/14 exit 1` 기록과 원문을 보존한다.

추가 상태 검토에서 ‘A 적용 → B의 미적용 제안 → 제안 제거’ 때 전역 복구 세션 때문에 B를 적용 완료로 잘못 표시할 수 있음을 발견했다. 실제 적용 성공 시에만 수정안/소유 대화를 기록하고, 같은 카드의 원본·수정안·저장·복구 대상을 일치시켰다. 이미 실행한 전체 액세스 변경 카드에는 수정안 승인/적용 버튼을 표시하지 않는다.

## 검증 근거

| 검사 | 독립 소스 결과 | 제품 통합 |
|---|---|---|
| 선택 영역·충돌·저장·복구 | 38/38, `artifacts/sel/9ae26cb7c2/result.json` | 후보4 38/38, `bundle-candidate4-final/selection/result.json` |
| 실제 부채꼴 기하/클릭/200px/큰 글꼴/반복 hover/종료 | 겹침 회귀 강화 후 구소스 실패→보정 소스 21/21 | 후보1 겹침 실패 → 후보4 21/21, `bundle-candidate4-final/fan/result.json` |
| 상세 콘텐츠 | 14/14, `artifacts/task021/help-content-d8f622b8abde4b4ea5271718b9f3db42/result.json` | 후보4 14/14, `bundle-candidate4-final/help/result.json` |
| 안내 상세·뒤로 초점·목차 | 23/23, `artifacts/task017/dialog-guide-1436e16922344723a1ceb99f2436a386/result.json` | 전체 통합 회귀는 TASK-020 범위 |
| 변경 줄/계수 회귀 | 18/18, `artifacts/diff/e6375b2f11/result.json` | 후보4 18/18, `bundle-candidate4-final/diff/result.json` |
| 이미지 후속 실제 화면 | 고정 제품 전용 | 후보2 Thinking 실패 → 후보3 55/55 후 시각 검토로 선택 누락 발견 → 후보4 56/56, `bundle-candidate4-final/images/result.json` |
| bounded 디스크 해시·설정 거래·설정 크기/오류 | 22/22, `artifacts/task021/secure-2108c20e82/result.json` | 후보4 22/22, `bundle-candidate4-final/secure/result.json` |
| 기존 설정 거래 호환 | 37/37, `artifacts/task012/settings-transaction-5b0e80fe8dd24883895e03519ed69150/result.json` | 전체 통합 회귀는 TASK-020 범위 |
| 승인 계획/실행 경로·Git 환경·취소/timeout | 48/48, `artifacts/task021/secure-plan-after-final/result.json` | 후보4 48/48, `bundle-candidate4-final/secure-execution/result.json` |
| 고정 4KiB stdout/stderr 배수·실제 합성 worker | 12/12, `artifacts/task021/worker-output-f5ba92a2a0474ebc81519fcf7b515af8/result.json` | 후보4 12/12, `bundle-candidate4-final/worker-output/result.json` |

위 후보4 상대 경로의 기준은 `artifacts/task021/`이다. 동일한 고정 DLL의 [검증 JSON](CodexCode_TASK-021_Validation.json)과 `bundle-candidate4-final/summary.json`에 8그룹·229건을 기록했다. 실제 원격 AI·운영 서버·IDE 검사는 이 229건에 포함하지 않는다.

부채꼴의 420px/200px/28 글자 PNG를 직접 열어 표시를 확인했다. 독립 안내의 일반/상세/320px 이미지는 담당 작업자가 직접 확인했다. 공유 `bin/obj` 빌드와 배포는 TASK-020 단독 소유로 충돌을 피한다.

후보1 DLL SHA-256은 `18AE15D0A8F6F9604E3E18A4DB81B1C813A0C37E744EF77A6D49FD65901C7570`이다. 680px/200px 입력란과 색상 비교·여러 파일 PNG를 직접 확인했다. 같은 후보의 캡처 위치를 바로잡은 재실행은 같은 42개 검사이며 중복 합산하지 않는다. 최초 하네스의 존재하지 않는 디스크 해시 API 가정은 실제 fixture 파일 SHA-256 계산으로 고쳤다.

실제 부채꼴 검사에서 200px 축소 시 trigger 하단 42.62px에 대해 고정 40px만 제외해 클릭 영역이 겹침을 확인했다. 실제 trigger hit도 있는 `(144,41)`에서 Help 버튼까지 동시에 hit되는 결과를 `artifacts/task021/fan-candidate1-actual-hit/result.json`에 보존했다. 실제 trigger를 Fan 좌표로 변환한 경계에 4px 여유를 두고 Clip에서 제외하도록 보정했다. 강화한 소스 검사 전후 결과와 후보2의 실제 WPF 21/21 결과를 각각 보존했다.

시각 검토로 추가 확인한 탭 줄바꿈의 Claude/Codex 순서 역전은 한 줄 수평 스크롤로, 입력란 내부의 별도 검은 면은 투명 배경으로 보정했다. 긴 절대 경로가 파일명을 가리지 않도록 파일명을 먼저 표시하고 정확한 전체 경로와 탐색 위치는 유지했다.

후보2에서 Thinking의 내부 브러시·시계는 움직여도 글자의 실제 Foreground는 상속 단색으로 남는 제품 결함을 확인했다. 단순 순서 변경으로는 해결되지 않았다. 원래 local 값이 없는 상속/스타일일 때만 임시 local 브러시를 연결하고 종료 시 ClearValue로 원래 값 출처를 복원했다. 기존 local/binding/resource는 SetCurrentValue를 유지한다. `artifacts/task021/thinking-fixed-348d373bd4a84c2082913fec451619cc/probe.json`의 실제 수정 소스 probe에서 다섯 출처의 연결·Freezable 변경·중복 시작·원래 값/출처 복귀·후속 원본 변경·Dispose 정리를 확인했다. 이 메모리 결과는 실제 시간별 화면 이동 검사를 대신하지 않는다.

후보3 `375B55507A948687E10098FE36F6826AE85D6BFF7C839F05D310B2EA5E3BAE43`의 첫 묶음 실행은 8/8·228/228·exit 0이다. `artifacts/task021/bundle-candidate3/summary.json`에 각 프로세스 결과와 SHA를 보관했다. 실제 시간별 Thinking 이동도 통과했고 큰 글자 PNG를 직접 확인했다. 이후 여러 파일 PNG에서 두 번째 파일을 선택하면 첫 파일로 돌아갈 행이 사라지는 검사 누락을 발견했다. 선택 파일을 제외한 나머지 행을 재구성하고 선택을 보존하도록 수정했다. 첫 파일의 diff/계수/탐색 위치로 복귀했다가 다시 두 번째로 가는 회귀 1건을 추가했다. 앞선 228개 성공을 이 새 조건의 성공으로 대신하지 않는다.

마지막 **후보4 DLL SHA-256은 `DEC74A499646CD5611CCA3C210F3B0DB9CEB428BC488F007BBD90A9BAE75E4B0`**이며, 그 실제 사본으로 56개 UI와 나머지 7그룹을 포함해 229/229 통과했다. 200px 입력/부채꼴, 여러 파일 선택, 색상 diff와 큰 글자 Thinking PNG를 직접 열어 확인했다. GUI 프로세스 종료도 통지했다.

후보4 전달과 동시에 원본 공유 `bin/obj`를 사용하지 않는 별도 55파일 스냅샷 빌드도 완료됐다. 독립 Release 0오류/0경고·229/229는 `artifacts/task021/isolated-candidate4/`와 `bundle-isolated-candidate4/`의 별도 자료이며, 위 실제 후보4 229건에 다시 더하지 않는다. 최종 빌드/배포 소유는 TASK-020으로 유지했다.

후보4 이후 SDK17 분석기의 순수 WPF/조건부 VS COM 오인 3건에 대해서만 2개 소스의 정확한 base 초기화/await 한 줄에 근거 pragma와 설명 주석을 추가했다. 실행 코드·COM guard·ConfigureAwait(false)·예외 관찰은 바꾸지 않았다. 이 비동작 변경의 최종 SDK 재빌드와 최종 패키지 전체 회귀는 TASK-020이 확인한다.

추가 읽기 검토에서 새 런타임의 연결 전환/늦은 이벤트, 취소 분류, 입력 선검증, JSON-RPC 요청·응답 구분, 누락 사용량, 오류 메시지 저장 경계를 담당 작업자에게 전달했다. 이 검토는 작성 중 소스의 관찰이며 수정 완료 여부는 후속 고정 제품 검사로 별도 확인한다.

## 시큐어 코딩 보완

[저장·소스 경계 상세 보고서](CodexCode_TASK-021_SecureCoding.md)에 재현 조건과 한계를 기록했다. 실제 두 프로세스에서 같은 설정을 저장할 때 첫 거래의 실패 복구가 나중 성공을 되돌리는 ABA를 재현해, 경로별 named Mutex로 전체 거래와 읽기/직접 저장을 직렬화했다. 디스크 해시는 최대 2MiB, 설정은 실제 UTF-8 64KiB로 제한하고 원문이 포함된 JSON 오류 노출을 줄였다. 원자 파일 교체와 비협력 작성자의 다른 바이트를 보존하는 기존 방어는 유지했다.

SCM 작업의 `C:file.cs` 같은 드라이브 상대 경로가 CWD 변화에 따라 승인 시와 실행 시 다른 대상을 가리키는 문제도 합성 조건으로 재현했다. 드라이브/루트 상대·장치·ADS 경로를 거부하고, 승인한 요청을 깊은 복사하여 고정한 뒤 실행 직전 정책·대상·실행 파일·인수를 재검증한다. `GIT_COMMON_DIR`·`GIT_NAMESPACE`도 하위 프로세스 환경에서 제거한다. 수정 전 두 실패와 수정 후 48개 성공을 분리 보관했다. 운영 SCM·서버에 명령을 보내지 않았다.

전체 액세스 실행 전 문서 검사는 `IFullAccessDocumentService.CaptureFullAccessContext()`로 제공한다. 열린 모든 문서의 미저장 상태를 먼저 확인해 차단하고, 사용할 활성 텍스트 문서가 없으면 null을 반환한다. 따라서 소스를 열지 않은 솔루션 폴더 작업도 가능하다. 기존 단일 수정안의 활성 소스 필수 계약은 유지했다. 실제 DTE의 미저장 다른 파일·소스 없음 검사는 TASK-020 IDE 검사로 확인한다.

문서 worker의 버리는 출력은 줄이 끝날 때까지 누적하는 이벤트 대신 4KiB 고정 버퍼로 stdout/stderr를 읽는다. 8MiB씩 개행 없는 두 파이프를 보내는 실제 합성 worker와 동기/비동기 스트림으로 검증했다. 15초 제한, 작업 프로세스 메모리/종료, 결과 프로토콜과 기존 worker exe는 유지했다. `Process.Start` 이후 job 연결 전의 자식 생성 경합까지 완전히 차단했다고 주장하지 않는다.

## 재현 방법

최종 고정 제품 DLL을 지정하여 신규 8개 그룹을 서로 다른 PowerShell 프로세스에서 순차 실행한다. `artifacts` 아래에는 이전 결과와 겹치지 않는 새 디렉터리를 사용한다.

```powershell
powershell.exe -NoProfile -NonInteractive -STA -ExecutionPolicy Bypass `
  -File tests/task021/Test-Task021.ps1 `
  -AssemblyPath <고정 제품 DLL 경로> `
  -OutputDirectory artifacts/task021/<새 결과 디렉터리>
```

`summary.json`에서 8개 그룹의 exit code와 각 JSON·stdout·stderr, 입력 DLL SHA를 확인한다. 계정 탐색과 SCM 자동 조회는 격리하며, 실제 AI·IDE·운영 서버 검증을 대신하지 않는다. WorkerOutput은 검사 전용 복사 DLL 옆에만 합성 worker를 만들고 배포용 worker는 변경하지 않는다. SecureExecution은 합성 native exe만 실행한다.

## 주요 파일

- 화면: `Views/AssistantControl.xaml`, `AssistantTheme.xaml`, `AssistantControl.Changes.cs`, `AssistantControl.Fan.cs`, `AssistantControl.Dropdowns.cs`, `AssistantControl.Experience.cs`, `AssistantControl.Dialogs.cs`
- 설명: `Views/AssistantDialogs.cs`, `Views/AssistantHelpContent.cs`
- 생각 중 표시: `Views/AssistantControl.Thinking.cs`
- 선택/비교: `Editing/DocumentSnapshot.cs`, `Editing/EditProposal.cs`, `Services/VisualStudioDocumentService.cs`
- 보안: `Chat/AssistantSettingsTransaction.cs`, `Chat/ChatWorkspace.cs`의 Store.Load/Save, `Chat/DocumentWorkerLauncher.cs`, `Services/WorkspaceActionService.cs`, `Views/WorkspaceActionsDialog.cs`
- IDE 제목: `ToolWindows/AssistantToolWindow.cs`
- 신규 검사: `tests/task021/`

모델 목록·사용량·권한 설명은 [OpenAI App Server 공식 문서](https://learn.chatgpt.com/docs/app-server) 및 실제 연결 결과를 따른다. 요금제 이름만으로 모델을 임의 차단하거나 제공되지 않은 토큰 수를 추정하지 않는다.

담당 작업 종료: 2026-10-18 19:31:39 KST / 소요 01시간 13분 06초. 소유 제품·검사·보고 편집과 하위 작업자를 종료하고 최종 전체 빌드/IDE/릴리스 검증은 TASK-020에 인계했다.
