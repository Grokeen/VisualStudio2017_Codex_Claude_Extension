# TASK-034 독립 감사 — 제거 안내·교차 AI·투명 배경·프로젝트 동기화

감사자 **CodexOrig2**. 기준은 Role.md의 **2026-10-19 03:42 추가 요청1~3**과 TASK032의 메시지/로고 수정이다. 최초 후보10, 추가 후보13, 최종 인계 후보14를 구분해 감사했다. 기존 TASK019/023/028 및 후속 보고서는 수정하지 않았다.

**판정: 검사 범위에서 추가로 확정한 제품 결함은 없다. 제거 안내, 교차 AI 호출의 격리/중단, 투명 배경 우선순위와 프로젝트 보호 정리 논리를 확인했다. 최종 실제 IDE 실행은 통합 담당자의 자료로 구분한다.**

[검증 JSON](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig2_TASK-034_Evidence.json)에 원문 결과·해시·실패 보존·검증 주체를 기록했다. 전체 요구에 대한 무조건적인 완료 보증이나 실제 계정의 협업 성공 판정은 아니다.

## 범위와 판정

| 요구/변경 | 확인 결과 | 검증 한계 |
|---|---|---|
| 추가1 확장 제거 기능 또는 방법 | README/사용자 안내에 버전별 제거·비활성화·IDE 종료·데이터 보존 절차가 있음. 공식 문서/패키지 표시 이름과 대조 | 실제 사용자 확장을 제거하지 않음 |
| 추가2 서로 다른 AI 소통 | Claude→Codex 및 Codex→Claude의 순차 검토/종합, 제공자별 CLI·모델·강도·속도 격리, 실제 반환된 검토만 종합함을 실제 DLL로 확인 | AI 경계는 관찰 가능한 fixture. 실제 CLI/계정/유료 추론 미실행 |
| TASK032 투명 사용자 메시지/회색 글자/로고 | 실제 템플릿과 preview/TextView/FlowDocument/content-host의 투명 배경·Foreground 우선순위 확인 | 비연결 렌더의 픽셀만으로 실제 로고 가림 전체 해소를 인증하지 않음 |
| 추가3 프로젝트 외부 수정 모달(image39) | 이미지가 csproj 프로젝트 계층 알림임을 확인. C13 보호 원문을 SDK 경계 shim과 컴파일해 정리11사례 통과. 같은 코드가 C14에 유지됨 | 소스 fixture는 실제 COM/IDE 실행이 아님. 실제3IDE 결과는 TASK032 자료 확인 |
| C13 VS2022의 닫힌 DTE 참조 실패 | 개발 담당자의 최초 실패 보존. C14 ResolveOpenDocument 변경을 읽고, 최종3IDE 결과와 연결 | 해당 실패를 감사자가 처음 발견했다고 주장하지 않음 |

## 대상 고정

개인 사본은 `C:/dydgyqud/AO34_20261019`이다. C10/C13/C14의 입력 각각120개가 목록의 SHA와 전부 일치했다. 제품·공유bin/obj·공유빌드·GUI·실물 입력·실제 계정/CLI·배포는 사용하거나 수정하지 않았다. 개인 시험 파일의 생성/변경/삭제와 감사 보고/근거만 작성했다.

| 후보 | 입력 목록 SHA256 | 의미 |
|---|---|---|
| C10 | `0D50B9492D0E17FECDC9014BB3D0438A1CE5864154D1E6B382A3033EE6BF658A` | 최초 UI/교차 AI 고정 대상 |
| C13 | `2DCEF903B4C30891170DC2B3D1AB8C96E7E42018FB8146E400E6388C16DE9F8C` | 프로젝트 보호 추가. C10과 제품 차이는 scope 한 파일 |
| C14 | `B64FF86800FDD450EE6C37D0970DAB3B03CAF2C6D803A50662F53AE4F1ADF6EE` | C13과 제품 차이는 DocumentService 한 파일 |

검토 Role 사본 SHA256은 `7EBA744723C33741420334286C90F1972B7BF802680B7153879EF2868E0EAF69`이다. 이 감사에서는 Role을 편집하지 않았다.

| 최종 C14 SKU | 실제 검사 DLL SHA256 | VSIX SHA256 |
|---|---|---|
| 2017 | `8FE5B4C7FF6DA7731B2892C71E258D8872248F2DF83111A6AE8031DE168E293A` | `33EA90EBAC97ACC27CAE6D00A0BD33DC2796A9C0F63D560C64780698828543C2` |
| 2019 | `E47CA65030A3F3A521009E494465F63D45DCA643F878550687DA436B0DF3E443` | `A54F1AD5C9096B9EEDF15E33CE9331F8A3084DDF28B13F308518AEB147B579CA` |
| 2022 | `5421B4BA56A4B66F5148DF8BE903128C816D1D71100F4ED4456445975637A0B0` | `94B0D919687F519FA74714D6B6E2DCF02FC55BD271FB820695F70AD306501249` |

## 교차 AI의 직접 실행 검사

