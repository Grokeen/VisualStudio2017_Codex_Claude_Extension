# TASK-028F3 — 후보8 R3-03F4 수정 확인

감사자 **CodexOrig2**. 2026-10-19 03:12 KST 요청에 따라 고정 candidate8을 독립 확인했다. [C7 후속 감사](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-028_Followup_C7.md)와 당시 실패는 보존하고 새 결과를 기록한다.

**판정: R3-03F4를 세 버전의 실제 C8 DLL에서 수정 확인했다. 이전 R3-03F1~F3·삭제 확인·좁은 UI의 회귀도 유지됐다. 이번 확인 범위에서 새 개발 수정 지적은 없다.**

17열 표는16열까지만 표시하면서 생략 안내를 추가한다.15열과 정확히16열에는 잘못된 생략 안내가 생기지 않는다. 기존 문서21개·독립 보안23개·문서UI117개도 각 SKU에서 통과했고, VS2017 기능5그룹231개도 직접 통과했다. 실제 IDE·계정·배포 등 별도 범위의 완료로 확대하지 않는다.

## 고정 대상

- [검증 JSON](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-028_Followup_C8_Evidence.json), [readiness](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate8-readiness.json).
- 입력120개 사본의 SHA가 전부 일치했다. 목록 SHA256 `08AE13DE57637766A994868ACBDEB27A8F2FC587CD1786895EDCAD47FF7B0787`.
- 검토 Role SHA256 `DE232D862F2B59E069470776CA8AB0F2F54AE7460F426759D7698CB32181C828`.
- C7→C8 고정 입력 차이는 **MarkdownRendering.cs 한 파일**이다. [차이 원문](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c8/product-change.diff)에서 탐지용17번째 셀과 출력 열 수 초과 시 omitted 설정만 추가된 것을 확인했다.
- 개인 사본 `C:/dydgyqud/AO28_20261019/candidate8`; 제품/공유bin·obj/빌드/GUI/로그인/설치/배포 작업 없이 검사했다. 문서 파일 조작은 개인 시험 파일에 한정했다.

| SKU | 제품 DLL SHA256 | VSIX SHA256 |
|---|---|---|
| 2017 | `A2A10A3922CC3FD47F5AA10E15FFF7FCB70A44494D4B26BEE22610B3C4C2172E` | `698876F45360499F7A50B55D71E2979BE0504B12BF0ED02ADFACC055F1576A46` |
| 2019 | `45F4E4D06E4CCDBAF3FB28CE54E9ED3FB6B2DF46BE69F6E8B23F686741D06420` | `3E3D313BCAF7AAEE6A0C8EDB1A076B26DD905F97FCF0307DB9201F8C7DDC7FB2` |
| 2022 | `06C41981F4D0049C07AC1BAA753E9A0DD3DC7FFB67BF385176F123D3A6419049` | `294CE1E7B574C703F26C852C3C1674EECE56FC0EA0B07324CE5EEFBE35561563` |

## F4 원래 재현과 열 경계

C7에서0PASS/1FAIL이던 원래 probe의 소스·기대 조건을 바꾸지 않고 C8 세 SKU에 실행해 **각1/1 통과**했다. 아래 별도6사례는 native Table의 실제 열 수·표시 텍스트·안내 유무·원문 보존까지 검사했다.

| 독립 문서 사례 | C8 기대/실제 |
|---|---|
| 15열 | 15열·생략 안내 없음 |
| 정확히16열 | 16열·생략 안내 없음 |
| 17열 | 16열·마지막 sentinel 생략·안내 있음 |
| 제목/구분은16열, 본문만17열 | 16열·본문 초과 내용 생략·안내 있음 |
| 정확히16열에 escaped pipe/인라인 코드 pipe |16열 유지·`a\|b`의 a|b와 코드 x|y 보존·안내 없음 |
| 17열에 위 literal pipe 혼합 |16열 유지·literal 보존·초과 안내 있음 |

세 SKU 각각6/6 통과했다. [원래17열 문서](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c8/fixtures/markdown-17-columns.md), [직접 관측값](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c8/boundary-2017-observations.json), [검사 소스](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c8/AuditBoundaryProbe.cs)에 재현 근거를 남겼다. **R3-03F4 종결은 위 고정 C8의 검증 범위에 한정한다.**

