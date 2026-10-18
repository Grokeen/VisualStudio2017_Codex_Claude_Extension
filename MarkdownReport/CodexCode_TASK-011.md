# TASK-011 — 설정·첫 사용 안내의 사용자 문서 및 독립 검증

- 작업자: CodexCode
- 시작: 2026-10-18 11:28:11 KST
- 상태: 사용자 문서·최종 고정 DLL 독립 검증 완료. 최종 GitHub 배포는 TASK-010 담당에게 인계.
- 종료: 2026-10-18 11:45:52 KST
- 소요: 17분 41초

## 요구사항과 협업

Role.md 추가 8~11번과 image-1.png를 확인했다. 요구는 우클릭 설정 메뉴·화면 중앙 설정 창, 일관된 UI와 버튼별 아이콘, 화면만 보고 이해할 수 있는 사용 흐름, 처음 사용 시 단계별 안내와 다시 보지 않기다. 참고 이미지는 번호·상황 그림·행동 문구가 반복되는 네 단계 구조다.

동시에 제품 작업을 시작한 CodexOrig TASK-010을 WORK_STATUS.md에서 확인했다. 중복 편집을 피하기 위해 제품 코드·기존 테스트·버전·배포는 TASK-010 담당으로 유지하고, CodexCode는 README.md·docs/USER_GUIDE.md·화면 예제·tests/review의 독립 검증과 자기 보고를 맡았다. 독립 검토 하위 작업자는 제품과 기존 테스트를 변경하지 않는다.

## 문서와 검토 범위

README와 사용 안내에서 설정 진입 경로를 버튼·우클릭 메뉴로 안내하고, 첫 사용 가이드의 연결 → 요청 → 비교·승인 → 저장 흐름과 안내 숨김·재열기를 설명한다. 기존 설치·계정 인증·첨부·누적 대화·충돌 검사·적용/저장 구분을 유지한다. 최종 동작·버튼명·다운로드·화면은 고정 빌드에 맞춰 확인한다.

독립 검토는 TextBox의 복사·붙여넣기 유지, 가이드 표시 설정과 기존 설정의 상호 보존, 이전 버전 설정 호환, 수동 안내 재열기, 밝음/어두움 화면과 작은 창의 접근성에 집중한다. 사용자 설정과 프로젝트는 검사에 사용하지 않는다. 새 그림은 제품 WPF에 합성 예제를 표시한 화면임을 명시한다.

기존 v0.2.0·v0.3.0 릴리스와 사용자 Role.md·참고 이미지는 변경하지 않는다. TASK-011 완료 전에는 GitHub에 배포하지 않도록 협업 파일에 기록했다.

## 독립 검토 결과

초기 코드에서 부모 컨트롤에 Loaded 핸들러를 연결해 동적 대화 TextBox의 메뉴를 지정하려는 경로를 발견했다. 실제 설치된 WPF에서 Loaded는 Direct 이벤트이고 ContextMenu는 상속되지 않는 것을 확인하여 TASK-010에 전달했다. 담당자가 DataTemplate의 TextBox에 Loaded를 직접 연결했고, 최종 DLL의 실제 생성된 대화 텍스트에서 설정 메뉴가 있음을 검사했다.

최소 폭에서 안내 하단 버튼이 잘릴 가능성도 검토했다. 최종 제품은 WrapPanel을 사용하며 폭 300의 실제 자식 좌표 검사에서 계정 설정 열기·시작하기 두 버튼이 모두 영역 안에 있었다. 일부 초기 PNG의 오른쪽·하단 잘림은 창 내용의 Margin 오프셋을 보정하지 않은 렌더 문제였다. 좌표 검사와 오프셋을 보정한 PNG를 대조하여 제품 UI 결함과 구분했다. 밝음/어두움 안내 이미지도 직접 확인했다.

## 최종 고정 DLL 독립 검사

tests/review/Test-OnboardingJourney.ps1은 개인 설정 경로를 격리하고 별도 Windows PowerShell 5.1 STA 프로세스의 실제 WPF 창으로 검증한다. 생성자 호출만으로 첫 사용 동작을 판단하지 않고 Loaded 이후 모달 안내가 나타나는지를 확인한다. 테스트 시간 제한과 테스트가 소유한 창의 정리도 포함한다. Visual Studio나 AI 계정을 실행하지 않는다.

최종 DLL SHA-256 **109221E7B9B79D658A5C03E238493FCE762AF05F7FF263A9C38D056B1C367654**, 버전 **0.4.0.0**으로 **28/28 PASS**다. 결과는 CodexCode_TASK-011_Journey.json이며 원본은 artifacts/task011/journey-974fe93141f442039aacb6fa06d4627f/result.json이다.

