# TASK-022 — 추가 요청 1~11·이미지 24~27 반영 및 검증 완료

- 작성자: CodexCode
- 시작: 2026-10-18 20:36:38 KST
- 소유 구현·검증 완료: 2026-10-18 23:28:46 KST, 전체 경과 02시간 52분 08초. 사용자 입력 desktop 활성화 대기 시간을 포함한다.
- 상태: 소유 제품·검사·보고 완료. 전체 GitHub 배포·원격 해시 확인은 TASK020 CodexOrig가 마감한다.
- 기준 Role SHA256: `0164CD35E136103ED1C6122435199D53396D6C79B3A2F43F0BC906B8D8D40465`. 원문·이미지·감사 근거를 `artifacts/task022/requirements.json`과 후속 원문 사본에 보존했다.

## 반영 내용

| 요청 | 최종 처리 |
|---|---|
| 1 버튼 내용 중앙 | 부채꼴 안내의 아이콘·문자와 표준 명령 버튼의 가로·세로 중심 정렬 |
| 2 대화만 스크롤 | 외부 무한 측정을 유한 viewport로 제한하고 입력란 고정. 변경 카드도 대화 내부에서 스크롤 |
| 3 외부 소스 변경 모달 | TASK020이 직접 실행 동안 문서 알림·일시 읽기 전용 보호를 관리하고 저장 버퍼를 안전하게 reload. 미저장 변경 차단·원래 상태 복원 |
| 4 흰 대화 기록 | 빈 ListBox viewport·선택·스크롤 표면까지 검정 테마 적용 |
| 5 진행·생각 중 | 실제 WorkId의 동일 행 갱신, 종류별 Bold·색상·그라데이션 아이콘·hover 상세. 생각 중은 기본 하단, 이전 대화 열람 중 상단 |
| 6 CLI 자동 발견 | TASK020이 process/user/machine PATH와 알려진 native 설치 경로를 검색. 명시한 경로의 오류는 다른 실행 파일로 우회하지 않음 |
| 7 흰 우클릭 메뉴 | ContextMenu·항목·구분선·하위 Popup·기본 텍스트 메뉴까지 dark template 적용 |
| 8 숨김 필수 지침 | Role8 원문 규칙을 내장하고 `.mandatory-instructions.md` 관리 사본 생성. 사용자 규칙을 별도로 보존하고 최종 세 요청 경계에서 정확히 한 번 조합 |
| 9 PDF·설치 버튼 | 원본 한글 PDF를 WebView2로 표시. Markdown 대체 안내·기본 PDF 앱 연결·동일 테마 설치 확인 창과 원본 배치 실행 버튼 |
| 10 README | TASK020이 사용자 중심 설치·사용법·세 VSIX·PDF·설명서 ZIP·숨김/사용자 지침 구분을 최신화 |
| 11 감사 후 검증·배포 | TASK023 원본 보존, Claude metadata 인증 오판·CLI 검색·줄바꿈 검사 보완 후 최종 빌드·IDE·전체 반복·동봉 파일 검증. 배포는 마지막 통합 단계 |

수신 큐·요청 세대·최종 답변 순서는 TASK020의 `AssistantControl.Progress.cs` 소유를 유지했다. TASK022는 별도 ProgressPresentation/ProgressNotice/Viewport와 Guide·필수 규칙을 구현했다. 공유 빌드·IDE·패키지·Git은 협업 상태 파일로 조율했다.

## 최종 실제 제품 결과

최종 후보11의 2017 Release DLL SHA256은 `68B15E40EA79678F48A497010EF541D98871A8C70133A0FA458B2DDEABD0DA70`이다. 진단용 Guide stub은 포함하지 않는다.

