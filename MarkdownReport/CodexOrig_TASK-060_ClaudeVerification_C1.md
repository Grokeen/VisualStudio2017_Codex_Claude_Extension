# TASK060 후속 · Root C1 네 대상 Claude 경계 검사

사용자의 최신 선택 **“이번에는 로그인 없이 검사해줘”**에 따라 실제 Claude 로그인·계정 조회·모델 요청은 추가로 실행하지 않았다. 기존 177개 검사와 native CLI 비인증 근거는 원문을 보존했다.

Root가 제공한 **0.12.1 C1** (`artifacts/task054/candidate1`)의 네 제품 DLL을 그대로 복사하여 합성 Claude 경계 검사 25개씩 실행했다. **4개 대상 모두 종료 코드 0, 합계 100/100 통과**다. 개인 테스트 executable만 컴파일했으며 제품/공유 빌드/GUI/배포 파일을 변경하지 않았다.

| 대상 | 통과 | 실제 검사한 DLL SHA256 |
| --- | ---: | --- |
| VS2017 Legacy | 25/25 | `D07D78824C67EC6ACCCF9EB98DECE62F6617A5FD34CCA92F5D25F11D05E4B9EB` |
| VS2017 | 25/25 | `3E527BBC4D93BD0E0B9969B06C85651F2BAD2CD74A1C650E5CA347564053A995` |
| VS2019 | 25/25 | `9081A66E338679BF020DE625BED1EA0F511849EAADB38D8A3CBD4501F58C4529` |
| VS2022 | 25/25 | `CF94A149AA968421A231F71C543F560F09680F2614F604A2180054C0F69784F0` |

각 결과는 `artifacts/task060/candidate1-<대상>/claude-boundaries/result.json`에 있다. 결과 원문 SHA256, 입력 DLL 경로, 실행 시각과 상태는 별도 `CodexOrig_TASK-060_ClaudeVerification_C1.json`에 기록했다. 입력 DLL과 검사 폴더에 복사한 DLL의 SHA256 일치를 확인했다. 검사는 UI/VS SDK 형식을 생성하거나 로드하지 않는 Provider API만 사용하므로 타깃별 `project.assets.json` resolver가 필요하지 않았으며 네 대상 모두 SDK 어셈블리 로드 오류 없이 완료했다.

Root C1 source의 `ClaudeRuntimeRunner.cs` 및 `CliEditProvider.cs` SHA256은 TASK060 동결본과 각각 일치했다. 명시 모델/Fast 버전 선검사, 과대/잘못된 버전 출력, 버전 명령 실패, 취소/전체 제한 시간, 이미지/문맥 전달, 수정안 쓰기 제한, 정책 추가 승인/실행 오류/연결 종료 구분을 네 SDK 대상으로 확인했다.

실제 Claude AI 응답·계정 모델 이용 권한·고객 PC 환경은 이 검사의 범위가 아니다. 이전 native 확인 당시 CLI는 미로그인이었고, 초기화 모델 목록을 인증 성공으로 해석하지 않는다. Root054가 실제 IDE/시각 검사·독립 감사·최신 Release 제작·배포·공개 다운로드 검증을 이어가도록 통합 인계한다.
