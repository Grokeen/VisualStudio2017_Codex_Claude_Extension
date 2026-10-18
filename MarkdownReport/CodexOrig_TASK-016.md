# TASK-016 — 문서 첨부·모델 상세 설정·대화 AI 이름과 협업

담당 CodexOrig. 시작 2026-10-18 14:03:42 KST. 최신 Role.md와 이미지7~11을 검토하고, 사용자가 답한 권한 범위(확장의 DB·형상관리·배포)를 반영했다. TASK-015 CodexCode가 문서·모델 능력·협업·SCM 서비스를, TASK-016이 기존 모델/뷰 연결·실제 협업 실행·전체 검증·문서/패키지·Git을 담당했다.

현재 상태: 구현·최종 검증·GitHub 배포 및 원격 해시 확인 완료. 다른 작업자 전원 종료14:34:25 이후 배포했다. 기존 v0.6.0은 보존하고 추가 기능을 v0.7.0으로 준비했다. 작업 중에는 GitHub 배포를 보류했고 모든 작업자 종료를 확인했다.

## 구현 결과

- 문서: 파일 선택·드롭·복사 후 Ctrl+V에서 문서 본문을 비동기로 추출한다. DOCX/PPTX/XLSX·ODT/ODP/ODS·RTF·HTML과 PC의 Windows IFilter로 읽는 PDF·DOC/PPT/XLS를 지원한다. 입력16MB·본문 UTF-8 100KB·최대4개, 별도 도우미/15초 제한·취소·실패한 묶음의 부분 첨부 방지. 원본은 변경하지 않는다.
- 모델: 실제 Codex CLI 캐시의 모델·지원 강도·Fast 능력을 표시한다. 상세 팝업의 생각 강도 슬라이더·Fast·계정/사용량·최소 CLI 버전 안내, 제공자별 모델/강도/속도 저장. 미지원 모델의 고급 강도·Fast는 차단한다. 생각 강도는 정확도 점수가 아니다.
- 권한: 승인 요청/내 대신 승인/전체 권한은 확장의 세 작업 권한을 선택한다. 실제 작업 계획·실행 동의와 AI 수정안 비교·승인/충돌 검사·별도 저장을 유지한다.
- AI 이름·역할: 각 대화 AI에 고유 이름·역할·팀을 저장한다. GitHub 로그인 확인 계정 또는 명확히 선택 가능한 GCM 저장 계정이면 자동 기본 이름에 계정::AI-ID를 사용한다. 저장 계정의 서버 인증 미확인을 구분하고 사용자 이름/역할은 보존한다.
- 실제 협업: 같은 제공자·같은 팀의 열린 대화 최대3개를 선택한다. 고정 소스로 동료를 순차 호출해 실제 검토를 전달한 뒤 현재 AI를 호출한다. 중간 수정안은 적용하지 않는다. 요청/회신/전달·실패·취소와 대화별 실제 상태/읽지 않음, Markdown 보고를 남긴다. 선택 인원과 사용량 증가를 표시한다.
- 복구: 대화 파일 저장 실패 시 이름·팀·참여자 원복, 리비전 충돌 시 외부 변경 보존. 이미 전달된 회신은 대화 파일 오류로 실패 회신으로 덮지 않는다. 보조 보고/상태 저장 오류가 원래 CLI 오류나 취소를 덮지 않게 분리한다.
- 설정: 계정·응답 모델·공통 작업 규칙·화면 표시를 검정 계열 주제 구역으로 분리했다. README는 사용자 목적·설치·실제 사용 순서를 기준으로 갱신했다.

## 변경 경로

제품은 src/VS2017.AIAssistant/Chat/ChatWorkspace.cs·MarkdownWorkspaceStore.cs, Views/AssistantControl.Options.cs·Chat.cs·Experience.cs·Dialogs.cs·xaml.cs·xaml·WorkspaceActionsDialog.cs 및 csproj/버전이다. TASK015 소유 신규 서비스는 Chat/DocumentAttachmentReader.cs·DocumentWorkerLauncher.cs·DocumentFilterWorker.cs·ConversationCollaboration.cs, Providers/ProviderExecutionOptions.cs·CliEditProvider.cs, Services/ScmIdentityService.cs와 src/VS2017.AIAssistant.DocumentReader이다.

검사는 tests/review/AdvancedExperienceChecks.cs·Test-WorkspaceExperience.ps1, tests/ide/IdeTests.cs, tests/launcher와 TASK015 신규 documents/collaboration/provider-options 폴더다. 루트 VisualStudio_AIChat_ExtentionTest.exe는 기본13/선택2 묶음을 제공하고 지정 VSIX에서 제품 DLL과 문서 도우미를 함께 추출한다.

