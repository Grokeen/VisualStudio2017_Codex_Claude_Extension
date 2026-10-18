# 프로젝트 작업 회귀 검사

Windows PowerShell 5.1에서 실행한다. C# 컴파일러와 Git이 필요하다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/workspace-actions/Test-WorkspaceActions.ps1
```

제품 빌드 후 실제 DLL과 WPF 팝업을 함께 검증하려면 다음과 같이 실행한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/workspace-actions/Test-WorkspaceActions.ps1 -ProductAssemblyPath src/VS2017.AIAssistant/bin/Release/VS2017.AIAssistant.dll
```

결과는 고유한 `artifacts/workspace-actions/run-*` 디렉터리에 저장된다. 제품 DLL 모드는 개인 설정과 Markdown 경로를 이 디렉터리로 격리한다.

- 실제 로컬 Git bare 저장소를 만들어 fetch, fast-forward pull, 기존 브랜치 checkout, 지정한 추적 파일 commit, push를 실행한다.
- 커밋 대상 외의 staged 파일과 untracked 파일이 포함되지 않는지 확인한다.
- 네이티브 fixture로 한글·공백·따옴표·역슬래시·빈 인수 전달, 실패 코드, 출력 상한, 시간 제한, 취소 및 PID 종료를 검증한다.
- DB 권한·연결 정보 검증은 접속 전에 검사한다. 실제 SQL Server 접속과 스키마 조회는 이 테스트에 포함하지 않는다.
- SVN/TFVC는 검토용 인수 생성과 경로 제한을 검증한다. 실제 SVN/TFVC 서버와 실제 배포 대상에 접속하지 않는다.
- 제품 DLL 모드에서는 팝업의 명령 확인·실행 동의·권한이나 대상 변경 후 재승인·DB 연결 문자열의 AI 미전달을 검사하고 다크 화면 PNG를 만든다.

Git pull은 [공식 `--ff-only` 설명](https://git-scm.com/docs/git-pull), 선택 파일 커밋은 [공식 `--only` 설명](https://git-scm.com/docs/git-commit/2.25.0)을 따른다. TFVC의 [checkout](https://learn.microsoft.com/azure/devops/repos/tfvc/checkout-or-edit-command?view=azure-devops)과 [명령 구문](https://learn.microsoft.com/azure/devops/repos/tfvc/use-team-foundation-version-control-commands?view=azure-devops), SVN의 [checkout](https://svnbook.red-bean.com/en/1.7/svn.ref.svn.c.checkout.html) 문서를 참고했다. Role의 `tns`는 TFS/TFVC로 해석했으며 별도의 TNS 제품 연동은 포함하지 않는다.
