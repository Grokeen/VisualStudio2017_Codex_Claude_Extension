# TASK-045 — 부채꼴 메뉴의 원점 툴팁 중첩 수정

CodexOrig5의 TASK-044 제안을 인수해, ☷ 원점의 ‘설정 및 사용 안내’ 툴팁이 열린 부채꼴의 안내·설정 글자를 가리는 문제를 수정했다. 이 작업은 부채꼴 원점에 한정한다. Role 22:07의 작업 상세 팝업·설정 창 크기 등은 CodexCode TASK-047의 별도 작업이며 이번 수정으로 전체 요청을 완료했다고 보고하지 않는다.

## 변경

- `AssistantControl.xaml`: 기존 도움말 문구를 실제 `ToolTip` 객체에 유지해 이미 표시된 팝업도 직접 닫을 수 있게 했다. Automation Name은 유지했다.
- `AssistantControl.Fan.cs`: 메뉴를 열 때 `ToolTipService.IsEnabled=false`로 예정된 툴팁을 막고 현재 `ToolTip.IsOpen=false`로 열린 팝업도 닫는다. 닫을 때 도움말 서비스를 다시 허용한다. 열린 메뉴 재진입 때도 적용한다.
- 기존 부채꼴 좌표·클릭 영역·애니메이션·175ms 이동 여유·Tab 순서·Escape 처리·포커스 복귀·구독 해제 코드는 유지했다. 기존 주석은 보존하고 Role 형식으로 변경 설명을 추가했다.

Microsoft의 [ToolTipService.IsEnabled](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.tooltipservice.isenabled?view=windowsdesktop-10.0)와 [ToolTip.IsOpen](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.tooltip.isopen?view=windowsdesktop-10.0) 계약을 구분해 사용했다. 이 수정은 표준 WPF 표시 상태만 변경하며 파일·명령·계정·네트워크 권한을 추가하지 않는다.

## 검증

개인 `artifacts/task045/candidate1`은 기존 C17 입력 122개에서 제품 두 파일만 변경해 동결했다. 공유 `bin/obj`, 기존 VSIX, 사용자 IDE 설치를 교체하지 않았다. 네 대상 Release 빌드는 오류·경고 모두 0이다.

| 대상 | 실제 ToolTip/WPF 키보드 | 기존 기하 | 기존 중앙 정렬 |
|---|---:|---:|---:|
| VS2017 15.7 전용 | 33/33 | 21/21 | 73/73 |
| VS2017 | 33/33 | 21/21 | 73/73 |
| VS2019 | 33/33 | 21/21 | 73/73 |
| VS2022 | 33/33 | 21/21 | 73/73 |

새 검사는 실제 제품 XAML·DLL을 로드하고 ToolTip Popup을 실제로 연결한 뒤, 200/260px·light/dark 네 조합에서 현재 툴팁 닫기·재진입·닫힘 후 도움말 복구를 확인한다. WPF `Keyboard.Focus`·Tab traversal·관리 Escape 라우트로 메뉴 두 항목 탐색과 원점 복귀도 확인했다. 이 세 항목은 OS 키 주입 검사와 구분한다. 처음 30개 결과는 보존하고 최종 검사 소스에서 33개를 다시 실행했다.

computer-use로 별도 `MenuVerificationHost.exe`에 실제 입력을 주어, 제품 폭 260px/dark 및 200px/light에서 원점 hover 시 메뉴가 열리고 툴팁이 억제되는 것을 관측했다. 260px에서 안내를 한 번 클릭했을 때 클래스 Click 계측 1회와 실제 안내 창 하나를 확인했고, 200px에서는 실제 Escape로 메뉴가 닫히고 도움말 서비스가 복구됐다. IDE 안의 Codex/Claude 세션이나 계정을 조작하지 않았다. 임시 설정·작업 폴더와 CLI 발견 비활성화를 사용했고 두 최종 검사 창을 닫았다.

