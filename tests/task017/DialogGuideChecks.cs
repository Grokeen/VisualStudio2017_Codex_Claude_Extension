using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : DialogGuideChecks
/// desc         : 실제 WPF 창에서 안내·목차·이미지·초점과 좁은 폭 배치를 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : TASK-017 안내 전용 독립 검사와 PNG 증거 생성.
/// </summary>
/// ---------------------------------------------------------------------
public static class DialogGuideChecks
{
    public static readonly List<string> Checks = new List<string>();
    public static readonly List<string> Errors = new List<string>();

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 사용자 설정·계정 연결 없이 안내 UI의 실제 상태 전환을 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 고정 DLL 또는 독립 소스 검증 어셈블리를 동일하게 검사.
    ///                2026-10-19 CodexOrig : 의미 팔레트와 실제 Markdown·동봉 그림으로 바뀐 상세 화면을 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Run(string directory)
    {
        var source = new AssistantControl();
        if (!source.Resources.Contains("PrimaryButton")) source.Resources["PrimaryButton"] = new Style(typeof(Button));
        source.Resources["DialogBackground"] = new SolidColorBrush(Color.FromRgb(23,23,23)); source.Resources["DialogForeground"] = Brushes.Gainsboro;
        var owner = new Window { Width = 320, Height = 200, ShowActivated = false }; owner.Show(); owner.Hide(); owner.Content = source;
        AssistantDialog dialog = null;
        try
        {
            dialog = new GettingStartedDialog(source,true) { Background = new SolidColorBrush(Color.FromRgb(23,23,23)), Foreground = Brushes.Gainsboro };
            dialog.Show(); dialog.Activate(); Pump();
            var guide = (GettingStartedDialog)dialog;
            Assert(guide.HideNextTime.IsChecked == true && guide.StartButton.IsDefault && guide.ConnectButton != null, "Existing onboarding preferences and actions remain available");
            Assert(dialog.Footer.Background is SolidColorBrush && ((SolidColorBrush)dialog.Footer.Background).Color == ((SolidColorBrush)source.FindResource("AssistantSurfaceBrush")).Color && dialog.Footer.BorderThickness.Top == 1, "Footer follows the selected dark surface palette and keeps its top divider");
            var cards = Descendants<Button>(dialog.Body).Where(x=>x.DataContext is GuideStep).ToArray();
            Assert(cards.Length == 4, "Four overview cards expose meaningful guide topics");
            var first = cards[0]; var surface = (Border)first.Content; var original = first.TranslatePoint(new Point(0,0),dialog);
            first.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseEnterEvent}); Pump();
            Assert(surface.Opacity > .95 && ((Border)cards[1].Content).Opacity < .6, "Hover promotes one card while dimming the other cards");
            Assert(Panel.GetZIndex(first) > Panel.GetZIndex(cards[1]) && ((TranslateTransform)surface.RenderTransform).Y < -1, "Hovered surface moves forward and has greater drawing depth");
            Assert(first.TranslatePoint(new Point(0,0),dialog) == original, "Hover leaves the button hit rectangle in its original position");
            Capture(dialog,Path.Combine(directory,"guide-overview-hover.png"));
            first.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseLeaveEvent}); Pump();
            Assert(cards.All(x=>((Border)x.Content).Opacity > .95), "Leaving cards restores every overview card");
            first.Focus(); Pump();
            Assert(first.IsKeyboardFocused && ((Border)cards[1].Content).Opacity < .6, "Keyboard focus receives the same guide emphasis");
            first.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(first),0,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent}); Pump();
            Assert(Descendants<Image>(dialog.Body).Any(x=>x.Source != null), "Enter opens a detail screen containing a packaged procedure image or its drawing fallback");
            Assert(!Descendants<Button>(dialog.Body).Any(x=>x.DataContext is GuideStep), "Detail replaces overview in the same dialog body");
            Assert(Descendants<MarkdownPreview>(dialog.Body).Any(x=>x.Markdown.Contains("공식 CLI") && x.Markdown.Contains("로그인") && new System.Windows.Documents.TextRange(x.TextView.Document.ContentStart,x.TextView.Document.ContentEnd).Text.Contains("로그인")), "Detail renders actionable topic-specific Markdown explanation");
            var back = Descendants<Button>(dialog.Body).Single(x=>Convert.ToString(x.Content).Contains("안내 목록으로"));
            Assert(back.IsKeyboardFocused, "Opening detail places focus on the return button");
            Assert(guide.HideNextTime.IsChecked == true && guide.HideNextTime.IsVisible, "Detail retains the user's onboarding display preference");
            Capture(dialog,Path.Combine(directory,"guide-detail.png"));
            back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Assert(first.IsKeyboardFocused && Descendants<Button>(dialog.Body).Count(x=>x.DataContext is GuideStep)==4, "Returning restores overview and focus to the original card");
            var third=cards[2]; third.Focus(); Pump();
            third.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(third),0,Key.Space){RoutedEvent=Keyboard.KeyDownEvent});
            third.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(third),0,Key.Space){RoutedEvent=Keyboard.KeyUpEvent}); Pump();
            Assert(Descendants<Image>(dialog.Body).Any(), "Space also opens the focused card detail");
            dialog.Width=320; dialog.Height=580; Pump();
            Assert(dialog.Body.ActualWidth>200 && Descendants<ScrollViewer>(dialog.Body).All(x=>x.HorizontalScrollBarVisibility==ScrollBarVisibility.Disabled), "Narrow guide detail remains readable without horizontal scrolling");
            Capture(dialog,Path.Combine(directory,"guide-detail-narrow.png"));
            dialog.Close(); Pump();
            Assert(!surface.HasAnimatedProperties && !((TranslateTransform)surface.RenderTransform).HasAnimatedProperties, "Closing releases card animation clocks");
            dialog = new AssistantDialog(source,"설정 검사","상단 목차에서 구역을 선택하세요."){Background=new SolidColorBrush(Color.FromRgb(23,23,23)),Foreground=Brushes.Gainsboro,Height=500};
            var panel=new StackPanel(); var input=new TextBox{Text="보존할 입력"};
            var top=AssistantDialog.CreateSection("⚙","계정",input); panel.Children.Add(top);
            panel.Children.Add(new Border{Height=600});var bottom=AssistantDialog.CreateSection("✎","공통 규칙",new TextBox{Text="규칙"});panel.Children.Add(bottom);
            var scroll=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};dialog.Body.Content=scroll;
            dialog.SetSections(scroll,new DialogSection("⚙","계정",top),new DialogSection("✎","공통 규칙",bottom));
            dialog.Show();dialog.Activate();Pump();
            var navigationTop=dialog.Navigation.TranslatePoint(new Point(0,0),dialog);
            ((Button)dialog.Navigation.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Assert(scroll.VerticalOffset>400, "Clicking the fixed table of contents scrolls to the actual lower section");
            Assert(dialog.Navigation.TranslatePoint(new Point(0,0),dialog)==navigationTop, "Table of contents remains fixed outside the scrolling body");
            Assert(input.Text=="보존할 입력", "Section navigation preserves unsaved editor values");
            Assert(bottom.Padding.Top==16 && bottom.Margin.Bottom>=20 && Descendants<TextBlock>(bottom).Any(x=>x.Text.Contains("✎")), "Shared sections include icons and increased spacing");
            Capture(dialog,Path.Combine(directory,"dialog-section-navigation.png"));
            ((Button)dialog.Navigation.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Assert(scroll.VerticalOffset<5, "Table of contents can return to the first section");
            dialog.Width=320;Pump();
            Assert(dialog.Navigation.ActualWidth<=280 && dialog.Footer.ActualWidth>dialog.Body.ActualWidth, "Narrow navigation wraps while footer spans the dialog width");
            Capture(dialog,Path.Combine(directory,"dialog-section-narrow.png"));
        }
        catch(Exception error){Errors.Add(error.ToString());}
        finally{if(dialog!=null)dialog.Close();owner.Content=null;owner.Close();var disposable=(object)source as IDisposable;if(disposable!=null)disposable.Dispose();Pump();}
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Descendants
    /// desc         : 실제 렌더된 WPF 자식에서 검사 대상을 찾는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 구현의 private 필드에 의존하지 않는 화면 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject
    {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T)yield return (T)child;foreach(var nested in Descendants<T>(child))yield return nested;}}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Pump
    /// desc         : 유한 애니메이션과 레이아웃이 끝날 때까지 UI 메시지를 처리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 검사 타이머는 매번 즉시 해제.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Pump(){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(230)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Capture
    /// desc         : 검증 창을 실제 PNG로 저장하여 직접 시각 검토할 수 있게 한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 현재 레이아웃 크기 그대로 저장.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Capture(Window window,string path){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Assert
    /// desc         : 화면 검사의 성공 목록을 남기고 실패하면 원인을 명시한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 조용한 부분 성공 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Assert(bool value,string name){if(!value)throw new InvalidOperationException(name);Checks.Add(name);}
}
