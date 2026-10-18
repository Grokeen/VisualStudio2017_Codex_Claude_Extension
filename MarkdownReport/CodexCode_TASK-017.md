# TASK-017 — 첨부 이미지 분석과 드롭다운·안내·AI 지정 개선

담당 CodexCode. 시작 2026-10-18 16:56:53 KST. 사용자 요청에 따라 최신 Role.md와 image-7~16을 직접 열어 기존 화면·동작과 비교했다. 기존 image.png 및 image-1~6의 요구는 TASK-018과 함께 회귀 범위로 유지한다. 제품은 v0.8.0으로 준비하고, 다른 작업자의 편집 종료 전에는 배포하지 않는다.

TASK-017은 제품 UI·A~Z 이름 서비스·버전/csproj·Debug/Release 빌드·신규 명명/안내 검사를 담당한다. TASK-018 CodexOrig는 기존 review/ide/launcher/ux 검사, README/사용 안내·화면·실제 IDE·릴리스/Git을 담당한다. 제품과 공유 bin/obj는 TASK-017만 빌드했다.

## 이미지별 분석과 구현

| 사용자 이미지 | 기존 구현에서 확인한 차이 | 반영한 동작 |
| --- | --- | --- |
| [7 Fast](image-7.png) | 입력창의 번개가 단독 토글이었다. | 번개에서도 입력 버튼에 붙는 드롭다운을 연다. 지원 모델의 Fast·사용량 안내를 강도와 함께 검토해 적용한다. |
| [8 강도/모델](image-8.png), [10 게이지](image-10.png) | 강도 Slider가 별도 모달 창에 있었다. | 버튼 위 비모달 Popup에 현재 강도·단계별 색상 게이지·모델·Fast를 함께 표시한다. 실제 광고된 강도만 선택하며 키보드 화살표도 지원한다. |
| [9 권한](image-9.png) | 권한 버튼은 프로젝트 작업 모달만 열었다. | 승인 요청/내 대신 승인/전체 권한을 드롭다운에서 선택하고 상세 DB·SCM·배포 작업은 별도 항목으로 연다. |
| [11 모델 목록](image-11.png) | 모델 ComboBox와 모달 상세 설정이 나뉘었다. | 기존 직접 입력 모델 ComboBox를 유지하고 같은 드롭다운에서도 모델 목록과 강도를 고른다. 미확인 모델의 고급 강도·Fast는 차단한다. |
| [12 안내 카드](image-12.png) | 정적인 설명 카드만 있었다. | hover 또는 키보드 초점 카드의 내부 표시만 살짝 전진시키고 다른 카드를 흐리게 한다. 클릭·Enter·Space로 같은 창의 단계별 그림/상세 설명을 열고 목록으로 돌아가면 원래 카드에 초점을 복원한다. |
| [13 대화 위치/이름](image-13.png) | 새 대화·불러오기·이름 버튼이 상단에 있고 이름은 자유 문자열/GUID였다. | 대화 목록 하단에 가로 스크롤 툴바를 고정한다. 고정 문구 ‘AI 지정하여 대화’의 목록에서 A~Z AI를 선택하면 실제 해당 대화로 이동한다. 역할·팀·협업 설정은 별도 항목으로 유지한다. |
| [14 메뉴 오류](image-14.png) | 메뉴 버튼의 이동 시작점이 원점 버튼을 덮고 MouseEnter마다 애니메이션이 재시작됐다. | 실제 부채꼴 배경과 서로 다른 방향의 고정 버튼을 배치한다. 원점 상단40픽셀은 메뉴 hit 영역에서 제외하고 내부 글자만 애니메이션한다. 열린 상태 재진입은 다시 펼치지 않으며175ms 닫기 유예로 메뉴까지 이동할 수 있다. |
| [15 모델 화면](image-15.png) | 구역 목차와 하단 버튼의 시각 구분이 없었다. | 드롭다운에도 모델/강도/속도의 상단 고정 목차와 색을 구분한 고정 적용 영역을 제공한다. 본문만 스크롤한다. |
| [16 설정 화면](image-16.png) | 긴 instruction 아래로 이동하기 어렵고 구역·footer 구분이 약했다. | 계정/모델/규칙/화면의 고정 목차, 실제 구역 위치로 스크롤, 제목 아이콘·24픽셀 간격과 별도 톤의 하단 동작 영역을 적용한다. |
| 작업 중 추가된 Role9 툴팁 | 기본 Windows 툴팁의 색과 긴 문장 처리가 제품 UI와 달랐다. | 공통 ToolTip 스타일에 검정 배경·밝은 글자·테두리·여백·줄바꿈·최대 크기와 열림 효과를 적용한다. |

