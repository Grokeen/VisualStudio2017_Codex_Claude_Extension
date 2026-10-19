# TASK-036 / VS2017 15.7.1 독립 호환성 검토 — CodexCode

2026-10-19 09:04 KST. 사용자가 고객 PC의 **Visual Studio Professional 2017 15.7.1**과 **업데이트 불가**를 확인했다. [Microsoft VS2017 릴리스 이력](https://learn.microsoft.com/en-us/visualstudio/releases/2017/vs2017-relnotes-history)의 해당 빌드는 `15.7.27703.2000`이다. 기존 v0.11.0 VS2017 VSIX의 `InstallationTarget`과 `CoreEditor` 하한 `15.0.28307.0`은 이를 제외한다. 고객 오류 `NoApplicableSKUsException`과 일치하는 설치 차단 조건을 확인했으며, 고객이 실제 사용한 파일과 전체 로그를 대조하기 전에는 다른 요인까지 배제하지 않는다. 원본 v0.11.0 패키지 SHA와 검사 방법은 [이전 매니페스트 검토](CodexCode_TASK-036_ManifestReview.md)에 있다.

CodexOrig가 작업 중인 `src/VS2017.Legacy.AIAssistant`는 기존 15.9 프로젝트와 별도다. 검토 시점의 참조는 `Microsoft.VisualStudio.Shell.15.0` **15.7.27703**, `Microsoft.VisualStudio.Threading` **15.6.31**, `Microsoft.VSSDK.BuildTools` **15.7.109**이다. 공통 C# 70개·XAML Page 2개·Content 11개가 연결됐고, 초기 Release DLL의 실제 어셈블리 참조에는 Shell/Framework 15.0.0.0, Threading 15.6.0.0, EnvDTE 8.0.0.0, VS Interop 7.1이 있다. Newtonsoft.Json 직접 참조는 없다. 초기 VSIX 안에는 제품 DLL·`extension.vsixmanifest`·`catalog.json`·`manifest.json`이 들어 있다. **이는 15.7 SDK 정적 컴파일·패키징 근거**이며 고객 Professional 15.7.1에서의 설치·로드·메뉴·도구창·문서 동기화 성공 증거는 아니다. 이 개발 PC의 VS2017은 Community 15.9.83이다.

독립 검토에서 통합 담당자에게 전달한 지점:

1. 최초 설치기 테스트의 `15.7.27703.1` fixture는 실제로 **15.7.0**이다. 고객 15.7.1은 `15.7.27703.2000`으로 시험해야 한다. VSIX 범위 표현은 [Microsoft 문서](https://learn.microsoft.com/en-us/visualstudio/extensibility/how-to-add-a-dependency-to-a-vsix-package?view=visualstudio)의 `15.0.<build>.<revision>` 규칙에 맞춰 비교한다.
2. 15.7 전용 VSIX와 기존 VS2017 VSIX가 같은 확장 ID·버전에서 설치 범위가 겹치면 수동으로 패키지를 고르는 15.9 사용자가 둘 다 설치 대상으로 볼 수 있다. 전용 범위와 일반 범위를 나누거나 설치기·안내에서 중복을 의도적으로 처리해야 한다.
3. 설치기 초안의 `Year=2017` 단일 선택·v0.11.0 고정 파일명은 새 15.7 패키지를 선택할 수 없었다. 새 버전/패키지 선택과 정확한 15.7.1 진단, 두 2017 인스턴스 및 2019/2022 경계 검증이 필요하다. 명시한 패키지 폴더에 체크섬이 없을 때 과거 경로로 조용히 바뀌는 동작도 잘못된 파일 선택을 숨길 수 있다.
4. [Microsoft vswhere 설명](https://github.com/microsoft/vswhere/wiki/Encoding)에 따르면 `-utf8`은 **vswhere 2.5 이상**에서 지원된다. 오프라인 고객 PC의 실제 `vswhere.exe` 버전은 알 수 없다. 설치 전 진단 스크립트가 구버전 도구에서도 동작하거나, 지원하지 않는 경우 분명한 대체 안내를 제공해야 한다. 직접 VSIX 설치 가능성과 보조 스크립트 실행 가능성은 별도 판정이다.
5. 고객 PC의 .NET Framework 4.6.2와 WebView2 Runtime 존재 여부, 실제 VSIXInstaller 결과·ActivityLog는 아직 확인하지 않았다. 이를 격리 테스트 성공으로 대신 표시하지 않는다.

이 검토는 제품·매니페스트·설치기·테스트·공유 빌드·GUI·릴리스를 변경하지 않았다. 초안 상태를 읽은 결과이므로 통합 담당자의 최종 후보에서 다시 확인해야 한다. 구현·다중 버전 빌드·실제 IDE 회귀·감사·배포는 TASK-036 CodexOrig가 소유한다.

## 09:06 선택기 후속 확인

별도 `artifacts/task036-codexcode/Verify-Selector.ps1`에서 현재 v0.11.1 네 VSIX와 설치기 함수를 사용해 Professional 합성 인스턴스 **9/9**를 검사했다. 결과는 `selector-results.json`에 원문을 보존했다. 공식 15.7.1 값 `15.7.27703.2000`은 `Ready / 2017-15.7`, 15.9의 첫 일반 패키지 경계 `15.9.28307.0`은 `Ready / 2017`, 2019·2022도 각각 해당 패키지를 선택했다. 15.7 하한 아래 값은 거부됐다. 이는 실제 패키지 메타데이터를 사용한 **합성 선택 검사**이고 고객 IDE 설치 검사가 아니다. 설치기 초안의 파일명은 이후 0.11.1로 갱신됐으므로 위 항목 3은 최초 초안에서 발견한 상태로 읽는다.
