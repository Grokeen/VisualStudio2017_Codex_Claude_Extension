



# Update_History

## TASK-003 — CodexOrig / 2026-10-18 01:33:36 KST

- 작업 시간: 2026-10-18 00:50:54 ~ 01:33:36 (KST), 소요 42분 42초. 설치 실행 구간 약 27분 35초.
- 요청/작업: VS2017·2019·2022를 각각 설치하여 VSIX 실제 테스트 환경 구성. Community 15.9.83 / 16.11.60 / 17.14.42 설치 완료, 자동 재부팅 없음.
- 구성: 한국어, .NET 데스크톱/WPF, Visual Studio 확장 개발/VSSDK, .NET Framework 4.6.2 Targeting Pack·SDK. 공식 Microsoft bootstrapper 서명·버전·SHA-256 확인.
- 소스/문서 경로: `scripts/Install-VsixTestEnvironment.ps1`, `scripts/Test-VsixTestEnvironment.ps1`, `docs/TEST_ENVIRONMENT.md`, `MarkdownReport/CodexOrig_TASK-003*`, 자기 협업 현황과 OWERORDER 항목.
- 수정/검증: PS5 빈 JSON 배열 처리와 재실행 시 구성된 IDE 건너뛰기 수정. IDE용 probe 프로젝트·솔루션 구성 보완. 세 버전의 필수 구성 및 MSBuild WPF 컴파일 PASS, 세 IDE의 devenv.com 솔루션 빌드 각각 성공 1/실패 0/종료 0.
- 한계: GUI 도구 `GetCursorPos failed ... 0x80070005`로 대화형 화면·VSIX 런타임 미검증. VS2017의 30일 라이선스 안내는 사용자 로그인/라이선스 확인 필요. GUI 통합 검증 미완료이므로 GitHub push·릴리스 배포하지 않음.


## TASK-002 — CodexOrig / 2026-10-17 22:19:55 KST

- 작업 시간: 2026-10-17 22:11:32 ~ 22:19:55 (KST), 소요 8분 23초.
- 작업 내용: VSIX 원본 매니페스트·프로젝트의 VS2017/2019/2022 정적 호환성 검사 도구, 44개 회귀 테스트, 공식 근거와 실제 IDE 검증 절차 작성. Windows PowerShell 5.1 한글 인코딩 문제 수정.
- 소스 경로: `scripts/Test-VsixCompatibility.ps1`, `tests/compatibility/Test-VsixCompatibility.Tests.ps1`.
- 문서/결과: `docs/VSIX_COMPATIBILITY.md`, `MarkdownReport/CodexOrig_TASK-002.md`, `MarkdownReport/CodexOrig_TASK-002_Compatibility.json`, 자기 협업 현황 및 OWERORDER 보고.
- 검증: Windows PowerShell 5.1·PowerShell 7 각각 44/44 PASS. 구문 오류 0건. 실제 Phase 1 소스 VS2017 기본 정적 검사 PASS. TASK-001의 원본 추가 변경을 감지해 22:19:55 최신 소스로 재검사 PASS, JSON 갱신. VS2019/2022는 미선언이며 실제 IDE 설치 검증은 미수행.
- 빌드/배포: PowerShell 구문 분석·실행 검증 완료. VSIX 빌드는 TASK-001 담당자의 진행 범위이며, 실제 IDE 설치는 별도 필요. CodexCode 작업 중이므로 GitHub push·릴리스 배포하지 않음.




## TASK-001 — CodexCode / 2026-10-17 22:25:46 KST

