# TASK051 문서·사용 안내 보완 / CodexOrig3 / 2026-10-20

## 범위

Role.md의 2026-10-18 23:45 사용자 추가 요청(README GIF, 정지 그림, 접고 펼치는 긴 설명, 사용 안내)과 2026-10-19 23:09 UI 요청, TASK049·TASK050 결과 중 구매 사용자에게 보여 줄 설정, 팔레트, 완료 알림, 대화 이력, Markdown 안내를 반영했다. Role의 3차 감사 지시/후속 안내 기록은 기존 TASK-029·030 문서가 이미 다루는 Markdown 작성·미리보기 범위와 대조했고, 이번 문서에는 v0.12 후보 UI 변경과 공통 사용 흐름 시각 안내를 덧붙였다. 제품 소스 및 기존 배포 파일은 수정하지 않았다. 정식 공개 버전은 여전히 v0.11.1이며, v0.12.0 내용은 후보 기능 미리보기로 표시했다.

## 변경 및 결과

- `README.md`: v0.12.0 후보 UI 미리보기 요약과 사용자 안내서/PDF 링크를 추가했다. 기존 v0.11.1 설치 링크와 다운로드 표는 그대로 유지한다.
- `docs/USER_GUIDE.md`: 후보3 화면 캡처 4장을 포함해 설정 페이지와 상세 안내 접기/펼치기, 앱 팔레트와 IDE 테마의 차이, 성공 완료 작업 표시줄 balloon의 범위, 최근 20개/이전 20개 이력 탐색과 100개·4MB 한도, Markdown 여섯 도구 그룹과 키보드 탐색을 설명했다. README의 연결·요청·검토·안내·설치 GIF와 정지 그림 대안으로 연결했다. 실제 IDE 캡처가 아니라는 점과 미검증 경계를 각 안내에 명시했다.
- `output/pdf/VisualStudio_AI_Assistant_설치_사용설명서_v0.12.0_미리보기.pdf`: 기존 고객 사용설명서 18쪽을 보존해 뒤에 5쪽 시각 안내를 덧붙인 총 23쪽 PDF다. candidate3 WPF 캡처 4장과 README의 5개 사용 흐름 PNG, 알림/이력 흐름 도식을 포함한다. 렌더링 검토 중 19쪽·21쪽 간격 문제를 수정하고 19~23쪽을 다시 렌더링해 경계 겹침과 가독성을 확인했다.
- `docs/assets/ui-guide-v0.12.0/candidate3/`: 설정/VS 팔레트, Kakao, Dracula, Markdown 작성기 캡처 및 캡처 manifest를 보관한다. 대상 DLL은 `artifacts/task051/candidate3/2017/Release/VS2017.AIAssistant.dll`, SHA-256 `058C68A5725501B5659D4E7DD8B807B0CA10ACBAE887B3DD97A3F8CC62A49143`다. 캡처 창은 표시되지 않았으며 실제 Visual Studio IDE·고객 PC·Windows balloon을 캡처하거나 검증하지 않았다.

## 패키지 인계

TASK051 C5 소스 패키지에 아래 문서와 자산을 포함한다. `tmp/pdfs/task051/`의 제작 스크립트/중간 렌더는 패키지에 포함하지 않는다.

1. `README.md`
2. `docs/USER_GUIDE.md`
3. `output/pdf/VisualStudio_AI_Assistant_설치_사용설명서_v0.12.0_미리보기.pdf`
4. `docs/assets/ui-guide-v0.12.0/candidate3/settings-vs.png`
5. `docs/assets/ui-guide-v0.12.0/candidate3/settings-kakao.png`
6. `docs/assets/ui-guide-v0.12.0/candidate3/settings-dracula.png`
7. `docs/assets/ui-guide-v0.12.0/candidate3/markdown-editor.png`
8. `docs/assets/ui-guide-v0.12.0/candidate3/capture-manifest.json`
9. `docs/assets/guide-media/01-connect.gif` 및 `.png`
10. `docs/assets/guide-media/02-request.gif` 및 `.png`
11. `docs/assets/guide-media/03-review.gif` 및 `.png`
12. `docs/assets/guide-media/04-guide.gif` 및 `.png`
13. `docs/assets/guide-media/05-install.gif` 및 `.png`

기존 `output/pdf/VisualStudio_AI_Assistant_설치_사용설명서.pdf`(1,089,143 bytes)와 `releases/v0.11.1/`의 기존 고객 전달물은 변경하지 않았다. 이 미리보기는 공개 릴리스, 실제 IDE 설치 검증, 감사 완료 또는 배포 완료를 뜻하지 않는다. C5 패키징·릴리스·최종 통합은 Root TASK051 범위다.

## 검증

- PDF 구조: 23쪽(기존 18 + 후보 부록 5). 미리보기 파일은 1,455,493 bytes, SHA-256 `41004D84A1D96D53171CAF4E17FA027D19ACDCAC7F27B0DB68DD4E694417D42A`다. 기존 고객 PDF는 1,089,143 bytes, SHA-256 `EAE9BDD2CA1602677FEAF6BD1F50C19C8F7E29068D33B1DC06215D998C60B30A`로 보존했다.
- 시각 검수: 전체 PDF contact sheet, 부록 19~23쪽 원본 해상도 렌더링. 19쪽 경고 상자 간격과 21쪽 저장 한도 상자 간격 수정 후 재렌더링.
- candidate3 manifest의 assembly SHA-256과 문서 자산 경로 확인.
- README, 사용자 안내서, PDF, 4개 이미지, manifest SHA-256을 작업 기록으로 산출했다.
- 독립 감사 결과와 최종 Release/IDE 검증 결과를 대신하지 않는다.

## candidate5 화면 캡처 후속 갱신 / 2026-10-20 00:32 KST

Root의 문서 동결 해제 요청에 따라 R53-02 테마형 `WindowChrome` 적용 뒤 새 candidate5 고정 DLL에서 동일한 4개 화면을 다시 별도 WPF 렌더링했다. 새 자산은 `docs/assets/ui-guide-v0.12.0/candidate5/`에 기록하고 이전 candidate3 그림과 manifest는 보존했다. 새 DLL SHA-256은 `073747D000F747B509DE3966360CD905D53D42A42D4207D0F8E1A681DD9FBAFF`이며 캡처 manifest는 `WindowShown=false`, `ActualVisualStudioIdeCaptured=false`다.

`README.md`, `docs/USER_GUIDE.md`, 같은 경로의 23쪽 미리보기 PDF에서 이미지 경로를 candidate5로 전환하고, 공통 팝업 제목 표시줄이 확장 팔레트를 따르며 이동·최소화·최대화·닫기를 제공한다고 설명했다. 기존 18쪽 고객 PDF는 SHA-256 `EAE9BDD2CA1602677FEAF6BD1F50C19C8F7E29068D33B1DC06215D998C60B30A` 그대로다. 23쪽 PDF는 1,455,702 bytes, SHA-256 `A17D4A74CD195CBAAEB63971C13A488FDDD483F98289ADCE2493CB91819A8693`다. 19~23쪽을 다시 렌더링했고 20쪽 캡처에서 새 제목 표시줄/창 제어를, 부록의 전체 배치와 페이지 수를 검수했다.

Root가 전달한 실제 IDE VS2017/VS2019 145/145 및 Windows balloon 1회 표시는 통합 담당자의 검증 근거이며, 이 문서의 이미지 캡처는 실제 IDE 사진이라고 표시하지 않았다. 문서 입력은 이 갱신본으로 다시 동결했고 최종 C6 패키지 포함을 Root에 인계한다.
