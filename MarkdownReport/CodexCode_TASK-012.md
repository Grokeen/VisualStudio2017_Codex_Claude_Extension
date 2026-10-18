# TASK-012 — Role 추가 기능·대화 화면·검사 실행기

- 담당: CodexCode. 시작: 2026-10-18 12:58:21 KST.
- 상태: 구현·검증·자기 보고 완료. 완료 2026-10-18 13:41:24 KST, 소요 43분 3초. 최종 GitHub 반영은 TASK-013 담당자에게 인계했다.
- 요구: Role.md 프로젝트2·4·5·7·8·9·10·12·13, image-2~6, 추가 대화 화면 예시, 기본6의 최상위 테스트 실행기.
- 협업: 제품과 신규 저장/업무/UI 검사는 TASK-012, 사용자 README/가이드·기존 검사·실제 IDE·버전/배포는 TASK-013 CodexOrig가 담당했다. 소유권은 WORK_STATUS의 13:00:30 최종 합의를 적용했다.

## 구현

Claude를 첫 탭으로 배치하고 이름을 Claude로 통일했다. 공식 제공자 로고와 반투명 배경, 블랙 계열 버튼·팝업·콤보·스크롤·라디오·토글·컨텍스트 메뉴를 적용했다. 상단 Chat, 요청 입력 제목과 별도 붙여넣기 버튼을 제거하고 모델·생각 모드·작업 권한을 입력란에 모았다. 이미지는 입력란 위에 표시하고 hover/키보드 포커스 시 첨부 제거 버튼을 제공한다. Enter 전송, Ctrl+Enter 줄바꿈, IME 조합 중 전송 차단을 적용했다.

설정/사용 안내는 우측 상단에서 부채꼴로 펼쳐지고 우클릭 진입도 유지한다. CLI 설치·경로 선택·로그인·연결 확인을 그림형 단계로 안내한다. 중앙 알림은 잠시 유지한 뒤 천천히 사라지며 최근 알림에서 다시 확인한다. 버튼·팝업·작업 중 표시에는 실제 이벤트에 따른 애니메이션을 사용한다.

사용자 요청은 오른쪽 둥근 말풍선으로, AI 답변은 왼쪽 본문으로 표시한다. 대화의 실제 생성 시각을 중앙에 표시하고 현재 요청의 실제 Stopwatch 경과 시간을 별도 표시한다. 대화별 마지막 측정 결과는 현재 실행 중에만 유지한다. 저장된 메시지에 없는 시각이나 도구 실행 내역을 만들어 표시하지 않는다.

대화는 `%LOCALAPPDATA%\VS2017AIAssistant\workspace\conversations\{claude|codex}\{id}.md`에 front matter와 정확한 원문으로 기록한다. 시작 시 열린 대화를 복원하며 새 대화·자동 제목·이름 변경·좌측 목록 토글·닫기·명시적 불러오기를 지원한다. 닫은 대화는 파일을 남기고 자동 복원에서 제외한다. 초안과 메시지를 저장하고 저장 실패 시 대화/AI 전환을 중단해 입력을 보존한다. 이미지 첨부·계정 비밀·원본 소스 스냅샷은 대화 Markdown에 저장하지 않는다. 사용자가 입력한 대화/초안은 로컬 평문이다.

공통 규칙은 같은 workspace의 `instructions.md`로 모든 프로젝트에서 공유한다. 설정 창 크기에 맞춰 편집란이 늘어나며 요청마다 디스크 규칙을 다시 읽는다. JSON 설정과 Markdown 저장을 묶어 두 번째 저장 실패 시 JSON 원문을 복구한다. 복구 전에 외부 변경을 확인하며 다른 프로그램의 변경을 덮어쓰지 않는다. 테스트는 설정·workspace 환경변수로 개인 경로와 격리한다.

한 파일의 수정안은 기존 승인 흐름을 유지하고 변경 카드에 실제 추가/삭제 줄 수·되돌리기·변경 보기를 표시한다. 소스 링크는 실제 IDE의 해당 파일 첫 변경 줄로 이동한다. 빈 원본 문서에는 단일 삽입을 허용하되 비어 있지 않은 문서의 빈 검색 범위는 계속 거부한다. SQL Server 스키마와 요청을 검토해 AI 입력에 넣고 빈 SQL 파일에도 쿼리 수정안을 생성·승인·저장할 수 있다. 스키마는 정의만 읽으며 DB에서 SQL을 실행하지 않는다.

