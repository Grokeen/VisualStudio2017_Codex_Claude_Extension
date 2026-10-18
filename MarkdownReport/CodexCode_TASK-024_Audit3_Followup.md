# TASK-024F1 · 3차 감사 후속 독립 검증

- 담당: **CodexCode**
- 시작: **2026-10-19 02:41:26 KST**
- 상태: **C8 담당 검증 완료 — 기능231·각 SKU 독립23/테마31/번들·실제 hover28·GIF24 통과. 최초 C7 클릭 실패 원인 미확정 기록 보존**
- 기준: Role.md의 2026-10-19 02:39:16 3차 감사 후속 지시 1~5
- 소유: TASK025 제품·전체 통합, TASK026 문서 UI, TASK027 README·사용 안내, TASK028 독립 감사. 본 작업은 새 검사와 자체 보고만 편집한다.

## 담당 후속

1. R3-01/02 회귀를 포함한 새 제품 검사와 R3-03 보완의 실패·취소·경계 회귀를 정확한 DLL에 연결한다.
2. 같은 최종 후보의 기능·테마·필요 실제 hover/GIF 수명을 확인한다. 이전 C4 결과를 새 후보에 소급하지 않는다.
3. 동봉 GIF5·PNG5와 정지 그림, AvalonEdit MIT 라이선스가 원래 자산·runtime·VSIX에서 같은 바이트인지 확인한다.
4. 로그인 창·설치·상태 확인·실제 요청 성공 및 실계정/운영 연동 미검증을 구분한다. 제품/사용자 문서의 변경 담당은 각각 TASK025/027이다.

## 새 독립 검사

[Test-MarkdownAuditFollowup.ps1](../tests/task024/Test-MarkdownAuditFollowup.ps1)와 [MarkdownAuditFollowupChecks.cs](../tests/task024/MarkdownAuditFollowupChecks.cs)는 새 파일이므로 기존 MarkdownControlsChecks를 편집하지 않는다. 현재 23조건이다.

- 파일 잠금 중 현재 저장·이름 변경·확인 삭제가 실패해도 디스크·원문·dirty·선택·목록·이미지 기준을 보존한다.
- 이름 충돌에서 대상 파일을 보존하고, Unicode 이름 변경 후 현재 저장은 변경된 파일에만 반영한다.
- 폴더 선택은 열려 있는 초안을 바꾸지 않고, 문서 목록은 최상위 Markdown 256개로 제한한다.
- 확인 없음·새 초안·Dispose 뒤에는 파일을 임의로 변경하지 않는다. 확인 삭제는 해당 파일만 처리하고 undo를 정리한다.
- 다른 문서를 연 뒤에는 이전 삭제 확인 상태를 승계하지 않는다.
- 문서별 참조 정의와 렌더 당시 링크 기준을 격리하고, HTML 미리보기가 원문 저장 바이트를 바꾸지 않는다.
- HTML의 로컬 PNG 정상 사례와 상위 폴더 이탈 거부, DTD/XInclude의 로컬 비밀 미노출을 확인한다.
- HTML image/script/iframe/CSS에 loopback 주소를 넣고 창 없이 500ms Dispatcher를 관찰한다. 외부 인터넷에 접속하지 않으며 관찰 중 요청 유무를 명시한다.
- 위험 href와 깊고 큰 HTML의 경계도 확인한다. 부모는 자체 자식 실행 파일에 20초 제한을 둔다.

## 확인 결과와 보존한 실패

| 대상 | 결과 | 근거 (`artifacts/task024/` 아래) |
|---|---|---|
| 초기 source fixture | 21PASS/1FAIL | `audit-followup-source-first/result.json` |
| C6 2017 실제 DLL, 23번 추가 전 | 22/22 PASS | `audit-followup-candidate6-first/result.json` |
| C6 2017 실제 DLL, 삭제 전환 추가 | **22PASS/1FAIL** | `audit-followup-candidate6-delete-transition/result.json` |
| C6 2017 ZIP/runtimes/원본 미디어 | 10/10 파일·라이선스·DLL 일치 | `bundle-candidate6-first/result.json` |

