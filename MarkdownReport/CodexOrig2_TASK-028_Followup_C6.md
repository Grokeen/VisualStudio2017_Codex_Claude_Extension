# TASK-028F1 — R3-03 후속 확인 / candidate6

감사자 **CodexOrig2**. 2026-10-19 02:45 KST 요청에 따라 후보6을 독립 확인했다. 결과 기준은 02:52 KST의 고정 candidate6이며, 이후 개발 중인 소스와 구분한다. 기존 [3차 감사 원본](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-028_Audit3.md)과 Role.md는 편집하지 않았다.

**판정: 작성 기능과 참조형 링크는 보완 확인. HTML 미리보기의 재현 가능한 차이와 표시 누락이 남아 R3-03은 부분 해소 상태다.**

기존 제품 검사 218/218을 감사자의 고정 사본에서 통과했다. 별도로 작성한 문서 사례 21개에서는 **14개 통과·7개 실패**였다. 통과 수치가 서로 다른 검사 범위를 나타내므로 합산하지 않는다. 아래 7개 실패는 기존218개에 없던 추가 문서 사례다.

## 대상과 방법

- [후속 검증 JSON](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-028_Followup_C6_Evidence.json), [직접 관측값](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c6/observations-final.json), [독립 검사 원문](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c6/result-final.json).
- 입력120개 사본의 SHA가 전부 일치했다. 목록 SHA256 `580E3C9F7700EDA1815D8AE30410FF37879A531D1CD175763BE49517923B1189`.
- C6 검토 Role SHA256 `EDF43D1A6D347C188F1DB4C2D7A21E7B4B5EBFD922289CC6086708A92820781D`. 원래 설치 PDF도 이번 입력 목록에 포함됐다.
- 실제 검사 DLL: VS2017 Release SHA256 `3C8083975BA43696C0946F54DD7D7B73759A0ABEEE6DEF9CD7FB3F04CDCCCD4A`.
- 개인 사본 `C:/dydgyqud/AO28_20261019/candidate6`; 실제 제품 DLL을 로드하고 임시 문서만 생성·저장·이름 변경·삭제했다. 창 표시·실물 입력·브라우저/탐색기 실행·실제 CLI·공유 빌드·배포는 없었다.
- C6는 기능 확인용 후보다. TASK026의 후속 UI 보정과 이후 HTML 보완이 포함된 최종 후보의 검증을 대신하지 않는다.

## 보완 확인

| R3-03 범위 | 이번 확인 |
|---|---|
| 문서 폴더·목록 | 최초 자동 조회 없음. 명시 폴더의 최상위 .md/.markdown만 표시. 하위/다른 확장자 제외 직접 확인 |
| 현재 파일 저장 | SaveCurrentFile로 같은 경로에 CRLF·한글 저장, clean 상태 직접 확인 |
| Ctrl+S / Ctrl+Shift+S 분리 | 고정 소스 EditorKeyDown/도구 모음에서 분리 확인. 물리 키 입력 검사는 하지 않음 |
| 저장 실패 보호 | 잠긴 현재 파일에서 저장 거부 후 원본 바이트·draft·dirty·경로 보존 직접 확인 |
| 이름 변경 | 정상 변경 후 미저장 draft·디스크 원문 보존. 이름 충돌·../ 이탈·비문서 확장자 거부 직접 확인 |
| 삭제 | null/거부 확인에서 삭제 없음. 잠금 실패에서 원문·경로 보존. 승인 시 현재 임시 파일만 삭제하고 초기화 직접 확인 |
| 파일 위치 열기 | explorer.exe 고정 경로와 /select, UseShellExecute=false를 코드로 확인. 탐색기 실행은 미실행 |
| 참조형 링크·이미지 | full/collapsed/shortcut 링크3개, 코드 펜스 정의 미적용, 명시 문서 폴더의 로컬 PNG 참조 정의 직접 확인 |
| 기본 정적 HTML | 단일 HTML 표·강조 native 표시, script/iframe의 실행 경로 없음·원격 이미지 컨트롤 없음·DTD 평문 대체 확인 |

저장 API 성공만으로 파일 선택 대화상자·탐색기·실제 키 입력까지 실행했다고 집계하지 않았다. 문서 기능 근거는 [고정 Documents partial](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate6/source/src/VS2017.AIAssistant/Views/MarkdownEditor.Documents.cs:128), [저장 단축키](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate6/source/src/VS2017.AIAssistant/Views/MarkdownEditor.cs:538)에 있다.

