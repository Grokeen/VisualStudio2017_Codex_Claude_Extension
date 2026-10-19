using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.PlatformUI;
using Task = System.Threading.Tasks.Task;

namespace CodexOrig.ThemeIntegration
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ThemeIdePackage
    /// desc         : 명시한 자기 RootSuffix 안에서 설치된 제품의 실제 IDE 테마 적용을 검증한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 사용자 프로필·입력·데스크톱 자동화 없이 실제 DTE import/export 계약을 검사한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid("7CC6F17F-DA77-4C8F-A47F-064CA8D3B962")]
    public sealed class ThemeIdePackage : AsyncPackage
    {
        private readonly List<object> rows = new List<object>();
        private string output;
        private int failures;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeAsync
        /// desc         : 전용 설정 파일이 명시된 테스트 프로세스에서만 검사를 예약한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 일반 Visual Studio에서는 실행하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override Task InitializeAsync(CancellationToken token, IProgress<ServiceProgressData> progress)
        {
            var path = Environment.GetEnvironmentVariable("CODEXORIG_THEME064_CONFIG");
            if (!String.IsNullOrEmpty(path) && File.Exists(path))
            {
                var scheduled = JoinableTaskFactory.RunAsync(() => RunAsync(path));
            }
            return Task.CompletedTask;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunAsync
        /// desc         : 실제 호스트를 확인한 뒤 제품을 로드하고 두 테마를 적용·재수출하여 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 소유 IDE와 pending 파일만 정리하고 실패도 그대로 보존한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task RunAsync(string path)
        {
            var json = new JavaScriptSerializer();
            var config = json.Deserialize<ThemeConfig>(File.ReadAllText(path));
            output = config.OutputDirectory;
            DTE dte = null;
            bool owned = false;
            string pending = null;
            string fatal = null;
            try
            {
                File.AppendAllText(Path.Combine(output, "progress.log"), "helper initialized\r\n");
                await Task.Delay(3000);
                await JoinableTaskFactory.SwitchToMainThreadAsync();
                dte = await GetServiceAsync(typeof(DTE)) as DTE;
                if (dte == null || !String.Equals(dte.RegistryRoot, config.ExpectedRegistryRoot, StringComparison.OrdinalIgnoreCase) ||
                    !config.RootSuffix.StartsWith("ThemeApply64", StringComparison.Ordinal) || !dte.RegistryRoot.EndsWith(config.RootSuffix, StringComparison.Ordinal))
                    throw new InvalidOperationException("Exact dedicated RootSuffix identity is required before any settings operation.");
                owned = true;
                Check("IDE-01 exact dedicated profile and year", dte.Version.StartsWith(config.Major + "."));
                File.AppendAllText(Path.Combine(output, "progress.log"), "DTE ready " + dte.RegistryRoot + "\r\n");
                object input = null, commandOutput = null;
                dte.Commands.Raise("{c9201782-e997-4591-a509-7dabd90e97ce}", 0x0100, ref input, ref commandOutput);
                var shell = (IVsShell)await GetServiceAsync(typeof(SVsShell));
                if (shell == null) throw new InvalidOperationException("Visual Studio shell service unavailable.");
                var id = new Guid("f31272f1-3188-4cca-b6e1-37c118cf7824");
                IVsPackage loaded;
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(shell.IsPackageLoaded(ref id, out loaded));
                if (loaded == null) throw new InvalidOperationException("Installed product was not loaded.");
                var product = (Package)loaded;
                var assembly = product.GetType().Assembly;
                Check("IDE-02 installed DLL SHA256 matches fixed candidate", Hash(assembly.Location) == config.ExpectedProductSha256);
                var type = assembly.GetType("VS2017.AIAssistant.Views.AssistantControl", true);
                var paneType = assembly.GetType("VS2017.AIAssistant.ToolWindows.AssistantToolWindow", true);
                var pane = product.FindToolWindow(paneType, 0, false);
                var control = pane.Content as FrameworkElement;
                if (control == null) throw new InvalidOperationException("Installed product control missing.");
                var section = (Border)control.FindName("DisplaySection");
                var buttons = Descendants(section).OfType<Button>().Where(b => Convert.ToString(b.Content).Contains("테마 설치·적용")).ToArray();
                Check("UI-01 both named IDE theme buttons present", buttons.Length == 2 && buttons.Any(b => Equals(b.Tag, "kakao")) && buttons.Any(b => Equals(b.Tag, "dracula")));
                Check("UI-02 buttons stay enabled under default approval permission", buttons.All(b => b.IsEnabled));
                Check("PKG-01 supported year maps to current host", Equals(Static(type, "SupportedIdeYear", dte.Version), config.Year));
                Check("PKG-02 unsupported year rejected", Static(type, "SupportedIdeYear", "18.0") == null);
                var bundled = (string)Static(type, "FindBundledIdeThemePackage", config.Year);
                Check("PKG-03 bundled path is beside installed extension", Path.GetDirectoryName(bundled) == Path.Combine(Path.GetDirectoryName(assembly.Location), "Themes"));
                Check("PKG-04 exact bundled theme VSIX checksum", Hash(bundled) == config.ExpectedThemeSha256);
                pending = (string)Static(type, "PendingIdeThemePath", dte.RegistryRoot);
                var expectedPending = PendingPath(dte.RegistryRoot);
                if (!String.Equals(pending, expectedPending, StringComparison.OrdinalIgnoreCase) || File.Exists(pending))
                    throw new InvalidOperationException("Pending file is not a fresh owned profile path.");
                Directory.CreateDirectory(Path.GetDirectoryName(pending));
                await ExportAsync(dte, "warmup.vssettings");
                var baseline = await ExportAsync(dte, "baseline.vssettings");
                var settings = GeneralSettings(baseline);
                Check("SET-01 meaningful unrelated settings baseline", settings.Count >= 10);
                if (config.ProbeOnly) { await ProbeAsync(dte, type, baseline); return; }
                ReadDebugSettings(dte, "debug-settings-before-noop.json");
                var xamlProperties = new List<object>();
                try
                {
                    foreach (Property property in dte.Properties["TextEditor", "XAML"])
                        try { xamlProperties.Add(new { Name = property.Name, Value = Convert.ToString(property.Value) }); }
                        catch (Exception error) { xamlProperties.Add(new { Name = property.Name, Error = error.Message }); }
                }
                catch (Exception error) { xamlProperties.Add(new { Error = error.ToString() }); }
                File.WriteAllText(Path.Combine(output, "xaml-settings-before-noop.json"), json.Serialize(xamlProperties));
                var noop = new XmlDocument { XmlResolver = null };
                var original = ReadXml(baseline);
                noop.AppendChild(noop.ImportNode(original.DocumentElement.CloneNode(false), false));
                noop.DocumentElement.AppendChild(noop.ImportNode(original.SelectSingleNode("/UserSettings/ApplicationIdentity"), true));
                var originalFonts = original.SelectSingleNode("//Category[@name='Environment_FontsAndColors']");
                var originalGroup = noop.ImportNode(originalFonts.ParentNode, false);
                noop.DocumentElement.AppendChild(originalGroup); originalGroup.AppendChild(noop.ImportNode(originalFonts, true));
                var noopPath = Path.Combine(output, "noop-current-theme-import.vssettings"); noop.Save(noopPath);
                dte.ExecuteCommand("Tools.ImportandExportSettings", "/import:\"" + noopPath + "\"");
                await Task.Delay(1500);
                var settledBaseline = await ExportAsync(dte, "baseline-after-noop.vssettings");
                ReadDebugSettings(dte, "debug-settings-after-noop.json");
                var settled = GeneralSettings(settledBaseline);
                var normalization = settings.Keys.Union(settled.Keys).Where(key => !settings.ContainsKey(key) || !settled.ContainsKey(key) || settings[key] != settled[key]).Select(key => new {
                    Key = key, Before = settings.ContainsKey(key) ? settings[key] : null, After = settled.ContainsKey(key) ? settled[key] : null
                }).ToArray();
                File.WriteAllText(Path.Combine(output, "noop-import-normalization.json"), json.Serialize(normalization));
                Check("SET-02 current-theme no-op import retains selected GUID", SelectedTheme(baseline) == SelectedTheme(settledBaseline));
                baseline = settledBaseline; settings = settled;
                if (!String.IsNullOrEmpty(config.SentinelTemplatePath))
                {
                    baseline = await InitializeSentinelAsync(dte, baseline, config);
                    settings = GeneralSettings(baseline);
                }
                ReadXamlStore("xaml-store-before-apply.json");
                File.WriteAllText(pending, config.Year + "|unknown-theme", new UTF8Encoding(false));
                var malformedInstance = (FrameworkElement)Activator.CreateInstance(type);
                bool malformedEscaped = false;
                try
                {
                    try { type.GetMethod("ApplyPendingIdeThemeOnLoad", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(malformedInstance, new object[] { malformedInstance, new RoutedEventArgs() }); }
                    catch (TargetInvocationException error)
                    {
                        malformedEscaped = true;
                        File.AppendAllText(Path.Combine(output, "progress.log"), "Malformed pending escaped handler: " + error.InnerException + "\r\n");
                    }
                    if (!malformedEscaped) await WaitForProductAsync(malformedInstance, type);
                    Check("BAD-01 malformed pending does not escape Loaded handler", !malformedEscaped);
                    var malformedStatus = (TextBlock)type.GetField("ideThemeInstallStatus", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(malformedInstance);
                    Check("BAD-02 rejected request gives manual recovery guidance", malformedStatus.Text.Contains("도구 > 옵션 > 환경 > 일반"));
                    var malformedExport = await ExportAsync(dte, "malformed.vssettings");
                    var malformedSettings = GeneralSettings(malformedExport);
                    Check("BAD-03 rejected request preserves selected theme and unrelated settings", SelectedTheme(baseline) == SelectedTheme(malformedExport) && settings.Count == malformedSettings.Count && settings.All(x => malformedSettings.ContainsKey(x.Key) && malformedSettings[x.Key] == x.Value));
                }
                finally { ((IDisposable)malformedInstance).Dispose(); }
                var tempDirectory = Path.Combine(Path.GetTempPath(), "GrokeenVisualStudioThemes");
                var tempBefore = Directory.Exists(tempDirectory) ? Directory.GetFiles(tempDirectory).ToArray() : new string[0];
                File.WriteAllText(Path.Combine(output, "temp-before.json"), json.Serialize(tempBefore));
                foreach (var theme in new[] { "kakao", "dracula" })
                {
                    File.WriteAllText(pending, config.Year + "|" + theme, new UTF8Encoding(false));
                    var instance = (FrameworkElement)Activator.CreateInstance(type);
                    try
                    {
                        type.GetMethod("ApplyPendingIdeThemeOnLoad", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, new object[] { instance, new RoutedEventArgs() });
                        await WaitForProductAsync(instance, type);
                        var status = (TextBlock)type.GetField("ideThemeInstallStatus", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
                        File.AppendAllText(Path.Combine(output, "progress.log"), theme + " product status: " + status.Text + "\r\n");
                        Check(theme + "-01 product reported successful apply", status.Text.Contains("테마를 적용했습니다."));
                        Check(theme + "-02 pending consumed after verified apply", !File.Exists(pending));
                        var exported = await ExportAsync(dte, theme + ".vssettings");
                        var expected = theme == "kakao" ? "{1D41C4A3-CF75-4F0A-B2FF-9A8C2D431B92}" : "{8EC84919-8A34-422A-ABE1-CC238AD4D4B1}";
                        Check(theme + "-03 actual IDE export selected theme GUID", String.Equals(SelectedTheme(exported), expected, StringComparison.OrdinalIgnoreCase));
                        if (!String.IsNullOrEmpty(config.SentinelTemplatePath))
                        {
                            string sentinelName = config.Year == "2017" ? "AutoUpdateTags" : "DefaultZoomSetting";
                            string sentinelExpected = config.Year == "2017" ? "False" : "0";
                            var sentinelActual = ReadXml(exported).SelectSingleNode("//PropertyValue[@name='" + sentinelName + "']");
                            Check(theme + "-07 explicit nondefault sentinel retained", sentinelActual != null && String.Equals(sentinelActual.InnerText, sentinelExpected, StringComparison.OrdinalIgnoreCase));
                        }
                        var actualColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey).ToArgb().ToString("X8");
                        var expectedColor = theme == "kakao" ? "FFD9DDE3" : "FF282A36";
                        Check(theme + "-06 actual host ToolWindowBackground matches compiled definition", actualColor == expectedColor);
                        File.AppendAllText(Path.Combine(output, "progress.log"), theme + " ToolWindowBackground=" + actualColor + " expected=" + expectedColor + "\r\n");
                        var after = GeneralSettings(exported);
                        Check(theme + "-04 unrelated settings retained", settings.Count == after.Count && settings.All(x => after.ContainsKey(x.Key) && after[x.Key] == x.Value));
                        ReadDebugSettings(dte, theme + "-debug-settings.json");
                        ReadXamlStore(theme + "-xaml-store.json");
                        var tempAfter = Directory.Exists(tempDirectory) ? Directory.GetFiles(tempDirectory).ToArray() : new string[0];
                        for (int cleanupAttempt = 0; cleanupAttempt < 60 && tempAfter.Except(tempBefore, StringComparer.OrdinalIgnoreCase).Any(); cleanupAttempt++)
                        {
                            await Task.Delay(100);
                            tempAfter = Directory.Exists(tempDirectory) ? Directory.GetFiles(tempDirectory).ToArray() : new string[0];
                        }
                        File.WriteAllText(Path.Combine(output, theme + "-temp-after.json"), json.Serialize(tempAfter));
                        Check(theme + "-05 no import or verification temp file leaked", !tempAfter.Except(tempBefore, StringComparer.OrdinalIgnoreCase).Any());
                    }
                    finally { ((IDisposable)instance).Dispose(); }
                }
            }
            catch (Exception exception) { fatal = exception.ToString(); File.AppendAllText(Path.Combine(output, "progress.log"), fatal + "\r\n"); }
            finally
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();
                if (owned && pending != null && File.Exists(pending)) File.Delete(pending);
                File.WriteAllText(Path.Combine(output, "result.json"), json.Serialize(new {
                    Succeeded = fatal == null && failures == 0, Passed = rows.Count - failures, Failed = failures, FatalError = fatal,
                    Checks = rows, ActualIdeRuntimeTest = true, ActualImportExport = true, UserDefaultProfileChanged = false,
                    DesktopInputUsed = false, VisualSwitchVerified = false, RegistryRoot = owned ? dte.RegistryRoot : null
                }));
                if (owned && dte != null) { dte.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo); dte.Solution.Close(false); dte.Quit(); }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : 검사 결과와 실패 수를 원본 실행 기록에 남긴다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 전용 테스트 프로필과 기록만 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Check(string name, bool ok)
        {
            rows.Add(new { Name = name, Passed = ok });
            if (!ok) failures++;
            File.AppendAllText(Path.Combine(output, "progress.log"), (ok ? "PASS " : "FAIL ") + name + "\r\n");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WaitForProductAsync
        /// desc         : 실제 제품의 비동기 적용 완료 또는 실패 상태를 UI 스레드를 막지 않고 기다린다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 제품 동작을 교체하지 않고 최대 35초 상태를 관찰한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task WaitForProductAsync(FrameworkElement instance, Type type)
        {
            for (int attempt = 0; attempt < 350; attempt++)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var status = (TextBlock)type.GetField("ideThemeInstallStatus", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
                if (status.Text.Contains("테마를 적용했습니다.") || status.Text.Contains("테마 자동 적용을 확인하지 못했습니다.")) return;
                await Task.Delay(100);
            }
            throw new TimeoutException("Product did not report apply completion within 35 seconds.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProbeAsync
        /// desc         : 자기 프로필의 공개 DTE 속성과 최소/전체 설정 import의 차이를 진단한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 원래 export를 보존하고 프로브 뒤 자기 프로필 설정만 복구한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task ProbeAsync(DTE dte, Type type, string baseline)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var json = new JavaScriptSerializer();
            var properties = new List<object>();
            try
            {
                foreach (Property property in dte.Properties["Environment", "General"])
                {
                    object value = null;
                    if (property.Name.IndexOf("theme", StringComparison.OrdinalIgnoreCase) >= 0 || property.Name.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0)
                        try { value = Convert.ToString(property.Value); } catch (Exception error) { value = error.Message; }
                    properties.Add(new { Name = property.Name, Value = value });
                }
            }
            catch (Exception error) { properties.Add(new { Error = error.ToString() }); }
            File.WriteAllText(Path.Combine(output, "general-properties.json"), json.Serialize(properties));
            var commands = new List<string>();
            foreach (Command command in dte.Commands)
                if ((command.Name ?? "").IndexOf("theme", StringComparison.OrdinalIgnoreCase) >= 0) commands.Add(command.Name);
            File.WriteAllText(Path.Combine(output, "theme-commands.json"), json.Serialize(commands));
            var observations = new List<object>();
            try
            {
                foreach (var variant in new[] { "wrapped-kakao", "wrapped-dracula" })
                {
                    string expected = variant.EndsWith("kakao") ? "{1D41C4A3-CF75-4F0A-B2FF-9A8C2D431B92}" : "{8EC84919-8A34-422A-ABE1-CC238AD4D4B1}";
                    var import = Path.Combine(output, variant + "-import.vssettings");
                    var document = new XmlDocument { XmlResolver = null };
                    var source = ReadXml(baseline);
                    document.AppendChild(document.ImportNode(source.DocumentElement.CloneNode(false), false));
                    document.DocumentElement.AppendChild(document.ImportNode(source.SelectSingleNode("/UserSettings/ApplicationIdentity"), true));
                    var fonts = source.SelectSingleNode("//Category[@name='Environment_FontsAndColors']");
                    var group = document.ImportNode(fonts.ParentNode, false);
                    document.DocumentElement.AppendChild(group);
                    group.AppendChild(document.ImportNode(fonts, true));
                    document.SelectSingleNode("//Category[@name='Environment_FontsAndColors']//Theme/@Id").Value = expected;
                    document.Save(import);
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    dte.ExecuteCommand("Tools.ImportandExportSettings", "/import:\"" + import + "\"");
                    await Task.Delay(2000);
                    var exported = await ExportAsync(dte, variant + "-export.vssettings");
                    var selected = SelectedTheme(exported);
                    var before = GeneralSettings(baseline); var after = GeneralSettings(exported);
                    bool preserved = before.Count == after.Count && before.All(x => after.ContainsKey(x.Key) && after[x.Key] == x.Value);
                    observations.Add(new { Variant = variant, ExpectedTheme = expected, ActualTheme = selected, Matches = String.Equals(expected, selected, StringComparison.OrdinalIgnoreCase), UnrelatedSettingsPreserved = preserved });
                    File.AppendAllText(Path.Combine(output, "progress.log"), "PROBE " + variant + " selected=" + selected + "\r\n");
                }
            }
            finally
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var original = ReadXml(Path.Combine(output, "wrapped-kakao-import.vssettings"));
                original.SelectSingleNode("//Category[@name='Environment_FontsAndColors']//Theme/@Id").Value = SelectedTheme(baseline);
                var restorePath = Path.Combine(output, "restore-minimal.vssettings"); original.Save(restorePath);
                dte.ExecuteCommand("Tools.ImportandExportSettings", "/import:\"" + restorePath + "\"");
                await Task.Delay(2000);
                var restored = await ExportAsync(dte, "restored.vssettings");
                var before = GeneralSettings(baseline); var after = GeneralSettings(restored);
                Check("PROBE restore original owned profile theme", SelectedTheme(baseline) == SelectedTheme(restored));
                Check("PROBE restore unrelated owned profile settings", before.Count == after.Count && before.All(x => after.ContainsKey(x.Key) && after[x.Key] == x.Value));
                File.WriteAllText(Path.Combine(output, "import-probes.json"), json.Serialize(observations));
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadDebugSettings
        /// desc         : 자기 IDE의 공개 DTE 디버깅 속성을 읽어 export 정규화와 실제 값 변경을 구분한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 접근 불가 속성도 오류로 보존하며 값을 변경하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ReadDebugSettings(DTE dte, string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var rows = new List<object>();
            try
            {
                foreach (Property property in dte.Properties["Debugging", "General"])
                {
                    try { rows.Add(new { Name = property.Name, Value = Convert.ToString(property.Value) }); }
                    catch (Exception error) { rows.Add(new { Name = property.Name, Error = error.Message }); }
                }
            }
            catch (Exception error) { rows.Add(new { Error = error.ToString() }); }
            File.WriteAllText(Path.Combine(output, name), new JavaScriptSerializer().Serialize(rows));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadXamlStore
        /// desc         : 자기 IDE의 UserSettings를 읽기 전용으로 열어 XAML AutoUpdateTags 존재와 값을 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 기본 사용자 프로필·쓰기 API 없이 최대 3000개 collection 이름만 탐색한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ReadXamlStore(string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var rows = new List<object>(); var xamlCollections = new List<string>();
            var queue = new Queue<string>(); queue.Enqueue(""); int visited = 0;
            try
            {
                var manager = new Microsoft.VisualStudio.Shell.Settings.ShellSettingsManager(this);
                var store = manager.GetReadOnlySettingsStore(Microsoft.VisualStudio.Settings.SettingsScope.UserSettings);
                while (queue.Count > 0 && visited < 3000)
                {
                    var collection = queue.Dequeue(); visited++;
                    if (collection.IndexOf("xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        xamlCollections.Add(collection);
                        foreach (var property in store.GetPropertyNames(collection))
                            if (property.IndexOf("AutoUpdateTags", StringComparison.OrdinalIgnoreCase) >= 0 || property.IndexOf("DefaultZoomSetting", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                var kind = store.GetPropertyType(collection, property);
                                string value;
                                try { value = store.GetBoolean(collection, property).ToString(); }
                                catch (Exception) { try { value = store.GetString(collection, property); } catch (Exception error) { value = error.Message; } }
                                object rawNumber = null;
                                try { rawNumber = store.GetUInt32(collection, property); } catch (Exception) { }
                                rows.Add(new { Collection = collection, Property = property, Type = kind.ToString(), Exists = store.PropertyExists(collection, property), Value = value, UInt32Value = rawNumber });
                            }
                    }
                    if (collection.Count(c => c == '\\') < 7)
                        foreach (var child in store.GetSubCollectionNames(collection)) queue.Enqueue(collection.Length == 0 ? child : collection + "\\" + child);
                }
            }
            catch (Exception error) { rows.Add(new { Error = error.ToString() }); }
            File.WriteAllText(Path.Combine(output, name), new JavaScriptSerializer().Serialize(new { Scope = "UserSettings", ReadOnly = true, VisitedCollections = visited, Truncated = queue.Count > 0, XamlCollections = xamlCollections, TargetProperties = rows }));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeSentinelAsync
        /// desc         : 자기 프로필에서 공식 최소 설정 import로 비기본 XAML 옵션을 만들고 실제 export로 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 레지스트리 쓰기 없이 실제 IDE가 생성한 category의 단일 속성만 복사한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async System.Threading.Tasks.Task<string> InitializeSentinelAsync(DTE dte, string baseline, ThemeConfig config)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            string name = config.Year == "2017" ? "AutoUpdateTags" : "DefaultZoomSetting";
            string expected = config.Year == "2017" ? "False" : "0";
            var source = ReadXml(config.SentinelTemplatePath);
            var sourceNode = source.SelectSingleNode("//PropertyValue[@name='" + name + "']");
            if (sourceNode == null) throw new InvalidOperationException("Known nondefault sentinel schema is absent from preserved IDE export.");
            var document = new XmlDocument { XmlResolver = null };
            document.AppendChild(document.CreateElement("UserSettings"));
            document.DocumentElement.AppendChild(document.ImportNode(ReadXml(baseline).SelectSingleNode("/UserSettings/ApplicationIdentity"), true));
            var subtree = document.ImportNode(sourceNode, true); subtree.InnerText = expected;
            for (var ancestor = sourceNode.ParentNode; ancestor != null && ancestor.Name != "UserSettings"; ancestor = ancestor.ParentNode)
            {
                var parent = document.ImportNode(ancestor, false); parent.AppendChild(subtree); subtree = parent;
            }
            document.DocumentElement.AppendChild(subtree);
            var path = Path.Combine(output, "nondefault-sentinel-import.vssettings"); document.Save(path);
            if (document.SelectNodes("//PropertyValue").Count != 1) throw new InvalidOperationException("Only one nondefault sentinel property may be imported.");
            dte.ExecuteCommand("Tools.ImportandExportSettings", "/import:\"" + path + "\"");
            await Task.Delay(1500);
            var exported = await ExportAsync(dte, "baseline-with-sentinel.vssettings");
            var actual = ReadXml(exported).SelectSingleNode("//PropertyValue[@name='" + name + "']");
            Check("SENTINEL-01 actual IDE exports selected nondefault " + name, actual != null && String.Equals(actual.InnerText, expected, StringComparison.OrdinalIgnoreCase));
            return exported;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Static
        /// desc         : 설치된 제품 DLL의 비공개 정적 계약을 호출한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 전용 테스트 프로필과 기록만 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static object Static(Type type, string name, params object[] args)
        { return type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Hash
        /// desc         : 고정 DLL 또는 VSIX의 SHA256을 계산한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 전용 테스트 프로필과 기록만 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Hash(string path)
        { using (var sha = SHA256.Create()) using (var file = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", ""); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PendingPath
        /// desc         : 명시된 자기 RegistryRoot의 pending 경로를 독립 계산한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 전용 테스트 프로필과 기록만 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string PendingPath(string registryRoot)
        {
            using (var sha = SHA256.Create())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Grokeen", "VisualStudioThemes", "pending-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(registryRoot.ToUpperInvariant())), 0, 12).Replace("-", "") + ".txt");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ExportAsync
        /// desc         : 현재 소유 IDE에서 정식 명령으로 설정을 내보낸다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 전용 테스트 프로필과 기록만 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async System.Threading.Tasks.Task<string> ExportAsync(DTE dte, string name)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var path = Path.Combine(output, name);
            dte.ExecuteCommand("Tools.ImportandExportSettings", "/export:\"" + path + "\"");
            for (int attempt = 0; attempt < 200; attempt++)
            {
                try
                {
                    var document = ReadXml(path);
                    if (document.DocumentElement != null && document.DocumentElement.Name == "UserSettings") return path;
                }
                catch (IOException) { }
                catch (XmlException) { }
                await Task.Delay(100);
            }
            throw new IOException("Actual IDE export did not finish a complete readable XML file within 20 seconds.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadXml
        /// desc         : 외부 엔터티가 금지된 XML reader로 설정을 읽는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 전용 테스트 프로필과 기록만 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static XmlDocument ReadXml(string path)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(path, settings)) document.Load(reader);
            return document;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SelectedTheme
        /// desc         : 실제 export의 선택 테마 GUID를 추출한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 전용 테스트 프로필과 기록만 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string SelectedTheme(string path)
        {
            var node = ReadXml(path).SelectSingleNode("//Category[@name='Environment_FontsAndColors']//Theme/@Id");
            return node == null ? null : node.Value;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GeneralSettings
        /// desc         : 테마 이외의 설정 값을 변경 없이 수집한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 전용 테스트 프로필과 기록만 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Dictionary<string, string> GeneralSettings(string path)
        {
            var values = new Dictionary<string, string>();
            foreach (XmlNode node in ReadXml(path).SelectNodes("//PropertyValue[not(ancestor::Category[@name='Environment_FontsAndColors'])]"))
            {
                var ancestors = new List<string>();
                for (var current = node; current != null && current.NodeType == XmlNodeType.Element; current = current.ParentNode)
                    ancestors.Add(current.Name + ":" + (current.Attributes["name"] == null ? "" : current.Attributes["name"].Value));
                var key = String.Join("/", ancestors.ToArray());
                if (!values.ContainsKey(key)) values.Add(key, node.InnerText);
            }
            return values;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Descendants
        /// desc         : 제품 내부 논리 자식에서 테마 버튼을 열거한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 전용 테스트 프로필과 기록만 사용한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            yield return root;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                foreach (var item in Descendants(child)) yield return item;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ThemeConfig
    /// desc         : 명시한 자기 프로필과 고정 제품·테마 해시만 전달하는 검사 설정이다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 실제 로그인·사용자 기본 프로필 정보는 포함하지 않는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ThemeConfig
    {
        public string OutputDirectory { get; set; }
        public string ExpectedRegistryRoot { get; set; }
        public string RootSuffix { get; set; }
        public string Year { get; set; }
        public string Major { get; set; }
        public string ExpectedProductSha256 { get; set; }
        public string ExpectedThemeSha256 { get; set; }
        public bool ProbeOnly { get; set; }
        public string SentinelTemplatePath { get; set; }
    }
}
