using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : WorkerOutputChecks
/// desc         : 합성 APM 스트림과 격리 프로세스로 무개행 출력의 고정 메모리 배수를 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 사용자 문서·계정·필터 없이 제품 출력 경계를 직접 실행.
/// </summary>
/// ---------------------------------------------------------------------
internal static class WorkerOutputChecks
{
    private const int PayloadBytes = 8 * 1024 * 1024;
    private const int ExpectedChecks = 12;
    private static readonly List<object> checks = new List<object>();
    private static MethodInfo drain;
    private static int passed;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 고립 폴더만 검사하고 실패를 포함한 JSON 증거를 항상 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 제품 DLL과 독립 소스 컴파일 양쪽을 같은 검사로 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        var clock = Stopwatch.StartNew();
        string failure = null;
        try { Run(args[0], args[1]); }
        catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(failure); }
        File.WriteAllText(Path.Combine(args[0], "result.json"), new JavaScriptSerializer().Serialize(new
        {
            Passed = passed, Total = ExpectedChecks, Success = failure == null && passed == ExpectedChecks,
            Checks = checks, Failure = failure, ElapsedMilliseconds = clock.ElapsedMilliseconds,
            SubjectAssembly = typeof(DocumentAttachmentReader).Assembly.Location,
            SubjectHash = Hash(typeof(DocumentAttachmentReader).Assembly.Location),
            NoUserDocuments = true, NoRealFilter = true, OutputBytesPerPipe = PayloadBytes
        }), new UTF8Encoding(false));
        if (failure != null) return 1;
        Console.WriteLine("PASS " + passed + " worker output checks");
        return 0;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Run
    /// desc         : 동기·비동기 완료와 예외를 확인한 뒤 실제 별도 프로세스의 두 파이프를 끝까지 배수한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : EndRead 중복·동기 콜백 재귀·무제한 출력 저장 회귀를 탐지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Run(string output, string sandbox)
    {
        Assembly subject = typeof(DocumentAttachmentReader).Assembly;
        Type launcher = subject.GetType("VS2017.AIAssistant.Chat.DocumentWorkerLauncher", true);
        drain = launcher.GetMethod("DrainDiscardedOutput", BindingFlags.NonPublic | BindingFlags.Static);
        Check(drain != null && string.Equals(Path.GetDirectoryName(subject.Location), sandbox, StringComparison.OrdinalIgnoreCase)
            && File.Exists(Path.Combine(sandbox, "VS2017.AIAssistant.DocumentReader.exe")),
            "isolated-copy", "제품 복사 DLL과 합성 worker가 전용 sandbox에만 존재한다.");

        using (var sync = new CounterStream(PayloadBytes, 4096, false, null, null))
        {
            Drain(sync);
            Check(sync.BytesDrained == PayloadBytes && sync.EofCount == 1, "sync-8mb-eof", "동기 완료 8MB 무개행 출력과 EOF를 모두 소비한다.");
            Check(sync.MaxRead == 4096 && sync.SameBuffer && sync.FirstBuffer != null && sync.FirstBuffer.Length == 4096,
                "fixed-4kb-buffer", "최대 4096바이트 읽기와 단일 버퍼 재사용을 유지한다.");
            Check(sync.BeginCalls == PayloadBytes / 4096 + 1 && sync.EndCalls == sync.BeginCalls && sync.DuplicateEndCalls == 0
                && sync.MaxBeginDepth == 1 && sync.CallbackCalls == sync.BeginCalls,
                "sync-no-recursion-or-double-end", "동기 콜백은 중복 EndRead 또는 읽기 재귀를 만들지 않는다.");
        }

        Type state = launcher.GetNestedType("DiscardedOutputReader", BindingFlags.NonPublic);
        FieldInfo[] fields = state == null ? new FieldInfo[0] : state.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Check(fields.Length == 2 && fields.Count(field => field.FieldType == typeof(byte[])) == 1
            && fields.Count(field => typeof(Stream).IsAssignableFrom(field.FieldType)) == 1,
            "no-output-accumulator", "배수 상태는 Stream과 byte[]만 보유하고 문자열·누적 출력 저장소를 두지 않는다.");

        using (var async = new CounterStream(65539, 137, true, null, null))
        {
            Drain(async);
            Wait(async);
            Check(async.BytesDrained == 65539 && async.EofCount == 1 && async.BeginCalls == async.EndCalls
                && async.DuplicateEndCalls == 0 && async.SameBuffer && async.MaxRead == 4096 && async.CallbackError == null,
                "async-chunks-eof", "비동기 작은 조각과 마지막 불완전 조각·EOF를 정확히 한 번씩 소비한다.");
        }

        using (var broken = new CounterStream(10, 10, false, "io", null))
        { Drain(broken); Check(broken.BeginCalls == 1 && broken.EndCalls == 0, "begin-io-safe", "BeginRead의 I/O 종료를 호출자 예외 없이 정리한다."); }
        using (var closed = new CounterStream(10, 10, false, null, "disposed"))
        { Drain(closed); Check(closed.EndCalls == 1 && closed.BytesDrained == 0, "sync-dispose-safe", "동기 EndRead의 폐기 경합을 정리한다."); }
        using (var broken = new CounterStream(10, 10, true, null, "io"))
        { Drain(broken); Wait(broken); Check(broken.EndCalls == 1 && broken.CallbackError == null && broken.BeginCalls == 1, "async-io-safe", "비동기 EndRead의 I/O 종료가 콜백 밖으로 유출되지 않는다."); }
        using (var closed = new CounterStream(10, 10, true, null, "disposed"))
        { Drain(closed); Wait(closed); Check(closed.EndCalls == 1 && closed.CallbackError == null && closed.BeginCalls == 1, "async-dispose-safe", "비동기 EndRead의 폐기 경합을 안전하게 종료한다."); }

        string synthetic = Path.Combine(sandbox, "synthetic.pdf");
        File.WriteAllText(synthetic, "%PDF-WORKER-OUTPUT-SYNTHETIC\n", new UTF8Encoding(false));
        string originalHash = Hash(synthetic);
        var clock = Stopwatch.StartNew();
        string body = DocumentAttachmentReader.Extract(synthetic);
        string marker = Path.Combine(sandbox, "worker-completed.txt");
        string[] evidence = File.Exists(marker) ? File.ReadAllLines(marker, Encoding.UTF8) : new string[0];
        Check(body == "WORKER_NO_NEWLINE_BODY 한글" && evidence.Length == 4
            && evidence[0] == PayloadBytes.ToString() && evidence[1] == PayloadBytes.ToString() && clock.Elapsed < TimeSpan.FromSeconds(15),
            "real-two-pipe-8mb", "별도 worker의 stdout·stderr 각 8MB 무개행 출력 후 본문이 제한 시간 내 정상 반환된다.");
        Check(Hash(synthetic) == originalHash && !File.Exists(evidence[2]) && !File.Exists(evidence[3])
            && !Directory.Exists(Path.GetDirectoryName(evidence[2])),
            "synthetic-source-and-temp-cleanup", "합성 원본은 그대로 유지하고 이번 추출의 임시 스냅샷·본문·폴더는 정리한다.");
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Drain
    /// desc         : 내부 제품 helper를 reflection으로 호출하되 실제 예외를 검증자에게 전달한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 테스트를 위해 제품 공개 API를 늘리지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Drain(Stream stream)
    {
        try { drain.Invoke(null, new object[] { stream }); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Wait
    /// desc         : 비동기 fixture 콜백이 종료되었는지 유한 시간 동안만 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 무응답 시 테스트를 무기한 대기시키지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Wait(CounterStream stream)
    { if (!stream.Finished.WaitOne(5000)) throw new TimeoutException("합성 비동기 읽기가 5초 안에 끝나지 않았습니다."); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 의미 있는 경계별 결과를 기록하고 최초 실패에서 중단한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실패한 조건도 결과 JSON에 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool condition, string id, string description)
    {
        checks.Add(new { Id = id, Passed = condition, Description = description });
        if (!condition) throw new InvalidOperationException("FAIL " + id + ": " + description);
        passed++;
        Console.WriteLine("PASS " + id);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Hash
    /// desc         : 합성 파일과 실제 참조 DLL의 무결성 증거를 SHA256으로 계산한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 파일 내용이나 사용자 계정 정보는 출력하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Hash(string path)
    { using (var algorithm = SHA256.Create()) using (var input = File.OpenRead(path)) return BitConverter.ToString(algorithm.ComputeHash(input)).Replace("-", ""); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CounterStream
    /// desc         : 대용량 본문 저장 없이 APM 완료 방식·바이트 수·버퍼·예외 경계를 계측한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 동기 완료 콜백도 실제 호출하여 재귀·중복 종료 회귀 탐지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class CounterStream : Stream
    {
        private readonly long size;
        private readonly int chunk;
        private readonly bool asynchronous;
        private readonly string beginFault, endFault;
        private long position;
        private int beginDepth;
        internal readonly ManualResetEvent Finished = new ManualResetEvent(false);
        internal int BeginCalls, EndCalls, DuplicateEndCalls, MaxRead, MaxBeginDepth, CallbackCalls, EofCount;
        internal long BytesDrained;
        internal byte[] FirstBuffer;
        internal bool SameBuffer = true;
        internal Exception CallbackError;
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return size; } }
        public override long Position { get { return position; } set { throw new NotSupportedException(); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CounterStream
        /// desc         : 바이트 크기·조각·완료 모드와 고정된 예외 종류만 받아 합성 스트림을 구성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 외부 파일·네트워크를 열지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal CounterStream(long size, int chunk, bool asynchronous, string beginFault, string endFault)
        { this.size = size; this.chunk = chunk; this.asynchronous = asynchronous; this.beginFault = beginFault; this.endFault = endFault; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BeginRead
        /// desc         : 동일 버퍼를 검사하고 동기 또는 ThreadPool 콜백으로 제한된 조각을 완료한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 잘못된 동기 재귀는 깊이 경계에서 차단하여 테스트 프로세스를 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            int depth = Interlocked.Increment(ref beginDepth);
            try
            {
                MaxBeginDepth = Math.Max(MaxBeginDepth, depth);
                if (!asynchronous && depth > 64) throw new InvalidOperationException("합성 스트림에서 과도한 동기 콜백 재귀가 감지되었습니다.");
                BeginCalls++;
                MaxRead = Math.Max(MaxRead, count);
                if (FirstBuffer == null) FirstBuffer = buffer; else SameBuffer &= ReferenceEquals(FirstBuffer, buffer);
                if (beginFault == "io") throw new IOException("SYNTHETIC_BEGIN_IO");
                if (offset != 0 || count <= 0 || count > buffer.Length) throw new InvalidOperationException("잘못된 읽기 경계입니다.");
                int length = (int)Math.Min(Math.Min(count, chunk), size - position);
                for (int i = 0; i < length; i++) buffer[offset + i] = (byte)'Z';
                position += length;
                var result = new CounterResult(length, !asynchronous, state);
                if (asynchronous) ThreadPool.QueueUserWorkItem(ignored => Complete(result, callback));
                else Complete(result, callback);
                return result;
            }
            finally { Interlocked.Decrement(ref beginDepth); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Complete
        /// desc         : 실제 제품 콜백의 예외를 수집하고 마지막 완료 뒤에만 대기자를 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 콜백 실패가 ThreadPool 미처리 예외로 테스트를 종료하지 않게 분리.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Complete(CounterResult result, AsyncCallback callback)
        {
            try { result.Complete(); Interlocked.Increment(ref CallbackCalls); callback(result); }
            catch (Exception error) { CallbackError = error; }
            finally { if (result.Count == 0 || endFault != null || CallbackError != null) Finished.Set(); }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EndRead
        /// desc         : 완료당 한 번 종료하는지 확인하고 지정된 I/O·폐기 경합을 재현한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 성공한 읽기만 누적 바이트 수에 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        public override int EndRead(IAsyncResult asyncResult)
        {
            var result = (CounterResult)asyncResult;
            Interlocked.Increment(ref EndCalls);
            if (Interlocked.Exchange(ref result.Ended, 1) != 0) { Interlocked.Increment(ref DuplicateEndCalls); throw new InvalidOperationException("중복 EndRead"); }
            if (endFault == "io") throw new IOException("SYNTHETIC_END_IO");
            if (endFault == "disposed") throw new ObjectDisposedException("SYNTHETIC_END_DISPOSED");
            BytesDrained += result.Count;
            if (result.Count == 0) EofCount++;
            return result.Count;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 테스트가 소유한 완료 이벤트만 정리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 외부 스트림과 프로세스를 건드리지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        protected override void Dispose(bool disposing) { if (disposing) Finished.Dispose(); base.Dispose(disposing); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Flush / Read / Seek / SetLength / Write
        /// desc         : APM 검증 경로 외 동기 읽기·쓰기·탐색을 금지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 제품이 검증 대상 APM 경로를 우회하지 못하도록 구성.
        /// </summary>
        /// ---------------------------------------------------------------------
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CounterResult
    /// desc         : 한 APM 요청의 완료·EndRead 횟수만 보관한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 출력 본문을 저장하지 않는 가벼운 합성 완료 정보.
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class CounterResult : IAsyncResult
    {
        internal readonly int Count;
        internal int Ended;
        private int complete;
        private readonly object state;
        private readonly bool synchronous;
        public object AsyncState { get { return state; } }
        public bool CompletedSynchronously { get { return synchronous; } }
        public bool IsCompleted { get { return Volatile.Read(ref complete) != 0; } }
        public WaitHandle AsyncWaitHandle { get { throw new NotSupportedException("fixture는 콜백 완료만 사용합니다."); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CounterResult
        /// desc         : 결과 크기와 동기 완료 플래그를 고정한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : Stream APM 콜백 경계 재현.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal CounterResult(int count, bool synchronous, object state) { Count = count; this.synchronous = synchronous; this.state = state; }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Complete
        /// desc         : 콜백 호출 전에 결과를 완료 상태로 공개한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 스레드 간 완료 관측 순서 보장.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal void Complete() { Volatile.Write(ref complete, 1); }
    }
}
