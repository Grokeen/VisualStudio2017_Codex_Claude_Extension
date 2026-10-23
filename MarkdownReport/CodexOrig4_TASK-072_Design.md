# TASK-072 Dracula 댓글·diff·스크롤 색상 설계 검토

담당: CodexOrig4 · 2026-10-23
범위: 읽기 전용 테마 키 조사 및 설계 권고. TASK071 자료와 겹치지 않도록 현 작업은 TASK072로 기록한다. `scripts/Build-VisualStudioThemes.ps1`, baseline, 제품 소스, build/output, Release, 공유 이력은 수정하지 않았고 IDE도 실행하지 않았다.

## 결론

일반 C# `//`와 `/* */`는 Roslyn syntax tree에서 각각 `SingleLineCommentTrivia`와 `MultiLineCommentTrivia`로 구분된다. 하지만 편집기 색상 분류는 두 일반 주석 형식 모두 공통 `comment` 분류 토큰을 사용하므로 테마만으로 둘을 서로 다른 색으로 나눌 수 없다. `///` XML 문서 주석은 구조화된 trivia이며 `xml doc comment - text`, `xml doc comment - delimiter`, `xml doc comment - name` 등 별도 분류를 사용한다. 그러므로 `///`를 일반 주석과 구분하는 것은 테마 분류 키로 가능하지만, 실제 적용은 대상 VS 버전에서 해당 Roslyn 키가 활성 format definition으로 등록되는지 확인해야 한다.

