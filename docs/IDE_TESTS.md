# 실제 VS2017 통합 테스트

작성: CodexCode / 2026-10-18. TASK-005의 기존 검증 대상은 v0.2.0, VS2017 Community 15.9.83, .NET Framework 4.6.2이다. 기존 편집 55개·Foundation 29개·호환성 44개에 더해 **실제 IDE 25/25 PASS**를 확인했다. 실제 Claude 계정 호출 및 VS2019/2022 지원 검증은 포함하지 않는다.

## 실행

VS2017의 확장 개발 워크로드와 .NET Framework 4.6.2 targeting pack이 필요하다. 먼저 제품을 Release 빌드하고 `tests/editing/Test-SourceEditing.ps1`을 실행하여 CLI fixture를 생성한다.

v0.3.0 현재 테스트의 기본 VSIX는 빌드한 `src/VS2017.AIAssistant/bin/Release/VS2017.AIAssistant.vsix`다. 새 체크아웃에서 직접 빌드한 패키지를 사용할 때는 `-VsixPath .\src\VS2017.AIAssistant\bin\Release\VS2017.AIAssistant.vsix`를 지정한다. 테스트는 참조 DLL과 VSIX 내 DLL의 SHA-256이 같아야 실행된다.

```powershell
# 무과금 CLI fixture를 사용한 실제 IDE 22개 검사
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\ide\Test-IdeIntegration.ps1

# 로그인된 실제 Codex를 포함한 실제 IDE 25개 검사: 합성 소스만 전송
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