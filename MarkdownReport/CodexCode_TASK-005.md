# TASK-005 — 구성된 환경의 실제 VS2017 통합 테스트

- 작업자: CodexCode
- 시작: 2026-10-18 01:49:18 KST
- 종료: 2026-10-18 02:04:14 KST
- 소요: 14분 56초
- 결과: **v0.2.0 실제 VS2017/Codex 소스 수정·승인·적용·저장 PASS.** 제품 소스를 수정할 결함은 이번 검증에서 발견되지 않았다.

## 검증 대상

VS2017 Community 15.9.83, MSBuild 15.9.30.58560, .NET Framework 4.6.2, C# 7.0을 사용했다. VS2019/2022는 환경이 설치되어 있으나 현재 제품 매니페스트가 해당 IDE를 지원하지 않으므로 제품 설치 성공으로 보고하지 않는다.

고정 인계 패키지 `artifacts/source-editing/handoff/VS2017.AIAssistant-0.2.0.vsix`를 그대로 사용했다. SHA-256은 `0B7FD44509A6CA02344E645924378EB544966A0831B942A9F7D4B6CDAB872D94`다. 테스트 패키지는 제품 DLL을 동봉하지 않으며 실제 설치된 제품을 사용한다. 테스트 참조 DLL과 제품 VSIX 내부 DLL의 해시 일치도 런처에서 검사한다.

## 결과

| 검사 | 결과 | 범위 |
|---|---|---|
| 편집/CLI/WPF 회귀 | 55/55 PASS | 스냅샷·승인·충돌·복구, subprocess·UTF-8·취소·시간 초과·하위 프로세스 |
| 패키지·WPF | 29/29 PASS | VSIX·pkgdef·메뉴·BAML·승인 상태, 밝은/어두운·좁은 폭 렌더링 |
| 호환성 회귀 | 44/44 PASS | 매니페스트·프로젝트 호환성 검사 도구 |
| 실제 VS2017 + 실제 Codex | 25/25 PASS | 설치된 제품 메뉴·도구 창·실제 EnvDTE·승인·적용·저장·Undo |
| 새 인스턴스 재현 실행 | 22/22 PASS | 최종 런처 명령을 처음부터 실행, 실제 AI 대신 fixture 사용 |
| 테스트 보조 VSIX Release 빌드 | PASS | VS2017 MSBuild 15, 경고·오류 0건 |

서로 다른 검사 기준으로 총 **153/153 PASS**이며, 22개 재현 실행은 중복 검사이므로 합계에 더하지 않았다. 제품 Release 패키지는 TASK-004에서 빌드한 고정 파일을 설치했으며 이번 작업에서 덮어쓰지 않았다.

실제 IDE에서 미저장 한글 주석을 포함한 소스를 읽고, 요청 후 다른 파일로 전환해도 요청한 원래 파일만 수정하는 것을 확인했다. 승인 전에는 버퍼·디스크가 유지되고 승인 후에는 버퍼만 바뀌었다. 별도 저장은 UTF-8 BOM과 CRLF를 유지했다. IDE 실행 취소 한 번으로 적용 전 버퍼를 복구했고, 전용 되돌리기에서도 이전 미저장 주석을 보존했다. 수정안 생성 후 사용자가 내용을 바꾼 경우 적용을 거부했다.

실제 로그인된 Codex CLI `0.162.0-alpha.17.2`를 도구 창에서 호출하여 `GetValue`의 `return 1;` → `return 2;` 변경을 생성·검토·승인·적용·저장했다. 사용자 프로젝트는 전송하지 않았고 테스트 스크립트가 만든 합성 코드만 사용했다. Claude는 실제 IDE에서 CLI fixture의 구조화 응답 파싱·승인·적용까지 확인했다.

## 실행 방법과 산출물