- 작업 시간: 2026-10-17 22:10:21 ~ 2026-10-17 22:25:46 KST, 소요 15분 25초.
- 작업 내용: VS2017 15.9용 독립 VSIX 솔루션, 비동기 패키지·도구 메뉴 등록, WPF 도구 창과 테마 대응, 빌드 스크립트 및 패키지·WPF 검사 구현.
- 소스 경로: `VS2017.AIAssistant.sln`, `src/VS2017.AIAssistant/`, `scripts/build.ps1`, `tests/foundation/Test-FoundationPackage.ps1`.
- 문서: `README.md`, `docs/DEVELOPMENT.md`, `MarkdownReport/CodexCode_TASK-001.md`, `MarkdownReport/WORK_STATUS.md`, `MarkdownReport/OWERORDER.md`.
- 검증: 임시 전체 Framework MSBuild 17.11.4·Roslyn 4.11(C# 7.0)·NuGet 참조 주입 환경에서 Debug/Release VSIX 생성 PASS, Foundation 검사 각 22/22 PASS. CodexOrig 회귀 검사 재실행 44/44 PASS, 최신 VS2017 원본 정적 검사 PASS. 생성 DLL Framework 4.6.2/MSIL 확인.
- 산출물: `src/VS2017.AIAssistant/bin/Release/VS2017.AIAssistant.vsix`. 로그·테마별 렌더링·최신 호환성 JSON은 `artifacts/phase1/`.
- 미완료: VS2017/SDK 미설치로 실제 MSBuild 15.x 빌드, F5, 설치·메뉴·도킹·종료/복원·제거 테스트 미실행. Phase 1 전체 완료 아님.
- 배포: 협업 작업 중 push 금지 준수. 다른 작업 완료 후에도 실제 IDE 검증 완료 조건이 남아 Git 커밋·push·GitHub 릴리스 미실행.
## TASK-004 — CodexCode / 2026-10-18 01:26:19 KST

- 작업 시간: 2026-10-18 00:55:01 ~ 2026-10-18 01:26:19 KST, 소요 31분 18초.
- 검토: 추가 Role.md의 확장 내 Codex/Claude 소스 수정 요구를 확인하고 기존 MVP의 수정 제외 문구를 갱신.
- 개발: v0.2.0 현재 파일 수정 요청 → CLI 구조화 수정안 → 비교 → 사용자 승인 → EnvDTE 버퍼 적용 → 별도 저장·직전 적용 복구 구현. 편집기·디스크·읽기 전용 충돌 검사, UTF-8 파이프, 출력 제한, 취소·타임아웃·Job Object 정리 추가.
- 소스 경로: `src/VS2017.AIAssistant/Editing/`, `Providers/`, `Services/VisualStudioDocumentService.cs`, `Views/AssistantControl.xaml*`, `ToolWindows/AssistantToolWindow.cs`, csproj·AssemblyInfo·vsixmanifest.
- 테스트: `tests/editing/EditingTests.cs`, `Test-SourceEditing.ps1`, `Test-LiveCodex.ps1`, `tests/foundation/Test-FoundationPackage.ps1`. 편집/CLI/WPF 55/55, 패키지·렌더링 29/29, 기존 호환성 44/44 PASS.
- 빌드: 실제 VS2017 15.9.83의 MSBuild 15.9.30.58560 및 Roslyn으로 Debug/Release 표준 복원·빌드 PASS, 경고·오류 0건. 최종 빌드에서 임시 참조 주입 사용하지 않음.
- 실제 AI 검증: 로그인된 Codex CLI 0.162.0-alpha.17.2로 합성 코드의 return 1 → return 2 수정안 반환 및 승인 전 원본 파일 보존 PASS. 사용자 프로젝트 소스는 보내지 않음.
- 고정 산출물: `artifacts/source-editing/handoff/VS2017.AIAssistant-0.2.0.vsix`, SHA-256 `0B7FD44509A6CA02344E645924378EB544966A0831B942A9F7D4B6CDAB872D94`.
- 보고/사용법: `MarkdownReport/CodexCode_TASK-004.md`, `docs/SOURCE_EDITING.md`, `docs/DEVELOPMENT.md`, README·Project_Plan·자기 협업 기록 갱신.
- 미검증: 실제 IDE EnvDTE 적용·저장·Undo 및 실제 Claude 계정 호출, VS2019/2022 지원. IDE 검증은 CodexOrig에 고정 패키지와 fixture를 인계.
- 배포: TASK-003이 진행 중이므로 Role.md 6번에 따라 Git 커밋·push·릴리스 미실행. 다른 작업자의 설치 스크립트·보고 보존.

## TASK-006 — CodexOrig / 2026-10-18 02:02:51 KST

- 작업 시간: 2026-10-18 01:55:40 ~ 02:02:51 (KST), 소요 7분 11초.
- 검토: 추가 Role.md의 소스 수정 요구와 라이선스 안내 관련 배치 요구 확인. v0.2.0의 승인·버퍼 적용·저장 연결 검토, 실제 IDE 검증은 CodexCode TASK-005에 유지.
- 개발 경로: `scripts/Build-Vsix.cmd`, `scripts/Invoke-VsixBuild.ps1`, `tests/build-entry/Test-BuildEntry.ps1`, `docs/BUILD_AND_LICENSE.md`, `MarkdownReport/CodexOrig_TASK-006*`.
- 구현: 지정 주 버전의 MSBuild/VSSDK 탐색, 설치·파일 버전 확인, PlanOnly, 기존 빌드 스크립트 호출·종료 코드·출력 보존. PS5.1 한글 네이티브 JSON/리다이렉트 출력 인코딩 수정.
- 검증: PS5.1/PS7 각각 16/16 PASS, 구문 오류 0건. 설치된 MSBuild 15/16/17의 격리된 Release VSIX 빌드 모두 종료 0·패키지 생성, IDE 라이선스 배너 관측 안 됨.
- 범위: 기존 확장 소스·build.ps1·고정 인계 패키지·IDE 프로필 미변경. 빌드 엔진 선택이 VS2019/2022 확장 지원을 뜻하지 않음. IDE 로그인/활성화 및 화면 안내 해소는 미검증으로 명시.
- 배포: TASK-005 진행 중이므로 Role.md에 따라 GitHub push·릴리스 미실행.

## TASK-005 — CodexCode / 2026-10-18 02:04:14 KST

- 작업 시간: 2026-10-18 01:49:18 ~ 2026-10-18 02:04:14 KST, 소요 14분 56초.
- 검증: 고정 v0.2.0 VSIX를 VS2017 15.9.83 별도 인스턴스에 설치하고 실제 메뉴·도구 창·EnvDTE·미저장 버퍼·승인·적용·저장·Undo·복구·충돌 검사 25/25 PASS. 실제 Codex 계정의 합성 코드 수정안 생성부터 IDE 저장까지 PASS.
- 회귀: 편집 55/55, Foundation 29/29, 호환성 44/44. 총 고유 검사 153/153 PASS. 최종 런처를 새 인스턴스에서 추가 재현하여 fixture 22/22 PASS. 테스트 보조 VSIX Release 빌드 경고·오류 0건.
- 소스 경로: tests/ide/의 csproj·C# 보조 패키지/케이스·PowerShell 런처·매니페스트. 제품 src/와 고정 인계 VSIX 미변경.
- 보고: docs/IDE_TESTS.md, MarkdownReport/CodexCode_TASK-005.md 및 IDE/Repro JSON. README·DEVELOPMENT·SOURCE_EDITING·Project_Plan·협업 기록 갱신.
- 한계: 실제 Claude 계정, VS2019/2022 패키지, 일부 IDE 특수 문서·수동 사용성 검증은 별도. TASK-006의 담당 파일 보존 및 작업 중 배포 금지 준수.

## TASK-007 — CodexCode / 2026-10-18 02:07:23 KST

- 작업 시간: 2026-10-18 02:05:41 ~ 2026-10-18 02:07:23 KST, 소요 1분 42초.
- 내용: TASK-006 완료 재확인 후 검증한 소스·테스트·보고서 및 고정 VSIX를 GitHub main에 반영. 구현 커밋 b07415505a5be7253d25ccc078003bf492353674.
- 경로: releases/v0.2.0/VS2017.AIAssistant-0.2.0.vsix, SHA256SUMS.txt, README.md 및 루트 README의 다운로드 안내.
- 검증: 원격 main 커밋 일치, GitHub 재다운로드 파일 39,860바이트·SHA-256 일치. 배포 파일의 비밀키/토큰 형식 검사 검출 없음. 실제 IDE 25/25 및 기존 128/128, 새 인스턴스 재현 22/22 PASS 패키지와 동일.
- 범위: GitHub 저장소 배포 완료. Marketplace·일반 IDE 설치·별도 GitHub Releases 객체 생성은 미수행. 상세 CodexCode_TASK-007.md.

## TASK-009 — CodexCode / 2026-10-18 02:57:20 KST

- 작업 시간: 2026-10-18 02:31:42 ~ 2026-10-18 02:57:20 KST, 소요 25분 38초.
- 내용: Role 기본7번과 프로젝트3~8번 확인. README를 사용자 가치·설치·계정 연결·요청·첨부·대화·instruction·승인/저장 중심으로 전면 개편하고 상세 USER_GUIDE·실제 제품 UI 예제 이미지 추가.
- 소스/문서 경로: README.md, docs/USER_GUIDE.md, docs/assets/chat-0.3-preview.png, tests/review/Test-LiveChatContext.ps1. 제품 구현 및 기존 회귀/IDE 검사는 CodexOrig TASK-008과 분담.
- 독립 검토: 어두운 Expander 제목 가독성 문제 발견·수정 재확인, 이미지 Ctrl+V 명령 경로 검토 인계 및 수정 확인. 이전 v0.2 파일 보존.
- 최종 검증: 고정 DLL로 실제Codex 이미지·텍스트·대화·규칙/계정6/6, 편집71/71, Foundation33/33 PASS. IDE35/35 결과 확인. 문서 로컬 참조 16개·패키지 해시 일치.
- 보고: CodexCode_TASK-009.md, _LiveContext.json, _Documentation.json. 작업 종료 후 최종 GitHub 반영을 TASK-008 CodexOrig에 인계. 직접 중복 커밋·push하지 않음.

## TASK-008 — CodexOrig / 2026-10-18 02:58:58 KST

- 작업 시간: 2026-10-18 02:32:55 ~ 02:58:58 KST, 로컬 검증까지 소요 26분 3초.
- 요청: Role.md의 Chat 이동·이미지/파일·누적 프롬프트·instruction·계정/텍스트 설정 및 README 7번 반영. 추가 사용자 지시로 CodexOrig C# 주석의 줄바꿈·콜론 열을 기존 CodexCode 형식으로 통일.
- 구현: v0.3.0 Chat MDI/Float/Dock, 마지막 소스 추적, Provider별 대화/초안/첨부, 제한된 첨부 스냅샷·이미지 전달·붙여넣기 명령, 최근 대화/instruction, 공식 CLI 계정 연결/확인, 경로/모델/글자 크기 영속 설정.
- 소스: src/VS2017.AIAssistant/Chat/ChatWorkspace.cs, Views/AssistantControl*, Providers/CliEditProvider.cs·CliAccountService.cs, Services/VisualStudioDocumentService.cs, 창/패키지 등록·csproj·버전.
- 테스트: tests/editing·foundation·ide, tests/chat/Test-LiveChat.ps1. scripts/Test-VsixTestEnvironment.ps1 및 tests/build-entry의 생성 C# 주석도 통일.
- 검증: VS2017 표준 Debug/Release 경고·오류0, 편집/Chat71·Foundation33·호환성44 전부 PASS. 고정 패키지 실제 IDE35/35(실제 Codex 포함), 실제 이미지/텍스트/대화/instruction 수정안 PASS. 최종 DLL을 TASK-009가 독립 재검증해 컨텍스트6/6·편집71·Foundation33 PASS.
- 문서/보고: docs/DEVELOPMENT.md·IDE_TESTS.md·SOURCE_EDITING.md, Project_Plan.md, CodexOrig_TASK-008* 및 협업/상관 보고. 사용자 README·USER_GUIDE·예제 화면은 TASK-009 CodexCode가 담당하여 보존.
- 패키지: releases/v0.3.0/VS2017.AIAssistant-0.3.0.vsix, SHA256 A7F1C597BA0CCF7B18AC52DEB29BACE2D6B66E300B9A90C6A55EB5A3E7A88DF2. 0.2 파일 미변경.
- 한계: 실제 Claude 계정·VS2019/2022 확장 지원·수동 마우스/키보드 전체 제스처·디스크 대화 복원 미검증/미구현. 최종 배포는 TASK-009 완료 확인 후 별도 기록.
