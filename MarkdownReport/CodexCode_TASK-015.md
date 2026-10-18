# TASK-015 — 문서 첨부·모델/속도·이름을 가진 AI 협업

- 담당: CodexCode. 시작: 2026-10-18 14:01:49 KST.
- 상태: 담당 구현과 최종 제품 DLL/문서 도우미 검증 완료. 완료 시각과 배포 인계는 아래에 기록한다.
- 요구: 최신 Role.md의 문서(PPT/PDF/DOC 등) 첨부, image-7~11 모델·추론 수준·Fast·권한 예시, 입력/설정의 주제별 구분, 대화별 이름·역할을 통한 협업, SCM 계정명과 구분자를 이용한 기본 이름.

TASK-014 CodexOrig가 진행하던 대화 목록 드래그/숨김·상태 표시 변경은 v0.6.0으로 로컬 고정했다. 새 요구는 v0.7.0에 통합한다. TASK-015는 신규 문서·모델 능력·협업·SCM 이름 서비스 및 `Providers/CliEditProvider.cs`, 신규 회귀 검사를 담당한다. TASK-016 CodexOrig는 기존 Chat 모델/Markdown·Views 연결, csproj·버전·README·기존 검사·제품 빌드·실제 IDE·최종 배포를 담당한다. 두 작업과 하위 작업자가 끝나기 전에는 push하지 않는다.

## 문서 첨부

`Chat/DocumentAttachmentReader.cs`는 DOCX/PPTX/XLSX, ODT/ODP/ODS, RTF/HTML 및 텍스트의 본문을 읽는다. Office ZIP에서는 정해진 본문·슬라이드·시트 XML만 순서대로 읽고 DTD·외부 리소스를 실행하지 않는다. 원본을 수정하거나 Office 애플리케이션·매크로를 실행하지 않는다. 원본 최대16MB, ZIP2048개 항목·확장 합계32MB·XML4MB, 추출 텍스트는 UTF-8 100KB로 제한한다. 너무 긴 문서는 필요한 부분으로 나누도록 안내한다.

PDF·구형 DOC/PPT/XLS는 Windows에 등록된 IFilter를 사용한다. `DocumentWorkerLauncher.cs`와 별도 `VS2017.AIAssistant.DocumentReader.exe`로 원본의 읽기 전용 임시 사본만 전달한다. 도우미는15초·256MB Job 제한과 취소를 적용하며 자기 하위 프로세스만 종료한다. PC에 맞는 필터가 없거나 암호 문서/추출 가능한 글자가 없는 PDF인 경우 명확한 원인을 안내한다. 스캔 PDF의 OCR 기능은 포함하지 않는다. 현재 PC에서 실제 DOC 본문·PPT 슬라이드/노트·XLS 셀·합성 PDF 본문 추출을 확인했다.

생산 도우미 프로젝트는 `src/VS2017.AIAssistant.DocumentReader/VS2017.AIAssistant.DocumentReader.csproj`다. 제품에는 Reader/Launcher만 컴파일하며 `DocumentFilterWorker.cs`는 도우미 전용이다. 도우미 exe와 config를 VSIX에 함께 넣어 제품 DLL 옆에 배치한다. 추가 NuGet 문서 파서나 Office 자동화 의존성은 없다. UI는 ExtractAsync를 사용해 문서 필터 대기가 IDE를 멈추지 않게 연결한다.

## 모델·추론·Fast와 권한

`Providers/ProviderExecutionOptions.cs`는 공개 모델 메타데이터인 로컬 Codex `models_cache.json`을 최대2MB의 엄격 JSON으로 읽는다. 실제 모델 ID·표시명·광고된 추론 수준·기본 수준·Fast 능력을 제공한다. 계정 비밀 파일은 읽지 않는다. 캐시가 없거나 손상된 경우 경고와 CLI 기본/직접 입력 경로를 제공하며, 예시 모델을 계정에서 사용할 수 있다고 보장하지 않는다. 직접 입력한 미확인 모델에는 기본 추론 수준만 허용하고 고급 수준/Fast를 추정해 켜지 않는다.

