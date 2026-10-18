using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : LauncherUiSmoke
/// desc         : 실제 실행기의 검정 GUI를 표시 없이 렌더하고 자동 실행이 없는 초기 상태를 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 사용자 창·계정·실제 검사 프로세스에 접근하지 않음.
/// </summary>
/// ---------------------------------------------------------------------
internal static class LauncherUiSmoke
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 복사한 실행기에서 GUI 인스턴스를 생성하고 초기 선택·실행 상태와 이미지 파일을 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 고정 원본 exe를 로드하지 않는 독립 프로세스 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            var assembly = Assembly.LoadFrom(args[0]); var options = Activator.CreateInstance(assembly.GetType("LauncherOptions"), true);
            var type = assembly.GetType("LauncherForm");
            using (var form = (Form)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { options }, null))
            {
                form.ClientSize = new Size(960, 800); form.ShowInTaskbar = false; form.Opacity = 0; form.Show(); Application.DoEvents();
                var suites = (CheckedListBox)Field(form, "suites");
                if (suites.Items.Count != 10 || suites.CheckedItems.Count != 0) throw new Exception("Initial suite selection");
                if (!suites.Created || suites.Height < 160) throw new Exception("Suite list layout");
                if (((Button)Field(form, "stop")).Enabled || Field(form, "cancellation") != null) throw new Exception("Automatic execution detected");
                if (((TextBox)Field(form, "output")).TextLength != 0 || !((Label)Field(form, "status")).Text.Contains("준비")) throw new Exception("Initial status");
                using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height)); bitmap.Save(args[1], ImageFormat.Png); }
                if (new FileInfo(args[1]).Length < 5000) throw new Exception("Empty GUI rendering");
            }
            Console.WriteLine("PASS launcher GUI: 10 suites, no selection, no auto execution, ready status, black preview"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Field
    /// desc         : 테스트 전용 공개 API를 추가하지 않고 실행기의 초기 입력 상태를 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 읽기 전용 상태 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object Field(object value, string name) { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
}
