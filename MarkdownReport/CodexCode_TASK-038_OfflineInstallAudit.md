# TASK-038 독립 검토: 인터넷 없는 고객 PC의 VSIX 설치·실행

- 담당: CodexCode
- 검토 시각: 2026-10-19 11:03~11:05 KST
- 사용자 확인 범위: **VSIX를 다른 PC에서 받아 옮긴 뒤 고객 PC에 설치하고 Visual Studio에서 여는 것이 우선**. AI 대화·CLI 오프라인 구동은 이번 확인의 성공 조건이 아니다.
- TASK-038의 설치 도구·문서·새 배포는 CodexOrig가 소유한다. 이 검토는 기존 공개 v0.11.1을 읽고 별도 검사 파일만 만들었다.

## 확인 결과

1. 공개 v0.11.1 ZIP의 SHA-256은 `62B4D4A231B8D8338FCF0D1B241B755A9F2603E602D6671DD55D0EE46C871E71`이며 `RELEASE_SHA256SUMS.txt`와 일치했다. 새 폴더에 압축을 풀어 `BUNDLE_SHA256SUMS.txt`의 **30/30** 파일을 다시 검사했다. ZIP에는 4개 VSIX와 로컬 VSIX 설치 배치·PowerShell 스크립트가 들어 있다.
2. 압축 해제 폴더의 `Install-AIAssistant.ps1 -PackageDirectory <해제 폴더> -ReportPath <새 JSON>`을 실행했다. 로컬 VS2017 **15.9.37627.17**, VS2019 **16.11.37627.13**, VS2022 **17.14.37806.7** 세 인스턴스가 모두 `Ready`였고 각 연도 VSIX가 선택됐다. 별도로 **합성** Professional 2017 `15.7.27703.2000` 입력은 `VS2017.15.7.AIAssistant-0.11.1.vsix`를 `Ready`로 선택했다. 이는 **설치 전 진단**이며 VSIXInstaller를 통한 실제 설치·도구 창 열기 결과는 아니다.
3. VSIX 설치 스크립트 `InstallAIAssistant.bat`와 `Install-AIAssistant.ps1`에는 다운로드 명령이 없고, 로컬 `vswhere.exe`, SHA-256 검사, 로컬 `VSIXInstaller.exe`를 사용한다. `InstallCLI.bat`는 `Invoke-WebRequest`로 Codex·Claude 설치기를 내려받으므로 인터넷 없는 PC에서 VSIX 설치 절차로 안내하면 안 된다. ZIP에 Codex·Claude CLI 실행 파일도 없다.
4. VS2017 15.7 전용 VSIX는 `Microsoft.VisualStudio.Community [15.0.27703.0,16.0)`와 CoreEditor 같은 하한, `.NET Framework [4.6.2,)` 의존성을 선언한다. Professional은 앞선 호환성 감사의 대상 SKU 규칙을 따른다. 고객 PC에 이미 있는 Visual Studio 설치 구성과 .NET 상태를 확인해야 하며, 로컬 스크립트의 `Ready`만으로 최종 의존성 충족을 보증하지 않는다. 제품 화면의 WebView2 PDF 설명서에는 런타임 부재 시 대체 안내가 있다.

## 고객에게 전달할 최소 절차

인터넷 가능한 다른 PC에서 [v0.11.1 설치 묶음 ZIP](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.11.1/VisualStudio_AI_Assistant-0.11.1.zip)을 받고 위 SHA-256을 확인한다. USB 등으로 ZIP **전체**를 고객 PC에 옮겨 새 폴더에 모두 압축 해제한다. Visual Studio를 닫고 `InstallAIAssistant.bat`를 실행하여 설치 대상 VS2017 Professional 15.7.1을 선택한다. 설치가 끝나면 Visual Studio를 다시 열고 **도구 → AI Assistant**를 연다. 설치가 실패하면 ZIP의 `INSTALL_TROUBLESHOOTING.md`에 따른 진단 JSON과 VSIXInstaller 로그를 고객 PC에 보존한다. 자동 스크립트가 실행되지 않으면 `VS2017.15.7.AIAssistant-0.11.1.vsix`를 직접 실행할 수 있다. `InstallCLI.bat`는 실행하지 않는다.

## 검증 경계

이 개발 PC의 네트워크를 차단하지 않았고 고객의 **VS2017 Professional 15.7.1**에 접근하지 못했다. 따라서 이 결과는 배포물의 로컬 설치 준비와 사전 진단만 증명한다. 고객 PC에서 실제 설치, 메뉴 노출, 도구 창 생성은 별도 확인이 필요하다. AI 요청은 제공자 서비스와 CLI 인증 정책에 달려 있으므로 이번 설치 성공 기준과 분리한다.

검사 재현: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File artifacts/task038-codexcode/Verify-OfflineBundle.ps1`. 로컬 근거: `artifacts/task038-codexcode/evidence.json` 및 `preflight-*.json`.