Codex Fast는 `--config service_tier="fast"`와 `features.fast_mode=true`, 추론 수준은 해당 모델의 광고 값으로 전달한다. Claude는 공식 모델/별칭의 지원 범위와 최소 CLI 버전을 제공하고, print Fast는 `--settings '{"fastMode":true}'`로 요청한다. 표준 Claude 요청은 false를 명시한다. Fast·추론 수준·모델은 별개의 선택이며, 빠른 속도를 낮은 추론 수준으로 대체하지 않는다. 실제 계정/모델의 Fast 사용 권한과 추가 사용량은 서비스 정책에 따른다.

`CliEditProvider.cs`는 helper가 검증한 인수를 실제 네이티브 argv로 전달한다. 최소 버전이 필요한 기능에는 고정 `--version` 명령의 최대4KB 출력·10초/Job 제한을 적용한다. 오래된 Claude print Fast는 실제 요청 전에 차단한다. CLI 오류는 모델/수준/속도/버전·로그인 상태 확인을 안내하고 계정 관련 stderr 원문은 노출하지 않는다.

권한의 전체 허용 범위는 협업 중 확인된 사용자 답변에 따라 확장의 DB·형상관리·배포 작업이다. AI CLI의 파일 직접 쓰기·셸 권한 해제와 구분하며 read-only/no-tools 수정안 생성, 한 파일의 비교·승인·별도 저장, 개별 프로젝트 명령 계획 동의는 유지한다. UI의 권한 프리셋과 주제별 입력 구역은 TASK-016이 연결한다.

