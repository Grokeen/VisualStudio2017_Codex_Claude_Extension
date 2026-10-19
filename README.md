# Visual Studio AI Assistant

**Visual Studio에서 Claude·Codex와 대화하고, 코드를 고치고, 테스트까지 이어 가세요.**

오류 화면과 참고 문서를 보여 주고 자연어로 작업을 요청하세요. 바뀔 코드를 먼저 비교해 승인하거나, **전체 액세스**를 선택해 AI가 직접 파일 수정과 명령·테스트를 수행하게 할 수 있습니다. 대화와 공통 규칙은 내 PC의 Markdown에 남습니다.

**최신 미리보기 v0.12.1-preview.1** · [GitHub Release](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.12.1-preview.1) · [CLI 포함 완성 오프라인 ZIP](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.12.1-preview.1/VisualStudio_AI_Assistant-0.12.1-Preview1-FullOffline-win-x64.zip) · [CLI 제외 설치 ZIP](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.12.1-preview.1/VisualStudio_AI_Assistant-0.12.1.zip) · [설치 안내](releases/v0.12.1/README.md) · [자세한 사용법](docs/USER_GUIDE.md)

기존 정식 [v0.11.1](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.11.1)과 이전 오프라인 시험 파일은 그대로 제공합니다. 새 미리보기는 고객의 별도 오프라인 PC에서 확인할 수 있도록 배포하는 시험 버전입니다.

| 사용 중인 Visual Studio | 설치 파일 |
|---|---|
| 2017 15.7·15.8 · x86 IDE | [VS2017 15.7용 다운로드](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.12.1-preview.1/VS2017.15.7.AIAssistant-0.12.1.vsix) |
| 2017 15.9 이상 · x86 IDE | [VS2017용 다운로드](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.12.1-preview.1/VS2017.AIAssistant-0.12.1.vsix) |
| 2019 16.11 · x86 IDE | [VS2019용 다운로드](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.12.1-preview.1/VS2019.AIAssistant-0.12.1.vsix) |
| 2022 17.x · x64 IDE | [VS2022용 다운로드](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.12.1-preview.1/VS2022.AIAssistant-0.12.1.vsix) |

## v0.12.1 사용자 안내 (미리보기)

최신 공개 미리보기는 **v0.12.1-preview.1**입니다. 위 완성 오프라인 ZIP에는 확장 네 종류와 CLI, IDE 테마, 설치 검사 도구 및 사용자 PDF가 포함됩니다. 아래 그림은 이해를 돕는 합성 WPF 렌더이며 실제 Visual Studio/Windows 바탕 화면 캡처나 GIF가 아닙니다.

아래 예시는 WPF `RenderTargetBitmap`으로 별도 렌더한 합성 화면입니다. 실제 Visual Studio IDE·Windows 바탕 화면 캡처가 아니며, 화면 구성을 이해하기 위한 참고 그림입니다.

| 밝은 팝업 | 어두운 팝업 |
|---|---|
| ![밝은 배경 합성 화면 위의 설정 팝업. WPF 합성 렌더이며 실제 IDE 캡처가 아니다.](docs/assets/ui-guide-v0.12.1/candidate1/popup-light.png) | ![어두운 배경 합성 화면 위의 설정 팝업. WPF 합성 렌더이며 실제 IDE 캡처가 아니다.](docs/assets/ui-guide-v0.12.1/candidate1/popup-dark.png) |

| 어두운 팝업 제목 표시줄 | 공통 버튼과 간결한 아래쪽 동작 영역 |
|---|---|
| ![어두운 팝업의 제목 표시줄 부분 확대. 합성 WPF 렌더.](docs/assets/ui-guide-v0.12.1/candidate1/caption-dark.png) | ![어두운 팝업의 적용·닫기 버튼 영역 부분 확대. 합성 WPF 렌더.](docs/assets/ui-guide-v0.12.1/candidate1/compact-footer-dark.png) |

| Markdown 작성기 | 좁은 패널의 Claude/Codex 탭 |
|---|---|
| ![어두운 테마의 Markdown 작성기 컨트롤. WPF 합성 렌더이며 실제 IDE 캡처가 아니다.](docs/assets/ui-guide-v0.12.1/candidate1/markdown-dark.png) | ![폭 200픽셀 합성 패널의 Claude/Codex 탭과 입력 영역.](docs/assets/ui-guide-v0.12.1/candidate1/provider-tabs-200.png) |