작업 권한 창은 Git fetch/pull/checkout/지정 파일 commit/push, SVN update/checkout/commit, TFVC get/checkout/checkin, 지정 네이티브 배포 프로그램 실행을 제공한다. 권한을 켜고 대상·실제 명령을 검토한 뒤 실행 동의해야 동작한다. 조건을 바꾸면 동의를 해제한다. 권한 기본값은 별도 저장 버튼으로만 보관한다. 셸 배치 실행·임의 셸 문자열·옵션/경로 주입을 제한하고 취소/시간 제한 시 하위 프로세스를 종료한다. Git pull은 fast-forward만 허용하며 지정하지 않은 staged/untracked 파일을 커밋에 포함하지 않는다. Role의 tns는 TFS/TFVC로 해석했다.

## 소스 및 검사 경로

| 범위 | 경로 |
| --- | --- |
| 대화/규칙·설정 트랜잭션 | `src/VS2017.AIAssistant/Chat/MarkdownWorkspaceStore.cs`, `AssistantSettingsTransaction.cs`, `ChatWorkspace.cs` |
| 새 UI와 연결 | `Views/AssistantControl.Experience.cs`, `AssistantControl.xaml`, 기존 `AssistantControl.*`, `AssistantDialogs.cs`, `AssistantTheme.xaml` |
| 프로젝트 작업·DB | `Services/WorkspaceActionService.cs`, `Views/WorkspaceActionsDialog.cs` |
| 소스 링크·빈 SQL·줄 수·CLI 모드 | `Services/VisualStudioDocumentService.cs`, `Editing/DocumentSnapshot.cs`, `EditProposal.cs`, `Providers/CliEditProvider.cs` |
| 공식 제공자 이미지 | `Assets/Claude.png`, `Codex.png`, `THIRD-PARTY-NOTICES.md` |
| 신규 자동 검사 | `tests/workspace/`, `tests/workspace-actions/`, `tests/review/Test-WorkspaceExperience.ps1` |
| 실행기 | `tests/launcher/`, `scripts/Build-TestLauncher.ps1`, 루트 `VisualStudio_AIChat_ExtentionTest.exe` |

각 신규 클래스/메소드 및 수정 메소드에 Role의 한국어 name/desc/author/create date/update date 주석 형식을 적용했다. 사용자 Role.md는 편집하지 않았다. 13:17 사본 이후 사용자가 13:27:47에 실행기 요구를 추가했으므로 두 사본의 해시가 다른 것은 사용자 변경이며, 최종 확인 사본은 `artifacts/task012/Role.reviewed-1331.md`다.

## 고정 제품 및 검증

환경은 Windows PowerShell 5.1, .NET Framework 4.6.2, C#7, VS2017 Community 15.9.83/MSBuild15다. Debug·Release 빌드는 모두 경고·오류0으로 완료했다. 이전 v0.2/v0.3/v0.4 릴리스와 이번 중간 검토 사본은 보존했다.

최종 추가 대화 화면 사본: `artifacts/task012/handoff-conversation/`.

```text
AE807FFD7CABA335DCC9A61BD2BB931BCF77CA692B079E8E29D0930A78D55AE6  VS2017.AIAssistant.dll
DC2DB3178CD6490CCE529EA95E31133CD7B8997F9B146B656C54EC0FA474C768  VS2017.AIAssistant-0.5.0.vsix
```

같은 폴더 `source-hashes.json`으로 인계 후 제품 소스 변경 여부를 검사한다. 아래 검사는 소스를 중복 컴파일하지 않고 이 실제 제품 DLL을 참조했다.

