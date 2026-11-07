# 독립 comment-code core 자체 검증 인계

작성: CodexOrig 보조064 · 2026-11-07. 소유 경로는 이 새 tests 폴더와 `src/CommentCodePreview.Poc/`뿐이다. 공개 API/통합 계약은 해당 README에 있다. 실제 adapter 담당061에게 정확한 생성자/속성, UI thread capture, atomic 재진입/own 변경 알림과 postimage 경계를 전달했다.

## 실제 결과

- 최초 `artifacts/run-8a28697916f5/`: 94 PASS/0 FAIL, 7그룹, 원본 보존.
- 최종 `artifacts/run-0399c959903d/`: **104 PASS/0 FAIL, 7그룹**. core와 fixture를 .NET Framework 4.6.2 reference로 private C#5 `/warnaserror+` 컴파일하고 별도 비GUI 프로세스에서 실행했다.
- `result.json` SHA-256: `491B63C14037C1A70300795324D4C7BA75EBAB0A5AFC1EF3C73349B3E48AB191`
- `validation.json` SHA-256: `47D979234BFCA970E5075B44E532839B034A2D7450446016AE7021FF71411FEF`
- 실제 로드한 private `CommentCodePreview.Poc.dll` SHA-256: `89621219DFCBA1F77A149EFCB4F372FA4FFB3AC5A596B74634B45E118D7F0337`. 테스트 안에서도 로드 경로와 SHA를 검증했다. 기존 VS 확장 DLL을 검사한 것은 아니다.
- 원본 source/사본 before/after SHA가 모두 같았다. csc 2개와 fixture 1개는 각각 exit 0, 정상 종료, 강제 정리 0, 생존 0이다. PID/개별 시간은 각 `*.process.json`에 있다. 이는 실제 AI 지연 측정값이 아니다.

## 검증 범위

7그룹은 happy, stale-guards, cancel-late, bounds-errors, unicode, atomic-undo, capacity-dispose이다. 명시 요청 전 provider 호출 0, 미리보기 중 버퍼 변경 0, 정확한 요청 ID 수락/한 번 Undo, 잘못된 ID·재수락 거절, 선택의 문맥 사용/삽입 전용 동작을 확인했다.

문서 ID/버전/내용 hash/커서/선택/탭 왕복 세대, readonly/diff/닫힘/IME 상태의 초기·late·수락 경계, 알림 후 미리보기 폐기, 새 요청이 이전 응답을 격리하는 동작을 확인했다. 공급자 오류는 typed 상태로만 남기고 빈/너무 큰/잘못된 Unicode·개행 문자열을 거절했다. UTF-16/UTF-8 상한, CRLF·surrogate 중간 위치 거절과 한글·emoji·combining/ZWJ 문자열의 정확한 삽입/Undo를 확인했다.

실제 두 worker thread로 동시 수락을 시도해 하나만 삽입됐고, 가짜 원자 경계에서 외부 편집/취소가 끼면 삽입을 거절했다. 수락 후 외부 편집과 readonly 변화가 있으면 Undo가 기존 버퍼를 덮어쓰지 않았다. 취소를 무시하는 모의 공급자 4개 상한과 회복, 종료 후 늦은 결과, 재진입 Dispose의 session 부활 방지도 검사했다. 가짜 editor에는 저장/명령 실행 API가 없으며 코드 문자열을 실행하지 않는다.

## 한계와 반납

이 결과는 **순수 core + FakeEditorFixture + 고정/지연 모의 공급자**다. 실제 VS SDK/IntelliSense/IME 탐지, 실제 열린 파일·편집 버퍼, 고스트 텍스트/UI, 실제 AI·계정·모델·CLI·네트워크·소스 전송·실제 VS Undo는 검증하지 않았다. 기존 C6/베스트케어/프로젝트/manifest/릴리스 입력이나 배포 차단 조건을 변경하지 않았다.

실제 adapter는 문서 이벤트 세대, UI thread 연결, 한 임계구역의 compare/apply와 receipt, 사용자에게 보일 전송 범위·공급자·동의, 기존 FullAccess를 자동 계승하지 않는 정책을 별도 구현/검증해야 한다. 취소 후 비협조 공급자 Task 자체를 강제 종료하거나 실제 서비스의 timeout/요금을 제어하는 모델은 아니다. Snapshot은 전체 문서 상한을 적용하며 큰 문서를 부분 문맥으로 가장하지 않는다. Undo는 정확한 fake postimage만 인정하는 보수적 정책이다.

모든 생성 소스/README/검증기 및 고정 근거를 Root에게 반납한다. Root가 같은 고정 소스와 실제 연동 후보를 기준으로 CodexOrig2 감사와 CodexOrig6 연구 양쪽 검토를 진행한다. 이 자체 검증의 완료는 사용자의 최종 실제 VS→AI→수락→버퍼 삽입/Undo 기능의 완료나 배포 승인이 아니다.