## 검증 근거

최종 DLL BB80BC8ED422F6F36857AB7367A5B5432A65218E8CFE6F51F94A1143A16A1D44, VSIX DB9C5D706760D693A2BB758E9E1E6DC2C0BF1DB4B28FA134F3307B35ABEBC059 (275314바이트). Debug/Release 경고0·오류0. VSIX 루트의 DLL·도우미 exe/config는 고정 사본과 해시가 일치한다.

| 최종 검사 | 결과 |
|---|---|
| 실제 WPF 문서·모델·대화·협업·복구 | 101/101 PASS |
| 실제 VS2017 설치·C#/SQL·Codex·native 협업 | 73/73 PASS |
| 신규 서비스 고유 사례 | 250/250 PASS |
| 추가 x86 문서 호출 | 47/47 PASS |
| 루트 실행기 기본 전체 | 13/13 묶음 PASS·종료0 |
| 루트 실행기 GUI·실패 전파·선택 VSIX·자식 종료 | 9/9 PASS |

기본 전체 묶음은 호환44·빌드진입16·기록52·설정37·업무48·편집92·Foundation33·UI101·문서47·협업60·모델65·CLI15·SCM63을 실행한다. 서비스 별도 재검증과 같은 사례를 중복 합산하지 않았다. 최종 기본 실행기 근거는 artifacts/tl/680a98b066, 실제IDE는 artifacts/ide/run-7aad0ebf22594992a83aea0fdcec44ee이다.

첫 통합 실행에서 SCM fixture를 하위 Git 설치 위치에 복사할 때 제품 DLL 의존성을 빠뜨린 테스트 배치 문제를 발견해 수정했다. 실제 IDE에서는 소스 탭 활성 상태에서 UI visibility를 확인하던 검사를 소유 도구 프레임 표시 후 실행하도록 바로잡았다. 수정 뒤 같은 고정 제품으로 전체13묶음 및 실제IDE73개 모두 통과했다. 독립 비동기 UI 검사는 DispatcherSynchronizationContext를 설치해 실제 WPF 이벤트 루프와 같은 스레드 복귀를 검사한다.

제품 소스36개 해시 불일치0, 사용자 Role 원문 D25E6ED1167A33AF028BEB0EF23EDCEE918A9118C583BA295993B70C1C310866 보존, 기존 릴리스 변경0. 사용자 화면7개를 렌더하고 Chat·모델·AI 역할·설정 화면을 직접 열어 검토했다. 문서46개 로컬 참조가 정상이다. 원격의 v0.6.0·v0.7.0 VSIX와 루트 실행기를 다시 내려받아 로컬 해시 일치를 확인했다. 제품 커밋 da337035378fefec8a3b73f883c68aa20028372f, 원격 검증은 [배포 JSON](CodexOrig_TASK-016_Deployment.json)에 보존했다.

[검증 JSON](CodexOrig_TASK-016_Validation.json) · [실제 IDE](CodexOrig_TASK-016_IDE.json) · [UI 사례](CodexOrig_TASK-016_Experience.json) · [TASK015 서비스 검증](CodexCode_TASK-015.md) · [설치 파일](../releases/v0.7.0/README.md)

## 범위와 재현

루트 실행기의 --all은 실제 계정 호출·IDE 설치 없이13개 묶음을 실행한다. 실제 VS2017은 tests/ide/Test-IdeIntegration.ps1 -VsixPath <고정 VSIX> -LiveCodexPath <네이티브 Codex.exe>로 별도 테스트 인스턴스에서 수행한다. 개인 프로젝트·설정·클립보드를 테스트 원본으로 사용하지 않는다. 문서·협업·모델·CLI·SCM 개별 재현은 각 tests 폴더 README를 참고한다.

현재 패키지는 VS2017 15.9 이상용이다. VS2019/2022 확장 패키지, 실제 Claude 인증·Fast 과금 호출, 운영 DB·외부 SVN/TFVC·배포 서버는 이번 완료 범위와 구분한다. PDF/구형 Office는 설치된 필터에 따르며 스캔 PDF OCR·원본 그림/서식 전달을 제공하지 않는다. GCM 저장 계정은 서버 재인증과 다르다. 실제 검토 중 공유 내용이 길면 생략 표시와 제한을 적용한다.

최종 완료 2026-10-18 14:38:46 KST. 로컬 검증32분57초, 원격 확인과 보고 마감까지 총 35분 4초다. 제품/패키지는 고정 사본을 유지하고 최종 배포 기록만 후속 커밋으로 반영한다.
