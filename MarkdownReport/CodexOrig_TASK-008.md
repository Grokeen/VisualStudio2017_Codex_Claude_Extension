# TASK-008 — v0.3.0 Chat 기능과 주석 형식 통일

- 작업자: CodexOrig
- 시작: 2026-10-18 02:32:55 KST
- 완료 시각·소요·배포 결과: 아래 완료 기록 참고
- 담당: 제품 구현·기존 회귀/IDE 테스트·개발 문서·고정 VSIX. 사용자 README와 상세 안내는 TASK-009 CodexCode 담당으로 협업했다.

## 요구사항 반영

| Role.md 요구 | 반영 내용 |
|---|---|
| 프로젝트 3: 열린 파일처럼 자유로운 탭 이동 | Chat을 문서 탭(MDI)으로 등록. 실제 VS 프레임의 Float/Dock/MDI 이동 확인. Chat 활성 시 마지막 소스 파일 추적 |
| 프로젝트 4: 이미지/파일 드롭·붙여넣기 | 파일 드롭·다중 파일 선택·이미지/파일 붙여넣기, 썸네일/이름·제거. PNG/JPEG와 텍스트 첨부 |
| 프로젝트 5: 참고 이미지의 구성 | 상단 Codex/Claude Code 탭, 누적 대화, 하단 프롬프트/첨부, 접을 수 있는 설정·변경 검토 |
| 프로젝트 6: 여러 번 보내는 프롬프트 누적 | AI별 요청·응답·초안·첨부 분리, 후속 요청에 최근 대화 전달, 새 대화 |
| 프로젝트 7: instruction | 공통 규칙 입력·명시적 저장, 모든 새 요청의 별도 필드로 전달 |
| 프로젝트 8: 계정/텍스트 기본 설정 | 공식 CLI 로그인 실행·연결 확인, AI별 실행 경로/모델, 글자 크기 10~28, UTF-8 JSON 설정 저장·복원 |
| 기본 규칙 7: 사용자 설득/상세 사용법 README | TASK-009에서 소개·효용·설치·AI 연결·첫 요청·첨부·반복 대화·승인/저장/복구 중심으로 개편 |
| 추가 사용자 지시: C# 주석 형식 | CodexOrig의 축약 주석을 기존 CodexCode의 항목별 줄바꿈·콜론 열로 통일. 생성되는 C# 테스트 코드도 포함 |

기존 현재 파일 수정안 → 비교·승인 → EnvDTE 적용 → 별도 저장·Undo·복구는 유지했다. 다중 파일 자동 수정이나 무승인 적용을 추가하지 않았다. 명세의 일부 미래 항목을 전체 완료로 표시하지 않았다.

## 구현과 제한

- `Chat/ChatWorkspace.cs`: 첨부 당시 내용 고정, 제한된 텍스트 읽기·이미지 형식/픽셀 검사·작은 미리보기, Provider별 메모리 대화, 요청 컨텍스트 및 설정 저장소.
- `Views/AssistantControl.xaml`, `.xaml.cs`, `.Chat.cs`: 대화/입력 UI, 파일 드롭 및 Pasting 이벤트, 이미지 Paste의 PreviewCanExecute/PreviewExecuted 경로, 설정·계정·검토 연결. 낮은 창에서 전체 스크롤, 어두운 테마의 설정/검토 제목 가독성 보완.
- `Providers/CliEditProvider.cs`: 현재 소스·명시적 요청·instruction·최근 대화·텍스트 첨부를 구분한 JSON 전달. Codex 이미지는 고정 임시 스냅샷과 `--image`, Claude 이미지는 stdin stream-json 이미지 블록. 직접 소스 쓰기 권한은 부여하지 않는다. Codex 인수 끝에 `-- -`를 사용한다.
- `Providers/CliAccountService.cs`: Codex `login`/`login status`, Claude `auth login`/`auth status`. 계정 상세 출력은 UI에 노출하지 않고 인증 토큰·암호를 저장하지 않는다. 확인 프로세스는 제한 시간/출력 상한/Job 정리를 적용한다.
- `Services/VisualStudioDocumentService.cs`: 문서 탭 전환 후 마지막 소스 문서를 유지하고 닫힘/창 해제 시 구독·참조를 정리한다.
- 패키지 및 어셈블리 버전: 0.3.0 / 0.3.0.0. 설치 대상은 VS2017 15.9 이상이다.

