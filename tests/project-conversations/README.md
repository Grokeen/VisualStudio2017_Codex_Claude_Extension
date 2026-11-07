# 솔루션별 대화 저장 Preview 통합 계약

독립 worktree `C:/dydgyqud/ProjectConversations_20261107`, 기준 `ab9d1204e35cabda823c1cafe77b67559336f3e2`의 구현이다. 최종 TA 트리나 공유 BestCare checkout으로 직접 복사하지 않는다.

솔루션 절대 경로를 대소문자·구분자·점 경로 정규화 후 SHA256으로 구분한다. 저장은 기존 workspace의 `p/<hash 앞 32자리>`이며 전체 64자리 identity를 `scope-id.txt`에서 대조하므로 충돌·다른 바인딩은 거절한다. 솔루션 없음은 `p/none`이다. 솔루션 파일이나 기존 전역 기록은 이동·삭제하지 않는다. junction/symlink 조상 경로는 거절하며 서로 다른 솔루션 경로가 같은 실제 파일을 가리키는 경우 같은 범위로 합치지 않는다. 공통 instruction과 필수규칙은 전역 저장소를 유지한다.

DTE `SolutionEvents.Opened`/`AfterClosing`와 현재 `Solution.IsOpen`/`FullName`을 UI 스레드에서 관찰한다. 실제 실행·큐·진행 Task·모달 대화상자·요청 캡처가 남으면 이전 저장소를 유지하고 새 솔루션 전환을 보류한다. 이 동안 새 요청은 입력 소비 전에 고정 안내로 거절한다. 모든 기존 작업이 끝나면 이전 초안을 저장한 뒤 자동 전환한다. 범위별 대화·초안·첨부·미승인 제안·메모리 캐시 객체는 동일 참조로 보존한다. 서로 다른 범위의 동일 record ID도 별도 map이다. 저장/범위 준비 실패는 자동 반복하지 않고 메뉴에서 명시 재시도한다. 이 정책은 동시 솔루션 간 실행을 구현한 것이 아니다.

대화 목록의 범위 메뉴에서 이전 전역 기록에 명시 접근할 수 있다. 이전 전역 범위에서 새 전송은 차단하며 현재 솔루션으로 돌아가야 한다. 별도 그룹을 삭제·정리하는 기능은 추가하지 않았다. 장시간 실행 또는 승인 대기와 연결된 큐는 완료/중지 후 전환해야 한다.

## 통합 소유 경계

제품 파일 10개의 정확 목록과 SHA는 `artifacts/project-conversations/final-03/source-manifest.json`이다. Options.cs 변경은 기존 두 `workspaceStore.Save(record)` 호출을 `ConversationStorage.Save(record)`로 치환한 것뿐이다. 다른 CLI 변경과 병합 시 이 두 줄만 적용한다. derived SKU, 버전, 문서/PDF, 최종 제품 빌드와 게시 판단은 Root/TA 소유다.

최종 C6 활동함은 base ab9에 없으므로 다음 두 partial 구현을 Root/TA가 추가해야 한다.

- `ProjectConversationViewsChanging()`은 이전 초안 저장·이전 scope 캡처 이후, 새 scope 적용 직전 호출된다. 여기서 활동함 view를 닫고 Choices/nav/read/CAS-target 참조를 clear한다.
- `CaptureProjectConversationViewState(ref Action restore)`는 기존 scope 복원 Action 안에 추가 state 복원 Action을 넣는다. 활동함 SaveFailed provider+id 집합 사본을 캡처하고 restore 시 Clear+복원한다. rollback에서도 호출되므로 이전 실패표시를 보존한다.
- C6 ActivityFeed.cs의 직접 `workspaceStore.Save(record)` 1회도 `ConversationStorage.Save(record)`로 병합한다. 공통 규칙은 치환하지 않는다.
- C6 PersistConversation의 기존 postcommit 캐시/UI 분리 수정은 보존하고 저장 primitive 호출만 scope로 연결한다. baseab9 전체 메서드를 덮어쓰지 않는다.

## 비GUI 검증

Windows PowerShell 5.1에서 실행한다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/project-conversations/Test-ProjectConversations.ps1
```

최종 후보를 받은 Root는 같은 fixture를 실제 DLL로 실행할 수 있다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/project-conversations/Test-ProjectConversations.ps1 -ProductAssemblyPath '<frozen DLL>' -ExpectedProductSha256 '<64자리 SHA>' -OutputDirectory '<존재하지 않는 새 출력>'
```

source 모드는 실제 저장 소스 2개와 실제 ChatMessage 선언을 사용한다. DTE adapter는 `PROJECT_CONVERSATION_CORE_TEST`로 제외한다. HasProjectConversationWork와 map 복원 메서드는 제품 원문에서 추출하지만 실제 WPF controller가 아니라 fake flags/dictionaries/Task와 modal boolean으로 실행한다. actual-DLL 모드는 identity/gate/store를 해시 고정 사본 DLL에서 참조하며 WorkBoundary는 여전히 source excerpt이다. 이 모드는 아직 최종 제품 DLL로 실행하지 않았다.

52개 계약은 경로 식별·binding 불일치·global/2솔루션/none 디스크 분리·늦은 A 결과와 B 바이트 보존·draft/규칙/legacy 보존·실제 CAS·busy/queue/capture/modal/task 전환 보류·동일 ID cache 참조를 검증한다. 전환 UI rollback과 partial 활동함 hook은 정적 검토만 했다. 실제 DTE 이벤트, WPF 화면/포커스, 네이티브 AI PID, SDK 분석기와 최종 4SKU 빌드·설치는 아직 검증하지 않았다.

첫 실행 `r-23dd4358`은 C#52개 PASS 후 driver가 process 변수를 파일경로 변수로 덮어 validation 생성에 실패했다. 원본은 보존했고 driver 변수명만 정정했다. 후속 새 nonce 결과와 SHA를 최종 manifest에서 지정한다. 원시 `contract-result.json`과 driver `result.json`은 별도 보존한다. 테스트가 쓰는 경로는 출력 아래 합성 workspace뿐이다.

`final-01` provenance 시도는 git blob의 stdout가 지정 output 파일로 저장되지 않아 Options baseline 비교 단계에서 중단됐다. `final-02`는 git cat-file의 원시 stdout stream으로 baseline 바이트를 보존하고 두 줄 delta를 확인한 성공 기록이다. `final-03`은 이 문서의 최종 경로·이력 보완 이후 TestFiles SHA를 다시 고정한 인계 manifest다. 기존 원본은 수정하지 않는다.
