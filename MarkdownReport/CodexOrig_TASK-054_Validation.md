# TASK-054 / Role 2026-10-20 01:40 통합 검증·배포

담당 CodexOrig. 시작 2026-10-20 02:23:11.405 +09:00. 최종 빌드·설치·감사 후 v0.12.1-preview.1을 공개했고 2026-10-20 04:20:12 +09:00에 13개 자산 익명 다운로드·해시 검증까지 완료했다. 실제 공개 근거는 `CodexOrig_TASK-054_Deployment.json`에 기록했다.

## 요청·협업 범위

Role.md의 이미지 56–66과 #1–8/#11–16을 확인했다. CodexOrig4는 팝업·버튼·Markdown·여백·탭·사용자 말풍선, 보조 TASK055는 입력 FIFO·전환 성능, TASK060은 로그인 없는 Claude, TASK061은 새 파일 저장, CodexCode TASK063과 후속 TASK064는 독립 IDE 테마 설치·적용을 맡았다. CodexOrig6/Orig5 연구는 hover 동작과 VS 설정 형식·벤더 기본값을 조사했고, CodexOrig3가 사용자 문서와 PDF, CodexOrig2가 독립 감사했다. Root는 소스 통합, 부채 메뉴·OS 알림, 네 SKU 빌드·검증·패키징·공개를 담당한다.

사용자 선택 **“이번에는 로그인 없이 검사해줘”**에 따라 Claude 로그인과 실제 AI 답변 호출은 검사하지 않았다. 계정·토큰·인증 프로필을 패키지에 복사하지 않았다.

| Role 항목 | 반영 내용 | 근거 |
|---|---|---|
| #1–4 | 팝업 경계·음영, 공통 버튼/체크박스, Markdown·안내 하단 여백 축소 | TASK056 보고 및 합성 WPF 렌더 |
| #5–6 | 제공자 탭 간격 축소, 바뀌지 않은 대화의 파일 쓰기·목록 재구성 생략 | TASK055·TASK058 성능 검사 |
| #7 | 실행 중 입력·전송 활성, 실행 1건+대기 최대 8건 FIFO, 제출 시 문맥 고정 | 실제 DLL FIFO 58/58 |
| #8 | 브랜드 아이콘·고정 결과 유형·보고된 완료 건수의 OS 알림 | 개인정보/유형 검사, 실제 Shell 알림 콜백 |
| #11–12 | 부채 안쪽 반경 44, 구역 대비, hover 220ms/이탈 유예 280ms, easing·순차 모션 | Fan·Tooltip 실제 WPF 검사 |
| #13 | 답변 전체/단일 코드 블록을 새 MD/TXT/HTML/DOCX/코드 파일로 저장 | Writer 및 실제 메뉴 검사 |
| #14 | Claude 버전/help/비인증 초기화, 합성 프로토콜·취소·오류 | TASK060 보고 |
| #15 | 사용자 요청은 오른쪽 회색 둥근 말풍선, AI 답변은 본문 | TASK056 및 UI 회귀 |
| #16 | 설정에서 동봉 독립 VS 테마 설치 관리자 호출, 재시작 후 선택 테마 적용 | TASK063/064 실제 전용 IDE 프로필 검사 |

미승인 수정안이 있으면 다음 CLI 실행을 멈춘다. 승인·적용 또는 **현재 수정안 건너뛰기**를 명시적으로 선택해야 한다. 적용으로 원본이 달라진 기존 대기 요청은 보호 검사에서 중단될 수 있으며, 재시작 후 대기 요청을 자동 실행하지 않는다. 새 파일 저장은 기존 파일을 덮어쓰지 않으며 PDF/XLSX 자동 생성은 포함하지 않는다. DOCX는 OpenXML 원문 중심 문서이고 Word/LibreOffice 시각 렌더는 미검증이다.

## 고정 입력·빌드

최종 `artifacts/task054/candidate12`는 입력 133개를 SHA-256으로 복사·동결했다. C9→C10은 사용자 PDF 한 파일만 바뀌었고, C10→C11은 Fan.cs 클래스 설명 주석 한 줄의 들여쓰기만 정렬했다. C11→C12는 PDF의 실제 파일명을 서비스 경로에 맞추는 alias와 네 프로젝트의 Content 경로만 변경했다. C#/XAML/manifest 입력은 C11과 같다(`candidate9-to10.json`, `candidate10-to11.json`, `candidate11-to12.json`). VS2017 Legacy/2017/2019/2022 Release 네 빌드 모두 오류 0·경고 0, 빌드 중 작업 입력 불변 검사를 통과했다. 최종 DLL 직접 실행과 앞선 후보의 검증 근거는 구분해 기록한다.

