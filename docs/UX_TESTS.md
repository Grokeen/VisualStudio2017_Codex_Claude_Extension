# 설정·첫 사용 안내 검증

## v0.9.0 추가 이미지 요구

`tests/task021/Test-Task021.ps1 -AssemblyPath <고정 DLL> -OutputDirectory <새 폴더>`는 Windows PowerShell 5.1 `-STA`에서 실행한다. 선택 코드·색상 diff·다중 파일 왕복 선택·상세 도움말·200px 부채꼴의 실제 경계·생각 중 그라데이션·설정 저장 경쟁·승인 작업·문서 worker를 검사한다. 원본 실패도 보존한 [TASK-021](../MarkdownReport/CodexCode_TASK-021.md)과 최종 통합 [TASK-020](../MarkdownReport/CodexOrig_TASK-020.md)에 DLL별 결과를 구분한다.

루트 실행기의 기본 검사는 16묶음이며 실제 IDE와 유료 AI 호출은 별도 선택이다. `native-runtime`은 자식 프로세스 기반 프로토콜 46개, `image-followup`은 위 이미지 요구 묶음이다. UI 검사에서는 자동 계정 조회를 꺼 개인 CLI에 연결하지 않는다. 네이티브 실계정 검사는 별도의 명시적 실행으로 구분한다.

## 기존 설정·첫 사용 안내

Role.md 프로젝트8~11번을 구현했다. 설정 버튼과 우클릭 메뉴는 동일한 CenterScreen 모달 창을 연다. 저장 성공 후 닫고, 닫기·Esc·X는 미저장 설정을 복원한다. 저장 오류는 팝업 안에 표시한다. 처음 실제로 표시되는 Chat은 네 단계 안내를 열며 `HideWelcome` 선택을 설정 파일에 저장한다. 이 선택만 변경할 때는 기존 파일을 읽어 다른 설정을 보존한다.

## 자동 검사

