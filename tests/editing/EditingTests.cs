using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Views;
using VS2017.AIAssistant.Chat;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : EditingTests
/// desc         : 실제 빌드 DLL의 편집 정책·CLI 프로세스·WPF 버튼 흐름을 독립 fixture로 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 승인·충돌·복구와 실제 파이프·취소 회귀 테스트 추가.
/// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·첨부·영속 설정·계정 회귀 추가.
/// </summary>
/// ---------------------------------------------------------------------
internal static class EditingTests
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 3000000 };
    private static string root;
    private static string fixture;
    private static int passed, failed;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 테스트 또는 무과금 가짜 CLI 모드로 실행하며 실패 시 비정상 종료한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 테스트 프로세스 및 fixture 진입점 구현.
    /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
    /// update date  : 2026-10-18 CodexOrig : 실제 첫 실행·중앙 팝업·저장/취소 UX 회귀 연결.
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);
        if (args.Length > 0 && (args[0] == "login" || args[0] == "auth")) { Console.Write("fixture account details should not appear in UI"); return 0; }
        if (args.Length > 0 && args[0] == "--fixture-child")
        {
            File.WriteAllText(args[1], Process.GetCurrentProcess().Id.ToString());
            Thread.Sleep(60000);
            return 0;
        }
        if (args.Length > 0 && (args[0] == "exec" || args[0] == "--print")) return RunFixture(args);
        if (args.Length != 2) return 2;
        root = args[0]; fixture = args[1];
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        TestParser();
        TestEditing();
        TestProcesses();
        TestUi();
        TestChat();
        DialogChecks.Run(Path.Combine(root, "ux"), (name, condition) => Check(name, () => Assert(condition)));
        Console.WriteLine("Editing tests: " + passed + "/" + (passed + failed) + " PASS");
        return failed == 0 ? 0 : 1;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TestParser
    /// desc         : JSON 형식·정확한 원본 일치·개행·겹침·크기 제한을 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 잘못된 AI 응답을 소스에 적용하지 않는 정책 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void TestParser()
    {
        Check("unique replacement preserves surrounding source", () => {
            var source = Snapshot("// keep\nreturn 1;\n// keep tail");
            var plan = EditProposal.Parse(source, Plan("return 1;", "return 2;"));
            Assert(plan.ProposedText == "// keep\nreturn 2;\n// keep tail" && plan.ChangePreview.Contains("원본 줄 2"));
            Assert(source.Content.Contains("return 1;"));
        });
        Check("multiple replacements use original offsets", () => {
            var source = Snapshot("alpha beta gamma");
            var json = Json.Serialize(new { summary = "two", edits = new[] { new { old_text = "alpha", new_text = "ALPHA LONG" }, new { old_text = "gamma", new_text = "G" } } });
            Assert(EditProposal.Parse(source, json).ProposedText == "ALPHA LONG beta G");
        });
        Check("CRLF preserved for AI LF fragments", () => Assert(EditProposal.Parse(Snapshot("one\r\ntwo\r\n"), Plan("one\ntwo", "one\nthree")).ProposedText == "one\r\nthree\r\n"));
        Check("Unicode quotes and backslashes preserved", () => {
            string oldText = "var s = \"한글 \\\\ original\";";
            string newText = "var s = \"수정 \\\\ result\";";
            Assert(EditProposal.Parse(Snapshot(oldText), Plan(oldText, newText)).ProposedText == newText);
        });
        Check("empty edits return unchanged document", () => Assert(!EditProposal.Parse(Snapshot("keep"), "{\"summary\":\"ok\",\"edits\":[]}").HasChanges));
        Check("missing anchor rejected", () => Expect<InvalidOperationException>(() => EditProposal.Parse(Snapshot("abc"), Plan("missing", "new"))));
        Check("ambiguous anchor rejected", () => Expect<InvalidOperationException>(() => EditProposal.Parse(Snapshot("x x"), Plan("x", "y"))));
        Check("empty anchor rejected", () => Expect<InvalidOperationException>(() => EditProposal.Parse(Snapshot("abc"), Plan("", "new"))));
        Check("overlapping changes rejected", () => {
            string json = Json.Serialize(new { summary = "overlap", edits = new[] { new { old_text = "abc", new_text = "x" }, new { old_text = "bcde", new_text = "y" } } });
            Expect<InvalidOperationException>(() => EditProposal.Parse(Snapshot("abcdef"), json));
        });
        foreach (string bad in new[] { "not json", "null", "[]", "{}", "{\"summary\":null,\"edits\":[]}", "{\"summary\":\"x\",\"edits\":null}", "{\"summary\":\"x\",\"edits\":[{\"old_text\":\"abc\",\"new_text\":null}]}", "{\"summary\":\"x\",\"edits\":[],\"file_path\":\"outside.cs\"}" })
            Check("invalid schema rejected " + bad, () => Expect<InvalidOperationException>(() => EditProposal.Parse(Snapshot("abc"), bad)));
        Check("oversized source rejected", () => Expect<InvalidOperationException>(() => EditProposal.Parse(Snapshot(new string('x', 200001)), Plan("x", "y"))));
        Check("oversized response rejected", () => Expect<InvalidOperationException>(() => EditProposal.Parse(Snapshot("x"), new string('x', 2000001))));
        Check("too many edits rejected", () => {
            string json = Json.Serialize(new { summary = "many", edits = Enumerable.Range(0, 65).Select(x => new { old_text = "x", new_text = "y" }).ToArray() });
            Expect<InvalidOperationException>(() => EditProposal.Parse(Snapshot("x"), json));
        });
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TestEditing
    /// desc         : 승인 전 쓰기 차단, 적용·별도 저장·복구 및 충돌 상황을 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 임시 파일과 메모리 버퍼를 구분하는 테스트 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void TestEditing()
    {
        Check("unapproved apply makes no editor or disk write", () => {
            var doc = NewDocument(); var session = new SourceEditSession(); var plan = Proposal(doc);
            Expect<InvalidOperationException>(() => session.Apply(doc, plan, false));
            Assert(doc.ReplaceCount == 0 && doc.SaveCount == 0 && doc.Buffer == doc.DiskText);
        });
        Check("approved apply edits buffer, explicit save persists", () => {
            var doc = NewDocument(); string original = doc.Buffer; var session = new SourceEditSession();
            session.Apply(doc, Proposal(doc), true);
            Assert(doc.Buffer.Contains("return 2;") && doc.DiskText == original && doc.SaveCount == 0);
            session.Save(); Assert(doc.DiskText == doc.Buffer && doc.SaveCount == 1 && !session.CanSave);
            session.Restore(true); Assert(doc.Buffer == original && doc.DiskText.Contains("return 2;") && !session.CanRestore);
        });
        Check("unsaved user edits included and restored", () => {
            var doc = NewDocument(); doc.Buffer += "\n// user unsaved"; string before = doc.Buffer; string disk = doc.DiskText;
            var session = new SourceEditSession(); session.Apply(doc, Proposal(doc), true); session.Restore(true);
            Assert(doc.Buffer == before && doc.DiskText == disk && doc.SaveCount == 0);
        });
        Check("user edit after preview blocks apply", () => {
            var doc = NewDocument(); var plan = Proposal(doc); doc.Buffer += "// new edit";
            Expect<InvalidOperationException>(() => new SourceEditSession().Apply(doc, plan, true)); Assert(doc.ReplaceCount == 0);
        });
        Check("external disk edit blocks apply", () => {
            var doc = NewDocument(); var plan = Proposal(doc); File.AppendAllText(doc.Path, "// external");
            Expect<InvalidOperationException>(() => new SourceEditSession().Apply(doc, plan, true)); Assert(doc.ReplaceCount == 0);
        });
        Check("different target file blocked", () => {
            var first = NewDocument(); var other = NewDocument();
            Expect<InvalidOperationException>(() => new SourceEditSession().Apply(other, Proposal(first), true)); Assert(other.ReplaceCount == 0);
        });
        Check("read-only target blocked", () => {
            var doc = NewDocument(); var plan = Proposal(doc); doc.ReadOnly = true;
            Expect<InvalidOperationException>(() => new SourceEditSession().Apply(doc, plan, true)); Assert(doc.ReplaceCount == 0);
        });
        Check("reapplying stale proposal blocked", () => {
            var doc = NewDocument(); var plan = Proposal(doc); var session = new SourceEditSession(); session.Apply(doc, plan, true);
            Expect<InvalidOperationException>(() => session.Apply(doc, plan, true)); Assert(doc.ReplaceCount == 1);
        });
        Check("later user edit blocks save and restore", () => {
            var doc = NewDocument(); var session = new SourceEditSession(); session.Apply(doc, Proposal(doc), true); doc.Buffer += "// user";
            Expect<InvalidOperationException>(() => session.Save()); Expect<InvalidOperationException>(() => session.Restore(true));
            Assert(doc.SaveCount == 0 && doc.Buffer.EndsWith("// user"));
        });
        Check("later external disk edit blocks save and restore", () => {
            var doc = NewDocument(); var session = new SourceEditSession(); session.Apply(doc, Proposal(doc), true); File.AppendAllText(doc.Path, "// external");
            Expect<InvalidOperationException>(() => session.Save()); Expect<InvalidOperationException>(() => session.Restore(true));
            Assert(doc.SaveCount == 0 && doc.DiskText.EndsWith("// external"));
        });
        Check("editor apply failure leaves no successful session", () => {
            var doc = NewDocument(); doc.FailReplace = true; var session = new SourceEditSession();
            Expect<InvalidOperationException>(() => session.Apply(doc, Proposal(doc), true)); Assert(!session.CanRestore && doc.Buffer == doc.DiskText);
        });
        Check("save failure remains recoverable", () => {
            var doc = NewDocument(); var session = new SourceEditSession(); session.Apply(doc, Proposal(doc), true); doc.FailSave = true;
            Expect<InvalidOperationException>(() => session.Save()); Assert(session.CanRestore && session.CanSave); session.Restore(true);
            Assert(doc.Buffer == doc.DiskText);
        });
        Check("unapproved restore blocked", () => {
            var doc = NewDocument(); var session = new SourceEditSession(); session.Apply(doc, Proposal(doc), true);
            Expect<InvalidOperationException>(() => session.Restore(false)); Assert(doc.Buffer.Contains("return 2;"));
        });
        Check("save and restore without applied edit rejected", () => {
            var session = new SourceEditSession(); Expect<InvalidOperationException>(() => session.Save()); Expect<InvalidOperationException>(() => session.Restore(true));
        });
        Check("no-change proposal cannot write", () => {
            var doc = NewDocument(); var plan = EditProposal.Parse(doc.Capture(), "{\"summary\":\"none\",\"edits\":[]}");
            Expect<InvalidOperationException>(() => new SourceEditSession().Apply(doc, plan, true)); Assert(doc.ReplaceCount == 0);
        });
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TestProcesses
    /// desc         : 실제 exe·stdin/stdout/stderr를 사용해 CLI 인수·응답·취소·타임아웃을 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 AI 호출 없이 결정적인 프로세스 통합 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void TestProcesses()
    {
        foreach (string provider in new[] { "Codex", "Claude" })
            Check(provider + " native CLI protocol and Unicode stdin", () => {
                var doc = NewDocument(); string before = doc.DiskText;
                var result = new CliEditProvider(TimeSpan.FromSeconds(10)).GenerateAsync(provider, fixture, doc.Capture(), "fixture:ok 한글", CancellationToken.None).GetAwaiter().GetResult();
                Assert(result.ProposedText.Contains("return 2;") && doc.Buffer == before && doc.DiskText == before);
            });
        Check("stderr drained concurrently", () => {
            var result = RunCli("fixture:stderr"); Assert(result.HasChanges);
        });
        Check("nonzero CLI exit rejected", () => Expect<InvalidOperationException>(() => RunCli("fixture:error")));
        Check("malformed CLI output rejected", () => Expect<InvalidOperationException>(() => RunCli("fixture:bad-json")));
        Check("Claude error envelope rejected", () => Expect<InvalidOperationException>(() => RunCli("fixture:claude-error", "Claude")));
        Check("Claude missing structured output rejected", () => Expect<InvalidOperationException>(() => RunCli("fixture:missing", "Claude")));
        Check("excessive output terminates CLI", () => Expect<InvalidOperationException>(() => RunCli("fixture:huge")));
        Check("missing executable rejected", () => Expect<InvalidOperationException>(() => new CliEditProvider().GenerateAsync("Codex", System.IO.Path.Combine(root, "missing.exe"), NewDocument().Capture(), "x", CancellationToken.None)));
        Check("cmd wrapper rejected without shell execution", () => Expect<InvalidOperationException>(() => new CliEditProvider().GenerateAsync("Codex", "codex.cmd", NewDocument().Capture(), "x", CancellationToken.None)));
        Check("read-only source request blocked", () => {
            var doc = NewDocument(); doc.ReadOnly = true;
            Expect<InvalidOperationException>(() => new CliEditProvider().GenerateAsync("Codex", fixture, doc.Capture(), "x", CancellationToken.None));
        });
        Check("empty user request blocked", () => Expect<ArgumentException>(() => new CliEditProvider().GenerateAsync("Codex", fixture, NewDocument().Capture(), " ", CancellationToken.None)));
        Check("unsupported provider blocked", () => Expect<ArgumentException>(() => new CliEditProvider().GenerateAsync("Other", fixture, NewDocument().Capture(), "x", CancellationToken.None)));
        Check("pre-cancelled request does not run", () => {
            using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); Expect<OperationCanceledException>(() => new CliEditProvider().GenerateAsync("Codex", fixture, NewDocument().Capture(), "fixture:ok", cancel.Token).GetAwaiter().GetResult()); }
        });
        Check("cancellation stops CLI child process tree", () => {
            string childLog = System.IO.Path.Combine(root, "child-" + Guid.NewGuid().ToString("N") + ".txt");
            using (var cancel = new CancellationTokenSource())
            {
                Task<EditProposal> task = new CliEditProvider(TimeSpan.FromSeconds(15)).GenerateAsync("Codex", fixture, NewDocument().Capture(), "fixture:child " + childLog, cancel.Token);
                WaitFor(() => File.Exists(childLog), 10000);
                int childPid = int.Parse(File.ReadAllText(childLog)); cancel.Cancel();
                Expect<OperationCanceledException>(() => task.GetAwaiter().GetResult());
                WaitFor(() => !ProcessExists(childPid), 3000);
            }
        });
        Check("timeout stops hung CLI", () => Expect<TimeoutException>(() => new CliEditProvider(TimeSpan.FromMilliseconds(500)).GenerateAsync("Codex", fixture, NewDocument().Capture(), "fixture:hang", CancellationToken.None).GetAwaiter().GetResult()));
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TestUi
    /// desc         : 빌드된 WPF의 실제 버튼 이벤트로 요청부터 승인·저장·복구까지 실행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : UI 승인 체크 및 Provider 전환 시 이전 승인 해제 검증.
    /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void TestUi()
    {
        Check("WPF request review approve apply save restore workflow", () => {
            var doc = NewDocument(); string original = doc.Buffer;
            using (var control = new AssistantControl())
            {
                control.Initialize(new TestDocuments(doc), new CliEditProvider(TimeSpan.FromSeconds(10)));
                ((TextBox)control.FindName("ExecutablePath")).Text = fixture;
                ((TextBox)control.FindName("Instruction")).Text = "fixture:ok 한글";
                var generate = (Button)control.FindName("GenerateButton"); var apply = (Button)control.FindName("ApplyButton");
                var approval = (CheckBox)control.FindName("ReviewApproval");
                Click(generate); Assert(!generate.IsEnabled); PumpUntil(() => generate.IsEnabled, 15000);
                Assert(doc.ReplaceCount == 0 && doc.DiskText == original && !apply.IsEnabled && approval.IsEnabled);
                Assert(((TextBox)control.FindName("DiffPreview")).Text.Contains("return 2;"));
                RenderReview(control);
                approval.IsChecked = true; Assert(apply.IsEnabled); Click(apply);
                Assert(doc.Buffer.Contains("return 2;") && doc.DiskText == original && !apply.IsEnabled);
                Click((Button)control.FindName("SaveButton")); Assert(doc.DiskText == doc.Buffer);
                Click((Button)control.FindName("RestoreButton")); Assert(doc.Buffer == original && doc.DiskText.Contains("return 2;"));
            }
        });
        Check("WPF rejects conflict and resets approval on provider switch", () => {
            var doc = NewDocument();
            using (var control = new AssistantControl())
            {
                control.Initialize(new TestDocuments(doc), new CliEditProvider(TimeSpan.FromSeconds(10)));
                ((TextBox)control.FindName("ExecutablePath")).Text = fixture; ((TextBox)control.FindName("Instruction")).Text = "fixture:ok";
                var generate = (Button)control.FindName("GenerateButton"); Click(generate); PumpUntil(() => generate.IsEnabled, 15000);
                ((CheckBox)control.FindName("ReviewApproval")).IsChecked = true;
                doc.Buffer += "// later edit"; Click((Button)control.FindName("ApplyButton")); Assert(doc.ReplaceCount == 0 && doc.Buffer.EndsWith("// later edit"));
                ((TabControl)control.FindName("ProviderChoice")).SelectedIndex = 1;
                Assert(((CheckBox)control.FindName("ReviewApproval")).IsChecked == false && !((Button)control.FindName("ApplyButton")).IsEnabled);
            }
        });
        Check("WPF applies to captured file after active file switches", () => {
            var first = NewDocument(); var other = NewDocument(); var documents = new TestDocuments(first);
            using (var control = new AssistantControl())
            {
                control.Initialize(documents, new CliEditProvider(TimeSpan.FromSeconds(10)));
                ((TextBox)control.FindName("ExecutablePath")).Text = fixture; ((TextBox)control.FindName("Instruction")).Text = "fixture:ok";
                var generate = (Button)control.FindName("GenerateButton"); Click(generate); PumpUntil(() => generate.IsEnabled, 15000);
                documents.Current = other;
                var apply = (Button)control.FindName("ApplyButton");
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(first.ReplaceCount == 0 && other.ReplaceCount == 0);
                ((CheckBox)control.FindName("ReviewApproval")).IsChecked = true; Click(apply);
                Assert(first.Buffer.Contains("return 2;") && first.ReplaceCount == 1 && other.ReplaceCount == 0);
            }
        });
        Check("WPF stop cancels request and allows a fresh request", () => {
            var doc = NewDocument();
            using (var control = new AssistantControl())
            {
                control.Initialize(new TestDocuments(doc), new CliEditProvider(TimeSpan.FromSeconds(10)));
                ((TextBox)control.FindName("ExecutablePath")).Text = fixture; ((TextBox)control.FindName("Instruction")).Text = "fixture:hang";
                var generate = (Button)control.FindName("GenerateButton"); Click(generate);
                Click((Button)control.FindName("StopButton")); PumpUntil(() => generate.IsEnabled, 15000);
                Assert(doc.ReplaceCount == 0 && !((Button)control.FindName("ApplyButton")).IsEnabled);
                ((TextBox)control.FindName("Instruction")).Text = "fixture:ok"; Click(generate); PumpUntil(() => generate.IsEnabled, 15000);
                Assert(((CheckBox)control.FindName("ReviewApproval")).IsEnabled && doc.ReplaceCount == 0);
            }
        });
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TestChat
    /// desc         : 첨부 스냅샷·멀티턴 컨텍스트·설정 보존·실제 WPF 탭/붙여넣기를 검사한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 새 Role 요구의 데이터 및 화면 회귀 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void TestChat()
    {
        string textPath = Path.Combine(root, "참고 문서.txt");
        File.WriteAllText(textPath, "첨부 원문", new UTF8Encoding(true));
        var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[] {0,0,255,255, 0,0,255,255, 0,0,255,255, 0,0,255,255}, 8);
        byte[] image;
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = new MemoryStream()) { encoder.Save(stream); image = stream.ToArray(); }
        string imagePath = Path.Combine(root, "참고 이미지.png"); File.WriteAllBytes(imagePath, image);
        Check("attachment snapshots preserve text after original file changes", () => {
            var attachment = ChatAttachment.FromFile(textPath);
            File.WriteAllText(textPath, "변경됨", Encoding.UTF8);
            Assert(attachment.Text == "첨부 원문" && attachment.Name == "참고 문서.txt" && !attachment.IsImage);
            File.WriteAllText(textPath, "첨부 원문", Encoding.UTF8);
        });
        Check("PNG attachment has frozen thumbnail and original payload", () => {
            var attachment = ChatAttachment.FromFile(imagePath);
            Assert(attachment.IsImage && attachment.MediaType == "image/png" && attachment.Thumbnail.IsFrozen && Convert.FromBase64String(attachment.ImageBase64).SequenceEqual(image));
        });
        Check("binary and invalid image attachments rejected", () => {
            string path = Path.Combine(root, "binary.bin"); File.WriteAllBytes(path, new byte[] {0,1,2,3});
            Expect<InvalidOperationException>(() => ChatAttachment.FromFile(path));
            Expect<InvalidOperationException>(() => ChatAttachment.FromImage(new byte[100], "fake.png"));
        });
        Check("oversize text and attachment count rejected", () => {
            string path = Path.Combine(root, "large.txt"); File.WriteAllBytes(path, new byte[102401]);
            Expect<InvalidOperationException>(() => ChatAttachment.FromFile(path));
            Expect<ArgumentException>(() => new ChatRequestContext(attachments: Enumerable.Repeat(ChatAttachment.FromFile(textPath), 5).ToArray()));
        });
        Check("context copies recent messages and limits rules/history", () => {
            var messages = Enumerable.Range(0, 20).Select(i => new ChatMessage { Role = "user", Text = "message" + i }).ToArray();
            var context = new ChatRequestContext("rules", "model", messages);
            messages[19].Text = "changed";
            Assert(context.History.Length == 10 && context.History.Last().Text == "message19");
            Expect<ArgumentException>(() => new ChatRequestContext(new string('x', 8001)));
        });
        Check("chat stack bounds old messages without sharing providers", () => {
            var first = new ChatSession(); var second = new ChatSession();
            for (int i = 0; i < 120; i++) first.Add("user", i.ToString());
            Assert(first.Messages.Count == 100 && first.Messages.First().Text == "20" && second.Messages.Count == 0);
        });
        Check("settings persist and atomically replace Unicode preferences", () => {
            var store = new AssistantSettingsStore(Path.Combine(root, "설정", "preferences.json"));
            var settings = new AssistantSettings { CodexPath = fixture, ClaudePath = "한글 경로.exe", CodexModel = "model-a", ClaudeModel = "model-b", Instructions = "기존 코드 유지", FontSize = 18 };
            store.Save(settings); Assert(store.Load().Instructions == "기존 코드 유지");
            settings.FontSize = 20; store.Save(settings);
            Assert(store.Load().FontSize == 20 && store.Load().ClaudePath == "한글 경로.exe" && Directory.GetFiles(Path.GetDirectoryName(store.FilePath)).Length == 1);
        });
        Check("malformed and invalid settings are rejected", () => {
            var store = new AssistantSettingsStore(Path.Combine(root, "bad-settings.json"));
            File.WriteAllText(store.FilePath, "null"); Expect<InvalidDataException>(() => store.Load());
            Expect<InvalidDataException>(() => store.Save(new AssistantSettings { FontSize = 500 }));
        });
        Check("WPF tabs retain drafts attachments paths and separate conversations", () => {
            using (var control = new AssistantControl()) {
                ((TextBox)control.FindName("Instruction")).Text = "Codex 초안";
                ((TextBox)control.FindName("ExecutablePath")).Text = fixture;
                control.AttachFiles(new[] { textPath });
                var tabs = (TabControl)control.FindName("ProviderChoice"); tabs.SelectedIndex = 1;
                Assert(((TextBox)control.FindName("Instruction")).Text == "" && ((ListBox)control.FindName("AttachmentList")).Items.Count == 0);
                ((TextBox)control.FindName("Instruction")).Text = "Claude 초안"; tabs.SelectedIndex = 0;
                Assert(((TextBox)control.FindName("Instruction")).Text == "Codex 초안" && ((TextBox)control.FindName("ExecutablePath")).Text == fixture && ((ListBox)control.FindName("AttachmentList")).Items.Count == 1);
            }
        });
        Check("WPF repeated sends transmit rules attachment model and prior turns", () => {
            using (var control = new AssistantControl()) {
                control.Initialize(new TestDocuments(NewDocument()), new CliEditProvider(TimeSpan.FromSeconds(10)));
                ((TextBox)control.FindName("ExecutablePath")).Text = fixture;
                ((TextBox)control.FindName("RulesText")).Text = "테스트 규칙";
                ((TextBox)control.FindName("ModelName")).Text = "fixture-model";
                control.AttachFiles(new[] { textPath });
                var generate = (Button)control.FindName("GenerateButton");
                ((TextBox)control.FindName("Instruction")).Text = "fixture:ok"; Click(generate); PumpUntil(() => generate.IsEnabled, 15000);
                ((TextBox)control.FindName("Instruction")).Text = "fixture:context"; Click(generate); PumpUntil(() => generate.IsEnabled, 15000);
                Assert(((ItemsControl)control.FindName("ConversationMessages")).Items.Count == 4 && ((CheckBox)control.FindName("ReviewApproval")).IsEnabled && !((Button)control.FindName("ApplyButton")).IsEnabled);
                Click((Button)control.FindName("NewChatButton"));
                Assert(((ItemsControl)control.FindName("ConversationMessages")).Items.Count == 0 && ((ListBox)control.FindName("AttachmentList")).Items.Count == 0 && !((Button)control.FindName("ApplyButton")).IsEnabled);
            }
        });
        Check("WPF Ctrl+V image event attaches without touching OS clipboard", () => {
            using (var control = new AssistantControl()) {
                var data = new DataObject(); data.SetData(DataFormats.Bitmap, bitmap);
                var args = new DataObjectPastingEventArgs(data, false, DataFormats.Bitmap);
                ((TextBox)control.FindName("Instruction")).RaiseEvent(args);
                Assert(args.CommandCancelled && ((ListBox)control.FindName("AttachmentList")).Items.Count == 1);
            }
        });
        Check("WPF settings survive control recreation", () => {
            using (var control = new AssistantControl()) {
                ((TextBox)control.FindName("RulesText")).Text = "저장 규칙";
                ((TextBox)control.FindName("ExecutablePath")).Text = fixture;
                ((ComboBox)control.FindName("TextSizeChoice")).SelectedValue = "18";
                Click((Button)control.FindName("SaveSettingsButton"));
            }
            using (var control = new AssistantControl()) {
                Assert(control.FontSize == 18 && ((TextBox)control.FindName("RulesText")).Text == "저장 규칙" && ((TextBox)control.FindName("ExecutablePath")).Text == fixture);
            }
        });
        foreach (string provider in new[] { "Codex", "Claude" }) {
            Check(provider + " image input protocol and temporary image cleanup", () => {
                string imageLog = Path.Combine(root, "image-path.txt"); Environment.SetEnvironmentVariable("CODEXORIG_CHAT_IMAGE_LOG", imageLog);
                var context = new ChatRequestContext(attachments: new[] { ChatAttachment.FromFile(imagePath) });
                var proposal = new CliEditProvider(TimeSpan.FromSeconds(10)).GenerateWithContextAsync(provider, fixture, NewDocument().Capture(), "fixture:image", context, CancellationToken.None).GetAwaiter().GetResult();
                Assert(proposal.HasChanges);
                if (provider == "Codex") Assert(!File.Exists(File.ReadAllText(imageLog)));
            });
        }
        Check("CLI account status uses official fixed arguments", () => {
            Assert(new CliAccountService().CheckAsync("Codex", fixture, CancellationToken.None).GetAwaiter().GetResult());
            Assert(new CliAccountService().CheckAsync("Claude", fixture, CancellationToken.None).GetAwaiter().GetResult());
        });
        Check("WPF account status hides CLI account details", () => {
            using (var control = new AssistantControl()) {
                ((TextBox)control.FindName("ExecutablePath")).Text = fixture;
                Click((Button)control.FindName("AccountCheckButton"));
                PumpUntil(() => ((Button)control.FindName("AccountCheckButton")).IsEnabled, 15000);
                Assert(((TextBlock)control.FindName("AccountLabel")).Text.StartsWith("연결됨") && !((TextBlock)control.FindName("AccountLabel")).Text.Contains("fixture account details"));
            }
        });
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunFixture
    /// desc         : 실제 네트워크 없이 공식 CLI 형태의 인수를 검증하고 결정적인 응답을 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 오류·대량 출력·자식 프로세스·무응답 fixture 구현.
    /// update date  : 2026-10-18 CodexOrig : Chat 컨텍스트·설정·창 이동 및 검증 흐름 갱신.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int RunFixture(string[] args)
    {
        bool codex = args[0] == "exec";
        string prompt = Console.In.ReadToEnd();
        object[] imageBlocks = null;
        if (!codex && args.Contains("--input-format"))
        {
            var envelope = (Dictionary<string, object>)Json.DeserializeObject(prompt);
            var message = (Dictionary<string, object>)envelope["message"];
            var content = (object[])message["content"];
            imageBlocks = content.Skip(1).ToArray();
            prompt = (string)((Dictionary<string, object>)content[0])["text"];
        }
        string payload = prompt.Substring(prompt.IndexOf('\n') + 1);
        var input = (Dictionary<string, object>)Json.DeserializeObject(payload);
        string request = (string)input["user_request"];
        if (request == "fixture:context")
        {
            if ((string)input["user_instructions"] != "테스트 규칙" || ((object[])input["history"]).Length < 2 ||
                ((object[])input["attachments"]).Length != 1 || args[Array.IndexOf(args, "--model") + 1] != "fixture-model") return 91;
            var attachment = (Dictionary<string, object>)((object[])input["attachments"])[0];
            if ((string)attachment["text"] != "첨부 원문") return 92;
        }
        if (request == "fixture:image")
        {
            if (codex)
            {
                int imageIndex = Array.IndexOf(args, "--image");
                if (imageIndex < 0 || !File.Exists(args[imageIndex + 1]) || File.ReadAllBytes(args[imageIndex + 1])[0] != 137) return 93;
                File.WriteAllText(Environment.GetEnvironmentVariable("CODEXORIG_CHAT_IMAGE_LOG"), args[imageIndex + 1]);
            }
            else if (imageBlocks == null || imageBlocks.Length != 1 ||
                Convert.FromBase64String((string)((Dictionary<string, object>)((Dictionary<string, object>)imageBlocks[0])["source"])["data"])[0] != 137) return 94;
        }
        if (!Directory.GetCurrentDirectory().Contains("VS2017AI-")) return 81;
        if (codex)
        {
            if (!args.Contains("--sandbox") || args[Array.IndexOf(args, "--sandbox") + 1] != "read-only" || !args.Contains("--ignore-user-config") || !args.Contains("--ephemeral") || args.Last() != "-") return 82;
            string schema = File.ReadAllText(args[Array.IndexOf(args, "--output-schema") + 1]);
            if (schema != EditProposal.JsonSchema) return 83;
        }
        else
        {
            if (args[Array.IndexOf(args, "--tools") + 1] != "" || args[Array.IndexOf(args, "--setting-sources") + 1] != "" || !args.Contains("--no-session-persistence") || !args.Contains("--strict-mcp-config")) return 84;
            if (args[Array.IndexOf(args, "--json-schema") + 1] != EditProposal.JsonSchema) return 85;
            if (File.ReadAllText(args[Array.IndexOf(args, "--mcp-config") + 1]) != "{\"mcpServers\":{}}") return 86;
        }
        if (!((string)input["source_text"]).Contains("테스트")) return 87;
        if (request.Contains("한글") && !prompt.Contains("한글")) return 88;
        if (request == "fixture:error") return 7;
        if (request == "fixture:bad-json") { Console.Write("bad json"); return 0; }
        if (request == "fixture:huge") { Console.Write(new string('x', 2000001)); return 0; }
        if (request == "fixture:stderr") Console.Error.Write(new string('e', 100000));
        if (request == "fixture:hang") { Thread.Sleep(60000); return 0; }
        if (request.StartsWith("fixture:child ", StringComparison.Ordinal))
        {
            string childLog = request.Substring("fixture:child ".Length);
            Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--fixture-child \"" + childLog + "\"") { UseShellExecute = false, CreateNoWindow = true });
            Thread.Sleep(60000); return 0;
        }
        if (request == "fixture:claude-error") { Console.Write("{\"is_error\":true,\"structured_output\":{}}"); return 0; }
        if (request == "fixture:missing") { Console.Write("{\"is_error\":false,\"result\":\"no structured data\"}"); return 0; }
        object plan = Json.DeserializeObject(Plan("return 1;", "return 2;"));
        Console.Write(codex ? Json.Serialize(plan) : Json.Serialize(new { type = "result", is_error = false, structured_output = plan }));
        return 0;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 각 회귀 시나리오를 독립 실행하고 실패를 집계한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 예외 원인과 종료 코드 검증 지원.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception exception) { failed++; Console.WriteLine("FAIL " + name + ": " + exception); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Assert
    /// desc         : 기대 결과가 다르면 테스트를 실패 처리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 단정 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Assert(bool condition) { if (!condition) throw new InvalidOperationException("Assertion failed"); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Expect
    /// desc         : 실패 경로가 요구한 예외 형식으로 거부되는지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 예상하지 않은 성공을 테스트 실패로 처리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Snapshot
    /// desc         : 파서 검증용 원본 스냅샷을 생성한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 독립 fixture 생성.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static DocumentSnapshot Snapshot(string content) { return new DocumentSnapshot(System.IO.Path.Combine(root, "sample.cs"), content, "disk", "C# 7.0", false, false); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Plan
    /// desc         : 정확한 JSON 이스케이프가 적용된 단일 교체 fixture를 생성한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 한글·따옴표 응답 지원.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Plan(string oldText, string newText) { return Json.Serialize(new { summary = "테스트 수정안", edits = new[] { new { old_text = oldText, new_text = newText } } }); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Proposal
    /// desc         : 현재 fixture 문서를 기반으로 적용할 수정안을 검증하여 생성한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 현재 버퍼와 수정안 연결.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static EditProposal Proposal(TestDocument doc) { return EditProposal.Parse(doc.Capture(), Plan("return 1;", "return 2;")); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : NewDocument
    /// desc         : 각 테스트에 한글과 공백 경로의 별도 임시 파일을 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 사용자 소스와 격리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static TestDocument NewDocument() { return new TestDocument(System.IO.Path.Combine(root, "문서 " + Guid.NewGuid().ToString("N") + ".cs")); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunCli
    /// desc         : 무과금 fixture exe에 실제 Provider 실행 경로로 요청한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 오류·출력 제한 fixture 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static EditProposal RunCli(string instruction, string provider = "Codex") { return new CliEditProvider(TimeSpan.FromSeconds(10)).GenerateAsync(provider, fixture, NewDocument().Capture(), instruction, CancellationToken.None).GetAwaiter().GetResult(); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WaitFor
    /// desc         : 짧은 제한 시간 안에 프로세스 검증 조건을 기다린다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 무한 대기 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void WaitFor(Func<bool> condition, int milliseconds)
    {
        var timer = Stopwatch.StartNew();
        while (!condition()) { if (timer.ElapsedMilliseconds > milliseconds) throw new TimeoutException("Test wait failed"); Thread.Sleep(20); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProcessExists
    /// desc         : 취소된 fixture의 자식 프로세스가 종료되었는지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 프로세스 종료 경합 처리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool ProcessExists(int pid)
    {
        try { using (var process = Process.GetProcessById(pid)) return !process.HasExited; } catch (ArgumentException) { return false; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Click
    /// desc         : 실제 WPF 버튼의 클릭 이벤트를 실행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 활성화 상태와 연결된 핸들러 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Click(Button button) { Assert(button.IsEnabled); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PumpUntil
    /// desc         : WPF Dispatcher를 유지하며 비동기 UI 요청의 종료를 기다린다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : UI 스레드를 막지 않는 테스트 대기.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void PumpUntil(Func<bool> condition, int milliseconds)
    {
        var frame = new DispatcherFrame(); var clock = Stopwatch.StartNew(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (sender, args) => { if (condition() || clock.ElapsedMilliseconds > milliseconds) frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop(); Assert(condition());
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RenderReview
    /// desc         : 실제 수정안이 표시된 WPF 화면을 이미지로 저장하여 육안 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 빈 화면뿐 아니라 승인 전 비교 화면도 렌더링.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void RenderReview(AssistantControl control)
    {
        Type brushes = Type.GetType("Microsoft.VisualStudio.Shell.VsBrushes, Microsoft.VisualStudio.Shell.15.0", true);
        control.Resources[brushes.GetProperty("ToolWindowBackgroundKey").GetValue(null)] = System.Windows.Media.Brushes.White;
        control.Resources[brushes.GetProperty("ToolWindowTextKey").GetValue(null)] = System.Windows.Media.Brushes.Black;
        control.Measure(new Size(420, 1100)); control.Arrange(new Rect(0, 0, 420, 1100)); control.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(420, 1100, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(control);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var image = File.Create(System.IO.Path.Combine(root, "review.png"))) encoder.Save(image);
    }
}

/// ---------------------------------------------------------------------
/// <summary>
/// name         : TestDocument
/// desc         : 테스트용 편집기 버퍼와 실제 임시 디스크 파일을 구분한다. EnvDTE 동작을 대신 검증하지 않는다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 충돌·저장·실패 fixture 구현.
/// </summary>
/// ---------------------------------------------------------------------
internal sealed class TestDocument : IEditorDocument
{
    public string Path, Buffer;
    public bool ReadOnly, FailReplace, FailSave;
    public int ReplaceCount, SaveCount;
    public string DiskText { get { return File.ReadAllText(Path, Encoding.UTF8); } }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TestDocument
    /// desc         : 테스트 전용 파일을 만들고 초기 버퍼를 동일하게 설정한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 독립 파일 초기화.
    /// </summary>
    /// ---------------------------------------------------------------------
    public TestDocument(string path) { Path = path; Buffer = "// 테스트 원본\r\nclass C { int M() { return 1; } }\r\n"; File.WriteAllText(path, Buffer, new UTF8Encoding(true)); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Capture
    /// desc         : 실제 파일 바이트의 해시와 현재 테스트 버퍼를 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 디스크·버퍼 독립 상태 제공.
    /// </summary>
    /// ---------------------------------------------------------------------
    public DocumentSnapshot Capture()
    {
        string hash; using (var sha = SHA256.Create()) hash = Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(Path)));
        return new DocumentSnapshot(Path, Buffer, hash, "C# 7.0 / .NET Framework 4.6.2", Buffer != DiskText, ReadOnly);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Replace
    /// desc         : 예상 스냅샷을 검증한 뒤 테스트 버퍼만 교체한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 적용 실패 주입 및 호출 수 집계.
    /// </summary>
    /// ---------------------------------------------------------------------
    public void Replace(DocumentSnapshot expected, string content) { expected.RequireUnchanged(Capture()); if (FailReplace) throw new InvalidOperationException("fixture apply failure"); Buffer = content; ReplaceCount++; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Save
    /// desc         : 별도 저장 호출에서만 테스트 파일을 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 저장 실패 주입 및 적용과 저장 분리 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    public void Save(DocumentSnapshot expected) { expected.RequireUnchanged(Capture()); if (FailSave) throw new InvalidOperationException("fixture save failure"); File.WriteAllText(Path, Buffer, new UTF8Encoding(true)); SaveCount++; }
}

/// ---------------------------------------------------------------------
/// <summary>
/// name         : TestDocuments
/// desc         : WPF 테스트에 현재 fixture 문서를 공급한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : IDE 없는 UI 통합 테스트 연결.
/// </summary>
/// ---------------------------------------------------------------------
internal sealed class TestDocuments : IActiveDocumentService
{
    public IEditorDocument Current;
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : TestDocuments
    /// desc         : 테스트 문서 공급자를 초기화한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 서비스 fixture 초기화.
    /// </summary>
    /// ---------------------------------------------------------------------
    public TestDocuments(IEditorDocument document) { Current = document; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GetActiveDocument
    /// desc         : UI 요청의 대상으로 지정한 테스트 문서를 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 활성 문서 fixture 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    public IEditorDocument GetActiveDocument() { return Current; }
}
