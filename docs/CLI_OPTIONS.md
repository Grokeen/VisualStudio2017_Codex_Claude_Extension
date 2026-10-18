# 모델·생각 모드의 CLI 전달

2026-10-18 확인. 모델은 사용자가 지정한 문자열을 해당 CLI의 `--model` 인수로 전달한다. 빈 모델은 CLI 기본값을 사용한다. 표시되는 예시 이름이 해당 계정의 이용 권한을 보장하지 않으므로 사용자 정의 모델 이름도 입력할 수 있다.

생각 강도는 auto와 모델이 광고한 수준을 구분한다. Codex는 로컬 models_cache.json의 visible 모델·supported_reasoning_levels·default_reasoning_level·additional_speed_tiers를 읽는다. low/medium/high/xhigh/max/ultra와 none/minimal은 실제 모델이 광고할 때만 허용하며, 기본/알 수 없는 직접 입력 모델은 auto/low/medium/high까지만 사용한다. Codex는 `--config model_reasoning_effort=...`, Claude는 `--effort ...`로 전달한다. 기본 모드는 추가 effort 인수를 넣지 않는다. 인수는 argv 규칙으로 인코딩하며 셸 문자열로 실행하지 않는다.

근거는 [공식 OpenAI의 Codex CLI 사용 예제](https://developers.openai.com/cookbook/examples/codex/build_iterative_repair_loops_with_codex)와 [Claude 공식 CLI 참조](https://code.claude.com/docs/en/cli-reference)다. 설치된 Codex 0.162.0-alpha.17.2의 `codex exec --help`에서도 모델과 설정 재정의 인수를 확인했다. 실제 계정별 모델/effort 조합은 계정과 CLI가 판단하며 오류를 UI에 보고한다.

확장의 형상관리·DB 스키마·배포 권한은 각 업무 작업 창의 검토·실행에 적용한다. 소스 수정안 요청에서 CLI의 셸·파일 도구 제한을 해제하는 의미가 아니다. 코드 적용은 기존의 비교·승인·충돌 검사·별도 저장 흐름을 유지한다.

Fast는 Codex에서 --config service_tier='fast' 및 features.fast_mode=true, Claude에서는 --settings의 fastMode로 전달한다. Claude 기본 속도는 fastMode=false를 명시한다. 지원 모델·최소 CLI 버전과 계정 권한이 필요하다. [Codex 공식 속도 안내](https://learn.chatgpt.com/docs/agent-configuration/speed), [모델 목록 계약](https://learn.chatgpt.com/docs/app-server), [Claude Fast 안내](https://code.claude.com/docs/en/fast-mode)를 기준으로 확인했다. UI에서 특정 모델을 모든 계정에 제공한다고 보장하지 않는다.

AI 이름·역할·이번 요청의 실제 협업 결과는 별도의 agent_context로 stdin JSON에 담고 최대 8,000자로 제한한다. 협업 참여자는 동일 원본·첨부로 차례로 호출하며 중간 수정안은 적용하지 않는다. 모델·강도·Fast가 CLI의 파일 쓰기·도구·승인 권한을 확대하지 않는다.
