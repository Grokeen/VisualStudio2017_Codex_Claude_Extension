using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ActivityFeedSnapshot73
/// desc         : 실제 workspace의 안정된 읽기 사본을 새 nonce에 보존하며 상태 metadata는 편집하지 않는다.
/// author       : CodexOrig 보조 개발 055
/// create date  : 2026-11-03
/// update date  :
/// </summary>
/// ---------------------------------------------------------------------
public static class ActivityFeedSnapshot73
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CaptureStable
    /// desc         : 복사 전후 원본 목록·해시가 같은 경우만 실제 저장 snapshot으로 확정한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public static string CaptureStable(string workspace, string parent, string heldProvider, string heldId)
    {
        string source = Path.GetFullPath(workspace), targetParent = Path.GetFullPath(parent);
        if (!targetParent.Contains(Path.Combine("task073", "runtime")) || targetParent.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Snapshot destination must be owned and outside the source workspace.");
        RejectLink(targetParent);
        string root = Path.Combine(targetParent, Guid.NewGuid().ToString("N").Substring(0, 16)), data = Path.Combine(root, "data");
        if (Directory.Exists(root) || File.Exists(root)) throw new IOException("A snapshot nonce already exists; prior evidence was preserved.");
        Directory.CreateDirectory(data);
        var before = Inventory(source);
        foreach (var file in before)
        {
            string destination = RelativePath(data, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            byte[] bytes = ReadShared(RelativePath(source, file.RelativePath));
            if (Hash(bytes) != file.SHA256) throw new IOException("Workspace changed before snapshot copy.");
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)) output.Write(bytes, 0, bytes.Length);
        }
        var after = Inventory(source);
        if (Signature(before) != Signature(after) || Signature(before) != Signature(Inventory(data)))
            throw new IOException("Workspace inventory or committed bytes changed during snapshot capture.");
        var states = new List<object>(); bool heldCaptured = heldId == null;
        foreach (var file in before.Where(item => Regex.IsMatch(item.RelativePath.Replace('\\', '/'), @"\Aconversations/(claude|codex)/[a-f0-9]{32}\.md\z")))
        {
            string text = Encoding.UTF8.GetString(ReadShared(RelativePath(data, file.RelativePath)));
            string provider = Header(text, "provider"), id = Header(text, "conversation_id"), state = Header(text, "last_status");
            if (file.RelativePath.Replace('\\', '/') != "conversations/" + provider.ToLowerInvariant() + "/" + id + ".md") throw new InvalidDataException("Snapshot main file identity differs from its path.");
            if (Header(text, "is_open") != "true") continue;
            states.Add(new { Provider = provider, Id = id, RawState = state, HasUnread = Header(text, "has_unread") == "true" });
            if (provider == heldProvider && id == heldId) { if (state != "Running") throw new InvalidDataException("Held actual request was not durably Running at capture."); heldCaptured = true; }
        }
        if (!heldCaptured)
            throw new InvalidDataException("Held actual conversation has no committed main file.");
        string manifest = Path.Combine(root, "snapshot.json");
        File.WriteAllText(manifest, new JavaScriptSerializer().Serialize(new {
            Schema = 1, CapturedAt = DateTimeOffset.UtcNow.ToString("o"), DataRoot = data, Files = before, SavedStates = states,
            HeldProvider = heldProvider, HeldId = heldId, SourceStableDuringCapture = true, SourceBytesEdited = false,
            StateMetadataEdited = false, CrashReproduced = false, SourceExpectedToChangeAfterNormalCancellation = heldId != null
        }), new UTF8Encoding(false));
        VerifySnapshot(manifest); return manifest;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : VerifySnapshot
    /// desc         : 고정 manifest의 whitelist·링크·전체 목록·해시를 모두 확인하고 상태 값을 바꾸지 않는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public static Dictionary<string, object> VerifySnapshot(string path)
    {
        string manifestPath = Path.GetFullPath(path);
        if (!manifestPath.Contains(Path.Combine("task073", "runtime"))) throw new InvalidOperationException("An owned snapshot manifest is required.");
        RejectLink(manifestPath);
        var manifest = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(manifestPath));
        if (Convert.ToInt32(manifest["Schema"]) != 1 || !Convert.ToBoolean(manifest["SourceStableDuringCapture"]) || Convert.ToBoolean(manifest["StateMetadataEdited"]))
            throw new InvalidDataException("Unsupported or mutated snapshot contract.");
        string data = Path.GetFullPath(Convert.ToString(manifest["DataRoot"]));
        if (!string.Equals(data, Path.Combine(Path.GetDirectoryName(manifestPath), "data"), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Snapshot data root escaped its nonce.");
        var expected = ((IEnumerable)manifest["Files"]).Cast<Dictionary<string, object>>().Select(item => new SnapshotFile73 {
            RelativePath = Convert.ToString(item["RelativePath"]), SHA256 = Convert.ToString(item["SHA256"]), Bytes = Convert.ToInt64(item["Bytes"]) }).ToArray();
        foreach (var file in expected) RelativePath(data, file.RelativePath);
        if (Signature(expected) != Signature(Inventory(data))) throw new InvalidDataException("Frozen snapshot inventory or bytes changed.");
        return manifest;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Seed
    /// desc         : 존재하지 않는 새 private workspace에 검증된 저장 bytes만 복사한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public static void Seed(string manifestPath, string newWorkspace)
    {
        var manifest = VerifySnapshot(manifestPath);
        string target = Path.GetFullPath(newWorkspace);
        if (!target.Contains(Path.Combine("artifacts", "ide")) || Directory.Exists(target) || File.Exists(target)) throw new InvalidOperationException("Only a new owned workspace may be seeded.");
        RejectLink(target);
        Directory.CreateDirectory(target);
        string data = Convert.ToString(manifest["DataRoot"]);
        var files = Inventory(data);
        foreach (var file in files)
        {
            string destination = RelativePath(target, file.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(RelativePath(data, file.RelativePath), destination, false);
        }
        if (Signature(files) != Signature(Inventory(target))) throw new IOException("Seeded workspace differs from its frozen snapshot.");
        VerifySnapshot(manifestPath);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Inventory
    /// desc         : 링크·임시 파일을 거부하고 bounded md/json whitelist만 안정적으로 해시한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static SnapshotFile73[] Inventory(string root)
    {
        RejectLink(root);
        var result = new List<SnapshotFile73>(); long total = 0;
        var directories = new Stack<string>(); directories.Push(root);
        while (directories.Count > 0)
        {
            string directory = directories.Pop();
            foreach (string file in Directory.EnumerateFileSystemEntries(directory))
            {
                RejectLink(file);
                if ((File.GetAttributes(file) & FileAttributes.Directory) != 0) { directories.Push(file); continue; }
                if (Path.GetFileName(file) == ".writer.lock") continue;
                string extension = Path.GetExtension(file);
                if (extension != ".md" && extension != ".json") throw new InvalidDataException("Unexpected workspace file in snapshot whitelist.");
                byte[] bytes = ReadShared(file); total += bytes.Length;
                if (bytes.Length > 4 * 1024 * 1024 || total > 32 * 1024 * 1024 || result.Count >= 10000) throw new InvalidDataException("Snapshot size limit exceeded.");
                result.Add(new SnapshotFile73 { RelativePath = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar), SHA256 = Hash(bytes), Bytes = bytes.Length });
            }
        }
        return result.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RelativePath
    /// desc         : manifest의 상대 경로가 지정된 nonce 루트를 벗어나지 못하게 검증한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string RelativePath(string root, string relative)
    {
        if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.Split('\\', '/').Any(part => part == ".." || part == ".")) throw new InvalidDataException("Invalid snapshot relative path.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Snapshot path escaped its root.");
        return path;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RejectLink
    /// desc         : reparse 대상이나 상위 경로를 읽기·복사 전에 거절한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void RejectLink(string path)
    { for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current)) if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Snapshot links are not allowed."); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadShared
    /// desc         : 원본 writer를 잠그지 않고 읽은 bytes를 전후 inventory 대조에 사용한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static byte[] ReadShared(string path)
    { using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) using (var memory = new MemoryStream()) { if (input.Length > 4 * 1024 * 1024) throw new InvalidDataException("Snapshot file too large."); var buffer = new byte[8192]; int read; while ((read = input.Read(buffer, 0, buffer.Length)) > 0) { if (memory.Length + read > 4 * 1024 * 1024) throw new InvalidDataException("Snapshot file grew too large."); memory.Write(buffer, 0, read); } return memory.ToArray(); } }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Header
    /// desc         : 원본 YAML header의 단일 필드만 읽고 enum이나 미읽음 metadata를 수정하지 않는다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Header(string text, string name)
    { var match = Regex.Match(text, "(?m)^" + Regex.Escape(name) + ": (.+)\\r?$"); if (!match.Success) throw new InvalidDataException("Missing snapshot header: " + name); string value = match.Groups[1].Value.Trim(); return value.StartsWith("\"", StringComparison.Ordinal) ? new JavaScriptSerializer().Deserialize<string>(value) : value; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Hash
    /// desc         : 복사 전후 동일성을 비교하는 SHA256을 계산한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Signature
    /// desc         : 파일 순서와 무관한 상대 경로·길이·SHA inventory 서명을 만든다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Signature(IEnumerable<SnapshotFile73> files) { return string.Join("\n", files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).Select(file => file.RelativePath + "\0" + file.Bytes + "\0" + file.SHA256)); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : SnapshotFile73
    /// desc         : 복사한 실제 저장 파일의 위치·길이·해시만 보존한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-03
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class SnapshotFile73 { public string RelativePath; public string SHA256; public long Bytes; }
}
