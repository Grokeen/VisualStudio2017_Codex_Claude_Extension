using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ThemeRegistrationChecks64
/// desc         : 자기 ThemeApply64 인스턴스의 구성 hive만 KEY_READ로 열어 실제 VS 테마 등록을 확인한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK064 자기 프로필의 테마 구성과 AI 등록을 읽기만 한다.
/// </summary>
/// ---------------------------------------------------------------------
public static class ThemeRegistrationChecks64
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadActualPalette
    /// desc         : 실제 호스트 공개 테마 키를 읽어 고정 정의 일치와 스크롤·탭 대비를 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 보조 개발 palette 조회와 물리 hover·화면 가시성 검사를 구분.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static object ReadActualPalette(string theme, string definition, Action<string, bool> check)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var document = new XmlDocument { XmlResolver = null }; document.Load(definition);
        var colors = new Dictionary<string, Color>(); var rows = new List<object>(); var contrasts = new List<object>();
        string[] names = theme == "dracula" ? new[] { "ScrollBarBackground", "ScrollBarThumbBackground", "ScrollBarThumbMouseOverBackground", "ScrollBarThumbPressedBackground", "ScrollBarThumbBorder", "ToolWindowBackground" } :
            new[] { "FileTabHotGradientTop", "FileTabHotGradientBottom", "FileTabHotText", "ToolWindowTabMouseOverBackgroundBegin", "ToolWindowTabMouseOverBackgroundEnd", "ToolWindowTabMouseOverText", "ToolWindowBackground", "EnvironmentBackground" };
        if (theme == "dracula")
        {
            var expanded = new List<string>(names);
            expanded.AddRange(new[] { "EnvironmentBackground", "CommandBarMenuBackgroundGradientBegin", "CommandBarMenuBackgroundGradientEnd", "CommandBarMenuBorder", "CommandBarMenuSeparator", "CommandBarMenuItemMouseOver", "CommandBarMenuItemMouseOverText", "CommandBarMenuItemMouseOverBorder", "CommandBarMenuMouseOverGlyph", "CommandBarMenuMouseOverSubmenuGlyph" });
            names = expanded.ToArray();
        }
        foreach (string name in names)
        {
            var property = typeof(EnvironmentColors).GetProperty(name + "ColorKey", BindingFlags.Public | BindingFlags.Static);
            if (property == null) throw new MissingMemberException("Expected public EnvironmentColors key: " + name);
            var key = property.GetValue(null, null);
            var getter = typeof(VSColorTheme).GetMethod("GetThemedColor", new[] { key.GetType() });
            if (getter == null) throw new MissingMethodException("Public VSColorTheme.GetThemedColor overload missing.");
            var actual = (Color)getter.Invoke(null, new[] { key }); colors.Add(name, actual);
            string definitionName = name == "CommandBarMenuItemMouseOverText" ? "CommandBarMenuItemMouseOver" : name;
            string channel = name == "CommandBarMenuItemMouseOverText" ? "Foreground" : "Background";
            var expected = document.SelectSingleNode("//Category[@Name='Environment']/Color[@Name='" + definitionName + "']/" + channel);
            if (expected == null || expected.Attributes["Type"].Value != "CT_RAW") throw new InvalidDataException("Expected compiled raw color missing: " + name);
            string expectedArgb = expected.Attributes["Source"].Value.ToUpperInvariant(); string actualArgb = actual.ToArgb().ToString("X8", CultureInfo.InvariantCulture);
            rows.Add(new { Key = property.Name, ActualArgb = actualArgb, ExpectedArgb = expectedArgb });
            check(theme + "-PALETTE-" + name + " actual EnvironmentColors equals fixed compiled definition", actualArgb == expectedArgb);
        }
        string[][] pairs = theme == "dracula" ? new[] {
            new[] { "ScrollBarThumbBackground", "ScrollBarBackground" }, new[] { "ScrollBarThumbMouseOverBackground", "ScrollBarBackground" }, new[] { "ScrollBarThumbPressedBackground", "ScrollBarBackground" }
        } : new[] {
            new[] { "FileTabHotText", "FileTabHotGradientTop" }, new[] { "FileTabHotText", "FileTabHotGradientBottom" },
            new[] { "ToolWindowTabMouseOverText", "ToolWindowTabMouseOverBackgroundBegin" }, new[] { "ToolWindowTabMouseOverText", "ToolWindowTabMouseOverBackgroundEnd" }
        };
        double threshold = theme == "dracula" ? 3.0 : 4.5;
        foreach (var pair in pairs)
        {
            double ratio = Contrast(colors[pair[0]], colors[pair[1]]);
            contrasts.Add(new { Foreground = pair[0], Background = pair[1], Ratio = ratio, Minimum = threshold });
            check(theme + "-CONTRAST-" + pair[0] + "/" + pair[1] + " meets " + threshold.ToString(CultureInfo.InvariantCulture), ratio >= threshold);
        }
        object editor = null;
        object menu = null;
        if (theme == "dracula")
        {
            double hoverText = Contrast(colors["CommandBarMenuItemMouseOverText"], colors["CommandBarMenuItemMouseOver"]);
            double hoverGlyph = Contrast(colors["CommandBarMenuMouseOverGlyph"], colors["CommandBarMenuItemMouseOver"]);
            double outsideBorder = Contrast(colors["CommandBarMenuBorder"], colors["EnvironmentBackground"]);
            double insideBorder = Contrast(colors["CommandBarMenuBorder"], colors["CommandBarMenuBackgroundGradientBegin"]);
            check("dracula-MENU hover text contrast meets 4.5", hoverText >= 4.5);
            check("dracula-MENU hover glyph contrast meets 4.5", hoverGlyph >= 4.5);
            check("dracula-MENU border contrasts against surrounding IDE by at least 3", outsideBorder >= 3.0);
            check("dracula-MENU border contrasts against inner menu background by at least 3", insideBorder >= 3.0);
            check("dracula-MENU hover background differs from menu and IDE", colors["CommandBarMenuItemMouseOver"].ToArgb() != colors["CommandBarMenuBackgroundGradientBegin"].ToArgb() && colors["CommandBarMenuItemMouseOver"].ToArgb() != colors["EnvironmentBackground"].ToArgb());
            menu = new { HoverTextContrast = hoverText, HoverGlyphContrast = hoverGlyph, OuterBorderContrast = outsideBorder, InnerBorderContrastObserved = insideBorder, PhysicalHoverPerformed = false };
        }
        if (theme == "kakao")
        {
            var category = document.SelectSingleNode("//Category[@Name='Text Editor Text Manager Items']");
            var plain = category.SelectSingleNode("Color[@Name='Plain Text']/Background");
            var editorKey = new ThemeResourceKey(new Guid(category.Attributes["GUID"].Value), "Plain Text", ThemeResourceKeyType.BackgroundColor);
            var actualEditor = VSColorTheme.GetThemedColor(editorKey);
            string expectedEditor = plain.Attributes["Source"].Value.ToUpperInvariant(); string actualEditorArgb = actualEditor.ToArgb().ToString("X8", CultureInfo.InvariantCulture);
            check("kakao-AREA actual editor background equals fixed definition", actualEditorArgb == expectedEditor);
            check("kakao-AREA actual editor and tool window backgrounds differ", actualEditor.ToArgb() != colors["ToolWindowBackground"].ToArgb());
            editor = new { EditorArgb = actualEditorArgb, ToolWindowArgb = colors["ToolWindowBackground"].ToArgb().ToString("X8"), DistinctRegions = actualEditor.ToArgb() != colors["ToolWindowBackground"].ToArgb() };
        }
        object expandedResult = theme == "dracula" ? ReadExpandedPalette(document, check) : null;
        return new { Theme = theme, ActualHostPaletteRead = true, PhysicalHoverPerformed = false, PhysicalVisibleStateVerified = false, Colors = rows, ContrastPairs = contrasts, EditorAndToolWindow = editor, ContextMenu = menu, ExpandedHoverAndMarkers = expandedResult };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadExpandedPalette
    /// desc         : 최종 테마의 공통 hover·디버그·오류 표식 리소스를 실제 호스트에서 조회하고 대비를 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK069 카테고리 GUID와 채널을 원문 정의에서 사용, 에디터의 실제 표식 소비는 별도 한계.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object ReadExpandedPalette(XmlDocument document, Action<string, bool> check)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var colors = new Dictionary<string, Color>(); var rows = new List<object>(); var contrasts = new List<object>();
        string[][] tokens = {
            new[] { "Environment", "CommandBarHover", "Background" }, new[] { "Environment", "CommandBarTextHover", "Background" },
            new[] { "Environment", "CommandBarHoverOverSelected", "Background" }, new[] { "Environment", "CommandBarTextHoverOverSelected", "Background" },
            new[] { "Environment", "CommandBarMouseOverBackgroundBegin", "Background" }, new[] { "Environment", "CommandBarMouseOverBackgroundMiddle1", "Background" },
            new[] { "Environment", "CommandBarMouseOverBackgroundMiddle2", "Background" }, new[] { "Environment", "CommandBarMouseOverBackgroundEnd", "Background" },
            new[] { "Environment", "FileTabHotGradientTop", "Background" }, new[] { "Environment", "FileTabHotText", "Background" },
            new[] { "Environment", "FileTabHotGradientBottom", "Background" },
            new[] { "Environment", "ToolWindowTabMouseOverBackgroundBegin", "Background" }, new[] { "Environment", "ToolWindowTabMouseOverText", "Background" },
            new[] { "Environment", "ToolWindowTabMouseOverBackgroundEnd", "Background" },
            new[] { "Environment", "AutoHideTabMouseOverBackgroundBegin", "Background" }, new[] { "Environment", "AutoHideTabMouseOverText", "Background" },
            new[] { "Environment", "AutoHideTabMouseOverBackgroundEnd", "Background" },
            new[] { "CommonControls", "ButtonHover", "Background" }, new[] { "CommonControls", "ButtonHover", "Foreground" },
            new[] { "CommonControls", "ButtonBorderHover", "Background" },
            new[] { "CommonControls", "ComboBoxListItemBackgroundHover", "Background" }, new[] { "CommonControls", "ComboBoxListItemTextHover", "Background" },
            new[] { "CommonControls", "ComboBoxListItemBorderHover", "Background" },
            new[] { "Text Editor Text Manager Items", "Indicator Margin", "Background" },
            new[] { "ColorizedSignatureHelp colors", "OverviewMarginBackground", "Background" },
            new[] { "ColorizedSignatureHelp colors", "OverviewMarginVisible", "Background" }, new[] { "ColorizedSignatureHelp colors", "OverviewMarginVisible", "Foreground" },
            new[] { "ColorizedSignatureHelp colors", "MarkerFormatDefinition/BreakpointInScrollBar", "Background" },
            new[] { "Text Editor Text Marker Items", "Breakpoint (Enabled)", "Background" }, new[] { "Text Editor Text Marker Items", "Breakpoint (Enabled)", "Foreground" },
            new[] { "Text Editor Text Marker Items", "Breakpoint (Disabled)", "Foreground" },
            new[] { "Text Editor Text Marker Items", "compiler error", "Foreground" }, new[] { "Text Editor Text Marker Items", "syntax error", "Foreground" },
            new[] { "Text Editor Text Marker Items", "other error", "Foreground" }, new[] { "Text Editor Text Marker Items", "compiler warning", "Foreground" }
        };
        foreach (var token in tokens)
        {
            var category = document.SelectSingleNode("//Category[@Name='" + token[0] + "']");
            var definition = category == null ? null : category.SelectSingleNode("Color[@Name='" + token[1] + "']/" + token[2]);
            if (definition == null || definition.Attributes["Type"].Value != "CT_RAW") throw new InvalidDataException("Expanded raw theme token missing: " + String.Join("/", token));
            var key = new ThemeResourceKey(new Guid(category.Attributes["GUID"].Value), token[1], token[2] == "Foreground" ? ThemeResourceKeyType.ForegroundColor : ThemeResourceKeyType.BackgroundColor);
            var actual = VSColorTheme.GetThemedColor(key);
            string id = String.Join("/", token), actualArgb = actual.ToArgb().ToString("X8", CultureInfo.InvariantCulture), expected = definition.Attributes["Source"].Value.ToUpperInvariant();
            colors.Add(id, actual); rows.Add(new { Category = token[0], Name = token[1], Channel = token[2], ActualArgb = actualArgb, ExpectedArgb = expected });
            check("dracula-EXPANDED actual " + id + " equals fixed definition", actualArgb == expected);
        }
        string[][] pairs = {
            new[] { "Environment/CommandBarTextHover/Background", "Environment/CommandBarHover/Background", "4.5" },
            new[] { "Environment/CommandBarTextHoverOverSelected/Background", "Environment/CommandBarHoverOverSelected/Background", "4.5" },
            new[] { "Environment/FileTabHotText/Background", "Environment/FileTabHotGradientTop/Background", "4.5" },
            new[] { "Environment/FileTabHotText/Background", "Environment/FileTabHotGradientBottom/Background", "4.5" },
            new[] { "Environment/ToolWindowTabMouseOverText/Background", "Environment/ToolWindowTabMouseOverBackgroundBegin/Background", "4.5" },
            new[] { "Environment/ToolWindowTabMouseOverText/Background", "Environment/ToolWindowTabMouseOverBackgroundEnd/Background", "4.5" },
            new[] { "Environment/AutoHideTabMouseOverText/Background", "Environment/AutoHideTabMouseOverBackgroundBegin/Background", "4.5" },
            new[] { "Environment/AutoHideTabMouseOverText/Background", "Environment/AutoHideTabMouseOverBackgroundEnd/Background", "4.5" },
            new[] { "CommonControls/ButtonHover/Foreground", "CommonControls/ButtonHover/Background", "4.5" },
            new[] { "CommonControls/ComboBoxListItemTextHover/Background", "CommonControls/ComboBoxListItemBackgroundHover/Background", "4.5" },
            new[] { "ColorizedSignatureHelp colors/MarkerFormatDefinition/BreakpointInScrollBar/Background", "ColorizedSignatureHelp colors/OverviewMarginBackground/Background", "3" },
            new[] { "ColorizedSignatureHelp colors/OverviewMarginVisible/Foreground", "ColorizedSignatureHelp colors/OverviewMarginVisible/Background", "3" },
            new[] { "Text Editor Text Marker Items/Breakpoint (Enabled)/Foreground", "Text Editor Text Marker Items/Breakpoint (Enabled)/Background", "4.5" },
            new[] { "Text Editor Text Marker Items/Breakpoint (Disabled)/Foreground", "Text Editor Text Manager Items/Indicator Margin/Background", "3" },
            new[] { "Text Editor Text Marker Items/compiler error/Foreground", "ColorizedSignatureHelp colors/OverviewMarginBackground/Background", "3" }
        };
        foreach (var pair in pairs)
        {
            double threshold = Double.Parse(pair[2], CultureInfo.InvariantCulture), ratio = Contrast(colors[pair[0]], colors[pair[1]]);
            contrasts.Add(new { Foreground = pair[0], Background = pair[1], Ratio = ratio, Minimum = threshold });
            check("dracula-EXPANDED contrast " + pair[0] + " meets " + pair[2], ratio >= threshold);
        }
        return new { Tokens = rows, ContrastPairs = contrasts, ActualHostPaletteRead = true, EditorMarkerConsumptionVerified = false, PhysicalHoverPerformed = false };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Contrast
    /// desc         : 실제 불투명 sRGB 색의 상대 명도로 대비 비율을 계산한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    private static double Contrast(Color left, Color right)
    {
        if (left.A != 255 || right.A != 255) throw new InvalidDataException("Contrast requires opaque actual host colors.");
        double a = Luminance(left), b = Luminance(right);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Luminance
    /// desc         : WCAG sRGB 변환과 채널 가중치를 사용한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    private static double Luminance(Color color)
    {
        Func<byte, double> linear = channel => { double value = channel / 255.0; return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4); };
        return 0.2126 * linear(color.R) + 0.7152 * linear(color.G) + 0.0722 * linear(color.B);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RegLoadAppKey
    /// desc         : Win32의 namespace 밖 application hive를 읽기 전용 handle로 연다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 기존 파일만 허용하고 모든 handle은 검사 종료 시 닫는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegLoadAppKeyW")]
    private static extern int RegLoadAppKey(string path, out IntPtr handle, int access, int options, int reserved);

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Read
    /// desc         : 두 테마 이름·명시된 색상 category·AI package 존재를 전용 IDE 구성에서 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 설치/구성 성공을 검사하며 실제 테마 전환 시각 검증과 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static object Read(string path, string configurationRoot, int expectedCategories)
    {
        if (!File.Exists(path) || Path.GetFileName(path) != "privateregistry.bin" || !Path.GetDirectoryName(path).Contains("ThemeApply64") || !configurationRoot.Contains("ThemeApply64"))
            throw new InvalidOperationException("Only an existing owned ThemeApply64 hive is allowed.");
        IntPtr pointer; int code = RegLoadAppKey(path, out pointer, 0x20019, 0, 0);
        if (code != 0) throw new Win32Exception(code, "Cannot read the owned theme configuration hive.");
        using (var handle = new SafeRegistryHandle(pointer, true))
        using (var root = RegistryKey.FromHandle(handle))
        using (var configuration = root.OpenSubKey(configurationRoot, false))
        {
            if (configuration == null) throw new InvalidOperationException("Configuration root missing: " + configurationRoot + "; top keys=" + String.Join(",", root.GetSubKeyNames()));
            var rows = new List<object>();
            string[] ids = { "{1d41c4a3-cf75-4f0a-b2ff-9a8c2d431b92}", "{8ec84919-8a34-422a-abe1-cc238ad4d4b1}" };
            string[] names = { "Grokeen Kakao Inspired", "Grokeen Dracula Inspired" };
            for (int index = 0; index < ids.Length; index++)
            using (var theme = configuration.OpenSubKey("Themes\\" + ids[index], false))
            {
                if (theme == null || Convert.ToString(theme.GetValue("Name")) != names[index]) throw new InvalidOperationException("Theme missing from actual IDE configuration: " + names[index]);
                int categories = 0; long bytes = 0;
                foreach (string name in theme.GetSubKeyNames())
                using (var category = theme.OpenSubKey(name, false))
                {
                    var data = category.GetValue("Data") as byte[];
                    if (data != null && data.Length > 16) { categories++; bytes += data.Length; }
                }
                if (categories != expectedCategories) throw new InvalidOperationException("Incomplete IDE theme data: " + names[index] + " / " + categories);
                rows.Add(new { Theme = names[index], Guid = ids[index], Categories = categories, CompiledDataBytes = bytes });
            }
            using (var ai = configuration.OpenSubKey("Packages\\{f31272f1-3188-4cca-b6e1-37c118cf7824}", false))
            {
                if (ai == null) throw new InvalidOperationException("Owned workflow probe is missing its expected AI extension registration.");
            }
            return new { Succeeded = true, Themes = rows, AiExtensionRegistered = true, ReadOnly = true, GlobalHiveMounted = false, SelectedThemeChanged = false, VisualSwitchVerified = false };
        }
    }
}
