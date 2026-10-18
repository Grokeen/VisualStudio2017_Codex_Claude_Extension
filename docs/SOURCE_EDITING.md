v0.9의 현재 권한·동적 모델·토큰·전체 액세스 계약은 [사용 안내](USER_GUIDE.md)와 [CLI 계약](CLI_OPTIONS.md)을 따른다. 아래 과거 버전별 검증 기록은 이력으로 보존한다.

# 수정안 검토와 직접 소스 수정

2026-10-18 추가된 Role.md의 “비주얼스튜디오에서 확장자로 실행된 코덱스나 클로드가 소스수정이 가능해야 한다”는 요구를 반영했다. 초기 MVP에서 수정 기능을 제외했던 문구도 이 요구에 맞춰 갱신했다.

## 사용 순서

1. Windows 네이티브 Codex 또는 Claude Code CLI를 설치하고 해당 터미널에서 로그인한다.
2. VS2017 15.9에서 기존 소스 파일을 텍스트 편집기로 연다. 새 문서는 먼저 파일로 저장한다.
3. 도구 → AI Assistant에서 Chat을 열고 Codex/Claude Code 탭을 선택한다. 설정에서 CLI 실행 파일 `.exe` 경로를 선택하거나 계정 연결·연결 확인을 사용한다. PATH에서 찾을 수 있으면 경로는 비워도 된다.
4. 수정 요청을 입력하고 **보내기**을 누른다. 현재 파일의 전체 편집 내용과 얻을 수 있는 프로젝트 언어·프레임워크 정보가 선택한 AI에 전달된다. 미저장 편집 내용도 포함된다.
5. **변경 비교**, **원본**, **수정안** 탭에서 변경 위치와 결과를 확인한다.
6. 적용 동의에 체크한 다음 **승인 후 적용**을 누른다. 편집기 버퍼가 변경되며 파일은 아직 자동 저장하지 않는다.
7. 내용을 확인하고 **적용 내용 저장** 또는 편집기의 Ctrl+S로 저장한다.
8. 마지막 AI 적용을 복구하려면 **직전 적용 되돌리기** 또는 편집기의 실행 취소(Ctrl+Z)를 사용한다. 복구한 버퍼의 저장은 Ctrl+S로 수행한다.

CLI 경로와 모델, 공통 instruction, 글자 크기는 설정 저장을 누르면 재시작 후에도 유지한다. 대화·첨부·작성 중 입력은 Provider별로 창 수명 동안 유지하며 새 대화로 초기화한다. 상세 사용법은 [USER_GUIDE.md](USER_GUIDE.md)를 참고한다. `.cmd`/`.bat` 셸 래퍼는 실행하지 않으므로 네이티브 `.exe`를 선택해야 한다. 로그인/CLI 기능이 맞지 않으면 오류로 안내하며 권한 제한을 풀어서 자동 재시도하지 않는다.

## 현재 범위

| 항목 | v0.3.0 |
|---|---|
| 파일 범위 | 활성 텍스트 파일 한 개, 이미 디스크에 존재하는 파일 |
| 요청 내용 | 전체 편집기 버퍼, 파일명, 제공 가능한 언어·프레임워크 정보 |
| 수정안 | 원본에서 한 번만 나타나는 `old_text`를 `new_text`로 교체, 최대 64개 |
| 비교 | 원본 줄 위치와 변경 전/후 조각, 전체 원본·수정안 탭 |
| 쓰기 시점 | 사용자 승인 후 EnvDTE 편집기 버퍼 변경, 명시적 별도 저장 |
| 복구 | 직전 AI 적용 전 버퍼 및 Visual Studio Undo 그룹 |
| 충돌 | 파일 식별자, 편집기 내용, 디스크 SHA-256 및 읽기 전용 상태 확인 |
| 크기·시간 | 요청 파일 최대 200,000자, 요청 최대 16,000자, 응답 각 스트림 최대 2,000,000자, 기본 5분 제한 |
| 제외 | 다중 파일, 새 파일 생성·삭제, 무승인 자동 적용, 일반 채팅·대화 기록 |
| IDE | v0.9부터 VS2017 15.9·VS2019·VS2022별 패키지 제공 |

원본 내용이 AI 응답의 교체 문자열과 맞지 않거나, 여러 번 일치하거나, 수정 범위가 겹치면 적용하지 않는다. CRLF/LF 차이는 원본 기준으로 정규화한다. 승인 이후라도 사용자가 편집했거나 파일이 외부에서 바뀌면 적용·저장·복구를 중단한다. 다시 수정안을 생성하거나 필요한 내용을 직접 확인해야 한다.

