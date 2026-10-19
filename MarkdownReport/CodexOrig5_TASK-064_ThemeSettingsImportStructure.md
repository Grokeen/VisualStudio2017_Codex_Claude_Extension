# TASK064 보충 조사 — .vssettings 전체 IDE 테마 import 구조

작성: CodexOrig5 / 2026-10-20 KST  
목적: CodexOrig의 요청에 따라 공식 Microsoft 자료에서 .vssettings의 지원 범위와 전체 테마/Fonts and Colors 구분, 최소 XML 작성에 적용할 구조 보존 원칙을 정리한다. Visual Studio 실행·사용자 설정 변경·제품 코드 변경은 하지 않았다.

## 판정

Microsoft의 공식 문서는 `Tools.ImportandExportSettings`를 `.vssettings` 가져오기/내보내기 명령으로 안내한다. 공식 문서가 설명하는 XML 형식은 열린 스키마(open schema)로, 일반적인 구조에서 각 category가 tag가 되고, 그 안에 subcategory tag와 property value tag가 올 수 있다고 설명한다. Visual Studio의 어느 package든 자체 스키마를 가진 임의의 XML을 추가할 수 있다. 따라서 `<Category>` fragment만 골라 새로운 파일을 직접 구성하는 것보다, 대상 Visual Studio가 실제로 export한 파일에서 category/group wrapper와 해당 노드 순서를 그대로 유지하고 필요한 값만 바꾸는 것이 보수적인 절차다. [공식 Import and Export Settings 문서](https://learn.microsoft.com/en-us/visualstudio/ide/reference/import-and-export-settings-command?view=visualstudio)

CodexOrig가 #064 테스트에서 공유한 진행 정보에 따르면, 전체 export의 Theme GUID만 교체해 import하면 두 테마 선택 모두 통과했고, 기존 최소 XML은 `Environment_Group` wrapper를 생략한 상태였다. 이는 현재 root에서 진행 중인 검증 결과로 보고서 작성자가 IDE에서 재실행한 것은 아니다. 그래서 064가 다음으로 시험하는 `Environment_Group`과 FontsCategory-only 노드를 원본 export 구조대로 보존한 최소 import probe가 적절하다. 최소 probe가 성공하더라도 package가 임의 XML을 기여할 수 있다는 열린 스키마 특성상, 지원되는 부분 범위는 테스트한 SKU/파일 구조에 한정해야 한다.

## 혼동하면 안 되는 두 설정

Microsoft 문서는 **Color Theme**를 IDE frame과 tool windows에 적용되는 전체 IDE 색 테마로 설명하고, Tools > Options > Environment > General의 선택 항목으로 안내한다. **Fonts and Colors**는 IDE 요소별 글꼴, 크기, 전경/배경색 scheme을 정하는 별도 경험이며, 설정 변경이 현재 session에 즉시 적용되지 않는 경우도 있다고 안내한다. 따라서 `Environment_FontsAndColors`의 font/color category를 import한 것을 전체 IDE 테마 변경 성공으로 판정하면 안 된다. [테마·글꼴·색상 공식 문서](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-change-fonts-and-colors-in-visual-studio?view=vs-2022) [Fonts and Colors 옵션 공식 문서](https://learn.microsoft.com/en-us/visualstudio/ide/reference/fonts-and-colors-environment-options-dialog-box?view=visualstudio)

공식 UI 문서는 VS2022의 테마 선택 위치와 의미를 문서화하지만, 공개 `EnvDTE.Properties("Environment", "General")` 항목의 이름이나 VS2017–2022에 걸쳐 사용할 수 있는 프로그램 테마 선택 API를 명시하지 않는다. 따라서 DTE General 속성에서 `ColorTheme` setter를 추측해 호출하는 것은 공식 문서 근거가 없다. 이 메모는 해당 공개 API가 불가능하다고 단정하지 않고, 확인된 공식 공개 근거를 발견하지 못했다고 한정한다. [개인 IDE 설정 공식 문서](https://learn.microsoft.com/en-us/visualstudio/ide/personalizing-the-visual-studio-ide?view=visualstudio)

## #064에 적용할 구현·판정 원칙

1. VS2017, 2019, 2022의 각 격리 테스트 인스턴스에서 각 SKU의 실제 `.vssettings` export를 기준 파일로 보존한다.
2. 해당 파일의 Theme GUID만 변경한 전체 export import는 전체 테마의 양성 대조군으로 사용한다. 검증 성공 기준은 GUID 텍스트가 파일로 이동한 것이 아니라, import가 완료되고 IDE의 실제 theme selection이 바뀐 것이다.
3. 최소 import probe를 만들 때는 `Environment_Group`/`FontsAndColors` 노드의 부모-자식 wrapper, `Name`/`Package` 식별자 및 namespace를 해당 SKU export에서 그대로 보존하고 값만 최소 변경한다. 문서상 XML schema가 열려 있으므로 임의의 tag 이름/축약 구조를 새로 구성할 근거가 없다.
4. 테스트별로 무엇을 import했는지, 대상 VS 버전/build, import 결과, import 후 실제 theme ID, 기존 UI/text 색상 설정 보존 결과를 별도 기록한다. Full export probe와 최소 Fonts-only probe를 서로 다른 acceptance로 구분한다.
5. 사용자의 기본 프로필을 건드리지 않고 별도 RootSuffix/격리 프로필에서만 실행한다. 이 메모 작성자는 registry나 Visual Studio 설정을 변경하지 않았다.

## 공식 근거 및 범위 한계

- `.vssettings`는 Visual Studio 설정 import/export용이며, 문서된 XML은 package가 category/subcategory/property-value를 구성하는 open schema다.
- Microsoft 사용자 가이드는 IDE theme와 Fonts and Colors를 별도 설정으로 설명한다.
- Microsoft 문서는 category wrapper를 보존하는 최소 XML recipe 또는 `Environment_Group`의 모든 SKU별 serialization detail을 규범적으로 정의하지 않는다. wrapper-preservation 권고는 공개된 open schema 설명과 실제 export가 import 양성 대조군이 된다는 #064 진행 상황을 합친 보수적 테스트 원칙이다.
- 이 메모는 실제 Theme service나 IDE 내부 COM API를 장려하거나 그 안정성을 주장하지 않는다.

## 공식 자료

- [Microsoft Learn — Import and Export Settings command (.vssettings file)](https://learn.microsoft.com/en-us/visualstudio/ide/reference/import-and-export-settings-command?view=visualstudio)
- [Microsoft Learn — Change fonts, colors, and themes in Visual Studio](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-change-fonts-and-colors-in-visual-studio?view=vs-2022)
- [Microsoft Learn — Fonts and Colors Environment Options Dialog Box](https://learn.microsoft.com/en-us/visualstudio/ide/reference/fonts-and-colors-environment-options-dialog-box?view=visualstudio)
- [Microsoft Learn — Customize & save your personal Visual Studio IDE settings](https://learn.microsoft.com/en-us/visualstudio/ide/personalizing-the-visual-studio-ide?view=visualstudio)
