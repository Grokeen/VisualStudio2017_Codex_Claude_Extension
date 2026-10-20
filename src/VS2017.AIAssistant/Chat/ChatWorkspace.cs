using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;

namespace VS2017.AIAssistant.Chat
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ChatAttachment
    /// desc         : 첨부 시점의 텍스트/이미지를 보관하여 이후 파일 변경과 분리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 제한된 크기·형식 검사와 미리보기 추가.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    ///                2026-10-20 CodexOrig : TASK069 텍스트 첨부 종류 아이콘과 실제 본문 분량·재사용 도움말.
    /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ChatAttachment
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FromText
        /// desc         : 파일 쓰기 없이 권한을 받아 읽은 스키마 등 텍스트 참고 자료를 첨부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 메모리 텍스트 첨부와 크기 검증 추가.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static ChatAttachment FromText(string text, string name)
        {
            if (text == null || Encoding.UTF8.GetByteCount(text) > 102400) throw new ArgumentException("텍스트 첨부는 100KB 이하입니다.");
            return new ChatAttachment { Name = Path.GetFileName(name ?? "reference.txt"), Text = text, MediaType = "text/plain" };
        }
        public string Name { get; private set; }
        public string Text { get; private set; }
        public string ImageBase64 { get; private set; }
        public string MediaType { get; private set; }
        public BitmapSource Thumbnail { get; private set; }
        public bool IsImage { get { return ImageBase64 != null; } }
        public bool IsDocument { get; private set; }
        public string Label { get { return Name + (IsImage ? " · 이미지" : IsDocument ? " · 문서 본문" : " · 텍스트"); } }
        public string IconText { get { return AttachmentDisplayInfo.IconText(this); } }
        public string DisplayInfo { get { return AttachmentDisplayInfo.Tooltip(this); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FromFile
        /// desc         : 파일은 실행하지 않고 최대 2MB 이미지 또는 100KB 텍스트로 읽는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 디렉터리·바이너리·대용량 입력 거부.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static ChatAttachment FromFile(string path)
        {
            if (IsDocumentPath(path))
            {
                var document = FromText(DocumentAttachmentReader.Extract(path), Path.GetFileName(path));
                document.IsDocument = true;
                return document;
            }
            string extension = Path.GetExtension(path).ToLowerInvariant();
            bool image = extension == ".png" || extension == ".jpg" || extension == ".jpeg";
            int limit = image ? 2 * 1024 * 1024 : 100 * 1024;
            byte[] bytes;
            using (var input = File.OpenRead(path))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                int count;
                while ((count = input.Read(buffer, 0, Math.Min(buffer.Length, limit + 1 - (int)output.Length))) > 0)
                {
                    output.Write(buffer, 0, count);
                    if (output.Length > limit) throw new InvalidOperationException("첨부 제한: 이미지 2MB, 텍스트 파일 100KB입니다.");
                }
                bytes = output.ToArray();
            }
            if (image) return FromImage(bytes, Path.GetFileName(path));
            string text;
            try
            {
                using (var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false, true), true))
                    text = reader.ReadToEnd();
            }
            catch (DecoderFallbackException) { throw new InvalidOperationException("UTF-8 또는 BOM이 있는 Unicode 텍스트 파일을 첨부하세요."); }
            if (text.IndexOf('\0') >= 0 || text.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
                throw new InvalidOperationException("이 파일은 텍스트로 읽을 수 없습니다. PNG/JPEG 또는 텍스트 파일을 첨부하세요.");
            return new ChatAttachment { Name = Path.GetFileName(path), Text = text };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FromFileAsync
        /// desc         : 문서 추출을 취소 가능하게 기다리고 UI 스레드에서 파일을 읽지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 본문 첨부와 비동기 읽기 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static async Task<ChatAttachment> FromFileAsync(string path, CancellationToken cancellationToken)
        {
            if (!IsDocumentPath(path)) return await Task.Run(() => FromFile(path), cancellationToken);
            string text = await DocumentAttachmentReader.ExtractAsync(path, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var attachment = FromText(text, Path.GetFileName(path)); attachment.IsDocument = true;
            return attachment;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsDocumentPath
        /// desc         : 문서 추출과 기존 소스·텍스트 읽기 제한을 구분한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 구조화 문서만 추출 서비스로 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsDocumentPath(string path)
        { return new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".odt", ".odp", ".ods", ".rtf", ".htm", ".html" }.Contains(Path.GetExtension(path).ToLowerInvariant()); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FromImage
        /// desc         : 파일/클립보드 이미지를 검증하고 독립된 작은 미리보기를 만든다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : PNG/JPEG 및 1600만 픽셀 제한.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static ChatAttachment FromImage(byte[] bytes, string name)
        {
            if (bytes == null || bytes.Length < 8 || bytes.Length > 2 * 1024 * 1024)
                throw new InvalidOperationException("이미지는 PNG/JPEG, 최대 2MB로 첨부하세요.");
            string mime = bytes.Take(8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ? "image/png" :
                (bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? "image/jpeg" : null);
            if (mime == null) throw new InvalidOperationException("지원하는 이미지는 PNG/JPEG입니다.");
            using (var stream = new MemoryStream(bytes))
            {
                var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                if ((long)frame.PixelWidth * frame.PixelHeight > 16000000)
                    throw new InvalidOperationException("이미지는 1600만 픽셀 이하로 첨부하세요.");
                stream.Position = 0;
                var thumbnail = new BitmapImage();
                thumbnail.BeginInit(); thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                thumbnail.DecodePixelWidth = 96; thumbnail.StreamSource = stream; thumbnail.EndInit(); thumbnail.Freeze();
                return new ChatAttachment { Name = name, MediaType = mime, ImageBase64 = Convert.ToBase64String(bytes), Thumbnail = thumbnail };
            }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ChatMessage
    /// desc         : 화면의 사용자/AI 메시지와 후속 요청에 사용할 대화 데이터.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 원문 프롬프트와 응답을 메모리에 누적.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    ///                2026-10-20 CodexCode : 시간별 기록을 위한 메시지 식별자·시각과 선택지 제공.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
        public string Role { get; set; }
        public string Text { get; set; }
        public string Speaker { get { return Role == "user" ? "나" : Role == "assistant" ? "AI" : (Text ?? "").StartsWith("⌘", StringComparison.Ordinal) ? "명령·테스트" : (Text ?? "").StartsWith("✎", StringComparison.Ordinal) ? "소스 변경" : (Text ?? "").StartsWith("⌕", StringComparison.Ordinal) ? "조회·도구" : "안내"; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Choices
        /// desc         : AI가 사용자 선택을 요청하며 제시한 마지막 번호 목록만 버튼 후보로 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexCode : 영어 선택 동사를 단어 단위로 판정해 완료 설명의 selected를 제외.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string[] Choices
        {
            get
            {
                if (Role != "assistant" || String.IsNullOrEmpty(Text) || Text.Length > 64000) return new string[0];
                var lines = Text.Replace("\r\n", "\n").Split('\n');
                var choices = new System.Collections.Generic.List<string>();
                int last = lines.Length - 1;
                while (last >= 0 && String.IsNullOrWhiteSpace(lines[last])) last--;
                if (last < 0) return new string[0];
                int first = last;
                for (; first >= 0; first--)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(lines[first].Trim(), @"^([1-5])[.)]\s+(.{1,160})$");
                    if (!match.Success) break;
                    choices.Insert(0, match.Groups[2].Value.Trim());
                    if (choices.Count > 5) return new string[0];
                }
                if (choices.Count < 2) return new string[0];
                int start = first + 1;
                for (int number = 0; number < choices.Count; number++)
                    if (lines[start + number].TrimStart()[0] != (char)('1' + number)) return new string[0];
                int prompt = first;
                while (prompt >= 0 && String.IsNullOrWhiteSpace(lines[prompt])) prompt--;
                if (prompt < 0) return new string[0];
                string question = lines[prompt].Trim();
                bool asksChoice = System.Text.RegularExpressions.Regex.IsMatch(question,
                    @"(?:선택(?:해|하여|해주|해 주|하세요)|골라(?:주|주세요)|고르(?:세요|십시오)|어느\s+것으로\s+진행할까요|\b(?:choose|select)\b|\bwhich\s+option\b)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase) &&
                    (question.EndsWith("?", StringComparison.Ordinal) || question.EndsWith(".", StringComparison.Ordinal) ||
                     question.EndsWith(":", StringComparison.Ordinal) || question.EndsWith("요", StringComparison.Ordinal));
                if (!asksChoice) return new string[0];
                bool fenced = false;
                for (int index = 0; index <= last; index++)
                    if (lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal)) fenced = !fenced;
                return fenced ? new string[0] : choices.ToArray();
            }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ChatSession
    /// desc         : Provider별 대화·첨부·작성 중 프롬프트를 분리한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 탭 전환 시 대화 보존 및 100개 메시지 상한.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ChatSession
    {
        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();
        public ObservableCollection<ChatAttachment> Attachments { get; } = new ObservableCollection<ChatAttachment>();
        public string Draft { get; set; } = "";

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Add
        /// desc         : 새 메시지를 아래쪽에 누적하고 가장 오래된 메시지를 제한한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 장시간 사용의 무제한 메모리 증가 방지.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Add(string role, string text)
        {
            if (role == "system" && Messages.Count > 0 && Messages.Last().Role == role && Messages.Last().Text == text) return;
            Messages.Add(new ChatMessage { Role = role, Text = text });
            while (Messages.Count > 100) Messages.RemoveAt(0);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ChatRequestContext
    /// desc         : 요청 시점의 instruction·최근 대화·첨부·모델을 고정한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : UI 변경과 독립된 Provider 컨텍스트.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
    ///                2026-10-18 CodexOrig : TASK-020 신규 모델이 광고한 안전한 강도 토큰 수용.
    ///                2026-10-18 CodexOrig : 필수 규칙을 요청 경계에서만 조합하고 사용자 규칙 원문 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ChatRequestContext
    {
        public string Instructions { get; private set; }
        public string EffectiveInstructions { get { return MandatoryInstructionStore.Compose(Instructions); } }
        public string Model { get; private set; }
        public ChatMessage[] History { get; private set; }
        public ChatAttachment[] Attachments { get; private set; }
        public string ReasoningEffort { get; private set; }
        public string Speed { get; private set; }
        public string AgentContext { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChatRequestContext
        /// desc         : 입력 제한을 확인하고 최근 대화의 값 복사본을 만든다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 최신 10개/24000자 대화 및 첨부 4개 제한.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
        ///                2026-10-18 CodexOrig : TASK-020 신규 모델이 광고한 안전한 강도 토큰 수용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ChatRequestContext(string instructions = "", string model = "", ChatMessage[] history = null, ChatAttachment[] attachments = null, string reasoningEffort = "auto", string speed = "standard", string agentContext = "")
        {
            if (reasoningEffort == null || !System.Text.RegularExpressions.Regex.IsMatch(reasoningEffort, @"\A[a-z][a-z0-9_-]{0,31}\z")) throw new ArgumentException("지원하지 않는 생각 모드입니다.");
            if (speed != "standard" && speed != "fast") throw new ArgumentException("지원하지 않는 속도입니다.");
            AgentContext = agentContext ?? "";
            if (AgentContext.Length > 8000) throw new ArgumentException("AI 역할·협업 문맥은 8000자 이하입니다.");
            Speed = speed;
            ReasoningEffort = reasoningEffort;
            Instructions = instructions ?? ""; Model = (model ?? "").Trim();
            if (Instructions.Length > 8000 || Model.Length > 120 || Model.Any(char.IsControl))
                throw new ArgumentException("instruction은 8000자, 모델 이름은 120자 이하로 입력하세요.");
            Attachments = (attachments ?? new ChatAttachment[0]).ToArray();
            if (Attachments.Length > 4 || Attachments.Any(a => a == null)) throw new ArgumentException("첨부는 최대 4개입니다.");
            var recent = (history ?? new ChatMessage[0]).Where(m => m != null && (m.Role == "user" || m.Role == "assistant")).Reverse().Take(10).ToList();
            int count = 0;
            History = recent.TakeWhile(m => (count += (m.Text ?? "").Length) <= 24000).Reverse()
                .Select(m => new ChatMessage { Role = m.Role, Text = m.Text ?? "" }).ToArray();
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantSettings
    /// desc         : 인증 토큰 없이 CLI 경로·모델·instruction·글자 설정을 저장한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : Provider별 기본 설정 추가.
    /// update date  : 2026-10-18 CodexOrig : 이전 설정과 호환되는 첫 사용 안내 표시 선택 추가.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    /// update date  : 2026-10-18 CodexOrig : TASK-014 대화 목록 폭·표시 여부를 이전 설정과 호환되게 보관.
    /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
    ///                2026-10-18 CodexOrig : TASK-020 명시적인 네이티브 전체 액세스 설정 추가.
    ///                2026-10-19 CodexOrig4 : 화면 테마 auto/light/dark 설정을 이전 파일과 호환되게 보존.
    ///                2026-10-19 CodexCode : TASK049 알림 선택과 확장 테마를 이전 JSON 기본값과 호환되게 보관.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class AssistantSettings
    {
        public string CodexPath { get; set; } = "";
        public string ClaudePath { get; set; } = "";
        public string CodexModel { get; set; } = "";
        public string ClaudeModel { get; set; } = "";
        public string Instructions { get; set; } = "";
        public double FontSize { get; set; } = 14;
        // TASK-024 CodexCode : 비밀 없는 표시 선택만 저장하며 이전 JSON은 IDE 우선 자동 모드를 따른다.
        public string ThemeMode { get; set; } = "auto";
        // TASK049 CodexCode : 이전 설정에는 두 키가 없으므로 알림 사용·기본 VS 팔레트로 복원한다.
        public bool EnableCompletionNotifications { get; set; } = true;
        public string VisualThemePreset { get; set; } = "vs";
        // 이전 설정 파일에 항목이 없으면 첫 실행 안내를 표시한다.
        public bool HideWelcome { get; set; }
        public string CodexEffort { get; set; } = "auto";
        public string ClaudeEffort { get; set; } = "auto";
        public string CodexSpeed { get; set; } = "standard";
        public string ClaudeSpeed { get; set; } = "standard";
        public bool AllowSourceControl { get; set; }
        public bool AllowDatabaseSchema { get; set; }
        public bool AllowDeployment { get; set; }
        public bool AgentFullAccess { get; set; }
        public double HistoryWidth { get; set; } = 220;
        public bool ShowHistory { get; set; } = true;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantSettingsStore
    /// desc         : 사용자 설정을 UTF-8 JSON으로 원자적으로 읽고 쓴다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 손상/대용량 설정 거부와 테스트 경로 격리.
    /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class AssistantSettingsStore
    {
        public string FilePath { get; private set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssistantSettingsStore
        /// desc         : 기본 로컬 사용자 경로나 명시한 격리 경로를 사용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : IDE/독립 테스트의 개인 설정 분리.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        public AssistantSettingsStore(string path = null)
        {
            FilePath = Path.GetFullPath(path ?? Environment.GetEnvironmentVariable("VS2017_AI_SETTINGS_PATH") ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VS2017AIAssistant", "settings.json"));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Load
        /// desc         : 기존 설정이 없으면 기본값을 반환하고 손상된 값은 오류로 전달한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : UI가 기본 설정으로 복구할 수 있도록 검사.
        /// update date  : 2026-10-18 CodexCode : 설정 거래 Mutex로 중간 상태 읽기를 차단하고 BOM 호환 UTF-8을 최대 64KB만 읽음.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        public AssistantSettings Load()
        {
            using (AssistantSettingsTransaction.AcquireSettingsLock(FilePath))
            {
                if (!File.Exists(FilePath)) return new AssistantSettings();
                using (var input = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var memory = new MemoryStream())
                {
                    if (input.Length > 65536) throw new InvalidDataException("설정 파일은 UTF-8 기준 64KB 이하만 읽을 수 있습니다.");
                    byte[] buffer = new byte[4096]; int count;
                    while ((count = input.Read(buffer, 0, Math.Min(buffer.Length, 65537 - (int)memory.Length))) > 0)
                    {
                        memory.Write(buffer, 0, count);
                        if (memory.Length > 65536) throw new InvalidDataException("설정 파일은 UTF-8 기준 64KB 이하만 읽을 수 있습니다.");
                    }
                    byte[] bytes = memory.ToArray(); int offset = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 ? 3 : 0;
                    AssistantSettings settings;
                    try { settings = new JavaScriptSerializer { MaxJsonLength = 65536, RecursionLimit = 16 }.Deserialize<AssistantSettings>(new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset)); }
                    catch (Exception error) when (error is DecoderFallbackException || error is ArgumentException || error is InvalidOperationException)
                    { throw new InvalidDataException("설정 파일의 UTF-8 또는 JSON 형식이 올바르지 않습니다.", error); }
                    Validate(settings); return settings;
                }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Save
        /// desc         : 명시적 저장 버튼에서만 임시 파일 교체로 설정을 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : 중간 실패 시 기존 파일 유지 및 임시 파일 정리.
        /// update date  : 2026-10-18 CodexCode : 설정 거래와 같은 Mutex를 사용하고 직렬화 UTF-8 64KB 초과를 쓰기 전에 거부.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Save(AssistantSettings settings)
        {
            using (AssistantSettingsTransaction.AcquireSettingsLock(FilePath))
            {
                Validate(settings);
                byte[] bytes = new UTF8Encoding(false, true).GetBytes(new JavaScriptSerializer().Serialize(settings));
                if (bytes.Length > 65536) throw new InvalidDataException("설정 파일은 UTF-8 기준 64KB 이하로 저장해야 합니다.");
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllBytes(temp, bytes);
                    MarkdownWorkspaceStore.CommitTemporaryFile(temp, FilePath);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Validate
        /// desc         : 비정상 글자 크기·누락 문자열·과도한 설정을 거부한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : JSON으로 주입된 범위 밖 값 검사.
        /// update date  : 2026-10-18 CodexCode : v0.5 대화·입력·프로젝트 작업 흐름 연결 및 검토.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 대화 목록 폭의 유한 범위 검사 추가.
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
        ///                2026-10-19 CodexOrig4 : 테마 모드의 허용값을 검사하여 잘못된 저장값을 거부.
        ///                2026-10-19 CodexCode : TASK049 확장 테마 선택의 세 값만 허용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Validate(AssistantSettings settings)
        {
            if (settings == null || double.IsNaN(settings.HistoryWidth) || settings.HistoryWidth < 140 || settings.HistoryWidth > 420 || double.IsNaN(settings.FontSize) || settings.FontSize < 10 || settings.FontSize > 28 ||
                settings.Instructions == null || settings.Instructions.Length > 8000 ||
                !new[] { "auto", "light", "dark" }.Contains(settings.ThemeMode) ||
                !new[] { "vs", "kakao", "dracula" }.Contains(settings.VisualThemePreset) ||
                !new[] { "standard", "fast" }.Contains(settings.CodexSpeed) || !new[] { "standard", "fast" }.Contains(settings.ClaudeSpeed) ||
                new[] { settings.CodexPath, settings.ClaudePath, settings.CodexModel, settings.ClaudeModel }.Any(s => s == null || s.Length > 4096))
                throw new InvalidDataException("저장된 설정 형식이나 값이 올바르지 않습니다.");
        }
    }
}
