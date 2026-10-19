# TASK-045 / CodexOrig2 / 부채꼴 메뉴 툴팁 독립 감사

2026-10-19 KST. 감사자 **CodexOrig2**. TASK-044 제안을 반영한 고정 candidate1을 감사했다. **이번 툴팁 변경 범위의 새 확정 결함은 0건**이다. 메뉴 열림 시 원점 툴팁 억제, 닫힘 시 도움말 복구와 기존 동작 보존을 확인했다. Role 22:07의 나머지 요청이나 전체 OS 키보드 검증 완료를 뜻하지 않는다.

## 대상과 변경 범위

개인 사본 `C:/dydgyqud/AO45_20261019/candidate1/`의 입력 **122/122**가 선언 SHA와 일치한다. C17 대비 제품 변경은 아래 **2파일**뿐이다.

- [AssistantControl.xaml](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/src/VS2017.AIAssistant/Views/AssistantControl.xaml): 원점의 문자열 ToolTip을 같은 문구의 명시적 ToolTip 객체로 바꿨다.
- [AssistantControl.Fan.cs](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/src/VS2017.AIAssistant/Views/AssistantControl.Fan.cs): Open에서 툴팁 서비스 억제·현재 툴팁 닫기, Close에서 서비스 복구를 추가했다.

추가한 4문장과 문서 주석을 제외한 Fan 코드가 줄 단위로 같고, ToolTip 표현을 정규화한 나머지 XAML 구조도 같다. 부채꼴 geometry·175ms·기존 포커스 처리·클릭 처리·이름·문구·배치가 변경되지 않았다. 파일·프로세스·네트워크·계정·보안 경계를 바꾸는 코드는 추가되지 않았다. 각 네 대상 VSIX의 제품 DLL은 고정 Release DLL SHA와 일치하고 필수 AvalonEdit/WebView2 runtime 4개도 포함됐다.

## 직접 검사와 통합 담당자 검사 구분

| 검사 | 결과 | 실행 주체·범위 |
|---|---:|---|
| 네 대상 독립 비표시 상태 검사 | **각 36/36** | 감사자 직접. 실제 DLL·해당 대상 SDK 의존성, Window/Popup 표시 0 |
| 최종 툴팁·관리 키보드 검사 | 각 **33/33** | TASK045 자료 확인. 실제 WPF 창/Popup·Keyboard.Focus·Tab traversal·관리 Escape 경로 |
| 기존 부채꼴 기하 | 각 21/21 | TASK045 자료 확인·고정 DLL SHA 연결 |
| 기존 중앙 정렬 | 각 73/73 | TASK045 자료 확인. 2022는 exact-sdk-final 결과 |
| 네 대상 Release 빌드 | 각 오류0·경고0 | TASK045 빌드 기록 확인. 감사자 공유/제품 빌드 0 |
| 보호 원본 | **76/76 SHA 보존** | Role·완료 감사·TASK044 문서·기존 릴리스·오프라인 테스트 완성 ZIP |

감사자의 36검사는 200/260 폭·light/dark, 열기/닫기·20회 반복 진입·timer 예약/정지·busy 거부·owner deactivation·unload·dispose·disposed 재열기 거부, 문구·Automation Name을 확인했다. 창을 띄우지 않았으므로 **실제 Popup이 화면을 덮는지 또는 물리 입력이 동작하는지의 직접 검사는 아니다.**

ToolTip 서비스의 표시 허용 상태와 ToolTip의 실제 열림 상태는 다른 속성이므로 두 가지 제어가 함께 필요하다는 구현을 [Microsoft ToolTipService 문서](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.tooltipservice.isenabled?view=windowsdesktop-9.0)와 대조했다. 실제 Popup 닫힘은 Root의 최종33 검사 자료로 구분한다.

