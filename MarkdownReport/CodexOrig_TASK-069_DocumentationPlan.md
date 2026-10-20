# TASK069 사용자 안내·캡처 준비안

작성자: CodexOrig
기준: `MarkdownReport/Role.md`의 2026-10-20 12:47 추가 요청 #1–10 및 TASK069 담당 분리 메모
상태: 후보/전용 설치기 확정 전 준비 문서. README, 사용자 안내서, 오프라인 설치 안내, PDF, 이미지 자산은 이번 단계에서 수정하지 않음.

## 문서 반영 원칙

- v0.13.0 사용자 안내와 배포 PDF는 보존한다. v0.14.0-preview.1의 동결 후보와 설치기 흐름이 확인된 뒤 별도 안내서/PDF를 만든다.
- 실제 동작을 확인한 뒤에만 사용자 절차를 적는다. off-screen 또는 합성 렌더는 실제 Visual Studio 캡처로 표기하지 않는다.
- 실제 계정 로그인, 실제 AI 추론, 고객의 Visual Studio 15.7.1/제한망 설치, 물리 포인터 조작은 수행한 증거가 없으면 검증됐다고 쓰지 않는다.
- 화면 캡처에 사용자명, 경로, 프로젝트명, 계정, 토큰, 실제 대화 또는 내부 검증 식별자가 노출되지 않도록 예시 데이터로 대체한다.
- 숨김 필수 instruction과 작업자 간 협업 파일의 내용·경로는 사용자 문서에 노출하지 않는다. 사용자가 알아야 하는 자동 관리 결과와 사용자 지침 보존만 설명한다.

## Role 요구 #1–9의 사용자 문서 영향

