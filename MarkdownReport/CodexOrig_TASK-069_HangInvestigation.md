# TASK069 #5 VS2017 멈춤 조사

담당: CodexOrig 보조 개발 055 · 2026-10-20

사진 `image-72.png`의 2026-10-20 16:46 멈춤 원인은 **확정하지 못했다**. 사진은 정지 시점의 호출 스택이나 대기 소유자를 제공하지 않고, 제목에도 응답 없음 표시는 없다. 별도 무창 검사에서는 확장의 UI 스레드가 협업 파일 잠금을 최대 5초 동안 기다리는 결함과 변경 없는 담당자 조회가 파일을 재저장하는 동작을 확인하여 보완했다. 이 재현을 사진 속 사고의 직접 원인이라고 간주하지 않는다.

## 보존한 근거

- Application 이벤트 18건, WER 보고서 10개, 기존 VS2017 설치 ActivityLog/스타일시트와 프로세스 응답 여부·작업공간 파일 크기 요약을 `artifacts/task069/diagnostics055`에 보존했다. 원문에는 로컬 경로 등이 있어 개발 증거로만 관리하며 사용자 배포에 넣지 않는다.
- 16:46 전후 30분에는 확인한 필터의 devenv 충돌·멈춤 기록이 없었다. 당일 02:36의 RequestQueueRunner 및 13:29의 ConcurrentClaudeFixture 예외는 테스트 실행기 사건이다. 14:13 이전의 devenv 기록은 VisualStudioNonFatalErrors2/System.IO.IOException이며 해당 사진과의 연결은 없다.
- 발견한 ActivityLog XML은 10월 18일 설치 로그이다. 함께 복사한 XSL은 표시 스타일시트이며 실행 중 IDE 로그가 아니다. 해당 시각의 런타임 ActivityLog와 덤프는 발견하지 못했다. 로그 부재가 실제 멈춤이 없었다는 뜻은 아니다.
- 22:01의 두 devenv 프로세스는 응답 중이었다. 하나는 17:21에 시작하여 사진 시각 이후 프로세스이다. 현재 응답 상태로 과거 상태를 판단하지 않는다.
- 사진 출력 창에는 XAML 디자이너 ShadowCache/.NET sandbox 관련 경고가 보이지만, 이 경고 역시 멈춤 원인으로 확정할 수 없다. 고객 프로젝트·IDE 설정·실행 프로세스를 변경하지 않았다.

## 재현과 보완

`artifacts/task069/hang055/baseline-result.json`은 이전 TASK068 C5 실제 제품 DLL(SHA256 `AD6327BB58CEBF0E362B90134F9DFB803CDB566B457452AC1D17C7C999E66E02`)을 직접 참조한다. 공개 배포된 최종 C6 DLL과는 구분한다. 별도 스레드가 해당 개인 작업공간의 named mutex를 소유하면 UI에서 EnsureAgent가 **5002ms** 걸렸고 대기 중 Dispatcher heartbeat가 0, 반환 뒤 1이었다. 변경 없는 EnsureAgent도 파일 수정 시각을 변경했다. 창 표시는 사용하지 않았다. 수정 직전 현행 소스에서도 동일한 동기 5초 대기와 무조건 저장 코드를 확인했다.

- 기존 한 인자 생성자와 기본·백그라운드 호출의 5초 유한 대기를 보존했다. UI가 주입한 정책에서는 잠금을 기다리지 않고 식별 가능한 IOException 파생 오류로 반환한다. 잠금을 획득하기 전에 실패하므로 협업 파일이나 메시지를 변경하지 않는다.
- EnsureAgent는 담당자 생성 또는 열림 상태 변경이 있을 때만 저장한다. 최신 파일 검증, 기존 이름·역할·Revision과 named mutex 직렬화는 유지한다.
- 담당자 표시만 250ms 간격, 최대 20회, 단일 DispatcherTimer로 확인한다. 현재 선택 주소를 매번 확인하고 성공·영구 오류·종료 시 타이머와 콜백을 해제한다. Send/Receive/Reply 등 사용자 작업을 자동 재전송하지 않는다.
- Markdown의 원자 커밋·CAS·시간별 페이지 저장과 비동기 이전 대화 조회는 변경하지 않았다. 전체 초기화·초안 저장의 동기 디스크 I/O가 모두 제거된 것은 아니며 느린 디스크의 UI 지연은 별도 가능성으로 남는다. Task.Wait/Result 기반의 미완료 Task UI 대기는 발견하지 못했다.

## 검증과 인계

- `artifacts/task069hang/run-3039e74e5c124f68a20aed17286e2123/result.json`: 17/17. 실제 저장 소스와 원문 UI 4개 메서드 추출 검증이며 전체 제품 DLL UI 검사는 아니다. UI 즉시 반환·heartbeat, 파일 원문·Revision 보존, 실제 변경 저장, 단일 재시도·선택 전환·Dispose·상한·영구 오류·미전송·기존/백그라운드 대기 계약을 확인했다.
- `artifacts/task069hang/regression-7c1f63e9f5c34a8fa602e4d3456b41c8/result.json`: 기존 협업 검사 60/60. 기존 assertion을 변경하지 않고 실제 별도 프로세스 경합·상태 전이·오류·보안 검사까지 통과했다.
- 제품 범위는 ConversationCollaboration.cs의 생성자/Access/EnsureAgent/경합 오류, Experience.cs 생성자 정책·종료 hook, Options.cs 담당자 표시·재시도만이다. 061의 BuildPromptContext 및 새 메시지 기능과 역할을 분리했다.
- 고정 통합 DLL로 같은 17개 계약을 다시 검사한 결과는 후속으로 추가한다. 네 SKU 빌드·실제 IDE·감사·최신 Release·실제 게시·익명 다운로드 hash 검증은 Root의 최종 통합 책임으로 남는다. 이 파트 완료를 전체 배포 완료로 표현하지 않는다.

## C3 실제 DLL 재검사

`artifacts/task069hang/run-135629f407d449eaa2bfe6505d8365b6/hang-summary.json`에서 동일 **17/17 PASS**, `ActualProductDll=true`, `InputsUnchanged=true`를 확인했다. C3의 2017 실제 DLL과 exact `project.assets.json`, C3/source의 정확한 UI 네 메서드를 사용했다. 검사 실행기·Python 추출기·fixture·컴파일 입력·프레임워크 참조 사본을 함께 고정하고 실행 전후 SHA를 비교했다. 컴파일 경고와 오류는 없었다. 전체 제품 UI 검사를 주장하지 않으며 `FullProductUiTest=false`로 구분한다. 최초 source 검사와 최초 DLL 결과도 덮어쓰지 않고 보존했다.

최상위 실행기 담당 064에게 `Test-HangBoundaries.ps1 -AssemblyPath <DLL> -AssetsPath <assets> -ProductSourceRoot <candidate/source> -OutputDirectory <새 경로>` 계약을 인계했다. 제품 파일은 C3에서 추가 변경하지 않았다. Root는 실제 IDE와 최종 배포 게이트를 계속 진행해야 한다.
