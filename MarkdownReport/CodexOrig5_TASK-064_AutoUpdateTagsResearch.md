# TASK064 보충 — XAML `AutoUpdateTags` 기본값 및 동작 의미

작성: CodexOrig5 / 2026-10-20 KST  
범위: Microsoft 공식 공개 문서에서 항목 이름을 확인하고, 설치된 Visual Studio 2017/2019/2022 vendor XAML package의 읽기 전용 IL / 기본 프로필을 대조했다. IDE 실행, 사용자 설정 변경, 제품 소스 편집은 하지 않았다.

## 판정

`XAML_Miscellaneous`의 `AutoUpdateTags`는 **XAML 요소의 시작 태그 이름을 수정하면 짝이 되는 끝 태그 이름도 자동으로 맞춰 주고, 끝 태그 쪽 이름을 수정할 때도 시작 태그와 동기화하는 기능**이다. 패키지 구현에 `ProcessBufferChange`와 `RenameElementTags`가 있고, 버퍼 변경 경로에서 `AutoUpdateTags`가 false이면 그 rename path를 건너뛴다. 따라서 C8에서 새로 저장된 `True`는 테마 색상 데이터가 아니라 XAML 편집 동작을 켜 둔 값이다.

설치된 vendor package 근거는 다음과 같다.

- **VS2017:** `Microsoft.VisualStudio.DesignTools.XamlDesignerHost.dll`의 `MS.Internal.XamlSettings.get_AutoUpdateTags` IL은 setting key `Microsoft.VisualStudio.TextEditor.Xaml.Miscellaneous.AutoUpdateTags`를 읽고 `ISettingsManager.GetValueOrDefault<bool>(key, true)`에 해당하는 호출을 한다. 이 key가 없는 경우 기본값은 `true`다.
- **VS2019:** 같은 설정 getter가 기본 인수 `true`를 전달한다. 같은 package의 default constant field도 `.field ... AutoUpdateTags = bool(true)`다.
- **VS2022:** 설치된 `General.vssettings`, `CSharp.vssettings` 및 다른 기본 profile 파일들에 `XAML_Miscellaneous` 아래 `<PropertyValue name="AutoUpdateTags">True</PropertyValue>`가 기록돼 있다. XAML language service의 `XamlMiscPage.get_AutoUpdateTags`도 설정 서비스가 없으면 `true`를 반환한다. 설치된 package 자체의 settings getter는 default-profile의 true와 일치한다.

VS2017의 `General.vssettings` 기본 파일에는 이 property 행이 없지만, package getter가 key 부재 때 `true` fallback을 사용하므로 미기록을 `false`로 해석하면 안 된다. 즉 이 값은 설치된 VS2017/2019/2022 기준에서 정상 기본값이며, C8에서 보인 행 하나는 실제 사용자가 변경한 설정으로 볼 근거가 없다. 전후 값 검증에서 `True → True` 또는 원래 값 유지로 처리하는 편이 맞다.

## 근거 자료

공식 Microsoft 문서 검색에서는 `AutoUpdateTags` 이름 자체의 설명을 찾지 못했다. 따라서 옵션 의미에 대한 근거는 설치된 Microsoft XAML package 내부의 메서드/설정 key 이름과 호출 흐름이며, 공개 API 계약이나 별도 Microsoft Learn 항목으로 과장하지 않는다. 각 설치 파일을 ILDasm으로 메타데이터/IL 읽기만 했다.

| 설치본 | 설치된 파일 | 확인 사항 |
|---|---|---|
| Visual Studio 2017 Community | `C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.DesignTools.XamlDesignerHost.dll` (15.0.36227.7) | 설정 key와 fallback `true`; XAML source update path에서 AutoUpdateTags 조회 후 paired-tag rename |
| Visual Studio 2019 Community | `C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\CommonExtensions\Microsoft\DesignTools\Microsoft.VisualStudio.DesignTools.XamlDesignerHost.dll` (16.0.34830.201) | getter fallback `true`; package default constant `AutoUpdateTags=true` |
| Visual Studio 2022 Community | `C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\XamlLanguageService\Microsoft.VisualStudio.DesignTools.XamlLanguageService.dll` 및 `C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\Profiles\General.vssettings` | getter에서 fallback `true`; 기본 profile에서 serialized value `True` |

2022 XAML language service 구현은 `OnBufferChanged`에서 AutoUpdateTags 설정을 검사하고, `ProcessBufferChange`가 편집 위치가 element `StartNameRange` 또는 `EndNameRange`에 속하는지 확인한 뒤 `RenameElementTags`를 호출한다. 이는 속성이나 색 테마를 자동 수정하는 설정이 아니다. 구현은 하나의 XAML 편집 변경이 paired element-name에 해당할 때 실행되며, 구체적인 IDE UX 문구는 공식 문서에서 찾지 못했으므로 내부 구현의 동작 범위를 기준으로 설명한다.

## Root / #064 인계

- C8 비교에서 `AutoUpdateTags=True` 행만 새로 보인 것은 XAML package 설정의 기본값 직렬화 차이다. VS2017 프로필은 기본값을 생략하지만 package fallback은 `true`; 2019/2022 기본 profiles는 해당 값을 명시한다.
- 이 행을 테마 import에서 불필요한 사용자 변경으로 간주하거나 원래 설정을 덮어쓴 것으로 보고하면 안 된다. 검증 harness에서 비교 시 대상 package의 기본값 차이로 처리하고, 사용자 설정 보존 검증은 실제 설정 store 전후값으로 판정한다.
- 다만 기존 private IDE SettingsStore에서 관찰한 사용자 값이 `False`라면 `True` import는 실제 변경이다. 기본 profile/IL fallback은 특정 사용자의 현재 값을 증명하지 않으므로, #064가 진행 중인 isolated profile의 실제 before/after read 검증을 우선한다.

