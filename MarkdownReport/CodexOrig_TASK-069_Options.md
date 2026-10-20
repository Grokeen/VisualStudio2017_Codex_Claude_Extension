# TASK-069 #2 모델·강도 실제 전달 검증

- 담당: CodexOrig theme064. image-69.png를 직접 확인했다. 표시값은 **gpt-6-luna · High**, 실행 모드는 전체 액세스다.
- 현재 판정: TASK069 C3 실제 제품 **86/86 PASS**, 설치 CLI의 로컬 스키마 실제 관측 필드 **20/20 PASS**. 이전 TASK068 C6 선행 결과도 별도 보존한다. 전체 출시 완료 판정은 Root의 최종 감사·배포 범위다.
- 제품 수정: 0개. tests/task069options의 개인 하네스 5개만 추가했다. Options.cs/RequestQueue.cs 및 Root의 runner 줄바꿈 수정에는 쓰지 않았다.

## 실제로 확인한 경로

실제 AssistantControl의 제공자 탭·ModelName·ModeChoice 컨트롤 이벤트와 FastChanged 핸들러로 값을 선택했다. 실제 UI 문구와 내부 상태가 일치하는지 확인한 뒤 CaptureCurrentRequest가 고정한 문맥을 관측했다. 그 다음 화면을 다른 모델·자동 강도로 바꾸고 BuildQueuedExecutionContext가 원래 model/high/speed를 유지하는지 검사했다. 그 문맥을 실제 제품 Runtime runner에 전달해 자기 네이티브 EXE가 받은 argv와 stdin JSON 원문을 보존했다.

| 경로 | 관측값 |
|---|---|
| Codex 전체 액세스 | thread/start.model=gpt-6-luna, turn/start.effort=high, threadId 동일 |
| Codex 권한 | approvalPolicy=never, sandbox=danger-full-access, ephemeral=true |
| Codex 속도 | 기본은 serviceTier=null, Fast는 serviceTier=fast |
| Codex 승인 모드 | --model gpt-6-luna, model_reasoning_effort="high", read-only/ignore-user-config/ephemeral 유지 |
| Claude 전체/승인 모드 | --model opus, --effort high, fastMode true/false 명시 |
| Claude 권한 | 전체는 bypassPermissions/stream-json, 승인은 tools 빈 값/mcp__* 차단/no-session-persistence 유지 |

제공자 2개 × 전체/승인 2개 × 기본/Fast 2개 = 8조합을 실행했다. native 실행 8회, wire 기록 26행이다. 합성 소스는 변경되지 않았다. 문맥 캡처는 실제 제품 경로이며, 대기열의 순서 스케줄링 전체를 이번 옵션 검사에서 검증했다고 주장하지 않는다.

## 공식 계약과 설치된 CLI

