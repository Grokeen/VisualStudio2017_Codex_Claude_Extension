# 모델·생각 강도·Fast 옵션 계약

2026-10-18에 공식 문서와 설치된 `codex-cli 0.162.0-alpha.17.2`의 도움말, 로컬 모델 캐시 구조를 확인했다. 이 검사는 CLI 호출 옵션을 검증하며 실제 Claude 계정 권한이나 응답 속도를 측정하지 않는다.

## 제품 연결

```cs
var options = new ProviderExecutionOptions(provider, context.Model,
    context.ReasoningEffort, context.Speed);
arguments.AddRange(options.BuildProposalArguments());
```

`BuildProposalArguments`에는 모델·강도·속도 인수만 있다. 기존 Codex `read-only` sandbox, Claude `--tools ""`, 수정안 비교·승인 흐름은 호출자가 유지한다. 확장 프로그램의 DB·형상관리·배포 허용 설정은 CLI 전체 파일 접근이나 인터넷 권한과 별개의 기능이다.

| 공개 API | 용도 |
| --- | --- |
| `ProviderModelCatalog.ForProvider(provider)` | Codex 캐시 또는 Claude 공식 모델 정보 |
| `ProviderModelCatalog.LoadCodex(cachePath = null)` | 캐시를 읽고 실패하면 빈 목록·일반 안내 반환 |
| `ProviderModelCatalog.FromCodexJson(json, sourcePath = "")` | 엄격 JSON 모델 fixture/메타데이터 읽기 |
| `ProviderModelCatalog.ForClaude()` | 공식 Claude 별칭·명시 버전 모델 기능 |
| `catalog.Models` / `catalog.Find(id)` | 읽기 전용 목록 / 대소문자 구분 조회 |
| `descriptor.Id` / `DisplayName` / `Efforts` / `DefaultEffort` | 실제 인수 값과 화면 표시 값 구분 |
| `descriptor.SupportsFast` / `SpeedDescription` | 모델이 광고한 속도 기능과 설명 |
| `descriptor.MinimumCliVersion` | 명시 Claude 모델의 문서상 최소 버전 |
| `new ProviderExecutionOptions(provider, model="", reasoningEffort="auto", speed="standard", catalog=null)` | 요청 시점의 불변 옵션 검증 |
| `options.BuildProposalArguments()` | 네이티브 argv 배열 |
| `options.ValidateCliVersion(installedVersion)` | 알려진 최소 CLI 버전 확인 |
| `ProviderExecutionOptions.BuildProposalArguments(provider, model, effort, speed)` | 컨텍스트 통합용 정적 편의 API |
| `ProviderExecutionOptions.QuoteArgument(value)` | Windows `ProcessStartInfo.Arguments` 인코딩 |

Claude Fast 또는 명시 최신 모델에서 `MinimumCliVersion`이 있는 경우, 실행기가 실제 `claude --version`을 확인한 뒤 `ValidateCliVersion`을 호출해야 한다. 확인되지 않은 버전을 전달하면 검증에 실패한다. 모델·조직별 사용 권한과 제한은 최종적으로 공식 CLI/서버가 판정하며 호출 오류를 사용자에게 표시해야 한다.

## 모델 목록과 제한

Codex는 `VS2017_AI_MODELS_CACHE_PATH`의 **절대 파일 경로**를 먼저 사용한다. 이 테스트용 변수가 없으면 `CODEX_HOME/models_cache.json`, 그 환경변수도 없으면 사용자 `.codex/models_cache.json`을 읽는다. 인증 파일은 읽지 않는다.

캐시는 최대 UTF8 **2 MiB**, 모델 256개, JSON 중첩 32단계다. 중복 키·주석·작은따옴표·뒤 쉼표 등 비표준 JSON은 거부한다. `visibility=list` 또는 과거 형식의 미지정 항목만 표시하며 숨김·명시 사용 불가 모델은 제외한다. 모델별 `supported_reasoning_levels`, 기본 강도, `additional_speed_tiers`, `service_tiers`, 우선순위와 업그레이드 ID를 복사한다. 캐시의 identity·지침·그 밖의 원문은 제품 모델에 보관하지 않는다.

