# TASK069 사용자 문서 동결 · CodexOrig3

작성: 2026-10-20 (KST) · 문서 소유: README, `docs/USER_GUIDE.md`, v0.14.0-preview.1 사용자 PDF와 이미지 provenance

## 사용자 문서 반영

- README의 현재 설치·다운로드 안내를 v0.14.0-preview.1로 갱신했다. 통합 ZIP, 44쪽 PDF, 오프라인 설치기, 간단 ZIP, VS2017 15.7/15.9+, VS2019, VS2022 VSIX 링크는 Root가 승인한 계획 Release URL과 파일명을 사용한다.
- 최신+직전 버전 보존 정책을 v0.14.0/v0.13.0으로 바꾸고, v0.12.1의 첨부·로컬 패키지는 새 자산과 체크섬 게시 뒤 정리 대상으로 명시했다. v0.13.0 본문은 역사 기록으로 남겼다.
- USER_GUIDE의 v0.14.0 절은 같은 Release PDF/FullOffline 자산과 보존 정책을 가리킨다. v0.13.0 절과 34쪽 PDF는 구버전 기록으로 구분했다.
- 공개 README/USER_GUIDE에서 C8의 내부 검사 횟수는 제거했다. #18은 사용자가 DBG로 중단 위치와 열린 소스 문맥/호출 스택을 기존 초안에 넣고 확인 후 직접 전송하는 절차, 열린 문서·16,000자 원자 제한·실패 시 초안 보존, 값·식 평가/예외 추정/디버거 제어/자동 전송 제외만 안내한다.
- #17은 실제 Visual Studio 테마 리소스에 들어간 색상 범례(`#BD93F9`, `#7586B5`, `#FF79C6`)와 확인 경계를 안내한다. 실제 포인터 hover나 오류·중단점 marker가 스크롤 표면에 그려지는 것으로 오해하지 않게 했고 IDE 설정을 자동 변경하지 않는다고 밝혔다.

## 그림 및 개인정보

- C8 anonymized synthetic-reader WPF 캡처 6장을 `docs/assets/ui-guide-v0.14.0/candidate8/`에 원본 PNG 그대로 복사했다. synthetic identity는 `사용자 · Codex`, 파일명은 `DebugSample.cs`이다. 실제 IDE 화면, 실제 사용자/프로젝트 정보, 전송/AI 결과가 아니다.
- provenance: `artifacts/task069/orig4/candidate8-debug-helper-pass3/capture-result.json`, `manifest.json`. 기준 DLL SHA256 `F821132D6DD4225F98853A68F9154F692432437041AE59EE01512F7B564F9DB2`, assets SHA256 `E4E14C7C98ECD9D2AF9A56CE9DA66A3A04482916D65E0FED484E418447C43AA2`. 복사한 PNG 6장의 원본/대상 SHA가 각각 일치한다.
- #17 실제 host resource 검사 보고는 `MarkdownReport/CodexOrig_TASK-069_MenuRuntime.md` 및 `.json`. 범위는 고정 C5 DLL + final4 theme private probes의 VS2017/2019/2022 resource/contrast checks이며, 물리 hover와 editor marker 소비·가시성은 미검증이다. 문서의 범위를 이 한계와 맞췄다.
- #18 isolated VS2017 통합 근거는 `artifacts/ide/run-a02c6f22d94a450c894a9de282bfde4f/result.json`, `task069-debug-context.txt`, `task069-debug-observation.json`. 격리 시험은 실제 DTE frame/source/stack을 확인했지만 시험용 debuggee를 사용했으며 고객 프로젝트·실제 Claude 로그인·AI 응답은 포함하지 않았다.

## PDF 동결본 검토

- 출력: `output/pdf/VisualStudio_AI_Assistant_설치_사용설명서_v0.14.0_미리보기.pdf`
- 용량: 2,162,148 bytes · SHA-256: `ec3d06f5ed2443d6bc7ca18a92b5422c9dafa7cff3cf037539e8c295e3b9407d`
- 총 44쪽: 원본 v0.13.0 34쪽 + v0.14.0 안내 10쪽. `pypdf` content stream 비교에서 원본 34쪽 모두 동일했다.
- 신규 쪽 35–44를 Poppler로 PNG 렌더 후 시각 검토했다. #17 색상 범례와 미검증 경계, #18 설명·사용 흐름·합성 화면·읽기 오류/글자수 초과 입력 보존 그림을 포함한다. 페이지 넘침, 잘림, 겹침 및 callout 고아 페이지를 정리했다.
- README 이미지 참조 20개/누락 0개, USER_GUIDE 32개/누락 0개; 예정 Release asset URL/파일명은 Root 승인 목록과 일치한다. `git diff --check` 통과.

## C9 인계

이 파일과 v0.14.0 PDF/문안은 문서 freeze 기준이다. Root가 최종 C9 package에 `docs/USER_GUIDE.md` 및 PDF를 동기화하고, package 내 PDF 자산명·바이트·SHA 일치 검사를 마친 뒤 release manifest에 연결한다. 실제 게시, 다운로드 SHA, 구버전 첨부 정리는 Root 통합 범위다.

## Role #19 후속 문서 반영

Role #19의 대화 가장자리 여백 요청에 따라 README, 사용자 안내서 및 PDF의 후보5 대화 화면 설명에 한 문장을 추가했다. Orig4의 최종 source freeze에서 좌우 12~20px 여백으로 확인되어 안내 수치도 일치시켰다. PDF는 기존 44쪽 구조와 v0.13.0의 34쪽 본문을 유지하며 이미지나 기능 설명 범위를 넓히지 않았다. 새 캡처와 제품 XAML 변경은 담당 개발자의 별도 작업이다. 이 문서의 위 SHA는 이전 freeze 스냅샷을 가리킨다; 최종 PDF의 새 바이트·SHA와 재검증 결과는 아래에 추가한다.

