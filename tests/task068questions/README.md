# TASK068 질문 기능 검사 준비

이 폴더는 Role 2026-10-20 12:47 #4의 **제안 계약과 미실행 검사 자료**다. Code TASK067이 제품 소유권을 갖고 있으므로 질문 파서·UI·전송 구현과 제품 파일은 수정하지 않았다. 제품 컴파일·GUI·원격 AI·로그인·실추론도 실행하지 않았다.

`QuestionProtocolCases.json`은 단일 `ask_user` fence 계약을 가정한 파서 33개 사례와 호스트/카드 상호작용 14개 사례다. 실제 구현자가 계약을 확정한 뒤 실제 제품 파서와 UI/큐에 연결해 검사해야 한다. 현재 Code의 번호 목록→입력란 초안 방식과 구조화 파서 계약은 다르다. 일반 번호 목록은 이 제안의 구조화 parser에서는 0개이며, legacy 방식의 지원 여부는 별도로 정해야 한다.

`Test-QuestionFixtureData.ps1`은 검사 데이터 자체의 ID·형식·제안된 유효 JSON 예시를 확인할 뿐 제품 테스트가 아니다. 결과에 `ProductParserTested=false`, `ProductUiTested=false`, `ContractAcceptedForImplementation=false`를 명시한다. 이 준비 결과를 기능 구현 완료나 제품 PASS라고 표현하면 안 된다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/task068questions/Test-QuestionFixtureData.ps1 -OutputDirectory artifacts/task068questions/new-fixture-readiness
```

제품 구현 후에는 최소한 다음 별도 검사를 실행해야 한다.

- 최종 assistant 원문만 인식하고 user/system/도구·첨부·인용·코드 예제·부분 스트림은 카드로 만들지 않는다.
- 한 batch의 질문/선택지 ID와 타입·길이·개수·중복 JSON key를 검사하고 거부 시 원문을 유지한다.
- 기본 선택 없음, 선택/직접 입력만으로 전송하지 않음, 명시적 응답 제출, 소유 대화·provider 결합, 이중 클릭 중복 방지.
- 기존 초안과 질문 응답을 보존하고, 큐 가득 참·저장 실패·취소·dispose·오래된 카드·대화 전환 경쟁에서 다른 대화로 보내지 않는다.
- 이전 10개/24000자 모델 문맥에서 질문이 제외돼도 제출 내용에 질문/선택지의 고정 원문과 실제 사용자 응답을 담는다.
- 재로드·시간별 history 페이지 이동에서 이미 답변/보류한 batch를 새 미응답 질문으로 재활성화하지 않는다.
- 기본 메뉴/테마/키보드/접근성·좁은 화면을 유지하고, 기존 도구 승인 거부 경계를 질문 응답으로 우회하지 않는다.

Root가 실제 제품 구현·고정 후보·독립 감사·4SKU 컴파일·최신 Release 제작·게시·익명 다운로드 해시 확인까지 통합한다.
