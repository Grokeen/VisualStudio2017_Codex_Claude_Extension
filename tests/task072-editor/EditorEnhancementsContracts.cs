using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

// Private source-compiled SDK contracts. No IDE, Window.Show, native input or Roslyn host is used.
internal static class Product
{
    internal static Assembly Assembly;
    internal static Type Type(string name) { return Assembly.GetType("Grokeen.VisualStudioThemes." + name, true); }
    internal static object New(string name, params object[] args)
    { return Activator.CreateInstance(Type(name), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, args, null); }
    internal static object Call(object target, string name, params object[] args)
    { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args); }
    internal static object Static(string type, string name, params object[] args)
    { return Type(type).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Invoke(null, args); }
}
internal static class DraculaEditorKeys
{
    internal const string ErrorHighlight = "Grokeen.Dracula.ErrorHighlight";
    internal static readonly Guid ThemeId = new Guid("8ec84919-8a34-422a-abe1-cc238ad4d4b1");
    internal static bool IsDracula(Guid id) { return (bool)Product.Static("DraculaEditorKeys", "IsDracula", id); }
    internal static Guid ReadSelectedTheme(ISettingsManager manager, bool highContrast) { return (Guid)Product.Static("DraculaEditorKeys", "ReadSelectedTheme", manager, highContrast); }
}
internal static class DraculaSpanRules
{
    internal static bool IsBlock(SnapshotSpan span, IClassificationType type) { return (bool)Product.Static("DraculaSpanRules", "IsBlock", span, type); }
    internal static bool IsError(string type) { return (bool)Product.Static("DraculaSpanRules", "IsError", new object[] { type }); }
    internal static bool TryErrorSpan(SnapshotSpan source, out SnapshotSpan display)
    { var args = new object[] { source, null }; bool result = (bool)Product.Static("DraculaSpanRules", "TryErrorSpan", args); display = (SnapshotSpan)args[1]; return result; }
}
internal sealed class DraculaOptionScope : IDisposable
{
    private readonly object value;
    internal DraculaOptionScope(IEditorOptions options) { value = Product.New("DraculaOptionScope", options); }
    internal void Activate() { Product.Call(value, "Activate"); }
    internal void Restore() { Product.Call(value, "Restore"); }
    public void Dispose() { ((IDisposable)value).Dispose(); }
}
internal sealed class DraculaEditorSession : IDisposable
{
    internal readonly object Value;
    internal DraculaEditorSession(IWpfTextView view, Func<Guid> readTheme, bool shellEvents)
    { Value = Product.New("DraculaEditorSession", view, readTheme, shellEvents); }
    internal object CommentTagger(ITextBuffer buffer, IBufferTagAggregatorFactoryService factory, IClassificationTypeRegistryService registry, IViewTagAggregatorFactoryService viewFactory)
    { return Product.Call(Value, "CommentTagger", buffer, factory, registry, viewFactory); }
    internal bool NativeQueryActive { get { return (bool)Product.Call(Value, "get_NativeClassificationQueryActive"); } }
    internal object ErrorTagger(ITextBuffer buffer, IViewTagAggregatorFactoryService factory)
    { return Product.Call(Value, "ErrorTagger", buffer, factory); }
    internal void Refresh() { Product.Call(Value, "RefreshTheme"); }
    public void Dispose() { ((IDisposable)Value).Dispose(); }
}
internal sealed class DraculaEditorProvider
{
    internal IBufferTagAggregatorFactoryService BufferTags;
    internal IViewTagAggregatorFactoryService ViewTags;
    internal IClassificationTypeRegistryService Classifications;
    private readonly object value = Product.New("DraculaEditorProvider");
    internal ITagger<T> CreateTagger<T>(ITextView view, ITextBuffer buffer) where T : ITag
    {
        foreach (var name in new[] { "BufferTags", "ViewTags", "Classifications" })
            value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this));
        return (ITagger<T>)value.GetType().GetMethod("CreateTagger").MakeGenericMethod(typeof(T)).Invoke(value, new object[] { view, buffer });
    }
}
internal sealed class DraculaChangeQueue : IDisposable
{
    private readonly object value;
    internal DraculaChangeQueue(Action<Action> dispatch, Action changed) { value = Product.New("DraculaChangeQueue", dispatch, changed); }
    internal void Request() { Product.Call(value, "Request"); }
    public void Dispose() { ((IDisposable)value).Dispose(); }
}

internal sealed class InterfaceProxy : RealProxy
{
    private readonly Func<MethodInfo, object[], object> invoke;
    private readonly Type interfaceType;
    private InterfaceProxy(Type type, Func<MethodInfo, object[], object> invoke) : base(type) { this.invoke = invoke; interfaceType = type; }
    internal static T For<T>(Func<MethodInfo, object[], object> invoke) where T : class
    { return (T)new InterfaceProxy(typeof(T), invoke).GetTransparentProxy(); }
    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        try
        {
            object value;
            if (call.MethodBase.DeclaringType == typeof(object))
            {
                if (call.MethodName == "GetHashCode") value = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
                else if (call.MethodName == "GetType") value = interfaceType;
                else if (call.MethodName == "Equals") value = Object.ReferenceEquals(GetTransparentProxy(), call.Args[0]);
                else value = "Private SDK contract proxy";
            }
            else value = invoke((MethodInfo)call.MethodBase, call.Args);
            return new ReturnMessage(value, null, 0, call.LogicalCallContext, call);
        }
        catch (Exception error) { return new ReturnMessage(error, call); }
    }
}