새 소스는 `tests/ide/IdeIntegration.csproj`, `IdeTestPackage.cs`, `IdeTests.cs`, `Test-IdeIntegration.ps1`, `source.extension.vsixmanifest`다. 테스트 보조 패키지는 별도 인스턴스에서만 실행하고 종료하며 제품 VSIX에 포함하지 않는다. 메서드·클래스 설명, 작성자 및 날짜 주석을 추가했다.

```powershell
# 실제 IDE 22개 검사
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\ide\Test-IdeIntegration.ps1

# 실제 Codex를 포함한 25개 검사
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\ide\Test-IdeIntegration.ps1 `
    -LiveCodexPath 'C:\path\to\codex.exe'
```

- 25개 실제 IDE/Codex 결과: `MarkdownReport/CodexCode_TASK-005_IDE.json`
- 새 인스턴스 22개 재현 결과: `MarkdownReport/CodexCode_TASK-005_Repro.json`
- 실제 Codex 실행 폴더: `artifacts/ide/run-61f5670fd3e6488c8e9a355c88d0ed2c/`
- 최종 런처 재현 폴더: `artifacts/ide/run-88649b677370461992546b9e3f3aa5fd/`
- 편집 회귀: `artifacts/source-editing/run-16a08ef2d8364e508c6409846d66c769/results.log`
- 패키지·렌더링: `artifacts/ide/foundation/`
- 테스트 사례 25개 및 설치/구성 절차: `docs/IDE_TESTS.md`
- README, `docs/DEVELOPMENT.md`, `docs/SOURCE_EDITING.md`, Project_Plan 및 협업·이력·OWERORDER에 검증 상태 반영

처음 실행한 새 프로필은 Visual Studio 시작 화면에서 대기했고, 설치된 확장의 pkgdef 캐시가 아직 반영되지 않아 보조 패키지가 자동 실행되지 않았다. 테스트 suffix에 공식 `/UpdateConfiguration`을 적용하고 `/ResetSettings General`로 프로필을 준비한 후 해결했다. 해당 절차를 최종 런처에 반영하고 새 suffix에서 재현 실행 22/22 PASS를 확인했다. Microsoft 로그인을 자동 수행하거나 라이선스 설정을 바꾸지 않았다.

Windows Computer Use로 실제 VS2017 도구 창이 표시되는 화면을 관찰했다. 기능 검증은 IDE 내부 SDK/EnvDTE와 WPF 버튼 이벤트로 수행했다. 마우스 클릭·키보드 조작을 통한 모든 사용성 시나리오까지 검증한 것으로 보고하지 않는다.

## 남은 범위와 협업

실제 Claude CLI/계정 호출, VS2019/2022용 패키지, IDE의 외부 파일 변경 알림·읽기 전용/닫기/이름 변경·저장 취소, 도킹·테마 전환·반복 개폐·재시작·제거는 추가 검증 대상이다. 관련 독립 회귀 통과와 실제 IDE 검증을 구분했다. 단일 파일 승인 기반 편집 범위이며 다중 파일·무승인 자동 적용은 포함하지 않는다.

VS 초기 구성 로그에는 Community 설치 폴더의 `detection_keys_reg.pkgdef`, XAML UI 리소스 및 일부 선택 구성요소의 오류가 기록되었다. 이번 제품 25개 검사는 통과했으나 전체 VS 설치의 모든 구성요소가 정상이라는 뜻은 아니다. 원본 로그를 보존했고 별도 설치 파일은 변경하지 않았다.

검증 진행 중에는 CodexOrig의 TASK-006(빌드 배치 및 라이선스 안내)과 겹쳐 GitHub 배포를 보류했다. 이후 TASK-006 완료 기록을 재확인하고, 후속 TASK-007에서 Role.md 6번에 따라 검증 소스와 고정 VSIX를 GitHub 저장소에 반영했다. 담당 파일과 기록을 보존했다. [배포 검증 보고](CodexCode_TASK-007.md)를 참고한다.