공식 근거: [Codex Speed](https://learn.chatgpt.com/docs/agent-configuration/speed), [Codex 설정](https://learn.chatgpt.com/docs/config-file/config-reference), [Claude Fast](https://code.claude.com/docs/en/fast-mode), [Claude CLI](https://code.claude.com/docs/en/cli-reference), [Claude 모델 설정](https://code.claude.com/docs/en/model-config). 세부 출처/사용 API는 `tests/provider-options/README.md`에 있다.

## 고유 이름과 실제 협업

`Chat/ConversationCollaboration.cs`는 대화 ID/AI별 고유 이름40자·역할1000자·팀80자를 `workspace/collaboration.json`에 보관한다. 기본 이름은 Codex-id8/Claude-id8이며 제공된 계정명이 있으면 `계정명::Codex-id8` 형식으로 만든다. 기존 사용자가 지정한 이름은 다시 생성하지 않는다. 고유성·Unicode·오래된 UI 리비전 충돌을 검사한다.

협업은 실제 요청→수신→회신→전달과 실패/재시도/취소 상태를 저장한다. 주소가 맞는 같은 팀의 열린 대화끼리만 전달할 수 있다. 닫힌 발신자의 회신은 보관하고 재열기 후 전달한다. 공유 요청6000자(JSON 표현6500자)·회신12000자·오류2000자, 최근32개 상태 이벤트를 보관한다. 자기 이름/역할·명시 수신한 작업만 최대8000자 별도 AgentContext로 구성해 공통 instruction과 분리해서 stdin JSON에 넣는다. 다른 대화나 비밀 파일을 탐색해 자동 공유하지 않는다.

UI에서 사용자가 선택한 참여자만 순서대로 실제 CLI를 호출하고 현재 AI가 결과를 조정한다. 중간 수정안은 자동 적용하지 않는다. `workspace/collaboration-report.md`는 실제 담당자·요청·회신·오류/상태 이력만 원자 저장한다. 서비스는 가짜 답변이나 도구 실행 기록을 만들지 않는다. metadata는4MB로 제한하고 named mutex 및 원자 교체로 프로세스 간 저장을 직렬화한다. 실제 스트레스에서 확인한 일시 파일 교체 잠금은 해당 오류 코드에만 최대375ms 재시도한다. 영구 잠금은 원문을 보존한 채 오류로 알린다.

SCM 기본 이름의 출처는 새 `Services/ScmIdentityService.cs`가 제공한다. Git 프로젝트에서 로컬 작성자 설정을 읽으며 이것을 로그인 검증으로 표시하지 않는다. 정확한 GitHub origin과 설치된 GitHub CLI가 있는 경우 고정 사용자 조회로 확인된 로그인 계정을 우선한다. 그다음 Git Credential Manager의 고정 `github list --no-ui`로 공개 저장 계정명만 읽는다. 한 계정이거나 원격/credential username과 일치하는 계정만 선택하고 여러 계정 중 임의로 선택하지 않는다. 저장 계정은 `HasStoredAccount=true`, `AuthenticationVerified=false`로 서버 인증 확인과 구분한다. 토큰·credential fill·인증 파일은 읽거나 반환하지 않는다. 전체 조회5초·출력4KB·Job 취소를 적용한다. 공식 근거는 [GCM 사용법](https://github.com/git-ecosystem/git-credential-manager/blob/main/docs/usage.md), [복수 계정](https://github.com/git-ecosystem/git-credential-manager/blob/main/docs/multiple-users.md), [GitHub CLI API](https://cli.github.com/manual/gh_api)다.

계정 조회는 UI를 막지 않고 수행한다. 뒤늦게 계정명을 확인해도 리비전1의 자동 기본 이름에만 `계정::AI-ID`를 붙인다. 이름/역할을 저장한 대화와 다른 프로세스에서 변경한 리비전은 보존한다. 계정을 확인할 수 없을 때는 출처/인증 미확인 상태를 보여 주고 사용자가 이름을 바꿀 수 있다.

## 검증과 재현

독립 구성 요소 검사 후 최종 제품 DLL `BB80BC8ED422F6F36857AB7367A5B5432A65218E8CFE6F51F94A1143A16A1D44`와 함께 포장된 문서 도우미를 직접 참조해 검사했다. 아래 수치는 최종 제품 기준이며 중간 후보의 재실행 횟수를 중복 합산하지 않는다.

| 검사 | 최종 제품 근거 |
| --- | --- |
| 모델 메타데이터·광고 수준·Fast argv·잘못된 캐시·네이티브 인수 | 65/65 PASS, [JSON](CodexCode_TASK-015_ProviderOptions.json) |
| 협업 이름·전달/회신·실패/재시도·잠금·동시 프로세스·보고서 | 60/60 PASS, [JSON](CodexCode_TASK-015_Collaboration.json); 별도 소스 스트레스48개 프로세스 실패0 |
| 문서 실제 추출·경계·원본/작업 종료 | AnyCPU47/47·x86호출47/47 PASS, 동일47개 사례의 비트수별 실행, [JSON](CodexCode_TASK-015_Documents.json) |
| 제품 Provider 실제 argv/stdin·버전 거부 | 15/15 PASS, [JSON](CodexCode_TASK-015_ProviderCli.json) |
| SCM 이름·출처와 인증 구분 | 63/63 PASS, [JSON](CodexCode_TASK-015_ScmIdentity.json) |

신규 서비스는 고유250개 사례를 모두 통과했고, 같은47개 문서 사례를32비트 호출에서도 추가 실행했다. 초기 제품 DLL 모드의 SCM 검사1개는 복사한 Git/GCM fixture 옆에 의존 DLL이 없어 실패했다. `ScmIdentityTests.cs`에서 제품 참조 모드에만 동일 DLL을 함께 복사하도록 검사 도구를 보정하고63개 모두 재실행했다. 제품 소스나 고정 패키지는 변경하지 않았다.

문서의 x86 검사는 VS2017과 같은32비트 호출 프로세스에서 실제 패키지 AnyCPU 워커와 통신한다. 64비트 Windows에서는 해당 워커가64비트로 실행된다. 별도 x86 워커의 실제 필터 추출은 앞선 소스 검사에서 검증했다. 암호 문서의 오류 응답은 fixture 상태 코드 전파 검사이며 실제 암호 문서를 검증한 결과로 주장하지 않는다. 실제 GCM 검사는 계정명 없이 [공개 상태만 기록](CodexCode_TASK-015_GcmMetadata.json)했다.

VSIX275,314바이트·SHA-256 `DB9C5D706760D693A2BB758E9E1E6DC2C0BF1DB4B28FA134F3307B35ABEBC059` 안의 DLL/worker exe/config 세 항목은 고정 사본과 바이트 해시가 같다. 제품 소스36개는 handoff의 해시와 모두 일치하고 최신 Role 원문도 검토 사본과 일치한다. [패키지 검증](CodexCode_TASK-015_Package.json).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/collaboration/Test-ConversationCollaboration.ps1 -AssemblyPath <고정 제품 DLL>
powershell -NoProfile -ExecutionPolicy Bypass -File tests/provider-options/Test-ProviderOptions.ps1 -ProductAssemblyPath <고정 제품 DLL>
powershell -NoProfile -ExecutionPolicy Bypass -File tests/provider-options/Test-ProviderCliIntegration.ps1 -AssemblyPath <고정 제품 DLL>
powershell -NoProfile -ExecutionPolicy Bypass -File tests/provider-options/Test-ScmIdentity.ps1 -ProductAssemblyPath <고정 제품 DLL>
powershell -NoProfile -ExecutionPolicy Bypass -File tests/documents/Test-DocumentAttachments.ps1 -AssemblyPath <고정 제품 DLL> -WorkerPath <같이 배포한 도우미 EXE> -Platform AnyCPU
# VS2017 호출 프로세스와 같은 비트수 검사는 마지막 인수를 -Platform x86으로 바꿔 실행한다.
```

제품 Debug/Release는 TASK-016에서 경고·오류0으로 고정했다. 기존 회귀·실제 VS2017와 최상위 실행기13묶음, README/사용 가이드·화면 및 원격 배포 근거는 [TASK-016 보고서](CodexOrig_TASK-016.md)에서 기록한다. 모델·이름/역할·대화 화면3개를 직접 열어 주제별 검정톤 구분·입력/저장 버튼·스크롤 영역을 확인했다. 협업의 완료 후 저장 실패·원인 오류 보존·설정 롤백3곳도 최종 소스로 재검토했다. 사용자 Role.md와 이미지, 기존 v0.2~v0.6 릴리스는 보존한다. 새 클래스/메소드에는 Role의 한국어 name/desc/author/create date/update date 주석을 적용한다.

현재 VSIX는 VS2017 15.9+용이다. 실제 Claude 로그인/Fast 과금 요청, GitHub CLI 실계정 및 외부 SVN/TFVC·운영 DB/배포는 검증 범위를 구분한다. PDF/구형 Office는 해당 PC 필터에 따라 지원이 달라진다. VS2019/2022 패키지 지원과 OCR은 이번 구현 범위에 포함되지 않는다.

## 완료와 배포 인계

담당 작업 완료: 2026-10-18 14:33:59 KST, 소요 32분 10초 (시작14:01:49). 신규 서비스 고유250개 사례와 별도 x86 문서47개 실행을 모두 통과했다. [종합 검증 JSON](CodexCode_TASK-015_Validation.json). 모든 하위 작업자의 편집·개인 테스트 프로세스는 종료했고 제품/패키지는 변경하지 않는다. 최종 전체 실행기·실제IDE와 GitHub 반영은 TASK-016 CodexOrig에 인계했다. GitHub 배포는 그 담당자의 검증·문서 완료 후 진행한다.