초기 source fixture 실패는 `SourcePath`가 원래 상대 경로 문자열이라는 계약을 검사에서 절대 경로로 오인한 것이다. 실제 `AssetBaseDirectory + SourcePath`의 경로로 검사 기준을 바로잡았으며 원본 결과를 보존했다. 제품 수정으로 표현하지 않는다.

C6 2017 DLL SHA는 **`3C8083975BA43696C0946F54DD7D7B73759A0ABEEE6DEF9CD7FB3F04CDCCCD4A`**이다. C6는 UI·HTML 후속 전 기능 확인 후보이며 최종 릴리스 판정으로 사용하지 않는다. TASK028F1의 독립 HTML 추가 지적과 구분한다.

## SC024-02 · 문서 전환 후 삭제 확인 상태 승계

고정 C6에서 실제 `삭제` Button.Click으로 확인 행을 펼친 뒤 `TryLoadFile(다른 문서, 승인 callback)`을 호출하면 CurrentPath는 새 문서로 바뀌지만 이전 확인 행이 계속 Visible이다. 이 API는 도구 모음 파일 열기 동작에서도 사용한다. 확인 버튼은 현재 CurrentPath를 대상으로 동작하므로 이전 문서의 확인 상태가 새 문서에 남을 수 있다.

검사에서는 실제 삭제를 수행하지 않았다. 파일 내용·위치는 모두 검사 소유 폴더이며 사용자 문서·계정·OS 설정·GUI를 변경하지 않았다. 코드 실행이나 경로 이탈 결함으로 분류하지 않는다. 성공한 문서 식별자 전환에서 확인 상태를 초기화하고 취소·실패의 원문 보존을 유지하도록 TASK025에 전달했다. TASK026이 맡은 UI 배치 메서드는 편집하지 않았다.

TASK025가 성공한 문서 전환에서 확인을 초기화하고 확인 대상을 표시 당시 경로에 결합했다. 수정 소스 사본에서 23/23, 이어 **정식 C7의 세 SKU DLL에서 각각 23/23**을 확인해 SC024-02의 해당 회귀가 해소됐다. C6 최초22PASS/1FAIL은 보존한다. 현재 검사 동결은 `artifacts/task024/audit-followup-test-freeze2.json`이다.

## C7 정식 DLL 결과

| SKU | DLL SHA-256 | 독립 검사 | 원본/runtime/VSIX 미디어·MIT |
|---|---|---|---|
| 2017 | `9749270FFFA7053C2E4EC2FCCAF0D9DE2DEE09FA77FAD45228947CF3E064935F` | **23/23** | **미디어10/10·MIT·제품/Avalon DLL 일치** |
| 2019 | `A7A4A5AA714FF168BBEE0A4BE949443C68D58620AA0D03CFED41D8B37A71C236` | **23/23** | **미디어10/10·MIT·제품/Avalon DLL 일치** |
| 2022 | `9A9DA74D84B3831D490052B97382BC09ACB5C7E6F50C3D7B7895FCB1005DFEA0` | **23/23** | **미디어10/10·MIT·제품/Avalon DLL 일치** |

각 결과는 `artifacts/task024/candidate7-{year}-audit/result.json`, `input-receipt.json`, `candidate7-{year}-bundle/result.json`에 있다. SourceOnly=false, 입력·격리 사본 불변, 외부 네트워크·계정·사용자 문서·창 표시·공유 빌드0이다. 500ms loopback 요청 관찰은 각 실행0이었다. SKU 간 반복과 기본 회귀의 겹치는 항목을 합산한 고유 총계는 제시하지 않는다.

