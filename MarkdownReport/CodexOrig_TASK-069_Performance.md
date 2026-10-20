# TASK069 #12 자동 숨김 탭 재표시 개선

image-75.png는 창을 새로 만드는 첫 실행이 아니라, 숨겨진 Claude·Codex 탭을 다시 펼치는 상황이다. 이 작업에서는 실제 WPF 컨트롤의 Loaded API만 사용했고 Window/Popup 표시·OS 입력·실제 IDE 탭 조작은 하지 않았다.

## 재현과 최소 수정

C3 실제 제품 DLL에서 대화 미리보기 20개를 5회 다시 로드했을 때 100회 모두 FlowDocument를 새로 만들었고 선택 영역이 사라졌다. 기존 GIF 객체와 디코딩된 Source도 교체됐다. 원문/자산/팔레트가 같아도 PreviewLoaded가 매번 공개 Refresh를 호출하던 경로를 재현했다.

MarkdownPreview는 인스턴스에 마지막으로 완성한 문서와 입력 스냅샷만 보관한다. 자동 Loaded/DP 변경은 원문·자산 기준 폴더·폰트·전경/배경/추가 네 팔레트 brush의 색/opacity/transform이 같고 실제 TextView.Document도 동일한 경우 재사용한다. 전역 캐시나 외부 파일 탐색은 추가하지 않았다. 알 수 없는 brush 형식은 보수적으로 기존 재렌더링 경로를 따른다.

명시적인 Refresh/SetDocument는 같은 문자열이어도 외부 자산을 다시 읽는다. 실제 원문·폰트·팔레트·기준 폴더 변경, 외부에서 교체한 Document는 새 문서를 만든다. Dispose는 기존 GIF 해제와 함께 인스턴스 스냅샷 참조도 해제한다. Root의 AssistantControl.Theme.cs 변경은 같은 유효 팔레트 재할당을 생략하며, 그 제품 파일은 본 작업에서 수정하지 않았다.

## C3와 C4 실제 관측

| 같은 무창 테스트 | C3 | C4 |
|---|---:|---:|
| Loaded 이벤트 수 | 100 | 100 |
| 문서 교체 | 100 | 0 |
| 선택 영역 유지 | 실패 | 유지 |
| 내부 스크롤 offset | 40 → 40 | 40 → 40 |
| 기존 GIF 객체·Source 유지 | 실패 | 유지 |
| 한 차례 관측 시간 | 677.8432 ms | 0.5182 ms |
| 현재 STA managed allocation | 95,872,040 B | 309,272 B |

시간과 allocation은 단일 합성 입력의 무창 반복 구간 측정치다. 시간 임계값으로 통과시키지 않았으며 실제 Visual Studio의 화면 표시 시간·GPU·전체 프로세스 메모리·사용자 체감 배율로 확대하지 않는다. 사용자 안내에는 숫자 대신 “탭을 다시 펼칠 때 이미 그린 내용을 재사용해 더 부드럽게 복원합니다” 정도가 적절하다.

C4 기본 의미 계약은 **32/32 PASS**다. 비제로 스크롤과 선택/GIF 수명, 실제 내용·폰트·여섯 팔레트 brush 변경, 명시 외부 자산 재로딩, Document 외부 교체, Dispose, Root 테마의 동일 brush 참조·실제 mode/preset 변경·필수 자원 복원 세 계약을 포함한다.

## 추가 경계 실패와 보완

C4 기본 통과 뒤 WPF에서 허용하는 null Transform/RelativeTransform을 추가로 검사했고 새 스냅샷 코드에서 NullReferenceException **1건**을 실제 재현했다. `candidate4-null-transform-1`의 28PASS/1FAIL과 앞 C4 32PASS는 모두 그대로 보존했다. 이는 새 최적화 코드의 경계 결함이었다. 두 null 변환을 Matrix.Identity로 비교하는 최소 guard를 추가했고 기존 source-only Markdown 회귀 **111/111**을 다시 통과했다. 최종 하네스는 null 계약을 포함한 **33개**이며 수정된 실제 C5 DLL에서의 재검증은 아직 대기 중이다. C4를 최종 전체 성공으로 판정하지 않는다.