| Role | 문서에서 다룰 내용 | 검토할 문서 | 필요한 시각 자료·근거 |
|---|---|---|---|
| #1 AI 모델 간 메시지 전달 | 실제 전달 기능이 완성·검증되면 대상 AI/전달 상태/사용 절차를 설명한다. 연결 경로가 없거나 전달에 실패하는 상태라면 협업 기능으로 표현하지 않고 사용 가능한 현재 흐름과 한계를 적는다. | `docs/USER_GUIDE.md`, README 기능 요약, 필요 시 문제 해결 안내 | 송신 모델과 수신 모델, 전달 전·후 상태를 보여주는 예시 화면. 실제 전송 성공 증거가 없으면 합성 예시로 명확히 표기. Role image-67의 개인·세션 값은 사용 금지. |
| #2 현재 설정·모델 상태 확인 | 현재 선택된 제공자/모델/추론 설정을 어디서 확인하는지, 화면에 보이는 값과 실제 요청에 쓰이는 값의 구분을 설명한다. | `docs/USER_GUIDE.md` 설정 절, README 요약 | 설정 패널 전체와 좁은 폭 화면. 선택값 잘림 없이 표시하고, 상태 표시가 실제 적용 설정과 일치하는 테스트 근거가 필요. Role image-69는 잘림 사례로만 참고. |
| #3 작업 기록은 클릭으로 표시 | 작업 기록 버튼을 클릭해 상세를 열고 다시 닫는 동작을 설명한다. hover만으로 열리는 것으로 안내하지 않는다. | `docs/USER_GUIDE.md` 작업 상태/기록 절, 필요한 경우 README 변경 요약 | 기본 상태와 클릭 후 상세 상태의 나란한 PNG 또는 짧은 GIF. GIF는 클릭 지점/열림·닫힘이 또렷하고 실제 입력 방식이 검증된 경우에만 사용. Role image-70은 요구 원본. |
| #4 정규화 메시지 | 정규화가 자동 처리되고 알림을 숨기는 동작이 확정되면 사용자는 별도 확인을 하지 않아도 된다고 간단히 기술한다. 파일 내용/줄끝 보존 범위나 실패 시 동작은 검사 결과에 맞춰 쓴다. | 문제 해결 안내 또는 `docs/USER_GUIDE.md` 저장·요청 절 | 정상 작업 중 불필요한 팝업이 없다는 화면 증거와 줄끝 정규화 경계 검사 결과. 사용자 팝업 캡처는 정규화 경고가 여전히 노출되는 회귀가 있을 때만 추가. Role image-71은 재현 참고이며 개인 경로를 가린다. |
| #5 멈춤·원인 분석 | 멈춤이 재현·분석된 경우에만 사용자가 할 수 있는 안전한 복구와 진단 자료 위치를 추가한다. 로그 전달 전 개인정보·토큰 제거 방법을 안내한다. 원인이 확정되지 않으면 원인을 단정하지 않는다. | `docs/INSTALL_TROUBLESHOOTING.md`, 필요 시 `docs/USER_GUIDE.md` 문제 해결 절 | 오류 재현·복구 절차의 깨끗한 캡처 또는 진단 파일 위치 도식. Role image-72의 실제 IDE/프로젝트/사용자 경로는 배포 문서에서 재사용하지 않는다. |
| #6 Dracula 스크롤바 | 실제 수정 후보에서 세로 스크롤바가 표시되고 끝까지 이동하는지 확인한 뒤 팔레트/스크롤 확인 절차를 설명한다. | `docs/USER_GUIDE.md` 테마 절, 필요 시 릴리스 노트 | Dracula 화면의 위쪽·아래쪽 스크롤 위치(스크롤 thumb 포함), 가능하면 실제 설치 IDE 캡처. 합성 이미지면 합성임을 명기. Role image-73은 회귀 사례. |
| #7 Kakao 영역 대비 | 탭, hover, 선택, 본문 영역 사이의 대비가 각 상태에서 읽히는지 설명한다. 테마 이름만 열거하지 말고 실제 구별 상태를 확인한다. | `docs/USER_GUIDE.md` 테마 절 및 README 테마 요약 | Kakao 테마의 기본/hover/선택 탭과 본문이 함께 보이는 이미지. 밝기/색약 대비 확인 결과가 없으면 접근성 합격 표현은 금지. Role image-74는 회귀 사례. |
| #8 오프라인 전용 설치기 | 설치기가 감지하는 Visual Studio 목록, 대상 선택, 확장·IDE 테마 설치 순서, 오프라인 검사/완료 결과, 재시작 또는 추가 선택 필요 여부를 실제 구현에 맞춰 설명한다. | `docs/OFFLINE_INSTALL.md`, `docs/INSTALL_TROUBLESHOOTING.md`, README 설치 요약, 새 v0.14 PDF | 설치기 시작, 감지된 IDE/버전 선택, 오프라인 패키지 검증, 설치 진행, 최종 결과의 단계별 화면. 오류·대기 상태도 별도 캡처. 실제 인터넷 차단 시험과 로그 경로·결과 상태를 확인한 뒤 사용. Code 담당 설치기 후보 동결 전에는 캡처·절차를 확정하지 않는다. |
| #9 숨김 필수 instruction·협업 파일 관리 | 필수 지침은 설치/업데이트 때 공식 구형 1–5 조항에서 최신 1–8로 자동 갱신되고 사용자 수정 사본과 별도 지침 원문은 보존된다고 안내한다. AI 간 협업 파일은 기본적으로 사용자에게 공개하지 않으며 사용자가 명시적으로 협업/파일 제공을 요청하면 예외다. 연결된 SCM과 2명 이상 참여가 확인된 경우에만 오래되고 수정 이력이 없는 위치에 협업용 폴더를 만들고, 다른 기기의 AI와 소통할 때 사용하며 문제·해결 제안을 공유하는 communication 공간을 활성화한다. 이 규칙을 자동 SCM commit/push 기능으로 설명하지 않는다. | README 기능 요약, `docs/USER_GUIDE.md` 사용자 지침·협업 절, `docs/OFFLINE_INSTALL.md` 설치 후 참고 | 설치/업데이트 후 기존 사용자 지침 보존 흐름을 설명하는 도식. 협업 폴더 생성은 SCM 연결/참여자 조건이 확인된 합성 예시만 가능하며 실제 협업 파일이나 개인 경로를 노출하지 않는다. |
| #10 TFS/TFVC 명령 | 기존 get/checkout/checkin 흐름과 Visual Studio 설치에서 PATH 없이 `tf.exe`를 찾는 지원 범위를 안내한다. 필요한 Visual Studio/Team Explorer 또는 TFVC 명령줄 구성 요소, 서버 연결 주소, 작업영역 및 인증 요건은 최종 후보의 실제 탐지·실행 계약과 공식 공급자 안내를 대조해 적는다. 로컬 fixture 통과는 실제 TFS 서버 통신 성공으로 표현하지 않는다. | `docs/USER_GUIDE.md` 형상관리 절, `docs/INSTALL_TROUBLESHOOTING.md`, 필요 시 `docs/OFFLINE_INSTALL.md` 필수 구성 요소 표 | 공급자/명령/탐지 경로와 필요한 구성 요소를 보여주는 안전한 상태 화면. 실제 서버 계정/작업공간/프로젝트 이름을 캡처하지 않는다. 실서버 시험이 없으면 “실제 TFS 서버 연결 미검증”을 표시한다. |

