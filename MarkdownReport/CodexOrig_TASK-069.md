# TASK-069 / CodexOrig / v0.14.0 Preview 완료 보고

Role.md의 2026-10-20 추가 요청 #1–19와 첨부 이미지를 검토하여 구현·감사 보완·Release 빌드·패키지 제작·실제 공개·다운로드 검증·승인된 구버전 정리를 완료했다. 역할은 CodexCode(설치기), CodexOrig4(UI), CodexOrig3(사용자 문서), CodexOrig2(독립 감사), 연구 담당으로 나눴으며 Root가 최종 통합과 배포를 수행했다.

- 시작: 2026-10-20 21:57:14 KST. 최종 기록 시각과 경과 시간은 Update_History.md의 TASK069 완료 항목에 기록한다.
- 최종 제품: C13 / final5, 버전 `v0.14.0-preview.1`.
- 제품 소스 커밋/태그 대상: `68c4be1ef6fd4a3a69bb4dce936276be741c75ea`.
- [공개 다운로드](https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tag/v0.14.0-preview.1), Release ID `418594108`. Preview로 게시했고 기존 안정판 latest는 유지했다.
- 소스 제외 전달 폴더: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\UserDistribution\v0.14.0`.

## 반영 내용

| 요청 | 결과와 주요 변경 위치 |
| --- | --- |
| #1–2 | 같은 팀 메시지 전달 상태와 읽기 전용 수신·명시적 승인 경계, 선택 모델/effort/Fast/권한의 CLI 인자 전달을 보완했다. `Chat`, `Providers`, `Services` 및 관련 task069 검사. |
| #3–5 | 작업 기록 클릭 표시, 변경된 UTF 텍스트의 제한된 자동 줄끝 정규화, 협업 잠금의 비차단·한정 재시도로 UI 대기를 줄였다. 기존 현장 멈춤의 직접 원인은 미확정으로 남겼다. |
| #6–8, #17 | Dracula 스크롤·메뉴/도구모음/탭 hover·중단점 보라색·오류 분홍색 및 Kakao 영역 대비를 보완했다. `scripts/Build-VisualStudioThemes.ps1`. 버전 자동 탐색과 테마 동시 설치, IDE 종료 대기·중지·중앙 양방향 게이지를 갖춘 오프라인 EXE를 제작했다. `installer/OfflineThemeInstaller/Program.cs`, `scripts/Install-AIAssistant.ps1`. |
| #9–10 | 숨김 필수 지침 1–8 자동 갱신과 사용자 편집 보존, 형상관리 기여자 2명 이상 확인 후 협업 디렉터리 사용, TFVC get/checkout/checkin 및 PATH 밖 tf.exe 탐색을 추가했다. 자동 커밋은 수행하지 않는다. |
| #11–12 | Markdown 비교 연구와 성능 개선안 100개를 검토하고 기존 Markdown/팔레트 인스턴스 재사용을 적용했다. |
| #13 | TXT 네 개의 이름·본문을 실제 요청에 보존하고 텍스트 아이콘을 추가했다. 송신 JSON 한도 16MiB와 응답 한도 2MiB를 구분했다. |
| #14–16, #19 | 유휴 시 전송만, 실행/대기 중 중지만 표시한다. 사용자 말풍선은 작성란과 같은 표면색, AI 답변은 더 진한 색으로 표시한다. 대화 이름/시간 머리글을 고정하고 본문에 반응형 12–20px 좌우 여백을 적용했다. `Views/AssistantControl.xaml` 및 분할 code-behind. |
| #18 | 중단 중 현재 파일·줄·함수·열린 소스 ±3줄·상위 스택 3개를 기존 입력 초안에 넣는 읽기 전용 DBG 도우미를 추가했다. 변수 평가·디버거 제어·자동 전송 없이 16KiB 한도를 적용했다. |

## 최종 검증

검사 수의 단위와 후보 범위를 유지한다. [Root 검토 완료 JSON](CodexOrig_TASK-069_FinalValidation.json)과 [독립 감사](CodexOrig2_TASK-069_Audit.md)에 원본 경로 및 SHA-256이 있다.

| 검사 | 결과 |
| --- | --- |
| C13 고정 입력 / Release 빌드 | 입력 147개 불변, Legacy 2017 / 2017 / 2019 / 2022 네 SKU 모두 오류 0·경고 0 |
| 최종 실행기 | 기본 27/27 suite 종료코드 0. 실행기 자체 smoke 55/55 |
| C13 실제 격리 IDE | 각 172/172, 합계 688/688. 실제 로드 DLL 해시 네 SKU 일치 |
| 설정 복구 | 실제 C13 DLL 105/105, 실제 Read/None 잠금·외부 변경·처음 파일 없음 포함 |
| Dracula 실제 편집기 소비 | 정식 C11에서 3호스트 각 146, 총 438/438. C13 테마 바이트 동일성을 별도 확인했으며 C13 재실행으로 표기하지 않음 |
| 설치 호환성 | 35/35 |
| final5 설치기 | 17/17, 공식 확장+테마 설치 6/6, TEMP 원본 및 보존 공식 로그 6/6 크기·해시 일치 |
| 중지 / 진행 게이지 | 대기 중 중지 exit 2·설치 0건, 중앙 대칭 게이지·진행 신호 1/6–6/6 확인 |
| 통신 차단 CLI | 최종 FullOffline ZIP을 네트워크 capability 0 AppContainer에서 6/6 검사, 호스트 네트워크 설정 변경 없음 |
| 공개 자산 | 14개 게시 후 인증 없이 재다운로드, 크기·SHA-256 14/14 일치 |
| 사용자 전달 폴더 | 69개 파일, 체크섬 68 payload, vendor CLI 46개. 프로젝트 소스/PDB/프로필/인증 정보/개발 증거 제외 |

실제 IDE의 Legacy·2017·2022 소유 프로세스는 정상 종료했다. VS2019는 검사 결과 보존 후 10초 종료 지연으로 PID·RootSuffix가 일치한 소유 프로세스만 강제 정리했다. 이 종료 지연의 원인은 미확정이며 정상 종료나 고객 PC의 닫기 검사로 표현하지 않는다. 테마 시험 헬퍼의 경고는 각각 1/1/8개로 제품 빌드 경고 0과 구분한다.

## 감사 보완

첫 C12 UX16 부분 저장 실패의 직접 원인은 미확정으로 원본을 보존했다. C13 `Chat/AssistantSettingsTransaction.cs`는 기존 JSON 복원과 최초 생성 JSON 삭제를 같은 보호 경계에 두고, 매 시도 현재 원문이 자신의 예상 원문과 같은지 확인한다. native 공유 오류 32/33/1175/1176/1224만 최대 6회, 명시적 대기 합계 125ms로 제한한다. 지속 잠금에서는 부분 저장이 남을 수 있음을 오류와 Release 노트에 명시하고 설정/공통 규칙 재확인을 안내한다. 외부 변경을 덮어쓰지 않는다. [실제 DLL 증거](CodexOrig_TASK-069_SettingsRollback_FinalDll.json).

공식 VSIXInstaller가 로그를 TEMP 아래 basename으로 생성하는 계약에 맞춰 로그 전달·검증·보존을 수정했다. 실제 존재·크기·SHA를 확인한 로그만 기록하며, 로그 없음/복사 실패는 설치 종료코드와 구분한다. PS5.1과 PS7에서 각각 23/23, 최종 공식 설치 로그 6건을 재확인했다. [로그 증거](CodexOrig_TASK-069_InstallerLogs_Final.json).

CodexOrig2의 최종 사후 감사는 C13/final5 로컬 Preview 검사·공개 자산 무결성·승인된 정리 모두 통과다. 감사자가 공개 자산을 추가 재다운로드한 것은 아니며 Root의 익명 다운로드 해시와 자산 목록을 독립 대조했다.

## 배포와 보존

[배포 기록](CodexOrig_TASK-069_Deployment.json)의 검증 시각은 `2026-10-21T01:04:01.841327+09:00`이다. EXE에는 AI VSIX 4개와 테마 VSIX 3개 및 설치 스크립트/체크섬을 동봉했다. 사용자 폴더에는 필요한 실행 파일·설치 스크립트·문서·라이선스만 포함하며 기존 GitHub 소스 공개 방식은 사용자의 선택대로 유지한다.

| 최종 파일 | SHA-256 |
| --- | --- |
| `Grokeen-AI-Theme-Offline-Installer-0.14.0.exe` | `42CE73ABE69760005E3C5B9C9977ADAFF1999A22085A31A7B73B9168D5100225` |
| `VisualStudio_AI_Assistant-0.14.0-Preview1-FullOffline-win-x64.zip` | `48EEA288CB88E23D7A19842BC451BDBAF64A537C7D63C80D26F2D3EAF81B81BF` |
| 44쪽 설치·사용 PDF | `124285DF5E7734917B5888E3C6D7868801234626D9CB7653C4E0142B1383FA35` |

새 버전 게시 및 공개 재다운로드 검증 후 사용자 승인 범위로 v0.12.1 GitHub 첨부 13개와 로컬 `releases/v0.12.1`의 23개 파일을 삭제했다. v0.14.0과 v0.13.0 자산/폴더, Release 항목·태그·Git 이력·개발 증거는 보존했다. [정리 완료 기록](CodexOrig_TASK-069_ReleaseCleanup.json)의 완료 시각은 `2026-10-21T01:05:56.9154493+09:00`이다. 배포 기록의 `PreviousUi121PreviewUnchanged=true`는 정리 전 게시 검증 스냅샷이며 현재 첨부 보존 상태를 뜻하지 않는다.

## 검증 한계와 역할 인계

사용자 지시대로 Claude 로그인이나 실제 AI 계정/응답 검사는 수행하지 않았다. 고객 VS2017 Professional 15.7.1, 별도 오프라인 PC, 실제 TFS 서버는 직접 검사하지 않았다. 설치 스크립트와 EXE 내장 파일은 검증했지만 설치기 UI의 실제 마우스 클릭은 검사하지 않았다. 테마는 실제 편집기 색/옵션/여백을 읽었으며 실제 오류·중단점 태그 생성, 물리 hover·픽셀 검사는 포함하지 않는다. 오프라인 환경은 설치·CLI 시작 검증 범위이며 AI 사용에는 연결이 필요하다.

완료 기준은 역할별 구현 종료가 아니라 빌드·컴파일 → 최신 Release 제작 → 실제 공개 → 공개 다운로드 해시 검증이다. 최종 결과와 이 기준을 다른 역할 채팅에 안내한다. CodexCode는 도구의 active-writer 오류로 직접 전달 수신을 주장하지 않고 WORK_STATUS.md의 공유 완료 기록을 통해 전달한다. 이번 제품 소스/자산은 동결하며 후속 변경은 별도 버전·검사·배포 계획으로 진행해야 한다.
