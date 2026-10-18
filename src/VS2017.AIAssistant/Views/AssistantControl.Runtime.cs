using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 실제 CLI 연결 정보·실시간 사용량·명시적 전체 액세스 실행을 화면에 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private bool runtimeInitialized;
        private CancellationTokenSource runtimeCancellation;
        private DispatcherTimer runtimeRefreshTimer;
        private RuntimeEditProvider runtimeProvider;
        private ToolTip runtimeToolTip;
        private ProviderRuntimeInfo runtimeInfo;
        private int runtimeGeneration;
        private int runtimeRequestVersion;
        private ProviderUsageSnapshot runtimeUsage;
        private string runtimeMetadataError;
        private long runtimeUsageEventSequence, runtimeAppliedUsageEventSequence;
        private bool agentFullAccess;
        private bool AgentFullAccessEnabled { get { return agentFullAccess; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeRuntime
        /// desc         : 자동 발견을 한 번 연결하고 고립 테스트에서는 외부 CLI 조회만 생략한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InitializeRuntime()
        {
            if (runtimeInitialized) return;
            runtimeInitialized = true;
            try { agentFullAccess = settingsStore.Load().AgentFullAccess; }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is InvalidOperationException || error is UnauthorizedAccessException || error is TimeoutException) { agentFullAccess = false; }
            runtimeToolTip = new ToolTip { Content = "CLI가 실제 보고한 사용량을 연결 후 표시합니다." };
            TokenUsageButton.ToolTip = runtimeToolTip;
            runtimeRefreshTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(650) };
            runtimeRefreshTimer.Tick += RuntimeRefreshTick;
            Loaded += RuntimeLoaded;
            ExecutablePath.TextChanged += RuntimePathChanged;
            RefreshRuntimeTooltip();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AttachRuntimeProvider
        /// desc         : 주입된 실제 Provider만 구독하고 중복 구독은 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AttachRuntimeProvider()
        {
            var next = provider as RuntimeEditProvider;
            if (ReferenceEquals(next, runtimeProvider)) return;
            if (runtimeProvider != null) { runtimeProvider.UsageUpdated -= RuntimeUsageUpdated; runtimeProvider.ProgressUpdated -= RuntimeProgressUpdated; }
            runtimeProvider = next;
            if (runtimeProvider != null) { runtimeProvider.UsageUpdated += RuntimeUsageUpdated; runtimeProvider.ProgressUpdated += RuntimeProgressUpdated; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryDiscoverRuntimePath
        /// desc         : 경로가 비어 있을 때만 설치된 CLI를 찾아 표시하고 명시한 설정은 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 20:25 자동 발견·실제 요청 연결 및 사용자 경로 우선.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void TryDiscoverRuntimePath()
        {
            if (!String.IsNullOrWhiteSpace(ExecutablePath.Text) || Environment.GetEnvironmentVariable("VS2017_AI_DISABLE_RUNTIME_DISCOVERY") == "1") return;
            string found = NativeCliDiscovery.Find(selectedProvider);
            if (found == null) return;
            ExecutablePath.Text = found; executablePaths[selectedProvider] = found;
            ExecutablePath.ToolTip = "설치된 " + selectedProvider + " CLI를 자동으로 찾았습니다. 다른 실행 파일을 직접 지정할 수도 있습니다.\n" + found;
            if (runtimeRefreshTimer != null) runtimeRefreshTimer.Stop();
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RuntimeLoaded
        /// desc         : 실제 제품 로드 뒤 모델·계정 메타데이터를 자동 갱신한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100", Justification = "WPF/Dispatcher event handler; awaited operation handles cancellation and failures.")]
        private async void RuntimeLoaded(object sender, RoutedEventArgs args)
        {
            AttachRuntimeProvider();
            if (Environment.GetEnvironmentVariable("VS2017_AI_DISABLE_RUNTIME_DISCOVERY") == "1") return;
            await RefreshRuntimeAsync();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RuntimePathChanged
        /// desc         : 실행 파일 변경과 제공자 전환을 짧게 모아 재연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RuntimePathChanged(object sender, TextChangedEventArgs args)
        {
            RuntimeProviderChanged();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RuntimeProviderChanged
        /// desc         : 경로가 같은 탭 전환도 이전 계정·사용량을 지우고 최신 연결 조회를 예약한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 느린 이전 연결의 성공·실패가 현재 선택을 덮지 않게 한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RuntimeProviderChanged()
        {
            runtimeGeneration++;
            runtimeRequestVersion++;
            ProviderModelCatalog.PublishDiscovered(selectedProvider, null);
            runtimeInfo = null; runtimeUsage = null; runtimeMetadataError = null; RefreshRuntimeTooltip();
            if (runtimeCancellation != null) { runtimeCancellation.Cancel(); runtimeCancellation = null; }
            if (!IsLoaded || runtimeRefreshTimer == null || Environment.GetEnvironmentVariable("VS2017_AI_DISABLE_RUNTIME_DISCOVERY") == "1") return;
            runtimeRefreshTimer.Stop(); runtimeRefreshTimer.Start();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RuntimeRefreshTick
        /// desc         : 예약된 연결 조회만 실행하며 요청 중인 AI 작업을 중복 시작하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100", Justification = "WPF/Dispatcher event handler; awaited operation handles cancellation and failures.")]
        private async void RuntimeRefreshTick(object sender, EventArgs args)
        { runtimeRefreshTimer.Stop(); if (!busy && !disposed) await RefreshRuntimeAsync(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshRuntimeAsync
        /// desc         : 연결별 결과를 확인하고 이전 제공자의 늦은 응답을 현재 선택에 적용하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private async Task RefreshRuntimeAsync()
        {
            if (disposed) return;
            if (runtimeCancellation != null) { runtimeRefreshTimer.Stop(); runtimeRefreshTimer.Start(); return; }
            TryDiscoverRuntimePath();
            string providerName = selectedProvider, executable = ExecutablePath.Text;
            int generation = runtimeGeneration;
            var cancellation = new CancellationTokenSource(); runtimeCancellation = cancellation;
            try {
                var info = await new ProviderRuntimeService().ReadAsync(providerName, executable, cancellation.Token);
                if (disposed || generation != runtimeGeneration || selectedProvider != providerName || ExecutablePath.Text != executable) return;
                runtimeInfo = info; runtimeMetadataError = null;
                ProviderModelCatalog.PublishDiscovered(providerName, info.Catalog);
                if (runtimeUsage == null) runtimeUsage = info.Usage;
                else if (info.Usage != null) { runtimeUsage.QuotaUsedPercent = info.Usage.QuotaUsedPercent; runtimeUsage.QuotaResetsAt = info.Usage.QuotaResetsAt; }
                RefreshModelChoices(); RefreshRuntimeTooltip();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) when (error is IOException || error is InvalidOperationException || error is ArgumentException || error is System.ComponentModel.Win32Exception) {
                if (!disposed && generation == runtimeGeneration && selectedProvider == providerName && ExecutablePath.Text == executable) { runtimeInfo = null; runtimeMetadataError = "CLI 연결 정보를 조회하지 못했습니다. 실행 파일·버전·로그인을 확인하세요."; RefreshRuntimeTooltip(); }
            }
            finally { cancellation.Dispose(); if (runtimeCancellation == cancellation) runtimeCancellation = null; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RuntimeUsageUpdated
        /// desc         : 백그라운드의 실제 사용량 이벤트를 UI 표시 갱신으로만 예약한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RuntimeUsageUpdated(ProviderUsageSnapshot usage)
        {
            if (disposed || usage == null) return;
            int connection = runtimeGeneration, request = runtimeRequestVersion;
            long sequence = Interlocked.Increment(ref runtimeUsageEventSequence);
            if (!Dispatcher.CheckAccess()) {
#pragma warning disable VSTHRD001 // UI 표시 예약이며 COM 호출·동기 대기·스레드 전환 구현이 아니다.
                var posted = Dispatcher.InvokeAsync(() => { Dispatcher.VerifyAccess(); ApplyRuntimeUsage(usage, connection, request, sequence); }, DispatcherPriority.Input);
#pragma warning restore VSTHRD001
                return;
            }
            ApplyRuntimeUsage(usage, connection, request, sequence);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApplyRuntimeUsage
        /// desc         : 수신 당시의 연결·요청 세대를 확인하여 다른 대화의 늦은 사용량을 버린다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 토큰과 계정 한도 병합.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ApplyRuntimeUsage(ProviderUsageSnapshot usage, int connection, int request, long sequence)
        {
            if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("사용량 표시는 UI 스레드에서만 갱신합니다.");
            if (disposed || usage == null || connection != runtimeGeneration || request != runtimeRequestVersion || sequence <= runtimeAppliedUsageEventSequence) return;
            runtimeAppliedUsageEventSequence = sequence;
            if (runtimeUsage != null) {
                usage.QuotaUsedPercent = runtimeUsage.QuotaUsedPercent; usage.QuotaResetsAt = runtimeUsage.QuotaResetsAt;
                usage.ContextWindow = usage.ContextWindow ?? runtimeUsage.ContextWindow;
                if (!usage.TotalTokens.HasValue) usage.TotalTokens = ProviderRuntimeService.SumKnown(usage.InputTokens, usage.OutputTokens);
            }
            runtimeUsage = usage; RefreshRuntimeTooltip();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BeginRuntimeRequest
        /// desc         : 새 요청의 토큰과 진행 횟수만 초기화하고 현재 계정 한도는 유지한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 요청 간 사용량 혼합 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void BeginRuntimeRequest()
        {
            Dispatcher.VerifyAccess();
            runtimeRequestVersion++; ResetRuntimeProgress();
            runtimeUsage = runtimeInfo == null || runtimeInfo.Usage == null ? null : new ProviderUsageSnapshot { QuotaUsedPercent = runtimeInfo.Usage.QuotaUsedPercent, QuotaResetsAt = runtimeInfo.Usage.QuotaResetsAt, Source = "현재 요청의 토큰 보고 대기" };
            RefreshRuntimeTooltip();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RuntimeProgressUpdated
        /// desc         : 공개 CLI 진행을 수신 순서대로 대화에 표시하고 과도한 기록 증가는 제한한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 중간 상태의 표시·영구 기록.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD010", Justification = "CLI 작업 스레드 콜백은 제한 큐와 Dispatcher 예약만 호출하며 UI 처리는 게시된 delegate에서 검증한다.")]
        private void RuntimeProgressUpdated(AgentProgress progress)
        { QueueRuntimeProgress(progress); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RuntimeTokenUsageHovered
        /// desc         : 열린 같은 툴팁을 갱신하고 오래된 계정 한도는 다시 조회한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RuntimeTokenUsageHovered(object sender, MouseEventArgs args)
        {
            RefreshRuntimeTooltip();
            if (!busy && runtimeInfo != null && DateTimeOffset.Now - runtimeInfo.CheckedAt > TimeSpan.FromSeconds(45) && runtimeRefreshTimer != null) {
                runtimeRefreshTimer.Stop(); runtimeRefreshTimer.Start();
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshRuntimeTooltip
        /// desc         : 요금제·모델 출처·토큰·할당량 단위를 구분하여 사실대로 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshRuntimeTooltip()
        {
            if (runtimeToolTip == null) return;
            PermissionsButton.Content = agentFullAccess ? "⚿ 전체 액세스" : workspacePolicy.AllowSourceControl || workspacePolicy.AllowDatabaseSchema || workspacePolicy.AllowDeployment ? "⚿ 작업 도구 허용" : "⚿ 승인 요청";
            string connection = runtimeInfo == null ? runtimeMetadataError ?? "연결 정보: 아직 조회하지 않음" : "AI: " + runtimeInfo.Provider + "\n계정 종류: " + (String.IsNullOrWhiteSpace(runtimeInfo.Plan) ? "미제공" : runtimeInfo.Plan) + "\n계정 확인: " + runtimeInfo.CheckedAt.ToLocalTime().ToString("HH:mm:ss") + "\n" + runtimeInfo.Status;
            runtimeToolTip.Content = connection + "\n\n" + (runtimeUsage == null ? "사용량: CLI가 아직 제공하지 않음" : runtimeUsage.Describe()) +
                "\n\n실행 모드: " + (agentFullAccess ? "전체 액세스 · CLI가 직접 파일 수정·명령·테스트 수행" : "수정안 검토 · 비교/승인 후 편집기에 적용");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SetAgentFullAccess
        /// desc         : 이미 원자 저장된 권한 모드를 메모리와 툴팁에 적용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SetAgentFullAccess(bool enabled)
        {
            if (busy || disposed) return;
            agentFullAccess = enabled; RefreshRuntimeTooltip();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunFullAccessFromUiAsync
        /// desc         : 전체 액세스 요청의 실제 실행·변경·토큰·중단 상태를 대화에 저장한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        ///                2026-10-18 CodexOrig : 외부 편집기 보호·완료 후 늦은 중간 카드 게시 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD109", Justification = "검증한 WPF Click 경로에서만 호출하며 Dispatcher.VerifyAccess로 같은 UI 계약을 검사한다.")]
        private async Task RunFullAccessFromUiAsync()
        {
            Dispatcher.VerifyAccess();
            ChatSession chat = ActiveChat; ConversationRecord record = activeRecords[selectedProvider];
            AgentFileChange[] changes = new AgentFileChange[0];
            IExternalEditingScope externalEditing = null;
            string progressOutcome = "failed";
            bool requestFinished = false;
            try {
                if (String.IsNullOrWhiteSpace(Instruction.Text)) throw new InvalidOperationException("실행할 작업과 테스트 요청을 입력하세요.");
                var navigation = documents as ISourceNavigationService;
                if (navigation == null) throw new InvalidOperationException("전체 액세스로 작업할 프로젝트 폴더를 열어 주세요.");
                string directory = navigation.GetProjectDirectory();
                var fullAccessDocuments = documents as IFullAccessDocumentService;
                DocumentSnapshot activeSource = fullAccessDocuments != null ? fullAccessDocuments.CaptureFullAccessContext() : documents.GetActiveDocument().Capture();
                if (activeSource != null) {
                    activeSource.RequireValidSelection();
                    if (activeSource.HasUnsavedChanges) throw new InvalidOperationException("CLI 직접 수정 전에 편집 중인 파일을 저장하세요.");
                }
                if (activeSource == null && record.CollaboratorIds.Length > 0) throw new InvalidOperationException("함께 검토할 AI가 지정되어 있습니다. 협업 검토에 사용할 소스를 선택하거나 AI 설정에서 참여자를 해제하세요.");
                MandatoryInstructionStore.Ensure(workspaceStore.RootPath);
                if (File.Exists(workspaceStore.InstructionsPath)) RulesText.Text = workspaceStore.LoadInstructions();
                string editorContext = activeSource == null ? "선택한 편집 파일 없음. 현재 프로젝트 폴더의 사용자 요청을 수행하세요." : "현재 편집 파일: " + activeSource.FilePath + (activeSource.HasSelection ? "\n선택한 코드(" + activeSource.Selection.StartLine + "~" + activeSource.Selection.EndLine + "행):\n" + activeSource.Selection.Text : "");
                var context = new ChatRequestContext(RulesText.Text, ModelName.Text, chat.Messages.ToArray(), chat.Attachments.ToArray(), efforts[selectedProvider], speeds[selectedProvider], editorContext.Length > 8000 ? editorContext.Substring(0, 8000) : editorContext);
                new ProviderExecutionOptions(selectedProvider, context.Model, context.ReasoningEffort, context.Speed);
                ClearProposal(); chat.Add("user", Instruction.Text); FollowConversation(true); busy = true;
                SetConversationState(record, ConversationRunState.Running);
                if (!PersistCurrentConversation()) throw new IOException("대화 기록을 저장하지 못해 실행하지 않았습니다.");
                requestCancellation = new CancellationTokenSource(); BeginRuntimeRequest();
                StatusLabel.Text = "전체 액세스 실행 중 · 파일 수정과 명령·테스트가 실제로 수행됩니다."; UpdateButtons();
                if (record.CollaboratorIds.Length > 0) {
                    pendingDocument = documents.GetActiveDocument();
                    context = await RunCollaboratorsAsync(record, activeSource, Instruction.Text, context, requestCancellation.Token);
                }
                else context = new ChatRequestContext(context.Instructions, context.Model, context.History, context.Attachments, context.ReasoningEffort, context.Speed, collaborationStore.BuildPromptContext(record.Id, record.Provider));
                string combinedContext = editorContext.Substring(0, Math.Min(2000, editorContext.Length)) + "\n" + context.AgentContext;
                if (combinedContext.Length > 8000) combinedContext = combinedContext.Substring(0, 7970) + "\n[편집·협업 문맥 일부 생략]";
                context = new ChatRequestContext(context.Instructions, context.Model, context.History, context.Attachments, context.ReasoningEffort, context.Speed, combinedContext);
                int connection = runtimeGeneration, request = runtimeRequestVersion;
                Action<AgentFileChange[]> reportChanges = files => {
                    if (disposed) return;
                    Volatile.Write(ref changes, files.ToArray());
#pragma warning disable VSTHRD001 // 실제 CLI 변경 이벤트를 UI 카드에 게시하는 예약만 수행한다.
                    var posted = Dispatcher.InvokeAsync(() => { Dispatcher.VerifyAccess(); if (!disposed && !requestFinished && connection == runtimeGeneration && request == runtimeRequestVersion) PresentRuntimeChanges(files, "실행 중 확인된 프로젝트 변경"); }, DispatcherPriority.Input);
#pragma warning restore VSTHRD001
                };
                var synchronizedDocuments = documents as IExternalEditingDocumentService;
                if (synchronizedDocuments != null) externalEditing = synchronizedDocuments.BeginExternalEditing(() => requestCancellation.Cancel());
                AgentExecutionResult result;
                if (selectedProvider == "Codex") result = await new CodexRuntimeRunner().RunAgentAsync(ExecutablePath.Text, directory, Instruction.Text, context, RuntimeUsageUpdated, reportChanges, requestCancellation.Token, RuntimeProgressUpdated);
                else result = await new ClaudeRuntimeRunner().RunAgentAsync(ExecutablePath.Text, directory, Instruction.Text, context, RuntimeUsageUpdated, reportChanges, requestCancellation.Token, RuntimeProgressUpdated);
                if (disposed) return;
                requestFinished = true;
                ApplyRuntimeUsage(result.Usage, connection, request, Interlocked.Increment(ref runtimeUsageEventSequence));
                AddRuntimeFinalReply(result.Text); PresentRuntimeChanges(result.Changes, "전체 액세스 · 실행 중 확인된 변경");
                progressOutcome = "completed";
                SetConversationState(record, ConversationRunState.Completed); Instruction.Clear();
                StatusLabel.Text = "전체 액세스 실행 완료 · CLI가 수행한 실제 결과와 파일 변경을 확인하세요.";
            }
            catch (OperationCanceledException) {
                progressOutcome = "stopped";
                if (!disposed) { SetConversationState(record, ConversationRunState.Stopped); StatusLabel.Text = "실행을 중지했습니다. 이미 수행된 파일 변경과 명령 결과는 남을 수 있습니다."; chat.Add("system", StatusLabel.Text); PresentRuntimeChanges(Volatile.Read(ref changes), "중지 전 보고된 변경"); }
            }
            catch (Exception error) {
                if (!disposed) { SetConversationState(record, ConversationRunState.Failed); StatusLabel.Text = "전체 액세스 실행 실패: " + error.Message + "\n이미 수행된 변경은 직접 확인하세요."; chat.Add("system", StatusLabel.Text); }
            }
            finally {
                requestFinished = true;
                try {
                    if (externalEditing != null) {
                        try { externalEditing.Dispose(); }
                        catch (Exception) { if (!disposed) chat.Add("system", "편집기 동기화 상태를 확인하세요. 미저장 편집은 디스크와 비교 후 저장하세요."); }
                        if (!disposed && !String.IsNullOrWhiteSpace(externalEditing.Notice)) chat.Add("system", externalEditing.Notice);
                    }
                }
                finally {
                    try { if (!disposed && requestCancellation != null) { FlushRuntimeProgress(); CompleteProgressRequest(progressOutcome); } }
                    finally {
                        busy = false;
                        if (requestCancellation != null) { requestCancellation.Dispose(); requestCancellation = null; }
                        if (!disposed) { UpdateButtons(); FollowConversation(false); PersistCurrentConversation(); }
                    }
                }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PresentRuntimeChanges
        /// desc         : CLI가 보고한 diff의 실제 추가·삭제 줄만 세어 실행 카드로 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PresentRuntimeChanges(IEnumerable<AgentFileChange> changes, string summary)
        {
            if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("변경 카드는 UI 스레드에서만 갱신합니다.");
            var files = (changes ?? Enumerable.Empty<AgentFileChange>()).Select(change => {
                int added, removed; bool known = AgentProtocolEvents.CountDiff(change.Diff, out added, out removed) && change.HasLineCounts;
                string evidence = change.Kind == "프로젝트 전후 비교" ? "출처: 제한된 프로젝트 전후 비교. 실행 기간 중 관찰한 변경이며 AI 단독 작성자를 증명하지 않습니다.\n" : "출처: CLI 도구가 보고한 변경.\n";
                return new ChangeCardFile(change.Path, added, removed, evidence + (String.IsNullOrEmpty(change.Diff) ? "CLI가 전후 diff를 제공하지 않았습니다. 보고된 경로와 실제 파일을 확인하세요." : change.Diff), AgentProtocolEvents.FirstChangedLine(change.Diff), known);
            }).ToArray();
            PresentExecutedChanges(files, summary);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DisposeRuntime
        /// desc         : 연결 조회·타이머·Provider 구독을 이 도구 창의 수명에 맞춰 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 연결·사용량·전체 액세스 UI.
        ///                2026-10-18 CodexOrig : 완료 사용량 순서 보존·연결 조회 실패와 실제 토큰 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DisposeRuntime()
        {
            if (runtimeCancellation != null) runtimeCancellation.Cancel();
            if (runtimeRefreshTimer != null) { runtimeRefreshTimer.Stop(); runtimeRefreshTimer.Tick -= RuntimeRefreshTick; runtimeRefreshTimer = null; }
            if (runtimeProvider != null) { runtimeProvider.UsageUpdated -= RuntimeUsageUpdated; runtimeProvider.ProgressUpdated -= RuntimeProgressUpdated; }
            Loaded -= RuntimeLoaded; ExecutablePath.TextChanged -= RuntimePathChanged;
        }
    }
}

