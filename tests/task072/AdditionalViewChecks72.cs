using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Task = System.Threading.Tasks.Task;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : AdditionalViewChecks72
/// desc         : 자기 view 옵션만 변경하여 실제 테마 전환·복원·독립성과 큰 익명 문서 query를 검사한다.
/// author       : CodexOrig 보조 개발 055
/// create date  : 2026-10-23
/// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 기존 읽기 전용 core와 쓰기 범위를 분리하고 글로벌 옵션은 읽기만 한다.
///                2026-10-23 CodexOrig 보조 개발 055 : 같은 view의 복수 소비자 수명과 재획득을 실제 aggregator로 검사한다.
///                2026-10-23 CodexOrig 보조 개발 055 : 실제 정의된 native diff overview 옵션도 동일한 local·상속·사용자 변경 복원 계약에 포함한다.
/// </summary>
/// ---------------------------------------------------------------------
public static class AdditionalViewChecks72
{
    private static readonly string[] Keys = { "OverviewMargin/ShowErrors", "OverviewMargin/ShowChangeTracking", "Diff/View/ShowDiffOverviewMargin" };
    private const string Block = "Grokeen.Dracula.BlockComment";
    private const string Error = "Grokeen.Dracula.ErrorHighlight";

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunAsync
    /// desc         : 두 자기 view의 local·상속 옵션과 실제 custom 태그를 테마 전환 전후에 비교한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 제품 pending handler만 테마를 바꾸며 finally에서 Dracula와 소유 자원을 복구한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static async System.Threading.Tasks.Task<object> RunAsync(DTE dte, IComponentModel component, IWpfTextView sourceView, string output, Func<string, Task> switchTheme, Action<string, bool> check)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (!dte.RegistryRoot.Contains("ThemeApply72") || !Path.GetFullPath(output).Contains(Path.Combine("task072", "runtime")))
            throw new InvalidOperationException("Dedicated TASK072 profile and artifact output are required.");
        var factory = component.GetService<ITextEditorFactoryService>();
        var optionFactory = component.GetService<IEditorOptionsFactoryService>();
        var buffer = sourceView.TextBuffer;
        var keys = Keys.Where(k => sourceView.Options.IsOptionDefined(k, false)).ToArray();
        check("TRANSITION72 mandatory error and change option definitions are available", Keys.Take(2).All(k => keys.Contains(k)));
        var global = optionFactory.GlobalOptions;
        var globalBefore = keys.Select(k => global.GetOptionValue<bool>(k)).ToArray();
        var parentA = optionFactory.CreateOptions();
        var parentB = optionFactory.CreateOptions();
        int parentWrites = 0, viewWrites = 0;
        foreach (var key in keys) { parentA.SetOptionValue(key, false); parentB.SetOptionValue(key, false); parentWrites += 2; }
        IWpfTextView a = null, b = null;
        IWpfTextViewHost hostA = null, hostB = null;
        System.Windows.Window window = null;
        ITagAggregator<IClassificationTag> tagsA = null, tagsB = null;
        ITagAggregator<ITextMarkerTag> errorsA = null, errorsB = null;
        object sessionA = null, sessionB = null;
        var observations = new List<object>();
        object large = null;
        try
        {
            large = await ReadLargeDocumentAsync(dte, component, output, check);
            await switchTheme("kakao");
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            a = factory.CreateTextView(buffer, factory.DefaultRoles, parentA);
            b = factory.CreateTextView(buffer, factory.DefaultRoles, parentB);
            hostA = factory.CreateTextViewHost(a, false); hostB = factory.CreateTextViewHost(b, false);
            var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(hostB.HostControl, 1); grid.Children.Add(hostA.HostControl); grid.Children.Add(hostB.HostControl);
            window = new System.Windows.Window { Title = "TASK072 anonymous option views", Width = 960, Height = 480, ShowInTaskbar = false, Content = grid };
            window.Show(); window.UpdateLayout();
            foreach (var key in keys) { a.Options.SetOptionValue(key, false); viewWrites++; }
            check("TRANSITION72 first view starts with explicit false local options", keys.All(k => !a.Options.GetOptionValue<bool>(k) && a.Options.IsOptionDefined(k, true)));
            check("TRANSITION72 second view starts with inherited false options", keys.All(k => !b.Options.GetOptionValue<bool>(k) && !b.Options.IsOptionDefined(k, true)));
            var aggregates = component.GetService<IViewTagAggregatorFactoryService>();
            tagsA = aggregates.CreateTagAggregator<IClassificationTag>(a); tagsB = aggregates.CreateTagAggregator<IClassificationTag>(b);
            errorsA = aggregates.CreateTagAggregator<ITextMarkerTag>(a); errorsB = aggregates.CreateTagAggregator<ITextMarkerTag>(b);
            sessionA = Session(a); sessionB = Session(b);
            check("TRANSITION72 real two views have distinct product sessions", sessionA != null && sessionB != null && !Object.ReferenceEquals(sessionA, sessionB));
            observations.Add(State("initial-kakao", a, b, tagsA, tagsB, errorsA, errorsB, keys));
            check("TRANSITION72 Kakao produces no custom block or error overlay", Count(tagsA, a) == 0 && Count(tagsB, b) == 0 && Count(errorsA, a) == 0 && Count(errorsB, b) == 0);
            await switchTheme("dracula");
            await WaitAsync(() => keys.All(k => a.Options.GetOptionValue<bool>(k) && b.Options.GetOptionValue<bool>(k)) && Count(tagsA, a) > 0 && Count(tagsB, b) > 0 && Count(errorsA, a) > 0 && Count(errorsB, b) > 0);
            observations.Add(State("first-dracula", a, b, tagsA, tagsB, errorsA, errorsB, keys));
            check("TRANSITION72 Dracula activates local options in both real views", keys.All(k => a.Options.GetOptionValue<bool>(k) && b.Options.GetOptionValue<bool>(k)));
            check("TRANSITION72 Dracula activates real comment and error overlays in both views", Count(tagsA, a) > 0 && Count(tagsB, b) > 0 && Count(errorsA, a) > 0 && Count(errorsB, b) > 0);
            var secondTagsA = aggregates.CreateTagAggregator<IClassificationTag>(a);
            var secondErrorsA = aggregates.CreateTagAggregator<ITextMarkerTag>(a);
            try
            {
                check("TRANSITION72 second consumer on the same view receives both actual tag kinds", Count(secondTagsA, a) > 0 && Count(secondErrorsA, a) > 0);
                tagsA.Dispose(); tagsA = null; errorsA.Dispose(); errorsA = null;
                check("TRANSITION72 disposing one consumer preserves the second consumer", Count(secondTagsA, a) > 0 && Count(secondErrorsA, a) > 0 && !Disposed(sessionA));
                tagsA = aggregates.CreateTagAggregator<IClassificationTag>(a); errorsA = aggregates.CreateTagAggregator<ITextMarkerTag>(a);
                check("TRANSITION72 reacquired consumer remains live on the same view", Count(tagsA, a) > 0 && Count(errorsA, a) > 0 && Count(secondTagsA, a) > 0 && Count(secondErrorsA, a) > 0);
            }
            finally { secondTagsA.Dispose(); secondErrorsA.Dispose(); }
            await switchTheme("kakao");
            await WaitAsync(() => keys.All(k => !a.Options.GetOptionValue<bool>(k) && !b.Options.GetOptionValue<bool>(k)) && Count(tagsA, a) == 0 && Count(tagsB, b) == 0 && Count(errorsA, a) == 0 && Count(errorsB, b) == 0);
            observations.Add(State("restored-kakao", a, b, tagsA, tagsB, errorsA, errorsB, keys));
            check("TRANSITION72 Kakao restores original explicit false local state", keys.All(k => !a.Options.GetOptionValue<bool>(k) && a.Options.IsOptionDefined(k, true)));
            check("TRANSITION72 Kakao restores original false inheritance without leftover local state", keys.All(k => !b.Options.GetOptionValue<bool>(k) && !b.Options.IsOptionDefined(k, true)));
            check("TRANSITION72 actual theme switch removes both custom tag kinds", Count(tagsA, a) == 0 && Count(tagsB, b) == 0 && Count(errorsA, a) == 0 && Count(errorsB, b) == 0);
            await switchTheme("dracula");
            await WaitAsync(() => Count(tagsA, a) > 0 && Count(tagsB, b) > 0 && Count(errorsA, a) > 0 && Count(errorsB, b) > 0);
            foreach (var key in keys) { a.Options.SetOptionValue(key, false); viewWrites++; }
            observations.Add(State("user-changed-first-view", a, b, tagsA, tagsB, errorsA, errorsB, keys));
            check("TRANSITION72 local user change leaves the other view enabled", keys.All(k => !a.Options.GetOptionValue<bool>(k) && b.Options.GetOptionValue<bool>(k)));
            await switchTheme("kakao");
            await WaitAsync(() => keys.All(k => !b.Options.GetOptionValue<bool>(k)) && Count(tagsA, a) == 0 && Count(tagsB, b) == 0);
            check("TRANSITION72 user-written false remains explicit after theme restore", keys.All(k => !a.Options.GetOptionValue<bool>(k) && a.Options.IsOptionDefined(k, true)));
            await switchTheme("dracula");
            await WaitAsync(() => Count(tagsA, a) > 0 && Count(tagsB, b) > 0);
            var bOptions = b.Options;
            hostB.Close(); hostB = null;
            check("TRANSITION72 closing one real view disposes its actual product session", b.IsClosed && Disposed(sessionB));
            check("TRANSITION72 closing restores inherited false state", keys.All(k => !bOptions.GetOptionValue<bool>(k) && !bOptions.IsOptionDefined(k, true)));
            check("TRANSITION72 closing another view preserves the live view overlay", !a.IsClosed && Count(tagsA, a) > 0 && !Disposed(sessionA));
            hostA.Close(); hostA = null;
            check("TRANSITION72 both actual product sessions are disposed on close", a.IsClosed && Disposed(sessionA));
            check("TRANSITION72 global options remain unchanged", keys.Select(k => global.GetOptionValue<bool>(k)).SequenceEqual(globalBefore));
            return new { ActualThemeTransitions = true, ViewsCreated = 2, OwnParentOptionWrites = parentWrites, OwnViewOptionWrites = viewWrites, GlobalOptionWrites = 0,
                GlobalOptionsUnchanged = keys.Select(k => global.GetOptionValue<bool>(k)).SequenceEqual(globalBefore), TestedOptionKeys = keys, UndefinedOptionalOptionKeys = Keys.Except(keys).ToArray(), Observations = observations, LargeDocument = large,
                ProductSessionsDisposed = Disposed(sessionA) && Disposed(sessionB), PhysicalPointerUsed = false };
        }
        finally
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (errorsA != null) errorsA.Dispose(); if (errorsB != null) errorsB.Dispose();
            if (tagsA != null) tagsA.Dispose(); if (tagsB != null) tagsB.Dispose();
            if (hostA != null && !hostA.IsClosed) hostA.Close(); if (hostB != null && !hostB.IsClosed) hostB.Close();
            if (window != null) { window.Content = null; window.Close(); }
            await switchTheme("dracula");
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WaitAsync
    /// desc         : 실제 UI 이벤트가 반영되는지 최대 5초 확인하고 판정은 호출자의 기존 assertion에 맡긴다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 고정 대기로 상태 실패를 성공으로 바꾸지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static async Task WaitAsync(Func<bool> ready)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!ready() && clock.ElapsedMilliseconds < 5000) { await Task.Delay(50); await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Count
    /// desc         : 실제 view의 현재 스냅샷에 매핑된 해당 제품 태그만 센다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 다른 native 태그를 제품 성공 수로 합산하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Count<T>(ITagAggregator<T> tags, IWpfTextView view) where T : ITag
    {
        var span = new SnapshotSpan(view.TextSnapshot, 0, view.TextSnapshot.Length);
        return tags.GetTags(span).Count(t => (t.Tag is IClassificationTag && ((IClassificationTag)t.Tag).ClassificationType.IsOfType(Block)) || (t.Tag is ITextMarkerTag && ((ITextMarkerTag)t.Tag).Type == Error));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : State
    /// desc         : 두 view 옵션의 값·local 정의와 실제 태그 수를 원형 그대로 기록한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 전환 시점별 상태를 이후 성공으로 덮어쓰지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object State(string phase, IWpfTextView a, IWpfTextView b, ITagAggregator<IClassificationTag> ta, ITagAggregator<IClassificationTag> tb, ITagAggregator<ITextMarkerTag> ea, ITagAggregator<ITextMarkerTag> eb, IEnumerable<string> keys)
    {
        return new { Phase = phase, First = keys.Select(k => new { Key = k, Value = a.Options.GetOptionValue<bool>(k), Local = a.Options.IsOptionDefined(k, true) }).ToArray(),
            Second = keys.Select(k => new { Key = k, Value = b.Options.GetOptionValue<bool>(k), Local = b.Options.IsOptionDefined(k, true) }).ToArray(),
            FirstBlockTags = Count(ta, a), SecondBlockTags = Count(tb, b), FirstErrorMarkers = Count(ea, a), SecondErrorMarkers = Count(eb, b) };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Session
    /// desc         : 해당 view가 소유한 실제 제품 session 참조만 읽어 종료 상태 검증에 보존한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 제품 생성자·비공개 메소드를 호출하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Session(IWpfTextView view)
    {
        return view.Properties.PropertyList.Select(p => p.Value).FirstOrDefault(v => v != null && v.GetType().FullName == "Grokeen.VisualStudioThemes.DraculaEditorSession");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Disposed
    /// desc         : 보존한 실제 session의 종료 플래그를 읽기만 한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : view.IsClosed와 제품 cleanup을 서로 다른 근거로 확인한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool Disposed(object session)
    {
        if (session == null) return false;
        var field = session.GetType().GetField("disposed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field != null && Object.Equals(field.GetValue(session), true);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadLargeDocumentAsync
    /// desc         : 자기 실제 C# 프로젝트의 약2MB 문서에서 2KB query 네 번과 UI heartbeat를 관찰한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 빈 native/custom 태그를 no-op 성능 성공으로 취급하지 않는다.
    ///                2026-10-23 CodexOrig 보조 개발 055 : view-only native 태그는 own overlay와 최종 병합 분류를 제외해 별도 계수한다.
    ///                2026-10-23 CodexOrig 보조 개발 055 : 초기 비동기 native 준비와 이후 2KB 계측 4회를 분리하고 최초 빈 결과도 보존한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static async System.Threading.Tasks.Task<object> ReadLargeDocumentAsync(DTE dte, IComponentModel component, string output, Action<string, bool> check)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        string directory = Path.Combine(Path.GetFullPath(output), "NativeCSharp72");
        string projectPath = Path.Combine(directory, "NativeCSharp72.csproj"), path = Path.Combine(directory, "LargeProbe72.cs");
        var text = new System.Text.StringBuilder("namespace Large72 { class LargeFixture {\r\n");
        for (int line = 0; line < 50000; line++) text.Append("/* BLOCK_LARGE_72_").Append(line.ToString("D5")).Append("_0123456789 */\r\n");
        text.Append("} }\r\n"); string source = text.ToString(); File.WriteAllText(path, source);
        Project project = null;
        foreach (Project candidate in dte.Solution.Projects)
            if (String.Equals(candidate.FullName, projectPath, StringComparison.OrdinalIgnoreCase))
            {
                if (project != null) throw new InvalidOperationException("Duplicate owned project identity.");
                project = candidate;
            }
        if (project == null) throw new InvalidOperationException("Exact owned large-fixture project is missing.");
        project.ProjectItems.AddFromFile(path); project.Save();
        EnvDTE.Window document = null;
        ITagAggregator<IClassificationTag> tags = null;
        var pulses = new List<double>(); var queries = new List<object>();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(25) };
        timer.Tick += (sender, args) => pulses.Add(clock.Elapsed.TotalMilliseconds);
        try
        {
            timer.Start();
            document = dte.ItemOperations.OpenFile(path, EnvDTE.Constants.vsViewKindTextView); document.Activate();
            var manager = (IVsTextManager)Package.GetGlobalService(typeof(SVsTextManager)); IVsTextView native;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(manager.GetActiveView(0, null, out native));
            var host = component.GetService<IVsEditorAdaptersFactoryService>().GetWpfTextViewHost(native);
            if (host == null || !String.Equals(dte.ActiveDocument.FullName, path, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Owned large CSharp view required.");
            var view = host.TextView;
            var bufferClassifier = component.GetService<IClassifierAggregatorService>().GetClassifier(view.TextBuffer);
            var classifier = component.GetService<IViewClassifierAggregatorService>().GetClassifier(view);
            var format = component.GetService<IClassificationFormatMapService>().GetClassificationFormatMap(view);
            tags = component.GetService<IViewTagAggregatorFactoryService>().CreateTagAggregator<IClassificationTag>(view);
            int start = source.IndexOf("/*", StringComparison.Ordinal);
            var readinessPolls = new List<LargeQueryObservation72>();
            var readinessClock = System.Diagnostics.Stopwatch.StartNew();
            bool ready = false;
            while (true)
            {
                var observation = ReadLargeQuery72(view, start, bufferClassifier, classifier, format, tags, output, "native-readiness", readinessPolls.Count);
                observation.ReadinessElapsedMilliseconds = readinessClock.Elapsed.TotalMilliseconds;
                readinessPolls.Add(observation);
                ready = observation.NativeCommentSpans > 0 && observation.ActualCustomTags > 0 && observation.CorrectFinalForeground
                    && observation.Milliseconds <= 10000 && readinessClock.ElapsedMilliseconds <= 15000;
                File.WriteAllText(Path.Combine(output, "task072-large-native-readiness.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {
                    ReadinessPolls = readinessPolls, ReadinessSucceeded = ready, MaximumReadinessMilliseconds = 15000,
                    DirectViewTagAggregatorQueries = readinessPolls.Count, SyntheticTagsCreated = 0, ProcessColdStartMeasured = false }));
                if (ready || readinessClock.ElapsedMilliseconds >= 15000) break;
                await Task.Delay(100); await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }
            readinessClock.Stop();
            check("LARGE72 actual native custom and final-color readiness completes within fifteen seconds", ready);
            check("LARGE72 every readiness query obeys the ten-second callback completion bound", readinessPolls.All(p => p.Milliseconds <= 10000));
            for (int round = 0; ready && round < 4; round++)
            {
                var observation = ReadLargeQuery72(view, start, bufferClassifier, classifier, format, tags, output,
                    round == 0 ? "first-recorded-after-native-readiness" : "warm-" + round, round);
                queries.Add(observation);
                check("LARGE72 actual native comment and custom classification work round " + round,
                    observation.NativeCommentSpans > 0 && observation.ActualCustomTags > 0 && observation.CorrectFinalForeground);
                check("LARGE72 2KB UI query returns within ten-second completion bound round " + round,
                    observation.RequestedLength == 2048 && observation.Milliseconds <= 10000);
                await Task.Delay(50); await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }
            check("LARGE72 four strict queries were recorded after actual native readiness", queries.Count == 4);
            check("LARGE72 owned source text remains unchanged", view.TextSnapshot.GetText() == source && File.ReadAllText(path) == source);
            check("LARGE72 actual UI heartbeat callbacks occurred", pulses.Count > 0);
            return new { Bytes = System.Text.Encoding.UTF8.GetByteCount(source), Lines = view.TextSnapshot.LineCount, QueryLength = 2048, Queries = queries, ReadinessPolls = readinessPolls, ReadinessSucceeded = ready,
                ReadinessElapsedMilliseconds = readinessClock.Elapsed.TotalMilliseconds, NativeReadinessDirectTagQueries = readinessPolls.Count,
                NativeReadinessDirectBufferClassifierQueries = readinessPolls.Count, NativeReadinessDirectFinalClassifierQueries = readinessPolls.Count,
                RecordedQueryCount = queries.Count, DispatcherPulsesMilliseconds = pulses,
                MaximumObservedPulseGapMilliseconds = pulses.Count < 2 ? (double?)null : pulses.Skip(1).Zip(pulses, (a, b) => a - b).Max(),
                SourceUnchanged = view.TextSnapshot.GetText() == source && File.ReadAllText(path) == source, ClaimedPerformanceImprovement = false,
                ProcessColdStartMeasured = false, NativeAndCustomWorkRequired = true };
        }
        finally
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            timer.Stop(); if (tags != null) tags.Dispose();
            if (document != null && document.Document != null) document.Document.Close(vsSaveChanges.vsSaveChangesNo);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadLargeQuery72
    /// desc         : 준비 및 계측 query에 같은 독립 10초 watchdog과 실제 native·custom·최종색 검증을 적용한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  : 2026-10-23 CodexOrig 보조 개발 055 : 준비 query는 콜드 계측으로 주장하지 않고 원천별 count·전체 span을 보존한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static LargeQueryObservation72 ReadLargeQuery72(IWpfTextView view, int start, IClassifier bufferClassifier, IClassifier classifier,
        IClassificationFormatMap format, ITagAggregator<IClassificationTag> tags, string output, string phase, int round)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var span = new SnapshotSpan(view.TextSnapshot, start, 2048);
        var observation = new LargeQueryObservation72 { Round = round, Phase = phase, RequestedLength = span.Length };
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var cancellation = new System.Threading.CancellationTokenSource();
        var token = cancellation.Token;
        var watchdog = Task.Run(async () => {
            try
            {
                await Task.Delay(10000, token).ConfigureAwait(false);
                File.WriteAllText(Path.Combine(output, "task072-query-timeout.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {
                    Stage = phase, Round = round, CompletionLimitMilliseconds = 10000, ActualUiCallbackUnresponsive = true, RequestedLength = 2048 }));
            }
            catch (OperationCanceledException) { }
        });
        try
        {
            observation.NativeBufferCommentSpans = bufferClassifier.GetClassificationSpans(span).Count(c => c.ClassificationType.IsOfType("comment"));
            var raw = tags.GetTags(span).ToArray();
            observation.NativeViewRawCommentSpans = raw.Count(t => !t.Tag.ClassificationType.IsOfType(Block) && t.Tag.ClassificationType.IsOfType("comment"));
            observation.NativeViewRawSpans = raw.Where(t => !t.Tag.ClassificationType.IsOfType(Block))
                .SelectMany(t => t.Span.GetSpans(view.TextSnapshot).Select(s => new { Classification = t.Tag.ClassificationType.Classification, Start = s.Start.Position, Length = s.Length })).ToArray();
            observation.NativeCommentSpans = observation.NativeBufferCommentSpans > 0 ? observation.NativeBufferCommentSpans : observation.NativeViewRawCommentSpans;
            observation.NativeClassificationSource = observation.NativeBufferCommentSpans > 0 ? "native-buffer-classifier" : "raw-native-view-tags-excluding-own-block";
            observation.ActualCustomTags = raw.Count(t => t.Tag.ClassificationType.IsOfType(Block));
            var final = classifier.GetClassificationSpans(span).Where(c => c.ClassificationType.IsOfType(Block)).ToArray();
            observation.FinalCustomClassifications = final.Length;
            observation.CorrectFinalForeground = final.Length > 0 && final.All(c => {
                var brush = format.GetTextProperties(c.ClassificationType).ForegroundBrush as System.Windows.Media.SolidColorBrush;
                return brush != null && brush.Color.ToString().ToUpperInvariant() == "#FFB5BDD4";
            });
        }
        finally { cancellation.Cancel(); cancellation.Dispose(); }
        clock.Stop(); observation.Milliseconds = clock.Elapsed.TotalMilliseconds;
        return observation;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : LargeQueryObservation72
    /// desc         : 준비 poll과 이후 계측 query의 원 native·custom·최종색 및 응답 시간을 구분해 직렬화한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-10-23
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class LargeQueryObservation72
    {
        public int Round;
        public string Phase;
        public double Milliseconds;
        public double ReadinessElapsedMilliseconds;
        public int RequestedLength;
        public int NativeCommentSpans;
        public int NativeBufferCommentSpans;
        public int NativeViewRawCommentSpans;
        public string NativeClassificationSource;
        public object NativeViewRawSpans;
        public int ActualCustomTags;
        public int FinalCustomClassifications;
        public bool CorrectFinalForeground;
    }
}
