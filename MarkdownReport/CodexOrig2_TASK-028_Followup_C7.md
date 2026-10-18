# TASK-028F2 — candidate7 수정 확인 및 추가 경계 지적

감사자 **CodexOrig2**. 2026-10-19 03:02 KST 요청으로 C7을 별도 고정 사본에서 확인했다. 기능 검사 뒤 추가 경계 확인까지 포함한 고정 C7이 대상이며, 기존 [3차 감사](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-028_Audit3.md)와 [C6 후속 감사](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-028_Followup_C6.md)·실패 증거는 보존한다.

**판정: C6의 HTML7사례·삭제 확인·좁은 UI 지적은 수정 확인했다. 추가로 일반 Markdown의 표 생략 안내 누락(R3-03F4)을 재현해 전체 R3-03은 미종결로 유지한다.**

C6와 SHA가 동일한 독립 probe를 사용해 **각 SKU 21/21**을 통과했다. 이 중 이전 HTML7실패도 전부 통과했다. 별도 경계/삭제 검사 **각23/23**, 문서 UI **각117/117**, VS2017 기능5그룹 **226/226**도 감사자가 직접 실행해 통과했다. 그 뒤 추가한 일반 Markdown 표1사례는 실패했다. 반복·중복 조건을 합쳐 고유 총계로 제시하지 않는다.

종결은 직접 통과한 기존 지적에 한정한다. 새 R3-03F4, 모든 Markdown/브라우저 HTML 기능의 동등성, 직접 ID/암호 로그인, 운영 연동, 최종 실제 IDE·GUI·배포 검증은 구분해 남긴다.

## 고정 대상과 방법

- [검증 JSON](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-028_Followup_C7_Evidence.json), [readiness](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate7-readiness.json).
- 입력120개 사본 해시 전부 일치. 목록 SHA256 `489FEC7A9D019E25120A62468F2E903BF66E1807DAD2F48D86D53C23E3214902`.
- 검토 Role SHA256 `1508A4450ACC98F531770CF9639EA02A2931474F1DCA0754B05BBAA3D86BE782`.
- 개인 사본 `C:/dydgyqud/AO28_20261019/candidate7`. 제품/공유bin·obj/공유빌드/GUI/배포 변경·사용 없이 검사했다. 문서 쓰기·이름 변경·삭제는 개인 시험 파일에 한정했다.

| SKU | 실제 검사 DLL SHA256 | VSIX SHA256 |
|---|---|---|
| 2017 | `9749270FFFA7053C2E4EC2FCCAF0D9DE2DEE09FA77FAD45228947CF3E064935F` | `8B85E27CB3A79DA2FC3687EB4313B842167CDE03534BEF2F01582A98B5D2BB7F` |
| 2019 | `A7A4A5AA714FF168BBEE0A4BE949443C68D58620AA0D03CFED41D8B37A71C236` | `12B5CB9E5EDC3665B50BAF9C7B37D1743B5DB2C086C43A8DD2152586BD06A256` |
| 2022 | `9A9DA74D84B3831D490052B97382BC09ACB5C7E6F50C3D7B7895FCB1005DFEA0` | `6D07724D297CFA1EBB802DB88F19BE9E40C52850C7AC4A15A96DE486B208AD66` |

## 지적별 수정 확인

| 지적 | C6 | C7 직접 확인과 판정 |
|---|---|---|
| R3-03F1 지원 HTML 블록 경계 | 중첩div·문단 직후table·복수p 3실패 | 같은3문서가 태그 평문 없이 표시되고 표가 native Table로 생성됨. **종결** |
| R3-03F2 엔티티/완전한 정적 HTML | &copy;·full document 2실패 | © 텍스트와 body 표시, html 태그 평문 없음. **해당 재현 범위 종결** |
| R3-03F3 표 한도 생략 무안내 | 17열·129행 2실패 | 제한을 유지하면서 공통 생략 안내가 표시됨. **종결** |
| R3-03 문서 관리·저장·참조형 링크 | C6에서 주요 보완 확인 | C7 동일21사례에서 폴더/목록·현재 저장·rename 충돌/이탈·dirty 보존·삭제 거부/잠금·단일 삭제·참조3형식/로컬PNG 유지. **지적한 누락의 보완 확인** |
| SC024-02 삭제 확인 승계 | 다른 문서를 열어도 이전 확인 행 유지 | 세 SKU의 실제23검사에서 전환 후 확인 초기화 통과. 표시 당시 경로 결합도 고정 소스로 확인. **종결** |
| 좁은 문서 목록/버튼 겹침·스크롤 접근 | C6 UI9PASS/6FAIL | 각117검사에서320/480/900폭·Dark→Light→Dark·확인/취소 접근·휠 소비 통과. 실제 렌더 시각 대조. **종결** |
| R3-01 문서 상대 링크·R3-02 코드 색 | C4 수정 확인 | C7 호스트/Markdown 회귀226 통과로 유지 확인. 기존 수정 판정 유지 |
| R3-03F4 일반 Markdown 표 한도 | 이번 추가 사례 | 17열 Markdown 표의 마지막 열과 생략 안내가 모두 없음. **신규 미해소** |

