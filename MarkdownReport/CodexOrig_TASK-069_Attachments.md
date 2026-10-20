# TASK069 #13 · 텍스트 첨부 4개와 종류 아이콘

담당: CodexOrig 보조061. Role.md 마지막 #13과 image-76.png를 직접 확인했다. 사용자 화면에는 파일명 4개와 첨부 칩이 남아 있고, AI는 제공된 원본을 기준으로 현재 작업 파일을 찾고 비교하겠다고 답했다. 이 답변만으로 실제 첨부 본문 누락을 단정할 수 없다. 칩은 다음 요청에도 재사용하도록 남기는 기존 동작이다.

C3 제품 DLL에서 실제 `AttachFilesFromUiAsync`로 네 파일을 읽고, 실제 queued capture를 거쳐 Codex full app-server/승인 exec와 Claude full stream-json/승인 경로의 실제 자식 EXE가 받은 JSON을 비교했다. 일반 한글 파일 4개, 각 100KB·합계400KB 파일 모두 이름 4개와 본문 4개가 정확히 전달됐다. 첨부 뒤 원본 파일이나 현재 첨부 목록을 바꿔도 캡처된 원문이 유지됐다. 다섯 개 파일, 100KB 초과 파일, 잘못된 UTF-8, 바이너리 NUL은 명시 오류와 함께 일괄 거절됐으며 앞의 정상 파일만 부분 첨부하지 않았다.

별도 경계 결함도 발견했다. 입력 제한 안에 있는 100KB 파일 네 개에 JSON escape 대상 문자가 매우 많으면 요청 JSON이 약2.46M자로 확장된다. C3 승인 모드의 송신 serializer는 2,000,000자 한도여서 Codex·Claude 모두 CLI 시작 전에 명시적으로 실패했다. 이는 화면의 일반 파일에서 누락됐다는 실측 근거는 아니지만, 허용된 입력이 전송되지 않는 실제 경계였다. Root 승인으로 **송신 serializer만 full runtime과 같은16MiB**로 맞췄다. 응답2M 제한, 파일 수4개, 텍스트 파일100KB, 기존 이미지·문서 제한은 변경하지 않았다.

추가 요청한 텍스트 아이콘은 실제 첨부 템플릿에 **TXT** 배지로 표시한다. 추출 문서는 DOC로 구분하고 기존 이미지 미리보기는 유지한다. 전체 파일명은 원래 이름을 보존하며 tooltip과 automation name/help에 전체 이름·읽은 본문 문자수·UTF-8 바이트수를 제공한다. 좁은 영역의 WrapPanel과 × 삭제 동작은 유지했다. 원본 파일을 삭제하거나 수정하지 않는다.

이번 요청에 고정한 첨부는 사용자 메시지에 파일별 이름과 본문 분량으로 표시한다. **첨부 본문 포함 요청 · 4개**와 각 파일의 문자수·UTF-8 분량을 볼 수 있고, 다음 요청에도 유지되므로 제외할 때 ×로 제거하라는 안내가 나온다. 이는 캡처한 요청의 내역이며 서버 수신·모델 사용 완료를 주장하지 않는다. 숨겨진 본문을 사용자 메시지에 다시 복사하지 않는다. 요청 원문16,000자에 파일 네 개의 metadata를 더한 저장·캡처도 검증했으며, 프롬프트 자체의16,000자 제한과 저장 실패 시 기존 입력 복원은 보존했다.

제품 소유 범위는 새 `Chat/AttachmentDisplayInfo.cs`, `ChatAttachment` 표시 속성, `AssistantControl.xaml`의 첨부 템플릿, `RequestQueue`의 제출 요약, `CliEditProvider.BuildPrompt`의 송신 serializer 한 곳이다. Root가 새 Compile 등록과4SKU 동기화를 담당했다. AI 메시지 #1 구현은 변경하지 않았다.

검증 원본은 다음과 같이 모두 보존했다.

| 원본 | 결과 | 해석 |
| --- | --- | --- |
| `artifacts/task069messages/attachments-candidate3-1` | fixture 오류13개 | reflection params에 string[]를 한 인자로 넘기지 않은 하네스 오류. 제품 첨부 검사 전 실패이며 수정 뒤 재실행했다. |
| `attachments-candidate3-2` | 94 PASS / 2 시나리오 오류 | 일반4개·합계400KB는 네 경로 모두 전달. 고밀도 escape의 두 승인 경로만 송신 JSON 크기 한도에서 실패했다. |
| `attachments-candidate4-1` | **120/120 PASS** | 네 경로×일반/cap/escape-cap 12조합의 이름·본문·문자수·UTF-8 합계·hash, 실제 BAML/접근성/줄바꿈/삭제/16K 입력을 통과했다. |
| `candidate4-messages-1` | **58/58 PASS** | 기존 메시지 전달 기능의 실제 UI/API·readonly CLI 회귀도 통과했다. |

C4 DLL SHA256은 `E9B5192292C331A3E3F22861F572AFB2EE596674B26EE4AFC6F55C4BFD904D87`다. `tests/task069attachments/Test-AttachmentWire.ps1`은 AssemblyPath/AssetsPath/새OutputDirectory를 받는 Windows PowerShell5.1 STA 검사이며, 실행기 담당064에게 기본 검사 연결을 인계했다. 원문 JSON/argv/PID/cwd와 실제 파일 hash는 각 증거 폴더에 있다.

실제 AI 계정·로그인·원격 추론, 물리 화면 렌더링·클릭은 이번 검증 범위가 아니다. 사용자의 실제 네 파일이나 실제 AI가 어느 내용을 활용했는지는 확인하지 않았다. 스크린샷의 AI 행동 원인은 미확정으로 유지한다. Root의 최종 통합 후보26개 회귀·설치 IDE/독립 감사·최신Release 생성·실제 공개·익명 다운로드 해시 검증까지 전체 마감은 별도 필요하다.