### TFS 문서 확정 전 확인 사항

- 공식 TFVC CLI 안내는 Visual Studio가 설치되어 있고 Azure DevOps 프로젝트 접근 권한 및 로컬 작업영역 매핑이 준비되어야 하는 기본 흐름을 설명한다. VS2019+에서는 `tf.exe`가 고정 경로에 있지 않을 수 있으므로 매뉴얼은 설치 경로를 하드코딩하지 않고 이 확장 기능이 탐지하는 범위에 맞춘다.
- 현재 제품 wrapper는 `tf.exe`를 숨김 프로세스로 실행하며 표준 입력/출력을 리디렉션한다. 따라서 사용자가 실행 중 자격 증명 프롬프트에 응답할 수 있다고 안내하지 않는다. 최종 후보에서 인증이 Visual Studio/TFVC 클라이언트에 저장된 인증을 사용하는지, 인증 만료·미설정일 때 어떤 오류/복구가 가능한지 확인한다.
- get, checkout, checkin은 프로젝트 workspace mapping, 서버 주소/컬렉션, 네트워크, 작업별 TFVC 권한의 영향을 받는다. 실제 TFS/Azure DevOps Server에서 사용자가 확인할 단계와 필요한 권한은 현장 서버 시험 전까지 특정 UI 절차로 단정하지 않는다.
- 제품의 로컬 native fixture가 명령줄과 한글 인자를 확인한 사실은 명령 전달 시험 근거로만 기록하고 실제 TFVC 서버 로그인/권한/파일 반영 성공과 분리한다.

## 캡처 패키지 제안

후보가 고정되면 `docs/assets/ui-guide-v0.14.0/candidate1/` 아래에 필요한 캡처를 별도 보관하고 `manifest.json`에 후보 DLL/설치기 해시, 캡처 방법, 날짜, 실제 IDE/합성 여부, OS 포인터 사용 여부, 가림 처리 내용을 기록한다. v0.13 자산은 덮어쓰지 않는다.

최소 캡처 목록:

1. 작업 기록 닫힘/클릭 열림 (PNG 2장 또는 기능이 자연스럽게 보이는 짧은 GIF 1개).
2. Dracula 스크롤 thumb가 보이는 화면, 위·아래로 이동한 화면.
3. Kakao 테마의 탭 기본·hover·선택 및 본문 대비.
4. 오프라인 전용 설치기의 IDE 감지/버전 선택, 패키지 사전검사, 설치 결과 및 실패 복구 화면.
5. #1 모델 메시지 전달은 실제 전달 성공/실패 상태를 구분하는 예시. 기능이 완성되지 않으면 캡처하지 않고 문서상 미지원/제한으로 남긴다.
6. #2 현재 모델 설정 화면과 실제 요청에 적용되는 모델이 일치하는지의 검증 기록.
7. #4 정규화 알림 숨김 회귀 확인, #5 문제 해결 자료는 사용자 정보 제거 후 도식 또는 안전한 예시 로그.
8. #9 필수 지침 자동 갱신/사용자 지침 보존 흐름도와 SCM 2명 이상 확인 시 협업 디렉터리·communication 공간 조건. 자동 commit/push가 아님을 분명히 하고 숨겨진 지침/협업 파일 자체는 캡처하지 않는다.
9. #10 TFS/TFVC 동작 전 `tf.exe` 자동 탐색, 필요한 구성 요소 안내, 인증/권한 문제와 연결 실패 복구 화면. 실제 사용자명·서버·작업공간은 가린다.

