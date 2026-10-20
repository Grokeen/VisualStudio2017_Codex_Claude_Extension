# TASK069 디버깅 초안 검사

Windows PowerShell 5.1 `-STA`에서 `Test-DebuggingHelper.ps1 -AssemblyPath <고정 DLL> -AssetsPath <같은 후보 assets> -OutputDirectory <새 디렉터리>`를 실행한다. 기존 출력은 보존하고 재사용하지 않는다. 후보 source의 `Services/DebuggingContextService.cs`도 필요하다.

실제 제품 `DebuggingContextService`와 `AssistantControl`의 DBG Click 이벤트를 사용한다. 읽기 adapter만 합성이며 IDE의 실제 디버거를 시작하거나 제어하지 않는다. 제한된 소스/스택 형식, 현재 위치 변경 거부, 취소, 기존 초안·선택 보존, 16,000자 초과 원자적 거부, 대화 전환, Dispose 해제를 확인한다. 설치된 VS2017 `StackFrame2`의 IID/DispId 대조는 실제 중단 frame의 QueryInterface 성공을 증명하지 않는다.

개인 실행기만 컴파일하며 공유 bin/obj, 실제 계정·AI·네트워크, Window.Show·Popup·OS 입력을 사용하지 않는다. `inputs.json`, 복사한 fixture/제품 source, 컴파일 로그, `result.json`에 실제 결과와 한계를 남긴다. 실패 출력도 삭제하거나 통과로 바꾸지 않는다.

실제 IDE/debuggee의 파일·줄·열린 버퍼 읽기와 이벤트 구독 해제는 통합 담당의 별도 실제 IDE 검사 범위다. 최종 4SKU 빌드·감사·최신 Release 제작·실제 게시·익명 다운로드 SHA 확인까지 통합 마감은 별도로 필요하다.