## Role #19 후속 검증 결과

- 기존 freeze 이후 문서와 v0.14.0 PDF/내장 PDF 자원을 함께 갱신했다. 최종 문구는 “대화 폭에 따라 좌우 12~20px 여백을 두어 가장자리와 본문을 구분합니다.”이며 README, `docs/USER_GUIDE.md`, 44쪽 PDF의 같은 화면 설명에 반영했다.
- 새 PDF는 44쪽이며 앞의 v0.13.0 34쪽 콘텐츠 스트림은 모두 원본과 일치한다. 부록 3쪽(문서 37쪽)을 900px 높이 PNG로 다시 렌더링해 확인했다. 여백 안내는 대화 캡처 아래, 계정 인증 안내 위에 한 줄로 들어가고 겹침·잘림이 없다.
- 최종 PDF: 2,162,704 bytes · SHA-256 `e7873afeec045f24ca68392ebf0b49510ff700fd2e05a3a5fd25536596af0538`. 내장 `Assets/CliGuide` PDF도 같은 바이트와 SHA로 갱신했다. README의 18쪽 CLI 설치 PDF는 별도 문서이므로 변경하지 않았다.
- PDF의 새 캡처는 추가하지 않았다. 기존 후보5 합성 대화 이미지 및 실제 IDE 캡처가 아니라는 설명을 유지했다. 제품 XAML 수정이나 빌드는 수행하지 않았다.

## R69-12–14 공개 안내 최종 정정 및 동결 (2026-10-21)

- `docs/USER_GUIDE.md`에서 대화 말풍선을 사용자=작성란과 같은 배경색, AI=조금 더 진한 배경색으로 정리했다. 전송 아이콘은 현재 대화에 실행·대기 요청이 모두 없는 유휴 상태에만 표시한다고 설명하고, 해당 대화에서 실행·대기 요청이 있으면 중지 아이콘을 표시한다고 맞췄다. 작업 기록 상세는 클릭 또는 Enter로 열며 마우스 오버만으로 열리지 않는다고 정정했고, 실행기 기본 검사 수를 27개로 갱신했다.
- `docs/OFFLINE_INSTALL.md`는 v0.14.0-preview.1 FullOffline ZIP을 현재 기준으로 명시하고, v0.13.0을 이전 버전 기록으로 표시했다. 첨부 보존 기준을 v0.14.0+v0.13.0으로 고치고, 패키지에 IDE 테마가 포함됨을 안내했다. “공개 패키지 미확인” 문구는 네 SKU 빌드/격리 IDE 확인과 고객 PC 미검증 범위를 설명하는 문장으로 바꿨다. `releases/v0.14.0/OFFLINE_INSTALL.md`는 source와 byte-identical이다.
- 100개/4MB 보관 설명은 v0.12.0 공개 UI 절의 과거 버전 맥락으로 유지했다. 현재 절의 메인 Markdown 최신 20개와 같은 ID의 `.history` 전체 백업 안내와 구분된다.
- 읽기 감사에서 현재 README의 129행과 v0.14 PDF 최신 부록 38쪽에도 유휴(send)/대기(stop) 라벨이 애매한 문구로 남아 있음을 발견해 Root에 보고했다. README는 Root 소유이며, 본 요청의 PDF 편집 금지에 따라 PDF는 수정하지 않았다. README·PDF의 최종 조치는 Root final4 동결 전에 반영·결정해야 한다.
- `git diff --check` 통과. 이 문서담당의 요청 범위는 동결했으며 Orig3는 추가 문서 변경을 하지 않는다. Root가 공개 최종 포장·체크섬·Release/실제 게시를 마감한다.

## PDF 38쪽 유휴/중지 상태 문구 최종 보완 (2026-10-21)

Root의 최종 보완 요청으로 PDF 부록 4/10(문서 38쪽)의 상태 설명만 수정했다. “실행·대기 요청 없음(유휴)”일 때는 돋보기 보내기 버튼, “현재 대화의 실행·대기 요청 있음” 상태에서는 중지 버튼만 보인다고 명확히 했다. 해당 설명 외 이미지는 바꾸지 않았다.

- 총 44쪽이며 v0.13.0 원본 첫 34쪽 content stream은 모두 동일하다. C11 배포 직전 PDF와 비교할 때 추출 텍스트가 달라진 페이지는 38쪽 하나뿐이며, 39–44쪽은 동일 해상도 PNG 렌더 픽셀 비교에서 모두 완전히 동일하다. 모든 페이지의 이미지 stream payload도 유지됐다.
- 38쪽을 1000px 높이로 렌더링해 버튼 상태와 첨부 화면 설명의 줄바꿈·겹침·잘림을 확인했다. 기존 이미지 3개와 크기는 그대로다.
- 최신 PDF와 `src/VS2017.AIAssistant/Assets/CliGuide` 내장 PDF는 2,162,985 bytes, SHA-256 `124285df5e7734917b5888e3c6d7868801234626d9cb7653c4e0142b1383fa35`로 byte-identical이다. Release 폴더 복사본은 수정하지 않았다. Root가 이 해시로 C12 최종 재빌드·패키징을 마감한다.
- 이전 기록에서 이 문구를 미수정 상태로 표시했으나, 위 요청에 따라 해결했다. 이 변경 뒤 Orig3 담당 문서와 PDF를 최종 동결하며 추가 편집하지 않는다.
