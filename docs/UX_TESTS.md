# 설정·첫 사용 안내 검증 (v0.4.0)

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
| UX-10 | 설정 창과 텍스트에 동일한 어두운 VS 테마 |
| UX-11 | 저장 후 닫힘, 다른 AI 설정·안내 선택 유지 |
| UX-12~13 | 저장 실패를 팝업 안에 표시하고 원본 파일 보존 |
| UX-14 | 컨트롤 재생성 후 자동 안내 억제·저장 설정 복원 |
| UX-15 | 안내 선택 해제 후 다음 자동 표시 복원, 다른 설정 유지 |

실행별 `ux/guide-light.png`, `guide-dark.png`, `settings-light.png`, `settings-dark.png`를 직접 열어 확인한다. UI 요소의 Margin 때문에 잘리지 않도록 VisualBrush로 본문 경계를 렌더한다. Foundation은 Chat의 밝은/어두운 테마·320/200폭도 검사한다. 독립 첫 사용 여정은 `tests/review/Test-OnboardingJourney.ps1`과 TASK-011 보고를 참고한다.

## 수동으로 추가 확인할 항목

실제 마우스 우클릭·키보드 전체 제스처, 여러 모니터·DPI, 스크린리더, 실제 Claude 로그인은 이 자동 검사의 완료 범위에 포함하지 않는다. 자동 검사는 실제 WPF 이벤트·명령 대상과 UI 상태를 검증하며, OS의 개인 클립보드를 덮어쓰지 않는다.
