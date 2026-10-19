# TASK-039 — 오프라인 설치 사용 설명

완료 시각: 2026-10-19 11:13:25 KST

요청 범위에 맞춰 새 사용자 문서 `docs/OFFLINE_INSTALL.md`를 작성했다. 이 안내는 확장과 CLI를 오프라인으로 설치한 뒤, 네트워크가 허용될 때 본인 계정으로 로그인해 실제 AI 서비스를 확인하는 절차를 설명한다.

대조한 구현: `PrepareOfflineSetup.bat`, `scripts/Prepare-OfflineSetup.ps1`, `InstallOfflineCLI.bat`, `scripts/Install-OfflineCli.ps1`, `scripts/OfflineCli.Common.ps1`, `InstallAIAssistant.bat`, `scripts/Install-AIAssistant.ps1`. 설치 준비 PC에서 CLI 게시자 서명을 확인하며, Codex standalone 런타임의 보조 실행파일·리소스·metadata/license와 Claude CLI를 함께 구성한다. 프로필/로그인/토큰은 복사하지 않는다. 오프라인 설치는 manifest 크기·SHA256·x64 PE를 검사한 뒤 사용자 전용 경로에 복사하며 CLI 실행·네트워크 요청·PATH/설정 수정은 하지 않는다. 실제 배치 이름, 기본 설치/로그 경로, ZIP checksum 및 지원 OS/IDE를 소스와 대조했다.

## 검증 경계와 인계

현재 full-offline-candidate1은 문서를 확정하기 전 만들어져 ZIP 내 `OFFLINE_INSTALL.md` SHA256 `A06107CFF196EDB9AB6B1F3E608F12EE2734528AA0C2C732F6F8972696691C47`가 현재 원본 `D46B596AFCEF997C198BAC4AC15031608E975C75FF094A8FF1248E9FEF13B862`와 다르다. 후보 폴더·ZIP을 변경하지 않았으며, candidate1은 최종 사용자 배포에 사용하지 않는다. 최종 문서와 최신 스크립트로 FullOffline ZIP을 다시 생성하고 별도 테스트한 뒤 제공해야 한다. 이 후보 ZIP SHA256은 `0DADF4D88991BBA6C150E26F7A1153E17ACBF5AD5820909A635F38DD7892E991`다.

완전 네트워크 차단 고객 PC의 실제 설치·CLI 실행은 수행하지 않았다. CodexCode의 별도 검토는 VSIX checksum 30/30 및 개발 PC의 local preflight readiness를 확인했지만 고객 VS2017 Professional 15.7.1의 실제 설치/창 열기와 OS 조건은 아직 확인하지 않았다. 이 점을 매뉴얼에도 명시했다. 실제 계정 로그인·모델 성공·배포완료로 표현하지 않았다. 제품 코드/공유 빌드/GUI/실계정/릴리스는 변경하지 않았고 C17/기존 온라인 CLI 안내/PDF/ZIP을 수정하지 않았다.

## SHA-256

| 파일 | SHA-256 |
|---|---|
| `docs/OFFLINE_INSTALL.md` | `D46B596AFCEF997C198BAC4AC15031608E975C75FF094A8FF1248E9FEF13B862` |
| stale snapshot: candidate1 ZIP 내부 안내 | `A06107CFF196EDB9AB6B1F3E608F12EE2734528AA0C2C732F6F8972696691C47` |
| 보존 확인: candidate1 ZIP | `0DADF4D88991BBA6C150E26F7A1153E17ACBF5AD5820909A635F38DD7892E991` |
