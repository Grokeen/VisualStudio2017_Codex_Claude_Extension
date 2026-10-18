# TASK-032 / CodexOrig / 메시지 표시·교차 AI 협업·프로젝트 동기화

- 시작: 2026-10-19 03:42:50 KST. 상태: v0.11.0 구현·통합 검사·독립 감사·GitHub 배포와 원격10파일 확인 완료.
- 사용자 이미지의 노란 표시는 로고 위 불투명 가로 띠, 초록 표시는 사용자 메시지 배경이었다. 이후 Role.md 03:42 추가 요청의 확장 제거 방법·서로 다른 AI 소통·image-39 프로젝트 외부 수정 경고를 같은 작업으로 연결했다.
- 문서 담당 CodexOrig3 TASK-033의 README/USER_GUIDE 제거·협업 설명을 수신했고 04:01:32 KST 소유 반납을 확인했다. 제품·통합 검사는 CodexOrig, 독립 감사는 사용자 지정 CodexOrig2 TASK-034다. 과거 보고서·감사·설치 MD/BAT/PDF/ZIP은 덮어쓰지 않는다.

## 변경

1. `AssistantControl.xaml`의 사용자 메시지는 투명 바탕과 테마의 회색 글자를 사용한다. `MarkdownPreview` 생성자의 로컬 Background/Foreground 지정이 메시지 템플릿보다 우선하던 원인을 제거하고 기본값을 스타일로 옮겼다. `AssistantTheme.xaml`에는 Markdown 본문에만 적용되는 투명 RichTextBox/viewport 템플릿을 추가했다. 코드 블록·표·입력란의 명시 배경과 편집 기능은 유지한다.
2. `AssistantControl.Options.cs`에서 같은 팀의 열린 Claude·Codex 대화를 협업 참여자로 교차 선택한다. 주 담당 외 최대3, 각 제공자의 CLI·모델·생각 강도·속도를 사용한다. 동료가 순서대로 한 번 검토하고 주 담당이 종합한다. 모호한 Guid·다른 팀·닫힌 참여자는 거부하고 실패·취소 뒤 주 담당 호출을 막는다. 동료의 제안은 소스에 적용하지 않으며 재귀 자동 대화는 없다. 선택 소스·요청·첨부가 선택한 서비스에 공유되고 호출별 사용량이 발생함을 화면·문서에서 설명한다.
3. `VisualStudioExternalEditingScope.cs`는 열린 텍스트뿐 아니라 로드 프로젝트 파일을 실행 범위에서 보호한다. 저장 상태·2MiB 해시·열거 상한을 확인한 뒤 파일 알림을 일시 중지한다. 완료·실패·취소 시 깨끗한 프로젝트만 다시 읽고 알림을 복원한다. 실제 기본 C# 프로젝트에는 `IVsPersistHierarchyItem2`가 없어 `IPersistFileFormat.IsDirty`와 `IVsSolution4.UnloadProject/ReloadProject` 호환 경로를 사용한다. 재로딩 때 닫힌 텍스트 탭은 다시 열고 원래 활성 창으로 돌아간다. 미저장·삭제·동기화 실패는 덮어쓰지 않고 안내한다. IDE의 전역 파일 변경 설정은 바꾸지 않는다.
4. 확장 제거 방법은 VS2017 도구→확장 및 업데이트, VS2019/2022 확장→확장 관리에서 해당 확장만 제거하고 모든 IDE 창을 닫는 절차다. CLI·계정·대화 데이터 삭제와 구분하며 실제 사용자 설치를 제거하지 않았다.
5. `VisualStudioDocumentService.ResolveOpenDocument`는 프로젝트 재로딩 뒤 활성/최근 문서를 현재 `DTE.Documents`와 대조한다. 닫힌 COM 참조는 버리고 소스 없는 솔루션 문맥으로 돌아간다. VS2022에서 C13의 프로젝트 검사 통과 뒤 마지막 CloseAll 단계에 나타난 E_INVALIDARG를 보완했다. 검사기는 실패 결과도 보존한 뒤 자기 PID·RootSuffix가 일치하는 전용 IDE만 정리하도록 수정했다.

## 재현과 검증 구분