Codex 모델은 고정 목록이 아니다. 광고된 강도 토큰을 그대로 사용하므로 `none`, `minimal`, `xhigh`, `max`, `ultra`도 해당 모델이 광고한 경우만 지원한다. `auto`는 강도 인수를 생략한다. 알 수 없는 직접 입력 모델과 CLI 기본 모델은 `auto/low/medium/high`의 보수적인 대체 경로를 제공하며, 고급 강도·Fast는 차단한다. 이 대체 경로의 모델 지원 여부도 CLI 오류로 확인한다.

Claude는 계정별 로컬 모델 캐시 계약이 없어 공식 문서의 모델 정보를 사용한다. `opus/sonnet/haiku` 별칭은 엔드포인트에 따라 해석 버전이 달라 기본 강도만 제시한다. 고급 강도는 명시 버전 모델의 확인된 기능을 사용한다. 이는 로그인한 계정의 이용 가능 모델 목록이라는 보장이 아니다. [공식 모델 설정](https://code.claude.com/docs/en/model-config)

## 실제 인수

Codex Fast는 `--config service_tier="fast" --config features.fast_mode=true`, 강도는 `--config model_reasoning_effort="값"`으로 전달한다. 속도와 강도는 별개 설정이다. 캐시의 광고값을 표시하며 일정 배율을 일괄 약속하지 않는다. [공식 속도 문서](https://developers.openai.com/codex/speed/), [공식 설정 참조](https://developers.openai.com/codex/config-reference/)

Claude Fast는 `--settings {"fastMode":true}`, 기본 속도는 `{"fastMode":false}`, 강도는 `--effort 값`이다. 비대화형 Fast는 CLI **2.1.205 이상**이 필요하다. 공식 지원 Opus 버전만 Fast를 허용하고 Sonnet·Haiku를 자동으로 Opus로 바꾸지 않는다. 사용 권한과 추가 사용량 조건은 공식 계정 설정에 따른다. [공식 Fast 문서](https://code.claude.com/docs/en/fast-mode), [공식 CLI 참조](https://code.claude.com/docs/en/cli-reference)

## 검사

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/provider-options/Test-ProviderOptions.ps1

# 고정된 실제 제품 DLL에 포함된 helper 검사
powershell -NoProfile -ExecutionPolicy Bypass -File tests/provider-options/Test-ProviderOptions.ps1 `
    -ProductAssemblyPath <고정 DLL 절대 경로>
```

첫 독립 검사는 **65/65 통과**했다. 모델 기능·잘못된 값·엄격 JSON·2 MiB·UTF8·실제 파일 잠금·캐시 경로 격리·불변성·최소 버전·실제 Windows 자식 프로세스 argv 왕복을 검사했다. 결과는 `artifacts/task015/provider-options-a5b3b0263ab94e61a51287c29bfbf616/result.json`이다. 기존 제품의 빌드·인증·프로젝트 파일은 변경하지 않았다.

이 폴더의 `ProviderCliIntegrationTests.cs`와 `Test-ProviderCliIntegration.ps1`은 부모 작업자가 소유한 별도 CLI 통합 검사다.

## 형상관리 이름 조회

`ScmIdentityService.ReadAsync(projectDirectory, cancellationToken)`은 현재 프로젝트의 공개 이름을 반환한다. `ScmIdentity.AccountName`, `Source`, `AuthenticationVerified`, `HasStoredAccount`, `Description`으로 작성자·저장 계정·실제 로그인 확인을 구분한다. 이 결과는 AI 이름의 접두어에 사용할 수 있지만 작성자 설정이나 저장 계정을 서버 인증 확인으로 표시하면 안 된다.

```cs
var identity = await new ScmIdentityService().ReadAsync(projectDirectory, token);
// identity.Description도 함께 표시한다.
// Git 작성자 설정만 읽었으면 AuthenticationVerified == false 이다.
```

생성자는 `ScmIdentityService(gitExecutable=null, ghExecutable=null, timeout=null, gcmExecutable=null)`이다. Git·GH는 PATH의 절대 네이티브 exe를 찾고, GCM은 선택한 Git 설치 경로 또는 Git execdir의 고정 `git-credential-manager.exe`만 찾는다. 각 실행기 경로가 빈 문자열이면 사용하지 않는다. 테스트에서 `.exe` 절대 경로와 20ms~5초 제한 시간을 주입할 수 있다. 제품 기본 상한은 전체 조회 **5초**, 출력은 stdout/stderr 각각 **4096자**다. 사용자 취소와 시간 초과는 Windows Job으로 부모·자식 CLI를 정리한다. 조회는 실제 로컬 절대 폴더만 지원하며 UNC 경로는 대기 시간을 제한하기 위해 사용하지 않는다.

먼저 `git -C <project> rev-parse --is-inside-work-tree`로 Git 프로젝트를 확인한 뒤 `git config --get user.name`을 읽는다. 이름이 있으면 **Git 작성자 설정 · 로그인 미확인**으로 반환할 수 있다. GitHub CLI가 설치되어 있고 origin이 정확한 공개 `github.com` HTTPS/SSH 원격일 때만 `gh api user --jq .login --hostname github.com --method GET`을 실행한다. 성공한 로그인 이름만 인증 확인으로 구분한다. 다른 서버나 GitHub Enterprise 호스트를 임의로 추정해 조회하지 않는다. [GitHub CLI 공식 API 설명](https://cli.github.com/manual/gh_api)

GH가 없거나 인증 확인에 실패하면 같은 `github.com` 프로젝트에서만 GCM의 `github list --no-ui` 공개 계정 메타데이터를 읽는다. 단일 저장 계정은 자동 선택하며, 여러 계정이면 HTTPS origin의 사용자 이름 또는 `credential.https://github.com.username` 설정과 목록이 일치할 때만 선택한다. 모호하면 안내를 표시하고 작성자로 대체한다. SSH 전송 사용자 `git`은 로그인 계정 선택에 사용하지 않는다. 저장 계정은 `HasStoredAccount=true`, `AuthenticationVerified=false`, **Git Credential Manager 저장 계정 · 서버 인증 미확인**으로 표시한다. [GCM 공식 다중 계정 설명](https://github.com/git-ecosystem/git-credential-manager/blob/main/docs/multiple-users.md), [공식 명령 설명](https://github.com/git-ecosystem/git-credential-manager/blob/main/docs/usage.md)

인증 파일·토큰·raw credential `get`/`fill`은 읽지 않는다. origin의 암호나 CLI 오류 원문은 결과와 안내 메시지에 넣지 않는다. GCM 목록에 이름 외 키 값·JSON·URL 등이 섞이면 전체 목록을 버린다. GitHub CLI와 저장 계정 모두 확인하지 못하면 작성자 설정이라는 출처를 명확히 표시한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/provider-options/Test-ScmIdentity.ps1
```

독립 **63/63 검사 통과**, 컴파일 경고 0: 한글 작성자, 고정 argv, GitHub 호스트 검사, 로그인/저장 계정/작성자 구분, 다중 계정 선택·모호성, 비밀 형태 줄 거부, stdout/stderr 출력 제한, 취소·시간 초과·자식 프로세스 정리, 기본 GCM 파일 탐색, 실제 임시 Git 저장소 로컬 설정을 검증했다. 결과는 `artifacts/task015/scm-identity-01891483cd8b4f73b3e306f6848043a0/result.json`이다. 현재 PC의 실제 GCM도 저장 계정 메타데이터 조회를 확인했으며 계정명은 증거에 기록하지 않았다(`actual-gcm-metadata.json`). 실제 GH CLI가 설치되지 않아 외부 인증 요청은 수행하지 않았다. 고정 제품 DLL은 `-ProductAssemblyPath`로 동일 검사를 실행할 수 있다.