빌드 후 Windows PowerShell 5.1에서 실행한다.

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\editing\Test-SourceEditing.ps1
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\ide\Test-IdeIntegration.ps1
```

`tests/ux/DialogChecks.cs`를 두 실행 경로에서 공통 컴파일한다. 실제 제품 WPF 창을 열고 Dispatcher 타이머로 테스트 소유 팝업을 검사한다. 개인 설정은 별도 경로로 격리하고 종료 후 환경 변수를 복원한다. 파일 저장 실패도 디렉터리 충돌로 재현한다. IDE 실행기는 실제 메뉴로 여는 기본 Chat의 안내를 격리 설정으로 숨긴 후 별도 제품 컨트롤에서 첫 실행을 재현한다.

| 사례 | 기대 결과 |
|---|---|
| UX-01~02 | 실제 Loaded에서 네 단계 안내 자동 표시, CenterScreen, 체크 기본 해제 |
| UX-03 | 다시 보지 않기 저장, 두 CLI 경로·모델·규칙·크기 보존 |
| UX-04~06 | 입력의 Cut/Copy/Paste/SelectAll 대상 유지, 비교는 Copy, 설정·사용 안내 메뉴 존재 |
| UX-07 | 숨김 상태에서도 수동 안내 재열기 |
| UX-08 | 우클릭 설정이 화면 중앙에서 열림, 실제 창 좌표 검사 |
| UX-09 | 취소 후 경로·규칙·글자 크기 및 디스크 원상 유지 |
| UX-10 | 밝은/어두운 IDE 호스트에서도 설정 창·텍스트에 동일한 제품 검정 팔레트 |
| UX-11 | 저장 후 닫힘, 다른 AI 설정·안내 선택 유지 |
| UX-12~13 | 저장 실패를 팝업 안에 표시하고 원본 파일 보존 |
| UX-14 | 컨트롤 재생성 후 자동 안내 억제·저장 설정 복원 |
| UX-15 | 안내 선택 해제 후 다음 자동 표시 복원, 다른 설정 유지 |
| UX-16 | 공통 instructions.md를 잠가 저장을 실패시키면 기존 Markdown과 JSON 원문 모두 보존 |

실행별 `ux/guide-light.png`, `guide-dark.png`, `settings-light.png`, `settings-dark.png`를 직접 열어 확인한다. UI 요소의 Margin 때문에 잘리지 않도록 VisualBrush로 본문 경계를 렌더한다. Foundation은 밝은/어두운 IDE 호스트·320/200폭에서도 제품 팔레트가 유지되는지 검사한다. 추가 대화·모델·키보드·저장 실패·좁은 화면 검증은 `tests/review/Test-WorkspaceExperience.ps1`을 사용한다. 문서용 화면은 `tests/ux/Render-UserExperience.ps1 -AssemblyPath <고정 DLL> -OutputDirectory <출력 폴더>`로 렌더하며 예제 대화는 합성 데이터다.

## 수동으로 추가 확인할 항목

실제 마우스 우클릭·키보드 전체 제스처, 여러 모니터·DPI, 스크린리더, 실제 Claude 로그인은 이 자동 검사의 완료 범위에 포함하지 않는다. 자동 검사는 실제 WPF 이벤트·명령 대상과 UI 상태를 검증하며, OS의 개인 클립보드를 덮어쓰지 않는다.

## v0.7 입력·협업 회귀

tests/review/Test-WorkspaceExperience.ps1와 AdvancedExperienceChecks.cs는 총101개 검사다. 모델 상세 팝업의 실제 슬라이더/저장·지원 수준에 따른 Fast 해제, 파일 clipboard의 비동기 본문, 동료 호출과 실제 결과 전달, 중지 및 보고서 잠금에 따른 원인 오류 보존, 전달 완료 후 대화 저장 실패, AI JSON/대화 Markdown 사이 롤백을 포함한다. Windows PowerShell 호스트에서는 DispatcherSynchronizationContext를 명시해 실제 WPF 이벤트 루프와 같은 스레드 복귀를 검사한다.

## v0.8 이미지별 상호작용

tests/review/Test-WorkspaceExperience.ps1은 모달에서 anchor Popup으로 바뀐 경로와 A~Z 실제 대화 라우팅을 검사한다. 안내 hover에서 다른 카드 dim·표면 이동·고정 hit 영역, 상세의 실제 ImageSource, 복귀 시 키보드 포커스, 설정 목차의 실제 스크롤·고정 footer, 옵션 fan의 반복 진입/공통 경계/Escape/타이머 정리를 포함한다. 표시 예제 화면은 docs/assets/*-0.8.png이며 합성 자료만 사용한다. tests/task017/Test-DialogGuide.ps1의 안내23개와 collaboration/Test-AlphabetAgentRouting.ps1의 이름40개는 루트의 image-requirements 기본 묶음에 포함된다.

## v0.9 후속 이미지와 PDF

`image-followup`은 TASK021 8그룹과 TASK022 6그룹을 순서대로 실행한다. TASK021의 이미지 검사는 격리 프로세스에 `SoftwareOnly` 렌더를 지정하고 실제 WPF 시간 샘플로 그라데이션 이동을 검사한다. 비활성 Windows desktop에서 GPU 렌더가 멈추는 조건과 제품 애니메이션을 분리하기 위한 설정이며 제품이나 IDE의 렌더 설정은 변경하지 않는다.

활성 화면의 기본 GPU 경로를 별도로 확인하려면 `tests/task021/Test-ImageFollowup.ps1 -UseHardwareRendering`에 고정 DLL과 새 출력 폴더를 지정한다. 이 결과와 소프트웨어 렌더 결과는 구분해 기록한다.

TASK022는 버튼 중앙·작은 대화 영역의 고정 입력란·어두운 기록·중복 진행 상태·우클릭 메뉴·필수 지침·PDF 자원 경계를 검사한다. 실제 포인터 이동과 WebView2 PDF 표시는 `tests/task022/Test-ProgressPresentation.ps1 -VerifyHover`, `tests/task022/Test-CliGuide.ps1 -VerifyWebView`로 별도 실행한다. 입력 desktop과 자기 창의 전경·포인터 대상을 확인하며 화면 보호기나 잠금 상태에서는 실제 입력 검사를 수행하지 않는다. 기본 검사 통과를 실제 마우스 검사 완료로 대신 보고하지 않는다.
