using System.Threading;
using System.Threading.Tasks;

namespace CommentCodePreview.Poc
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MockCommentCodeProvider
    /// desc         : 생성자에 명시한 고정 모의 응답만 반환하며 네트워크·CLI·모델·계정을 사용하지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class MockCommentCodeProvider : ICommentCodeProvider
    {
        private readonly string text;
        private int calls;
        public int Calls { get { return Volatile.Read(ref calls); } }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MockCommentCodeProvider
        /// desc         : 익명 합성 응답만 보관하고 외부 공급자 탐색은 하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public MockCommentCodeProvider(string text) { this.text = text; }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GenerateAsync
        /// desc         : 취소되지 않은 명시 호출에 고정 문자열 Task만 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public Task<string> GenerateAsync(CommentCodeRequest request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Interlocked.Increment(ref calls); return Task.FromResult(text); }
    }
}
