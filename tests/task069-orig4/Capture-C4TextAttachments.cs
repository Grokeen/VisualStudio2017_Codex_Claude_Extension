using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Views;

/// <summary>Renders four synthetic UTF-8 text attachments in the frozen C4 product control.</summary>
internal static class CaptureC4TextAttachments
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string Output = Environment.GetEnvironmentVariable("TASK069_C4_CAPTURE_OUTPUT");
    private static readonly string FixtureRoot = Path.Combine(Output, "fixture-text-files");
    private static readonly string[] Names = { "01-requirements.txt", "02-build-log.txt", "03-errors.txt", "04-notes.txt" };
    private static readonly List<object> FixtureInfo = new List<object>();

    [STAThread]
    private static int Main(string[] args)
    {
        string productPath = Path.GetFullPath(args[0]);
        var dependencies = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(args[1]));
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            string name = new AssemblyName(eventArgs.Name).Name;
            string sibling = Path.Combine(Path.GetDirectoryName(productPath), name + ".dll");
            if (File.Exists(sibling)) return Assembly.LoadFrom(sibling);
            string mapped;
            return dependencies.TryGetValue(name, out mapped) && File.Exists(mapped) ? Assembly.LoadFrom(mapped) : null;
        };
        Assembly.LoadFrom(productPath);
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory(FixtureRoot);
        var control = new AssistantControl();
        var activeChat = control.GetType().GetProperty("ActiveChat", Private).GetValue(control, null);
        var attachments = (System.Collections.IList)activeChat.GetType().GetProperty("Attachments").GetValue(activeChat, null);
        for (int index = 0; index < Names.Length; index++)
        {
            string path = Path.Combine(FixtureRoot, Names[index]);
            string content = String.Concat(Enumerable.Repeat("검토 항목과 확인할 근거를 정리한 합성 텍스트 예시입니다.\n", 80));
            File.WriteAllText(path, content, new UTF8Encoding(false));
            var attachment = ChatAttachment.FromFile(path);
            attachments.Add(attachment);
            FixtureInfo.Add(new
            {
                Name = attachment.Name,
                Label = attachment.Label,
                IconText = attachment.IconText,
                Characters = attachment.Text.Length,
                Utf8Bytes = Encoding.UTF8.GetByteCount(attachment.Text),
                Tooltip = attachment.DisplayInfo
            });
        }

        var prompt = (TextBox)control.GetType().GetField("Instruction", Private).GetValue(control);
        prompt.Text = "첨부한 텍스트 네 개를 읽고, 서로 다른 요청 항목과 확인할 오류를 구분해 요약해 주세요.";
        foreach (string theme in new[] { "dark", "light" })
        {
            SetTheme(control, theme);
            Capture(control, theme + "-wide.png", 620, 760);
            Capture(control, theme + "-narrow.png", 390, 760);
        }

        var result = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(new
        {
            Status = "PASS",
            ProductCandidate = "TASK069 C4 VS2017 Release",
            Themes = new[] { "dark", "light" },
            Viewports = new[] { new { Width = 620, Height = 760 }, new { Width = 390, Height = 760 } },
            Files = FixtureInfo,
            FileIo = "Only four synthetic UTF-8 .txt fixtures under this output folder were read.",
            ProductSendOrAi = false,
            WindowShown = false,
            PopupOpened = false,
            OsInputUsed = false,
            NetworkUsed = false,
            Images = new[] { "dark-wide.png", "light-wide.png", "dark-narrow.png", "light-narrow.png" }
        });
        File.WriteAllText(Path.Combine(Output, "capture-result.json"), result, new UTF8Encoding(false));
        control.Dispose();
        return 0;
    }

    private static void SetTheme(AssistantControl control, string theme)
    {
        var choice = (ComboBox)control.GetType().GetField("ThemeModeChoice", Private).GetValue(control);
        var item = choice.Items.Cast<ComboBoxItem>().First(value => String.Equals(Convert.ToString(value.Tag), theme, StringComparison.OrdinalIgnoreCase));
        choice.SelectedItem = item;
        control.UpdateLayout();
    }

    private static void Capture(AssistantControl control, string filename, int width, int height)
    {
        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0, 0, width, height));
        control.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(control);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(Output, filename))) encoder.Save(stream);
    }
}
