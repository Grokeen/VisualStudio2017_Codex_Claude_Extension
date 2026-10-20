# CodexOrig2 — TASK068 C6 고정 후보 재감사

대상: `artifacts/task068/candidate6`의 v0.13.0-preview.1 제품 후보. `source-hashes.json`의 **138/138** 입력은 고정 사본과 일치한다. 네 SKU Release 빌드는 종료코드·오류·경고 모두 **0**이다. C5와의 변경 입력은 [`AssistantControl.Dropdowns.cs` 1개](../artifacts/task068/candidate5-to6-input-diff.json)이며, C5 실패 패키지는 [별도 보존](../artifacts/task068/preserved-candidate5-package/preservation-result.json)돼 C6 성과에 합산하지 않는다. **C6의 무창 제품·로컬 패키지와 네 SKU 실제 IDE 628/628 감사는 통과**했다. 공개 게시·익명 재다운로드는 미완료다.

## A68-04 재선택 후 미승인 수정안 복원

C5 실제 IDE에서 담당 AI를 재선택하면 방별 `roomProposals`는 남았으나 표시용 `pendingProposal`과 Discard 버튼이 사라져 대기 요청이 막혔다. [C5 실패 원본](../artifacts/ide/run-ee398a3430034af1a4b9d8fc08f039e7/progress.log) 174–175행. C6 고정 소스의 [`ActivateAssignedAgent`](../artifacts/task068/candidate6/source/src/VS2017.AIAssistant/Views/AssistantControl.Dropdowns.cs#L260)는 `ShowChat()` 뒤 `RestoreRoomPresentation()`을 호출한다. 이 메서드는 방별 보관 수정안을 `ShowRoomProposal()`로 다시 표시하고 `pendingProposal`, 원본·수정·diff 미리보기, 승인 상태를 복원한다.

감사자는 고정 C6 2017 DLL SHA-256 `BC7C35B7929D316A8F12DE333EBCEEDBB4AFE25D6DB92A5D8B644183CE14E4C5`의 실제 `RuntimeEditProvider`·로컬 Claude 프로토콜 무창 검사를 재실행했다. **120/120**이고, 새 재선택 5개 검사는 실제 `ActivateAssignedAgent` 호출→같은 수정안 참조와 세 미리보기 복원→후속 요청 1개와 Discard 활성→명시적 skip→원본 파일 쓰기 **0**·다음 실제 CLI 시작을 확인한다. 별도 공식 실행도 **120/120**이며 검토 JSON은 기존 115개 누락 **0**, 새 5개 추가, 제품 DLL·assets·fixture 원본/사본 해시 일치를 기록한다. [감사자 검사 원본](../artifacts/task068-audit/candidate6-concurrent-1/result.json) · [공식 검토](../artifacts/task068concurrent/candidate6-agent-reselect-1/review.json). **A68-04는 무창 합성 호스트와 아래 네 실제 IDE 검증에서 해결**됐다.

## 다른 요구와 범위

A68-01 역할 문맥, A68-02 선택지 오인, A68-03 읽기 전용 초안 보존을 포함한 C6 동시 대화 검사는 위 120/120에 들어 있다. 선택지 getter 직접 검사도 **8/8**이다. C5→C6에서 저장·비동기 기록·필수 지침 소스는 바뀌지 않아 C5의 고정 소스 저장 19/19+13/13, 실제 DLL 저장 32/32, 비동기 기록 10/10, 필수 지침 36/36은 관련 소스 회귀 근거로 유지한다. C6 새 DLL의 독립 저장·지침 검사를 수행했다고 주장하지 않는다. [선택지 검사](../artifacts/task068-audit/candidate6-choices.json) · [C5 저장 검사](../artifacts/task068-audit/storage-c5/run1/result.json).

네 C6 VSIX의 canonical PDF는 각 **1개**, **1,666,997바이트**, SHA-256 `E96A03BD125550AF364E7B4476B25262716463C990632E088B55CCBCCD233E87`이다. 네 C6 Release DLL의 실제 `CliGuideService.GetPdfPath()` 호출도 동일한 이름·바이트·해시로 성공했다. 네 VSIX의 바깥 소스/PDB형 항목은 **0**이다. [VSIX 내용](../artifacts/task068-audit/candidate6-vsix-content.json) · [Guide 서비스 2017](../artifacts/task068-audit/candidate6-guide-2017.json).

Root의 C6 공식 회귀 20개 영역은 모두 종료코드 **0**이고 마지막 동시 대화도 고정 C6 DLL에서 **120/120**이다. [공식 회귀 목록](../artifacts/tl/a2d4802889/summary.txt).

**C6 신규 패키지 직접 검사:** 13개 배포 자산 전부 바이트 수·SHA-256 일치. FullOffline ZIP은 **311,250,542바이트**, SHA-256 `5A2B01C884C97D8661FF251CE776357C2132879A219328C73DCBF87E94764E83`; 내부 70항목에서 자체 체크섬을 제외한 **69/69** 항목 일치. C6 공개용 AI VSIX 4개도 고정 C6 빌드 VSIX와 해시가 같다. `UserDistribution/v0.13.0`은 성공 생성 로그 뒤 검증했고 **68/68** 파일이 manifest의 크기·해시와 일치, 재생성한 체크섬 **67개 고유 행**이 나머지 파일 전부를 덮는다. CLI 46개를 유지하며 소스·PDB·계정·개발 증거·추가 파일·reparse 항목은 **0**이다. [자산/전달 폴더 독립 검사](../artifacts/task068-audit/candidate6-package-independent.json) · [ZIP 독립 검사](../artifacts/task068-audit/candidate6-zip-independent.json) · [C6 전달 manifest](../artifacts/task068/user-distribution-final2/user-distribution-manifest.json).

해당 정확한 C6 ZIP의 합성 SKU/로컬 설치 경계 **35/35** 및 통신 차단 AppContainer에서 CLI 검증·재설치·버전 실행 **6/6**을 확인했다. 고객의 실제 VS2017 Professional 15.7.1 PC 설치 결과로 해석하지 않는다. [설치 경계](../artifacts/task068/installer-final2/results.json) · [통신 차단 원본](../artifacts/task041/run-a2d91f18eefa4849bb64562fe7dd4873/worker-results.json).

**실제 IDE 검증:** [네 SKU 실행 요약](../artifacts/task068/candidate6-ide1-summary.json)의 각각 **157/157**, 합계 **628/628**, 실패 **0**을 감사자가 각 [2017.Legacy](../artifacts/ide/run-195ab4163d27492688c003ad2d7cb4f0/result.json) · [2017](../artifacts/ide/run-b930f22dd3e241f0b4a6f00d49f875db/result.json) · [2019](../artifacts/ide/run-86b61db5063f4febb18fffa1ce704a36/result.json) · [2022](../artifacts/ide/run-fe680fd0907447cf9db1537326b425c7/result.json) 원본 157행 및 `progress.log` 157 PASS와 대조했다. 네 설치 DLL의 SHA-256은 각 고정 C6 Release DLL과, 네 설치 VSIX의 SHA-256은 각 고정 C6 VSIX와 일치한다. 네 `ROOM-SYNC-01`~`08`은 각 **8/8**, `ROOM-QUEUE-03` 관측은 모두 수정안 보존·Discard 활성·원본 쓰기 방지(`MarkerExists=false`)를 기록한다. 네 `GUIDE-01`은 설치 PDF 해시가 고정본과 일치하고 `FULL-04`~`08`도 통과했다. IDE 호스트는 순서대로 VS 15.0, 15.0, 16.0, 17.0이다. 전용 프로세스는 세 건 정상 종료했고 2019 한 건은 [결과 보존 뒤 강제 정리](../artifacts/ide/run-86b61db5063f4febb18fffa1ce704a36/owned-process-cleanup.json)됐다. 종료 차이는 테스트 실패나 제품 결함으로 단정할 근거가 없지만 그대로 기록한다. 이는 Root 실행 증거를 감사자가 대조한 것이며 감사자가 IDE를 직접 실행한 것은 아니다.

**기술 게이트 판정: PASS.** C6 고정 소스·빌드·기능·로컬 전달 패키지·네 실제 IDE 검증은 현재 증거에서 통과했다. 감사자는 제품·공유 빌드·GUI·IDE·원격 AI·로그인·배포를 수정/실행하지 않았다. 고객 PC 실설치, C6 13자산 공개 게시/익명 재다운로드·이전 첨부 정리는 아직 검증되지 않았으므로 **배포 완료 판정은 보류**한다. [기계 판정](CodexOrig2_TASK-068_C6_Evidence.json).
