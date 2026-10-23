using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Grokeen.VisualStudioThemes
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DraculaEditorKeys
    /// desc         : 별도 테마의 고정 GUID와 충돌을 피한 MEF 형식 이름을 정의한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-23
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class DraculaEditorKeys
    {
        internal const string BlockComment = "Grokeen.Dracula.BlockComment";
        internal const string NativeComment = "comment";
        internal const string ErrorHighlight = "Grokeen.Dracula.ErrorHighlight";
        internal static readonly Guid ThemeId = new Guid("8ec84919-8a34-422a-abe1-cc238ad4d4b1");
        private static readonly Guid SettingsServiceId = new Guid("9b164e40-c3a2-4363-9bc5-eb4039def653");

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(BlockComment)]
        [BaseDefinition(NativeComment)]
        internal static ClassificationTypeDefinition BlockCommentDefinition = null;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsDracula
        /// desc         : 활성 테마 GUID를 고정된 Dracula 식별자와 정확히 비교한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static bool IsDracula(Guid themeId) { return themeId == ThemeId; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadCurrentTheme
        /// desc         : 현재 호스트의 공개 읽기 전용 설정만 사용하고 조회 실패 시 비활성화한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static Guid ReadCurrentTheme()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var manager = ServiceProvider.GlobalProvider.GetService(SettingsServiceId) as ISettingsManager;
                return ReadSelectedTheme(manager, System.Windows.SystemParameters.HighContrast);
            }
            catch (Exception) { /* A missing settings service must not change another theme's editor. */ }
            return Guid.Empty;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadSelectedTheme
        /// desc         : 공개 설정 서비스의 선택 GUID만 읽고 새 설정이 잘못되었으면 이전 값을 사용하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static Guid ReadSelectedTheme(ISettingsManager manager, bool highContrast)
        {
            if (manager == null || highContrast) return Guid.Empty;
            try
            {
                string selected = manager.GetValueOrDefault<string>("Microsoft.VisualStudio.ColorThemeNew", null) ??
                    manager.GetValueOrDefault<string>("Microsoft.VisualStudio.ColorTheme", null);
                Guid id;
                return Guid.TryParse(selected, out id) ? id : Guid.Empty;
            }
            catch (Exception) { /* A transient or incompatible settings value must leave the overlay inactive. */ }
            return Guid.Empty;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DraculaBlockCommentFormat
    /// desc         : C# 블록 주석의 MEF 표시 형식을 정의한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-23
    /// </summary>
    /// ---------------------------------------------------------------------
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = DraculaEditorKeys.BlockComment)]
    [Name(DraculaEditorKeys.BlockComment)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    [Order(After = "Comment")]
    internal sealed class DraculaBlockCommentFormat : ClassificationFormatDefinition
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DraculaBlockCommentFormat
        /// desc         : C# 블록 주석의 기본 표시 색상을 등록한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        public DraculaBlockCommentFormat()
        {
            DisplayName = "Grokeen Dracula block comment";
            ForegroundColor = Color.FromRgb(0xB5, 0xBD, 0xD4);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DraculaErrorFormat
    /// desc         : 언어 서비스 오류에 배경 채움 없이 MEF 테두리만 정의한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-23
    /// </summary>
    /// ---------------------------------------------------------------------
    [Export(typeof(EditorFormatDefinition))]
    [Name(DraculaEditorKeys.ErrorHighlight)]
    [UserVisible(true)]
    internal sealed class DraculaErrorFormat : MarkerFormatDefinition
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DraculaErrorFormat
        /// desc         : 언어 서비스 오류의 분홍색 테두리를 등록하고 배경은 지정하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        public DraculaErrorFormat()
        {
            DisplayName = "Grokeen Dracula error highlight";
            Border = new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0x79, 0xC6)), 1);
            Border.Freeze();
            ZOrder = 5;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DraculaEditorProvider
    /// desc         : 버퍼 분류를 우선 읽고 필요한 뷰 분류는 세션 차단으로 보호하며 오류 태그는 별도 형식으로 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-23
    /// </summary>
    /// ---------------------------------------------------------------------
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [TagType(typeof(ClassificationTag))]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class DraculaEditorProvider : IViewTaggerProvider
    {
        [Import] internal IBufferTagAggregatorFactoryService BufferTags = null;
        [Import] internal IViewTagAggregatorFactoryService ViewTags = null;
        [Import] internal IClassificationTypeRegistryService Classifications = null;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateTagger
        /// desc         : 편집기가 요청한 인터페이스 또는 구체 태그 형식에 해당하는 뷰 태거를 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            try { return CreateSafe<T>(textView, buffer); }
            catch (Exception) { return null; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateSafe
        /// desc         : 열린 UI 뷰와 지원 태그 형식의 조건을 확인하고 뷰별 태거를 재사용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private ITagger<T> CreateSafe<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            var view = textView as IWpfTextView;
            if (view == null || buffer == null || view.IsClosed || !view.VisualElement.Dispatcher.CheckAccess()) return null;
            var session = DraculaEditorSession.For(view);
            // Dormant wrappers permit later theme switches; native aggregators attach only in Dracula.
            if (typeof(T) == typeof(ClassificationTag) || typeof(T) == typeof(IClassificationTag))
            {
                if (!buffer.ContentType.IsOfType("CSharp")) return null;
                return new DraculaViewTagger<ClassificationTag>(session.CommentTagger(buffer, BufferTags, Classifications, ViewTags)) as ITagger<T>;
            }
            if (typeof(T) == typeof(TextMarkerTag) || typeof(T) == typeof(ITextMarkerTag))
            {
                return new DraculaViewTagger<TextMarkerTag>(session.ErrorTagger(buffer, ViewTags)) as ITagger<T>;
            }
            return null;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DraculaViewListener
    /// desc         : 일반 편집기와 차이 보기의 뷰별 테마 수명을 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-23
    /// </summary>
    /// ---------------------------------------------------------------------
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class DraculaViewListener : IWpfTextViewCreationListener
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TextViewCreated
        /// desc         : 새 텍스트 뷰에 뷰 범위의 테마 수명 관리를 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        public void TextViewCreated(IWpfTextView textView)
        {
            try { if (textView != null && !textView.IsClosed) DraculaEditorSession.For(textView); }
            catch (Exception) { /* Native text views remain usable if a host service is unavailable. */ }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DraculaViewTagger
    /// desc         : 소비자마다 전달 객체를 제공하고 실제 태거 해제 권한은 뷰 세션에 유지한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-23
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class DraculaViewTagger<T> : ITagger<T> where T : ITag
    {
        private readonly ITagger<T> inner;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DraculaViewTagger
        /// desc         : 세션 소유 태거를 참조하며 소비자에게 IDisposable을 노출하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal DraculaViewTagger(ITagger<T> inner) { this.inner = inner; }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { inner.TagsChanged += value; }
            remove { inner.TagsChanged -= value; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetTags
        /// desc         : 세션 소유 태거의 결과를 전달하고 뷰 종료 뒤에는 원래 빈 결과를 유지한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        public IEnumerable<ITagSpan<T>> GetTags(NormalizedSnapshotSpanCollection spans) { return inner.GetTags(spans); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DraculaOptionScope
    /// desc         : 뷰의 원래 로컬/상속 상태를 보존하며 사용자가 바꾼 옵션은 되돌리지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-23
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class DraculaOptionScope : IDisposable
    {
        // The public VS2019/2022 option is absent from SDK15.6 and the VS2017 host.
        private const string DiffOverviewOptionName = "Diff/View/ShowDiffOverviewMargin";
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OwnedOption
        /// desc         : 테마가 바꾼 뷰 옵션의 원래 상태와 사용자 변경 여부를 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class OwnedOption { internal string Name; internal bool HadLocal; internal bool Original; internal bool Touched; }
        private readonly IEditorOptions options;
        private readonly List<OwnedOption> owned = new List<OwnedOption>();
        private bool setting;
        private bool disposed;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DraculaOptionScope
        /// desc         : 현재 뷰의 옵션 변경을 관찰하고 원래 로컬·상속 상태를 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal DraculaOptionScope(IEditorOptions options) { this.options = options; options.OptionChanged += OptionChanged; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Activate
        /// desc         : 지원되는 뷰 옵션만 켜고 복원에 필요한 기존 상태를 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Activate()
        {
            if (disposed || owned.Count != 0) return;
            foreach (var name in new[] { DefaultTextViewHostOptions.ShowErrorsOptionName, DefaultTextViewHostOptions.ShowChangeTrackingMarginOptionName, DiffOverviewOptionName })
            {
                try
                {
                    if (!options.IsOptionDefined(name, false) || options.GetOptionValue<bool>(name)) continue;
                    var item = new OwnedOption { Name = name, HadLocal = options.IsOptionDefined(name, true), Original = false };
                    owned.Add(item);
                    setting = true;
                    try { options.SetOptionValue(name, true); }
                    finally { setting = false; }
                }
                catch (Exception) { /* An unavailable view option must not prevent native editor rendering. */ }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Restore
        /// desc         : 사용자가 변경하지 않은 옵션만 원래 값 또는 상속 상태로 되돌린다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Restore()
        {
            foreach (var item in owned)
            {
                try
                {
                    if (item.Touched || !options.GetOptionValue<bool>(item.Name)) continue;
                    setting = true;
                    try
                    {
                        if (item.HadLocal) options.SetOptionValue(item.Name, item.Original);
                        else options.ClearOptionValue(item.Name);
                    }
                    finally { setting = false; }
                }
                catch (Exception) { /* A closed view may already have released its option store. */ }
            }
            owned.Clear();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OptionChanged
        /// desc         : 외부에서 바뀐 옵션은 이후 복원 대상에서 제외한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OptionChanged(object sender, EditorOptionChangedEventArgs args)
        {
            if (!setting) foreach (var item in owned) if (item.Name == args.OptionId) item.Touched = true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 이 인스턴스의 이벤트와 네이티브 태그 참조를 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            if (disposed) return;
            try { Restore(); }
            finally { disposed = true; options.OptionChanged -= OptionChanged; }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DraculaEditorSession
    /// desc         : 뷰별 테마 상태·옵션·태거 수명을 관리하고 Shell 이벤트를 UI 큐로 넘긴다.
    /// author       : CodexOrig
    /// create date  : 2026-10-23
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class DraculaEditorSession : IDisposable
    {
        private readonly IWpfTextView view;
        private readonly Dispatcher dispatcher;
        private readonly DraculaOptionScope options;
        private readonly Dictionary<ITextBuffer, DraculaCommentTagger> comments = new Dictionary<ITextBuffer, DraculaCommentTagger>();
        private readonly Dictionary<ITextBuffer, DraculaErrorTagger> errors = new Dictionary<ITextBuffer, DraculaErrorTagger>();
        private bool refreshQueued;
        private bool disposed;
        private readonly Func<Guid> readTheme;
        private readonly bool shellEvents;
        private bool nativeClassificationQuery;
        internal bool NativeClassificationQueryActive { get { return nativeClassificationQuery; } }
        internal bool PublishingClassificationChange { get; private set; }
        internal bool Active { get; private set; }
        internal event EventHandler ActiveChanged;
        internal bool OnUiThread { get { return dispatcher.CheckAccess(); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : For
        /// desc         : 같은 텍스트 뷰의 단일 세션을 가져오거나 생성한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static DraculaEditorSession For(IWpfTextView view)
        {
            return view.Properties.GetOrCreateSingletonProperty(typeof(DraculaEditorSession), () => new DraculaEditorSession(view));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DraculaEditorSession
        /// desc         : 뷰의 테마·옵션·종료 이벤트를 같은 UI 수명에 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private DraculaEditorSession(IWpfTextView view) : this(view, DraculaEditorKeys.ReadCurrentTheme, true) { }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DraculaEditorSession
        /// desc         : 뷰의 테마·옵션·종료 이벤트를 같은 UI 수명에 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal DraculaEditorSession(IWpfTextView view, Func<Guid> readTheme, bool shellEvents)
        {
            this.view = view; this.readTheme = readTheme; this.shellEvents = shellEvents;
            dispatcher = view.VisualElement.Dispatcher; options = new DraculaOptionScope(view.Options);
            view.Closed += Closed; if (shellEvents) VSColorTheme.ThemeChanged += ThemeChanged;
            RefreshTheme();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CommentTagger
        /// desc         : 해당 버퍼의 C# 주석 태거를 한 번 생성하여 재사용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal DraculaCommentTagger CommentTagger(ITextBuffer buffer, IBufferTagAggregatorFactoryService factory, IClassificationTypeRegistryService registry, IViewTagAggregatorFactoryService viewFactory)
        {
            DraculaCommentTagger tagger;
            if (!comments.TryGetValue(buffer, out tagger))
            {
                tagger = new DraculaCommentTagger(this, view, buffer, factory, viewFactory, registry.GetClassificationType(DraculaEditorKeys.BlockComment));
                comments.Add(buffer, tagger);
            }
            return tagger;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ErrorTagger
        /// desc         : 해당 버퍼의 언어 서비스 오류 태거를 한 번 생성하여 재사용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal DraculaErrorTagger ErrorTagger(ITextBuffer buffer, IViewTagAggregatorFactoryService factory)
        {
            DraculaErrorTagger tagger;
            if (!errors.TryGetValue(buffer, out tagger))
            {
                tagger = new DraculaErrorTagger(this, view, buffer, factory); errors.Add(buffer, tagger);
            }
            return tagger;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispatch
        /// desc         : 종료된 뷰를 제외하고 작업을 뷰 UI 디스패처에 예약한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "Queues a fire-and-forget callback on the owned IWpfTextView dispatcher at Background priority; it never synchronously waits or switches the caller continuation. Shell-dependent callbacks independently verify main-thread access.")]
        internal void Dispatch(Action action)
        {
            if (disposed || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
            var queued = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { if (!disposed && !view.IsClosed) action(); }));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryEnterNativeClassificationQuery
        /// desc         : 같은 뷰의 모든 자체 주석 태거를 잠시 제외하여 뷰 집계기의 재진입을 차단한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal bool TryEnterNativeClassificationQuery()
        {
            if (disposed || !OnUiThread || nativeClassificationQuery) return false;
            nativeClassificationQuery = true; return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ExitNativeClassificationQuery
        /// desc         : 네이티브 태그 목록 생성이 끝나거나 실패하면 뷰 단위 재진입 차단을 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void ExitNativeClassificationQuery() { nativeClassificationQuery = false; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PublishClassificationChange
        /// desc         : 자체 변경 알림이 네이티브 집계기를 거쳐 돌아오는 동안 새 갱신 예약을 차단한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void PublishClassificationChange(Action publish)
        {
            bool previous = PublishingClassificationChange; PublishingClassificationChange = true;
            try { publish(); }
            finally { PublishingClassificationChange = previous; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ThemeChanged
        /// desc         : Shell 테마 이벤트를 UI 작업으로 모아 현재 테마를 다시 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ThemeChanged(ThemeChangedEventArgs args)
        {
            Dispatch(() => { if (!refreshQueued) { refreshQueued = true; Dispatch(() => { refreshQueued = false; RefreshTheme(); }); } });
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshTheme
        /// desc         : 정확한 테마 GUID에 따라 뷰 옵션과 네이티브 구독을 전환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshTheme()
        {
            bool active = DraculaEditorKeys.IsDracula(readTheme());
            if (active == Active) return;
            Active = active;
            if (active) options.Activate(); else options.Restore();
            var handler = ActiveChanged; if (handler != null) handler(this, EventArgs.Empty);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Closed
        /// desc         : 뷰 종료 시 세션의 구독과 옵션을 정리한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Closed(object sender, EventArgs args) { Dispose(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 이 인스턴스의 이벤트와 네이티브 태그 참조를 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; if (shellEvents) VSColorTheme.ThemeChanged -= ThemeChanged; view.Closed -= Closed;
            foreach (var tagger in comments.Values) tagger.Dispose();
            foreach (var tagger in errors.Values) tagger.Dispose();
            comments.Clear(); errors.Clear(); ActiveChanged = null; options.Dispose();
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DraculaSpanRules
    /// desc         : 원래 분류·진단 형식과 두 시작 문자만 확인하며 파일을 다시 파싱하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-23
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class DraculaSpanRules
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsBlock
        /// desc         : 네이티브 일반 주석 분류의 시작 두 문자만 읽어 블록 주석을 구분한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static bool IsBlock(SnapshotSpan span, IClassificationType type)
        {
            return type != null && String.Equals(type.Classification, DraculaEditorKeys.NativeComment, StringComparison.Ordinal) &&
                span.Length >= 2 && span.Snapshot[span.Start.Position] == '/' && span.Snapshot[span.Start.Position + 1] == '*';
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsError
        /// desc         : 언어 서비스가 보고한 문법·컴파일러·기타 오류만 허용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static bool IsError(string errorType)
        {
            return errorType == PredefinedErrorTypeNames.SyntaxError || errorType == PredefinedErrorTypeNames.CompilerError || errorType == PredefinedErrorTypeNames.OtherError;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryErrorSpan
        /// desc         : 길이 없는 오류 표시는 같은 줄의 인접 문자 한 개에만 한정한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static bool TryErrorSpan(SnapshotSpan source, out SnapshotSpan display)
        {
            display = source;
            if (source.Length > 0) return true;
            var line = source.Snapshot.GetLineFromPosition(source.Start.Position);
            if (line.Length == 0) return false;
            int position = Math.Min(source.Start.Position, line.End.Position - 1);
            display = new SnapshotSpan(source.Snapshot, position, 1);
            return true;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DraculaChangeQueue
    /// desc         : 연속된 네이티브 알림을 하나의 UI 작업으로 합친다.
    /// author       : CodexOrig
    /// create date  : 2026-10-23
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class DraculaChangeQueue : IDisposable
    {
        private readonly object gate = new object();
        private readonly Action<Action> dispatch;
        private readonly Action changed;
        private bool pending;
        private bool disposed;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DraculaChangeQueue
        /// desc         : 태그 변경 알림을 하나의 예약 작업으로 합치는 큐를 생성한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal DraculaChangeQueue(Action<Action> dispatch, Action changed) { this.dispatch = dispatch; this.changed = changed; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Request
        /// desc         : 반복 이벤트를 합치고 실행 시점의 최신 스냅샷 갱신만 요청한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Request()
        {
            lock (gate) { if (disposed || pending) return; pending = true; }
            dispatch(() => { lock (gate) { pending = false; if (disposed) return; } changed(); });
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 이 인스턴스의 이벤트와 네이티브 태그 참조를 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose() { lock (gate) { disposed = true; pending = false; } }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DraculaCommentTagger
    /// desc         : 기존 C# 분류 중 일반 블록 주석에만 추가 표시를 제공한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-23
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class DraculaCommentTagger : ITagger<ClassificationTag>, IDisposable
    {
        private readonly DraculaEditorSession session;
        private readonly ITextView view;
        private readonly ITextBuffer buffer;
        private readonly IBufferTagAggregatorFactoryService factory;
        private readonly IViewTagAggregatorFactoryService viewFactory;
        private readonly IClassificationType blockType;
        private ITagAggregator<IClassificationTag> source;
        private ITagAggregator<IClassificationTag> viewSource;
        private readonly DraculaChangeQueue changes;
        private bool disposed;
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DraculaCommentTagger
        /// desc         : 버퍼 우선 분류와 재진입을 차단한 뷰 분류로 블록 주석 표시를 구성한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal DraculaCommentTagger(DraculaEditorSession session, ITextView view, ITextBuffer buffer, IBufferTagAggregatorFactoryService factory, IViewTagAggregatorFactoryService viewFactory, IClassificationType blockType)
        {
            this.session = session; this.view = view; this.buffer = buffer; this.factory = factory; this.viewFactory = viewFactory; this.blockType = blockType;
            changes = new DraculaChangeQueue(session.Dispatch, RaiseChanged);
            session.ActiveChanged += ActiveChanged; UpdateSource();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateSource
        /// desc         : 활성 테마에서만 네이티브 태거를 구독하고 전환·종료 시 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateSource()
        {
            ReleaseSource(ref source); ReleaseSource(ref viewSource);
            if (!disposed && session.Active && blockType != null)
            {
                try { source = factory.CreateTagAggregator<IClassificationTag>(buffer); source.TagsChanged += SourceChanged; }
                catch (Exception) { ReleaseSource(ref source); }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReleaseSource
        /// desc         : 버퍼 또는 뷰 집계기의 즉시 이벤트와 소유 참조를 모두 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ReleaseSource(ref ITagAggregator<IClassificationTag> ownedSource)
        {
            var previous = ownedSource; ownedSource = null;
            if (previous != null)
            {
                try { previous.TagsChanged -= SourceChanged; }
                catch (Exception) { /* Native services may already be released during view shutdown. */ }
                try { previous.Dispose(); }
                catch (Exception) { /* View shutdown must still release the remaining subscriptions. */ }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActiveChanged
        /// desc         : 테마 전환 후 네이티브 구독과 표시를 갱신한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ActiveChanged(object sender, EventArgs args) { UpdateSource(); RaiseChanged(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SourceChanged
        /// desc         : 반복되는 네이티브 태그 이벤트를 한 UI 갱신 요청으로 합친다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SourceChanged(object sender, TagsChangedEventArgs args)
        {
            if (sender is DraculaCommentTagger || (session.OnUiThread && session.PublishingClassificationChange)) return;
            changes.Request();
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RaiseChanged
        /// desc         : 문자열 재파싱 없이 실행 시점의 최신 버퍼 범위에 표시 갱신을 알린다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RaiseChanged()
        {
            if (disposed) return;
            var handler = TagsChanged;
            if (handler != null) session.PublishClassificationChange(() => handler(this, new SnapshotSpanEventArgs(new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetTags
        /// desc         : 지원 범위의 표시 태그를 반환하고 종료되거나 실패한 서비스는 비활성화한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (session.NativeClassificationQueryActive) return new ITagSpan<ClassificationTag>[0];
            try { return CollectTags(spans); }
            catch (Exception) { return new ITagSpan<ClassificationTag>[0]; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CollectTags
        /// desc         : 요청과 교차하는 원래 네이티브 태그 범위만 사용하여 표시 태그를 만든다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private IList<ITagSpan<ClassificationTag>> CollectTags(NormalizedSnapshotSpanCollection spans)
        {
            var result = new List<ITagSpan<ClassificationTag>>();
            if (disposed || source == null || !session.Active || !session.OnUiThread || spans == null || spans.Count == 0) return result;
            var snapshot = spans[0].Snapshot;
            if (!Object.ReferenceEquals(snapshot.TextBuffer, buffer)) return result;
            var candidates = new List<SnapshotSpan>(); var competing = new List<SnapshotSpan>(); var seen = new HashSet<Span>();
            var nativeTags = new List<IMappingTagSpan<IClassificationTag>>(source.GetTags(spans));
            if (nativeTags.Count == 0 && viewFactory != null && session.TryEnterNativeClassificationQuery())
            {
                try
                {
                    if (viewSource == null)
                    {
                        try { viewSource = viewFactory.CreateTagAggregator<IClassificationTag>(view); viewSource.TagsChanged += SourceChanged; }
                        catch (Exception) { ReleaseSource(ref viewSource); throw; }
                    }
                    nativeTags = new List<IMappingTagSpan<IClassificationTag>>(viewSource.GetTags(spans));
                }
                finally { session.ExitNativeClassificationQuery(); }
            }
            foreach (var tag in nativeTags)
            {
                var type = tag.Tag.ClassificationType;
                if (type != null && (type.Classification == DraculaEditorKeys.BlockComment || type.IsOfType(DraculaEditorKeys.BlockComment))) continue;
                foreach (var full in tag.Span.GetSpans(snapshot))
                {
                    if (DraculaSpanRules.IsBlock(full, type)) { if (seen.Add(full.Span)) candidates.Add(full); }
                    else if (type != null && type.IsOfType(DraculaEditorKeys.NativeComment)) competing.Add(full);
                }
            }
            foreach (var full in candidates)
            {
                bool occupied = false; foreach (var other in competing) if (other.IntersectsWith(full)) { occupied = true; break; }
                if (!occupied && spans.IntersectsWith(full)) result.Add(new TagSpan<ClassificationTag>(full, new ClassificationTag(blockType)));
            }
            return result;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 이 인스턴스의 이벤트와 네이티브 태그 참조를 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; changes.Dispose(); session.ActiveChanged -= ActiveChanged; UpdateSource(); TagsChanged = null;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DraculaErrorTagger
    /// desc         : 언어 서비스가 제공한 실제 오류에만 추가 표시를 제공한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-23
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class DraculaErrorTagger : ITagger<TextMarkerTag>, IDisposable
    {
        private readonly DraculaEditorSession session;
        private readonly ITextView view;
        private readonly ITextBuffer buffer;
        private readonly IViewTagAggregatorFactoryService factory;
        private ITagAggregator<IErrorTag> source;
        private readonly DraculaChangeQueue changes;
        private bool disposed;
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DraculaErrorTagger
        /// desc         : 기존 오류 태그를 읽기만 하는 뷰 표시 태거를 구성한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        internal DraculaErrorTagger(DraculaEditorSession session, ITextView view, ITextBuffer buffer, IViewTagAggregatorFactoryService factory)
        {
            this.session = session; this.view = view; this.buffer = buffer; this.factory = factory;
            changes = new DraculaChangeQueue(session.Dispatch, RaiseChanged);
            session.ActiveChanged += ActiveChanged; UpdateSource();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateSource
        /// desc         : 활성 테마에서만 네이티브 태거를 구독하고 전환·종료 시 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateSource()
        {
            var previous = source; source = null;
            if (previous != null)
            {
                try { previous.TagsChanged -= SourceChanged; }
                catch (Exception) { /* Native services may already be released during view shutdown. */ }
                try { previous.Dispose(); }
                catch (Exception) { /* View shutdown must still release the remaining subscriptions. */ }
            }
            if (!disposed && session.Active)
            {
                try { source = factory.CreateTagAggregator<IErrorTag>(view); source.TagsChanged += SourceChanged; }
                catch (Exception) { var failed = source; source = null; if (failed != null) try { failed.Dispose(); } catch (Exception) { } }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActiveChanged
        /// desc         : 테마 전환 후 네이티브 구독과 표시를 갱신한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ActiveChanged(object sender, EventArgs args) { UpdateSource(); RaiseChanged(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SourceChanged
        /// desc         : 반복되는 네이티브 태그 이벤트를 한 UI 갱신 요청으로 합친다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SourceChanged(object sender, TagsChangedEventArgs args) { changes.Request(); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RaiseChanged
        /// desc         : 문자열 재파싱 없이 실행 시점의 최신 버퍼 범위에 표시 갱신을 알린다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RaiseChanged()
        {
            if (disposed) return;
            var handler = TagsChanged; if (handler != null) handler(this, new SnapshotSpanEventArgs(new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length)));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetTags
        /// desc         : 지원 범위의 표시 태그를 반환하고 종료되거나 실패한 서비스는 비활성화한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            try { return CollectTags(spans); }
            catch (Exception) { return new ITagSpan<TextMarkerTag>[0]; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CollectTags
        /// desc         : 요청과 교차하는 원래 네이티브 태그 범위만 사용하여 표시 태그를 만든다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        private IList<ITagSpan<TextMarkerTag>> CollectTags(NormalizedSnapshotSpanCollection spans)
        {
            var result = new List<ITagSpan<TextMarkerTag>>();
            if (disposed || source == null || !session.Active || !session.OnUiThread || spans == null || spans.Count == 0) return result;
            var snapshot = spans[0].Snapshot; var seen = new HashSet<Span>();
            if (!Object.ReferenceEquals(snapshot.TextBuffer, buffer)) return result;
            foreach (var tag in source.GetTags(spans))
            {
                if (!DraculaSpanRules.IsError(tag.Tag.ErrorType)) continue;
                foreach (var full in tag.Span.GetSpans(snapshot))
                {
                    SnapshotSpan display;
                    if (DraculaSpanRules.TryErrorSpan(full, out display) && spans.IntersectsWith(display) && seen.Add(display.Span))
                        result.Add(new TagSpan<TextMarkerTag>(display, new TextMarkerTag(DraculaEditorKeys.ErrorHighlight)));
                }
            }
            return result;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 이 인스턴스의 이벤트와 네이티브 태그 참조를 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-23
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; changes.Dispose(); session.ActiveChanged -= ActiveChanged; UpdateSource(); TagsChanged = null;
        }
    }
}
