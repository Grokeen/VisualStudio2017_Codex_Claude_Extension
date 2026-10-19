// ---------------------------------------------------------------------
// name         : HistoryPagingChecks
// desc         : 격리된 WPF 창에서 저장 기록과 화면 페이징·위치 복원을 검사한다.
// author       : CodexCode
// create date  : 2026-10-19
// update date  : 2026-10-19 CodexCode : TASK049 실제 제품 DLL의 최근20/상단20/전체 저장 계약.
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
            Check("initial view is latest 20 of 65 stored messages", chat.Messages.Count == 65 && messages.Items.Count == 20 && Text(messages, 0).StartsWith("HISTORY_45") && Text(messages, 19).StartsWith("HISTORY_64"));

            window = new Window { Content = control, Width = 560, Height = 470, ShowActivated = false, ShowInTaskbar = false };
            window.Show(); Pump(); Pump();
            var scroll = (ScrollViewer)control.FindName("ConversationScroll");
            Check("initial latest page has a finite scroll range", scroll.ScrollableHeight > 100);
            double before = scroll.ExtentHeight;
            scroll.ScrollToTop(); Pump(); Pump();
            Check("first top reach prepends previous 20", messages.Items.Count == 40 && Text(messages, 0).StartsWith("HISTORY_25") && Text(messages, 39).StartsWith("HISTORY_64"));
            Check("prepend preserves reading anchor", scroll.ExtentHeight > before && Math.Abs(scroll.VerticalOffset - (scroll.ExtentHeight - before)) < 8);
            scroll.ScrollToTop(); Pump(); Pump();
            Check("second top reach prepends another 20", messages.Items.Count == 60 && Text(messages, 0).StartsWith("HISTORY_05"));
            scroll.ScrollToTop(); Pump(); Pump();
            Check("last top reach exposes remaining five", messages.Items.Count == 65 && Text(messages, 0).StartsWith("HISTORY_00"));
            chat.Add("assistant", "HISTORY_NEW"); Pump();
            Check("new reply appends to visible page and full model", chat.Messages.Count == 66 && messages.Items.Count == 66 && Text(messages, 65) == "HISTORY_NEW");
            bool saved = (bool)typeof(AssistantControl).GetMethod("PersistCurrentConversation", Private).Invoke(control, null);
            var restored = store.LoadOpen("Claude").Single(item => item.Id == record.Id);
            Check("save retains oldest and newest, independently of initial view", saved && restored.Messages.Count == 66 && restored.Messages[0].Text.StartsWith("HISTORY_00") && restored.Messages[65].Text == "HISTORY_NEW");
        }
        catch (Exception error) { Failed.Add("Harness or product exception: " + error); }
        finally { if (control != null) control.Dispose(); if (window != null) window.Close(); }
    }

    private static string Text(ItemsControl messages, int index) { return ((ChatMessage)messages.Items[index]).Text; }
    private static void Check(string name, bool result) { if (result) Passed.Add(name); else Failed.Add(name); }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
}