사용자 PDF 및 영문 Asset 별칭은 26쪽, 1,406,441바이트, SHA-256 `F853789CE823A1EABE8B4E98226064A9E86F808389CA77033DF83925744F2372`로 동일하다. 네 VSIX 내부 PDF도 동일 바이트다. PDF 19쪽의 공개 상태는 문서 제작 시점 기준이며 예정 tag·파일명은 이번 배포와 일치한다. 구버전 ZIP 다운로드를 지시하거나 구버전 URL을 포함하지 않는다. 기존 18쪽 고객 PDF와 v0.12.0 PDF는 보존했다. 그림은 합성 WPF 렌더이며 실제 바탕화면 캡처로 표현하지 않았다.

## 실행 검증

- C9 기본 검사기 **19/19** 종료코드 0: `artifacts/task054/candidate9-default-suites.json`, `artifacts/tl/729d5c37e8`.
- 해당 실행의 FIFO **58/58**, editing **92/92**, experience **152개**, 파일 메뉴 **68/68**, 알림 내용 **30/30**. 중첩 검사를 합산해 독립 커버리지 수로 주장하지 않는다.
- C9 네 DLL의 테마 XML/UTF-8 상한 경계 **112/112**, 팬·툴팁 **36/36**.
- C9 Codex x64·Claude x86 실제 Shell 알림 각 1회 표시 콜백, 중복 완료 시 아이콘 유지, 시간 만료·Dispose 정리 확인. 합성 요청이며 실제 IDE의 live AI 완료나 알림 픽셀 검증은 아니다. OS 알림 설정을 바꾸지 않았다.
- 최종 C12 v0.12.1 네 VSIX로 설치 하한·에디션·아키텍처·체크섬·오류 경계 **35/35** (`artifacts/task054/installer-final4`). 이는 고객 PC 실제 설치 성공을 대신하지 않는다.
- C12 최종 FullOffline ZIP으로 capability 0 AppContainer 토큰의 TCP 거부·CLI 설치·재설치·버전 시작 **6/6** (`artifacts/task041/run-a51ada1a8fad40519478b22d6cb0a7fb`). 네트워크·방화벽 설정 변경 0, ACL 및 전용 프로필 정리 확인. 이 토큰에서 VSIX 설치기 실행은 검증하지 않았다. 앞선 C10/C11 결과도 별도 원본으로 보존했다.

앞선 C11의 실제 IDE 네 SKU 검사는 각각 **145/145**, 총 **580/580**이었다. 설명서 호출 검사를 추가한 최종 C12는 각각 **146/146**, 총 **584/584** 통과했다. 실행·설치 VSIX 해시·PDF 서비스 반환 경로·정리 근거는 `artifacts/task054/candidate12-ide1-summary.json`과 각 run의 `installed-guide.json`이다. 실제 호스트는 VS2017 15.9/VS2019 16.11/VS2022 17.14다. Legacy 15.7용 패키지의 실행도 개발 PC의 15.9에서 확인한 것이다. GUI는 소유한 테스트 창·전용 RootSuffix로만 사용하며 기존 사용자 IDE는 종료하지 않았다. 별도 실제 테마 적용 검사는 TASK064 C11 보고로 구분한다.

배포 전 추가 검사에서 R58-05 PDF 파일명 결함을 발견했다. C11은 bin의 Link 경로에는 base PDF가 있지만 실제 VSIX·설치 폴더에는 버전 접미사가 있는 파일명만 들어 있었다. 실제 설치 DLL의 `CliGuideService.GetPdfPath()`를 새 프로세스에서 호출해 FileNotFoundException·종료 코드 1을 재현했다. C12에서는 물리 파일명 자체가 고정 경로와 같도록 동일 바이트의 PDF alias를 Content에 사용했다. 새 패키징 검사는 올바른 PDF 바이트라도 잘못된 파일명이면 거부하며 합성 준비 검사 **10/10**을 통과했다. TASK066의 C12 네 패키지 사본은 새 프로세스 서비스 호출 **4/4** 성공이다. 실제 C12 IDE 검사에는 설치된 서비스의 반환 경로·선택 VSIX의 PDF SHA를 확인하는 GUIDE-01을 추가했다. 원본 실패와 복사본 검증·실제 IDE 검증의 범위를 구분한다.

C11 실제 테마 3개 IDE의 원본 결과는 **77/81**이다. 직렬화 행 집합 비교 실패 4건을 유지하고, 두 테마 GUID·대표색·pending 소비·임시 정리·malformed 복구는 모두 통과했다. 2017 False/2019 Zoom=0의 비기본값 보존은 **4/4**, 기존 비테마 값 변경·삭제는 **0**, 추가 행은 2017/2019/2022 각각 6/9/0개다. C12는 테마 실행 소스를 바꾸지 않았으며 C11의 실제 테마 결과를 C12 DLL 직접 실행으로 표기하지 않는다. 근거: `CodexOrig_TASK-064_C11.md`와 `CodexOrig_TASK-064_C11_Validation.json`.

## 감사 발견·원본 보존

