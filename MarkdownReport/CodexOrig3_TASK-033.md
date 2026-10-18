# CodexOrig3 TASK-033 — Visual Studio AI Assistant 확장 제거 안내

완료 시각: 2026-10-19 03:48:58 KST

Role.md의 2026-10-19 03:42 추가 요청 중 설치된 확장 제거 안내를 README와 사용자 안내서에 반영했다. 사용자가 Visual Studio 전체 제거와 혼동하지 않도록 VSIX 확장만 비활성화/제거하는 메뉴 경로와 예약된 제거를 완료하는 절차를 명시했다.

- VS2017 15.9+: **도구 → 확장 및 업데이트 → 설치됨**, 설치 이름 ``VS2017 AI Assistant``
- VS2019 16.11: **확장 → 확장 관리 → 설치됨**, 설치 이름 ``Visual Studio AI Assistant 2019``; VS2022 17.x는 같은 메뉴 경로이며 설치 이름 ``Visual Studio AI Assistant 2022``.
- **사용 안 함**은 설치 상태를 유지하며 재시작 뒤 로드를 중지한다. **제거**를 고르면 모든 Visual Studio 창을 닫아 적용한다.
- 여러 IDE에 설치된 경우 각 IDE에서 별도로 제거한다. 재설치는 설치 안내에 연결했다.
- 기본 설정·대화 폴더는 `%LOCALAPPDATA%\VS2017AIAssistant`이므로 보존 안내를 추가했다. Visual Studio 확장 관리자에서 제거만 했으며, 이 경로나 CLI, 계정·대화 데이터를 삭제하지 않았다.

## 공식 근거

- [Microsoft Learn: Find, install, and manage extensions for Visual Studio](https://learn.microsoft.com/en-us/visualstudio/ide/finding-and-using-visual-studio-extensions?view=visualstudio) — 확장 관리자를 열고 Installed에서 Disable/Uninstall, 재시작으로 비활성 확장을 적용하는 안내.
- [Microsoft Learn: Visual Studio 2017 release notes — Modify Extensions in Bulk](https://learn.microsoft.com/en-us/visualstudio/releases/2017/vs2017-relnotes-v15.0#modify-extensions-in-bulk) — VS2017의 예약된 설치·업데이트·제거 작업과 모든 IDE 창을 닫은 뒤 적용되는 흐름.

Role.md의 다른 요청인 AI 간 협업·대화는 현재 README와 USER_GUIDE에 담당자 지정·동료 AI 순차 검토·결과 전달 안내가 이미 있다. 현재 소유자가 구현 검토 중이므로 신규 화면 캡처나 기능 주장 확대는 하지 않았다.

## 범위와 검증

README 및 USER_GUIDE의 설치·사용 안내만 수정했다. 세 VSIX manifest의 DisplayName을 확인해 버전별 설치 목록 이름을 표에 넣었다. 제품 소스·빌드·GUI와 Git 기록·브랜치·커밋 변경은 하지 않았다. 기존 C7 PDF·ZIP은 재생성하지 않고 해시로 보존을 확인했다. Microsoft 공식 안내와 메뉴 차이를 대조했고 `git diff --check -- README.md docs/USER_GUIDE.md`를 실행했다(줄바꿈 형식 변환 알림만 있고 공백 오류 없음).

## SHA-256

| 파일 | SHA-256 |
|---|---|
| `README.md` | `F6C4FEF85E9C2C03BC0125DADBE78ABED013A786359C087A91B09EF64B18AB90` |
| `docs/USER_GUIDE.md` | `AF37C3A611E86D26FC8061484AEA478AFC2221998906101E64A0FADC19EBC84C` |
| 보존 확인: C7 PDF | `EF6C8A3B5BE274F6FDAB77FBA6E07221F9E33B13E85B574380625EB0E3AF5453` |
| 보존 확인: C7 사용자 설명서 ZIP | `019E8BF35FB07A5E36D48EFC2E162EC9C35A0C7DDB1CC5747BA6B6B7DC542F64` |