HTML 수정은 [고정 TryHtmlBlock](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate7/source/src/VS2017.AIAssistant/Views/MarkdownRendering.Html.cs:41)의 중첩 수 계산·fragment wrapper·엔티티 변환과 [표 한도 검사](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate7/source/src/VS2017.AIAssistant/Views/MarkdownRendering.Html.cs:141)에 있다. 문단과 HTML의 경계도 StartsBlock에 반영됐다. 삭제 확인은 [pendingDeletePath와 확인 버튼](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate7/source/src/VS2017.AIAssistant/Views/MarkdownEditor.Documents.cs:68), 문서 열기/새 문서/저장 성공 시 ResetDocumentConfirmation 호출로 보완됐다.

## R3-03F4 / P2 — 일반 Markdown 표의 17번째 열 생략 안내 없음

HTML 표의 기존 지적은 수정됐다. 별도 native Markdown 표 경계에서는 동일한 종류의 표시 누락이 남아 있어 추가 검사했다. VS2017 고정 C7 DLL에서 [17열 Markdown 문서](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c7/markdown-17-columns.md)를 렌더하면 H1~H16/V1~V16만 표시되고 마지막 `MD_TAIL_SENTINEL`과 공통 생략 안내는 모두 없다. 원문은 유지된다.

[직접 실패 결과](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c7/markdown-limit-result.json)는0PASS/1FAIL·exit1이며, 기존21사례의 입력이나 기대를 변경하지 않은 별도 추가 검사다. 원인은 [Table/Cells](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate7/source/src/VS2017.AIAssistant/Views/MarkdownRendering.cs:350)의16열 절단에서 omitted가 설정되지 않는 것이다. 처리 상한을 유지하고 실제 초과 시 생략 사실을 표시해야 한다. 수정 포함 새 후보에서 한도 아래/동일/초과 및 기존 escaped pipe·inline code의 회귀를 확인한다.

## 동일 입력과 실제 실행 근거

독립 probe 소스 SHA256은 C6/C7 모두 `327929D52C52F4CDABFC9B8C5C6C89396A4FFAA1512D6DE6868928FC2B227F20`이다. 기대 조건을 바꾸지 않았다. 클래스명 AuditC6Probe는 비교를 위해 유지했으며 실제 입력 DLL은 위 C7 해시다. 로컬 이미지 시험의 고정 PNG도 C6/C7 바이트가 동일함을 확인했다.

| 직접 실행 | 결과 | 범위 |
|---|---|---|
| 동일 문서 probe | 2017/2019/2022 각각21/21 | C6 14PASS/7FAIL과 동일 입력/기대 조건 |
| 독립 파일/삭제/HTML 경계 | 세 SKU 각각23/23 | SourceOnly=false. 잠금/충돌/전환·경로/외부entity/XInclude/시험용loopback 관측 포함 |
| 문서 UI | 세 SKU 각각117/117 | SourceOnly=false, WindowShown=false, ActualIdeTested=false |
| 기능5그룹 | VS2017 226/226 | Markdown106/Hover35/Guide31/Account24/RootUi30 |
| 추가 Markdown 열 한도 | VS2017 0/1 | 새 R3-03F4 재현. 기존21사례와 별도 |

결과 원문은 `artifacts/audit-codexorig2-followup-c7/`의 `probe-{SKU}-result.json`, `probe-{SKU}-observations.json`, `security-{SKU}-result.json`, `document-ui-{SKU}-result.json`, `functional-result.json`에 보관했다. 실제 GUI/마우스 검사를 실행한 수치로 해석하지 않는다.

