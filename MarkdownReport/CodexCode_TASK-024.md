# TASK-024 · 추가 이미지 요구 및 Markdown 작성·안내 개선

- 담당: **CodexCode**
- 시작: **2026-10-19 00:17:49 KST**
- 상태: **담당 C8 후속 검증 완료 — 기능231·세 SKU 독립23/테마31/번들·실제 hover28·GIF24 통과. 전체 통합·배포는 TASK025 소유**
- 기준: [Role.md](Role.md)의 2026-10-18 23:45 추가 1~15, image-28~38, 제공 Markdown 프로젝트
- 협업: TASK025 통합·패키징·감사·Git, TASK026 테마 전담, TASK027 별도 보완 설명서

최신 근거는 [3차 감사 후속 보고](CodexCode_TASK-024_Audit3_Followup.md)와 [검증 JSON](CodexCode_TASK-024_Audit3_Validation.json)이다. 아래 C3/C4 검사는 당시 기록으로 보존한다. C6에서 새로 확인한 삭제 확인 승계 SC024-02는 C7/C8 세 SKU의 독립23으로 수정 확인했다. 첫 C7 물리 클릭18PASS/1FAIL은 원인 미확정으로 보존하며 관찰을 추가한 C8 실제28/28과 구분한다.

## 구현 내용

| 요구 | 변경 | 검증 경계 |
|---|---|---|
| 1·15 색상 통일 | 공통 동적 팔레트와 popup의 동일 사전 공유. VS 우선 자동 및 밝게·어둡게 본체는 TASK026에 인계 | 세 실제 IDE와 Light/Dark 검증은 TASK025/026 |
| 2·5 hover와 휠 | 파일명·줄 수 고정, 읽을 수 있는 소스 diff·행 번호·변경색·최근 3건. 가장 가까운 스크롤을 우선하고 끝에서도 배경 누수 차단 | 소스 메모리 검사와 실제 물리 휠 검사를 구분 |
| 3 답변 Markdown | 제목·강조·목록·표·인용·코드·링크를 WPF로 표시. 로컬 파일 링크는 IDE 텍스트 뷰로 이동 | 실제 응답 템플릿·파일/행 탐색 및 위험 링크 거부 |
| 4 작성기 이식 | AvalonEdit 작성·미리보기·분할, 검색·서식·목록·체크·Undo/Redo·확대·UTF-8 파일. 입력/규칙 우클릭 및 독립 문서 작성 | 제공 프로젝트는 읽기만 사용. 원문·실패·취소 보존 |
| 6 좁은 대화 목록 | 배경 흐림·입력 차단·초점 제한·Escape·목록 닫기 후 복원 | 작은 폭의 overlay 상태·기존 비활성 상태 보존 |
| 7~9 설명과 GIF | 짧은 요약·접이 상세·Markdown 안내·공용 GIF5/PNG5. README와 제품에서 같은 자산 재사용 | 합성 예제를 넣은 실제 제품 UI와 실제 인증·설치를 구분 |
| 10 돌아가기 | 안내 상세의 우상단 고정 영역에 배치 | 본문만 스크롤해도 복귀 버튼 유지 |
| 11 버튼 색상 | 로그인 안내와 설치 설명 진입을 다른 의미 색으로 구분 | Light/Dark 팔레트에서 읽을 수 있는 대비 |
| 12 돋보기 | 기존 전송 이벤트의 원형 버튼 안에 굵은 벡터 돋보기 | 전송 의미와 Enter/Ctrl+Enter 유지, 실제 중심 위치 검사 |
| 13 중복 버튼 | Fast·토큰의 중복 모델 진입 단추를 숨기고 모델 버튼 하나로 통합 | 상세 속도 선택과 실제 사용량 tooltip 유지 |
| 14 계정 로그인 | 전용 계정 화면에서 제공자·실행 경로 고정, 공식 로그인 및 연결 확인·취소 | 화면 열기만으로 인증/조회하지 않음. 늦은 결과와 오류 비밀 차단 |

## 제공 프로젝트 대비

`C:\dydgyqud\Markdown_Memo_Interchange`의 작성기 구조와 편집/구문 helper를 참고했다. 원본 프로젝트를 수정하지 않았다. AvalonEdit **6.3.1.120**과 원본 MIT 라이선스를 배포에 포함하며, 원본 XSHD를 보존하고 문서별 색상 객체를 복제해 밝은·어두운 편집기가 서로 영향을 주지 않게 했다.

