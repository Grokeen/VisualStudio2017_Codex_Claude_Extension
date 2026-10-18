using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ImageFollowupChecks
/// desc         : 이미지17~21의 실제 입력 배치·색상 diff·변경 이력을 고정 제품 DLL에서 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 실제 AI·계정·명령 실행 없이 검토/실행 결과 표시 경계를 검사.
/// </summary>
/// ---------------------------------------------------------------------
public static class ImageFollowupChecks
{
    public static readonly List<string> Checks=new List<string>(), Errors=new List<string>();
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 격리된 문서와 연결 서비스를 주입하고 사용자에게 보이는 배치·상태를 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 원격 조회 차단·가상 결과 주입·폭별 PNG와 실패 보존.
    /// update date  : 2026-10-18 CodexCode : 여러 실행 파일의 두 번째 선택 뒤 첫 파일 왕복·행 재구성 회귀를 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run(string directory)
    {
        AssistantControl control=null;Window owner=null;
        try
        {
            Assert(Environment.GetEnvironmentVariable("VS2017_AI_DISABLE_RUNTIME_DISCOVERY")=="1","Runtime account and model discovery is explicitly disabled for the UI fixture");
            new AssistantSettingsStore().Save(new AssistantSettings{HideWelcome=true,ShowHistory=false});
            string path=Path.Combine(directory,"sample.cs"),before="anchor-start\nvalue-old\nanchor-end",after="anchor-start\nvalue-new\nextra-added\nanchor-end";
            File.WriteAllText(path,before);
            var documents=new FixtureDocuments(path,before);var provider=new NeverRunProvider();
            control=new AssistantControl{HorizontalAlignment=HorizontalAlignment.Left};typeof(AssistantControl).GetField("scmLookupStarted",Private).SetValue(control,true);control.Initialize(documents,provider);
            control.Foreground=Brushes.Gainsboro;control.Background=new SolidColorBrush(Color.FromRgb(23,23,23));
            owner=new Window{Content=control,Width=730,Height=900,ShowInTaskbar=false,Title="Isolated image follow-up review"};owner.Show();Pump();
            var tabs=Named<TabControl>(control,"ProviderChoice");var first=(TabItem)tabs.Items[0];first.ApplyTemplate();
            var tabSurface=(Border)first.Template.FindName("B",first);
            Assert(tabSurface.Margin.Right>=14,"Provider tabs have at least fourteen pixels of explicit separation");
            Assert(Transparent(tabSurface.Background)&&Transparent(Named<Control>(control,"OptionsToggle").Background)&&Transparent(Named<Control>(control,"HistoryToggle").Background),"Tabs, settings menu and history toggle blend into their background");
            Assert(new[]{"AttachButton","PermissionsButton","ModelOptionsButton","TokenUsageButton"}.All(n=>Transparent(Named<Control>(control,n).Background)),"Composer secondary controls use the shared borderless surface appearance");
            Assert(Transparent(Named<TextBox>(control,"Instruction").Background)&&Named<TextBox>(control,"Instruction").BorderThickness==new Thickness(0),"Prompt background and border blend into the enclosing composer surface");
            foreach(int width in new[]{200,320,680})
            {
                control.Width=width;owner.Width=width+45;owner.UpdateLayout();Pump();
                var composer=Named<Border>(control,"ComposerSurface");var start=Named<WrapPanel>(control,"ComposerStartActions");var end=Named<WrapPanel>(control,"ComposerEndActions");
                Assert(Math.Abs(control.ActualWidth-width)<1&&composer.ActualWidth<=width&&new[]{"AttachButton","PermissionsButton","ModelOptionsButton","FastToggle","TokenUsageButton","GenerateButton","StopButton"}.All(n=>Inside(Named<FrameworkElement>(control,n),composer)),"All composer actions stay inside the requested "+width+"px surface");
                Assert(Named<Button>(control,"AttachButton").Parent==start&&Named<Button>(control,"PermissionsButton").Parent==start&&Ordered(start.Children.OfType<FrameworkElement>().ToArray(),composer)&&new[]{"ModelOptionsButton","FastToggle","TokenUsageButton","GenerateButton"}.All(n=>Named<FrameworkElement>(control,n).Parent==end)&&Ordered(end.Children.OfType<FrameworkElement>().ToArray(),composer),"Leading attachment/permission and trailing model/speed/usage/send retain their reading order at "+width+"px");
                Assert(start.TranslatePoint(new Point(),composer).Y<=end.TranslatePoint(new Point(),composer).Y+1&&end.HorizontalAlignment==HorizontalAlignment.Right,"Trailing actions remain on the right or wrap below the leading group at "+width+"px");
                Snapshot(control,Path.Combine(directory,"composer-"+width+".png"));
            }
            control.Width=200;owner.Width=245;owner.UpdateLayout();Pump();
            var secondTab=(TabItem)tabs.Items[1];var firstPosition=first.TranslatePoint(new Point(),tabs);var secondPosition=secondTab.TranslatePoint(new Point(),tabs);
            Assert(Convert.ToString(first.Tag)=="Claude"&&Convert.ToString(secondTab.Tag)=="Codex"&&Math.Abs(firstPosition.Y-secondPosition.Y)<1&&firstPosition.X<secondPosition.X,"At 200px Claude and Codex stay in their original left-to-right order on one tab row");
            Snapshot(control,Path.Combine(directory,"tabs-200.png"));
            double tabFont=tabs.FontSize;tabs.FontSize=28;owner.UpdateLayout();Pump();
            var tabScroll=Descendants<ScrollViewer>(tabs).First();tabScroll.ScrollToRightEnd();Pump();
            firstPosition=first.TranslatePoint(new Point(),tabs);secondPosition=secondTab.TranslatePoint(new Point(),tabs);
            Assert(Math.Abs(firstPosition.Y-secondPosition.Y)<1&&firstPosition.X<secondPosition.X&&tabScroll.ScrollableWidth>0&&secondPosition.X>=-1&&secondPosition.X+secondTab.ActualWidth<=tabs.ActualWidth+1,"Large tab text keeps one-row order and horizontal scrolling reaches Codex without clipping its tab");
            Snapshot(control,Path.Combine(directory,"tabs-200-large.png"));
            tabs.FontSize=tabFont;tabScroll.ScrollToLeftEnd();control.Width=680;owner.Width=725;owner.UpdateLayout();Pump();
            var proposal=Proposal(documents.Capture(),before,after,"FIRST-REVIEW");
            string[] unified=proposal.UnifiedChangePreview.Replace("\r\n","\n").Split('\n');
            Assert(unified.Contains(" anchor-start")&&unified.Contains(" anchor-end")&&!unified.Contains("-anchor-start")&&!unified.Contains("+anchor-end"),"Unchanged replacement anchors remain neutral context in the unified preview");
            Assert(unified.Contains("-value-old")&&unified.Contains("+value-new")&&unified.Contains("+extra-added"),"Unified preview contains the actual removed and added lines");
            Assert(proposal.AddedLines==2&&proposal.RemovedLines==1&&unified.Count(x=>x.StartsWith("+"))==2&&unified.Count(x=>x.StartsWith("-"))==1,"Proposal totals equal the actual signed unified rows");
            Present(control,documents,proposal);var rich=Named<RichTextBox>(control,"ColoredDiffPreview");var paragraphs=rich.Document.Blocks.OfType<Paragraph>().ToArray();
            Assert(paragraphs.Where(p=>Text(p).StartsWith("+")).All(p=>ColorOf(p)=="#FF57CC8A")&&paragraphs.Any(p=>Text(p)=="+value-new"),"Actual added rows are rendered green in the RichTextBox document");
            Assert(paragraphs.Where(p=>Text(p).StartsWith("-")).All(p=>ColorOf(p)=="#FFFF7373")&&paragraphs.Any(p=>Text(p)=="-value-old"),"Actual removed rows are rendered red in the RichTextBox document");
            Assert(paragraphs.Where(p=>Text(p).StartsWith(" ")||Text(p).StartsWith("@@")).All(p=>ColorOf(p)=="#FFD3D3D3"),"Context anchors and hunk headers remain gray rather than change colors");
            Assert(Named<TextBlock>(control,"AddedLinesLabel").Text=="+2"&&Named<TextBlock>(control,"RemovedLinesLabel").Text=="−1"&&Named<TextBlock>(control,"FileAddedLinesLabel").Text=="+2"&&Named<TextBlock>(control,"FileRemovedLinesLabel").Text=="−1","Total and per-file counters show the proposal's actual additions and removals");
            Assert(Named<TextBox>(control,"DiffPreview").Text==proposal.ChangePreview,"Colored rendering preserves the existing DiffPreview.Text contract");
            string history=Convert.ToString(Named<Button>(control,"SourceLink").ToolTip);
            Assert(history.Contains("-value-old")&&history.Contains("+value-new")&&history.Contains("FIRST-REVIEW")&&history.Contains("검토할 수정안")&&history.Contains(DateTimeOffset.Now.ToString("yyyy-MM-dd"))&&Regex.IsMatch(history,@"\d{2}:\d{2}:\d{2}"),"Source hover shows actual before/after snippets, proposal state and local date/time");
            Assert(paragraphs.Where(p=>Text(p).StartsWith("+")||Text(p).StartsWith("-")).All(p=>Convert.ToString(p.ToolTip)==history),"Changed RichTextBox paragraphs expose the same concrete hover history");
            Named<Button>(control,"SourceLink").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(documents.LastPath==path&&documents.LastLine==proposal.FirstChangedLine,"The proposal source link navigates to its actual file and recorded change line");
            Snapshot(control,Path.Combine(directory,"proposal-colored-diff.png"));
            var second=Proposal(documents.Capture(),before,"anchor-start\nvalue-second\nanchor-end","SECOND-REVIEW");Present(control,documents,second);
            string combined=Convert.ToString(Named<Button>(control,"SourceLink").ToolTip);
            Assert(combined.Contains("FIRST-REVIEW")&&combined.Contains("SECOND-REVIEW")&&combined.Contains("+value-second")&&combined.Contains("+value-new"),"A later proposal for the same file retains both real review records");
            Call(control,"UpdateChangeCard");Assert(Regex.Matches(Convert.ToString(Named<Button>(control,"SourceLink").ToolTip),"SECOND-REVIEW").Count==1,"Refreshing the same proposal does not invent duplicate history events");
            var signs=Proposal(new DocumentSnapshot(path,"-- old","fixture","test",false,false),"-- old","++ new","SIGNS-INSIDE-HUNK");Present(control,documents,signs);
            var signRows=rich.Document.Blocks.OfType<Paragraph>().ToArray();
            Assert(signRows.Any(p=>Text(p)=="--- old"&&ColorOf(p)=="#FFFF7373")&&signRows.Any(p=>Text(p)=="+++ new"&&ColorOf(p)=="#FF57CC8A"),"Hunk content beginning with triple signs is still a real red removal and green addition");
            Set(control,"pendingProposal",null);
            string otherPath=Path.Combine(directory,"other.cs"),otherDiff="@@ original 3 @@\n-older\n+newer\n+line2\n+line3";
            Call(control,"PresentExecutedChanges",new[]{new ChangeCardFile(path,2,1,proposal.UnifiedChangePreview,2),new ChangeCardFile(otherPath,3,1,otherDiff,3)},"FIXTURE-VERIFIED-RUN");Pump();
            Assert(Named<TextBlock>(control,"ChangeSummary").Text.Contains("2개")&&Named<TextBlock>(control,"AddedLinesLabel").Text=="+5"&&Named<TextBlock>(control,"RemovedLinesLabel").Text=="−2","Two verified execution records produce two files and combined line totals");
            var reviewTabs=Named<TabControl>(control,"ReviewTabs");
            Assert(Named<CheckBox>(control,"ReviewApproval").Visibility==Visibility.Collapsed&&Named<Button>(control,"ApplyButton").Visibility==Visibility.Collapsed&&Named<Button>(control,"SaveButton").Visibility==Visibility.Collapsed&&((TabItem)reviewTabs.Items[1]).Visibility==Visibility.Collapsed&&((TabItem)reviewTabs.Items[2]).Visibility==Visibility.Collapsed&&reviewTabs.SelectedIndex==0,"Executed changes hide proposal approval/apply/save and original/proposal tabs");
            var rows=Named<StackPanel>(control,"ExecutedFilesPanel").Children.OfType<Button>().ToArray();
            Assert(rows.Length==1,"Additional executed files appear as individually selectable file rows");
            rows[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Assert(Named<TextBox>(control,"DiffPreview").Text==otherDiff&&Named<TextBlock>(control,"FileAddedLinesLabel").Text=="+3"&&Named<TextBlock>(control,"TargetLabel").Text.StartsWith(Path.GetFileName(otherPath),StringComparison.Ordinal)&&Convert.ToString(Named<TextBlock>(control,"TargetLabel").ToolTip).Contains(otherPath),"Selecting an execution row changes the diff and counters while identifying the filename with its full path available");
            Assert(Convert.ToString(Named<Button>(control,"SourceLink").ToolTip).Contains("실제 파일 변경")&&Convert.ToString(Named<Button>(control,"SourceLink").ToolTip).Contains("FIXTURE-VERIFIED-RUN"),"Execution hover identifies the verified execution state and real supplied diff");
            Named<Button>(control,"SourceLink").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(documents.LastPath==otherPath&&documents.LastLine==3,"Selected executed file links navigate to that file's recorded line");
            var returnRows=Named<StackPanel>(control,"ExecutedFilesPanel").Children.OfType<Button>().ToArray();
            bool firstRowAvailable=returnRows.Length==1&&Convert.ToString(returnRows[0].ToolTip).Contains(path)&&!Convert.ToString(returnRows[0].ToolTip).Contains(otherPath),firstSelectionRestored=false,secondSelectionRestored=false;
            if(firstRowAvailable)
            {
                returnRows[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();Named<Button>(control,"SourceLink").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                firstSelectionRestored=Named<TextBox>(control,"DiffPreview").Text==proposal.UnifiedChangePreview&&Named<TextBlock>(control,"FileAddedLinesLabel").Text=="+2"&&Named<TextBlock>(control,"FileRemovedLinesLabel").Text=="−1"&&Convert.ToString(Named<TextBlock>(control,"TargetLabel").ToolTip).Contains(path)&&documents.LastPath==path&&documents.LastLine==2;
                var forwardRows=Named<StackPanel>(control,"ExecutedFilesPanel").Children.OfType<Button>().ToArray();
                if(forwardRows.Length==1&&Convert.ToString(forwardRows[0].ToolTip).Contains(otherPath))
                {
                    forwardRows[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();Named<Button>(control,"SourceLink").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    secondSelectionRestored=Named<TextBox>(control,"DiffPreview").Text==otherDiff&&Named<TextBlock>(control,"FileAddedLinesLabel").Text=="+3"&&Named<TextBlock>(control,"FileRemovedLinesLabel").Text=="−1"&&Convert.ToString(Named<TextBlock>(control,"TargetLabel").ToolTip).Contains(otherPath)&&documents.LastPath==otherPath&&documents.LastLine==3;
                }
            }
            Assert(firstRowAvailable&&firstSelectionRestored&&secondSelectionRestored,"Selecting the second execution file leaves the first file available and round-trips both files' diff, counts and exact source navigation");
            Snapshot(control,Path.Combine(directory,"executed-multiple-files.png"));
            var fiveFiles=Enumerable.Range(1,5).Select(i=>new ChangeCardFile(Path.Combine(directory,"executed-"+i+".cs"),1,1,"@@ original 1 @@\n-old"+i+"\n+new"+i,1)).ToArray();
            Call(control,"PresentExecutedChanges",fiveFiles,"FIVE-VERIFIED-FILES");Pump();
            var more=Named<Button>(control,"ShowMoreFilesButton");
            Assert(Named<TextBlock>(control,"TargetLabel").Text.StartsWith(Path.GetFileName(fiveFiles[0].FilePath),StringComparison.Ordinal)&&Convert.ToString(Named<TextBlock>(control,"TargetLabel").ToolTip).Contains(fiveFiles[0].FilePath)&&Named<StackPanel>(control,"ExecutedFilesPanel").Children.Count==2&&more.IsVisible&&Named<TextBlock>(control,"AddedLinesLabel").Text=="+5"&&Named<TextBlock>(control,"RemovedLinesLabel").Text=="−5","Five executed files initially show three identifiable file entries while retaining full paths and all five files' totals");
            more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();bool allFiveVisible=Named<StackPanel>(control,"ExecutedFilesPanel").Children.Count==4;
            more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Assert(allFiveVisible&&Named<StackPanel>(control,"ExecutedFilesPanel").Children.Count==2&&more.IsVisible,"Show-more expands the remaining executed files and collapses back to three entries");
            var missingCounts=new ChangeCardFile(path,0,0,"미제공",1,false);Call(control,"PresentExecutedChanges",new[]{missingCounts},"COUNTS-NOT-REPORTED");Pump();
            string totalUnknown=Named<TextBlock>(control,"AddedLinesLabel").Text+" "+Named<TextBlock>(control,"RemovedLinesLabel").Text,fileUnknown=Named<TextBlock>(control,"FileAddedLinesLabel").Text+" "+Named<TextBlock>(control,"FileRemovedLinesLabel").Text;
            Assert(totalUnknown.Contains("미제공")&&fileUnknown.Contains("미제공")&&!totalUnknown.Contains("+0")&&!totalUnknown.Contains("−0")&&!fileUnknown.Contains("+0")&&!fileUnknown.Contains("−0"),"Unreported execution line counts remain explicitly unknown rather than invented zeros");
            Call(control,"PresentExecutedChanges",new[]{new ChangeCardFile(path,2,1,proposal.UnifiedChangePreview,2),missingCounts},"PARTIAL-COUNTS");Pump();
            Assert(Named<TextBlock>(control,"AddedLinesLabel").Text.StartsWith("+2",StringComparison.Ordinal)&&Named<TextBlock>(control,"RemovedLinesLabel").Text.StartsWith("−1",StringComparison.Ordinal)&&Named<TextBlock>(control,"AddedLinesLabel").Text.Contains("일부 미제공")&&Named<TextBlock>(control,"RemovedLinesLabel").Text.Contains("일부 미제공"),"Mixed known and unknown line counts retain known totals with an explicit partial-data indication");
            var next=Proposal(documents.Capture(),before,"anchor-start\nvalue-third\nanchor-end","AFTER-EXECUTION");Present(control,documents,next);
            Assert(Named<CheckBox>(control,"ReviewApproval").Visibility==Visibility.Visible&&Named<CheckBox>(control,"ReviewApproval").IsEnabled&&Named<Button>(control,"ApplyButton").Visibility==Visibility.Visible&&Named<Button>(control,"SaveButton").Visibility==Visibility.Visible,"A fresh proposal restores its review approval and apply/save controls");
            Assert(((TabItem)reviewTabs.Items[1]).Visibility==Visibility.Visible&&((TabItem)reviewTabs.Items[2]).Visibility==Visibility.Visible&&Named<StackPanel>(control,"ExecutedFilesPanel").Children.Count==0,"A fresh proposal restores original/proposal tabs and clears execution-only rows");
            var appliedConversation=(ConversationRecord)Named<ListBox>(control,"ConversationList").SelectedItem;
            Named<CheckBox>(control,"ReviewApproval").IsChecked=true;Named<Button>(control,"ApplyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            var session=(SourceEditSession)typeof(AssistantControl).GetField("session",Private).GetValue(control);
            Assert(session.CanRestore&&documents.Capture().Content==next.ProposedText&&(bool)typeof(AssistantControl).GetMethod("CanUsePresentedEditSession",Private).Invoke(control,null)&&Named<TextBlock>(control,"ChangeSummary").Text.Contains("편집기에 적용"),"Actual approved in-memory apply records an applied card owned by this conversation");
            Call(control,"CreateConversation");Pump();
            var otherConversation=(ConversationRecord)Named<ListBox>(control,"ConversationList").SelectedItem;
            var unapplied=Proposal(documents.Capture(),next.ProposedText,"anchor-start\nB-never-applied\nanchor-end","B-UNAPPLIED");Present(control,documents,unapplied);Call(control,"ClearProposal");Call(control,"UpdateButtons");Pump();
            Assert(otherConversation.Id!=appliedConversation.Id&&session.CanRestore&&Named<Border>(control,"ChangeCard").Visibility==Visibility.Collapsed&&!(bool)typeof(AssistantControl).GetMethod("CanUsePresentedEditSession",Private).Invoke(control,null)&&!Named<Button>(control,"RestoreButton").IsEnabled,"An unapplied B proposal cannot borrow A's live restore session or appear as applied");
            Named<ListBox>(control,"ConversationList").SelectedItem=appliedConversation;Pump();Call(control,"UpdateChangeCard");
            Assert(Named<Border>(control,"ChangeCard").Visibility==Visibility.Visible&&Named<TextBox>(control,"OriginalPreview").Text==next.Source.Content&&Named<TextBox>(control,"ProposedPreview").Text==next.ProposedText&&Named<Button>(control,"RestoreButton").IsEnabled,"Returning B to applied A restores A's original/proposed previews and its own restore action");
            Named<Button>(control,"RestoreButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Assert(!session.CanRestore&&documents.Capture().Content==before&&Named<Border>(control,"ChangeCard").Visibility==Visibility.Collapsed,"Actual restore clears applied-card ownership after restoring the synthetic buffer");
            Assert(provider.Calls==0&&documents.Mutations==2&&File.ReadAllText(path)==before,"Only synthetic editor memory was applied/restored; AI, disk writes and actual commands remain unused");
            CheckThinking(control,owner,directory);
        }
        catch(Exception error){Errors.Add(error.ToString());}
        finally{if(control!=null)control.Dispose();if(owner!=null)owner.Close();Pump();}
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CheckThinking
    /// desc         : 실제 텍스트 브러시의 시작·시간별 이동·중복·중지·큰 글자·해제를 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 추론 원문이나 외부 작업 없이 이미지23의 sweep 수명 검증.
    /// update date  : 2026-10-18 CodexCode : 실제 표시 브러시 진단과 중지 후 원래 Foreground 값 출처 복원을 확인.
    ///                2026-10-18 CodexOrig : 큰 글자의 실제 크기·표시·시간 샘플을 실패 근거에 함께 기록.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void CheckThinking(AssistantControl control,Window owner,string directory)
    {
        var indicator=Named<TextBlock>(control,"ThinkingIndicator");string originalText=indicator.Text;double originalSize=indicator.FontSize;var originalFamily=indicator.FontFamily;var originalForeground=indicator.Foreground;var originalLocalForeground=indicator.ReadLocalValue(TextBlock.ForegroundProperty);var originalForegroundSource=DependencyPropertyHelper.GetValueSource(indicator,TextBlock.ForegroundProperty).BaseValueSource;
        Call(control,"UpdateThinkingAnimation",true);
        Assert(indicator.Visibility==Visibility.Visible&&indicator.Text==originalText&&indicator.FontSize==originalSize&&indicator.FontFamily==originalFamily,"Thinking sweep starts without replacing the existing text or font API");
        var brush=indicator.Foreground as LinearGradientBrush;var transform=brush==null?null:brush.RelativeTransform as TranslateTransform;
        string sweepDiagnostics=" [foreground="+(indicator.Foreground==null?"null":indicator.Foreground.GetType().Name)+", transform="+(transform==null?"null":transform.GetType().Name)+", stops="+(brush==null?"none":string.Join("/",brush.GradientStops.Select(stop=>stop.Color.ToString())))+"]";
        Assert(brush!=null&&transform!=null&&brush.MappingMode==BrushMappingMode.RelativeToBoundingBox&&brush.GradientStops.First().Color.R<255&&brush.GradientStops.Last().Color.R<255&&brush.GradientStops.Any(stop=>stop.Color==Colors.White),"Thinking uses a gray-white-gray horizontal relative gradient with a translated highlight"+sweepDiagnostics);
        double firstSample=transform.X;Pump();double secondSample=transform.X;
        Assert(secondSample>firstSample+.02&&transform.HasAnimatedProperties,"Two actual time samples show the white highlight moving from left to right");
        Call(control,"UpdateThinkingAnimation",true);
        Assert(ReferenceEquals(indicator.Foreground,brush)&&ReferenceEquals(brush.RelativeTransform,transform)&&transform.X>=secondSample-.01&&!DependencyPropertyHelper.GetValueSource(indicator,UIElement.OpacityProperty).IsAnimated,"Repeated active updates retain one advancing sweep and do not restart an opacity pulse");
        indicator.FontSize=30;owner.UpdateLayout();double largeSample=transform.X;Pump();
        Assert(indicator.FontSize==30&&indicator.Text==originalText&&brush.MappingMode==BrushMappingMode.RelativeToBoundingBox&&transform.X>largeSample&&indicator.ActualWidth>0,"Large text keeps its content and the same width-independent moving highlight [size="+indicator.FontSize+", width="+indicator.ActualWidth+", visibility="+indicator.Visibility+", before="+largeSample+", after="+transform.X+", sameText="+(indicator.Text==originalText)+"]");
        Pump();Pump();Snapshot(control,Path.Combine(directory,"thinking-sweep-large.png"));
        Call(control,"UpdateThinkingAnimation",false);
        Assert(indicator.Visibility==Visibility.Collapsed&&!transform.HasAnimatedProperties,"Stopping thinking hides the label and detaches the transform animation clock");
        Call(control,"UpdateThinkingAnimation",false);
        Assert(ReferenceEquals(indicator.Foreground,originalForeground)&&ReferenceEquals(indicator.ReadLocalValue(TextBlock.ForegroundProperty),originalLocalForeground)&&DependencyPropertyHelper.GetValueSource(indicator,TextBlock.ForegroundProperty).BaseValueSource==originalForegroundSource&&indicator.Text==originalText&&indicator.FontSize==30&&!transform.HasAnimatedProperties,"Repeated stop is safe and restores the original foreground and value source without changing user font size");
        indicator.FontSize=originalSize;Call(control,"UpdateThinkingAnimation",true);var disposalBrush=(LinearGradientBrush)indicator.Foreground;var disposalTransform=(TranslateTransform)disposalBrush.RelativeTransform;
        control.Dispose();Pump();
        Assert(!disposalTransform.HasAnimatedProperties&&indicator.Visibility==Visibility.Collapsed&&ReferenceEquals(indicator.Foreground,originalForeground),"Disposing an active control removes the sweep clock and restores the original foreground");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Present
    /// desc         : 실제 Generate 완료 경로와 같은 미리보기 계약을 채운 뒤 제품 카드 갱신을 호출한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : AI 실행을 건너뛰되 legacy 텍스트·승인 상태 흐름은 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Present(AssistantControl control,FixtureDocuments documents,EditProposal proposal){Set(control,"pendingDocument",documents);Set(control,"pendingProposal",proposal);Named<TextBox>(control,"OriginalPreview").Text=proposal.Source.Content;Named<TextBox>(control,"ProposedPreview").Text=proposal.ProposedText;Named<TextBox>(control,"DiffPreview").Text=proposal.ChangePreview;Named<CheckBox>(control,"ReviewApproval").IsChecked=false;Named<Expander>(control,"ReviewPanel").IsExpanded=true;Call(control,"UpdateButtons");Call(control,"UpdateChangeCard");control.UpdateLayout();Pump();}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Proposal
    /// desc         : 합성 원문에 실제 EditProposal JSON 검증기를 적용한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 줄 계수·색상 문자열을 테스트에서 재구현하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static EditProposal Proposal(DocumentSnapshot source,string before,string after,string summary){return EditProposal.Parse(source,new JavaScriptSerializer().Serialize(new{summary=summary,edits=new[]{new{old_text=before,new_text=after}}}));}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Named
    /// desc         : 제품 XAML에 실제 존재하는 이름을 타입과 함께 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 누락 컨트롤은 의미 있는 검사 실패로 처리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static T Named<T>(AssistantControl control,string name)where T:class{var result=control.FindName(name)as T;if(result==null)throw new InvalidOperationException("Missing control: "+name);return result;}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Descendants
    /// desc         : 실제 렌더된 템플릿 안에서 탐색 가능한 스크롤 표면을 찾는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 템플릿 부품 이름에 의존하지 않는 큰 글자 접근성 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<T> Descendants<T>(DependencyObject parent)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T)yield return (T)child;foreach(var nested in Descendants<T>(child))yield return nested;}}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Set
    /// desc         : 허가된 UI fixture 상태만 private 필드로 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 제품 저장소·계정 상태를 건드리지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Set(AssistantControl control,string name,object value){typeof(AssistantControl).GetField(name,Private).SetValue(control,value);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Call
    /// desc         : 테스트할 제품 표시 메서드를 직접 호출한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 외부 실행 대신 검증된 표시 진입점만 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Call(AssistantControl control,string name,params object[] values){typeof(AssistantControl).GetMethod(name,Private).Invoke(control,values);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Inside
    /// desc         : 실제 조작 요소가 입력 표면의 수평 경계 안에 보이는지 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 고정 상수가 아닌 렌더된 WPF 위치 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool Inside(FrameworkElement child,FrameworkElement surface){var point=child.TranslatePoint(new Point(),surface);return child.IsVisible&&child.ActualWidth>0&&point.X>=-1&&point.Y>=-1&&point.X+child.ActualWidth<=surface.ActualWidth+1&&point.Y+child.ActualHeight<=surface.ActualHeight+1;}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Ordered
    /// desc         : 줄바꿈을 허용하면서 도구의 좌우·상하 읽기 순서를 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 좁은 폭의 자연스러운 다음 행 배치 허용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool Ordered(FrameworkElement[] elements,FrameworkElement surface){for(int i=1;i<elements.Length;i++){var a=elements[i-1].TranslatePoint(new Point(),surface);var b=elements[i].TranslatePoint(new Point(),surface);bool sameRow=b.Y<a.Y+elements[i-1].ActualHeight-1&&a.Y<b.Y+elements[i].ActualHeight-1;if(sameRow?b.X<a.X+elements[i-1].ActualWidth-1:b.Y<a.Y+elements[i-1].ActualHeight-1)return false;}return true;}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Transparent
    /// desc         : 배경이 투명하여 입력 표면과 일체화되는지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 브러시 색의 알파값 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool Transparent(Brush brush){return brush==null||(brush is SolidColorBrush&&((SolidColorBrush)brush).Color.A==0);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Text
    /// desc         : 실제 FlowDocument 단락의 표시 문자열을 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 텍스트 선택·복사와 같은 문서 범위 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Text(Paragraph paragraph){return new TextRange(paragraph.ContentStart,paragraph.ContentEnd).Text.TrimEnd('\r','\n');}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ColorOf
    /// desc         : 단락 안 실제 Run에 지정된 색을 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 화면 색상 계약을 제품 문자열과 독립 비교.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string ColorOf(Paragraph paragraph){var run=paragraph.Inlines.OfType<Run>().First();return ((SolidColorBrush)run.Foreground).Color.ToString();}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pump
    /// desc         : UI 메시지와 유한 애니메이션을 짧게 처리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 검사 타이머는 매번 해제.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Pump(){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Snapshot
    /// desc         : 실제 고정 제품 컨트롤을 PNG 증거로 저장한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 폭별 입력과 실제 diff 비교를 직접 시각 검토 가능.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Snapshot(FrameworkElement element,string path){element.UpdateLayout();var bounds=new Rect(0,0,element.ActualWidth,element.ActualHeight);var visual=new DrawingVisual();using(var drawing=visual.RenderOpen())drawing.DrawRectangle(new VisualBrush(element){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=bounds,ViewportUnits=BrushMappingMode.Absolute,Viewport=bounds,Stretch=Stretch.Fill},null,bounds);var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),(int)Math.Ceiling(element.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Assert
    /// desc         : 검사 성공을 기록하고 첫 상태 불일치에서 원인을 보존한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 부분 검증을 전체 통과로 표시하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Assert(bool value,string name){if(!value)throw new InvalidOperationException(name);Checks.Add(name);}

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : FixtureDocuments
    /// desc         : 격리된 합성 문서와 소스 이동 기록만 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 IDE 파일·프로세스·인증 상태에 접근하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class FixtureDocuments:IEditorDocument,IActiveDocumentService,ISourceNavigationService
    {
        private readonly string path;private string content;public string LastPath;public int LastLine,Mutations;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FixtureDocuments
        /// desc         : 생성한 합성 원문과 경로를 고정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 경로와 버퍼를 실제 사용자 문서와 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public FixtureDocuments(string path,string content){this.path=path;this.content=content;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Capture
        /// desc         : 실제 합성 파일의 디스크 버전과 원문 스냅샷을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 제품 수정안 검증기를 그대로 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public DocumentSnapshot Capture(){using(var hash=SHA256.Create())return new DocumentSnapshot(path,content,BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))),"isolated fixture",false,false);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetActiveDocument
        /// desc         : 합성 문서만 활성 대상으로 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 편집기 접근 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        public IEditorDocument GetActiveDocument(){return this;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Replace
        /// desc         : 실제 제품 편집 세션의 적용·복구를 합성 메모리에만 반영한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 스냅샷을 검증하고 디스크에는 쓰지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Replace(DocumentSnapshot expected,string value){expected.RequireUnchanged(Capture());Mutations++;content=value;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Save
        /// desc         : 표시 검사에서 저장이 호출되면 즉시 실패한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 저장 경계 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Save(DocumentSnapshot expected){Mutations++;throw new InvalidOperationException("Unexpected source save.");}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigateTo
        /// desc         : 선택한 소스 경로와 줄만 기록한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 IDE 이동 대신 제품 전달값 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void NavigateTo(string filePath,int line){LastPath=filePath;LastLine=line;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetProjectDirectory
        /// desc         : 격리된 합성 프로젝트 폴더만 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : SCM 조회는 별도 플래그로 비활성화.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string GetProjectDirectory(){return Path.GetDirectoryName(path);}
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : NeverRunProvider
    /// desc         : 실제 AI 호출이 발생하면 검사를 실패시키는 제공자다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : UI 결과 주입 검사에서 외부 실행 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class NeverRunProvider:IAiEditProvider
    {
        public int Calls;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GenerateAsync
        /// desc         : 외부 요청을 만들지 않고 잘못된 호출을 즉시 드러낸다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 네트워크·과금·프로세스 실행 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public Task<EditProposal> GenerateAsync(string provider,string executable,DocumentSnapshot source,string instruction,CancellationToken cancellationToken){Calls++;throw new InvalidOperationException("Unexpected AI invocation.");}
    }
}
