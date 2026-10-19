# TASK-051 · Role 23:09 통합 검증과 0.12.0 미리보기 배포

CodexOrig · 시작 2026-10-19 23:40:45 KST. 공개 배포 및 13개 파일의 익명 다운로드 검증을 완료했다. 결과는 별도 Deployment JSON에 연결한다.

사용자가 지정한 CodexOrig4의 디자인 범위를 보존하고, 이미 작업 중이던 CodexCode와 중복을 정리했다. CodexCode TASK049는 완료 알림·대화 표시 페이징과 설정 모델을, CodexOrig4 TASK050은 팝업·탭·설정·Markdown·앱 팔레트·제목 표시줄을 담당했다. Root는 URL 렌더 보완, 독립 IDE 테마, SDK/버전 통합, 전체 검사·감사·문서 연결·패키지·배포를 맡았다. 기존 TASK048 모션 변경도 포함했다. 담당별 코드 완료를 전체 완료로 처리하지 않았다.

## 구현과 고정 후보

Role 23:09의 11항목과 image44–55를 대조했다. Windows NotifyIcon 완료 알림, 공통 테마형 팝업과 창 제어, 왼쪽 위 뒤로, Markdown 크기/체크박스/여섯 도구 그룹, provider 키보드 탭, 안전한 링크 굵기와 URL 번호의 정렬, 최근20/상단20 표시, 왼쪽 설정 네 페이지, 간격, 앱 전용 Kakao/Dracula와 독립 IDE 테마를 연결했다.

C6 입력124개를 해시 고정했다. VS2017 15.7 전용/15.9 이상/2019/2022 Release는 모두 경고0·오류0이다. C4→C5는 공통 caption 코드 1파일, C5→C6는 새23쪽 PDF 1파일만 바뀌었다. 최종 PDF SHA256은 `A17D4A74CD195CBAAEB63971C13A488FDDD483F98289ADCE2493CB91819A8693`; 기존18쪽 원본과 과거 배포 자산은 보존했다.

| 검사 | 결과 |
|---|---|
| 최종 C6 기본 실행기 전체 | 16/16 묶음 |
| C6 실제 IDE · 2017/2019/2022 | 각145/145 |
| C6 Legacy 패키지 · VS2017 15.9 호스트 | 145/145 |
| 대화 페이징·전체 저장 · C6 4대상 | 각10/10 |
| 최종 창 caption 독립 감사 · C6 4대상 | 각19/19 |
| C5 caption/UI · 디자인 담당 4대상 | 각각13/13·14/14 |
| Windows 합성 완료 · C6 | BalloonTipShown 1회, 중복 완료/타이머/Dispose 정리 통과 |
| 설치 선택·무결성 · 최종 4VSIX | 35/35 |
| 독립 IDE 테마 패키지 | 69/69 |
| 독립 테마 실제 설치·구성 · 3IDE 시험 프로필 | installer/config exit0, 테마 두 종류 등록, AI 확장 등록 없음 |
| 최종 완성 ZIP · 통신 없는 AppContainer | 설치·재설치·Codex/Claude 실행·전후 TCP 차단6/6 |
| 실행기 재빌드 후 자체 검사 | 15/15 |

전용 IDE 검사는 합성 소스·CLI 프로토콜을 사용했고 실제 유료 AI 계정을 호출하지 않았다. 기본 IDE 검사 결과와 고객의 VS2017 15.7.1 Professional 결과를 구분한다. Legacy는 현재 개발 PC의 15.9 호스트에서 실행했다.

## 감사와 실패 보존

CodexOrig2 TASK053에 구현 약90% 시 감사를 요청했다. R53-01은 plain URL 기능이 HTML 속성/주석 내부를 링크로 승격한 회귀였다. quote-aware 경계와 다음 줄 상태를 보완했고 독립10사례·추가18사례로 해소했다. R53-02는 image45의 녹색 OS 제목 표시줄을 본문 header 개선만으로 완료 처리할 수 없다는 지적이었다. 공통 WindowChrome caption으로 보완하고 네 대상 재감사를 통과했다. 최종 확정 미해결 지적은 없다. 감사 보고/후속 JSON의 기존 원문은 수정하지 않았다.

초기 하네스 실패(Resolver의 자동 `$args`, 바뀐 뒤로 라벨/왼쪽 페이지 기대, 숨긴 비선택 규칙 페이지 색상, owner 표시와 CommandManager 재질의)는 원본 결과를 남겼다. 사용자에게 실제로 보이는 페이지·현재 요구를 검사하도록 수정했고 검사를 제거하지 않았다. 최초 전체16은13/16이었으며 최종 C6는16/16이다.

