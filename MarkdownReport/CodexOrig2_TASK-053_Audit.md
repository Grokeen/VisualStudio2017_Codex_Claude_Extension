# CodexOrig2 / TASK-053 / 2차 감사 후속 보고

감사일: 2026-10-20 KST. 대상: TASK051 **candidate4 v0.12.0** 및 **themes-candidate4**. **판정: 일부 반영, 전체 충족으로 승인할 수 없음.** 새로 발견한 URL 렌더 경계 오류 R53-01은 C4에서 수정 확인했다. **R53-02(P2), 팝업의 비클라이언트 제목 표시줄 통일은 남아 있다.** 감사 종료와 전체 빌드·최신 Release 제작·배포 완료를 구분한다.

[Role.md](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/Role.md)의 **2026-10-19 23:09 추가 요구 11개**, image44–55 및 Code052 독립 검토를 대조했다. 종전 감사는 덮어쓰지 않았으며, 앞선 Role 전체 기능/고객 오프라인 설치/실계정 검증을 이번 UI 변경만으로 종결하지 않는다. 상세 기계 근거는 [Evidence JSON](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-053_Evidence.json)에 있다.

## 미해결 지적

### R53-02 / P2 / 이미지에 표시된 팝업 제목 표시줄은 변경되지 않음

사용자 [image45](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/image-45.png)의 빨간 영역은 녹색 OS/VS **비클라이언트 제목 표시줄**이다. Code052도 같은 영역으로 판독했다. C4의 공통 `AssistantDialog`는 기본 `Window`를 사용하며, 새 `headerBand`는 `Content` 내부 첫 행에만 적용된다. 제목 표시줄을 테마에 연결하는 WindowChrome/DWM 등의 처리는 없다. C3 네 대상 실제 DLL에서 `WindowStyle=SingleBorderWindow`, `WindowChrome=null`을 직접 확인했고, 관련 C4 구현은 동일하다.

따라서 본문 header/footer·버튼 통일 검사가 통과해도 표시된 제목 표시줄의 통일을 입증하지 못한다. 어두운/Kakao/Dracula 본문과 OS 강조색 제목 표시줄의 조합이 남는다. **지정한 제목 표시줄까지 공통 테마를 적용하고, 실제 IDE에서 밝음/어두움/사용자 팔레트의 제목·본문을 함께 확인해야 한다.** 제목 표시줄을 요구 범위에서 제외하기로 사용자와 합의한 근거는 발견하지 못했다.

근거: [C4 고정 AssistantDialogs.cs:130](C:/dydgyqud/AO53_20261020/candidate4/source/src/VS2017.AIAssistant/Views/AssistantDialogs.cs:130), [본문 header:156](C:/dydgyqud/AO53_20261020/candidate4/source/src/VS2017.AIAssistant/Views/AssistantDialogs.cs:156), [4대상 속성 관측](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task053/c3-state-summary.json). 실제 IDE 창의 시각 검사는 Root 담당이며 감사자는 비표시 속성과 소스 경계를 확인했다.

## 수정 확인한 지적

### R53-01 / P2 / 속성·주석 URL의 클릭 가능 링크 승격 — C4 해소

C2에서 발견된 기본 iframe 속성 회귀를 독립 재현했고 C3에서도 다음 3개는 각각 예상 Hyperlink 0개 대신 1개를 만들었다.

```text
prefix <iframe title="x > https://example.test/hidden"></iframe>
prefix <iframe title='x > https://example.test/hidden'></iframe>
prefix <!-- x > https://example.test/comment -->
```

첫 `>`만 태그 끝으로 보던 처리에서 따옴표 속성·주석이 잘렸다. 자동 접속이나 브라우저 실행은 발생하지 않았고 원문은 보존됐다. 따라서 실행 취약점으로 확대하지 않고, 표시가 비활성이어야 하는 markup의 링크 승격 회귀로 판정했다.

