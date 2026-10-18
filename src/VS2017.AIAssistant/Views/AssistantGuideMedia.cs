using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantGuideMedia
    /// desc         : 알려진 안내 키의 동봉 그림만 선택하고 공용 GIF 컨트롤에 표시를 맡긴다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 임의 경로·재분석 지점·원격 이미지 읽기 없음.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class AssistantGuideMedia
    {
        private const long MaximumMediaBytes = 8L * 1024 * 1024;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetAssetDirectory
        /// desc         : 현재 제품 assembly 아래의 정상 로컬 안내 자산 폴더만 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 폴더 누락이나 링크 경로이면 안전한 그림 대체 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string GetAssetDirectory()
        {
            try
            {
                string root = Path.GetDirectoryName(typeof(AssistantGuideMedia).Assembly.Location);
                if (String.IsNullOrWhiteSpace(root) || root.StartsWith(@"\\", StringComparison.Ordinal)) return null;
                string directory = Path.GetFullPath(Path.Combine(root, "Resources", "GuideMedia"));
                return Directory.Exists(directory) && IsPlainPath(directory) ? directory : null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Resolve
        /// desc         : 다섯 고정 안내 키를 작은 GIF 또는 PNG 파일로만 해석한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 문자열을 파일 이름이나 명령으로 조합하지 않음.
        ///                2026-10-19 CodexCode : PNG 재시도도 동일한 고정 키·경로·크기 검증을 공유.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string Resolve(string key)
        {
            return ResolveCandidate(key, false);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResolveCandidate
        /// desc         : 고정 자산 후보를 검증하며 GIF 실패 뒤에는 PNG 후보만 다시 선택한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 재시도에서도 로컬 동봉 경로·재분석 지점·바이트 한도 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ResolveCandidate(string key, bool pngOnly)
        {
            string file;
            switch (key)
            {
                case "connect": file = "01-connect"; break;
                case "request": file = "02-request"; break;
                case "review": file = "03-review"; break;
                case "guide": file = "04-guide"; break;
                case "install": file = "05-install"; break;
                default: return null;
            }
            string directory = GetAssetDirectory(); if (directory == null) return null;
            foreach (string extension in pngOnly ? new[] { ".png" } : new[] { ".gif", ".png" })
            {
                string path = Path.Combine(directory, file + extension);
                try
                {
                    if (!File.Exists(path) || !IsPlainPath(path)) continue;
                    long length = new FileInfo(path).Length;
                    if (length > 0 && length <= MaximumMediaBytes) return path;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsPlainPath
        /// desc         : 파일과 모든 상위 폴더에 재분석 지점이 없는지 확인한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 동봉 자산 폴더 밖으로 이어지는 링크 거부.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsPlainPath(string path)
        {
            for (string current = path; !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
            return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : KeyForTitle
        /// desc         : 기존 안내 제목을 동봉 그림의 고정 키로 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 기존 GuideStep 생성자와 원문 수정 없이 그림 공유.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string KeyForTitle(string title)
        {
            title = title ?? "";
            if (title.Contains("설치")) return "install";
            if (title.Contains("연결") || title.Contains("계정") || title.Contains("파일 선택") || title.Contains("경로")) return "connect";
            if (title.Contains("비교") || title.Contains("승인") || title.Contains("검토") || title.Contains("저장")) return "review";
            if (title.Contains("요청") || title.Contains("첨부")) return "request";
            return "guide";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateIllustration
        /// desc         : 공용 애니메이션 그림 또는 기존 단계 그림과 정확한 예시 표시를 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 GIF 디코더·타이머 중복 구현 없이 자산 누락 대체 제공.
        ///                2026-10-19 CodexCode : Role15 안내 프레임과 보조 문구는 동적 테마 색상 사용.
        ///                2026-10-19 CodexCode : 손상 GIF는 검증한 PNG로 재시도하며 실제 인증·설치 결과와 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static FrameworkElement CreateIllustration(string key, string title, ImageSource fallback = null)
        {
            string path = Resolve(key);
            var body = new StackPanel();
            if (path != null)
            {
                var image = new MarkdownAnimatedImage { AssetBaseDirectory = GetAssetDirectory(), SourcePath = path, Stretch = Stretch.Uniform, MaxHeight = 300 };
                if (image.Source == null && String.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase))
                {
                    path = ResolveCandidate(key, true);
                    image.SourcePath = path;
                }
                if (image.Source != null)
                { AutomationProperties.SetName(image, (title ?? "안내") + " 절차 그림"); body.Children.Add(image); }
                else { image.Dispose(); path = null; }
            }
            if (path == null && fallback != null)
            {
                var image = new Image { Source = fallback, Stretch = Stretch.Uniform, MaxHeight = 210 };
                AutomationProperties.SetName(image, (title ?? "안내") + " 절차 그림"); body.Children.Add(image);
            }
            var caption = new TextBlock { Text = path != null ? "제품 UI 합성 예시 · 실제 로그인·설치 결과가 아닙니다." : fallback != null ? "절차 예시 · 단계 그림으로 순서를 확인하세요." : "그림 안내가 없는 환경입니다. 아래 상세 설명으로 순서를 확인하세요.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };
            caption.SetResourceReference(TextBlock.ForegroundProperty, "AssistantMutedForegroundBrush"); body.Children.Add(caption);
            var frame = new Border { Child = body, Padding = new Thickness(12), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 16) };
            frame.SetResourceReference(Border.BackgroundProperty, "AssistantSurfaceBrush"); frame.SetResourceReference(Border.BorderBrushProperty, "AssistantBorderBrush"); return frame;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NavigateLink
        /// desc         : 실제 링크 선택을 소유 도구 창의 명시적인 탐색으로 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 소유 창이 없으면 임의 셸 실행 없이 종료.
        ///                2026-10-19 CodexCode : 실제 호출 요소의 Dispatcher를 검증한 뒤 소유 창을 탐색.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static void NavigateLink(FrameworkElement origin, MarkdownLinkEventArgs args)
        {
            if (origin == null || args == null) return;
            origin.Dispatcher.VerifyAccess();
            for (DependencyObject current = origin; current != null; current = VisualTreeHelper.GetParent(current))
            {
                var source = current as AssistantControl;
                if (source != null) { source.NavigateMarkdownLink(args); return; }
                var dialog = current as AssistantDialog;
                if (dialog != null) { dialog.NavigateGuideLink(args); return; }
            }
            var window = Window.GetWindow(origin) as AssistantDialog;
            if (window != null) window.NavigateGuideLink(args);
        }
    }
}