internal sealed class FakeOptions
{
    internal readonly Dictionary<string, bool> Local = new Dictionary<string, bool>();
    internal readonly Dictionary<string, bool> Parent = new Dictionary<string, bool>();
    internal EventHandler<EditorOptionChangedEventArgs> Changed;
    internal bool Throw;
    internal int Writes;
    internal readonly IEditorOptions Value;
    internal FakeOptions()
    {
        Parent[DefaultTextViewHostOptions.ShowErrorsOptionName] = false;
        Parent[DefaultTextViewHostOptions.ShowChangeTrackingMarginOptionName] = false;
        Value = InterfaceProxy.For<IEditorOptions>((m, a) =>
        {
            if (m.Name == "add_OptionChanged") { Changed += (EventHandler<EditorOptionChangedEventArgs>)a[0]; return null; }
            if (m.Name == "remove_OptionChanged") { Changed -= (EventHandler<EditorOptionChangedEventArgs>)a[0]; return null; }
            if (Throw) throw new ObjectDisposedException("options");
            var name = a.Length > 0 ? (string)a[0] : null;
            switch (m.Name)
            {
                case "IsOptionDefined": return Local.ContainsKey(name) || (!(bool)a[1] && Parent.ContainsKey(name));
                case "GetOptionValue": return Local.ContainsKey(name) ? Local[name] : Parent[name];
                case "SetOptionValue": Set(name, (bool)a[1]); return null;
                case "ClearOptionValue": Writes++; bool removed = Local.Remove(name); Raise(name); return removed;
            }
            throw new NotSupportedException(m.Name);
        });
    }
    internal void Set(string name, bool value) { Local[name] = value; Writes++; Raise(name); }
    private void Raise(string name) { var h = Changed; if (h != null) h(Value, new EditorOptionChangedEventArgs(name)); }
}

internal sealed class FakeText
{
    internal readonly string Text;
    internal readonly ITextSnapshot Snapshot;
    internal readonly ITextBuffer Buffer;
    internal readonly PropertyCollection Properties = new PropertyCollection();
    internal bool CSharp = true;
    internal FakeText(string text)
    {
        Text = text;
        var content = InterfaceProxy.For<IContentType>((m, a) => m.Name == "IsOfType" ? (object)(CSharp && (string)a[0] == "CSharp") : "text");
        Buffer = InterfaceProxy.For<ITextBuffer>((m, a) =>
        {
            if (m.Name == "get_CurrentSnapshot") return Snapshot;
            if (m.Name == "get_ContentType") return content;
            if (m.Name == "get_Properties") return Properties;
            throw new NotSupportedException(m.Name);
        });
        Snapshot = InterfaceProxy.For<ITextSnapshot>((m, a) =>
        {
            switch (m.Name)
            {
                case "get_Length": return Text.Length;
                case "get_TextBuffer": return Buffer;
                case "get_Item": return Text[(int)a[0]];
                case "GetText": return a.Length == 0 ? Text : Text.Substring((int)a[0], (int)a[1]);
                case "GetLineFromPosition": return Line((int)a[0]);
                case "Equals": return Object.ReferenceEquals(Snapshot, a[0]);
                case "GetHashCode": return Text.GetHashCode();
            }
            throw new NotSupportedException(m.Name);
        });
    }
    private ITextSnapshotLine Line(int position)
    {
        int start = position == 0 ? 0 : Text.LastIndexOf('\n', Math.Min(position - 1, Text.Length - 1)) + 1;
        int end = Text.IndexOf('\n', position); if (end < 0) end = Text.Length;
        if (end > start && Text[end - 1] == '\r') end--;
        return InterfaceProxy.For<ITextSnapshotLine>((m, a) =>
        {
            switch (m.Name)
            {
                case "get_Length": return end - start;
                case "get_Start": return new SnapshotPoint(Snapshot, start);
                case "get_End": return new SnapshotPoint(Snapshot, end);
                case "get_Snapshot": return Snapshot;
            }
            throw new NotSupportedException(m.Name);
        });
    }
    internal SnapshotSpan Span(int start, int length) { return new SnapshotSpan(Snapshot, start, length); }
}

internal sealed class FakeAggregator<T> where T : ITag
{
    internal readonly List<IMappingTagSpan<T>> Tags = new List<IMappingTagSpan<T>>();
    internal EventHandler<TagsChangedEventArgs> Changed;
    internal int Disposals;
    internal bool Throw;
    internal Func<IEnumerable<IMappingTagSpan<T>>> GetTagsOverride;
    internal readonly ITagAggregator<T> Value;
    internal FakeAggregator()
    {
        Value = InterfaceProxy.For<ITagAggregator<T>>((m, a) =>
        {
            switch (m.Name)
            {
                case "add_TagsChanged": Changed += (EventHandler<TagsChangedEventArgs>)a[0]; return null;
                case "remove_TagsChanged": Changed -= (EventHandler<TagsChangedEventArgs>)a[0]; return null;
                case "Dispose": Disposals++; return null;
                case "GetTags": if (Throw) throw new InvalidOperationException("native query failed"); return GetTagsOverride == null ? Tags : GetTagsOverride();
            }
            throw new NotSupportedException(m.Name);
        });
    }
    internal void Add(SnapshotSpan span, T tag)
    {
        var mapping = InterfaceProxy.For<IMappingSpan>((m, a) =>
        {
            if (m.Name == "GetSpans") return new NormalizedSnapshotSpanCollection(span);
            throw new NotSupportedException(m.Name);
        });
        Tags.Add(new MappingTagSpan<T>(mapping, tag));
    }
    internal void Fire() { Fire(Value); }
    internal void Fire(object sender)
    {
        var h = Changed;
        var span = Tags.Count == 0 ? InterfaceProxy.For<IMappingSpan>((m, a) => { throw new NotSupportedException(m.Name); }) : Tags[0].Span;
        if (h != null) h(sender, new TagsChangedEventArgs(span));
    }
}

