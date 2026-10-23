# CodexOrig3 TASK-072 최종 사용자 안내 기록

작성일: 2026-10-23

작업 범위: README.md, docs/USER_GUIDE.md, docs/OFFLINE_INSTALL.md, 합성 설명 그림, v0.14.2-preview.1 PDF 초안. 제품 소스, themes 폴더, Release 폴더, embedded PDF resource는 수정하지 않았습니다.

## 문서 반영

- 최신 안내·예상 다운로드 자산을 v0.14.2-preview.1로 갱신했습니다. Preview 다운로드 URL은 Release 게시 후 활성화된다고 표시했습니다.
- 직전 v0.14.1-preview.1을 보존 대상으로 두고, 그보다 이전의 오래된 Release 첨부 정리는 0.14.2 게시와 익명 다운로드 검증 뒤 진행하도록 안내했습니다. v0.14.0의 당시 44쪽 PDF는 저장소의 `output/pdf` 보존 파일로 연결했습니다.
- 현재 오프라인 설치기 파일명은 `Grokeen-AI-Theme-Offline-Installer-0.14.2.exe`, 확장만 설치하는 배치는 `InstallAIAssistant.bat`으로 구분했습니다. SKU별 VSIX 및 FullOffline ZIP도 0.14.2 이름으로 갱신했습니다.
- 독립 테마 VSIX에 작은 MEF DLL이 들어가며 Visual Studio Editor SDK DLL/PDB는 포함하지 않는다고 설명했습니다. IDE 테마 확인에는 CLI 설치·로그인·AI 서비스 연결이 필요하지 않습니다.
- Dracula 추가/삭제 diff 줄 면 및 단어색, overview 변경 표식과 viewport 윤곽, C#·XML 주석 색상을 반영했습니다. 오류 강조는 분홍 배경 채움 없이 분홍 1px 테두리 `#FFFF79C6`만 사용하며 native squiggle을 유지한다고 수정했습니다.
- VS2019/2022 diff view별 overview 로컬 활성화와 테마 변경/view 종료 시 local/inherited 옵션 복원을 설명했습니다.
- 당시 C4 검증 기록은 다음처럼 보존합니다. VS2017 270/270 통과, VS2019 270/270 통과, VS2022는 조사 중이었습니다. 후속 C4 IDE 실행에서 VS2022는 191 pass/21 fail로 확인됐고, 버퍼 전용 Roslyn 소스에 block-comment 별도 분류가 없어 색상 구분이 되지 않는 문제가 발견되어 제품 수정이 진행 중입니다. 상세 버전별 실행 내역과 한계는 별도 [TASK-072 통합 검증 보고서](CodexOrig2_TASK-072_Audit.md)에 기록합니다.
- 최신 후보는 변경 중이므로 사용자 문서와 PDF에서 C4/270/실행 중 같은 후보별 일시 통계를 제거하고 통합 검증 보고서 링크로 대체했습니다. 이 편집은 전체 검증 완료를 주장하지 않으며, CodexOrig3 작업 기록에는 C4의 과거 결과를 남깁니다.
- 고객 VS2017 Professional 15.7.1·별도 오프라인 PC·실제 AI·TFS/TFVC·물리 포인터 흐름은 미검증으로 유지했습니다. 기존 v0.14.0/0.14.1 스크린샷과 기록 섹션은 변경하지 않았습니다.
- 최종 문구 정정: 합성 그림의 제목·설명에서 후보, 색 적용 미검증, 2017/2019/2022 실검증 중 같은 임시 상태 문구를 제거하고 “합성 설명 그림 · 실제 Visual Studio IDE 캡처가 아닙니다.”로 정리했습니다. 그림은 기능 통과를 주장하지 않습니다.
- 후속 이미지 회귀 수정: diff 예시에 추가(밝은 초록)와 삭제(밝은 분홍)를 다시 나누고, overview의 초록·분홍 변경 위치 표식과 보라 viewport 윤곽이 서로 다른 표시임을 그림과 설명에 함께 복원했습니다. 오류 위치는 기존처럼 배경 채움 없는 분홍 테두리만 사용합니다.
- 최신 설치 순서는 오프라인 EXE 설치 완료 → 같은 IDE 재실행 → **도구 → 옵션 → 환경 → 일반 → 색 테마**에서 정확한 이름 **Grokeen Dracula Inspired** 선택으로 통일했습니다. AI Assistant 설정의 테마 설치·적용 버튼은 IDE 테마 VSIX가 없는 수동 AI-VSIX 설치자를 위한 별도 선택 경로로만 안내합니다.
- 사용자 문서와 PDF 46쪽은 C4 검사 수를 노출하지 않고 통합 검증 보고서로 연결합니다. 이전 C4 결과는 이 내부 작업 기록에 역사적 결과로 남깁니다.

## 산출물 및 검사

- 합성 그림: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\docs\assets\ui-guide-v0.14.2-preview.1\dracula-editor-colors.png`; 107344 bytes, SHA-256 `fe1d3963e2adb0ce9d687fd5b10e8bf70fc5c93425c3091f480aac2458c8dec3`. 추가/삭제 색상, 두 overview 변경 표식, 별도 viewport 윤곽을 보여 주며, 오류 패널은 배경 채움 없이 분홍 테두리와 기존 squiggle만 그립니다.
- PDF: `C:\dydgyqud\VisualStudio2017_Codex_Claude_Extension\output\pdf\VisualStudio_AI_Assistant_설치_사용설명서_v0.14.2-preview.1_미리보기.pdf`; 2427105 bytes, SHA-256 `155df0b5dd2c2c4ab958fe09958853c73a7c8ee1dbed0ab7022e21e18ff5b0de`; 46쪽. v0.14.1의 기존 45쪽 content streams는 모두 동일합니다. 46쪽은 EXE 설치 후 재실행과 정확한 색 테마 선택을 안내합니다. 최종 페이지를 PNG로 렌더해 시각 확인했습니다.
- README, USER_GUIDE, OFFLINE_INSTALL, 작업 기록에 `git diff --check`를 실행했습니다. PDF page 46과 이미지 파일 경로가 존재하며, page 46은 통합 검증 보고서로 링크하고 고객 환경 등 미검증 범위를 유지합니다.
- 배포 전이라 Release 다운로드 URL은 예상 경로이며, remote asset 존재·실제 게시·익명 다운로드 hash는 이번 문서 작업에서 주장하지 않습니다. Release/Git/embedded PDF는 수정하지 않았습니다.
