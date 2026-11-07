# C# 주석 코드 제안 임시 중지 문서 후보 · 2026-11-08

## 기준과 상태

- 문서 브랜치: `codex/comment-code-pause-docs-20261108`. 기준 문서는 소스 후보 `267a81ea849c1a9bb9858176dd3d65501fded460`의 사용자 변경을 반영합니다. 이 문서 작업에서는 소스, BestCare, 고정 태그, 공개 Release 첨부를 수정하지 않았습니다.
- 현재 공개 설치본은 `v0.15.0-preview.1`입니다. 해당 설치본은 C# 주석 코드 제안 메뉴를 자동 차단하지 않으므로 사용하지 말고, 기존 확인 창이 나오면 **아니요/취소**를 선택하라고 사용자 문서에 알립니다.
- 차기 `v0.15.1-preview.1`은 아직 공개되지 않은 소스·문서 후보입니다. 소스 후보의 전송 전 중지 안내 문구를 문서와 대조했지만, 새 패키지 빌드나 실제 Visual Studio 동작은 확인하지 않았습니다. 공개 패키지 동작으로 단정하지 않습니다.
- 임시 중지는 문서 대상 확인 보완을 위한 예방 조치입니다. 실제 편집기 문서 불일치, 원치 않는 변경 또는 정보 유출이 확인됐다고 표현하지 않습니다. 일반 대화는 이 중지 범위 밖이지만 실제 IDE·계정 동작은 검증되지 않았습니다.
- 새 PDF는 공개 `v0.15.0-preview.1`의 55쪽 사용자 안내 PDF 앞에 공지 1쪽을 붙이는 별도 검토 후보입니다. 공개 PDF나 고정 태그 파일을 대체하지 않습니다.

## 공개 안내 상태와 고정 태그

