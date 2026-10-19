# TASK-040 / CodexOrig2 / 오프라인 설치 독립 감사

감사자 **CodexOrig2**. 2026-10-19 KST. 사용자 조건은 **확장·CLI 설치만 오프라인, AI 사용 시 연결 가능**이다. Role 최신 추가, TASK-038 설치 도구와 TASK-039 설명서를 대조했다.

**판정: R40-01/P2 1건을 발견했고 수정 코드와 FullOffline C3·최종 C4 및 공개 준비 도구의 정확 바이트에서 보완을 확인하여 종결한다. 이번 범위의 미해소 확정 지적은 0건이다. 실제 고객 PC의 오프라인 설치·Professional 15.7.1 실행·로그인·AI 요청 성공을 뜻하지 않는다.**

## R40-01 / P2 / manifest 파일 링크로 경계·크기 상한 우회 — 수정 확인

고정 `OfflineCli.Common.ps1` SHA256 `7E45FBE20F999469F6C65335633C4CE6E43C82CC529D4ED110CD317B6D6D8601`의 `Read-OfflineCliBundle`은 루트만 일반 로컬 경로로 검사한 뒤 `offline-manifest.json`을 직접 읽는다. manifest 파일의 reparse point는 확인하지 않는다.

독립 로컬 fixture에서 `offline-manifest.json`을 개인 감사 폴더의 외부 JSON으로 연결한 파일 SymbolicLink로 바꾸면 정상 수용한다. 외부 JSON에 공백 1,100,000개를 붙여 **1,102,000 bytes**로 만들어도 링크의 `Length=0`만 검사하여 **1MiB 상한이 우회**된다. 정상 파일 1사례는 통과했으나 기대 거부 2사례는 모두 수용됐다. 실제 UNC 주소로 접속하지 않았다. 네트워크 대상을 가리키는 링크에서도 읽기가 일어날 수 있다는 점은 코드에 근거한 추론이다.

- 재현 스크립트: [Verify-ManifestBoundary40.ps1](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task040/Verify-ManifestBoundary40.ps1).
- 최초 결과: [manifest-boundary-initial/result.json](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task040/manifest-boundary-initial/result.json). 같은 고정 코드의 일반 payload 링크 검사는 통과하므로 manifest 파일 자체의 검사 누락과 구분한다.
- 인계: 11:18:43 KST WORK_STATUS에 manifest와 준비 metadata의 파일 단위 검사를 요청했다. 준비 단계도 `codex-package.json` 외부 파일 링크를 수용하는 것을 합성 PE·게시자 대역으로 별도 재현했다. 최초 실패 3사례와 중간 보고서를 보존했다. 실제 사용자 프로필을 읽은 검사가 아니다.
- 수정: TASK-038이 파일 읽기 전 일반 로컬 경로·leaf·1MiB 상한을 확인하고 경로를 상위부터 조사하도록 보완했다. 같은 manifest 링크 2사례가 모두 거부되고 정상 파일은 수용됐다(3/3). 준비 metadata 링크는 서명 조회 0·출력 폴더 0에서 연결 경로 오류로 거부됐다.
- 종결: 검사한 수정 코드 SHA가 FullOffline C3와 공개 도구 entry에 연결됐다. C2→C3 완성 묶음 변경은 공통 스크립트·준비 시각을 담은 manifest·checksum 3파일이다. CLI 46개와 네 VSIX는 같다. **R40-01을 이 고정 후보와 검사 범위에서 종결**한다. 감사자는 제품·설치 도구를 수정하지 않았다.

## 직접 확인한 결과

| 검사 | 결과 | 범위 |
|---|---:|---|
| 고정 설치 코드의 기존 검사 재실행 | 28/28 | 개인 복제본·가짜 PE·복사/경로/무결성/기존 설치 보존 |
| 수정 후 기존 검사 재실행 | 31/31 | metadata·manifest 상한과 링크 회귀 포함, 개인 복제본·Windows PowerShell 5.1 |
| 독립 경계 수정 확인 | manifest 3/3 + metadata 거부 | 원래 실패한 링크 3사례 수정 확인, 최초 실패 원문 보존 |
| C3 코드의 금지 호출 대역 + 실행 표식 | 진단·설치·재실행 모두 정상 | `VerifiedOnly` → `Installed` → `AlreadyInstalled`, CLI 표식·금지 호출 0, 초기 실행에서 사용자 PATH 보존도 확인 |
| 가짜 프로필/게시자 대역의 준비 범위 | 프로필 표식 4개 미복사·불변 | `current` junction 1회 해석, signature 대역 5회. 실제 서명 검증 증거가 아님 |
| FullOffline C2·C3·최종 C4 ZIP | 각각 59/59 entry SHA 일치 | 각각 내부 checksum 58/58·외부 checksum 일치, CLI 파일 46, C17 VSIX 4개 동일, 준비 서명 기록 5개와 payload SHA 일치 |
| 최초/최종 공개 준비 도구 ZIP | 각각 37/37 entry SHA 일치 | 각각 내부 checksum 36/36·외부 checksum 일치, Codex·Claude CLI payload 0, 실제 ZIP README 직접 확인 |
| 보호 원본·제품 입력 | 67/67 및 122/122 | Role·기존 감사·기존 릴리스·원본 온라인 BAT/MD·C17 제품 입력의 SHA 보존 |

## 고정 바이트와 문서

