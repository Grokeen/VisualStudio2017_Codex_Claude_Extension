# TASK-072 Dracula 편집기 표시 UX 짧은 조사

작성: CodexOrig6 · 2026-10-23

범위: Dracula native diff/스크롤 변경 표시, C# 주석 분류, 오류 표시를 공식 편집기 UX와 대조한다. 이 문서는 권고이며 구현 지시나 런타임 승인 판정이 아니다.

## 확인한 프로젝트 상태

- 제공된 최신 상태에 따르면 source9AB 계열 themes6/C3에서 VS2017 실제 IDE 검사는 269개 통과했고 VS2019/2022 검사는 진행 중이다. 이 조사는 결과물·IDE를 다시 실행하거나 SHA를 재검증하지 않았다. 앞선 C1/C2 실패 원문은 별도 보존 대상이다.
- `Build-VisualStudioThemes.ps1`는 Dracula inline added/removed diff 색, `deltadiff.overview.color`, scrollbar changes 색, 주석 및 진단 키를 별도 지정한다.
- `themes/VisualStudio/EditorEnhancements.cs`는 정확한 Dracula 테마와 C# 뷰에 범위를 한정한 block comment 분류와 native error tag 기반 추가 강조를 제공한다. XML 문서 주석은 native 분류를 존중한다.
- `tests/task072/RuntimeEditorChecks72.cs`는 실제 C# editor buffer/view의 `//`, 여러 줄 `/* */`, XML doc, 문자열 안의 comment 모양 텍스트, native error tag 및 추가 marker를 확인하도록 구성되어 있다. 이 조사에서는 그 테스트를 실행하지 않았다.

## 공식 편집기와 비교

Visual Studio는 구문 분류와 그 분류의 글꼴/색 형식을 분리하고, 오류 squiggle을 `ErrorTag` 같은 별도 표시로 다룬다. 스크롤바도 변경(changes), 일반 표시(marks), 오류(errors), caret을 독립 주석으로 나누며, 변경·오류·중단점에 각각 위치와 의미를 부여한다. VS Code 역시 진단을 severity를 가진 별도 language feature로 제공하므로 단순 텍스트 색칠과 오류 의미를 분리하는 방향은 공통적이다. [Visual Studio editor architecture](https://learn.microsoft.com/en-us/visualstudio/extensibility/inside-the-editor), [Visual Studio scrollbar annotations](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-track-your-code-by-customizing-the-scrollbar?view=vs-2022), [VS Code diagnostics API](https://code.visualstudio.com/api/language-extensions/programmatic-language-features)

| UX 영역 | 프로젝트에서 확인한 방향 | 중요한 확인점 |
|---|---|---|
| Native diff와 스크롤 | inline add/remove, scrollbar change marker, viewport/오류·breakpoint marker를 별도 키로 다룸 | 좁은 스크롤 rail에서 변경·오류·breakpoint가 서로 덮이지 않고, marker 의미가 한눈에 구분되는지 실제 host에서 확인 |
| C# 주석 | 일반 주석과 block 주석은 별도 색 분류를 두고 XML doc native 분류를 유지 | `///` XML doc과 `/** */` 경계, 여러 줄 내부 범위, 문자열 안 `//`·`/*` 미분류를 보존. 다른 테마/언어에 Dracula 전용 분류가 새지 않아야 함 |
| 오류 표시 | 기존 native error tag에 border/highlight를 추가 | native squiggle·Error List 의미를 유지하고 warning/suggestion을 오류처럼 승격하지 않음. 추가 테두리로 본문 가독성이나 selection/focus 표시를 가리지 않는지 확인 |

## 이번 범위의 우선 권고

1. **의미 색을 합치지 않는다.** 변경 위치, 오류, 중단점, viewport는 서로 다른 신호로 유지한다. Dracula의 녹색 diff/주석 및 분홍·노랑 진단 색이 실제 narrow scrollbar와 inline diff에서 혼동되지 않는지 VS2017/2019/2022 host별로 확인한다.
2. **native C# 문법을 단일 원천으로 둔다.** 현재처럼 Roslyn/native classification과 error tag를 소비하고, 텍스트 전체를 다시 파싱하는 별도 오류 엔진을 추가하지 않는다. 테스트는 XML doc, block comment, 문자열/빈 주석 경계와 실제 compile error·warning을 구분해야 한다.
3. **모양과 의미를 함께 전달한다.** 오류는 색상뿐 아니라 기존 squiggle 및 좁은 테두리/overview 위치를 유지하고, hover/keyboard 접근으로 진단 설명을 확인할 수 있어야 한다. 고대비/선택/다른 테마에서 강조가 사라지거나 과도하게 밝아지지 않는지 확인한다.
4. **후속 검증의 중심은 픽셀과 상태 전환이다.** 현재 테스트 코드가 format map과 tag span을 읽는지와 별개로, 각 host 실제 화면에서 Dracula 활성·비활성 전환, 저장/미저장 diff, 오류와 breakpoint 동시 표시, 좁은 스크롤바, 다중 편집기 view를 관찰한다. 색 대비 숫자나 static theme key 존재만으로 가독성 승인을 하지 않는다.

범위 밖 기능으로 자체 compiler 진단, 새 diff 시스템, 모든 C# comment 종류의 추가 파서, 전역 IDE 색상/설정 쓰기를 권하지 않는다. 기존 host 신호를 보존하고 표시만 분명히 하는 것이 이 작업의 가장 작은 UX 개선 방향이다.

## 조사 한계

코드·Role·TASK-072 중간 보고와 세 개의 Microsoft/VS Code 공식 자료만 읽었다. source, existing docs, build, VS/IDE, Git, release를 수정하거나 실행하지 않았다. Root가 전달한 2017/2019/2022 후보 상태는 독립 재검증하지 않았다. 실제 픽셀 대비·마우스/키보드 동작·VS2019/2022 런타임은 여기서 판정하지 않았으며, 최종 제품 확인은 통합 담당이 관리하는 최신 후보의 빌드·실IDE 및 배포 절차에 남긴다.
