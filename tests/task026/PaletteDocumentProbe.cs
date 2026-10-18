using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : PaletteDocumentProbe
/// desc         : 본문/배경 변경 때 문서를 다시 만드는 WPF 소비자에게 완성된 팔레트가 전달되는지 관찰한다.
/// author       : CodexOrig4
/// create date  : 2026-10-19
/// update date  : 2026-10-19 CodexOrig4 : 실제 제품에서 발견한 코드/링크 혼합 팔레트의 게시 경계 회귀.
/// </summary>
/// ---------------------------------------------------------------------
public sealed class PaletteDocumentProbe : Control
{
    public Color CachedCode { get; private set; }
    public Color CachedLink { get; private set; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : PaletteDocumentProbe
    /// desc         : 같은 사전을 참조하고 일반 WPF 동적 자원의 변경을 관찰한다.
    /// author       : CodexOrig4
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig4 : 창 표시나 제품 문서 파서 대체 없이 게시 순서만 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    public PaletteDocumentProbe(ResourceDictionary resources)
    {
        Resources.MergedDictionaries.Add(resources);
        SetResourceReference(BackgroundProperty, "AssistantBackgroundBrush");
        SetResourceReference(ForegroundProperty, "AssistantForegroundBrush");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : OnPropertyChanged
    /// desc         : WPF가 문서 갱신을 알리는 시점에 코드와 링크 자원을 함께 기록한다.
    /// author       : CodexOrig4
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig4 : 최종 사전 값만 검사해서 놓치는 이미 렌더된 색상 문제를 재현.
    /// </summary>
    /// ---------------------------------------------------------------------
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (args.Property != BackgroundProperty && args.Property != ForegroundProperty) return;
        var code = TryFindResource("AssistantCodeBackgroundBrush") as SolidColorBrush;
        var link = TryFindResource("AssistantLinkBrush") as SolidColorBrush;
        if (code != null) CachedCode = code.Color;
        if (link != null) CachedLink = link.Color;
    }
}
