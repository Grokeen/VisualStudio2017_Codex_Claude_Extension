using System;
using System.Linq;
using System.Windows.Controls;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ProductUi
/// desc         : AI 탭 순서·브랜드 헤더 표현과 독립적으로 실제 Provider를 선택한다.
/// author       : CodexOrig
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexOrig : Claude 우선 탭에 맞춰 회귀/IDE의 인덱스 가정 제거.
///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
/// </summary>
/// ---------------------------------------------------------------------
internal static class ProductUi
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SelectProvider
    /// desc         : XAML의 Provider Tag를 검사하고 해당 실제 탭을 선택한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 없는 Provider는 명시적 테스트 오류로 보고.
    ///                2026-10-18 CodexOrig : TASK-020 최종 버전·선택·전체 액세스 검사 계약 반영.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static void SelectProvider(AssistantControl control, string provider)
    {
        var tabs = (TabControl)control.FindName("ProviderChoice");
        var item = tabs.Items.OfType<TabItem>().SingleOrDefault(tab => Equals(tab.Tag, provider));
        if (item == null) throw new InvalidOperationException("Provider tab not found: " + provider);
        tabs.SelectedItem = item;
        string internalProvider = Convert.ToString(typeof(AssistantControl).GetField("selectedProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(control));
        if (!ReferenceEquals(tabs.SelectedItem, item) || internalProvider != provider)
            throw new InvalidOperationException("Provider selection failed: requested=" + provider + ", selected=" + Convert.ToString((tabs.SelectedItem as TabItem)?.Tag) + ", internal=" + internalProvider + ", enabled=" + tabs.IsEnabled + ", status=" + ((TextBlock)control.FindName("StatusLabel")).Text);
    }
}
