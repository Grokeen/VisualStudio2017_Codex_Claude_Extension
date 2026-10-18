using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VS2017.AIAssistant.Views;

namespace CodexCode.Task024
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownAuditFollowupChecks
    /// desc         : R3-03 file-failure isolation and inert preview regression using owned fixtures.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : Independent checks; no product source, window or account changes.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class MarkdownAuditFollowupChecks
    {
        private static readonly List<object> checks = new List<object>();
        private static readonly UTF8Encoding utf8 = new UTF8Encoding(false, true);
        private static string work;
        private static int passed, failed, loopbackPending;
        private static readonly List<object> timings = new List<object>();

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : Preserve every assertion and elapsed time, including the first failure.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Each case has independent owned file fixtures.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Check(string name, Action action)
        {
            var clock = Stopwatch.StartNew();
            try { action(); passed++; checks.Add(new { name, passed = true }); }
            catch (Exception error) { failed++; checks.Add(new { name, passed = false, error = error.GetType().Name + ": " + error.Message }); }
            finally { timings.Add(new { name, elapsedMilliseconds = clock.Elapsed.TotalMilliseconds }); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Require
        /// desc         : Fail instead of silently weakening a file or preview expectation.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Assertions describe user-visible preservation.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Require(bool condition, string reason)
        { if (!condition) throw new InvalidOperationException(reason); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Reject
        /// desc         : Require an expected filesystem or disposed-state rejection.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Unexpected exception types remain failures.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Reject<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("Expected rejection: " + typeof(T).Name);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Folder
        /// desc         : Create unique fixture folders strictly below the fresh result directory.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Fixture bytes are preserved after verification.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Folder(string name)
        {
            string path = Path.GetFullPath(Path.Combine(work, name));
            Require(path.StartsWith(work + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Fixture path escaped output.");
            Directory.CreateDirectory(path); return path;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EditorCase
        /// desc         : Load one owned Markdown file with a dirty draft and reliable cleanup.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : File mutations never target user documents.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void EditorCase(string name, Action<MarkdownEditor, string, string> action)
        {
            string folder = Folder(name), path = Path.Combine(folder, "draft.md");
            File.WriteAllText(path, "saved original\r\n", utf8);
            using (var editor = new MarkdownEditor { MaxLength = 128 * 1024 })
            {
                Require(editor.TryLoadFile(path, () => true), "Fixture document did not load.");
                editor.Text = "unsaved draft\r\nkeep me";
                editor.Editor.Select(2, 4);
                action(editor, folder, path);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Snapshot
        /// desc         : Record the original draft, origin, list and selection before an operation.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Compare failure results with observable editor state.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class Snapshot
        {
            private readonly string text, path, origin, directory;
            private readonly string[] listed;
            private readonly bool dirty;
            private readonly int selectionStart, selectionLength;
            internal Snapshot(MarkdownEditor editor)
            {
                text = editor.Text; path = editor.CurrentPath; origin = editor.AssetBaseDirectory;
                directory = editor.DocumentDirectory; listed = editor.ListedDocuments;
                dirty = editor.IsDirty; selectionStart = editor.Editor.SelectionStart; selectionLength = editor.Editor.SelectionLength;
            }
            internal void RequireUnchanged(MarkdownEditor editor)
            {
                Require(text == editor.Text && path == editor.CurrentPath && origin == editor.AssetBaseDirectory && directory == editor.DocumentDirectory,
                    "Failure changed draft/path/image origin/document directory.");
                Require(dirty == editor.IsDirty && listed.SequenceEqual(editor.ListedDocuments), "Failure changed dirty baseline or document list.");
                Require(selectionStart == editor.Editor.SelectionStart && selectionLength == editor.Editor.SelectionLength, "Failure changed selection.");
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FileCases
        /// desc         : Verify locked operations, confirmation, caps and post-dispose isolation.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Cases intentionally differ from ordinary success-path checks.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void FileCases()
        {
            Check("locked current save preserves draft, origin, list, selection and disk", () => EditorCase("locked-save", (editor, folder, path) =>
            {
                var before = new Snapshot(editor);
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Reject<IOException>(() => editor.SaveCurrentFile());
                before.RequireUnchanged(editor);
                Require(File.ReadAllText(path, utf8) == "saved original\r\n", "Locked save changed disk bytes.");
                Require(!Directory.EnumerateFiles(folder, ".markdown-*.tmp").Any(), "Failed save leaked a temporary file.");
            }));
            Check("locked rename preserves both disk and unsaved editor state", () => EditorCase("locked-rename", (editor, folder, path) =>
            {
                var before = new Snapshot(editor);
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Reject<IOException>(() => editor.RenameCurrentDocument("renamed.md"));
                before.RequireUnchanged(editor);
                Require(File.Exists(path) && !File.Exists(Path.Combine(folder, "renamed.md")), "Locked rename mutated filenames.");
            }));
            Check("locked confirmed delete preserves unsaved state and calls confirmation once", () => EditorCase("locked-delete", (editor, folder, path) =>
            {
                int confirms = 0; var before = new Snapshot(editor);
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Reject<IOException>(() => editor.DeleteCurrentDocument(() => { confirms++; return true; }));
                before.RequireUnchanged(editor);
                Require(confirms == 1 && File.Exists(path), "Delete confirmation or lock handling changed.");
            }));
            Check("rename collision preserves existing destination and dirty selection", () => EditorCase("rename-collision", (editor, folder, path) =>
            {
                string other = Path.Combine(folder, "existing.md"); File.WriteAllText(other, "destination sentinel", utf8);
                var before = new Snapshot(editor);
                Reject<IOException>(() => editor.RenameCurrentDocument("existing.md")); before.RequireUnchanged(editor);
                Require(File.ReadAllText(other, utf8) == "destination sentinel" && File.Exists(path), "Rename overwrote destination.");
            }));
            Check("save after unicode rename targets renamed file without losing draft", () => EditorCase("rename-save", (editor, folder, path) =>
            {
                string draft = editor.Text;
                Require(editor.RenameCurrentDocument("연구"), "Rename failed.");
                string renamed = Path.Combine(folder, "연구.md");
                Require(editor.CurrentPath == renamed && editor.Text == draft && editor.IsDirty && !File.Exists(path), "Rename changed draft or baseline.");
                Require(editor.SaveCurrentFile() && File.ReadAllText(renamed, utf8) == draft && !editor.IsDirty, "Current save targeted the wrong file.");
            }));
            Check("folder browsing leaves active dirty document and asset origin intact", () => EditorCase("folder-switch", (editor, folder, path) =>
            {
                string another = Folder("browse-target"); File.WriteAllText(Path.Combine(another, "other.md"), "other", utf8);
                string draft = editor.Text, origin = editor.AssetBaseDirectory; bool dirty = editor.IsDirty;
                editor.SetDocumentDirectory(another);
                Require(editor.CurrentPath == path && editor.Text == draft && editor.IsDirty == dirty && editor.AssetBaseDirectory == origin,
                    "Browsing a folder implicitly switched or discarded the active document.");
                Require(editor.DocumentDirectory == another && editor.ListedDocuments.SequenceEqual(new[] { Path.Combine(another, "other.md") }), "Selected folder not shown.");
            }));
            Check("failed folder selection retains previous list and dirty active document", () => EditorCase("bad-folder", (editor, folder, path) =>
            {
                var before = new Snapshot(editor);
                Reject<DirectoryNotFoundException>(() => editor.SetDocumentDirectory(Path.Combine(folder, "absent")));
                before.RequireUnchanged(editor);
            }));
            Check("document list caps output and never includes nested or unrelated files", () =>
            {
                string folder = Folder("bounded-list");
                for (int i = 0; i < 270; i++) File.WriteAllText(Path.Combine(folder, i.ToString("D4") + ".md"), "x", utf8);
                Directory.CreateDirectory(Path.Combine(folder, "nested")); File.WriteAllText(Path.Combine(folder, "nested", "hidden.md"), "hidden", utf8);
                File.WriteAllText(Path.Combine(folder, "unrelated.txt"), "foreign", utf8);
                using (var editor = new MarkdownEditor())
                {
                    editor.SetDocumentDirectory(folder); string[] listed = editor.ListedDocuments;
                    Require(listed.Length == 256 && listed.All(x => Path.GetDirectoryName(x) == folder && Path.GetExtension(x) == ".md"), "Unbounded or recursive document listing.");
                    Require(listed.SequenceEqual(listed.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)), "List order is unstable.");
                }
            });
            Check("missing confirmation never deletes a dirty current document", () => EditorCase("missing-confirmation", (editor, folder, path) =>
            {
                var before = new Snapshot(editor); Require(!editor.DeleteCurrentDocument(null), "Delete accepted no confirmation.");
                before.RequireUnchanged(editor); Require(File.Exists(path), "File deleted without confirmation.");
            }));
            Check("new draft operations cannot choose or delete an implicit file", () =>
            {
                using (var editor = new MarkdownEditor())
                {
                    editor.Text = "new unsaved"; int confirms = 0;
                    Require(!editor.SaveCurrentFile() && !editor.RenameCurrentDocument("implicit.md") && !editor.DeleteCurrentDocument(() => { confirms++; return true; }), "New draft silently targeted a file.");
                    Require(confirms == 0 && editor.Text == "new unsaved" && editor.IsDirty && editor.CurrentPath == null, "New draft operation changed state.");
                }
            });
            Check("disposed document APIs reject before touching any fixture file", () => EditorCase("disposed", (editor, folder, path) =>
            {
                editor.Dispose();
                Reject<ObjectDisposedException>(() => editor.SaveCurrentFile());
                Reject<ObjectDisposedException>(() => editor.RenameCurrentDocument("after.md"));
                Reject<ObjectDisposedException>(() => editor.DeleteCurrentDocument(() => true));
                Reject<ObjectDisposedException>(() => editor.SetDocumentDirectory(folder));
                Require(File.Exists(path) && File.ReadAllText(path, utf8) == "saved original\r\n" && !File.Exists(Path.Combine(folder, "after.md")), "Disposed operation mutated disk.");
            }));
            Check("confirmed delete clears undo and preserves unrelated file bytes", () => EditorCase("delete-scope", (editor, folder, path) =>
            {
                string other = Path.Combine(folder, "keep.md"); File.WriteAllText(other, "keep sentinel", utf8);
                Require(editor.DeleteCurrentDocument(() => true), "Confirmed delete failed.");
                Require(!File.Exists(path) && File.ReadAllText(other, utf8) == "keep sentinel", "Delete affected unrelated data.");
                Require(editor.CurrentPath == null && editor.AssetBaseDirectory == null && editor.Text == "" && !editor.IsDirty && !editor.Editor.CanUndo,
                    "Deleted draft or image origin remained in undo state.");
            }));
            Check("pending delete confirmation cannot migrate to another document", () => EditorCase("delete-transition", (editor, folder, path) =>
            {
                string another = Path.Combine(folder, "another.md"); File.WriteAllText(another, "second file sentinel", utf8);
                var browser = Controls(editor).OfType<Expander>().Single(x => Convert.ToString(x.Header) == "문서 목록");
                browser.IsExpanded = true;
                var delete = Controls(editor).OfType<Button>().Single(x => Convert.ToString(x.Content) == "삭제");
                var confirm = Controls(editor).OfType<Button>().Single(x => Convert.ToString(x.Content) == "삭제 확인");
                var confirmationRow = LogicalTreeHelper.GetParent(confirm) as FrameworkElement;
                Require(confirmationRow != null, "Delete confirmation row was not found.");
                delete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(confirmationRow.Visibility == Visibility.Visible, "Explicit delete did not show its confirmation.");
                Require(editor.TryLoadFile(another, () => true) && editor.CurrentPath == another, "Second document did not load.");
                Require(confirmationRow.Visibility == Visibility.Collapsed, "A deletion confirmation remains active after opening a different document.");
                Require(File.Exists(path) && File.ReadAllText(another, utf8) == "second file sentinel", "Transition changed fixture files.");
            }));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Controls
        /// desc         : Inspect the constructed logical controls without creating a real window.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Finite traversal checks destructive confirmation lifetime.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<DependencyObject> Controls(DependencyObject root)
        {
            var pending = new Stack<DependencyObject>(); var visited = new HashSet<DependencyObject>(); pending.Push(root);
            while (pending.Count != 0)
            {
                var item = pending.Pop(); if (!visited.Add(item)) continue;
                Require(visited.Count <= 4000, "Logical control traversal exceeded its limit."); yield return item;
                foreach (var child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>()) pending.Push(child);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Nodes
        /// desc         : Traverse only native FlowDocument content, avoiding visual template creation.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Explicit recursion is limited to the product document tree.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<DependencyObject> Nodes(DependencyObject node)
        {
            yield return node;
            var document = node as FlowDocument;
            if (document != null) foreach (Block item in document.Blocks) foreach (var found in Nodes(item)) yield return found;
            var section = node as Section;
            if (section != null) foreach (Block item in section.Blocks) foreach (var found in Nodes(item)) yield return found;
            var paragraph = node as Paragraph;
            if (paragraph != null) foreach (Inline item in paragraph.Inlines) foreach (var found in Nodes(item)) yield return found;
            var span = node as Span;
            if (span != null) foreach (Inline item in span.Inlines) foreach (var found in Nodes(item)) yield return found;
            var list = node as System.Windows.Documents.List;
            if (list != null) foreach (ListItem item in list.ListItems) foreach (var found in Nodes(item)) yield return found;
            var listItem = node as ListItem;
            if (listItem != null) foreach (Block item in listItem.Blocks) foreach (var found in Nodes(item)) yield return found;
            var table = node as Table;
            if (table != null) foreach (TableRowGroup group in table.RowGroups) foreach (TableRow row in group.Rows) foreach (TableCell cell in row.Cells) foreach (Block item in cell.Blocks) foreach (var found in Nodes(item)) yield return found;
            var container = node as InlineUIContainer;
            if (container != null && container.Child != null) yield return container.Child;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Plain
        /// desc         : Read rendered text separately from the untouched Markdown dependency property.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Source and preview have different responsibilities.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Plain(MarkdownPreview preview)
        { return new TextRange(preview.TextView.Document.ContentStart, preview.TextView.Document.ContentEnd).Text; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Pump
        /// desc         : Observe queued WPF work for a bounded time without showing a window.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : The parent also enforces a 20-second process watchdog.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Pump(int milliseconds)
        {
            var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (s, e) => { timer.Stop(); frame.Continue = false; }; timer.Start();
            try { Dispatcher.PushFrame(frame); } finally { timer.Stop(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Png
        /// desc         : Generate a tiny valid local bitmap for the allowed-image control case.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : No user pictures or remote files are opened.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Png(string path)
        {
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 60, 120, 220, 255 }, 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = new FileStream(path, FileMode.CreateNew)) encoder.Save(stream);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PreviewCases
        /// desc         : Check reference isolation, static HTML and external-access restrictions.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Loopback observation is explicit and never contacts the internet.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void PreviewCases()
        {
            Check("reference definitions do not leak to the replacement document", () =>
            {
                using (var preview = new MarkdownPreview())
                {
                    preview.Markdown = "[guide]\n\n[guide]: https://example.invalid/old";
                    Require(Nodes(preview.TextView.Document).OfType<Hyperlink>().Count() == 1, "Reference control did not render.");
                    preview.Markdown = "[guide]";
                    Require(!Nodes(preview.TextView.Document).OfType<Hyperlink>().Any() && Plain(preview).Contains("[guide]"), "Old document reference leaked.");
                }
            });
            Check("retained reference click keeps its original immutable document origin", () =>
            {
                string first = Folder("origin-first"), second = Folder("origin-second");
                using (var preview = new MarkdownPreview())
                {
                    MarkdownLinkEventArgs captured = null; preview.LinkRequested += (s, e) => { captured = e; e.Handled = true; };
                    preview.SetDocument("[source][ref]\n\n[ref]: chapter.cs#L7", first);
                    var link = Nodes(preview.TextView.Document).OfType<Hyperlink>().Single();
                    preview.SetDocument("replacement", second);
                    link.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
                    Require(captured != null && captured.IsLocalFile && captured.Line == 7 && captured.DocumentBaseDirectory == first,
                        "Old click was retargeted to the new document origin.");
                }
            });
            Check("HTML hyperlink waits for explicit delegated click", () =>
            {
                using (var preview = new MarkdownPreview())
                {
                    int clicks = 0; preview.LinkRequested += (s, e) => { clicks++; e.Handled = true; };
                    preview.Markdown = "<div><a href='https://example.invalid/docs'>Documentation</a></div>";
                    Require(clicks == 0, "HTML rendering navigated automatically.");
                    Nodes(preview.TextView.Document).OfType<Hyperlink>().Single().RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
                    Require(clicks == 1, "Explicit click was not delegated exactly once.");
                }
            });
            Check("static HTML preview and current save preserve exact source bytes", () =>
            {
                string folder = Folder("html-roundtrip"), path = Path.Combine(folder, "html.md");
                string source = "<div>\r\n<p><b>문서</b> &amp; text</p>\r\n<table><tr><td>Cell</td></tr></table>\r\n</div>\r\n";
                File.WriteAllText(path, source, utf8);
                using (var editor = new MarkdownEditor { MaxLength = 128 * 1024 })
                {
                    editor.TryLoadFile(path, () => true);
                    Require(Plain(editor.Preview).Contains("Cell") && !Plain(editor.Preview).Contains("<table>"), "Static HTML control did not render.");
                    Require(editor.Text == source && editor.Preview.Markdown == source && editor.SaveCurrentFile() && File.ReadAllText(path, utf8) == source,
                        "Preview or saving rewrote user markup.");
                }
            });
            Check("HTML images allow an explicit local control and reject parent escape", () =>
            {
                string folder = Folder("html-images"), root = Path.Combine(folder, "document"); Directory.CreateDirectory(root);
                Png(Path.Combine(root, "inside.png")); Png(Path.Combine(folder, "outside.png"));
                using (var preview = new MarkdownPreview())
                {
                    preview.SetDocument("<div><img src='inside.png' alt='inside'/><img src='../outside.png' alt='outside'/></div>", root);
                    var images = Nodes(preview.TextView.Document).OfType<MarkdownAnimatedImage>().ToArray();
                    Require(images.Length == 1 && images[0].Source != null && images[0].AssetBaseDirectory == root &&
                        Path.GetFullPath(Path.Combine(images[0].AssetBaseDirectory, images[0].SourcePath)) == Path.Combine(root, "inside.png"),
                        "Image origin or parent escape policy changed.");
                }
            });
            Check("external XML entity text never exposes an owned secret file", () =>
            {
                string folder = Folder("xml-entity"), secret = Path.Combine(folder, "secret.txt"), marker = "SECRET-" + Guid.NewGuid().ToString("N");
                File.WriteAllText(secret, marker, utf8);
                using (var preview = new MarkdownPreview())
                {
                    string source = "<div>\n<!DOCTYPE div [<!ENTITY xxe SYSTEM '" + new Uri(secret).AbsoluteUri + "'>]>\n<p>&xxe;</p>\n</div>";
                    preview.SetDocument(source, folder);
                    Require(!Plain(preview).Contains(marker) && preview.Markdown == source, "External entity exposed file bytes or rewrote source.");
                }
            });
            Check("XML include elements never import a local text file", () =>
            {
                string folder = Folder("xml-include"), secret = Path.Combine(folder, "include.txt"), marker = "INCLUDE-" + Guid.NewGuid().ToString("N");
                File.WriteAllText(secret, marker, utf8);
                using (var preview = new MarkdownPreview())
                {
                    preview.SetDocument("<div xmlns:xi='http://www.w3.org/2001/XInclude'><p>Visible</p><xi:include href='" + new Uri(secret).AbsoluteUri + "' parse='text'/></div>", folder);
                    Require(Plain(preview).Contains("Visible") && !Plain(preview).Contains(marker), "XInclude imported external bytes.");
                }
            });
            Check("HTML image script iframe and CSS make zero loopback requests during observation", () =>
            {
                var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
                try
                {
                    string url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/never-fetch";
                    using (var preview = new MarkdownPreview())
                    {
                        preview.SetDocument("<div><p style='background-image:url(" + url + ")'>Visible</p><img src='" + url + "' alt='remote'/><script src='" + url + "'>ignored</script><iframe src='" + url + "'></iframe></div>", Folder("no-network"));
                        Pump(500); bool pending = listener.Pending(); if (pending) loopbackPending++;
                        Require(!pending && !Nodes(preview.TextView.Document).OfType<InlineUIContainer>().Any() && Plain(preview).Contains("Visible"), "HTML initiated an implicit network request or embedded content.");
                    }
                }
                finally { listener.Stop(); }
            });
            Check("unsafe HTML href never creates a clickable command", () =>
            {
                using (var preview = new MarkdownPreview())
                {
                    preview.Markdown = "<div><a href='javascript:alert(1)'>unsafe</a><a href='shell:AppsFolder'>shell</a><a href='data:text/html,x'>data</a></div>";
                    Require(!Nodes(preview.TextView.Document).OfType<Hyperlink>().Any() && Plain(preview).Contains("unsafe"), "Unsafe HTML href became a hyperlink.");
                }
            });
            Check("deep and oversized static HTML stays bounded without mutating source", () =>
            {
                using (var preview = new MarkdownPreview())
                {
                    string source = "<div>" + new string('x', 66000) + "</div>\n" + String.Concat(Enumerable.Repeat("<div>", 20)) + "deep" + String.Concat(Enumerable.Repeat("</div>", 20));
                    preview.Markdown = source;
                    Require(preview.Markdown == source && Nodes(preview.TextView.Document).Take(6101).Count() <= 6100, "HTML source changed or native document exceeded bounds.");
                }
            });
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Main
        /// desc         : Run isolated STA checks and identify the exact product or source-fixture assembly.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Child lifetime is bounded by the parent; no windows are shown.
        /// </summary>
        /// ---------------------------------------------------------------------
        [STAThread]
        public static int Main(string[] args)
        {
            string output = Path.GetFullPath(args[0]); bool sourceOnly = Boolean.Parse(args[1]);
            work = Path.Combine(output, "fixtures"); Directory.CreateDirectory(work);
            string assembly = typeof(MarkdownEditor).Assembly.Location, hash;
            using (var stream = File.OpenRead(assembly)) using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            try { FileCases(); PreviewCases(); }
            catch (Exception error) { failed++; checks.Add(new { name = "Harness failure", passed = false, error = error.ToString() }); }
            var result = new { Succeeded = failed == 0, Passed = passed, Failed = failed, SourceOnly = sourceOnly,
                ProductAssemblySha256 = sourceOnly ? null : hash, AssemblySha256 = hash, AssemblyPath = assembly,
                Checks = checks, Timings = timings, WindowShown = false, NativePointerUsed = false, ExternalNetworkUsed = false,
                LoopbackListenerUsed = true, LoopbackObservationMilliseconds = 500, LoopbackRequestsObserved = loopbackPending,
                UserDocumentsTouched = false, AccountUsed = false, InstallerStarted = false, CheckedAt = DateTimeOffset.Now.ToString("o") };
            string json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Serialize(result);
            File.WriteAllText(Path.Combine(output, "result.json"), json, new UTF8Encoding(true));
            Console.WriteLine(json); return failed == 0 ? 0 : 1;
        }
    }
}
