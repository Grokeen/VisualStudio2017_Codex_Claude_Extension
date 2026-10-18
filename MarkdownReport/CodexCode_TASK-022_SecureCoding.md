# TASK-022 시큐어 코딩 검토

- 작성: CodexCode, 2026-10-18 21:49 KST 증거 대조 시작.
- 최종 갱신: 2026-10-18 23:28 KST. **소유 보안 검토 완료, 실제 C11 최종 검증 PASS.** 기본 회귀 245/245·6빌드 경고0에 이어 활성 desktop에서 Default 56/56·hover 55/55·PDF 50/50을 확인했다. 전체 Git 배포는 CodexOrig/루트의 협업 조율 중으로 아직 완료되지 않았다.
- 기준: [Role.md](Role.md)의 필수 규칙·설치 안내·진행 표시 요구와 [source-freeze5.json](../artifacts/task022/source-freeze5.json). 이번 문서 작성 중 제품·검사·기존 보고서·감사 TASK-019/023 원본을 수정하거나 GUI·설치·공유 빌드를 실행하지 않았다.

## 필수 규칙의 파일·요청 경계

[MandatoryInstructionStore.cs](../src/VS2017.AIAssistant/Chat/MandatoryInstructionStore.cs)는 최신 Role8의 협업 문장, 다섯 필드 주석 예시의 원래 작업자명·날짜, 기존 주석 내용 변경 금지를 690자 내장 문안으로 제공한다. `.mandatory-instructions.md`는 그 문안의 관리 사본이다. `Compose`는 파일을 읽지 않고 내장 문안만 사용하므로 변조된 사본이 AI 지시로 승격되지 않는다. 숨김 속성은 접근권한이나 암호화를 대신하지 않는다.

`Ensure`는 완전한 workspace 경로를 검사하고 사본·조상의 reparse point를 거부한다. `CreateNew`와 프로세스 내 gate로 기존 내용을 덮지 않으며, 외부 생성 경쟁도 기존 파일 확인으로 처리한다. 비교 읽기는 **16KiB+1바이트**에서 멈춘다. 변조·과대·읽기 불가 사본은 보존하고 notice를 반환한다. `Hidden`을 더할 때 기존 `ReadOnly`·`Archive` 비트는 유지한다. 삭제된 사본은 복구한다.

최초 화면 준비와 요청 직전의 `Ensure` 연결을 소스로 확인했다. `ChatRequestContext.Instructions`는 사용자 원문을 유지하고 `EffectiveInstructions`에서 내장 규칙을 한 번만 조합한다. 최종 전송 경계 세 곳은 `CliEditProvider`의 수정안 JSON, `CodexRuntimeRunner`의 직접 실행 JSON, `ClaudeRuntimeRunner`의 직접 실행 JSON이다. 사용자 8000자를 자르지 않으며 이 문안 기준 유효 규칙은 최대 8692자다. 규칙 조합은 OS 권한 부여나 정책 우회 기능이 아니다. 세 전송 경계는 소스 확인이며, 이번 보고서 작성에서 유료 AI 요청으로 다시 검증하지 않았다.

## 설명서·설치 실행 경계

[CliGuideService.cs](../src/VS2017.AIAssistant/Services/CliGuideService.cs)는 로컬 절대 설치 폴더의 고정 `Resources/CliGuide` 자산만 선택하고 reparse 우회를 거부한다. PDF는 10MiB·서명, Markdown은 128KiB·유한 읽기, 배치는 64KiB·고정 SHA-256을 검사한다. Markdown은 읽기 전용 텍스트로 표시한다.

배치는 명시적 확인 후에만 고정 시스템 `cmd.exe`와 고정 파일 인수로 시작한다. 설치 경로의 명령 해석 문자를 거부하고 관리자 승격 옵션을 추가하지 않는다. 검증한 배치의 `FileShare.Read` 핸들을 **실제 종료까지** 유지해 실행 중 쓰기·교체를 막는다. static gate는 같은 확장 프로세스의 서비스 인스턴스 간 중복 시작을 막으며, OS 전체의 설치 mutex로 표현하지 않는다. 실제 프로세스와 sender를 대조해 오래된 종료 이벤트가 새 소유권을 풀지 못하게 하고, 개별 알림 구독자 예외도 실행 소유권과 분리한다. 안내창을 닫는 동작은 설치를 강제 종료하거나 성공으로 처리하지 않는다.

[CliGuideDialog.cs](../src/VS2017.AIAssistant/Views/CliGuideDialog.cs)는 정확히 같은 로컬 PDF와 쪽 fragment만 허용한다. 다른 파일·외부 URL·쿼리·UNC, 새 창, 다운로드, 권한 요청, 외부 URI 실행을 차단하고 http/https 하위 요청도 차단 응답으로 처리한다. script·host object·web message·개발 도구를 비활성화하며 기존 브라우저와 다른 전용 프로필을 사용한다. 기본 PDF 앱 열기는 별도 사용자 동작이다. 설치 실행 검사는 callback fixture에 한정했고 실제 `InstallCLI.bat` 실행은 **0회**다.