## 진행 순서와 동결 조건

1. Code의 #8 설치기와 Orig4의 #6/#7 화면 후보가 공유되면 범위·설치 순서·설정 문자열을 읽기 전용으로 확인한다.
2. Root가 최종 통합 후보와 검사 근거를 동결한 뒤에만 그 후보를 사용해 화면 자료를 생성한다. 실제 UI 사용 순서를 보여주는 GIF/화면 녹화는 실제 포인터 절차를 별도 검증한 경우에만 제작한다.
3. 화면 및 기능별 증거를 확인한 뒤 README/USER_GUIDE/OFFLINE_INSTALL/TROUBLESHOOTING의 변경안을 작성하고, 문구와 그림 설명이 모두 같은 릴리스를 가리키는지 대조한다.
4. 기존 v0.13.0 PDF를 교체하지 않고 `VisualStudio_AI_Assistant_설치_사용설명서_v0.14.0_미리보기.pdf`를 별도로 생성한다. 페이지 렌더링 및 캡처 출처를 확인한 뒤 Root에 인계한다.

이번 담당 범위는 #1–10의 사용자 설명·시각 자료 준비다. TFS 필요 구성 요소·인증 방식은 현재 공유된 로컬 fixture 결과만으로 확정할 수 없으므로 final candidate와 공급자 문서 확인 뒤 확정한다. C8 별도 미완료 문서가 있다면 이번 입력에 섞지 않는다.

## 기술 참고 문서

