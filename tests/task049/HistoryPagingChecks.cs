// ---------------------------------------------------------------------
// name         : HistoryPagingChecks
// desc         : 격리된 WPF 창에서 저장 기록과 화면 페이징·위치 복원을 검사한다.
// author       : CodexCode
// create date  : 2026-10-19
// update date  : 2026-10-19 CodexCode : TASK049 실제 제품 DLL의 최근20/상단20/전체 저장 계약.
//                2026-10-20 CodexOrig : TASK068 최신20 복원·비동기 완료·전체 시간 기록 왕복 계약 검증.
// ---------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Views;

public static class HistoryPagingChecks
{
    public static readonly List<string> Passed = new List<string>();
    public static readonly List<string> Failed = new List<string>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        AssistantControl control = null;
        Window window = null;
        try
        {
            var settingsStore = new AssistantSettingsStore();
            settingsStore.Save(new AssistantSettings { HideWelcome = true, ShowHistory = false });
            var json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(settingsStore.FilePath));
            json.Remove("EnableCompletionNotifications"); json.Remove("VisualThemePreset");
            File.WriteAllText(settingsStore.FilePath, new JavaScriptSerializer().Serialize(json));
            var legacy = settingsStore.Load();
            Check("old settings default to notifications on and VS preset", legacy.EnableCompletionNotifications && legacy.VisualThemePreset == "vs");
            legacy.VisualThemePreset = "unexpected";
            bool rejected = false;
            try { settingsStore.Save(legacy); } catch (InvalidDataException) { rejected = true; }
            Check("unknown palette preset is rejected", rejected);

            var store = new MarkdownWorkspaceStore();
            var record = store.Create("Claude");
            for (int index = 0; index < 65; index++) record.Messages.Add(new ChatMessage { Role = index % 2 == 0 ? "user" : "assistant", Text = "HISTORY_" + index.ToString("D2") + "\n" + new string('가', 180) });
            store.Save(record);
            control = new AssistantControl();
            typeof(AssistantControl).GetField("scmLookupStarted", Private).SetValue(control, true);
            var messages = (ItemsControl)control.FindName("ConversationMessages");
            var chat = (ChatSession)typeof(AssistantControl).GetProperty("ActiveChat", Private).GetValue(control, null);
            Check("initial model and view load only latest 20 of 65 stored messages", chat.Messages.Count == 20 && messages.Items.Count == 20 && Text(messages, 0).StartsWith("HISTORY_45") && Text(messages, 19).StartsWith("HISTORY_64"));

            window = new Window { Content = control, Width = 560, Height = 470, ShowActivated = false, ShowInTaskbar = false };
            window.Show(); Pump(); Pump();
            var scroll = (ScrollViewer)control.FindName("ConversationScroll");
            Check("initial latest page has a finite scroll range", scroll.ScrollableHeight > 100);
            double before = scroll.ExtentHeight;
            scroll.ScrollToTop(); WaitPage(control, messages, 40);
            Check("first top reach prepends previous 20", messages.Items.Count == 40 && Text(messages, 0).StartsWith("HISTORY_25") && Text(messages, 39).StartsWith("HISTORY_64"));
            Check("prepend preserves reading anchor", scroll.ExtentHeight > before && Math.Abs(scroll.VerticalOffset - (scroll.ExtentHeight - before)) < 8);
            scroll.ScrollToTop(); WaitPage(control, messages, 60);
            Check("second top reach prepends another 20", messages.Items.Count == 60 && Text(messages, 0).StartsWith("HISTORY_05"));
            scroll.ScrollToTop(); WaitPage(control, messages, 65);
            Check("last top reach exposes remaining five", messages.Items.Count == 65 && Text(messages, 0).StartsWith("HISTORY_00"));
            chat.Add("assistant", "HISTORY_NEW"); Pump();
            Check("new reply appends to visible page while live model retains its latest window", chat.Messages.Count == 21 && messages.Items.Count == 66 && Text(messages, 65) == "HISTORY_NEW");
            bool saved = (bool)typeof(AssistantControl).GetMethod("PersistCurrentConversation", Private).Invoke(control, null);
            var restored = store.LoadOpen("Claude").Single(item => item.Id == record.Id);
            var complete = restored.Messages.ToList();
            for (int page = 0; page < 10 && complete.Count > 0; page++)
            {
                var older = store.LoadOlder(restored, complete[0], 20);
                if (older.Length == 0) break;
                complete.InsertRange(0, older);
            }
            Check("save preserves latest20 main and all66 original/new messages in time pages", saved && restored.Messages.Count == 20 &&
                complete.Count == 66 && complete.Select(message => message.Id).Distinct().Count() == 66 &&
                complete.Take(65).Select((message, index) => message.Text.StartsWith("HISTORY_" + index.ToString("D2"))).All(value => value) && complete[65].Text == "HISTORY_NEW");
        }
        catch (Exception error) { Failed.Add("Harness or product exception: " + error); }
        finally { if (control != null) control.Dispose(); if (window != null) window.Close(); }
    }

    private static string Text(ItemsControl messages, int index) { return ((ChatMessage)messages.Items[index]).Text; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WaitPage
    /// desc         : 비동기 파일 조회와 앵커 레이아웃이 모두 끝난 뒤 원래 페이지·위치 검사를 수행한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 임의의 짧은 Pump 횟수를 완료 신호로 사용하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void WaitPage(AssistantControl control, ItemsControl messages, int expected)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (messages.Items.Count != expected || (bool)typeof(AssistantControl).GetField("conversationPrepending", Private).GetValue(control))
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("History page/anchor did not finish: expected=" + expected + ",actual=" + messages.Items.Count);
            Pump();
        }
        Pump();
    }
    private static void Check(string name, bool result) { if (result) Passed.Add(name); else Failed.Add(name); }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
}
