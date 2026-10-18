# CodexOrig3 TASK-029 — 3차 감사 후속 사용자 안내 문서

작성: 2026-10-19 02:48 KST  
요청: `Role.md`의 사용자 설명·매뉴얼·PDF/이미지 관련 요청을 확인해 필요한 문서를 만들고, 제품 소스 수정은 최소화한다.

## 검토 대상과 판정

`MarkdownReport/Role.md`의 원래 사용자 요구 4번(작성기 전체 이식), 7~9번(움직이는 사용 안내, 접이식 설명, 읽기 쉬운 Markdown 그림 안내)과 CodexOrig3 제안, CodexOrig2의 3차 감사 R3-03을 대조했다. R3-03의 핵심은 문서 목록·현재 문서 저장·이름 변경·삭제·위치 열기와 참조형 링크·HTML 미리보기 차이를 사용자에게 정확히 설명하는 것이다.

candidate6 소스에서 확인한 동작을 바탕으로 전용 그림 매뉴얼을 작성했다. README.md와 docs/USER_GUIDE.md에도 링크와 짧은 기능 안내를 넣었다. 문서·이미지 외 제품 소스, 검사 코드, DLL, 설치 패키지는 변경하지 않았다.

## 산출물

- `docs/visual-guide-orig3/markdown-editor/Markdown작성기_사용설명서.md` — PDF와 같은 주제를 담은 Markdown 원문.
- `docs/visual-guide-orig3/markdown-editor/images/01-entry.png`부터 `07-scope.png` — 일곱 개의 설명 도식. 실제 제품 화면 캡처가 아님을 각각 표기.
- `output/pdf/Markdown작성기_사용설명서.pdf` — 표지와 7개 절, 총 8쪽. 한글 글꼴과 그림을 포함.
- `output/Markdown작성기_안내자료_검토용.zip` — PDF, Markdown, 이미지, 안내 README를 포함한 휴대용 묶음.
- `docs/USER_GUIDE.md`, `README.md` — 작성기 파일 목록/저장/참조형 링크/정적 HTML의 짧은 안내와 부록 링크.

## 사용자에게 안내한 기능 범위

- 요청 입력란·공통 규칙 연결 작성기와 독립 문서 작성의 시작점 및 반영 차이.
- 편집·미리보기·분할 보기, 작성기 전용 단축키, 선택 폴더의 최상위 Markdown 파일 관리.
- 현재 저장과 다른 이름 저장, 미저장 변경 확인, UTF-8 및 파일 크기 제한.
- 명시적으로 연 문서의 상대 링크/이미지 및 참조형 링크.
- 일부 정적 HTML의 제한된 미리보기와 스크립트·CSS·iframe·원격 로딩을 실행하지 않는 경계.

모든 Markdown 확장 지원, 임의 HTML 실행 또는 원격 자료 자동 수집을 주장하지 않는다. 이 안내서는 candidate6 구현 기준이며 최종 통합 검사 결과를 대신하지 않는다. TASK-025의 최신 상태 기록에 따라 최종 UI·패키지 대조 후 고객용 매뉴얼로 승격하도록 표시했다.

## 검증 및 한계

PDF 메타데이터와 8쪽 구성을 확인했고 Poppler로 전 페이지를 PNG로 렌더해 시각 검토했다. Markdown 이미지 7개가 존재하며 해당 문서의 상대 경로를 확인했다. 문서·리포지터리 테스트는 실행하지 않았다. 이 작업은 설명 자료 작성이며 candidate6 기능의 독립 회귀 검증이나 최종 고객 패키지 승인이 아니다.

## 소유권과 협업

TASK-025가 docs/USER_GUIDE.md 및 README.md의 문서 작업을 TASK-027에 명시적으로 인계한 상태를 확인해 최소 안내와 링크를 추가했다. 기존 CLI 설치 Markdown/BAT/PDF/ZIP 및 이전 시각 안내 자료는 수정하지 않았다. 최종 후보에서 라벨이 바뀌면 본 부록·README·USER_GUIDE를 같은 변경으로 갱신한다.
