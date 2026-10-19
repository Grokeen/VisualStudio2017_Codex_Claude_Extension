using System;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using VS2017.AIAssistant.Views;
using WpfWindow = System.Windows.Window;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : CompletionNotificationChecks
/// desc         : 합성 완료 요청 한 번에서 제품의 실제 NotifyIcon 생성·OS 이벤트·수명 정리를 관측한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : 실제 AI·개인 대화·키 입력·OS 알림 설정을 사용하지 않는다.
/// </summary>
/// ---------------------------------------------------------------------
public static class CompletionNotificationChecks
{
    public static bool IconCreated, TimerStarted, DuplicateCompletionKeptSameIcon, TimedCleanup, DisposedCleanup;
    public static int BalloonShownEvents;
    public static string Failure;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 자기 WPF 창만 표시하고 완료 API를 호출한 뒤 22초 수명과 Windows 표시 이벤트를 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : OS가 표시를 억제하면 관측 false로 남기며 설정을 변경하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run()
    {
        AssistantControl control = null; WpfWindow window = null; DispatcherTimer lifetime = null;
        var frame = new DispatcherFrame();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            control = new AssistantControl();
            typeof(AssistantControl).GetField("scmLookupStarted", flags).SetValue(control, true);
            window = new WpfWindow { Title = "Completion notification verification", Content = control, Width = 320, Height = 440, ShowActivated = false, ShowInTaskbar = false };
            window.Show(); window.UpdateLayout();
            typeof(AssistantControl).GetMethod("BeginProgressRequest", flags).Invoke(control, null);
            typeof(AssistantControl).GetMethod("CompleteProgressRequest", flags).Invoke(control, new object[] { "completed" });
            var iconField = typeof(AssistantControl).GetField("completionNotificationIcon", flags);
            var timerField = typeof(AssistantControl).GetField("completionNotificationLifetime", flags);
            var icon = iconField.GetValue(control);
            var timer = (DispatcherTimer)timerField.GetValue(control);
            IconCreated = icon != null && (bool)icon.GetType().GetProperty("Visible").GetValue(icon, null);
            TimerStarted = timer != null && timer.IsEnabled;
            if (icon != null) icon.GetType().GetEvent("BalloonTipShown").AddEventHandler(icon, new EventHandler((sender, args) => { BalloonShownEvents++; }));
            typeof(AssistantControl).GetMethod("CompleteProgressRequest", flags).Invoke(control, new object[] { "completed" });
            DuplicateCompletionKeptSameIcon = Object.ReferenceEquals(icon, iconField.GetValue(control));
            lifetime = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(22) };
            lifetime.Tick += (sender, args) => {
                lifetime.Stop();
                TimedCleanup = iconField.GetValue(control) == null && timerField.GetValue(control) == null;
                frame.Continue = false;
            };
            lifetime.Start(); Dispatcher.PushFrame(frame);
            control.Dispose();
            DisposedCleanup = iconField.GetValue(control) == null && timerField.GetValue(control) == null;
        }
        catch (Exception error) { Failure = error.ToString(); }
        finally { if (lifetime != null) lifetime.Stop(); if (control != null) control.Dispose(); if (window != null) window.Close(); }
    }
}
