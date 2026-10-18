using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace VS2017.AIAssistant.Chat
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConversationRecord
    /// desc         : 한 대화의 표시 이름·열림 상태와 원문 메시지만 보관한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 계정 정보와 첨부 파일을 저장 모델에서 제외.
    /// update date  : 2026-10-18 CodexOrig : TASK-014 작업 결과·읽지 않음 상태와 즉시 바인딩 알림 추가.
    /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ConversationRecord : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Provider { get; set; }
        public string Title { get; set; } = "새 대화";
        public bool IsOpen { get; set; } = true;
        public bool IsTitleAutomatic { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public string Draft { get; set; } = "";
        public string[] CollaboratorIds { get; set; } = new string[0];
        private ConversationRunState state;
        private bool hasUnread;
        public event PropertyChangedEventHandler PropertyChanged;
        public ConversationRunState State { get { return state; } set { state = value; NotifyStatus(); } }
        public bool HasUnread { get { return hasUnread; } set { hasUnread = value; NotifyStatus(); } }
        public string StatusGlyph { get { return State == ConversationRunState.Running ? "◌" : State == ConversationRunState.Failed || State == ConversationRunState.Interrupted ? "!" : HasUnread ? "●" : State == ConversationRunState.Stopped ? "■" : State == ConversationRunState.Completed ? "✓" : "○"; } }
        public string StatusColor { get { return State == ConversationRunState.Failed ? "#F28B82" : State == ConversationRunState.Interrupted || State == ConversationRunState.Stopped ? "#FDD663" : State == ConversationRunState.Running || HasUnread ? "#4A9EFF" : State == ConversationRunState.Completed ? "#81C995" : "#808080"; } }
        public string StatusDescription { get { return (State == ConversationRunState.Running ? "작업 중" : State == ConversationRunState.Completed ? "완료" : State == ConversationRunState.Failed ? "요청 실패" : State == ConversationRunState.Stopped ? "중지됨" : State == ConversationRunState.Interrupted ? "이전 실행에서 중단됨" : "대기") + (HasUnread ? " · 읽지 않음" : ""); } }
        public string AccessibleLabel { get { return Title + " · " + StatusDescription; } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NotifyStatus
        /// desc         : 대화 상태와 읽음 표시를 목록 바인딩에 즉시 전달한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-014 대화별 상태·색상·접근성 알림.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void NotifyStatus()
        {
            foreach (string name in new[] { "State", "HasUnread", "StatusGlyph", "StatusColor", "StatusDescription", "AccessibleLabel" })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ConversationRunState
    /// desc         : 실제 요청 수명과 종료 결과를 대화별로 구분한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : 재시작 시 실행 중을 중단 상태로 복구.
    /// </summary>
    /// ---------------------------------------------------------------------
    public enum ConversationRunState { Idle, Running, Completed, Failed, Stopped, Interrupted }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownWorkspaceStore
    /// desc         : 프로젝트와 무관한 로컬 Markdown 대화와 공통 instruction을 읽고 쓴다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 원자 저장·크기 제한·안전한 경로·보관 대화 재열기 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class MarkdownWorkspaceStore
    {
        private const int MaximumFileBytes = 4 * 1024 * 1024;
        private const int MaximumFilesPerProvider = 500;
        private const int MaximumMessages = 100;
        private const string MessageEnd = "\n<!-- assistant-message-end -->\n\n";
        private const string DraftEnd = "\n<!-- assistant-draft-end -->\n";
        private static readonly Regex MessagePattern = new Regex(@"\G## (사용자|AI|안내)\n\n<!-- assistant-message role=(user|assistant|system) chars=([0-9]{1,7}) -->\n");
        private static readonly Regex DraftPattern = new Regex(@"\G## 작성 중인 요청\n\n<!-- assistant-draft chars=([0-9]{1,5}) -->\n");
        private readonly UTF8Encoding encoding = new UTF8Encoding(false, true);
        public string RootPath { get; private set; }
        public string InstructionsPath { get { return Path.Combine(RootPath, "instructions.md"); } }
        public string[] LastLoadWarnings { get; private set; } = new string[0];

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkdownWorkspaceStore
        /// desc         : 사용자 공통 위치 또는 테스트가 지정한 독립 위치를 고정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 상대 작업 디렉터리에 의존하지 않는 저장 루트 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public MarkdownWorkspaceStore(string rootPath = null)
        {
            RootPath = Path.GetFullPath(rootPath ?? Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH") ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VS2017AIAssistant", "workspace"));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LoadOpen
        /// desc         : 선택 AI에서 닫지 않은 대화를 생성 순으로 복원하며 손상 파일을 건너뛴다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 유효 대화 복원 유지 및 오류 파일명만 별도 안내.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ConversationRecord[] LoadOpen(string provider)
        {
            provider = NormalizeProvider(provider);
            string directory = ProviderDirectory(provider);
            var warnings = new List<string>();
            var records = new List<ConversationRecord>();
            if (Directory.Exists(directory))
            {
                EnsureNoReparsePoint(directory);
                int count = 0;
                foreach (string file in Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly))
                {
                    if (++count > MaximumFilesPerProvider)
                    {
                        warnings.Add("대화 파일은 AI별 최대 500개까지 자동으로 읽습니다.");
                        break;
                    }
                    try
                    {
                        var record = ReadRecord(file);
                        if (record.Provider != provider || !String.Equals(Path.GetFullPath(file), GetPath(record), StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("대화 파일 위치와 식별자가 일치하지 않습니다.");
                        if (record.IsOpen) records.Add(record);
                    }
                    catch (Exception error) when (error is InvalidDataException || error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
                    {
                        warnings.Add(Path.GetFileName(file) + " 대화는 손상되었거나 읽을 수 없어 건너뛰었습니다.");
                    }
                }
            }
            LastLoadWarnings = warnings.ToArray();
            return records.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id, StringComparer.Ordinal).ToArray();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Create
        /// desc         : 충돌하지 않는 식별자로 새 대화를 생성하여 재실행 복원 대상으로 저장한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 두 AI의 대화 파일 경로 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ConversationRecord Create(string provider)
        {
            var record = new ConversationRecord { Id = Guid.NewGuid().ToString("N"), Provider = NormalizeProvider(provider), CreatedAt = DateTimeOffset.Now };
            Save(record);
            return record;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Save
        /// desc         : 원문 대화·초안·탭 상태를 읽기 쉬운 Markdown으로 원자 교체한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 첫 질문 자동 제목 및 수동 제목 보존.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 종료 상태와 읽지 않음 선택 메타데이터를 이전 형식에 추가.
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Save(ConversationRecord record)
        {
            Validate(record);
            if (record.IsTitleAutomatic)
            {
                var question = record.Messages.FirstOrDefault(m => m.Role == "user" && !String.IsNullOrWhiteSpace(m.Text));
                record.Title = question == null ? "새 대화" : Compact(question.Text, 120);
            }
            record.UpdatedAt = DateTimeOffset.Now;
            string path = GetPath(record);
            EnsureCapacity(path, record.Provider);
            var body = new StringBuilder();
            foreach (var message in record.Messages)
            {
                body.Append("## ").Append(message.Role == "user" ? "사용자" : message.Role == "assistant" ? "AI" : "안내").Append("\n\n");
                body.Append("<!-- assistant-message role=").Append(message.Role).Append(" chars=").Append(message.Text.Length.ToString(CultureInfo.InvariantCulture)).Append(" -->\n");
                body.Append(message.Text).Append(MessageEnd);
            }
            body.Append("## 작성 중인 요청\n\n<!-- assistant-draft chars=").Append(record.Draft.Length.ToString(CultureInfo.InvariantCulture)).Append(" -->\n");
            body.Append(record.Draft).Append(DraftEnd);
            AtomicWrite(path, Header("conversation", record.Title, record.CreatedAt, record.UpdatedAt, record.Provider, record.Id, Compact(record.Messages.FirstOrDefault(m => m.Role == "user")?.Text ?? "", 160),
                "provider: " + Quote(record.Provider) + "\nconversation_id: " + Quote(record.Id) + "\nis_open: " + (record.IsOpen ? "true" : "false") + "\nautomatic_title: " + (record.IsTitleAutomatic ? "true" : "false") + "\nlast_status: " + Quote(record.State.ToString()) + "\nhas_unread: " + (record.HasUnread ? "true" : "false") + "\ncollaborators: " + Quote(string.Join(",", record.CollaboratorIds)) + "\n") + body);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Close
        /// desc         : 파일은 보관하면서 다음 실행의 자동 복원 대상에서만 제외한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실패하면 메모리의 열림 상태도 복구.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Close(ConversationRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            bool previous = record.IsOpen;
            record.IsOpen = false;
            try { Save(record); }
            catch { record.IsOpen = previous; throw; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Rename
        /// desc         : 사용자가 지정한 제목을 저장하고 이후 첫 질문에 의한 덮어쓰기를 막는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 제목 120자 제한과 실패 시 원래 제목 복구.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Rename(ConversationRecord record, string title)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (String.IsNullOrWhiteSpace(title) || title.Length > 120 || title.Any(char.IsControl)) throw new ArgumentException("대화 이름은 한 줄로 1~120자까지 입력하세요.");
            string previous = record.Title;
            bool automatic = record.IsTitleAutomatic;
            record.Title = title.Trim(); record.IsTitleAutomatic = false;
            try { Save(record); }
            catch { record.Title = previous; record.IsTitleAutomatic = automatic; throw; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Load
        /// desc         : 선택한 Markdown 대화를 다시 열고 외부 파일이면 새 식별자로 안전하게 가져온다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 다른 AI 대화 혼입과 외부 식별자의 기존 파일 덮어쓰기 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ConversationRecord Load(string path, string provider)
        {
            provider = NormalizeProvider(provider);
            string fullPath = Path.GetFullPath(path);
            if (!String.Equals(Path.GetExtension(fullPath), ".md", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Markdown 대화 파일을 선택하세요.");
            var record = ReadRecord(fullPath);
            if (record.Provider != provider) throw new InvalidDataException("선택한 AI와 대화 파일의 AI가 다릅니다.");
            if (!String.Equals(fullPath, GetPath(record), StringComparison.OrdinalIgnoreCase)) record.Id = Guid.NewGuid().ToString("N");
            record.IsOpen = true;
            Save(record);
            return record;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LoadInstructions
        /// desc         : 모든 프로젝트가 공유하는 Markdown 본문을 원문 그대로 불러온다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 공통 instruction은 일반 편집기로 수정 가능한 본문 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string LoadInstructions()
        {
            if (!File.Exists(InstructionsPath)) return "";
            string text = ReadBounded(InstructionsPath);
            string body;
            var header = ReadHeader(text, out body);
            if (Required(header, "document_type") != "instructions" || body.Length > 8000) throw new InvalidDataException("공통 instruction 파일 형식이나 길이가 올바르지 않습니다.");
            return body;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SaveInstructions
        /// desc         : 공통 규칙을 YAML front matter와 실제 Markdown 본문으로 저장한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 다른 설정과 독립된 명시적 저장 및 8000자 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void SaveInstructions(string text)
        {
            if (text == null || text.Length > 8000) throw new ArgumentException("공통 instruction은 8000자 이하로 입력하세요.");
            DateTimeOffset now = DateTimeOffset.Now;
            AtomicWrite(InstructionsPath, Header("instructions", "모든 프로젝트의 공통 instruction", now, now, "Common", "instructions", Compact(text, 160), "") + text);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetPath
        /// desc         : 검증한 AI와 GUID만으로 생성되는 저장 위치를 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 제목과 외부 파일명을 경로 구성에서 제외.
        /// </summary>
        /// ---------------------------------------------------------------------
        public string GetPath(ConversationRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            ValidateId(record.Id);
            return Path.Combine(ProviderDirectory(NormalizeProvider(record.Provider)), record.Id + ".md");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadRecord
        /// desc         : 선언된 문자 수만큼만 원문을 읽어 Markdown 구분자가 포함된 메시지를 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 경계 위조·잘못된 역할·메시지 개수·후행 데이터 검사.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 이전 대화와 호환하며 실행 중 기록을 재시작 시 중단으로 복구.
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        private ConversationRecord ReadRecord(string path)
        {
            string body;
            var header = ReadHeader(ReadBounded(path), out body);
            if (Required(header, "document_type") != "conversation" || Required(header, "format_version") != "1") throw new InvalidDataException("지원하는 대화 Markdown 형식이 아닙니다.");
            var record = new ConversationRecord {
                Id = Required(header, "conversation_id"), Provider = Required(header, "provider"), Title = Required(header, "title"),
                IsOpen = ReadBoolean(header, "is_open"), IsTitleAutomatic = ReadBoolean(header, "automatic_title"),
                CreatedAt = ReadDate(header, "date"), UpdatedAt = ReadDate(header, "updated_at") };
            if (header.ContainsKey("last_status"))
            {
                ConversationRunState state;
                if (!Enum.TryParse(Required(header, "last_status"), out state) || !Enum.IsDefined(typeof(ConversationRunState), state)) throw new InvalidDataException("대화 상태가 올바르지 않습니다.");
                record.State = state == ConversationRunState.Running ? ConversationRunState.Interrupted : state;
            }
            record.HasUnread = header.ContainsKey("has_unread") && ReadBoolean(header, "has_unread");
            if (header.ContainsKey("collaborators")) record.CollaboratorIds = Required(header, "collaborators").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (header.ContainsKey("last_status") && Required(header, "last_status") == "Running") record.HasUnread = true;
            int position = 0;
            while (position < body.Length && !DraftPattern.IsMatch(body, position))
            {
                if (record.Messages.Count >= MaximumMessages) throw new InvalidDataException("대화 메시지가 너무 많습니다.");
                var marker = MessagePattern.Match(body, position);
                if (!marker.Success) throw new InvalidDataException("대화 메시지 경계가 올바르지 않습니다.");
                string role = marker.Groups[2].Value;
                if (marker.Groups[1].Value != (role == "user" ? "사용자" : role == "assistant" ? "AI" : "안내")) throw new InvalidDataException("메시지 역할이 일치하지 않습니다.");
                position += marker.Length;
                string text = ReadBodyPart(body, ref position, marker.Groups[3].Value, MessageEnd);
                record.Messages.Add(new ChatMessage { Role = role, Text = text });
            }
            var draftMarker = DraftPattern.Match(body, position);
            if (!draftMarker.Success) throw new InvalidDataException("작성 중 요청의 경계가 올바르지 않습니다.");
            position += draftMarker.Length;
            record.Draft = ReadBodyPart(body, ref position, draftMarker.Groups[1].Value, DraftEnd);
            if (position != body.Length) throw new InvalidDataException("대화 파일에 알 수 없는 후행 데이터가 있습니다.");
            Validate(record);
            return record;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadBodyPart
        /// desc         : 길이와 종료 표식을 함께 검사하여 본문의 표식을 데이터로 취급한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 길이 오버플로와 잘린 본문 거부.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ReadBodyPart(string body, ref int position, string lengthText, string ending)
        {
            int length;
            if (!Int32.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out length) || length > body.Length - position || body.Length - position - length < ending.Length)
                throw new InvalidDataException("대화 본문 길이가 올바르지 않습니다.");
            string text = body.Substring(position, length);
            position += length;
            if (String.CompareOrdinal(body, position, ending, 0, ending.Length) != 0) throw new InvalidDataException("대화 종료 표식이 일치하지 않습니다.");
            position += ending.Length;
            return text;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Header
        /// desc         : 요청한 front matter 항목과 기계 판독용 최소 상태를 함께 작성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 제목·요약의 개행과 따옴표를 YAML 호환 문자열로 이스케이프.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Header(string type, string title, DateTimeOffset created, DateTimeOffset updated, string provider, string id, string excerpt, string extra)
        {
            return "---\nlayout: content\ntitle: " + Quote(title) + "\ndate: " + Quote(created.ToString("o", CultureInfo.InvariantCulture)) +
                "\npermalink: " + Quote("/ai-assistant/" + type + "/" + id + "/") + "\ncategories: [\"AI Assistant\"]\ntags: [" + Quote(provider) +
                "]\npin: false\nnews_id: " + Quote(id) + "\nsource_url: \"\"\nsource_date: " + Quote(created.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) +
                "\nreviewed_at: " + Quote(updated.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) + "\nexcerpt: " + Quote(excerpt) +
                "\ndocument_type: " + Quote(type) + "\nformat_version: 1\nupdated_at: " + Quote(updated.ToString("o", CultureInfo.InvariantCulture)) + "\n" + extra + "---\n\n";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadHeader
        /// desc         : 제한된 front matter에서 필요한 단순 키·값을 읽고 나머지 본문은 변경하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 중복 키와 과도한 메타데이터 및 잘못된 문자열 거부.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Dictionary<string, string> ReadHeader(string text, out string body)
        {
            // 생성 파일은 LF를 사용하지만 일반 편집기의 CRLF front matter도 허용한다.
            var opening = Regex.Match(text, @"\A---\r?\n");
            if (!opening.Success) throw new InvalidDataException("Markdown front matter가 없습니다.");
            int position = opening.Length;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            while (position < text.Length && position < 16384)
            {
                int end = text.IndexOf('\n', position);
                if (end < 0) break;
                string line = text.Substring(position, end - position).TrimEnd('\r');
                position = end + 1;
                if (line == "---")
                {
                    if (position < text.Length && text[position] == '\n') position++;
                    else if (position + 1 < text.Length && text[position] == '\r' && text[position + 1] == '\n') position += 2;
                    body = text.Substring(position);
                    return values;
                }
                int colon = line.IndexOf(':');
                if (colon <= 0) throw new InvalidDataException("Markdown 메타데이터가 올바르지 않습니다.");
                string key = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();
                if (values.ContainsKey(key)) throw new InvalidDataException("중복된 Markdown 메타데이터가 있습니다.");
                if (value.StartsWith("\"", StringComparison.Ordinal))
                {
                    try { value = new JavaScriptSerializer().Deserialize<string>(value); }
                    catch (Exception error) when (error is ArgumentException || error is InvalidOperationException) { throw new InvalidDataException("메타데이터 문자열이 올바르지 않습니다.", error); }
                    if (value == null) throw new InvalidDataException("빈 메타데이터 값입니다.");
                }
                values.Add(key, value);
            }
            throw new InvalidDataException("Markdown front matter가 닫히지 않았거나 너무 큽니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadBounded
        /// desc         : 파일 크기를 읽는 동안 제한하고 잘못된 UTF-8이나 링크 파일을 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 크기 사전 확인 이후의 파일 확장도 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private string ReadBounded(string path)
        {
            EnsureNoReparsePoint(path);
            using (var input = File.OpenRead(path))
            using (var memory = new MemoryStream())
            {
                if (input.Length > MaximumFileBytes) throw new InvalidDataException("대화 파일은 4MB 이하만 읽을 수 있습니다.");
                byte[] buffer = new byte[8192];
                int count;
                while ((count = input.Read(buffer, 0, Math.Min(buffer.Length, MaximumFileBytes + 1 - (int)memory.Length))) > 0)
                {
                    memory.Write(buffer, 0, count);
                    if (memory.Length > MaximumFileBytes) throw new InvalidDataException("대화 파일은 4MB 이하만 읽을 수 있습니다.");
                }
                try
                {
                    byte[] data = memory.ToArray();
                    int offset = data.Length >= 3 && data[0] == 239 && data[1] == 187 && data[2] == 191 ? 3 : 0;
                    return encoding.GetString(data, offset, data.Length - offset);
                }
                catch (DecoderFallbackException error) { throw new InvalidDataException("UTF-8 Markdown 파일을 선택하세요.", error); }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AtomicWrite
        /// desc         : 같은 폴더의 임시 파일을 교체하여 저장 실패 시 기존 기록을 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : UTF-8 바이트 크기·링크 경로 검사 및 임시 파일 정리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void AtomicWrite(string path, string text)
        {
            if (encoding.GetByteCount(text) > MaximumFileBytes) throw new InvalidDataException("대화 기록은 파일당 4MB까지 저장할 수 있습니다.");
            string directory = Path.GetDirectoryName(path);
            EnsureNoReparsePoint(directory);
            Directory.CreateDirectory(directory);
            EnsureNoReparsePoint(path);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, text, encoding);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Validate
        /// desc         : 직렬화 이전에 모델과 메시지 크기·역할·날짜를 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 저장 모델의 무한 증가와 불완전 데이터 차단.
        /// update date  : 2026-10-18 CodexOrig : TASK-014 저장할 대화 상태가 정의된 값인지 검사.
        /// update date  : 2026-10-18 CodexOrig : TASK-016 문서 첨부·모델 설정·AI 역할 협업과 실패 복구 흐름 연결.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void Validate(ConversationRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            ValidateId(record.Id);
            if (record.CollaboratorIds == null || record.CollaboratorIds.Length > 3 || record.CollaboratorIds.Distinct().Count() != record.CollaboratorIds.Length || record.CollaboratorIds.Contains(record.Id)) throw new InvalidDataException("협업 참여자는 다른 대화 AI 최대 3명입니다.");
            foreach (string id in record.CollaboratorIds) ValidateId(id);
            if (!Enum.IsDefined(typeof(ConversationRunState), record.State) || NormalizeProvider(record.Provider) != record.Provider || record.Title == null || record.Title.Length > 120 || record.Title.Any(char.IsControl) ||
                record.CreatedAt == default(DateTimeOffset) || record.Messages == null || record.Messages.Count > MaximumMessages || record.Draft == null || record.Draft.Length > 16000 ||
                record.Messages.Any(m => m == null || (m.Role != "user" && m.Role != "assistant" && m.Role != "system") || m.Text == null || m.Text.Length > 2000000))
                throw new InvalidDataException("대화 데이터의 형식이나 크기가 올바르지 않습니다.");
            long totalBytes = Encoding.UTF8.GetByteCount(record.Draft);
            foreach (var message in record.Messages)
            {
                totalBytes += Encoding.UTF8.GetByteCount(message.Text);
                if (totalBytes > MaximumFileBytes) throw new InvalidDataException("대화 기록은 파일당 4MB까지 저장할 수 있습니다.");
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EnsureCapacity
        /// desc         : 새 파일 생성 시 AI별 기록 수 제한을 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 제한 도달 시 기존 대화 저장은 계속 허용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void EnsureCapacity(string path, string provider)
        {
            string directory = ProviderDirectory(provider);
            EnsureNoReparsePoint(directory);
            if (!File.Exists(path) && Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.md").Take(MaximumFilesPerProvider).Count() >= MaximumFilesPerProvider)
                throw new IOException("AI별 대화는 최대 500개까지 보관할 수 있습니다. 불필요한 Markdown 기록을 다른 폴더로 옮겨 주세요.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EnsureNoReparsePoint
        /// desc         : 파일과 상위 경로의 정션·심볼릭 링크를 통한 저장 위치 우회를 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 존재하는 모든 상위 경로를 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void EnsureNoReparsePoint(string path)
        {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("링크 또는 정션 경로는 대화 저장에 사용할 수 없습니다.");
                current = Path.GetDirectoryName(current);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProviderDirectory
        /// desc         : 허용한 AI 이름을 고정된 하위 폴더로 변환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 외부 입력의 경로 구분자 사용 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private string ProviderDirectory(string provider)
        {
            return Path.Combine(RootPath, "conversations", NormalizeProvider(provider).ToLowerInvariant());
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NormalizeProvider
        /// desc         : 기존 내부 AI 키와 알려진 표시 이름만 허용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Codex·Claude 전용 식별자 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string NormalizeProvider(string provider)
        {
            if (String.Equals(provider, "Codex", StringComparison.OrdinalIgnoreCase)) return "Codex";
            if (String.Equals(provider, "Claude", StringComparison.OrdinalIgnoreCase) || String.Equals(provider, "Claude Code", StringComparison.OrdinalIgnoreCase)) return "Claude";
            throw new ArgumentException("지원하지 않는 AI 대화입니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidateId
        /// desc         : 파일명으로 사용할 식별자를 32자리 소문자 GUID로 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 상위 디렉터리 이동과 장치 파일명 주입 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ValidateId(string id)
        {
            if (id == null || !Regex.IsMatch(id, @"\A[0-9a-f]{32}\z")) throw new InvalidDataException("대화 식별자가 올바르지 않습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Compact
        /// desc         : 제목과 요약의 공백을 정리하고 한 줄 표시 길이를 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 자동 제목 말미의 Unicode surrogate 분리 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Compact(string text, int length)
        {
            string value = Regex.Replace(text, @"\s+", " ").Trim();
            value = new string(value.Where(c => !char.IsControl(c)).ToArray());
            if (value.Length <= length) return value;
            int end = length - 1;
            if (end > 0 && char.IsHighSurrogate(value[end - 1])) end--;
            return value.Substring(0, end) + "…";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Quote
        /// desc         : JSON과 YAML 양쪽에서 유효한 따옴표 문자열로 메타데이터를 작성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 대화 본문은 직렬화 문자열로 감싸지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Quote(string value)
        {
            return new JavaScriptSerializer().Serialize(value);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Required
        /// desc         : 필수 front matter 항목의 누락을 일관된 파일 오류로 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 임의 메타데이터 기본값에 의한 상태 변경 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Required(Dictionary<string, string> values, string key)
        {
            string value;
            if (!values.TryGetValue(key, out value)) throw new InvalidDataException("대화 메타데이터가 누락되었습니다: " + key);
            return value;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadBoolean
        /// desc         : 열림과 자동 제목 플래그는 명시적인 true·false 값만 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 손상된 상태의 묵시적 복원 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool ReadBoolean(Dictionary<string, string> values, string key)
        {
            string value = Required(values, key);
            if (value != "true" && value != "false") throw new InvalidDataException("대화 상태가 올바르지 않습니다.");
            return value == "true";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadDate
        /// desc         : 시간대가 포함된 생성·수정 시각을 정확하게 복원한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 문화권별 날짜 해석 차이 제거.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static DateTimeOffset ReadDate(Dictionary<string, string> values, string key)
        {
            DateTimeOffset result;
            if (!DateTimeOffset.TryParseExact(Required(values, key), "o", CultureInfo.InvariantCulture, DateTimeStyles.None, out result)) throw new InvalidDataException("대화 날짜 형식이 올바르지 않습니다.");
            return result;
        }
    }
}
