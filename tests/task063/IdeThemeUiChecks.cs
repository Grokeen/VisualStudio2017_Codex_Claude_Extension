using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using VS2017.AIAssistant.Views;

public static class IdeThemeUiChecks
{
    public static void Run()
    {
        var view = new AssistantControl();
        try
        {
            var display = view.FindName("DisplaySection") as Border;
            var panel = display == null ? null : display.Child as StackPanel;
            if (panel == null) throw new InvalidOperationException("Display settings section missing.");
            var actions = panel.Children.OfType<WrapPanel>()
                .SelectMany(p => p.Children.OfType<Button>())
                .Where(b => (b.Tag as string) == "kakao" || (b.Tag as string) == "dracula")
                .ToArray();
            if (actions.Length != 2 || actions.Select(b => b.Tag as string).Distinct().Count() != 2)
                throw new InvalidOperationException("Both Visual Studio theme install actions must be present once.");
            if (actions.Any(b => b.Style == null || String.IsNullOrWhiteSpace(AutomationProperties.GetName(b))))
                throw new InvalidOperationException("Theme actions need styling and accessible names.");
            var status = panel.Children.OfType<TextBlock>().LastOrDefault();
            if (status == null || !status.Text.Contains("2017·2019·2022"))
                throw new InvalidOperationException("Supported IDE versions are not explained.");
            Console.WriteLine("Theme settings UI: 2 distinct accessible install actions and version status PASS");
        }
        finally { view.Dispose(); }
    }
}
