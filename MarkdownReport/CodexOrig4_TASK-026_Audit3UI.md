# TASK-026 3차 감사 후 문서 UI 보완

담당: CodexOrig4. Role.md의 3차 감사 지시와 요청15를 연결하여, 새 문서 관리 화면의 Light/Dark 통일성과 좁은 폭의 접근성을 검토했다. R3-03의 파일 기능·HTML 파서 구현은 TASK025, 독립 파일·보안 회귀는 TASK024의 소유를 유지했다.

## 수정

고정 candidate6 제품을 480×600으로 렌더했을 때, 170 높이로 제한된 문서 목록의 DockPanel에서 목록과 이름 입력란이 겹치고 하단 파일 동작이 잘렸다. 색상만 검사한 최초 9조건은 통과했지만, PNG 직접 검토와 겹침 회귀에서 제품 결함을 확인했다.

TASK025의 명시적 파일 인계 후 `MarkdownEditor.Documents.cs`의 `InitializeDocumentBrowser`만 수정했다. 기존 패널을 세로 스크롤로 감싸 이름 변경·파일 위치·삭제 확인·취소까지 접근할 수 있게 했다. 폴더 조회·저장·이름 변경·삭제 API, 확인 규칙과 보안 검증은 변경하지 않았다. 기존 주석을 보존하고 UTF-8 BOM으로 저장했다.

공통 스타일을 조사한 결과 문서 목록과 Expander는 양 모드의 기존 자원을 정상 상속하므로 `AssistantTheme.xaml`은 변경하지 않았다. 편집기의 고정 SlateGray 구분선은 TASK025가 기존 `AssistantBorderBrush` 동적 자원을 사용하도록 수정했다.

수정 파일 SHA256: `E9DE88E3D4164989E2775989BB805665A44E22824A5317459A5B6DBDD678BFFD`. [동결 목록](../artifacts/task026/audit3-ui-freeze.json).

## 검증

| 범위 | 결과 | 근거 |
|---|---|---|
| C6 실제 제품의 최초 색상 위주 검사 | 9/9, 시각 결함 발견으로 UI 전체 합격 판정하지 않음 | `artifacts/task026/audit3-documents-baseline` |
| C6 실제 제품의 겹침·스크롤 회귀 | 9 PASS / 6 FAIL | `artifacts/task026/audit3-documents-clipping-repro/result.json` |
| 수정 소스 독립 컴파일·1차 배치 | 18/18 | `artifacts/task026/audit3-documents-layout-source1/result.json` |
| 수정 소스 독립 컴파일·확대 검사 | 111/111 | `artifacts/task026/audit3-documents-layout-source2/result.json` |
| 파일 목록 위 휠 전달까지 포함한 최종 소스 검사 | 117/117 | `artifacts/task026/audit3-documents-layout-source3/result.json` |

확대 검사는 Dark → Light → Dark의 동일 객체 전환, 320/480/900 폭에서 목록·입력란 비중첩, 각 파일 동작과 삭제 확인/취소의 BringIntoView 접근, 취소 후 파일·현재 경로 보존, 이미 렌더된 HTML 표·코드·링크의 색상 갱신을 검사한다. 마지막 6조건은 좁은 화면의 파일 항목에 전달한 합성 휠 이벤트가 바깥 문서 스크롤을 실제 이동시키는지 확인한다. 물리 마우스 검사는 아니다. Light/Dark의 넓은 화면과 320 폭 삭제 확인 PNG도 직접 열어 검토했다.

소스 검사는 동결한 실제 Markdown UI/렌더러/팔레트 소스를 별도 csc로 컴파일하고, 지정 C6 제품의 테마 BAML·스크롤 정책을 사용한다. `SourceOnly=true`이며 최종 제품 DLL 성공으로 합산하지 않는다. 모든 실행은 별도 STA 프로세스이며 실제 창·마우스·IDE·사용자 파일·네트워크를 사용하지 않았다. 최초 실패와 원래 PNG는 보존했다.

실제 최종 후보 검사는 다음 명령으로 연결한다. `-SourceOnly`를 지정하지 않는다.

```powershell
powershell.exe -NoProfile -NonInteractive -STA -ExecutionPolicy Bypass -File tests/task026/Test-DocumentUi.ps1 -AssemblyPath <고정_제품_DLL> -OutputDirectory <새_출력_폴더>
```

후속 제품의 정확한 DLL 해시와 검사 결과는 확인 후 아래에 추가한다. 기존 테마 31조건·전체16묶음·실제 세 IDE 및 감사자의 HTML 후속 확인은 TASK025가 통합하며, 이 UI 수정의 성공으로 전체 이식·배포 완료를 선언하지 않는다.