Root 보완은 따옴표 밖의 `>`와 주석 `-->`를 구분하고 다음 줄에도 상태를 유지한다. **동일 독립 10사례: C2 5통과/5실패 → C3 7통과/3실패 → C4 네 대상 각10/10 통과.** 줄바꿈·빈 문단·미종료 quote/comment·종료 뒤 정상 URL을 포함한 별도 18사례도 C4 2017에서 18/18 통과했다. C2/C3 실패 결과는 보존했다.

근거: [C3 실패 원문](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task053/candidate3-markup-boundary.json), [C4 4대상 결과](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task053/c4-link-summary.json), [추가18 결과](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task053/results/multiline-c4-2017.json), [독립 fixture](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task053/MarkupMultiline53.cs).

## Role 11항목 대조

| 번호 | 요구 | 판정과 확인 범위 |
|---|---|---|
| 1 | Windows 작업 완료 알림 | **구현 확인, 실환경 범위 제한.** 실제 completed 1회·설정 on·Loaded일 때만 NotifyIcon 알림. 실패/취소/미표시 제외 정책20조합 직접 통과. Root의 C4 합성 Windows 실행에서 BalloonTipShown 1회와 타이머·중복 방지·Dispose 정리 기록 확인. WinRT Reply/알림센터/클릭 복귀 계약이나 고객 PC에서의 표시까지 입증하지 않음. |
| 2 | 표시한 팝업 UI 통일 | **부분 반영 / R53-02 미해결.** 본문·footer·actions는 공통화됐으나 지정된 OS 제목 표시줄은 기본 방식 유지. |
| 3 | 브라우저처럼 상단 뒤로가기 | **반영 확인.** 안내 상세의 스크롤 본문 밖 왼쪽 위 `← 뒤로`. 종전 오른쪽 기대를 고친 Guide31/31은 Root 실행 근거로 구분. |
| 4 | Markdown 팝업 크기·체크박스 | **반영 확인.** 작업영역을 고려한 최대1040×780, 최소 크기 및 공통 CheckBox 스타일 연결. 무창 실제 UI에서 스타일/기본값 확인. 작은 화면 전체 시각 적합성은 별도. |
| 5 | Claude/Codex 탭 이동·간격 | **반영 확인.** 표준 TabControl·Contained 방향 탐색·서로 다른 접근성 이름, 탭 사이22px 및 키보드 포커스 표시. 모든 물리 키 입력을 감사자가 직접 실행한 것은 아님. |
| 6 | URL 기본 Bold·콜론 정렬 | **반영 확인 / 발견 회귀 수정 확인.** Markdown/reference/source/anchor/raw 링크 bold, URL1/URL1000의 선택 가능한 WPF 표3열, 200/320/600px·밝음/어두움 검사 통과. 긴 source label은 그룹으로 처리. R53-01 C4 해소. |
| 7 | 이전 대화 점진 조회 | **화면 페이징 반영, 저장소 조회 범위 제한.** 처음 최신20개, 상단 도달 때 이전20개, 읽기 위치 유지·전체 보관 모델 저장 통과. 전체 보관 모델은 먼저 메모리에 로드하며 기존 최대100개/4MB 제한 유지. 무제한 디스크 이력의 지연 조회로 구현된 것은 아님. |
| 8 | 설정 좌측 탭·페이지 전환 | **반영 확인.** 계정/모델/규칙/화면4개 좌측 페이지와 선택 본문 전환·종료 시 원본 컨트롤 복원 통과. 감사자 fixture는 합성 section을 사용했고 실제 모달 전체 클릭은 Root IDE 검사 범위. |
| 9 | 관련 버튼·상하 간격 구분 | **반영 확인.** 계정 카드·연결/확인 버튼 행, 카드 padding18·하단28·소제목/입력/보조버튼 간격 적용. 소스와 후보 UI 구조를 대조했으며 모든 DPI에서의 사용자 시각 평가는 미실시. |
| 10 | Markdown toolbar 구분 | **반영 확인.** 문서/편집/서식/삽입/체크목록/보기6개 표면 그룹, 명령 기호·기능별 묶음과 공통 색·간격. 실제 후보 무창 UI 검사 통과. |
| 11 | 기본 VS4테마·독립 Kakao/Dracula | **패키지 구현 확인, IDE 적용 검증 일부 남음.** 확장 auto는 VS host 배경/전경을 따르고 기존 auto/light/dark 및 vs/kakao/dracula 프리셋 유지. 네 기본 테마의 모든 토큰을 각각 대조한 증거는 없음. 독립 IDE용3SKU VSIX는 공통80category/2470color, 독립ID/GUID·등록·manifest·license 검사를 통과. 감사자의 실제 IDE 설치/색 선택/가독성 실측은 미실시. |