| 검증 | 최종 결과 | 근거 |
|---|---:|---|
| TASK022 실제 기본 6그룹 | **245/245**, exit0 | `artifacts/task022/bundle-candidate11-final/summary.json` — 중앙72·viewport29·진행45·메뉴26·필수33·Guide40 |
| 활성 desktop 기본 렌더 | **56/56**, exit0 | `artifacts/task020/active-candidate11-20261018-232509/gpu/result.json`, RenderMode=`Default` |
| 실제 마우스 hover | **55/55**, exit0 | 같은 active 경로 `hover/result.json` — 기본45+추가10 |
| 실제 WebView PDF | **50/50**, exit0 | 같은 active 경로 `pdf/result.json` — 기본40+추가10 |
| 3 SKU Debug/Release | **6/6**, 경고0·exit0 | `artifacts/task020/build-candidate11-summary.log` |
| 실제 VS2017·2019·2022 | **각100/100** | TASK020의 최종 세 IDE 결과·로드 DLL SHA 대조 |
| 최상위 실행기 전체 | **2·3차 각16/16**, exit0 | `artifacts/tl/c292d4c9ac/launcher.log`, `artifacts/tl/524da2bf96/launcher.log` |

반복 기본 검사와 TASK021의 기본 렌더 56건을 새 고유 검사로 합산하지 않는다. TASK022는 **기본245 + hover 추가10 + WebView 추가10**으로 구분한다. 실제 PDF Runtime은 `155.0.4283.62`다. 전경 PID·화면 범위·최상위 HWND 9개 지점 가림 검사를 통과한 native PNG를 직접 보아 PDF와 전체 창이 가림 없이 표시되는 것을 확인했다. 설치 실행은 0회다.

최종 hover는 1초 동안 Opened1/Closed0과 본문의 실제 PresentationSource를 확인했고, 상세 진입·이탈 닫힘·클릭 고정·포커스를 받지 않는 소유 창 빈 영역 클릭 닫힘·재열기·Escape·열린 창의 Unload 해제까지 실제 마우스로 통과했다. WindowShown/NativePointerUsed/HoverExecuted 모두 true다.

실제 세 IDE의 진행 카드 6개는 모두 `Microsoft.VisualStudio.PlatformUI.MainWindow`에 연결됐다. VS2019/2022의 성공 결과 이후 남은 전용 IDE는 TASK020이 launch PID·RootSuffix·설치 exe를 대조해 자기 테스트 프로세스만 정리했다. 잔류 원인은 미확정이며 cleanup 증거를 보존했다. IDE full runner는 격리 프로토콜 fixture이고 유료 live 추론으로 계산하지 않는다.

## 원래 실패와 보정 경로

1. C8 최초 System.Drawing 참조 누락, 후속 VSTHRD001 및 C9 SDK17 생성자 VSTHRD010 경고를 보존했다. framework 참조와 정확한 WPF 호출/생성자 계약 구간만 보완했고 실제 VS COM 보호는 유지했다. 기존 주석 본문은 바꾸지 않았다.
2. C9 실제 hover는 PopupRoot native capture 이후 본문이 분리되는데 IsOpen=true가 남는 경계를 재현했다. 첫 anchor 검사는 IsOpen/Opened만 보아 실제 화면 안정성을 입증하지 못했다. 제품은 capture 없는 자체 hover/focus/Escape/deactivation 관리와 owner 바깥 클릭 닫기로 보정했다. 검사는 Closed0·본문 연결·자기 전경/표면 guard를 추가했다. `progress-hover-candidate9-actual`, `-diagnostics`, `-desktop` 원본을 보존했다.
3. C11 첫 선택 hover는 GetCursorPos 오류5, thread=`Default`/input=`Screen-saver`로 제품 동작 전에 중단했고 실제 실행 플래그 모두 false였다. 사용자 활성화 후 새 경로의 최종 실제 입력 검사가 통과했다. 잠금이 첫 C9 Popup 실패의 원인이라고 주장하지 않는다.
4. C11 전체 첫 실행은 **12/16**(`artifacts/tl/a914214f43/launcher.log`)이었다. 편집·Provider CLI 두 fixture는 필수 규칙 정확1회와 사용자 원문/별도 협업 문맥 보존 계약으로 갱신했다. Foundation은 IDE SDK DLL 금지를 유지하면서 WebView managed3/native loader3만 정확히 허용했다. 이 세 건은 제품 권한·필수 규칙을 제거한 수정이 아니다.
5. 나머지 Thinking 큰 글자 검사에서는 실제 두 시간 샘플이 멈췄다(52 PASS/1 FAIL, `artifacts/task020/thinking-candidate11-diagnostics/result.json`). 같은 DLL의 검사 프로세스만 SoftwareOnly로 하면56/56이었고 전체2·3차도 이 모드를 명시해 통과했다. Screen-saver를 유일한 근본 원인으로 확정하지 않는다. 제품·IDE 렌더 설정과 시계 코드는 바꾸지 않았다. 사용자 활성 desktop에서 `-UseHardwareRendering`으로 별도 **RenderMode=Default 56/56**을 확인했다. 이 스위치는 하드웨어 렌더 강제가 아니다. 초기 prototype의 namespace 오류도 원본 로그에 남겼다.
6. 처음 WebView native PNG는 다른 앱에 가려졌으므로 private 진단에만 보존했다. 최종 캡처는 자기 창 전경·가림 guard를 통과했고 직접 검토했다. TASK020의 별도 active 첫 시도 전경 실패도 최종 성공으로 덮지 않는다.

