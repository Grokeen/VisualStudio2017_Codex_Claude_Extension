using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

/// <summary>
/// Test-only loaded WPF keyboard-focus probe for the fixed TASK073 candidate3 DLL.
/// It uses one actual AssistantDialog.Show window and synthetic workspace records.
/// </summary>
internal static class ActivityFeedFocusRunner
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024, RecursionLimit = 32 };

    [STAThread]
    private static int Main(string[] args)
    {
        var result = new Dictionary<string, object>
        {
            ["SchemaVersion"] = 1,
            ["WindowShowCalled"] = false,
            ["WindowShowCount"] = 0,
            ["ShowDialogCalled"] = false,
            ["PopupOpened"] = false,
            ["OsInputInjected"] = false,
            ["PhysicalMouseUsed"] = false,
            ["GlobalKeyboardInputUsed"] = false,
            ["MarkCurrentReadCalledDirectly"] = false,
            ["ReadButtonClickUsed"] = false,
            ["CliStarted"] = false,
            ["AiOrProviderCalled"] = false,
            ["VisualStudioIdeStarted"] = false,
            ["ProductBuildPerformed"] = false,
            ["ProductInstallPerformed"] = false,
            ["SyntheticWorkspaceOnly"] = true,
            ["SyntheticPersistenceFlowVerified"] = false,
            ["Cases"] = new List<object>()
        };
        if (args == null || args.Length != 1) return 64;

        var environmentNames = new[]
        {
            "VS2017_AI_SETTINGS_PATH", "VS2017_AI_WORKSPACE_PATH",
            "VS2017_AI_DISABLE_CLI_DISCOVERY", "VS2017_AI_DISABLE_RUNTIME_DISCOVERY"
        };
        var previousEnvironment = environmentNames.ToDictionary(name => name,
            name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process), StringComparer.Ordinal);
        Application app = null;
        object control = null, view = null, resolver = null;
        Window dialog = null;
        Assembly productAssembly = null;
        FieldInfo experienceReadyField = null;
        Dictionary<string, object> input = null;
        string resultPath = null;

        try
        {
            string inputPath = Path.GetFullPath(args[0]);
            input = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(inputPath));
            resultPath = Convert.ToString(input["ResultPath"]);
            result["RunId"] = input["RunId"];
            result["ProductDllSha256"] = input["ProductDllSha256"];
            result["ProductVsixSha256"] = input["ProductVsixSha256"];
            result["ProjectAssetsSha256"] = input["ProjectAssetsSha256"];
            result["CandidateInventorySha256"] = input["CandidateInventorySha256"];
            result["CandidateActivityFeedViewSourceSha256"] = input["CandidateActivityFeedViewSourceSha256"];
            result["ResolverSourceSha256"] = input["ResolverSourceSha256"];
            result["ResolverDllSha256"] = input["ResolverDllSha256"];
            result["ResolverSmokeEvidenceSha256"] = input["ResolverSmokeEvidenceSha256"];
            result["StartedUtc"] = DateTimeOffset.UtcNow.ToString("O");
            result["ProcessId"] = System.Diagnostics.Process.GetCurrentProcess().Id;

            string dllPath = Path.GetFullPath(Convert.ToString(input["ProductDllPath"]));
            string resolverDllPath = Path.GetFullPath(Convert.ToString(input["ResolverDllPath"]));
            string settingsPath = Path.GetFullPath(Convert.ToString(input["SettingsPath"]));
            string workspacePath = Path.GetFullPath(Convert.ToString(input["WorkspacePath"]));
            string[] runtimeDirectories = ((ArrayList)input["RuntimeDirectories"]).Cast<object>().Select(Convert.ToString).ToArray();
            string actualDllHash = HashFile(dllPath);
            if (!EqualHash(actualDllHash, Convert.ToString(input["ProductDllSha256"])))
                throw new InvalidDataException("Frozen product DLL SHA changed before runner load.");

            Environment.SetEnvironmentVariable("VS2017_AI_SETTINGS_PATH", settingsPath, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", workspacePath, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("VS2017_AI_DISABLE_CLI_DISCOVERY", "1", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("VS2017_AI_DISABLE_RUNTIME_DISCOVERY", "1", EnvironmentVariableTarget.Process);

            Assembly bridgeAssembly = Assembly.LoadFrom(resolverDllPath);
            Type bridgeType = bridgeAssembly.GetType("CodexOrig.Task073.Validation.ManagedCandidateResolver", true);
            ConstructorInfo bridgeConstructor = bridgeType.GetConstructor(new[] { typeof(string), typeof(string[]) });
            if (bridgeConstructor == null) throw new MissingMethodException("Fixed managed resolver constructor is unavailable.");
            resolver = bridgeConstructor.Invoke(new object[] { dllPath, runtimeDirectories });

            app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            productAssembly = Assembly.LoadFrom(dllPath);
            Type controlType = productAssembly.GetType("VS2017.AIAssistant.Views.AssistantControl", true);
            control = Activator.CreateInstance(controlType);
            experienceReadyField = controlType.GetField("experienceReady", InstanceMembers);
            if (experienceReadyField == null || !Convert.ToBoolean(experienceReadyField.GetValue(control)))
                throw new MissingFieldException("AssistantControl experienceReady initialization contract changed.");

            object[] fixtureRecords = SeedSyntheticWorkspace(control, productAssembly);
            Type dialogType = productAssembly.GetType("VS2017.AIAssistant.Views.AssistantDialog", true);
            ConstructorInfo dialogConstructor = dialogType.GetConstructor(new[] { controlType, typeof(string), typeof(string) });
            if (dialogConstructor == null) throw new MissingMethodException("Frozen AssistantDialog constructor is unavailable.");
            dialog = (Window)dialogConstructor.Invoke(new object[] { control, "TASK073 focus fixture", "Synthetic keyboard-focus check." });
            dialog.Width = 700;
            dialog.Height = 620;
            dialog.MinWidth = 520;
            dialog.MinHeight = 420;
            dialog.ShowInTaskbar = false;

            MethodInfo factory = controlType.GetMethod("BuildActivityFeedView", BindingFlags.Instance | BindingFlags.NonPublic);
            if (factory == null || factory.GetParameters().Length != 1 || factory.GetParameters()[0].ParameterType != dialogType)
                throw new MissingMethodException("Frozen BuildActivityFeedView(AssistantDialog) signature is unavailable.");
            view = factory.Invoke(control, new object[] { dialog });
            FrameworkElement root = (FrameworkElement)ReadMember(view, "Root");
            ContentControl body = (ContentControl)ReadMember(dialog, "Body");
            body.Content = root;
            Action refresh = (Action)ReadMember(view, "Refresh");
            Action dispose = (Action)ReadMember(view, "Dispose");
            refresh();
            dialog.Show();
            result["WindowShowCalled"] = true;
            result["WindowShowCount"] = 1;
            result["DialogType"] = dialog.GetType().FullName;

            bool loaded = PumpUntil(() => root.IsLoaded && dialog.IsVisible, 5000);
            dialog.Activate();
            PumpUntil(() => dialog.IsActive, 1500);
            result["WindowLoaded"] = loaded;
            result["WindowActiveAfterShow"] = dialog.IsActive;
            result["RootIsLoaded"] = root.IsLoaded;
            result["FocusCasesRun"] = false;

            if (!loaded || !dialog.IsActive)
            {
                result["EnvironmentLimit"] = "Shown WPF fixture window did not become active and loaded; focus assertions are recorded as not run.";
                AddNotRunCases(result, "WindowNotActiveOrLoaded");
            }
            else
            {
                result["FocusCasesRun"] = true;
                ConfigureFilter(view, "Attention");
                PumpUntil(() => dialog.IsActive && root.IsLoaded, 1000);
                var rows = (IDictionary)ReadMember(view, "Rows");
                object targetUnread = fixtureRecords[2];
                string targetId = Convert.ToString(ReadMember(targetUnread, "Id"));
                object finalAttentionRow = GetRow(rows, "Claude", targetId);
                Button readButton = (Button)ReadMember(finalAttentionRow, "ReadButton");
                object initialFocus = SetWpfFocus(dialog, readButton);
                bool beforeUnread = Convert.ToBoolean(ReadMember(targetUnread, "HasUnread"));
                readButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, readButton));
                result["ReadButtonClickUsed"] = true;
                bool readRemoved = PumpUntil(() => !rows.Contains("Claude\0" + targetId), 2500);
                bool rowRemoved = !rows.Contains("Claude\0" + targetId);
                object focusAfterRead = CaptureFocusSnapshot(dialog, root, view);
                bool focusAfterReadAcceptable = FocusIsFilter(dialog, view) || FocusIsWithinLiveRow(view);
                AddCase(result, "AttentionFinalUnreadRowReadRestoresFocus", focusAfterReadAcceptable,
                    new Dictionary<string, object>
                    {
                        ["WindowActiveBeforeAction"] = GetBoolean(initialFocus, "WindowActive"),
                        ["UnreadBefore"] = beforeUnread,
                        ["UnreadAfter"] = Convert.ToBoolean(ReadMember(targetUnread, "HasUnread")),
                        ["TargetRowRemovedFromAttentionFilter"] = readRemoved && rowRemoved,
                        ["FocusAfter"] = focusAfterRead,
                        ["FocusOnFilterOrLiveRow"] = focusAfterReadAcceptable,
                        ["Method"] = "Actual ReadButton.Click routed handler; no direct controller method call."
                    });

                ConfigureFilter(view, "All");
                PumpUntil(() => dialog.IsActive && root.IsLoaded, 1000);
                rows = (IDictionary)ReadMember(view, "Rows");
                object firstRecord = fixtureRecords[0];
                object secondRecord = fixtureRecords[1];
                string firstId = Convert.ToString(ReadMember(firstRecord, "Id"));
                string secondId = Convert.ToString(ReadMember(secondRecord, "Id"));
                object firstRow = GetRow(rows, "Claude", firstId);
                object secondRow = GetRow(rows, "Claude", secondId);
                Button firstOpen = (Button)ReadMember(firstRow, "OpenButton");
                Button secondOpen = (Button)ReadMember(secondRow, "OpenButton");

                object firstFocus = SetWpfFocus(dialog, firstOpen);
                SetMember(firstRecord, "State", Enum.Parse(productAssembly.GetType("VS2017.AIAssistant.Chat.ConversationRunState", true), "Failed"));
                SetMember(firstRecord, "UpdatedAt", DateTimeOffset.Now.AddMinutes(10));
                Action refreshForAnchor = (Action)ReadMember(view, "Refresh");
                refreshForAnchor();
                DispatcherOperation pendingForFilter = (DispatcherOperation)ReadMember(view, "PendingFocusRestore");
                bool filterCallbackQueued = pendingForFilter != null && pendingForFilter.Status == DispatcherOperationStatus.Pending;
                object filter = ReadMember(view, "Filter");
                object filterFocus = SetWpfFocus(dialog, (IInputElement)filter);
                PumpOperation(pendingForFilter, 2500);
                object focusAfterFilter = CaptureFocusSnapshot(dialog, root, view);
                bool filterStayedFocused = FocusIsFilter(dialog, view);
                AddCase(result, "PendingRefreshDoesNotStealFilterFocus", filterCallbackQueued && filterStayedFocused,
                    new Dictionary<string, object>
                    {
                        ["WindowActiveBeforeRefresh"] = GetBoolean(firstFocus, "WindowActive"),
                        ["PendingCallbackWasQueued"] = filterCallbackQueued,
                        ["FilterFocusAcceptedBeforeCallback"] = GetBoolean(filterFocus, "FocusRequestAccepted"),
                        ["FocusAfterCallback"] = focusAfterFilter,
                        ["FilterFocusPreserved"] = filterStayedFocused
                    });

                object rapidFirstRow = GetRow(rows, "Claude", firstId);
                object rapidSecondRow = GetRow(rows, "Claude", secondId);
                Button rapidFirstOpen = (Button)ReadMember(rapidFirstRow, "OpenButton");
                Button rapidSecondOpen = (Button)ReadMember(rapidSecondRow, "OpenButton");
                object rapidInitialFocus = SetWpfFocus(dialog, rapidFirstOpen);
                SetMember(firstRecord, "UpdatedAt", DateTimeOffset.Now.AddMinutes(11));
                refresh();
                DispatcherOperation rapidOperation = (DispatcherOperation)ReadMember(view, "PendingFocusRestore");
                bool rapidCallbackQueued = rapidOperation != null && rapidOperation.Status == DispatcherOperationStatus.Pending;
                object rapidFocus = SetWpfFocus(dialog, rapidSecondOpen);
                PumpOperation(rapidOperation, 2500);
                object focusAfterRapid = CaptureFocusSnapshot(dialog, root, view);
                bool secondRowKeptFocus = rapidSecondOpen.IsKeyboardFocused;
                AddCase(result, "RapidFocusOnAnotherRowIsPreserved", rapidCallbackQueued && secondRowKeptFocus,
                    new Dictionary<string, object>
                    {
                        ["WindowActiveBeforeRefresh"] = GetBoolean(rapidInitialFocus, "WindowActive"),
                        ["PendingCallbackWasQueued"] = rapidCallbackQueued,
                        ["OtherRowFocusRequestAccepted"] = GetBoolean(rapidFocus, "FocusRequestAccepted"),
                        ["FocusAfterCallback"] = focusAfterRapid,
                        ["OtherRowStillFocused"] = secondRowKeptFocus
                    });

                object anchorCase = RunAnchorAndReferenceCase(dialog, root, view, fixtureRecords);
                result["Cases"] = AppendCase((List<object>)result["Cases"], anchorCase);

                object disposeInitialRow = GetRow(rows, "Claude", secondId);
                Button disposeOpen = (Button)ReadMember(disposeInitialRow, "OpenButton");
                object disposeFocus = SetWpfFocus(dialog, disposeOpen);
                SetMember(secondRecord, "UpdatedAt", DateTimeOffset.Now.AddMinutes(12));
                refresh();
                DispatcherOperation disposeOperation = (DispatcherOperation)ReadMember(view, "PendingFocusRestore");
                bool disposeCallbackQueued = disposeOperation != null && disposeOperation.Status == DispatcherOperationStatus.Pending;
                dispose();
                dialog.Close();
                PumpFor(300);
                bool aborted = disposeOperation != null && disposeOperation.Status == DispatcherOperationStatus.Aborted;
                bool isDisposed = Convert.ToBoolean(ReadMember(view, "IsDisposed"));
                int remainingRows = ((IDictionary)ReadMember(view, "Rows")).Count;
                bool closed = !dialog.IsVisible;
                AddCase(result, "CloseAndDisposeCancelsPendingFocusCallback", disposeCallbackQueued && aborted && isDisposed && remainingRows == 0 && closed,
                    new Dictionary<string, object>
                    {
                        ["WindowActiveBeforeRefresh"] = GetBoolean(disposeFocus, "WindowActive"),
                        ["PendingCallbackWasQueued"] = disposeCallbackQueued,
                        ["PendingCallbackAborted"] = aborted,
                        ["ViewDisposed"] = isDisposed,
                        ["RowsAfterDispose"] = remainingRows,
                        ["WindowClosed"] = closed,
                        ["FocusAfterClose"] = CaptureFocusSnapshot(dialog, root, view)
                    });
            }
        }
        catch (Exception error)
        {
            result["Succeeded"] = false;
            result["ErrorType"] = error.GetType().FullName;
            result["ErrorHResult"] = error.HResult;
            if (error.InnerException != null)
            {
                result["InnerExceptionType"] = error.InnerException.GetType().FullName;
                result["InnerExceptionHResult"] = error.InnerException.HResult;
            }
        }
        finally
        {
            try
            {
                if (control != null && experienceReadyField != null) experienceReadyField.SetValue(control, false);
                if (view != null)
                {
                    try { ((Action)ReadMember(view, "Dispose"))(); } catch { }
                }
                if (dialog != null && dialog.IsVisible) dialog.Close();
                if (control != null)
                {
                    try
                    {
                        MethodInfo dispose = control.GetType().GetMethod("Dispose", InstanceMembers);
                        if (dispose != null) dispose.Invoke(control, new object[0]);
                        result["ActualControlCleanupInvoked"] = dispose != null;
                        result["ExperienceReadyWasSetFalseBeforeCleanup"] = experienceReadyField != null && !Convert.ToBoolean(experienceReadyField.GetValue(control));
                    }
                    catch (Exception cleanupError)
                    {
                        result["CleanupErrorType"] = cleanupError.GetType().FullName;
                    }
                }
                if (app != null) app.Shutdown();
            }
            catch (Exception cleanupError)
            {
                result["CleanupErrorType"] = cleanupError.GetType().FullName;
            }
            foreach (string name in environmentNames)
                Environment.SetEnvironmentVariable(name, previousEnvironment[name], EnvironmentVariableTarget.Process);
            if (resolver != null)
            {
                try { resolver.GetType().GetMethod("Dispose").Invoke(resolver, new object[0]); result["ResolverDisposed"] = true; }
                catch { result["ResolverDisposed"] = false; }
                try { result["ResolverResolutions"] = ReadResolverRecords(resolver); }
                catch { result["ResolverResolutions"] = new object[0]; }
            }
        }

        try
        {
            result["WindowClosedByRunner"] = dialog == null || !dialog.IsVisible;
            result["OwnProcessCleanupVerifiedByRunner"] = false;
            result["OwnProcessCleanupVerification"] = "The wrapper must verify this process exited after this result file was written.";
            if (input != null && !String.IsNullOrWhiteSpace(resultPath))
            {
                result["ProductDllSha256After"] = HashFile(Convert.ToString(input["ProductDllPath"]));
                result["ResolverDllSha256After"] = HashFile(Convert.ToString(input["ResolverDllPath"]));
                result["FinishedUtc"] = DateTimeOffset.UtcNow.ToString("O");
                var cases = (List<object>)result["Cases"];
                int failed = cases.Count(value => String.Equals(Convert.ToString(((Dictionary<string, object>)value)["Status"]), "FAIL", StringComparison.Ordinal));
                int notRun = cases.Count(value => String.Equals(Convert.ToString(((Dictionary<string, object>)value)["Status"]), "NOT_RUN", StringComparison.Ordinal));
                result["Passed"] = cases.Count - failed - notRun;
                result["Failed"] = failed;
                result["NotRun"] = notRun;
                result["Succeeded"] = failed == 0 && notRun == 0 && Convert.ToBoolean(result["FocusCasesRun"] ?? false);
                File.WriteAllText(resultPath, Json.Serialize(result), new UTF8Encoding(false));
            }
        }
        catch (Exception)
        {
            if (input != null && !String.IsNullOrWhiteSpace(resultPath))
            {
                try { result["ResultWriteFailure"] = true; File.WriteAllText(resultPath, Json.Serialize(result), new UTF8Encoding(false)); } catch { }
            }
        }

        int exitFailed = result.ContainsKey("Failed") ? Convert.ToInt32(result["Failed"]) : 1;
        int exitNotRun = result.ContainsKey("NotRun") ? Convert.ToInt32(result["NotRun"]) : 1;
        return exitFailed == 0 && exitNotRun == 0 && Convert.ToBoolean(result.ContainsKey("Succeeded") ? result["Succeeded"] : false) ? 0 : 1;
    }

    private static object[] SeedSyntheticWorkspace(object control, Assembly assembly)
    {
        Type controlType = control.GetType();
        IDictionary records = (IDictionary)ReadMember(control, "conversationRecords");
        IDictionary active = (IDictionary)ReadMember(control, "activeRecords");
        var failures = ReadMember(control, "activitySaveFailures") as ICollection;
        records.Clear(); active.Clear(); failures?.GetType().GetMethod("Clear").Invoke(failures, new object[0]);
        Type recordType = assembly.GetType("VS2017.AIAssistant.Chat.ConversationRecord", true);
        Type stateType = assembly.GetType("VS2017.AIAssistant.Chat.ConversationRunState", true);
        Type listType = records.GetType().GetGenericArguments()[1];
        var claude = (IList)Activator.CreateInstance(listType);
        var codex = (IList)Activator.CreateInstance(listType);
        records["Claude"] = claude;
        records["Codex"] = codex;
        object workspaceStore = ReadMember(control, "workspaceStore");
        MethodInfo create = workspaceStore.GetType().GetMethod("Create", new[] { typeof(string) });
        MethodInfo save = workspaceStore.GetType().GetMethod("Save", new[] { recordType });
        if (create == null || save == null) throw new MissingMethodException("Frozen MarkdownWorkspaceStore Create/Save methods are unavailable.");
        var fixtures = new object[12];
        DateTimeOffset now = DateTimeOffset.Now;
        for (int index = 0; index < fixtures.Length; index++)
        {
            object record = create.Invoke(workspaceStore, new object[] { "Claude" });
            SetMember(record, "IsOpen", true);
            SetMember(record, "HasUnread", index < 3);
            SetMember(record, "State", Enum.Parse(stateType, "Completed"));
            SetMember(record, "CreatedAt", now.AddMinutes(-index - 1));
            SetMember(record, "UpdatedAt", now.AddMinutes(-index - 1));
            SetMember(record, "Draft", "");
            string id = Convert.ToString(ReadMember(record, "Id"));
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[0-9a-f]{32}$"))
                throw new InvalidDataException("Product store did not create a valid opaque conversation key.");
            claude.Add(record);
            save.Invoke(workspaceStore, new[] { record });
            fixtures[index] = record;
        }
        active["Claude"] = fixtures[0];
        active["Codex"] = fixtures[fixtures.Length - 1];
        return fixtures;
    }

    private static object RunAnchorAndReferenceCase(Window dialog, FrameworkElement root, object view, object[] fixtures)
    {
        ConfigureFilter(view, "All");
        PumpUntil(() => dialog.IsActive && root.IsLoaded, 1000);
        object filter = ReadMember(view, "Filter");
        SetWpfFocus(dialog, (IInputElement)filter);
        object scrollObject = ReadMember(view, "Scroll");
        ScrollViewer scroll = (ScrollViewer)scrollObject;
        var rows = (IDictionary)ReadMember(view, "Rows");
        root.UpdateLayout(); scroll.UpdateLayout();
        if (!scroll.IsLoaded || scroll.ScrollableHeight <= 0)
        {
            return MakeCase("UnchangedRowsAndScrollAnchor", "NOT_RUN", new Dictionary<string, object>
            {
                ["Reason"] = "Synthetic row count did not produce a loaded scrollable viewport.",
                ["ScrollViewerLoaded"] = scroll.IsLoaded,
                ["ScrollableHeight"] = scroll.ScrollableHeight
            });
        }
        scroll.ScrollToVerticalOffset(Math.Min(180, scroll.ScrollableHeight));
        root.UpdateLayout(); scroll.UpdateLayout();
        object anchorRow = FindFirstVisibleRow(view, scroll);
        if (anchorRow == null)
        {
            return MakeCase("UnchangedRowsAndScrollAnchor", "NOT_RUN", new Dictionary<string, object>
            {
                ["Reason"] = "No loaded row intersected the actual viewport after scroll.",
                ["ScrollableHeight"] = scroll.ScrollableHeight
            });
        }
        object anchorSurface = ReadMember(anchorRow, "Surface");
        double yBefore = RelativeY((UIElement)anchorSurface, scroll);
        object unchangedRecord = fixtures[3];
        object changedRecord = fixtures[fixtures.Length - 1];
        string unchangedId = Convert.ToString(ReadMember(unchangedRecord, "Id"));
        string changedId = Convert.ToString(ReadMember(changedRecord, "Id"));
        if (String.Equals(unchangedId, changedId, StringComparison.Ordinal)) throw new InvalidDataException("Fixture identity collision.");
        object unchangedRow = GetRow(rows, "Claude", unchangedId);
        object unchangedSurface = ReadMember(unchangedRow, "Surface");
        object unchangedOpen = ReadMember(unchangedRow, "OpenButton");
        Type stateType = changedRecord.GetType().Assembly.GetType("VS2017.AIAssistant.Chat.ConversationRunState", true);
        SetMember(changedRecord, "State", Enum.Parse(stateType, "Failed"));
        SetMember(changedRecord, "UpdatedAt", DateTimeOffset.Now.AddHours(1));
        ((Action)ReadMember(view, "Refresh"))();
        DispatcherOperation operation = (DispatcherOperation)ReadMember(view, "PendingFocusRestore");
        bool callbackQueued = operation != null && operation.Status == DispatcherOperationStatus.Pending;
        PumpOperation(operation, 2500);
        root.UpdateLayout(); scroll.UpdateLayout();
        object anchorAfterRow = GetRow(rows, "Claude", Convert.ToString(ReadMember(ReadMember(anchorRow, "Item"), "Id")));
        double yAfter = RelativeY((UIElement)ReadMember(anchorAfterRow, "Surface"), scroll);
        object unchangedAfter = GetRow(rows, "Claude", unchangedId);
        bool sameControls = ReferenceEquals(unchangedRow, unchangedAfter) &&
            ReferenceEquals(unchangedSurface, ReadMember(unchangedAfter, "Surface")) &&
            ReferenceEquals(unchangedOpen, ReadMember(unchangedAfter, "OpenButton"));
        bool anchorStayed = Math.Abs(yBefore - yAfter) <= 3.0;
        return MakeCase("UnchangedRowsAndScrollAnchor", callbackQueued && sameControls && anchorStayed ? "PASS" : "FAIL",
            new Dictionary<string, object>
            {
                ["WindowActive"] = dialog.IsActive,
                ["PendingAnchorCallbackWasQueued"] = callbackQueued,
                ["UnchangedRowObjectAndControlsPreserved"] = sameControls,
                ["AnchorWithinViewportBefore"] = true,
                ["AnchorRelativeYBefore"] = Math.Round(yBefore, 2),
                ["AnchorRelativeYAfter"] = Math.Round(yAfter, 2),
                ["ScrollOffsetBeforeRefresh"] = Math.Round(scroll.VerticalOffset, 2),
                ["ScrollAnchorPreservedWithin3Dip"] = anchorStayed
            });
    }

    private static object FindFirstVisibleRow(object view, ScrollViewer scroll)
    {
        var rows = (IDictionary)ReadMember(view, "Rows");
        var orderedRows = rows.Values.Cast<object>().ToArray();
        var items = (Panel)ReadMember(view, "Items");
        foreach (UIElement child in items.Children)
        {
            object row = orderedRows.FirstOrDefault(candidate => ReferenceEquals(ReadMember(candidate, "Surface"), child));
            if (row == null || !((FrameworkElement)child).IsLoaded) continue;
            double y = RelativeY(child, scroll);
            double height = ((FrameworkElement)child).ActualHeight;
            if (height > 0 && y + height > 0 && y < scroll.ViewportHeight) return row;
        }
        return null;
    }

    private static void ConfigureFilter(object view, string value)
    {
        ComboBox filter = (ComboBox)ReadMember(view, "Filter");
        ComboBoxItem match = filter.Items.Cast<ComboBoxItem>().FirstOrDefault(item => String.Equals(Convert.ToString(item.Tag), value, StringComparison.Ordinal));
        if (match == null) throw new MissingMemberException("Required frozen activity filter entry is missing.");
        filter.SelectedItem = match;
    }

    private static object SetWpfFocus(Window window, IInputElement element)
    {
        window.Activate();
        bool accepted = Keyboard.Focus(element) != null;
        if (element is UIElement ui) ui.Focus();
        PumpFor(50);
        return new Dictionary<string, object>
        {
            ["FocusRequestAccepted"] = accepted,
            ["WindowActive"] = window.IsActive,
            ["TargetIsKeyboardFocused"] = element is UIElement target && target.IsKeyboardFocused,
            ["FocusedElementType"] = FocusedType(),
            ["FocusedElementConnectedToWindow"] = FocusConnectedToWindow(window)
        };
    }

    private static object CaptureFocusSnapshot(Window window, FrameworkElement root, object view)
    {
        object filter = ReadMember(view, "Filter");
        var rows = (IDictionary)ReadMember(view, "Rows");
        int focusedRows = rows.Values.Cast<object>().Count(row =>
            ((UIElement)ReadMember(row, "OpenButton")).IsKeyboardFocusWithin ||
            ((UIElement)ReadMember(row, "ReadButton")).IsKeyboardFocusWithin);
        object keyboard = Keyboard.FocusedElement;
        object logical = FocusManager.GetFocusedElement(window);
        return new Dictionary<string, object>
        {
            ["WindowActive"] = window.IsActive,
            ["WindowVisible"] = window.IsVisible,
            ["KeyboardFocusedElementType"] = keyboard == null ? "none" : keyboard.GetType().FullName,
            ["KeyboardFocusConnectedToActivityView"] = keyboard is DependencyObject dependency && IsDescendantOf(dependency, root),
            ["LogicalFocusedElementType"] = logical == null ? "none" : logical.GetType().FullName,
            ["FilterIsKeyboardFocused"] = filter is UIElement filterElement && filterElement.IsKeyboardFocused,
            ["LiveRowsWithKeyboardFocus"] = focusedRows
        };
    }

    private static bool FocusIsFilter(Window window, object view)
    {
        object filter = ReadMember(view, "Filter");
        object keyboard = Keyboard.FocusedElement;
        if (filter is UIElement element && (element.IsKeyboardFocused || element.IsKeyboardFocusWithin)) return true;
        return ReferenceEquals(FocusManager.GetFocusedElement(window), filter) || ReferenceEquals(keyboard, filter);
    }

    private static bool FocusIsWithinLiveRow(object view)
    {
        var rows = (IDictionary)ReadMember(view, "Rows");
        return rows.Values.Cast<object>().Any(row =>
            ((UIElement)ReadMember(row, "OpenButton")).IsKeyboardFocusWithin ||
            ((UIElement)ReadMember(row, "ReadButton")).IsKeyboardFocusWithin);
    }

    private static bool FocusConnectedToWindow(Window window)
    {
        return Keyboard.FocusedElement is DependencyObject dependency && IsDescendantOf(dependency, window);
    }

    private static bool IsDescendantOf(DependencyObject candidate, DependencyObject root)
    {
        DependencyObject current = candidate;
        while (current != null)
        {
            if (ReferenceEquals(current, root)) return true;
            DependencyObject parent = null;
            if (current is Visual || current is Visual3D)
            {
                try { parent = VisualTreeHelper.GetParent(current); } catch { }
            }
            if (parent == null) parent = LogicalTreeHelper.GetParent(current);
            if (parent == null && current is ContentElement content) parent = ContentOperations.GetParent(content);
            current = parent;
        }
        return false;
    }

    private static string FocusedType()
    {
        object element = Keyboard.FocusedElement;
        return element == null ? "none" : element.GetType().FullName;
    }

    private static double RelativeY(UIElement element, ScrollViewer scroll)
    {
        return element.TransformToAncestor(scroll).Transform(new Point(0, 0)).Y;
    }

    private static bool PumpUntil(Func<bool> predicate, int timeoutMs)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!predicate())
        {
            if (watch.ElapsedMilliseconds >= timeoutMs) return false;
            PumpFor(20);
        }
        PumpFor(25);
        return true;
    }

    private static void PumpOperation(DispatcherOperation operation, int timeoutMs)
    {
        if (operation == null) return;
        PumpUntil(() => operation.Status != DispatcherOperationStatus.Pending, timeoutMs);
    }

    private static void PumpFor(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        { Interval = TimeSpan.FromMilliseconds(Math.Max(1, milliseconds)) };
        timer.Tick += (sender, args) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    private static object GetRow(IDictionary rows, string provider, string id)
    {
        string key = provider + "\0" + id;
        if (!rows.Contains(key)) throw new KeyNotFoundException("Synthetic activity row is not in the current filter.");
        return rows[key];
    }

    private static object ReadMember(object target, string name)
    {
        if (target == null) throw new InvalidOperationException("A required WPF fixture member is null.");
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(name, InstanceMembers);
        if (property != null)
        {
            MethodInfo getter = property.GetGetMethod(true);
            if (getter == null) throw new MissingMethodException("Frozen reflection property getter is unavailable.");
            return getter.Invoke(target, new object[0]);
        }
        FieldInfo field = type.GetField(name, InstanceMembers);
        if (field != null) return field.GetValue(target);
        throw new MissingMemberException("Frozen WPF test member is unavailable.");
    }

    private static void SetMember(object target, string name, object value)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(name, InstanceMembers);
        if (property != null)
        {
            MethodInfo setter = property.GetSetMethod(true);
            if (setter != null) { setter.Invoke(target, new[] { value }); return; }
        }
        FieldInfo field = type.GetField(name, InstanceMembers);
        if (field != null) { field.SetValue(target, value); return; }
        throw new MissingMemberException("Frozen fixture setter is unavailable.");
    }

    private static object MakeCase(string name, string status, Dictionary<string, object> details)
    {
        details["Name"] = name;
        details["Status"] = status;
        return details;
    }

    private static void AddCase(Dictionary<string, object> result, string name, bool passed, Dictionary<string, object> details)
    {
        ((List<object>)result["Cases"]).Add(MakeCase(name, passed ? "PASS" : "FAIL", details));
    }

    private static List<object> AppendCase(List<object> cases, object outcome)
    {
        cases.Add(outcome);
        return cases;
    }

    private static void AddNotRunCases(Dictionary<string, object> result, string reason)
    {
        foreach (string name in new[]
        {
            "AttentionFinalUnreadRowReadRestoresFocus",
            "PendingRefreshDoesNotStealFilterFocus",
            "RapidFocusOnAnotherRowIsPreserved",
            "UnchangedRowsAndScrollAnchor",
            "CloseAndDisposeCancelsPendingFocusCallback"
        })
            ((List<object>)result["Cases"]).Add(MakeCase(name, "NOT_RUN", new Dictionary<string, object> { ["Reason"] = reason }));
    }

    private static object ReadResolverRecords(object resolver)
    {
        MethodInfo getRecords = resolver.GetType().GetMethod("GetRecords");
        if (getRecords == null) return new object[0];
        var entries = getRecords.Invoke(resolver, new object[0]) as IEnumerable;
        if (entries == null) return new object[0];
        return entries.Cast<object>().Select(entry => (object)new Dictionary<string, object>
        {
            ["RequestedIdentity"] = Convert.ToString(ReadMember(entry, "RequestedIdentity")),
            ["CandidateSha256"] = Convert.ToString(ReadMember(entry, "CandidateSha256")),
            ["LoadedIdentity"] = Convert.ToString(ReadMember(entry, "LoadedIdentity")),
            ["LoadedSha256"] = Convert.ToString(ReadMember(entry, "LoadedSha256")),
            ["Succeeded"] = Convert.ToBoolean(ReadMember(entry, "Succeeded")),
            ["ErrorType"] = Convert.ToString(ReadMember(entry, "ErrorType")),
            ["HResult"] = Convert.ToInt32(ReadMember(entry, "HResult"))
        }).ToArray();
    }

    private static bool EqualHash(string left, string right)
    { return String.Equals(left, right, StringComparison.OrdinalIgnoreCase); }

    private static string HashFile(string path)
    {
        using (var input = File.OpenRead(path))
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
    }

    private static bool GetBoolean(object value, string name)
    { return value is IDictionary<string, object> map && map.ContainsKey(name) && Convert.ToBoolean(map[name]); }

    private static bool ProcessExists(int id)
    {
        try { return System.Diagnostics.Process.GetProcessById(id).HasExited == false; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}
