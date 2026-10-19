# TASK-038 — 확장·CLI 오프라인 설치 묶음

사용자는 확장과 CLI는 인터넷 없이 설치하고, AI를 사용할 때는 연결할 수 있다고 확인했다. Role.md의 추가 조건은 AI 관련 서버만 허용되는 환경이다. 기존 `InstallCLI.bat`는 온라인 다운로드용이므로 대상 PC에서 실행하는 절차로 해결할 수 없다. 공식 설치된 Windows x64 CLI 전체 런타임과 기존 v0.11.1 네 VSIX를 준비 PC에서 묶고, 대상 PC에는 로컬 파일만 복사하는 별도 도구를 추가했다.

## 변경과 사용 순서

- `PrepareOfflineSetup.bat`와 `scripts/Prepare-OfflineSetup.ps1`: 준비 PC의 공식 Codex standalone 및 Claude 네이티브 CLI를 사용한다. OpenAI 실행 파일 4개와 Claude 본체의 유효한 게시자 서명, 전체 런타임 구성, 네 VSIX 체크섬을 확인한다. CLI를 실행하거나 로그인하지 않으며 계정·프로필·토큰·대화를 수집하지 않는다.
- `InstallOfflineCLI.bat`, `scripts/Install-OfflineCli.ps1`, `scripts/OfflineCli.Common.ps1`: 로컬 manifest·파일 수·SHA256·크기·metadata·x64 PE를 확인하고 새 사용자별 폴더에 복사한다. 기본 PS 호출은 검증만 수행하고 BAT 또는 `-Install`에서만 복사한다. 다운로드·온라인 서명 조회·CLI 실행·로그인·PATH/VS 설정 변경을 하지 않는다. 같은 설치는 재검증하고 내용이 다른 기존 파일은 보존한다.
- manifest와 metadata를 읽기 전에 부모부터 파일까지 연결 경로를 거부한다. UNC·네트워크 드라이브·경로 이탈·ADS·예약 이름·추가 파일·계정 자료·중복 경로와 1MiB 초과 JSON을 거부한다. 보고서 경로도 복사 전에 검사하며 기존 로그를 덮어쓰지 않는다.
- README·사용 안내·설치 오류 문서에서 새 [오프라인 안내](../docs/OFFLINE_INSTALL.md)로 연결한다. CodexOrig3가 실제 도구명·경로·로그·필수 구성 요소 및 AI 서버 연결과 최초 인증의 차이를 설명했다. VSIX/CLI만 포함하므로 빠진 Windows·Visual Studio 구성 요소를 추가 설치하지 않는다.

완성 ZIP을 전부 압축 해제하고 **InstallOfflineCLI.bat → InstallAIAssistant.bat → 확장 설정에서 출력된 CLI 경로 지정 → 허용된 연결에서 공식 로그인·실제 요청 확인** 순서로 사용한다. VS2017 15.7·15.8에는 기존 전용 VSIX가 선택된다. Windows x64, 동봉 Claude 기준 Windows 10 1809/Server 2019 이상이 필요하다. 고객 Windows 버전 질문에는 아직 답변이 없으므로 그 조건을 확정 사실로 취급하지 않았다.

## 최종 파일과 검증

제품 C#·네 VSIX·테스트 실행기는 기존 C17/v0.11.1 그대로다. 이번 변경은 새 설치 도구·검사·문서·보충 자산이다. 기존 122개 제품 입력과 보호 자산 47개, v0.11.1 원본 다운로드 7개가 모두 동일하다. 과거 전체/IDE 검사를 새 오프라인 검사 결과로 재집계하지 않는다.

| 검사 | 결과와 범위 |
|---|---|
| Windows PowerShell 5.1 회귀 | 최종 31/31. 손상·누락·경로/링크·metadata·기존 설치/로그 보존·미실행·PATH 불변 검사. C# 대역 주석도 Role 예시 정렬에 맞춘 뒤 새 실행으로 확인 |
| 완성 C4 ZIP | 59/59 항목 SHA·크기 일치, 내부 체크섬 58/58, CLI 46개, 기존 VSIX 4개 동일 |
| 공개 준비 도구 ZIP | 37/37 항목 원문 일치, 내부 체크섬 36/36, CLI 실행 파일 포함 0 |
| 공개 ZIP 실제 준비 실행 | 새 폴더에 공개 ZIP을 풀고 동봉 Prepare 스크립트로 완성 폴더 생성. CLI 46개 해시 및 최종 안내서 동일, 로그인·CLI 실행 없음 |
| 실제 C4 복사·버전 실행 | 전용 새 폴더에 46개 복사 성공. 별도 검사에서 Codex 0.163.0·Claude 2.1.302의 `--version` 각각 통과. 설치기는 CLI를 실행하지 않음 |
| 반복 설치 | C3의 동일 코드·payload로 `AlreadyInstalled`, 파일 46개 재검증. C4 회귀의 합성 자료에서도 반복 설치 검증 |
| 독립 감사 TASK-040 | CodexOrig2가 R40-01 링크 결함 재현 후 수정·최종 C4 연결을 확인. 미해소 확정 지적 0. 첫 실패와 수정 근거 모두 보존 |

