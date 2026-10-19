using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Task = System.Threading.Tasks.Task;
using System.Xml;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 동봉된 독립 IDE 테마를 설치하고 재시작 후 해당 인스턴스의 선택만 적용한다.
    /// author       : CodexCode
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK054 통합, 보류 입력 상한과 예외·임시파일 정리 보완.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private const string KakaoIdeThemeId = "{1D41C4A3-CF75-4F0A-B2FF-9A8C2D431B92}";
        private const string DraculaIdeThemeId = "{8EC84919-8A34-422A-ABE1-CC238AD4D4B1}";
        private TextBlock ideThemeInstallStatus;
        private bool pendingIdeThemeChecked;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeIdeThemeInstallation
        /// desc         : 설정 화면에 독립 IDE 테마 설치 안내와 두 동작을 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK054 통합 인수 및 예외·입력 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InitializeIdeThemeInstallation()
        {
            Dispatcher.VerifyAccess();
            var section = DisplaySection.Child as Panel;
            if (section == null) return;
            section.Children.Add(new TextBlock {
                Text = "Visual Studio 전체 테마",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 12, 0, 5)
            });
            section.Children.Add(new TextBlock {
                Text = "설치 파일은 확장에 포함됩니다. 설치 관리자에서 완료한 뒤 Visual Studio를 다시 열면 선택한 테마가 적용됩니다.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
            var actions = new WrapPanel();
            actions.Children.Add(CreateIdeThemeInstallButton("Kakao 테마 설치·적용", "kakao"));
            actions.Children.Add(CreateIdeThemeInstallButton("Dracula 테마 설치·적용", "dracula"));
            section.Children.Add(actions);
            ideThemeInstallStatus = new TextBlock {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0),
                Text = "Visual Studio 2017·2019·2022에서 사용 가능"
            };
            ideThemeInstallStatus.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush");
            section.Children.Add(ideThemeInstallStatus);
            Loaded += ApplyPendingIdeThemeOnLoad;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateIdeThemeInstallButton
        /// desc         : 공통 버튼 스타일과 접근성 이름으로 테마별 설치 동작을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK054 통합 인수 및 예외·입력 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private Button CreateIdeThemeInstallButton(string label, string theme)
        {
            var button = new Button {
                Content = label,
                Tag = theme,
                Margin = new Thickness(0, 0, 8, 6),
                MinHeight = 34,
                ToolTip = "현재 Visual Studio 버전용 오프라인 VSIX 설치 관리자를 엽니다."
            };
            button.SetResourceReference(StyleProperty, "AssistantActionButton");
            AutomationProperties.SetName(button, label);
            button.Click += InstallIdeThemeClicked;
            return button;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InstallIdeThemeClicked
        /// desc         : 현재 IDE의 공식 설치 관리자를 열고 재시작 후 적용 선택을 인스턴스별 저장한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK054 통합 인수 및 예외·입력 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InstallIdeThemeClicked(object sender, RoutedEventArgs args)
        {
            Dispatcher.VerifyAccess();
            if (disposed) return;
            var button = sender as Button;
            var theme = button == null ? null : button.Tag as string;
            if (theme != "kakao" && theme != "dracula") return;
            try
            {
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                var year = SupportedIdeYear(dte == null ? null : dte.Version);
                if (year == null) throw new InvalidOperationException("지원되는 Visual Studio 2017·2019·2022에서 실행해 주세요.");
                var package = FindBundledIdeThemePackage(year);
                var installer = Path.Combine(Path.GetDirectoryName(dte.FullName), "VSIXInstaller.exe");
                if (!File.Exists(installer)) throw new FileNotFoundException("Visual Studio VSIX 설치 관리자를 찾을 수 없습니다.");
                var pending = PendingIdeThemePath(dte.RegistryRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(pending));
                File.WriteAllText(pending, year + "|" + theme, new UTF8Encoding(false));
                try
                {
                    System.Diagnostics.Process.Start(new ProcessStartInfo {
                        FileName = installer,
                        Arguments = "\"" + package.Replace("\"", "\\\"") + "\"",
                        UseShellExecute = true
                    });
                }
                catch { File.Delete(pending); throw; }
                ideThemeInstallStatus.Text = "설치 관리자가 열렸습니다. 설치를 완료하고 Visual Studio를 다시 열면 " +
                    (theme == "kakao" ? "Kakao" : "Dracula") + " 테마가 적용됩니다.";
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is InvalidOperationException || error is System.ComponentModel.Win32Exception || error is ArgumentException || error is System.Runtime.InteropServices.COMException)
            {
                ideThemeInstallStatus.Text = "테마 설치를 시작하지 못했습니다: " + error.Message;
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SupportedIdeYear
        /// desc         : 지원되는 IDE 주 버전을 오프라인 패키지 연도에 대응한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK054 통합 인수 및 예외·입력 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string SupportedIdeYear(string version)
        {
            if (String.IsNullOrWhiteSpace(version)) return null;
            var major = version.Split('.')[0];
            if (major == "15") return "2017";
            if (major == "16") return "2019";
            if (major == "17") return "2022";
            return null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FindBundledIdeThemePackage
        /// desc         : 현재 IDE용 동봉 VSIX 하나만 허용하고 누락·중복은 거절한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK054 통합 인수 및 예외·입력 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string FindBundledIdeThemePackage(string year)
        {
            var directory = Path.Combine(Path.GetDirectoryName(typeof(AssistantControl).Assembly.Location), "Themes");
            if (!Directory.Exists(directory)) throw new FileNotFoundException("현재 Visual Studio용 테마 설치 파일이 확장에 포함되지 않았습니다.");
            var packages = Directory.GetFiles(directory, "Grokeen-VisualStudio-Themes-*-VS" + year + ".vsix");
            if (packages.Length != 1) throw new FileNotFoundException("현재 Visual Studio용 테마 설치 파일이 없거나 중복되었습니다.");
            return packages[0];
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PendingIdeThemePath
        /// desc         : IDE 인스턴스의 해시로 다른 프로필과 분리된 보류 경로를 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK054 통합 인수 및 예외·입력 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string PendingIdeThemePath(string registryRoot)
        {
            if (String.IsNullOrWhiteSpace(registryRoot)) throw new InvalidOperationException("Visual Studio 인스턴스를 확인할 수 없습니다.");
            byte[] bytes;
            using (var sha = SHA256.Create())
                bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(registryRoot.ToUpperInvariant()));
            var key = BitConverter.ToString(bytes, 0, 12).Replace("-", "");
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Grokeen", "VisualStudioThemes", "pending-" + key + ".txt");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApplyPendingIdeThemeOnLoad
        /// desc         : 설치 확인 후 테마만 가져오고 내보낸 GUID를 확인하여 보류를 소비한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK054 통합 인수 및 예외·입력 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100", Justification = "WPF Loaded event; bounded waits and all expected failures are observed within the handler.")]
        private async void ApplyPendingIdeThemeOnLoad(object sender, RoutedEventArgs args)
        {
            if (disposed || pendingIdeThemeChecked || !IsVisualStudioHost()) return;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            pendingIdeThemeChecked = true;
            string importPath = null;
            var exportPaths = new List<string>();
            try
            {
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte == null) return;
                var pending = PendingIdeThemePath(dte.RegistryRoot);
                if (!File.Exists(pending)) return;
                var request = ReadBoundedThemeFile(pending, 64).Trim().Split('|');
                var year = SupportedIdeYear(dte.Version);
                if (request.Length != 2 || request[0] != year ||
                    (request[1] != "kakao" && request[1] != "dracula"))
                    throw new InvalidDataException("저장된 테마 선택이 현재 Visual Studio와 일치하지 않습니다.");
                if (!IsIdeThemeInstalled(dte.RegistryRoot, year))
                    throw new InvalidOperationException("테마 VSIX 설치 완료를 확인하지 못했습니다.");
                var themeId = request[1] == "kakao" ? KakaoIdeThemeId : DraculaIdeThemeId;
                var version = year == "2017" ? "15.0" : year == "2019" ? "16.0" : "17.0";
                var temp = Path.Combine(Path.GetTempPath(), "GrokeenVisualStudioThemes");
                Directory.CreateDirectory(temp);
                importPath = Path.Combine(temp, "theme-" + Guid.NewGuid().ToString("N") + ".vssettings");
                File.WriteAllText(importPath, ThemeSettingsXml(version, themeId), new UTF8Encoding(false));
                dte.ExecuteCommand("Tools.ImportandExportSettings", "/import:\"" + importPath + "\"");
                // DTE returns before its dispatcher finishes importing or closes the export stream.
                // Keep the import file alive, yield the UI thread, and verify completed exports.
                bool applied = false;
                var deadline = DateTime.UtcNow.AddSeconds(10);
                do
                {
                    await Task.Delay(250);
                    if (disposed) return;
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var exportPath = Path.Combine(temp, "check-" + Guid.NewGuid().ToString("N") + ".vssettings");
                    exportPaths.Add(exportPath);
                    dte.ExecuteCommand("Tools.ImportandExportSettings", "/export:\"" + exportPath + "\"");
                    var exported = await ReadReadyThemeExportAsync(exportPath);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    if (disposed) return;
                    var selected = exported.SelectSingleNode("//Category[@name='Environment_FontsAndColors']/FontsAndColors/Theme/@Id");
                    applied = selected != null && String.Equals(selected.Value, themeId, StringComparison.OrdinalIgnoreCase);
                } while (!applied && DateTime.UtcNow < deadline);
                if (!applied)
                    throw new InvalidOperationException("Visual Studio가 선택한 테마를 확인하지 못했습니다.");
                File.Delete(pending);
                ideThemeInstallStatus.Text = (request[1] == "kakao" ? "Kakao" : "Dracula") + " Visual Studio 테마를 적용했습니다.";
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is InvalidOperationException || error is ArgumentException || error is XmlException || error is System.Runtime.InteropServices.COMException)
            {
                ideThemeInstallStatus.Text = "테마 자동 적용을 확인하지 못했습니다. 도구 > 옵션 > 환경 > 일반에서 테마를 선택해 주세요. " + error.Message;
            }
            finally
            {
                if (importPath != null) await DeleteThemeTemporaryFileAsync(importPath);
                foreach (var path in exportPaths) await DeleteThemeTemporaryFileAsync(path);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsIdeThemeInstalled
        /// desc         : 현재 IDE 프로필의 독립 테마 설치 매니페스트를 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK054 통합 인수 및 예외·입력 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsIdeThemeInstalled(string registryRoot, string year)
        {
            var split = registryRoot.LastIndexOf('\\');
            var instance = registryRoot.Substring(split + 1);
            if (!Regex.IsMatch(instance, @"^(15|16|17)\.0_[A-Za-z0-9]+$")) return false;
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "VisualStudio", instance, "Extensions");
            if (!Directory.Exists(directory)) return false;
            foreach (var manifest in Directory.GetFiles(directory, "extension.vsixmanifest", SearchOption.AllDirectories))
            {
                var content = ReadBoundedThemeFile(manifest, 2 * 1024 * 1024);
                if (Regex.IsMatch(content, "<Identity\\s+Id=\"Grokeen\\.VisualStudioThemes\\." + year + "\"",
                    RegexOptions.IgnoreCase)) return true;
            }
            return false;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ThemeSettingsXml
        /// desc         : 허용된 테마 GUID와 IDE가 요구하는 Environment_Group 계층의 최소 설정 XML을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK054 통합 인수 및 예외·입력 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string ThemeSettingsXml(string version, string themeId)
        {
            if (version != "15.0" && version != "16.0" && version != "17.0") throw new ArgumentException("Unsupported Visual Studio version.", nameof(version));
            if (themeId != KakaoIdeThemeId && themeId != DraculaIdeThemeId) throw new ArgumentException("Unsupported theme.", nameof(themeId));
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<UserSettings><ApplicationIdentity version=\"" + version + "\"/>" +
                "<Category name=\"Environment_Group\" RegisteredName=\"Environment_Group\">" +
                "<Category name=\"Environment_FontsAndColors\" Category=\"{1EDA5DD4-927A-43A7-810E-7FD247D0DA1D}\" " +
                "Package=\"{DA9FB551-C724-11D0-AE1F-00A0C90FFFC3}\" RegisteredName=\"Environment_FontsAndColors\" " +
                "PackageName=\"Visual Studio Environment Package\"><PropertyValue name=\"Version\">2</PropertyValue>" +
                "<FontsAndColors Version=\"2.0\"><Theme Id=\"" + themeId + "\"/><Categories/></FontsAndColors></Category></Category></UserSettings>";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadBoundedThemeFile
        /// desc         : 4KB 버퍼로 로컬 설정을 읽으며 문자 상한을 넘는 입력은 거절한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK054 통합 인수 및 예외·입력 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ReadBoundedThemeFile(string path, int maximumCharacters)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                var buffer = new char[Math.Min(maximumCharacters + 1, 4096)];
                var text = new StringBuilder(Math.Min(maximumCharacters, 4096));
                int count;
                while ((count = reader.Read(buffer, 0, Math.Min(buffer.Length, maximumCharacters - text.Length + 1))) > 0)
                {
                    if (count > maximumCharacters - text.Length)
                        throw new InvalidDataException("테마 설정 파일이 허용 크기를 초과했습니다.");
                    text.Append(buffer, 0, count);
                }
                return text.ToString();
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadReadyThemeExportAsync
        /// desc         : IDE의 공유 잠금과 미완성 XML이 해제될 때까지 UI를 막지 않고 제한 시간 동안 기다린다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 실제 VS2017 DTE 비동기 내보내기 경합 보완, DTD·외부 참조 거절.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async System.Threading.Tasks.Task<XmlDocument> ReadReadyThemeExportAsync(string path)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (true)
            {
                try
                {
                    var text = ReadBoundedThemeFile(path, 16 * 1024 * 1024);
                    var document = new XmlDocument { XmlResolver = null };
                    using (var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                        document.Load(reader);
                    if (document.DocumentElement == null || document.DocumentElement.Name != "UserSettings")
                        throw new InvalidDataException("테마 확인 파일 형식이 올바르지 않습니다.");
                    return document;
                }
                catch (Exception error) when ((error is IOException || error is XmlException) && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(100);
                }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DeleteThemeTemporaryFileAsync
        /// desc         : 현재 요청의 임시 파일만 삭제하며 IDE의 짧은 공유 잠금은 제한된 재시도로 기다린다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 내보내기 완료 전 삭제 경합에서 잔존 파일 최소화.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static async Task DeleteThemeTemporaryFileAsync(string path)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                try { File.Delete(path); return; }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    if (attempt == 49) return;
                    await Task.Delay(100);
                }
            }
        }
    }
}
