using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CompletionNotificationContent
    /// desc         : 요청 원문 없이 실제 보고된 완료 종류와 수만 Windows 알림에 표시한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK054 사용자 draft·비공개 추론·명령 원문과 알림 요약 분리.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class CompletionNotificationContent
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SafeRequestTitle
        /// desc         : 기존 호출과 호환하며 자유 문장·개인정보를 알림 제목으로 반환하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 감사 R58-01, 부분 치환 대신 요청 원문을 전부 제외.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string SafeRequestTitle(string request)
        {
            return "AI 요청";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Body
        /// desc         : CLI가 completed로 보고한 종류별 수를 표시하며 요청 원문·파일명·명령은 제외한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 작업 상태가 없으면 일반 답변 확인 문구만 표시.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string Body(string resultKind, ProgressNotice notice)
        {
            var completed = notice == null ? new ProgressStep[0] : notice.Steps.Where(s => s.State == "completed").ToArray();
            string details = String.Join(" · ", new[] {
                completed.Count(s => s.Kind == "file") > 0 ? "파일 " + completed.Count(s => s.Kind == "file") : "",
                completed.Count(s => s.Kind == "command") > 0 ? "명령 " + completed.Count(s => s.Kind == "command") : "",
                completed.Count(s => s.Kind == "tool") > 0 ? "조회·도구 " + completed.Count(s => s.Kind == "tool") : ""
            }.Where(s => s.Length > 0));
            string category = resultKind == "proposal" ? "소스 수정안 준비 완료" :
                completed.Any(s => s.Kind == "file") ? "파일 작업 완료 보고" :
                completed.Any(s => s.Kind == "command" || s.Kind == "tool") ? "실행·조회 작업 완료 보고" : "질문 답변 작성 완료";
            return category + "\n" + (details.Length > 0 ? "보고된 완료: " + details : "Visual Studio에서 답변을 확인하세요.");
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : BrandedCompletionNotification
    /// desc         : 공개 Shell 알림 API로 제공자 브랜드를 tray와 balloon 아이콘에 함께 전달한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK054 NIIF_USER·hBalloonIcon 사용, OS quiet time과 알림 정책 유지.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class BrandedCompletionNotification : NativeWindow, IDisposable
    {
        private const uint CallbackMessage = 0x8036;
        private Icon brandIcon;
        public bool Visible { get; private set; }
        public string Provider { get; private set; }
        public string BalloonTitle { get; private set; }
        public string BalloonText { get; private set; }
        public event EventHandler BalloonTipShown;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BrandedCompletionNotification
        /// desc         : 외부 파일이나 계정을 읽지 않고 VSIX 내 브랜드 자원과 전용 비표시 창을 준비한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : constructor 실패에서도 native 창·HICON·bitmap을 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal BrandedCompletionNotification(string provider)
        {
            Provider = provider == "Codex" ? "Codex" : "Claude";
            try
            {
                brandIcon = LoadBrandIcon(Provider);
                CreateHandle(new CreateParams { Caption = "AI Assistant completion", Parent = new IntPtr(-3) });
                var data = Data(0x87); // MESSAGE | ICON | TIP | SHOWTIP
                if (!ShellNotifyIcon(0, ref data)) throw new InvalidOperationException("Windows 알림 아이콘을 만들지 못했습니다.");
                Visible = true;
                data.Version = 4;
                ShellNotifyIcon(4, ref data);
            }
            catch { Dispose(); throw; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Show
        /// desc         : Windows가 알림을 허용할 때 브랜드 아이콘과 제한된 요청 요약을 표시한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : Windows 알림 억제·quiet time은 강제로 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Show(string title, string body)
        {
            if (!Visible) throw new ObjectDisposedException("BrandedCompletionNotification");
            BalloonTitle = title; BalloonText = body;
            var data = Data(0x10);
            data.InfoTitle = title; data.Info = body;
            data.InfoFlags = 0xA4; // USER | LARGE_ICON | RESPECT_QUIET_TIME
            data.BalloonIcon = brandIcon.Handle;
            if (!ShellNotifyIcon(1, ref data)) throw new InvalidOperationException("Windows 알림을 표시하지 못했습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LoadBrandIcon
        /// desc         : 내장 PNG를 현재 시스템의 큰 아이콘 크기로 만들고 임시 HICON을 즉시 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 복제한 소유 아이콘만 수명 동안 유지, 알림 생성당 GDI 누수 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Icon LoadBrandIcon(string provider)
        {
            var uri = new Uri("pack://application:,,,/VS2017.AIAssistant;component/Assets/" + provider + ".png", UriKind.Absolute);
            var resource = System.Windows.Application.GetResourceStream(uri);
            if (resource == null) throw new InvalidOperationException("브랜드 자원을 찾지 못했습니다.");
            using (var stream = resource.Stream)
            using (var source = new Bitmap(stream))
            using (var bitmap = new Bitmap(source, SystemInformation.IconSize))
            {
                IntPtr handle = bitmap.GetHicon();
                try { using (var borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Data
        /// desc         : 전용 HWND·ID만 대상으로 크기와 Unicode 문자열을 포함한 native 계약을 만든다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 다른 앱의 tray·창·설정을 조회하거나 수정하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private NotificationData Data(uint flags)
        {
            return new NotificationData { Size = (uint)Marshal.SizeOf(typeof(NotificationData)), Window = Handle,
                Id = 54, Flags = flags, Callback = CallbackMessage, Icon = brandIcon == null ? IntPtr.Zero : brandIcon.Handle,
                Tip = Provider + " · Visual Studio AI Assistant", Info = "", InfoTitle = "" };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WndProc
        /// desc         : Windows가 보낸 실제 balloon 표시 callback만 관측 이벤트로 전달한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 요청 성공과 OS 실제 표시 관측을 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == CallbackMessage && (message.LParam.ToInt64() & 0xFFFF) == 0x402)
            { var handler = BalloonTipShown; if (handler != null) handler(this, EventArgs.Empty); }
            base.WndProc(ref message);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 자기 알림과 비표시 callback 창 및 브랜드 아이콘을 중복 호출에도 안전하게 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : Unloaded·20초 수명·도구 창 종료의 동일 정리 경로.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            if (Visible) { var data = Data(0); ShellNotifyIcon(2, ref data); Visible = false; }
            if (Handle != IntPtr.Zero) DestroyHandle();
            if (brandIcon != null) { brandIcon.Dispose(); brandIcon = null; }
            BalloonTipShown = null;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NotificationData
        /// desc         : Shell NOTIFYICONDATAW의 Unicode 고정 버퍼와 포인터 크기별 필드 정렬 계약.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : x86 956byte·x64 976byte ABI를 독립 fixture에서 검증.
        /// </summary>
        /// ---------------------------------------------------------------------
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotificationData
        {
            public uint Size; public IntPtr Window; public uint Id, Flags, Callback; public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
            public uint State, StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
            public uint Version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
            public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ShellNotifyIcon
        /// desc         : 전용 callback 창의 알림 항목만 추가·표시·삭제하는 공개 Windows API.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 인증·알림 정책·다른 프로그램의 항목을 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShellNotifyIcon(uint operation, ref NotificationData data);
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DestroyIcon
        /// desc         : Bitmap.GetHicon에서 소유한 임시 native 아이콘만 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : 시스템 공유 아이콘의 수명을 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);
    }
}