감사자가 만든 [독립 fixture](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task034/Audit34Checks.cs)는 실제 AssistantControl·Generate/Stop 이벤트·저장소·비동기 정리 코드를 호출한다. IAiContextEditProvider 경계만 교체해 호출 순서와 컨텍스트를 기록하고 실패/취소를 주입했다. 창은 열지 않았다.

- 양방향에서 동료 검토 후 현재 제공자가 종합하며, CLI 경로·모델·강도·속도는 해당 제공자의 설정을 사용한다.
- 동료는 자기 대화 이력을 받고 주 담당자의 과거 이력은 섞이지 않는다. 공통 규칙/선택 첨부는 공유되고, 주 담당자의 종합에는 실제 반환되어 전달된 검토가 들어간다.
- 첫 동료 실패·취소 및 두 번째 동료 실패에서 종합 호출이 차단된다. 이전에 전달된 검토는 실패 기록으로 덮어쓰지 않는다.
- 최대3명의 순차 검토/재귀 호출 없음, 4명 저장 거부, 변경된 팀·닫힌 참여자·교차 제공자의 중복 Guid를 확인했다.
- 검토 도중 원본 버퍼 충돌은 종합 전에 차단한다. 정상/실패/취소에서 제품의 Replace/Save 호출이 없고 시험 원본 디스크 바이트도 유지됐다.
- 전체 액세스 경로도 소스가 있는 협업을 요구하고 같은 RunCollaboratorsAsync를 선행하는 것을 코드로 확인했다. 이번 실행 검사는 승인 후 적용하는 수정안 경로이며 실제 전체 액세스 CLI 실행을 한 것은 아니다.

모델 지원 정보는 명시적인 시험용 카탈로그로 게시했다. 이는 실제 계정의 Fast 지원 증거가 아니다. 지원 정보 없이 Fast를 강제했던 초기 fixture에서 표준 속도로 정규화되는 정상 동작을 관측했으며 이를 제품의 설정 혼선으로 보고하지 않았다.

## 투명 배경·회색 글자의 직접 확인

Codex/Claude × Light/Dark의 실제 메시지 템플릿에서 아래를 확인했다.

1. MarkdownPreview 생성자가 Background/Foreground의 local 값을 선점하지 않는다. 호출자가 명시한 색은 실제 문서에도 반영된다.
2. 사용자 메시지는 AssistantMutedForegroundBrush, 답변은 AssistantForegroundBrush를 사용한다. template trigger/binding의 값이 TextView와 현재 FlowDocument로 전달된다.
3. MessageBubble, preview, TextView, FlowDocument와 전용 PART_ContentHost의 배경이 투명하다. RichTextBox에 범위를 한정한 스타일을 사용한다.
4. 원문 텍스트가 문서에 남고, theme 전환 뒤 기존 문서의 색도 갱신된다. 이 결과에 픽셀 기반 로고 완전 노출 인증은 포함하지 않았다.

개발 담당자의 [C13 실제 연결 IDE 렌더](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/ide/run-6bfbcbfbfeb441bdba5872afc2fbc673/ui032-light.png)도 직접 열어 사용자 회색 글자와 보이는 본문/로고 대역을 대조했다. 비연결 FlowDocument 캡처의 일부만 그려지는 한계와 구분하며, 모든18행을 동시에 픽셀로 확인했다고 확대하지 않는다.

## 프로젝트 보호 코드의 독립 검토

검토한 [C13 scope 원문](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task034/VisualStudioExternalEditingScope.frozen.cs)의 SHA256은 `FC1F51393407C7B8602A37F3D65E81689CAE473B76AE1382F2B7A17197DB6E75`이며 C14 파일과 동일하다. 원문을 변경하지 않고 [SDK 경계 shim](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task034/ScopeFixture.final.cs)과 컴파일했다.

**아래11사례는 소스 논리 검사이며 실제 제품 DLL/COM/IDE 검사가 아니다.**

| 사례 | 관측 결과 |
|---|---|
| 변경 없는 프로젝트·중복 Dispose | 재로딩 없이 path ignore/Sync/복원·이벤트 해제, 중복 정리 없음 |
| 깨끗한 계층 프로젝트 변경 | ReloadItem 호출과 두 ignore 상태 복원 |
| legacy 프로젝트 변경 | unload→reload, 닫힌 텍스트 탭 재열기·원래 window 활성화 시도 |
| 시작 전 dirty 프로젝트 | 알림 중지 전에 실행 차단 |
| 실행 중 dirty가 된 프로젝트 | 미저장 모델 재로딩 보류·안내·알림 복원 |
| 두 번째 파일 ignore 실패 | 앞서 적용한 프로젝트 보호 복원 |
| 계층 ignore 설정 실패 | 먼저 적용된 path ignore 복원 |
| 두 번째 프로젝트 dirty | 첫 프로젝트 보호 복원 |
| 재로딩 API 실패 | 실패 안내와 ignore 복원 |
| 프로젝트 삭제 | 삭제 안내와 ignore 복원 |
| 문서 닫힘/취소 callback | 중지 호출·읽기 전용/다른 buffer flags 및 알림 복원 |