문서 UI 검사에서 Section 내부 Table을 찾도록 바뀐 테스트 전용 보정은 [diff](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c7/ui-harness-correction.diff)를 검토했다. 최상위 검색을 재귀 검색으로 바꿨으며 기존117개 기대/색상 검사를 삭제하지 않았다. 감사자는 보정된 사본으로 실행했고 root의 최초 실패는 별도 보존된다.

시각 대조: [320px Dark 삭제 확인](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c7/dark-320-confirmation.png), [480px Light](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c7/light-480.png). 두 그림은 실제 고정 제품 객체를 창 없이 렌더한 결과다.

## 처리 경계와 패키지

문서128Ki 문자·노드6000·scan-work8Mi·깊이8, HTML64Ki·512행·표128행16열의 제한을 유지한다. 이름 있는 엔티티를 문자로 변환하면서 DTD/외부resolver를 허용하지 않는다. 정적 body를 native WPF로 표시하며 CSS·script·iframe 실행이나 원격 이미지 자동 로딩을 지원한다고 주장하지 않는다. 실제23검사에서 시험용 비밀 파일 노출과 loopback 요청이 없었다.

세 DLL/VSIX 해시는 readiness와 일치하고, 각 VSIX 선택 핵심23파일도 Release 출력과 전부 일치했다. GIF5/PNG5, MIT LICENSE/고지, AvalonEdit·WebView2/loader·worker를 포함한다. [패키지 상세](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-followup-c7/package-integrity-c7.json)에 선택 목록과 전체 패키지 해시를 남겼다. 모든 ZIP 항목의 전수 검사라고 확대하지 않는다. 빌드6개 오류0·경고0는 TASK025의 결과를 확인한 것이며 감사자가 제품을 빌드한 것은 아니다.

기존 감사 및 설치/이미지 보호26파일을 재대조하고, 이전 Role 전체 바이트를 보존한 뒤 말미에 이번 확인 결과만 추가한다. C6의 실패 보고서·증거를 C7 성공 내용으로 덮어쓰지 않는다.

## 인계 및 남는 범위

기존 R3-03F1~F3, SC024-02, 좁은 문서UI는 검사한 C7에서 종결한다. 새 R3-03F4는 미해소로 인계한다. 파일 위치 열기는 코드 대조이며 탐색기 실행은 하지 않았고, 모든 HTML/CSS·브라우저 동작의 동일성을 보장하지 않는다.

마감 중 TASK025가 인계한 C7 [전체16묶음](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/tl/a54a19667b/summary.txt)은16/16 exit0, [실행기 자체 검사](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/ls/bcdaee3a4b/results.json)는15/15였다. 감사자는 원문을 읽어 확인했으며 직접 실행 수에 넣지 않았다. 실제3IDE와 물리hover/GIF 및 문서·배포 마감은 통합 담당자/TASK024의 별도 결과에 연결한다. 새 F4는 위 통과 후 추가한 사례다. 별도 공식 CLI 인증과 직접 ID/암호 요구 사이의 조사/대안 구분, 실제 Claude·운영 DB/TFVC/운영 배포의 기존 미검증 범위도 유지한다.

TASK024의 C7 [실제 GIF 창 검사](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task024/candidate7-playback/result.json)는24/24였다. 반면 [첫 물리hover 검사](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task024/candidate7-native/result.json)는18PASS 뒤 `original anchor click action still runs exactly once`에서1FAIL로 중단됐다. 나머지를 통과로 집계하지 않는다. 원인은 제품/하네스/입력 시점 중 아직 구분되지 않았으며, 팝업 소스가 C4와 동일하다는 사실만으로 성공 처리하지 않는다. 통합 담당자와 TASK024가 재현·원인·후속 결과를 남겨야 한다. 감사자가 직접 실행한 GUI 결과는 아니다.

감사 보고서·증거와 Role 말미 확인/반영 지시를 append-only로 마감해 TASK025에 인계한다. 최종 통합 결과·담당자 종료·원격 파일 해시의 마감은 통합 담당자 소유이며, 이 보고서만으로 전체 반영/배포 완료를 선언하지 않는다.