- 익명 GET으로 확인한 Release: [v0.15.0-preview.1](https://api.github.com/repos/Grokeen/VisualStudio2017_Codex_Claude_Extension/releases/tags/v0.15.0-preview.1), Release ID `437695130`.
- 이전 확인에는 오래된 C# 사용 절차가 남아 있었으나, 이후 익명 GET에서 Release 본문의 C# bullet은 이미 임시 중지 상태 및 공개 설치본 자동 차단 부재를 안내하도록 바뀌어 있었습니다. 현재 Release 본문 수정은 이 문서 작업이 수행한 것이 아닙니다.
- 최신 Release 본문은 고정 태그의 `docs/OFFLINE_INSTALL.md`, `docs/USER_GUIDE.md`를 계속 링크하며 해당 태그의 문서와 55쪽 PDF에는 이전 절차가 남아 있습니다. 이 브랜치의 README·안내서·후보 PDF에서 그 오래된 단계를 따르지 말라고 표시했습니다. 고정 태그와 Release 첨부는 변경하지 않았습니다.
- v0.15.1 문서 후보와 PDF는 미공개 상태로 표시합니다. 사용자에게 공개 0.15.1 설치 파일이 있다고 안내하지 않습니다.

## 오프라인 설치 안내의 안전 보완

- 공개 `v0.15.0` FullOffline 묶음의 `VerifyOfflineSetup.bat`은 이름과 달리 설치 작업을 수행합니다. 소스의 동작을 확인했으며 이 배치에는 `Test-OfflineSetupOnTarget.ps1 -Install`이 연결됩니다.
- 소스 담당자는 차기 `v0.15.1`에서 `VerifyOfflineSetup.bat`을 `scripts/Verify-OfflineFiles.ps1` 기반 읽기 전용 파일 목록·해시 검사(설치/CLI 호출 없음)로 바꾸는 작업이 아직 진행 중이라고 알렸습니다. 해당 변경은 이 기록 시점에 미고정이고 본 문서 브랜치에서는 소스나 동작을 검증하지 않았습니다. 문서와 PDF는 그 계획을 완료된 사실로 쓰지 않고, 공개 0.15.0 배치의 확정된 동작과 분리해 표시합니다.
- 설치 흐름은 사전 검사, 오프라인 대상 여부에 대한 사용자 확인, 실행 중 Visual Studio 종료 요구, 설치 대상 선택(필요한 경우), CLI 설치 및 버전 확인, 공식 VSIX 설치기 실행을 포함합니다. 로그인을 하거나 AI 모델 요청을 보내지는 않습니다. 오프라인 여부 프롬프트는 사용자 진술이며 네트워크 차단을 증명하거나 설정하지 않습니다.
- 따라서 `VerifyOfflineSetup.bat`은 checksum 전용이나 읽기 전용 검증 수단으로 안내하지 않습니다. ZIP 자체의 해시 확인은 PowerShell `Get-FileHash` 결과를 같은 Release의 `.zip.sha256` sidecar와 비교하도록 안내합니다. 이 비교는 ZIP 바이트만 확인하며 내부 파일 목록 전체를 검증하지 않습니다.
- 설치 절차에서는 보존할 사용자 데이터를 먼저 백업하고 `%LOCALAPPDATA%\VS2017AIAssistant`를 삭제하지 않도록 주의시킵니다. 설치가 목적일 때에만 배치를 실행하고, Visual Studio를 닫은 뒤 대상 선택과 설치 안내를 따르도록 명시합니다. `-Install` 없는 직접 진단은 설치를 수행하지 않지만 Release 전체의 검증으로 과장하지 않습니다.
- 익명 Release 확인에서 FullOffline ZIP과 동일 이름의 `.zip.sha256` sidecar가 게시된 것을 확인했습니다. ZIP SHA-256은 `a221e0753ddce6daf88b14ecaab4d3e24dda9212a47a51a1a3e330722f95e2a8`입니다. 이 문서 작업에서는 오프라인 배치나 설치를 실행하지 않았습니다.

## PDF 후보와 검증

- 후보 위치: `output/pdf/VisualStudio_AI_Assistant_설치_사용설명서_v0.15.1-preview.1_comment-code-pause-notice-candidate.pdf`.
- 첫 페이지는 현재 공개 v0.15.0이 자동 차단하지 않는다는 점, 미공개 v0.15.1 소스 후보의 전송 전 중지 코드, 실제 IDE 검증 미완료, 중지 범위와 오래된 뒤쪽 절차보다 공지를 우선할 점을 구분해 안내합니다.
- 기존 v0.15.0의 55쪽 콘텐츠 스트림은 수정하지 않고 첫 페이지를 앞에 붙입니다. 후보는 56쪽입니다. PDF 추출에서 공지의 고유 제목·본문 anchor가 각각 한 번만 나타나는지, 뒤쪽 55페이지 콘텐츠 스트림이 보존됐는지 확인하고 첫 페이지를 Poppler로 렌더링해 시각 점검합니다.
- 앞서 만든 D266 PDF는 공지 텍스트가 추출상 중복되어 폐기했습니다. CodexOrig6의 첫 독립 검토에서 이를 발견했고 생성기의 측정 단계에서 그리기를 끄도록 수정했습니다. 실제 스크린리더 동작 및 전체 PDF 리소스/주석 동등성은 확인하지 않았습니다.

## SHA-256 · 문서 후보

PDF는 56쪽이며 공개 v0.15.0 55쪽의 콘텐츠 스트림 55/55를 보존합니다. 생성기를 두 번 실행해 같은 바이트 결과를 얻었습니다. 각 첫 페이지 문구 anchor 9개는 텍스트 추출에서 정확히 한 번 나타납니다. 첫 페이지는 Poppler 140dpi 렌더링으로 점검했고 텍스트/패널 겹침과 잘림은 보이지 않았습니다.

- PDF `output/pdf/VisualStudio_AI_Assistant_설치_사용설명서_v0.15.1-preview.1_comment-code-pause-notice-candidate.pdf`: 3,327,938 bytes · SHA-256 `2fc07dcc78ad358b90c9747e1119b0ddf9077931c5d56e8eeb00fdff874b54fc`
- `README.md`: `dde80fa80ce0dd258d9b6d9561502ba4f65449c2fb25592a33792876e526b0b8`
- `docs/USER_GUIDE.md`: `018d6eeb30f5bc8e81c8300cd4a833efc6e0d50427d87d9031643108de421b53`
- `docs/OFFLINE_INSTALL.md`: `6e07d438717fb45f45d8bf231bd2031bd6bec74f939ec06dd91d2e19774cdf22`
- `MarkdownReport/Build_CommentCodePauseNotice.py`: `71ddf77a97dcd02a9c7392ac3a2168251aee9258cbbb0635e9b5f6eaaaff27ec`
- 이 보고서 자체의 최종 해시와 문서 commit/tree는 최종 인계에 기록합니다.

## 확인 경계 및 후속 검토

- 문서 후보 소스의 중지 안내 문구를 정적으로 대조했습니다. 소스 빌드·테스트, Visual Studio GUI, 실제 IDE 동작, 실제 AI 전송, 고객 PC 설치 또는 데이터 불일치/유출 재현은 수행하지 않았습니다.
- Release 본문은 읽기 전용 익명 GET으로 확인했으며 공개 Release·태그·첨부는 변경하지 않았습니다. 0.15.1 패키지는 아직 없습니다.
- 이 보고서와 문서 후보를 Orig2·Orig6에 최종 독립 검토 요청하고, CodexOrig8 및 소스 담당 CodexOrig에 0.15.1 전환 내용과 오프라인 설치 배치 주의를 전달합니다. 검토 응답을 받기 전에는 이를 공개 승인으로 표현하지 않습니다.
