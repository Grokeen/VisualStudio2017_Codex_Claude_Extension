# TASK069 #17 편집기 스크롤·여백 색상 읽기 조사

담당: CodexOrig / theme064. 설치된 VS2017·VS2019·VS2022의 편집기 어셈블리를 ReflectionOnlyLoadFrom으로 열어 상수·MEF NameAttribute·메서드 IL을 읽었다. 제품/테스트 소스 수정, IDE 시작, GUI/OS 입력, 실제 AI 요청은 수행하지 않았다. 실제 IDE 적용 성공을 주장하는 보고가 아니다.

| 대상 | 세 버전에서 확인한 실제 경로 | 테마 설정 권고 |
|---|---|---|
| 오류 스크롤 | ErrorMarginElement → OverviewErrorMargin; IErrorTag.ErrorType → IEditorFormatMap.GetProperties → Foreground | `syntax error`, `compiler error`, `other error`, `compiler warning`, `suggestion`, `hinted suggestion`의 Foreground를 배경과 구분한다. `information` 상수는 설치된 2022에서만 관찰했다. |
| 중단점 스크롤 | MarkMarginElement → OverviewMarkMargin; IOverviewMarkTag.MarkKindName → Background | 실존 MEF 키 `MarkerFormatDefinition/BreakpointInScrollBar`의 Background를 설정한다. |
| 여백 바탕 | GlyphMarginProperties의 NameAttribute = `Indicator Margin` | 여백 바탕 키이며 hover 상태의 증거는 아니다. |
| 여백 tooltip | TextMarkerMouseHandler.HoverAtPoint → `VsBrush.ScreenTipText`, `VsBrush.ScreenTipBorder`, `VsBrush.ScreenTipBackground` | Environment.ScreenTip 계열은 tooltip에 한정해 적용한다. |
| 기존 marker glyph | TextMarkerGlyphFactory → IVsTextMarkerColorSet.GetMarkerColors → DrawGlyphWithColors | 실제 marker type의 전경·배경을 읽어 검증한다. `Breakpoint - Selected`가 mousehover ghost 상태라는 대응은 미확정이다. |

PlainDark.xml에서 오류 항목 카테고리는 Text Editor Text Marker Items `{ff349800-ea43-46c1-8c98-878e78f46501}`, 중단점 스크롤 항목은 ColorizedSignatureHelp colors `{75a05685-00a8-4ded-bae5-e7a50bfa929a}`이다. 별도 `ErrorInScrollBar` 이름을 만들 근거는 발견하지 않았다. `GlyphMouseOver` 이름은 Cider/TreeView 카테고리에 존재하므로 이를 편집기 gutter hover라고 단정하지 않는다.

실제 IDE 후속 검증은 격리 RootSuffix의 IWpfTextView에서 IEditorFormatMap을 얻어 위 키의 색상·alpha를 읽고 실제 IErrorTag.ErrorType / IOverviewMarkTag.MarkKindName과 대조한다. `OverviewMargin/ShowErrors`, `OverviewMargin/ShowMarks`가 켜졌고 `OverviewMargin/ErrorMarginWidth`, `OverviewMargin/MarkMarginWidth`가 0보다 큰지도 확인한다. 옵션 문자열은 설치된 2022 constructor에서 확인했으며 이전 버전은 공개 EditorOptionKey로 조회하는 방식을 권한다. 임의 키 조회 결과만으로 실제 가시성을 판정하지 않는다.

Microsoft 문서는 스크롤의 오류와 일반 표시를 구분하며, format map API는 등록 이름으로 ResourceDictionary를 조회한다. marker API 문서는 GetMarkerColors 결과가 DrawGlyphWithColors에 사용됨을 명시한다. [스크롤 표시 설정](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-track-your-code-by-customizing-the-scrollbar?view=visualstudio), [IEditorFormatMap.GetProperties](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.text.classification.ieditorformatmap.getproperties?view=visualstudiosdk-2022), [PredefinedErrorTypeNames](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.text.adornments.predefinederrortypenames?view=visualstudiosdk-2022), [IVsTextMarkerColorSet](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.textmanager.interop.ivstextmarkercolorset?view=visualstudiosdk-2022).

원본 메타데이터·IL·세 SKU 어셈블리 경로/버전/SHA는 `artifacts/task069theme-research/read-only-final3-1/evidence.json`에 보존했다. SHA256: `2BEECF300AE0FE7A98FD2BC35603EE48F53D8C0EEDAC05D72B69DC8CE7227488`. 개발 증거이며 사용자 설치 묶음에는 포함하지 않는다. 기존 성능 제품 소스와 33개 검사는 동결을 유지했다.
