using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Animation;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Views;

namespace VS2017.AIAssistant.Views
{
    public partial class AssistantControl : UserControl
    {
        internal bool disposed = false, thinkingAnimationActive = false;
        internal int MotionRefreshes;
        private void UpdateThinkingAnimation(bool active) { MotionRefreshes++; thinkingAnimationActive = active; }
    }
}

/// ---------------------------------------------------------------------
/// <summary>
/// name         : MotionChecks
/// desc         : motion token duration, easing, OS-off immediate state and settings-event lifetime are tested independently.
/// author       : CodexOrig4
/// create date  : 2026-10-19
/// update date  : 2026-10-19 CodexOrig4 : no native windows, registry changes or shared product build.
/// </summary>
/// ---------------------------------------------------------------------
internal static class MotionChecks
{
    private static readonly List<object> checks = new List<object>();
    private static int failures;
    private static void Check(string name, bool pass, string detail)
    {
        checks.Add(new { Name = name, Passed = pass, Detail = detail });
        if (!pass) failures++;
        Console.WriteLine((pass ? "PASS " : "FAIL ") + name + " " + detail);
    }
    private static void Invoke(object target, string method, params object[] arguments)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }
    [STAThread]
    private static int Main(string[] args)
    {
        var animated = AssistantMotionTokens.CreatePrimaryEntry(0, 1, 55, true);
        Check("Primary entry token is 220ms", animated.Duration.TimeSpan == TimeSpan.FromMilliseconds(220), animated.Duration.ToString());
        Check("Primary entry uses EaseOut", animated.EasingFunction is CubicEase && ((CubicEase)animated.EasingFunction).EasingMode == EasingMode.EaseOut, "");
        Check("Fan stagger remains 55ms", animated.BeginTime == TimeSpan.FromMilliseconds(55), animated.BeginTime.ToString());
        var micro = AssistantMotionTokens.CreateMicroInteraction(1, .45, true);
        Check("Guide hover and focus feedback remains a 150ms micro-interaction", micro.Duration.TimeSpan == TimeSpan.FromMilliseconds(150), micro.Duration.ToString());
        Check("Micro-interaction shares the common EaseOut curve", micro.EasingFunction is CubicEase && ((CubicEase)micro.EasingFunction).EasingMode == EasingMode.EaseOut, "");
        Check("Reduced motion also disables micro-interaction animation", AssistantMotionTokens.CreateMicroInteraction(0, 1, false).Duration.TimeSpan == TimeSpan.Zero, "");
        var reduced = AssistantMotionTokens.CreatePrimaryEntry(0, 1, 0, false);
        Check("Reduced motion token has zero duration", reduced.Duration.TimeSpan == TimeSpan.Zero, reduced.Duration.ToString());
        Check("Reduced motion token has no easing", reduced.EasingFunction == null, "");
        var surface = new Border { Opacity = .2 };
        bool usedAnimation = AssistantMotionTokens.ApplyPrimaryEntry(surface, UIElement.OpacityProperty, .2, .75, 0, false);
        Check("OS motion-off applies the final visible state immediately", !usedAnimation && Math.Abs(surface.Opacity - .75) < .001, "Opacity=" + surface.Opacity);
        Check("OS motion-off creates no animation clock", !DependencyPropertyHelper.GetValueSource(surface, UIElement.OpacityProperty).IsAnimated, "");
        var boundSurface = new Border();
        BindingOperations.SetBinding(boundSurface, UIElement.OpacityProperty, new Binding("Value") { Source = new { Value = .35 } });
        AssistantMotionTokens.ApplyPrimaryEntry(boundSurface, UIElement.OpacityProperty, .35, .8, 0, false);
        Check("OS motion-off preserves an existing dependency-property binding", BindingOperations.IsDataBound(boundSurface, UIElement.OpacityProperty), "");
        usedAnimation = AssistantMotionTokens.ApplyPrimaryEntry(surface, UIElement.OpacityProperty, .75, .4, 0, true);
        Check("Motion-on selects the animation path", usedAnimation, "A not-yet-presented Border does not receive a WPF animation clock.");
        usedAnimation = AssistantMotionTokens.ApplyPrimaryEntry(surface, UIElement.OpacityProperty, .4, .9, 0, false);
        Check("Disabling motion clears an active clock and reaches the final state", !usedAnimation && Math.Abs(surface.Opacity - .9) < .001 && !DependencyPropertyHelper.GetValueSource(surface, UIElement.OpacityProperty).IsAnimated, "Opacity=" + surface.Opacity);
        var control = new AssistantControl { thinkingAnimationActive = true };
        try
        {
            Invoke(control, "MotionPreferenceChanged", new object[] { null, new PropertyChangedEventArgs("ClientAreaAnimation") });
            Check("Client-area preference change refreshes active thinking indication", control.MotionRefreshes == 1, "Refreshes=" + control.MotionRefreshes);
            Invoke(control, "MotionPreferenceChanged", new object[] { null, new PropertyChangedEventArgs("FontSmoothingGammaCurve") });
            Check("Unrelated Windows setting change is ignored", control.MotionRefreshes == 1, "Refreshes=" + control.MotionRefreshes);
        }
        catch (Exception error)
        {
            while (error.InnerException != null) error = error.InnerException;
            Check("Windows preference event fixture execution", false, error.GetType().FullName + ": " + error.Message);
        }
        var result = new {
            Succeeded = failures == 0, Passed = checks.Count - failures, Failed = failures, Checks = checks,
            ClientAreaAnimationsEnabledOnTestHost = SystemParameters.ClientAreaAnimation,
            WindowShown = false, WindowsSettingsChanged = false, SourceOnly = true
        };
        System.IO.File.WriteAllText(args[0], new JavaScriptSerializer().Serialize(result));
        return failures == 0 ? 0 : 1;
    }
}