다음 두 그림은 입력 말풍선과 IDE 테마 설정 UI를 보여 주는 합성 WPF 렌더이며 실제 Visual Studio/Windows 캡처가 아닙니다. 말풍선 문장은 가상 예시입니다. 실제 VS2017 15.9, VS2019 16.11, VS2022 17.14의 IDE 통합 시험에서 Kakao·Dracula 테마 선택 ID, 대표 환경 색상 적용, 보류 상태 소비와 임시 파일 정리를 확인했습니다. 설치 관리자 버튼의 실제 물리 클릭과 완료, 재시작을 포함한 고객 사용 흐름은 별도 확인이 필요합니다.

| 요청 메시지 말풍선 | Visual Studio 테마 설치 UI |
|---|---|
| ![합성 요청 문장이 어두운 사용자 말풍선으로 표시된 예시.](docs/assets/ui-guide-v0.12.1/candidate2/user-bubble.png) | ![표시 설정의 Kakao 및 Dracula 테마 설치 버튼. WPF 합성 렌더이며 실제 Visual Studio 설정 화면이 아니다.](docs/assets/ui-guide-v0.12.1/candidate2/display-ide-themes.png) |

- 요청을 실행하는 동안 다음 요청을 계속 입력할 수 있습니다. 현재 요청 뒤 최대 8개까지 FIFO 순서로 대기하며, 앞 요청이 끝나면 순서대로 실행합니다. 실패·취소·중지 뒤 남은 요청은 자동 실행하지 않습니다.
- 다음 후보의 성공 알림은 Claude/Codex 브랜드와 고정된 결과 유형(질문 답변·수정안 준비·파일 작업·실행/조회 작업), 보고된 완료 건수를 표시합니다. 요청 제목·원문과 답변 문장은 넣지 않습니다. Windows 알림 설정에 따라 표시되지 않을 수 있습니다.
- AI 답변을 우클릭해 답변 전체 또는 완결된 단일 코드 블록을 새 로컬 파일로 저장할 수 있습니다. 기존 파일은 덮어쓰지 않으며 PDF·XLSX·실행 파일 내보내기는 지원하지 않습니다.
- Claude 검사는 로그인 없이 CLI 버전·비인증 초기화·합성 프로토콜/오류/취소만 확인했습니다. 실제 Claude 계정 로그인과 실제 AI 답변을 검증한 결과는 아닙니다.
- 설정에서 **Kakao 테마 설치·적용** 또는 **Dracula 테마 설치·적용**을 선택하고 VSIX 설치를 완료한 뒤 모든 Visual Studio 창을 닫고 같은 IDE를 다시 여세요. 시작 시 선택한 IDE 테마를 적용합니다. **도구 → 옵션 → 환경 → 일반 → 색 테마**에서 결과를 확인할 수 있습니다. 세 IDE의 통합 시험에서 선택 ID와 대표 색상(Kakao `#FFD9DDE3`, Dracula `#FF282A36`) 적용을 확인했으며, 설치 버튼의 물리 클릭과 고객 PC의 전체 흐름은 별도 검증 범위입니다.

[v0.12.1 사용자 안내 PDF](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.12.1-preview.1/VisualStudio_AI_Assistant-0.12.1-Preview-UserGuide.pdf) · [자세한 사용자 안내](docs/USER_GUIDE.md)

각 버전에 맞는 설치 파일을 사용하세요. Windows 네이티브 `codex.exe` 또는 `claude.exe`와 해당 서비스 계정이 필요합니다. 확장을 직접 빌드할 필요는 없습니다.

**VS2017 15.7.1에서 업데이트 없이 설치:** 위의 **VS2017 15.7용**을 선택하세요. 15.7 당시 SDK로 별도로 컴파일한 패키지이며, 기존 15.9 이상용의 설치 하한만 바꾼 파일이 아닙니다. Community·Professional·Enterprise를 지원합니다. 현재 실제 IDE 검사는 VS2017 15.9에서 수행했으며, 고객의 15.7.1 PC 실행 결과는 별도 확인이 필요합니다.