- R58-01: 요청 제목·원문이 OS 알림에 들어가던 경로 제거.
- R58-02: 답변·수정안·파일·실행/조회 유형을 신뢰한 완료 상태와 고정 문구로 구분.
- R58-03: 테마 import XML의 Environment_Group 계층·비동기 완료·임시 파일 수명·pending 처리를 보완. C9 실제 두 IDE에서 비기본 AutoUpdateTags=False/DefaultZoomSetting=0 보존 **4/4**, 기존 비테마 값 변경·삭제 **0**. 기본 직렬화 추가 행으로 raw export 완전 일치 실패는 유지한다. C8 원본 **71/75**, C9 원본 **50/54**를 모두 통과로 재분류하지 않았다. 설치된 벤더 기본값·별도 전후 분석 근거를 TASK064/Orig5 보고에서 확인할 수 있다.
- R58-04: 대기 0/1에서 원래 CLI 실패를 일반 취소 안내로 덮던 결함 수정. 원본 **0/2**와 수정 후 **2/2**를 보존하고 FIFO 기본 58개에 포함했다.

C10 실제 IDE 하네스의 옛 계약 실패도 보존한다. `run-8b1b04cd30e24fb1905292e9f0665ac1`은 '요청 실패:' 접두사 가정, `run-b3061d3897d441748d55f656631d525f`는 Root가 뒤단 오류 문구를 잘못 지정한 검사 실패, `run-b9a501f51eaf4b369878a5fab705f72a`는 제출 전 거절을 대화 system 행으로 세던 이전 >=3개 가정이다. `run-b87aeff4dc8d4d658c345e7cf437aaf1`은 Root가 첨부 문구를 누락한 검사의 실패이며, `run-687bb77b4f9c4770aea832417c7327e3`은 옛 즉시 hover 열림 조건에서 실패했다. 정확한 프로젝트 사전검사 안내·초안 보존·Stop 비활성, 실제 제출한 user/첨부와 assistant 역할/본문, 합성 hover의 220ms 예약·명시 열림 1회를 확인하도록 하네스를 강화했다. 완료 대기는 busy·draining·Stop 종료를 모두 확인한다. 제품 실행 코드는 바꾸지 않았다. 각 전용 IDE 정상 종료 기록을 보존한다.

## 패키징·공개 경계

`Prepare-ReleasePackages.ps1`은 네 AI VSIX·세 데이터 전용 테마 VSIX·최종 PDF를 검사했고, 각 AI VSIX에는 해당 IDE용 테마 VSIX가 정확히 하나 포함됐다. 작은 ZIP 23개 항목/22개 체크섬, 전체 ZIP 70개 항목/69개 체크섬을 모두 다시 읽어 확인했다. CLI는 이전 동결 묶음에서 manifest의 46개 파일만 SHA·크기·고정 서명 신원을 대조해 재사용했다. 사용자 프로필·인증 복사와 준비 중 CLI 실행은 없다. 현재 온라인 인증서 체인을 새로 검증했다고 주장하지 않는다.

공개된 `v0.12.1-preview.1`은 13개 자산이다. C12 최종 FullOffline ZIP은 311,147,867바이트, SHA-256 `89A3CDA8BBD21918795205A828657BB27F72B40B04BBD8085A90CC459E57C165`. 패키지 검증 원본은 `artifacts/task054/package-inputs-final4/package-validation.json`이다. 오프라인 안내를 최종 배포 링크와 완성 ZIP 직접 설치 절차로 갱신했고, 네 AI VSIX의 PDF 실제 경로가 서비스 계약과 같음을 확인했다. 앞선 미공개 준비본은 경로 검증 후 `artifacts/task054/superseded-candidate10`, `superseded-candidate11-pre-guide`, `superseded-candidate11-final3`에 보존했다. 공개 전 13개 로컬 파일과 GitHub 업로드 digest, 공개 후 13개 익명 다운로드의 바이트·SHA가 모두 일치했다. Release ID는 417654310, tag/source commit은 `974f703018cece52086ad2c81c935b9233b93ddc`다. 기존 정식 v0.11.1 latest·9개 자산과 이전 오프라인/미리보기 tag·자산은 서버 확인에서 모두 보존됐다.

공개 후 README·USER_GUIDE 링크와 이 배포 기록만 갱신했다. 원본 감사 보고서는 변경하지 않았고 Markdown의 의도적인 hard break 및 동결 원문의 EOF 빈 줄을 보존한 상태로 코드의 줄 끝 공백을 검사했다. CodexOrig2·3·4·6에게 빌드·최신 Release·실제 게시·익명 다운로드 검증까지가 완료 기준임을 전달했다. CodexCode 채팅 전송은 active-writer 도구 오류로 실패했으므로 WORK_STATUS의 공유 완료 안내로 남겼다.

고객의 Windows 10/11 x64 제한망 PC 및 VS2017 Professional 15.7.1 실제 환경은 별도 테스트가 필요하다. 현재 개발 PC의 VS2017은 15.9다. 실제 Claude 계정 답변, 물리 포인터 이동·전체 화면 픽셀·DPI별 시각 검사, Word 렌더는 이번 검증 범위에 없다.
