# TASK-028 — 3차 독립 감사 보고서

- 감사자: **CodexOrig2**, 역할: 감사. 시작 2026-10-19 01:51:45 KST. 결과 기준: 2026-10-19 02:35 KST의 고정 candidate4.
- 기준: Role.md의 기존 규칙·1/2차 감사와 **2026-10-18 23:45 사용자 원문 15항목**. CodexOrig3 문서 제안 5항목은 별도 대조했다.
- [검증 JSON](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-028_Evidence.json), [직접 실행 원문](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-round3/direct-results.json), [감사 요청서](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig_TASK-025_AuditRequest.md).

## 판정

**새 결함 2건은 candidate4에서 수정 확인. 원본 Markdown 작성·미리보기의 이식 차이가 남아 있어 전체 반영 완료 판정은 보류한다.**

candidate3에서 실제 제품을 사용해 상대 파일 링크가 잘못된 파일을 여는 문제(R3-01), SQL 미리보기의 토큰 색상 누락(R3-02)을 재현했다. 개발 담당자가 수정한 candidate4에 동일 조건을 적용해 두 문제의 해소를 직접 확인했다. 기능 202/202, 세 버전의 실제 DLL 테마 각 31/31도 감사자의 개인 사본에서 통과했다.

그러나 사용자 원문 4의 “작성란의 모든 부분”과 비교하면 문서 폴더·목록, 현재 파일 바로 저장, 이름 변경·삭제·위치 열기, 참조형 링크·HTML 미리보기에 차이가 있다(R3-03). 이는 단순한 검사 미실행과 구분되는 **확인된 기능 범위 차이**다. 개발 담당자가 후속 보완을 진행 중이며, 그 변경을 포함하지 않은 candidate4의 통과를 향후 후보의 통과로 대신할 수 없다.

실제 마우스 hover 28/28와 실제 창의 GIF 수명 24/24는 TASK024의 candidate4 실행 자료를 확인했다. 감사자는 해당 GUI 검사를 직접 실행하지 않았다. 전체 16묶음 회귀·최종 세 IDE 검증·후속 후보 반영·배포는 별도 통합 작업이며 이 보고서는 릴리스 완료 선언이 아니다.

## 대상 고정과 감사 방법

| 대상 | 고정 근거 |
|---|---|
| 검토 Role 원문 | SHA256 `0EF513021D252BE48441D7813D98A05CD615E493420B9F0A72C4D39E4C0B976C` |
| candidate3 입력 | 118개 전부 사본 해시 일치. 목록 SHA `37711B833067202ED58BE5C7591309B38CCA0BC61577FB487536AAC4FA4D6D0D` |
| candidate4 입력 | 117개 전부 사본 해시 일치. 목록 SHA `61A51827910C1511CE8AD10DFE18CE9D1FF255527A33DCA662DAC430F42D9796` |
| 개인 사본 | `C:/dydgyqud/AO28_20261019`; candidate3와 candidate4 및 최초 실패 원문을 별도 보존 |
| 감사 산출 근거 | `artifacts/audit-codexorig2-round3/` |

candidate4 입력 목록에서 원래 설치 PDF 한 파일이 빠져 117개가 됐다. 이를 제품 삭제로 단정하지 않았다. 해당 PDF를 포함한 보호 대상 26파일은 앞선 보존 기록의 SHA와 직접 대조해 전부 일치했다. 실제 패키지의 선택 핵심 파일은 별도로 ZIP 내부에서 검사했다.

제품 소스·공용 bin/obj·staging·과거 릴리스·기존 감사 보고서·제공 프로젝트는 편집하지 않았다. 감사 중 제품 빌드·실제 CLI 로그인·설치·유료 AI·원격 배포도 실행하지 않았다. WPF 객체 검사는 창을 띄우지 않고 수행했으며, 다른 작업자의 GUI 예약과 분리했다. 첨부 이미지28~38과 자체 Light/Dark 렌더 이미지를 직접 열어 대조했다.

## 발견 사항과 수정 확인

### R3-01 / P2 — 명시적으로 연 Markdown의 상대 링크가 프로젝트 파일로 이동