처음 작은 검사 호스트에서는 소유 팝업이 도구의 부모 창 좌표 범위를 넘어 닫기 입력이 거부됐다. 그 시도와 종료 근거를 보존하고 제품 폭은 유지한 채 검사 호스트 바깥 크기를 620×800으로 바꾸었다. 이후 소유 창 요소 캐시 오류도 원문을 보존하고 새 관측의 실제 좌표로 안내 창을 닫았다. 260px에서 Space 후 일시 열림·닫힘이 관측됐으며, 별도 200px 계측에는 외부 활성 창 전환도 기록됐다. 원인과 제품 결함 여부를 단정하지 않으며 전체 OS Tab/Space/포커스 검사를 성공으로 표시하지 않는다. 관리 WPF 키보드 검사 성공과 위에 명시한 실제 OS 입력 관측만 보고한다.

VS2022 중앙 정렬 검사는 기존 driver가 VS2017 SDK/Framework 의존성을 고정 선택하여 처음 Interop 누락, 다음 ThreadHelper TypeLoad, 이어 잘못된 Framework 대상 선택으로 중단했다. 초기 원문을 각각 보존했다. 기존 `CenterChecks.cs`와 공유 driver는 수정하지 않고, 개인 driver 복사본의 의존성 경로와 Framework 선택을 실제 2022 후보의 SDK/net472에 맞춘 뒤 73/73을 확인했다. 이 초기 중단을 제품 assertion 실패로 바꾸거나 숨기지 않는다.

재현은 Windows PowerShell 5.1에서 다음과 같다. 결과 폴더는 새 경로를 사용한다.

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task045/Test-FanToolTip.ps1 -AssemblyPath <고정 제품 DLL> -AssetsPath <같은 대상의 obj/project.assets.json> -OutputDirectory <새 검증 폴더>
```

이 검사는 실제 WPF 창과 Popup을 표시한다. `-PrepareInteractive`는 계측용 EXE/구성을 만들며 실제 입력 자동 성공을 뜻하지 않는다. 네 대상 DLL/VSIX 해시·검사 소스·원문 위치·관측 및 한계는 [검증 JSON](CodexOrig_TASK-045_Validation.json)에 기록한다.

## 협업·배포 상태

90% 구현 시점에 사용자 지시에 따라 CodexOrig2에게 독립 감사를 요청했다. 독립 감사가 완료됐고 각 대상 비표시 상태 검사 36/36, 보호 원본 76/76 보존, 새 확정 지적 0건을 확인했다. [감사 보고](CodexOrig2_TASK-045_Audit.md)와 [감사 근거](CodexOrig2_TASK-045_Evidence.json)를 연결한다. Root GUI 예약은 22:31:35에 종료했다.

검사 동결 뒤 CodexCode가 별도 TASK-047 제품 파일을 수정하고 있다. 이번 결과는 그 변경을 포함한 통합 후보의 성공이 아니다. Root 소유 두 파일과 동결본 122개의 해시를 별도로 대조하며 타담당 변경은 이번 커밋·검증에 포함하지 않는다. 설정 소제목의 XAML 소유권은 감사·기록 마감 후 넘긴다.

이 UI 수정 후보를 기존 공개 파일에 덮어쓰지 않았다. 다운로드 가능한 [오프라인 테스트 릴리스](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.11.1-offline-test.2)는 앞서 게시한 검증용 C6 그대로이며 이번 UI 수정을 포함하지 않는다. 고객 PC의 실제 오프라인 VSIX 설치와 IDE 창 결과도 여전히 대기한다. 다음 통합 제품 배포는 TASK-047 등 진행 중인 작업·검증 종료와 버전 정리를 거쳐 별도로 판단한다.


마감: 2026-10-19 22:36:31 +09:00, 시작 2026-10-19 22:15:38 +09:00, 총 경과 00:20:53.3380471. 수정·개인 빌드·검증·감사 대응을 종료한다. XAML 소유권은 부채꼴 툴팁 변경을 보존하는 조건으로 CodexCode TASK-047의 소제목 보완·통합 검증에 넘긴다. DR046-1은 현재 통합 작업과 실제 오프라인 PC 결과 수집 뒤 별도 후보로 검토하는 요청으로 기록했다. 이 후보를 새 공개 릴리스로 게시하지 않았다.
