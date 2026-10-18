using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using VS2017.AIAssistant.Editing;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ChangeCardFile
    /// desc         : 검토 수정안 또는 실제 실행에서 확인된 한 파일의 증감·위치·비교 기록을 화면에 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-021 실제 변경 근거와 시각을 가진 공통 카드 계약 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ChangeCardFile
    {
        public string FilePath { get; private set; }
        public int AddedLines { get; private set; }
        public int RemovedLines { get; private set; }
        public string DiffText { get; private set; }
        public int FirstChangedLine { get; private set; }
        public bool HasLineCounts { get; private set; }
        public DateTimeOffset RecordedAt { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChangeCardFile
        /// desc         : 호출자가 실제로 확인한 줄 증감과 unified diff를 보관하며 음수 계수는 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 단일 파일·여러 파일 공통 표시 초기화.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ChangeCardFile(string filePath, int addedLines, int removedLines, string diffText, int firstChangedLine = 1)
            : this(filePath, addedLines, removedLines, diffText, firstChangedLine, true) { }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChangeCardFile
        /// desc         : CLI가 줄 증감을 제공하지 않은 실행은 실제 0줄 변경과 구별해 저장한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 미제공 사용량·변경량을 추정하지 않는 표시 계약.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ChangeCardFile(string filePath, int addedLines, int removedLines, string diffText, int firstChangedLine, bool hasLineCounts)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("변경 파일 경로가 없습니다.");
            if (addedLines < 0 || removedLines < 0) throw new ArgumentOutOfRangeException(nameof(addedLines));
            FilePath = filePath; AddedLines = addedLines; RemovedLines = removedLines;
            DiffText = diffText ?? ""; FirstChangedLine = Math.Max(1, firstChangedLine); RecordedAt = DateTimeOffset.Now; HasLineCounts = hasLineCounts;
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantControl
    /// desc         : 이미지18~21에 맞춘 변경 색상·수정 기록과 반응형 일체형 입력 도구 배치를 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : TASK-021 실제 diff 색상·위치 툴팁·실행 변경 카드 구현.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class AssistantControl
    {
        private EditProposal presentedProposal;
        private string proposalConversationId;
        private EditProposal appliedCardProposal;
        private string appliedCardConversationId;
        private string executedConversationId;
        private ChangeCardFile[] executedFiles = new ChangeCardFile[0];
        private string selectedExecutedFilePath;
        private bool showAllExecutedFiles;
        private readonly Dictionary<string, List<string>> changeHoverHistory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> lastExecutionHistory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ComposerToolbarResized
        /// desc         : 넓은 입력란에서는 좌우 한 줄, 좁은 도킹 창에서는 조작 그룹을 다음 줄로 배치한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 200px 창에서도 버튼을 가리지 않는 입력란 배치.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ComposerToolbarResized(object sender, SizeChangedEventArgs e)
        {
            if (ComposerEndActions == null || ModelOptionsButton == null) return;
            bool narrow = ComposerToolbar.ActualWidth < 470;
            Grid.SetRow(ComposerEndActions, narrow ? 1 : 0); Grid.SetColumn(ComposerEndActions, narrow ? 0 : 1);
            Grid.SetColumnSpan(ComposerEndActions, narrow ? 2 : 1);
            ComposerEndActions.Margin = new Thickness(0, narrow ? 4 : 0, 0, 0);
            ModelOptionsButton.MaxWidth = Math.Max(76, Math.Min(230, ComposerToolbar.ActualWidth - (narrow ? 145 : 220)));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PresentExecutedChanges
        /// desc         : 전체 액세스 실행기가 확인한 실제 여러 파일 변경을 현재 대화의 카드에 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 수정안 승인과 이미 실행된 변경을 구분하는 통합 진입점.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PresentExecutedChanges(IEnumerable<ChangeCardFile> files, string summary)
        {
            if (!Dispatcher.CheckAccess()) throw new InvalidOperationException("변경 카드는 이 화면의 UI 스레드에서 갱신해야 합니다.");
            executedFiles = (files ?? Enumerable.Empty<ChangeCardFile>()).Where(f => f != null).ToArray();
            executedConversationId = CurrentChangeConversationId();
            selectedExecutedFilePath = null;
            showAllExecutedFiles = false;
            foreach (var file in executedFiles) RememberChange(file, "실제 파일 변경 · " + summary, true);
            RefreshChangePresentation();
            if (executedFiles.Length > 0) ReviewPanel.IsExpanded = true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CurrentChangeConversationId
        /// desc         : 초기 화면 로드 전에도 안전하게 현재 제공자의 대화 식별자를 얻는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 대화별 변경 기록 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private string CurrentChangeConversationId()
        {
            return activeRecords.ContainsKey(selectedProvider) ? activeRecords[selectedProvider].Id : "";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RecordProposalApplied
        /// desc         : 실제 편집기 적용이 성공한 경우에만 복구 가능한 수정안과 소유 대화를 기록한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 다른 대화의 미적용 제안을 적용 이력으로 오인하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RecordProposalApplied(EditProposal proposal)
        {
            appliedCardProposal = proposal; appliedCardConversationId = CurrentChangeConversationId();
            if (proposal != null) RememberChange(ProposalCardFile(proposal), "편집기에 적용됨 · " + proposal.Summary);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RecordProposalRestored
        /// desc         : 실제 복구가 성공한 뒤 적용 카드 소유와 내용을 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 복구 실패 때는 기존 적용 기록을 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RecordProposalRestored()
        { appliedCardProposal = null; appliedCardConversationId = null; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CanUsePresentedEditSession
        /// desc         : 현재 카드와 같은 대화의 실제 적용에만 저장·복구 단추를 허용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 여러 대화·전체 액세스 간 이전 문서 복구 혼동 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool CanUsePresentedEditSession()
        {
            string current = CurrentChangeConversationId();
            return appliedCardProposal != null && appliedCardConversationId == current &&
                (pendingProposal == null || !pendingProposal.HasChanges) && !(executedConversationId == current && executedFiles.Length > 0);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshChangePresentation
        /// desc         : 수정안·직전 적용·실제 실행을 구분하고 파일별 증감과 색상 비교를 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 추가 초록·제거 빨강 및 hover 변경 기록 반영.
        /// update date  : 2026-10-18 CodexCode : 선택 파일을 제외한 나머지 행을 구성해 첫 파일로도 다시 돌아갈 수 있게 함.
        ///                2026-10-19 CodexCode : TASK024 대화·파일 카드 재배치 전 이전 hover 비교를 닫음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshChangePresentation()
        {
            if (ChangeCard == null) return;
            string conversation = CurrentChangeConversationId();
            bool pending = pendingProposal != null && pendingProposal.HasChanges;
            if (pending && !ReferenceEquals(presentedProposal, pendingProposal))
            {
                presentedProposal = pendingProposal; proposalConversationId = conversation;
                executedFiles = new ChangeCardFile[0];
                RememberChange(ProposalCardFile(presentedProposal), "검토할 수정안 · " + selectedProvider + " · " + presentedProposal.Summary);
            }
            bool pendingVisible = pending && proposalConversationId == conversation;
            bool appliedVisible = appliedCardProposal != null && appliedCardConversationId == conversation && session.CanRestore;
            bool proposalVisible = pendingVisible || appliedVisible;
            bool actualVisible = executedConversationId == conversation && executedFiles.Length > 0 && !pending;
            ChangeCard.Visibility = proposalVisible || actualVisible ? Visibility.Visible : Visibility.Collapsed;
            ChangeDiffPopup.CloseWithin(ChangeCard);
            if (!proposalVisible && !actualVisible) { ColoredDiffPreview.Document.Blocks.Clear(); return; }
            ChangeCardFile[] files = actualVisible ? executedFiles : new[] { ProposalCardFile(pendingVisible ? pendingProposal : appliedCardProposal) };
            if (!actualVisible)
            {
                var displayed = pendingVisible ? pendingProposal : appliedCardProposal;
                OriginalPreview.Text = displayed.Source.Content; ProposedPreview.Text = displayed.ProposedText; DiffPreview.Text = displayed.ChangePreview;
            }
            ChangeSummary.Text = (actualVisible ? "실행으로 변경된 파일 " : pendingVisible ? "수정안 파일 " : "편집기에 적용한 파일 ") + files.Length + "개";
            bool countsComplete = files.All(f => f.HasLineCounts), anyKnown = files.Any(f => f.HasLineCounts);
            AddedLinesLabel.Text = anyKnown ? "+" + files.Where(f => f.HasLineCounts).Sum(f => (long)f.AddedLines) + (countsComplete ? "" : " · 일부 미제공") : "추가 줄 수 미제공";
            RemovedLinesLabel.Text = anyKnown ? "−" + files.Where(f => f.HasLineCounts).Sum(f => (long)f.RemovedLines) + (countsComplete ? "" : " · 일부 미제공") : "제거 줄 수 미제공";
            AddedLinesLabel.SetResourceReference(TextBlock.ForegroundProperty, anyKnown ? "AssistantAddedBrush" : "AssistantMutedForegroundBrush"); RemovedLinesLabel.SetResourceReference(TextBlock.ForegroundProperty, anyKnown ? "AssistantRemovedBrush" : "AssistantMutedForegroundBrush");
            ExecutedFilesPanel.Children.Clear();
            var selectedFile = actualVisible ? files.FirstOrDefault(f => String.Equals(f.FilePath, selectedExecutedFilePath, StringComparison.OrdinalIgnoreCase)) ?? files[0] : files[0];
            ShowChangeFile(selectedFile, actualVisible);
            if (actualVisible)
            {
                selectedExecutedFilePath = selectedFile.FilePath;
                foreach (var file in files.Where(f => !ReferenceEquals(f, selectedFile)).Take(showAllExecutedFiles ? 199 : 2)) AddExecutedFileRow(file);
            }
            ShowMoreFilesButton.Visibility = actualVisible && files.Length > 3 ? Visibility.Visible : Visibility.Collapsed;
            ShowMoreFilesButton.Content = showAllExecutedFiles ? "파일 목록 접기 ⌃" : files.Length > 200 ? "파일 더 보기 ⌄ (총 " + files.Length + "개 · 미리보기 최대 200개)" : (files.Length - 3) + "개 파일 더 보기 ⌄";
            ShowMoreFilesButton.ToolTip = files.Length > 200 ? "계수는 모든 파일의 합계입니다. 화면은 처음 200개 파일까지 표시하며 전체는 실행 기록에서 확인하세요." : "파일별 수정 기록과 색상 비교를 펼치거나 접습니다.";
            SourceLink.IsEnabled = documents is ISourceNavigationService;
            RestoreButton.IsEnabled = !busy && CanUsePresentedEditSession() && session.CanRestore;
            SaveButton.IsEnabled = !busy && CanUsePresentedEditSession() && session.CanSave;
            // 실제 실행은 별도 proposal이 아니므로 승인/원본 탭으로 잘못 적용할 수 없게 구별한다.
            ReviewApproval.Visibility = actualVisible ? Visibility.Collapsed : Visibility.Visible;
            ApplyButton.Visibility = SaveButton.Visibility = actualVisible ? Visibility.Collapsed : Visibility.Visible;
            ((TabItem)ReviewTabs.Items[1]).Visibility = ((TabItem)ReviewTabs.Items[2]).Visibility = actualVisible ? Visibility.Collapsed : Visibility.Visible;
            if (actualVisible) ReviewTabs.SelectedIndex = 0;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShowMoreChangedFilesClicked
        /// desc         : 여러 파일의 처음 세 행을 유지하고 나머지 실제 파일을 제한 높이 안에서 펼친다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 이미지18·19의 파일 더 보기와 입력란 공간 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ShowMoreChangedFilesClicked(object sender, RoutedEventArgs e)
        { showAllExecutedFiles = !showAllExecutedFiles; RefreshChangePresentation(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProposalCardFile
        /// desc         : 승인 대기 중인 불변 수정안에서 직접 계수와 비교 문자열을 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 추정 계수나 가짜 변경 기록 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static ChangeCardFile ProposalCardFile(EditProposal proposal)
        {
            return new ChangeCardFile(proposal.Source.FilePath, proposal.AddedLines, proposal.RemovedLines, proposal.UnifiedChangePreview, proposal.FirstChangedLine);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShowChangeFile
        /// desc         : 선택된 변경 파일의 링크·줄 증감·최근 기록 및 색상 diff를 함께 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 카드 행 선택과 실제 소스 줄 이동 연결.
        ///                2026-10-19 CodexCode : TASK024 장문 Tooltip을 스크롤 가능한 비교 Dropdown으로 대체.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ShowChangeFile(ChangeCardFile file, bool executed)
        {
            reviewFilePath = file.FilePath; reviewLine = file.FirstChangedLine; TargetLabel.Text = VisibleChangePath(file.FilePath);
            string history = ChangeHistoryText(file);
            string hint = file.FilePath + "\n변경 비교: 마우스를 올리거나 Alt+아래 · 클릭은 소스 이동";
            SourceLink.ToolTip = hint; TargetLabel.ToolTip = hint;
            ChangeDiffPopup.Attach(SourceLink, file, history); ToolTipService.SetIsEnabled(TargetLabel, false);
            FileAddedLinesLabel.Text = file.HasLineCounts ? "+" + file.AddedLines : "줄 수"; FileRemovedLinesLabel.Text = file.HasLineCounts ? "−" + file.RemovedLines : "미제공";
            FileAddedLinesLabel.SetResourceReference(TextBlock.ForegroundProperty, file.HasLineCounts ? "AssistantAddedBrush" : "AssistantMutedForegroundBrush"); FileRemovedLinesLabel.SetResourceReference(TextBlock.ForegroundProperty, file.HasLineCounts ? "AssistantRemovedBrush" : "AssistantMutedForegroundBrush");
            FileAddedLinesLabel.ToolTip = FileRemovedLinesLabel.ToolTip = "실제 변경 줄 수 · 파일에 마우스를 올려 비교 보기";
            ChangeDiffPopup.Attach(ColoredDiffPreview, file, history);
            RenderColoredDiff(file.DiffText, history);
            if (executed) DiffPreview.Text = file.DiffText;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddExecutedFileRow
        /// desc         : 실제 여러 파일 변경에서 개별 파일의 색상 계수·기록·비교 선택을 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 이미지18·19 파일별 행 구현.
        /// update date  : 2026-10-18 CodexCode : 파일 선택 시 다른 행을 재구성해 현재 파일 중복과 첫 파일 접근 누락 방지.
        ///                2026-10-19 CodexCode : TASK024 클릭 선택은 보존하고 hover는 유한 비교 popup으로 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AddExecutedFileRow(ChangeCardFile file)
        {
            var row = new DockPanel();
            var counts = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            if (file.HasLineCounts)
            {
                var added = new Run("+" + file.AddedLines + " "); added.SetResourceReference(TextElement.ForegroundProperty, "AssistantAddedBrush"); counts.Inlines.Add(added);
                var removed = new Run("−" + file.RemovedLines); removed.SetResourceReference(TextElement.ForegroundProperty, "AssistantRemovedBrush"); counts.Inlines.Add(removed);
            }
            else { var unknown = new Run("줄 수 미제공"); unknown.SetResourceReference(TextElement.ForegroundProperty, "AssistantMutedForegroundBrush"); counts.Inlines.Add(unknown); }
            DockPanel.SetDock(counts, Dock.Right); row.Children.Add(counts);
            row.Children.Add(new TextBlock { Text = VisibleChangePath(file.FilePath), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            var button = new Button { Content = row, HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = file.FilePath + "\n마우스: 변경 비교 · 클릭: 파일 선택", Style = (Style)FindResource("SurfaceButton") };
            ChangeDiffPopup.Attach(button, file, ChangeHistoryText(file));
            button.Click += (sender, args) => { selectedExecutedFilePath = file.FilePath; RefreshChangePresentation(); ReviewPanel.IsExpanded = true; ReviewTabs.SelectedIndex = 0; };
            ExecutedFilesPanel.Children.Add(button);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : VisibleChangePath
        /// desc         : 긴 절대 경로가 실제 파일명을 가리지 않도록 파일명을 먼저 표시하고 전체 경로는 툴팁에 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 좁은 변경 카드에서도 대상 파일 식별 가능.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string VisibleChangePath(string path)
        {
            string name = System.IO.Path.GetFileName(path), directory = System.IO.Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(name) || string.IsNullOrEmpty(directory) ? path : name + " · " + directory;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RememberChange
        /// desc         : 현재 창에서 발생한 파일별 최근 세 변경의 시각·상태·실제 전후 조각을 제한된 메모리에 보관한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 hover 기록을 대화별 최대100파일·각3건으로 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RememberChange(ChangeCardFile file, string description, bool suppressRepeatedExecution = false)
        {
            string key = CurrentChangeConversationId() + "|" + file.FilePath;
            string fingerprint = null;
            if (suppressRepeatedExecution)
                using (var hash = SHA256.Create()) fingerprint = description + "\n" + file.HasLineCounts + ":" + file.AddedLines + ":" + file.RemovedLines + "\n" + Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(file.DiffText)));
            string previous;
            if (suppressRepeatedExecution && lastExecutionHistory.TryGetValue(key, out previous) && previous == fingerprint) return;
            List<string> records;
            if (!changeHoverHistory.TryGetValue(key, out records))
            {
                if (changeHoverHistory.Count >= 100)
                { string oldest = changeHoverHistory.Keys.First(); changeHoverHistory.Remove(oldest); lastExecutionHistory.Remove(oldest); }
                changeHoverHistory[key] = records = new List<string>();
            }
            if (suppressRepeatedExecution) lastExecutionHistory[key] = fingerprint;
            string excerpt = file.DiffText.Length > 1800 ? file.DiffText.Substring(0, 1800) + "\n… 전체 내용은 변경 비교에서 확인" : file.DiffText;
            records.Insert(0, file.RecordedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + " · " + description + "\n" + (file.HasLineCounts ? "+" + file.AddedLines + " −" + file.RemovedLines : "줄 수 미제공") + "\n" + excerpt);
            if (records.Count > 3) records.RemoveAt(records.Count - 1);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChangeHistoryText
        /// desc         : 원본 경로와 현재 창의 최근 변경을 조회해 재시작 후 보존을 오인하지 않는 툴팁을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 기록의 범위와 상세 비교 진입을 명시.
        /// </summary>
        /// ---------------------------------------------------------------------
        private string ChangeHistoryText(ChangeCardFile file)
        {
            List<string> records;
            return file.FilePath + "\n이 창에서 기록한 최근 변경 · 파일 링크를 누르면 소스로 이동\n\n" +
                (changeHoverHistory.TryGetValue(CurrentChangeConversationId() + "|" + file.FilePath, out records) ? string.Join("\n\n", records) : file.DiffText);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RenderColoredDiff
        /// desc         : unified diff의 실제 추가·삭제 부호를 각각 초록·빨강으로 표시하고 줄마다 기록을 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : TASK-021 텍스트 선택·복사와 긴 응답의 표시량 제한 유지.
        ///                2026-10-19 CodexCode : TASK024 장문 중복 Tooltip 대신 공통 비교 Popup으로 상세 제공.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RenderColoredDiff(string diff, string history)
        {
            var document = new FlowDocument { PagePadding = new Thickness(0), FontFamily = new FontFamily("Consolas"), FontSize = 12 };
            document.SetResourceReference(TextElement.ForegroundProperty, "AssistantForegroundBrush"); document.SetResourceReference(TextElement.BackgroundProperty, "AssistantCodeBackgroundBrush");
            int characters = 0, count = 0; bool inHunk = false;
            foreach (string line in (diff ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                if (count++ >= 1000 || characters >= 60000)
                {
                    document.Blocks.Add(new Paragraph(new Run("… 표시 한도에 도달했습니다. 원본·수정안 또는 실행 기록에서 전체 내용을 확인하세요.")) { Margin = new Thickness(0, 6, 0, 0) }); break;
                }
                string shown = line.Length > 2000 ? line.Substring(0, 2000) + "…" : line; characters += shown.Length;
                if (line.StartsWith("diff --git ", StringComparison.Ordinal)) inHunk = false;
                bool fileHeader = !inHunk && (line.StartsWith("+++ ", StringComparison.Ordinal) || line.StartsWith("--- ", StringComparison.Ordinal));
                if (line.StartsWith("@@", StringComparison.Ordinal)) inHunk = true;
                bool added = line.StartsWith("+", StringComparison.Ordinal) && !fileHeader;
                bool removed = line.StartsWith("-", StringComparison.Ordinal) && !fileHeader;
                var text = new Run(shown); text.SetResourceReference(TextElement.ForegroundProperty, added ? "AssistantAddedBrush" : removed ? "AssistantRemovedBrush" : "AssistantForegroundBrush");
                var paragraph = new Paragraph(text) { Margin = new Thickness(0), LineHeight = 18, ToolTip = "마우스를 올려 변경 비교 · Alt+아래로 고정" };
                ToolTipService.SetIsEnabled(paragraph, false);
                if (added || removed) paragraph.Background = new SolidColorBrush(added ? Color.FromArgb(28, 87, 204, 138) : Color.FromArgb(28, 255, 115, 115));
                document.Blocks.Add(paragraph);
            }
            ColoredDiffPreview.Document = document;
        }
    }
}
