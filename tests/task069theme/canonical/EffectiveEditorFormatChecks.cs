using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : EffectiveEditorFormatChecks
/// desc         : 자기 문서의 실제 WPF 편집기 포맷 맵과 overview 옵션을 공개 API로 읽는다.
/// author       : CodexOrig 보조 개발 055
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK069 Role17 별도 하네스이며 물리 표식 표시와 구분한다.
///                2026-10-20 CodexOrig 보조 개발 055 : timing 사본에서 primary MEF category와 지연 관찰을 구분한다.
/// </summary>
/// ---------------------------------------------------------------------
public static class EffectiveEditorFormatChecks
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Read
    /// desc         : 활성 자기 문서에서 MEF adapter와 실제 view별 포맷 맵·옵션·margin을 관찰한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : UI 상태 변경·가상 error key·태그 생성 없이 실제 채널을 비교한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static object Read(DTE dte, string ownedDocument, string definition, Action<string, bool> check)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        string fullPath = Path.GetFullPath(ownedDocument);
        if (Path.GetFileName(fullPath) != "private-xaml-baseline.xaml" ||
            !fullPath.Contains(Path.Combine("task069theme", "canonical")) ||
            !String.Equals(dte.ActiveDocument.FullName, fullPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the active owned effective-map fixture document is allowed.");

        var componentType = Api("Microsoft.VisualStudio.ComponentModelHost.IComponentModel", "Microsoft.VisualStudio.ComponentModelHost");
        var component = Package.GetGlobalService(Api("Microsoft.VisualStudio.ComponentModelHost.SComponentModel", "Microsoft.VisualStudio.ComponentModelHost"));
        if (component == null) throw new InvalidOperationException("Actual component model service is unavailable.");
        var adaptersType = Api("Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService", "Microsoft.VisualStudio.Editor");
        var adapters = Mef(componentType, component, adaptersType);
        var managerType = Api("Microsoft.VisualStudio.TextManager.Interop.IVsTextManager", "Microsoft.VisualStudio.TextManager.Interop");
        var manager = Package.GetGlobalService(Api("Microsoft.VisualStudio.TextManager.Interop.SVsTextManager", "Microsoft.VisualStudio.TextManager.Interop"));
        var activeArguments = new object[] { 0, null, null };
        int hr = (int)managerType.GetMethod("GetActiveView").Invoke(manager, activeArguments);
        if (hr < 0) System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(hr);
        if (activeArguments[2] == null) throw new InvalidOperationException("Actual active text view is unavailable.");
        var nativeViewType = Api("Microsoft.VisualStudio.TextManager.Interop.IVsTextView", "Microsoft.VisualStudio.TextManager.Interop");
        var host = adaptersType.GetMethod("GetWpfTextViewHost", new[] { nativeViewType }).Invoke(adapters, new[] { activeArguments[2] });
        if (host == null) throw new InvalidOperationException("Actual active editor has no WPF text view host.");
        var hostType = Api("Microsoft.VisualStudio.Text.Editor.IWpfTextViewHost", "Microsoft.VisualStudio.Text.UI.Wpf");
        var view = hostType.GetProperty("TextView").GetValue(host, null);
        var viewType = Api("Microsoft.VisualStudio.Text.Editor.ITextView", "Microsoft.VisualStudio.Text.UI");
        var wpfViewType = Api("Microsoft.VisualStudio.Text.Editor.IWpfTextView", "Microsoft.VisualStudio.Text.UI.Wpf");
        var background = ReadColor(wpfViewType.GetProperty("Background").GetValue(view, null));
        check("EFFECTIVE actual editor background is an opaque solid color", background.HasValue && background.Value.A == 255);
        var options = viewType.GetProperty("Options").GetValue(view, null);
        var optionsType = Api("Microsoft.VisualStudio.Text.Editor.IEditorOptions", "Microsoft.VisualStudio.Text.Logic");
        var optionGetter = optionsType.GetMethods().Single(m => m.Name == "GetOptionValue" && !m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
        var formatServiceType = Api("Microsoft.VisualStudio.Text.Classification.IEditorFormatMapService", "Microsoft.VisualStudio.Text.UI.Wpf");
        var formatService = Mef(componentType, component, formatServiceType);
        var formatMap = formatServiceType.GetMethod("GetEditorFormatMap", new[] { viewType }).Invoke(formatService, new[] { view });
        var formatType = Api("Microsoft.VisualStudio.Text.Classification.IEditorFormatMap", "Microsoft.VisualStudio.Text.UI.Wpf");
        var getProperties = formatType.GetMethod("GetProperties", new[] { typeof(string) });
        var xml = new XmlDocument { XmlResolver = null }; xml.Load(definition);
        var rows = new List<object>();
        foreach (string key in new[] { "syntax error", "compiler error", "other error", "compiler warning", "suggestion", "hinted suggestion", "MarkerFormatDefinition/BreakpointInScrollBar" })
        {
            bool breakpoint = key == "MarkerFormatDefinition/BreakpointInScrollBar";
            bool coreError = key == "syntax error" || key == "compiler error" || key == "other error" || key == "compiler warning";
            bool required = coreError || breakpoint;
            string channel = breakpoint ? "Background" : "Foreground";
            var properties = (ResourceDictionary)getProperties.Invoke(formatMap, new object[] { key });
            object property = properties.Contains(channel) ? properties[channel] : null;
            Color? color = ReadColor(property);
            var brush = property as SolidColorBrush;
            bool brushCompatible = brush != null && brush.Opacity == 1.0;
            XmlNode expected = null;
            foreach (XmlNode category in xml.SelectNodes("//Category"))
            {
                if (String.Equals(category.Attributes["GUID"].Value.Trim('{', '}'), "75a05685-00a8-4ded-bae5-e7a50bfa929a", StringComparison.OrdinalIgnoreCase))
                    expected = category.SelectSingleNode("Color[@Name='" + key + "']/" + channel + "[@Type='CT_RAW']");
            }
            string expectedArgb = expected == null ? null : expected.Attributes["Source"].Value.ToUpperInvariant();
            string actualArgb = color.HasValue ? color.Value.ToString(CultureInfo.InvariantCulture).ToUpperInvariant() : null;
            if (actualArgb != null) actualArgb = actualArgb.TrimStart('#');
            double? contrast = color.HasValue && background.HasValue ? (double?)Contrast(color.Value, background.Value) : null;
            rows.Add(new { Key = key, Channel = channel, ValueType = property == null ? null : property.GetType().FullName,
                ActualArgb = actualArgb, ExpectedRawArgb = expectedArgb, ExactDefinitionAvailable = expectedArgb != null, ExpectedCategoryGuid = "75A05685-00A8-4DED-BAE5-E7A50BFA929A", ObservationOnly = !required,
                ContrastAgainstActualEditorBackground = contrast,
                MarginBrushConsumerCompatible = brushCompatible,
                BrushOpacity = brush == null ? (double?)null : brush.Opacity,
                BrushTransformIdentity = brush == null ? (bool?)null : brush.Transform == null || brush.Transform.Value.IsIdentity,
                BrushRelativeTransformIdentity = brush == null ? (bool?)null : brush.RelativeTransform == null || brush.RelativeTransform.Value.IsIdentity,
                PropertyKeys = properties.Keys.Cast<object>().Select(k => Convert.ToString(k, CultureInfo.InvariantCulture)).OrderBy(k => k).ToArray() });
            if (required)
            {
                check("EFFECTIVE channel exists " + key + "/" + channel, color.HasValue && color.Value.A == 255);
                check("EFFECTIVE margin consumer receives an opaque solid Brush " + key + "/" + channel, brushCompatible);
                check("EFFECTIVE exact MEF category compiled definition " + key + "/" + channel, expectedArgb != null && actualArgb == expectedArgb);
                check("EFFECTIVE actual channel contrasts with editor background " + key, contrast.HasValue && contrast.Value >= 3.0);
            }
        }
        var optionRows = new List<object>();
        foreach (string key in new[] { "OverviewMargin/ShowErrors", "OverviewMargin/ErrorMarginWidth", "OverviewMargin/ShowMarks", "OverviewMargin/MarkMarginWidth" })
        {
            var value = optionGetter.Invoke(options, new object[] { key });
            bool show = key.EndsWith("ShowErrors", StringComparison.Ordinal) || key.EndsWith("ShowMarks", StringComparison.Ordinal);
            bool usable = show ? value is bool && (bool)value : value != null && Convert.ToDouble(value, CultureInfo.InvariantCulture) > 0;
            optionRows.Add(new { Key = key, Value = value, ValueType = value == null ? null : value.GetType().FullName });
            check("EFFECTIVE overview option enabled or positive " + key, usable);
        }
        var margins = new List<object>();
        var marginType = Api("Microsoft.VisualStudio.Text.Editor.ITextViewMargin", "Microsoft.VisualStudio.Text.UI");
        var wpfMarginType = Api("Microsoft.VisualStudio.Text.Editor.IWpfTextViewMargin", "Microsoft.VisualStudio.Text.UI.Wpf");
        foreach (string name in new[] { "OverviewErrorMargin", "OverviewMarkMargin" })
        {
            var margin = hostType.GetMethod("GetTextViewMargin").Invoke(host, new object[] { name });
            bool enabled = margin != null && (bool)marginType.GetProperty("Enabled").GetValue(margin, null);
            double size = margin == null ? 0 : (double)marginType.GetProperty("MarginSize").GetValue(margin, null);
            var element = margin == null ? null : (FrameworkElement)wpfMarginType.GetProperty("VisualElement").GetValue(margin, null);
            margins.Add(new { Name = name, Present = margin != null, Enabled = enabled, MarginSize = size,
                Visibility = element == null ? null : element.Visibility.ToString(), ActualWidth = element == null ? 0 : element.ActualWidth });
            check("EFFECTIVE actual margin registered and enabled " + name, margin != null && enabled && size > 0);
        }
        check("EFFECTIVE active document remains owned fixture", String.Equals(dte.ActiveDocument.FullName, fullPath, StringComparison.OrdinalIgnoreCase));
        return new { ActualEditorFormatMapRead = true, ActualViewOptionsRead = true, ActualMarginRead = true,
            GatingScope = "Four canonical error/warning keys and breakpoint; suggestion/hinted suggestion are observed without imposing nontransparent defaults",
            ActualErrorTagsCreated = false, ActualBreakpointTagsCreated = false, PhysicalMarkerVisibilityVerified = false,
            ActualEditorBackground = background.HasValue ? background.Value.ToString(CultureInfo.InvariantCulture) : null,
            FormatMap = rows, Options = optionRows, Margins = margins };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Api
    /// desc         : 공식 SDK Reference의 컴파일된 전체 assembly identity로 공개 API 타입을 가져온다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : VS2017 단순 이름 Load 실패를 private helper의 공식 Reference로 보완한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static Type Api(string name, string assembly)
    {
        Assembly official;
        switch (assembly)
        {
            case "Microsoft.VisualStudio.Editor": official = typeof(Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService).Assembly; break;
            case "Microsoft.VisualStudio.ComponentModelHost": official = typeof(Microsoft.VisualStudio.ComponentModelHost.IComponentModel).Assembly; break;
            case "Microsoft.VisualStudio.Text.UI.Wpf": official = typeof(Microsoft.VisualStudio.Text.Editor.IWpfTextView).Assembly; break;
            case "Microsoft.VisualStudio.Text.UI": official = typeof(Microsoft.VisualStudio.Text.Editor.ITextView).Assembly; break;
            case "Microsoft.VisualStudio.Text.Logic": official = typeof(Microsoft.VisualStudio.Text.Editor.IEditorOptions).Assembly; break;
            case "Microsoft.VisualStudio.TextManager.Interop": official = typeof(Microsoft.VisualStudio.TextManager.Interop.IVsTextManager).Assembly; break;
            default: throw new InvalidOperationException("Unexpected editor API assembly identity.");
        }
        return official.GetType(name, true, false);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Mef
    /// desc         : 실제 component model의 공개 generic GetService만 호출한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Mef(Type componentType, object component, Type service)
    {
        var method = componentType.GetMethods().Single(m => m.Name == "GetService" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
        var value = method.MakeGenericMethod(service).Invoke(component, null);
        if (value == null) throw new InvalidOperationException("Actual MEF service unavailable: " + service.FullName);
        return value;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadColor
    /// desc         : 실제 Foreground/Background 채널의 Color 또는 SolidColorBrush만 인정한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    private static Color? ReadColor(object value)
    {
        if (value is Color) return (Color)value;
        var brush = value as SolidColorBrush;
        return brush == null ? (Color?)null : brush.Color;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Contrast
    /// desc         : 실제 opaque 채널과 자기 편집기 배경의 WCAG sRGB 대비를 계산한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    private static double Contrast(Color left, Color right)
    {
        if (left.A != 255 || right.A != 255) return 0;
        Func<byte, double> linear = channel => { double value = channel / 255.0; return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4); };
        Func<Color, double> luminance = value => 0.2126 * linear(value.R) + 0.7152 * linear(value.G) + 0.0722 * linear(value.B);
        double a = luminance(left), b = luminance(right);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
}