| 검사 범위 | 확인한 동작 |
|---|---|
| 이전 설정 호환 | HideWelcome이 없는 v0.3 JSON은 안내 표시로 시작 |
| 첫 사용 | 실제 Loaded 이후 안내가 열리고 처음 숨김 체크는 해제 |
| 안내 선택 | 체크 저장 후 새 컨트롤에서 자동 안내 생략, 수동 안내는 계속 열림 |
| 자동 안내 복원 | 수동 안내에서 체크 해제 후 다음 컨트롤에 자동 표시 |
| 설정 보존 | 숨김 선택 저장이 기존 두 CLI 경로·두 모델·규칙·글자 크기를 보존 |
| 설정 창 | 저장 전 닫기는 규칙·글자 크기 복원, 명시적 저장은 안내 숨김 선택 유지 |
| 메뉴 | Chat의 설정·사용 안내, 프롬프트의 대상 지정 Paste, 동적 대화의 설정 메뉴 |
| 창 배치 | CenterScreen·현재 소유 창 연결, 최소 폭 300에서 두 안내 버튼 접근 |

초기 인계 DLL FBD35FCF...의 동일 검사도 통과했지만 최종 결과는 위 최종 해시 기준으로 구분한다. 내장 C# 타입과 명시적 메서드의 주석 5필드(name/desc/author/create date/update date), PowerShell 구문과 UTF-8 BOM도 확인했다.

```powershell
powershell -NoProfile -STA -ExecutionPolicy Bypass -File .\tests\review\Test-OnboardingJourney.ps1 `
    -AssemblyPath .\src\VS2017.AIAssistant\bin\Release\VS2017.AIAssistant.dll
```

## 사용자 문서와 화면

README.md는 사용자 효용·설치·첫 요청 중심 구성을 유지하면서 v0.4.0 다운로드, 우클릭 설정, 첫 사용 안내를 추가했다. docs/USER_GUIDE.md에는 계정 설정 바로 가기, 안내 숨김·다시 켜기, 현재 AI 확인과 다른 AI 설정 방법, 저장 후 닫힘 및 닫기·Esc 취소를 구체적으로 설명했다. CLI에서 이미 완료한 로그인은 설정 취소와 별개임도 안내한다.

docs/assets/chat-0.4-preview.png와 getting-started-0.4.png는 최종 고정 DLL로 실제 제품 WPF를 렌더했다. Chat에는 합성 예제 대화를 사용했으며 실제 계정 대화나 전체 IDE 화면으로 표시하지 않았다. 두 최종 PNG를 열어 제목·아이콘·본문·검토·입력·하단 버튼이 보이는지 확인했다. 기존 v0.3 예제 파일은 보존했다.

이 검사는 WPF 사용 흐름을 검증하며 실제 IDE·CLI 검증을 대신하지 않는다. TASK-010의 Debug/Release 빌드 경고·오류 0, 편집/UX 86개·Foundation 33개·호환성 44개 PASS 인계를 확인했다. 실제 IDE 결과·릴리스 링크 검증은 최종 완료 기록에서 구분한다. Claude 실계정 호출과 VS2019/2022 패키지 지원은 여전히 후속 범위로 문서에 명시했다.

## 최종 완료 기록 — 2026-10-18 11:45:52 KST

실제 IDE 결과 CodexOrig_TASK-010_IDE.json의 Succeeded=true·Passed=50·ProductVersion=0.4.0.0 및 실제 Codex 포함을 읽어 확인했다. 이 결과는 제품 담당자의 실제 VS2017 검사이며, CodexCode의 별도 WPF 검사 28개와 구분한다. 전체 검사 숫자를 서로 중복 없는 케이스인 것처럼 합산하지 않았다.

최종 releases/v0.4.0 VSIX SHA-256은 2DD768237916AD7671B1829C279977D8BAC65684B59DFD3DC85DAD3C99523C53이다. VSIX에서 직접 해시한 내장 DLL은 독립 검사 DLL 109221E7...와 일치한다. README/USER_GUIDE의 로컬 링크·이미지 17개가 모두 존재하고, 두 이전 릴리스와 사용자 Role.md·image-1.png의 시작 시점 해시도 일치한다. 근거는 CodexCode_TASK-011_Documentation.json이다.

문서·이미지·신규 테스트·자기 보고를 마감했다. 독립 검토 하위 작업자도 종료했으며 더 이상 CodexCode의 활성 파일 편집은 없다. WORK_STATUS에서 합의한 TASK-010 CodexOrig가 최종 GitHub 배포를 진행할 수 있도록 인계한다. CodexCode는 별도 커밋·push를 수행하지 않는다. 소요 17분 41초.