Roslyn은 일반 주석의 두 trivia kind를 각각 명시하고 `SyntaxFactory.Comment`가 두 종류를 반환할 수 있다고 설명한다. `ClassificationTypeNames`에는 일반 `comment` 상수와 여러 XML 문서 주석 전용 분류 키가 따로 정의되어 있다. [SyntaxFactory.Comment API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.comment?view=roslyn-dotnet-5.0.0), [Roslyn ClassificationTypeNames.cs](https://github.com/dotnet/roslyn/blob/main/src/Workspaces/Core/Portable/Classification/ClassificationTypeNames.cs), [Roslyn XML 문서 주석 format definitions](https://github.com/dotnet/roslyn/blob/main/src/EditorFeatures/Core/Classification/ClassificationTypeFormatDefinitions.cs).

## 현재 Dracula 테마 구성과 통일성 이슈

`Build-VisualStudioThemes.ps1`의 Dracula 전용 보정은 `#282A36` 스크롤 트랙, `#6272A4` thumb, hover `#BD93F9`, pressed `#FF79C6`, breakpoint 및 진단 키를 별도로 설정한다. 이 상태별 분리는 일반 thumb, hover, pressed, breakpoint, 오류/경고의 역할을 구별해 주므로 유지할 만하다. 오류 overview margin은 `ErrorMarginElement → OverviewErrorMargin`에서 실제 `IErrorTag.ErrorType`에 대응하는 `IEditorFormatMap` foreground를 사용하고, breakpoint는 `MarkMarginElement → OverviewMarkMargin`에서 `IOverviewMarkTag.MarkKindName`과 `MarkerFormatDefinition/BreakpointInScrollBar` background를 사용한다. 과거 TASK069 보고서의 VS2017 실제 format map 검증은 이 키들의 값과 margin 등록을 확인했지만, 실제 진단·breakpoint 태그 생성이나 픽셀 표시는 검증하지 않았다.

기준 파일 `Themes/VisualStudio/Baselines/PlainDark.xml`에는 다음 역할별 기존 키가 있다.

| 역할 | 기준 키 | 기준값/관찰 |
|---|---|---|
| 일반 댓글 | `Text Editor Language Service Items/Comment` | 기준 foreground `#57A64A` |
| XML 문서 댓글 본문·태그 | `xml doc comment - text`, `... - delimiter`, `... - name` 등 | 여러 세부 classification format 정의 |
| inline added diff | `deltadiff.add.line`, `deltadiff.add.word` | 녹색 배경 및 단어 표시 색 |
| diff overview marker | `deltadiff.overview.color` | 기준 전경 `#424042`, 배경 `#252925` |
| 변경 스냅샷 텍스트 | `SnapshotDiffAddedText` | 별도 보조 UI 키 |

현재 Dracula 팔레트 변환기는 대부분의 baseline 색을 색상 계열별 대표값으로 바꾼 뒤, 별도 Dracula 블록에서 일부 UI 키만 고정한다. 그러므로 단순히 baseline에 정의된 diff green이나 댓글 green만 보고 최종 Dracula 결과를 판단하면 안 된다. 변환 로직을 따라가면 일반 `Comment` 및 XML doc green 분류 값과 `deltadiff.add.line`의 어두운 녹색 계열 배경이 같은 대표 밝은 녹색 `#50FA7B`로 치환될 것으로 예상된다. comment, 문서, 변경면을 모두 같은 강한 초록으로 만들면 색만으로 기능 상태를 구분하기 어렵고 면적이 큰 diff가 시선을 과하게 끌 수 있다. `deltadiff.overview.color`의 어두운 중립 전경은 `#44475A`, 트랙 배경은 `#282A36`으로 바뀔 것으로 예상된다. 이 overview 조합의 상대 대비는 약 **1.56:1**이라 작은 스크롤 마커로는 약하다. 이는 소스 변환 규칙으로부터 계산한 예상치이며 이번에는 생성 theme/VSIX로 확인하지 않았다.

Visual Studio는 스크롤바 주석을 변경(changes), 일반 표시(marks), 오류(errors), caret으로 구분한다. 변경 표시는 scrollbar 왼쪽 가장자리, marks는 중간, errors/warnings는 오른쪽에 나타나며, 변경 상태별 기본 색도 Git/파일 추적 상태에 따라 녹색·빨강·파랑·금색 등으로 나뉜다. 따라서 inline diff의 `deltadiff.*`와 scrollbar changes 표시를 하나의 토큰으로 가정하면 안 된다. Visual Studio 문서도 map mode의 `Overview` display items를 Fonts and Colors에서 조정하도록 안내한다. [Visual Studio 스크롤바 주석과 설정](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-track-your-code-by-customizing-the-scrollbar?view=visualstudio).

## 통일성을 위한 설계 권고

* Dracula 본문 배경 `#282A36`에서 일반 주석은 저채도 slate `#A0A6B5`를 권한다(계산 대비 약 **5.84:1**). XML 문서 본문은 `#8BE9FD`(약 **10.29:1**), XML tag/delimiter는 `#BD93F9`(같은 dark base에서 6:1 이상)처럼 청록/보라 계열을 쓰면 일반 주석 녹색과 diff 녹색을 겹치지 않게 정리할 수 있다. 변환 후 실제 format map에서 `comment` 및 `xml doc comment - ...`의 유효 키/색을 확인하고 해당 지원 키에 명시 override하는 설계이며 이번 작업에서 적용하지 않았다.
* inline added diff의 면은 어두운 녹색 계열로 유지해 본문과의 위계를 과도하게 키우지 말고, overview marker만 `#50FA7B`로 밝게 분리하는 것을 권한다. 어두운 line fill은 변경 영역을 넓게 표시하고 밝은 marker는 좁은 overview rail에서 찾기 쉽게 하는 역할 분담이다. `deltadiff.overview.color`는 이후 hue mapper의 결과에 맡기지 말고 Dracula 전용 명시 override로 시험해야 한다. 비교용 밝은 녹색 marker와 `#282A36`의 계산 대비는 약 **10.38:1**이다.
* 현재 scrollbar thumb/hover/pressed와 error/breakpoint의 보라·분홍·황색 역할은 유지하고, added diff green은 이 상태색들과 같은 의미로 재사용하지 않는다. 이렇게 하면 “편집 상태”와 “탐색/진단 상태”가 일관되면서도 서로 혼동되지 않는다.
* 구현 시에는 확장된 색 key 존재만으로 결과를 단정하지 말고, VS2017/2019/2022 각 실제 editor format map에서 로드된 diff/color entry를 읽고, change marker의 실제 표시·가시성과 contrast를 검증한다. theme XML에 색을 넣는 것만으로 Visual Studio 기능을 켜거나 실제 Git/file change marker를 만들어 주지는 않는다.

## 검증 경계와 참고 자료

이번 TASK072는 baseline XML과 generator의 키/변환 순서를 읽어 설계 근거를 정리했다. 색 대비는 WCAG sRGB 상대휘도 식으로 계산했으며 generated VSIX, IDE runtime, 실제 Git 변경선/scrollbar marker, 화면 픽셀을 검사하지 않았다. 따라서 위의 mapper 결과는 구현 전 후보이지 런타임 승인 결과가 아니다.

기존 기술 근거: `MarkdownReport/CodexOrig_TASK-069_ThemeTokenResearch.md`, `MarkdownReport/CodexOrig_TASK-069_MefRouteValidation.md`. TASK072에서는 두 보고서와 소유 제품 파일 및 공용 이력을 변경하지 않았다.
