# TASK-049: 작업 완료 알림과 대화 기록 점진 표시

작성: CodexCode · 2026-10-19

Role.md 2026-10-19 23:09 추가 요청 중 1번과 7번을 구현했다. 다른 담당자의 UI/테마 및 배포 작업과 파일 소유를 분리했다.

## 동작

- AI 요청이 실제로 `completed`로 종료된 경우에만 사용자 설정에 따라 Windows 작업 표시줄 알림을 한 번 표시한다. 실패·중단·중복 완료에는 표시하지 않는다. 알림에는 요청·응답·파일 경로를 넣지 않는다. 설정 기본값은 사용이며 이전 설정 JSON도 같은 기본값을 갖는다. 아이콘은 20초 후 또는 도구 창이 내려갈 때 해제한다.
- 저장된 대화를 열면 화면에는 최신 메시지 20개만 우선 표시한다. 대화 창의 위쪽에 도달하면 이전 메시지 20개를 앞에 추가하고 스크롤 위치를 보정한다. 대화 전환 때 앞 대화의 이벤트를 해제한다. 저장과 AI 요청 문맥에는 원래 `ChatSession.Messages` 전체를 계속 사용한다.
- 기존 저장 형식과 메시지 100개·파일 4MB 상한은 유지한다. 이미 상한 때문에 삭제된 메시지는 이 기능으로 복원할 수 없다.

## 검증

- `tests/task022/Test-ProgressPresentation.ps1` source-only 격리 검사: 47/47 통과, `artifacts/task049/progress-source-final/result.json`.
- `tests/task049/Test-HistoryPaging.ps1`는 실제 제품 DLL과 WPF 창이 필요한 검사로 작성했다. 새 통합 Release DLL 및 공유 GUI 사용 순서가 정해진 뒤 실행해야 한다.
- 최종 제품 DLL에서 Windows 알림 표시와 Visual Studio 2017/2019/2022 동작은 아직 확인하지 않았다.

## 표시 방식의 범위

현재 알림은 .NET Framework의 `NotifyIcon.ShowBalloonTip` 작업 표시줄 balloon이다. Role.md image-44의 WinRT 원형 toast, 상단 위치, 클릭 시 Visual Studio 복귀는 보장하지 않는다. VSIX만으로 [Windows desktop toast의 Start 바로가기/AppUserModelID 전제](https://learn.microsoft.com/en-us/windows/win32/shell/enable-desktop-toast-with-appusermodelid)를 충족한다고 가정할 수 없어 [지원되는 NotifyIcon API](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.showballoontip)를 사용했다. 실제 표시 여부·시간은 Windows 알림 설정에 따른다.