개인 사본은 `C:/dydgyqud/AO40_20261019/`에 후보별로 분리했다. 최종 FullOffline C4는 **308,366,787 bytes**다. C3에서 실행 코드를 바꾸지 않고 최종 문서를 반영했다.

| 최종 대상 | SHA256 |
|---|---|
| OfflineCli.Common.ps1 | `D84D713D088360A5A104BE4A85FAE011E29DEC4FDE2F009E0906DC6F9256F650` |
| Prepare-OfflineSetup.ps1 | `099F3BCED2E94795F46C456938ADDB214A12B3DD681B754151075BBF0ECA0A84` |
| Install-OfflineCli.ps1 | `908EE57AF2174D81F2283EACBF8CB47C76457A11E00D67DC3864CB3A3301F443` |
| FullOffline candidate4.zip | `DB06BF91635D4C3F0B23F057E5CC39EAEB22714E2DC298C8AA477E142F8CA558` |
| OfflineTools1.zip 최종 공개 준비 도구 | `C1743C89604DE34B8F0BBC68416017E719C9925759AF807169AC87B16F059E83` |

보존한 최초 FullOffline C2 SHA256: `FBEE7BEB25765AE6C0974A9B3F9C5EFC9B3A9FDD13DB448A764CBA9B573132DE`.

보존한 공개 도구 최초 SHA256: `9DC30FBC31D23E70A91251898ABB47AF18BE5741CDFFBB249FEF12A96002BEA0`.

C3 설명서 SHA256 `D46B596AFCEF997C198BAC4AC15031608E975C75FF094A8FF1248E9FEF13B862`에서 네트워크 안내를 보강한 **C4 최종 문서 SHA256은 `47BABB457DCB087F4BCBCC12EB90FF6E7C48736D8230DF831E47D1A06C10B8A7`**이며 두 최종 ZIP에 일치한다. AI 모델 서버만 허용돼도 최초 계정 인증까지 성공한다고 단정하지 않고, 공급자·인증 방식에 필요한 공식 연결을 네트워크 담당자와 확인하도록 안내한다. Windows/Visual Studio 필수 구성 요소는 이 묶음이 자동 설치하지 않는다고 설명한다.

실제 ZIP README에서 공개 준비 도구와 CLI를 포함한 완성 묶음의 차이, 인터넷 준비 PC의 온라인 InstallCLI.bat와 대상 PC의 오프라인 설치 BAT를 구분했다. PS 기본은 진단 전용이고 BAT/`-Install`에서만 복사한다. VS 설정의 실행 파일 경로 지정, 연결 후 공식 로그인과 실제 AI 요청도 별도 절차로 안내한다. Windows x64·Windows 10 1809/Server 2019 이상 및 서비스 연결 필요 조건은 [Claude 공식 setup 문서](https://code.claude.com/docs/en/setup)와 대조했다. 고객 Windows 버전을 확인한 것은 아니다.

## 검사 방법과 한계

공통 모듈·설치기의 AST는 구문 오류 0, 명시적인 다운로드·프로세스/CLI 시작·서명 조회 명령과 System.Net 타입 참조 0이었다. 금지 호출 대역은 알려진 네트워크/서명/프로세스 명령을 호출하면 실패하게 했다. 실행 표식은 CLI가 실행되면 파일을 남기는 합성 PE다. C3의 세 모드에서 두 표식 모두 발생하지 않았다. 이 검사는 운영체제 전체 패킷 캡처나 물리적인 망 차단을 뜻하지 않는다.

전체 payload의 크기·SHA·필수 PE·metadata·추가 파일을 검증한 후 staging에 복사하고 각 파일 SHA를 다시 확인해 새 목적지로 이동한다. 같은 설치는 멱등이며 다른 내용·기존 로그·부모 연결·경로 이탈·UNC·추가 프로필 등은 거부하는 것을 코드/fixture에서 확인했다. 온라인 서명 조회는 준비 PC의 단계이며 대상 설치기에 없다. 준비 기록의 서명 5건과 payload SHA를 대조했지만 감사자가 별도 온라인 trust 검증을 수행한 것은 아니다.

실제 사용자 프로필·토큰·계정·기존 CLI를 읽거나 바꾸지 않았다. 프로필 검사는 합성 표식 4개와 게시자 함수 대역을 사용했다. 실제 CLI 본체 실행, 고객 PC의 망 차단·Professional 15.7.1 설치·GUI·로그인·AI 요청은 미검증이다. 초기 감사 하네스의 list scope 및 WinPS5 New-Item symlink 권한 오류는 개인 변수·테스트용 CreateSymbolicLinkW flag2로 보정했다. 하네스 실패 로그와 실제 제품의 R40-01 실패를 구분 보존했다.

근거는 [TASK-040 Evidence](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-040_Evidence.json)와 `C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task040/`에 있다. 감사자는 새 보고·fixture와 공유 작업 기록만 작성했다. 최종 공개 도구 업로드·원격 해시 및 고객 전달 파일 마감은 TASK-038이 소유한다.

## 감사 종료

시작 2026-10-19 11:16:08 +09:00, 종료 2026-10-19 11:30:51 +09:00, 경과 00:14:43. TASK040 독립 감사·보고·검증을 완료하고 담당 소유를 반납한다. R40-01 수정 확인·미해소 확정 지적 0건이며 TASK038의 최종 게시·원격 검증과 고객 전달 마감은 별도다.
