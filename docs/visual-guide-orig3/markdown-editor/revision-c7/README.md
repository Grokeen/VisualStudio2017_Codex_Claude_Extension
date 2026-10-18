# Markdown 작성기 사용 설명서 · C7 검토 개정

이 폴더는 기존 TASK-029 검토판을 보존한 뒤 candidate7 화면과 감사 결과를 반영한 별도 개정입니다.

- `Markdown작성기_사용설명서_C7.md`: 10개 그림을 포함한 Markdown 매뉴얼.
- `images/01`~`07`, `10`: 설명용 도식.
- `images/08`~`09`: VS2017 candidate7 Release DLL의 작성기 컨트롤을 별도 STA 프로세스에서 렌더한 화면 예시. 창을 표시하거나 실제 IDE를 실행하지 않았습니다.
- PDF: `output/pdf/Markdown작성기_사용설명서_C7.pdf`.

C7의 3개 SKU 문서 UI 검사는 각각 117/117, 독립 HTML 후속 검사는 21/21 통과했습니다. 최초 UI 검사 105/108은 중첩 Section 안의 표를 찾지 못한 검사 전제로 실패했으며, 검사기를 재귀 조회로 수정한 뒤 다시 확인했습니다. 최초 실패와 결과는 보존했습니다. 실제 IDE 통합 검증은 이 문서의 캡처 범위에 포함되지 않습니다.

대표 화면의 VS2017 Release DLL SHA-256: `9749270FFFA7053C2E4EC2FCCAF0D9DE2DEE09FA77FAD45228947CF3E064935F`.
