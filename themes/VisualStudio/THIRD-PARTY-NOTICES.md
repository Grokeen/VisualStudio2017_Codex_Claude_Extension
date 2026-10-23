# Independent Visual Studio themes

These optional themes contain no AI Assistant, CLI, account, project or network dependency. Installing a package adds theme choices; the user chooses a theme in **Tools → Options → Environment → General → Color theme**. Existing Visual Studio themes and the current selection are preserved. Remove the package through Extensions to remove its added choices.

Starting with 0.14.2, each theme package also contains `Grokeen.VisualStudioThemes.dll`, a small editor MEF component. In the selected Grokeen Dracula theme it reads existing C# classification and diagnostic tags to distinguish block comments and add an error highlight. It does not parse or edit source files, contact a service, or require the AI Assistant. Editor SDK assemblies are provided by Visual Studio and are not redistributed in the package. View-specific annotation settings are restored when leaving the theme; global editor settings are not written.

**Grokeen Kakao Inspired** is an independently authored light IDE palette inspired by the reference screenshots and Kakao's published yellow `#FEE500` and text `#191919`. It is not an official Kakao product. No Kakao logo or artwork is included.

**Grokeen Dracula Inspired** adapts the public Dracula palette to Visual Studio color tokens. It is not the official Dracula extension, and VS Code JSON themes cannot be installed directly in Visual Studio.

The complete color-token baseline `PlainDark.xml` is derived from [Microsoft VS-ColorThemes](https://github.com/microsoft/VS-ColorThemes/tree/9ac1dc46730cd977302abd20b76baa7db433e0bf/VSColorThemes/Themes), commit `9ac1dc46730cd977302abd20b76baa7db433e0bf`, under the MIT license. Both generated palettes use the same 80-category, 2470-color baseline. The source repository retains the original in `themes/VisualStudio/Baselines/`; VSIX packages include the license at `Licenses/Microsoft-VS-ColorThemes.txt`. Generated themes use new GUIDs and names and do not overwrite Microsoft or other third-party themes. Raw ARGB entries incorrectly marked as system-color indices in the baseline are normalized to raw colors; real Windows system-color indices remain inherited.

Dracula palette sources: [official specification](https://draculatheme.com/spec), [user-requested VS Code project](https://github.com/dracula/visual-studio-code), [Visual Studio project](https://github.com/dracula/visual-studio). The MIT license from the VS Code project is included as `Licenses/Dracula.txt`.

Kakao source: [Kakao Developers design guide](https://developers.kakao.com/docs/en/kakaologin/design-guide). The published button colors are a visual reference, not a Visual Studio theme specification.

Packaging follows Microsoft's [theme registration sample](https://github.com/microsoft/VS-ColorThemes) and [VSIX Color Compiler documentation](https://learn.microsoft.com/en-us/visualstudio/extensibility/internals/vsix-color-compiler). The build uses the installed VS2017 color compiler and produces separate VS2017, VS2019 and VS2022 manifests. Theme installation in an actual IDE and customer offline installation must be reported separately from compiler/package checks.
