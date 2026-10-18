# CodexOrig3 TASK-033 follow-up — Claude/Codex 교차 제공자 협업 문구

완료 시각: 2026-10-19 03:52:30 KST

README의 협업 개요와 USER_GUIDE의 **대화 AI 이름과 협업** 부분을 확장했다. candidate10(v0.11.0) 개발 후보에만 새 동작을 귀속하고, 기존 v0.10.0 버전·다운로드 안내는 바꾸지 않았다. 통합 검증과 배포가 끝나지 않았고 실제 Claude 계정 호출 성공을 보증하지 않는다는 상태 문구를 넣었다.

- 같은 협업팀의 열린 Claude/Codex 대화를 서로 교차해 주 담당자 외 최대3명 선택. Claude 주 담당→Codex 동료 또는 Codex 주 담당→Claude 동료의 양방향 구성 가능.
- 보내기를 명시하고 소스를 열었을 때만 요청이 시작된다. 각 참여자는 자기 대화의 저장 CLI 경로·모델·생각 강도·응답 속도로 순차 한 차례 검토하며, 주 담당 AI가 회신을 종합한다.
- 요청·선택 소스·현재 첨부가 각 선택 서비스에 전달된다. 사용량과 시간이 추가되고, 재귀 자동 대화/무한 토론은 없다.
- 사용자가 선택 서비스로 보내는 내용과 사용량·시간을 전송 전에 고려하도록 표시했다.

C7 PDF와 ZIP은 재생성하지 않고 SHA-256으로 기존 바이트를 확인했다. 제품 코드·빌드·GUI·IDE 검증·실계정 요청·배포는 수행하지 않았고, Git 기록·브랜치·커밋도 변경하지 않았다. README/USER_GUIDE diff whitespace check는 통과했으며 작업공간의 CRLF 정규화 알림만 있었다. cross-provider candidate10 검증은 제품 소유자가 계속 진행한다.

## SHA-256

| 파일 | SHA-256 |
|---|---|
| `README.md` | `EB8397B4B7DADFD24EE3819B6F30D5A185522EADC660439A583B809B2B34C6E5` |
| `docs/USER_GUIDE.md` | `703B4DECCDA74D031DB567AB24FEF1AB33D811FCF43776B56A8F8C085C8E784B` |
| 보존 확인: C7 PDF | `EF6C8A3B5BE274F6FDAB77FBA6E07221F9E33B13E85B574380625EB0E3AF5453` |
| 보존 확인: C7 ZIP | `019E8BF35FB07A5E36D48EFC2E162EC9C35A0C7DDB1CC5747BA6B6B7DC542F64` |


