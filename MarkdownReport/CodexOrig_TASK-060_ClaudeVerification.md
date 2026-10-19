# TASK060 · Claude 연동 검증 및 버전 선검사 보완

- 담당: CodexOrig (Root054 위임)
- 시작: 2026-10-20 02:27:56 KST
- 범위: Role.md 2026-10-20 01:40의 **14번 Claude 정상 동작 테스트**. 새 13번 문서/파일 생성 요구가 추가되기 전 분담의 “13번 Claude”는 현재 14번에 해당한다.
- 상태: 두 Provider 파일 수정 동결·개별 검증 완료. Root의 0.12.1 최종 4종 후보 및 빌드·감사·최신 Release 배포는 별도 통합 단계이다.

Claude CLI 연동 계약과 실제 설치 CLI의 실행·계정 확인·추론 없는 초기화를 검증했다. **실제 CLI 계정은 미로그인(`LoggedIn=false`, 종료 코드 1)이므로 실제 AI 응답 성공을 확인한 것은 아니다.** 브라우저·Claude 앱 로그인과 CLI 인증은 구분했다. 로그인, 브라우저/Claude 앱 자동 조작, 인증 파일 직접 읽기, 자격 증명 추출, 유료 추론은 수행하지 않았다.

## 확인한 결함과 수정

`ClaudeRuntimeRunner.RunAgentAsync`의 전체 액세스 경로는 모델/속도 옵션을 전달하면서 최소 CLI 버전 선검사를 호출하지 않았다. 기존 수정안 경로는 이미 선검사를 수행했다. `version-gap-repro-c6`의 합성 2.1.204 CLI와 Opus Fast 요청으로, C6가 버전 조회 없이 agent를 시작하는 것을 재현했다. 이 재현 실패를 후속 성공으로 덮어쓰지 않았다.

Root 승인을 받은 두 제품 파일만 변경했다.

| 파일 | 변경 |
| --- | --- |
| `src/VS2017.AIAssistant/Providers/ClaudeRuntimeRunner.cs` | `ProviderExecutionOptions.MinimumCliVersion`이 있으면 `--version`을 먼저 검사한다. 미지원 버전은 제어 초기화/사용자 요청을 시작하기 전에 거부한다. 버전 조회에도 전체 실행 취소·제한 시간을 적용한다. |
| `src/VS2017.AIAssistant/Providers/CliEditProvider.cs` | 기존 `ReadCliVersionAsync`를 내부 공유 helper로 공개한다. 기존 10초 제한·stdout/stderr 각각 4096자 상한·Job 종료·오류 원문 비노출 로직은 유지한다. |

해당 기능이 최소 버전을 요구하지 않는 일반 별칭/표준 속도 요청은 불필요한 버전 subprocess를 추가하지 않는다. 수정안의 도구/MCP 제한과 전체 액세스의 명시적 권한 설정은 유지했다. UI, 큐, 디자이너 파일, 공유 bin/obj, 기존 release 및 감사 원문을 수정하지 않았다.

## 검증 결과

C6 원본 source 사본에 최종 두 Provider만 덮어쓴 `artifacts/task060/fixed-source-c2`를 VS2017 Release로 개인 빌드했다. **오류 0·경고 0**, DLL SHA256 `0B137EF54821DB7DEC151D2912C29C635E1A05AECCF0ABD3852C6F9565EBF502`. 이 사본은 0.12.0 기반의 수정 검증용이며 최종 0.12.1 배포 패키지가 아니다.

| 검사 | 통과 | 근거 |
| --- | ---: | --- |
| 기존 native runtime | 67/67 | `artifacts/task060/fixed-c2-2017-tests/runtime/result.json` |
| 기존 Provider CLI 전달 | 15/15 | `artifacts/task060/fixed-c2-2017-tests/provider-cli/result.json` |
| 기존 모델/강도/Fast/argv | 65/65 | `artifacts/task060/fixed-c2-2017-tests/provider-options/result.json` |
| 새 Claude 경계/버전 선검사 | 25/25 | `artifacts/task060/fixed-c2-followup-tests/claude-boundaries/result.json` |
| 실제 Claude native CLI | 5/5 | `artifacts/task060/fixed-c2-followup-tests/native-cli/result.json` |
| **합계** | **177/177** | 서로 다른 검사를 합산하며, 반복 실행 수는 합산하지 않았다. |

새 경계 검사는 이미지 base64 입력 envelope, stdin의 요청·문맥/argv 분리, 도구·MCP·사용자 설정 차단, 구조화 수정안과 원본 디스크 보존, malformed/오류/비정상 종료 거부, 취소와 timeout 구분을 확인한다. 전체 액세스에서는 추가 권한 요청·오류 result·연결 종료를 성공으로 오인하지 않는다. 구버전/잘못된 버전/버전 명령 비정상 종료/과대 출력, 표준 속도에서 명시 최신 모델의 최소 버전, 지원 Fast의 version→agent 순서, 버전 조회 도중 취소·전체 제한 시간을 검사했다. 기존 native 검사는 합성 파일의 실제 수정과 실제 하위 프로세스 assertion 및 취소 시 자식 트리 종료를 포함한다. 합성 CLI는 실제 Claude 모델을 호출하지 않는다.

