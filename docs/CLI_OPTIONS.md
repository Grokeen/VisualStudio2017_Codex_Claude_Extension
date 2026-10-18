# v0.9 CLI 연결 계약

모델·계정 조회와 실제 요청을 구분한다. 확장은 암호·토큰 파일을 직접 읽지 않으며 사용자가 선택한 네이티브 CLI `.exe`를 실행한다. CLI 인증은 각 공식 도구의 책임이다.

## 자동 모델·계정 조회

Codex app-server의 initialize → account/read → model/list(페이지 처리) → account/rateLimits/read를 사용한다. Claude는 stream-json 제어 initialize 응답의 모델·계정 메타데이터를 읽는다. 이 경로에서 thread/turn이나 사용자 추론 요청을 생성하지 않는다. 제한 시간은 18초이며, 미지원 CLI는 기본값·직접 입력·로컬 카탈로그로 안내한다.

모델 ID, 지원하는 생각 강도, 광고된 빠른 속도 기능을 UI에 반영한다. 요금제 문자열은 계정 정보로 표시하며 임의의 Pro/하위 요금제 기능표를 만들지 않는다. 모델 목록은 사용 권한 보장이 아니고 실제 요청 시 서비스가 판정한다. 실행 파일·AI 탭을 바꾸면 이전 연결의 모델·사용량을 초기화하고 늦은 이전 조회가 현재 선택을 덮지 않게 한다.

## 수정안 검토

기존 고립 실행을 유지한다. Codex exec는 사용자 설정을 제외하고 빈 임시 작업 폴더에서 읽기 전용 sandbox·명령 도구 금지·출력 스키마를 사용한다. 공개 JSON 이벤트와 output-last-message로 구조화 결과·실제 사용량을 관찰한다. Claude print는 도구·MCP·설정 원본을 제한한 stream-json/구조화 결과를 사용한다.

선택한 코드의 UTF-16 위치·행·열·텍스트를 전체 원문과 별도로 보낸다. 적용·저장·복구는 전체 문서와 디스크 해시의 충돌 검증을 유지한다. 정상 결과만 한 번 요청하며 실패를 숨기는 자동 AI 재호출은 하지 않는다.

## 명시적인 전체 액세스

Codex는 프로젝트 cwd의 app-server thread에 approvalPolicy=never, sandbox=danger-full-access를 요청한다. Claude는 stream-json/bypassPermissions를 사용한다. 선택한 모델·강도·속도와 공통 규칙·담당 역할·대화·첨부를 stdin으로 전달한다. 기본 실행 제한은 30분이며 종료 시 자신의 Job만 정리한다.

열린 미저장 문서는 실행 전에 차단한다. 동료가 지정되면 기존 읽기 전용 검토를 먼저 수행하고 최종 담당자만 직접 실행한다. 실패·중지는 완료와 구분하며 이미 실행된 결과의 자동 복구를 주장하지 않는다. 민감한 서버 오류 원문과 표준 오류 인증 출력을 대화에 저장하지 않는다.

공개 작업 이벤트·중간 답변과 실제 usage만 관찰하고 내부 추론은 표시하지 않는다. Codex의 누적 turn diff와 도구별 보고를 합치며, 명령 쓰기는 제한된 프로젝트 전후 텍스트 비교로 보완한다. 비교 한도·줄 수 미제공·작성자 귀속 범위는 [사용 안내](USER_GUIDE.md)에 설명한다.

## 강도·속도·사용량

빈 실행 경로는 프로세스·사용자·시스템 PATH와 알려진 native 설치 위치에서 자동 발견한다. 명시 경로의 오류를 다른 설치로 자동 우회하지 않는다. 발견만으로 로그인 상태를 확정하지 않으며 Claude 모델 초기화 응답의 account 정보도 인증 성공 근거로 사용하지 않는다. 실제 `claude auth status --json`을 이용하는 계정 연결 확인은 별도다.

요청의 `user_instructions`에는 내장 필수 협업·소스 주석 규칙과 사용자 규칙을 한 번 조합한다. `ChatRequestContext.Instructions`는 사용자 원문·8000자 한도를 그대로 유지하고, 최종 전송 경계의 `EffectiveInstructions`만 기본 규칙을 포함한다. 일반 수정안·전체 액세스·동료 검토가 같은 경계를 거친다. 숨김 관리 사본은 사용자 파일 내용을 덮지 않으며 요청 전 부재 복구를 수행한다.

기본 모델은 빈 문자열, 강도 자동은 추가 재정의 없음이다. 안전한 강도 토큰 형식을 허용하되 선택 모델이 광고한 단계만 실행한다. Codex exec는 model_reasoning_effort/service_tier 설정, app-server는 turn의 effort/serviceTier, Claude는 effort와 fastMode 설정을 사용한다. 일반 수정안 경로는 Claude의 알려진 비대화형 Fast 최소 CLI 조건도 검사한다.

일반 Codex usage는 완료 시점 값, 전체 액세스는 thread 사용량 이벤트다. Claude 부분 이벤트의 누락 입력·캐시는 추정하지 않는다. 계정 백분율과 토큰 수는 별도 표시하며 청구액·월간 잔량으로 환산하지 않는다.

공식 계약: [Codex app-server](https://learn.chatgpt.com/docs/app-server), [Codex CLI](https://learn.chatgpt.com/docs/cli/reference), [Claude CLI](https://code.claude.com/docs/en/cli-reference), [Claude 모델](https://code.claude.com/docs/en/model-config), [Claude SDK](https://github.com/anthropics/claude-agent-sdk-typescript). 설치된 Codex 0.163.0-alpha.10.2의 생성 스키마도 실제 API 검사에 사용했다.