[공식 App Server 문서](https://learn.chatgpt.com/docs/app-server)는 모델 선택과 turn별 effort 재정의를 설명한다. [공식 계정 연동 문서](https://developers.openai.com/siwc/token-sharing-open-source/codex-app-server)는 모델 목록만으로 계정 접근 권한을 확정할 수 없다고 설명한다.

설치된 codex-cli 0.163.0-alpha.10.2의 `app-server generate-json-schema --experimental`을 개인 출력 폴더에서 실행했다. 추론·로그인 명령은 실행하지 않았다. 관측한 두 thread/start와 두 turn/start의 실제 20개 필드를 생성된 스키마의 required/property/type, effort 참조 및 최소 길이, 승인·sandbox enum, text input 형식과 대조했다. 전체 Draft07 validator를 사용한 것은 아니다. 설치 CLI와 배포 묶음의 CLI 버전이 완전히 같다는 주장도 하지 않는다.

## 증거 및 원본 실패 보존

- 통과 원문: artifacts/task069options/baseline068c6-4/result.json
- 실제 argv/JSON: 위 폴더의 각 scenario/wire/*.wire.jsonl
- schema 근거: artifacts/task069options/local-schema-1/validation-baseline068c6.json 및 v2/{ThreadStartParams,TurnStartParams}.json
- 초기 1~3회는 하네스의 ToggleButton/ArrayList 캐스팅과 무창 ComboBox.SelectedValue 바인딩 지연을 확인한 실패다. SelectedItem+dispatcher로 실제 선택 이벤트를 처리했고 제품 파일은 변경하지 않았다. 실패 폴더/소스 사본/원문은 그대로 보존했다. 초기 1회 runner의 고정 true 범위 metadata는 낙관적이었으며 이후 실제 카운터로 바꿨다. 초기 실패를 제품 실행 성공 근거로 사용하지 않는다.
- 전체 파일 해시·범위: CodexOrig_TASK-069_OptionsValidation.json

## 한계 및 후속

모델 카탈로그는 gpt-6-luna/Fast를 광고하는 개인 합성 capability다. 실제 계정의 모델 접근 권한·추론 결과·과금·서버 내부 적용은 이번 검사로 증명되지 않는다. 로그인·원격 AI·OS 입력·Window.Show·Popup 표시·실제 IDE 실행은 0회이며 실제 dropdown 팝업 외관/저장 버튼은 검사하지 않았다. 옵션 관측 후 자기 fixture 생존 프로세스 0개다.

TASK069 C3 재검사 결과: artifacts/task069options/task069c3-1/result.json (86/86), local-schema-validation.json (20/20). DLL SHA256은 EDB57654F5D455C73DF2BCBBDC191206D06252A663A2D7DE059BE6184D94751E이며 입력/사본/실행 후 동일함을 확인했다. 재현 가능한 schema 필드 검사 driver는 tests/task069options/Test-ObservedSchema.ps1이다. C3 이후 출시 대상 DLL이 변경되면 추가 재검사 범위를 Root와 조율한다. 전체 빌드·감사·최신 Release 제작·배포·익명 다운로드 검증은 Root가 계속 담당한다.
## 실제 배포 CLI 및 실행기 연결 추가 검증

현재 PC의 CLI와 구분해 **실제 전달 폴더의 Codex 0.163.0**을 검사했다. `UserDistribution/v0.13.0/cli/codex/bin/codex.exe`의 SHA256 `2692C8C7BB2CF8B0AEFE638741FC90236F4B824C59578CE84994F9DE58200993`가 frozen offline-manifest 46개 allowlist의 해당 행과 일치했다. 버전과 generate-json-schema 명령만 실행했고 전후 바이너리 해시도 동일했다. 새 개인 schema 폴더는 artifacts/task069options/bundle-schema-1이며 C3의 실제 wire **20/20 PASS**다. 설치 current CLI alpha.10.2 schema의 20/20과 별도 근거이며 인증·프로필 반출·서버 추론은 없다.

최상위 VisualStudio_AIChat_ExtentionTest.exe에 agent-messages/hang-boundaries/selected-options/line-endings를 연결했다. 기본 **24개 + 선택 2개**이며 신규 검사도 기존 증거 폴더를 덮어쓰지 않도록 출력 폴더를 미리 만들지 않는다. messages/options는 candidate Assets, hang은 동일 Assets와 candidate/source까지 전달한다. 각 담당자에게 PS1 인터페이스와 동결 상태를 확인했다. options의 SchemaDirectory는 선택 입력이므로 설치 CLI의 개인 schema가 없어도 기본 86개 제품 검사는 실행되며 schema 검사 수행 여부는 별도 입력 기록으로 구분된다.

- 격리 실행기 smoke: **40/40 PASS**, GUI 제외. 네 SKU마다 신규 네 검사의 DLL/Assets/source/새 출력 계약을 가짜 child script로 검사하며 실제 제품 검사를 흉내 낸 성공으로 집계하지 않았다.
- 이후 최상위 EXE `--run`으로 실제 C3를 사용해 신규 네 suite를 실행했다. agent-messages **58/58**, hang-boundaries **17/17**, selected-options **86/86**, line-endings **15/15**, 총 **176/176 PASS**다. hang은 제품 DLL+정확한 UI 메소드 추출 경계 검사이고 전체 제품 UI 검사로 확대하지 않는다.
- 증거: artifacts/ls/bcc3e682bb/results.json, artifacts/task069options/launcher-c3-new4/{summary.json,observations.json,*.log}. 실제 각 suite 출력 경로도 observations.json에 기록했다.
- EXE SHA256: C5F9FD17A14C05803592CE903F9D1BC952D938252907DB2A6E73A3BDC450E981.
- 초기 EXE 컴파일은 Root의 기존 전체 검사 PID85864가 파일을 사용해 CS0016으로 거절됐다. 그 프로세스를 건드리지 않았고 종료 안내 후 재컴파일했다. 기존 20개 EXE 카탈로그 불일치 실패(a2bcb2a6ab)와 새 가짜 dispatch 스크립트의 DirectoryInfo 캐스팅 우선순위 실패(b25c82f71f)도 보존했다. 최종 smoke는 별도 새 폴더에 저장했다.

제품 파일/공유 bin·obj/Git/index는 변경하지 않았다. 실행기 단독 컴파일만 수행했고 검사 소스와 실행기는 동결했다. Root의 최종 전체 24개 회귀·빌드·감사·Release 제작·배포·익명 다운로드 검증이 후속 범위다.