전역 IDE 옵션을 쓰는 API 호출은 이 소스에 없으며 파일별 scope로 동작한다. 실제 전역 옵션의 전후 값을 독립 IDE에서 측정한 검사는 아니다. 재로딩 실패 시 안내/알림 정리는 확인했지만 모든 COM 실패에서 프로젝트 모델이나 창 포커스가 완전히 복원된다고 보증하지 않는다.

C14의 [ResolveOpenDocument](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/task025/candidate14/source/src/VS2017.AIAssistant/Services/VisualStudioDocumentService.cs:116)는 현재 dte.Documents 집합과 활성/캐시 문서를 대조하며 닫힌 COM/ArgumentException에서 캐시를 버린다. GetActiveDocument·CaptureFullAccessContext·GetProjectDirectory에 적용된 변경을 읽어 확인했다. 이 메서드를 별도 COM fixture로 실행한 것은 아니다.

## 실행 주체와 결과

| 실행 | 결과 | 주체/한계 |
|---|---|---|
| C10 독립 UI/교차 AI | 세 SKU 각각15/15 | 감사자 직접, 실제 DLL, 창 없음 |
| 최종 C14 동일 검사 | 세 SKU 각각15/15 | 감사자 직접, 실제 DLL, 기대 조건 동일 |
| C13/C14 동일 scope 소스 |11/11 | 감사자 직접, SourceFixture=true, 실제 COM/IDE 아님 |
| C14 실제VS2017/2019/2022 |각145/145 | TASK032 실행, 감사자는 원문·제품 해시 확인 |
| C14 빌드6개 |오류0·경고0 | 통합 담당자 결과 확인, 감사자 빌드0 |

실제 C14 IDE 원문: [2022](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/ide/run-0fefc10f029b4866af1e7bd066f2fe4d/result.json), [2017](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/ide/run-a84f9d521134453fb30c40a0a1f6c626/result.json), [2019](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/ide/run-23aabafbde1f45d8b577750a4d11bd59/result.json). 각 ProductSha256이 위 C14 DLL과 일치한다. PROJECT032의 모델 변경/복원·미저장 차단과 UI032를 포함한다. C13 VS2022의 [기존 실패](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/ide/run-43eabf275b6844f9bab0ac1198fdd6a6/result.json)는 보존한다.

반복/중복되는 fixture15와 기존 통합 검사·소스11을 합쳐 고유 합격 총계로 표시하지 않는다. 초기 감사 하네스의 Save 인터페이스 누락, instructions 형식/초기 provider 준비, 지원 정보 없는 Fast 기대 오류는 원문을 보존하고 최종15/15와 구분했다.

## 제거 안내·패키지·인계

[사용자 안내의 제거 절차](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/docs/USER_GUIDE.md:18)는 VS2017과 VS2019/2022 경로, 제거/비활성화, IDE 종료와 데이터 보존을 구분한다. Microsoft의 [확장 관리 안내](https://learn.microsoft.com/en-us/visualstudio/ide/finding-and-using-visual-studio-extensions?view=visualstudio), [VS2017 확장 변경 예약 안내](https://learn.microsoft.com/en-us/visualstudio/releases/2017/vs2017-relnotes-v15.0#modify-extensions-in-bulk)와 대조했다. 패키지 표시 이름과 외부 데이터 경로도 로컬 자료로 확인했다. 실제 제거 성공을 실행 검증한 것은 아니다.

C14 각 VSIX 선택 핵심23파일의 바이트가 Release 출력과 일치했다. 제품 DLL·의존성/loader·worker·GIF5/PNG5·MIT 라이선스/고지를 포함한다. [패키지 근거](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/audit-codexorig2-task034/package-integrity-c14.json)에 선택 목록과 해시를 남겼으며 전체 ZIP 항목 전수 검사로 확대하지 않는다.

이번 범위에서 새로 수정해야 할 확정 제품 지적은 없다. 최종 전체16·릴리스 버전/다운로드·문서·배포/원격 해시 마감은 통합 담당자 소유다. 실제 Claude/Codex 계정의 교차 요청, 모든 프로젝트 형식/COM 실패, 실제 사용자 확장 제거는 검증하지 않았다. Role·기존 감사·원본을 보존하고 TASK034 보고/근거 소유를 반납하며 감사 작업을 종료한다.

마감 중 추가 인계된 [C14 전체16 원문](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/artifacts/tl/12f0f3cf46/summary.txt)은16묶음 모두 exit0이었다. [통합 검증 JSON](C:/dydgyqud/VisualStudio2017_Codex_Claude_Extension/MarkdownReport/CodexOrig_TASK-032_Validation.json)도 연결했다. 제품5그룹247·워크스페이스UI145·독립23·문서UI117의 통과는 통합 담당자 실행 자료이며 위 감사자의15/11에 합산하지 않는다. 최종 버전/다운로드 마감과 공개·원격 해시 확인은 통합 담당자가 이어서 수행한다.