## R3-03F1 / P2 — 지원 HTML 태그의 블록 경계 처리 오류

**직접 재현, C6 미해소.** 아래 문서들은 현재 지원 목록에 있는 div/p/table을 사용하지만 원시 태그가 표시된다.

| 재현 파일 | 기대 | C6 실제 |
|---|---|---|
| [중첩 div](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c6/fixtures/html-nested-same-tag.md) | 중첩된 본문 표시 | `<div> <div><p>Nested content</p></div> </div>` 평문 |
| [문단 직후 표](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c6/fixtures/html-paragraph-then-table.md) | Intro 다음에 native 표1개 | 표0개, table 태그 평문 |
| [연속 p](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c6/fixtures/html-sibling-roots.md) | 두 문단 표시 | `<p>One</p><p>Two</p>` 평문 |

최소 중첩 재현 원문:

```html
<div>
<div><p>Nested content</p></div>
</div>
```

원인: [TryHtmlBlock 45행](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate6/source/src/VS2017.AIAssistant/Views/MarkdownRendering.Html.cs:45)은 첫 동일 닫는 태그가 있는 행에서 바로 XML 파싱을 시도해 외부 div가 닫히기 전에 실패한다. [52행](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate6/source/src/VS2017.AIAssistant/Views/MarkdownRendering.Html.cs:52)은 단일 XML 문서로 읽어 연속 p 루트를 거부한다. [StartsBlock 394행](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate6/source/src/VS2017.AIAssistant/Views/MarkdownRendering.cs:394)에는 HTML 시작 판정이 없어 일반 문단에 바로 이어진 표가 기존 문단에 흡수된다.

조치: 지원 블록의 중첩·복수 블록과 Markdown/HTML 전환 경계를 처리하고, 위3문서의 실제 WPF 구조를 회귀에 추가한다. 길이·깊이·scan-work 제한은 유지한다.

## R3-03F2 / P2 — 원본 대비 남은 정적 HTML 범위 차이

**직접 비교, C6 미해소.** 임의 CSS·스크립트 실행과 관계없는 정적 문서에서도 차이가 남는다.

- [이름 있는 엔티티](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c6/fixtures/html-named-entity.md): `<p>Copyright &copy; 2026 &amp; text</p>`가 문단 전체의 태그·엔티티 평문으로 표시된다. XML에 앞서 &nbsp;만 치환하므로 &copy;에서 파싱이 실패한다.
- [완전한 HTML 문서](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c6/fixtures/html-full-document.md): doctype/html/head/body가 포함된 단순 문서가 전체 평문이다. HtmlStart는 이 문서의 시작을 인식하지 않는다.

원본 프로젝트는 [MarkdownnHtmlRenderer.FullDocument/Render](C:/dydgyqud/Markdown_Memo_Interchange/RemoteControlDemo/Features/Markdown/MarkdownnHtmlRenderer.cs:81)에서 완전한 HTML 문서를 명시적으로 처리한다. 원본의 Markdig1.3.2 변환을 실제 실행했을 때 문단 직후 table도 별도 HTML 블록으로 보존됐다. [변환 비교](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c6/original-transform-comparison.json)는 변환 출력과 원본 경로 검토이며 원본 앱의 브라우저 UI 실행 증거는 아니다.

조치: 안전한 본문 추출·엔티티 표시를 보완하거나 해당 원본 기능의 미지원 범위를 명시적으로 정리한다. 이 차이가 남은 상태에서 원본 전체 미리보기 이식 완료로 표시하지 않는다. 외부 DTD/entity resolver·스크립트·CSS·iframe 실행을 허용하라는 요청은 아니다.

## R3-03F3 / P2 — HTML 표 제한 초과 시 내용 생략 안내 없음

**직접 재현, C6 미해소.**

- [17열 표](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c6/fixtures/html-column-limit-notice.md)의 17번째 `TAIL_SENTINEL`이 사라지고 생략 안내도 없다.
- [129행 표](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c6/fixtures/html-row-limit-notice.md)의 129번째 `LAST_ROW_SENTINEL`이 사라지고 생략 안내도 없다.