설치 묶음 ZIP을 모두 풀고 **InstallAIAssistant.bat**를 실행하면 설치된 IDE의 버전·패키지 무결성을 검사하고 해당 버전용 설치기를 엽니다. 여러 IDE가 있으면 설치할 대상을 선택합니다. [설치 오류 해결과 진단 방법](docs/INSTALL_TROUBLESHOOTING.md)을 함께 제공합니다.

**설치할 때 인터넷이 없다면:** 위 **CLI 포함 완성 오프라인 ZIP**을 내려받아 USB로 옮기고, 압축을 모두 해제한 뒤 **VerifyOfflineSetup.bat**을 실행하세요. CLI를 다시 내려받아 묶음을 준비할 필요가 없습니다. [오프라인 설치 안내](docs/OFFLINE_INSTALL.md)와 [v0.12.1 사용자 PDF](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/download/v0.12.1-preview.1/VisualStudio_AI_Assistant-0.12.1-Preview-UserGuide.pdf)를 참고하세요. 로그인과 AI 사용은 연결 가능한 시점에 진행합니다.

설치된 확장만 제거하려면 VS2017에서 **도구 → 확장 및 업데이트 → 설치됨**, VS2019·2022에서 **확장 → 확장 관리 → 설치됨**을 여세요. 목록에서 해당 버전의 AI Assistant 항목을 **제거**한 뒤 모든 Visual Studio 창을 닫습니다. 잠시 끄기만 하려면 **사용 안 함**을 선택하고 Visual Studio를 다시 여세요. 버전별 실제 항목 이름과 데이터 보존 안내는 [사용자 안내서](docs/USER_GUIDE.md#visual-studio-ai-assistant-확장-제거)를 참조하세요.

확장을 처음 열면 협업과 소스 주석 규칙을 담은 숨김 필수 지침을 생성합니다. 직접 작성한 공통 지침은 별도로 보존하며, 두 지침을 AI 요청에 함께 전달합니다.

## v0.12.0 공개 미리보기 UI

이번 미리보기에는 완료 알림, 최근 대화의 단계별 불러오기, 설정 페이지 탐색, Markdown 도구 그룹, 앱 전용 Kakao·Dracula 색상 팔레트와 독립 Visual Studio 테마 패키지를 포함합니다. 공통 팝업은 OS의 녹색 제목 표시줄 대신 확장 화면과 어울리는 제목 표시줄을 사용하며, 창 이동과 최소화·최대화·닫기를 제공합니다. 아래 그림은 별도 WPF 렌더 이미지이며 실제 Visual Studio IDE 캡처가 아닙니다. 고객 오프라인 PC의 설치 결과는 별도로 확인해야 합니다.

![candidate5 DLL의 테마형 팝업 제목 표시줄과 설정 화면. 별도 WPF 캡처이며 실제 IDE 화면이 아니다.](docs/assets/ui-guide-v0.12.0/candidate5/settings-vs.png)

- 성공으로 끝난 작업에만 Windows 작업 표시줄 알림을 한 번 표시합니다. 알림은 작업 표시줄 balloon이며 Windows의 WinRT toast나 클릭 후 IDE 복귀 동작은 아닙니다.
- 이전 대화는 최신 20개 메시지를 먼저 보이고, 위로 스크롤하면 이전 메시지 20개를 추가로 표시합니다. 저장 한도는 대화당 100개와 파일 4MB로 유지되며 이미 한도를 넘어 사라진 메시지는 복원하지 않습니다.
- 설정 화면을 계정·모델·규칙·화면 페이지로 나누고, Claude·Codex 탭을 키보드로 이동할 수 있습니다. Markdown 작성기는 도구를 여섯 기능 그룹으로 묶고 URL 강조와 설명 정렬을 개선합니다.
- Kakao·Dracula 색상은 AI Assistant 확장 화면에만 적용합니다. 별도 Visual Studio 테마 패키지는 IDE 테마 설정에서 사용자가 선택하며, 설치만으로 현재 Visual Studio 설정을 바꾸지 않습니다.

[v0.12.0 사용 안내와 그림](docs/USER_GUIDE.md#v0120-화면-변경-미리보기) · [PDF 미리보기](output/pdf/VisualStudio_AI_Assistant_설치_사용설명서_v0.12.0_미리보기.pdf)


## 이런 일을 함께하세요

| 하고 싶은 일 | 사용하는 기능 |
|---|---|
| 오류를 설명하고 수정하기 | 소스 또는 일부 코드를 선택하고 오류 화면·참고 문서를 첨부 |
| 변경을 확인한 뒤 적용하기 | 추가는 초록색, 삭제는 빨간색으로 비교하고 승인·적용·저장 |
| 수정과 테스트까지 맡기기 | 전체 액세스로 요청하고 실제 명령·진행·변경 결과 확인 |
| 긴 작업의 진행 상황 보기 | 빌드·테스트·파일·조회별 상태 카드, 마우스 오버 상세, 움직이는 ‘생각 중’ 표시, 토큰 툴팁 |
| 지난 대화에 이어 작업하기 | AI별 대화 목록, 자동 제목·이름, 대화별 상태와 파란 읽지 않음 표시 |
| 코드와 대화 화면 넓게 쓰기 | 목록 경계 드래그·접기·복원, 대화만 스크롤하고 입력란은 항상 표시 |
| 문서를 참고해 개발하기 | 이미지·텍스트·PDF·Word·PowerPoint·Excel 본문 첨부 |
| 답변과 작업 규칙을 읽고 작성하기 | 서식 있는 Markdown 답변·소스 링크, 작성기·미리보기·문서 저장 |
| AI마다 역할 나누기 | A~Z 담당자, 역할·팀, 최대 3명 동료의 순차 검토 |
| DB 구조에 맞게 SQL 작성하기 | 명시적으로 허용한 SQL Server 스키마를 참고 자료로 공유 |
| 형상관리와 배포 실행하기 | 권한·대상·실행 계획을 확인한 뒤 Git·SVN·TFVC·배포 도구 실행 |

## 설치하고 첫 요청 보내기

1. 사용 중인 Visual Studio 버전에 맞는 VSIX를 내려받습니다. Visual Studio를 닫고 설치한 뒤 다시 엽니다.
2. **도구 → AI Assistant**를 엽니다. 첫 사용 안내에서 그림과 함께 연결·요청·결과 확인 순서를 볼 수 있습니다.
3. 맨 위에서 **Claude** 또는 **Codex**를 선택합니다. 설치된 네이티브 CLI를 자동으로 찾습니다. 다른 경로를 사용하려면 오른쪽 위 부채꼴 메뉴나 **우클릭 → 설정**에서 `.exe`를 지정합니다.
4. 설정의 **계정 로그인 화면**에서 Claude·Codex를 골라 **공식 로그인 열기**로 인증한 뒤 **연결 확인**을 누릅니다. 아이디와 비밀번호는 제공자의 공식 창에 입력합니다.
5. 소스를 열거나 필요한 코드를 선택합니다. 입력란 왼쪽 **+** 또는 파일 드롭으로 참고 자료를 붙입니다.
6. 오른쪽 모델 메뉴에서 상세 모델·생각 강도·Fast를 선택하고, 왼쪽 권한 메뉴에서 작업 방식을 고릅니다.
7. 요청을 입력하고 **Enter** 또는 원형 **돋보기 버튼**으로 보냅니다. **Ctrl+Enter**는 줄바꿈입니다.

설치부터 시작까지의 [그림 설명서](HowToInstallCLI.md), [PDF 설명서](output/pdf/VisualStudio_AI_Assistant_설치_사용설명서.pdf), [설명서·설치 스크립트 ZIP](output/VisualStudio_AI_Assistant_고객전달용.zip)을 함께 제공합니다. ZIP에는 설명서와 설치 스크립트가 있으며 확장은 위의 버전별 VSIX로 따로 설치합니다. 확장의 **CLI 환경 설정 → PDF 설명서 · CLI 설치**에서도 동봉 PDF를 볼 수 있습니다. **CLI 설치** 버튼은 실행 내용을 확인하는 창을 거쳐 동봉 [InstallCLI.bat](InstallCLI.bat)를 일반 사용자 권한으로 실행합니다. 설치 후에는 본인 계정으로 CLI 로그인을 진행하세요.

### 화면으로 익히는 다섯 가지 사용 흐름

다음 GIF는 제품 화면에 합성 예제를 넣어 사용 순서를 보여 줍니다. 실제 계정 로그인·AI 요청·설치를 실행한 기록은 아닙니다. 같은 그림을 확장의 설정 상세 설명과 사용 안내에서도 볼 수 있습니다.

**① 계정 연결 — 안내를 펼치고, 공식 로그인 후 연결을 확인하세요.**

<img src="docs/assets/guide-media/01-connect.gif" alt="계정 연결 설명을 펼쳐 설치·공식 로그인·연결 확인·설정 저장 순서를 읽는 예제" width="600">

**② 요청 작성 — 원하는 변경과 유지할 조건을 적고 Markdown 미리보기로 확인하세요.**

<img src="docs/assets/guide-media/02-request.gif" alt="요청 입력란에 변경 조건을 작성하고 Markdown 편집과 미리보기를 전환하는 예제" width="600">

**③ 변경 검토 — 비교·원본·수정안을 살펴보고 적용 범위를 결정하세요.**

<img src="docs/assets/guide-media/03-review.gif" alt="합성 코드의 변경 비교와 원본·수정안 탭을 확인하는 예제" width="600">

**④ 사용 안내 — 카드를 선택해 자세히 읽고 우측 상단의 돌아가기로 이동하세요.**

<img src="docs/assets/guide-media/04-guide.gif" alt="그림 안내 카드와 Markdown 상세 설명, 고정된 우상단 돌아가기 버튼을 사용하는 예제" width="600">

**⑤ 설치 설명 — PDF 또는 Markdown 설명서를 읽고 설치 실행 전에 내용을 확인하세요.**

<img src="docs/assets/guide-media/05-install.gif" alt="CLI 설명서와 설치 확인 화면의 합성 예제, 실제 설치는 실행하지 않음" width="600">

GIF가 재생되지 않는 환경에서는 [연결](docs/assets/guide-media/01-connect.png) · [요청](docs/assets/guide-media/02-request.png) · [검토](docs/assets/guide-media/03-review.png) · [안내](docs/assets/guide-media/04-guide.png) · [설치](docs/assets/guide-media/05-install.png)의 정지 그림을 볼 수 있습니다.

별도의 설명용 도식으로 살펴보려면 [화면 안내 보완 가이드](docs/visual-guide-orig3/화면안내_보완가이드.md), [보완 PDF](output/pdf/화면안내_보완가이드.pdf), [정지·재생을 선택하는 갤러리](docs/visual-guide-orig3/gallery.html)를 이용하세요. 이 자료의 그림은 제품을 촬영한 화면과 구분한 설명용 도식입니다.

### 원하는 방식으로 코드를 바꾸세요

| 권한 메뉴 | AI 작업 방식 |
|---|---|
| **승인 요청** | 원본을 보존한 수정안을 만듭니다. 비교 → 승인 체크 → 편집기에 적용 → 저장 순서로 진행합니다. |
| **작업 도구 허용** | 수정안 검토 방식에 더해 확장의 DB·형상관리·배포 작업 창을 사용할 수 있습니다. 해당 작업은 정확한 실행 계획을 별도로 확인합니다. |
| **전체 액세스** | 네이티브 CLI가 직접 파일을 바꾸고 명령·테스트를 실행합니다. 실행 전에 열린 미저장 파일을 저장하세요. |

전체 액세스는 현재 Windows 사용자에게 허용된 컴퓨터 자원에 접근하는 모드입니다. 관리자 권한을 자동으로 얻는 기능은 아닙니다. 직접 실행한 파일·서버 변경은 수정안의 ‘직전 적용 되돌리기’로 일괄 복구되지 않으며, 중지 후에도 이미 실행한 결과는 남을 수 있습니다.

변경 카드에서 파일을 선택해 비교하고, 파일 링크로 해당 소스 위치에 이동하세요. 마우스를 올리면 변경 내용과 기록을 볼 수 있습니다. 전후 diff가 제공되지 않은 파일은 줄 수를 ‘미제공’으로 표시합니다.

## 작업 중에도 흐름을 확인하세요

사용자 요청은 오른쪽에 배경 없는 회색 글자, AI 답변은 왼쪽 본문으로 표시합니다. 로고 위의 메시지 바탕도 투명하게 유지합니다. 입력란을 남겨 두고 대화만 스크롤합니다. 공개된 명령 실행·파일 변경·도구 상태는 한 카드에서 갱신하며, 마우스를 올려 작업별 상세를 확인할 수 있습니다. ‘생각 중’에는 흰색 빛이 왼쪽에서 오른쪽으로 흐르고, 이전 내용을 읽는 동안에는 진행 표시를 상단으로 옮깁니다.

입력란의 **모델·생각 강도** 버튼에 마우스를 올리면 실제 보고된 입력·출력·캐시 토큰과 계정 한도를 확인할 수 있습니다. 같은 버튼을 눌러 상세 모델·생각 강도·응답 속도를 선택합니다. 전체 액세스의 Codex는 사용량 이벤트마다 갱신합니다. 일반 수정안의 Codex는 CLI가 완료 시 보고한 사용량을 표시합니다. 제공되지 않은 수치는 추정하지 않습니다.

연결할 때 CLI에서 모델 목록과 지원 강도·속도를 자동 조회합니다. CLI가 새 모델을 광고하면 다시 연결하여 선택할 수 있습니다. 요금제 이름으로 임의의 제한표를 만들지 않으며, 실제 이용 가능 여부는 연결된 계정과 서비스가 판정합니다.

## 나에게 맞는 작업 공간

대화 목록의 경계를 드래그해 폭을 조절하고 왼쪽 끝으로 접으세요. **☰**로 이전 폭을 복원합니다. 각 대화의 **작업 중·완료·실패·중지**와 파란 읽지 않음 표시가 기록에 남습니다. 닫은 대화도 Markdown을 보존하며 다시 불러올 수 있습니다.

목록 아래 **AI 지정하여 대화**에서 담당자를 고릅니다. 이름은 사용자명+AI명+A~Z 형식이고, 슬롯을 모두 사용하면 기존 A로 돌아갑니다. 역할·팀과 동료를 지정하면 실제 동료 검토를 받은 뒤 현재 AI가 결과를 종합합니다. 전체 액세스에서도 동료는 수정안을 검토하고 최종 담당자가 직접 실행합니다.

**v0.11.0부터** 같은 팀의 열린 Claude·Codex 대화를 서로 교차해 최대 3명까지 선택할 수 있습니다. Claude를 주 담당으로 두고 Codex를 동료로 두거나 그 반대로 구성할 수 있습니다. 전송을 누르면 각 동료의 저장된 CLI·모델·생각 강도·응답 속도를 사용해 순서대로 한 차례 검토하고, 현재 AI가 결과를 정리합니다. 요청·선택 소스·첨부가 선택한 각 AI 서비스에 공유되며 사용량과 시간이 추가됩니다. 재귀 자동 대화는 하지 않습니다. 실제 Claude CLI 계정 연결은 별도 로그인이 필요하며, 격리 검사를 실제 계정 성공으로 해석하지 않습니다. [협업 사용 안내](docs/USER_GUIDE.md#대화-ai-이름과-협업).

공통 규칙은 `%LOCALAPPDATA%\VS2017AIAssistant\workspace\instructions.md`, 대화는 같은 위치의 AI별 Markdown에 보관합니다. 공통 규칙은 모든 프로젝트에서 공유합니다. 첨부 원본은 대화에 저장하지 않으므로 다시 연 대화에서 필요하면 새로 첨부하세요.

AI 답변은 제목·강조·목록·표·코드·링크를 Markdown으로 표시합니다. 소스 링크를 선택하면 Visual Studio의 해당 텍스트 파일과 행으로 이동합니다. 요청 입력란의 **M↓** 또는 입력란·공통 규칙의 **우클릭 → Markdown 작성 · 미리보기**에서 작성기를 여세요. 원래 입력란을 바꾸려면 **입력란에 적용**을 누릅니다.

별도 문서는 **우클릭 → Markdown 문서 작성**으로 시작합니다. 줄 번호와 구문 색상, 검색, Undo·Redo, 편집·미리보기·분할 보기, 자동 줄바꿈과 확대·축소를 사용할 수 있습니다. **Ctrl+B/I/K**는 서식·링크, **Ctrl+Shift+C**는 체크 목록, **Ctrl+Enter**는 체크 전환, **Ctrl+F**는 검색입니다. 작성기의 단축키는 대화 입력란의 전송 단축키와 구분됩니다. 명시적으로 불러온 Markdown 문서는 같은 폴더의 로컬 이미지·GIF도 미리보기로 확인할 수 있으며, UTF-8로 저장합니다.

**저장/Ctrl+S**는 현재 문서에, **다른 이름으로 저장/Ctrl+Shift+S**는 새 경로에 저장합니다. 접이식 문서 목록은 넓은 창에서 왼쪽, 좁은 창에서 탭 위에 나타나며 안쪽에서 스크롤할 수 있습니다. 목록에서 문서를 열고 이름 변경·파일 위치를 확인할 수 있고, 삭제는 현재 열려 있는 문서와 저장하지 않은 내용까지 지우므로 확인 단계를 거칩니다. 참조형 링크와 제한된 정적 HTML을 미리보기에서 지원합니다. 일반 Markdown 표는 최대 16열, 정적 HTML 표는 최대 128행·16열까지 표시하며 한도를 넘긴 내용이 있으면 생략 안내를 표시합니다. 스크립트·스타일시트·iframe 실행이나 원격 자료 자동 로딩은 하지 않습니다. [Markdown 작성기 그림 사용 설명서 C7 검토판](docs/visual-guide-orig3/markdown-editor/revision-c7/Markdown%EC%9E%91%EC%84%B1%EA%B8%B0_%EC%82%AC%EC%9A%A9%EC%84%A4%EB%AA%85%EC%84%9C_C7.md) · [PDF](output/pdf/Markdown%EC%9E%91%EC%84%B1%EA%B8%B0_%EC%82%AC%EC%9A%A9%EC%84%A4%EB%AA%85%EC%84%9C_C7.pdf).

**설정 → 화면 테마**의 기본값은 **자동 · Visual Studio 우선**입니다. IDE 밖에서는 Windows 앱 모드를 따릅니다. 밝게·어둡게를 직접 고르고 설정을 저장할 수도 있습니다. 설정의 상세 설명은 필요할 때 펼치고, 작은 창에서는 대화 목록 뒤의 화면을 흐리게 구분해 읽을 수 있습니다.

## 도입 전에 확인하세요

AI 사용량과 비용은 각 계정에 따릅니다. PDF 등 문서 추출은 PC에 설치된 필터에 따라 달라집니다. 스캔 문서는 자동 OCR 대상이 아니므로 이미지로 첨부할 수 있습니다.

프로젝트 전후 비교는 작은 텍스트 소스의 변경을 보완합니다. 비교 한도와 제외 폴더가 있으며, 다른 프로세스의 동시 변경도 포함될 수 있습니다. [사용 안내](docs/USER_GUIDE.md)의 추적 범위를 확인하세요.

[최신 설치 호환성 검증 기록](MarkdownReport/CodexOrig_TASK-036.md)에서 버전별 IDE·격리 협업 검사와 실제 Claude 계정·운영 DB·TFVC·운영 배포의 미검증 범위를 구분합니다. 테스트 재현에는 최상위 [검사 실행기](VisualStudio_AIChat_ExtentionTest.exe?raw=true)를 사용하세요. 현재 실행기는 기본 검사 19개와 직접 선택 검사 2개를 표시합니다. 기본 검사에서 실제 IDE 실행과 실시간 Codex 대화는 시작하지 않으며, 각 항목의 통과 여부는 최신 후보 검증 보고서에서 확인하세요. 기본 검사는 유료 AI 호출과 운영 서버 접속을 자동 실행하지 않습니다.

[상세 사용법](docs/USER_GUIDE.md) · [테스트 환경](docs/TEST_ENVIRONMENT.md) · [실제 IDE 검사](docs/IDE_TESTS.md) · [빌드·라이선스 안내](docs/BUILD_AND_LICENSE.md) · [개발 안내](docs/DEVELOPMENT.md)