실제 native CLI는 번들에 포함된 **2.1.302**, Authenticode `Valid`, 서명자 Anthropic으로 확인했다. SHA256 `3AB075F78998EFEF8B87F491C2E4A39C309DDDF44E51D0103EBA4F3713D2A0AE`. 버전과 실제 도움말 옵션을 검사했고 `auth status --json`의 bool/종료 코드/범주만 보존했다. 실제 제품 `CliAccountService.CheckAsync`도 `false`를 반환해 같은 상태임을 확인했다. 원문 계정 출력과 이메일·계정 ID·토큰은 저장하지 않았다.

실제 CLI 초기화는 새 `CLAUDE_CONFIG_DIR`, 자식의 인증 관련 환경 변수 제거, `--bare`, 빈 도구/빈 strict MCP, 설정 소스 제외, 세션 저장 제외 상태에서 `control_request/initialize`만 보냈다. 응답 성공 및 모델 개수 **5개**만 보존했다. **사용자 메시지/추론 요청은 0회**다. 초기화 모델 목록은 CLI 호환성 정보이며 계정 이용 권한이나 로그인 증거가 아니다.

## 공식 문서 대조 (2026-10-20 확인)

- `auth status`는 미로그인 시 종료 코드 1을 반환한다. print의 JSON/stream-json, 입력 형식, 구조화 schema 및 세션 저장 제외 옵션을 확인했다. `--tools ""`만으로 MCP를 막지는 않으므로 제품 수정안 경로의 별도 MCP deny와 빈 strict config까지 확인했다. [Anthropic CLI reference](https://code.claude.com/docs/en/cli-reference)
- 이미지 입력은 user 메시지 content의 text/image block과 `source.type=base64`, `media_type`, `data` 구조를 사용한다. 제품의 이미지 수정안 및 전체 액세스 입력이 이 구조와 일치한다. [Anthropic Streaming Input](https://code.claude.com/docs/en/agent-sdk/streaming-vs-single-mode)
- `bypassPermissions`는 명시적인 전체 액세스 옵션에 해당하며 OS 격리 자체를 제공하는 설정이 아니다. 제품은 정책상 추가 control request가 오면 성공으로 처리하지 않고 오류로 종료한다. 이번 실제 native 검증은 이 모드로 파일/명령을 실행하지 않았다. [Anthropic permissions](https://code.claude.com/docs/en/permissions)
- print Fast는 CLI 2.1.205 이상과 계정/조직의 이용 권한이 필요하다. 세션 `--settings`의 `fastMode` 전달 및 Sonnet/Haiku의 무단 Opus 전환 방지를 확인했다. [Anthropic Fast mode](https://code.claude.com/docs/en/fast-mode)
- 공식 명시 모델 최소 버전(Opus 5.5=2.1.280, Sonnet 5.5=2.1.284, Haiku 5.5=2.1.293) 및 모델별 강도 지원을 대조했다. 번들 2.1.302는 이 하한을 충족하며 실제 계정별 지원은 서버가 판정한다. [Anthropic model configuration](https://code.claude.com/docs/en/model-config)

## 보존한 초기 실패

1. `baseline-c6-2017/claude-boundaries`: 첫 합성 fixture가 stdin을 기본 Windows 코드 페이지로 읽어 UTF-8 JSON을 잘못 해석했다. fixture의 `Console.InputEncoding/OutputEncoding`을 UTF-8로 고쳤으며 `baseline-c6-2017-followup`에서 기존 Claude 17개/native 4개를 통과했다. 제품 인코딩 결함으로 판정하지 않았다.
2. `version-gap-repro-c6`: 새 최소 버전 선검사 회귀가 기존 C6에서 실패해 제품 결함을 재현했다.
3. `fixed-source-c1/build-2017-Release.log`: 일시적으로 helper 호출을 존재하지 않는 `path` 변수로 바꿔 CS0103 컴파일 오류가 발생했다. 해당 private 메서드는 이미 resolve된 `executable`을 받으므로 원래 인수를 유지하도록 바로 수정했다. 실패 사본/로그를 보존했으며 c2 빌드와 위 177개 검증은 수정 후 결과다.

## 동결 및 통합 인계

- `ClaudeRuntimeRunner.cs`: `A790A5944EDF5C538BCD75424FA1113C51D92990DAD4363514EA58F3693EE341`
- `CliEditProvider.cs`: `91F8D0A652B9FE72898CF2AF3F6DC32EC5D1ACBBEAFB2A7357F30A6EC54F218B`
- 최종 소스 목록: `artifacts/task060/product-freeze.json`. 새 테스트는 `tests/task060`의 3개 파일이다.
- 재현 실행: Windows PowerShell 5.1에서 `tests/task060/Test-ClaudeVerification.ps1`에 `-AssemblyPath`, `-ClaudePath`, 새 `-OutputDirectory`를 지정한다. `-SuiteNames @('claude-boundaries')`로 최종 네 대상 후보의 25개 새 경계만 따로 재검사할 수 있다. 네 대상 SDK/실제 IDE 통합은 Root의 고정 후보에 대해 수행한다.
- 별도 오프라인 고객 PC, CLI 로그인 뒤 실제 Claude 답변·모델 이용 권한·과금·사내 프록시 연결은 미검증이다. 소스 개별 검증은 전체 작업 완료가 아니며, Root의 네 대상 빌드·감사·최신 Release 제작·게시·익명 다운로드 해시 검증까지 이어져야 한다.