같은 C7 2017 DLL의 기존 5그룹은 **226/226**, Markdown106·hover35·안내31·계정24·응답 연결30이다. `artifacts/task024/candidate7-functional/summary.json`에서 전후 동일 SHA를 확인했다. 각 SKU 테마도 **31/31**, 결과 `candidate7-{year}-theme/result.json`이다. 실제 IDE 검사와 구분한다. TASK028의 C7 감사 및 TASK026 문서 UI117·TASK025 전체16과 세 IDE 통합은 해당 소유자가 진행한다.

## 로그인 범위와 원본 보존

`audit3-login-scope-review.json`에서 계정 UI 소스가 C6와 동일하고, 암호 입력 컨트롤이 없으며 상태 확인과 실제 모델 요청을 구분하는 안내가 유지됨을 확인했다. 현재 사용자 안내도 공식 창에서 ID/암호 입력과 실제 요청의 별도 확인을 설명한다. 기존 공식 문서 조사 범위를 재사용했으며 실계정 인증이나 직접 ID/암호 API의 보편적 부재를 증명한 것으로 표시하지 않는다. 사용자 문서는 TASK027 소유이며 본 작업에서 수정하지 않았다.

`audit3-preserved-originals.json`에서 기존 설치·감사·설명 자산 **26파일 SHA 불변**을 확인했다. 완료된 3차 감사와 C6 후속 보고·근거4개는 `audit3-completed-audit-hashes.json`에 기록해 본 작업에서 덮어쓰지 않는다. Role의 새 문서 부록은 기존 원문15·Orig3 제안·3차 지시와 구분해 읽었다.

## 최종 담당 검증 · candidate8

C7 이후 감사자가 발견한 일반 Markdown 표의17열 생략 안내 R3-03F4를 TASK025가 보완해 C8을 새로 고정했다. 본 작업은 이전 통과를 소급하지 않고 아래 결과를 다시 확인했다.

| SKU | C8 DLL SHA-256 | 독립 검사 / 테마 / 미디어 |
|---|---|---|
| 2017 | `A2A10A3922CC3FD47F5AA10E15FFF7FCB70A44494D4B26BEE22610B3C4C2172E` | **23/23 · 31/31 · 10/10** |
| 2019 | `45F4E4D06E4CCDBAF3FB28CE54E9ED3FB6B2DF46BE69F6E8B23F686741D06420` | **23/23 · 31/31 · 10/10** |
| 2022 | `06C41981F4D0049C07AC1BAA753E9A0DD3DC7FFB67BF385176F123D3A6419049` | **23/23 · 31/31 · 10/10** |

결과는 `artifacts/task024/candidate8-{year}-{audit,theme,bundle}/result.json`에 있다. 각 ZIP의 제품/AvalonEdit DLL 및 MIT 원본도 일치했다. C8 source120 목록 SHA는 `08AE13DE57637766A994868ACBDEB27A8F2FC587CD1786895EDCAD47FF7B0787`, 빌드 당시 Role SHA는 `DE232D862F2B59E069470776CA8AB0F2F54AE7460F426759D7698CB32181C828`이며 TASK025의 `candidate8-readiness.json`에 연결한다.

- **기능5그룹 231/231**: Markdown111·hover35·안내31·계정24·응답 연결30. `candidate8-functional/summary.json`, SourceOnly=false·전후 DLL SHA 동일.
- **실제 hover28/28**: native 태그24·합성2·구조1·수명1을 구분한다. `candidate8-native-diagnostic/result.json`, 실제 포인터·양방향 휠·pin·Escape·원래 클릭·unload 정리 확인.
- **실제 GIF 창24/24**: `candidate8-playback/result.json`, 생략0·AnimationAdvanceVerified=true,5GIF 프레임 진행/정지 PNG/접기·재열기·back·창 닫힘·Dispose 확인. 전체 loop 횟수 검사는 아니다.

03:13:51 TASK025의 GUI 인계 후 실행했고 **03:14:59 모든 검사창 종료·포인터 복원·GUI 예약 해제**를 통지했다. 후속 실제 IDE·전체16은 TASK025의 별도 통합 작업이다.

### 첫 C7 물리 클릭 실패와 진단 범위