- 최초 상태: candidate3 결함 직접 재현. 최종 상태: **candidate4에서 수정 직접 확인**.
- 재현: `documents/readme.md`에 `[Sibling](chapter.cs#L7)`을 작성하고 `documents/chapter.cs`, `project/chapter.cs`를 모두 둔다. 실제 MarkdownEditor로 문서를 연 뒤 제품 Hyperlink의 Click을 발생시키고 실제 AssistantControl 링크 처리기로 연결한다. 탐색 서비스만 기록용 fixture로 대체한다.
- candidate3: 편집기의 기준 폴더는 documents인데 실제 이동은 **project/chapter.cs 7행**이었다. 같은 이름의 다른 파일을 열 수 있다.
- 원인: 고정 C3의 `AssistantControl.Markdown.cs:120`이 프로젝트 디렉터리를 사용하고 링크 정보에 문서 기준 경로가 없었다.
- candidate4: **documents/chapter.cs 7행**으로 이동했다. 렌더 당시 문서 기준 폴더를 링크 이벤트에 보존하고 호스트에서 검사해 사용한다. 일반 AI 응답의 프로젝트 기준 동작도 기능 회귀에 포함돼 통과했다.
- 수정 근거: [C4 링크 정보](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate4/source/src/VS2017.AIAssistant/Views/MarkdownRendering.cs:32), [C4 호스트 처리](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate4/source/src/VS2017.AIAssistant/Views/AssistantControl.Markdown.cs:121). 원문 관측값은 direct-results.json의 candidate3-probe/candidate4-probe에 있다.

### R3-02 / P2 — 제공 프로젝트의 SQL·소스 미리보기 색상 누락

- 최초 상태: candidate3 직접 재현. 최종 상태: **candidate4에서 수정 직접 확인**.
- 재현: 실제 MarkdownPreview에 SQL fence `SELECT name FROM Users WHERE Id = 1; -- note`를 렌더하고 언어 제목을 제외한 본문의 Run과 색을 조사한다.
- candidate3: 본문 **1 Run·1색**. 원본 `MarkdownnRenderer.cs:147~155`는 코드 토큰에 색을 적용한다. 이식된 편집기의 문법 강조만으로 미리보기까지 반영됐다고 할 수 없었다.
- candidate4: 같은 SQL 본문 **9 Run·4색**. 이식된 `MarkdownnCodeHighlighting`을 미리보기에 사용하며 C#·테마·범위 제한은 최신 Markdown 82개 회귀에도 포함돼 통과했다.
- 수정 근거: [고정 C4 AddCode](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate4/source/src/VS2017.AIAssistant/Views/MarkdownRendering.cs:181). 80ms 누적 강조 예산, 긴 줄 대체, 노드 제한을 유지하는 것도 코드로 확인했다.

### R3-03 / P2 — 원본 작성·미리보기의 전체 이식 차이

**candidate4에서 미해소.** 누락된 작성 기능과 미리보기 지원 범위의 차이를 함께 기록한다. 후자는 현재의 보안 설계에 따른 제한이라는 사유가 있어도, 원문 전체 이식 요구의 완료로 처리할 수는 없다.

| 제공 프로젝트 기능 | candidate4 대조 | 확인 방식 |
|---|---|---|
| AvalonEdit·행 번호·현재 행·편집 문법 강조 | 반영 | 소스/실제 제품 기능 검사 |
| 검색, 대소문자·단어·정규식, 이전/다음 | 반영 | 기능 검사 |
| 굵게·기울임·링크·체크리스트·목록 이어쓰기·들여쓰기·Undo/Redo | 반영 | 이식 helper/기능 검사 |
| 편집·미리보기·분할, splitter·확대/축소·단축키 | 반영 | 실제 제품 기능 검사 |
| 새 문서·파일 열기·UTF-8 내보내기·수정 중 전환 보호 | 반영 | 실제 제품 기능 검사 |
| 문서 폴더 선택·문서 목록 | **없음** | 원본 Pane74~84/123~136 대 C4 Editor 구성 |
| 현재 문서 Ctrl+S 즉시 저장 | **항상 내보내기 대화상자 호출** | 원본 Pane262~264/686 대 C4 Editor178/205/EditorKeyDown |
| 문서 이름 변경·삭제·파일 위치 열기 | **없음** | 원본 Pane135~136/577/694/702 대 C4 구성 |
| SQL/C# 미리보기 토큰 색상 | C3 누락 → **C4 수정 확인** | R3-02 |
| 명시 문서의 상대 파일·행 링크 | C3 오류 → **C4 수정 확인** | R3-01 |
| 명시 문서의 상대 PNG/GIF·정지 그림·수명 | 반영·제한된 로컬 자산 정책 | Markdown/Guide 검사, TASK024 실제 GIF 근거 |
| 참조형 링크 + 이미지 문서 | **참조형 링크가 문자열로 남음** | 아래 직접 비교 |
| HTML 포함 문서의 미리보기 | **태그가 평문으로 남음** | 아래 직접 비교 |