- [Microsoft Learn: Use Team Foundation version control commands](https://learn.microsoft.com/en-us/azure/devops/repos/tfvc/use-team-foundation-version-control-commands?view=azure-devops) — TFVC 명령 실행과 VS2019+ `tf.exe` 경로가 고정되어 있지 않다는 안내.
- [Microsoft Learn: Set up Team Foundation Version Control on your dev machine](https://learn.microsoft.com/en-us/azure/devops/repos/tfvc/set-up-team-foundation-version-control-your-dev-machine?view=azure-devops) — Visual Studio, 프로젝트 접근, workspace 생성/매핑 및 코드 가져오기 절차.
- [Microsoft Learn: Checkin command](https://learn.microsoft.com/en-us/azure/devops/repos/tfvc/checkin-command?view=azure-devops) — checkin 기능과 기본 TFVC 권한 선행 조건.

## 2026-10-20 22:26 KST - 문서·PDF 작업본 상태

README, USER_GUIDE, OFFLINE_INSTALL과 v0.14 전용 PDF 부록 초안을 C3 후보에 맞춰 작성했다. PDF는 기존 v0.13.0 34쪽 content stream을 바꾸지 않고 8쪽을 붙여 42쪽이다. 앞 34쪽 content stream은 원본과 모두 동일함을 확인했다. TASK069 C3 제품 DLL의 숨겨진 WPF 렌더에서 나온 작업 카드/상세 dark·light 네 이미지를 실제 IDE 캡처와 구분해 문서에 포함했다. Pending/Replied/Delivered의 메시지 상태 의미와 명시적 읽기 전용 응답 절차도 반영했다.

이것은 사용자 검토용 draft이며 최종 freeze가 아니다. Root 지시에 따라 Role.md #11–13 조사/검증 결과와 Orig4 message-center 캡처 handoff를 받은 뒤 해당 내용과 그림을 통합하고 재렌더·최종 검증한다. 실제 Claude 로그인/AI 응답, 실 TFS 서버, 고객 VS2017 Professional 15.7.1 및 별도 고객 오프라인 PC 흐름은 미검증으로 유지한다.

## 2026-10-20 22:44 KST - 새 요구사항·문서 동결 조건

Role.md #14–15가 추가되었다. 문서에는 대기/실행 중 입력 버튼의 단일 상태와 실행 중 입력 보존 동작, 사용자 말풍선과 작성란 배경 일치, AI 답변의 약간 더 진한 배경을 추가할 예정이다. Root C4 확인에 따라 #12는 속도 수치 없이 같은 내용/팔레트 재사용으로 재표시 부담을 줄인다고만 기술하고, #13은 TXT 배지/전체 파일명 tooltip/첨부별 문자·UTF-8 크기와 전송 경계를 설명한다. 100개 아이디어 또는 내부 검사 수치는 사용자 문서에 노출하지 않는다.

현재 Orig4 C3/C4 캡처에는 개인 계정명 `ygree`가 렌더되어 있어 재사용하지 않는다. C5의 익명 표시명 캡처와 Root 최종 검증 후 README, USER_GUIDE, OFFLINE_INSTALL, 42쪽 PDF를 같은 내용으로 통합한다. PDF는 새 페이지 수를 늘리지 않고 앞의 v0.13 34쪽을 보존한다.

## 2026-10-20 22:45 KST - Role 추가 #16–17

Role.md #16은 대화 이름과 시작 시각이 스크롤 영역 밖에 고정되고 선택 AI/대화 이름의 대비를 확인하는 UI 설명 및 그림이다. #17은 Dracula 테마의 우클릭 메뉴에서 항목 위치를 알기 어렵고 Visual Studio와 배경 색이 비슷한 문제로, 메뉴와 IDE 경계가 보이는 최종 후보 그림이 필요하다. 아직 소스 후보는 C5로 고정되지 않았으므로 이 내용은 사용자 문서에 확정 반영하지 않는다. C5의 실제 제품 렌더와 테스트 상태를 받은 뒤 README/USER_GUIDE/PDF를 같은 문구와 그림으로 맞춘다.

Role.md #17 뒤에 상세 증상이 추가됨: Dracula에서 우클릭 메뉴 item hover/click 위치가 불분명하고 메뉴/IDE surface가 비슷함; 일부 hover 및 중간 UI가 테마 색과 안 맞음; debug/error scrollbar marker의 색이 튀거나 오류 marker가 보이지 않는 사례를 함께 점검한다. 기존 theme-final2 결과는 palette/host 측 검사이며, 실제 Visual Studio UI 포인터 상태와 오류/진단 marker가 화면에 나타나는지는 현재 capture 없음. 공개 문서에서 해결/현재 사용법으로 설명하지 말고, 최신 패키지의 context menu normal/hover/selected, theme outer boundary, error/warning marker 상태와 신뢰 가능한 계정 없는 렌더/실IDE evidence를 받은 뒤 문구와 이미지 결정.

## 2026-10-20 22:50 KST - Markdown 연구 반영 및 Role #18 추적

Orig5의 #11 연구 결론 중 사용자가 알아야 할 범위만 `README.md`와 `docs/USER_GUIDE.md`에 반영했다. 이 확장을 CommonMark/GFM 완전 호환 제품으로 설명하지 않고, 이미 안내된 지원 문법을 우선 사용하며 표준 확장 문법은 미리보기에서 확인하도록 안내한다. Obsidian 전용 위키 링크·임베드·콜아웃은 현재 범위 밖으로 명시했다. 100개 아이디어 목록·시장 비교 세부표·성능 수치·내부 검증 횟수는 고객 문서에 넣지 않았다. 같은 세션에서 입력/유효 팔레트가 유지되는 재표시 시 Markdown 렌더를 재사용한다는 경계만 사용자 설명에 포함했다.

Role.md #18은 디버깅 중 오류가 발생할 때의 도우미 기능 요청이다. 제품 UI·동작 또는 캡처가 검증되지 않아 제공 기능으로 설명하지 않고, 기본 문제 보고에 필요한 기록 항목과 민감 정보 제거 절차만 안내했다. #17 Dracula 우클릭 메뉴의 최종 시각 증거도 현재 없음. 별도 익명 후보5 이미지가 공유되면 #14–17 문구·그림 및 42쪽 PDF 부록을 일치시킨다. 후보5 빌드 산출물이 보이더라도 C5 통합 회귀/실제 IDE/익명 캡처 인계 전에는 최종 PDF freeze 금지.

## 2026-10-20 22:50 KST - 사용자 설명 추가

README #11–16 사용자용 Markdown 및 대화 UI 요약을 업데이트했다. USER_GUIDE 기존 v0.14 절에 대화 머리글/입력 버튼, TXT 첨부 세부 확인과 전체 전송 실패 동작, Markdown 재사용 범위, 아직 안내하지 않는 #17/#18 기능을 추가했다. 첨부 수·용량은 기존 사용자 한도(4개/파일당 100KB)만 표기하며 C4/C5 내부 검사 수는 노출하지 않는다. 현재 후보3 작업 기록 그림은 문서에 후보 DLL 화면 밖 렌더라고 정확히 표시된 초안 상태; 최종 배포 그림으로 쓰려면 계정명 없는 C5 이미지로 교체한다.

## 2026-10-20 22:54 KST - C5 pass4 캡처 반영, PDF 검토본 갱신

Orig4에서 `artifacts/task069/orig4/candidate5-ui14-16-pass4/`를 받았다. exact C5 DLL SHA256 `17E93831EF6CD71A7E20952F151CD9AD4174402B71625434BCB85E6D90E5561E`, 리소스 assets SHA256 `BB6BD39F3C98162592751A94E586F5873EC2807CB7B255B4EA267C7A4E2D3B4C`, 결과 24/24 PASS. synthetic 표시 `사용자 · Codex`; 실제 IDE 창·Popup·OS 입력·AI 요청·네트워크 없이 오프스크린 렌더. 실제 WPF visual tree의 5개 팔레트 사용자/AI bubble 배경, idle/running/wait/other-room 상태, 좁은 화면에서 scroll 중 대화 머리글 고정을 확인했다. 캡처 결과에서 개인 계정명은 발견되지 않았다.

해당 15 PNG를 `docs/assets/ui-guide-v0.14.0/candidate5/`에 SHA 보존 복사하고 출처/제한을 함께 기록했다. README와 USER_GUIDE는 후보3 작업 기록 렌더는 클릭 펼치기 예시로 라벨 유지하고, #14–16 화면은 pass4 dark/wide/light/narrow 및 idle/running 상태 이미지로 교체·추가했다. #17의 Dracula 우클릭 메뉴 이미지는 이 캡처 묶음에 없어 사용자 안내에서 개선을 주장하지 않는다. #18 디버깅 도우미도 검증 전까지 기능으로 안내하지 않는다.

42쪽 PDF 작업본을 C5 그림과 Markdown/TXT 설명으로 갱신했다. 원본 v0.13.0 34쪽의 모든 content stream은 동일함을 재확인했으며, 새 부록은 8쪽이다. PDF 페이지 35–42를 모두 PNG로 다시 렌더해 확인했고 하단 메모/표/이미지 겹침은 보이지 않았다. 이 파일은 여전히 검토본이며 Root의 C6 통합 패키지·릴리스 검증 후 최종 동결해야 한다.

## 2026-10-20 22:58 KST - C5 TXT 첨부 캡처 보완

기존 C4 첨부 캡처는 개인정보가 보이는 상태라 배포 자료에서 제외했다. 같은 고정 C5 DLL/assets로 합성 파일 네 개를 표시하는 새 오프스크린 WPF capture를 로컬 temp harness로 수행했다. `artifacts/task069/orig4/candidate5-text-attachments-pass1/` 결과는 PASS이며 사용자 표시에 `사용자 · Codex`를 적용하고 실제 AI/CLI/전송/OS 포인터/창/Popup/네트워크를 사용하지 않았다. dark wide와 light narrow PNG 두 장을 `docs/assets/ui-guide-v0.14.0/candidate5/`로 복사하고 README/USER_GUIDE/PDF에 포함했다. 캡처는 TXT 배지와 네 개 첨부 레이아웃만 시각화하며, tooltip·문자수·UTF-8 분량 및 실패 처리의 동작은 문구로 설명한다. 결과/manifest에서 개인 계정명이 나오지 않음을 확인했다.

PDF v0.14 검토본 재생성 뒤 42쪽 유지, v0.13 앞 34쪽 content stream 동일함을 재검증했다. 새 8쪽을 모두 다시 렌더했고, 수정한 p38의 대기/실행/TXT 세 이미지가 너비 안에 있고 텍스트와 겹치지 않음을 시각 검토했다. C6/C7 final package sync와 Role #17/#18 검증 완료 전까지 최종 freeze 아님.
