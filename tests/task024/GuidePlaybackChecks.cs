using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VS2017.AIAssistant.Views;

namespace CodexCode.Task024
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GuidePlaybackResult
    /// desc         : 실제 동봉 이미지의 표시·프레임 진행·해제 결과와 OS 정지 정책을 구분한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 실제 재생을 수행하지 않은 CompileOnly와 별도 결과.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class GuidePlaybackResult
    {
        public List<string> Passed { get; } = new List<string>();
        public List<string> Failures { get; } = new List<string>();
        public List<string> Skipped { get; } = new List<string>();
        public List<GuideFrameObservation> Observations { get; } = new List<GuideFrameObservation>();
        public bool AnimationAllowed { get; set; }
        public bool WindowShown { get; set; }
        public bool AnimationAdvanceVerified { get; set; }
        public bool Succeeded { get { return Failures.Count == 0; } }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GuideFrameObservation
    /// desc         : 실제 public Source 프레임 참조의 관찰 횟수와 경과 시간을 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 private frameIndex나 시간을 조작하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class GuideFrameObservation
    {
        public string Stage { get; set; }
        public int Image { get; set; }
        public int FrameCount { get; set; }
        public int SourceChanges { get; set; }
        public double ElapsedMilliseconds { get; set; }
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GuidePlaybackChecks
    /// desc         : 최종 제품의 한 안내 창에서 실제 GIF Loaded·접힘·복귀·종료 수명을 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 호출은 GUI 예약 뒤에만, 네이티브 입력·인증·설치 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class GuidePlaybackChecks
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Run
        /// desc         : 실제 동봉 다섯 GIF와 PNG를 읽고 한 번 연 WPF 안내 창만 사용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 OS 모션 설정은 변경하지 않고 정지 정책을 결과에 명시.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static GuidePlaybackResult Run(string outputDirectory)
        {
            var result = new GuidePlaybackResult { AnimationAllowed = SystemParameters.ClientAreaAnimation };
            var retained = new List<MarkdownAnimatedImage>();
            AssistantControl source = null; GettingStartedDialog window = null;
            try
            {
                string assets = Path.Combine(Path.GetDirectoryName(typeof(AssistantControl).Assembly.Location), "Resources", "GuideMedia");
                string[] names = { "01-connect", "02-request", "03-review", "04-guide", "05-install" };
                Check(result, "The actual product directory contains five GIF and five PNG guide assets", () => Assert(names.All(n => File.Exists(Path.Combine(assets, n + ".gif")) && File.Exists(Path.Combine(assets, n + ".png"))), "GuideMedia bundle incomplete."));
                var gifs = names.Select(n => new MarkdownAnimatedImage { AssetBaseDirectory = assets, SourcePath = Path.Combine(assets, n + ".gif"), MaxHeight = 100, Margin = new Thickness(2) }).ToArray();
                retained.AddRange(gifs);
                Check(result, "All five actual GIF files decode into multiple frames", () => Assert(gifs.All(i => i.FrameCount > 1 && i.Source != null && i.FailureReason == null), "GIF decode or frame count failed."));
                Check(result, "An unshown GIF does not start its timer", () => Assert(gifs.All(i => !i.IsLoaded && !i.IsVisible && !i.IsAnimationRunning), "An unshown GIF is running."));
                var pngs = names.Select(n => new MarkdownAnimatedImage { AssetBaseDirectory = assets, SourcePath = Path.Combine(assets, n + ".png") }).ToArray(); retained.AddRange(pngs);
                Check(result, "All five actual PNG fallbacks remain static single frames", () => Assert(pngs.All(i => i.FrameCount == 1 && i.Source != null && !i.IsAnimationRunning), "PNG fallback invalid."));
                foreach (var png in pngs) png.Dispose();

                source = new AssistantControl(); window = new GettingStartedDialog(source, false);
                var gallery = (ContentControl)window.Body.Content; var overview = (ScrollViewer)gallery.Content;
                var grid = new UniformGrid { Rows = 3, Columns = 2 }; foreach (var gif in gifs) grid.Children.Add(gif);
                window.Body.Content = grid; window.Width = Math.Min(720, window.MaxWidth); window.Height = Math.Min(640, window.MaxHeight);
                window.ShowActivated = false; window.Show(); result.WindowShown = true; Pump(220);
                Check(result, "One actual guide window is visible and its five GIFs are Loaded and visible", () => Assert(window.IsVisible && gifs.All(i => i.IsLoaded && i.IsVisible), "Shown window did not load its images."));
                Check(result, "Loaded GIF timer state follows the unchanged OS animation policy", () => Assert(gifs.All(i => i.IsAnimationRunning == result.AnimationAllowed), "Animation policy mismatch."));
                var direct = Observe("five-gif", gifs, 1100); result.Observations.AddRange(direct);
                Check(result, "Frame observation waits at least 1.1 seconds on the actual Dispatcher", () => Assert(direct.All(i => i.ElapsedMilliseconds >= 1100), "Observation was shorter than the real-time boundary."));
                CheckAdvance(result, direct, "All five visible GIF Source references advance");
                Snapshot(window, Path.Combine(outputDirectory, "guide-playback.png"));

                window.Body.Content = null; Pump(160);
                Check(result, "Removing the image page unloads every GIF and stops its timer", () => Assert(gifs.All(i => !i.IsLoaded && !i.IsAnimationRunning), "Unloaded image timer remains active."));
                window.Body.Content = grid; Pump(160);
                Check(result, "Reattaching the same page resumes according to OS policy", () => Assert(gifs.All(i => i.IsLoaded && i.IsVisible && i.IsAnimationRunning == result.AnimationAllowed), "Reattached timer state mismatch."));
                gifs[4].Dispose();
                Check(result, "Dispose while visible clears frames Source and timer", () => Assert(gifs[4].FrameCount == 0 && gifs[4].Source == null && !gifs[4].IsAnimationRunning, "Disposed image retained playback state."));

                var settings = new AssistantGuideDetails { Summary = "동봉 연결 안내 재생 검사", Markdown = "## 실제 동봉 GIF\n\n설명은 접고 펼 수 있습니다.", MediaKey = "connect" };
                window.Body.Content = settings; Pump(160);
                var expander = Descendants(settings).OfType<Expander>().Single();
                Check(result, "Actual settings details begin collapsed without creating a GIF", () => Assert(!expander.IsExpanded && !Descendants(settings).OfType<MarkdownAnimatedImage>().Any(), "Collapsed details eagerly created an image."));
                expander.IsExpanded = true; Pump(160);
                var settingsGif = Descendants(settings).OfType<MarkdownAnimatedImage>().Single(); retained.Add(settingsGif);
                Check(result, "Expanding actual settings loads the packaged GIF and starts expected playback", () => Assert(settingsGif.FrameCount > 1 && settingsGif.IsLoaded && settingsGif.IsVisible && settingsGif.IsAnimationRunning == result.AnimationAllowed, "Expanded details did not play the packaged image."));
                expander.IsExpanded = false; Pump(160);
                Check(result, "Collapsing details stops playback while preserving decoded frames", () => Assert(!settingsGif.IsAnimationRunning && (!settingsGif.IsVisible || !settingsGif.IsLoaded) && settingsGif.FrameCount > 1, "Collapsed details still animate or lost their frames."));
                expander.IsExpanded = true; Pump(160);
                Check(result, "Reopening details reuses the same image and resumes expected playback", () => Assert(ReferenceEquals(Descendants(settings).OfType<MarkdownAnimatedImage>().Single(), settingsGif) && settingsGif.IsLoaded && settingsGif.IsVisible && settingsGif.IsAnimationRunning == result.AnimationAllowed, "Details failed to reuse and resume."));
                var reopened = Observe("settings-reopened", new[] { settingsGif }, 1100); result.Observations.AddRange(reopened); CheckAdvance(result, reopened, "Reopened details advance their Source frame");

                window.Body.Content = gallery; Pump(160);
                Check(result, "Switching to guide cards unloads the previous details GIF", () => Assert(!settingsGif.IsLoaded && !settingsGif.IsAnimationRunning, "Previous details continued playback after page removal."));
                var card = ((Panel)overview.Content).Children.OfType<Button>().First(); card.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(180);
                var detailPage = (Grid)gallery.Content; var back = detailPage.Children.OfType<Button>().Single();
                var detailGif = Descendants(detailPage).OfType<MarkdownAnimatedImage>().Single(); retained.Add(detailGif);
                Check(result, "Actual gallery detail has a fixed upper-right back button and loaded packaged GIF", () => Assert(Grid.GetRow(back) == 0 && back.HorizontalAlignment == HorizontalAlignment.Right && detailGif.FrameCount > 1 && detailGif.IsLoaded && detailGif.IsVisible, "Actual gallery layout or image load failed."));
                var galleryFrames = Observe("gallery-detail", new[] { detailGif }, 1100); result.Observations.AddRange(galleryFrames); CheckAdvance(result, galleryFrames, "Actual guide detail advances its Source frame");
                back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(160);
                Check(result, "Back restores the original cards and stops the removed detail GIF", () => Assert(ReferenceEquals(gallery.Content, overview) && !detailGif.IsLoaded && !detailGif.IsAnimationRunning, "Back left a detail GIF running."));

                var closeGif = new MarkdownAnimatedImage { AssetBaseDirectory = assets, SourcePath = Path.Combine(assets, "05-install.gif") }; retained.Add(closeGif);
                window.Body.Content = closeGif; Pump(160);
                Check(result, "The image used for the close check is Loaded before window close", () => Assert(closeGif.IsLoaded && closeGif.IsVisible && closeGif.FrameCount > 1 && closeGif.IsAnimationRunning == result.AnimationAllowed, "Close check did not start with a loaded image."));
                window.Close(); Pump(180);
                Check(result, "Closing the sole window unloads its current image and stops all retained timers", () => Assert(!window.IsVisible && !closeGif.IsLoaded && retained.All(i => !i.IsAnimationRunning), "Window close left playback active."));
                foreach (var image in retained) image.Dispose(); Pump(120);
                Check(result, "Final Dispose releases all retained image frames Sources and timers", () => Assert(retained.All(i => i.FrameCount == 0 && i.Source == null && !i.IsAnimationRunning), "Final image cleanup incomplete."));
                foreach (var preview in Descendants(settings).OfType<MarkdownPreview>()) preview.Dispose();
                result.AnimationAdvanceVerified = result.AnimationAllowed && result.Observations.All(o => o.SourceChanges > 0);
                Check(result, "The test never changes the OS animation policy", () => Assert(result.AnimationAllowed == SystemParameters.ClientAreaAnimation, "OS animation policy changed during the test."));
            }
            catch (Exception error) { result.Failures.Add("Setup/completion: " + error.GetBaseException().Message); }
            finally
            {
                if (window != null) window.Close(); foreach (var image in retained) image.Dispose();
                if (source != null) source.Dispose();
            }
            return result;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckAdvance
        /// desc         : OS가 애니메이션을 허용하는 경우에만 프레임 진행을 성공으로 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 모션을 끈 환경은 정지 상태와 advance 미검증을 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckAdvance(GuidePlaybackResult result, GuideFrameObservation[] observations, string name)
        {
            if (result.AnimationAllowed) Check(result, name, () => Assert(observations.All(o => o.SourceChanges > 0), "No real Source frame change observed within 1.1 seconds."));
            else { Check(result, "OS disabled motion retains static Source: " + observations[0].Stage, () => Assert(observations.All(o => o.SourceChanges == 0), "Source changed while OS motion was disabled.")); result.Skipped.Add(name + " · ClientAreaAnimation=false, advance not measured; OS policy unchanged."); }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Observe
        /// desc         : 유한 Dispatcher timer 동안 실제 Source 참조 변화를 누적한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 한 주기를 돌아 같은 마지막 프레임이 되어도 중간 진행을 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static GuideFrameObservation[] Observe(string stage, MarkdownAnimatedImage[] images, int milliseconds)
        {
            var values = images.Select((i, index) => new GuideFrameObservation { Stage = stage, Image = index, FrameCount = i.FrameCount }).ToArray();
            var last = images.Select(i => i.Source).ToArray();
            var elapsed = Pump(milliseconds, () => { for (int i = 0; i < images.Length; i++) if (!ReferenceEquals(last[i], images[i].Source)) { values[i].SourceChanges++; last[i] = images[i].Source; } });
            foreach (var value in values) value.ElapsedMilliseconds = elapsed; return values;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Pump
        /// desc         : 실시간 Stopwatch를 기준으로 제한된 WPF 메시지 처리만 수행한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 Thread.Sleep·시계 수정·가짜 Tick 호출 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static double Pump(int milliseconds, Action sample = null)
        {
            var frame = new DispatcherFrame(); var clock = Stopwatch.StartNew();
            var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(25) };
            EventHandler tick = (sender, args) => { if (sample != null) sample(); if (clock.ElapsedMilliseconds >= milliseconds) frame.Continue = false; };
            timer.Tick += tick; timer.Start(); try { Dispatcher.PushFrame(frame); } finally { timer.Stop(); timer.Tick -= tick; } return clock.Elapsed.TotalMilliseconds;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Snapshot
        /// desc         : 자신의 WPF 안내 창만 렌더하고 다른 앱 화면을 캡처하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 출력은 새 결과 폴더의 PNG 한 장으로 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Snapshot(FrameworkElement element, string path)
        {
            int width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth)), height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) encoder.Save(stream);
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Descendants
        /// desc         : 실제 공개 콘텐츠 트리에서 그림·Expander·버튼만 찾는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 private 재생 필드나 상태 값을 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        { yield return root; foreach (object child in LogicalTreeHelper.GetChildren(root)) { var item = child as DependencyObject; if (item != null) foreach (var childItem in Descendants(item)) yield return childItem; } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Assert
        /// desc         : 실제 표시·수명 계약 위반을 명확한 실패로 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 미검증을 성공으로 간주하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Check
        /// desc         : 병렬 실행 없이 각 의미 있는 관찰 결과를 수집한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 실패 원문과 실행 순서를 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Check(GuidePlaybackResult result, string name, Action test)
        { try { test(); result.Passed.Add(name); } catch (Exception error) { result.Failures.Add(name + ": " + error.GetBaseException().Message); } }
    }
}