## 실행기와 증거

- 최상위 실행기에 markdown-reuse와 attachment-wire를 추가해 기본 **26개 + 선택 2개**다. 고정 후보 Assets를 전달하고 원본 보존형 새 출력 폴더 계약을 유지한다.
- 격리 smoke **50/50 PASS**: artifacts/ls/0ec2c09f30/results.json. 실제 제품 검사를 수행하는 대신 fake child로 네 SKU 경로 전달만 검증한 결과다.
- 첨부 fixture 계약은 061에게 확인했고 최상위 EXE의 실제 C4 `--run attachment-wire`는 **120/120 PASS**다. 로그는 artifacts/task069performance/launcher-c4-attachment-1.log다.
- 기준 재현: artifacts/task069performance/baseline-c3-1 및 baseline-c3-2.
- C4 의미 계약: artifacts/task069performance/candidate4-1/result.json.
- C4 null 실패: artifacts/task069performance/candidate4-null-transform-1/result.json.
- 해시·범위: CodexOrig_TASK-069_PerformanceValidation.json.

제품 파일 수정은 MarkdownPreview.cs에 한정했다. 공유 bin/obj·Git/index·계정·실제 원격 AI 사용은 없다. Root가 수정된 C5 실제 DLL의 33개 계약, 최종 전체26개 회귀·4SKU 빌드/실제 IDE·감사·Release 제작·배포·익명 다운로드 해시 확인을 이어간다.

Root 버전 통합 위임에 따라 기본 VSIX/DLL 검색과 GUI 경로 안내를 v0.14.0으로 맞췄다. 0.14.0 기본 DLL decoy보다 명시 VSIX를 우선하는 smoke도 포함해 재컴파일 후 50/50을 다시 통과했다. 최신 실행기 SHA256: C00391E6021B73C74880E512FCDC491CA2AD97BE171928233A5EC718E016F7F0. 앞 실제 첨부 C4 검사는 버전 문구 변경 전 26개 실행기(5727B067...)의 명시 AssemblyPath로 수행했으며, 변경 후 최종 C5 전체26개 회귀는 Root 담당이다.


## C5 실제 수정본 재검증

수정된 C5 실제 DLL에서 **33/33 PASS**, 같은 DLL로 최상위 EXE의 markdown-reuse 연결도 **33/33 PASS**를 확인했다. 앞 C4의 유효 null 변환 실패는 수정된 실제 DLL에서 통과했으며 원본 실패는 보존한다. C5 DLL SHA256은 17E93831EF6CD71A7E20952F151CD9AD4174402B71625434BCB85E6D90E5561E다. 현재 작업 소스와 동결 후보의 MarkdownPreview 소스 해시도 같다.

C5 동일100 Loaded 구간에서 문서 교체0, 동일GIF/Source 유지, 선택영역·스크롤40 유지, managed allocation309,272B를 확인했다. 한차례 무창 관측 시간1.0155ms는 사용자 화면시간으로 확대하지 않는다. 직접증거 artifacts/task069performance/candidate5-1/result.json, 실행기증거 launcher-candidate5-reuse-1.json 및 artifacts/tl/dab0da93bb/markdown-reuse/result.json이다. 파일을 덮어쓰지 않은 두 독립 실행이며 창/OS입력/실계정/원격AI0이다.

제품·하네스·실행기 추가 수정 없이 동결을 유지한다. Root가 예정한 테마 리소스·PDF 갱신 최종C6가 제공되면 동일33계약을 다시 확인한다. 전체26회귀·실제4SKU IDE·최신Release 제작/게시·익명다운로드 해시는 Root 마감 범위다.
