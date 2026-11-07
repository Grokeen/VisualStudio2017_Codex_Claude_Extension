using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace CodexOrig.Task073.Validation
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ManagedCandidateResolver
    /// desc         : PowerShell 콜백 없이 명시한 읽기 전용 SDK 폴더에서 검사 의존성을 찾는다.
    /// author       : CodexOrig
    /// create date  : 2026-11-03
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class ManagedCandidateResolver : IDisposable
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Resolution
        /// desc         : 명시 SDK 후보와 실제 로드 파일의 식별자 및 검증 결과만 기록한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        public sealed class Resolution
        {
            public string RequestedIdentity;
            public string CandidatePath;
            public string CandidateSha256;
            public string LoadedIdentity;
            public string LoadedPath;
            public string LoadedSha256;
            public string ErrorType;
            public int HResult;
            public bool Succeeded;
        }

        private readonly string[] roots;
        private readonly List<Resolution> records = new List<Resolution>();
        private bool disposed;
        [ThreadStatic] private static HashSet<string> activeNames;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ManagedCandidateResolver
        /// desc         : 후보 폴더와 명시 SDK 폴더만 등록하고 이 검사 프로세스의 해결 이벤트를 구독한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        public ManagedCandidateResolver(string productDll, string[] runtimeDirectories)
        {
            var directories = new List<string>();
            directories.Add(Path.GetDirectoryName(Path.GetFullPath(productDll)));
            foreach (string directory in runtimeDirectories ?? new string[0])
                directories.Add(Path.GetFullPath(directory));
            foreach (string directory in directories)
                if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("An explicit dependency directory is missing.");
            roots = directories.ToArray();
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Resolve
        /// desc         : 일치하는 관리 DLL만 읽고 실제 로드 위치와 SHA를 기록하며 재귀 해결을 차단한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private Assembly Resolve(object sender, ResolveEventArgs args)
        {
            if (disposed) return null;
            var requested = new AssemblyName(args.Name);
            string name = requested.Name;
            if (String.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.GetFileName(name) != name) return null;
            if (activeNames == null) activeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!activeNames.Add(name)) return null;
            var record = new Resolution { RequestedIdentity = requested.FullName };
            try
            {
                foreach (string root in roots)
                {
                    string path = Path.Combine(root, name + ".dll");
                    if (!File.Exists(path)) continue;
                    AssemblyName identity;
                    try { identity = AssemblyName.GetAssemblyName(path); }
                    catch (BadImageFormatException) { continue; }
                    if (!AssemblyName.ReferenceMatchesDefinition(requested, identity)) continue;
                    record.CandidatePath = path;
                    record.CandidateSha256 = Hash(path);
                    var assembly = Assembly.LoadFrom(path);
                    record.LoadedIdentity = assembly.FullName;
                    record.LoadedPath = assembly.Location;
                    record.LoadedSha256 = Hash(assembly.Location);
                    if (record.CandidateSha256 != Hash(path) || record.CandidateSha256 != record.LoadedSha256)
                        throw new InvalidDataException("The resolved assembly bytes differ from the explicit input.");
                    record.Succeeded = true;
                    return assembly;
                }
                return null;
            }
            catch (Exception error)
            {
                record.ErrorType = error.GetType().FullName;
                record.HResult = error.HResult;
                return null;
            }
            finally
            {
                lock (records) records.Add(record);
                activeNames.Remove(name);
            }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Hash
        /// desc         : 명시 입력 파일의 SHA256을 읽기만 한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string Hash(string path)
        {
            using (var input = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GetRecords
        /// desc         : 현재 검사 프로세스에서 관찰한 SDK 해결 기록의 사본을 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        public Resolution[] GetRecords() { lock (records) return records.ToArray(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 이 객체가 구독한 해결 이벤트만 해제한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-03
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
        }
    }
}
