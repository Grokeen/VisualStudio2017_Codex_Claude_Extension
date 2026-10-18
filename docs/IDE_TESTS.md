# 실제 Visual Studio 통합 테스트

## v0.9.0 현재 검사

VS2017·2019·2022마다 지정한 VSIX를 별도 실험 인스턴스에 설치한다. 테스트 보조 프로젝트도 해당 SDK/아키텍처로 생성하며, 실제 로드한 제품 DLL의 SHA256을 VSIX에서 추출한 DLL과 비교한다. 기본 경로는 VS2017이며 다른 버전은 `-VisualStudioPath`로 선택한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\ide\Test-IdeIntegration.ps1 `
  -VisualStudioPath 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community' `
  -VsixPath .\releases\v0.9.0\VS2019.AIAssistant-0.9.0.vsix

powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\ide\Test-IdeIntegration.ps1 `
  -VisualStudioPath 'C:\Program Files\Microsoft Visual Studio\2022\Community' `
  -VsixPath .\releases\v0.9.0\VS2022.AIAssistant-0.9.0.vsix
```

기존 편집·SQL·대화 검사에 실제 선택/역방향 선택/선택 해제, 전체 원문과 충돌 해시 보존, 다른 미저장 문서의 직접 실행 차단을 추가했다. 네이티브 프로토콜 fixture는 실제 자식 프로세스로 파일을 쓰고 PowerShell 검증을 실행한다. UI의 실제 토큰 툴팁·공개 진행 메시지·변경 카드·완료 상태도 검사한다. 이것은 Claude 계정 검증과 구분한다. 소스 문서가 없어도 저장된 솔루션이 있으면 전체 액세스 실행 문맥을 만들 수 있다.

`license-observation.json`은 해당 세션의 표시된 WPF 텍스트에서 라이선스 안내를 관찰한 결과다. 라이선스 자체의 유효성이나 다음 실행의 안내 부재를 인증하지 않는다. `scripts/Check-VisualStudioLicense.cmd`가 최신 관찰 결과와 정식 계정 갱신 방법을 안내한다. 최신 수치·패키지 해시는 [TASK-020](../MarkdownReport/CodexOrig_TASK-020.md)을 기준으로 한다. 아래는 버전별 누적 이력이다.

창 모드 전환은 [IVsWindowFrame.SetProperty](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.ivswindowframe.setproperty?view=visualstudiosdk-2022)의 HRESULT와 실제 읽은 모드를 함께 기록한다. 전환 후 표시하고 제한 시간 동안 UI를 진행시켜 지연 완료와 API 실패를 구분한다. 성공하지 않은 전환을 통과로 처리하지 않는다.

작성: CodexCode / 2026-10-18. TASK-005의 기존 검증 대상은 v0.2.0, VS2017 Community 15.9.83, .NET Framework 4.6.2이다. 기존 편집 55개·Foundation 29개·호환성 44개에 더해 **실제 IDE 25/25 PASS**를 확인했다. 실제 Claude 계정 호출 및 VS2019/2022 지원 검증은 포함하지 않는다.

## 실행

VS2017의 확장 개발 워크로드와 .NET Framework 4.6.2 targeting pack이 필요하다. 먼저 제품을 Release 빌드하고 `tests/editing/Test-SourceEditing.ps1`을 실행하여 CLI fixture를 생성한다.

기본 VSIX는 빌드한 `src/VS2017.AIAssistant/bin/Release/VS2017.AIAssistant.vsix`다. 배포 패키지를 검사하려면 `-VsixPath .\releases\v0.5.0\VS2017.AIAssistant-0.5.0.vsix`를 지정한다. 실행기는 지정 VSIX에서 DLL을 실행별 폴더로 추출하고 해시를 확인한 뒤, 그 DLL을 참조하여 보조 테스트 패키지를 빌드한다. 공유 Release 폴더를 재빌드해도 고정 VSIX의 검증은 영향을 받지 않는다.

```powershell
# CLI fixture를 사용한 실제 IDE 검사 (유료 AI 호출 없음)
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\ide\Test-IdeIntegration.ps1

# 로그인된 실제 Codex와 SQL 작성 포함: 합성 소스·스키마만 전송
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\ide\Test-IdeIntegration.ps1 `
    -LiveCodexPath 'C:\path\to\codex.exe'
```

