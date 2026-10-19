# TASK-042 C6 후속 / CodexOrig2

2026-10-19 KST. 완료된 TASK-042 본보고·Evidence와 C5 실패/수정 근거는 변경하지 않았다. 이번 후속은 대상 검증 도구의 경로 검사 순서 변경과 최종 C6 원본 해시를 연결한다.

**판정: C6의 독립 경계 5/5 및 묶음 무결성 검사가 통과했으며 새 확정 지적은 없다. 실제 오프라인 테스트 PC의 VSIX 설치·IDE 확인 결과가 미수집이므로 정식 추가 배포 보류는 유지한다.**

| 대상 | SHA256 | 직접 확인 |
|---|---|---|
| FullOffline C6 | `6CE8091677E29BC075D5C97BE98C6F86414537B02BC33F3DEF33A8DF108DCC79` | ZIP 61/61, 내부 checksum 60/60, 외부 checksum 일치, CLI 46·C17 VSIX 4개 |
| Tools2 C6 후보 | `B52D393C14BBB814EC3D8F955958B2099FC77E51206DA85421EE5298EEF61D66` | ZIP 39/39, 내부 checksum 38/38, 외부 checksum 일치, CLI payload 0 |
| 대상 검증 PS | `E3531A9FD1867966C6DE25C92F3DE6883962ECD19EB32191262B863DD4D3E2A2` | 개인 고정 소스와 두 ZIP entry 동일 |

`Find-OfflineInstalledExtension`에서 폴더·manifest의 `Assert-OfflinePlainPath`가 각각 `Test-Path`보다 먼저 실행됐다. 실제 함수를 호출하면서 경로 검사와 존재 조회를 기록한 독립 fixture에서 **정상 확장 발견, 폴더 검사 순서, manifest 검사 순서, 연결 manifest의 조회 전 제외, UNC 루트의 조회 전 거부 5/5**를 확인했다. 연결된 다른 확장의 manifest는 try/catch에서 제외되므로 정상 대상 검색을 중단시키지 않았다. UNC는 실제 연결하지 않았고 존재 조회 호출도 없었다.

AST와 원문 대조에서 해당 함수 외 코드가 동일했고, C5→C6 완성 묶음의 변경은 대상 PS·준비 시각이 든 manifest·checksum 3파일뿐이었다. Installer/Common/CLI·검증 BAT·안내서·네이티브 격리 하네스는 이전 감사와 같은 SHA다. 따라서 TASK-042의 회귀31·수명3/3·R42-01 종결·합성 version의 인증변수 제거 확인을 같은 바이트에 연결한다. C6에서 모든 이전 검사를 새로 실행했다고 기록하지 않는다.

설치기·Common·대상 PS·BAT의 SHA를 두 ZIP과 대조했으며, 안내서 SHA `4C387A0A5B3959184D1AF8A3868E64A3FA1986BE728B43F91BF69CAE71227255`도 유지됐다. C5·기존 릴리스·완료된 감사·Role 등 보호 원본의 SHA가 보존됐다.

이번 감사에서는 제품·원본 설치기·기존 보고·사용자 프로필·권한·계정·GUI·배포를 변경하지 않았다. 개인 사본은 `C:/dydgyqud/AO42C6_20261019/`, 독립 fixture·결과·보존 기록은 `C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task042-c6/`에 있다. 상세 [C6 Evidence](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-042_Followup_C6_Evidence.json).

실제 PC의 `RESULT.json` 및 IDE 확인은 TASK-041이 수집한다. AppContainer 결과와 사용자 오프라인 확인을 실제 고객 PC의 전체 설치 성공으로 확대하지 않으며, 이 후속을 추가 공개 배포 승인으로 해석하지 않는다.

완료: 2026-10-19 13:01:01 +09:00, 시작 2026-10-19 12:57:39 +09:00, 경과 00:03:22. C6 후속 감사·보고를 마치고 소유를 반납한다.

