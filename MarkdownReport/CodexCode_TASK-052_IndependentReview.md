# TASK-052 · Role 23:09 독립 요구사항·회귀 검토

- 담당: CodexCode (제품 파일 편집 없이 TASK051 CodexOrig / TASK050 CodexOrig4 구현 검토)
- 기준: `Role.md` 2026-10-19 23:09의 1~11번, `image-44.png`~`image-55.png`
- 협업 범위: TASK051은 Windows 완료 알림·기록 점진 표시·통합, TASK050은 팝업·설정·Markdown·테마를 소유한다. 이 문서는 독립 관찰과 후속 검증을 기록하며 두 담당자의 완료 선언을 대신하지 않는다.

## 초기 확인

| 요구 | 현재 구조와 검증 관점 |
|---|---|
| 1. Windows 완료 알림 | 기존 `ToastHost`는 도구 창 중앙의 3초 안내이며 image44의 운영체제 알림과 다르다. 완료 1회, 대화별 소유권, 비밀·프롬프트를 제외한 문구, 중지·실패와의 구분, 창 종료 시 자원 해제를 확인해야 한다. |
| 2~3. 공통 팝업·뒤로가기 | image45는 안내 창의 녹색 OS/VS 비클라이언트 제목 표시줄, image46은 상세 안내 상단의 돌아가기 버튼이다. 공통 창의 제목·본문·버튼 팔레트와 크기·키보드 이동을 함께 확인한다. |
| 4~5. Markdown 창·탭 | image47의 편집 창은 넓은 작업 영역에 맞게 크기를 조절해야 한다. 체크박스는 편집기 명령 버튼과 미리보기 표시의 의미를 구분한다. image48의 제공자 탭은 기존 클릭 전환을 보존하면서 간격과 키보드 전환을 확인한다. |
| 6. 링크·콜론 정렬 | image49의 링크는 굵기 강조 대상이다. `URLn : 설명`의 수직 정렬은 비례 글꼴의 공백 패딩으로 보장되지 않으므로 표 또는 두 열 레이아웃을 검토한다. |
| 7. 긴 대화 이력 | 현재 `ChatSession.Add`와 작업 메시지 추가는 100개를 넘으면 앞 메시지를 제거하고, `MarkdownWorkspaceStore`도 100개 저장 상한이다. 최근 20개 최초 표시/상단 스크롤 시 이전 20개 추가 구현은 현재 저장된 최대 100개 안에서만 작동한다. 표시 컬렉션만 저장하면 과거 기록이 지워지므로 저장·AI 문맥은 전체 세션과 분리해야 한다. |
| 8~10. 설정·Markdown 도구 | image51의 가로 목차를 왼쪽 섹션 탭으로 전환하고, image52의 계정 관련 버튼·설명 간격과 image53의 15개 명령 도구군을 시각적으로 구획한다. 취소 시 설정 복원, 좁은 창, 접근성 이름을 회귀 검증한다. |
| 11. VS 테마 | 현재 auto/light/dark 팔레트는 VS 기본 Blue/Blue Extra Contrast/Dark/Light의 색조를 각각 재현하지 않는다. image54/55는 모바일 카카오톡 참고이며 Visual Studio 전체 테마로 그대로 복제할 수 없다. 앱 내부 프리셋과 IDE 전역 사용자 테마는 별도 산출물·설정으로 검증한다. |

## Windows 알림 구현 경계

Microsoft의 [데스크톱 toast 활성화 안내](https://learn.microsoft.com/en-us/windows/win32/shell/enable-desktop-toast-with-appusermodelid)와 [발송 안내](https://learn.microsoft.com/en-us/windows/win32/shell/quickstart-sending-desktop-toast)는 Start 메뉴 바로가기와 AppUserModelID를 요구한다. VSIX 설치만으로 이 조건이 충족된다고 가정할 수 없다. [.NET Framework `NotifyIcon.ShowBalloonTip`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.showballoontip)은 작업표시줄 balloon API이며 image44 형태, 클릭 후 해당 대화 복귀, Action Center 보존을 자동 보장하지 않는다. 구현·테스트 기록에서 제공한 수준을 정확히 구분한다.

## VS 테마 조사

Microsoft의 [VS-ColorThemes 예제](https://github.com/microsoft/VS-ColorThemes)와 [VSIX color compiler 안내](https://learn.microsoft.com/en-us/visualstudio/extensibility/internals/vsix-color-compiler?view=visualstudio)는 XML 색 테마를 `.pkgdef`로 컴파일해 별도 VSIX에 등록하는 경로를 보여 준다. 이 PC에는 VS2017/2019/2022 SDK별 `VsixColorCompiler.exe`가 있다. 사용자가 준 [Dracula VS Code 저장소](https://github.com/dracula/visual-studio-code)는 VS Code 전용이며 [Visual Studio용 설치 안내](https://github.com/dracula/visual-studio/blob/master/INSTALL.md)는 별도다. 실제 15.7.1 고객 PC에서 IDE 테마 설치·선택은 아직 검증되지 않았다.

최신 팔레트 참고는 [Dracula 공식 명세](https://draculatheme.com/spec)와 [Visual Studio 공식 포트 안내](https://draculatheme.com/visual-studio)로 확인했다. 카카오의 모바일 화면 이미지는 밝은 목록·노란 강조·연청 대화 배경을 보여 주지만 Visual Studio 토큰 명세는 아니다. 별도 테마에서는 이 색을 화면 전체에 그대로 채우기보다 포인트/텍스트 대비에 맞춰 사용해야 한다는 설계 추론이다.

## 후속 독립 검증

TASK051/050가 제품 파일 소유를 반납하면 새 DLL/VSIX 고정 SHA로 네 대상 빌드·기존 회귀 검사, 성공/실패/중단 알림 구분, 60~100개 대화 최초·상단 추가·저장 재실행, 설정 좌측 탐색·취소 복원, Markdown 링크·표·체크박스, 좁은 화면과 실제 WPF 캡처를 확인한다. 기존 릴리스와 고객 PC 검증 결과는 별도로 유지한다.
