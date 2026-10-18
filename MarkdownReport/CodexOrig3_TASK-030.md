# CodexOrig3 TASK-030 — candidate7 작성기 화면 설명·문서 개정

작성: 2026-10-19 03:08 KST  
요청: 새 문서 목록 화면과 candidate7 UI 검사 결과를 기존 TASK-029 사용자 문서에 반영한다. 개인 경로를 노출하지 않고, 제품 소스·GUI·공유 빌드는 건드리지 않는다.

## C7 기준과 확인

candidate7 120개 입력 source manifest SHA-256은 `489FEC7A9D019E25120A62468F2E903BF66E1807DAD2F48D86D53C23E3214902`이다. 세 SKU의 정확 Release DLL/VSIX 해시는 `artifacts/task025/candidate7-readiness.json`에 기록되어 있다. VS2017 Release DLL SHA-256은 `9749270FFFA7053C2E4EC2FCCAF0D9DE2DEE09FA77FAD45228947CF3E064935F`이다.

실제 DLL UI 검사는 2017 `artifacts/task025/document-ui-candidate7-fixture-fixed/result.json`, 2019 `artifacts/task025/document-ui-candidate7-2019/result.json`, 2022 `artifacts/task025/document-ui-candidate7-2022/result.json`에서 각각 117/117 통과했다. 세 결과는 `SourceOnly=false`, `WindowShown=false`, `ActualIdeTested=false`; 따라서 제품 DLL의 WPF 컨트롤을 별도 STA에서 렌더했으나 실제 IDE 창 캡처는 아니다. 좁은 폭의 세로 스크롤, 문서 이름/버튼 접근, 확인 단계·취소 후 파일/초안 보존이 검사됐다. 정확한 세 SKU Release DLL/VSIX SHA는 `artifacts/task025/candidate7-readiness.json`에 있다.

최초 CodexOrig3 실행의 105/108 실패와 PNG는 `artifacts/visualguide-orig3/candidate7-document-ui/`에 보존했다. 표 자체는 그림에서 렌더됐지만 검사기는 HTML 표가 WPF `Section` 안에 중첩될 수 있음을 반영하지 못하고 문서 최상위 블록만 검사했다. TASK025가 표를 재귀 조회하도록 검사기를 보정했고, 위 세 제품 DLL 결과에서 117/117 통과했다. 제품 코드는 이 검사 전제 수정 때문에 변경하지 않았다. 독립 3차 감사 후속 C7 HTML 21/21 근거는 `MarkdownReport/CodexOrig2_TASK-028_Followup_C7.md`와 상세 결과 `artifacts/audit-codexorig2-followup-c7/probe-{SKU}-result.json`, 관측 JSON, 검사 로그에 있다. 실제 IDE 검증과 구분한다.

## 문서에 반영한 화면 동작

- 넓은 화면에서는 문서 패널이 편집 탭 왼쪽, 좁은 화면에서는 탭 위에 접이식으로 놓인다. 좁은 패널은 안쪽에서 세로 스크롤한다.
- 사용자가 폴더를 선택한 뒤 해당 폴더 바로 아래 Markdown 문서만 읽는다. 더블 클릭/Enter로 연다.
- 이름 변경·파일 위치·삭제 버튼은 선택된 목록 행이 아니라 현재 열린 문서에 동작한다.
- 삭제 확인은 현재 파일과 저장되지 않은 작성 내용도 지운다. 취소하면 보존한다. 문서 전환 후에는 확인 상태가 새 문서로 승계되지 않는다.
- 정적 native HTML 미리보기와 표 한도(128행×16열) 및 생략 알림, CSS/스크립트/iframe/원격 이미지 자동 로딩 금지를 설명했다.

## 개정 산출물

- `docs/visual-guide-orig3/markdown-editor/revision-c7/Markdown작성기_사용설명서_C7.md`와 그림 10개. 그림 08~09는 위 2017 C7 DLL에서 캡처가 아닌 STA 오프스크린 렌더로 생성했다. 검사 폴더를 `C:\Temp\MarkdownManualC7-fixed`로 지정해 개인 사용자·프로젝트 경로를 화면에 남기지 않았다.
- `output/pdf/Markdown작성기_사용설명서_C7.pdf`: 10쪽. PDF/Pillow/Poppler 구성과 글자 및 여백을 확인했고 10쪽을 PNG로 렌더해 전체 contact sheet 및 UI 렌더 페이지를 직접 검토했다.
- `output/Markdown작성기_C7_사용설명서_검토용.zip`: PDF, Markdown, 그림 10개, package README 포함.
- `MarkdownReport/CodexOrig3_TASK-030_Evidence.json`: C7 readiness, 세 SKU UI·HTML 검사결과, 산출물 ZIP 항목 및 최종 파일 SHA-256.
- `docs/USER_GUIDE.md`와 README의 작성기 사용 안내/부록 링크를 C7 내용으로 갱신했다. README의 버전 표시 및 다운로드 링크는 TASK-025 지시에 따라 수정하지 않았다.
- `output/pdf/Markdown작성기_사용설명서.pdf`, 원 TASK-029 MD/PDF/ZIP은 수정하지 않고 보존했다.

## 제한

candidate7 6 빌드는 오류0·경고0이다. 해당 후보의 실제 2017/2019/2022 IDE 통합 실행은 별도 담당 작업에서 진행 중이므로 이 문서는 고객 최종 릴리스 문구로 승격되지 않는다. 이후 IDE 대조에서 버튼 라벨·배치가 바뀌면 이 C7 개정판을 다시 갱신한다. GUI 창/마우스 조작, 공용 build, 제품·검사 소스 수정, Git 배포는 수행하지 않았다.
