# TASK063 — Role 추가요청 16: Visual Studio 테마 설치·적용

담당: CodexCode. 범위: `AssistantControl.Theme.cs` 초기화 연결, `AssistantControl.IdeThemes.cs` 설정 UI·설치/적용 흐름, 4개 프로젝트 파일의 Compile/오프라인 테마 VSIX Content 항목. 기존 버전/SDK·최종 릴리스 패키징·공유 IDE/GUI는 Root054 소유로 유지했다.

## 구현

- 설정의 **화면** 페이지에 Kakao/Dracula Visual Studio 전체 테마 버튼을 표시한다. 기존 “앱 색상 팔레트”와 명확히 구별한다.
- 현재 IDE의 `DTE.Version`을 기준으로 VS2017/2019/2022용 오프라인 테마 VSIX 하나를 확장 설치 폴더의 `Themes/`에서 찾는다. 현재 IDE의 `VSIXInstaller.exe`로 설치 UI를 연다. 이때 선택한 테마는 해당 인스턴스의 로컬 보류 파일에 기록한다.
- 설치 완료 후 IDE를 다시 열어 도구창이 로드되면 현재 인스턴스의 확장 설치 manifest를 확인하고, `Tools.ImportandExportSettings /import`로 **Environment_FontsAndColors 테마 ID만** 포함한 `.vssettings`를 적용한다. `/export`로 현재 선택된 ID가 맞는지 확인한 뒤 보류 파일을 지운다. 오류/취소/미설치 시 보류를 유지하고 수동 선택 경로를 표시한다. 임시 설정 내보내기 파일은 즉시 삭제한다.
- 테마 GUID는 `scripts/Build-VisualStudioThemes.ps1`의 Kakao `{1d41c4a3-cf75-4f0a-b2ff-9a8c2d431b92}` / Dracula `{8ec84919-8a34-422a-abe1-cc238ad4d4b1}`와 일치한다. 버튼에는 사용자 파일이나 임의 명령을 전달하지 않는다.

## 검증

- 현재 4개 `.csproj`를 격리 복사본에 복제해 VS2017 Legacy/VS2017/VS2019/VS2022 Release VSIX 빌드 모두 성공, 오류 0. 제품 공유 `bin/obj` 사용 0.
- 실제 제품 DLL의 정적 메서드에서 VS15/16/17 버전 매핑 4/4, 테마별·연도별 최소 `.vssettings` XML 6/6 파싱 및 ID 확인 통과.
- 독립 테마 VSIX 2017/2019/2022 3개에서 연도별 manifest ID와 파일 해시 확인.
- 실제 로컬 사용자 VS2017 확장 설치 manifest 검출과 다른 연도 거부 확인. 설치 또는 현재 IDE 설정 변경은 하지 않았다.
- 격리 DLL 옆 `Themes/`에 오프라인 VS2017 설치 파일을 둔 뒤 실제 제품의 파일 선택 메서드로 경로 탐색 통과.
- 격리 생성 4개 AI VSIX ZIP 내부 `Themes/`에 정확히 한 개의 해당 연도 테마 VSIX가 들어 있고, 빌드 출력 폴더에도 정확히 한 개가 복사되는 것을 확인했다.
- 격리 DLL의 기존 WPF 설정/테마 관련 UI fixture 14/14 통과, `WindowShown=false`; 실제 Visual Studio 설치·테마 변경은 수행하지 않았다.
- 새 `tests/task063/Test-IdeThemeUi.ps1`의 숨김 STA WPF 검사에서 설정 화면의 두 설치 동작, 서로 다른 테마 태그, 접근성 이름, 버전 안내를 확인했다. 설치 관리자를 실행하지 않았다.

## 통합 인계 및 남은 검증

4개 `.csproj`에 Compile/Content 항목을 등록했고, 기존 `releases/v0.12.0`의 검증된 테마 VSIX를 오프라인 기본 자산으로 포함했다. 최종 배포 후보 ZIP과 설치 후 `Assembly.Location/Themes` 경로는 Root054가 다시 확인해야 한다. 테마 VSIX는 VS2017 Legacy와 일반 VS2017에서 동일한 VS2017용 파일이다. 각 테마 VSIX에는 두 테마가 모두 들어 있으며 버튼은 재시작 후 적용할 하나를 선택한다.

실제 VSIXInstaller 클릭, IDE 재시작, `.vssettings` 가져오기/내보내기 후 현재 테마 GUID 확인은 Root의 공유 GUI·설치 예약으로 남겼다. 특히 설치를 사용자가 취소한 상태에서 자동 적용 성공으로 표시하지 않는지, VS2017 15.7.1 및 VS2019/2022에서 확인해야 한다. 설치 관리자와 Visual Studio 확장은 실행 중 IDE의 종료/재시작을 요구할 수 있으므로 설정에 해당 절차를 명시했다.

근거: [Microsoft Visual Studio 테마 선택](https://learn.microsoft.com/en-us/visualstudio/ide/personalizing-the-visual-studio-ide?view=visualstudio), [Microsoft 설정 가져오기 명령](https://learn.microsoft.com/en-us/visualstudio/ide/reference/import-and-export-settings-command?view=visualstudio), [Microsoft VSIX 배포](https://learn.microsoft.com/en-us/visualstudio/extensibility/shipping-visual-studio-extensions?view=visualstudio), [Microsoft 확장 설치와 재시작](https://learn.microsoft.com/en-us/visualstudio/ide/finding-and-using-visual-studio-extensions?view=vs-2022).
