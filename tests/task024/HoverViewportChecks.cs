using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : HoverViewportChecks
/// desc         : 창·native 포인터 없이 실제 소유 source의 색상 diff·중첩 휠·목록 overlay를 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-19
/// update date  : 2026-10-19 CodexCode : source fixture와 실제 제품 DLL 모드를 결과에 구분.
/// </summary>
/// ---------------------------------------------------------------------
public static class HoverViewportChecks
{
    public static readonly List<string> Checks = new List<string>(), Errors = new List<string>();
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    /// <summary>
    /// name         : Run
    /// desc         : 의미 있는 데이터·경계·수명 회귀를 각각 독립 실행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 공개 popup을 실제로 열거나 계정에 연결하지 않음.
    /// </summary>
    public static void Run()
    {
        Diff(); Wheel(); Overlay(); Palette();
    }
    /// <summary>
    /// name         : Diff
    /// desc         : 실제 hunk 줄 번호·부호·문자 한도와 읽기 전용 고정 제목을 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : HTML·Markdown 명령·파일 이동을 실행하지 않음.
    /// </summary>
    private static void Diff()
    {
        var view = new DiffPreviewPresenter();
        var editor = (RichTextBox)Field(view, "editor");
        string diff = "--- old.cs\n+++ new.cs\n@@ -39,2 +40,2 @@\n-- old\n++ new\n unchanged";
        view.Present(new ChangeCardFile(@"C:\synthetic\sample.cs",1,1,diff), "상태 실제 변경\n기록1\n기록2\n기록3");
        var p = editor.Document.Blocks.OfType<Paragraph>().ToArray();
        Check("File title is fixed and identifies the actual filename", () => ((TextBlock)Field(view,"fileTitle")).Text=="sample.cs" && Grid.GetRow(editor)==1);
        Check("Header reports supplied counts without parsing invented totals", () => new TextRange(((TextBlock)Field(view,"counts")).ContentStart,((TextBlock)Field(view,"counts")).ContentEnd).Text.Contains("+1"));
        Check("Read-only diff explicitly uses the selected semantic code surface", () => editor.IsReadOnly && !editor.IsDocumentEnabled && ((SolidColorBrush)editor.Background).Color==((SolidColorBrush)view.TryFindResource("AssistantCodeBackgroundBrush")).Color);
        Check("File headers are neutral while triple-sign hunk content uses semantic removed/added colors", () => ColorOf(p[0])==((SolidColorBrush)view.TryFindResource("AssistantForegroundBrush")).Color && ColorOf(p[1])==((SolidColorBrush)view.TryFindResource("AssistantForegroundBrush")).Color && ColorOf(p[3])==((SolidColorBrush)view.TryFindResource("AssistantRemovedBrush")).Color && ColorOf(p[4])==((SolidColorBrush)view.TryFindResource("AssistantAddedBrush")).Color);
        Check("Old and new line numbers follow actual hunk counters independently", () => Text(p[3]).StartsWith("   39        ") && Text(p[4]).StartsWith("         40  ") && Text(p[5]).StartsWith("   40    41  "));
        Check("Diff remains selectable and has both bounded scroll directions", () => editor.VerticalScrollBarVisibility==ScrollBarVisibility.Auto && editor.HorizontalScrollBarVisibility==ScrollBarVisibility.Auto);
        Check("Three recent history records remain available without a long default tooltip", () => ((TextBlock)Field(view,"historyText")).Text.Contains("기록3") && view.ToolTip==null);
        view.Present(new ChangeCardFile("unknown.cs",0,0,"미제공",1,false), "");
        Check("Unknown line counts are explicitly unavailable rather than +0/-0", () => ((TextBlock)Field(view,"counts")).Text=="줄 수 미제공");
        view.Present(new ChangeCardFile("huge.cs",5,0,String.Join("\n",Enumerable.Repeat(new String('x',2001),1100))),new String('y',20000));
        Check("Huge diff and history are bounded without changing supplied totals", () => new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Text.Length<65000 && ((TextBlock)Field(view,"historyText")).Text.Length<=12000);
        var target = new Button(); var file = new ChangeCardFile("sample.cs",1,0,"+x"); int click=0; target.Click += (s,e)=>click++;
        ChangeDiffPopup.Attach(target,file,"one"); ChangeDiffPopup.Attach(target,file,"two"); target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("Attaching and rebinding a diff never replaces the original link click", () => click==1);
        var table=typeof(ChangeDiffPopup).GetField("Attached",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);var lookup=new object[]{target,null};table.GetType().GetMethod("TryGetValue").Invoke(table,lookup);var controller=lookup[1];
        Check("Diff popup preserves capture-free C11 behavior and an explicit original anchor",()=>((Popup)Field(controller,"popup")).StaysOpen && ReferenceEquals(((Popup)Field(controller,"popup")).PlacementTarget,target));
        ChangeDiffPopup.CloseWithin(target); target.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Check("Closing an unshown diff controller releases timer and owner without showing native popup", () => !((Popup)Field(controller,"popup")).IsOpen && !((DispatcherTimer)Field(controller,"closeTimer")).IsEnabled && Field(controller,"owner")==null);
        Layout(view,680,420);
        Check("Long diff is measured within a finite panel while retaining its fixed heading",()=>view.ActualHeight<=420 && editor.ActualHeight>0 && editor.ActualHeight<420);
        var progress=new ProgressNoticePresenter{Notice=new ProgressNotice()};var progressBody=(Border)Field(progress,"popupBody");var progressScroll=(ScrollViewer)Field(progress,"detailsScroll");
        Check("Progress title remains outside its finite scrolling rows",()=>progressBody.Child is DockPanel && progressScroll.MaxHeight<=320 && !Double.IsInfinity(progressScroll.MaxHeight));
        var progressWheel=SendWheel(progressBody,-120);
        Check("A progress popup consumes its wheel even with no scroll range",()=>progressWheel.Handled);
    }
    /// <summary>
    /// name         : Wheel
    /// desc         : 실제 routed preview 휠에서 가까운 스크롤 우선·끝 누수·구독 해제를 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : Show나 native 포인터를 사용하지 않는 메모리 레이아웃.
    /// </summary>
    private static void Wheel()
    {
        var leaf = new Border { Height=1500 };
        var inner = new ScrollViewer { Height=100, Content=leaf, CanContentScroll=false };
        var stack = new StackPanel(); stack.Children.Add(inner); stack.Children.Add(new Border { Height=1500 });
        var outer = new ScrollViewer { Height=200, Width=300, Content=stack, CanContentScroll=false };
        HoverScrollPolicy.SetContainMouseWheel(outer,true); HoverScrollPolicy.SetContainMouseWheel(inner,true); Layout(outer,300,200);
        var args = SendWheel(leaf,-120); Layout(outer,300,200);
        Check("Nested viewer consumes the actual routed wheel before its parent", () => args.Handled && inner.VerticalOffset>0 && outer.VerticalOffset==0);
        inner.ScrollToEnd(); Layout(outer,300,200); double end=inner.VerticalOffset; args=SendWheel(leaf,-120); Layout(outer,300,200);
        Check("Nested end consumes wheel without scrolling the background", () => args.Handled && inner.VerticalOffset==end && outer.VerticalOffset==0);
        inner.ScrollToHome(); Layout(outer,300,200); args=SendWheel(leaf,120); Layout(outer,300,200);
        Check("Nested start also contains the upward wheel", () => args.Handled && inner.VerticalOffset==0 && outer.VerticalOffset==0);
        args=SendWheel(leaf,-60); Layout(outer,300,200); double half=inner.VerticalOffset;
        Check("High-resolution half-notch produces a finite smaller movement", () => half>0 && half<100);
        inner.ScrollToHome(); Layout(outer,300,200); HoverScrollPolicy.SetContainMouseWheel(inner,true); HoverScrollPolicy.SetContainMouseWheel(inner,true); SendWheel(leaf,-60); Layout(outer,300,200);
        Check("Repeated attached true does not duplicate wheel distance", () => Math.Abs(inner.VerticalOffset-half)<.1);
        HoverScrollPolicy.SetContainMouseWheel(inner,false); HoverScrollPolicy.SetContainMouseWheel(outer,false); args=SendWheel(leaf,-120);
        Check("Disabling the attached policy releases its preview handling", () => !args.Handled);
        var shortLeaf = new Border { Height=20 }; var shortInner = new ScrollViewer { Height=100,Content=shortLeaf }; var longStack=new StackPanel();longStack.Children.Add(shortInner);longStack.Children.Add(new Border{Height=1500});
        var validParent = new ScrollViewer{Height=200,Width=300,Content=longStack}; HoverScrollPolicy.SetContainMouseWheel(validParent,true);HoverScrollPolicy.SetContainMouseWheel(shortInner,true);Layout(validParent,300,200);args=SendWheel(shortLeaf,-120);Layout(validParent,300,200);
        Check("An unscrollable child delegates to a valid parent rather than swallowing movement",()=>args.Handled && validParent.VerticalOffset>0 && shortInner.VerticalOffset==0);
        var run=new Run("a"); var rich=new RichTextBox{Height=100,Width=250,IsReadOnly=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};rich.Document.Blocks.Add(new Paragraph(run));for(int i=0;i<90;i++)rich.Document.Blocks.Add(new Paragraph(new Run("line "+i)));
        var richHost=new ScrollViewer{Height=150,Width=300,Content=rich};HoverScrollPolicy.SetContainMouseWheel(richHost,true);Layout(richHost,300,150);
        foreach(var scroll in Visuals(rich).OfType<ScrollViewer>())HoverScrollPolicy.SetContainMouseWheel(scroll,true);
        var contentWheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=Mouse.PreviewMouseWheelEvent};run.RaiseEvent(contentWheel);Layout(richHost,300,150);
        Check("FlowDocument Run routes its wheel to the document viewer without parent movement [handled="+contentWheel.Handled+", rich="+rich.VerticalOffset+", outer="+richHost.VerticalOffset+", extent="+rich.ExtentHeight+", viewport="+rich.ViewportHeight+"]",()=>contentWheel.Handled && rich.VerticalOffset>0 && richHost.VerticalOffset==0);
    }
    /// <summary>
    /// name         : Overlay
    /// desc         : 좁은·넓은 배치, 원래 값 복원과 종료 시 이벤트 정리를 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 실제제품 모드는 runtime discovery 격리가 필요.
    /// </summary>
    private static void Overlay()
    {
        using(var control=new AssistantControl())
        {
            var toggle=(ToggleButton)Field(control,"HistoryToggle"); var panel=(FrameworkElement)Field(control,"HistoryPanel"); var surface=(FrameworkElement)control.FindName("ConversationSurface");var backdrop=(FrameworkElement)control.FindName("HistoryBackdrop");
            toggle.IsChecked=true; panel.Visibility=Visibility.Visible;Layout(control,320,500);Invoke(control,"UpdateHistoryOverlay");
            Check("Narrow history open shows a distinct backdrop and disables underlying conversation and composer",()=>backdrop.Visibility==Visibility.Visible && !surface.IsEnabled && !((FrameworkElement)control.FindName("ComposerSurface")).IsEnabled);
            Check("Overlay accessibility describes Escape and outside click dismissal",()=>System.Windows.Automation.AutomationProperties.GetHelpText(panel).Contains("Escape"));
            Check("Overlay keyboard navigation cycles inside the visible history",()=>KeyboardNavigation.GetTabNavigation(panel)==KeyboardNavigationMode.Cycle);
            Invoke(control,"UpdateHistoryOverlay");Check("Repeated layout retains the same temporary state without restoring early",()=>!surface.IsEnabled && (bool)Field(control,"historyOverlayActive"));
            Layout(control,800,500);Invoke(control,"UpdateHistoryOverlay");
            Check("Wide side-by-side history removes dimming and restores original inherited enable state",()=>backdrop.Visibility==Visibility.Collapsed && surface.IsEnabled && surface.ReadLocalValue(UIElement.IsEnabledProperty)==DependencyProperty.UnsetValue);
            surface.SetValue(UIElement.IsEnabledProperty,false);Layout(control,320,500);Invoke(control,"UpdateHistoryOverlay");Layout(control,800,500);Invoke(control,"UpdateHistoryOverlay");
            Check("An originally disabled conversation is not incorrectly enabled when overlay closes",()=>!surface.IsEnabled);
            surface.ClearValue(UIElement.IsEnabledProperty);var bindingSource=new Button{IsEnabled=true};BindingOperations.SetBinding(surface,UIElement.IsEnabledProperty,new Binding("IsEnabled"){Source=bindingSource});Layout(control,320,500);Invoke(control,"UpdateHistoryOverlay");Layout(control,800,500);Invoke(control,"UpdateHistoryOverlay");
            Check("Temporary overlay preserves an existing enabled binding and restores its original result",()=>BindingOperations.IsDataBound(surface,UIElement.IsEnabledProperty) && surface.IsEnabled);
            BindingOperations.ClearBinding(surface,UIElement.IsEnabledProperty);Layout(control,320,500);Invoke(control,"UpdateHistoryOverlay");Invoke(control,"DismissHistoryOverlay");
            Check("Explicit dismissal clears toggle and backdrop and restores interaction",()=>toggle.IsChecked==false && backdrop.Visibility==Visibility.Collapsed && surface.IsEnabled);
            toggle.IsChecked=true;panel.Visibility=Visibility.Visible;Invoke(control,"UpdateHistoryOverlay");control.Dispose();
            Check("Dispose removes overlay hooks, dimming and pending layout focus",()=>!(bool)Field(control,"historyOverlayHooks") && !(bool)Field(control,"historyOverlayFocusPending") && backdrop.Visibility==Visibility.Collapsed && surface.IsEnabled && ((FrameworkElement)control.FindName("ComposerSurface")).IsEnabled);
        }
    }
    /// <summary>
    /// name         : Palette
    /// desc         : 분리된 popup이 실제 host 사전·implicit style과 열린 문서의 동적 색상을 공유하는지 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 실제 native 창 표시 증거와는 분리.
    /// </summary>
    private static void Palette()
    {
        using(var host=new AssistantControl())
        {
            host.Resources["AssistantPopupBackgroundBrush"]=Brushes.WhiteSmoke;host.Resources["AssistantPopupBorderBrush"]=Brushes.Gray;host.Resources["AssistantForegroundBrush"]=Brushes.Black;host.Resources["AssistantMutedForegroundBrush"]=Brushes.DimGray;host.Resources["AssistantCodeBackgroundBrush"]=Brushes.White;host.Resources["AssistantAddedBrush"]=new SolidColorBrush(Color.FromRgb(25,116,60));host.Resources["AssistantRemovedBrush"]=new SolidColorBrush(Color.FromRgb(179,38,50));
            var style=new Style(typeof(Button));host.Resources[typeof(Button)]=style;
            var anchor=new Button();((Grid)Field(host,"LayoutRoot")).Children.Add(anchor);var file=new ChangeCardFile("light.cs",1,1,"@@ -1 +1 @@\n-old\n+new");ChangeDiffPopup.Attach(anchor,file,"real history");
            var table=typeof(ChangeDiffPopup).GetField("Attached",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);var query=new object[]{anchor,null};table.GetType().GetMethod("TryGetValue").Invoke(table,query);var controller=query[1];var body=(Border)Field(controller,"body");var view=(DiffPreviewPresenter)Field(controller,"preview");
            var palette=typeof(ChangeDiffPopup).Assembly.GetType("VS2017.AIAssistant.Views.PopupPalette");palette.GetMethod("Attach",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{view,anchor});view.Present(file,"real history");Layout(view,600,350);
            Check("Independent popup shares the host dictionary and its actual implicit button style",()=>body.Resources.MergedDictionaries.Contains(host.Resources) && ReferenceEquals(body.TryFindResource(typeof(Button)),style) && ((SolidColorBrush)body.Background).Color==Colors.WhiteSmoke);
            var editor=(RichTextBox)Field(view,"editor");var added=editor.Document.Blocks.OfType<Paragraph>().Last().Inlines.OfType<Run>().Last();
            Check("Light popup and FlowDocument runs resolve readable semantic colors",()=>((SolidColorBrush)editor.Background).Color==Colors.White && ((SolidColorBrush)added.Foreground).Color==Color.FromRgb(25,116,60));
            host.Resources["AssistantPopupBackgroundBrush"]=Brushes.Black;host.Resources["AssistantCodeBackgroundBrush"]=Brushes.DarkSlateGray;host.Resources["AssistantAddedBrush"]=Brushes.LimeGreen;Layout(view,600,350);
            Check("Replacing host palette brushes updates existing popup and document expressions without rebuilding",()=>((SolidColorBrush)body.Background).Color==Colors.Black && ((SolidColorBrush)editor.Background).Color==Colors.DarkSlateGray && ((SolidColorBrush)added.Foreground).Color==Colors.LimeGreen);
            ChangeDiffPopup.CloseWithin(host);
        }
    }
    /// <summary>
    /// name         : Check
    /// desc         : 실패 한 건이 다른 독립 경계 검사를 가리지 않도록 결과를 수집한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 실패 문자열과 검사 이름 보존.
    /// </summary>
    private static void Check(string name,Func<bool> test){try{if(!test())throw new InvalidOperationException(name);Checks.Add(name);}catch(Exception e){Errors.Add(name+": "+e.Message);}}
    /// <summary>
    /// name         : Field
    /// desc         : 검사 대상 private 상태만 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 실제 명령·저장을 실행하지 않음.
    /// </summary>
    private static object Field(object target,string name){return target.GetType().GetField(name,Private).GetValue(target);}
    /// <summary>
    /// name         : Invoke
    /// desc         : UI 메모리 상태 helper를 호출한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : source/actual 동일 계약.
    /// </summary>
    private static void Invoke(object target,string name){target.GetType().GetMethod(name,Private).Invoke(target,null);}
    /// <summary>
    /// name         : Text
    /// desc         : 선택 가능한 문단의 실제 원문을 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 렌더 색상과 데이터 검사 구분.
    /// </summary>
    private static string Text(Paragraph p){return new TextRange(p.ContentStart,p.ContentEnd).Text;}
    /// <summary>
    /// name         : ColorOf
    /// desc         : 실제 문단 본문의 색상을 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 줄 번호의 중립색은 제외.
    /// </summary>
    private static Color ColorOf(Paragraph p){return ((SolidColorBrush)p.Inlines.OfType<Run>().Last().Foreground).Color;}
    /// <summary>
    /// name         : Layout
    /// desc         : Show 없이 유한 크기의 메모리 레이아웃과 queued scroll만 처리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 포인터·전경 앱에 접근하지 않음.
    /// </summary>
    private static void Layout(FrameworkElement element,double width,double height){element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background,new Action(()=>{}));element.UpdateLayout();}
    /// <summary>
    /// name         : SendWheel
    /// desc         : 메모리 트리에 synthetic routed wheel만 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : native 입력은 생성하지 않음.
    /// </summary>
    private static MouseWheelEventArgs SendWheel(UIElement target,int delta){var e=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,delta){RoutedEvent=Mouse.PreviewMouseWheelEvent};target.RaiseEvent(e);return e;}
    /// <summary>
    /// name         : Visuals
    /// desc         : 내부 RichTextBox ScrollViewer를 메모리에서 찾는다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : Popup/Window를 열지 않음.
    /// </summary>
    private static IEnumerable<DependencyObject> Visuals(DependencyObject root){if(!(root is Visual))yield break;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var item in Visuals(child))yield return item;}}
}
