using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Grokeen.OfflineThemeInstaller
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Program
    /// desc         : 오프라인 AI 확장·테마 VSIX와 검증 스크립트를 내장한 Windows 설치기를 시작한다.
    /// author       : CodexCode
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class Program
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Main
        /// desc         : STA Windows Forms 메시지 루프에서 설치기 창을 시작한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : InstallerForm
    /// desc         : 공통 어두운 UI에서 IDE 검색·오프라인 설치·안전한 중지 상태를 표시한다.
    /// author       : CodexCode
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class InstallerForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, int wParam, int lParam);
        private const string Version = "__PACKAGE_VERSION__";
        // Dracula IDE 테마와 동일한 기본 팔레트를 설치기에도 적용한다.
        private static readonly Color BaseColor = Color.FromArgb(40, 42, 54);       // #282A36
        private static readonly Color SurfaceColor = Color.FromArgb(68, 71, 90);   // #44475A
        private static readonly Color ForegroundColor = Color.FromArgb(248, 248, 242); // #F8F8F2
        private static readonly Color MutedColor = Color.FromArgb(185, 185, 196);   // #B9B9C4
        private static readonly Color PurpleColor = Color.FromArgb(189, 147, 249); // #BD93F9
        private static readonly Color CyanColor = Color.FromArgb(139, 233, 253);   // #8BE9FD
        private static readonly Color GreenColor = Color.FromArgb(80, 250, 123);   // #50FA7B
        private static readonly Color AmberColor = Color.FromArgb(255, 184, 108);  // #FFB86C
        private static readonly Color CoralColor = Color.FromArgb(255, 85, 85);    // #FF5555
        private readonly string[] payloadNames = {
            "VS2017.15.7.AIAssistant-" + Version + ".vsix", "VS2017.AIAssistant-" + Version + ".vsix",
            "VS2019.AIAssistant-" + Version + ".vsix", "VS2022.AIAssistant-" + Version + ".vsix",
            "Grokeen-VisualStudio-Themes-" + Version + "-VS2017.vsix",
            "Grokeen-VisualStudio-Themes-" + Version + "-VS2019.vsix",
            "Grokeen-VisualStudio-Themes-" + Version + "-VS2022.vsix"
        };
        private readonly Label status;
        private readonly Label detail;
        private readonly Label requestedAction;
        private readonly Label stage;
        private readonly TextBox log;
        private readonly Button install;
        private readonly Button stop;
        private readonly Button close;
        private readonly CenterGauge activity;
        private readonly string payloadDirectory;
        private readonly string reportDirectory;
        private readonly string cancelFlagPath;
        private bool busy;
        private bool stopRequested;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InstallerForm
        /// desc         : 설치 상태·작업 안내·로그·시작/중지 동작을 한 화면에 구성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        internal InstallerForm()
        {
            Text = "Grokeen AI Assistant + Visual Studio 테마 설치";
            ClientSize = new Size(850, 690);
            MinimumSize = new Size(780, 660);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None;
            Font = new Font("Malgun Gothic", 9F);
            BackColor = BaseColor;
            ForeColor = ForegroundColor;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = BaseColor, Margin = Padding.Empty, Padding = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));

            var header = new HeaderPanel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            var eyebrow = new Label { Text = "GROKEEN  /  OFFLINE DEPLOYMENT", ForeColor = PurpleColor, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Location = new Point(30, 22) };
            var title = new Label { Text = "Visual Studio AI Assistant", ForeColor = ForegroundColor, BackColor = Color.Transparent, Font = new Font("Segoe UI", 23F, FontStyle.Bold), AutoSize = true, Location = new Point(27, 46) };
            var subtitle = new Label { Text = "확장과 Kakao·Dracula IDE 테마를 한 번에 설치합니다", ForeColor = MutedColor, BackColor = Color.Transparent, Font = new Font("Malgun Gothic", 9.5F), AutoSize = true, Location = new Point(30, 103) };
            header.Controls.Add(eyebrow);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            stage = new Label { Dock = DockStyle.Fill, Text = "01  패키지 확인     ›     02  Visual Studio 검색     ›     03  설치", ForeColor = PurpleColor, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(30, 19, 0, 0), Margin = Padding.Empty };

            var statusCard = new Panel { Dock = DockStyle.Fill, BackColor = SurfaceColor, Margin = new Padding(22, 0, 22, 12), Padding = Padding.Empty };
            statusCard.Paint += PaintCardBorder;
            var statusKicker = new Label { Text = "CURRENT STATUS", ForeColor = CyanColor, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8F, FontStyle.Bold), Location = new Point(22, 16), AutoSize = true };
            status = new Label { Text = "설치 대상 확인 중", ForeColor = ForegroundColor, BackColor = Color.Transparent, Font = new Font("Malgun Gothic", 14F, FontStyle.Bold), Location = new Point(20, 35), AutoSize = true };
            detail = new Label { Text = "내장 패키지의 무결성과 설치된 IDE를 확인합니다.", ForeColor = MutedColor, BackColor = Color.Transparent, Font = new Font("Malgun Gothic", 9F), Location = new Point(22, 72), AutoSize = true };
            requestedAction = new Label { Text = "", ForeColor = AmberColor, BackColor = Color.Transparent, Font = new Font("Malgun Gothic", 9F, FontStyle.Bold), Location = new Point(22, 96), AutoSize = true };
            activity = new CenterGauge { Dock = DockStyle.Bottom, Height = 47, Active = true };
            statusCard.Controls.Add(statusKicker);
            statusCard.Controls.Add(status);
            statusCard.Controls.Add(detail);
            statusCard.Controls.Add(requestedAction);
            statusCard.Controls.Add(activity);

            var logCard = new Panel { Dock = DockStyle.Fill, BackColor = SurfaceColor, Margin = new Padding(22, 0, 22, 0), Padding = new Padding(19, 14, 19, 18) };
            logCard.Paint += PaintCardBorder;
            var logHeading = new Label { Dock = DockStyle.Top, Height = 32, Text = "INSTALL ACTIVITY   /   설치 기록", ForeColor = MutedColor, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = SurfaceColor, ForeColor = ForegroundColor, Font = new Font("Consolas", 9F), TabStop = false };
            logCard.Controls.Add(log);
            logCard.Controls.Add(logHeading);

            var footer = new Panel { Dock = DockStyle.Fill, BackColor = BaseColor, Margin = Padding.Empty };
            var offline = new Label { Text = "●  OFFLINE READY   ·   VS 2017 / 2019 / 2022", ForeColor = GreenColor, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), AutoSize = true, Location = new Point(30, 30) };
            install = CreateActionButton("설치 시작", PurpleColor, BaseColor, 148);
            stop = CreateActionButton("중지", CoralColor, BaseColor, 100);
            close = CreateActionButton("닫기", SurfaceColor, ForegroundColor, 90);
            install.Enabled = false;
            SetStopEnabled(false);
            footer.Controls.Add(offline);
            footer.Controls.Add(install);
            footer.Controls.Add(stop);
            footer.Controls.Add(close);
            footer.Resize += (s, e) => {
                close.Location = new Point(footer.Width - close.Width - 22, 18);
                stop.Location = new Point(close.Left - stop.Width - 10, 18);
                install.Location = new Point(stop.Left - install.Width - 10, 18);
            };
            install.Click += async (s, e) => await InstallAsync();
            stop.Click += (s, e) => StopInstallation();
            close.Click += (s, e) => Close();
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(stage, 0, 1);
            root.Controls.Add(statusCard, 0, 2);
            root.Controls.Add(logCard, 0, 3);
            root.Controls.Add(footer, 0, 4);
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = BaseColor };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            var chrome = new Panel { Dock = DockStyle.Fill, BackColor = BaseColor, Margin = Padding.Empty };
            chrome.Paint += (s, e) => { using (var pen = new Pen(SurfaceColor)) e.Graphics.DrawLine(pen, 0, chrome.Height - 1, chrome.Width, chrome.Height - 1); };
            var chromeTitle = new Label { Text = "◈  GROKEEN  /  INSTALLER", ForeColor = MutedColor, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), AutoSize = true, Location = new Point(19, 9) };
            var minimize = CreateActionButton("—", BaseColor, MutedColor, 38);
            var dismiss = CreateActionButton("×", BaseColor, MutedColor, 38);
            minimize.Height = 30;
            dismiss.Height = 30;
            minimize.Click += (s, e) => WindowState = FormWindowState.Minimized;
            dismiss.Click += (s, e) => Close();
            chrome.MouseDown += BeginWindowMove;
            chromeTitle.MouseDown += BeginWindowMove;
            chrome.Controls.Add(chromeTitle);
            chrome.Controls.Add(minimize);
            chrome.Controls.Add(dismiss);
            chrome.Resize += (s, e) => { dismiss.Location = new Point(chrome.Width - dismiss.Width - 4, 2); minimize.Location = new Point(dismiss.Left - minimize.Width, 2); };
            shell.Controls.Add(chrome, 0, 0);
            shell.Controls.Add(root, 0, 1);
            Controls.Add(shell);
            payloadDirectory = Path.Combine(Path.GetTempPath(), "GrokeenOfflineInstaller", Guid.NewGuid().ToString("N"));
            cancelFlagPath = Path.Combine(payloadDirectory, "cancel.flag");
            reportDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Grokeen", "InstallerLogs", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Shown += async (s, e) => await PrepareAsync();
            FormClosing += OnFormClosing;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateActionButton
        /// desc         : 설치·중지·닫기 버튼에 동일한 간격과 색상 상태를 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Button CreateActionButton(string text, Color fill, Color foreground, int width)
        {
            var button = new Button { Text = text, Width = width, Height = 40, BackColor = fill, ForeColor = foreground, FlatStyle = FlatStyle.Flat, Font = new Font("Malgun Gothic", 9F, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(fill, .15F);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(fill, .1F);
            return button;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BeginWindowMove
        /// desc         : 사용자 정의 제목 영역을 드래그해 설치기 창을 이동한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void BeginWindowMove(object sender, MouseEventArgs args)
        {
            if (args.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, 2, 0);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SetStopEnabled
        /// desc         : 중지 버튼의 동작 가능 상태와 대비를 함께 바꾼다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SetStopEnabled(bool enabled)
        {
            stop.Enabled = enabled;
            stop.BackColor = enabled ? CoralColor : SurfaceColor;
            stop.ForeColor = enabled ? BaseColor : MutedColor;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PaintCardBorder
        /// desc         : 상태 카드와 설치 활동 카드에 공통 경계선을 그린다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void PaintCardBorder(object sender, PaintEventArgs args)
        {
            var panel = (Panel)sender;
            using (var pen = new Pen(Color.FromArgb(98, 114, 164)))
                args.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PrepareAsync
        /// desc         : 내장 리소스를 임시 폴더에 풀고 SHA256 검증 후 읽기 전용 설치 진단을 실행한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task PrepareAsync()
        {
            busy = true;
            close.Enabled = false;
            try
            {
                await Task.Run((Action)ExtractAndVerify);
                Append("내장 VSIX 7개와 설치 도구의 체크섬 확인 완료.");
                stage.Text = "01  패키지 확인 완료     ›     02  Visual Studio 검색 중     ›     03  설치";
                var report = Path.Combine(reportDirectory, "preflight.json");
                var result = await Task.Run(() => RunScript(report, false));
                if (result != 0) throw new InvalidOperationException("설치 대상 진단에 실패했습니다. 위 내용을 확인하세요.");
                var json = File.ReadAllText(report, Encoding.UTF8);
                var ready = Regex.Matches(json, "\"Status\"\\s*:\\s*\"Ready\"").Count;
                if (ready == 0) throw new InvalidOperationException("설치 가능한 Visual Studio가 없습니다.");
                status.Text = "설치 준비 완료";
                detail.Text = ready + "개 Visual Studio 인스턴스를 확인했습니다. 호환되는 버전에만 설치합니다.";
                requestedAction.ForeColor = AmberColor;
                requestedAction.Text = "설치를 진행하려면 ‘설치 시작’을 눌러 주세요.";
                stage.Text = "01  패키지 확인 완료     ›     02  IDE 검색 완료     ›     03  설치 대기";
                activity.Active = false;
                install.Enabled = true;
                Append("진단 파일: " + reportDirectory);
            }
            catch (Exception error)
            {
                status.Text = "설치 준비 실패";
                detail.Text = error.Message;
                requestedAction.ForeColor = CoralColor;
                requestedAction.Text = "설치 파일과 Visual Studio 상태를 확인해 주세요.";
                activity.Active = false;
                Append(error.ToString());
            }
            finally { busy = false; close.Enabled = true; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InstallAsync
        /// desc         : 사용자 클릭 뒤 열린 IDE 종료를 기다려 버전별 확장과 테마를 순서대로 설치한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task InstallAsync()
        {
            busy = true;
            stopRequested = false;
            install.Enabled = false;
            close.Enabled = false;
            SetStopEnabled(true);
            activity.Reset();
            activity.Active = true;
            stage.Text = "01  패키지 확인 완료     ›     02  IDE 검색 완료     ›     03  설치 중";
            status.Text = "확장과 테마 설치 중";
            detail.Text = "현재 패키지를 설치한 뒤 다음 대상의 설치를 이어갑니다.";
            requestedAction.ForeColor = AmberColor;
            requestedAction.Text = "Visual Studio가 열려 있다면 작업을 저장하고 모두 닫아 주세요.";
            try
            {
                if (File.Exists(cancelFlagPath)) File.Delete(cancelFlagPath);
                var attemptReport = Path.Combine(reportDirectory, "installation-" + Guid.NewGuid().ToString("N") + ".json");
                var exit = await Task.Run(() => RunScript(attemptReport, true));
                if (exit == 2)
                {
                    status.Text = "설치를 중지했습니다";
                    detail.Text = "완료된 패키지는 유지되고, 남은 패키지는 설치하지 않았습니다.";
                    requestedAction.ForeColor = AmberColor;
                    requestedAction.Text = "다시 진행하려면 ‘설치 시작’을 눌러 주세요.";
                    stage.Text = "01  패키지 확인 완료     ›     02  IDE 검색 완료     ›     03  중지됨";
                    install.Enabled = true;
                    return;
                }
                if (exit != 0) throw new InvalidOperationException("설치에 실패했습니다. 로그 경로: " + reportDirectory);
                status.Text = "설치가 완료됐습니다";
                activity.Value = 100;
                detail.Text = "확장과 Kakao·Dracula 테마를 설치했습니다.";
                requestedAction.ForeColor = AmberColor;
                requestedAction.Text = "Visual Studio를 다시 열고 도구 > 옵션 > 환경 > 일반에서 테마를 선택해 주세요.";
                stage.Text = "01  패키지 확인 완료     ›     02  IDE 검색 완료     ›     03  설치 완료";
                Append("설치 완료. Kakao·Dracula 테마는 Visual Studio의 도구 > 옵션 > 환경 > 일반에서 선택할 수 있습니다.");
            }
            catch (Exception error)
            {
                status.Text = "설치를 완료하지 못했습니다";
                detail.Text = error.Message;
                requestedAction.ForeColor = CoralColor;
                requestedAction.Text = "설치 기록을 확인한 뒤 다시 시도해 주세요.";
                stage.Text = "01  패키지 확인 완료     ›     02  IDE 검색 완료     ›     03  확인 필요";
                Append(error.ToString());
                install.Enabled = true;
            }
            finally { busy = false; close.Enabled = true; SetStopEnabled(false); activity.Active = false; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StopInstallation
        /// desc         : 강제 종료 없이 중지 플래그를 기록해 IDE 대기 또는 다음 VSIX 경계에서 멈춘다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void StopInstallation()
        {
            if (!busy || stopRequested) return;
            try
            {
                File.WriteAllText(cancelFlagPath, DateTimeOffset.Now.ToString("o"), Encoding.ASCII);
                stopRequested = true;
                SetStopEnabled(false);
                status.Text = "중지 요청을 받았습니다";
                detail.Text = "열린 설치 단계가 끝나면 다음 패키지부터 진행하지 않습니다.";
                requestedAction.ForeColor = CoralColor;
                requestedAction.Text = "현재 단계가 마무리될 때까지 잠시 기다려 주세요.";
                Append("중지 요청: 진행 중인 공식 설치기는 강제 종료하지 않습니다.");
            }
            catch (IOException error) { Append("중지 요청 기록 실패: " + error.Message); }
            catch (UnauthorizedAccessException error) { Append("중지 요청 기록 실패: " + error.Message); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ExtractAndVerify
        /// desc         : 고정 이름의 내장 파일만 추출하고 동봉 SHA256SUMS와 비교한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ExtractAndVerify()
        {
            Directory.CreateDirectory(payloadDirectory);
            Directory.CreateDirectory(reportDirectory);
            Extract("Install-AIAssistant.ps1");
            Extract("SHA256SUMS.txt");
            foreach (var name in payloadNames) Extract(name);
            var sums = File.ReadAllLines(Path.Combine(payloadDirectory, "SHA256SUMS.txt"));
            foreach (var name in payloadNames)
            {
                var matches = Array.FindAll(sums, line => Regex.IsMatch(line, "^[0-9a-fA-F]{64}\\s+\\*?" + Regex.Escape(name) + "$"));
                if (matches.Length != 1) throw new InvalidDataException("체크섬 목록이 올바르지 않습니다: " + name);
                using (var sha = SHA256.Create())
                using (var file = File.OpenRead(Path.Combine(payloadDirectory, name)))
                {
                    var actual = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
                    if (!actual.Equals(matches[0].Substring(0, 64), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("체크섬이 다릅니다: " + name);
                }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Extract
        /// desc         : 고정 이름의 내장 리소스 한 개를 임시 설치 폴더에 기록한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Extract(string name)
        {
            using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload." + name))
            {
                if (source == null) throw new FileNotFoundException("내장 설치 파일이 없습니다: " + name);
                using (var target = File.Create(Path.Combine(payloadDirectory, name))) source.CopyTo(target);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunScript
        /// desc         : 네트워크 호출 없이 동봉 PowerShell 사전검사·공식 VSIX 설치를 실행한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private int RunScript(string report, bool doInstall)
        {
            var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            var script = Path.Combine(payloadDirectory, "Install-AIAssistant.ps1");
            var arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\" -PackageDirectory \"" + payloadDirectory + "\" -PackageVersion " + Version + " -ReportPath \"" + report + "\" -RequireTheme -CompactOutput";
            if (doInstall) arguments += " -Install -InstallTheme -AllReady -WaitForIdeExit -CancelFlagPath \"" + cancelFlagPath + "\"";
            var info = new ProcessStartInfo(powershell, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.Default, StandardErrorEncoding = Encoding.Default };
            using (var process = new Process { StartInfo = info })
            {
                process.OutputDataReceived += (s, e) => { if (e.Data != null) Append(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) Append(e.Data); };
                if (!process.Start()) throw new InvalidOperationException("PowerShell 설치 도구를 실행하지 못했습니다.");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Append
        /// desc         : 하위 설치 프로세스의 출력을 UI 스레드 로그와 안내 상태에 반영한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Append(string line)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke((Action)(() => Append(line))); return; }
            var progress = Regex.Match(line, "^INSTALL_PROGRESS ([0-9]+)/([0-9]+)$");
            if (progress.Success)
            {
                var completed = int.Parse(progress.Groups[1].Value);
                var total = int.Parse(progress.Groups[2].Value);
                if (total > 0 && completed <= total)
                {
                    activity.Value = (int)Math.Round(100D * completed / total);
                    if (activity.Active && !stopRequested) detail.Text = completed + "/" + total + "개 패키지 설치 완료";
                }
                return;
            }
            if (line.Contains("Visual Studio가 실행 중입니다") && !stopRequested)
            {
                status.Text = "Visual Studio 종료 대기 중";
                detail.Text = "열린 IDE가 닫히면 설치를 자동으로 계속합니다.";
                requestedAction.ForeColor = AmberColor;
                requestedAction.Text = "작업을 저장하고 모든 Visual Studio 창을 닫아 주세요.";
            }
            log.AppendText(line + Environment.NewLine);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OnFormClosing
        /// desc         : 설치 중 창 종료를 막고 완료 후 임시 패키지 파일을 정리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OnFormClosing(object sender, FormClosingEventArgs args)
        {
            if (busy) { args.Cancel = true; return; }
            try { if (Directory.Exists(payloadDirectory)) Directory.Delete(payloadDirectory, true); }
            catch { /* 시스템 임시 파일은 다음 정리 주기에 삭제된다. 설치 로그는 별도 보존한다. */ }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : HeaderPanel
    /// desc         : Dracula 팔레트의 자주색 그라데이션과 청록색 궤도 패턴을 그린다.
    /// author       : CodexCode
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class HeaderPanel : Panel
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HeaderPanel
        /// desc         : 깜박임 없는 헤더 그리기 옵션을 사용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        internal HeaderPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OnPaint
        /// desc         : Dracula 배경 그라데이션·궤도·자주색 하단 강조선을 그린다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var gradient = new LinearGradientBrush(ClientRectangle, Color.FromArgb(68, 71, 90), Color.FromArgb(40, 42, 54), 0F))
                e.Graphics.FillRectangle(gradient, ClientRectangle);
            using (var line = new Pen(Color.FromArgb(98, 114, 164), 1F))
            {
                for (var x = Width - 240; x < Width + 50; x += 26) e.Graphics.DrawLine(line, x, 0, x - 80, Height);
            }
            using (var ring = new Pen(Color.FromArgb(95, 189, 147, 249), 1.5F))
            using (var inner = new Pen(Color.FromArgb(70, 139, 233, 253), 1F))
            {
                e.Graphics.DrawEllipse(ring, Width - 191, -83, 246, 246);
                e.Graphics.DrawEllipse(inner, Width - 157, -49, 178, 178);
            }
            using (var edge = new Pen(Color.FromArgb(189, 147, 249), 2F))
                e.Graphics.DrawLine(edge, 29, Height - 2, Width - 30, Height - 2);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CenterGauge
    /// desc         : 성공한 패키지 비율을 가운데에서 양끝으로 확장되는 Dracula 색 게이지로 표시한다.
    /// author       : CodexCode
    /// create date  : 2026-10-20
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class CenterGauge : Control
    {
        private readonly Timer timer;
        private bool active;
        private int phase;
        private int value;
        private int displayedValue;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CenterGauge
        /// desc         : 패키지 완료 비율 변화와 중앙 대기 신호를 부드럽게 그릴 타이머를 준비한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        internal CenterGauge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            timer = new Timer { Interval = 42 };
            Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            timer.Tick += (s, e) => {
                phase = (phase + 1) % 30;
                if (displayedValue < value) displayedValue = Math.Min(value, displayedValue + 2);
                else if (displayedValue > value) displayedValue = Math.Max(value, displayedValue - 2);
                if (!active && displayedValue == value) timer.Stop();
                Invalidate();
            };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Value
        /// desc         : 성공한 패키지 수로 계산한 실제 설치 비율을 0~100으로 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        internal int Value
        {
            get { return value; }
            set { this.value = Math.Max(0, Math.Min(100, value)); if (displayedValue != this.value) timer.Start(); Invalidate(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Reset
        /// desc         : 재시도 때 이전 설치 완료 비율이 잠시 보이지 않도록 0%로 즉시 되돌린다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Reset()
        {
            value = 0;
            displayedValue = 0;
            if (!active) timer.Stop();
            Invalidate();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Active
        /// desc         : 확인·설치 중에는 중앙 신호를 움직이고 대기·완료 시에는 고정 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        internal bool Active
        {
            get { return active; }
            set { active = value; if (value || displayedValue != this.value) timer.Start(); else timer.Stop(); Invalidate(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OnPaint
        /// desc         : 트랙 중앙에서 양쪽으로 늘어나는 완료 비율과 중앙 표식을 그린다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.FromArgb(68, 71, 90));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var center = Width / 2;
            var half = Math.Max(0, (Width - 48) / 2);
            var top = Height - 17;
            using (var track = new SolidBrush(Color.FromArgb(40, 42, 54)))
                e.Graphics.FillRectangle(track, center - half, top, 2 * half, 8);
            var filledHalf = (int)Math.Round(half * displayedValue / 100D);
            if (filledHalf > 0)
            {
                using (var fill = new LinearGradientBrush(new Rectangle(center - filledHalf, top, 2 * filledHalf, 8), Color.FromArgb(189, 147, 249), Color.FromArgb(139, 233, 253), 0F))
                    e.Graphics.FillRectangle(fill, center - filledHalf, top, 2 * filledHalf, 8);
            }
            var alpha = active ? 150 + 3 * Math.Abs(15 - phase) : 220;
            using (var marker = new SolidBrush(Color.FromArgb(alpha, 189, 147, 249)))
                e.Graphics.FillEllipse(marker, center - 5, top - 1, 10, 10);
            using (var caption = new SolidBrush(Color.FromArgb(248, 248, 242)))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                e.Graphics.DrawString(displayedValue + "%", Font, caption, new RectangleF(0, 1, Width, 21), format);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 설치기 창 종료 시 진행 표시 타이머를 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
