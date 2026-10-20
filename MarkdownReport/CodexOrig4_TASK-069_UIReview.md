# TASK069 UI 보완 — CodexOrig4

2026-10-20 12:47에 추가된 `Role.md`의 #3, #6, #7 및 원본 이미지 `image-70.png`, `image-73.png`, `image-74.png`를 직접 확인했다. 작업 범위는 작업기록 카드, 독립 Visual Studio 테마 생성기와 격리 검사로 한정했다. 설치기·IDE 테마 설정 버튼, 공용 `bin/obj`, 버전, commit/push는 변경하지 않았다.

`image-70`은 포인터가 작업기록 카드 위를 지나갈 때 상세 Popup이 자동으로 열리는 경로를 보여준다. `ProgressNoticePresenter`에서 헤더 `MouseEnter` 구독을 제거해 카드 hover로 자동 공개하지 않도록 바꾸고, 기존 클릭/Enter 열기와 재클릭/닫기 동작은 유지했다. UI Automation 도움말도 명시적 클릭 또는 Enter 안내로 갱신했다.

`image-73`에서 보이는 Dracula 스크롤 문제는 생성된 XML의 `Environment/ScrollBarBackground`와 `ScrollBarThumbBackground`가 모두 `#44475A`로 변환되는 팔레트 충돌이다. 트랙을 `#282A36`, 손잡이를 `#6272A4`, hover를 `#BD93F9`, 누름 상태를 `#FF79C6`로 분리했다. #7의 이전 Kakao 생성본은 탭 normal/hover/selected surface가 서로 가깝고 hover 글자 색이 다른 Visual Studio token에 따라 회색 또는 검정으로 매핑될 수 있었다. 탭 기본은 연한 중립색+짙은 글자, hover는 차콜+흰 글자+Kakao 노랑 경계, 선택은 Kakao 노랑+짙은 글자로 역할을 구분했다. 문서 탭, 도구 창 탭, 자동 숨김 탭에 동일 hover 의미를 적용했다.

격리 검사 결과는 `artifacts/task069/orig4/full-check-3/result.json`에 있다. VS2017/2019/2022 theme VSIX 3종은 각 오류·경고 0으로 생성됐다. Dracula 손잡이/트랙 대비는 3.03:1, hover 손잡이는 5.9:1, Kakao hover 흰 글자/차콜 바탕은 12.11:1, 선택 노랑/짙은 글자는 13.75:1이다. WPF 무창·무포인터 소스 검사는 작업기록 hover 이벤트 구독 0개와 click handler 1개, 접근성 도움말 및 기존 진행 표시 47/47 회귀를 확인해 전체 12/12 검사를 통과했다.

이 증거는 독립 테마 컴파일과 무창 WPF 검증이다. 실제 Visual Studio에 테마를 설치하거나 IDE UI를 띄워 스크롤·탭을 재확인하지 않았고, 새 릴리스 버전도 정하지 않았다. Root의 통합 후보에서 예정된 오프스크린 캡처/실IDE 확인이 남아 있다.

## C3 고정 후보의 실제 DLL 오프스크린 확인

2026-10-20에 `artifacts/task069/candidate3/2017/Release/VS2017.AIAssistant.dll`을 직접 로드했다 (SHA256 `EDB57654F5D455C73DF2BCBBDC191206D06252A663A2D7DE059BE6184D94751E`). 동봉된 프로젝트 assets도 SHA256 `B90AF315ADA4FAF4918863FE425FD214195DC9192BC6F37B7B521026046D859F`로 고정본과 일치했다. 검사기는 보이지 않는 HwndSource를 화면 바깥 좌표에 두고 실제 후보 DLL의 ProgressNoticePresenter를 로드했다. 카드와 Popup에 dark/light semantic brush 사전을 공유해 실제 PopupPalette의 host-resource 연결 방식을 재현한 뒤, 두 모드에서 카드와 열린 상세 패널을 RenderTargetBitmap으로 캡처했다. 네 PNG를 확인해 글자·행 간격·카드/상세의 배경과 경계가 각 모드 안에서 일관적인지 살폈다.