## 공개 진행·Popup 수명 경계

[ProgressNotice.cs](../src/VS2017.AIAssistant/Views/ProgressNotice.cs)는 공개 작업을 최대 **80행**, 정제된 한 줄 Detail을 **120자**, 저장 Markdown을 **2000자**로 제한한다. 카드와 저장 요약은 안전한 종류·상태·실제 종료 코드만 사용하고 원문 명령·도구 출력·비공개 사고 내용을 복제하지 않는다. 실제 작업 ID로 시작·완료를 같은 행에 반영하고 종료 이벤트 없는 작업을 성공으로 추정하지 않는다. 메시지 연결은 `ConditionalWeakTable`이며 요청 종료 이후 이벤트를 거부한다.

C9 실제 hover 진단에서는 마우스 capture 뒤 본문이 분리됐고, `IsOpen=true`만으로 실제 표시를 증명하지 못했다. [원래 결과](../artifacts/task022/progress-hover-candidate9-actual/result.json)와 [후속 진단](../artifacts/task022/progress-hover-candidate9-desktop/progress-hover-diagnostics.json)을 보존했다. freeze5는 `StaysOpen=true`, 자체 hover·focus·비활성화 닫기, 소유 창 바깥 클릭 닫기로 보정했다. 최종 읽기에서 Loaded의 기존 owner 중복 해제·재구독, Unloaded의 모델·owner 이벤트 해제, timer·Popup 닫기를 확인했다. 이후 **실제 C11 hover 기본45+추가10 통과**로 연결된 본문 유지·진입·고정·바깥 클릭·Escape·창 종료를 검증했다. 추가로 확정한 수명 누락은 없다.

## 실제 증거와 재현 경로

아래 값은 기존 JSON을 대조한 결과이며 이번 문서 작성 중 새 실행한 계수가 아니다. C9 DLL SHA-256은 `6B74C86C16AD67AEBCC429A28F4CA843540692346EE773F27F9BCEF3293C3BA5`다.

| 검사 | 확인된 결과 | 근거·경계 |
|---|---:|---|
| 실제 C9 기본 6그룹 | 245/245, 실패 0 | [summary.json](../artifacts/task022/bundle-candidate9-first/summary.json). 실제 hover·설치 성공을 포함하지 않음 |
| 실제 C11 기본 6그룹 | 245/245, 실패 0 | 루트 후속 실행의 [summary.json](../artifacts/task022/bundle-candidate11-final/summary.json). DLL `68B15E40EA79678F48A497010EF541D98871A8C70133A0FA458B2DDEABD0DA70`, 동봉 의존 26개 전후 일치. C9와 중복 합산하지 않음 |
| 그중 필수 규칙 | 33/33 | [result.json](../artifacts/task022/bundle-candidate9-first/mandatory/result.json). 8000자·중복 prefix·변조 보존·삭제 복구·16 thread/4 process 경쟁·실제 root/ancestor/leaf junction 거부 |
| 그중 CLI 안내 | 40/40 | [result.json](../artifacts/task022/bundle-candidate9-first/cli-guide/result.json). path/URI/크기/hash·실행 승인·유지 잠금·stale exit·구독자 예외를 fixture로 확인 |
| 선택 WebView clean 실행 | 추가 10/10 | [result.json](../artifacts/task022/webview-candidate9-clean/result.json)의 `OptionalWebView`. 전체 50은 기본 Guide40 재검사+추가10이므로 50을 모두 신규 계수로 더하지 않음. 실제 로컬 PDF 표시·종료를 확인했고 설치 실행 0 |
| 최종 C11 Default 렌더링 | 56/56, exit 0 | [result.json](../artifacts/task020/active-candidate11-20261018-232509/gpu/result.json). 실제 시간 샘플의 sweep 이동 포함. `UseHardwareRendering` 선택은 `RenderMode=Default`이며 하드웨어 강제를 뜻하지 않음 |
| 최종 C11 native hover | 55/55, exit 0 | [result.json](../artifacts/task020/active-candidate11-20261018-232509/hover/result.json). 기본45+추가10이며 앞선 기본 계수와 중복 합산하지 않음 |
| 최종 C11 PDF 표시 | 50/50, exit 0 | [result.json](../artifacts/task020/active-candidate11-20261018-232509/pdf/result.json). 기본 Guide40+추가10, 실제 설치 0. 루트가 [native PNG](../artifacts/task020/active-candidate11-20261018-232509/pdf/cli-guide-native-window.png)를 직접 보고 실제 PDF·전체 창 가림 없음을 확인. [캡처 진단](../artifacts/task020/active-candidate11-20261018-232509/pdf/cli-guide-webview-capture-diagnostics.json)은 자기 프로세스 전경·9/9 probe를 기록 |
| 실제 IDE 2017·2019·2022 | 각각 100 PASS, exit 0 | 실제 [2017 결과](../artifacts/ide/run-94c3fb87034b43d283aa86fb46e40a58/result.json)·[2019 결과](../artifacts/ide/run-1d767054d895478bb8a2fc191ef85468/result.json)·[2022 결과](../artifacts/ide/run-37945398687d477aa5543d9254b3c63b/result.json)와 루트 실행 요약 대조 |
| 전체 실행기 whole2·whole3 | 각각 16/16 그룹 PASS | [whole2 summary](../artifacts/tl/c292d4c9ac/summary.txt)·[whole3 summary](../artifacts/tl/524da2bf96/summary.txt). 두 반복을 서로 다른 기능 계수로 합산하지 않음 |