| 검사 | 결과 | 근거 |
| --- | --- | --- |
| Markdown 원문·front matter·초안·닫기/재열기·경계/손상파일 | 52/52 PASS | `artifacts/task012/workspace-e54f316f031f4220bf320e58571eb364/result.json` |
| JSON/Markdown 실패 복구·실제 잠금·외부 변경 보존 | 37/37 PASS | `artifacts/task012/settings-transaction-7b7b24ff85a74edbb1042aad2b1c485a/result.json` |
| 실제 로컬 Git·프로세스 취소/실패·인수·업무 WPF 승인/비밀 분리 | 48/48 PASS | `artifacts/workspace-actions/run-df04fdab53c740d7ba3cb86eb8f26ecf/results.log` |
| 실제 WPF·키/모델/첨부/보존·날짜/말풍선/측정시간·작업 해제 | 56/56 PASS | `artifacts/task012/experience-conversation-b/result.json`, `CodexCode_TASK-012_Experience.json` |
| 편집/설정 UX 기존+추가 | 92/92 PASS | TASK-013 최종 새 사본 실행 결과 |
| 기반 패키지/검정 테마 | 33/33 PASS | TASK-013 최종 새 사본 실행 결과 |
| VSIX 호환성 도구 | 44/44 PASS | TASK-013 검증 JSON |
| 실제 VS2017·실제 Codex/빈 SQL·대화 복원 | 58/58 PASS | `CodexOrig_TASK-013_IDE.json`, `_IDELaunch.json` |

WPF 검사56개는 기존43개와 추가 대화 화면13개다. 2024년 생성일 fixture를 실제 Markdown에서 복원해 정확한 시각을 확인하고, 독립 Stopwatch로 실제 경과 시간을 비교했다. 작업 종료/폐기 시 타이머·반복 애니메이션 해제, 새 대화/다른 AI 전환 시 표시 분리, 동적 본문 복사 메뉴도 확인했다. 780/420/220px PNG를 직접 열어 배치와 좁은 화면을 검토했다. 테스트의 첫 Loaded 대기 오류는 테스트 타이밍 수정 후 통과했으며 제품 변경은 없었다.

실제 IDE 결과의 지정 VSIX 해시와 참조 DLL 해시가 위 최종 사본과 일치함을 직접 읽어 확인했다. 실제 Codex는 합성 소스·SQL 스키마로 호출했으며 사용자 프로젝트를 전송하지 않았다. 이전 후보 및 중간 IDE 실행은 최종 결과로 합산하지 않는다.

최상위 실행기는 기본 전체 검사 **8/8 묶음 PASS·종료코드0**를 확인했다. `artifacts/tl/e61dd70ec3/launcher.log`와 `summary.txt`, 콘솔 사본 `artifacts/task012/launcher-all-final.log`가 근거다. 실행기는 배포 VSIX에서 위 AE807FFD… DLL을 추출해 사용했고 제품 파일을 재빌드하지 않았다. 같은 검사들의 실행 경로 검증이므로 기존 통과 건수에 중복 가산하지 않는다. 첫 실행의 긴 출력 경로로 인한 UI 검사 실패는 실행기 결과 폴더를 짧게 조정한 뒤 같은 전체 검사를 다시 통과했다. 명시한 VSIX 우선 사용과 자식 검사 실패 종료코드 전달도 독립 검토로 보완했다.

실행기 SHA-256: `3C22B6545035A508452E8886EB37B94D1A0BA90D763A48D8943EE52E7B598EFB`.

실행기 독립 스모크 **9/9 PASS**도 확인했다. 목록10개·기본8/선택2 분리, 실제 호환성44개, 알 수 없는 검사ID 종료2, 자식 exit7의 정확한 wrapper 전달과 실행기 실패1, 없는 VSIX 거부, 선택한 VSIX가 기본 DLL보다 우선함, 실행기 종료 시 소유 child PID 정리, 실제 GUI의 빈 선택·자동 실행 없음·준비 상태를 검증했다. 근거는 `artifacts/ls/8ce1ba8a53/results.json`의 보관 사본 [CodexCode_TASK-012_Launcher.json](CodexCode_TASK-012_Launcher.json)이다. [실제 실행기 렌더](assets/task012-test-launcher.png)도 직접 열어 확인했다. root는 잘못된 ID/옵션이 각각2임을 추가 확인했다. 테스트 결과 보관 파일·최종 검증 요약은 [CodexCode_TASK-012_Validation.json](CodexCode_TASK-012_Validation.json)에 있다.

## 최상위 테스트 실행기