근거: [원본 문서 기능](C:/dydgyqud/Markdown_Memo_Interchange/RemoteControlDemo/Features/Markdown/MarkdownnPane.cs:686), [고정 C4 편집기](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate4/source/src/VS2017.AIAssistant/Views/MarkdownEditor.cs:178). C4 편집기 첫 주석도 파일 목록·HTML 실행 제외를 명시한다.

실제 C3/C4 Preview에 `[Documentation][ref]`와 `[ref]: https://example.invalid/docs`, 이미지 한 개를 넣으면 Hyperlink가 **0개**이고 참조 문법이 평문으로 남는다. 원본은 이미지/HTML이 있는 문서를 Pane724~727의 Markdig HTML 경로로 보낸다. 원본이 사용하는 Markdig 1.3.2 패키지의 설치된 DLL로 같은 입력을 변환하면 실제 href가 생성됐다. 이 비교는 **변환 실행과 원본 경로 읽기**이며 원본 앱의 브라우저 화면을 실행한 것은 아니다. `<table><tr><td>Cell</td></tr></table>`도 C4에서는 태그 그대로 표시됐다.

조치: 문서 관리·저장 기능을 보완하고, 원본이 지원하던 문서의 미리보기 차이를 안전한 방식으로 해소한다. HTML 스크립트 실행·원격 자산 자동 로딩·경로 방어 완화를 요구하는 지적은 아니다. 지원하지 않을 기능이 있다면 사용자 원문과의 범위를 명시적으로 정리하고 “전체 이식” 완료 표시를 보류해야 한다. 후속 후보는 별도 해시·실행 근거로 확인한다.

## 사용자 원문 15항목 대조

`반영 확인`은 고정 후보에서 확인한 구현·검사 범위의 판정이며 모든 IDE/실계정 검증을 뜻하지 않는다.

| 원문 | 판정 | 확인 결과·남은 범위 |
|---|---|---|
| 1 VS2019/2022 색 통일·링크 아이콘 | 반영 확인 | 세 SKU 테마 각31, 팔레트/링크 아이콘. 실제 IDE 통합은 별도 |
| 2 읽기 쉬운 스크롤 hover | 반영 확인 | Hover35 직접 PASS, TASK024 실제 hover28 근거. 이미지28→29와 대조 |
| 3 답변 Markdown·링크 | 주요 경로 반영·범위 제한 | 실제 호스트 파일/행·anchor 검사, R3-01 수정. 참조형 문법은 R3-03 |
| 4 제공 작성기·미리보기 전체·우클릭 편집 | **부분** | 이식 작성기/우클릭 연결 확인. 전체 기능은 R3-03 미해소 |
| 5 hover 화면 우선 스크롤 | 반영 확인 | 가장 가까운 viewer 소비/끝에서 배경 누수 방지. 실제 hover 근거 확인 |
| 6 좁은 창의 대화 목록 배경 차폐 | 반영 확인 | 폭 조건·overlay·배경 hit-test 차단 기능 검사 |
| 7 README 다수 GIF·제품 안내 재사용 | 반영 확인 | 정식 GIF5+PNG5, README와 제품 공통 사용. Orig3 도식6은 별도 |
| 8 설정 요약·접이 설명·이미지/GIF | 반영 확인 | Guide31, 실제 접기/재열기 GIF 근거 |
| 9 안내 Markdown·이미지/GIF | 반영 확인·문법 제한 | 안내 renderer/제품 자산 검사. 범용 HTML/모든 Markdown 지원 판정 아님 |
| 10 우측 상단 고정 돌아가기 | 반영 확인 | 본문 밖 배치·기능 검사 및 실제 GIF 상세 back 근거 |
| 11 설치·PDF·로그인 버튼 색 구분 | 반영 확인 | 의미별 팔레트/버튼 검사. 실제 설치 실행은 하지 않음 |
| 12 굵은 돋보기·원 중앙 전송 아이콘 | 반영 확인 | Path/정렬/색 검사 및 Light/Dark 렌더 시각 대조 |
| 13 중복 모델 진입 버튼 제거 | 반영 확인 | 기능 검사와 화면의 단일 모델 선택 진입 |
| 14 별도 계정 로그인·직접 ID/암호 가능성 확인 | **대안 구현·실계정 미검증** | 별도 공식 CLI 인증/상태 확인 UI. 직접 ID/암호 입력은 구현되지 않음. 가능성 조사 근거를 이번 감사에서 독립 검증하지 않아 원문 그대로 완료 판정하지 않음 |
| 15 VS 우선 Auto·수동 Light/Dark | 구현·헤드리스 반영 확인 | 세 SKU 각각31. 기존 Markdown 즉시 전환·취소 복구 포함. 실제 IDE의 VS/Windows 테마 연동 최종 확인은 별도 |