## 영향 범위 재검사

| 감사자가 직접 실행 | 결과 | 범위 |
|---|---|---|
| 원래 F4 probe | 각 SKU1/1 | C7 실패와 동일 입력/기대 |
| 추가 열 경계 | 각 SKU6/6 | 위 표의 독립 사례 |
| 동일 문서 probe | 각 SKU21/21 | C6/C7와 같은 코드·기대. HTML7사례 및 문서 관리/참조형 링크 유지 |
| 독립 파일/삭제/HTML 경계 | 각 SKU23/23 | SourceOnly=false. 실패/충돌 보존·삭제 확인 수명·경로/DTD/XInclude/시험용loopback 포함 |
| 문서UI | 각 SKU117/117 | SourceOnly=false. 좁은 폭·Dark/Light·스크롤/확인·취소. 실제 IDE/마우스 아님 |
| 기능5그룹 | VS2017 231/231 | Markdown111/Hover35/Guide31/Account24/RootUi30 |

결과는 `artifacts/audit-codexorig2-followup-c8/`의 `{종류}-{SKU}-result.json`, `*-observations.json`, `functional-result.json`에 보관했다. 원래 F4와 새 경계, 기존 회귀에 중복 조건이 있으므로 고유 총계로 합산하지 않는다.

## 다른 담당자의 실제 GUI 결과

TASK024의 고정 C8 [물리hover 검사](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task024/candidate8-native-diagnostic/result.json)는28/28, [실제 GIF 창 수명](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task024/candidate8-playback/result.json)은24/24였다. 감사자는 실행 원문을 읽었으며 GUI를 직접 실행하지 않았다. hover28에는 native24·합성2·구조1·수명1이 포함된다.

관찰을 추가한 C8 검사에서는 실제 anchor 위치·자기 HWND·Modifiers/capture·Click0→1이 확인됐다. 기존28개 검사 이름도 C4 통과 자료와 동일했다. C7 첫18PASS/1FAIL의 당시 원인은 여전히 미확정이며 C8에서 재현되지 않았다는 관측과 함께 보존한다. 제품 팝업을 수정해 해결했다고 단정하지 않는다.

## 패키지·보존·남는 범위

세 DLL/VSIX 해시는 readiness와 일치했다. 각 VSIX 선택 핵심23파일의 바이트도 Release 출력과 일치했다. GIF5/PNG5·MIT LICENSE/고지·AvalonEdit·WebView2/loader·worker를 포함한다. [패키지 상세](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c8/package-integrity-c8.json)에 선택 목록과 SHA를 보존했다.6빌드 오류0·경고0는 TASK025 결과 확인이며 감사자가 제품을 빌드한 것이 아니다.

표 출력16열·탐지17셀과 기존 문서/노드/깊이/scan-work 한도는 유지된다. 임의 script/CSS/iframe 실행·원격 이미지 자동 로딩을 지원하는 변경은 없다. 이번 감사에서 추가한 새 제품 수정 지적은 없으며, 구체적으로 제기했던 R3-03F1~F4의 수정/유지를 직접 확인했다.

기존 감사10파일·보호 설치/감사/이미지26파일을 대조하고 Role의 기존 전체 바이트를 보존한 뒤 말미에 이번 확인만 추가한다. 과거 시점의 미해소 판정과 실패 원문을 C8 성공으로 덮어쓰지 않는다.

03:16 기준 TASK025가 C8 전체16묶음·최종 실제VS2017/2019/2022와 마감 작업을 진행 중이다. C7 세 IDE 각133 통과를 C8 통과로 대신하지 않았다. 직접 ID/암호 요구의 대안·조사 구분, 실제 Claude/운영 DB·TFVC/운영 배포, 모든 브라우저 HTML 동등성의 기존 미검증 범위도 유지한다.

이번 감사 산출물 작성과 검사 범위의 지적 확인을 마감해 통합 담당자에게 인계한다. 최종 실제 IDE·문서/연구 담당자 종료·버전/다운로드·배포 및 원격 해시 마감은 통합 담당자가 별도로 연결한다.