첨부는 합계4개이며 PNG/JPEG는 각2MB·1600만 픽셀, 텍스트는 각100KB이다. UTF-8 또는 BOM이 있는 Unicode 텍스트를 받는다. 현재 첨부는 제거할 때까지 다음 요청에도 포함된다. 디렉터리·바이너리·범위 초과 입력은 거부한다.

화면 대화는 탭마다 최대100개 메시지를 보존한다. 후속 요청은 최근10개/24000자 이내의 사용자·AI 메시지, 8000자 이내 instruction과 현재 첨부를 포함한다. 대화·첨부·초안은 메모리에 유지하며 디스크 대화 복원은 제공하지 않는다. 저장된 설정은 `%LOCALAPPDATA%/VS2017AIAssistant/settings.json`이다. 모든 UI/IDE 테스트의 설정은 별도 경로로 격리하고 테스트 실행 환경 변수도 복원한다.

## 주석 보정

아래처럼 기존 형식을 적용했다. 클래스/메서드의 설명 내용과 기존 작성자의 기록을 보존했다.

```csharp
/// ---------------------------------------------------------------------
/// <summary>
/// name         : ChatAttachment
/// desc         : 첨부 시점의 텍스트/이미지를 보관하여 이후 파일 변경과 분리한다.
/// author       : CodexOrig
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexOrig : 제한된 크기·형식 검사와 미리보기 추가.
/// </summary>
/// ---------------------------------------------------------------------
```

초기 7개 파일에서 축약형을 보정했으며, 후속 메서드도 같은 형식을 사용했다. `scripts/Test-VsixTestEnvironment.ps1` 및 `tests/build-entry/Test-BuildEntry.ps1`의 생성 C# 주석도 포함한다. 전체 src/tests/scripts 검색에서 `/// name:`·`/// author: CodexOrig`·정렬되지 않은 update date 축약형 0건을 확인했다.

## 검증

| 구분 | 최종 결과 |
|---|---|
| 실제 VS2017 MSBuild 15의 Debug/Release | 경고·오류 0건, VSIX 생성 |
| 편집·CLI·Chat·WPF 회귀 | 71/71 PASS |
| 패키지·테마·화면 | 33/33 PASS |
| 호환성 도구 회귀 | 44/44 PASS |
| 최신 원본 호환성 | VS2017 PASS. VS2019/2022 미선언 |
| 고정 VSIX의 실제 VS2017 통합 | 실제 Codex 포함 35/35 PASS |
| 실제 Codex 이미지+텍스트+대화+instruction | 합성 이미지의 빨간 사각형2개에 맞춰 return1→2, 규칙확인·원본 보존 PASS |
| TASK-009 독립 최종 DLL 검증 | 실제 이미지의 숫자7에 맞춘 변경 및 instruction/history/text/계정/원본 보존 6/6 PASS. 편집71·Foundation33 독립 재현 PASS |
| 고정 이후 제품 소스 변경 | 0건 |

처음 발견한 글자 크기 SelectionChanged 시점/계정 확인 버튼 상태 문제를 수정한 뒤 회귀가 통과했다. 실제 IDE 초기 재현의 Claude 실패는 새 탭의 초안을 명시하지 않았던 테스트 입력 문제였으며, 탭 분리 단언 및 입력을 추가한 최종 실행에서 통과했다. Dark Expander 제목 가독성은 직접 시각 확인과 TASK-009 독립 검토 후 수정·단언을 추가했다.