## 참고

- [Microsoft Learn — Import and Export Settings command (.vssettings file)](https://learn.microsoft.com/en-us/visualstudio/ide/reference/import-and-export-settings-command?view=visualstudio): `.vssettings`를 가져오고 내보내는 표준 경로 및 열린 XML schema에 대한 공식 설명.
- [Microsoft Learn — Customize and save Visual Studio IDE settings](https://learn.microsoft.com/en-us/visualstudio/ide/personalizing-the-visual-studio-ide?view=visualstudio): 사용자별 설정 import/export의 공식 맥락. `AutoUpdateTags`의 세부 의미는 설명하지 않는다.
- 설치된 Microsoft vendor DLL / default profile 파일 위 표의 absolute paths. IL 내용은 읽기 전용으로 검사했다.

## #064 추가 raw 차이 — `DefaultZoomSetting=1` (VS2019/2022)

Root가 추가로 공유한 C8 비교에서는 VS2019에서 `ToolsOptions/XAML Designer/General/DefaultZoomSetting=1` 행만 export에 추가되었고, 기존값 변경/삭제는 없었다. 설치된 VS2019 XAML Designer package를 ILDasm으로 읽어 다음을 확인했다.

- `Microsoft.VisualStudio.DesignTools.UtilityBase.dll` enum `Microsoft.VisualStudio.DesignTools.Utility.Settings.DefaultZoomSetting`은 `LastUsed = 0`, `FitAll = 1`이다.
- VS2019의 `Microsoft.VisualStudio.DesignTools.XamlDesignerHost.dll`에서 `XamlSettingsDefaults..cctor`는 `DefaultZoomSetting` 기본 필드를 정수 `1`로 초기화한다. `get_DefaultZoomSetting`은 해당 default를 `ISettingsManager.GetValueOrDefault`의 fallback으로 전달한다.
- VS2022의 동일한 enum은 `LastUsed=0`, `FitAll=1`이다. 설치된 기본 profile 파일들도 `DefaultZoomSetting=1`을 직렬화하고, package의 `XamlSettingsDefaults..cctor` 기본 초기값이 정수 `1`임을 확인했다.
- 설치된 VS2017 `Common7\IDE`의 기본 profiles / vendor DLL 검색에서는 `DefaultZoomSetting` 항목을 찾지 못했다. 따라서 이번 행은 VS2017 값과 비교할 값이 아니라 VS2019/2022 XAML Designer General option이다.

Microsoft Learn의 XAML Designer 옵션 문서는 `Default zoom setting`을 XAML 문서의 기본 zoom 결정으로 설명한다. `Last Used`는 문서를 처음 열 때만 `Fit All`을 쓰고 이후 마지막 zoom을 사용한다. `Fit All`은 다른 Visual Studio session에서 기본 zoom을 Fit All로 시작한다. 따라서 VS2019/2022에서 새로 나타난 `1` 행은 **기본값 Fit All**을 직렬화한 것이다. C8에서 확인한 이 행 하나만으로 사용자의 실제 옵션이 변경됐다고 할 수 없으며, raw before/after에 행만 새로 생긴 것이 실제 설정 store에서 기존의 비기본값을 바꿨는지는 별도 격리 store before/after 검사로 판정해야 한다. [Microsoft Learn — Configure XAML Designer options](https://learn.microsoft.com/en-us/visualstudio/xaml-tools/xaml-designer)

## C9 보충 — VS2017 XAML 기본값 여섯 항목

Root가 공유한 C9 raw export 차이의 다음 여섯 `XAML_Miscellaneous` property를 설치된 VS2017 vendor package의 `MS.Internal.XamlSettings` getter IL로 각각 확인했다. 여섯 getter 모두 동일한 `ISettingsManager.GetValueOrDefault<bool>(settingKey, true)` 경로다. 따라서 해당 키가 설정 저장소에 없을 때 사용하는 package 기본값은 `true`다.

| `XAML_Miscellaneous` property | 설정 key suffix | VS2017 package fallback |
|---|---|---:|
| `AutoInsertAttributeQuotes` | `AutoInsertAttributeQuotes` | `true` |
| `AutoInsertCommas` | `AutoInsertCommas` | `true` |
| `AutoInsertEndBraces` | `AutoInsertEndBraces` | `true` |
| `AutoInsertEndTags` | `AutoInsertEndTags` | `true` |
| `ShowDesignerErrors` | `ShowDesignerErrors` | `true` |
| `WarnMinTargetCompatibility` | `WarnMinTargetCompatibility` | `true` |

근거 파일: `C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.DesignTools.XamlDesignerHost.dll` (FileVersion 15.0.36227.7). 각 getter는 `Microsoft.VisualStudio.TextEditor.Xaml.Miscellaneous.<property>` 키를 읽는다. 이는 C9에서 raw export에 `True` 행이 새로 나온 것이 각 옵션의 package fallback과 일치한다는 점만 입증한다. 실제 사용자 값이 다르게 명시되어 있었는지, import가 바꾸었는지는 해당 격리 설정 저장소의 before/after 검사로 별도 판정해야 한다. Root가 보고한 `AutoUpdateTags=False`는 raw export 및 read-only settings-store 모두 보존됐으며, 이 여섯 키와 구분된다. 제품/IDE 설정은 이 조사에서 수정하지 않았다.