internal sealed class FakeView : IDisposable
{
    internal Guid Theme = DraculaEditorKeys.ThemeId;
    internal bool Closed;
    internal EventHandler ClosedHandlers;
    internal readonly FakeOptions Options = new FakeOptions();
    internal readonly PropertyCollection Properties = new PropertyCollection();
    internal readonly IWpfTextView Value;
    internal readonly DraculaEditorSession Session;
    internal FakeView()
    {
        var visual = new Border();
        Value = InterfaceProxy.For<IWpfTextView>((m, a) =>
        {
            switch (m.Name)
            {
                case "get_VisualElement": return visual;
                case "get_IsClosed": return Closed;
                case "get_Properties": return Properties;
                case "get_Options": return Options.Value;
                case "add_Closed": ClosedHandlers += (EventHandler)a[0]; return null;
                case "remove_Closed": ClosedHandlers -= (EventHandler)a[0]; return null;
            }
            throw new NotSupportedException(m.Name);
        });
        Session = new DraculaEditorSession(Value, () => Theme, false);
        Properties.AddProperty(Product.Type("DraculaEditorSession"), Session.Value);
    }
    internal void Refresh()
    { Session.Refresh(); }
    public void Dispose() { Closed = true; var h = ClosedHandlers; if (h != null) h(Value, EventArgs.Empty); Session.Dispose(); }
}

