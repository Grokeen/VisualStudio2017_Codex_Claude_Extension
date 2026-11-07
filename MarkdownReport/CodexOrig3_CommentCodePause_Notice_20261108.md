# C# 주석 코드 제안 임시 중지 문서 후보 · 2026-11-08

## 기준과 상태

- 문서 브랜치: `codex/comment-code-pause-docs-20261108`. 기준 문서는 소스 후보 `267a81ea849c1a9bb9858176dd3d65501fded460`의 사용자 변경을 반영합니다. 이 문서 작업에서는 소스, BestCare, 고정 태그, 공개 Release 첨부를 수정하지 않았습니다.
- 현재 공개 설치본은 `v0.15.0-preview.1`입니다. 해당 설치본은 C# 주석 코드 제안 메뉴를 자동 차단하지 않으므로 사용하지 말고, 기존 확인 창이 나오면 **아니요/취소**를 선택하라고 사용자 문서에 알립니다.
- 차기 `v0.15.1-preview.1`은 아직 공개되지 않은 소스·문서 후보입니다. 소스 후보의 전송 전 중지 안내 문구를 문서와 대조했지만, 새 패키지 빌드나 실제 Visual Studio 동작은 확인하지 않았습니다. 공개 패키지 동작으로 단정하지 않습니다.
- 임시 중지는 문서 대상 확인 보완을 위한 예방 조치입니다. 실제 편집기 문서 불일치, 원치 않는 변경 또는 정보 유출이 확인됐다고 표현하지 않습니다. 일반 대화는 이 중지 범위 밖이지만 실제 IDE·계정 동작은 검증되지 않았습니다.
- 새 PDF는 공개 `v0.15.0-preview.1`의 55쪽 사용자 안내 PDF 앞에 공지 1쪽을 붙이는 별도 검토 후보입니다. 공개 PDF나 고정 태그 파일을 대체하지 않습니다.

## 공개 안내 상태와 고정 태그