알림 API는 작업표시줄 balloon을 표시하며 표시 시간은 OS 설정 영향을 받는다. API 호출이나 정책 통과를 모든 사용자 화면에서 표시된다는 보장으로 사용하지 않는다. [Microsoft NotifyIcon 문서](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.showballoontip?view=windowsdesktop-10.0).

## 직접 실행과 타 담당 근거

| 검사 | 결과 | 대상·실행 주체 |
|---|---:|---|
| UI 구조·설정 페이지·checkbox·toolbar | 각14/14 | 감사자 직접, C2 실제4DLL, Window.Show 없음 |
| 기존 링크·표 geometry | 각14/14 | 감사자 직접, C2 및 최종C4 실제4DLL |
| 이력65→20/40/60/65·읽기 위치·전체66저장 | 각10/10 | 감사자 직접, C3 실제4DLL, 격리 standalone WPF 창. 예약 후 실행하고00:14:47 GUI 반환 |
| 이력 경계·알림 정책·설정 저장/복원·Dispose | 각42 assertions 통과 + caption 관측1개 | 감사자 직접, C3 실제4DLL, 무창. 원본 JSON의 Passed43에는 관측1개가 포함됨 |
| R53-01 독립 경계 | 각10/10 | 감사자 직접, C4 실제4DLL |
| multiline 확장 경계 | 18/18 | 감사자 직접, C4 2017 실제DLL |
| 독립 IDE 테마 패키지 | 31/31 | 감사자 직접, themes-C4 3VSIX 정적 내용·정의 대조. 설치/레지스트리 변경0 |
| C4 Release 빌드 | 4대상 오류0·경고0 | Root의 고정 builds.json/로그 확인. 감사자가 제품/공유 빌드하지 않음 |
| 합성 Windows 완료 알림 | 성공 기록·BalloonTipShown1회 | Root 실행 결과의 DLL SHA 연결 확인. 감사자 직접 표시/시각 확인 아님 |

C3→C4 제품의 동작 변경은 MarkdownRendering scanner다. Chat/Dialogs/ProgressPresentation은 주석 추가만 확인했고, 그 밖의 변경은23쪽 PDF 입력과4개 csproj 리소스 연결이다. 이력·UI·테마 소스의 동일성을 확인해 이전 후보의 검사 적용 범위를 제한적으로 연결했다. Root가 C4 전체 회귀와 실제 IDE fixture를 별도로 수행 중이며, 아직 없는 결과를 통과로 미리 계상하지 않았다. source-only 팔레트55/알림47은 예비 근거로만 취급하고 위 실제 DLL 검사 수에 합치지 않았다.

상태 검사는 이력0/19/20/21/100, 100상한에서 추가/최고령 제거, 대화 전환 후 이전 구독 차단과 예약된 DispatcherOperation Abort를 포함한다. 설정 preview가 파일을 쓰지 않음, 취소 복원 helper, store 왕복·알 수 없는 프리셋 거절 시 기존 파일 보존을 직접 확인했다. 실제 모달의 Cancel 버튼 경로 전체를 실행한 검사는 아니다.

## 고정 입력·패키지·보존

