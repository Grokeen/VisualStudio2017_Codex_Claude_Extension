// [기능 폴더] Features/Markdown
// [역할] 코드 블록의 언어별 구문 색상을 연결합니다.
// [읽는 순서] 언어 판별 → 구문 정의 → 편집기 색상
// [안내] 같은 폴더의 README.md에 관련 파일·연결 지점이 있습니다.
// Markdownn 코드 펜스의 SQL·일반 소스 토큰을 편집기와 미리보기에 같은 팔레트로 표시합니다.
// 출처: 사용자 제공 Markdown_Memo_Interchange/RemoteControlDemo/Features/Markdown/MarkdownnCodeHighlighting.cs.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;

namespace VS2017.AIAssistant.Views
{
    /// <summary>
    /// name         : MarkdownnCodeKind
    /// desc         : 원본 Markdownn의 일반·SQL·소스 코드 분류를 유지한다.
    /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK024 원본 분류 계약 이식.
    /// </summary>
    internal enum MarkdownnCodeKind { Plain, Sql, Source }
    /// <summary>
    /// name         : MarkdownnTokenKind
    /// desc         : 원본 코드 토큰의 색상 의미를 유지한다.
    /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK024 키워드·문자열·숫자·주석·타입.
    /// </summary>
    internal enum MarkdownnTokenKind { Keyword, String, Number, Comment, Type }