실행마다 `CodexCodeTest<고유번호>` root suffix를 만들고 제품 VSIX와 테스트 보조 VSIX를 설치한다. 해당 suffix에 `/UpdateConfiguration`을 실행하고 `/ResetSettings General`로 첫 실행 프로필을 초기화한다. 사용자 기본 프로필은 초기화하지 않는다. 로그인을 자동 수행하거나 라이선스를 변경하지 않는다. Microsoft의 [실험 인스턴스 설명](https://learn.microsoft.com/en-us/visualstudio/extensibility/internals/createexpinstance-utility?view=vs-2019), [ResetSettings 옵션](https://learn.microsoft.com/en-us/visualstudio/ide/reference/resetsettings-devenv-exe), [패키지 로드 설명](https://learn.microsoft.com/en-us/visualstudio/extensibility/loading-vspackages)을 참고했다.

테스트 보조 패키지가 해당 IDE 프로세스 안에서 등록된 제품 메뉴 명령을 실행한다. 제품 도구 창이 주입한 실제 `VisualStudioDocumentService`를 사용하고 실제 WPF 버튼 이벤트를 호출한다. AI 응답만 fixture로 대체하는 22개와 실제 Codex를 포함하는 추가 3개를 구분한다. 이 테스트는 마우스·키보드 조작의 수동 사용성 검사를 대신하지 않는다.

실행 결과는 `artifacts/ide/run-<고유번호>/`에 저장한다. `launch.json`, 설치 로그, `helper-build.log`, `ActivityLog.xml`, `progress.log`, `result.json`, 합성 솔루션을 보관한다. 정상 종료 시 테스트 소유 IDE만 종료한다. 제한 시간에 결과가 없으면 시작 화면과 로그를 확인하며, 다른 IDE 프로세스를 강제 종료하지 않는다. 테스트용 확장은 별도 suffix에 남는다. 테스트 보조 패키지는 제품 VSIX에 포함하지 않는다.

## 테스트 케이스

| ID | 실제 IDE에서 확인한 내용 | 기대 결과 |
|---|---|---|
| IDE-01 | 등록된 메뉴 명령 실행 | 설치된 제품 패키지 로드 |
| IDE-02 | 호스트 버전 | VS2017 / DTE 15.0 |
| IDE-03 | 도구 창 생성 | 실제 프레임·WPF 콘텐츠 생성 |
| IDE-04 | 도구 창 서비스 초기화 | 생성 버튼 활성화 |
| IDE-05 | 문서 서비스 타입 | 제품의 실제 EnvDTE 어댑터 사용 |
| IDE-06 | 소스 문서 없이 요청 | 오류 안내, 수정 없음 |
| IDE-07 | 활성 파일 캡처 | 버퍼 원문·저장·읽기 전용 상태 일치 |
| IDE-08 | 프로젝트 컨텍스트 | 프로젝트명 및 Framework 4.6.2 포함 |
| IDE-09 | 저장하지 않은 한글 주석 추가 | 현재 버퍼에 포함, 디스크 보존 |
| IDE-10 | Codex fixture 생성 | 정확한 return 1 → return 2 수정안 표시 |
| IDE-11 | 미리보기 직후 | 승인 해제, 적용 버튼 비활성 |
| IDE-12 | 승인 전 | 실제 버퍼와 디스크 원본 유지 |
| IDE-13 | 다른 파일을 연 뒤 승인 적용 | 요청 시점 파일만 변경 |
| IDE-14 | 적용 직후 | 버퍼만 변경, 디스크 미저장 |
| IDE-15 | IDE Edit.Undo 1회 | 기존 미저장 편집까지 적용 전 상태 복구 |
| IDE-16 | 적용 내용 저장 | IDE 저장 상태와 디스크 수정안 일치 |
| IDE-17 | 저장 바이트·개행 | UTF-8 BOM 및 CRLF 유지 |
| IDE-18 | 전용 되돌리기 | 미저장 주석을 포함한 적용 전 버퍼 복구 |
| IDE-19 | 되돌리기 직후 | 디스크는 별도 저장 전까지 유지 |
| IDE-20 | 미리보기 이후 사용자 편집 | 적용 거부, 새 편집 보존 |
| IDE-21 | Claude fixture로 전환·생성 | CLI envelope 파싱 및 정확한 수정안 표시 |
| IDE-22 | Claude fixture 승인 적용 | 실제 EnvDTE 버퍼 변경, 디스크 보존 |
| IDE-23 | 실제 Codex 계정으로 생성 | 정확한 수정안 표시 |
| IDE-24 | 실제 Codex 생성 후 승인 전 | 버퍼·디스크 유지, 적용 비활성 |
| IDE-25 | 실제 Codex 승인·적용·저장 | VS2017이 디스크에 수정 내용 저장 |

독립 회귀 검사는 외부 디스크 변경, 읽기 전용, 잘못된 JSON·교체 범위, 취소·시간 초과·하위 프로세스 종료도 다룬다. 실제 IDE의 외부 변경 알림, 저장 취소, 문서 닫기/이름 변경, 테마 전환·도킹·반복 개폐·제거, 실제 Claude 인증은 추가 검증 대상이다.

결과 근거: [TASK-005 보고서](../MarkdownReport/CodexCode_TASK-005.md), [실제 Codex 포함 25개 결과](../MarkdownReport/CodexCode_TASK-005_IDE.json).

## v0.3.0 추가 IDE 검사 (CodexOrig / TASK-008)

기존 IDE-01~25에 다음 Chat 사례를 추가했다. Provider별 초안을 분리하므로 Claude 탭으로 전환한 뒤 해당 탭에 프롬프트를 입력한다. 실행 결과와 실제 Codex 포함 여부는 TASK-008 보고서/JSON을 기준으로 한다.

| ID | 검사 |
|---|---|
| CHAT-01 | Chat 제목과 기본 문서 탭 모드 |
| CHAT-02 | 프레임 분리(Float) 전환 |
| CHAT-03 | 도킹(Dock) 전환 |
| CHAT-04 | 문서 탭(MDI)으로 복귀 |
| CHAT-05 | 소스→Chat 탭 전환 후 마지막 소스 대상 유지 |
| CHAT-06 | 실제 IDE 첨부 목록에 텍스트 추가 |
| CHAT-07 | 설정 저장 후 새 컨트롤의 규칙/글자 크기 복원 |
| CHAT-08 | 설정 파일을 테스트 디렉터리에 격리 |
| CHAT-09 | 실제 요청/응답 누적 |
| CHAT-10 | Provider 탭의 초안/첨부 분리 |

마우스의 실제 드래그 제스처와 로그인 화면의 사람 인증은 이 테스트가 자동화하지 않는다. WPF 회귀는 실제 붙여넣기 이벤트와 파일 첨부 처리, Claude 이미지 프로토콜을 검사한다. 테스트 UI의 모드 전환은 실제 IVsWindowFrame API를 사용한다.

## v0.4.0 설정·첫 사용 안내 (TASK-010)

기존35개(실제 Codex 포함)에 [UX-01~15](UX_TESTS.md)를 추가하여 실제 VS2017 프로세스에서 총50/50 PASS를 확인했다. IDE 실행기는 기본 Chat의 자동 안내를 테스트 설정으로 숨기고, 격리된 제품 컨트롤의 실제 Loaded에서 처음 사용을 재현한다. 개인 설정 파일·클립보드·사용자 프로젝트를 테스트 데이터로 사용하지 않는다. 결과는 MarkdownReport/CodexOrig_TASK-010_IDE.json에 보존했다.

실제 Codex를 지정하지 않으면 기존32개와 UX15개로47개를 실행한다. UX PNG는 실행별 artifacts/ide/run-*/ux/에 저장한다. 제품 DLL과 VSIX의 DLL 해시가 일치하지 않으면 테스트를 시작하지 않는다. 팝업 검사는 실제 WPF 창·Click 이벤트·CenterScreen 좌표를 사용하며 마우스의 물리 클릭을 자동화하는 검사는 아니다.

## v0.5.0 Markdown·SQL·변경 파일 (TASK-013)

기존35개와 UX16개에 WORK7개를 추가했다. 실제 Codex 지정 시58개, fixture만 사용하면55개다. 실제 호출은 기존 소스 수정 외에 합성 SQL Server 테이블 정의로 빈 SQL 파일의 쿼리를 작성하는 요청을 포함한다. 운영 DB 연결·SQL 실행은 이 검사에 포함하지 않는다.

| ID | 검사 |
|---|---|
| WORK-01 | 기본 첫 AI 탭이 Claude이며 Tag로 Codex 선택 가능 |
| WORK-02 | 변경 카드에서 수정 대상 한 파일 표시 |
| WORK-03 | 다른 파일을 연 상태에서 파일 링크가 요청 대상 소스로 실제 이동 |
| WORK-04 | 빈 SQL 파일에 작성한 SELECT 수정안은 승인 전 버퍼·디스크 보존 |
| WORK-05 | 승인·적용·저장 후 실제 IDE가 SQL 파일에 쿼리 저장 |
| WORK-06 | 새 대화가 이전 메시지를 유지하고 새 미전송 초안을 Markdown에 보관 |
| WORK-07 | 컨트롤 재생성 시 해당 AI의 열린 대화 목록 복원 |

설정과 Markdown 루트는 실행별 폴더로 격리한다. `launch.json`은 VSIX·참조 DLL 해시를, `result.json`은 실제 Codex 및 SQL 호출 여부를 기록한다. 최신 완료 결과는 [TASK-013](../MarkdownReport/CodexOrig_TASK-013.md)을 참고한다. 손으로 하는 도킹·마우스 드래그·다중 모니터 DPI와 실제 Claude 인증은 별도 확인 범위다.

## v0.7.0 문서·모델·대화 AI 협업 (TASK-016)

실제 VS2017에서 SIDEBAR7개에 ADVANCED8개를 추가했다. fixture만 사용하면70개, 실제 Codex의 소스 수정·SQL 요청을 포함하면73개다. ADVANCED는 비동기 문서 본문 첨부, AI 이름·참여자 표시, 실제 native peer→coordinator 호출·전달, 승인 전 IDE 버퍼/디스크 유지, peer 완료/읽지않음, 실제 협업 보고, 모델/Fast UI, 재생성시 참여자·이름 복원을 검사한다.

최종 VSIX를 직접 설치하고 그 패키지에서 추출한 DLL을 참조한다. IDE에서 소스 탭을 연 뒤 UI 표시를 검사할 때는 테스트 소유 도구 프레임을 다시 표시한 뒤 실제 배치를 갱신한다. 제품의 숨김 동작이나 승인 조건을 테스트에 맞춰 바꾸지 않는다. 결과는 [TASK-016](../MarkdownReport/CodexOrig_TASK-016.md)과 해당 IDE JSON을 참고한다.

## v0.8.0 이미지 요구 (TASK-018)

기존73개에 IMAGES7개를 추가한다. 실제 IDE에서 하단 가로 스크롤, 모델 버튼에 붙은 Popup·게이지·목차, Escape 닫기, 권한3종과 상세 작업 메뉴, AI 선택의 실제 대화 라우팅, hover 반복 시 한 번만 열리는 부채꼴과 실제 도구 설명의 테마·줄바꿈을 확인한다. 실제 Codex를 지정하면80개, fixture만 실행하면77개다. 검사 helper는 지정 VSIX의 추출 DLL을 참조하며 제품을 재빌드하지 않는다. 실제 합성 C#·SQL 요청과 승인 전 보존 검사는 유지한다.

최종 결과와 이미지별 요구의 대응은 [TASK-018](../MarkdownReport/CodexOrig_TASK-018.md)에 기록한다. 물리 마우스·다중 모니터 검사는 별도이며 이 자동 검사는 실제 호스트의 WPF routed event와 시각 트리를 사용한다.

### 직접 수정 후 편집기 동기화

SYNC-01~09는 합성 솔루션의 실제 EnvDTE/RDT 텍스트 편집기를 사용한다. 미저장 문서 사전 차단, 실행 중 임시 읽기 전용, 파일 변경 안내를 보류한 버퍼 보존, 완료 후 디스크 재로딩·Saved 상태·원래 편집 가능 상태, 새 문서 보호와 닫기/재열기 시 중지·재보호를 확인한다. 이 검사는 전역 IDE의 외부 파일 알림 설정을 바꾸거나 사용자 문서를 덮어쓰지 않는다. 결과가 생성되지 않고 IDE가 모달에서 멈추면 성공으로 집계하지 않는다. COM 이벤트 해제 오류 경계는 소스의 독립 cleanup 경로와 실제 정상 종료 검증을 구분하며, 모든 COM 장애를 주입해 검증했다고 주장하지 않는다.