실제 IDE 검사는 새 root suffix에서 제품 VSIX를 설치하고 실제 메뉴·IVsWindowFrame·EnvDTE·Undo·디스크 저장을 사용했다. 제품 승인 전 사용자 소스 대신 합성 코드만 사용했다. IDE 내 마우스 드래그/키보드 제스처의 수동 검증과 실제 계정 로그인 화면의 인증은 자동화하지 않았다. 이미지 붙여넣기 회귀는 실제 WPF Pasting 이벤트를 사용하며 OS 클립보드를 변경하지 않았다. 실제 Claude 계정 호출은 미검증이다.

- 최종 IDE: `artifacts/ide/run-0ed36ab0384d4c78a2ea5e2ff61ac226/result.json`, 사본 `CodexOrig_TASK-008_IDE.json`
- 최종 편집 회귀: `artifacts/source-editing/run-fcd2341901694925b21300ceb0a16da6/results.log`
- 최종 화면: `artifacts/phase1/`, `artifacts/task009/final-render/`
- 실제 Codex 합성 사각형: `artifacts/chat/live-458d0bd3175d464d8c954db0147db6c1/result.json`
- 최종 고정 DLL 독립 컨텍스트: `CodexCode_TASK-009_LiveContext.json`
- 호환성: `CodexOrig_TASK-008_Compatibility.json`
- 전체 확인 결과와 해시: `CodexOrig_TASK-008_Validation.json`

## 패키지와 재현

공개용 패키지는 `releases/v0.3.0/VS2017.AIAssistant-0.3.0.vsix`이며, 고정 인계 파일과 같은 바이트이다. 기존 v0.2.0 패키지는 변경하지 않았다.

```text
VSIX SHA-256: A7F1C597BA0CCF7B18AC52DEB29BACE2D6B66E300B9A90C6A55EB5A3E7A88DF2
DLL SHA-256 : 28ACEEBE6A83AD63941F20CE38E77682DB40F30794DB4630564AC5AB7912C34C
```

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\editing\Test-SourceEditing.ps1
powershell -NoProfile -NonInteractive -STA -ExecutionPolicy Bypass -File .\tests\foundation\Test-FoundationPackage.ps1
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\compatibility\Test-VsixCompatibility.Tests.ps1
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\ide\Test-IdeIntegration.ps1 -LiveCodexPath 'C:\path\to\codex.exe'
powershell -NoProfile -NonInteractive -STA -ExecutionPolicy Bypass -File .\tests\chat\Test-LiveChat.ps1 -CodexPath 'C:\path\to\codex.exe'
```

마지막 두 명령은 실제 로그인 계정을 사용하며 합성 자료만 전송합니다.

## 남은 범위

실제 Claude 계정, VS2019/2022 패키지, 다중 파일 수정, 프로젝트별 디스크 대화 기록, 수동 제스처·설치 제거·장시간 사용성 검증은 별도이다. 다른 작업자가 진행 중일 때는 Role.md에 따라 GitHub에 배포하지 않는다. 최종 배포 결과는 TASK-009 완료 확인 후 아래에 추가한다.

## 최종 완료 및 GitHub 반영 — 2026-10-18 03:00:29 KST

- 전체 작업 시간: 2026-10-18 02:32:55 ~ 03:00:29 KST, 소요 27분 34초.
- TASK-009의 02:57:20 KST 최종 종료 및 활성 편집 없음 확인 후 배포했다.
- 기능/문서/설치 파일 커밋: `9b3d930ba69a3ef1791e72efe06ef5baff7ee9ff`, origin/main push 성공.
- GitHub 다운로드 URL에서 VSIX를 다시 받아 66,118바이트와 SHA-256 `A7F1C597BA0CCF7B18AC52DEB29BACE2D6B66E300B9A90C6A55EB5A3E7A88DF2`가 실제 IDE 검증한 고정 파일과 일치함을 확인했다.
- 기존 v0.2.0 파일을 보존했고, 최종 원본 해시와 주석 형식·로컬 문서 링크·Git diff 검사를 통과했다. 정상 사용자 프로필에 확장을 자동 설치하거나 Marketplace에 공개하지 않았다.