    /// <summary>
    /// name         : MarkdownnCodeToken
    /// desc         : 원문 UTF16 위치와 길이를 가진 불변 색상 구간이다.
    /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 입력을 변경하거나 실행하지 않음.
    /// </summary>
    internal struct MarkdownnCodeToken
    {
        internal readonly int Start, Length;
        internal readonly MarkdownnTokenKind Kind;
        /// <summary>
        /// name         : MarkdownnCodeToken
        /// desc         : 검증된 정규식 매치의 원문 범위만 보관한다.
        /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 원본 토큰 API 유지.
        /// </summary>
        internal MarkdownnCodeToken(int start, int length, MarkdownnTokenKind kind) { Start = start; Length = length; Kind = kind; }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownnCodeHighlighting
    /// desc         : 원본 SQL·소스 팔레트와 토큰을 재사용하며 입력·정규식·문서 처리량을 제한한다.
    /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : .NET462/API 유지, timeout·XSHD 안전 로더·문서 버전 캐시 추가.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class MarkdownnCodeHighlighting
    {
        internal const int MaximumLineLength = 32768, MaximumTokens = 2048, MaximumTokenLength = 4096;
        internal const int MaximumDocumentLength = 2 * 1024 * 1024, MaximumDocumentLines = 50000;
        internal static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);
        private static readonly Regex SqlTokens = new Regex(
            @"(?<string>'(?:''|[^'])*'|""(?:""""|[^""])*"")|(?<comment>--.*$)|(?<number>\b\d+(?:\.\d+)?\b)|(?<keyword>\b(?:select|from|where|join|left|right|inner|outer|on|insert|into|values|update|set|delete|create|alter|drop|table|view|index|as|and|or|not|null|is|in|like|between|group|by|order|having|limit|offset|top|distinct|union|case|when|then|else|end|count|sum|avg|min|max|primary|key|foreign|references|database|with|recursive|returning|begin|commit|rollback)\b)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Regex SourceTokens = new Regex(
            @"(?<string>@?""(?:""""|\\.|[^""])*""|'(?:\\.|[^'\\])*')|(?<comment>//.*$|#.*$|/\*.*?\*/)|(?<type></?[A-Za-z][A-Za-z0-9:-]*\b)|(?<number>\b\d+(?:\.\d+)?\b)|(?<keyword>\b(?:using|namespace|class|struct|interface|enum|public|private|protected|internal|static|void|int|string|double|float|decimal|bool|var|new|return|if|else|for|foreach|while|do|switch|case|break|continue|try|catch|finally|throw|async|await|this|base|null|true|false|function|const|let|export|import|default|def|lambda|self|None|True|False|package|extends|implements|fn|mut|pub|impl|use)\b)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Brush PlainBrush = Paint("#D1FAE5"), SqlBrush = Paint("#E0F2FE"), SourceBrush = Paint("#E2E8F0");
        private static readonly Brush KeywordBrush = Paint("#C4B5FD"), StringBrush = Paint("#FCD34D"), NumberBrush = Paint("#67E8F9"), CommentBrush = Paint("#94A3B8"), TypeBrush = Paint("#7DD3FC");
        private static readonly Brush[] LightBaseBrushes = { Paint("#166534"), Paint("#0B5D80"), Paint("#1F2937") };
        private static readonly Brush[] LightTokenBrushes = { Paint("#6D28D9"), Paint("#8A5700"), Paint("#006B7B"), Paint("#59636F"), Paint("#086F9C") };
        private static readonly ConditionalWeakTable<TextDocument, DocumentSafety> SafeDocuments = new ConditionalWeakTable<TextDocument, DocumentSafety>();

        /// <summary>
        /// name         : DocumentSafety
        /// desc         : 같은 문서 버전의 행 상한 검사를 반복하지 않는 약한 캐시 값이다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 문서를 전역 strong key로 보존하지 않음.
        /// </summary>
        private sealed class DocumentSafety { internal ITextSourceVersion Version; internal bool Safe; }

        /// <summary>
        /// name         : Language
        /// desc         : 원본 fence 언어 별칭을 최대 128자 정보 문자열에서 판별한다.
        /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 초대형 info를 Split하기 전에 제한.
        /// </summary>
        internal static MarkdownnCodeKind Language(string info)
        {
            info = info ?? string.Empty; if (info.Length > 128) info = info.Substring(0, 128);
            string[] first = info.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string language = first.Length == 0 ? string.Empty : first[0].Trim('{', '}', '.').ToLowerInvariant();
            switch (language)
            {
                case "sql": case "tsql": case "mysql": case "postgres": case "postgresql":
                case "sqlite": case "plsql": case "mssql": case "sqlserver": case "db": case "query": return MarkdownnCodeKind.Sql;
                case "": case "text": case "plain": case "none": return MarkdownnCodeKind.Plain;
                default: return MarkdownnCodeKind.Source;
            }
        }

        /// <summary>
        /// name         : Tokens
        /// desc         : 원본 패턴의 유효 토큰만 반환하며 100ms·2048개·개별4096자 한도로 강조를 생략한다.
        /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : timeout은 입력 손상 없이 빈 강조로 처리.
        /// </summary>
        internal static IEnumerable<MarkdownnCodeToken> Tokens(string line, MarkdownnCodeKind kind)
        {
            var tokens = new List<MarkdownnCodeToken>(); line = line ?? string.Empty;
            if (kind == MarkdownnCodeKind.Plain || line.Length > MaximumLineLength) return tokens;
            Regex regex = kind == MarkdownnCodeKind.Sql ? SqlTokens : SourceTokens;
            var timer = Stopwatch.StartNew();
            try
            {
                foreach (Match match in regex.Matches(line))
                {
                    if (tokens.Count >= MaximumTokens || timer.ElapsedMilliseconds >= 100) break;
                    MarkdownnTokenKind type = match.Groups["string"].Success ? MarkdownnTokenKind.String : match.Groups["comment"].Success ? MarkdownnTokenKind.Comment : match.Groups["number"].Success ? MarkdownnTokenKind.Number : match.Groups["type"].Success ? MarkdownnTokenKind.Type : MarkdownnTokenKind.Keyword;
                    if (match.Length <= MaximumTokenLength) tokens.Add(new MarkdownnCodeToken(match.Index, match.Length, type));
                    if (type == MarkdownnTokenKind.Comment) break;
                }
            }
            catch (RegexMatchTimeoutException) { tokens.Clear(); }
            return tokens;
        }

        /// <summary>
        /// name         : Base
        /// desc         : 원본 언어별 기본 색을 공유 frozen brush로 제공한다.
        /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 원본 SQL·소스·일반 팔레트 보존.
        /// </summary>
        internal static Brush Base(MarkdownnCodeKind kind) { return kind == MarkdownnCodeKind.Sql ? SqlBrush : kind == MarkdownnCodeKind.Source ? SourceBrush : PlainBrush; }
        /// <summary>
        /// name         : Base
        /// desc         : Light 편집기에는 충분히 어두운 언어 기본색을 선택한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 원본 dark 팔레트는 그대로 보존.
        /// </summary>
        internal static Brush Base(MarkdownnCodeKind kind, bool light) { return light ? LightBaseBrushes[(int)kind] : Base(kind); }
        /// <summary>
        /// name         : Token
        /// desc         : 원본 토큰 종류의 공유 색상을 반환한다.
        /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 원문 실행 없이 표시만 지원.
        /// </summary>
        internal static Brush Token(MarkdownnTokenKind kind)
        {
            switch (kind) { case MarkdownnTokenKind.Keyword: return KeywordBrush; case MarkdownnTokenKind.String: return StringBrush; case MarkdownnTokenKind.Number: return NumberBrush; case MarkdownnTokenKind.Comment: return CommentBrush; default: return TypeBrush; }
        }
        /// <summary>
        /// name         : Token
        /// desc         : 같은 토큰 의미를 Light 대비 팔레트로 표시할 수 있게 한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 열린 편집기의 현재 foreground에서 매 렌더 모드를 결정.
        /// </summary>
        internal static Brush Token(MarkdownnTokenKind kind, bool light) { return light ? LightTokenBrushes[(int)kind] : Token(kind); }
        /// <summary>
        /// name         : IsLightTheme
        /// desc         : 현재 글자색이 어두우면 Light 배경에 맞는 코드 대비를 선택한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 알 수 없는 brush는 원래 dark 팔레트 유지.
        /// </summary>
        internal static bool IsLightTheme(Brush foreground)
        { var color = foreground as SolidColorBrush; return color != null && .2126 * color.Color.R + .7152 * color.Color.G + .0722 * color.Color.B < 128; }
        /// <summary>
        /// name         : Paint
        /// desc         : 고정 팔레트 문자열로 불변 brush를 만든다.
        /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 외부 리소스·색상 스크립트 없음.
        /// </summary>
        private static Brush Paint(string value) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); brush.Freeze(); return brush; }