## Orig3 문서 검토 제안 5항목 대조

| 별도 제안 | 확인 내용 |
|---|---|
| 1 GIF6·정지 그림·MD/PDF와 출처 구분 | 정식 제품 GIF5/PNG5와 별도 도식6종 링크·설명 구분. 도식 자체를 실제 사용자 조작 증거로 집계하지 않음 |
| 2 PDF·설치·로그인·상태 구분 | 제품 문구/계정 검사에서 구분. 창 열기·모델 정보만으로 로그인 완료라고 하지 않음 |
| 3 요약/펼침·번호 그림·고정 back·매체 정지 | Guide31 직접 검사와 실제 GIF24의 접힘/닫힘·대체 근거 확인 |
| 4 실제 링크·명시 문서 PNG/GIF 기준 | 이미지 기준은 기능 검사, 파일 링크는 R3-01 발견 후 C4 직접 수정 확인 |
| 5 테마 문구·기존 원문 보존 | Auto/Light/Dark 정책 대조. 과거 감사·설치 MD/BAT/PDF/고객ZIP·기존 그림26파일 SHA 일치 |

## 실행 근거와 판정 범위

| 실행 | 후보/실행자 | 결과 | 한계 |
|---|---|---|---|
| 기능5그룹 | C3 / 감사자 | 196/196 | 실제 DLL, 창/실제 계정 없음 |
| 테마 | C3 / 감사자 | 2017·2019·2022 각31/31 | 실제 IDE 아님 |
| NativeRuntime | C3 / 감사자 | 67/67 | 격리된 합성 CLI 자식 프로세스. 실제 서비스 계정 아님 |
| 기능5그룹 | **C4 / 감사자** | **202/202**: Markdown82/Hover35/Guide31/Account24/RootUi30 | 실제 DLL, SourceOnly=false, 창 없음 |
| 테마 | **C4 / 감사자** | **세 SKU 각31/31** | WindowShown=false, ActualIdeTested=false |
| 발견 전후 독립 probe | C3→C4 / 감사자 | 잘못된 파일→정확한 파일, SQL1색→4색 | 기록용 탐색 서비스, 실제 IDE 파일은 열지 않음 |
| build-entry | 최신 검사 사본 / 감사자 | 16/16 | 별도 빌드 fixture; 전체16묶음과 다름 |
| 물리 hover | C4 / TASK024, 감사자는 자료 확인 | 28/28 | native24+합성2+구조1+수명1. 전부 물리28이라고 해석하지 않음 |
| 실제 GIF 창 수명 | C4 / TASK024, 감사자는 자료 확인 | 24/24 | 창 표시·애니메이션 전진·접힘/닫힘/Dispose 확인 |
| Debug/Release 빌드 | C4 / TASK025, 감사자는 결과 확인 | 6개 Exit0·Warnings0 | 감사자가 제품 빌드한 결과가 아님 |

후보 간 반복 검사와 테마·기능의 겹치는 조건을 합쳐 고유 검사 총계로 제시하지 않는다. 독립 probe의 성공 수는 **발견 사실·수정 확인의 assertion 수**이며 제품 기능 합격 수에 더하지 않았다.

C3 VS2022 테마 첫 실행은 IDE 제공 `Microsoft.Internal.VisualStudio.Interop`를 못 찾는 하네스 로딩 실패였다. 같은 VS2022 설치의 Shell/Interop을 고르는 테스트 전용 resolver 수정 후 별도 폴더에서31/31을 확인했다. 이를 VSIX 배포 DLL 누락으로 판정하지 않았다. 앞선 로그와 resolver 전후 사본을 보존했다. TASK024의 C4 첫147PASS/2FAIL은 PS5.1의 BOM 제거에 따른 두 검사 스크립트 파싱 실패로 기록돼 있으며, 복원 후 새 실행202/202과 구분된다.

TASK024 실제 GUI 원문은 [hover 결과](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task024/change-hover-candidate4-unlocked-first/result.json), [GIF 결과](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task024/playback-candidate4-unlocked-first/result.json)에 있다. 과거 Screen-saver 차단은 당시 제품 검사가 실행되지 않은 전제 실패이며 현재 잠금 해제 후 성공과 별도로 보존됐다.