## 무결성·보안·한계

소유14개 제품 파일의 최종 동결은 `artifacts/task022/source-freeze5.json`이다. 빌드 후 14개와 원본 PDF·Markdown·배치3개를 대조해 변경0을 확인했다. 기본 검사에서 제품 DLL과 동봉 의존26개 해시는 전후 일치했다. 전체 TASK020 입력90개와 세 VSIX 각32개 동봉파일 대조는 TASK020 근거에 기록됐다.

고객 전달 ZIP은 PDF·MD·배치·PNG18개, 총21개인 **설명서 묶음**이며 확장 VSIX는 별도 제공한다. 21개가 모두 원본 및 후보11 세 SKU 자원과 일치했고 세 릴리스 VSIX도 후보11 고정본과 일치했다(`artifacts/task022/customer-zip-read-review.json`). README와 소유 보고서 로컬 링크31개 누락0을 확인했다.

필수 지침은 내장 정본을 최종 payload에 조합한다. 관리 사본의 변경 내용을 신뢰 지시로 읽지 않으며 기존 내용을 덮지 않는다. 설치 배치는 원본 SHA와 열린 파일 잠금·static 중복 gate·stale 종료·구독자 오류 경계를 검사했다. 자세한 근거는 [보안 보고서](CodexCode_TASK-022_SecureCoding.md), 구조화 결과는 [검증 JSON](CodexCode_TASK-022_Validation.json)이다.

규칙 조합 검사는 실제 AI가 모든 규칙을 따랐다는 증거가 아니다. 설치·로그인·실제 Claude 추론·운영 DB·외부 TFVC·운영 배포는 수행하지 않았다. 실제 Codex의 합성 C# 수정·실제 테스트는 TASK020의 **후보9** 실계정 근거이며 후보11 검사로 바꾸어 부르지 않는다. 라이선스 배치는 합법적인 검사·관찰 경로이고 활성화 우회 기능이 아니다. 기존 감사019/023 원문과 이전 릴리스는 보존했다.

## 재현

Windows PowerShell 5.1 STA와 새 출력 폴더를 사용한다. 실제 마우스/PDF는 활성 Default 입력 desktop에서 GUI를 순차 독점해 실행한다.

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task022/Test-Task022.ps1 -AssemblyPath <고정DLL> -OutputDirectory <새폴더>
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task022/Test-ProgressPresentation.ps1 -AssemblyPath <고정DLL> -OutputDirectory <새폴더> -VerifyHover
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task022/Test-CliGuide.ps1 -AssemblyPath <자원동봉고정DLL> -OutputDirectory <새폴더> -VerifyWebView
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task021/Test-ImageFollowup.ps1 -AssemblyPath <고정DLL> -OutputDirectory <새폴더> -UseHardwareRendering
```

로그는 결과 폴더의 형제 경로에 둔다. 기본 실행기는 최상위 `VisualStudio_AIChat_ExtentionTest.exe --all --vsix releases/v0.9.0/VS2017.AIAssistant-0.9.0.vsix`다. 이 보고서의 소유 검증 완료는 원격 배포 완료를 대신하지 않으며, 원격 SHA 확인 결과는 TASK020의 최종 배포 기록을 따른다.
