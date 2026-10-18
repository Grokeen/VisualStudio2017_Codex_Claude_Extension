using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
    /// update date  : 2026-10-18 CodexCode : 설정 경로별 named Mutex로 두 파일 거래의 ABA 동시 저장 손실 차단.
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
        /// update date  : 2026-10-18 CodexCode : 전체 두 파일 거래를 경로별 재진입 가능 Mutex로 감싸 협력 저장자의 ABA 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static void SaveCore(AssistantSettingsStore settingsStore, AssistantSettings settings, Action writeInstructions)
        {
            if (settingsStore == null) throw new ArgumentNullException(nameof(settingsStore));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (writeInstructions == null) throw new ArgumentNullException(nameof(writeInstructions));
            using (AcquireSettingsLock(settingsStore.FilePath))
            {
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
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AcquireSettingsLock
        /// desc         : 정규화한 설정 경로의 저장·읽기·복구를 같은 프로세스와 다른 창 사이에서 직렬화한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 5초 유한 대기·중첩 재진입·abandoned 획득과 비밀 원문 없는 오류 처리.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static IDisposable AcquireSettingsLock(string filePath)
        {
            if (String.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("설정 파일 경로를 지정하세요.", nameof(filePath));
            string normalized = Path.GetFullPath(filePath).ToUpperInvariant();
            string name;
            using (var hash = SHA256.Create()) name = "Local\\VS2017AISettings-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(normalized))).Replace("-", "");
            var mutex = new Mutex(false, name);
            try
            {
                bool acquired;
                try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) throw new IOException("다른 창에서 설정을 저장하고 있습니다. 잠시 후 다시 시도하세요.");
                return new SettingsLock(mutex);
            }
            catch { mutex.Dispose(); throw; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SettingsLock
        /// desc         : 획득한 설정 Mutex의 한 번의 재진입 소유권을 using 범위 종료 때 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실패·성공 경로 모두 Mutex와 운영체제 핸들 해제.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class SettingsLock : IDisposable
        {
            private readonly Mutex mutex;
            private bool disposed;

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : SettingsLock
            /// desc         : 현재 스레드가 이미 획득한 Mutex만 범위 소유권으로 받는다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : Store.Save의 같은 스레드 중첩 획득 지원.
            /// </summary>
            /// ---------------------------------------------------------------------
            internal SettingsLock(Mutex mutex) { this.mutex = mutex; }

            /// ---------------------------------------------------------------------
            /// <summary>
            /// name         : Dispose
            /// desc         : 획득 스레드의 중첩 소유권 한 번만 반환하고 핸들을 닫는다.
            /// author       : CodexCode
            /// create date  : 2026-10-18
            /// update date  : 2026-10-18 CodexCode : 반복 Dispose에도 과도한 ReleaseMutex를 수행하지 않음.
            /// </summary>
            /// ---------------------------------------------------------------------
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                try { mutex.ReleaseMutex(); }
                finally { mutex.Dispose(); }
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