두 모드 모두 hover 뒤 Popup은 닫힌 상태, 실제 routed click 뒤 열린 상태, 두 번째 click 뒤 다시 닫힌 상태였다. 접근성 이름과 도움말에 작업 상세 및 클릭/Enter 안내가 남아 있었다. 검증 중 창을 표시하거나 OS 포인터를 움직이지 않았다. 기계 판정과 캡처는 `artifacts/task069/orig4/candidate3-actual-ui/actual-ui-results.json` 및 `progress-dark-card.png`, `progress-dark-details.png`, `progress-light-card.png`, `progress-light-details.png`에 기록했다. 실행은 `tests/task069-orig4/Run-C3ActualUiChecks.ps1` 및 동반 C# 검사기다.

C3 Assistant VSIX 내부의 VS2017 테마 패키지 SHA256은 `6BDA86E6122DAF7BCE52B77A4121684121030A14930DEB3484F1459E07B1A4C0`이고, 독립 0.14.0 theme-final1 패키지와 바이트 단위로 같다. 실제 생성 XML을 다시 읽어 Dracula track/thumb/hover/pressed가 각각 `FF282A36`, `FF6272A4`, `FFBD93F9`, `FFFF79C6`, Kakao 탭 기본/hover/선택이 `FF191919` 텍스트, `FF34363A` hover와 흰 텍스트/노랑 경계, `FFFEE500` 선택과 `FF191919` 텍스트로 들어간 것을 확인했다. 이 검사는 오프스크린 화면과 패키지 내용 확인이므로 실제 Visual Studio에서 테마 적용 후 스크롤·탭을 조작한 통합 확인은 여전히 별도다.

## C5 Role #14~#16 unified UI verification

C5 source changes for Role #14~#16 are recorded in `WORK_STATUS.md`. The exact C5 VS2017 DLL SHA256 is `17E93831EF6CD71A7E20952F151CD9AD4174402B71625434BCB85E6D90E5561E`; its `project.assets.json` SHA256 is `BB6BD39F3C98162592751A94E586F5873EC2807CB7B255B4EA267C7A4E2D3B4C`. Both hashes remained unchanged before and after the captures.

The hidden WPF control rendered 15 PNGs at 620x760 and 390x760 for VS dark/light, Kakao dark/light, and Dracula dark, plus send/stop-state screenshots. Private settings/workspace and a fixed `사용자 · Codex` identity replaced the Windows profile in visible UI; the actual Windows username did not appear in the result text. No product request was sent, no AI/CLI/network was invoked, and no visible window, popup, or OS pointer action was used.

The live WPF state checks show Generate alone for an idle selected conversation, Generate still visible if only another room is running, and Stop alone for the selected running or waiting room and the legacy FIFO. User bubbles use the exact ComposerSurface brush. AI bubbles use a darker, bounded palette brush with measured text contrast of 13.67:1 (VS dark), 13.87:1 (VS light), 13.28:1 (Kakao dark), 15.81:1 (Kakao light), and 12.05:1 (Dracula). Ten pairs of synthetic messages produced 2417 px of scroll range while the fixed header Y coordinate remained 93.24 px before and after scrolling.

The watermark stayed at opacity 0.055 and 170 px. A pixel difference between a message-filled render with the watermark visible and the same render with it collapsed counted 2,591 changed pixels, confirming it was not fully masked by the new reply backgrounds. No overlay or opacity adjustment was needed. Full result and images: `artifacts/task069/orig4/candidate5-ui14-16-pass3/capture-result.json` and PNGs. This is a hidden offscreen WPF RenderTargetBitmap test, not a physical IDE or desktop capture.
The final C5 capture run at `artifacts/task069/orig4/candidate5-ui14-16-pass4/` also walks the actual WPF visual tree. It found 20 rendered message bubbles across the five palettes and compared their runtime `SolidColorBrush` values to the live ComposerSurface and reply palette resources; all ten per-role assertions passed. Overall C5 WPF checks are 24/24 with 15 anonymous PNGs; `capture-result.json` and `manifest.json` retain the exact before/after DLL/assets hashes.