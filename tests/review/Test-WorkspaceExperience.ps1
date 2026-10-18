#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-WorkspaceExperience
desc         : 합성 문서와 지연 가능한 AI 응답으로 실제 WPF 입력·탐색·표시 흐름을 독립 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 개인 설정·인증·공유 빌드에 접근하지 않는 격리 검증 구성.
-------------------------------------------------------------------------
.SYNOPSIS
    Run Windows PowerShell 5.1 -STA with a frozen product DLL.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [string]$OutputDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw 'Windows PowerShell 5.1 -STA로 실행하세요.'
}
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$assemblySha256 = (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repositoryDir ('artifacts\task012\experience-' + [guid]::NewGuid().ToString('N')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
foreach ($framework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Web.Extensions')) {
    Add-Type -AssemblyName $framework
}
$assetGraph = Get-Content -LiteralPath (Join-Path $repositoryDir 'src\VS2017.AIAssistant\obj\project.assets.json') -Raw | ConvertFrom-Json
$cacheRoot = $assetGraph.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$referencePaths = @{}
foreach ($entry in $assetGraph.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) {
    if ($entry.Value.PSObject.Properties.Name -contains 'compile') {
        foreach ($item in $entry.Value.compile.PSObject.Properties.Name) {
            if ($item -like '*.dll') {
                $referencePaths[[IO.Path]::GetFileNameWithoutExtension($item)] = Join-Path $cacheRoot ($assetGraph.libraries.($entry.Name).path + '/' + $item)
            }
        }
    }
}
$resolveHandler = [ResolveEventHandler]{
    param($sender, $eventArgs)
    $name = (New-Object Reflection.AssemblyName($eventArgs.Name)).Name
    if ($referencePaths.ContainsKey($name)) { return [Reflection.Assembly]::LoadFrom($referencePaths[$name]) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolveHandler)
$previousSettingsPath = $env:VS2017_AI_SETTINGS_PATH
$previousWorkspacePath = $env:VS2017_AI_WORKSPACE_PATH
try {
    $env:VS2017_AI_SETTINGS_PATH = Join-Path $OutputDirectory 'isolated-settings.json'
    $env:VS2017_AI_WORKSPACE_PATH = Join-Path $OutputDirectory ('workspace-' + [guid]::NewGuid().ToString('N'))
    $productAssembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
    $null = [Reflection.Assembly]::LoadFrom($referencePaths['Microsoft.VisualStudio.Shell.15.0'])
    $referenceAssemblies = @($AssemblyPath, 'System.dll', 'System.Core.dll', 'System.Web.Extensions.dll',
        [Windows.DependencyObject].Assembly.Location, [Windows.Media.Visual].Assembly.Location,
        [Windows.Window].Assembly.Location, [System.Xaml.XamlReader].Assembly.Location)
    $experienceSource = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Views;

namespace CodexCode.WorkspaceReview
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ExperienceResult
    /// desc         : 사용자 관점의 검증 결과와 실패 원인을 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 검사 순서와 오류를 JSON 보고서에 전달.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ExperienceResult
    {
        public List<string> Checks = new List<string>();
        public string Error;
        public bool Succeeded { get { return Error == null; } }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SyntheticDocument
    /// desc         : 편집기 메모리와 디스크를 분리한 실제 임시 파일을 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 승인·저장 전 원본 유지 여부 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class SyntheticDocument : IEditorDocument
    {
        public string Path, Buffer;
        public int ReplaceCount, SaveCount;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SyntheticDocument
        /// desc         : 사용자 프로젝트와 무관한 합성 C# 원본을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : UTF8 테스트 원본 초기화.
        /// </summary>
        /// ---------------------------------------------------------------------
        public SyntheticDocument(string path)
        {
            Path = path; Buffer = "// 합성 UX 검증\r\nclass Probe { int Value() { return 1; } }\r\n";
            File.WriteAllText(path, Buffer, new UTF8Encoding(true));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Capture
        /// desc         : 디스크 해시와 현재 메모리 내용을 구분해 고정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 충돌 검사의 실제 파일 근거 제공.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot Capture()
        {
            string hash;
            using (var sha = SHA256.Create()) hash = Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(Path)));
            return new DocumentSnapshot(Path, Buffer, hash, "Synthetic C# / .NET Framework 4.6.2", Buffer != File.ReadAllText(Path), false);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Replace
        /// desc         : 원본 일치를 확인하고 편집기 버퍼에만 수정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 적용 요청 횟수 기록.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Replace(DocumentSnapshot expected, string content) { expected.RequireUnchanged(Capture()); Buffer = content; ReplaceCount++; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Save
        /// desc         : 명시적 저장 호출에서만 임시 원본 디스크에 쓴다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 생성·적용·저장의 경계 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Save(DocumentSnapshot expected) { expected.RequireUnchanged(Capture()); File.WriteAllText(Path, Buffer, new UTF8Encoding(true)); SaveCount++; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SyntheticProvider
    /// desc         : 로그인·네트워크 없이 입력과 컨텍스트를 기록하고 응답 지연을 조절한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 요청 중 재진입 및 요청별 상태 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class SyntheticProvider : IAiContextEditProvider
    {
        public int Calls;
        public string LastProvider, LastInstruction;
        public ChatRequestContext LastContext;
        public bool Hold;
        public TaskCompletionSource<EditProposal> Pending;
        public EditProposal Proposal;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GenerateAsync
        /// desc         : 이전 Provider 계약도 같은 컨텍스트 경로로 기록한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 오래된 계약에서 기본 컨텍스트 구성.
        /// </summary>
        /// ---------------------------------------------------------------------
        public Task<EditProposal> GenerateAsync(string provider, string executable, DocumentSnapshot source, string instruction, CancellationToken token)
        { return GenerateWithContextAsync(provider, executable, source, instruction, new ChatRequestContext(), token); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GenerateWithContextAsync
        /// desc         : 실제 UI가 보내는 문서·질문·모델·첨부를 받아 고정 수정안을 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 취소와 지연을 지원하는 독립 UI 응답 fixture.
        /// </summary>
        /// ---------------------------------------------------------------------
        public Task<EditProposal> GenerateWithContextAsync(string provider, string executable, DocumentSnapshot source, string instruction, ChatRequestContext context, CancellationToken token)
        {
            Calls++; LastProvider = provider; LastInstruction = instruction; LastContext = context;
            string before = source.Content.Contains("return 1;") ? "return 1;" : "return 2;";
            string after = before == "return 1;" ? "return 2;" : "return 3;";
            Proposal = EditProposal.Parse(source, "{\"summary\":\"합성 숫자 수정안\",\"edits\":[{\"old_text\":\"" + before + "\",\"new_text\":\"" + after + "\"}]}");
            if (!Hold) return Task.FromResult(Proposal);
            Pending = new TaskCompletionSource<EditProposal>();
            token.Register(() => Pending.TrySetCanceled());
            return Pending.Task;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SyntheticDocuments
    /// desc         : 합성 소스를 선택하고 IDE 파일 이동 요청의 대상 경로를 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 Visual Studio 제어와 독립된 탐색 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class SyntheticDocuments : IActiveDocumentService, ISourceNavigationService
    {
        public SyntheticDocument Document;
        public string NavigatedPath;
        public int NavigatedLine;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetActiveDocument
        /// desc         : 사용자가 연 합성 문서만 AI 요청 대상으로 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 테스트 전용 문서 선택.
        /// </summary>
        /// ---------------------------------------------------------------------
        public IEditorDocument GetActiveDocument() { return Document; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigateTo
        /// desc         : 변경 카드에서 실제 요청한 소스 경로와 행을 기록한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : URL 형태 소스 링크의 탐색 대상 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void NavigateTo(string filePath, int line) { NavigatedPath = filePath; NavigatedLine = line; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetProjectDirectory
        /// desc         : 사용자 프로젝트 대신 격리된 임시 디렉터리를 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 파일 위치의 개인 환경 의존 제거.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string GetProjectDirectory() { return System.IO.Path.GetDirectoryName(Document.Path); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : WorkspaceExperience
    /// desc         : 실제 WPF 라우팅·렌더·버튼과 합성 문서/응답으로 사용 흐름을 재현한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 제품 인계 후 확정된 UI 동작을 독립 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static partial class WorkspaceExperience
    {
        private static ExperienceResult result;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : 요구한 사용자 결과를 만족하는 검사만 성공 목록에 기록한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실패 시 후속 자동 동작 즉시 중단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); result.Checks.Add(message); Console.WriteLine("PASS " + message); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PumpUntil
        /// desc         : UI 스레드를 막지 않으면서 비동기 상태 전이의 종료를 기다린다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 네트워크 없는 응답에도 Dispatcher 이벤트 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void PumpUntil(Func<bool> condition, int milliseconds)
        {
            if (condition()) return;
            var frame = new DispatcherFrame(); var watch = Stopwatch.StartNew();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            timer.Tick += (sender, args) => { if (condition() || watch.ElapsedMilliseconds >= milliseconds) frame.Continue = false; };
            try { timer.Start(); Dispatcher.PushFrame(frame); }
            finally { timer.Stop(); }
            if (!condition()) throw new TimeoutException("UI 상태 전이가 제한 시간 내 완료되지 않았습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Descendants
        /// desc         : 실제 생성된 시각 요소의 배치와 사용자 표시 내용을 열거한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 동적 DataTemplate 요소도 독립 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            yield return root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Capture
        /// desc         : 현재 제품 UI의 합성 예제 화면을 PNG로 저장한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 계정·프로젝트 정보가 없는 시각 근거 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Capture(FrameworkElement root, string path)
        {
            root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(root.ActualWidth)), Math.Max(1, (int)Math.Ceiling(root.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) encoder.Save(file);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Click
        /// desc         : 연결된 실제 버튼 클릭 라우팅을 실행한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 비활성 버튼의 인위적 실행 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Click(Button button)
        { if (!button.IsEnabled) throw new InvalidOperationException("Button is disabled: " + button.Name); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HandleKey
        /// desc         : 실제 PreviewKeyDown에서 공유하는 수정 키 처리를 운영체제 입력 없이 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Ctrl+Enter 검사를 다른 사용자 창과 격리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool HandleKey(AssistantControl control, Key key, ModifierKeys modifiers)
        {
            var method = typeof(AssistantControl).GetMethod("HandlePromptKey", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException("Shared prompt key handler is missing.");
            return (bool)method.Invoke(control, new object[] { key, modifiers });
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ActivitySeconds
        /// desc         : 화면에 표시된 분·초를 실제 경과 시간과 비교할 수 있게 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 추가 대화 화면의 작업 시간 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static int ActivitySeconds(string text)
        {
            var match = Regex.Match(text ?? "", @"([0-9]+)분 ([0-9]+)초");
            return match.Success ? Int32.Parse(match.Groups[1].Value) * 60 + Int32.Parse(match.Groups[2].Value) : -1;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckMessagePresentation
        /// desc         : 실제 생성된 템플릿에서 사용자 말풍선과 AI 본문을 역할별로 구분한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 오른쪽 둥근 사용자 말풍선·왼쪽 무테 AI 본문과 복사 기능 검사.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckMessagePresentation(AssistantControl control)
        {
            control.UpdateLayout();
            var messages = (ItemsControl)control.FindName("ConversationMessages");
            var bubbles = Descendants(messages).OfType<Border>().Where(x => x.Name == "MessageBubble" && x.DataContext is ChatMessage).ToArray();
            var user = bubbles.Last(x => ((ChatMessage)x.DataContext).Role == "user");
            var assistant = bubbles.Last(x => ((ChatMessage)x.DataContext).Role == "assistant");
            Check(user.HorizontalAlignment == HorizontalAlignment.Right && user.CornerRadius.TopLeft >= 12 &&
                ((SolidColorBrush)user.Background).Color.A > 0 && user.Padding.Left > 0,
                "User messages appear as padded, rounded bubbles aligned to the right");
            Check(assistant.Background is SolidColorBrush && ((SolidColorBrush)assistant.Background).Color.A == 0 &&
                assistant.BorderThickness == new Thickness(0) && assistant.Padding == new Thickness(0) &&
                assistant.TranslatePoint(new Point(0, 0), messages).X < user.TranslatePoint(new Point(0, 0), messages).X,
                "AI replies appear as an unframed left-hand body distinct from the user bubble");
            var userText = Descendants(user).OfType<TextBox>().Single(x => x.Name == "MessageBody");
            var aiText = Descendants(assistant).OfType<TextBox>().Single(x => x.Name == "MessageBody");
            PumpUntil(() => userText.IsLoaded && aiText.IsLoaded, 2000);
            Check(userText.IsReadOnly && aiText.IsReadOnly && userText.ContextMenu != null && aiText.ContextMenu != null &&
                aiText.ContextMenu.Items.OfType<MenuItem>().Any(x => x.Command == ApplicationCommands.Copy),
                "Both message styles preserve selectable read-only text and the actual copy command");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckActivityConversationIsolation
        /// desc         : 새 대화·기존 대화·다른 AI로 전환할 때 작업 시간과 시작 날짜가 섞이지 않는지 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 대화별 완료 시간 복원 및 새 회차 초기화 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckActivityConversationIsolation(AssistantControl control)
        {
            var list = (ListBox)control.FindName("ConversationList");
            var original = (ConversationRecord)list.SelectedItem;
            var activity = (TextBlock)control.FindName("ActivityLabel");
            var started = (TextBlock)control.FindName("ConversationStartedLabel");
            string completed = activity.Text, originalDate = started.Text;
            Click((Button)control.FindName("NewChatButton"));
            var created = (ConversationRecord)list.SelectedItem;
            Check(created.Id != original.Id && !activity.IsVisible && activity.Text.Length == 0 &&
                started.Text.Contains(created.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm")),
                "A new conversation receives its actual creation date and no inherited elapsed-work label");
            list.SelectedItem = original;
            Check(activity.IsVisible && activity.Text == completed && started.Text == originalDate,
                "Returning to a previous conversation restores only that conversation's completed work duration and date");
            var tabs = (TabControl)control.FindName("ProviderChoice");
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(x => Convert.ToString(x.Tag) == "Codex");
            Check(!activity.IsVisible && activity.Text.Length == 0,
                "A different provider conversation does not inherit the previous provider's elapsed-work label");
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(x => Convert.ToString(x.Tag) == "Claude");
            Check(activity.Text == completed && started.Text == originalDate,
                "Returning to the original provider restores its own conversation timing without cross-provider leakage");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckConversationFailureRecovery
        /// desc         : 실제 파일 잠금으로 저장 실패를 만들고 새 대화·AI 전환의 초안 유실을 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 예외 모사 대신 FileShare.None 잠금으로 원자 저장 실패 재현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckConversationFailureRecovery(AssistantControl control)
        {
            var store = new MarkdownWorkspaceStore();
            var list = (ListBox)control.FindName("ConversationList");
            var original = (ConversationRecord)list.SelectedItem;
            var tabs = (TabControl)control.FindName("ProviderChoice");
            var prompt = (TextBox)control.FindName("Instruction");
            int count = list.Items.Count;
            string path = store.GetPath(original), draft = "저장 실패 상황에서도 반드시 보존할 합성 초안";
            prompt.Text = draft;
            using (var locked = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Click((Button)control.FindName("NewChatButton"));
                Check(list.Items.Count == count && list.SelectedItem == original && prompt.Text == draft,
                    "A real Markdown file lock aborts new conversation creation and preserves the active draft");
                tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(x => Convert.ToString(x.Tag) == "Codex");
                Check(Convert.ToString(tabs.SelectedValue) == "Claude" && list.SelectedItem == original && prompt.Text == draft,
                    "A real Markdown save failure aborts provider switching and restores the original visible provider");
            }
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(x => Convert.ToString(x.Tag) == "Codex");
            Check(Convert.ToString(tabs.SelectedValue) == "Codex" && store.LoadOpen("Claude").Single(x => x.Id == original.Id).Draft == draft,
                "Releasing the file lock allows a normal provider switch and persists the previously protected draft");
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(x => Convert.ToString(x.Tag) == "Claude");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckLastConversationCapacityFailure
        /// desc         : 500개 보관 제한에서 마지막 대화를 닫을 때 기존 대화가 유실되지 않는지 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 499개 닫힌 기록과 1개 열린 기록으로 실제 Create 실패 재현.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckLastConversationCapacityFailure(string output)
        {
            string previousRoot = Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH");
            string root = System.IO.Path.Combine(output, "capacity-workspace-" + Guid.NewGuid().ToString("N"));
            var store = new MarkdownWorkspaceStore(root);
            var active = store.Create("Claude");
            for (int i = 0; i < 499; i++)
                store.Save(new ConversationRecord { Id = Guid.NewGuid().ToString("N"), Provider = "Claude", IsOpen = false,
                    CreatedAt = DateTimeOffset.Now, Title = "합성 보관 대화 " + i });
            Window owner = null; AssistantControl control = null;
            try
            {
                Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", root);
                control = new AssistantControl();
                owner = new Window { Title = "Synthetic full conversation capacity review", Content = control, Width = 620, Height = 700, ShowInTaskbar = false };
                owner.Show(); PumpUntil(() => control.IsLoaded, 2000);
                var list = (ListBox)control.FindName("ConversationList");
                var original = (ConversationRecord)list.SelectedItem;
                var prompt = (TextBox)control.FindName("Instruction");
                prompt.Text = "마지막 열린 대화에서 보존할 초안";
                Check(list.Items.Count == 1 && original.Id == active.Id,
                    "At the 500-file capacity only the one open conversation is restored into the visible list");
                var close = list.ContextMenu.Items.OfType<MenuItem>().Single(x => Convert.ToString(x.Header).Contains("닫기"));
                close.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Check(list.Items.Count == 1 && list.SelectedItem == original && prompt.Text == "마지막 열린 대화에서 보존할 초안" &&
                    store.LoadOpen("Claude").Any(x => x.Id == original.Id) && Directory.GetFiles(System.IO.Path.GetDirectoryName(store.GetPath(original)), "*.md").Length == 500,
                    "Failing to reserve a replacement at capacity keeps the last conversation open, selected and intact");
            }
            finally
            {
                if (control != null) control.Dispose();
                if (owner != null) owner.Close();
                Environment.SetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH", previousRoot);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckSidebar
        /// desc         : 실제 WPF 경계 이벤트와 기록 복원으로 폭·숨김·읽지 않음·상태를 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-014 드래그·키보드·복원·구버전 호환 회귀.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckSidebar(AssistantControl control, Window owner, string output)
        {
            owner.Width = 900; owner.UpdateLayout();
            var toggle = (ToggleButton)control.FindName("HistoryToggle");
            toggle.IsChecked = true; toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); owner.UpdateLayout();
            var splitter = (GridSplitter)control.FindName("HistorySplitter");
            var column = (ColumnDefinition)control.FindName("HistoryColumn");
            double before = column.ActualWidth;
            Check(splitter.IsVisible && splitter.Focusable && Grid.GetRowSpan(splitter) == 3,
                "A keyboard-accessible drag boundary spans the conversation, review and composer height");
            splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            splitter.RaiseEvent(new DragDeltaEventArgs(65, 0) { RoutedEvent = Thumb.DragDeltaEvent }); owner.UpdateLayout();
            splitter.RaiseEvent(new DragCompletedEventArgs(65, 0, false) { RoutedEvent = Thumb.DragCompletedEvent }); owner.UpdateLayout();
            double expanded = column.ActualWidth;
            Check(expanded > before + 40 && expanded <= 420,
                "Real GridSplitter drag events expand the conversation column while preserving the message pane");
            var settings = new AssistantSettingsStore().Load();
            Check(Math.Abs(settings.HistoryWidth - expanded) < 2 && settings.ShowHistory,
                "Completing a resize persists the actual sidebar width and visibility");
            splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            splitter.RaiseEvent(new DragDeltaEventArgs(-1000, 0) { RoutedEvent = Thumb.DragDeltaEvent }); owner.UpdateLayout();
            splitter.RaiseEvent(new DragCompletedEventArgs(-1000, 0, false) { RoutedEvent = Thumb.DragCompletedEvent }); owner.UpdateLayout();
            Check(toggle.IsChecked == false && ((FrameworkElement)control.FindName("HistoryPanel")).Visibility == Visibility.Collapsed && column.ActualWidth < 1,
                "Dragging the boundary to the left edge collapses the sidebar and returns its space to the conversation");
            Check(!new AssistantSettingsStore().Load().ShowHistory,
                "The collapsed choice survives a settings reload");
            toggle.IsChecked = true; toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); owner.UpdateLayout();
            Check(Math.Abs(column.ActualWidth - expanded) < 2,
                "Reopening the sidebar restores its last useful width rather than the collapsed width");
            splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            splitter.RaiseEvent(new DragDeltaEventArgs(30, 0) { RoutedEvent = Thumb.DragDeltaEvent }); owner.UpdateLayout();
            splitter.RaiseEvent(new DragCompletedEventArgs(30, 0, true) { RoutedEvent = Thumb.DragCompletedEvent }); owner.UpdateLayout();
            Check(Math.Abs(column.ActualWidth - expanded) < 2,
                "Cancelling a resize restores the previous width without saving a partial drag");
            using (var recreated = new AssistantControl())
            {
                Check(((ToggleButton)recreated.FindName("HistoryToggle")).IsChecked == true && new AssistantSettingsStore().Load().HistoryWidth == settings.HistoryWidth,
                    "A recreated view restores the user's visible sidebar preference and saved width");
            }
            var list = (ListBox)control.FindName("ConversationList");
            var record = (ConversationRecord)list.SelectedItem;
            record.State = ConversationRunState.Completed; record.HasUnread = true; owner.UpdateLayout();
            var indicator = Descendants(list).OfType<TextBlock>().Single(x => x.Name == "ConversationStatus" && x.DataContext == record);
            Check(indicator.Text == "●" && ((SolidColorBrush)indicator.Foreground).Color == (Color)ColorConverter.ConvertFromString("#4A9EFF"),
                "An unread completed response is shown as a live blue dot on its own conversation row");
            ((ScrollViewer)control.FindName("ConversationScroll")).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            owner.UpdateLayout();
            Check(!record.HasUnread && record.State == ConversationRunState.Completed && indicator.Text == "✓",
                "Confirming the current conversation clears its unread dot without losing its completion result");
            var store = new MarkdownWorkspaceStore();
            Check(!store.LoadOpen(record.Provider).Single(x => x.Id == record.Id).HasUnread,
                "Reading a conversation also persists its cleared unread state");
            indicator = Descendants(list).OfType<TextBlock>().Single(x => x.Name == "ConversationStatus" && x.DataContext == record);
            record.State = ConversationRunState.Failed; owner.UpdateLayout();
            Check(indicator.Text == "!" && record.StatusDescription.Contains("실패"),
                "A failed conversation is distinguishable from an unread successful response");
            record.State = ConversationRunState.Running; owner.UpdateLayout();
            Check(indicator.Text == "◌" && indicator.HasAnimatedProperties,
                "The running conversation row animates its real running state");
            record.State = ConversationRunState.Stopped; owner.UpdateLayout();
            PumpUntil(() => !indicator.HasAnimatedProperties, 1500);
            Check(indicator.Text == "■" && !indicator.HasAnimatedProperties,
                "Stopping a conversation releases its row animation and retains a stopped marker");
            string scratch = System.IO.Path.Combine(output, "state-store");
            var isolated = new MarkdownWorkspaceStore(scratch); var saved = isolated.Create("Claude");
            saved.State = ConversationRunState.Completed; saved.HasUnread = true; isolated.Save(saved);
            var loaded = isolated.LoadOpen("Claude").Single();
            Check(loaded.State == ConversationRunState.Completed && loaded.HasUnread,
                "Completed and unread state round-trip through Markdown without losing the response");
            saved.State = ConversationRunState.Running; saved.HasUnread = false; isolated.Save(saved); loaded = isolated.LoadOpen("Claude").Single();
            Check(loaded.State == ConversationRunState.Interrupted && loaded.HasUnread,
                "A request left running by a previous process restores as interrupted and unread, never falsely running");
            string file = isolated.GetPath(saved);
            string legacy = Regex.Replace(File.ReadAllText(file), @"(?m)^(last_status|has_unread):.*\n", "");
            File.WriteAllText(file, legacy, new UTF8Encoding(false)); loaded = isolated.LoadOpen("Claude").Single();
            Check(loaded.State == ConversationRunState.Idle && !loaded.HasUnread,
                "Older conversation Markdown without status metadata remains fully readable");
            saved.State = ConversationRunState.Idle; isolated.Save(saved);
            File.WriteAllText(file, File.ReadAllText(file).Replace("last_status: \"Idle\"", "last_status: \"MadeUp\""), new UTF8Encoding(false));
            Check(isolated.LoadOpen("Claude").Length == 0 && isolated.LastLoadWarnings.Length == 1,
                "Invalid status metadata is reported as a damaged record rather than displayed as a valid result");
            record.State = ConversationRunState.Completed;
            Capture(control, System.IO.Path.Combine(output, "Sidebar-expanded.png"));
            toggle.IsChecked = false; toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); owner.UpdateLayout();
            Capture(control, System.IO.Path.Combine(output, "Sidebar-collapsed.png"));
            toggle.IsChecked = true; toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); owner.UpdateLayout();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Run
        /// desc         : 격리된 실제 창에서 입력·첨부·변경 검토·새 대화 기능을 사용자 순서로 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 입력 이벤트·첨부 제거·소스 이동·승인과 저장의 실제 경계 검증.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 실제 숨김 응답·실패/중지·경계/상태 회귀 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static ExperienceResult Run(string settingsPath, string output)
        {
            result = new ExperienceResult();
            Window owner = null; AssistantControl control = null;
            try
            {
                var app = Application.Current ?? new Application(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                new AssistantSettingsStore(settingsPath).Save(new AssistantSettings { HideWelcome = true });
                var fixtureStore = new MarkdownWorkspaceStore();
                var seededConversation = fixtureStore.Create("Claude");
                seededConversation.CreatedAt = new DateTimeOffset(2024, 2, 3, 4, 5, 0, TimeSpan.FromHours(9));
                fixtureStore.Save(seededConversation);
                var document = new SyntheticDocument(System.IO.Path.Combine(output, "SyntheticProbe.cs"));
                var documents = new SyntheticDocuments { Document = document };
                var provider = new SyntheticProvider();
                control = new AssistantControl(); control.Initialize(documents, provider);
                owner = new Window { Title = "Synthetic workspace experience review", Content = control, Width = 780, Height = 960,
                    ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterScreen };
                owner.Show(); PumpUntil(() => control.IsLoaded && control.ActualWidth > 0, 2000);
                owner.UpdateLayout();
                var tabs = (TabControl)control.FindName("ProviderChoice");
                Check(Convert.ToString(((TabItem)tabs.Items[0]).Tag) == "Claude" && Convert.ToString(tabs.SelectedValue) == "Claude",
                    "Claude is the first and initially selected provider");
                var started = (TextBlock)control.FindName("ConversationStartedLabel");
                var activity = (TextBlock)control.FindName("ActivityLabel");
                Check(started.Text.Contains(seededConversation.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm")) &&
                    started.HorizontalAlignment == HorizontalAlignment.Center,
                    "The centered conversation date comes from the restored record's actual creation timestamp");
                Check(!activity.IsVisible && activity.Text.Length == 0,
                    "A restored conversation has no fabricated work duration before an actual request runs");
                var claudeLogo = (Image)control.FindName("ClaudeTabLogo");
                var codexLogo = (Image)control.FindName("CodexTabLogo");
                var watermark = (Image)control.FindName("BrandWatermark");
                Check(claudeLogo.Source != null && codexLogo.Source != null && !watermark.IsHitTestVisible && watermark.Opacity > 0 && watermark.Opacity < 0.2,
                    "Both provider tabs show real brand images and the background mark stays translucent and non-interactive");
                var claudeMark = watermark.Source;
                tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(x => Convert.ToString(x.Tag) == "Codex"); owner.UpdateLayout();
                Check(watermark.Source != null && watermark.Source != claudeMark,
                    "Switching provider replaces the background logo with the selected brand (selected=" + tabs.SelectedValue +
                    ", state=" + typeof(AssistantControl).GetField("selectedProvider", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control) +
                    ", before=" + claudeMark + ", after=" + watermark.Source + ")");
                tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(x => Convert.ToString(x.Tag) == "Claude"); owner.UpdateLayout();
                Check(!Descendants(tabs).OfType<TextBlock>().Any(x => x.Text == "Claude Code"),
                    "The provider tab is labeled Claude without the obsolete Code suffix");
                Check(!Descendants(control).OfType<TextBlock>().Any(x => x.IsVisible && (x.Text == "Chat" || x.Text == "✎ 요청 입력")),
                    "The provider tabs lead the view without obsolete Chat or request headings");
                Check(!((Button)control.FindName("PasteButton")).IsVisible,
                    "The composer omits the redundant paste button while keyboard paste remains available");
                var brush = control.Background as SolidColorBrush;
                Check(brush != null && brush.Color.R < 70 && brush.Color.G < 70 && brush.Color.B < 70,
                    "The workspace uses a black tonal background");
                var model = (ComboBox)control.FindName("RequestModel");
                var mode = (ComboBox)control.FindName("ModeChoice");
                Check(model.IsEditable && mode.Items.Count >= 3,
                    "The composer allows a model alias and selectable thinking modes");
                model.Text = "synthetic-model";
                mode.SelectedItem = mode.Items.OfType<ComboBoxItem>().Single(x => Convert.ToString(x.Tag) == "high");
                var prompt = (TextBox)control.FindName("Instruction");
                prompt.Text = ""; Click((Button)control.FindName("GenerateButton"));
                Check(((ConversationRecord)((ListBox)control.FindName("ConversationList")).SelectedItem).State == ConversationRunState.Failed && provider.Calls == 0,
                    "A real rejected request records failure without inventing an AI invocation");
                prompt.Text = "한국어 조합 중인 요청";
                var composition = new TextComposition(InputManager.Current, prompt, "가");
                prompt.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
                    { RoutedEvent = TextCompositionManager.PreviewTextInputStartEvent });
                Check(!HandleKey(control, Key.Return, ModifierKeys.None) && provider.Calls == 0,
                    "An Enter key used during Korean IME composition never submits the prompt");
                prompt.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
                    { RoutedEvent = TextCompositionManager.PreviewTextInputEvent });
                prompt.Text = new string('x', prompt.MaxLength); prompt.CaretIndex = prompt.Text.Length;
                HandleKey(control, Key.Return, ModifierKeys.Control);
                Check(prompt.Text.Length == prompt.MaxLength && provider.Calls == 0,
                    "Ctrl+Enter at the prompt limit preserves the existing text without overflow or accidental send");
                prompt.Text = "abXYcd"; prompt.Select(2, 2);
                Check(HandleKey(control, Key.Return, ModifierKeys.Control) && prompt.Text.Replace("\r\n", "\n") == "ab\ncd" && provider.Calls == 0,
                    "Ctrl+Enter replaces the selected prompt range with a newline and never sends");
                Check(!HandleKey(control, Key.A, ModifierKeys.None) && provider.Calls == 0,
                    "Ordinary typing is delegated to the text editor");
                var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
                    new byte[] { 0,0,255,255, 0,0,255,255, 0,0,255,255, 0,0,255,255 }, 8);
                string attachmentPath = System.IO.Path.Combine(output, "SyntheticImage.png");
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(attachmentPath)) encoder.Save(file);
                control.AttachFiles(new[] { attachmentPath }); owner.UpdateLayout();
                var attachments = (ListBox)control.FindName("AttachmentList");
                Check(attachments.Items.Count == 1 && Descendants(attachments).OfType<Image>().Any(x => x.Source != null && x.ActualHeight >= 48),
                    "An attached image is displayed as a visible thumbnail above the prompt");
                var remove = Descendants(attachments).OfType<Button>().Single(x => x.Tag is ChatAttachment);
                Check(remove.Focusable && !String.IsNullOrWhiteSpace(Convert.ToString(remove.ToolTip)),
                    "Attachment deletion has an accessible keyboard target and a clear tooltip");
                Check(remove.Opacity < 0.1 || !remove.IsVisible,
                    "The attachment delete control stays quiet until the image is hovered or focused");
                remove.Focus(); owner.UpdateLayout();
                PumpUntil(() => remove.IsVisible && remove.Opacity > 0.5, 2000);
                Check(remove.IsVisible && remove.Opacity > 0.5,
                    "Keyboard focus reveals the attachment delete action");
                Click(remove);
                Check(attachments.Items.Count == 0 && File.Exists(attachmentPath),
                    "Deleting an attachment removes it from the prompt and preserves its original file");
                control.AttachFiles(new[] { attachmentPath });
                prompt.Text = "숫자를 2로 바꿔줘"; prompt.Focus();
                var enter = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(prompt), Environment.TickCount, Key.Return)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                provider.Hold = true;
                prompt.RaiseEvent(enter);
                PumpUntil(() => provider.Pending != null, 2000);
                owner.Hide(); provider.Pending.SetResult(provider.Proposal);
                PumpUntil(() => provider.Calls == 1 && ((Button)control.FindName("StopButton")).IsEnabled == false, 2000);
                var unseen = (ConversationRecord)((ListBox)control.FindName("ConversationList")).SelectedItem;
                Check(unseen.State == ConversationRunState.Completed && unseen.HasUnread && new MarkdownWorkspaceStore().LoadOpen(unseen.Provider).Single(x => x.Id == unseen.Id).HasUnread,
                    "A real response received while the conversation is hidden becomes unread and is persisted");
                owner.Show(); owner.UpdateLayout(); provider.Hold = false;
                Check(enter.Handled && provider.LastProvider == "Claude" && provider.LastInstruction == "숫자를 2로 바꿔줘",
                    "The routed Enter key sends the current prompt to the selected provider exactly once");
                Check(provider.LastContext.Model == "synthetic-model" && provider.LastContext.Attachments.Length == 1,
                    "The actual request boundary receives the selected model and captured attachment");
                Check(provider.LastContext.ReasoningEffort == "high",
                    "The actual request boundary receives the selected thinking mode");
                Check(((ConversationRecord)((ListBox)control.FindName("ConversationList")).SelectedItem).State == ConversationRunState.Completed,
                    "An actual successful proposal marks only its conversation completed");
                CheckMessagePresentation(control);
                Check(document.ReplaceCount == 0 && document.SaveCount == 0 && File.ReadAllText(document.Path).Contains("return 1;"),
                    "Receiving an AI proposal changes neither editor buffer nor disk before approval");
                var approval = (CheckBox)control.FindName("ReviewApproval");
                Check(approval.IsChecked != true && !((Button)control.FindName("ApplyButton")).IsEnabled,
                    "The compact change card still requires explicit approval before applying");
                string summary = ((TextBlock)control.FindName("ChangeSummary")).Text;
                Check(summary.Contains("+1") && (summary.Contains("−1") || summary.Contains("-1")),
                    "A one-line replacement displays one added and one removed line instead of a misleading zero net change");
                var source = (Button)control.FindName("SourceLink"); Click(source);
                Check(String.Equals(documents.NavigatedPath, document.Path, StringComparison.OrdinalIgnoreCase) && documents.NavigatedLine == 2,
                    "The changed-file link requests the exact proposed source file and its first changed line");
                var options = (System.Windows.Controls.Primitives.ToggleButton)control.FindName("OptionsToggle");
                options.IsChecked = true; options.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); owner.UpdateLayout();
                var fan = (FrameworkElement)control.FindName("FanPanel");
                PumpUntil(() => fan.IsVisible && fan.Opacity > 0.5, 2000);
                Check(((Button)control.FindName("SettingsButton")).IsVisible && ((Button)control.FindName("HelpButton")).IsVisible,
                    "The options trigger reveals reachable settings and usage-guide actions");
                var escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(options), Environment.TickCount, Key.Escape)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                options.RaiseEvent(escape);
                Check(escape.Handled && !fan.IsVisible && options.IsChecked != true,
                    "Escape closes the expanded options menu and resets its toggle state");
                var toast = (FrameworkElement)control.FindName("ToastHost");
                Check(toast.HorizontalAlignment == HorizontalAlignment.Center && toast.VerticalAlignment == VerticalAlignment.Center,
                    "Transient status feedback is centered over the workspace");
                Capture(control, System.IO.Path.Combine(output, "Workspace-780.png"));
                approval.IsChecked = true; Click((Button)control.FindName("ApplyButton"));
                Check(document.Buffer.Contains("return 2;") && File.ReadAllText(document.Path).Contains("return 1;"),
                    "Explicit approval applies the proposal to the editor while preserving the disk until save");
                Click((Button)control.FindName("SaveButton"));
                Check(document.SaveCount == 1 && File.ReadAllText(document.Path).Contains("return 2;"),
                    "The separate save action writes the approved edit exactly once");
                provider.Hold = true; prompt.Text = "지연 응답으로 중복 전송 검사";
                var requestWatch = Stopwatch.StartNew();
                HandleKey(control, Key.Return, ModifierKeys.None);
                PumpUntil(() => provider.Calls == 2 && ((Button)control.FindName("StopButton")).IsEnabled, 2000);
                var runningRecord = (ConversationRecord)((ListBox)control.FindName("ConversationList")).SelectedItem;
                Check(runningRecord.State == ConversationRunState.Running && !runningRecord.HasUnread,
                    "A pending actual request has a running status without an unread completion");
                var thinking = (FrameworkElement)control.FindName("ThinkingIndicator");
                Check(thinking.IsVisible && thinking.HasAnimatedProperties,
                    "A pending AI request shows an animated thinking indicator even while toast feedback may fade");
                Check(activity.IsVisible && activity.Text.Contains("작업 중") && ActivitySeconds(activity.Text) == 0,
                    "Starting a new real request resets its elapsed duration and shows a running status");
                PumpUntil(() => ActivitySeconds(activity.Text) >= 1, 3500);
                Check(Math.Abs(ActivitySeconds(activity.Text) - (int)requestWatch.Elapsed.TotalSeconds) <= 1,
                    "The running duration advances with independently measured wall time rather than fixed demonstration text");
                string busyPrompt = prompt.Text;
                HandleKey(control, Key.Return, ModifierKeys.None); HandleKey(control, Key.Return, ModifierKeys.Control);
                Check(provider.Calls == 2 && prompt.Text == busyPrompt && !((Button)control.FindName("GenerateButton")).IsEnabled,
                    "Enter and Ctrl+Enter cannot mutate or resend the prompt while a request is running");
                Click((Button)control.FindName("StopButton"));
                PumpUntil(() => !((Button)control.FindName("StopButton")).IsEnabled && ((Button)control.FindName("GenerateButton")).IsEnabled, 2000);
                Check(provider.Pending.Task.IsCanceled && document.ReplaceCount == 1 && document.SaveCount == 1,
                    "Stopping a pending request cancels its provider task without additional source edits or saves");
                Check(runningRecord.State == ConversationRunState.Stopped,
                    "Actual request cancellation records stopped rather than successful completion");
                Check(!thinking.IsVisible && !thinking.HasAnimatedProperties,
                    "Stopping the request hides its thinking indicator and releases the repeated animation");
                var activityTimer = (DispatcherTimer)typeof(AssistantControl).GetField("activityTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control);
                var activityClock = (Stopwatch)typeof(AssistantControl).GetField("activityClock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(control);
                Check(!activityTimer.IsEnabled && !activityClock.IsRunning && activity.Text.Contains("작업 소요") &&
                    ActivitySeconds(activity.Text) == (int)activityClock.Elapsed.TotalSeconds,
                    "Completing or stopping a request freezes its actual elapsed duration and stops the update timer");
                PumpUntil(() => toast.Visibility != Visibility.Visible || toast.Opacity < 0.1, 9000);
                Check(toast.Visibility != Visibility.Visible || toast.Opacity < 0.1,
                    "Centered transient feedback fades away after the request stops");
                CheckActivityConversationIsolation(control);
                CheckConversationFailureRecovery(control);
                CheckLastConversationCapacityFailure(output);
                CheckSidebar(control, owner, output);
                foreach (var width in new[] { 420.0, 220.0 })
                {
                    owner.Width = width; owner.UpdateLayout();
                    Capture(control, System.IO.Path.Combine(output, "Workspace-" + width.ToString("0") + ".png"));
                    Check(prompt.ActualWidth > 0 && ((Button)control.FindName("GenerateButton")).ActualWidth > 0,
                        "The prompt and send action retain usable geometry at window width " + width);
                    var claudePosition = claudeLogo.TranslatePoint(new Point(0, 0), control);
                    var codexPosition = codexLogo.TranslatePoint(new Point(0, 0), control);
                    Check(Math.Abs(claudePosition.Y - codexPosition.Y) <= 2 && claudePosition.X < codexPosition.X,
                        "Claude retains the first visible position in a single provider row at window width " + width);
                }
                CheckAdvancedFeatures(output);
                prompt.Text = "폐기 시 작업 타이머 해제 검사";
                HandleKey(control, Key.Return, ModifierKeys.None);
                PumpUntil(() => provider.Calls == 3 && activityTimer.IsEnabled && activityClock.IsRunning, 2000);
                control.Dispose();
                Check(!activityTimer.IsEnabled && !activityClock.IsRunning && provider.Pending.Task.IsCanceled && !thinking.HasAnimatedProperties,
                    "Disposing the view cancels its pending request and releases the elapsed timer, clock and repeated indicator animation");
            }
            catch (Exception error) { result.Error = error.ToString(); }
            finally
            {
                if (control != null) control.Dispose();
                if (owner != null) owner.Close();
            }
            return result;
        }
    }
}
'@
    Add-Type -ReferencedAssemblies $referenceAssemblies -TypeDefinition ($experienceSource + [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'AdvancedExperienceChecks.cs')))
    $experience = [CodexCode.WorkspaceReview.WorkspaceExperience]::Run($env:VS2017_AI_SETTINGS_PATH, $OutputDirectory)
    $report = [pscustomobject]@{
        Succeeded=$experience.Succeeded; CheckKind='IndependentWpfWorkspaceExperience'; Passed=$experience.Checks.Count
        Checks=$experience.Checks; Error=$experience.Error; ProductVersion=$productAssembly.GetName().Version.ToString()
        AssemblySha256=$assemblySha256
        IdeRuntimeTest=$false; AccountLoginAttempted=$false; PersonalSettingsRead=$false
        OutputDirectory=$OutputDirectory; WorkspaceDirectory=$env:VS2017_AI_WORKSPACE_PATH; CheckedAt=(Get-Date).ToString('O')
    }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    $report | ConvertTo-Json -Depth 5
    if (!$experience.Succeeded) { throw "작업 화면 독립 검증 실패: $OutputDirectory" }
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolveHandler)
    $env:VS2017_AI_SETTINGS_PATH = $previousSettingsPath
    $env:VS2017_AI_WORKSPACE_PATH = $previousWorkspacePath
}