저장소 전체가 있는 최상위의 `VisualStudio_AIChat_ExtentionTest.exe`를 더블클릭하면 검사 선택과 로그 화면만 열린다. 선택 실행 또는 기본 전체 검사를 눌러 시작하고 중지할 수 있다. 기본 8개는 호환성·빌드 진입 fixture·Markdown·설정·프로젝트 작업·편집·기반 패키지·사용 흐름이다. 실제 VS2017와 실제 Codex 계정 검사는 직접 선택해야 한다. DLL/VSIX를 비우면 배포 v0.5.0을 우선 사용하고 없을 때 기존 Release 산출물을 찾는다. 제품을 자동 빌드하지 않는다. DLL/VSIX는 저장소 안의 경로만 받으며 실제 Codex .exe는 별도 설치 위치를 지정할 수 있다.

```powershell
.\VisualStudio_AIChat_ExtentionTest.exe --list
.\VisualStudio_AIChat_ExtentionTest.exe --all
.\VisualStudio_AIChat_ExtentionTest.exe --run experience --vsix .\releases\v0.5.0\VS2017.AIAssistant-0.5.0.vsix
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-TestLauncher.ps1
```

각 실행의 전체 로그·개별 종료코드 요약은 `artifacts/tl/<고유ID>/launcher.log`와 `summary.txt`에 저장한다. 명령행 성공은0, 검사실패는1, 잘못된 옵션은2, 중지는130이다. 각 자식 검사는 15분 제한과 Windows Job으로 소유한 하위 프로세스만 정리한다. GUI 닫기는 진행 중 검사를 취소하고 정리한 뒤 종료한다. 실행기 소스는 기본 .NET Framework 컴파일러로 다시 만들 수 있고 PowerShell 파일은 PS5.1 한글 보존을 위해 UTF-8 BOM을 사용한다.

## 재현

저장소 루트에서 다음과 같이 실제 고정 DLL로 실행한다.

```powershell
$dll = '.\artifacts\task012\handoff-conversation\VS2017.AIAssistant.dll'
powershell -NoProfile -ExecutionPolicy Bypass -File tests/workspace/Test-MarkdownWorkspace.ps1 -AssemblyPath $dll
powershell -NoProfile -ExecutionPolicy Bypass -File tests/workspace/Test-SettingsTransaction.ps1 -AssemblyPath $dll
powershell -NoProfile -ExecutionPolicy Bypass -STA -File tests/workspace-actions/Test-WorkspaceActions.ps1 -ProductAssemblyPath $dll
powershell -NoProfile -ExecutionPolicy Bypass -STA -File tests/review/Test-WorkspaceExperience.ps1 -AssemblyPath $dll -OutputDirectory artifacts/task012/experience-reproduce
```

## 한계와 인계

현재 VSIX 대상은 VS2017 15.9+다. VS2019/2022 지원, 실제 Claude CLI/계정, 외부 SVN/TFVC·실제 SQL Server 접속·운영 배포는 검증하지 않았다. Git은 실제 임시 로컬 bare 원격까지 검증했다. SQL 작성은 스키마 기반 AI 요청이며 SQL 실행 기능은 없다. CLI의 직접 쓰기/셸 도구는 제한하며 소스 적용은 한 파일 수정안의 명시적 승인·저장이다. 여러 파일 자율 수정이나 AI의 자동 셸 작업으로 안내하지 않는다.

Codex 생각 모드는 [공식 config reference](https://learn.chatgpt.com/docs/config-file/config-reference), Claude는 [공식 CLI reference](https://code.claude.com/docs/en/cli-reference)를 확인해 auto/low/medium/high만 전달한다. 실제 지원 모드는 선택 모델에 따라 다르며 auto는 CLI 기본값을 사용한다. 제공자 원본 이미지의 출처/해시/상표 안내는 `Assets/THIRD-PARTY-NOTICES.md`에 있다.

CodexOrig는 README/USER_GUIDE와 실제 제품 렌더 이미지를 갱신했다. 최종 실제 IDE 및 릴리스·GitHub 검증은 [TASK-013](CodexOrig_TASK-013.md)에 기록한다. TASK-012와 하위 작업자가 모두 종료한 뒤 CodexOrig만 최종 Git 작업을 수행한다.