독립 테마의 첫 자동로드 helper는 빈/startup IDE에서 결과를 받지 못했다. 실제 설치/구성은 정상 종료했으므로 후속 검사는 helper/AI 확장을 설치하지 않고 공식 VSIXInstaller와 `/UpdateConfiguration`이 만든 자기 시험 hive를 KEY_READ로 읽는 방식으로 분리했다. XML은 두 팔레트 모두80 category/2470 color이고, compiler는10개 CT_INVALID 전용 placeholder를 생략해70개 실제 category를 만든다. 세 IDE 구성에서 두 테마의70 category/80,290 bytes를 확인했다. 현재 사용자 테마와 전역 hive/권한을 변경하지 않았다. 전체 테마 전환 시각 검증은 수행하지 않았다.

배포 ZIP의 한글 PDF 파일명이 ASCII checksum에서 손상되는 초기 포장 오류도 찾아 UTF-8로 수정했다. 이전 ZIP/manifest는 `artifacts/task051/package-attempt1/`·`package-attempt2/`에 보존했다. 최종 ZIP은 내부 모든 checksum과 Unicode 파일명을 다시 검사하고 새 완성 ZIP으로 통신 차단 검사를 재실행했다.

## 배포 파일과 범위

일반 ZIP: `VisualStudio_AI_Assistant-0.12.0.zip`, SHA256 `DBFC23E38E35135B0E85943FB4BAA430B1BAE4E94D53D1CF646D56743D19D101`.

CLI 포함 ZIP: `VisualStudio_AI_Assistant-0.12.0-Preview1-FullOffline-win-x64.zip`, 310,954,283 bytes, SHA256 `140F770A8D236C1FB0475A66CB231657E9575FC48D0C5320CACE02B1A7FA682E`. 68파일 중 checksum 자신을 제외한67개 모두 일치했다. 공식 CLI46파일·라이선스/버전 metadata를 포함하고 개인 계정/대화/인증 파일을 복사하지 않았다.

독립 테마3VSIX, 확장4VSIX, 두 ZIP, 사용자 PDF와 checksum을 별도 `v0.12.0-preview.1` 사전 릴리스13자산으로 배포한다. 기존 정식 v0.11.1과 offline-test.2를 교체하거나 최신 정식으로 승격하지 않는다. 게시 후 익명 다운로드13파일과 SHA256, 태그/소스 연결을 검증하고 Deployment JSON에 기록한다.

이력은 기존100개·4MB 저장 범위의 화면 페이징이다. Windows 알림은 NotifyIcon이며 WinRT toast 활성화/클릭 복귀와 다르다. 모든 기본 IDE 테마의 전체 색상 전환, native 물리 drag/resize, 실제 Claude 계정·운영 DB/형상관리 및 별도 고객 오프라인 PC의 설치/AI 창 결과는 미검증 범위로 유지한다. 사용자에게 내려받아 시험할 파일을 제공하는 배포와 고객 PC 시험 완료를 구분한다.

상세 실행 경로·DLL/VSIX SHA는 `CodexOrig_TASK-051_Validation.json`, 감사는 `CodexOrig2_TASK-053_Followup_C5_C6.md`, 문서는 `CodexOrig3_TASK-051_Documentation.md`에 연결한다. 최종 SourceCommit·공개 주소·13개 원격 파일 검증은 `CodexOrig_TASK-051_Deployment.json`에 추가한다.


## TASK051 공개 배포·원격 검증 완료 / CodexOrig / 2026-10-20 01:06:34 +09:00
v0.12.0-preview.1, Release ID 417485280, source commit e717ccc16c9d8db06d353286a17aea133f07d428 를 공개했다. 주소: https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.12.0-preview.1 . 13개 자산을 인증 없는 새 HTTP 클라이언트로 재다운로드하여 크기·SHA256 13/13 일치를 확인했다. 기존 v0.11.1 정식 9자산/태그/최신 정식 지정 및 v0.11.1-offline-test.2의 2자산/태그가 그대로임을 API로 확인했다. 게시·원격 검증 근거는 MarkdownReport/CodexOrig_TASK-051_Deployment.json에 남겼다.
최종 4대상 Release 오류0·경고0, 기본16/16, 실제 IDE 145x4, 최종 완성 ZIP 통신 차단6/6과 공개 파일 연결을 확인했다. 고객 오프라인 PC/VS2017 15.7.1 실제 설치와 계정·운영 연동, 전체 테마 전환/물리 창 조작은 확인 전 범위로 유지한다. 시험용 배포 완료를 고객 시험 완료로 보고하지 않는다.
사용자 마지막 지시의 완료 기준(통합 빌드·컴파일 → 최신 Release 제작 → 배포 → 공개 다운로드·해시 확인)을 CodexOrig2·3·4 및 연구 담당에게 직접 안내했다. CodexCode 직접 전송은 active writer 오류로 거부되어 본 WORK_STATUS 공유 인계와 Root 검증 JSON에 같은 기준·최종 결과를 명시했다. 이번 통합·배포는 Root가 끝까지 수행했으며 중복 배포를 요청하지 않았다. 제품 고정 후보와 기존 감사 보고는 변경하지 않았다.
시작 2026-10-19 23:40:45 KST, 최종 배포 검증 기록 2026-10-20 01:06:34 +09:00, 경과 01:25:49.4697348.