두 입력 모두 일반 문자·노드 한도보다 작아 별도 한도 경고가 발생하지 않았다. [HtmlTable 116/119행](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate6/source/src/VS2017.AIAssistant/Views/MarkdownRendering.Html.cs:116)은 Take(128)/Take(16)로 잘라내면서 공통 omitted 상태를 설정하지 않는다. 편집 원문은 유지되지만 미리보기만 보면 표 전체가 표시된 것으로 오해할 수 있다.

조치: 128행·16열 제한을 유지하고, 실제로 생략한 경우 미리보기의 공통 생략 안내를 표시한다. 제한 바로 아래/동일/바로 위를 구분해 검증한다.

## 다른 담당자의 추가 결과 — 직접 실행과 구분

| 자료 실행자 | 고정 C6 결과와 확인 범위 |
|---|---|
| TASK025 전체 실행기 | `artifacts/tl/0e5d65768c/summary.txt`의16묶음 모두 exit0. 감사자는 원문을 읽었으며 전체 실행기를 직접 실행하지 않음 |
| TASK024 독립 경계 검사 | [22PASS/1FAIL 원문](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task024/audit-followup-candidate6-delete-transition/result.json). SC024-02: 삭제 확인을 연 뒤 다른 문서를 열면 확인 행이 남아 새 현재 문서에 적용될 수 있음. 감사자는 결과·고정 소스를 확인했고 자체 재현 수에 합산하지 않음 |
| TASK026 UI 검사 | [C6 9PASS/6FAIL 원문](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task026/audit3-documents-clipping-repro/result.json). 좁은 폭에서 목록/이름 입력 겹침 및 스크롤 접근 누락. 감사자는 원문과 C6 좁은 폭 렌더 이미지를 직접 열어 확인 |
| TASK026 수정 소스 | 후속 소스111/111 통과 인계. **수정 전 C6가 통과한 것은 아니며**, 수정 포함 최종 DLL 확인 필요 |

기존218개 또는 전체16묶음 통과로 이후 추가한 HTML·삭제 수명·좁은 폭 사례를 완료 처리하지 않는다. 실제 IDE/GUI 통합은 TASK025 소유이며 이 후속 감사는 창을 사용하지 않았다.

## 패키지·보존·완료 조건

세 C6 DLL/VSIX SHA는 readiness와 전부 일치했다. 각 VSIX 선택 핵심23파일의 내부 바이트도 Release 출력과 일치했다. GIF5/PNG5·AvalonEdit MIT LICENSE·WebView2 의존성/loader·worker를 포함한다. [패키지 근거](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c6/package-integrity-c6.json)에 세 SKU 전체 해시와 선택 목록을 보존했다. 기존 설치/감사/이미지26파일과 이번 시작 시 고정한 기존 감사·Role 해시를 재대조한다.

감사 중 Orig3가 Role 말미에 TASK029 문서 안내를 추가했다. C6에서 검토한 Role 전체가 현재 파일의 동일한 바이트 접두부로 남는지 별도 확인했다. 기존 감사 보고서·검증 JSON의 SHA는 그대로이며, 이번 감사자가 Role을 편집한 것은 아니다. 추가 전후 해시와 접두부 검사는 후속 검증 JSON에 기록했다.

처리 경계는 기존 문서128Ki·노드6000·scan-work8Mi·깊이8, 추가 HTML64Ki·512행·표128행16열을 유지한다. 독립 입력에서 새 코드 실행·원격 자산 자동 로딩을 확인하지 않았다. 최초 probe의 두 컴파일 준비 오류와 기대 거부 예외 처리 누락은 감사 하네스 수정으로 기록하고 원문을 보존했다. 최종21사례의7실패와 구분한다.

후속 후보에서 R3-03F1~F3와 SC024-02, 좁은 문서 목록의 수정 결과를 정확한 새 DLL에 연결해야 한다. 그 후보의 기능218·독립 문서 사례·새 UI 검사·필요한 실제 IDE/GUI 회귀를 변경 영향에 맞게 확인한다. 실제 로그인/운영 연동 등 기존 미검증 한계는 유지한다.

**이 후속 보고서 작성은 완료하며 R3-03은 미종결로 인계한다.** 기존3차 감사·검증 JSON·Role 지시는 그대로 보존하고, 이번 결과를 새 소스/후보의 성공으로 소급하지 않는다.
