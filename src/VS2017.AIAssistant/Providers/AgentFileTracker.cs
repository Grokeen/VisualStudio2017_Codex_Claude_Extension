using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VS2017.AIAssistant.Providers
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AgentFileTracker
    /// desc         : CLI가 명령으로 수정한 프로젝트 소스도 제한된 실행 전후 비교로 확인한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : TASK-020 실제 Codex 명령 수정에서 누락된 변경 카드 보완.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class AgentFileTracker
    {
        private const int MaximumFiles = 4000, MaximumCharacters = 200000, MaximumBytes = 32 * 1024 * 1024;
        private static readonly HashSet<string> Ignored = new HashSet<string>(new[] { ".git", ".vs", "bin", "obj", "node_modules", "packages", "releases", "artifacts" }, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Extensions = new HashSet<string>((".cs .vb .cpp .c .h .hpp .xaml .xml .sql .ps1 .py .js .ts .tsx .jsx .html .css .json .md .yaml .yml .toml .config .sln .csproj .vbproj .props .targets .bat .cmd .sh .resx .txt").Split(' '), StringComparer.OrdinalIgnoreCase);
        private readonly string directory;
        private readonly Dictionary<string, string> before;
        private readonly bool complete;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AgentFileTracker
        /// desc         : 제한된 원본 스냅샷을 요청 수명 안에만 보관한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 프로젝트 범위 비교.
        /// </summary>
        /// ---------------------------------------------------------------------
        private AgentFileTracker(string directory, Dictionary<string, string> before, bool complete) { this.directory = directory; this.before = before; this.complete = complete; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CreateAsync
        /// desc         : UI를 막지 않고 최대 4000개·32MiB의 작은 텍스트 소스만 읽는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 취소·파일 크기·정션 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static Task<AgentFileTracker> CreateAsync(string directory, CancellationToken cancellation)
        { return Task.Run(() => { bool complete; var files = ReadFiles(directory, cancellation, out complete); return new AgentFileTracker(directory, files, complete); }, cancellation); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadFiles
        /// desc         : 링크와 생성물 폴더를 따라가지 않으며 파일 성장·읽기 실패는 미확인으로 구분한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 실행 비교의 유한 자원 경계.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static Dictionary<string, string> ReadFiles(string root, CancellationToken cancellation, out bool complete)
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); var pending = new Stack<string>(); pending.Push(root);
            long total = 0; int directories = 0, visited = 0; complete = true;
            while (pending.Count > 0 && directories++ < 1000 && files.Count < MaximumFiles && total < MaximumBytes) {
                cancellation.ThrowIfCancellationRequested(); string current = pending.Pop();
                try {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) continue;
                    foreach (string child in Directory.EnumerateDirectories(current)) { if (pending.Count >= 1000) break; if (!Ignored.Contains(Path.GetFileName(child))) pending.Push(child); }
                    foreach (string file in Directory.EnumerateFiles(current)) {
                        cancellation.ThrowIfCancellationRequested();
                        if (files.Count >= MaximumFiles || total >= MaximumBytes || visited++ >= 20000) { complete = false; break; }
                        if (!Extensions.Contains(Path.GetExtension(file)) || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                        string content = null;
                        try {
                            using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                                if (input.Length <= MaximumCharacters * 4 && total + input.Length <= MaximumBytes) {
                                    byte[] bytes = new byte[MaximumCharacters * 4 + 1]; int length = 0, count;
                                    while (length < bytes.Length && (count = input.Read(bytes, length, Math.Min(8192, bytes.Length - length))) > 0) { length += count; cancellation.ThrowIfCancellationRequested(); }
                                    total += length;
                                    if (length < bytes.Length) using (var memory = new MemoryStream(bytes, 0, length)) using (var reader = new StreamReader(memory, new UTF8Encoding(false, true), true)) { string text = reader.ReadToEnd(); if (text.Length <= MaximumCharacters && text.IndexOf('\0') < 0) content = text; }
                                }
                            }
                        } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (DecoderFallbackException) { }
                        files[file] = content;
                    }
                } catch (IOException) { complete = false; } catch (UnauthorizedAccessException) { complete = false; }
                if (visited >= 20000) { complete = false; break; }
            }
            if (pending.Count > 0) complete = false;
            return files;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DetectChanges
        /// desc         : 확인된 전후 내용만 diff로 만들고 한도 밖 또는 읽기 실패는 삭제로 오인하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 명령을 통한 소스 수정의 실제 근거.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal AgentFileChange[] DetectChanges()
        {
            var changes = new Dictionary<string, AgentFileChange>(StringComparer.OrdinalIgnoreCase); Reconcile(changes); return changes.Values.ToArray();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Reconcile
        /// desc         : 확인된 최종 소스 diff를 CLI 보고와 합치며 원상 복구된 파일은 순변경에서 제외한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 명령 수정과 CLI 외부 경로 보고의 병합.
        ///                2026-10-20 CodexOrig : TASK069 직접 수정 파일의 줄 끝을 원래 형식으로 맞춘 뒤 최종 비교.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Reconcile(IDictionary<string, AgentFileChange> changes, CancellationToken cancellation = default(CancellationToken), bool normalizeLineEndings = false)
        {
            bool afterComplete; var after = ReadFiles(directory, cancellation, out afterComplete);
            if (normalizeLineEndings) {
                foreach (var entry in after) {
                    cancellation.ThrowIfCancellationRequested();
                    string original; bool existed = before.TryGetValue(entry.Key, out original);
                    if (entry.Value == null || (existed && original == null) || (!existed && !complete) || original == entry.Value) continue;
                    NormalizeChangedFile(entry.Key, entry.Value, PreferredLineEnding(original ?? entry.Value), cancellation);
                }
                after = ReadFiles(directory, cancellation, out afterComplete);
            }
            foreach (string path in before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase)) {
                cancellation.ThrowIfCancellationRequested();
                string oldText, newText; bool had = before.TryGetValue(path, out oldText), has = after.TryGetValue(path, out newText);
                if (!had && !complete) continue;
                if (!has && !afterComplete) continue;
                if (!has && File.Exists(path)) continue;
                if ((had && oldText == null) || (has && newText == null)) continue;
                oldText = oldText ?? ""; newText = newText ?? "";
                if (oldText == newText && had == has) { changes.Remove(path); continue; }
                bool precise; string diff = BuildDiff(oldText, newText, cancellation, out precise);
                changes[path] = new AgentFileChange { Path = path, Kind = "프로젝트 전후 비교", Diff = diff, HasLineCounts = precise };
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PreferredLineEnding
        /// desc         : 기존 파일의 가장 많은 줄 끝을 따르며 동률은 Windows 형식을 사용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string PreferredLineEnding(string text)
        {
            int windows = 0, unix = 0, mac = 0;
            for (int index = 0; index < text.Length; index++) {
                if (text[index] == '\r') { if (index + 1 < text.Length && text[index + 1] == '\n') { windows++; index++; } else mac++; }
                else if (text[index] == '\n') unix++;
            }
            return windows >= unix && windows >= mac ? "\r\n" : unix >= mac ? "\n" : "\r";
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : NormalizeChangedFile
        /// desc         : 실행 전후 비교로 확인한 작은 텍스트만 인코딩·BOM·속성을 보존해 원자 교체한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-20
        /// </summary>
        /// ---------------------------------------------------------------------
        private void NormalizeChangedFile(string path, string expected, string newline, CancellationToken cancellation)
        {
            string normalized = expected.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", newline);
            if (normalized == expected) return;
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;
            for (string ancestor = path; !String.IsNullOrEmpty(ancestor); ancestor = Path.GetDirectoryName(ancestor)) {
                if ((File.GetAttributes(ancestor) & FileAttributes.ReparsePoint) != 0) return;
                if (String.Equals(ancestor.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) break;
            }
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.ReadOnly | FileAttributes.Directory)) != 0) return;
            string temporary = Path.Combine(Path.GetDirectoryName(path), ".ai-eol-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                // 다른 쓰기는 막고 File.Replace에 필요한 삭제 공유만 허용한다.
                using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) {
                    if (input.Length > MaximumCharacters * 4) return;
                    var bytes = new byte[(int)input.Length]; int read = 0, count;
                    while (read < bytes.Length && (count = input.Read(bytes, read, bytes.Length - read)) > 0) read += count;
                    if (read != bytes.Length || input.ReadByte() >= 0) return;
                    int prefix = 0; Encoding encoding = new UTF8Encoding(false, true);
                    if (bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xfe && bytes[2] == 0 && bytes[3] == 0) { encoding = new UTF32Encoding(false, false, true); prefix = 4; }
                    else if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xfe && bytes[3] == 0xff) { encoding = new UTF32Encoding(true, false, true); prefix = 4; }
                    else if (bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe) { encoding = new UnicodeEncoding(false, false, true); prefix = 2; }
                    else if (bytes.Length >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff) { encoding = new UnicodeEncoding(true, false, true); prefix = 2; }
                    else if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf) prefix = 3;
                    if (encoding.GetString(bytes, prefix, bytes.Length - prefix) != expected) return;
                    byte[] replacement = encoding.GetBytes(normalized);
                    cancellation.ThrowIfCancellationRequested();
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                        output.Write(bytes, 0, prefix); output.Write(replacement, 0, replacement.Length); output.Flush(true);
                    }
                    File.SetAttributes(temporary, attributes & ~FileAttributes.ReadOnly);
                    File.Replace(temporary, path, null);
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BuildDiff
        /// desc         : 공통 앞뒤 줄은 보존하고 실제 전후 텍스트를 변환하는 hunk를 만든다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 UTF 개행 및 빈 파일 비교.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string BuildDiff(string before, string after, CancellationToken cancellation, out bool precise)
        {
            Func<string, string[]> lines = value => {
                if (value.Length == 0) return new string[0];
                string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n'); var parts = normalized.Split('\n');
                return normalized.EndsWith("\n", StringComparison.Ordinal) ? parts.Take(parts.Length - 1).ToArray() : parts;
            };
            var oldLines = lines(before); var newLines = lines(after);
            var operations = FindLineChanges(oldLines, newLines, cancellation);
            if (operations != null) {
                precise = true;
                int first = operations.FindIndex(item => item.Item1 != ' '), last = operations.FindLastIndex(item => item.Item1 != ' ');
                int skip = Math.Max(0, first - 3), end = Math.Min(operations.Count, last + 4);
                var selected = first < 0 ? new List<Tuple<char, string>>() : operations.Skip(skip).Take(end - skip).ToList();
                int oldStart = 1 + operations.Take(skip).Count(item => item.Item1 != '+'), newStart = 1 + operations.Take(skip).Count(item => item.Item1 != '-');
                var exact = new StringBuilder("@@ -" + oldStart + "," + selected.Count(item => item.Item1 != '+') + " +" + newStart + "," + selected.Count(item => item.Item1 != '-') + " @@\n");
                foreach (var item in selected) exact.Append(item.Item1).AppendLine(item.Item2);
                if (before.EndsWith("\n", StringComparison.Ordinal) != after.EndsWith("\n", StringComparison.Ordinal)) exact.AppendLine("\\ 줄 끝 개행 변경");
                return exact.ToString();
            }
            precise = false; int prefix = 0, suffix = 0;
            while (prefix < Math.Min(oldLines.Length, newLines.Length) && oldLines[prefix] == newLines[prefix]) prefix++;
            while (suffix < Math.Min(oldLines.Length, newLines.Length) - prefix && oldLines[oldLines.Length - 1 - suffix] == newLines[newLines.Length - 1 - suffix]) suffix++;
            var result = new StringBuilder("@@ -" + (prefix + 1) + "," + (oldLines.Length - prefix - suffix) + " +" + (prefix + 1) + "," + (newLines.Length - prefix - suffix) + " @@\n");
            foreach (string line in oldLines.Skip(prefix).Take(oldLines.Length - prefix - suffix)) result.Append('-').AppendLine(line);
            foreach (string line in newLines.Skip(prefix).Take(newLines.Length - prefix - suffix)) result.Append('+').AppendLine(line);
            if (before.EndsWith("\n", StringComparison.Ordinal) != after.EndsWith("\n", StringComparison.Ordinal)) result.AppendLine("\\ 줄 끝 개행 변경");
            return result.ToString();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FindLineChanges
        /// desc         : 공통 중간 줄을 보존하는 Myers 비교를 유한 단계 안에서 수행한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : TASK-020 최소 행수 계수와 대형 변경의 미확인 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static List<Tuple<char, string>> FindLineChanges(string[] before, string[] after, CancellationToken cancellation)
        {
            var frontier = new Dictionary<int, int> { { 1, 0 } }; var trace = new List<Dictionary<int, int>>(); int work = 0;
            Func<IDictionary<int, int>, int, int> get = (values, key) => values.ContainsKey(key) ? values[key] : 0;
            for (int distance = 0; distance <= Math.Min(256, before.Length + after.Length); distance++) {
                cancellation.ThrowIfCancellationRequested(); trace.Add(new Dictionary<int, int>(frontier));
                for (int diagonal = -distance; diagonal <= distance; diagonal += 2) {
                    int x = diagonal == -distance || (diagonal != distance && get(frontier, diagonal - 1) < get(frontier, diagonal + 1)) ? get(frontier, diagonal + 1) : get(frontier, diagonal - 1) + 1;
                    int y = x - diagonal;
                    while (x < before.Length && y < after.Length && before[x] == after[y]) { x++; y++; if (++work > 4000000) return null; }
                    frontier[diagonal] = x;
                    if (x < before.Length || y < after.Length) continue;
                    var result = new List<Tuple<char, string>>(); x = before.Length; y = after.Length;
                    for (int step = trace.Count - 1; step >= 0; step--) {
                        cancellation.ThrowIfCancellationRequested(); var previous = trace[step]; int current = x - y;
                        int next = current == -step || (current != step && get(previous, current - 1) < get(previous, current + 1)) ? current + 1 : current - 1;
                        int previousX = get(previous, next), previousY = previousX - next;
                        while (x > previousX && y > previousY) { result.Add(Tuple.Create(' ', before[x - 1])); x--; y--; }
                        if (step == 0) break;
                        if (x == previousX) result.Add(Tuple.Create('+', after[--y])); else result.Add(Tuple.Create('-', before[--x]));
                    }
                    result.Reverse(); return result;
                }
            }
            return null;
        }
    }
}
