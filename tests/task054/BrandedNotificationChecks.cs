using System;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using VS2017.AIAssistant.Views;
using WpfWindow = System.Windows.Window;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : BrandedNotificationChecks
/// desc         : 제공자별 합성 완료에서 내장 로고·요청 요약·실제 Shell 표시와 수명 정리를 관측한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : 실제 AI·개인 대화·키 입력·OS 알림 설정을 사용하지 않는다.
/// </summary>
/// ---------------------------------------------------------------------
public static class BrandedNotificationChecks
{
    public static bool IconCreated, TimerStarted, DuplicateCompletionKeptSameIcon, TimedCleanup, DisposedCleanup;
    public static int BalloonShownEvents;
    public static string Failure;
    public static bool ProviderMatched, RequestSummaryMatched, BrandIconExported;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 자기 WPF 창만 표시하고 완료 API를 호출한 뒤 22초 수명과 Windows 표시 이벤트를 기록한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : OS가 표시를 억제하면 관측 false로 남기며 설정을 변경하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run(string provider, string outputDirectory)
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
            var queueType = typeof(AssistantControl).GetNestedType("QueuedUiRequest", BindingFlags.NonPublic);
            var request = Activator.CreateInstance(queueType, true);
            queueType.GetField("Provider", flags).SetValue(request, provider);
            queueType.GetField("Instruction", flags).SetValue(request, "합성 보고서 파일 생성 token=HiddenValue");
            typeof(AssistantControl).GetField("executingRequest", flags).SetValue(control, request);
            typeof(AssistantControl).GetMethod("BeginProgressRequest", flags).Invoke(control, null);
            // 제출 뒤 작성하는 다음 draft가 실행 요청의 알림에 섞이지 않도록 별도 입력을 만든다.
            ((System.Windows.Controls.TextBox)control.FindName("Instruction")).Text = "알림에 표시하면 안 되는 다음 입력";
            typeof(AssistantControl).GetMethod("CompleteProgressRequest", flags).Invoke(control, new object[] { "completed" });
            var iconField = typeof(AssistantControl).GetField("completionNotificationIcon", flags);
            var timerField = typeof(AssistantControl).GetField("completionNotificationLifetime", flags);
            var icon = iconField.GetValue(control);
            var timer = (DispatcherTimer)timerField.GetValue(control);
            IconCreated = icon != null && (bool)icon.GetType().GetProperty("Visible").GetValue(icon, null);
            if (icon != null)
            {
                ProviderMatched = (string)icon.GetType().GetProperty("Provider").GetValue(icon, null) == provider;
                string title = (string)icon.GetType().GetProperty("BalloonTitle").GetValue(icon, null);
                string body = (string)icon.GetType().GetProperty("BalloonText").GetValue(icon, null);
                RequestSummaryMatched = title == provider + " 작업 완료" && body.Contains("답변을 확인") && !body.Contains("합성 보고서 파일 생성") &&
                    !body.Contains("HiddenValue") && !body.Contains("다음 입력");
                var brand = (System.Drawing.Icon)icon.GetType().GetField("brandIcon", flags).GetValue(icon);
                using (var bitmap = brand.ToBitmap())
                {
                    bitmap.Save(System.IO.Path.Combine(outputDirectory, provider + "-notification-icon.png"), System.Drawing.Imaging.ImageFormat.Png);
                    BrandIconExported = bitmap.Width >= 32 && bitmap.Height >= 32;
                }
            }
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
