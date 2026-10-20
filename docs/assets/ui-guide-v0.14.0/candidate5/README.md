# 후보5 대화 화면 캡처

- 기준 제품: TASK069 후보5 Visual Studio 2017 Release의 `VS2017.AIAssistant.dll`
- DLL SHA-256: `17E93831EF6CD71A7E20952F151CD9AD4174402B71625434BCB85E6D90E5561E`
- 리소스 인덱스 SHA-256: `BB6BD39F3C98162592751A94E586F5873EC2807CB7B255B4EA267C7A4E2D3B4C`
- 캡처 도구: 제품 컨트롤을 숨겨진 오프스크린 WPF로 렌더한 `CaptureC5Ui14to16.exe`; 실제 Visual Studio 창, Popup, OS 입력, AI 전송, 네트워크는 사용하지 않음.
- 예시 데이터: `사용자 · Codex` 표시명과 합성 대화. 실제 계정, 프로젝트, 작업 내역은 사용하지 않음.
- 검증: `candidate5-ui14-16-pass4/capture-result.json` 상태 PASS. 선택 대화의 유휴/실행/대기 및 다른 대화 실행 상태 버튼, 다섯 팔레트의 사용자·AI 말풍선 표면, 좁은 화면 스크롤 중 고정 머리글을 실제 WPF visual tree와 렌더에서 확인함.
- 제한: 이는 제품 DLL을 이용한 합성 WPF 그림이며 실제 설치 IDE의 운영 화면 캡처가 아님. Dracula 우클릭 컨텍스트 메뉴(#17)는 이 캡처 묶음에 포함되지 않음.

텍스트 첨부 예시는 같은 C5 DLL/assets의 별도 익명 fixture `candidate5-text-attachments-pass1`에서 만들었습니다. 네 개의 합성 TXT 파일을 실제 전송 없이 composer에 표시한 오프스크린 WPF 렌더입니다. 결과는 PASS이고, 제품·전송·AI·OS 입력·팝업·네트워크 동작은 사용하지 않았습니다. 파일 칩의 TXT 표시와 좁은 폭에서의 배치를 보여 주며, 파일명 전체/문자 수/UTF-8 크기 및 오류 시 전송 경계는 본문 설명으로 보완합니다.

PNG 파일은 캡처 결과의 SHA-256을 그대로 보존했습니다. 개인 경로와 실행 로그는 고객 문서에 포함하지 않습니다.