- 익명 GET으로 확인한 Release: [v0.15.0-preview.1](https://api.github.com/repos/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tags/v0.15.0-preview.1), Release ID `437695130`.
- 이전 확인에는 오래된 C# 사용 절차가 남아 있었으나, 이후 익명 GET에서 Release 본문의 C# bullet은 이미 임시 중지 상태 및 공개 설치본 자동 차단 부재를 안내하도록 바뀌어 있었습니다. 현재 Release 본문 수정은 이 문서 작업이 수행한 것이 아닙니다.
- 최신 Release 본문은 고정 태그의 `docs/OFFLINE_INSTALL.md`, `docs/USER_GUIDE.md`를 계속 링크하며 해당 태그의 문서와 55쪽 PDF에는 이전 절차가 남아 있습니다. 이 브랜치의 README·안내서·후보 PDF에서 그 오래된 단계를 따르지 말라고 표시했습니다. 고정 태그와 Release 첨부는 변경하지 않았습니다.
- v0.15.1 문서 후보와 PDF는 미공개 상태로 표시합니다. 사용자에게 공개 0.15.1 설치 파일이 있다고 안내하지 않습니다.
- CodexOrig6이 이번 변경에서 새로 생긴 문제는 아닌 README의 오래된 C8 Markdown 안내 링크 두 개가 현재 Git 대상 없이 남아 있다고 알렸습니다. C8 파일/ PDF가 이 checkout에 없어, 확인 가능한 C7 이전 검토판 `.md`와 PDF로 링크를 교체하고 “이전 기능 기준, 최신 README 우선”이라고 표시했습니다.
- CodexOrig6이 README·USER_GUIDE·OFFLINE_INSTALL의 역사 섹션에서 현재 설치를 v0.14.2로 연결하던 표현을 발견했습니다. 기록 내용은 보존하고 “당시 기능/기준”으로 표시한 뒤 현재 공개 v0.15.0 Release 링크로 교체했습니다. PDF 첫 페이지도 본문 속 “현재 v0.14.2”가 역사 문구이고 현재 공개본은 v0.15.0임을 보충해 오래된 본문과의 충돌을 줄였습니다.

## 오프라인 설치 안내의 안전 보완

### 최신 후속 소스 고정 상태 (2026-11-08)

- 앞의 초기/중간 진행 메모는 시점별 기록입니다. 현재 확인한 오프라인 후속 소스는 별도 브랜치 `codex/offline-required-vsix-20261108`의 clean commit `48a24197801cdb880caa4ad62151069967ec1052`, tree `5b0d319cb60cede9148e4c8d3dbe9d9b49337482`, parent `76971cdde9e9b5a844ed1bb6857d30f39ea1e1fd`입니다. 이전의 “7개 VSIX 목록/경로 검사 보완 진행 중” 상태는 이 고정 커밋에 대해 완료로 대체합니다.
- 고정 소스의 `VerifyOfflineSetup.bat`은 `Verify-OfflineFiles.ps1`만 호출합니다. 4개 AI VSIX·3개 테마 VSIX, 체크섬의 양방향 대응, 필수 항목·버전·누락·중복을 검사합니다. `Install-AIAssistant.ps1`은 평면/`Themes/` 구조를 처리합니다. 패키지 경로, 조상, 체크섬 및 선택된 제품/테마 VSIX에 대한 UNC·junction·심볼릭 링크 거부 검사도 있습니다. 테마 설치를 선택하지 않은 경우 Themes 경로는 요구하지 않습니다.
- 일반 드라이브 문자 경로로 패키지를 풀어 사용하도록 안내합니다. 드라이브 문자가 mapped network drive일 수 있으므로 네트워크 격리 증명으로 표현하지 않습니다. 검사와 실제 사용 사이에 경로가 바뀌는 경쟁 조건을 막는다고도 주장하지 않습니다. 이는 해당 커밋 소스의 경계 검사 설명이지, 신뢰 서명이나 실제 설치 성공 보증이 아닙니다.
- `48a2419`까지의 시험 수치와 결과 receipt는 이 문서 작업에서 독립 검토하지 않아 여기서는 PASS 수를 단정하지 않습니다. `76971` 당시 Root에서 관찰한 PS5 오류는 변경 중 미커밋 테스트와 관련됐다는 출하 담당자 정정이 있어, 고정 ASCII 테스트 원본 실패로 보았던 초기 해석은 철회합니다.
- 해당 개발 source commit 안의 기본 버전은 `0.15.0`이었습니다. 이후 별도 출하 준비 브랜치 `codex/public-hotfix-shipping-0151`의 clean HEAD `56141ec258da04d790eea8cd6b8741b8b1e31524` (tree `b7f2dfa284ecc2e3e9b9875667255fb74268b64d`, parent `0a3bc2b6151d52aef3fee477a59ce962b0a9f53c`)에서 배치·검증·설치 스크립트를 0.15.1로 동기화했습니다. 로컬 `releases/`에는 아직 `v0.15.1` 완성 ZIP이 없습니다. 변경된 `Install-AIAssistant.ps1`을 EXE에 포함하고 FullOffline/간단 ZIP의 실제 파일 구성을 확인한 최종 패키지 검증도 남아 있어, 공개 가능한 0.15.1 패키지라고 안내하지 않습니다. 출하 계약은 verifier·CLI manifest는 FullOffline에만, `BUNDLE_SHA256SUMS.txt`는 간단 ZIP에 두는 것입니다.
- 후속 감사가 지적한 경량 준비 묶음의 검증 템플릿 경로 불일치와 PS5 직접 스크립트 호출 경로 문제는 별도 root 수정 소스 `OfflineTemplateFallback_20261108`, commit `8f8c320be4012d21f7d5da3db531406db7ad45ae` (tree `899c347ef696e175c0f7dc65ef175bb12515e7ce`, parent `22108fc8ee2ff2404bdd3ae24517e353e89e2476`)에 보완됐습니다. 현 출하 준비 branch는 clean HEAD `7f3f51eacbf607099f60074539061f341523e11d` (tree `de8e96cb34ae62d7f683c9a492a4df5c12ba8bf9`, parent `40b542ba87e57fe934fd203873b9934dd1303933`)입니다. `Prepare-OfflineSetup.ps1`/`Verify-OfflineFiles.ps1`에서 루트 쌍이 완전하면 우선 사용하고 없으면 `offline-templates/` 쌍과 정확히 두 항목의 checksum 목록을 확인해 최종 FullOffline 루트에 놓는 지원이 커밋됐습니다. 로컬 `releases/v0.15.1` ZIP은 여전히 없습니다. 따라서 코드 지원과 실제 준비·배포 archive 검증은 구분합니다.
- 별도 합성 영수증 `C:/Users/ygree/AppData/Local/Temp/codex-lightprepare-evidence-1abab919c35b4681a7a2fd40ec5a7849/result.json` SHA-256 `53A25283EE8E2CBD816088D8044E1943F8BA143BC618962E0B9430C8E659F814`는 20개 기록에 기대 종료 코드 불일치 0건을 담고 있습니다. Windows PowerShell 5.1·PowerShell 7.6.9, 바깥 sentinel 불변, verifier template 없는 경량 입력 및 checksum/junction/link/error fixture를 포함합니다. JSON은 publisher/Claude version stub 사용, 실제 installer·CLI·network 실행 없음으로 표시합니다. 이 시험은 가짜 CLI·설치기와 서명/실행파일 구조 검사의 대체 입력을 사용했으며 공식 서명·x64 구조 검증, 최종 준비 ZIP, 실제 설치 성공을 증명하지 않습니다.
- 출하 계약은 FullOffline 루트에 실행 검증기와 CLI manifest를 두고, 간단 ZIP 루트에는 실행 검증기/CLI manifest를 제외하는 것입니다. 준비 도구 입력 ZIP은 `offline-templates/` 아래에 검증기 템플릿을 둘 수 있습니다. `BUNDLE_SHA256SUMS.txt`는 ZIP 내부 파일 해시 목록으로만 설명하고, 사용자가 실행할 검증 방법은 별도 출하 문서로 검증·추가해야 합니다. ZIP 자체 해시는 공개 Release의 외부 checksum 자료를 사용합니다. 실제 배포 ZIP의 archive entries·파일 해시·패키지 준비 흐름을 확인하기 전까지는 계약 구현 완료로 단정하지 않습니다.
- 공개 v0.15.0 ZIP 자체 해시 확인 방법도 구분합니다. FullOffline ZIP은 `.zip.sha256` sidecar, 간단 ZIP은 `RELEASE_SHA256SUMS.txt`의 간단 ZIP 행을 사용합니다. 두 ZIP 내부의 `VerifyOfflineSetup.bat`은 checksum-only 도구가 아니며 `-Install`을 실행합니다. `BUNDLE_SHA256SUMS.txt`는 ZIP 내부 payload checksum 목록이지 ZIP 외부 archive hash가 아닙니다.
- 공개 v0.15.0 간단 ZIP의 중앙 디렉터리에는 `VerifyOfflineSetup.bat`과 `BUNDLE_SHA256SUMS.txt`가 함께 있습니다. 읽은 BAT 본문은 `Test-OfflineSetupOnTarget.ps1 -Install`을 호출하므로 체크섬만 확인하려고 실행하면 안 됩니다. FullOffline ZIP도 `VerifyOfflineSetup.bat`을 포함하며 설치를 수행할 수 있습니다.
- 2026-11-08 04:44 KST 익명 GET 영수증 `artifacts/codexorig8/public-release-safety/theme-path-warning-after.json`은 `Passed=true`, 공개 v0.15.0 Release 본문 SHA-256 `8E62B7565461B497716348BC2CCBB1414A68A6D76016CA5BB64B991D742E01E5`를 기록합니다. 테마 경로 조건부 경고가 공개 본문에 추가됐습니다. Release 편집은 출하 담당자가 했으며 이 문서 브랜치의 작업이 아닙니다.
- 이 문서 작업에서는 오프라인 합성 시험을 실행하거나 결과 receipt를 독립 감사하지 않았습니다. 소스 파일과 출하 브랜치 기록은 읽기 전용으로 확인했으며, 실제 설치·Visual Studio IDE·CLI 실행·네트워크 격리는 확인하지 않았습니다.

### 최종 문서 후보 시점의 출하 상태 재확인

- 2026-11-08 최종 문서 검토 시 출하 준비 worktree `C:/dydgyqud/TA075_PublicHotfixShip_01`은 `codex/public-hotfix-shipping-0151`의 clean HEAD `eca0fefcde52b28571661f8c50a08df6ff6f99a1` (tree `c634037354eb728ea37d33a052945089c1a829e7`, parent `a39b963374af22875ffba70e1a64f2c18d1c8df5`)였고 `releases/v0.15.1`은 없었습니다. 이 확인은 파일·git 상태 조회뿐이며 패키지 생성, 빌드, 설치 또는 IDE 시험은 하지 않았습니다.
- 이 문서 브랜치의 README와 설치 안내는 공개 v0.15.0의 구형 `VerifyOfflineSetup.bat`을 checksum 전용 도구로 실행하지 말고, 실제 설치는 FullOffline 설치 절차를 따르도록 구분합니다. 간단 ZIP과 FullOffline ZIP의 외부 archive 해시 자료도 서로 다르게 설명합니다. `BUNDLE_SHA256SUMS.txt`에 대한 사용자 실행 검증 명령은 확정된 것으로 안내하지 않습니다.
- 추가 감사 문구를 반영했습니다. 간단 ZIP의 과거 `VerifyOfflineSetup.bat`은 실행하지 말고, FullOffline의 같은 이름 배치는 checksum 확인에 쓰지 말고 실제 설치 시에만 사용하도록 분리했습니다. v0.14.1은 2026-11-08 익명 Release API에서 자산 0개였다는 기록을 남기고 ZIP 다운로드 링크를 제거했습니다. 준비 ZIP의 `offline-templates/`는 원본을 이동한다고 하지 않고, 템플릿을 읽어 버전을 적용한 새 검증 파일을 FullOffline 출력 루트에 생성한다고 기술합니다.
- 공개 v0.15.0 직접 PowerShell 5.1 진단의 `-BundleDirectory` 생략 경로 문제는 문서에 한계로만 적었습니다. 합성 시험에서 인자 전달만 관찰됐고 실제 공통 모듈/진단 성공은 검증되지 않아 이를 사용자 해결 명령이나 검증된 우회법으로 제시하지 않습니다. 설치 없는 진단도 결과 로그/JSON 생성 및 레지스트리 읽기가 있어 무부작용·checksum 전용이라고 표현하지 않습니다.
- 출하 노트는 `MarkdownReport/ReleaseNotes_v0.15.1-preview.1_Candidate.md`로 별도 후보 문서에 작성했습니다. release 디렉터리나 태그는 만들지 않았고, 0.15.1 패키지 부재와 실제 IDE 미검증을 문서에 표시했습니다.

- 공개 `v0.15.0` FullOffline 묶음의 `VerifyOfflineSetup.bat`은 이름과 달리 설치 작업을 수행합니다. 소스의 동작을 확인했으며 이 배치에는 `Test-OfflineSetupOnTarget.ps1 -Install`이 연결됩니다.
- 고정된 오프라인 수정 소스: 별도 브랜치 `codex/comment-preview-pause-20261108`, commit `010f0f03f5d2e273ad6fee219821575e6d5bf6ae`, tree `871e2d94288bee4931381dede9b176d722440202`, parent `267a81ea849c1a9bb9858176dd3d65501fded460`. 읽기 전용으로 확인한 `VerifyOfflineSetup.bat`은 `scripts/Verify-OfflineFiles.ps1`만 실행합니다. verifier는 `OFFLINE_SHA256SUMS.txt`, 파일 목록, offline CLI manifest, 해시와 예상 항목을 확인하며 설치 스크립트·CLI·Visual Studio를 호출하지 않습니다.
- 후속 출하 준비 commit `d0c643b3117244b5ab1c4b4c7ca9d5403745e7ce` (tree `46df64444fac6864c09183daecb94463d78846ce`)에서 `InstallAIAssistant.bat`의 버전 선택은 `0.15.1`로 갱신됐습니다. 다만 이 커밋의 verifier는 필요한 모든 VSIX 항목을 대조하는 보완 및 최종 ZIP 검증을 기다리고 있으므로 배포 완료나 검증된 사용자 패키지로 취급하지 않습니다.
- 위 항목은 `010f0f0` 및 `d0c643b` 시점의 기록입니다. 필수 7개 VSIX와 checksum 대조는 최신 `48a2419` 커밋에서 소스 수준으로 보완됐습니다. 최종 0.15.1 ZIP의 검증은 여전히 남았습니다.
- `Verify-OfflineFiles.ps1`의 드라이브 문자 경로 형식 검사로는 물리 로컬 저장소나 실제 네트워크 차단을 보증할 수 없습니다(매핑된 네트워크 드라이브일 수도 있음). 문서의 “파일 무결성 검사” 표현은 네트워크 격리 확인과 분리했습니다. ZIP 외부의 Release sidecar SHA 대조는 ZIP 바이트와 공개값의 일치 확인이며 같은 배포 출처의 독립 인증으로 과장하지 않습니다.
- 실제 v0.15.0 FullOffline ZIP을 추출하지 않고 ZIP 중앙 디렉터리와 포함 스크립트를 읽었습니다. 71개 항목 중 테마 VSIX 3개와 관련 파일은 `Themes/`에 있고, 포함 PowerShell의 선택 테마 flags는 루트의 VSIX와 `SHA256SUMS.txt`를 확인합니다. 옵션과 해당 IDE SKU 조건에 따라 `InvalidThemePackage`를 반환할 수 있다는 정적 불일치를 기록했습니다. 일반 `InstallAIAssistant.bat`은 테마 flags 없이 호출됩니다. 기본 설치·별도 테마 EXE·직접 VSIX 설치의 성공/실패는 실행하지 않아 판정하지 않았습니다.
- 평면 Prepared 및 중첩 FullOffline 테마 경로 검사도 최신 `48a2419`에 반영됐습니다. 다만 설치기 EXE 재생성, 0.15.1 기본값 동기화와 final ZIP 검증은 아래 최신 상태에 적은 대로 미완료입니다.
- 이전 `010f0f0` 개발 커밋의 `InstallAIAssistant.bat` 기본값은 `0.15.0`이었고 `-PackageVersion`을 PowerShell에 전달했습니다. 후속 `d0c643b`에서는 선택 버전을 `0.15.1`로 맞췄습니다. 이 출하 준비 브랜치도 아직 최종 패키지가 아닙니다.
- 소스 담당자가 보고한 검증 범위: verifier 합성 조건 3개와 inventory/배치 분기 테스트가 PowerShell 7·5에서 통과했고 설치 실행은 0회라고 전달받았습니다. 이 결과는 후속 필수 VSIX inventory 보완 이전의 실행이며, 본 문서 작업에서는 그 테스트를 재실행하지 않았습니다. 소스 시험결과를 독립 감사한 결과로 표시하지 않습니다.
- 설치 흐름은 사전 검사, 오프라인 대상 여부에 대한 사용자 확인, 실행 중 Visual Studio 종료 요구, 설치 대상 선택(필요한 경우), CLI 설치 및 버전 확인, 공식 VSIX 설치기 실행을 포함합니다. 로그인을 하거나 AI 모델 요청을 보내지는 않습니다. 오프라인 여부 프롬프트는 사용자 진술이며 네트워크 차단을 증명하거나 설정하지 않습니다.
- 버전별 동작을 분리해 안내합니다. 공개 v0.15.0 배치는 checksum 전용이나 읽기 전용이 아니므로 실행하지 말고, ZIP 자체의 해시는 PowerShell `Get-FileHash` 결과를 같은 Release의 `.zip.sha256` sidecar와 비교합니다. 이 비교는 ZIP 바이트만 확인하며 내부 파일 목록 전체를 검증하지 않습니다. 최신 0.15.1 소스는 읽기 전용 verifier와 전체 VSIX inventory를 포함하지만 기본값은 0.15.0이며, 완성 패키지의 버전 동기화·EXE 재생성·검증이 미완료입니다.
- 추가 설치 경계(로컬 공개 v0.15.0 FullOffline ZIP을 파일로만 열어 확인): ZIP에는 `Themes/` 하위의 3개 테마 VSIX와 하위 체크섬이 있지만 동봉 `Install-AIAssistant.ps1`의 `Read-AIAssistantThemePackage`는 전달된 패키지 루트에서 해당 VSIX·`SHA256SUMS.txt`를 찾습니다. SKU 사전 조건을 통과해 `-InstallTheme` 또는 `-RequireTheme` 옵션을 사용하면 `InvalidThemePackage`로 끝날 수 있습니다. 일반 `InstallAIAssistant.bat`은 그 옵션을 지정하지 않습니다. 옵션 없는 설치, 별도 theme EXE, 직접 VSIX 설치가 실패한다고 확인하지 않았고 실행/GUI 설치는 하지 않았습니다. 문서에서 옵션 사용을 권하지 않고 이 범위만 조건부 주의로 설명합니다.
- 위 테마 경로 검사 진행 중 문구는 과거 상태이며 최신 `48a2419` 소스 후보에 반영됐습니다. 0.15.1 통합 패키지가 확정되기 전에는 그 동작을 공개 설치 패키지의 기능으로 안내하지 않습니다.
- 설치 절차에서는 보존할 사용자 데이터를 먼저 백업하고 `%LOCALAPPDATA%\VS2017AIAssistant`를 삭제하지 않도록 주의시킵니다. 설치가 목적일 때에만 배치를 실행하고, Visual Studio를 닫은 뒤 대상 선택과 설치 안내를 따르도록 명시합니다. `-Install` 없는 직접 진단은 설치를 수행하지 않지만 Release 전체의 검증으로 과장하지 않습니다.
- 익명 Release 확인에서 FullOffline ZIP과 동일 이름의 `.zip.sha256` sidecar가 게시된 것을 확인했습니다. ZIP SHA-256은 `a221e0753ddce6daf88b14ecaab4d3e24dda9212a47a51a1a3e330722f95e2a8`입니다. 이 문서 작업에서는 오프라인 배치나 설치를 실행하지 않았습니다.

## PDF 후보와 검증

- 후보 위치: `output/pdf/VisualStudio_AI_Assistant_설치_사용설명서_v0.15.1-preview.1_comment-code-pause-notice-candidate.pdf`.
- 첫 페이지는 현재 공개 v0.15.0이 C# 요청을 자동 차단하지 않는다는 점, 미공개 v0.15.1 소스 후보의 전송 전 중지 코드, 오프라인 검사 BAT의 버전별 역할과 아직 남은 inventory/패키지 검증, 파일 검사가 네트워크 차단을 증명하지 않는다는 점, 실제 IDE 미검증, 중지 범위와 오래된 뒤쪽 절차보다 공지를 우선할 점을 구분해 안내합니다.
- 기존 v0.15.0의 55쪽 콘텐츠 스트림은 수정하지 않고 첫 페이지를 앞에 붙입니다. 후보는 56쪽입니다. PDF 추출에서 공지의 고유 제목·본문 anchor가 각각 한 번만 나타나는지, 뒤쪽 55페이지 콘텐츠 스트림이 보존됐는지 확인하고 첫 페이지를 Poppler로 렌더링해 시각 점검합니다.
- 앞서 만든 D266 PDF는 공지 텍스트가 추출상 중복되어 폐기했습니다. CodexOrig6의 첫 독립 검토에서 이를 발견했고 생성기의 측정 단계에서 그리기를 끄도록 수정했습니다. 실제 스크린리더 동작 및 전체 PDF 리소스/주석 동등성은 확인하지 않았습니다.

## SHA-256 · 문서 후보

PDF는 56쪽이며 공개 v0.15.0 55쪽의 콘텐츠 스트림 55/55를 보존합니다. 생성기를 두 번 실행해 같은 바이트 결과를 얻었습니다. 각 첫 페이지 문구 anchor 13개는 텍스트 추출에서 정확히 한 번 나타납니다. 첫 페이지는 Poppler 140dpi 렌더링으로 점검했고 텍스트/패널 겹침과 잘림은 보이지 않았습니다.

- PDF `output/pdf/VisualStudio_AI_Assistant_설치_사용설명서_v0.15.1-preview.1_comment-code-pause-notice-candidate.pdf`: 3,340,751 bytes · SHA-256 `b0d261ad6fb3b9d50b57bb05242ab39483e3d8c05e8b99446ba0c2bef1e36483`
- `README.md`: `a3f2db5a5cbf104230c71de27d2e60668404e6e11895c01f8c4cb2997296effd`
- `docs/USER_GUIDE.md` (변경 없음): `cf5363c9827ec13adb28ef2739fcc759a7a2c130b042a33ee6cca3823cea4952`
- `docs/OFFLINE_INSTALL.md`: `4adb0bd7ef2a0a98c63734878e514887f7bb9d145ba34b7d1120ea8cd841d274`
- `MarkdownReport/Build_CommentCodePauseNotice.py`: `f403c9a63a73fb05d779d0b16273103d8a6bdf80f5a119d98fca710d770fef36`
- `MarkdownReport/ReleaseNotes_v0.15.1-preview.1_Candidate.md`: `4d3617f606d7af3355b218400d7910b64ba6903c2b0256667b655ce8610cf241`
- 현재 문서 브랜치의 수정은 README, OFFLINE_INSTALL, 이 공지 생성기, 검토 PDF 후보, 0.15.1 출하 노트 후보 및 이 보고서에 한정됩니다. 제품 소스·테스트·Release 파일은 이 작업에서 수정하지 않았습니다. 문서 commit/tree는 최종 인계에 기록합니다.

## 확인 경계 및 후속 검토

- 문서 후보 소스의 중지 안내 문구를 정적으로 대조했습니다. 소스 빌드·테스트, Visual Studio GUI, 실제 IDE 동작, 실제 AI 전송, 고객 PC 설치 또는 데이터 불일치/유출 재현은 수행하지 않았습니다.
- Release 본문은 읽기 전용 익명 GET으로 확인했으며 공개 Release·태그·첨부는 변경하지 않았습니다. 0.15.1 패키지는 아직 없습니다.
- 이 보고서와 문서 후보를 Orig2·Orig6에 최종 독립 검토 요청하고, CodexOrig8 및 소스 담당 CodexOrig에 0.15.1 전환 내용과 오프라인 설치 배치 주의를 전달합니다. 검토 응답을 받기 전에는 이를 공개 승인으로 표현하지 않습니다.
