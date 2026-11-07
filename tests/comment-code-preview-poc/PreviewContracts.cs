using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using CommentCodePreview.Poc;

namespace CommentCodePreview.Poc.Tests
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PreviewContracts
    /// desc         : 실제 독립 PoC assembly와 가짜 editor만 사용해 수락·경쟁·Undo 경계를 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class PreviewContracts
    {
        private sealed class CheckRow { public string Name; public bool Passed; }
        private sealed class DeferredProvider : ICommentCodeProvider
        {
            private readonly object sync = new object();
            private readonly List<TaskCompletionSource<string>> pending = new List<TaskCompletionSource<string>>();
            internal int Calls { get { lock (sync) return pending.Count; } }
            public Task<string> GenerateAsync(CommentCodeRequest request, CancellationToken token)
            {
                lock (sync) { var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously); pending.Add(completion); return completion.Task; }
            }
            internal void Complete(int index, string text) { TaskCompletionSource<string> task; lock (sync) task = pending[index]; task.SetResult(text); }
            internal void Fail(int index) { TaskCompletionSource<string> task; lock (sync) task = pending[index]; task.SetException(new IOException("synthetic-provider-detail-must-not-be-state")); }
        }
        private static readonly List<CheckRow> checks = new List<CheckRow>();
        private static readonly List<string> groups = new List<string>();
        private static void Check(string name, bool value) { checks.Add(new CheckRow { Name = name, Passed = value }); if (!value) throw new InvalidOperationException("Synthetic contract failed."); }
        private static T Await<T>(Task<T> task) { if (!task.Wait(5000)) throw new TimeoutException("Bounded synthetic task did not finish."); return task.GetAwaiter().GetResult(); }
        private static long Ready(PreviewSession session, FakeEditorFixture editor)
        { var request = Await(session.RequestAsync(editor, "// 명시적 모의 주석", CancellationToken.None)); Check("manual request publishes a current preview", request.Outcome == RequestOutcome.Ready && session.GetPreview() != null && session.GetPreview().RequestId == request.RequestId); return request.RequestId; }

        public static int Main(string[] args)
        {
            string errorType = null; int? hresult = null; bool loadedExact = false;
            try
            {
                string loaded = typeof(PreviewSession).Assembly.Location;
                using (var stream = File.OpenRead(loaded)) using (var hash = SHA256.Create()) loadedExact = args.Length == 3 && String.Equals(Path.GetFullPath(loaded), Path.GetFullPath(args[1]), StringComparison.OrdinalIgnoreCase) && BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "") == args[2];
                Check("tests load the exact private PoC assembly copy", loadedExact);
                Happy(); StaleAndGuards(); CancelAndLate(); BoundsAndErrors(); UnicodeBoundaries(); AtomicAndUndo(); CapacityAndDispose();
            }
            catch (Exception error) { errorType = error.GetType().FullName; hresult = error.HResult; }
            var expected = new[] { "happy", "stale-guards", "cancel-late", "bounds-errors", "unicode", "atomic-undo", "capacity-dispose" };
            var result = new { Succeeded = errorType == null && loadedExact && expected.SequenceEqual(groups) && checks.All(row => row.Passed), Passed = checks.Count(row => row.Passed), Failed = checks.Count(row => !row.Passed), Groups = groups, ExpectedGroups = expected, Checks = checks, ErrorType = errorType, ErrorHResult = hresult, ActualPocAssembly = loadedExact, LoadedExactPocAssembly = loadedExact, CoreSourceCompiled = true, FakeEditorOnly = true, ActualVsSdk = false, ActualIde = false, ActualEditorDocument = false, ActualProvider = false, Network = false, Cli = false, Accounts = false, WindowShown = false, Limitation = "Pure PoC and fake atomic editor. No actual VS/IntelliSense/IME/provider/UI integration or shipped extension result." };
            File.WriteAllText(args[0], new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }.Serialize(result), new System.Text.UTF8Encoding(false));
            Console.WriteLine("Passed=" + result.Passed + " Failed=" + result.Failed + " Groups=" + groups.Count);
            return result.Succeeded ? 0 : 1;
        }

        private static void Happy()
        {
            var editor = new FakeEditorFixture("// 모의 주석\r\n"); var provider = new MockCommentCodeProvider("return 42;\r\n");
            using (var session = new PreviewSession(provider))
            {
                Check("construction does not request or edit", provider.Calls == 0 && editor.Insertions == 0 && session.GetStatus().State == PreviewState.Idle);
                var original = editor.Capture(); long id = Ready(session, editor);
                for (int index = 0; index < 10; index++) session.GetPreview();
                Check("preview and repeated reads leave entire snapshot unchanged", original.Matches(editor.Capture()) && editor.ApplyAttempts == 0 && provider.Calls == 1);
                Check("wrong preview id cannot accept current text", !session.Accept(id + 100) && editor.Insertions == 0 && session.GetStatus().CanAccept);
                Check("explicit accept inserts exactly once", session.Accept(id) && editor.Insertions == 1 && editor.Text == original.Content + "return 42;\r\n");
                Check("accept consumes preview and rejects repeat", session.GetPreview() == null && !session.Accept(id) && editor.ApplyAttempts == 1);
                Check("wrong undo id cannot undo another request", !session.Undo(id + 1) && editor.Undos == 0);
                Check("one explicit undo restores preimage and selection", session.Undo(id) && editor.Undos == 1 && editor.Text == original.Content && editor.Capture().Cursor == original.Cursor);
                Check("second undo cannot edit again", !session.Undo(id) && editor.UndoAttempts == 1);
                Check("no save or command is performed", editor.Saves == 0 && editor.Commands == 0);
            }
            var selected = new FakeEditorFixture("// selected context\r\n"); selected.Select(0, 19);
            using (var session = new PreviewSession(new MockCommentCodeProvider("code")))
            { var before = selected.Capture(); long id = Ready(session, selected); Check("selection is fixed context and accept is insertion only", session.Accept(id) && selected.Text == before.Content.Insert(before.Cursor, "code")); }
            groups.Add("happy");
        }

        private static void StaleAndGuards()
        {
            Action<FakeEditorFixture>[] changes = { e => e.MoveCursor(0), e => e.Select(0, 1), e => e.AdvanceVersion(), e => e.MutateWithoutVersion("changed"), e => { e.SwitchDocument("anonymous-document-B"); e.SwitchDocument("anonymous-document-A"); } };
            string[] names = { "cursor", "selection", "version", "hash despite same version", "tab away and back" };
            for (int index = 0; index < changes.Length; index++)
            {
                var editor = new FakeEditorFixture("original"); var provider = new DeferredProvider();
                using (var session = new PreviewSession(provider))
                { var task = session.RequestAsync(editor, "// manual", CancellationToken.None); changes[index](editor); string changed = editor.Text; provider.Complete(0, "suggested"); Check("late response rejected after " + names[index], Await(task).Outcome == RequestOutcome.Discarded && session.GetPreview() == null && editor.Text == changed); }
                editor = new FakeEditorFixture("original");
                using (var session = new PreviewSession(new MockCommentCodeProvider("suggested")))
                { long id = Ready(session, editor); changes[index](editor); string changed = editor.Text; Check("atomic accept rejects " + names[index], !session.Accept(id) && editor.Text == changed && editor.Insertions == 0); }
            }
            Action<FakeEditorFixture>[] blocks = { e => e.SetReadOnly(true), e => e.SetDiff(true), e => e.SetClosed(true), e => e.SetIme(true) };
            PreviewReason[] reasons = { PreviewReason.ReadOnly, PreviewReason.DiffView, PreviewReason.Closed, PreviewReason.ImeActive };
            for (int index = 0; index < blocks.Length; index++)
            {
                var editor = new FakeEditorFixture("original"); blocks[index](editor); var mock = new MockCommentCodeProvider("new");
                using (var session = new PreviewSession(mock)) Check("initial blocked context never requests " + reasons[index], Await(session.RequestAsync(editor, "// manual", CancellationToken.None)).Reason == reasons[index] && mock.Calls == 0);
                editor = new FakeEditorFixture("original"); var delayed = new DeferredProvider();
                using (var session = new PreviewSession(delayed)) { var task = session.RequestAsync(editor, "// manual", CancellationToken.None); blocks[index](editor); delayed.Complete(0, "new"); Check("late blocked response discarded " + reasons[index], Await(task).Reason == reasons[index] && editor.Insertions == 0); }
                editor = new FakeEditorFixture("original");
                using (var session = new PreviewSession(new MockCommentCodeProvider("new"))) { long id = Ready(session, editor); blocks[index](editor); Check("accept boundary rechecks guard " + reasons[index], !session.Accept(id) && session.GetStatus().Reason == reasons[index] && editor.Text == "original"); }
            }
            var notified = new FakeEditorFixture("original"); var one = new MockCommentCodeProvider("new");
            using (var session = new PreviewSession(one)) { Ready(session, notified); notified.MoveCursor(0); session.NotifyContextChanged(); Check("context notification discards without automatic request", session.GetPreview() == null && one.Calls == 1 && notified.Insertions == 0); }
            groups.Add("stale-guards");
        }

        private static void CancelAndLate()
        {
            var editor = new FakeEditorFixture("original"); var provider = new DeferredProvider();
            using (var session = new PreviewSession(provider))
            {
                var first = session.RequestAsync(editor, "// first", CancellationToken.None); var second = session.RequestAsync(editor, "// second", CancellationToken.None);
                provider.Complete(1, "newer"); var newest = Await(second); provider.Complete(0, "older");
                Check("superseded response cannot replace newer ready preview", Await(first).Outcome == RequestOutcome.Discarded && session.GetPreview().Text == "newer" && session.GetPreview().RequestId == newest.RequestId);
                Check("stale UI accept id cannot accept the new preview", !session.Accept(newest.RequestId - 1) && session.GetPreview().Text == "newer");
                Check("explicit cancel discards without insertion", session.Cancel() && !session.Accept(newest.RequestId) && editor.Insertions == 0);
            }
            provider = new DeferredProvider();
            using (var session = new PreviewSession(provider)) { var pending = session.RequestAsync(editor, "// manual", CancellationToken.None); session.Cancel(); provider.Complete(0, "late"); Check("provider ignoring cancellation cannot publish a late result", Await(pending).Outcome == RequestOutcome.Discarded && session.GetPreview() == null); }
            using (var cancellation = new CancellationTokenSource()) using (var session = new PreviewSession(new MockCommentCodeProvider("new")))
            { var id = Await(session.RequestAsync(editor, "// manual", cancellation.Token)).RequestId; cancellation.Cancel(); Check("external cancellation after ready blocks accept", !session.Accept(id) && session.GetStatus().Reason == PreviewReason.Cancelled && editor.Insertions == 0); }
            using (var cancellation = new CancellationTokenSource()) using (var session = new PreviewSession(new MockCommentCodeProvider("new")))
            { long id = ReadyWithToken(session, editor, cancellation.Token); editor.BeforeAtomicApply = e => cancellation.Cancel(); Check("atomic editor observes cancellation racing after model check", !session.Accept(id) && editor.Insertions == 0 && session.GetStatus().Reason == PreviewReason.Cancelled); }
            var never = new MockCommentCodeProvider("new");
            using (var cancellation = new CancellationTokenSource()) using (var session = new PreviewSession(never))
            { cancellation.Cancel(); Check("already cancelled request never reaches provider", Await(session.RequestAsync(editor, "// manual", cancellation.Token)).Reason == PreviewReason.Cancelled && never.Calls == 0); }
            groups.Add("cancel-late");
        }
        private static long ReadyWithToken(PreviewSession session, FakeEditorFixture editor, CancellationToken token) { var result = Await(session.RequestAsync(editor, "// manual", token)); Check("token-bound manual request is ready", result.Outcome == RequestOutcome.Ready); return result.RequestId; }

        private static void BoundsAndErrors()
        {
            var editor = new FakeEditorFixture("original"); var bad = new DeferredProvider();
            using (var session = new PreviewSession(bad)) { var task = session.RequestAsync(editor, "// manual", CancellationToken.None); bad.Fail(0); Check("provider failure is typed and cannot mutate editor", Await(task).Outcome == RequestOutcome.Failed && session.GetStatus().Reason == PreviewReason.ProviderError && session.GetPreview() == null && editor.Insertions == 0); }
            string[] invalid = { new string('x', PreviewLimits.SuggestionCharacters + 1), new string('\uD55C', 6000), "\uD800", "bad\rtext", " ", null, "bad\0text" };
            foreach (string text in invalid) using (var session = new PreviewSession(new MockCommentCodeProvider(text))) { var result = Await(session.RequestAsync(editor, "// manual", CancellationToken.None)); Check("invalid or oversized provider response never becomes preview", result.Outcome == RequestOutcome.Discarded && session.GetPreview() == null && editor.Insertions == 0); }
            var mock = new MockCommentCodeProvider("new");
            using (var session = new PreviewSession(mock)) { Check("oversized input context rejected before provider", Await(session.RequestAsync(new FakeEditorFixture(new string('a', PreviewLimits.ContextCharacters + 1)), "// manual", CancellationToken.None)).Reason == PreviewReason.Oversize && mock.Calls == 0); Check("oversized instruction rejected before provider", Await(session.RequestAsync(editor, new string('a', PreviewLimits.InstructionCharacters + 1), CancellationToken.None)).Reason == PreviewReason.Oversize && mock.Calls == 0); }
            using (var session = new PreviewSession(mock)) { Check("UTF8 context byte limit is independent of UTF16 count", Await(session.RequestAsync(new FakeEditorFixture(new string('\uD55C', 50000)), "// manual", CancellationToken.None)).Reason == PreviewReason.Oversize && mock.Calls == 0); Check("empty instruction never reaches provider", Await(session.RequestAsync(editor, " ", CancellationToken.None)).Reason == PreviewReason.Empty && mock.Calls == 0); }
            Check("unknown editor rejection reasons fail closed to typed fallback", AtomicApplyResult.Rejected((PreviewReason)999).Reason == PreviewReason.ContextChanged && AtomicUndoResult.Rejected((PreviewReason)999).Reason == PreviewReason.UndoConflict);
            using (var session = new PreviewSession(new MockCommentCodeProvider("SaveAll();\r\nExecuteCommand();\r\n"))) { long id = Ready(session, editor); Check("candidate code remains inert text", session.Accept(id) && editor.Saves == 0 && editor.Commands == 0); }
            Check("editor contract exposes no save command or automatic transmission API", typeof(IAtomicPreviewEditor).GetMethods().Select(method => method.Name).OrderBy(name => name).SequenceEqual(new[] { "Capture", "CompareAndApply", "CompareAndUndo" }));
            var nearLimit = new FakeEditorFixture(new string('a', PreviewLimits.ContextCharacters));
            using (var session = new PreviewSession(new MockCommentCodeProvider("x"))) { long id = Ready(session, nearLimit); Check("oversized postimage is rejected before fake editor mutation", !session.Accept(id) && nearLimit.Text.Length == PreviewLimits.ContextCharacters && nearLimit.Insertions == 0); }
            groups.Add("bounds-errors");
        }

        private static void UnicodeBoundaries()
        {
            string[] contents = { "a\r\nb", "\uD83D\uDE00x" }; int[] invalidPositions = { 2, 1 };
            for (int index = 0; index < contents.Length; index++)
            {
                var editor = new FakeEditorFixture(contents[index]); editor.MoveCursor(invalidPositions[index]); var mock = new MockCommentCodeProvider("new");
                using (var session = new PreviewSession(mock)) Check("cursor cannot split CRLF or surrogate pair", Await(session.RequestAsync(editor, "// manual", CancellationToken.None)).Reason == PreviewReason.InvalidContext && mock.Calls == 0);
                editor = new FakeEditorFixture(contents[index]); editor.Select(0, invalidPositions[index]);
                using (var session = new PreviewSession(mock)) Check("selection endpoint cannot split CRLF or surrogate pair", Await(session.RequestAsync(editor, "// manual", CancellationToken.None)).Reason == PreviewReason.InvalidContext && mock.Calls == 0);
            }
            string original = "// 한글 \uD83D\uDE00\r\n"; string code = "한글 e\u0301 \uD83D\uDC69\u200D\uD83D\uDCBB\r\nreturn 1;\r\n"; var unicode = new FakeEditorFixture(original);
            using (var session = new PreviewSession(new MockCommentCodeProvider(code))) { long id = Ready(session, unicode); Check("Unicode and CRLF insertion preserves exact text", session.Accept(id) && unicode.Text == original + code); Check("one Undo restores exact Unicode and CRLF preimage", session.Undo(id) && unicode.Text == original); }
            groups.Add("unicode");
        }

        private static void AtomicAndUndo()
        {
            var editor = new FakeEditorFixture("original");
            using (var session = new PreviewSession(new MockCommentCodeProvider("insert"))) { long id = Ready(session, editor); editor.BeforeAtomicApply = e => e.ReplaceExternal("external-before-compare"); Check("race inside atomic boundary cannot overwrite external edit", !session.Accept(id) && editor.Text == "external-before-compare" && editor.Insertions == 0); }
            editor = new FakeEditorFixture("original");
            using (var session = new PreviewSession(new MockCommentCodeProvider("insert")))
            { long id = Ready(session, editor); bool[] accepted = new bool[2]; using (var start = new ManualResetEventSlim(false)) { var a = Task.Run(() => { start.Wait(); accepted[0] = session.Accept(id); }); var b = Task.Run(() => { start.Wait(); accepted[1] = session.Accept(id); }); start.Set(); Check("concurrent accept tasks finish within bound", Task.WaitAll(new[] { a, b }, 3000)); } Check("concurrent accepts linearize to one insertion", accepted.Count(value => value) == 1 && editor.Insertions == 1); editor.ReplaceExternal(editor.Text + "external-after-accept"); string external = editor.Text; Check("Undo refuses external edits instead of restoring whole old buffer", !session.Undo(id) && editor.Text == external && editor.Undos == 0); }
            editor = new FakeEditorFixture("original");
            using (var session = new PreviewSession(new MockCommentCodeProvider("insert")))
            { long id = Ready(session, editor); using (var inside = new ManualResetEventSlim(false)) using (var release = new ManualResetEventSlim(false)) { editor.BeforeAtomicApply = e => { inside.Set(); if (!release.Wait(3000)) throw new TimeoutException(); }; var accept = Task.Run(() => session.Accept(id)); Check("accept entered the fake atomic lock", inside.Wait(3000)); var external = Task.Run(() => editor.ReplaceExternal("external-winning-after-commit")); release.Set(); Check("real threaded apply and external edit finish", Task.WaitAll(new Task[] { accept, external }, 3000)); Check("external edit after commit is preserved by Undo guard", accept.Result && !session.Undo(id) && editor.Text == "external-winning-after-commit"); } }
            editor = new FakeEditorFixture("original");
            using (var session = new PreviewSession(new MockCommentCodeProvider("insert")))
            { long id = Ready(session, editor); session.Accept(id); editor.SetReadOnly(true); string before = editor.Text; Check("Undo also rejects readonly transition without erasing accepted code", !session.Undo(id) && session.GetStatus().Reason == PreviewReason.ReadOnly && editor.Text == before); }
            editor = new FakeEditorFixture("original");
            using (var session = new PreviewSession(new MockCommentCodeProvider("insert")))
            { long id = Ready(session, editor); session.Accept(id); editor.ReplaceExternal("external"); session.NotifyContextChanged(); Check("explicit external notification clears accepted Undo rights", !session.Undo(id) && !session.GetStatus().CanUndo && editor.Text == "external"); }
            groups.Add("atomic-undo");
        }

        private static void CapacityAndDispose()
        {
            var editor = new FakeEditorFixture("original"); var provider = new DeferredProvider();
            using (var session = new PreviewSession(provider))
            { var tasks = new List<Task<PreviewRequestResult>>(); for (int index = 0; index < PreviewLimits.OutstandingRequests; index++) tasks.Add(session.RequestAsync(editor, "// manual", CancellationToken.None)); Check("noncooperative outstanding providers are bounded", Await(session.RequestAsync(editor, "// over bound", CancellationToken.None)).Reason == PreviewReason.Busy && provider.Calls == PreviewLimits.OutstandingRequests); for (int index = 0; index < tasks.Count; index++) provider.Complete(index, "late"); foreach (var task in tasks) Check("capacity-superseded result stays discarded", Await(task).Outcome == RequestOutcome.Discarded); Check("capacity clears without editing after late providers complete", session.GetPreview() == null && editor.Insertions == 0); var recovered = session.RequestAsync(editor, "// recovered slot", CancellationToken.None); provider.Complete(PreviewLimits.OutstandingRequests, "recovered"); Check("completed provider slots permit a new manual request", Await(recovered).Outcome == RequestOutcome.Ready && provider.Calls == PreviewLimits.OutstandingRequests + 1); }
            provider = new DeferredProvider(); var disposed = new PreviewSession(provider); var pending = disposed.RequestAsync(editor, "// manual", CancellationToken.None); disposed.Dispose(); provider.Complete(0, "late"); Await(pending);
            Check("disposed session cannot resurrect from late provider", disposed.GetStatus().State == PreviewState.Disposed && disposed.GetPreview() == null && !disposed.Accept(1));
            Check("disposed session rejects new manual request", Await(disposed.RequestAsync(editor, "// manual", CancellationToken.None)).Reason == PreviewReason.Disposed && provider.Calls == 1);
            using (var session = new PreviewSession(new MockCommentCodeProvider("new"))) { long id = Ready(session, editor); editor.BeforeAtomicApply = e => session.Dispose(); session.Accept(id); Check("reentrant dispose cannot resurrect an accepted session", session.GetStatus().State == PreviewState.Disposed && !session.Undo(id)); }
            groups.Add("capacity-dispose");
        }
    }
}