첫 C7 물리 검사는18PASS 뒤 `original anchor click action still runs exactly once`에서1FAIL이었다. 실제 휠·고정·Escape까지 실행됐으므로 Screen-saver 전제 차단과 구분한다. 제품/하네스/입력 시점 중 최초 원인은 **미확정**이다. `candidate7-native/result.json`과 진단 원문을 보존했다.

조사에서 C4↔C7의 popup·diff presenter·테마 소스가 각각 동일함을 확인했지만 이것만으로 하네스 문제로 단정하지 않았다. `ChangeHoverChecks.cs`에 실제 Click/down/up, 입력 전후 cursor·HWND·capture·modifier 기록만 추가했다. **28개 기대값·대기 시간·SendInput 전송과 제품 소스는 그대로**다. 최초 검사 소스는 원래 동결 SHA `B7310B2DA3148A6C27F587E5CD0502D803F37ED19D078A5F698CC658FDDD6C28`와 정확히 일치하도록 복원해 `candidate7-native-first-source.cs`로 보존했다. 변경 영수증은 `candidate7-native-diagnostic-change-receipt.json`이다.

C8 관찰에서는 cursor176,82가 anchor 중앙 및 자기 HWND와 일치했고, modifier없음·capture없음 → Down Pressed → Up Released → Click0에서1 → capture해제 순서가 기록됐다. 같은 기존28조건이 모두 통과했다. **C8에서 해당 증상이 재현되지 않았다는 근거이며, 최초 C7 실패 원인이 증명됐거나 제품 수정으로 해소됐다고 표현하지 않는다.** 이 한계를 통합 담당자와 감사자에게 전달했다.

## 작업 시간·재현·인계

후속 시작 **2026-10-19 02:41:26 KST**, 마지막 담당 실행 검증 **03:14:26 KST**, 경과 **33분00초**다. 협업·빌드·GUI 인계 및 진단 대기를 포함한다. 보고·검증 JSON·Update_History·OWERORDER의 마감 시각은 별도로 남긴다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/task024/Test-MarkdownAuditFollowup.ps1 -AssemblyPath <고정 SKU DLL> -OutputDirectory <새 폴더>
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tests/task024/Test-GuideBundleIntegrity.ps1 -AssemblyPath <같은 DLL> -VsixPath <같은 SKU VSIX> -OutputDirectory <다른 새 폴더>
```

실제 GUI 검사는 단독 예약·Default desktop·전경/native 대상 확인 후 기존 `Test-ChangeHover.ps1 -UseNativePointer`, `Test-GuidePlayback.ps1`을 같은 DLL과 새 출력 경로로 실행한다. SourceFixture/CompileOnly는 제품 동작 성공으로 계산하지 않는다. 실행기 전체16에는 TASK025가 새 독립23을 연결했으며 전체 실행기 통과와 본 개별 결과를 중복 합산하지 않는다.

## 패키지 확인

[Test-GuideBundleIntegrity.ps1](../tests/task024/Test-GuideBundleIntegrity.ps1)는 VSIX를 추출하거나 제품 코드를 실행하지 않고 원본 동결 미디어10개·runtime 파일·동일 ZIP 항목의 SHA를 비교한다. GIF4프레임/PNG1프레임·600×420 디코드, 제품 DLL 일치, AvalonEdit DLL과 MIT 라이선스 원본 SHA를 확인한다. ZIP 중복 항목·읽기 상한도 검사하며 최종 후보의 각 SKU에서 실행할 예정이다.

## 마감 조건

본 담당의 새 검사·SC024-02 수정 확인·C8 기능/테마/번들/실제 입력/GIF 검증을 마쳤다. 보호 원본과 최초 실패는 보존하고 보고·검증 JSON·Update_History·OWERORDER에 연결한다. 전체16·실제3IDE·최초 C7 클릭 실패의 미확정 한계 수용 및 독립 감사 최종 판정·배포는 TASK025/028의 통합 범위다. 모든 담당자 종료와 감사 확인 전에는 배포하지 않는다.