| 원본 작성 기능 | 새 작성기의 대응 |
|---|---|
| 서식 도구·Ctrl+B/I/K | 선택 영역 서식·링크와 한 번의 Undo |
| Ctrl+Shift+C·Ctrl+Enter | 여러 줄 체크 목록 생성·완료 전환 |
| Enter·Tab·Shift+Tab·Backspace·아래 방향키 | 목록/인용 이어쓰기·들여쓰기·빈 항목 종료·다음 줄 끝 이동 |
| Ctrl+F | 이전/다음·대소문자·단어/정규식 선택, 정규식 유한 시간 |
| 줄 번호·현재 행·구문 색·Alt+Z | 실제 AvalonEdit와 줄바꿈 표시 |
| Ctrl+Shift+V·splitter | 편집·미리보기·분할 보기 |
| Ctrl+휠 | 60~200% 확대·축소 |
| 행/열·Undo/Redo | 현재 위치와 실제 문서 undo stack |
| 새 문서·불러오기·UTF-8 저장·변경 보호 | 실패 시 원문/경로/자산 기준 보존, 명시적 버리기/취소 |
| 원본의 문서 폴더·목록·이름 변경·삭제·현재 문서 Ctrl+S | C4 누락을 감사 R3-03으로 확인한 뒤 TASK025가 보완. C8 기본231 및 독립23에서 저장·충돌·잠금·삭제 수명 확인 |
| 참조형 링크·HTML 표 미리보기 | 참조형 링크·정적 HTML을 native WPF로 보완. C8 한도 생략 안내도 확인하며 CSS·스크립트·브라우저 전체 동등성을 주장하지 않음 |

초기 이식에서 빠졌던 문서 목록·현재 저장·이름 변경·삭제와 참조/정적 HTML 미리보기는 감사 후속으로 TASK025가 보완했다. 원격 제어·Pico 등 작성기 밖의 기능은 포함하지 않는다. C8은 정적 HTML 구조를 WPF로 표시하며 스크립트·CSS·iframe은 실행하지 않는다. 일반 AI 답변의 이미지 주소를 자동으로 읽거나 내려받지 않고 명시적으로 연 문서와 동봉 안내의 제한된 로컬 자산만 허용한다. 모든 CommonMark/브라우저 HTML 문법의 동등성을 주장하지 않는다.

## 로그인 조사

