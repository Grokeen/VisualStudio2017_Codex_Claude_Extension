# TASK-025 / CodexOrig2 3차 감사 요청

요청 시각: 2026-10-19 01:49 KST. 사용자가 지정한 기존 ‘Role.md 반영 여부 감사’ 대화에 요청한다. 최종 배포 전 약 90% 반영 단계이며, 아래 고정 후보에 대한 독립 감사를 요청한다.

## 기준과 고정 대상

- Role.md의 2026-10-18 23:45 사용자 원문 15항목을 기준으로 한다. 말미 CodexOrig3의 별도 문서 검토 제안 5개는 사용자 원문과 구분한다.
- 검토 Role 사본: `artifacts/task025/Role.reviewed.docs027.md`, SHA256 `0EF513021D252BE48441D7813D98A05CD615E493420B9F0A72C4D39E4C0B976C`.
- 최종 후보: `artifacts/task025/candidate3/`. 원본 경로별 118개 입력의 고정 사본은 `candidate3/source/`, 해시 목록은 `candidate3/source-hashes.json`이다. 목록 SHA256 `37711B833067202ED58BE5C7591309B38CCA0BC61577FB487536AAC4FA4D6D0D`.
- 세 버전 Debug/Release 6빌드 모두 오류 0·경고 0. 실제 패키지·DLL 해시, 빌드 로그와 검사 요약은 `artifacts/task025/audit-readiness.json`에 있다.
- VS2017 Release DLL SHA256 `26FEF3E2BCCA746985E87599F54029E8F8DC17877D9BFBBCA46A6F2FB01CE52A`.
- VS2019 Release DLL SHA256 `A704C5BC6B3C967080477438D00EEBE108A459AF23B8AE95124947C8C32D2182`.
- VS2022 Release DLL SHA256 `A013098D61A71AABBE4A952D0618BE924BE3A088EB08CDEBF3F26F43205E4BBE`.
- 각 `candidate3/{2017,2019,2022}/Release/`에는 VSIX와 실행 DLL·AvalonEdit·WebView·worker·Resources·GuideMedia·ThirdParty 라이선스를 함께 보관했다.

## 현재 증거와 미완료 범위

- 고정 VS2017 DLL의 신규 실제 제품 검사 196/196: `artifacts/task024/actual-candidate3-final/summary.json` (Markdown78/Hover35/Guide31/Account24/RootUi28).
- 동일 DLL의 테마 검사 31/31: `artifacts/task024/theme-candidate3-final/result.json`. 두 검사는 SourceOnly=false이며 Window.Show·실물 마우스·실제 로그인·유료 모델 요청은 실행하지 않았다.
- 실행기 15/15: `artifacts/ls/712580947c/results.json`. 이 결과를 나머지 16묶음 전체 회귀의 합격으로 간주하지 않는다.
- 남은 통합: 기본 전체 16묶음, VS2019/2022 테마 실제 DLL, 실제 VS2017/2019/2022 IDE, 물리 hover 및 실제 Window의 GIF 수명, 감사 반영과 최종 배포·원격 해시 확인.
- 물리 입력 검사는 현재 Screen-saver 데스크톱/GetCursorPos Error5로 미실행이다. TASK024가 이미 사용자에게 질문했으므로 중복 질문·잠금 설정 변경을 하지 않는다.
- 기존 실제 Claude CLI 계정 및 운영 DB/SVN/TFVC 서버 검증의 한계는 유지한다. 로그인 창 열기·인증 상태 확인·모델 실행 성공을 구분한다.

## 감사 요청

1. 사용자 원문 15항목 및 기존 1·2차 감사의 미해결 사항을 고정 후보의 실제 동작/코드/패키지와 대조한다. 이미지28~38의 시각 요구도 포함한다.
2. 제공 프로젝트의 Markdown 작성·미리보기 기능과 이식 기능을 명시적으로 대조하고 누락을 판정한다. Markdown 링크 실제 대상·소스 줄 이동, 명시적으로 연 로컬 문서의 PNG/GIF 상대 경로, 문법 처리와 계산량 제한을 확인한다.
3. 스크롤 우선순위·diff 가독성·좁은 대화목록 배경 차단·상단 고정 뒤로가기·버튼/전송 아이콘·설정 설명 접힘·로그인 구분을 확인한다.
4. Auto/Light/Dark 및 VS 우선 정책, 열린 팝업과 기존 Markdown 문서의 즉시 색 전환, GIF decode 실패 대체와 닫힘/접힘 수명을 검토한다.
5. VSIX 3종의 의존 DLL·실제 GIF/PNG·MIT 라이선스·경로 방어와 기존 원본 자산 보존을 확인한다. 설명용 그림과 실제 제품 캡처의 출처를 구분한다.
6. 별도 짧은 개인 경로에 사본을 만들어 감사한다. 공용 제품·bin/obj·릴리스·staging·과거 감사 보고서와 증거를 변경하거나 배포하지 않는다. GUI가 필요한 검사는 WORK_STATUS에 독점 예약을 협의한다. 우선 정적·headless 감사를 진행해 달라.
7. 새 3차 보고서와 검증 JSON에 발견 ID/우선순위/재현 절차/파일과 줄/해시/직접 검증 여부를 기록한다. 미실행을 합격으로 합산하지 않는다.
8. 완료 후 사용자 지시대로 Role.md 말미에 ‘3차 감사 결과 확인·반영 지시’를 날짜와 함께 추가한다. 사용자 원문15·기존 Orig3 제안은 보존하고, 확인된 결함과 추가 검증 필요 사항을 구분한다. WORK_STATUS에 종료와 보고서 경로를 남긴다.

관련 보고서: `MarkdownReport/CodexOrig_TASK-025.md`, `CodexCode_TASK-024.md`, `CodexOrig4_TASK-026.md`, `CodexOrig3_TASK-027.md` 및 각 Validation/SecureCoding 문서. 원본 CLI PDF/MD/bat와 기존 고객전달 ZIP·과거 릴리스는 보존한다. README 버전/다운로드는 최종 검증·패키지 확정 뒤 0.10.0으로 갱신한다.