- 동결 C13: `artifacts/task025/candidate13/source-hashes.json` 120입력, 각 SKU Debug/Release 6빌드 오류0·경고0. 준비 영수증 `artifacts/task032/candidate13-readiness.json`. 최초 C11 컴파일 실패 및 C12 실제 프로젝트 사전검사 실패는 별도 원문으로 보존했다.
- 실제 VS2017: `artifacts/ide/run-6bfbcbfbfeb441bdba5872afc2fbc673/result.json` **145/145 PASS**, 제품 SHA256 `D8FD54FBEA75CA881E603D7F34B1DA12895534BF0E2F82557574438574D7AD3C`. 실제 프로젝트 속성의 변경 중 보존→종료 후 반영→바이트/모델 복원→미저장 차단을 확인했다. `ui032-light.png`/`ui032-dark.png`에서 여러 본문 행과 가로 띠 없는 로고·투명 회색 사용자 메시지를 직접 검토했다.
- C13 실제2019도145/145였으나 2022는 마지막 소스 없는 문맥에서 닫힌 COM 참조 오류로 실패했다. `artifacts/ide/run-43eabf275b6844f9bab0ac1198fdd6a6/result.json`을 보존했고 통합 성공으로 집계하지 않았다. C14는120입력·6빌드0/0로 새로 동결했으며 2022부터 재실행해145/145를 통과했다. 당시 최종2017/2019·전체16·감사 후속은 진행 중이었으며 아래 최종 C14 표로 종결한다.
- 비연결 WPF C10 메시지 검사46/46, 협업 포함 WorkspaceExperience 성공(신규 교차 제공자10조건 포함), 실행기15/15, C13 시큐어22/22를 확인했다. 비연결 FlowDocument 캡처에서는 일부 본문이 그려지지 않는 하네스 한계가 있었고, 픽셀 coverage=1만으로 실제 로고 중첩 성공을 판단하지 않았다. 실제 연결 IDE 캡처와 속성 검사를 별도로 사용했다.
- 초기 before2 로고 픽셀 진단은 기존 오류 Toast와 잘못 적용한 테마 자원이 섞여 유효한 제품 재현 근거로 사용하지 않는다. 정리한 C8 before3에서는 사용자 투명/회색8조건 실패, C10에서는 수정 후46조건이 통과했다. 과거 실패 원문은 삭제하지 않았다.
- 재실행: 최상위 실행기에 최종 VSIX를 지정해 `--all --vsix <VSIX>`; 실제 IDE는 `tests/ide/Test-IdeIntegration.ps1 -VisualStudioPath <설치폴더> -VsixPath <같은SKU VSIX>`; 실행기 자체는 `tests/launcher/Test-TestLauncher.ps1`. UI 실제 이벤트와 격리된 합성 프로젝트를 사용하며 개인 프로젝트·실계정·운영 서버 호출을 하지 않는다.

## 보안 검토·한계

교차 제공자 호출 전 참여자/팀을 확인하고 기존 취소·소스 승인 경계를 유지했다. 자격 증명을 저장·복제하지 않고 참여자의 기존 CLI 경로만 사용한다. 프로젝트 자동 다시 읽기는 요청 실행 범위에만 적용하고, 실행 밖의 타 프로그램 변경까지 IDE 경고를 숨기지 않는다. 미저장 프로젝트/문서는 차단·보존하며 지원하지 않는 저장/재로딩 서비스는 직접 쓰기 전에 거부한다. 파일 해시와 프로젝트 열거를 제한하고 COM 이벤트·알림·읽기 전용 상태는 정리 경로에서 복원한다.

실제 Claude CLI 계정, 운영 DB·외부 TFVC·운영 배포는 미검증이다. Claude 앱·브라우저 로그인과 CLI 로그인은 별개다. 합성 제공자 양방향 성공을 실제 계정 성공으로 확대하지 않는다. v0.10 C8의 물리 마우스28/GIF24 성공과 당시 C7 1회 원인 미확정 실패는 기존 TASK025/028 기록을 보존하며 C13 직접 실행으로 바꾸어 쓰지 않는다.