internal static class EditorEnhancementsContracts
{
    private static int passed;
    private static int failed;
    private static void Check(string name, bool result)
    { Console.WriteLine((result ? "PASS " : "FAIL ") + name); if (result) passed++; else failed++; }
    private static IClassificationType Type(string name, bool comment)
    { return InterfaceProxy.For<IClassificationType>((m, a) => m.Name == "get_Classification" ? (object)name : m.Name == "IsOfType" ? (object)(comment && (string)a[0] == "comment") : new IClassificationType[0]); }
    private static void Pump()
    { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    [STAThread]
    private static int Main(string[] arguments)
    {
        Product.Assembly = Assembly.LoadFrom(arguments[0]);
        Console.WriteLine("PRODUCT " + Product.Assembly.Location);
        try { Rules(); ThemeSettings(); Options(); OptionalDiffOptions(); Taggers(); ViewOnlyClassifications(); FormatDefinitions(); Queue(); }
        catch (Exception error) { Console.WriteLine("FAIL unhandled " + error); failed++; }
        Console.WriteLine("TOTAL " + passed + " " + failed);
        return failed == 0 ? 0 : 1;
    }
    private static void Rules()
    {
        Check("SDK tagger is covariant", (typeof(ITagger<>).GetGenericArguments()[0].GenericParameterAttributes & GenericParameterAttributes.Covariant) != 0);
        foreach (var text in new[] { "/* block */", "/**/", "/* first\nsecond */" })
        { var t = new FakeText(text); Check("plain native block " + text.Replace('\n', ' '), DraculaSpanRules.IsBlock(t.Span(0, text.Length), Type("comment", true))); }
        foreach (var name in new[] { "xml doc comment - text", "string", "verbatim string", "interpolated string", "raw string", "custom comment" })
        { var t = new FakeText("/** documentation */"); Check("preserve " + name, !DraculaSpanRules.IsBlock(t.Span(0, t.Text.Length), Type(name, name.Contains("comment")))); }
        var single = new FakeText("// line"); Check("line comment native", !DraculaSpanRules.IsBlock(single.Span(0, single.Text.Length), Type("comment", true)));
        foreach (var type in new[] { PredefinedErrorTypeNames.SyntaxError, PredefinedErrorTypeNames.CompilerError, PredefinedErrorTypeNames.OtherError }) Check("error accepted " + type, DraculaSpanRules.IsError(type));
        foreach (var type in new[] { "warning", "suggestion", "hint", "other", "", null }) Check("non error excluded " + type, !DraculaSpanRules.IsError(type));
        var textValue = new FakeText("abc\r\n\nxyz"); SnapshotSpan display;
        Check("nonzero diagnostic unchanged", DraculaSpanRules.TryErrorSpan(textValue.Span(1, 2), out display) && display == textValue.Span(1, 2));
        Check("zero anchor same line", DraculaSpanRules.TryErrorSpan(textValue.Span(1, 0), out display) && display == textValue.Span(1, 1));
        Check("zero at newline stays before newline", DraculaSpanRules.TryErrorSpan(textValue.Span(3, 0), out display) && display == textValue.Span(2, 1));
        Check("zero at EOF preceding char", DraculaSpanRules.TryErrorSpan(textValue.Span(textValue.Text.Length, 0), out display) && display == textValue.Span(textValue.Text.Length - 1, 1));
        Check("empty line no arbitrary previous highlight", !DraculaSpanRules.TryErrorSpan(textValue.Span(5, 0), out display));
        Check("empty file native marker only", !DraculaSpanRules.TryErrorSpan(new FakeText("").Span(0, 0), out display));
        Check("exact Dracula GUID", DraculaEditorKeys.IsDracula(DraculaEditorKeys.ThemeId));
        Check("unknown GUID inactive", !DraculaEditorKeys.IsDracula(Guid.Empty) && !DraculaEditorKeys.IsDracula(Guid.NewGuid()));
    }
    private static void Options()
    {
        string errors = DefaultTextViewHostOptions.ShowErrorsOptionName, changes = DefaultTextViewHostOptions.ShowChangeTrackingMarginOptionName;
        var options = new FakeOptions(); var scope = new DraculaOptionScope(options.Value);
        scope.Activate(); Check("view two options active", options.Local.Count == 2 && options.Local.Values.All(v => v));
        scope.Restore(); Check("inherited false restored without local override", options.Local.Count == 0 && options.Parent.Values.All(v => !v)); scope.Dispose();
        options = new FakeOptions(); options.Local[errors] = false; scope = new DraculaOptionScope(options.Value); scope.Activate(); scope.Dispose();
        Check("local false restored as local", options.Local.Count == 1 && !options.Local[errors]);
        options = new FakeOptions(); options.Parent[errors] = options.Parent[changes] = true; scope = new DraculaOptionScope(options.Value); scope.Activate(); scope.Dispose();
        Check("initial true never written", options.Writes == 0);
        options = new FakeOptions(); scope = new DraculaOptionScope(options.Value); scope.Activate(); options.Set(errors, false); options.Set(changes, false); options.Set(changes, true); scope.Dispose();
        Check("user false preserved", options.Local.ContainsKey(errors) && !options.Local[errors]);
        Check("user toggled true preserved", options.Local.ContainsKey(changes) && options.Local[changes]);
        Check("option subscription released", options.Changed == null);
        options = new FakeOptions(); scope = new DraculaOptionScope(options.Value); options.Throw = true; scope.Activate(); scope.Dispose(); Check("released option service fail closed", options.Changed == null);
    }
    private static void OptionalDiffOptions()
    {
        const string diff = "Diff/View/ShowDiffOverviewMargin";
        var options = new FakeOptions(); options.Parent[diff] = false;
        var scope = new DraculaOptionScope(options.Value); scope.Activate();
        Check("defined diff overview becomes view local true", options.Local.Count == 3 && options.Local[diff]);
        Check("diff overview activation never changes parent global values", options.Parent.Count == 3 && options.Parent.Values.All(v => !v));
        scope.Dispose();
        Check("inherited diff overview restores no local override", options.Local.Count == 0 && !options.Parent[diff]);
        options = new FakeOptions(); options.Parent[diff] = false; options.Local[diff] = false;
        scope = new DraculaOptionScope(options.Value); scope.Activate(); scope.Dispose();
        Check("local diff overview false restores as local", options.Local.Count == 1 && options.Local.ContainsKey(diff) && !options.Local[diff]);
        options = new FakeOptions(); options.Parent[diff] = false; scope = new DraculaOptionScope(options.Value); scope.Activate();
        options.Set(diff, false); scope.Dispose();
        Check("user diff overview false preserved", options.Local.Count == 1 && !options.Local[diff]);
        options = new FakeOptions(); options.Parent[diff] = false; scope = new DraculaOptionScope(options.Value); scope.Activate();
        options.Set(diff, false); options.Set(diff, true); scope.Dispose();
        Check("user diff overview toggled true preserved", options.Local.Count == 1 && options.Local[diff]);
        options = new FakeOptions(); scope = new DraculaOptionScope(options.Value); scope.Activate();
        Check("old host missing diff option never written", options.Local.Count == 2 && !options.Local.ContainsKey(diff)); scope.Dispose();
        options = new FakeOptions(); options.Parent[diff] = true; scope = new DraculaOptionScope(options.Value); scope.Activate();
        bool untouched = !options.Local.ContainsKey(diff); scope.Dispose();
        Check("initial true diff option remains inherited", untouched && options.Local.Count == 0 && options.Parent[diff]);
    }
    private static void ThemeSettings()
    {
        const string current = "Microsoft.VisualStudio.ColorThemeNew", legacy = "Microsoft.VisualStudio.ColorTheme";
        var values = new Dictionary<string, string>(); var reads = new List<string>(); bool unavailable = false;
        var manager = InterfaceProxy.For<ISettingsManager>((m, a) =>
        {
            if (m.Name != "GetValueOrDefault") throw new InvalidOperationException("Only readonly settings access is permitted.");
            reads.Add((string)a[0]); if (unavailable) throw new InvalidOperationException("unavailable settings");
            string value; return values.TryGetValue((string)a[0], out value) ? value : a[1];
        });
        values[current] = DraculaEditorKeys.ThemeId.ToString();
        Check("settings current selected Dracula GUID", DraculaEditorKeys.ReadSelectedTheme(manager, false) == DraculaEditorKeys.ThemeId && reads.SequenceEqual(new[] { current }));
        values[legacy] = DraculaEditorKeys.ThemeId.ToString(); values[current] = Guid.NewGuid().ToString();
        Check("settings current other theme overrides stale legacy Dracula", !DraculaEditorKeys.IsDracula(DraculaEditorKeys.ReadSelectedTheme(manager, false)));
        values[current] = "malformed";
        Check("settings malformed current rejects stale legacy", DraculaEditorKeys.ReadSelectedTheme(manager, false) == Guid.Empty);
        values[current] = "";
        Check("settings empty current rejects stale legacy", DraculaEditorKeys.ReadSelectedTheme(manager, false) == Guid.Empty);
        values.Remove(current);
        Check("settings missing current uses legacy GUID", DraculaEditorKeys.ReadSelectedTheme(manager, false) == DraculaEditorKeys.ThemeId);
        values.Clear();
        Check("settings both missing inactive", DraculaEditorKeys.ReadSelectedTheme(manager, false) == Guid.Empty);
        values[current] = DraculaEditorKeys.ThemeId.ToString(); reads.Clear();
        Check("settings high contrast inactive without reads", DraculaEditorKeys.ReadSelectedTheme(manager, true) == Guid.Empty && reads.Count == 0);
        Check("settings unavailable service inactive", DraculaEditorKeys.ReadSelectedTheme(null, false) == Guid.Empty);
        unavailable = true;
        Check("settings native read failure inactive", DraculaEditorKeys.ReadSelectedTheme(manager, false) == Guid.Empty); unavailable = false;
        values.Remove(current); values[legacy] = "invalid";
        Check("settings malformed legacy inactive", DraculaEditorKeys.ReadSelectedTheme(manager, false) == Guid.Empty);
    }
    private static void Taggers()
    {
        using (var view = new FakeView())
        {
            var text = new FakeText("/* entire\nblock */ // rest");
            var native = new FakeAggregator<IClassificationTag>();
            var errorNative = new FakeAggregator<IErrorTag>();
            var registry = InterfaceProxy.For<IClassificationTypeRegistryService>((m, a) => Type((string)a[0], true));
            int bufferFactory = 0, viewFactory = 0;
            var provider = new DraculaEditorProvider
            {
                BufferTags = InterfaceProxy.For<IBufferTagAggregatorFactoryService>((m, a) => { bufferFactory++; if (!Object.ReferenceEquals(a[0], text.Buffer)) throw new Exception("wrong buffer"); return native.Value; }),
                ViewTags = InterfaceProxy.For<IViewTagAggregatorFactoryService>((m, a) => { viewFactory++; if (!Object.ReferenceEquals(a[0], view.Value)) throw new Exception("wrong view"); return errorNative.Value; }),
                Classifications = registry
            };
            var comments = provider.CreateTagger<IClassificationTag>(view.Value, text.Buffer);
            var errors = provider.CreateTagger<ITextMarkerTag>(view.Value, text.Buffer);
            if (comments == null) view.Session.CommentTagger(text.Buffer, provider.BufferTags, registry, provider.ViewTags);
            if (errors == null) view.Session.ErrorTagger(text.Buffer, provider.ViewTags);
            Check("factory interface classification covariance", comments != null);
            Check("factory interface marker covariance", errors != null);
            Check("factory concrete consumers have separate facade", !Object.ReferenceEquals(comments, provider.CreateTagger<ClassificationTag>(view.Value, text.Buffer)) && !Object.ReferenceEquals(errors, provider.CreateTagger<TextMarkerTag>(view.Value, text.Buffer)));
            Check("factory interface consumers have separate facade", !Object.ReferenceEquals(comments, provider.CreateTagger<IClassificationTag>(view.Value, text.Buffer)) && !Object.ReferenceEquals(errors, provider.CreateTagger<ITextMarkerTag>(view.Value, text.Buffer)));
            Check("factory paths buffer classification and view errors", bufferFactory == 1 && viewFactory == 1);
            Check("unsupported tag type null", provider.CreateTagger<IErrorTag>(view.Value, text.Buffer) == null);
            Check("null buffer fail closed", provider.CreateTagger<IClassificationTag>(view.Value, null) == null);
            native.Add(text.Span(0, 18), new ClassificationTag(Type("comment", true)));
            var partial = new NormalizedSnapshotSpanCollection(text.Span(8, 1));
            var result = comments.GetTags(partial).ToArray();
            Check("partial request preserves full native block extent", result.Length == 1 && result[0].Span == text.Span(0, 18));
            native.Add(text.Span(0, 18), new ClassificationTag(Type("third party comment", true)));
            Check("competing native subtype preserved", !comments.GetTags(partial).Any()); native.Tags.RemoveAt(1);
            native.Add(text.Span(0, 18), new ClassificationTag(Type("comment", true)));
            Check("duplicate mapping block emitted once", comments.GetTags(partial).Count() == 1);
            native.Throw = true; Check("native query failure returns empty", !comments.GetTags(partial).Any()); native.Throw = false;
            Check("snapshot buffer mismatch empty", !comments.GetTags(new NormalizedSnapshotSpanCollection(new FakeText("x").Span(0, 1))).Any());
            foreach (var type in new[] { PredefinedErrorTypeNames.SyntaxError, "warning", "suggestion" }) errorNative.Add(text.Span(1, 0), new ErrorTag(type));
            var errorResult = errors.GetTags(new NormalizedSnapshotSpanCollection(text.Span(0, text.Text.Length))).ToArray();
            Check("real errors only with zero anchor display", errorResult.Length == 1 && errorResult[0].Span == text.Span(1, 1) && errorResult[0].Tag.Type == DraculaEditorKeys.ErrorHighlight);
            var secondComments = provider.CreateTagger<IClassificationTag>(view.Value, text.Buffer);
            var secondErrors = provider.CreateTagger<ITextMarkerTag>(view.Value, text.Buffer);
            Check("classification facade never owns IDisposable", !(comments is IDisposable) && !(secondComments is IDisposable));
            Check("marker facade never owns IDisposable", !(errors is IDisposable) && !(secondErrors is IDisposable));
            int firstCalls = 0, secondCalls = 0;
            EventHandler<SnapshotSpanEventArgs> firstHandler = (s, a) => firstCalls++;
            EventHandler<SnapshotSpanEventArgs> secondHandler = (s, a) => secondCalls++;
            comments.TagsChanged += firstHandler; secondComments.TagsChanged += secondHandler;
            comments.TagsChanged -= firstHandler;
            var disposableConsumer = comments as IDisposable; if (disposableConsumer != null) disposableConsumer.Dispose();
            var disposableMarker = errors as IDisposable; if (disposableMarker != null) disposableMarker.Dispose();
            native.Fire(); Pump();
            Check("one consumer removal preserves other notifications", firstCalls == 0 && secondCalls == 1);
            Check("one consumer removal preserves other classification data", secondComments.GetTags(partial).Count() == 1);
            Check("one consumer removal preserves other marker data", secondErrors.GetTags(new NormalizedSnapshotSpanCollection(text.Span(0, text.Text.Length))).Count() == 1);
            var reacquiredComments = provider.CreateTagger<IClassificationTag>(view.Value, text.Buffer);
            Check("consumer reacquisition preserves native subscription", reacquiredComments.GetTags(partial).Count() == 1 && bufferFactory == 1 && viewFactory == 1);
            secondComments.TagsChanged -= secondHandler;
            int notifications = 0; comments.TagsChanged += (s, a) => notifications++;
            for (int i = 0; i < 10000; i++) native.Fire();
            Check("native events delayed to UI", notifications == 0); Pump();
            Check("native 10000 events coalesced to one", notifications == 1);
            view.Theme = Guid.NewGuid(); view.Refresh();
            Check("theme switch detaches native aggregators", native.Disposals == 1 && errorNative.Disposals == 1 && native.Changed == null && errorNative.Changed == null);
            Check("other theme overlays inactive", !comments.GetTags(partial).Any() && !errors.GetTags(partial).Any());
            Check("theme switch restores view options", view.Options.Local.Count == 0);
            view.Theme = DraculaEditorKeys.ThemeId; view.Refresh(); Check("Dracula switch reattaches", bufferFactory == 2 && viewFactory == 2);
            native.Fire(); int beforeDispose = notifications; view.Dispose(); Pump();
            Check("close cancels queued notification", notifications == beforeDispose);
            Check("close releases native subscriptions", native.Changed == null && errorNative.Changed == null && view.ClosedHandlers == null && view.Options.Changed == null);
            Check("disposed tagger returns empty", !comments.GetTags(partial).Any());
            Check("session close invalidates every classification facade", !secondComments.GetTags(partial).Any() && !reacquiredComments.GetTags(partial).Any());
            Check("session close invalidates every marker facade", !secondErrors.GetTags(partial).Any());
            Check("closed view factory null", provider.CreateTagger<IClassificationTag>(view.Value, text.Buffer) == null);
        }
        using (var view = new FakeView())
        {
            var text = new FakeText("/* */");
            var provider = new DraculaEditorProvider
            {
                BufferTags = InterfaceProxy.For<IBufferTagAggregatorFactoryService>((m, a) => { throw new ObjectDisposedException("factory"); }),
                Classifications = InterfaceProxy.For<IClassificationTypeRegistryService>((m, a) => Type((string)a[0], true))
            };
            var tags = provider.CreateTagger<IClassificationTag>(view.Value, text.Buffer);
            Check("disposed native factory fail closed", tags != null && !tags.GetTags(new NormalizedSnapshotSpanCollection(text.Span(0, 5))).Any());
            text.CSharp = false; Check("non CSharp classification excluded", provider.CreateTagger<IClassificationTag>(view.Value, text.Buffer) == null);
            provider.ViewTags = InterfaceProxy.For<IViewTagAggregatorFactoryService>((m, a) => new FakeAggregator<IErrorTag>().Value);
            Check("non CSharp native error consumer available", provider.CreateTagger<ITextMarkerTag>(view.Value, text.Buffer) != null);
        }
    }
    private static void Queue()
    {
        var pending = new List<Action>(); int notifications = 0;
        var queue = new DraculaChangeQueue(pending.Add, () => notifications++);
        System.Threading.Tasks.Parallel.For(0, 10000, i => queue.Request());
        Check("concurrent event flood one dispatch", pending.Count == 1); pending[0](); pending.Clear();
        Check("concurrent event flood one callback", notifications == 1);
        queue.Request(); Check("next snapshot can enqueue again", pending.Count == 1); queue.Dispose(); pending[0](); queue.Request();
        Check("disposed queue ignores pending and future events", notifications == 1 && pending.Count == 1);
    }
    private static void FormatDefinitions()
    {
        var marker = (MarkerFormatDefinition)Product.New("DraculaErrorFormat");
        var dictionary = marker.CreateResourceDictionary();
        Check("border only marker has no background color or brush", !marker.BackgroundColor.HasValue && marker.BackgroundBrush == null);
        var names = Product.Type("DraculaErrorFormat").GetCustomAttributes(typeof(NameAttribute), false).Cast<NameAttribute>().ToArray();
        Check("border only marker format exact key", names.Length == 1 && names[0].Name == DraculaEditorKeys.ErrorHighlight);
        Check("border only dictionary omits background", !dictionary.Contains(EditorFormatDefinition.BackgroundColorId) && !dictionary.Contains(EditorFormatDefinition.BackgroundBrushId));
        Check("border only dictionary has null fill", dictionary[MarkerFormatDefinition.FillId] == null);
        var border = dictionary[MarkerFormatDefinition.BorderId] as Pen;
        var brush = border == null ? null : border.Brush as SolidColorBrush;
        Check("border only exact pink one pixel", brush != null && brush.Color == Color.FromRgb(0xFF, 0x79, 0xC6) && border.Thickness == 1 && border.IsFrozen);
        Check("border only dictionary retains overlay order", dictionary.Contains(MarkerFormatDefinition.ZOrderId) && (int)dictionary[MarkerFormatDefinition.ZOrderId] == 5);
        var block = (ClassificationFormatDefinition)Product.New("DraculaBlockCommentFormat");
        Check("block definition exact default color", block.ForegroundColor == Color.FromRgb(0xB5, 0xBD, 0xD4));
        var orders = Product.Type("DraculaBlockCommentFormat").GetCustomAttributes(typeof(OrderAttribute), false).Cast<OrderAttribute>().ToArray();
        Check("block priority after high", orders.Any(o => o.After == Priority.High));
        Check("block priority after exact native format name", orders.Any(o => o.After == "Comment"));
    }
    private static IEnumerable<IMappingTagSpan<IClassificationTag>> LazyClassificationTags(Action beforeYield, IEnumerable<IMappingTagSpan<IClassificationTag>> tags)
    {
        foreach (var tag in tags) { beforeYield(); yield return tag; }
    }
    private static IEnumerable<IMappingTagSpan<IClassificationTag>> PartiallyFailingClassificationTags(IMappingTagSpan<IClassificationTag> tag)
    {
        yield return tag;
        throw new InvalidOperationException("native enumeration failed after first result");
    }
    private static void ViewOnlyClassifications()
    {
        using (var view = new FakeView())
        {
            var text = new FakeText("/* block */\n/** XML */\n\"/* string */\"");
            var projectionBuffer = new FakeText("/* projected */");
            var bufferSources = new List<FakeAggregator<IClassificationTag>>();
            var nativeView = new FakeAggregator<IClassificationTag>();
            nativeView.Add(text.Span(0, 11), new ClassificationTag(Type("comment", true)));
            nativeView.Add(text.Span(12, 10), new ClassificationTag(Type("xml doc comment - text", true)));
            nativeView.Add(text.Span(23, text.Text.Length - 23), new ClassificationTag(Type("string", false)));
            nativeView.Add(text.Span(0, 11), new ClassificationTag(Type("Grokeen.Dracula.BlockComment", true)));
            var partial = new NormalizedSnapshotSpanCollection(text.Span(4, 2));
            var projectedQuery = new NormalizedSnapshotSpanCollection(projectionBuffer.Span(3, 2));
            ITagger<IClassificationTag> comments = null, projected = null;
            int factories = 0, reads = 0, creationReentries = -1, sameBufferReentries = -1, otherBufferReentries = -1;
            bool throwFactory = true, creationGuard = false, iterationGuard = true;
            var provider = new DraculaEditorProvider
            {
                BufferTags = InterfaceProxy.For<IBufferTagAggregatorFactoryService>((m, a) => { var source = new FakeAggregator<IClassificationTag>(); bufferSources.Add(source); return source.Value; }),
                Classifications = InterfaceProxy.For<IClassificationTypeRegistryService>((m, a) => Type((string)a[0], true))
            };
            provider.ViewTags = InterfaceProxy.For<IViewTagAggregatorFactoryService>((m, a) =>
            {
                factories++; creationGuard = view.Session.NativeQueryActive;
                if (throwFactory) throw new InvalidOperationException("native view factory failed");
                var callbackConsumer = provider.CreateTagger<IClassificationTag>(view.Value, text.Buffer);
                creationReentries = callbackConsumer.GetTags(partial).Count();
                return nativeView.Value;
            });
            comments = provider.CreateTagger<IClassificationTag>(view.Value, text.Buffer);
            projected = provider.CreateTagger<IClassificationTag>(view.Value, projectionBuffer.Buffer);
            Check("view fallback remains lazy before first query", factories == 0);
            Check("view factory failure returns empty", !comments.GetTags(partial).Any());
            Check("view factory failure releases session query guard", !view.Session.NativeQueryActive);
            throwFactory = false;
            nativeView.GetTagsOverride = () => LazyClassificationTags(() =>
            {
                reads++; iterationGuard &= view.Session.NativeQueryActive;
                sameBufferReentries = comments.GetTags(partial).Count();
                otherBufferReentries = projected.GetTags(projectedQuery).Count();
            }, nativeView.Tags);
            var tags = comments.GetTags(partial).ToArray();
            Check("view only native block converted", tags.Length == 1 && tags[0].Tag.ClassificationType.Classification == "Grokeen.Dracula.BlockComment");
            Check("view fallback preserves full native extent from partial query", tags.Length == 1 && tags[0].Span == text.Span(0, 11));
            Check("view source creation is guarded before factory call", creationGuard && creationReentries == 0 && factories == 2);
            Check("view lazy enumeration stays inside query guard", iterationGuard && reads == nativeView.Tags.Count && !view.Session.NativeQueryActive);
            Check("view own same buffer query does not recurse", sameBufferReentries == 0 && factories == 2);
            Check("view projection other buffer query does not recurse", otherBufferReentries == 0 && factories == 2 && bufferSources.Count == 2);
            Check("view own classification is excluded without suppressing native comment", tags.Length == 1);
            Check("view XML documentation remains native", !comments.GetTags(new NormalizedSnapshotSpanCollection(text.Span(15, 3))).Any());
            Check("view string pattern remains native", !comments.GetTags(new NormalizedSnapshotSpanCollection(text.Span(26, 3))).Any());
            nativeView.Throw = true;
            Check("view enumeration failure returns empty and resets guard", !comments.GetTags(partial).Any() && !view.Session.NativeQueryActive); nativeView.Throw = false;
            Check("view query recovers after native failure", comments.GetTags(partial).Count() == 1);
            var workingRead = nativeView.GetTagsOverride;
            nativeView.GetTagsOverride = () => PartiallyFailingClassificationTags(nativeView.Tags[0]);
            Check("view partially failing lazy enumeration discards results and resets guard", !comments.GetTags(partial).Any() && !view.Session.NativeQueryActive);
            nativeView.GetTagsOverride = workingRead;
            EventHandler<SnapshotSpanEventArgs> throwingSubscriber = (sender, args) => { throw new InvalidOperationException("consumer failed"); };
            comments.TagsChanged += throwingSubscriber; bool consumerFailed = false;
            try
            {
                var inner = comments.GetType().GetField("inner", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(comments);
                Product.Call(inner, "RaiseChanged");
            }
            catch (TargetInvocationException) { consumerFailed = true; }
            finally { comments.TagsChanged -= throwingSubscriber; }
            Check("view notification exception restores publication guard", consumerFailed && !(bool)Product.Call(view.Session.Value, "get_PublishingClassificationChange"));
            int notifications = 0;
            EventHandler<SnapshotSpanEventArgs> loop = (sender, args) => { notifications++; nativeView.Fire(sender); nativeView.Fire(new object()); };
            comments.TagsChanged += loop;
            for (int i = 0; i < 10000; i++) nativeView.Fire();
            Pump(); Pump();
            Check("view 10000 native events coalesce with own cycle suppressed", notifications == 1);
            Check("view own notification completes without queued event storm", !view.Session.NativeQueryActive && !(bool)Product.Call(view.Session.Value, "get_PublishingClassificationChange"));
            comments.TagsChanged -= loop;
            var secondConsumer = provider.CreateTagger<IClassificationTag>(view.Value, text.Buffer);
            var disposable = comments as IDisposable; if (disposable != null) disposable.Dispose();
            Check("view fallback second consumer survives first consumer removal", secondConsumer.GetTags(partial).Count() == 1 && factories == 2);
            view.Dispose(); Pump();
            Check("view close releases buffer and view source subscriptions", nativeView.Changed == null && nativeView.Disposals == 1 && bufferSources.All(s => s.Changed == null && s.Disposals == 1));
            Check("view close invalidates every fallback facade", !comments.GetTags(partial).Any() && !secondConsumer.GetTags(partial).Any() && !projected.GetTags(projectedQuery).Any());
        }
        using (var view = new FakeView())
        {
            var text = new FakeText("/* block */");
            var buffer = new FakeAggregator<IClassificationTag>(); var nativeView = new FakeAggregator<IClassificationTag>();
            nativeView.Add(text.Span(0, text.Text.Length), new ClassificationTag(Type("comment", true)));
            var provider = new DraculaEditorProvider
            {
                BufferTags = InterfaceProxy.For<IBufferTagAggregatorFactoryService>((m, a) => buffer.Value),
                ViewTags = InterfaceProxy.For<IViewTagAggregatorFactoryService>((m, a) => nativeView.Value),
                Classifications = InterfaceProxy.For<IClassificationTypeRegistryService>((m, a) => Type((string)a[0], true))
            };
            var comments = provider.CreateTagger<IClassificationTag>(view.Value, text.Buffer);
            var spans = new NormalizedSnapshotSpanCollection(text.Span(2, 2));
            Check("view fallback active before theme transition", comments.GetTags(spans).Count() == 1);
            view.Theme = Guid.NewGuid(); view.Refresh();
            Check("theme transition releases both native classification sources", buffer.Disposals == 1 && nativeView.Disposals == 1 && buffer.Changed == null && nativeView.Changed == null);
            Check("other theme returns no fallback tags and no query scope", !comments.GetTags(spans).Any() && !view.Session.NativeQueryActive);
        }
    }
}