최종 고객 전달 파일은 로컬 `artifacts/task038/VisualStudio_AI_Assistant-0.11.1-FullOffline-win-x64.zip` 및 `.zip.sha256`이다. **308,366,787 bytes**, SHA256 `DB06BF91635D4C3F0B23F057E5CC39EAEB22714E2DC298C8AA477E142F8CA558`. CLI 계정 자료는 없다. 원본 후보 C1~C4와 처음 실패한 테스트/감사 자료는 덮어쓰지 않았다. C3 검증 중 안내서가 최종 수정되어 원문 비교가 중단된 로그도 보존하고, 최종 문서가 들어간 C4를 새로 검증했다.

GitHub에는 CLI payload 없이 준비 도구·네 VSIX를 담은 `VisualStudio_AI_Assistant-0.11.1-OfflineTools1.zip` 및 `OFFLINE_TOOLS1_SHA256SUMS.txt`만 보충한다. 준비 ZIP은 **16,940,303 bytes**, SHA256 `C1743C89604DE34B8F0BBC68416017E719C9925759AF807169AC87B16F059E83`. 기존 일곱 자산·태그는 그대로 두고 새로운 이름의 두 자산만 추가한다. [최종 검증 JSON](CodexOrig_TASK-038_Validation.json), [독립 감사](CodexOrig2_TASK-040_Audit.md), [문서 후속](CodexOrig3_TASK-039_Network_Followup.md)에 범위를 연결한다. 게시·공개 재다운로드 결과는 별도 Deployment JSON에 기록한다.

## 검증의 한계와 재현

버전 검사는 별도 임시 프로필, 인증 환경 변수 제거 및 접속 불가능한 로컬 프록시로 실행했다. **물리적으로 인터넷을 끊은 시험이나 고객 PC 실행을 뜻하지 않는다.** 고객 VS2017 Professional 15.7.1 실설치·GUI, 최초 로그인·실제 AI 요청, 조직 방화벽 허용 상태는 확인하지 않았다. 모델 서버 접속만으로 선택한 공식 인증 경로까지 허용됐다고 단정하지 않는다.

보관한 재현은 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/compatibility/Test-OfflineCli.ps1 -OutputDirectory <새 폴더>`다. 정확한 원문은 `artifacts/task038/tests-release-final/results.json`, `full-offline-candidate4-validation.json`, `public-bundle-evidence.json`, `prepare-from-public-tools-result.json`, `real-install-candidate4.json`, `real-cli-local-smoke-candidate4.json`에 있다. 설치기 전체/서명 준비·로컬 복사·별도 CLI 실행의 증거를 서로 구분한다.

공식 근거: [Codex Windows 배포 — standalone 런타임 전체 보존](https://learn.chatgpt.com/docs/enterprise/windows-deployment), [Claude 네이티브 설치·Windows 요구사항](https://code.claude.com/docs/en/setup), [Claude 기업 네트워크 설정](https://code.claude.com/docs/en/network-config). 확인일 2026-10-19. 계정 인증의 자세한 방법은 각 공급자의 공식 안내를 따른다.

## 배포 완료 / 2026-10-19 11:35:04 +09:00ST

[v0.11.1 GitHub Release](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.11.1)에 OfflineTools1 ZIP과 체크섬 **2개를 추가 게시**했다. 도구 소스 커밋은 97d606d4b64eca25406d418c66919f7b321ab429이며 원래 태그 대상 2b63332327fd0ccdb2041f003b02ab7ecc615a84와 기존 7개 자산의 ID·크기·SHA는 그대로다. 최종 자산은 9개이며 새 2개를 인증 없는 공개 다운로드로 다시 받아 크기와 SHA256을 모두 확인했다. 네이티브 CLI payload는 공개하지 않았고 고객 전달 완성 ZIP은 위 로컬 경로에 보관했다. [배포 검증 JSON](CodexOrig_TASK-038_Deployment.json)에 실제 URL·해시를 기록했다.

시작 2026-10-19 11:00:31 KST부터 총경과 00:34:33.7554274. 구현·검사·감사 조정·문서·게시·원격 확인을 완료하고 담당 소유권을 반납한다. 고객의 실제 설치·망차단·로그인·모델 사용을 완료했다는 뜻은 아니다.