        /// <summary>
        /// name         : CanHighlight
        /// desc         : 2Mi UTF16·50000행·한 행32768자 이하인지 버전별로 한 번 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 초대형 문서도 원문 편집은 유지하고 강조만 plain으로 강등.
        /// </summary>
        internal static bool CanHighlight(TextDocument document)
        {
            if (document == null || document.TextLength > MaximumDocumentLength || document.LineCount > MaximumDocumentLines) return false;
            var cached = SafeDocuments.GetValue(document, key => new DocumentSafety());
            if (!ReferenceEquals(cached.Version, document.Version))
            {
                cached.Safe = true;
                foreach (DocumentLine line in document.Lines) if (line.Length > MaximumLineLength) { cached.Safe = false; break; }
                cached.Version = document.Version;
            }
            return cached.Safe;
        }

        /// <summary>
        /// name         : LoadDefinition
        /// desc         : 원본 XSHD를 DTD·외부 resolver 없이 유한하게 읽고 실제 Avalon regex에 timeout을 부여한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : asset 내용은 보존하고 런타임 규칙만 동일 패턴으로 제한.
        /// </summary>
        internal static IHighlightingDefinition LoadDefinition(Stream source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 128 * 1024, CloseInput = false };
            IHighlightingDefinition definition;
            using (var reader = XmlReader.Create(source, settings)) definition = HighlightingLoader.Load(reader, null);
            var pending = new Stack<HighlightingRuleSet>(); var visited = new HashSet<HighlightingRuleSet>(); pending.Push(definition.MainRuleSet); int rules = 0;
            while (pending.Count > 0)
            {
                var set = pending.Pop(); if (set == null || !visited.Add(set)) continue;
                if (visited.Count > 128) throw new InvalidDataException("Markdown 구문 정의 범위를 초과했습니다.");
                foreach (var rule in set.Rules) { if (++rules > 1024) throw new InvalidDataException("Markdown 구문 규칙 범위를 초과했습니다."); rule.Regex = WithTimeout(rule.Regex); }
                foreach (var span in set.Spans) { if (++rules > 1024) throw new InvalidDataException("Markdown 구문 범위를 초과했습니다."); span.StartExpression = WithTimeout(span.StartExpression); span.EndExpression = WithTimeout(span.EndExpression); pending.Push(span.RuleSet); }
            }
            return definition;
        }
        /// <summary>
        /// name         : WithTimeout
        /// desc         : 원래 정규식 옵션·패턴을 유지하며 매치 시간을 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : null span은 원래 의미 유지.
        /// </summary>
        private static Regex WithTimeout(Regex value) { return value == null ? null : new Regex(value.ToString(), value.Options, MatchTimeout); }
    }

    /// <summary>
    /// name         : MarkdownnThemeDefinition
    /// desc         : 각 editor의 XSHD rule·span·color를 복제해 테마 전환이 다른 editor에 영향을 주지 않게 한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 원본 resource·정의는 불변, foreground만 context별 선택.
    /// </summary>
    internal sealed class MarkdownnThemeDefinition : IHighlightingDefinition
    {
        private readonly Dictionary<HighlightingColor, HighlightingColor> colors = new Dictionary<HighlightingColor, HighlightingColor>();
        private readonly Dictionary<HighlightingRuleSet, HighlightingRuleSet> sets = new Dictionary<HighlightingRuleSet, HighlightingRuleSet>();
        private bool? light;
        public string Name { get; private set; }
        public HighlightingRuleSet MainRuleSet { get; private set; }
        public IEnumerable<HighlightingColor> NamedHighlightingColors { get { return colors.Values; } }
        public IDictionary<string, string> Properties { get; private set; }
        /// <summary>
        /// name         : MarkdownnThemeDefinition
        /// desc         : 원본 규칙과 색상을 bounded 독립 참조 그래프로 복제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : regex는 immutable timeout 인스턴스를 재사용.
        /// </summary>
        internal MarkdownnThemeDefinition(IHighlightingDefinition original)
        { Name = original.Name; Properties = new Dictionary<string, string>(original.Properties); foreach (var color in original.NamedHighlightingColors) CopyColor(color); MainRuleSet = CopySet(original.MainRuleSet); }
        /// <summary>
        /// name         : CopyColor
        /// desc         : 원본 FontWeight·스타일을 그대로 가진 수정 가능한 색상 사본을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 다른 editor와 색상 상태 공유 금지.
        /// </summary>
        private HighlightingColor CopyColor(HighlightingColor original)
        { if (original == null) return null; HighlightingColor value; if (!colors.TryGetValue(original, out value)) { if (colors.Count >= 1024) throw new InvalidDataException("Markdown 색상 정의 범위 초과"); colors[original] = value = original.Clone(); } return value; }
        /// <summary>
        /// name         : CopySet
        /// desc         : 순환 rule set도 중복 없이 복제하고 128set 이하로 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 원본 span·delimiter 색상 관계 보존.
        /// </summary>
        private HighlightingRuleSet CopySet(HighlightingRuleSet original)
        {
            if (original == null) return null; HighlightingRuleSet value; if (sets.TryGetValue(original, out value)) return value;
            if (sets.Count >= 128) throw new InvalidDataException("Markdown 구문 정의 범위 초과"); sets[original] = value = new HighlightingRuleSet { Name = original.Name };
            foreach (var rule in original.Rules) value.Rules.Add(new HighlightingRule { Regex = rule.Regex, Color = CopyColor(rule.Color) });
            foreach (var span in original.Spans) value.Spans.Add(new HighlightingSpan { StartExpression = span.StartExpression, EndExpression = span.EndExpression, RuleSet = CopySet(span.RuleSet), StartColor = CopyColor(span.StartColor), EndColor = CopyColor(span.EndColor), SpanColor = CopyColor(span.SpanColor), SpanColorIncludesStart = span.SpanColorIncludesStart, SpanColorIncludesEnd = span.SpanColorIncludesEnd });
            return value;
        }
        /// <summary>
        /// name         : GetNamedRuleSet
        /// desc         : 복제한 정의 안에서만 이름을 조회한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 외부 정의 resolver 없음.
        /// </summary>
        public HighlightingRuleSet GetNamedRuleSet(string name) { foreach (var value in sets.Values) if (value.Name == name) return value; return null; }
        /// <summary>
        /// name         : GetNamedColor
        /// desc         : editor별 복제한 이름 색상만 반환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 원본 resource 색상에 쓰지 않음.
        /// </summary>
        public HighlightingColor GetNamedColor(string name) { foreach (var value in colors.Values) if (value.Name == name) return value; return null; }
        /// <summary>
        /// name         : SetLight
        /// desc         : 현재 editor에만 고대비 Light 또는 원래 dark 색상을 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : XSHD 원본 바이트와 별도 정의 객체는 보존.
        /// </summary>
        internal void SetLight(bool value)
        {
            if (light == value) return; light = value;
            foreach (var pair in colors)
            {
                string color = pair.Value.Name == "Heading" ? "#2457A7" : pair.Value.Name == "Strong" ? "#8A5700" : pair.Value.Name == "Emphasis" ? "#6D28D9" : pair.Value.Name == "Code" || pair.Value.Name == "Fence" ? "#19743C" : pair.Value.Name == "Link" ? "#075FA3" : "#59636F";
                pair.Value.Foreground = value ? new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString(color)) : pair.Key.Foreground;
            }
        }
    }

    /// <summary>
    /// name         : MarkdownnSyntaxColorizer
    /// desc         : 제공된 XSHD 강조에서 과대 문서·regex timeout이 UI 예외가 되지 않게 한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 동일 버전에서 timeout 후 반복 시도를 하지 않음.
    /// </summary>
    internal sealed class MarkdownnSyntaxColorizer : HighlightingColorizer
    {
        private readonly TextEditor editor;
        private readonly MarkdownnThemeDefinition theme;
        private ITextSourceVersion version;
        private bool timedOut;
        /// <summary>
        /// name         : MarkdownnSyntaxColorizer
        /// desc         : 유한 로더로 읽은 정의를 실제 AvalonEdit 강조기로 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TextView.LineTransformers에 등록해 사용.
        /// </summary>
        internal MarkdownnSyntaxColorizer(TextEditor editor, IHighlightingDefinition definition) : this(editor, new MarkdownnThemeDefinition(definition)) { }
        /// <summary>
        /// name         : MarkdownnSyntaxColorizer
        /// desc         : 각 editor 전용 사본을 실제 Avalon highlighter에 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 공유 color mutation 방지.
        /// </summary>
        private MarkdownnSyntaxColorizer(TextEditor editor, MarkdownnThemeDefinition definition) : base(definition) { this.editor = editor ?? throw new ArgumentNullException(nameof(editor)); theme = definition; }
        /// <summary>
        /// name         : RefreshTheme
        /// desc         : wrapper의 Foreground 변경 시 현재 테마를 선택하고 기존 visual line도 다시 그린다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 문서·undo 상태는 변경하지 않음.
        /// </summary>
        internal void RefreshTheme() { theme.SetLight(MarkdownnCodeHighlighting.IsLightTheme(editor.Foreground)); editor.TextArea.TextView.Redraw(); }
        /// <summary>
        /// name         : ColorizeLine
        /// desc         : 정상 유한 문서는 원본 문법 전체로 강조하고 실패한 버전은 plain으로 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 원문 편집·실행 취소 데이터에 접근하지 않음.
        /// </summary>
        protected override void ColorizeLine(DocumentLine line)
        {
            var document = editor.Document; if (!MarkdownnCodeHighlighting.CanHighlight(document)) return;
            theme.SetLight(MarkdownnCodeHighlighting.IsLightTheme(editor.Foreground));
            if (!ReferenceEquals(version, document.Version)) { version = document.Version; timedOut = false; }
            if (timedOut) return;
            try { base.ColorizeLine(line); } catch (RegexMatchTimeoutException) { timedOut = true; }
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownnFenceColorizer
    /// desc         : 원본 fence SQL·소스 색상을 유지하며 문서 버전별 유한 상태 캐시로 반복 전체 탐색을 없앤다.
    /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : 원본 O(n²) 재탐색을 한번의 bounded 순회로 대체.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class MarkdownnFenceColorizer : DocumentColorizingTransformer, IDisposable
    {
        private readonly TextEditor _editor;
        private static readonly Regex Opener = new Regex(@"^\s{0,3}(?<fence>`{3,}|~{3,})(?<info>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant, MarkdownnCodeHighlighting.MatchTimeout);
        private TextDocument cachedDocument;
        private ITextSourceVersion cachedVersion;
        private MarkdownnCodeKind[] kinds = new MarkdownnCodeKind[0];
        private bool[] delimiters = new bool[0];
        private int admittedLines;
        private bool disposed;
        /// <summary>
        /// name         : MarkdownnFenceColorizer
        /// desc         : 실제 편집기의 fence 색상 transformer를 만든다.
        /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 변경 이벤트 구독 없이 immutable Version으로 cache 무효화.
        /// </summary>
        internal MarkdownnFenceColorizer(TextEditor editor) { _editor = editor ?? throw new ArgumentNullException(nameof(editor)); }
        /// <summary>
        /// name         : PrepareCache
        /// desc         : 100ms 안에서 같은 문서 버전의 fence 상태를 한 번만 순차 계산한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 처리되지 않은 후반은 잘못된 언어를 추정하지 않고 plain 유지.
        /// </summary>
        private void PrepareCache(TextDocument document)
        {
            if (ReferenceEquals(cachedDocument, document) && ReferenceEquals(cachedVersion, document.Version)) return;
            cachedDocument = document; cachedVersion = document.Version; admittedLines = 0;
            kinds = new MarkdownnCodeKind[document.LineCount]; delimiters = new bool[document.LineCount];
            char marker = '\0'; int fenceLength = 0; var kind = MarkdownnCodeKind.Plain; var timer = Stopwatch.StartNew();
            foreach (DocumentLine line in document.Lines)
            {
                if (timer.ElapsedMilliseconds >= 100) break;
                string text = document.GetText(line.Offset, line.Length); bool delimiter = false;
                try
                {
                    if (marker == '\0')
                    {
                        Match open = Opener.Match(text);
                        if (open.Success) { marker = open.Groups["fence"].Value[0]; fenceLength = open.Groups["fence"].Length; kind = MarkdownnCodeHighlighting.Language(open.Groups["info"].Value); delimiter = true; }
                    }
                    else
                    {
                        string trimmed = text.Trim();
                        if (trimmed.Length >= fenceLength && trimmed.Trim(marker).Length == 0) { delimiter = true; marker = '\0'; }
                    }
                }
                catch (RegexMatchTimeoutException) { break; }
                int index = line.LineNumber - 1; delimiters[index] = delimiter; kinds[index] = marker != '\0' ? kind : MarkdownnCodeKind.Plain; admittedLines = line.LineNumber;
            }
        }
        /// <summary>
        /// name         : ColorizeLine
        /// desc         : 현재 행의 cached fence 상태와 유한 토큰만 실제 AvalonEdit 구간에 적용한다.
        /// author       : 원본 Markdownn 작성자 / 이식 CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 문서 초과·종료·cache 후반에서는 안전한 기본 강조 유지.
        /// </summary>
        protected override void ColorizeLine(DocumentLine target)
        {
            TextDocument document = _editor.Document;
            if (disposed || !MarkdownnCodeHighlighting.CanHighlight(document)) return;
            PrepareCache(document); int index = target.LineNumber - 1;
            bool light = MarkdownnCodeHighlighting.IsLightTheme(_editor.Foreground);
            if (target.LineNumber > admittedLines || target.Length == 0) return;
            if (delimiters[index]) { ChangeLinePart(target.Offset, target.EndOffset, element => element.TextRunProperties.SetForegroundBrush(MarkdownnCodeHighlighting.Token(MarkdownnTokenKind.Type, light))); return; }
            var kind = kinds[index]; if (kind == MarkdownnCodeKind.Plain) return;
            ChangeLinePart(target.Offset, target.EndOffset, element => element.TextRunProperties.SetForegroundBrush(MarkdownnCodeHighlighting.Base(kind, light)));
            foreach (MarkdownnCodeToken token in MarkdownnCodeHighlighting.Tokens(document.GetText(target.Offset, target.Length), kind))
            {
                Brush color = MarkdownnCodeHighlighting.Token(token.Kind, light);
                ChangeLinePart(target.Offset + token.Start, target.Offset + token.Start + token.Length, element => element.TextRunProperties.SetForegroundBrush(color));
            }
        }
        /// <summary>
        /// name         : Dispose
        /// desc         : 편집기 종료에서 문서 버전·상태 배열 참조를 해제한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 원본 문서나 외부 리소스를 변경하지 않음.
        /// </summary>
        public void Dispose() { disposed = true; cachedDocument = null; cachedVersion = null; kinds = new MarkdownnCodeKind[0]; delimiters = new bool[0]; admittedLines = 0; }
    }
}