[OpenAI 공식 인증 문서](https://learn.chatgpt.com/docs/auth)와 [Claude 공식 인증 문서](https://code.claude.com/docs/en/authentication)에서 CLI·공식 브라우저 인증을 확인했다. 조사한 공개 문서에서 확장이 아이디·비밀번호를 받아 직접 교환하는 공개 API는 확인하지 못했다. 따라서 공식 인증을 시작하고 상태를 확인하는 별도 화면을 제공한다. 이 판단은 조사 범위에 한정하며 비공개 API 존재 여부를 단정하지 않는다.

## 검증과 남은 작업

소스 전용 검사: TASK024 동결 시 Markdown **78/78**, hover·overlay **35/35**, helper **40/40**, 안내 **31/31**, 계정 **24/24**. 이후 TASK025 감사 보완의 Markdown source **82/82**도 별도 근거다. Markdown에는 계산량 제한 회귀 3개, 안내에는 손상 GIF의 PNG 재시도 회귀 1개가 포함된다. 소스 검사와 제품 DLL 검사를 합산하지 않는다. 개별 사전 실패와 수정 전 결과를 보존했다.

첫 실제 SDK15 bootstrap은 **오류1·경고35**로 실패했다. 계정 비동기 WPF 경계와 UI 콜백에 실제 Dispatcher 검증을 보완하고, 필요한 한정 메서드/예약 호출에만 근거를 남겼다. 다음 bootstrap2는 **오류0·경고0**이었고 고정 DLL의 초기 기능 회귀를 통과했다. 이후 보완한 Markdown 계산량·손상 GIF·앵커 링크를 포함한 196개는 TASK025의 독립 제품 사본에서도 통과했다. 이 사본은 최종 패키지의 합격 근거로 사용하지 않는다.

공용 안내 **GIF5·PNG5**를 완성하고 원본 프레임 20장을 모두 시각 검토했다. 각 GIF는 600×420, 4프레임, 프레임당 1.1초이며 가장 큰 파일은 252,810바이트다. README와 제품의 같은 안내 키에서 재사용한다. 실제 제품 WPF에 합성 내용을 넣어 렌더한 예시이고 실제 로그인·설치·모델 응답 결과가 아니다. TASK027의 별도 설명 그림 6종과 구분했다.

최종 제품 5그룹은 [Test-Task024.ps1](../tests/task024/Test-Task024.ps1), 물리 hover는 [Test-ChangeHover.ps1](../tests/task024/Test-ChangeHover.ps1)로 별도 실행한다. 화면 보호기·입력 데스크톱·다른 앱 가림은 제품 검사 전에 확인하며 강제로 전환하지 않는다.

2026-10-19 **01:20 및 01:40** 입력 데스크톱이 `Screen-saver`, GetCursorPos가 오류5로 확인됐다. 당시 물리 hover 시도는 제품 창 표시 전에 차단됐고 창 표시·포인터 이동·휠 실행은 모두 0이었다. 잠금 해제 질문은 한 번 전달했으며 설정·데스크톱을 강제로 바꾸지 않았다. 이 차단 기록은 제품 실패나 성공으로 분류하지 않고 보존한다. 사용자 해제 응답 후 **02:28 Default·커서 접근 정상**을 확인해 아래 실제 검사를 재개했다.

**이전 후보 candidate3의 VS2017 Release DLL**에서 2026-10-19 01:43:23~01:43:36 KST에 5/5그룹 **196/196·실패0·exit0**을 확인했다. Markdown78·hover35·안내31·계정24·실제 응답/설정 연결28이며 모두 SourceOnly=false다. 같은 DLL에서 01:43:53까지 TASK026 wrapper의 테마 **31/31·실패0·exit0**도 확인했다. 두 묶음은 각각 보고하고 고유 검사 수로 합산하지 않는다.

- DLL: `artifacts/task025/candidate3/2017/Release/VS2017.AIAssistant.dll`
- SHA-256: `26FEF3E2BCCA746985E87599F54029E8F8DC17877D9BFBBCA46A6F2FB01CE52A` (196개 검사 전후 동일)
- 기능 결과: `artifacts/task024/actual-candidate3-final/summary.json`
- 테마 결과: `artifacts/task024/theme-candidate3-final/result.json`

수정된 계산량 제한·손상 GIF의 PNG 재시도·앵커 위임과 기존 Markdown 코드/링크 색 전환 회귀를 이 후보에서 확인했다. 창 표시·물리 입력·실계정·유료 AI는 실행하지 않았다. 후보1의 VS2019 라이선스 연결 실패와 후보2의 SDK17 경고3개는 별도 로그로 보존했다. TASK025가 후보3에서 생성자 위임의 UI 검증 근거와 Dispatcher 게시 결과 관찰만 최소 보정했다. 이전 컴포넌트 동결 JSON은 역사 원본이고 최종 입력은 `artifacts/task025/candidate3/source-hashes.json` 및 소스 사본으로 추적한다.

**당시 후보 candidate4 / 2026-10-19 02:30 확인:** 감사에서 발견한 명시적 문서의 상대 소스 링크 문맥과 SQL·소스 미리보기 토큰 색상 수정이 포함된다. 같은 이름의 소스가 문서 폴더와 프로젝트 루트에 있는 충돌 회귀2개, 미리보기 색상·부하 회귀4개가 추가돼 당시 기능 검사 수는 **202**다.

| 검사 | 결과 | 근거 (`artifacts/task024/` 아래) |
|---|---|---|
| 실제 제품 5그룹 | **202/202**, Markdown82·hover35·안내31·계정24·응답 연결30 | `actual-candidate4-encoding-fixed/summary.json` |
| 테마 전환 | **31/31**, SourceOnly=false | `theme-candidate4-after-unlock/result.json` |
| 실제 hover·휠 | **28/28**, native 태그24·합성2·구조1·수명1 | `change-hover-candidate4-unlocked-first/result.json` |
| 실제 창 GIF 수명 | **24/24**, 생략0·프레임 이동 확인 | `playback-candidate4-unlocked-first/result.json` |

모두 같은 VS2017 Release DLL SHA **`1B544BA40419D20C2BD58D2FFED144E97CA9FCF00F604A590DD87C301B7398BD`**이며 고정 입력과 사본은 불변이다. 물리 검사에서는 자기 창 전경·native 대상 확인 뒤 실제 포인터와 양방향 휠, 경계의 배경 누수 차단, pin·Escape·Alt+Down·anchor unload를 확인했다. GIF5의 각 프레임4개 디코드·최소1.1초의 프레임 이동, PNG5의 단일 정지 프레임, 접기·재열기·페이지 이탈·back·창 종료·Dispose의 타이머 정지를 확인했다. OS 애니메이션은 원래 켜져 있었고 설정을 변경하지 않았다. 전체 4.4초 loop를 실시간 전부 관찰한 검사는 아니다.

`guide-playback.png`를 직접 시각 검토했다. 이 그림은 실제 안내 창에 검사 목적으로 GIF5를 동시에 배치한 시험 구성으로, 일반 갤러리의 최종 화면 캡처와 구분한다. 02:30:09 TASK025의 GUI 인계 후 실행했고 **02:30:52 모든 검사 창 종료·GUI 해제**를 기록했다. 실제 인증·설치·유료 AI·다른 앱 입력은 0이다.

C4 첫 검사147PASS/2FAIL은 GuideReadability.ps1·AccountLogin.ps1의 UTF-8 BOM 누락으로 Windows PowerShell이 한글을 잘못 읽은 파싱 중단이었다. 두 파일은 원본을 보존하고 BOM만 복원했으며 디코드한 코드 내용은 동일하다. 첫 요약·로그와 `test-encoding-resume-before/receipt.json`을 보존했다. 11개 PS의 AST 오류0 확인 후 새 결과 폴더에서 202/202를 재검사했다.

이후 C8에서 기능231, 세 SKU 독립23/테마31/번들 미디어10, 실제 hover28/GIF24를 확인했다. 정확한 새 해시·한계·최초 실패는 후속 보고에 연결한다. 3개 SKU의 빌드·실제 IDE·전체 실행기·독립 감사 최종 판정·GitHub 배포는 TASK025/028에서 통합하며 본 담당 완료를 프로젝트 배포 완료로 표시하지 않는다. 모든 작업자와 감사 담당이 끝나기 전에는 배포하지 않는다.

## 감사 대응

1·2차 감사 원본과 근거 JSON의 해시를 보존한다. R2-01(모델 정보와 계정 인증 구분), R2-02(PATH 밖 CLI 발견), R2-03(긴 경로 오류 출력 검사)의 기존 수정에 대한 새 3SKU·전체 회귀는 TASK025가 담당한다. 이번 계정 화면도 모델 조회·공식 로그인 시작·상태 확인·실제 모델 요청 성공을 각각 구분한다. R2-04에 따라 최신 소스·Role·DLL/VSIX와 실제 실행 근거를 연결하기 전에는 전체 반영 완료로 보고하지 않는다. 실제 Claude 인증·운영 SQL Server·외부 TFVC·운영 배포의 기존 미검증 범위는 유지한다. 약90% 시점에 기존 CodexOrig2 대화로 3차 감사를 요청했고 TASK028이 **01:51:45** 감사를 시작했다. C3의 두 결함은 C4, 작성기·HTML·UI·삭제 수명은 후속 C7/C8에서 확인했다. R3-03F4와 최초 C7 클릭의 미확정 한계를 포함한 감사 최종 판단은 TASK028의 새 후보 보고서와 구분한다.

## Orig3 문서 검토 제안 5개 대응

사용자 원문 15개와 별도인 2026-10-19 01:09 문서 제안이다. [개발자 적용 요청서](../docs/visual-guide-orig3/개발자_적용요청.md)의 D01~D09를 대조했다.

| 제안 | 적용과 검증 상태 |
|---|---|
| 1 설명 자료 재사용 | README에 보완 MD/PDF/갤러리 링크를 추가했다. 제품은 공용 5키의 실제 WPF 합성 예시를 유지하고 별도 도식 6종은 링크로 제공한다. 중복 11종 자동 로드는 추가하지 않았다. |
| 2 읽기·설치·인증 구분 | 의미별 이름·아이콘·설명과 색을 구분했다. 공식 로그인 창 열기·상태 확인·실제 요청 결과를 별도 안내하며 실계정 성공을 주장하지 않는다. 최종 계정24 및 기존 설치 회귀로 확인한다. |
| 3 긴 설명·재생 수명 | 요약·접이 상세·단계·상단 고정 돌아가기를 구현했다. 손상 GIF의 PNG 재시도도 보완했다. C4 안내31 및 실제 창 Playback24/24에서 확인했다. |
| 4 링크·로컬 그림 기준 | 명시적으로 연 문서·SaveAs의 폴더를 자산 기준으로 사용하고 실패·취소 시 보존한다. 일반 답변의 자동 로컬 그림 읽기는 없다. 문서 상대 소스 링크의 감사 보완까지 C4 Markdown82·응답 연결30에서 확인했다. |
| 5 테마·기존 문서 보존 | README의 자동/밝게/어둡게 표기를 TASK026 UI에 맞췄다. 기존 PDF/MD/BAT·고객 ZIP은 보존하고 새 보완 자료를 별도로 연결했다. 보호 문서 교체·최종 화면 통합 판단은 TASK025에 인계한다. |

근거: `artifacts/task024/requirements-latest.json`, `root-freeze3.json`, `markdown-freeze3.json`, `hover-markdown-freeze2.json`, `test-freeze2a.json`, `guide-account-freeze5.json`, `guide-media-freeze1.json`, `guide-media-encoded.json`, `desktop-preflight-0139.json`. `preserved-originals-final.json`에서 감사 원본·근거4개 및 기존 설치 MD/BAT/PDF/ZIP4개의 SHA, 기존 설명 PNG18개의 Git blob이 모두 유지됨을 01:43에 확인했다. 현재 Role SHA는 `0EF513021D252BE48441D7813D98A05CD615E493420B9F0A72C4D39E4C0B976C`이며 원문15·별도 문서 제안5를 구분한다.

## 작업 시간과 재현

초기 구현·고정 DLL 비표시 검사 단계는 **2026-10-19 00:17:49~01:43:53 KST**, 경과 **1시간 26분 04초**다. 사용자 해제 후 **02:27:33~02:30:38**에 C4 검증을 수행했고 경과2시간12분49초를 기록했다. 3차 감사 후속은 **02:41:26~03:14:26**,33분00초(대기 포함)이며 C8 담당 실행 검증까지 마쳤다. 최초 시작부터 C8 검증까지는 **2시간56분37초**다. 보고 마감 시각과 전체 프로젝트 통합/배포 종료는 별도다.

주요 소스는 `src/VS2017.AIAssistant/Views/Markdown*.cs`, `AssistantControl.Markdown.cs`, `AssistantControl.Viewport.cs`, `AssistantControl.HistoryOverlay.cs`, `ChangeDiffPopup.cs`, `DiffPreviewPresenter.cs`, `AssistantGuide*.cs`, `AssistantDialogs.cs`, `CliGuideDialog.cs`, `AssistantControl.AccountLogin.cs`, `AssistantControl.Runtime.cs`, `Services/VisualStudioDocumentService.cs`다. 정확한 파일별 해시는 위 컴포넌트 동결과 최종 후보 소스 목록에 있다. 새 안내는 `docs/assets/guide-media/`, 사용자 문서는 `README.md`, 검사는 `tests/task024/`에 있다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/task024/Test-Task024.ps1 -AssemblyPath artifacts/task025/candidate8/2017/Release/VS2017.AIAssistant.dll -OutputDirectory <새 결과 폴더>
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task026/Test-Task026.ps1 -AssemblyPath artifacts/task025/candidate8/2017/Release/VS2017.AIAssistant.dll -OutputDirectory <다른 새 결과 폴더>
```

실제 입력 데스크톱이 Default이고 커서 접근이 가능한 때 공유 GUI 예약 후 `Test-ChangeHover.ps1 -UseNativePointer` 및 `Test-GuidePlayback.ps1`을 각각 같은 DLL과 새 결과 폴더로 실행한다. CompileOnly는 동작 검사가 아니며, OS 애니메이션이 꺼져 있으면 재생 이동을 통과로 바꾸지 않고 생략으로 기록한다. 최상위 실행기 통합은 TASK025가 담당한다.
