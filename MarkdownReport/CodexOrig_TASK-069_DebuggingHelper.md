# TASK069 Role #18 디버깅 도우미

담당: CodexOrig 보조061. image-81과 Orig4 설계를 직접 확인했다. 그림은 현재 중단 실행줄과 호출스택이며 예외 대화상자가 아니다. 일반 중단을 오류·예외라고 추정하지 않는다.

사용자가 중단 중 표시되는 작성란의 `DBG`를 클릭하면 현재 파일명·줄·함수, 열린 해당 소스의 앞뒤 최대 3줄, 호출 스택 함수명 최대 3개를 기존 요청 초안에 추가한다. 전송 전 사용자가 검토한다. 기존 입력과 선택 영역을 보존하며 16,000자를 넘으면 전체 입력을 바꾸지 않고 안내한다. 읽기 중 초안·대화가 바뀌거나 위치가 변경되거나 읽기에 실패하면 추가하지 않는다.

수집 범위는 현재 frame과 정확히 일치하는 이미 열린 문서다. 디스크 파일을 새로 열지 않고 별도 EditPoint로 최대 3,000자를 읽어 7줄, 행별 400자, 전체 문맥 5,000자로 제한한다. 값·Locals·Autos·Watch·인수·임의식 평가·디버거 실행/중지/재개/step·자동 AI 전송은 없다. 예외 타입과 메시지는 수집하지 않는다. 전체 경로 대신 파일명만 초안에 넣는다. 소스 자체에는 민감한 내용이 있을 수 있으므로 전송 전 확인 안내를 표시한다.

제품 소유 변경은 새 `Services/DebuggingContextService.cs`, 새 `Views/AssistantControl.Debugging.cs`, XAML DBG 버튼, xaml.cs Initialize/Dispose hook뿐이다. Root가 Compile2와 4SKU Sync를 수행했다. SDK 위치 API는 설치된 VS2017 `EnvDTE90a.StackFrame2`의 IID/DispId와 같은 읽기 전용 COM 계약으로 사용하며 새 패키지 참조는 없다. UI dispatcher 검증과 자기 이벤트 해제를 적용했다. C6 분석기 실패와 C7 2022 경고 원문은 통합 담당 artifacts에 보존하며 최종 C8에서는 Root가 4SKU 오류·경고 0개를 확인했다.

독립 검사는 `tests/task069debughelper/Test-DebuggingHelper.ps1`을 C8 실제 DLL과 정확한 assets로 실행했다. 결과 `artifacts/task069debughelper/candidate8-1/result.json`: **47/47 PASS**. 실제 제품 형식화 서비스와 WPF Click handler에 합성 읽기 adapter를 주입했다. 중단 상태 가시성, source/stack 한도, 취소·위치변경 거부, 기존 입력·선택 보존, 16K 초과 원자적 거부, 읽기 실패, 초안/대화 변경, Dispose와 이벤트 해제, chat/queue/CLI 자동 실행 0을 확인했다. SDK 메타데이터 IID와 readonly 속성 DispId/type도 일치했다. 복사한 입력·fixture/source·로그와 SHA를 보존했다.

실제 제품 DLL SHA256: `F821132D6DD4225F98853A68F9154F692432437041AE59EE01512F7B564F9DB2`. exact assets SHA256: `E4E14C7C98ECD9D2AF9A56CE9DA66A3A04482916D65E0FED484E418447C43AA2`. 실행 전후 원본은 동일하다. 전체 검증/소스 SHA는 `CodexOrig_TASK-069_DebuggingHelper_Validation.json`에 기록했다.

이 검사는 native debuggee의 실제 COM frame QueryInterface 성공이나 실제 IDE 이벤트·물리 화면·AI 분석 품질을 증명하지 않는다. Window.Show·Popup·OS 입력·로그인·네트워크·실AI는 사용하지 않았다. Root 실제 IDE 중단 fixture가 해당 남은 경계를 검사한다. 독립 파트 완료 뒤에도 최종 4SKU 빌드·컴파일, 감사, 최신 Release 생성, 실제 공개 배포와 익명 다운로드 SHA 확인까지 Root 통합 마감이 필요하다. 기존 TASK069 #1 메시지와 #13 첨부 소스/증거는 변경하지 않았다.