최종 활성 검사는 사용자 23:24 응답 이후 실행됐고, [preflight](../artifacts/task020/active-candidate11-20261018-232509/preflight.json)는 input desktop `Default`·커서 조회 성공을 기록했다. 세 검사의 DLL 해시는 위 C11 해시와 같다. freeze5 제품 소스 **14개 전부 변경 0**을 이번 대조에서도 확인했다.

첫 [whole1 결과](../artifacts/tl/a914214f43/summary.txt)의 12/16 통과·4그룹 실패를 보존했다. 필수 규칙 payload 기대값 2곳과 WebView 정확 allowlist 기대값 1곳은 실제 제품 계약에 맞게 검사만 보정했다. 별도 [thinking 진단](../artifacts/task020/thinking-candidate11-diagnostics/result.json)의 52 PASS/1 FAIL은 두 실제 시간 샘플이 같았던 결과다. 뒤의 프로세스 한정 [SoftwareOnly 56 PASS](../artifacts/task020/thinking-candidate11-software/result.json)는 진단 대조이며, 최종 성공은 위 Default 56 PASS로 따로 확인했다. 이를 위해 제품 소스·IDE 렌더링 설정을 바꾸거나 권한 규칙을 삭제하지 않았다. 화면 보호기로 입력 전에 중단된 기록도 남아 있으나, **화면 보호기 하나를 모든 실패의 유일한 근본 원인으로 확정하지 않는다.** 모든 원본 실패 기록은 유지한다.

[Test-Task022.ps1](../tests/task022/Test-Task022.ps1)은 고정 DLL·의존성 사본의 해시를 전후 대조하고 reparse·기존 파일 덮어쓰기·의존성 복사 단위당 1024파일/64MiB 초과를 거부한다. 각 그룹을 별도 STA PowerShell에서 순차 실행하며 개인 설정을 격리하고 runtime discovery를 끈다. 소스 전용 결과를 실제 제품 결과로 집계하지 않는다. 원본 PDF·Markdown·bat의 해시는 freeze5의 `OriginalGuideAssets`에 보존돼 있으며 문서 작성 시 다시 대조해 일치했다. bat 고정 해시는 `81BAE40E698B7E16B4A15093248453C7A79EF5D564306FC9CE9416255D91D5CB`다.

새 빈 출력 폴더에서 `Test-MandatoryInstructions.ps1 -AssemblyPath <고정DLL> -OutputDirectory <새폴더>` 및 `Test-CliGuide.ps1 -AssemblyPath <고정DLL> -OutputDirectory <새폴더>`로 해당 경계를 재검사할 수 있다. `Test-Task022.ps1`은 기본 묶음이고, WebView·native hover는 명시적 선택·별도 GUI 소유권 아래 실행한다. 이 보고서를 쓰면서 이 명령들을 실행하지 않았다.

## 남은 범위

소유 보안 검토와 위 최종 검증은 완료했다. **전체 Git 배포는 CodexOrig/루트의 협업 조율 중이며 아직 완료되지 않았다.** 실제 CLI 설치·업데이트·PATH 변경은 하지 않았다. Claude의 실제 추론 요청과 운영 DB·TFVC·배포는 미검증이다. 로컬 파일 검사와 고정 해시는 이번 입력·실행 경계의 증거이며 관리자 공격·모든 파일시스템 경쟁·외부 설치기의 전체 안전성을 증명하지 않는다. 사용자 선택 전체 액세스 기능 자체는 취약점으로 분류하지 않았다.

**이번 최종 문서 대조에서 새로 확정한 제품 문제는 없다.** 기존 C9 Popup 문제·freeze5 보정·C11 최종 통과와 실제 미검증 범위를 구분해 인계했다. 기존 감사 원문 및 기존 실패 증거는 수정하지 않았다.
