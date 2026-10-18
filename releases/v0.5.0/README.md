# v0.5.0 — 대화를 남기고 작업을 이어 가는 AI Assistant

Claude·Codex와 나눈 대화, 공통 instruction을 로컬 Markdown에 보관합니다. 사용자 말풍선과 AI 본문을 구분한 대화 화면에서 이미지와 모델·생각 모드를 고르고, 변경 파일 카드에서 소스로 이동해 수정안을 검토하세요. 필요한 권한으로 SQL Server 스키마·형상관리·배포 작업도 실행할 수 있습니다.

[VSIX 다운로드](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/raw/refs/heads/main/releases/v0.5.0/VS2017.AIAssistant-0.5.0.vsix) · [상세 사용 안내](../../docs/USER_GUIDE.md) · [프로젝트 소개](../../README.md)

## 시작하기

1. VS2017 15.9 이상과 사용할 AI의 Windows 네이티브 CLI 및 계정을 준비합니다.
2. Visual Studio를 닫고 VSIX를 실행해 설치합니다.
3. 도구 → AI Assistant를 열고 첫 사용 안내를 확인합니다.
4. Claude 또는 Codex 탭을 고른 뒤 오른쪽 위 메뉴나 우클릭 설정에서 계정을 연결합니다.
5. 소스를 열고 요청을 입력합니다. Enter로 보내고 Ctrl+Enter로 줄바꿈합니다. 이미지·텍스트는 드롭·첨부·Ctrl+V로 추가합니다.
6. 변경 파일 카드에서 내용을 비교하고 승인 후 적용·별도 저장합니다.

새 대화는 기존 기록을 보존한 채 대화 목록에 추가합니다. 이름 변경·닫기·불러오기를 지원하며 닫힌 기록은 다음 시작의 자동 복원에서 제외합니다. 공통 instruction은 모든 프로젝트에서 공유합니다.

## SQL·프로젝트 작업

프로젝트 작업에서 DB 스키마 읽기를 허용하면 SQL Server의 테이블·열 정의를 참고해 SQL 작성을 요청할 수 있습니다. SQL을 DB에서 실행하지 않으며 연결 정보를 보관하거나 AI에 보내지 않습니다.

Git fetch/pull/checkout/commit/push, SVN update/checkout/commit, TFVC get/checkout/checkin과 지정 실행 파일의 배포를 지원합니다. 필요한 권한을 선택하고 정확한 명령과 대상을 확인한 뒤 실행합니다. 저장소 인증과 CLI 설치는 미리 준비합니다.

## 검증과 지원 범위

Debug/Release 빌드는 경고·오류0이며 기존 회귀169개, 신규 저장·업무·UI193개와 실제 VS2017 검사58개가 모두 통과했습니다. 실제 Codex의 소스 수정과 빈 SQL 작성·승인·저장도 확인했습니다. 세부 결과는 [검증 보고](../../MarkdownReport/CodexOrig_TASK-013.md)와 [신규 기능 보고](../../MarkdownReport/CodexCode_TASK-012.md)에 있습니다.

패키지 크기는179,232바이트이며 SHA-256은 아래와 같습니다. [SHA256SUMS.txt](SHA256SUMS.txt)와 비교할 수 있습니다.

```text
DC2DB3178CD6490CCE529EA95E31133CD7B8997F9B146B656C54EC0FA474C768
```

현재 패키지는 VS2017용입니다. VS2019/2022 확장 지원, 실제 Claude 계정·운영 DB·외부 SVN/TFVC 및 운영 배포는 검증 범위를 별도로 확인해야 합니다. 현재 코드 적용은 기존 텍스트 파일 한 개의 비교·승인·적용·별도 저장입니다.
