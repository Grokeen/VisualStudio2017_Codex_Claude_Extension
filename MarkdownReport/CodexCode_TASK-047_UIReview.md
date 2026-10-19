# TASK-047 Role 22:07 화면·협업 요청 검토

- 담당: CodexCode
- 기준: `MarkdownReport/Role.md` 2026-10-19 22:07의 1~6번과 `image-40.png`~`image-43.png`
- 동시 작업: CodexOrig의 TASK-045 원점 툴팁 수정을 보존했다. 22:36 소유 반납 이후 `AssistantControl.xaml`의 설정 소제목 네 곳만 추가 변경했다.

| 항목 | 확인·조치 |
|---|---|
| 1. 설정·설명/안내 크기 | 설정과 시작 안내의 기본 크기를 작업 영역 여백 48을 남기는 최대 900×740으로 조정했다. 창은 계속 사용자가 크기를 바꿀 수 있다. 설정·안내에 동봉된 절차 그림의 최대 높이를 300→440, 대체 그림은 210→320으로 늘려 큰 창의 빈 영역을 줄였다. |
| 2. 대화 UI | XAML에서 사용자 메시지 오른쪽, AI 답변 왼쪽, 공개 작업 카드, 대화 영역 스크롤·별도 고정 입력란을 확인했다. 새 VS2017 DLL의 `chat-0.11.1-preview.png`도 확인했다. 이미지 41의 회색 사용자 말풍선과 달리 현재 제품의 사용자 메시지는 투명 배경과 보조 글자색이다. 이는 Role의 기존 명시 지시를 따른다. 합성 대화 화면이므로 실제 IDE와 실계정 답변은 별도 범위다. |
| 3. AI 간 협업 | 열린 같은 팀의 Claude·Codex 대화를 최대 3명 선택하면 각 동료의 CLI·모델 설정으로 순차 검토하고 주 담당 AI가 실제 회신을 종합하는 경로가 있다. 공개 v0.11.1 DLL의 협업 상태·주소 검사 60/60, CLI 협업 문맥 검사 15/15가 통과했다. 이는 실제 두 계정 인증·유료 모델 응답 성공과 구분한다. |
| 4. 기록 상세 크기 | 이미지 42의 주황 팝업은 `ProgressNotice.cs`의 고정 420px 상한 때문에 빨간 작업 카드보다 좁았다. 카드의 실제 폭에 맞추고 도킹 창 크기가 바뀌면 열린 상세 폭도 갱신하도록 했다. 화면 작업 영역 폭은 넘기지 않는다. |
| 5. 소제목 강조 | 작업 카드 제목과 상세의 ‘공개 작업 내역’을 16px로 올렸다. 설정의 ‘계정 연결·응답 모델·공통 작업 규칙·화면 표시’ 네 제목은 18px로 올렸다. WPF 렌더에서 계정 제목을 확인했다. |
| 6. 중복 툴팁 | 이미지 43의 작업 카드 헤더에서 hover 팝업과 기본 ToolTip이 동시에 나왔다. 헤더 ToolTip을 제거하고 키보드 안내는 접근성 HelpText에 유지했다. 부채꼴 원점의 별도 툴팁은 TASK-045가 처리한다. |

## 최종 검사

- `tests/task024/Test-HoverViewport.ps1` 무창 source-only **38/38 통과**. 폭 520→310 레이아웃과 툴팁 중복 부재를 새로 검사했다. 증거: `artifacts/task047/hover-source2/result.json`.
- 네 대상 `Release` 빌드 **4/4 성공**, 빌드 로그에서 경고·오류 없음. `artifacts/task047/build/`.
- 새 제품 DLL `Test-HoverViewport.ps1`은 VS2017.Legacy/2017/2019/2022 각각 **38/38 통과**. VS2017 DLL SHA-256 `3C6DE2269CE88A65F8529EFEA5F9DD35B0A4AEFEF743A14BF5EA2F93A9644E94`; 결과 `artifacts/task047/hover-product-*/result.json`.
- 새 VS2017 DLL `Test-DialogGuide.ps1` **23/23**, `Test-ProgressPresentation.ps1` 무창 **45/45**, 선택적 실제 마우스 hover·click·Escape 포함 **55/55** 통과. 실제 hover에서 창 표시·물리 포인터 사용이 기록되었다. 결과 및 캡처: `artifacts/task047/dialog-guide-2017/`, `artifacts/task047/progress-product-2017/`, `artifacts/task047/progress-native-2017/`.
- 새 VS2017 DLL에서 협업 상태·주소 `Test-ConversationCollaboration.ps1` **60/60**, CLI 협업 문맥 `Test-ProviderCliIntegration.ps1` **15/15** 통과. 실계정·유료 AI 호출은 하지 않았다.
- 실제 제품 WPF의 합성 대화·설정·안내를 `tests/ux/Render-UserExperience.ps1`로 렌더하고 `artifacts/task047/ux/` 이미지를 확인했다. 설정·안내 창은 884×704 내용 캡처로 확대되고 계정 소제목은 강조된다. 테스트 예시 이미지와 실제 IDE 동작을 혼동하지 않는다.
- 공개 v0.11.1 VS2017 DLL SHA-256 `38C447920501991D15685C8DEC35485D4B1757ADC8CF6F65AE97A5723DFD7CCA`에 대해서도 수정 전 협업 60/60·CLI 문맥 15/15를 통과했다.
- 기존 `Test-ProgressPresentation.ps1` source-only와 `Test-ButtonCenters.ps1` source-only는 각각 현 제품에서 추가된 `HoverScrollPolicy`/`PopupPalette`, `MarkdownLinkEventArgs`가 오래된 fixture에 빠져 컴파일 단계에서 중단됐다. 제품 실패로 취급하지 않으며 성공한 HoverViewport source-only 검사와 최종 제품 빌드를 기준으로 판정한다.

## 범위와 남은 검증

이번 변경은 로컬 소스·빌드·검사 결과다. 기존 정식 v0.11.1과 오프라인 테스트 사전 릴리스에는 포함되지 않았다. 고객의 VS2017 Professional 15.7.1 오프라인 PC 설치·IDE 실행 결과는 TASK-041이 별도로 수집 중이다. 실제 두 AI 계정의 왕복 협업도 이 검사의 모의 CLI·저장 검증과 구분한다.
