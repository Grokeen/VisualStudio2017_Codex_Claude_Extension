using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommentCodePreview.Poc;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;

namespace VS2017.AIAssistant.Providers
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CommentCodeProviderAdapter
    /// desc         : 전체 권한 공급자를 계승하지 않고 승인형 CLI 응답에서 정확한 단일 위치 변경만 추출한다.
    /// author       : CodexOrig
    /// create date  : 2026-11-07
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class CommentCodeProviderAdapter : ICommentCodeProvider
    {
        private readonly IAiContextEditProvider proposals;
        private readonly string provider, executable, fileName;
        private readonly ChatRequestContext context;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CommentCodeProviderAdapter
        /// desc         : 명시적 전송 동의 뒤 호출자가 고정한 CLI와 모델만 사용하며 대화·첨부·전체 액세스는 전달하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        internal CommentCodeProviderAdapter(string provider, string executable, string fileName, string model)
            : this(new CliEditProvider(TimeSpan.FromMinutes(5), true), provider, executable, fileName, model) { }

        internal CommentCodeProviderAdapter(IAiContextEditProvider proposals, string provider, string executable, string fileName, string model)
        {
            if (proposals == null) throw new ArgumentNullException(nameof(proposals));
            if (provider != "Codex" && provider != "Claude") throw new ArgumentException("지원하지 않는 공급자입니다.");
            this.proposals = proposals; this.provider = provider; this.executable = executable;
            this.fileName = Path.GetFileName(fileName ?? "source.cs");
            context = new ChatRequestContext(model: model);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GenerateAsync
        /// desc         : 명시한 주석과 제한된 버퍼만 전송하고 원본 prefix와 suffix가 정확히 유지된 결과만 반환한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        public async Task<string> GenerateAsync(CommentCodeRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            EditorSnapshot captured = request.Snapshot;
            if (captured.BlockReason != PreviewReason.None) throw new InvalidOperationException("편집할 수 없는 문서입니다.");
            int start = captured.Cursor;
            var source = new DocumentSnapshot(Path.Combine(Path.GetTempPath(), fileName), captured.Content,
                "not-a-disk-version", "C# 7.0 compatible; manual comment-to-code preview only", true, false,
                DocumentSelection.FromOffsets(captured.Content, captured.SelectionStart, captured.SelectionLength));
            string instruction = "Generate code from the explicit comment below. Replace only the exact UTF-16 range [" +
                start + "," + start + ") in source_text: insert there without deleting any selected text. " +
                "Selection is reference context only. Preserve every character before and after the cursor, including the comment and line endings. " +
                "Return the existing summary/edits JSON format, using unique surrounding unchanged context as old_text if insertion needs an anchor. " +
                "Do not access files, tools, commands or save anything.\nExplicit comment:\n" + request.Instruction;
            EditProposal proposal = await proposals.GenerateWithContextAsync(provider, executable, source, instruction, context, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (proposal == null || proposal.Source != source) throw new InvalidOperationException("다른 원문의 응답을 거절했습니다.");
            return ExtractInsertion(captured, proposal.ProposedText);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ExtractInsertion
        /// desc         : AI가 문서 전체 변경안을 반환해도 허용한 커서·선택 바깥 변경은 거절한다.
        /// author       : CodexOrig
        /// create date  : 2026-11-07
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static string ExtractInsertion(EditorSnapshot source, string proposed)
        {
            int start = source.Cursor;
            string prefix = source.Content.Substring(0, start);
            string suffix = source.Content.Substring(start);
            if (proposed == null || proposed.Length < prefix.Length + suffix.Length ||
                !proposed.StartsWith(prefix, StringComparison.Ordinal) || !proposed.EndsWith(suffix, StringComparison.Ordinal))
                throw new InvalidOperationException("AI가 지정 위치 밖을 변경하여 제안을 거절했습니다.");
            string insertion = proposed.Substring(start, proposed.Length - prefix.Length - suffix.Length);
            if (insertion.Length > PreviewLimits.SuggestionCharacters || System.Text.Encoding.UTF8.GetByteCount(insertion) > PreviewLimits.SuggestionUtf8Bytes)
                throw new InvalidOperationException("제안 크기 제한을 초과했습니다.");
            if (String.Equals(proposed, source.Content, StringComparison.Ordinal)) throw new InvalidOperationException("변경할 코드가 없습니다.");
            return insertion;
        }
    }
}
