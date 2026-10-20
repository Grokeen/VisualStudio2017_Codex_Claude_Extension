using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : PreviewPerformanceChecks
/// desc         : 실제 미리보기의 자동숨김 재로드 비용과 문서·선택·GIF 수명 계약을 무창 관측한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : TASK069 C3 baseline과 고정 후보 의미 계약을 분리한다.
/// </summary>
/// ---------------------------------------------------------------------
internal static class PreviewPerformanceChecks
{
    private static readonly List<object> checks = new List<object>(), observations = new List<object>();
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 16000000 };
    private static int passed, failed;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 정확한 실제 DLL과 개인 dependency map을 로드하고 STA 검사를 실행한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : Window·Popup·OS입력·CLI·네트워크 사용 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        var map = Json.Deserialize<Dictionary<string, string>>(File.ReadAllText(args[1]));
        AppDomain.CurrentDomain.AssemblyResolve += (sender, value) => { string name = new AssemblyName(value.Name).Name; return map.ContainsKey(name) && File.Exists(map[name]) ? Assembly.LoadFrom(map[name]) : null; };
        Assembly.LoadFrom(args[0]);
        return Execute(args);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Execute
    /// desc         : baseline의 재생성 사실과 수정 후보의 재사용 의미를 서로 다른 기대값으로 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 시간 임계값으로 성공을 만들지 않고 실제 문서 정체성을 검사한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Execute(string[] args)
    {
        bool expectReuse = args[4] == "reuse";
        try { RepeatedLoads(args[3], expectReuse); if (expectReuse) { SemanticChecks(args[3]); ThemeChecks(); } }
        catch (Exception error) { Check("runner/product exception", false, error.ToString()); }
        var result = new { Passed = passed, Failed = failed, Checks = checks, Observations = observations, Mode = args[4], ProductAssemblySha256 = args[5], ActualProductDll = true, ActualWpfPreviewUsed = true, BaselineOnly = !expectReuse, WindowShown = false, PopupOpened = false, OsInputInjected = false, NetworkRequested = false, LoginAttempted = false, Limitation = "Explicit Loaded API event on unshown actual WPF controls. Host auto-hide interaction, rendered desktop latency, GPU, pointer and real IDE are not exercised. Allocation metrics cover managed bytes on the current STA thread only.", CheckedAt = DateTimeOffset.Now.ToString("o") };
        File.WriteAllText(args[2], Json.Serialize(result), new UTF8Encoding(false)); Console.WriteLine(Json.Serialize(result)); return failed == 0 ? 0 : 1;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RepeatedLoads
    /// desc         : 20개 대화 미리보기를 5회 다시 로드해 문서 교체·선택·GIF와 실제 managed allocation을 관측한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : baseline/후보에서 같은 입력과 루프를 사용한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void RepeatedLoads(string root, bool expectReuse)
    {
        string assets = Path.Combine(root, "assets"); Directory.CreateDirectory(assets); File.WriteAllBytes(Path.Combine(assets, "sample.gif"), Gif());
        string markdown = "# Existing conversation\n\nA **preserved** answer with [local guide](guide.md).\n\n| Key | Value |\n|---|---|\n|Model|selected|\n\n```cs\n" + String.Join("\n", Enumerable.Range(0, 30).Select(n => "var value" + n + " = \"preview\";")) + "\n```\n\n![sample](sample.gif)\n";
        var previews = Enumerable.Range(0, 20).Select(n => Create(markdown, assets)).ToArray();
        try
        {
            var first = previews[0]; var image = Images(first).Single(); var source = image.Source; var original = first.TextView.Document;
            first.TextView.Selection.Select(original.ContentStart.GetPositionAtOffset(3), original.ContentStart.GetPositionAtOffset(12)); string selection = first.TextView.Selection.Text;
            first.Width = 320; first.Height = 120; first.ApplyTemplate(); first.Measure(new Size(320, 120)); first.Arrange(new Rect(0, 0, 320, 120)); first.UpdateLayout();
            first.TextView.ScrollToVerticalOffset(40); first.UpdateLayout();
            double offset = first.TextView.VerticalOffset;
            var allocator = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            long before = allocator == null ? 0 : (long)allocator.Invoke(null, null); long heapBefore = GC.GetTotalMemory(false);
            int replacements = 0; var timer = Stopwatch.StartNew();
            for (int repeat = 0; repeat < 5; repeat++) foreach (var preview in previews) { var previous = preview.TextView.Document; Load(preview); if (!ReferenceEquals(previous, preview.TextView.Document)) replacements++; }
            timer.Stop(); long allocated = allocator == null ? -1 : (long)allocator.Invoke(null, null) - before;
            bool sameDocument = ReferenceEquals(original, first.TextView.Document), sameImage = Images(first).Any(item => ReferenceEquals(item, image));
            observations.Add(new { Scenario = "20-previews-5-loads", Loads = 100, DocumentReplacements = replacements, ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds, ManagedAllocatedBytesCurrentThread = allocated, ManagedAllocatorAvailable = allocator != null, HeapSizeDeltaBytes = GC.GetTotalMemory(false) - heapBefore, SameDocument = sameDocument, SameImage = sameImage, SameImageSource = ReferenceEquals(source, image.Source), SelectedTextBefore = selection, SelectedTextAfter = first.TextView.Selection.Text, OffsetBefore = offset, OffsetAfter = first.TextView.VerticalOffset });
            Check(expectReuse ? "100 unchanged Loaded events reuse completed documents" : "baseline reproduces document replacement for all 100 unchanged Loaded events", expectReuse ? replacements == 0 : replacements == 100);
            if (expectReuse)
            {
                Check("selection and scroll offset survive unchanged Loaded", selection.Length > 0 && first.TextView.Selection.Text == selection && first.TextView.VerticalOffset == offset);
                Check("nonzero internal scroll state is exercised without a Window", offset > 0);
                Check("GIF instance decoded frames and source survive unchanged Loaded", sameDocument && sameImage && image.FrameCount == 3 && ReferenceEquals(source, image.Source));
                first.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent, first)); Load(first);
                Check("Unloaded Loaded does not dispose a reusable document", ReferenceEquals(original, first.TextView.Document) && ReferenceEquals(source, image.Source));
            }
        }
        finally { foreach (var preview in previews) preview.Dispose(); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SemanticChecks
    /// desc         : 실제 입력·팔레트·자산 변경과 강제 새로고침·Dispose 계약을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 캐시로 변경을 숨기거나 외부 Document 교체를 잘못 재사용하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void SemanticChecks(string root)
    {
        string assets = Path.Combine(root, "assets");
        var preview = Create("# Header\n\n[link](guide.md)\n\n```cs\nvar code = 1;\n```\n\n> muted\n\n![sample](sample.gif)", assets);
        try
        {
            var document = preview.TextView.Document;
            preview.Foreground = new SolidColorBrush(Colors.White); Load(preview);
            Check("equivalent foreground brush does not replace document", ReferenceEquals(document, preview.TextView.Document));
            preview.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)); Load(preview);
            Check("equivalent background brush does not replace document", ReferenceEquals(document, preview.TextView.Document));
            foreach (string key in new[] { "AssistantLinkBrush", "AssistantCodeBackgroundBrush", "AssistantMutedForegroundBrush", "AssistantBorderBrush" })
            {
                var before = preview.TextView.Document; var color = ((SolidColorBrush)preview.Resources[key]).Color;
                preview.Resources[key] = new SolidColorBrush(color); Load(preview);
                Check(key + " equivalent palette preserves document", ReferenceEquals(before, preview.TextView.Document));
                preview.Resources[key] = new SolidColorBrush(Color.FromArgb(255, (byte)(color.R ^ 127), color.G, color.B)); Load(preview);
                Check(key + " changed palette renders fresh document", !ReferenceEquals(before, preview.TextView.Document));
            }
            document = preview.TextView.Document; ((SolidColorBrush)preview.Foreground).Color = Colors.LightGray; Load(preview);
            Check("mutable foreground color snapshot invalidates rendering", !ReferenceEquals(document, preview.TextView.Document) && ((SolidColorBrush)preview.TextView.Document.Foreground).Color == Colors.LightGray);
            document = preview.TextView.Document; preview.FontSize += 2;
            Check("font size change renders current document", !ReferenceEquals(document, preview.TextView.Document) && preview.TextView.Document.FontSize == preview.FontSize);
            document = preview.TextView.Document; preview.FontFamily = new FontFamily("Consolas");
            Check("font family change invalidates prior render", !ReferenceEquals(document, preview.TextView.Document));
            document = preview.TextView.Document; preview.FontWeight = FontWeights.Bold;
            Check("font weight change invalidates prior render", !ReferenceEquals(document, preview.TextView.Document));
            document = preview.TextView.Document; preview.FontStyle = FontStyles.Italic;
            Check("font style change invalidates prior render", !ReferenceEquals(document, preview.TextView.Document));
            document = preview.TextView.Document; preview.FontStretch = FontStretches.Condensed;
            Check("font stretch change invalidates prior render", !ReferenceEquals(document, preview.TextView.Document));
            document = preview.TextView.Document; preview.Background = new SolidColorBrush(Colors.DarkBlue);
            Check("actual background change rerenders current colors", !ReferenceEquals(document, preview.TextView.Document) && ((SolidColorBrush)preview.TextView.Document.Background).Color == Colors.DarkBlue);
            document = preview.TextView.Document; preview.Markdown += "\n\nnew text";
            Check("actual Markdown change updates content", !ReferenceEquals(document, preview.TextView.Document) && new TextRange(preview.TextView.Document.ContentStart, preview.TextView.Document.ContentEnd).Text.Contains("new text"));
            string other = Path.Combine(root, "other-assets"); Directory.CreateDirectory(other); File.WriteAllBytes(Path.Combine(other, "sample.gif"), Gif());
            document = preview.TextView.Document; preview.AssetBaseDirectory = other;
            Check("asset base change renders with the new base", !ReferenceEquals(document, preview.TextView.Document) && Images(preview).Single().FrameCount == 3);
            var image = Images(preview).Single(); var oldSource = image.Source; document = preview.TextView.Document;
            File.WriteAllBytes(Path.Combine(other, "sample.gif"), new byte[] { 1, 2, 3 }); Load(preview);
            Check("ordinary Loaded keeps completed external asset snapshot", ReferenceEquals(document, preview.TextView.Document) && ReferenceEquals(oldSource, image.Source));
            preview.Refresh();
            Check("public Refresh reloads changed external assets and disposes old media", !ReferenceEquals(document, preview.TextView.Document) && !Images(preview).Any() && image.Source == null && new TextRange(preview.TextView.Document.ContentStart, preview.TextView.Document.ContentEnd).Text.Contains("[이미지:"));
            File.WriteAllBytes(Path.Combine(other, "sample.gif"), Gif()); document = preview.TextView.Document;
            preview.SetDocument(preview.Markdown, other);
            Check("explicit same-input SetDocument reloads assets", !ReferenceEquals(document, preview.TextView.Document) && Images(preview).Single().FrameCount == 3);
            var external = new FlowDocument(new Paragraph(new Run("external replacement"))); preview.TextView.Document = external; Load(preview);
            Check("external Document replacement invalidates cached render", !ReferenceEquals(external, preview.TextView.Document) && new TextRange(preview.TextView.Document.ContentStart, preview.TextView.Document.ContentEnd).Text.Contains("new text"));
            preview.Foreground = new SolidColorBrush(Colors.LightGray) { Transform = null, RelativeTransform = null }; Load(preview);
            Check("valid null brush transforms use identity without throwing", ((SolidColorBrush)preview.TextView.Document.Foreground).Color == Colors.LightGray);
            image = Images(preview).Single(); preview.Dispose(); document = preview.TextView.Document; preview.Dispose(); Load(preview); preview.Refresh();
            Check("Dispose releases GIF and cannot resurrect cached document", image.Source == null && !image.IsAnimationRunning && ReferenceEquals(document, preview.TextView.Document));
        }
        finally { preview.Dispose(); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ThemeChecks
    /// desc         : 실제 AssistantControl의 동일 팔레트 재사용과 모드/누락 리소스 복원을 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : Root Theme.cs 최소 수정의 세 의미 계약을 읽기/자기 리소스로 검증한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void ThemeChecks()
    {
        new AssistantSettingsStore().Save(new AssistantSettings { HideWelcome = true, ShowHistory = false, EnableCompletionNotifications = false, ThemeMode = "light" });
        using (var control = new AssistantControl())
        {
            Invoke(control, "RestoreThemeMode", "light"); Invoke(control, "RestoreVisualThemePreset", "vs");
            object brush = control.Resources["AssistantBackgroundBrush"]; Invoke(control, "RefreshAssistantTheme"); Invoke(control, "RefreshAssistantTheme");
            Check("actual AssistantControl repeated same palette preserves brush reference", ReferenceEquals(brush, control.Resources["AssistantBackgroundBrush"]));
            var color = ((SolidColorBrush)control.Resources["AssistantBackgroundBrush"]).Color; Invoke(control, "RestoreThemeMode", "dark");
            bool darkChanged = ((SolidColorBrush)control.Resources["AssistantBackgroundBrush"]).Color != color; brush = control.Resources["AssistantBackgroundBrush"]; Invoke(control, "RestoreVisualThemePreset", "kakao");
            Check("actual mode and preset changes still recolor palette", darkChanged && !ReferenceEquals(brush, control.Resources["AssistantBackgroundBrush"]));
            control.Resources.Remove("AssistantBackgroundBrush"); Invoke(control, "RefreshAssistantTheme");
            Check("missing required palette resource is restored", control.Resources.Contains("AssistantBackgroundBrush") && control.Resources["AssistantBackgroundBrush"] is SolidColorBrush);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Create
    /// desc         : 고정 팔레트와 명시한 개인 자산을 실제 제품 미리보기에 전달한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 합성 renderer로 제품을 대체하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static MarkdownPreview Create(string markdown, string assets)
    {
        var view = new MarkdownPreview { Foreground = new SolidColorBrush(Colors.White), Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), FontSize = 13 };
        view.Resources["AssistantLinkBrush"] = new SolidColorBrush(Colors.SkyBlue); view.Resources["AssistantCodeBackgroundBrush"] = new SolidColorBrush(Colors.DarkSlateGray); view.Resources["AssistantMutedForegroundBrush"] = new SolidColorBrush(Colors.Gray); view.Resources["AssistantBorderBrush"] = new SolidColorBrush(Colors.SlateGray);
        view.SetDocument(markdown, assets); return view;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Load
    /// desc         : 자기 실제 미리보기에 WPF Loaded 이벤트만 전달한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실제 창 표시/IDE 탭/포인터 동작으로 주장하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Load(MarkdownPreview view) { view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, view)); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Images
    /// desc         : 실제 FlowDocument의 inline/block 이미지 컨트롤을 읽기 열거한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 제품이 만든 GIF 객체 정체성을 관측한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<MarkdownAnimatedImage> Images(MarkdownPreview view) { return Descendants(view.TextView.Document).OfType<MarkdownAnimatedImage>(); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Descendants
    /// desc         : 논리 트리를 따라 자기 문서 자식만 순회한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 외부 UI를 열거하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<DependencyObject> Descendants(DependencyObject item) { foreach (object child in LogicalTreeHelper.GetChildren(item)) { var value = child as DependencyObject; if (value == null) continue; yield return value; foreach (var nested in Descendants(value)) yield return nested; } }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 실패 원문을 보존하고 성공 개수와 분리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 시간 숫자는 판정 임계값으로 사용하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, bool value, string error = null) { if (value) passed++; else failed++; checks.Add(new { Name = name, Passed = value, Error = error }); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Invoke
    /// desc         : 자기 실제 AssistantControl의 테마 메소드만 명시적으로 호출한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 제품 소스·설정 전역값을 변경하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Invoke(object item, string method, params object[] args) { return item.GetType().GetMethod(method, Private).Invoke(item, args); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Gif
    /// desc         : 세 개의 작은 부분 프레임을 갖는 유효한 개인 GIF 바이트를 만든다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 기존 검증된 최소 fixture 구조를 재사용하며 외부 자산을 받지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static byte[] Gif()
    {
        using (var output = new MemoryStream()) using (var writer = new BinaryWriter(output)) {
            writer.Write(Encoding.ASCII.GetBytes("GIF89a")); writer.Write((ushort)2); writer.Write((ushort)1); writer.Write((byte)0x81); writer.Write((byte)0); writer.Write((byte)0); writer.Write(new byte[] { 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255 });
            for (int i = 0; i < 3; i++) { writer.Write(new byte[] { 0x21, 0xf9, 4, 4, 5, 0, 0, 0, 0x2c }); writer.Write((ushort)(i == 2 ? 1 : 0)); writer.Write((ushort)0); writer.Write((ushort)(i == 0 ? 2 : 1)); writer.Write((ushort)1); writer.Write((byte)0); writer.Write((byte)2); int bits = 0, used = 0; var packed = new List<byte>(); foreach (int code in i == 0 ? new[] { 4, 1, 4, 1, 5 } : new[] { 4, i == 1 ? 2 : 3, 5 }) { bits |= code << used; used += 3; while (used >= 8) { packed.Add((byte)bits); bits >>= 8; used -= 8; } } if (used > 0) packed.Add((byte)bits); writer.Write((byte)packed.Count); writer.Write(packed.ToArray()); writer.Write((byte)0); }
            writer.Write((byte)0x3b); return output.ToArray();
        }
    }
}
