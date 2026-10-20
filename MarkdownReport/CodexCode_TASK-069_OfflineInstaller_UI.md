# TASK069 #8 · 오프라인 설치기 UI와 중지 동작

- 담당: CodexCode
- 시각: 2026-10-20 22:17–22:29 KST
- 범위: `installer/OfflineThemeInstaller/Program.cs`, `scripts/Install-AIAssistant.ps1`, `tests/task069/Test-InstallerCancellation.ps1`

설치기 화면을 확장 화면과 조화로운 남색·청록색 체계로 바꿨다. 제목 표시줄·상태 카드·활동 로그·진행 표시·버튼의 색과 여백을 통일했다. 사용자가 해야 하는 동작은 주황색으로 표시하고, 중지 요청 후 기다려야 하는 문구는 코랄색으로 표시한다. 설치 중에만 `중지` 버튼이 활성화된다. Windows 접근성 트리에서 설치 시작·중지·닫기·최소화 버튼과 상태·행동 안내가 노출됨을 확인했고, 실제 창을 눈으로 검토했다. 정적 `DrawToBitmap` 출력은 자식 컨트롤이 나타나지 않아 판정 근거로 사용하지 않았다.

중지 버튼은 임시 설치 폴더에 신호 파일을 남긴다. `Install-AIAssistant.ps1`은 Visual Studio 종료 대기 중 2초 간격으로 이 파일을 확인하고, 각 VSIX를 시작하기 직전 다시 확인한다. 사용 중인 공식 VSIXInstaller 프로세스는 강제로 종료하지 않는다. 그 설치가 끝나면 다음 패키지를 시작하지 않는다. 보고서에는 `Cancelled=true`, `CancelledAt`, 완료된 `InstallationResults`가 남고 종료 코드는 2다. 앱에서는 중지 후 남은 패키지가 설치되지 않았으며 이전 완료분은 유지된다고 명시한다.

검증:

- [중지 대기 테스트](../artifacts/task069/cancel-wait-candidate2/result.json): 가상 `devenv.exe`가 열린 동안 중지 신호를 주자 2.87초 안에 종료 코드 2, 설치 결과 0건, VSIXInstaller 로그 0건. 가상 프로세스는 시험 소유 PID만 정리했다.
- [격리 설치 테스트](../artifacts/task069/installer-ui-test-candidate3/result.json): VS2017·2019·2022 각각 확장과 테마를 전용 RootSuffix에 공식 설치, 6/6 성공. 17/17 전체 검사 통과.
- [기존 설치 경계](../artifacts/task069/installer-ui-compatibility-candidate3/results.json): 35/35 통과. VS2017 15.7.1은 해당 전용 패키지 선택을 합성 인스턴스로 검사했다.
- [최종 소스 컴파일 EXE](../artifacts/task069/installer-ui-final-code/Grokeen-AI-Theme-Offline-Installer-0.13.0.exe): 17,000,448바이트, SHA256 `8F602FC831E1D5951FCB938CA3BB0A389C4ECF58D3681C60AE3F839DB59F3ED2`; 주석만 추가한 후 [내장/버전 검사](../artifacts/task069/installer-ui-final-static/result.json) 16/16 통과.

0.13.0 패키지는 설치기 구현·시험용 입력이다. 0.14.0 통합 VSIX 7개와 `SHA256SUMS.txt`가 고정되면 `scripts/Build-OfflineThemeInstaller.ps1`로 새 EXE를 생성하고 같은 검사를 다시 실행해야 한다. 기본 Visual Studio 프로필에 대한 설치 버튼 물리 클릭, 고객 15.7.1 PC, 완전한 오프라인 고객 PC 실측은 수행하지 않았다. 최종 0.14.0 패키징·배포·공개 자산 검증은 통합 담당 CodexOrig 소유다.

## 22:45–22:50 사용자 후속 요청: Dracula UI·중앙 게이지

사용자 요청에 따라 설치기의 남색·청록색 사용자 화면을 독립 Dracula IDE 테마의 팔레트 `#282A36` 배경, `#44475A` 카드, `#F8F8F2` 본문, `#BD93F9` 주 버튼·강조, `#8BE9FD` 보조색, `#50FA7B` 오프라인 표시, `#FFB86C` 사용자 행동 안내, `#FF5555` 중지·오류색으로 교체했다. 기존 카드·제목·버튼 구조와 중지 기능은 유지했다.

4px 가로 활동 표시를 47px 높이의 중앙 게이지로 교체했다. 가운데 표시점과 숫자 `%`가 보이고, 공식 VSIXInstaller에서 성공한 패키지마다 `INSTALL_PROGRESS 완료수/전체수`를 받아 중앙에서 양쪽으로 같은 거리만큼 채운다. 사전검사·IDE 대기 중에는 0%에서 중앙만 움직인다. 설치 중지·실패 시에는 완료된 비율을 남기고, 성공한 경우에만 100%를 표시한다.

- [0.13.0 입력으로 재빌드한 최종 검증용 EXE](../artifacts/task069/dracula-center-gauge-final/Grokeen-AI-Theme-Offline-Installer-0.13.0.exe): 17,001,984바이트, SHA256 `5A3EADED6FD668D78925A9D5FA0C2E473127D0BF5C5E7EE070198CEBE116D3F1`. 재시도 시 이전 완료율을 즉시 0%로 되돌리는 마지막 UI 수정까지 포함했다. 이 최종 EXE의 내장·버전 검사는 [16/16](../artifacts/task069/dracula-center-gauge-final/static-test/result.json) 통과했다.
- [50% 게이지 렌더](../artifacts/task069/dracula-center-gauge-final/gauge-test/dracula-center-gauge-50.png): 양쪽 채움/바깥 트랙 픽셀, 0–100 범위 제한과 재시도 즉시 0% 초기화 검사 통과.
- [격리 설치 결과](../artifacts/task069/dracula-center-gauge/test/result.json): VS2017·2019·2022에 확장·테마 6/6, 검사 17/17. 실제 출력에서 `1/6`부터 `6/6`까지 순서대로 확인.
- [중지 회귀](../artifacts/task069/dracula-center-gauge/cancel-test/result.json): IDE 종료 대기 중 중지 코드 2, 설치·VSIXInstaller 실행 0건, 3.04초.

`releases/v0.14.0` 입력은 아직 없어 0.14.0 공개 파일은 생성하지 않았다. 통합 담당은 0.14.0 VSIX가 확정되면 이 소스로 다시 빌드하고 게이지 테스트·설치 테스트를 실행해야 한다.