부채꼴은 클릭과 키보드로도 열고 Escape·외부 포인터/초점 이동·소유 창 비활성화·해제에서 닫는다. 카드의 외부 hit 영역과 버튼 자체 위치는 움직이지 않으므로 강조 효과가 반복 enter/leave를 만들지 않는다. 설명 카드 선택만으로 설치·로그인이나 외부 명령을 실행하지 않는다.

## 이름·대화 보존과 한도

최신 예시를 따라 `사용자명+클로드/코덱스+A~Z`로 만든다. 기존 대화 모델이 Claude/Codex별이므로 각 제공자에 영구26슬롯을 부여하고 세부 모델 변경으로 AI 주소를 바꾸지 않는다. SCM 로그인 확인 또는 명확한 GCM 저장 계정이 있으면 그 계정명을 사용하고, 없으면 Windows 사용자명이라는 출처를 표시한다. GCM 저장 계정은 서버 인증 완료와 구분한다. ‘계정 이름 다시 확인’으로 로그인 후 이름을 갱신할 수 있으며 소스 선택 전 최초 조회 실패도 다시 시도한다.

닫은 AI도 문자와 대화 ID를 유지한다. A~Z가 모두 있으면 새 Markdown을 만들지 않고 기존 A를 다시 연다. 최초 실행·새 대화·마지막 대화 닫기도 같은 라우팅을 사용한다. 마지막 A를 닫을 때는 기존 B를 대체 대상으로 열어 A의 닫힘을 보존한다. 새 기록 생성 후 AI 등록 실패는 이번 기록만 닫아 보관하고 원래 실패를 알린다.

기존 이름은 LegacyName에 보관하고 대화 ID·Markdown 본문·당시 협업 발신/수신 이름과 이력을 변경하지 않는다. 과거26개 초과 기록은 읽기 보관하며 AI 지정/협업 참여자 목록에서 제외한다. 자동 이름은 읽기 전용이고 역할·팀·참여자는 계속 편집한다. 계정명 갱신도 영구 문자·주소를 유지한다.

## 변경 경로

- UI: Views/AssistantControl.Dropdowns.cs·Fan.cs·Options.cs·Experience.cs·Dialogs.cs·Chat.cs·xaml.cs·xaml, AssistantDialogs.cs·AssistantTheme.xaml.
- 서비스: Chat/ConversationCollaboration.cs. 기존 CLI 옵션·문서 도우미·소스 승인/저장 경계는 유지한다.
- 빌드: VS2017.AIAssistant.csproj·Properties/AssemblyInfo.cs·source.extension.vsixmanifest.
- 신규 검사: tests/collaboration/AlphabetAgentRoutingTests.cs·Test-AlphabetAgentRouting.ps1, tests/task017/DialogGuideChecks.cs·Test-DialogGuide.ps1. 최상위 실행기 연결은 TASK-018 소유다.

새 클래스/메서드와 수정한 메서드에는 Role의 한국어 name/desc/author/create date/update date 주석 형식을 적용했다.

## 최종 제품 검증

최종 고정 DLL SHA-256 `DC8E3B8CAFF3E935100E8B3F287E8716263F2996C0C9C202C4AE36C58440B36A`를 사용한다. 중간 후보의 반복 실행은 합산하지 않는다.

| 검사 | 결과/근거 |
| --- | --- |
| VS2017 .NET4.6.2/C#7 Debug·Release | 둘 다 종료0·경고0·오류0, [JSON](CodexCode_TASK-017_Builds.json) |
| 명명·A~Z·마이그레이션·동시 생성·실패 복구 | 최종 DLL40/40 PASS, [JSON](CodexCode_TASK-017_Alphabet.json) |
| 기존 협업 상태·원자 저장·역할·보고서 | 최종 DLL60/60 PASS, [JSON](CodexCode_TASK-017_Collaboration.json) |
| 안내 hover/focus·실제 그림·상세/복귀·목차·footer·320px | 최종 DLL23/23 PASS, [JSON](CodexCode_TASK-017_Guide.json) |
| 부채꼴 상태의 독립 WPF 소스 검사 | 14/14 PASS, [JSON](CodexCode_TASK-017_FanIndependent.json); 최종 제품의 실제 메뉴 검사는 TASK-018 UI/IDE 근거와 구분 |

