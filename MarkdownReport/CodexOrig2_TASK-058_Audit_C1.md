# CodexOrig2 2차 감사 보고 — TASK-058 C1

감사 시각: 2026-10-20 02:51 KST  
대상: `artifacts/task054/candidate1` 버전 0.12.1, Role.md 2026-10-20 01:40 추가 요청 #1–8·#11–14 및 image56–65. #15·#16은 C2의 별도 변경 범위로 추적한다.  
판정: **부분 통과, C1 수정 1건 필요.** C1을 전체 UI·설치·배포 합격으로 판정할 근거는 아직 없다.

## 고정 후보와 감사 방법

`source-hashes.json`의 128개 입력을 개인 복사본과 다시 대조해 불일치 0개였다. 네 대상 Release 산출물과 VSIX의 SHA-256을 [고정 기록](../artifacts/audit-codexorig2-task058/candidate1-freeze.json)에 남겼다. 각 대상 빌드 기록은 오류 0·경고 0이며, 실제 실행에 쓴 VS2017 DLL은 `3E527BBC4D93BD0E0B9969B06C85651F2BAD2CD74A1C650E5CA347564053A995`다. 테스트는 개인 임시 폴더와 합성 입력만 사용했다. 제품 소스·공유 빌드·계정·GUI·배포를 변경하지 않았다.

## 확정 지적

**R58-01 / 보통(P2) / #8 완료 알림에 프롬프트의 사적 정보가 그대로 표시된다.** 고정 DLL의 `CompletionNotificationContent.SafeRequestTitle`을 직접 호출했다. 합성 요청 `직원 홍길동의 연봉 9000만원 보고서를 검토해줘`가 축약·비식별화 없이 동일하게 반환됐다. 반면 `password=abc123`은 `password=[숨김]`으로 바뀌었다. 구현은 지정된 토큰·주소·메일 패턴만 치환하고 일반 문장 속 사람 이름·금액·내부 업무 정보는 그대로 남긴다. `CurrentRequestTitle`은 원문 `Instruction`이고 `StartCompletion...` 경로가 이를 `Body`에 전달해 Windows 알림 본문 `요청: ...`에 넣는다. 알림을 켜 둔 PC나 잠금 화면의 노출 여부는 OS 정책에 좌우되지만, 제품이 해당 문자열을 OS 알림에 전달하는 것은 코드와 실제 DLL 결과로 확인된다. 브랜드별 아이콘 자체와 OS 팝업 실제 표시 여부는 이번 무창 감사에서 검증하지 않았다.

수정 권고: 원문 프롬프트를 정규식으로 부분 삭제하는 방식 대신, 앱이 아는 작업 종류·완료 건수처럼 비밀이 아닌 구조화된 상태로 한 줄 요약을 생성한다. 정확한 작업명을 사용자에게 보여줘야 한다면 별도 명시적 알림 표시 설정을 둔다. 수정 후보의 실제 DLL에서 자유 문장·인명·금액·프로젝트명·경로·인증 정보·긴 텍스트를 다시 검사한다. 근거: [알림 구현](../src/VS2017.AIAssistant/Views/CompletionNotification.cs), [전달 경로](../src/VS2017.AIAssistant/Views/AssistantControl.ProgressPresentation.cs), [독립 실행 결과](../artifacts/audit-codexorig2-task058/candidate1-boundary-result.json).

### 다음 후보 C2로 이월한 추가 요청

#15 image66 사용자 메시지 대화형 표시와 #16 IDE 테마 설치·적용은 C1의 #1–14 감사 합격 여부에 넣지 않는다. 참고로 C1의 [대화 템플릿](../artifacts/task054/candidate1/source/src/VS2017.AIAssistant/Views/AssistantControl.xaml)은 사용자 메시지를 투명·무테·무패딩으로 표시하고, 네 C1 VSIX ZIP에는 `Themes/` 또는 중첩 `.vsix` 항목이 각각 0개다. 이 관찰은 C2 구현 전 기준점이며 C1의 추가 결함 건수로 세지 않는다. C2에서는 image66의 대화 구획/줄바꿈, SKU별 테마 패키지와 설정→설치→VS 재시작/적용을 실제 IDE로 검증한다.

## 요구별 판정

