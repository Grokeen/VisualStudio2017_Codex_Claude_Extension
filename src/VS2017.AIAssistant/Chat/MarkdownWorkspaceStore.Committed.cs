using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace VS2017.AIAssistant.Chat
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : StaleConversationWriteException
    /// desc         : 다른 저장자가 먼저 확정한 대화를 오래된 입력 창으로 덮지 못하게 명시적으로 거절한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 보조 개발의 독립 수정 초안이며 제품에는 반영하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class StaleConversationWriteException : IOException
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StaleConversationWriteException
        /// desc         : 낡은 저장자를 명시적으로 거절해 기존 원문과 작성 중 입력을 모두 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        public StaleConversationWriteException() : base("다른 화면에서 이 대화를 먼저 저장했습니다. 작성한 내용은 보존했습니다. 최신 대화를 다시 불러온 뒤 요청을 다시 보내세요.") { }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownWorkspaceStore
    /// desc         : 불변 UTC 시간 파일과 수신 순서 인덱스를 메인 Markdown의 마지막 교체로 함께 확정한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : TASK068 보조 개발의 개인 사본에서만 저장 실패·경합·순서 경계를 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed partial class MarkdownWorkspaceStore
    {
        private const int MaximumHistoryEntries = 20000;
        private static readonly Regex ChunkNamePattern = new Regex(@"\A[0-9]{10}-[a-f0-9]{8}-[0-9]{4}\.md\z");
        private static readonly Regex ManifestNamePattern = new Regex(@"\Ai-[a-f0-9]{16}\.json\z");
        internal Action<string> StorageFaultInjector { get; set; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HistoryIndex
        /// desc         : 확정 revision과 메시지 수신 순서를 불변 시간 파일 참조로 보관한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class HistoryIndex
        {
            public int Version { get; set; } = 1;
            public string Provider { get; set; }
            public string ConversationId { get; set; }
            public string Revision { get; set; }
            public List<HistoryReference> Entries { get; set; } = new List<HistoryReference>();
            public List<HistoryChunk> Chunks { get; set; } = new List<HistoryChunk>();
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HistoryReference
        /// desc         : 본문을 메모리에 누적하지 않고 메시지 ID·순서·위치·무결성을 참조한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class HistoryReference
        {
            public string Id { get; set; }
            public long Sequence { get; set; }
            public string File { get; set; }
            public int Position { get; set; }
            public string Digest { get; set; }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : HistoryChunk
        /// desc         : 한 불변 시간 파일의 이름·체크섬·메시지 개수를 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class HistoryChunk
        {
            public string Name { get; set; }
            public string Digest { get; set; }
            public int Count { get; set; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Digest
        /// desc         : UTF-8 원문의 SHA256으로 메인·인덱스·본문의 변경을 식별한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Digest(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : StableLegacyId
        /// desc         : 기존 대화 식별자와 원문 순서로 재조회해도 같은 legacy ID를 생성한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string StableLegacyId(ConversationRecord record, int position, string role, string text)
        {
            return Digest(record.Provider + "\n" + record.Id + "\n" + position.ToString(CultureInfo.InvariantCulture) + "\n" + role + "\n" + text).Substring(0, 32);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Hex
        /// desc         : 허용한 길이의 소문자 식별자와 체크섬만 통과시킨다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool Hex(string text, int length)
        {
            return text != null && text.Length == length && text.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MessageDigest
        /// desc         : 역할·원래 시각·ID·원문을 함께 검사해 같은 ID의 안전한 갱신을 구분한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string MessageDigest(ChatMessage message)
        {
            return Digest(message.Id + "\n" + message.Role + "\n" + message.CreatedAt.ToString("o", CultureInfo.InvariantCulture) + "\n" + message.Text);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CopyMessage
        /// desc         : 저장 또는 조회 시 외부 컬렉션 변경과 분리된 메시지 값을 고정한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static ChatMessage CopyMessage(ChatMessage message)
        {
            return new ChatMessage { Id = message.Id, Role = message.Role, Text = message.Text, CreatedAt = message.CreatedAt };
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidateCommitMetadata
        /// desc         : 메인 파일의 상대 인덱스 참조·해시·revision 형식을 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ValidateCommitMetadata(ConversationRecord record)
        {
            if (String.IsNullOrEmpty(record.HistoryManifest))
            {
                if (!String.IsNullOrEmpty(record.HistoryManifestHash)) throw new InvalidDataException("기록 인덱스의 참조가 없습니다.");
                return;
            }
            if (!ManifestNamePattern.IsMatch(record.HistoryManifest) || !Hex(record.HistoryManifestHash, 64) || !Hex(record.StorageRevision, 32))
                throw new InvalidDataException("기록 인덱스 참조가 올바르지 않습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EnterHistoryTransaction
        /// desc         : 동일 대화만 파일 잠금으로 직렬화하고 제한된 시간 뒤 경합 실패를 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private FileStream EnterHistoryTransaction(ConversationRecord record)
        {
            string directory = HistoryDirectory(record);
            EnsureNoReparsePoint(directory); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, ".writer.lock");
            for (int attempt = 0; ; attempt++)
            {
                EnsureNoReparsePoint(path);
                try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException error)
                {
                    int code = error.HResult & 0xffff;
                    if ((code != 32 && code != 33) || attempt >= 99) throw;
                    Thread.Sleep(10);
                }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadIndex
        /// desc         : 확정된 인덱스만 읽고 소유권·해시·중복 ID·순서·크기 상한을 검증한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private HistoryIndex ReadIndex(ConversationRecord record)
        {
            ValidateCommitMetadata(record);
            if (String.IsNullOrEmpty(record.HistoryManifest)) return null;
            string text = ReadBounded(Path.Combine(HistoryDirectory(record), record.HistoryManifest));
            if (Digest(text) != record.HistoryManifestHash) throw new InvalidDataException("기록 인덱스 체크섬이 일치하지 않습니다.");
            HistoryIndex result;
            try { result = new JavaScriptSerializer { MaxJsonLength = MaximumFileBytes * 2, RecursionLimit = 32 }.Deserialize<HistoryIndex>(text); }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            { throw new InvalidDataException("기록 인덱스 형식이 올바르지 않습니다.", error); }
            if (result == null || result.Version != 1 || result.Provider != record.Provider || result.ConversationId != record.Id || result.Revision != record.StorageRevision ||
                result.Entries == null || result.Chunks == null || result.Entries.Count > MaximumHistoryEntries || result.Chunks.Count > MaximumHistoryEntries)
                throw new InvalidDataException("기록 인덱스의 소유권 또는 크기가 올바르지 않습니다.");
            var chunks = new Dictionary<string, HistoryChunk>(StringComparer.Ordinal);
            foreach (var chunk in result.Chunks)
            {
                if (chunk == null || chunk.Name == null || !ChunkNamePattern.IsMatch(chunk.Name) || !Hex(chunk.Digest, 64) || chunk.Count < 1 || chunk.Count > MaximumHistoryEntries || chunks.ContainsKey(chunk.Name))
                    throw new InvalidDataException("시간별 파일 참조가 올바르지 않습니다.");
                chunks.Add(chunk.Name, chunk);
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < result.Entries.Count; index++)
            {
                var entry = result.Entries[index]; HistoryChunk chunk;
                if (entry == null || !Hex(entry.Id, 32) || !ids.Add(entry.Id) || entry.Sequence != index + 1 || !Hex(entry.Digest, 64) || entry.File == null ||
                    !chunks.TryGetValue(entry.File, out chunk) || entry.Position < 0 || entry.Position >= chunk.Count)
                    throw new InvalidDataException("기록 순서 또는 메시지 참조가 올바르지 않습니다.");
            }
            return result;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadChunk
        /// desc         : 현재 인덱스가 참조한 UTC 시간 파일의 소유권과 본문 무결성을 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private List<ChatMessage> ReadChunk(ConversationRecord record, HistoryChunk chunk)
        {
            string text = ReadBounded(Path.Combine(HistoryDirectory(record), chunk.Name));
            if (Digest(text) != chunk.Digest) throw new InvalidDataException("시간별 기록 체크섬이 일치하지 않습니다.");
            string body; var header = ReadHeader(text, out body);
            if (Required(header, "document_type") != "conversation_hour" || Required(header, "provider") != record.Provider ||
                Required(header, "conversation_id") != record.Id || Required(header, "hour") != chunk.Name.Substring(0, 10) || Required(header, "time_basis") != "UTC")
                throw new InvalidDataException("시간별 기록 소유권이 올바르지 않습니다.");
            var messages = ParseHistoryBody(body, record, chunk.Name.Substring(0, 10), true);
            if (messages.Count != chunk.Count) throw new InvalidDataException("시간별 기록 메시지 수가 일치하지 않습니다.");
            return messages;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ParseHistoryBody
        /// desc         : 길이 기반 원문 경계·역할·ID·시각을 검증해 메시지를 복원한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private List<ChatMessage> ParseHistoryBody(string body, ConversationRecord record, string hour, bool utc)
        {
            int position = 0; var messages = new List<ChatMessage>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            while (position < body.Length)
            {
                var marker = MessagePattern.Match(body, position);
                if (!marker.Success || !marker.Groups[4].Success || !marker.Groups[5].Success || messages.Count >= MaximumHistoryEntries)
                    throw new InvalidDataException("시간별 메시지 경계가 올바르지 않습니다.");
                string role = marker.Groups[2].Value;
                if (marker.Groups[1].Value != (role == "user" ? "사용자" : role == "assistant" ? "AI" : "안내")) throw new InvalidDataException("시간별 역할이 일치하지 않습니다.");
                position += marker.Length;
                string value = ReadBodyPart(body, ref position, marker.Groups[3].Value, MessageEnd); DateTimeOffset at;
                if (!DateTimeOffset.TryParse(marker.Groups[5].Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out at) ||
                    utc && at.UtcDateTime.ToString("yyyyMMddHH", CultureInfo.InvariantCulture) != hour || !ids.Add(marker.Groups[4].Value))
                    throw new InvalidDataException("시간별 메시지 ID 또는 시각이 올바르지 않습니다.");
                messages.Add(new ChatMessage { Id = marker.Groups[4].Value, Role = role, Text = value, CreatedAt = at });
            }
            return messages;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadReference
        /// desc         : 최대 두 시간 파일만 캐시하며 요청한 메시지 위치와 원문 해시를 확인한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private ChatMessage ReadReference(ConversationRecord record, HistoryIndex index, HistoryReference reference, Dictionary<string, List<ChatMessage>> cache)
        {
            List<ChatMessage> messages;
            if (!cache.TryGetValue(reference.File, out messages))
            {
                if (cache.Count >= 2) cache.Remove(cache.Keys.First());
                messages = ReadChunk(record, index.Chunks.Single(item => item.Name == reference.File)); cache.Add(reference.File, messages);
            }
            if (reference.Position >= messages.Count) throw new InvalidDataException("메시지 위치가 올바르지 않습니다.");
            var result = messages[reference.Position];
            if (result.Id != reference.Id || MessageDigest(result) != reference.Digest) throw new InvalidDataException("메시지 참조가 일치하지 않습니다.");
            return CopyMessage(result);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LoadOlderCommitted
        /// desc         : 고정된 확정 인덱스에서 직전 페이지만 읽어 시계·시간대와 대화 순서를 분리한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private ChatMessage[] LoadOlderCommitted(ConversationRecord record, ChatMessage before, int count)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            ValidateId(record.Id); NormalizeProvider(record.Provider);
            if (before == null || !Hex(before.Id, 32) || count < 1 || count > 100) throw new ArgumentException("이전 메시지 위치와 페이지 크기가 올바르지 않습니다.");
            var index = ReadIndex(record);
            if (index == null)
            {
                var messages = ReadLegacyCommittedMessages(record); int position = messages.FindIndex(item => item.Id == before.Id);
                if (position < 0) throw new InvalidDataException("이전 메시지 커서를 찾을 수 없습니다.");
                return messages.Skip(Math.Max(0, position - count)).Take(Math.Min(count, position)).Select(CopyMessage).ToArray();
            }
            int cursor = index.Entries.FindIndex(item => item.Id == before.Id);
            if (cursor < 0) throw new InvalidDataException("이전 메시지 커서를 찾을 수 없습니다.");
            var cache = new Dictionary<string, List<ChatMessage>>(StringComparer.Ordinal);
            return index.Entries.Skip(Math.Max(0, cursor - count)).Take(Math.Min(count, cursor)).Select(entry => ReadReference(record, index, entry, cache)).ToArray();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadLegacyCommittedMessages
        /// desc         : 이전 형식의 마지막 확정 ID까지 원문을 복원해 최초 저장에서 마이그레이션한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private List<ChatMessage> ReadLegacyCommittedMessages(ConversationRecord record)
        {
            var result = new List<ChatMessage>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            string directory = HistoryDirectory(record);
            if (Directory.Exists(directory) && !String.IsNullOrEmpty(record.LastArchivedMessageId))
            {
                EnsureNoReparsePoint(directory);
                bool boundary = false;
                foreach (string path in Directory.GetFiles(directory, "??????????-????.md").OrderBy(p => p, StringComparer.Ordinal))
                {
                    string body; var header = ReadHeader(ReadBounded(path), out body); string name = Path.GetFileNameWithoutExtension(path);
                    if (!Regex.IsMatch(name, @"\A[0-9]{10}-[0-9]{4}\z") || Required(header, "document_type") != "conversation_hour" ||
                        Required(header, "provider") != record.Provider || Required(header, "conversation_id") != record.Id || Required(header, "hour") != name.Substring(0, 10))
                        throw new InvalidDataException("이전 시간별 기록의 소유권이 올바르지 않습니다.");
                    foreach (var message in ParseHistoryBody(body, record, name.Substring(0, 10), false))
                    {
                        if (ids.Add(message.Id)) result.Add(message);
                        if (result.Count > MaximumHistoryEntries) throw new InvalidDataException("대화 기록 인덱스 최대 크기를 초과했습니다.");
                        if (message.Id == record.LastArchivedMessageId) { boundary = true; break; }
                    }
                    if (boundary) break;
                }
                if (!boundary && record.Messages.Count > 0) throw new InvalidDataException("이전 시간별 기록의 확정 경계를 찾을 수 없습니다.");
            }
            foreach (var message in record.Messages) if (ids.Add(message.Id)) result.Add(CopyMessage(message));
            return result;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SaveCommitted
        /// desc         : 낡은 저장자를 거절하고 불변 파일 준비 뒤 메인 MD를 마지막 커밋으로 원자 교체한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SaveCommitted(ConversationRecord record)
        {
            Validate(record);
            var incoming = record.Messages.Select(CopyMessage).ToArray();
            if (incoming.Any(message => !Hex(message.Id, 32) || message.CreatedAt == default(DateTimeOffset)) || incoming.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != incoming.Length)
                throw new InvalidDataException("메시지 ID 또는 시각이 올바르지 않습니다.");
            string path = GetPath(record); EnsureCapacity(path, record.Provider);
            using (EnterHistoryTransaction(record))
            {
                ConversationRecord durable = File.Exists(path) ? ReadRecord(path) : null;
                if (durable != null && (durable.StorageRevision != record.StorageRevision || durable.StorageDocumentHash != record.StorageDocumentHash)) throw new StaleConversationWriteException();
                var index = durable == null ? null : ReadIndex(durable);
                var dirty = new Dictionary<string, ChatMessage>(StringComparer.Ordinal);
                if (index == null)
                {
                    index = new HistoryIndex { Provider = record.Provider, ConversationId = record.Id };
                    if (durable != null)
                        foreach (var message in ReadLegacyCommittedMessages(durable))
                        {
                            index.Entries.Add(new HistoryReference { Id = message.Id, Sequence = index.Entries.Count + 1, Digest = MessageDigest(message) });
                            dirty.Add(message.Id, message);
                        }
                }
                var byId = index.Entries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
                foreach (var message in incoming)
                {
                    HistoryReference reference;
                    string hash = MessageDigest(message);
                    if (byId.TryGetValue(message.Id, out reference))
                    {
                        if (reference.Digest != hash) { reference.Digest = hash; dirty[message.Id] = message; }
                    }
                    else
                    {
                        reference = new HistoryReference { Id = message.Id, Sequence = index.Entries.Count + 1, Digest = hash };
                        index.Entries.Add(reference); byId.Add(message.Id, reference); dirty.Add(message.Id, message);
                    }
                }
                if (index.Entries.Count > MaximumHistoryEntries) throw new InvalidDataException("대화 기록 인덱스 최대 크기를 초과했습니다. 기존 기록과 작성 중 내용은 보존했습니다.");
                if (durable != null && !String.IsNullOrEmpty(durable.HistoryManifest) && dirty.Count == 0 && record.Title == durable.Title && record.Draft == durable.Draft &&
                    record.IsOpen == durable.IsOpen && record.IsTitleAutomatic == durable.IsTitleAutomatic && record.State == durable.State && record.HasUnread == durable.HasUnread &&
                    record.CreatedAt == durable.CreatedAt && record.FirstUserMessageId == durable.FirstUserMessageId && record.CollaboratorIds.SequenceEqual(durable.CollaboratorIds)) return;
                string revision = Guid.NewGuid().ToString("N"); index.Revision = revision;
                var staged = new List<string>(); bool committed = false;
                try
                {
                    WriteDirtyChunks(record, index, dirty, staged);
                    InjectStorageFailure("after-chunks");
                    var used = new HashSet<string>(index.Entries.Select(entry => entry.File), StringComparer.Ordinal);
                    index.Chunks = index.Chunks.Where(chunk => used.Contains(chunk.Name)).ToList();
                    string indexText = new JavaScriptSerializer { MaxJsonLength = MaximumFileBytes * 2, RecursionLimit = 32 }.Serialize(index);
                    if (encoding.GetByteCount(indexText) > MaximumFileBytes) throw new InvalidDataException("기록 인덱스는 4MB 이하만 저장할 수 있습니다.");
                    string manifest = "i-" + Guid.NewGuid().ToString("N").Substring(0, 16) + ".json";
                    WriteImmutable(Path.Combine(HistoryDirectory(record), manifest), indexText, staged);
                    InjectStorageFailure("after-index");
                    var available = incoming.ToDictionary(message => message.Id, StringComparer.Ordinal);
                    foreach (var item in dirty) available[item.Key] = item.Value;
                    var cache = new Dictionary<string, List<ChatMessage>>(StringComparer.Ordinal);
                    var latest = index.Entries.Skip(Math.Max(0, index.Entries.Count - 20)).Select(entry => available.ContainsKey(entry.Id) ? available[entry.Id] : ReadReference(record, index, entry, cache)).ToArray();
                    string firstUser = !String.IsNullOrEmpty(record.FirstUserMessageId) ? record.FirstUserMessageId : durable?.FirstUserMessageId;
                    if (String.IsNullOrEmpty(firstUser)) firstUser = (durable?.Messages ?? new List<ChatMessage>()).Concat(incoming).FirstOrDefault(message => message.Role == "user" && !String.IsNullOrWhiteSpace(message.Text))?.Id;
                    string title = record.Title;
                    var firstQuestion = incoming.FirstOrDefault(message => message.Id == firstUser && message.Role == "user" && !String.IsNullOrWhiteSpace(message.Text));
                    if (record.IsTitleAutomatic && firstQuestion != null) title = Compact(firstQuestion.Text, 120);
                    DateTimeOffset updated = DateTimeOffset.Now;
                    string lastId = index.Entries.LastOrDefault()?.Id ?? "";
                    var body = new StringBuilder(); foreach (var message in latest) AppendMessage(body, message);
                    body.Append("## 작성 중인 요청\n\n<!-- assistant-draft chars=").Append(record.Draft.Length.ToString(CultureInfo.InvariantCulture)).Append(" -->\n").Append(record.Draft).Append(DraftEnd);
                    string manifestHash = Digest(indexText);
                    string extra = "provider: " + Quote(record.Provider) + "\nconversation_id: " + Quote(record.Id) + "\nis_open: " + (record.IsOpen ? "true" : "false") + "\nautomatic_title: " + (record.IsTitleAutomatic ? "true" : "false") +
                        "\nlast_status: " + Quote(record.State.ToString()) + "\nhas_unread: " + (record.HasUnread ? "true" : "false") + "\ncollaborators: " + Quote(string.Join(",", record.CollaboratorIds)) +
                        "\nlast_archived_message_id: " + Quote(lastId) + "\nfirst_user_message_id: " + Quote(firstUser ?? "") + "\nhistory_revision: " + Quote(revision) + "\nhistory_manifest: " + Quote(manifest) + "\nhistory_manifest_sha256: " + Quote(manifestHash) + "\n";
                    string main = Header("conversation", title, record.CreatedAt, updated, record.Provider, record.Id, Compact(latest.FirstOrDefault(m => m.Role == "user")?.Text ?? "", 160), extra) + body;
                    InjectStorageFailure("before-main"); AtomicWrite(path, main); committed = true;
                    record.Title = title; record.UpdatedAt = updated; record.FirstUserMessageId = firstUser; record.LastArchivedMessageId = lastId;
                    record.StorageRevision = revision; record.HistoryManifest = manifest; record.HistoryManifestHash = manifestHash;
                    record.StorageDocumentHash = Digest(main);
                }
                finally
                {
                    if (!committed) foreach (string file in staged)
                        try { EnsureNoReparsePoint(file); File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InjectStorageFailure
        /// desc         : 독립 검사에서 커밋 전 I/O 실패를 주입하며 일반 실행에서는 아무 동작도 하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InjectStorageFailure(string stage)
        {
            StorageFaultInjector?.Invoke(stage);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WriteDirtyChunks
        /// desc         : 변경 메시지만 UTC 시간별로 나누고 UTF-8 Markdown 실제 크기로 4MB 이전에 분할한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void WriteDirtyChunks(ConversationRecord record, HistoryIndex index, Dictionary<string, ChatMessage> dirty, List<string> staged)
        {
            var byId = index.Entries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
            foreach (var group in dirty.Values.GroupBy(message => message.CreatedAt.UtcDateTime.ToString("yyyyMMddHH", CultureInfo.InvariantCulture)))
            {
                string token;
                do { token = Guid.NewGuid().ToString("N").Substring(0, 8); }
                while (Directory.GetFiles(HistoryDirectory(record), group.Key + "-" + token + "-*.md").Length > 0);
                int part = 0;
                string header = Header("conversation_hour", record.Title, record.CreatedAt, DateTimeOffset.Now, record.Provider, record.Id, "시간별 확정 대화 기록",
                    "provider: " + Quote(record.Provider) + "\nconversation_id: " + Quote(record.Id) + "\nhour: " + Quote(group.Key) + "\ntime_basis: \"UTC\"\n");
                var buffer = new StringBuilder(header); int bytes = encoding.GetByteCount(header); var entries = new List<HistoryReference>();
                foreach (var message in group.OrderBy(message => byId[message.Id].Sequence))
                {
                    var line = new StringBuilder(); AppendMessage(line, message); string value = line.ToString(); int addition = encoding.GetByteCount(value);
                    if (encoding.GetByteCount(header) + addition > MaximumFileBytes) throw new InvalidDataException("한 메시지는 4MB 이하만 저장할 수 있습니다.");
                    if (bytes + addition > MaximumFileBytes)
                    {
                        FlushChunk(record, index, group.Key, token, part++, buffer.ToString(), entries, staged);
                        buffer = new StringBuilder(header); bytes = encoding.GetByteCount(header); entries.Clear();
                    }
                    entries.Add(byId[message.Id]); buffer.Append(value); bytes += addition;
                }
                if (entries.Count > 0) FlushChunk(record, index, group.Key, token, part, buffer.ToString(), entries, staged);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FlushChunk
        /// desc         : 불변 파일을 확정한 뒤 수신 순서 인덱스의 위치와 체크섬을 연결한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void FlushChunk(ConversationRecord record, HistoryIndex index, string hour, string token, int part, string text, List<HistoryReference> entries, List<string> staged)
        {
            if (part > 9999) throw new InvalidDataException("시간별 기록 분할 수가 너무 많습니다.");
            string name = hour + "-" + token + "-" + part.ToString("D4", CultureInfo.InvariantCulture) + ".md";
            WriteImmutable(Path.Combine(HistoryDirectory(record), name), text, staged);
            index.Chunks.Add(new HistoryChunk { Name = name, Digest = Digest(text), Count = entries.Count });
            for (int position = 0; position < entries.Count; position++) { entries[position].File = name; entries[position].Position = position; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidateIoPath
        /// desc         : 대상과 같은 폴더의 짧은 임시 이름이 Windows 경로 상한을 넘지 않게 검사한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void ValidateIoPath(string path)
        {
            if (Path.GetFullPath(path).Length >= 260 || Path.GetDirectoryName(Path.GetFullPath(path)).Length >= 248)
                throw new PathTooLongException("저장 경로가 너무 깁니다. 기존 기록과 작성 중 내용은 보존했습니다.");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WriteImmutable
        /// desc         : 새 이름의 참조 파일만 독점 생성하고 디스크에 비운 뒤 최종 메인 커밋을 기다린다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void WriteImmutable(string path, string text, List<string> staged)
        {
            if (encoding.GetByteCount(text) > MaximumFileBytes) throw new InvalidDataException("기록 파일은 4MB 이하만 저장할 수 있습니다.");
            ValidateIoPath(path); EnsureNoReparsePoint(path);
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                staged.Add(path); var bytes = encoding.GetBytes(text); output.Write(bytes, 0, bytes.Length); output.Flush(true);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WriteAtomicSibling
        /// desc         : 기존 대상의 긴 이름을 덧붙이지 않고 짧은 sibling 임시 파일로 원자 교체한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// update date  : 2026-10-20 CodexOrig : TASK068 시간별 저장 안전성 보완.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void WriteAtomicSibling(string path, string text)
        {
            if (encoding.GetByteCount(text) > MaximumFileBytes) throw new InvalidDataException("대화 기록은 파일당 4MB까지 저장할 수 있습니다.");
            ValidateIoPath(path); string directory = Path.GetDirectoryName(path);
            EnsureNoReparsePoint(directory); Directory.CreateDirectory(directory); EnsureNoReparsePoint(path);
            string temporary = null;
            try
            {
                FileStream output = null;
                for (int attempt = 0; output == null; attempt++)
                {
                    temporary = Path.Combine(directory, "." + Guid.NewGuid().ToString("N").Substring(0, 16) + ".tmp");
                    ValidateIoPath(temporary); EnsureNoReparsePoint(temporary);
                    try { output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
                    catch (IOException) { if (!File.Exists(temporary) || attempt >= 9) { temporary = null; throw; } }
                }
                using (output) { byte[] bytes = encoding.GetBytes(text); output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                CommitTemporaryFile(temporary, path);
            }
            finally { if (temporary != null && File.Exists(temporary)) { EnsureNoReparsePoint(temporary); File.Delete(temporary); } }
        }
    }
}
