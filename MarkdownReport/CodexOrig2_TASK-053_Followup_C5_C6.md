# CodexOrig2 / TASK-053 후속 감사 / C5·최종 C6

2026-10-20 KST. **R53-02 제목 표시줄 통일 보완을 확인해 해소로 전환한다. 이번 변경에서 추가 확정 지적은 없다.** 최종 C6의 네 대상 실제 DLL에서도 독립 caption 검사를 각19/19 통과했다. R53-01 URL 렌더 경계 수정은 유지됐다.

[기존 C4 감사 보고서](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-053_Audit.md)는 당시 판정 그대로 보존한다. 이 후속 보고서가 **R53-02의 최신 상태**를 갱신한다. [후속 Evidence JSON](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-053_Followup_C5_C6_Evidence.json)에 입력 차이·실행 원문·SHA를 기록했다. 전체 Role 충족, 전체 회귀 완료, 최신 Release 제작·배포까지 승인한 보고서는 아니다.

## 보완 내용과 판정

C4→C5의 고정 입력 변화는 `AssistantDialogs.cs` 한 개다. C5는 기본 OS 제목 표시줄을 `WindowStyle.None`과 `WindowChrome`으로 대체했다. 44px caption·4면6px resize 영역, 테마 표면/글자 자원, Window.Title 연동 제목과 접근성 이름, 최소화/최대화·복원/닫기 버튼 및 hit-test 연결을 갖췄다. Alt+F4와 기존 IsCancel/Escape 경로를 유지하고 최대화 상태에 따라 복원 명령·이름을 바꾼다.

감사자는 세 프리셋의 caption/버튼 자원, 실행 중 자원 교체, 긴 제목과300/560/1040px 폭에서의 버튼 배치, 창 명령과 키 바인딩, 관리 StateChanged 처리와 Close 명령 도달을 직접 확인했다. 무창 WPF 렌더에서 VS/Kakao/Dracula 제목 표시줄을 시각 검수했다. **본문 header만 바꾸던 C4와 달리, 사용자 image45가 표시한 제목 영역을 테마 처리하므로 R53-02는 해소다.**