| ID | 판정 | 근거와 남은 경계 |
|---|---|---|
| V01 #1 팝업/IDE 구분 | PARTIAL | Designer의 VS dark/Kakao/Dracula 무창 캡처와 caption 수정17/17을 읽고 실제 C1의 `AssistantDialogs.cs` 해시 일치를 확인했다. 실제 Visual Studio host의 소유창·겹침·Esc/Alt+F4·DPI는 미실행. |
| V02 #2 Markdown UI | PARTIAL | Designer의 `vs-dark-markdown.png`에서 checkbox와 도구 버튼의 스타일 통합, 편집기/하단 배치 개선이 보인다. 실제 C1 IDE 두 크기/키보드 상태 검사는 미실행. |
| V03 #3 전체 버튼 | PARTIAL | Caption 수정 증거 17/17, 공유 template 및 검은 배경의 대표 캡처가 있다. 모든 화면·팔레트의 hover/focus/disabled 실제 픽셀 행렬과 접근성 이름은 미확인. |
| V04 #4 안내 footer | PARTIAL | Designer 캡처의 고정 하단과 여백은 개선됐다. 실제 host 최소창·DPI·교차 영역은 미측정. |
| V05 #5 탭 간격 | PARTIAL | Designer의 200px 탭 캡처에서 과도한 공백은 줄었다. 실제 C1 header box 거리·클릭·키보드 선택은 미측정. |
| V06 #6 탭 전환 | PASS(무창 지연), PARTIAL(IDE) | C1 무창 WPF에서 변경 없는 전환 40회 총 806ms·디스크 쓰기0. 추가로 제공자별 100개×8,000자 메시지를 채우고 warm-up 10회 뒤 50회 개별 전환을 측정해 p95 40.3188ms, p99/max 76.1844ms였다. 사전 제안한 200/350ms 이하다. 실제 IDE click→화면 반응·빈 화면 여부는 별도 검증해야 한다. 실행·대기 중 provider 선택은 현 구현이 되돌린다. |
| V07 #7 연속 입력/FIFO | PASS(합성 통합) | C1 DLL의 WPF/합성 CLI 실행 56/56. 실행 중 입력·전송, A→B 순서, 선행 답변 문맥, 저장 실패 시 100개 복원, 대기8 상한, 승인/실패/취소/종료 경계를 포함한다. 실제 IDE 타이핑/50건 스트레스는 미실행이므로 UI 전체 PASS는 아니다. |
| V08 #8 알림 | **FAIL** | R58-01. 브랜드·OS 표시/중복률은 별도 실제 GUI 근거 필요. |
| V09 #11 fan 간격/색 | PARTIAL | 제품의 오프스크린 fan 검사와 시각 자료는 있으나 C1 실제 IDE의 화면좌표·대비·포인터 경로는 미확인. |
| V10 #12 hover/motion | PARTIAL | 220ms 열기·280ms 닫기 근거는 제품/소유자 검사에 있다. 실제 IDE 포인터 왕복과 프레임·가림은 미확인. |
| V11 #13 파일 생성 | PASS(직접 API), PARTIAL(UI) | C1 DLL을 직접 호출한 독립 15경계 중 14통과. MD/TXT/HTML/DOCX/CS를 새로 만들고 취소·기존 파일 보존·상대/UNC/ADS/`..`/장치명/실행 확장자 거절을 확인했다. DOCX ZIP 본문에 한글·emoji·문단·탭, 외부 관계 0을 확인했다. 유일한 실패는 V08 알림 사례다. 소유자 실제 제품 메뉴68/68은 참고 근거이나 저장 대화상자의 물리 클릭, Word 화면 렌더, VS host 통합은 미실행. |
| V12 #14 Claude 로그인 없이 | PASS(비인증 경계), OUT OF SCOPE(실답변) | 소유자의 native Claude CLI 서명·버전·비인증 상태 및 실제 C1 네 DLL별 합성 경계 25개씩 100/100 기록을 대조했다. 미로그인 상태의 실제 AI 답변 성공으로 간주하지 않는다. 사용자 지시대로 로그인·실계정 요청은 수행하지 않았다. |
| #15 사용자 요청 대화 표시 | C2 이월 | C1 기준점만 기록했다. TASK056 후속 후보에서 별도 판정한다. |
| #16 VS 테마 설치·적용 | C2 이월 | C1 기준점만 기록했다. TASK063 후속 후보에서 별도 판정한다. |

## 증거와 제한

- [독립 경계 실행기](../artifacts/audit-codexorig2-task058/Test-Candidate1Boundary.ps1), [결과 JSON](../artifacts/audit-codexorig2-task058/candidate1-boundary-result.json): 14 PASS·1 FAIL. 결과 SHA-256 `627838F8C89ACE3544D41C63409C71289B7B76AC4E1792477053B26C61828570`.
- [C1 FIFO 실행 결과](../artifacts/audit-codexorig2-task058/candidate1-queue-result.json): 56/56, 40회 806ms, 쓰기0, 실제 DLL SHA 일치. 결과 SHA-256 `4D36E9CDE7863BBE6565C888DBD2358173F55BE4E1804B85064C725838454936`.
- [100개 긴 메시지 탭 전환 원자료](../artifacts/audit-codexorig2-task058/candidate1-long-tab-latency.csv), [요약](../artifacts/audit-codexorig2-task058/candidate1-long-tab-summary.txt), [고정 DLL 검사 결과](../artifacts/audit-codexorig2-task058/candidate1-long-tab-result.json): 제공자당 100개×8,000자, 50회 p95 40.3188ms·p99 76.1844ms. 무창 WPF 조건이며 IDE 클릭/렌더 지연이 아니다.
- [Designer caption 수정 증거](../artifacts/task056/CaptionFixEvidence.json): 17/17. C1 소스 해시와 일치하지만 이는 실제 C1 IDE 검사의 대체 자료가 아니다.
- [파일 메뉴 소유자 C1 실행](../artifacts/task061/product-candidate1/result.json): 68/68, 네 팔레트의 실제 제품 WPF 메뉴. 독립 실행과 구분한다.
- [Claude 소유자 C1 검사](CodexOrig_TASK-060_ClaudeVerification_C1.json): 네 대상 각각 합성 25/25. 사용자 지정 비로그인 범위와 구분한다.

수정한 알림 문구를 포함하는 다음 고정 후보가 나오면 새 산출물 해시에서 R58-01과 연관 알림을 재감사해야 한다. #15·#16도 C2에서 별도 감사한다. V01–V06/V09–V10의 최종 제품 판정에는 Root가 예약한 실제 IDE GUI·물리 입력 증거가 필요하다. C1의 원래 PDF는 0.12.0이라 최신 릴리스 문서와 다운로드/설치 일치도 이 후보에서는 미확인이다.