새 이름의30개 동시 생성에서는26개 파일만 만들고 나머지4개는 A를 반환했다. 이전28개 기록 원문과 구 JSON 마이그레이션·닫힌 A 재열기를 확인했다. 보관된 옛 이름이 새 A 이름과 같아도 A의 역할 저장을 막지 않도록 수정하고 회귀 검사에 포함했다. 목차는 제목/입력이 함께 보이도록 실제 구역 Y 위치로 이동한다.

안내 단독23개 통과 후 전체 UI 검사에서 목록 복귀 초점이 누락되는 회귀를 발견했다. 일회 Loaded만으로는 이전 화면의 지연된 IsLoaded 상태를 구분하지 못했다. 본문 교체 후 UI Input 순서에서 실제 연결·가시성을 확인하고 초점을 복원하도록 수정했다. 이 예약은 UI 스레드 전환이나 동기 대기가 아니며, UI 접근 확인과 해당 호출 한 곳의 근거를 적은 VSTHRD001 억제만 사용한다. 보정한 handoff2의 전체 UI131개가 통과했다. 이후 사용자 Role이17:20:18에 툴팁 요구를 추가한 사실을 원문 해시 검사로 발견해 ToolTip 스타일을 반영한 handoff3을 최종 검증 대상으로 삼았다.

최종 VSIX306,771바이트·SHA-256 `AE33A8D0AFA3223C46037C56E00DC848D017BA19CACD85072D08E85C11B4DFEB`의 내장 DLL·문서 도우미 exe/config는 handoff3과 같은 해시다. 제품/도우미 소스39개와 최신 Role 원문의 확인 결과는 [패키지 근거](CodexCode_TASK-017_Package.json)에 기록한다. 초기 원문과17:20 추가 원문을 별도 사본으로 보존했다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/collaboration/Test-AlphabetAgentRouting.ps1 -AssemblyPath <고정 DLL>
powershell -NoProfile -ExecutionPolicy Bypass -File tests/collaboration/Test-ConversationCollaboration.ps1 -AssemblyPath <고정 DLL>
powershell -NoProfile -STA -ExecutionPolicy Bypass -File tests/task017/Test-DialogGuide.ps1 -AssemblyPath <고정 DLL>
```

전체 회귀·루트 실행기14묶음·실제 VS2017와 사용자용 최종 화면, GitHub/원격 해시 검증은 [TASK-018](CodexOrig_TASK-018.md)에서 기록한다. 사용자 Role/첨부 이미지와 이전 v0.2~v0.7 배포본을 보존한다. 현재 확장 패키지는 VS2017 15.9+용이며 VS2019/2022 패키지, 실제 Claude/Fast 과금 호출·외부 운영 DB/SCM/배포 검증은 별도 범위다.

## 완료와 배포 인계

최종 담당 작업 완료 2026-10-18 17:30:19 KST, 총 33분 26초 (시작16:56:53). 17:18의1차 확인 후 통합 UI에서 발견한 초점 복귀 회귀와17:20 추가된 툴팁 요구까지 보정했다. 최종 handoff3의 명명40·기존 협업60·안내23을 모두 통과했으며 실행 사본의 DLL 해시를 직접 확인했다. [구성 요소 출처](CodexCode_TASK-017_ComponentProvenance.json). 별도 부채꼴 소스 상태14개는 제품 UI 검사와 구분한다.

최종 제품 UI133개와 루트14/14 묶음 통과를 직접 읽어 확인했다. final3의 안내 hover·320px 상세·목차 화면3개도 직접 열었다. [종합 근거](CodexCode_TASK-017_Validation.json). 모든 하위 작업자와 CodexCode의 파일 편집·개인 테스트를 종료한다. TASK-018이 같은 VSIX의 실제 IDE·최종 화면·GitHub 배포와 원격 해시 확인을 완료한 후 최종 기록한다. 제품/고정 패키지는 변경하지 않는다.