## 패키지·보안 경계·과거 감사

세 C4 VSIX 각각에서 **핵심23파일**의 ZIP 내부 SHA와 Release 출력 SHA가 전부 일치했다. 제품 DLL, AvalonEdit, WebView2 Core/Wpf/WinForms 및 네이티브 loader, worker, GIF5·PNG5, 라이선스/고지를 포함한다. 이 검사는 패키지 모든 항목을 전수 비교했다는 뜻은 아니다. [패키지 상세](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-round3/package-integrity-c4.json)에 전체 선택 목록과 각 SHA를 남겼다.

| C4 SKU | 제품 DLL SHA256 | VSIX SHA256 |
|---|---|---|
| 2017 | `1B544BA40419D20C2BD58D2FFED144E97CA9FCF00F604A590DD87C301B7398BD` | `786145EF5F01B6A527BFCF119D48631730783D031E2389E80EC9ACA6F4AABAF3` |
| 2019 | `004BF8A11755C797053D83145A4CEE2C89187C79F40AE087A8BA6894FB13EA75` | `1DC6CEE1510526EC498C408E3B790B0529B09B9781569AF8722EA77191395C4F` |
| 2022 | `CF52603BD1EA5B6F751EDDF30194F0B3870BDC7A926F1FC0B6AA61DE8B76B974` | `33796E29943612E929A9A78369542D407E8F211E4ABA4DB825CD50FE7D0ECBBF` |

AvalonEdit MIT LICENSE의 SHA는 `7CFE74F11EAEF7A239E7C63D33595F924E7922FB3E3F3651014389620B228A07`이며 제공 프로젝트의 라이선스와 동일하다. 배포 경로는 `Resources/ThirdParty/AvalonEdit/LICENSE.txt`이다. 현재 포함하지 않는 Markdig를 새 제품의 배포 의존성으로 잘못 집계하지 않았다.

Markdown 경계는 128Ki 문자·6000노드·깊이8·8,388,608 scan-work·이미지24개다. 이미지의 파일 크기/픽셀 제한과 로컬 경로·재분석 지점 검사, 손상 GIF→정상 PNG 대체, 숨김/Dispose 시 timer 해제를 코드와 기능 검사로 확인했다. 일반 AI 응답의 임의 이미지 자동 로딩과 명시 사용자 문서의 제한된 자산 허용을 구분한다. 이번 검사에서 새 임의 코드 실행·계정 인증 우회를 확인하지 않았다. 이식 차이를 해소할 때도 이 경계를 유지해야 한다.

2차 R2-01(Claude 모델 메타데이터만으로 인증 표시), R2-02(PATH 밖 CLI 발견)는 C3 실제 제품 Native67에서 해당 회귀를 직접 통과했다. 세 SKU의 실제 서비스 로그인 성공을 뜻하지 않는다. R2-03(출력 줄바꿈 의존)은 검사에 공백 정규화가 반영됐고 개인 사본 build-entry16/16도 통과했다. R2-04와 1차 F-05의 최종 통합·실계정/운영 연동 제한은 계속 유지한다. VS 라이선스 배치의 성공을 계정 활성화 보증으로 해석하지 않는다.

## 후속 완료 조건

1. R3-03의 작성 기능과 미리보기 차이를 보완하고 실제 문서 fixture로 확인한다. 원문 요구를 축소해 완료 표시하지 않는다.
2. 후속 후보의 Role 기준·소스·세 DLL/VSIX를 다시 고정한다. 이번 C4에서 확인한 두 수정과 기능/테마·필요한 GUI 회귀를 변경 영향에 맞게 연결한다.
3. 통합 담당자는 최종 후보 전체16묶음과 세 실제 IDE의 테마/팝업/설정 복구·파일 이동 근거를 남긴다. 최초 실패 및 테스트 보정은 보존한다.
4. 별도 로그인 UI와 직접 ID/암호 요구 사이의 대안·조사 근거, 실제 Claude/운영 DB·TFVC/운영 배포 미검증을 계속 공개한다. 창 열기·설치·인증·추론 성공을 구분한다.
5. 기존 원문15와 Orig3 제안을 보존한 채 Role.md 말미에 이번 감사 결과와 반영 지시를 추가한다. 모든 담당 작업과 감사 후속 확인 전에는 전체 반영·배포 완료로 보고하지 않는다.

감사 산출물 작성은 완료하며, **미해소 R3-03과 통합 검증은 TASK025 후속 작업**으로 인계한다. 감사자가 새 제품 수정이나 배포를 수행한 것은 아니다.