C4 고정 입력 **124/124 SHA 일치**, 네 대상 DLL **0.12.0.0**, 실제 VSIX 내부 DLL과 Release DLL의 SHA 일치, 공유 고정 후보와 개인 사본 일치. 네 VSIX에 새 미리보기 PDF 리소스 포함을 확인했다. PDF23쪽의 렌더 검수는 Orig3 수행 근거이며 감사자가 재수행하지 않았다. 전체 SHA는 Evidence JSON에 보존했다.

| 대상 | C4 DLL SHA256 |
|---|---|
| VS2017 | E265464E11DF9A9AE5619EDC0D6204A861CE7AB76D708859587E1C43F58CCCC4 |
| VS2017 Legacy | 2BC4C32007B1251CC582E5D11D0A2302C02BE25EFFA1E7B10BC2A63C26F9C82B |
| VS2019 | 4E036C938A9C46471DD5E0B919766F45BE86439F765D3A1F608ED891FFF9CEE4 |
| VS2022 | CDAD7EE2A30F7DF1E4C88B1CA022F9446F7091FEA1B9F3DAEE23289683558DCF |

기존 감사·Role·Code052·기존 릴리스 등 보호 원본 **76/76 SHA 불변**. 감사자는 새 TASK053 보고/JSON·개인 fixture·자기 공유 진행 항목만 작성했다. 제품 소스·기존 보고·기존 Release·실제 계정/CLI/운영DB·SCM·고객PC·원격 배포를 수정하지 않았다.

독립 테마는 XML→pkgdef 방식, 새GUID, 실제 포함 asset 및 공통 토큰 집합을 확인했다. CT_SYSCOLOR의 값은16진수 Windows 색 인덱스다. CT_INVALID/CT_AUTOMATIC/CT_COLORINDEX 등 baseline 상속 값이 있다는 사실을 가독성 보장이나 신규 권장 색 작성 방식으로 해석하지 않는다. 최초 검사기에서 이를 부당 거절하고 Source를10진수로 가정했던2실패는 하네스 오류로 보존한 뒤 공식 schema에 맞게 바로잡아31/31 통과했다. [Microsoft 색상 compiler/schema](https://learn.microsoft.com/en-us/visualstudio/extensibility/internals/vsix-color-compiler?view=visualstudio).

디자인 출처는 [Dracula 공식 색상 명세](https://draculatheme.com/spec), [사용자가 지정한 VS Code 프로젝트](https://github.com/dracula/visual-studio-code), [Kakao Developers 색상 가이드](https://developers.kakao.com/docs/en/kakaologin/design-guide)와 대조했다. VS Code 테마 파일을 VS IDE에 그대로 설치하는 방식이 아니며, Kakao의 공개 로그인 버튼 색은 참고 자료이지 공식 전체 IDE 테마 명세가 아니다. Microsoft baseline·Dracula MIT 고지와 자체 inspired 표기를 세 패키지에서 확인했다.

독립 상태 fixture의 최초 실행은 MarkdownEditor를 Window로 잘못 가정한 컴파일 오류로 중단됐다. 제품 검사가 실행되기 전의 하네스 오류이며 원본 로그/fixture를 남기고 수정 후 별도 출력 디렉터리에서 실행했다. 렌더러의 C2/C3 실제 실패와 이러한 검사기 오류를 구분했다.

## 인계 및 남은 검증

감사 산출물 작성·검사 종료. R53-02는 Root051/디자인 담당의 보완 및 새 후보 재검사 대상으로 인계한다. #7 저장소 조회 범위와 #11 실제 IDE 네 기본 테마/독립 테마 적용은 현재 구현·검증 범위를 명시해야 한다. 최신 전체 통합 빌드·Release 제작·GitHub 배포·원격 해시 확인은 Root051 소유이며 이번 감사가 대신 완료한 것이 아니다. 실제 AI 계정 왕복, 운영DB/SCM 및 고객 네트워크 제한 PC 설치 결과도 계속 미검증으로 유지한다.
