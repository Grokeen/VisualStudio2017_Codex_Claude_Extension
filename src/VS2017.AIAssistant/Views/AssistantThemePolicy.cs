using System;
using System.Windows.Media;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantThemePolicy
    /// desc         : 사용자 선택과 IDE·Windows 테마의 우선순위를 외부 상태 변경 없이 결정한다.
    /// author       : CodexOrig4
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig4 : 요청15의 명시 선택 우선·VS 우선 자동·Windows 대체 정책 추가, 기존 소문자 저장 계약 유지.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class AssistantThemePolicy
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NormalizeMode
        /// desc         : 이전 설정의 누락 값과 알 수 없는 값은 안전한 자동 모드로 해석한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 문화권과 무관한 Auto·Light·Dark 표준값 반환.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string NormalizeMode(string mode)
        {
            if (string.Equals(mode, "light", StringComparison.OrdinalIgnoreCase)) return "light";
            if (string.Equals(mode, "dark", StringComparison.OrdinalIgnoreCase)) return "dark";
            return "auto";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Resolve
        /// desc         : 명시 모드가 없을 때 불투명 IDE 배경을 먼저 판별하고 Windows 앱 테마로 대체한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 밝은 VS/어두운 Windows 및 그 반대 조합의 우선순위 보장.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static string Resolve(string mode, Color? visualStudioBackground, bool windowsUsesLight)
        {
            string normalized = NormalizeMode(mode);
            if (normalized != "auto") return normalized;
            if (visualStudioBackground.HasValue && visualStudioBackground.Value.A == 255)
                return IsDark(visualStudioBackground.Value) ? "dark" : "light";
            return windowsUsesLight ? "light" : "dark";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsDark
        /// desc         : 색상 이름이나 테마 GUID 대신 실제 도구 창 배경의 밝기를 판별한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 사용자 지정/Blue 테마도 실제 배경에 따라 같은 기준 적용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static bool IsDark(Color background)
        {
            return 0.2126 * LinearChannel(background.R) + 0.7152 * LinearChannel(background.G) + 0.0722 * LinearChannel(background.B) <= 0.42;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LinearChannel
        /// desc         : sRGB 색을 선형 휘도로 변환하여 팔레트와 자동 판별의 기준을 통일한다.
        /// author       : CodexOrig4
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig4 : 채널 계산을 공통 정책 한 곳으로 모음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static double LinearChannel(byte channel)
        {
            double value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
    }
}