검사 소스는 초기와 최종을 분리 보관했다. 최종 `FanToolTipChecks.cs` SHA는 `95C435D5711B3916D69F5B0964CCBFD24BF8137FD6E0F9CB586DF8064D640485`, PS wrapper SHA는 `8CBA727732705FC0CAAFB0E685C8A785D232F61C9F4820CE90783C38BC451893`다. 이 호스트는 자동 모드도 `Window.Show()`를 호출하므로 감사자가 실행하지 않았다. 감사자는 준비 모드로 개인 설정만 만든 뒤 별도 무창 runner로 검사했다. 두 검사 모두 설정/workspace 경로 격리와 runtime discovery 비활성화를 사용한다.

## 물리 입력 자료와 실패 보존

Root의 독립 WPF 호스트 관측을 읽기 전용으로 확인했다. 260/dark 및 200/light hover에서 `MenuOpen=true`, `TooltipEnabled=false`, `TooltipOpen=false`였다. 260 Help 클릭 관측은 `HelpClicks=1`, 200 Escape 뒤 메뉴 닫힘·서비스 복구도 기록됐다. 실제 IDE 또는 감사자의 직접 OS 입력 결과로 표현하지 않는다.

**260 Space에서 openCount 증가 후 관측 시 닫힌 상태, 외부 포커스 전환 및 초기 부모 범위/캐시 요소 입력 실패는 미확정 관측으로 유지한다. 전체 native Tab/Space/Focus 성공으로 판정하지 않는다.** 최종33의 관리 키보드 통과와 구분하며 실제 IDE·정리된 물리 입력 환경의 후속 검증 대상으로 남긴다. 이 관측만으로 이번 변경의 새 제품 결함을 확정하지 않았다.

2022 중앙 정렬 첫 실행은 64검사 후 Interop17 로드 실패했고, 다음 의존성/Framework/target 선택 실패도 보존됐다. 공유 CenterChecks/driver와 제품을 바꾸지 않고 개인 driver의 2022 SDK·4.7.2 의존성 경로를 맞춘 최종 73/73을 연결했다. 초기 실패를 처음부터 통과했다고 쓰지 않는다.

감사자의 첫 XML 비교에서 빈 요소의 `<.../>`와 `<...></...>` 직렬화 차이가 남아 false였던 결과도 보존했다. 의미가 같은 빈 요소를 정규화해 실제 나머지 XAML 일치를 확인했으며 제품 실패로 집계하지 않았다.

## Role·통합·검증 한계

Role image-43은 진행 작업 상세 Tooltip 그림이다. 이번 fan 원점 수정으로 해당 진행 상세의 겹침이나 Role 22:07 전체를 종결하지 않는다. 설정/안내 크기, 작업 상세 폭·Tooltip, 소제목 등 TASK047 변경과 TASK046 연구 제안은 별도 소유·후속 후보 범위다. 다른 담당자가 수정 중인 현재 workspace 전체를 C1과 동일하다고 주장하지 않는다.

실제 VS2017/2019/2022 IDE, 고객 Professional 15.7.1, 실계정·AI 요청·운영 SQL/TFVC·실제 고객 오프라인 설치는 이번 감사가 실행하지 않았다. 오후 공개된 v0.11.1-offline-test.2 및 정식 v0.11.1은 그대로 보존했고, 실제 고객 PC의 VSIX/IDE 결과 대기는 계속된다. C1 변경은 아직 제품 통합/정식 배포 완료를 뜻하지 않는다.

상세 [TASK-045 Evidence](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-045_Evidence.json), 근거 `C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task045/`. 감사자는 새 보고·개인 fixture·공유 작업 기록만 작성했으며 제품·Role·타담당 문서·공유 build·GUI·사용자 프로필·계정·기존 배포물은 변경하지 않았다.

## 감사 종료

시작 2026-10-19 22:20:33 +09:00, 종료 2026-10-19 22:34:52 +09:00, 경과 00:14:19. TASK045 C1 독립 감사·보고를 마치고 소유를 반납한다. GUI를 사용하지 않았으며 이후 통합 후보·전체22:07 요구 검증·실제 고객 결과·배포 판단은 해당 담당자가 이어간다.

