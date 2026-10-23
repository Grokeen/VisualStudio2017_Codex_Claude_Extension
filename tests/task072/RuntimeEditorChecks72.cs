using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : RuntimeEditorChecks72
/// desc         : 전용 IDE의 실제 비교 버퍼·C# 분류·언어 서비스 오류와 테마 소비자를 검사한다.
/// author       : CodexOrig 보조 개발 055
/// create date  : 2026-10-23
/// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 네이티브 태그와 최종 view 분류를 분리하며 가짜 오류 태그를 생성하지 않는다.
///                2026-10-23 CodexOrig 보조 개발 055 : 새 border-only 설계를 native renderer Drawing과 구분된 쓰기 scope로 검사한다.
///                2026-10-23 CodexOrig 보조 개발 055 : 실제 세 호스트 IL의 manager 필드명·정확 타입을 검증한 뒤 동일 소비 brush를 읽는다.
///                2026-10-23 CodexOrig 보조 개발 055 : buffer 분류 부재 시 own overlay를 제외한 원 view 태그를 별도 native 관측으로 기록한다.
/// </summary>
/// ---------------------------------------------------------------------
public static class RuntimeEditorChecks72
{
    private const string Block = "Grokeen.Dracula.BlockComment";
    private const string Error = "Grokeen.Dracula.ErrorHighlight";

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunAsync
    /// desc         : 자기 익명 프로젝트와 비교 view를 열어 실제 MEF 결과만 기록하고 소유 자원을 정리한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 오류 생성은 최대 15초 기다리며 사용자 프로필·포맷 맵·옵션을 쓰지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static async System.Threading.Tasks.Task<object> RunAsync(DTE dte, string output, Action<string, bool> check, Func<bool> corePassing, Func<string, Task> switchTheme)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        string root = Path.GetFullPath(output);
        if (!root.Contains(Path.Combine("task072", "runtime")) || !dte.RegistryRoot.Contains("ThemeApply72"))
            throw new InvalidOperationException("Only the dedicated TASK072 profile and artifact directory are allowed.");
        PrepareOwnedEmptySolution(dte, root);
        var component = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
        if (component == null) throw new InvalidOperationException("Actual component model is unavailable.");
        var themeGate = ReadThemeGate(check);
        File.WriteAllText(Path.Combine(root, "task072-current-theme-gate.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(themeGate));
        object difference;
        try { difference = await ReadDifferenceAsync(component, root, check); }
        catch (Exception exception)
        {
            check("DIFF72 native difference stage completed without exception", false);
            difference = new { Error = exception.ToString(), ActualNativeDifferenceCompleted = false };
        }
        File.WriteAllText(Path.Combine(root, "task072-native-difference-runtime.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(difference));
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        string folder = Path.Combine(root, "NativeCSharp72");
        Directory.CreateDirectory(folder);
        string source = "namespace Runtime72\r\n{\r\n    /// <summary>XML_DOC_72</summary>\r\n    public class Sample\r\n    {\r\n        // LINE_COMMENT_72\r\n        /* BLOCK_COMMENT_72 */\r\n        /* MULTI_START_72\r\n           MULTI_INTERIOR_72\r\n        */\r\n        /* */\r\n        /** <summary>XML_BLOCK_72</summary> */\r\n        public string Token = \"/* STRING_TOKEN_72 */\";\r\n        public void M() { int stable = 1; int broken = ; }\r\n    }\r\n}\r\n";
        string document = Path.Combine(folder, "Probe.cs");
        File.WriteAllText(document, source);
        string project = Path.Combine(folder, "NativeCSharp72.csproj");
        File.WriteAllText(project, "<Project ToolsVersion=\"15.0\" DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"><PropertyGroup><ProjectGuid>{B2565F28-A782-4E9E-94F7-CF7F08A3DA72}</ProjectGuid><OutputType>Library</OutputType><TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion><AssemblyName>NativeCSharp72</AssemblyName><RootNamespace>Runtime72</RootNamespace><OutputPath>bin\\</OutputPath></PropertyGroup><ItemGroup><Reference Include=\"System\"/><Compile Include=\"Probe.cs\"/></ItemGroup><Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\"/></Project>");
        string solution = Path.Combine(folder, "NativeCSharp72.sln");
        File.WriteAllText(solution, "Microsoft Visual Studio Solution File, Format Version 12.00\r\n# Visual Studio 15\r\nVisualStudioVersion = 15.0.28307.168\r\nMinimumVisualStudioVersion = 10.0.40219.1\r\nProject(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"NativeCSharp72\", \"NativeCSharp72.csproj\", \"{B2565F28-A782-4E9E-94F7-CF7F08A3DA72}\"\r\nEndProject\r\nGlobal\r\n\tGlobalSection(SolutionConfigurationPlatforms) = preSolution\r\n\t\tDebug|Any CPU = Debug|Any CPU\r\n\tEndGlobalSection\r\n\tGlobalSection(ProjectConfigurationPlatforms) = postSolution\r\n\t\t{B2565F28-A782-4E9E-94F7-CF7F08A3DA72}.Debug|Any CPU.ActiveCfg = Debug|AnyCPU\r\n\t\t{B2565F28-A782-4E9E-94F7-CF7F08A3DA72}.Debug|Any CPU.Build.0 = Debug|AnyCPU\r\n\tEndGlobalSection\r\nEndGlobal\r\n");
        EnvDTE.Window window = null;
        ITagAggregator<IErrorTag> errors = null;
        ITagAggregator<ITextMarkerTag> markers = null;
        ITagAggregator<IClassificationTag> classifications = null;
        ITagAggregator<IClassificationTag> nativeClassificationTags = null;
        try
        {
            dte.Solution.Open(solution);
            window = dte.ItemOperations.OpenFile(document, EnvDTE.Constants.vsViewKindTextView);
            window.Activate();
            var manager = (IVsTextManager)Package.GetGlobalService(typeof(SVsTextManager));
            IVsTextView native;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(manager.GetActiveView(0, null, out native));
            var host = component.GetService<IVsEditorAdaptersFactoryService>().GetWpfTextViewHost(native);
            if (host == null || !String.Equals(dte.ActiveDocument.FullName, document, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The owned native C# editor must be the actual active view.");
            var view = host.TextView;
            check("NATIVE72 actual CSharp content type", view.TextBuffer.ContentType.IsOfType("CSharp"));
            var tags = component.GetService<IViewTagAggregatorFactoryService>();
            errors = tags.CreateTagAggregator<IErrorTag>(view);
            markers = tags.CreateTagAggregator<ITextMarkerTag>(view);
            classifications = tags.CreateTagAggregator<IClassificationTag>(view);
            nativeClassificationTags = component.GetService<IBufferTagAggregatorFactoryService>().CreateTagAggregator<IClassificationTag>(view.TextBuffer);
            var classifier = component.GetService<IViewClassifierAggregatorService>().GetClassifier(view);
            var bufferClassifier = component.GetService<IClassifierAggregatorService>().GetClassifier(view.TextBuffer);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var attempts = new List<object>();
            while (true)
            {
                var snapshot = view.TextSnapshot;
                var span = new SnapshotSpan(snapshot, 0, snapshot.Length);
                int errorCount = errors.GetTags(span).Count(t => IsError(t.Tag.ErrorType) && t.Span.GetSpans(snapshot).Any(s => s.Start.GetContainingLine().GetText().Contains("broken")));
                int markerCount = markers.GetTags(span).Count(t => t.Tag.Type == Error);
                int blockCount = classifications.GetTags(span).Count(t => t.Tag.ClassificationType.IsOfType(Block));
                attempts.Add(new { ElapsedMilliseconds = clock.Elapsed.TotalMilliseconds, NativeErrors = errorCount, CustomErrorMarkers = markerCount, CustomBlockTags = blockCount });
                File.WriteAllText(Path.Combine(root, "task072-native-error-polls.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(attempts));
                if ((errorCount > 0 && markerCount > 0 && blockCount > 0) || clock.ElapsedMilliseconds >= 15000) break;
                await Task.Delay(100);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }
            clock.Stop();
            var finalSnapshot = view.TextSnapshot;
            var all = new SnapshotSpan(finalSnapshot, 0, finalSnapshot.Length);
            var nativeErrorSpans = errors.GetTags(all).SelectMany(t => t.Span.GetSpans(finalSnapshot).Select(s => new { Type = t.Tag.ErrorType, Span = s })).ToArray();
            var markerSpans = markers.GetTags(all).Where(t => t.Tag.Type == Error).SelectMany(t => t.Span.GetSpans(finalSnapshot)).ToArray();
            var actualErrors = nativeErrorSpans.Where(t => IsError(t.Type) && t.Span.Start.GetContainingLine().GetText().Contains("broken")).ToArray();
            check("NATIVE72 Roslyn generated a real error on the invalid CSharp line", actualErrors.Length > 0);
            check("NATIVE72 custom marker overlaps a native error span", actualErrors.Any(e => markerSpans.Any(m => m.IntersectsWith(e.Span))));
            var format = component.GetService<IClassificationFormatMapService>().GetClassificationFormatMap(view);
            var sampleRows = new List<object>();
            foreach (var sample in new[] {
                new { Text = "LINE_COMMENT_72", Expected = "FFA0A6B5", Custom = false },
                new { Text = "BLOCK_COMMENT_72", Expected = "FFB5BDD4", Custom = true },
                new { Text = "XML_DOC_72", Expected = "FFC8D5F2", Custom = false },
                new { Text = "summary", Expected = "FFB9C8E8", Custom = false } })
            {
                var span = new SnapshotSpan(finalSnapshot, source.IndexOf(sample.Text, StringComparison.Ordinal), sample.Text.Length);
                var final = classifier.GetClassificationSpans(span).ToArray();
                var nativeObservation = ReadNativeClassifications(span, bufferClassifier, nativeClassificationTags, classifications);
                var nativeClassifications = nativeObservation.Classifications;
                var finalColors = final.Select(c => Argb(format.GetTextProperties(c.ClassificationType).ForegroundBrush)).ToArray();
                bool custom = classifications.GetTags(span).Any(t => t.Tag.ClassificationType.IsOfType(Block));
                check("NATIVE72 final view classification exists " + sample.Text, final.Length > 0);
                check("NATIVE72 final view foreground matches " + sample.Text, finalColors.Length > 0 && finalColors.All(c => c == sample.Expected));
                check("NATIVE72 block overlay is scoped " + sample.Text, custom == sample.Custom);
                check("NATIVE72 buffer classifier does not consume its view overlay " + sample.Text, !nativeObservation.BufferClassifications.Any(c => c.ClassificationType.IsOfType(Block)));
                sampleRows.Add(new { Sample = sample.Text, Expected = sample.Expected, FinalClassifications = final.Select(c => c.ClassificationType.Classification), Foregrounds = finalColors,
                    NativeClassifications = nativeClassifications.Select(c => c.ClassificationType.Classification), NativeClassificationSource = nativeObservation.Source,
                    NativeBufferClassifications = nativeObservation.BufferClassifications.Select(c => c.ClassificationType.Classification),
                    NativeBufferFullSpans = nativeObservation.BufferFullSpans, NativeViewRawSpans = nativeObservation.ViewRawSpans, CustomBlockTag = custom });
            }
            foreach (var sample in new[] {
                new { Text = "MULTI_START_72", Rule = "block" }, new { Text = "MULTI_INTERIOR_72", Rule = "block-interior" },
                new { Text = "/* */", Rule = "empty-block" }, new { Text = "XML_BLOCK_72", Rule = "native-doc-policy" },
                new { Text = "STRING_TOKEN_72", Rule = "string-preserve" } })
            {
                var span = new SnapshotSpan(finalSnapshot, source.IndexOf(sample.Text, StringComparison.Ordinal), sample.Text.Length);
                var final = classifier.GetClassificationSpans(span).ToArray();
                var nativeObservation = ReadNativeClassifications(span, bufferClassifier, nativeClassificationTags, classifications);
                var nativeClassifications = nativeObservation.Classifications;
                var nativeSpans = nativeObservation.FullSpans;
                bool xml = nativeClassifications.Any(c => c.ClassificationType.Classification.StartsWith("xml doc comment", StringComparison.Ordinal));
                bool plainBlock = sample.Rule != "string-preserve" && !(sample.Rule == "native-doc-policy" && xml);
                bool custom = classifications.GetTags(span).Any(t => t.Tag.ClassificationType.IsOfType(Block));
                var finalColors = final.Select(c => Argb(format.GetTextProperties(c.ClassificationType).ForegroundBrush)).ToArray();
                var nativeColors = nativeClassifications.Select(c => Argb(format.GetTextProperties(c.ClassificationType).ForegroundBrush)).Distinct().ToArray();
                check("NATIVE72 additional sample has actual native and final classifications " + sample.Text, nativeClassifications.Length > 0 && final.Length > 0);
                check("NATIVE72 additional block versus doc/string scope " + sample.Text, custom == plainBlock);
                check("NATIVE72 additional effective color preserves its native policy " + sample.Text,
                    finalColors.Length > 0 && (plainBlock ? finalColors.All(c => c == "FFB5BDD4") : finalColors.All(c => nativeColors.Contains(c))));
                if (sample.Rule == "block-interior")
                    check("NATIVE72 multiline interior query retains original native block start", nativeSpans.Any(s => s.Classification == "comment" && s.Start < span.Start.Position && finalSnapshot.GetText(s.Start, Math.Min(2, s.Length)) == "/*"));
                sampleRows.Add(new { Sample = sample.Text, Rule = sample.Rule, ExpectedCustom = plainBlock, NativeXmlDocClassification = xml, QueryStart = span.Start.Position, QueryLength = span.Length,
                    NativeClassifications = nativeClassifications.Select(c => c.ClassificationType.Classification), NativeForegrounds = nativeColors, NativeFullSpans = nativeSpans,
                    NativeClassificationSource = nativeObservation.Source, NativeBufferClassifications = nativeObservation.BufferClassifications.Select(c => c.ClassificationType.Classification),
                    NativeBufferFullSpans = nativeObservation.BufferFullSpans, NativeViewRawSpans = nativeObservation.ViewRawSpans,
                    FinalClassifications = final.Select(c => c.ClassificationType.Classification), Foregrounds = finalColors, CustomBlockTag = custom });
            }
            var map = component.GetService<IEditorFormatMapService>().GetEditorFormatMap(view);
            var properties = map.GetProperties(Error);
            var border = properties.Contains(MarkerFormatDefinition.BorderId) ? properties[MarkerFormatDefinition.BorderId] as Pen : null;
            string foreground = properties.Contains("Foreground") ? Argb(properties["Foreground"]) : null;
            string background = properties.Contains("Background") ? Argb(properties["Background"]) : null;
            check("NATIVE72 marker foreground is the required pink Brush", foreground == "FFFF79C6" && properties["Foreground"] is SolidColorBrush);
            object backgroundValue = properties.Contains("Background") ? properties["Background"] : null;
            var backgroundBrush = backgroundValue as SolidColorBrush;
            bool noOpaqueFill = backgroundValue == null || (backgroundBrush != null && (backgroundBrush.Color.A == 0 || backgroundBrush.Opacity == 0));
            check("NATIVE72 border-only marker has no opaque background fill", noOpaqueFill);
            object fillValue = properties.Contains(MarkerFormatDefinition.FillId) ? properties[MarkerFormatDefinition.FillId] : null;
            var fillBrush = fillValue as SolidColorBrush;
            check("NATIVE72 border-only native FillId has no opaque fill", fillValue == null || (fillBrush != null && (fillBrush.Color.A == 0 || fillBrush.Opacity == 0)));
            check("NATIVE72 marker border is pink and one pixel", border != null && Argb(border.Brush) == "FFFF79C6" && border.Thickness == 1);
            var options = new List<object>();
            foreach (string key in new[] { "OverviewMargin/ShowErrors", "OverviewMargin/ShowChangeTracking" })
            {
                object value = view.Options.GetOptionValue(key);
                check("NATIVE72 local overview option enabled " + key, value is bool && (bool)value);
                options.Add(new { Key = key, Value = value });
            }
            var errorMargin = host.GetTextViewMargin("OverviewErrorMargin") as IWpfTextViewMargin;
            check("NATIVE72 native error overview margin is present and enabled", errorMargin != null && errorMargin.Enabled && errorMargin.MarginSize > 0);
            object rendered = actualErrors.Length > 0 && markerSpans.Length > 0 ? await RenderNativeErrorAsync(view, root, check) : null;
            object transitions = null;
            bool coreSucceeded = corePassing();
            if (coreSucceeded)
            {
                transitions = await AdditionalViewChecks72.RunAsync(dte, component, view, root, switchTheme, check);
                File.WriteAllText(Path.Combine(root, "task072-additional-view-transitions.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(transitions));
            }
            return new { CurrentThemeGate = themeGate, ActualNativeDifference = difference, ActualRoslynErrorTagsGenerated = actualErrors.Length > 0,
                ActualViewClassificationRead = true, ActualNativeErrorTagQuery = true, SyntheticTagsCreated = 0, FormatMapWrites = 0, CoreOptionWrites = 0,
                PhysicalPointerUsed = false, PhysicalPixelVisibilityVerified = false, ErrorGenerationWaitMilliseconds = clock.Elapsed.TotalMilliseconds,
                Polls = attempts, Classifications = sampleRows, NativeErrors = nativeErrorSpans.Select(e => new { e.Type, Start = e.Span.Start.Position, Length = e.Span.Length }),
                Markers = markerSpans.Select(s => new { Start = s.Start.Position, Length = s.Length }), MarkerForeground = foreground, MarkerBackground = background,
                MarkerDesign = "pink-one-pixel-border-only; no opaque fill", MarkerBackgroundValueType = backgroundValue == null ? null : backgroundValue.GetType().FullName,
                MarkerBorder = border == null ? null : new { Color = Argb(border.Brush), Width = border.Thickness }, OverviewOptions = options, OwnedViewRender = rendered,
                CoreSucceededBeforeAdditionalWrites = coreSucceeded, AdditionalViewTransitions = transitions, AdditionalScopeHasOwnViewOptionWrites = transitions != null,
                ErrorMargin = errorMargin == null ? null : new { errorMargin.Enabled, errorMargin.MarginSize, Visibility = errorMargin.VisualElement.Visibility.ToString(), errorMargin.VisualElement.ActualWidth } };
        }
        finally
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (classifications != null) classifications.Dispose();
            if (nativeClassificationTags != null) nativeClassificationTags.Dispose();
            if (markers != null) markers.Dispose();
            if (errors != null) errors.Dispose();
            if (window != null && window.Document != null) window.Document.Close(vsSaveChanges.vsSaveChangesNo);
            if (dte.Solution.IsOpen && String.Equals(dte.Solution.FullName, solution, StringComparison.OrdinalIgnoreCase)) dte.Solution.Close(false);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadDifferenceAsync
    /// desc         : 실제 difference buffer/view를 생성하여 계산된 변경 위치와 소비 포맷을 읽는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 원본·변경 문서와 비교 창은 모두 자기 fixture이며 포인터를 사용하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static async System.Threading.Tasks.Task<object> ReadDifferenceAsync(IComponentModel component, string output, Action<string, bool> check)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var buffers = component.GetService<ITextBufferFactoryService>();
        var content = component.GetService<IContentTypeRegistryService>().GetContentType("text");
        string[] lines = Enumerable.Range(0, 120).Select(i => "unchanged line " + i.ToString(CultureInfo.InvariantCulture)).ToArray();
        var changed = (string[])lines.Clone();
        changed[2] = "added top content 72"; changed[55] = "added middle content 72"; changed[115] = "added bottom content 72";
        var left = buffers.CreateTextBuffer(String.Join("\r\n", lines), content);
        var right = buffers.CreateTextBuffer(String.Join("\r\n", changed), content);
        var difference = component.GetService<IDifferenceBufferFactoryService>().CreateDifferenceBuffer(left, right);
        IWpfDifferenceViewer viewer = null;
        System.Windows.Window window = null;
        try
        {
            viewer = component.GetService<IWpfDifferenceViewerFactoryService>().CreateDifferenceView(difference, null);
            viewer.ViewMode = DifferenceViewMode.SideBySide;
            window = new System.Windows.Window { Title = "TASK072 owned native difference fixture", Width = 960, Height = 640, ShowInTaskbar = false, Content = viewer.VisualElement };
            window.Show();
            window.UpdateLayout();
            await Task.Delay(150);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while ((difference.CurrentSnapshotDifference == null || !difference.CurrentSnapshotDifference.DifferenceSpans.AddedLineSpans.Any()) && clock.ElapsedMilliseconds < 5000)
            {
                await Task.Delay(50);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }
            var snapshot = difference.CurrentSnapshotDifference;
            check("DIFF72 native difference calculation completed", snapshot != null);
            if (snapshot == null) throw new InvalidOperationException("Native difference calculation did not complete within five seconds.");
            var additions = snapshot.DifferenceSpans.AddedLineSpans.Select(s => s.GetSpan(snapshot.RightBufferSnapshot)).ToArray();
            var removals = snapshot.DifferenceSpans.RemovedLineSpans.Select(s => s.GetSpan(snapshot.LeftBufferSnapshot)).ToArray();
            foreach (int line in new[] { 2, 55, 115 })
            {
                check("DIFF72 native added location includes line " + line, additions.Any(s => s.Start.GetContainingLine().LineNumber <= line && s.End.GetContainingLine().LineNumber >= line));
                check("DIFF72 native removed location includes line " + line, removals.Any(s => s.Start.GetContainingLine().LineNumber <= line && s.End.GetContainingLine().LineNumber >= line));
            }
            var view = viewer.RightView;
            var map = component.GetService<IEditorFormatMapService>().GetEditorFormatMap(view);
            var defaultForeground = component.GetService<IClassificationFormatMapService>().GetClassificationFormatMap(view).DefaultTextProperties.ForegroundBrush;
            var colors = new List<object>();
            foreach (var pair in new[] {
                new { Key = "deltadiff.add.line", Expected = "FF243B38" }, new { Key = "deltadiff.add.word", Expected = "FF294D44" },
                new { Key = "deltadiff.remove.line", Expected = "FF452D40" }, new { Key = "deltadiff.remove.word", Expected = "FF63384C" } })
            {
                var properties = map.GetProperties(pair.Key);
                object bg = properties.Contains("Background") ? properties["Background"] : null;
                object fg = properties.Contains("Foreground") ? properties["Foreground"] : null;
                object effectiveForeground = fg ?? defaultForeground;
                string actual = Argb(bg);
                check("DIFF72 actual native map background " + pair.Key, actual == pair.Expected && bg is SolidColorBrush);
                if (pair.Key.EndsWith("word", StringComparison.Ordinal)) check("DIFF72 changed word text remains readable " + pair.Key, Argb(fg) == (pair.Key.Contains("add.") ? "FF50FA7B" : "FFFFB3D9"));
                double contrast = Contrast(effectiveForeground as SolidColorBrush, bg as SolidColorBrush);
                check("DIFF72 actual changed text contrast at least 4.5 " + pair.Key, contrast >= 4.5);
                colors.Add(new { pair.Key, ActualBackground = actual, ExpectedBackground = pair.Expected, Foreground = Argb(fg), EffectiveForeground = Argb(effectiveForeground), Contrast = contrast });
            }
            var overviewProperties = map.GetProperties("deltadiff.overview.color");
            string overview = overviewProperties.Contains("Foreground") ? Argb(overviewProperties["Foreground"]) : null;
            check("DIFF72 native overview viewport outline is purple", overview == "FFBD93F9");
            var nativeOptions = new List<object>();
            nativeOptions.Add(ReadDiffOption("DifferenceBuffer.Options", difference.Options));
            var viewerOptionsProperty = typeof(IDifferenceViewer).GetProperty("Options");
            if (viewerOptionsProperty != null) nativeOptions.Add(ReadDiffOption("DifferenceViewer.Options", (IEditorOptions)viewerOptionsProperty.GetValue(viewer, null)));
            nativeOptions.Add(ReadDiffOption("RightHost.TextView.Options", viewer.RightHost.TextView.Options));
            var displayInterface = viewer.GetType().GetInterfaces().FirstOrDefault(t => t.FullName == "Microsoft.VisualStudio.Text.Differencing.IDifferenceViewer3");
            var displayProperty = displayInterface == null ? null : displayInterface.GetProperty("DisplayDifferences");
            object displayDifferences = displayProperty == null ? null : displayProperty.GetValue(viewer, null);
            var marginRows = new List<object>();
            check("DIFF72 actual viewer mode remains SideBySide", viewer.ViewMode == DifferenceViewMode.SideBySide);
            var hosts = new[] { viewer.LeftHost, viewer.RightHost, viewer.InlineHost };
            var roles = new[] { "LeftHost", "RightHost", "InlineHost" };
            for (int hostIndex = 0; hostIndex < hosts.Length; hostIndex++)
            {
                var host = hosts[hostIndex];
                if (host == null) continue;
                var margin = host.GetTextViewMargin("deltadifferenceViewerOverview") as IWpfTextViewMargin;
                marginRows.Add(new { Name = "deltadifferenceViewerOverview", Role = roles[hostIndex], Host = host.GetType().FullName, Present = margin != null, Enabled = margin != null && margin.Enabled,
                    Visibility = margin == null ? null : margin.VisualElement.Visibility.ToString(), IsVisible = margin != null && margin.VisualElement.IsVisible, IsLoaded = margin != null && margin.VisualElement.IsLoaded,
                    Width = margin == null ? 0 : margin.VisualElement.ActualWidth, Height = margin == null ? 0 : margin.VisualElement.ActualHeight, Size = margin == null ? 0 : margin.MarginSize });
            }
            var differenceMargin = viewer.RightHost.GetTextViewMargin("deltadifferenceViewerOverview") as IWpfTextViewMargin;
            check("DIFF72 exact RightHost native overview margin is visible loaded and laid out", differenceMargin != null && differenceMargin.Enabled && differenceMargin.VisualElement.IsVisible && differenceMargin.VisualElement.IsLoaded && differenceMargin.VisualElement.ActualWidth > 0 && differenceMargin.VisualElement.ActualHeight > 0);
            object rendered = RenderOwnedElement(viewer.VisualElement, Path.Combine(output, "native-difference-owned-view.png"));
            object actualConsumer = null;
            if (differenceMargin != null)
            {
                var managerFields = new[] { "brushManager", "_brushManager" }
                    .Select(name => differenceMargin.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                    .Where(candidate => candidate != null).ToArray();
                if (managerFields.Length != 1)
                    throw new InvalidOperationException("The native overview must expose exactly one primary-IL verified manager field.");
                var field = managerFields[0];
                object manager = field.GetValue(differenceMargin);
                string managerType = manager == null ? null : manager.GetType().FullName;
                bool knownManager = managerType == "Microsoft.VisualStudio.Text.Utilities.DifferenceBrushManager"
                    || (field.Name == "brushManager" && managerType == "Microsoft.VisualStudio.Text.Differencing.Implementation.DifferenceBrushManager");
                if (!knownManager || field.FieldType.FullName != managerType)
                    throw new InvalidOperationException("The native overview manager type differs from the installed primary-IL contract.");
                Func<string, object> read = name => manager == null ? null : manager.GetType().GetProperty(name).GetValue(manager, null);
                object addBrush = read("AddedWordForegroundBrush"), removeBrush = read("RemovedWordForegroundBrush"), overviewBrush = read("OverviewBrush");
                var pen = read("ViewportPen") as Pen;
                check("DIFF72 actual overview consumer uses green and pink changed word foreground brushes", Argb(addBrush) == "FF50FA7B" && Argb(removeBrush) == "FFFFB3D9");
                check("DIFF72 actual overview consumer uses compiled background and purple viewport pen", Argb(overviewBrush) == "FF282A36" && pen != null && Argb(pen.Brush) == "FFBD93F9");
                check("DIFF72 actual added marker contrasts with overview background", Contrast(addBrush as SolidColorBrush, overviewBrush as SolidColorBrush) >= 3);
                check("DIFF72 actual removed marker contrasts with overview background", Contrast(removeBrush as SolidColorBrush, overviewBrush as SolidColorBrush) >= 3);
                differenceMargin.VisualElement.UpdateLayout();
                var drawings = new List<object>();
                var markerPositions = new List<double>();
                var drawingClock = System.Diagnostics.Stopwatch.StartNew();
                while (true)
                {
                    drawings.Clear(); markerPositions.Clear();
                    RecordVisualDrawings(differenceMargin.VisualElement, drawings, markerPositions);
                    if (markerPositions.Distinct().Count() >= 3 || drawingClock.ElapsedMilliseconds >= 5000) break;
                    await Task.Delay(50);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                }
                check("DIFF72 native overview draws separated changed locations", markerPositions.Distinct().Count() >= 3);
                actualConsumer = new { ManagerType = managerType, ManagerField = field.Name, ManagerFieldContractVerified = knownManager, AddedMarker = Argb(addBrush), RemovedMarker = Argb(removeBrush),
                    OverviewBackground = Argb(overviewBrush), ViewportOutline = pen == null ? null : Argb(pen.Brush), DrawingObservations = drawings,
                    SelectedHost = "RightHost", DrawingCapturedAfterOwnedRender = true, DrawingCaptureMilliseconds = drawingClock.Elapsed.TotalMilliseconds,
                    SelectedVisualType = differenceMargin.VisualElement.GetType().FullName, DistinctChangedLocationY = markerPositions.Distinct().OrderBy(y => y).ToArray() };
            }
            return new { NativeService = difference.GetType().FullName, NativeViewer = viewer.GetType().FullName, SyntheticDifferenceTagsCreated = 0,
                AddedLines = additions.Select(s => new { StartLine = s.Start.GetContainingLine().LineNumber, EndLine = s.End.GetContainingLine().LineNumber }),
                RemovedLines = removals.Select(s => new { StartLine = s.Start.GetContainingLine().LineNumber, EndLine = s.End.GetContainingLine().LineNumber }),
                AddedWordSpanCount = snapshot.DifferenceSpans.AddedWordSpans.Count(), RemovedWordSpanCount = snapshot.DifferenceSpans.RemovedWordSpans.Count(),
                Colors = colors, OverviewViewportForeground = overview, NativeOptionObservations = nativeOptions, DisplayDifferences = displayDifferences,
                RightViewRoles = new { Diff = viewer.RightView.Roles.Contains("DIFF"), Document = viewer.RightView.Roles.Contains(PredefinedTextViewRoles.Document), Interactive = viewer.RightView.Roles.Contains(PredefinedTextViewRoles.Interactive) },
                MarginObservations = marginRows, NativeOverviewConsumer = actualConsumer,
                OwnedViewRender = rendered, PhysicalPixelVisibilityVerified = false };
        }
        finally
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (window != null) { window.Content = null; window.Close(); }
            if (viewer != null && !viewer.IsClosed) viewer.Close();
            difference.Dispose();
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadNativeClassifications
    /// desc         : 최종 병합 분류와 구분된 native buffer 및 own block 이외의 원 view 태그를 보존한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : VS2022의 실제 view-only Roslyn 경로는 buffer 부재를 숨기지 않고 기록한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static NativeClassificationObservation72 ReadNativeClassifications(SnapshotSpan query, IClassifier bufferClassifier,
        ITagAggregator<IClassificationTag> bufferTags, ITagAggregator<IClassificationTag> viewTags)
    {
        var buffer = bufferClassifier.GetClassificationSpans(query).ToArray();
        var bufferRaw = bufferTags.GetTags(query).SelectMany(tag => tag.Span.GetSpans(query.Snapshot)
            .Select(span => new ClassificationSpan(span, tag.Tag.ClassificationType))).ToArray();
        var viewRaw = viewTags.GetTags(query).Where(tag => !tag.Tag.ClassificationType.IsOfType(Block))
            .SelectMany(tag => tag.Span.GetSpans(query.Snapshot).Select(span => new ClassificationSpan(span, tag.Tag.ClassificationType))).ToArray();
        Func<ClassificationSpan[], NativeClassificationSpan72[]> describe = spans => spans.Select(span => new NativeClassificationSpan72 {
            Classification = span.ClassificationType.Classification, Start = span.Span.Start.Position, Length = span.Span.Length }).ToArray();
        return new NativeClassificationObservation72 {
            Source = buffer.Length > 0 ? "native-buffer-classifier" : "raw-native-view-tags-excluding-own-block",
            Classifications = buffer.Length > 0 ? buffer : viewRaw,
            FullSpans = describe(buffer.Length > 0 ? bufferRaw : viewRaw),
            BufferClassifications = buffer, BufferFullSpans = describe(bufferRaw), ViewRawSpans = describe(viewRaw)
        };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : NativeClassificationObservation72
    /// desc         : native 원천과 최종 view 분류를 혼동하지 않도록 각각의 관측 결과를 보존한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class NativeClassificationObservation72
    {
        public string Source;
        public ClassificationSpan[] Classifications;
        public ClassificationSpan[] BufferClassifications;
        public NativeClassificationSpan72[] FullSpans;
        public NativeClassificationSpan72[] BufferFullSpans;
        public NativeClassificationSpan72[] ViewRawSpans;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : NativeClassificationSpan72
    /// desc         : 요청 범위로 잘라내지 않은 실제 native 태그의 원래 위치와 분류를 직렬화한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class NativeClassificationSpan72
    {
        public string Classification;
        public int Start;
        public int Length;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Argb
    /// desc         : 실제 Brush/Color 채널을 알파를 유지한 문자열로 기록한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 투명 마커 배경을 불투명으로 정규화하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Argb(object value)
    {
        var brush = value as SolidColorBrush;
        if (brush != null) return brush.Color.ToString(CultureInfo.InvariantCulture).TrimStart('#').ToUpperInvariant();
        return value is Color ? ((Color)value).ToString(CultureInfo.InvariantCulture).TrimStart('#').ToUpperInvariant() : null;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadDiffOption
    /// desc         : native diff overview 옵션의 실제 정의·local·값과 부모 상태를 읽기만 한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 지원하지 않는 VS2017 옵션은 부재로 기록하며 자체 forceOn은 하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object ReadDiffOption(string owner, IEditorOptions options)
    {
        const string key = "Diff/View/ShowDiffOverviewMargin";
        bool defined = false, local = false, parentDefined = false;
        object value = null, parentValue = null; string error = null;
        try
        {
            defined = options != null && options.IsOptionDefined(key, false);
            local = options != null && options.IsOptionDefined(key, true);
            if (defined) value = options.GetOptionValue(key);
            parentDefined = options != null && options.Parent != null && options.Parent.IsOptionDefined(key, false);
            if (parentDefined) parentValue = options.Parent.GetOptionValue(key);
        }
        catch (Exception exception) { error = exception.ToString(); }
        return new { Owner = owner, Key = key, Defined = defined, LocallyDefined = local, Value = value, ParentDefined = parentDefined, ParentValue = parentValue, Error = error, Writes = 0 };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : IsError
    /// desc         : 네이티브 오류 종류만 허용하여 미사용 변수 경고를 구문 오류 성공으로 오인하지 않는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 실제 IErrorTag.ErrorType 원문을 비교한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool IsError(string type)
    {
        return type == "syntax error" || type == "compiler error" || type == "other error";
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PrepareOwnedEmptySolution
    /// desc         : 자기 프로필의 시작 솔루션을 기록하고 프로젝트 없는 자기 익명 상태만 닫는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 첫 실패를 보존하고 빈 FullName·프로젝트0·문서 소유권을 모두 검사한다.
    ///                2026-10-23 CodexOrig 보조 개발 055 : 공식 Misc 종류·빈 프로젝트 경로·항목0을 모두 만족한 가상 상태만 예외로 허용한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void PrepareOwnedEmptySolution(DTE dte, string root)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        bool open = dte.Solution.IsOpen;
        string name = dte.Solution.FullName;
        int count = dte.Solution.Projects.Count;
        var kinds = new List<string>();
        var projects = new List<object>();
        bool onlyEmptyMisc = true;
        foreach (Project project in dte.Solution.Projects)
        {
            string kind = null, fullName = null, error = null;
            int? items = null;
            try
            {
                kind = project.Kind; fullName = project.FullName; items = project.ProjectItems.Count;
                onlyEmptyMisc &= String.Equals(kind, EnvDTE.Constants.vsProjectKindMisc, StringComparison.OrdinalIgnoreCase) && String.IsNullOrEmpty(fullName) && items == 0;
            }
            catch (Exception exception) { onlyEmptyMisc = false; error = exception.ToString(); }
            kinds.Add(kind);
            projects.Add(new { Kind = kind, FullName = fullName, ProjectItemsCount = items, Error = error });
        }
        var documents = new List<object>();
        bool allOwned = true;
        foreach (Document document in dte.Documents)
        {
            string fullName = document.FullName;
            bool owned = !String.IsNullOrEmpty(fullName) && Path.GetFullPath(fullName).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            allOwned &= owned;
            documents.Add(new { Owned = owned, FullPath = owned ? fullName : null });
        }
        File.WriteAllText(Path.Combine(root, "task072-starting-solution.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {
            IsOpen = open, FullName = name, ProjectsCount = count, ProjectKinds = kinds, Projects = projects, OnlyEmptyMisc = onlyEmptyMisc, Documents = documents, AllDocumentsOwned = allOwned }));
        if (!String.IsNullOrEmpty(name) || (count != 0 && !onlyEmptyMisc) || !allOwned)
            throw new InvalidOperationException("Only an unnamed empty solution (or official empty Misc virtual projects) with exclusively owned documents may be replaced by the anonymous fixture.");
        if (open) dte.Solution.Close(false);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadThemeGate
    /// desc         : 실제 프로필의 설정 저장소와 registry CurrentTheme를 읽어 정확한 Dracula GUID를 검증한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 두 읽기 경로의 원형·실패를 보존하며 어느 경로에도 쓰지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object ReadThemeGate(Action<string, bool> check)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var rows = new List<object>();
        var guids = new List<Guid>();
        foreach (string route in new[] { "ShellSettingsManager.UserSettings", "VSRegistry.UserSettings", "ISettingsManager.ColorThemeNewThenColorTheme" })
        {
            object raw = null;
            string rawType = null, error = null, selectedKey = null;
            bool authoritative = route == "ISettingsManager.ColorThemeNewThenColorTheme";
            Guid parsed;
            bool valid = false;
            try
            {
                if (route.StartsWith("Shell", StringComparison.Ordinal))
                {
                    var manager = new Microsoft.VisualStudio.Shell.Settings.ShellSettingsManager(ServiceProvider.GlobalProvider);
                    var store = manager.GetReadOnlySettingsStore(Microsoft.VisualStudio.Settings.SettingsScope.UserSettings);
                    rawType = store.GetPropertyType("General", "CurrentTheme").ToString();
                    raw = store.GetString("General", "CurrentTheme");
                }
                else if (route.StartsWith("VSRegistry", StringComparison.Ordinal))
                {
                    var method = typeof(VSRegistry).GetMethods().Single(m => m.Name == "RegistryRoot" && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(bool));
                    object kind = Enum.Parse(method.GetParameters()[0].ParameterType, "RegType_UserSettings");
                    using (var root = (Microsoft.Win32.RegistryKey)method.Invoke(null, new[] { kind, (object)false }))
                    using (var general = root == null ? null : root.OpenSubKey("General", false))
                        raw = general == null ? null : general.GetValue("CurrentTheme");
                    rawType = raw == null ? null : raw.GetType().FullName;
                }
                else
                {
                    var manager = ServiceProvider.GlobalProvider.GetService(new Guid("9B164E40-C3A2-4363-9BC5-EB4039DEF653")) as Microsoft.VisualStudio.Settings.ISettingsManager;
                    if (manager == null) throw new InvalidOperationException("Actual settings persistence manager is unavailable.");
                    string current = manager.GetValueOrDefault<string>("Microsoft.VisualStudio.ColorThemeNew", null);
                    selectedKey = current == null ? "Microsoft.VisualStudio.ColorTheme" : "Microsoft.VisualStudio.ColorThemeNew";
                    raw = current ?? manager.GetValueOrDefault<string>("Microsoft.VisualStudio.ColorTheme", null);
                    rawType = raw == null ? null : raw.GetType().FullName;
                }
                valid = Guid.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out parsed);
                if (valid && authoritative) guids.Add(parsed);
            }
            catch (Exception exception) { error = exception.ToString(); }
            rows.Add(new { Route = route, Authoritative = authoritative, SelectedKey = selectedKey, RawType = rawType, RawValue = Convert.ToString(raw, CultureInfo.InvariantCulture), Parsed = valid, Error = error });
        }
        var expected = new Guid("8EC84919-8A34-422A-ABE1-CC238AD4D4B1");
        check("NATIVE72 exact active Dracula GUID is readable without writes", guids.Count > 0 && guids.All(g => g == expected));
        return new { Expected = expected, Routes = rows, Writes = 0 };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Contrast
    /// desc         : 실제 불투명 텍스트와 변경 배경의 WCAG 상대명도 대비를 계산한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 채널 부재나 투명색을 성공으로 취급하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static double Contrast(SolidColorBrush foreground, SolidColorBrush background)
    {
        if (foreground == null || background == null || foreground.Color.A != 255 || background.Color.A != 255 || foreground.Opacity != 1 || background.Opacity != 1) return 0;
        Func<byte, double> linear = channel => { double value = channel / 255.0; return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4); };
        Func<Color, double> luminance = color => 0.2126 * linear(color.R) + 0.7152 * linear(color.G) + 0.0722 * linear(color.B);
        double a = luminance(foreground.Color), b = luminance(background.Color);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RecordDrawing
    /// desc         : 네이티브 overview가 이미 그린 Drawing의 brush와 실제 변경 위치를 읽는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : OnRender를 직접 호출하거나 자체 표식·픽셀 캡처를 만들지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void RecordDrawing(Drawing drawing, List<object> rows, List<double> markerPositions)
    {
        if (drawing == null) return;
        var group = drawing as DrawingGroup;
        if (group != null) { foreach (var child in group.Children) RecordDrawing(child, rows, markerPositions); return; }
        var geometry = drawing as GeometryDrawing;
        if (geometry == null) return;
        var bounds = geometry.Bounds;
        string fill = Argb(geometry.Brush);
        rows.Add(new { Fill = fill, Pen = geometry.Pen == null ? null : Argb(geometry.Pen.Brush), IsEmpty = bounds.IsEmpty,
            X = bounds.IsEmpty ? (double?)null : bounds.X, Y = bounds.IsEmpty ? (double?)null : bounds.Y, Width = bounds.IsEmpty ? 0 : bounds.Width, Height = bounds.IsEmpty ? 0 : bounds.Height });
        if ((fill == "FF50FA7B" || fill == "FFFFB3D9") && !bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0) markerPositions.Add(bounds.Y);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RecordVisualDrawings
    /// desc         : 고정한 실제 RightHost margin과 그 자식 visual의 이미 렌더된 Drawing을 읽는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 비활성 InlineHost로 관찰 대상이 덮어써지지 않도록 범위를 한정한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void RecordVisualDrawings(Visual visual, List<object> rows, List<double> markerPositions)
    {
        RecordDrawing(VisualTreeHelper.GetDrawing(visual), rows, markerPositions);
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(visual); index++)
        {
            var child = VisualTreeHelper.GetChild(visual, index) as Visual;
            if (child != null) RecordVisualDrawings(child, rows, markerPositions);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RenderOwnedElement
    /// desc         : 자기 익명 fixture의 WPF view만 PNG로 렌더하고 OS 화면·창 프레임은 캡처하지 않는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 선택 QA 실패도 기록하며 물리 모니터 표시 성공으로 취급하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object RenderOwnedElement(FrameworkElement element, string path)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.Contains(Path.Combine("task072", "runtime")) ||
                !(Path.GetFileName(fullPath) == "native-csharp-owned-view.png" || Path.GetFileName(fullPath) == "native-difference-owned-view.png"))
                throw new InvalidOperationException("Only the two owned fixture PNG paths are allowed.");
            int width = (int)Math.Ceiling(element.ActualWidth), height = (int)Math.Ceiling(element.ActualHeight);
            if (width <= 0 || height <= 0 || width > 4096 || height > 4096) throw new InvalidOperationException("Owned view render size is outside the bounded fixture size.");
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = File.Create(fullPath)) encoder.Save(stream);
            return new { Succeeded = true, Path = fullPath, Width = width, Height = height, WpfViewOnly = true, OsCapture = false, PhysicalVisibilityVerified = false };
        }
        catch (Exception exception) { return new { Succeeded = false, Error = exception.ToString(), WpfViewOnly = true, OsCapture = false, PhysicalVisibilityVerified = false }; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RenderNativeErrorAsync
    /// desc         : 실제 태그 이후 UI idle·렌더를 유한 대기하고 native renderer의 분홍 pen과 자기 PNG를 읽는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 포맷 맵 조회만으로 화면 성공을 판정하지 않고 가짜 adornment를 만들지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static async System.Threading.Tasks.Task<object> RenderNativeErrorAsync(IWpfTextView view, string output, Action<string, bool> check)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var idle = view.VisualElement.Dispatcher.InvokeAsync(new Action(() => { }), System.Windows.Threading.DispatcherPriority.ApplicationIdle).Task;
        bool idleCompleted = await Task.WhenAny(idle, Task.Delay(2000)) == idle;
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var attempts = new List<object>();
        var borders = new List<object>();
        object rendered = null;
        while (true)
        {
            view.VisualElement.UpdateLayout();
            rendered = RenderOwnedElement(view.VisualElement, Path.Combine(output, "native-csharp-owned-view.png"));
            borders.Clear();
            CollectPinkBorders(view.VisualElement, view.VisualElement, borders);
            attempts.Add(new { ElapsedMilliseconds = clock.Elapsed.TotalMilliseconds, ActualNativePinkBorderDrawings = borders.Count });
            if (borders.Count > 0 || clock.ElapsedMilliseconds >= 4000) break;
            await Task.Delay(50); await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        }
        check("NATIVE72 native editor renderer actually draws the pink error border", borders.Count > 0);
        return new { OwnedViewPng = rendered, ApplicationIdleCompleted = idleCompleted, ElapsedMilliseconds = clock.Elapsed.TotalMilliseconds,
            Attempts = attempts, ActualNativePinkBorders = borders.ToArray(), SyntheticAdornmentsCreated = 0, FormatMapWrites = 0, OnRenderInvocations = 0, PhysicalMonitorVerified = false };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CollectPinkBorders
    /// desc         : 자기 편집기 visual의 실제 Drawing에서 분홍1px이상 pen의 좌표만 수집한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 다른 창·user document·OS 화면에 접근하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void CollectPinkBorders(Visual root, Visual visual, List<object> rows)
    {
        var drawings = new Stack<Drawing>();
        var existing = VisualTreeHelper.GetDrawing(visual); if (existing != null) drawings.Push(existing);
        while (drawings.Count > 0)
        {
            var item = drawings.Pop(); var group = item as DrawingGroup;
            if (group != null) { foreach (var child in group.Children) drawings.Push(child); continue; }
            var geometry = item as GeometryDrawing;
            if (geometry == null || geometry.Pen == null || Argb(geometry.Pen.Brush) != "FFFF79C6" || geometry.Pen.Thickness < 1 || geometry.Bounds.IsEmpty) continue;
            var penBrush = geometry.Pen.Brush as SolidColorBrush;
            if (penBrush == null || penBrush.Opacity != 1 || penBrush.Color.A != 255) continue;
            var bounds = Object.ReferenceEquals(root, visual) ? geometry.Bounds : visual.TransformToAncestor(root).TransformBounds(geometry.Bounds);
            rows.Add(new { VisualType = visual.GetType().FullName, Stroke = Argb(geometry.Pen.Brush), StrokeOpacity = penBrush.Opacity, Thickness = geometry.Pen.Thickness, Fill = Argb(geometry.Brush),
                X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height });
        }
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(visual); index++)
        {
            var child = VisualTreeHelper.GetChild(visual, index) as Visual;
            if (child != null) CollectPinkBorders(root, child, rows);
        }
    }
}