WindowChrome의 caption/resize 영역과 `IsHitTestVisibleInChrome` 사용은 [Microsoft 공식 설명](https://learn.microsoft.com/en-us/dotnet/api/system.windows.shell.windowchrome?view=windowsdesktop-10.0)과 대조했다. 실제 OS에서의 드래그·더블클릭·우클릭 시스템 메뉴·모니터 간 최대화·물리 resize를 감사자가 수행했다는 뜻은 아니다.

## 실제 검사 결과

각 대상은 VS2017, VS2017 Legacy, VS2019, VS2022다.

| 검사 | 결과 | 실행 범위 |
|---|---:|---|
| 독립 제목 표시줄 경계 | C5 각19/19, 최종 C6 각19/19 | 감사자 직접. Window.Show/HWND/물리입력 없음 |
| 기존 UI 페이지·toolbar·checkbox·탭 | C5 각14/14 | 감사자 직접. 실제 DLL, 무창 |
| R53-01 확장 multiline 경계 | C5 각18/18 | 감사자 직접. 원래10사례 및 줄바꿈·빈문단·미종료 markup·종료 뒤 정상 URL |
| Designer caption·Owner 검사 | C5 각13/13 | 타 담당 원문·DLL SHA 연결 확인. offscreen fixture owner를 Show/Hide하며 실제 dialog는 표시하지 않음 |
| C5·C6 Release 빌드 | 각4대상 오류0·경고0 | Root builds.json/로그 확인. 감사자 제품/공유 빌드0 |
| 최종 입력·패키지 연결 | 입력124/124, 네 VSIX DLL/PDF SHA 일치 | 감사자 직접 |

최대화/복원 상태 검사는 `WindowState`와 관리 StateChanged 처리로 명령 교체를 확인한 것이다. 실제 네이티브 최대화 결과로 계상하지 않는다. 감사자 검사는 standalone source의 taskbar 정책을 확인했고 Owner 관계는 Designer의 별도 검사 결과와 변경되지 않은 소스 경로를 근거로 구분한다.

Root가 전한 C4 실제 IDE 2017/2019 각145/145 및 OS balloon 이벤트 기록은 C4의 결과다. 이를 새 WindowChrome의 C5/C6 실제 IDE 검사로 바꾸어 표현하지 않는다. Root의 최종 C6 전체16suite·실제 IDE3종 결과는 별도 통합 완료 근거로 관리한다.

## 최종 C6 연결

C5→C6의 입력 차이는 **23쪽 미리보기 PDF 한 개뿐**이며 컴파일 소스·csproj·리소스 정의 변경/입력 삭제는 없다. C5와 C6 개인 사본의 고정 입력은 각각124/124 SHA 일치다. 재빌드된 DLL SHA가 달라 최종 C6에서도 caption19 검사를 네 대상에 직접 다시 실행했다.

최종 PDF SHA256은 **A17D4A74CD195CBAAEB63971C13A488FDDD483F98289ADCE2493CB91819A8693**이다. C6 snapshot의 PDF와 네 실제 VSIX의 PDF 항목이 모두 이 SHA와 일치한다. 네 VSIX 안의 제품 DLL도 해당 C6 Release DLL과 일치하고 공유 고정 후보/개인 사본의 DLL SHA도 같다. 전달 중 제시된8C02 중간 문서 SHA와의 최초 기대 불일치는 별도 보존했으며, 최종 A17D 입력으로 연결했다. PDF 내용·23쪽 렌더 검수는 문서 담당 결과이며 이번 후속 감사가 재수행하지 않았다.

| 대상 | 최종 C6 DLL SHA256 |
|---|---|
| VS2017 | DE66DCA24F00E85DC7F81F200722298E165CFF08228445D86725A65031A0B247 |
| VS2017 Legacy | E316CAF9E4D2A7FD7B1E4C826BE83A910702C74517CE400683CB5C5032BC6E09 |
| VS2019 | 3A8E611D26ED51BF60D83AE4B99A3303A67D9B4D54ACB5265EFA6B77B699BA32 |
| VS2022 | 7AC1A14DC326909DDE54A18E3AA0493475C33F64B1FB93EAF433A31AFE3A6D17 |

C4 보고/JSON을 추가한 보호 원본 **78/78 SHA 불변**. 제품·Role·기존 보고·기존 릴리스·실계정·운영DB/SCM·고객PC·원격 배포 변경0, 추가 GUI 점유0이다. 결과 원문과 프리셋 렌더 PNG는 [후속 검사 폴더](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task053-c5/results)에 있다.

## 실패 보존과 검증 한계

최초 독립 fixture는 표시하지 않은 Window를 Owner로 지정해 WPF 계약 예외로 중단됐다. 비표시 검사에서 그 합성 Owner를 제거하고 Owner 검증은 별도 수행 자료로 구분했다. 다음 실행의 색3실패는 아직 CommandManager 재질의가 이뤄지지 않은 버튼의 Disabled 색을 정상색과 비교한 것이다. Dispatcher에서 정상 재질의를 처리한 뒤 제품 색을 덮어쓰거나 검사를 제거하지 않고19/19 통과했다. 초기 원문·fixture와 최종 결과는 다른 경로에 보존했다.

기존 보고의 #7은 **최대100개/4MB 전체 보관 모델의 화면20개 페이징**, #11은 **기본 테마 자동 상속/독립 패키지 구성 검증**이라는 범위를 유지한다. 무제한 저장소 지연조회, 네 기본 IDE 테마의 모든 토큰, 고객 네트워크 제한 PC 설치·실계정 왕복 검증까지 충족했다고 확장하지 않는다. 독립 테마 실설치 probe와 전체 최종 회귀·Release 제작·배포·원격 해시 확인은 Root051 소유다.