“적용 내용 저장”은 IDE의 저장 기능을 호출하여 파일 인코딩 처리를 IDE에 맡긴다. 현재 기능은 하나의 Undo 그룹으로 편집하고 원래 미저장 내용도 복구 대상으로 보관한다. 도구 창 수명이 끝나면 메모리의 복구 상태는 사라진다. Ctrl+S 등 확장 밖에서 저장해 디스크 버전이 바뀐 경우 전용 복구 버튼은 충돌로 거부할 수 있으며, 이때는 IDE의 Ctrl+Z를 사용한다.

## Provider 실행 방식

Codex는 `codex exec --sandbox read-only --ignore-user-config --ephemeral --skip-git-repo-check --output-schema ... [--image ...] -- -`로 호출한다. 모델의 최종 출력이 수정 JSON이 되도록 schema를 전달하고 stdin에 요청을 쓴다. 이 방식은 [OpenAI Docs의 비대화형·구조화 출력 문서](https://developers.openai.com/codex/noninteractive)와 [CLI 옵션](https://developers.openai.com/codex/cli/reference)을 참고했다. 설치된 Codex `0.162.0-alpha.17.2`의 `exec --help`에서도 필요한 옵션을 확인했다. 사용자 CLI 설정은 로드하지 않으며 인증은 CLI의 기존 로그인에 따른다.

Claude는 `--print --output-format json --json-schema ... --tools "" --disallowedTools "mcp__*" --strict-mcp-config --mcp-config ... --setting-sources "" --no-session-persistence`를 사용한다. stdout의 `structured_output`만 수정안으로 읽는다. 도구 제한, 빈 MCP 구성 및 구조화 응답은 [Anthropic CLI 문서](https://code.claude.com/docs/en/cli-reference)와 [프로그램 호출 문서](https://code.claude.com/docs/en/headless)에 근거한다. 실제 설치된 Claude의 지원 옵션·인증·모델 응답은 별도 검증해야 한다.

두 Provider 모두 별도의 빈 작업 디렉터리에서 요청하고, 프로젝트 경로를 작업 디렉터리로 넘기지 않는다. 수정할 파일 경로를 AI 결과에서 받지 않으므로 결과가 다른 파일을 지정할 수 없다. 확장 자체는 프롬프트·전체 소스·AI stdout/stderr를 로그 파일에 저장하지 않는다. 임시 파일에는 고정 JSON schema, 빈 MCP 구성 및 Codex가 읽을 첨부 이미지 스냅샷을 기록하고 요청 종료 시 정리한다. 이미지 파일은 임의 이름으로 저장하며 원본 첨부 경로를 CLI에 전달하지 않는다. Claude 이미지는 stdin의 stream-json 이미지 블록으로 전달한다. CLI 자체의 인증·진단 정책은 해당 도구에 따른다.

stdout/stderr를 비동기로 동시에 읽고 Windows Job Object로 CLI와 하위 프로세스의 수명을 관리한다. 중지·제한 시간·출력 초과·IDE 도구 창 해제 시 프로세스를 정리한다.

## 자동 검증

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 `
    -MSBuildPath 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe'
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\editing\Test-SourceEditing.ps1 `
    -CompilerPath 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe'
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -STA -File .\tests\foundation\Test-FoundationPackage.ps1
```

편집 회귀 테스트는 실제 빌드 DLL과 실제 WPF 버튼 이벤트를 사용한다. 단, IDE 문서 어댑터와 AI 응답은 독립 fixture로 대체하므로 유료 AI 호출이나 사용자 소스 변경이 없다. 승인 전 쓰기 차단, 적용/저장 분리, 미저장 내용 복구, 잘못된/중복/겹치는 교체, 외부 변경, CLI 출력·오류·취소·자식 프로세스 종료를 확인한다. **이 테스트의 통과는 실제 계정 AI 응답이나 EnvDTE 런타임 검증을 의미하지 않는다.**

실제 Codex 검증은 기본 회귀에서 분리한 아래 명령으로 수행한다. 로그인된 CLI 계정을 사용하며, 스크립트 안에 고정한 작은 합성 코드만 전송한다. 사용자 프로젝트를 읽어 보내지 않는다.

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\tests\editing\Test-LiveCodex.ps1 `
    -CodexPath 'C:\path\to\codex.exe'
```

2026-10-18 TASK-005에서 실제 VS2017 15.9.83의 설치된 v0.2.0 도구 창에서 Codex CLI를 호출하여 `GetValue`의 `return 1;` → `return 2;` 수정안 생성, 승인 전 원본 보존, 승인 후 실제 EnvDTE 버퍼 적용 및 IDE 저장까지 확인했다. 실제 IDE 25개와 기존 회귀 128개가 통과했다. Claude는 실제 IDE에서 테스트 CLI 응답의 적용까지 검증했으며 실제 Claude 계정 호출은 남아 있다. 재현 명령과 사례별 범위는 [IDE_TESTS.md](IDE_TESTS.md)를 참고한다.

## 실제 IDE 체크리스트

`tests/ide/Test-IdeIntegration.ps1`은 별도 인스턴스와 합성 .NET Framework 4.6.2 프로젝트를 생성한다. 아래 완료 표시는 TASK-005에서 확인한 범위다.

- [x] VS2017 별도 테스트 인스턴스에 v0.2.0 VSIX 설치 후 등록된 메뉴 명령으로 도구 창 표시
- [x] 실제 Codex로 단일 메서드 수정안을 받아 비교·승인·적용·저장
- [ ] 실제 Claude로 같은 흐름 수행
- [x] 미저장 주석을 포함한 파일의 요청 및 되돌리기에서 미저장 내용 보존
- [x] 수정안 생성 후 사용자 편집 시 실제 IDE 적용 거부
- [ ] 생성 중 편집·외부 파일 변경 시 실제 IDE 적용·저장 거부 (독립 회귀 검사 통과)
- [x] 다른 파일로 전환한 뒤 적용해도 요청한 파일만 수정
- [x] IDE 실행 취소 명령으로 한 번에 적용 취소, 전용 되돌리기, 별도 저장, 재요청
- [ ] 읽기 전용 문서, 닫힌 문서, 이름 변경 문서, 저장 취소/실패
- [ ] 중지·시간 초과·IDE 종료 시 CLI 하위 프로세스 종료
- [ ] 밝은/어두운 테마와 좁은 도킹 창의 승인 문구 가독성

작업별 실제 실행 결과는 `MarkdownReport/CodexCode_TASK-005.md`에 기록했다. 도킹·테마 전환·반복 개폐·재시작·제거 및 특수 문서의 수동 검증은 별도다.

## v0.3.0 추가 동작

- Chat은 VS 문서 탭 영역에서 시작하고 VS의 도킹/분리 기능으로 이동할 수 있다. Chat을 선택해 ActiveDocument가 없어져도 마지막으로 활성화한 소스 문서를 사용하며, 닫힌 문서는 추적 대상에서 해제한다.
- 파일을 Chat에 드롭하거나 ＋ 첨부로 선택한다. 프롬프트에서 Ctrl+V 또는 붙여넣기 버튼으로 PNG 이미지/파일을 첨부할 수 있다. 텍스트 붙여넣기는 기존 입력 기능을 유지한다. 첨부는 전송 후에도 유지되므로 제거할 때까지 다음 요청에도 포함된다.
- 허용 범위: PNG/JPEG 2MB·1600만 픽셀, UTF-8/Unicode BOM 텍스트 100KB, 합계4개. 파일은 첨부 당시 내용으로 고정하고 디렉터리·지원하지 않는 바이너리는 거부한다.
- 대화는 각 AI별로 화면에 최대100개 메시지를 누적한다. 후속 요청에는 해당 탭의 최근10개/24000자 대화와 현재 파일, 공통 instruction(8000자), 현재 첨부를 포함한다. 설정 저장은 LocalAppData/VS2017AIAssistant/settings.json을 사용하며 대화나 인증 토큰을 저장하지 않는다.
- 계정 연결은 Codex login / Claude auth login으로 공식 CLI를 열고, 연결 확인은 login status / auth status 종료 코드를 사용한다. 실제 인증은 사용자가 CLI/브라우저에서 수행하며 확장에 암호를 입력하지 않는다.
- 실제 Codex 합성 이미지/텍스트/이전 대화/instruction 검사는 tests/chat/Test-LiveChat.ps1로 재현한다. 실제 Claude 계정 호출은 미검증이며 기본 회귀에서 네이티브 CLI 인수·stream-json 이미지·응답 envelope를 검사한다.

## v0.4.0 설정 진입·첫 사용

설정은 Chat 상단의 ⚙ 설정 또는 우클릭 설정에서 화면 중앙 팝업으로 연다. 저장 성공 후 닫히고, 닫기·Esc·X는 미저장 입력을 복원한다. 텍스트 우클릭 메뉴는 기존 편집 명령을 유지하며 누적 대화의 읽기 전용 텍스트에도 복사·설정·사용 안내를 제공한다. 처음 표시되는 Chat은 네 단계 안내를 연다. 다시 보지 않기 선택은 기존 설정 파일에 HideWelcome만 병합하고 수동 사용 안내로 언제든 재열 수 있다. 상세 검증은 [UX_TESTS.md](UX_TESTS.md)를 참고한다.
