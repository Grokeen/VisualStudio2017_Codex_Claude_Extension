using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Views;
using Task = System.Threading.Tasks.Task;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ActivityFeedRuntimeProbe73
/// desc         : 실제 호스트 addon이 상태 소유권·원 조회 결과·실제 피드 표시를 분리 관측하는 준비 모듈이다.
/// author       : CodexOrig 보조 개발 055
/// create date  : 2026-11-03
/// update date  :
/// </summary>
/// ---------------------------------------------------------------------
public static class ActivityFeedRuntimeProbe73
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Capture
    /// desc         : 메시지·제목·초안·상태 원문 없이 실제 선택과 feed typed 상태 및 run 소유권을 기록한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public static object Capture(AssistantControl control, string filterName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var assembly = control.GetType().Assembly;
        var filter = assembly.GetType("VS2017.AIAssistant.Chat.ConversationActivityFilter", true);
        var snapshotMethod = control.GetType().GetMethod("BuildActivityFeedSnapshot", Members);
        if (snapshotMethod == null) throw new MissingMethodException("The frozen product activity snapshot contract is unavailable.");
        object snapshot = snapshotMethod.Invoke(control, new[] { Enum.Parse(filter, filterName, false) });
        var items = ((IEnumerable)Read(snapshot, "VisibleItems")).Cast<object>().ToArray();
        var records = (IDictionary)Read(control, "activeRecords");
        var active = new List<object>();
        foreach (DictionaryEntry pair in records)
        {
            var record = (ConversationRecord)pair.Value;
            active.Add(new { Provider = Convert.ToString(pair.Key), record.Id, RecordProvider = record.Provider, record.IsOpen, record.HasUnread, State = record.State.ToString() });
        }
        return new {
            At = DateTimeOffset.UtcNow.ToString("o"), Filter = filterName,
            SelectedProvider = Convert.ToString(Read(control, "selectedProvider")),
            SelectedTabProvider = Convert.ToString((((TabControl)control.FindName("ProviderChoice")).SelectedItem as TabItem)?.Tag),
            SelectedConversation = SafeRecord(((ListBox)control.FindName("ConversationList")).SelectedItem as ConversationRecord),
            ActiveRecords = active, LiveRoomCount = ((IDictionary)Read(control, "roomRuns")).Count,
            RoomQueueDictionaryCount = ((IDictionary)Read(control, "roomQueues")).Count,
            QueuedRequestCount = ((IDictionary)Read(control, "roomQueues")).Values.Cast<object>().Sum(queue => Convert.ToInt32(Read(queue, "Count"))),
            IsDisposed = Read(control, "disposed"), MainBusy = Read(control, "busy"), Draining = Read(control, "drainingRequests"),
            TotalCount = Read(snapshot, "TotalCount"), UnreadCount = Read(snapshot, "UnreadCount"), FilteredCount = Read(snapshot, "FilteredCount"),
            AttentionCount = Read(snapshot, "AttentionCount"), RunningCount = Read(snapshot, "RunningCount"),
            Rows = items.Select(item => new { Provider = Read(item, "Provider"), Id = Read(item, "Id"), SafeName = Read(item, "SafeName"),
                Kind = Convert.ToString(Read(item, "Kind")), Status = Read(item, "Status"), Summary = Read(item, "Summary"),
                HasUnread = Read(item, "HasUnread"), NeedsAttention = Read(item, "NeedsAttention"), LastActivityAt = Read(item, "LastActivityAt") }).ToArray()
        };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadOpenedRecords
    /// desc         : 실제 assertion이 사용할 한 번의 LoadOpen 결과와 그 조회의 경고를 함께 보존한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public static ConversationRecord[] ReadOpenedRecords(string workspace, string provider, string evidence)
    {
        var store = new MarkdownWorkspaceStore(workspace);
        var records = store.LoadOpen(provider);
        File.WriteAllText(evidence, new JavaScriptSerializer().Serialize(new {
            At = DateTimeOffset.UtcNow.ToString("o"), Provider = provider, Records = records.Select(SafeRecord).ToArray(),
            Warnings = store.LastLoadWarnings, ActualSingleRead = true
        }), new UTF8Encoding(false));
        return records;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ObserveOwnedRequest
    /// desc         : 실제 start 증거의 PID와 제품 room run이 소유한 provider·record 참조를 동시에 검사한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public static object ObserveOwnedRequest(AssistantControl control, ConversationRecord owner, string startEvidence, string exactFixture)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var evidence = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(startEvidence));
        var runs = (IDictionary)Read(control, "roomRuns");
        object run = runs.Contains(owner.Id) ? runs[owner.Id] : null;
        object request = run == null ? null : Read(run, "Request");
        int pid = Convert.ToInt32(evidence["Pid"]);
        bool alive = false, executableMatches = false;
        using (var process = Process.GetProcessById(pid))
        {
            alive = !process.HasExited;
            executableMatches = string.Equals(Path.GetFullPath(process.MainModule.FileName), Path.GetFullPath(exactFixture), StringComparison.OrdinalIgnoreCase);
        }
        bool exactOwner = request != null && ReferenceEquals(Read(request, "Record"), owner)
            && Convert.ToString(Read(request, "Provider")) == owner.Provider && Convert.ToString(evidence["Provider"]) == owner.Provider;
        if (!alive || !executableMatches || !exactOwner || owner.State != ConversationRunState.Running)
            throw new InvalidOperationException("Fixture PID, native request and running conversation must share exact ownership.");
        return new { OwnerProvider = owner.Provider, OwnerId = owner.Id, Pid = pid, FixtureExecutableMatches = executableMatches,
            NativeRequestReferenceMatches = exactOwner, RealProcessAlive = alive, EvidenceRequestId = evidence["RequestId"], ObservedState = owner.State.ToString() };
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : InspectVisibleFeedAsync
    /// desc         : 실제 제품 비모달 singleton을 열어 자기 callback에서 표시를 관찰하고 해당 창만 닫는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public static async Task InspectVisibleFeedAsync(AssistantControl owner, Func<Window, Task> inspect)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var trigger = owner.FindName("ActivityFeedButton") as Button;
        if (trigger == null || !trigger.IsEnabled) throw new InvalidOperationException("The actual product activity trigger must be enabled.");
        trigger.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var dialog = Read(owner, "activityFeedDialog") as Window;
        if (dialog == null || !dialog.IsVisible) throw new InvalidOperationException("The product did not show its owned modeless activity window.");
        dialog.UpdateLayout();
        try { await inspect(dialog); }
        finally
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (dialog.IsVisible) dialog.Close();
        }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RequireNoPrivateText
    /// desc         : 실제 표시 텍스트·tooltip·Automation의 알려진 합성 민감표식 누출을 검사한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void RequireNoPrivateText(DependencyObject root, IEnumerable<string> privateCanaries)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var texts = DisplayTexts(root).ToArray();
        foreach (string canary in privateCanaries.Where(value => !string.IsNullOrEmpty(value)))
            if (texts.Any(text => text.IndexOf(canary, StringComparison.Ordinal) >= 0))
                throw new InvalidOperationException("A known synthetic private marker leaked into feed display, tooltip or accessibility.");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RequireExactRow
    /// desc         : 중복·누락을 Single 예외만으로 숨기지 않고 정확 provider+id 키의 row 한 개를 요구한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public static object RequireExactRow(AssistantControl owner, string provider, string id, string filterName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var type = owner.GetType().Assembly.GetType("VS2017.AIAssistant.Chat.ConversationActivityFilter", true);
        object snapshot = owner.GetType().GetMethod("BuildActivityFeedSnapshot", Members).Invoke(owner, new[] { Enum.Parse(type, filterName) });
        var rows = ((IEnumerable)Read(snapshot, "VisibleItems")).Cast<object>().ToArray();
        var matches = rows.Where(row => Convert.ToString(Read(row, "Provider")) == provider && Convert.ToString(Read(row, "Id")) == id).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Expected exactly one activity row; provider=" + provider + "; id=" + id + "; matched=" + matches.Length + "; rowCount=" + rows.Length);
        return matches[0];
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Read
    /// desc         : 고정 후보의 명명된 읽기 계약만 조회하고 API 부재를 명시 실패로 처리한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public static object Read(object owner, string name)
    {
        if (owner == null) throw new ArgumentNullException("owner");
        var property = owner.GetType().GetProperty(name, Members);
        if (property != null) return property.GetValue(owner, null);
        var field = owner.GetType().GetField(name, Members);
        if (field != null) return field.GetValue(owner);
        throw new MissingMemberException(owner.GetType().FullName, name);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SafeRecord
    /// desc         : 진단에도 대화 원문·자동/수동 제목과 초안을 포함하지 않는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static object SafeRecord(ConversationRecord record)
    { return record == null ? null : new { record.Provider, record.Id, record.IsOpen, record.HasUnread, State = record.State.ToString(), MessageCount = record.Messages.Count, DraftLength = record.Draft.Length }; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : DisplayTexts
    /// desc         : 실제 피드 visual·일반 tooltip·Automation의 노출 문자열만 순회한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<string> DisplayTexts(DependencyObject root)
    {
        if (root == null) yield break;
        var text = root as TextBlock; if (text != null && text.Text != null) yield return text.Text;
        var input = root as TextBox; if (input != null && input.Text != null) yield return input.Text;
        var window = root as Window; if (window != null && window.Title != null) yield return window.Title;
        var content = root as ContentControl; if (content != null && content.Content is string) yield return (string)content.Content;
        foreach (string value in new[] { AutomationProperties.GetName(root), AutomationProperties.GetHelpText(root), AutomationProperties.GetItemStatus(root), AutomationProperties.GetItemType(root) })
            if (!string.IsNullOrEmpty(value)) yield return value;
        var ui = root as UIElement;
        var peer = ui == null ? null : UIElementAutomationPeer.FromElement(ui) ?? UIElementAutomationPeer.CreatePeerForElement(ui);
        if (peer != null)
            foreach (string value in new[] { peer.GetName(), peer.GetHelpText(), peer.GetItemStatus(), peer.GetItemType() })
                if (!string.IsNullOrEmpty(value)) yield return value;
        var element = root as FrameworkElement;
        if (element != null && element.ToolTip is string) yield return (string)element.ToolTip;
        if (element != null && element.ToolTip is DependencyObject)
            foreach (string value in DisplayTexts((DependencyObject)element.ToolTip)) yield return value;
        int count = root is Visual || root is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(root) : 0;
        for (int index = 0; index < count; index++) foreach (string value in DisplayTexts(VisualTreeHelper.GetChild(root, index))) yield return value;
        if (count == 0 && content != null && content.Content is DependencyObject)
            foreach (string value in DisplayTexts((DependencyObject)content.Content)) yield return value;
    }
}
