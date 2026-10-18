using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace VS2017.AIAssistant.Chat
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssistantSettingsTransaction
    /// desc         : 개인 설정과 공통 Markdown 규칙을 저장하고 후속 실패 시 이번 JSON 변경을 복구한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 기존 원문 보존 및 외부 변경을 덮어쓰지 않는 실패 처리 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static class AssistantSettingsTransaction
    {
        private const int MaximumSettingsBytes = 65536;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Save
        /// desc         : JSON을 먼저 저장하고 공통 규칙 저장까지 성공해야 전체 저장을 완료한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Markdown 실패 시 JSON의 원래 바이트 또는 없음 상태 복구.
        /// </summary>
        /// ---------------------------------------------------------------------
        public static void Save(AssistantSettingsStore settingsStore, MarkdownWorkspaceStore workspace, AssistantSettings settings)
        {
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            SaveCore(settingsStore, settings, () => workspace.SaveInstructions(settings.Instructions));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SaveCore
        /// desc         : 두 번째 저장 경계를 분리하여 실제 파일 잠금과 외부 변경의 실패 처리를 검증한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 우리가 쓴 JSON과 현재 파일이 같을 때만 롤백 수행.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static void SaveCore(AssistantSettingsStore settingsStore, AssistantSettings settings, Action writeInstructions)
        {
            if (settingsStore == null) throw new ArgumentNullException(nameof(settingsStore));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (writeInstructions == null) throw new ArgumentNullException(nameof(writeInstructions));
            byte[] previous = ReadSnapshot(settingsStore.FilePath);
            byte[] expected = new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(settings));
            if (expected.Length > MaximumSettingsBytes) throw new InvalidDataException("설정 파일은 UTF-8 기준 64KB 이하로 저장해야 합니다.");
            settingsStore.Save(settings);
            try { writeInstructions(); }
            catch (Exception markdownError)
            {
                Exception rollbackError = null;
                try
                {
                    byte[] current = ReadSnapshot(settingsStore.FilePath);
                    if (current == null || !current.SequenceEqual(expected))
                        throw new IOException("다른 작업에서 변경하거나 삭제한 설정 파일을 보존하기 위해 복구를 중단했습니다.");
                    if (previous == null) File.Delete(settingsStore.FilePath);
                    else RestoreSnapshot(settingsStore.FilePath, previous, expected);
                }
                catch (Exception error) { rollbackError = error; }
                if (rollbackError != null)
                    throw new InvalidOperationException("설정이 일부만 저장되었습니다. 공통 규칙 저장 오류: " + markdownError.Message + " 기본 설정 복구 오류: " + rollbackError.Message,
                        new AggregateException(markdownError, rollbackError));
                throw new InvalidOperationException("공통 규칙을 저장하지 못해 기본 설정 변경을 원래 상태로 복구했습니다. " + markdownError.Message, markdownError);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReadSnapshot
        /// desc         : 설정을 해석하지 않고 64KB 이내 원문 바이트나 파일 없음 상태를 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 손상 JSON·추가 필드·BOM도 그대로 복원하도록 원문 보관.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static byte[] ReadSnapshot(string path)
        {
            try
            {
                using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var memory = new MemoryStream())
                {
                    if (input.Length > MaximumSettingsBytes) throw new InvalidDataException("기존 설정 파일이 64KB를 초과하여 변경하지 않았습니다.");
                    byte[] buffer = new byte[4096];
                    int count;
                    while ((count = input.Read(buffer, 0, Math.Min(buffer.Length, MaximumSettingsBytes + 1 - (int)memory.Length))) > 0)
                    {
                        memory.Write(buffer, 0, count);
                        if (memory.Length > MaximumSettingsBytes) throw new InvalidDataException("기존 설정 파일이 64KB를 초과하여 변경하지 않았습니다.");
                    }
                    return memory.ToArray();
                }
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RestoreSnapshot
        /// desc         : 같은 폴더의 임시 파일을 원자 교체하여 JSON 원문을 복구한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 교체 직전 현재 바이트를 다시 확인하고 임시 파일 정리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RestoreSnapshot(string path, byte[] previous, byte[] expected)
        {
            string temporary = path + ".rollback-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, previous);
                byte[] current = ReadSnapshot(path);
                if (current == null || !current.SequenceEqual(expected)) throw new IOException("설정 파일이 바뀌어 다른 작업의 변경을 보존했습니다.");
                File.Replace(temporary, path, null);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
