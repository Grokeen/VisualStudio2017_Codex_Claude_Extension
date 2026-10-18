# 모델·생각 모드의 CLI 전달

2026-10-18 확인. 모델은 사용자가 지정한 문자열을 해당 CLI의 `--model` 인수로 전달한다. 빈 모델은 CLI 기본값을 사용한다. 표시되는 예시 이름이 해당 계정의 이용 권한을 보장하지 않으므로 사용자 정의 모델 이름도 입력할 수 있다.

생각 모드는 기본·low·medium·high를 구분한다. Codex는 `--config model_reasoning_effort=...`, Claude는 `--effort ...`로 전달한다. 기본 모드는 추가 effort 인수를 넣지 않는다. 인수는 argv 규칙으로 인코딩하며 셸 문자열로 실행하지 않는다.

근거는 [공식 OpenAI의 Codex CLI 사용 예제](https://developers.openai.com/cookbook/examples/codex/build_iterative_repair_loops_with_codex)와 [Claude 공식 CLI 참조](https://code.claude.com/docs/en/cli-reference)다. 설치된 Codex 0.162.0-alpha.17.2의 `codex exec --help`에서도 모델과 설정 재정의 인수를 확인했다. 실제 계정별 모델/effort 조합은 계정과 CLI가 판단하며 오류를 UI에 보고한다.

확장의 형상관리·DB 스키마·배포 권한은 각 업무 작업 창의 검토·실행에 적용한다. 소스 수정안 요청에서 CLI의 셸·파일 도구 제한을 해제하는 의미가 아니다. 코드 적용은 기존의 비교·승인·충돌 검사·별도 저장 흐름을 유지한다.
