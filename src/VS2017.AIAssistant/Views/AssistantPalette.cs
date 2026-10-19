using System;
using System.Windows;
using System.Windows.Media;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantPalette
    /// desc         : 공유 리소스의 밝은·어두운 팔레트를 교체하여 열린 화면과 팝업의 동적 색을 함께 갱신한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : Role 추가15의 흰색·회색과 옅은 보조색 및 의미별 대비 제공.
    ///                2026-10-19 CodexOrig4 : 정책과 팔레트 밝기 판별 기준 통일 및 양 모드 대비 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class AssistantPalette
    {
        private static readonly string[] SemanticPairs = {
            "AssistantBackgroundBrush:171717:F7F8FA", "AssistantForegroundBrush:EBEBEB:20252B",
            "AssistantSurfaceBrush:242424:FFFFFF", "AssistantBorderBrush:424242:C7CDD4",
            "AssistantMutedForegroundBrush:B6B6B6:505B67", "AssistantLinkBrush:79C5F5:165C98",
            "AssistantPopupBackgroundBrush:1D1E20:FFFFFF", "AssistantPopupBorderBrush:52555C:B8C2CE",
            "AssistantDialogBorderBrush:7A7F87:737B86",
            "AssistantCodeBackgroundBrush:24282F:EEF2F7", "AssistantSelectionBrush:35414F:DCE9F6",
            "AssistantAddedBrush:57CC8A:19743C", "AssistantRemovedBrush:FF7373:B32632",
            "AssistantWarningBrush:EDA359:8C4A0D", "AssistantErrorBrush:FFA07A:A52D22",
            "AssistantOverlayBrush:B8000000:A6454F5E",
            "AssistantControlBackgroundBrush:282828:F0F2F5", "AssistantHoverBrush:373B43:E4EBF3",
            "AssistantPressedBrush:414650:D3DFED", "AssistantDisabledForegroundBrush:8E8E8E:626E7A",
            "AssistantPrimaryBrush:D0D0D0:354C65", "AssistantOnPrimaryBrush:141414:FFFFFF",
            "AssistantPrimaryHoverBrush:EEEEEE:29415B" };
        // 기존 XAML의 각 톤을 이름 있는 동적 자원으로 보존한다. 투명도는 양쪽 모드에서 유지한다.
        private static readonly string[] Tones = {
            "141414", "171717", "19473F", "1E1E1E", "202020", "202226", "213D58", "22262C", "232323", "242424", "252525", "25292F", "282828", "292929", "2B2B2B", "303030", "35414F", "373737", "383838", "393939", "3A3A3A", "3A4655", "3D3D3D", "404040", "414141", "414B59", "424242", "424A56", "444444", "454545", "454E5A", "464646", "477CAA", "479B87", "4A4A4A", "505050", "535353", "555555", "57CC8A", "5B5B5B", "606060", "666666", "6B6B6B", "79C5F5", "80AFFF", "888888", "8BC9F3", "8E8E8E", "8E9CF1", "9A9A9A", "9CD8FF", "A3A3A3", "A7B6FF", "A8A8A8", "AAAAAA", "B0B0B0", "B1B1B1", "B6B6B6", "BDE7FF", "C8CFFF", "CBCBCB", "CFCFCF", "D0D0D0", "D1FFF0", "D4D9E0", "D5D5D5", "D5ECFF", "D8D8D8", "DDDDDD", "EB333333", "EBEBEB", "EDA359", "EEEEEE", "EFEFEF", "F0F0F0", "F2F2F2", "F2F7FF", "FF7373", "FFFFFF" };

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Apply
        /// desc         : 해당 창의 사전만 갱신하며 Visual Studio 또는 Windows의 전역 테마는 변경하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 상위 IDE가 제공한 실제 배경·글자색을 자동 모드에서 우선 적용.
        ///                2026-10-19 CodexOrig4 : 문서 재렌더링을 유발하는 배경·본문은 보조 자원 갱신 후 게시해 혼합 팔레트 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void Apply(ResourceDictionary resources, bool light, Color? hostBackground = null, Color? hostForeground = null)
        {
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            foreach (string pair in SemanticPairs)
            {
                string[] values = pair.Split(':');
                if (values[0] == "AssistantBackgroundBrush" || values[0] == "AssistantForegroundBrush") continue;
                resources[values[0]] = Paint(Parse(values[light ? 2 : 1]));
            }
            foreach (string tone in Tones)
            {
                Color original = Parse(tone);
                resources["AssistantTone" + tone + "Brush"] = Paint(light ? LightTone(original) : original);
            }
            // Markdown/비교 문서가 이 두 자원의 변경에서 새 문서를 만든다. 나머지 색이 준비된 뒤 알린다.
            resources["AssistantBackgroundBrush"] = Paint(hostBackground ?? Parse(light ? "F7F8FA" : "171717"));
            resources["AssistantForegroundBrush"] = Paint(hostForeground ?? Parse(light ? "20252B" : "EBEBEB"));
        }

        /// <summary>
        /// Apply extension-local accent presets. These resources never change the Visual Studio IDE theme.
        /// </summary>
        public static void ApplyPreset(ResourceDictionary resources, string preset, bool light)
        {
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            if (String.Equals(preset, "kakao", StringComparison.OrdinalIgnoreCase))
            {
                Set(resources, "AssistantBackgroundBrush", light ? "F5F6F8" : "202124");
                Set(resources, "AssistantForegroundBrush", light ? "191919" : "F1F3F4");
                Set(resources, "AssistantSurfaceBrush", light ? "FFFFFF" : "2B2D30");
                Set(resources, "AssistantBorderBrush", light ? "D9DDE3" : "45484D");
                Set(resources, "AssistantMutedForegroundBrush", light ? "5F6368" : "B8BCC2");
                Set(resources, "AssistantLinkBrush", light ? "1558A6" : "8AB4F8");
                Set(resources, "AssistantPopupBackgroundBrush", light ? "FFFFFF" : "242629");
                Set(resources, "AssistantPopupBorderBrush", light ? "C7CDD4" : "50545A");
                Set(resources, "AssistantDialogBorderBrush", light ? "737B86" : "7A7F87");
                Set(resources, "AssistantCodeBackgroundBrush", light ? "F0F2F5" : "292B2F");
                Set(resources, "AssistantSelectionBrush", light ? "FFF4A8" : "554D1B");
                Set(resources, "AssistantAddedBrush", light ? "19743C" : "57CC8A");
                Set(resources, "AssistantRemovedBrush", light ? "B32632" : "FF7373");
                Set(resources, "AssistantWarningBrush", light ? "8C4A0D" : "EDA359");
                Set(resources, "AssistantErrorBrush", light ? "A52D22" : "FFA07A");
                Set(resources, "AssistantControlBackgroundBrush", light ? "F0F1F3" : "303236");
                Set(resources, "AssistantHoverBrush", light ? "E8EAED" : "3A3D42");
                Set(resources, "AssistantPressedBrush", light ? "DADCE0" : "45484D");
                Set(resources, "AssistantPrimaryBrush", "FEE500");
                Set(resources, "AssistantOnPrimaryBrush", "191919");
                Set(resources, "AssistantPrimaryHoverBrush", "F4D900");
                return;
            }
            if (!String.Equals(preset, "dracula", StringComparison.OrdinalIgnoreCase)) return;

            Set(resources, "AssistantBackgroundBrush", "282A36");
            Set(resources, "AssistantForegroundBrush", "F8F8F2");
            Set(resources, "AssistantSurfaceBrush", "343746");
            Set(resources, "AssistantBorderBrush", "52566A");
            Set(resources, "AssistantMutedForegroundBrush", "B9B9C4");
            Set(resources, "AssistantLinkBrush", "8BE9FD");
            Set(resources, "AssistantPopupBackgroundBrush", "2D2F3C");
            Set(resources, "AssistantPopupBorderBrush", "6272A4");
            Set(resources, "AssistantDialogBorderBrush", "7782A0");
            Set(resources, "AssistantCodeBackgroundBrush", "21222C");
            Set(resources, "AssistantSelectionBrush", "44475A");
            Set(resources, "AssistantAddedBrush", "50FA7B");
            Set(resources, "AssistantRemovedBrush", "FF5555");
            Set(resources, "AssistantWarningBrush", "FFB86C");
            Set(resources, "AssistantErrorBrush", "FF5555");
            Set(resources, "AssistantControlBackgroundBrush", "383A49");
            Set(resources, "AssistantHoverBrush", "44475A");
            Set(resources, "AssistantPressedBrush", "50536A");
            Set(resources, "AssistantPrimaryBrush", "BD93F9");
            Set(resources, "AssistantOnPrimaryBrush", "282A36");
            Set(resources, "AssistantPrimaryHoverBrush", "D0AEFF");
            foreach (string tone in Tones)
            {
                var original = Parse(tone);
                string source = original.ToString().Substring(3);
                string replacement = null;
                switch (source)
                {
                    case "79C5F5": case "80AFFF": case "8BC9F3": case "9CD8FF": case "BDE7FF": replacement = "8BE9FD"; break;
                    case "57CC8A": replacement = "50FA7B"; break;
                    case "FF7373": case "FFA07A": replacement = "FF5555"; break;
                    case "EDA359": replacement = "FFB86C"; break;
                    case "8E9CF1": case "A7B6FF": case "C8CFFF": replacement = "BD93F9"; break;
                    case "D1FFF0": replacement = "50FA7B"; break;
                }
                if (replacement != null) { var color = Parse(replacement); color.A = original.A; resources["AssistantTone" + tone + "Brush"] = Paint(color); }
            }
        }

        private static void Set(ResourceDictionary resources, string key, string hex)
        { resources[key] = Paint(Parse(hex)); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsLight
        /// desc         : 실제 배경의 상대 휘도로 밝은 표면 여부를 판별한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 테마 이름이나 Windows 모드보다 IDE가 준 색을 우선한다.
        ///                2026-10-19 CodexOrig4 : 공통 정책을 호출하여 자동 모드와 같은 판별 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static bool IsLight(Color color)
        { return !AssistantThemePolicy.IsDark(color); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Channel
        /// desc         : sRGB 한 채널을 상대 휘도 계산용 선형 값으로 변환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 단순 RGB 합산으로 회색 대비가 뒤바뀌는 경계 방지.
        ///                2026-10-19 CodexOrig4 : 기존 설명 이력을 보존하며 실제 판별은 공통 정책으로 이관.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static double Channel(byte value)
        { double level = value / 255.0; return level <= .04045 ? level / 12.92 : Math.Pow((level + .055) / 1.055, 2.4); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LightTone
        /// desc         : 기존 톤과 의미 색을 밝은 화면에서 읽히는 회색·옅은 보조색으로 대응한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 상태 초록·빨강과 링크·설치 버튼의 의미 및 대비 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Color LightTone(Color color)
        {
            string key = color.ToString().Substring(3);
            string mapped = null;
            switch (key)
            {
                case "141414": mapped = "FFFFFF"; break;
                case "171717": mapped = "F7F8FA"; break;
                case "19473F": mapped = "E0F3EB"; break;
                case "213D58": mapped = "E1EEF9"; break;
                case "22262C": case "202226": case "25292F": mapped = "F2F5F9"; break;
                case "35414F": case "3A4655": mapped = "DCE9F6"; break;
                case "414B59": case "424A56": case "454E5A": mapped = "C2CCD8"; break;
                case "477CAA": mapped = "8AACCC"; break;
                case "479B87": mapped = "8DBBAA"; break;
                case "57CC8A": mapped = "19743C"; break;
                case "FF7373": mapped = "B32632"; break;
                case "EDA359": mapped = "8C4A0D"; break;
                case "79C5F5": case "80AFFF": case "8BC9F3": case "9CD8FF": case "BDE7FF": mapped = "165C98"; break;
                case "8E9CF1": case "A7B6FF": case "C8CFFF": mapped = "637CC6"; break;
                case "D1FFF0": mapped = "215B49"; break;
                case "D4D9E0": case "F2F7FF": mapped = "263849"; break;
                case "D5ECFF": mapped = "23445F"; break;
                case "D0D0D0": mapped = "354C65"; break;
            }
            if (mapped != null) { Color result = Parse(mapped); result.A = color.A; return result; }
            int value = color.R < 128 ? 255 - color.R / 2 : 255 - color.R;
            return Color.FromArgb(color.A, (byte)value, (byte)value, (byte)value);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Parse
        /// desc         : 코드 내부의 유한한 색 정의만 변환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 사용자 문자열을 XAML이나 리소스로 실행하지 않는다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Color Parse(string value) { return (Color)ColorConverter.ConvertFromString("#" + value); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Paint
        /// desc         : 각 리소스 변경에서 새 불변 브러시를 만들어 다른 창의 색과 분리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 동적 리소스 교체가 열린 Popup과 문서까지 전파되도록 한다.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static SolidColorBrush Paint(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }
    }
}