프로젝트 알림 근거: [IgnoreFile](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.ivsfilechangeex.ignorefile?view=visualstudiosdk-2022), [SyncFile](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.ivsfilechangeex.syncfile?view=visualstudiosdk-2022), [프로젝트 영속화](https://learn.microsoft.com/en-us/visualstudio/extensibility/internals/project-persistence?view=visualstudio), [ReloadProject](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.ivssolution4.reloadproject?view=visualstudiosdk-2022). 실제 제공 인터페이스는 격리 IDE의 `project-interface-diagnostic.json` 관측으로 확인했다.

## 최종 C14 검증

[검증 JSON](CodexOrig_TASK-032_Validation.json)에120입력의 상대경로·바이트 SHA, 세 DLL/VSIX, 실제3IDE 결과·화면 SHA, 전체16 요약과 보존 범위를 기록했다. 최종 입력 목록 SHA256은 `B64FF86800FDD450EE6C37D0970DAB3B03CAF2C6D803A50662F53AE4F1ADF6EE`이며 검증 후 작업 트리와 차이0이다.

| 검사 | 최종 C14 결과 |
|---|---|
| 세 SKU Debug/Release | 6빌드, 오류0·경고0 |
| 실제 VS2017·2019·2022 | 각각145/145, 설치된 DLL SHA가 고정 후보와 일치 |
| 최상위 실행기 기본 전체 | 16/16·exit0, `artifacts/tl/12f0f3cf46` |
| Markdown·hover·안내·계정·실제 제품 UI | 247/247 =111+35+31+24+46, 전체16 안에 포함 |
| WorkspaceExperience | 145통과, 교차 제공자 신규10조건 포함, 합성 제공자 사용 |
| 독립 Markdown 보안·문서 UI | 23/23·117/117, 전체16 안에 포함 |
| 각 SKU 테마·번들 | 각각31/31·media10개 및 MIT/제품·Avalon DLL 일치 |
| 실행기 자체 | 15/15 |
| 보호 원본 | 기존47개 해시 일치, 이전 설치 자료·고객ZIP 불변 |
| 고객 문서 | README·USER_GUIDE·0.11 설치 안내의 로컬 링크55개, 누락0 |

위 검사는 중복 포함 관계가 있어 고유 전체 건수로 합산하지 않는다. 2022 검사 전용 IDE의 늦은 종료는 결과를 보존하고 PID·RootSuffix를 대조해 해당 프로세스만 정리했다. 일반 사용자 IDE 프로필의 확장을 자동 교체한 것은 아니며 새 VSIX를 설치해 사용한다.

| SKU | 최종 VSIX SHA256 |
|---|---|
| 2017 | `33EA90EBAC97ACC27CAE6D00A0BD33DC2796A9C0F63D560C64780698828543C2` |
| 2019 | `A54F1AD5C9096B9EEDF15E33CE9331F8A3084DDF28B13F308518AEB147B579CA` |
| 2022 | `94B0D919687F519FA74714D6B6E2DCF02FC55BD271FB820695F70AD306501249` |

CodexOrig2의 [TASK034 독립 감사](CodexOrig2_TASK-034_Audit.md)는 최종 C14 세 SKU에 동일15조건을 각각 직접 통과했고, C13/C14에서 바이트가 같은 프로젝트 보호 원문을 SDK 경계 shim과 컴파일한11사례를 별도로 확인했다. 추가로 확정한 제품 결함은 없다고 판정했다. 소스 fixture·실제 DLL headless·root 실제 IDE·실계정 미검증을 구분한다. 감사자는 2026-10-19 04:25:05 KST 최종 인계 보강까지 종료하고 소유권을 반환했다. [감사 JSON](CodexOrig2_TASK-034_Evidence.json)과 실제 IDE의 검증 주체를 구분해 확인했다. GitHub 원격 결과는 별도 배포 JSON으로 남긴다.


## 배포 마감

완료 2026-10-19 04:27:32 KST, 총경과 00:44:42.4498210. 제품 커밋은 7381415297dd277c3be12df84ce44b26d07a52ff이며, [배포 JSON](CodexOrig_TASK-032_Deployment.json)에 GitHub immutable commit URL에서 다시 받은10개 파일의 바이트·SHA256 일치를 기록했다. 세 VSIX·실행기·기존 MD/BAT/PDF/고객ZIP·C7안내 PDF/ZIP을 대조했다. 전원 작업 종료 후 게시했으며 이 확인 기록을 추가 게시해 마감한다.
