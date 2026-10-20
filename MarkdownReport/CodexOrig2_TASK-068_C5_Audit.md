# CodexOrig2 — TASK068 C5 고정 후보 감사

대상: `artifacts/task068/candidate5`, v0.13.0-preview.1 후보. 고정 입력 **138/138 SHA-256 일치**, 네 SKU Release 빌드 각각 종료코드·오류·경고 **0**. C4→C5 소스 차이는 새 공개 PDF 입력 1개 추가, 제품 canonical PDF 교체, `AssistantControl.RequestQueue.cs` 수정뿐이다. Root의 공식 무창 회귀 20개 영역도 모두 종료코드 **0**이며 마지막 동시 대화 검사는 같은 C5 DLL 해시에서 115/115다. **C5 배포 판정은 A68-04 실제 IDE 결함으로 실패**다. C5의 무창 기능·로컬 패키지 통과 증거는 원본대로 보존하며, 수정된 C6 후보를 별도로 감사한다. [공식 무창 회귀 목록](../artifacts/tl/9f11ff1c39/summary.txt).

## 추가 확정 결함 A68-04 — 담당 AI 재선택 후 수정안 조작 불가

고정 C5 실제 IDE [run-ee398a의 ROOM-QUEUE-03 원본](../artifacts/ide/run-ee398a3430034af1a4b9d8fc08f039e7/progress.log)에는 상태가 `이 대화방의 대기 요청 1개`, 수정안 미리보기 보존 `true`, 내부 수정안 보존 `true`인데 표시용 `pendingProposal`은 `false`, 명시적 Discard 버튼은 비활성, 후속 CLI marker는 없음으로 기록됐다. [C5 `ActivateAssignedAgent`](../artifacts/task068/candidate5/source/src/VS2017.AIAssistant/Views/AssistantControl.Dropdowns.cs#L259)는 현재 담당 AI를 다시 선택할 때 `ClearProposal(); ShowChat(); ... UpdateButtons();`를 호출하고, 방별 `roomProposals`로부터 `RestoreRoomPresentation()`을 호출하지 않는다. 따라서 **미승인 수정안은 방별로 남아 다음 요청을 막지만 사용자는 화면에서 승인·건너뛰기 동작에 접근하지 못한다.** 단순 테스트 기대치 문제가 아니라 사용자 작업이 멈추는 제품 결함이다. C5는 공개하지 않는다. 제품 수정·C6 재실행은 Root/Code 소유다.

## Role.md 12:47 추가 요구와 이전 지적

| 요구/지적 | C5 증거와 판정 |
|---|---|
| 1. 대화방별 동시 작업, A68-01 역할 문맥 | 고정 2017 DLL SHA-256 `AD6327BB58CEBF0E362B90134F9DFB803CDB566B457452AC1D17C7C999E66E02`의 실제 `RuntimeEditProvider`·로컬 Claude 프로토콜 무창 검사 **115/115**. 방별 PID·완료·취소·초안·승인/대기열·역할·사용량 분리와 공유 보호 범위의 합성 수명을 포함. **A68-01 해결(검사 범위)**. [실행 원본](../artifacts/task068-audit/candidate5-concurrent-1/result.json). |
| 2. 시간별 저장·스크롤 | C5 고정 소스에서 추출한 WPF 기록 메서드 13개와 저장 소스의 C5 해시 일치. 지연 읽기·다른 방 전환·Dispose·실패 재시도·읽기 위치 유지 등 **10/10**. C5 고정 저장 소스의 페이지·CAS·UTC·4 MiB·손상·동시 기록을 별도 재실행해 **19/19 + 13/13**. 같은 SHA-256의 실제 C5 DLL을 대상으로 한 별도 저장 검사도 **32/32**. **무창 회귀 통과**, 실제 IDE 스크롤 체감은 별도. [C5 UI 결과](../artifacts/task068-audit/candidate5-history-ui/history-ui-result.json) · [저장 결과](../artifacts/task068-audit/storage-c5/run1/result.json) · [저장 경계](../artifacts/task068-audit/storage-c5/run1/boundary-result.json) · [실제 DLL 저장 검사](../artifacts/task068storage/assembly-151a81dfe1154b8c84f21f396fcb9d6e/result.json). |
| 3. 좁은 화면 더블클릭 | C5의 이벤트 연결·폭 조건 코드는 C4와 같다. 실제 포인터/레이아웃은 Root의 IDE 검사 대상으로 남긴다. |
| 4. 선택지, A68-02 | 고정 C5 DLL의 선택지 getter 직접 검사 **8/8**. `selected` 완료 문장 오인 재발 없음. 버튼 선택은 수동 전송용 입력 초안을 만드는 코드 경로다. **A68-02 해결(검사 범위)**. [실행 원본](../artifacts/task068-audit/candidate5-choices.json). |
| 5. 필수 숨김 지침 | 고정 C5 DLL의 실제 파일 생성·공식 구버전 갱신·사용자 변경본 보존·Compose 경계 **36/36**. [실행 원본](../artifacts/task068-audit/mandatory-c5/result.json). |
| A68-03 읽기 전용 거절 후 초안 소실 | [전체 액세스 캡처](../artifacts/task068/candidate5/source/src/VS2017.AIAssistant/Views/AssistantControl.RequestQueue.cs#L153)는 사용자가 원래 읽기 전용으로 연 파일과 현재 보호 범위가 증명하지 못한 파일을 **사용자 메시지 추가·입력 삭제 전** 거절한다. 고정 DLL 검사에서 원래 읽기 전용 A/B·증명 없는 범위·다른 경로는 원문 입력을 보존하고 CLI 시작·파일 쓰기 없이 거절했다. 동일 범위가 임시 보호한 정확한 B 경로는 A와 병렬 실행·완료 뒤 원래 상태 복원. **A68-03 해결(합성 호스트 범위)**. 실제 IDE 읽기 전용 복원은 Root 검사. [검사 원본](../artifacts/task068-audit/candidate5-concurrent-1/result.json). |

## 새 PDF와 배포 경계

네 C5 VSIX를 독립적으로 열어 canonical `Resources/CliGuide/VisualStudio_AI_Assistant_설치_사용설명서.pdf`가 각각 정확히 1개이고 **1,666,997바이트, SHA-256 `E96A03BD125550AF364E7B4476B25262716463C990632E088B55CCBCCD233E87`**임을 확인했다. 네 C5 Release DLL에서 실제 `CliGuideService.GetPdfPath()`를 호출해 같은 이름·크기·해시를 확인했다. PDF는 `pdfinfo` 기준 **34쪽**이며 1쪽·34쪽 렌더를 표본으로 확인했다. 설치된 IDE 내 경로는 Root의 실제 검사 대상이다. [VSIX 원본 검사](../artifacts/task068-audit/candidate5-vsix-content.json) · [직접 서비스 호출 2017](../artifacts/task068-audit/candidate5-guide-2017.json).

네 AI VSIX의 바깥 항목과 각각 포함된 1개 테마 VSIX의 내부 항목에서 `.cs`·`.csproj`·`.sln`·`.pdb`·`.xaml`·`.resx`·`.map` 항목은 **0**이었다. GitHub 소스 공개는 사용자 요청대로 별개다. [중첩 VSIX 검사](../artifacts/task068-audit/candidate5-nested-vsix-content.json).

최종 배포 입력 자산 **13/13**의 바이트 수·SHA-256을 독립 재해시했다. FullOffline ZIP은 **311,250,479바이트**, SHA-256 `5C4ACBF1C683BA4764A0BC7694E49968FE23D689A0B068A20AC00BA458D08304`; 내부 70항목 중 자체 체크섬을 제외한 **69/69**가 ZIP의 `OFFLINE_SHA256SUMS.txt`와 일치했다. `UserDistribution/v0.13.0`은 실제 **68개 파일/manifest 68행** 전부 해시·크기 일치, 새 `OFFLINE_SHA256SUMS.txt`의 **67개 고유 행이 나머지 파일 전체를 덮는다**. 원본 FullOffline의 준비기 두 파일(`Prepare-OfflineSetup.ps1`, `PrepareOfflineSetup.bat`)은 전달 폴더에서 빠졌고 체크섬은 전달 파일용으로 다시 작성됐다. CLI 46개는 유지, 소스·PDB·계정·개발 증거·추가 파일·reparse 항목은 **0**. [자산/전달 폴더 독립 검사](../artifacts/task068-audit/candidate5-package-independent.json) · [ZIP 독립 검사](../artifacts/task068-audit/candidate5-zip-independent.json) · [생성 manifest](../artifacts/task068/user-distribution-final1/user-distribution-manifest.json).

해당 정확한 ZIP으로 합성 SKU·로컬 설치 경계 **35/35**, 통신 차단 AppContainer의 CLI 검증·재설치·버전 실행 **6/6**이다. 고객의 실제 VS2017 Professional 15.7.1 PC 설치를 뜻하지 않는다. [설치 경계](../artifacts/task068/installer-final1/results.json) · [격리 실행](../artifacts/task041/run-815c681bb28648ffbc2ff29f2361fa23/worker-results.json).

이 감사자는 창 표시·팝업·OS 포인터·실제 IDE·로그인·원격 AI·게시·삭제를 실행하지 않았다. Root의 앞선 C5 원본 [run-948b](../artifacts/ide/run-948b745918e841b690ff6c3d541da6a7/result.json)에서 실패한 복합 판정은 상세 관측을 추가한 run-ee398a로 A68-04가 확정됐다. C5 실제 IDE 네 SKU 156/156은 달성되지 않았고, 고객 PC 물리 설치·공개 게시·13자산 익명 재다운로드·이전 첨부 정리는 수행하지 않았다. [기계 판정](CodexOrig2_TASK-068_C5_Evidence.json).
