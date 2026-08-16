using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Lexer;
using Lens.Parser;
using Lens.Resolver;
using Lens.SyntaxTree;

namespace Lens.Analysis
{
    /// <summary>
    /// One reading of one version of one script, and everything an editor can ask about it.
    ///
    /// Holds the lexems, the parse tree and the bound model together, because every question needs
    /// more than one of them: colouring is lexical until it needs to know that a name is a type,
    /// and completion is semantic until it needs to know what the user has typed so far.
    /// </summary>
    public sealed partial class ScriptAnalysis : IDisposable
    {
        #region Constructor

        internal ScriptAnalysis(ScriptAnalyzer analyzer, string source, string baseDirectory, LensLexer lexer, LensParser parser, Context context, Exception fatal)
        {
            _analyzer = analyzer;
            _baseDirectory = baseDirectory;
            _lexer = lexer;
            _parser = parser;
            _context = context;

            Source = source;
            Diagnostics = CollectDiagnostics(fatal);
        }

        #endregion

        #region Fields

        private readonly ScriptAnalyzer _analyzer;
        private readonly string _baseDirectory;
        private readonly LensLexer _lexer;
        private readonly LensParser _parser;
        private readonly Context _context;

        private IReadOnlyList<ClassifiedToken> _tokens;
        private IReadOnlyList<OutlineItem> _outline;
        private bool _disposed;

        #endregion

        #region Properties

        /// <summary>
        /// The source this reading is of.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Everything that went wrong, from lexing through binding.
        /// </summary>
        public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }

        /// <summary>
        /// Whether the file failed to lex or parse.
        ///
        /// Renaming is refused in that state: the set of names a rename must avoid is worked out
        /// during binding, and binding a tree with a hole in it does not produce all of them.
        /// </summary>
        public bool HasSyntaxErrors { get; private set; }

        /// <summary>
        /// Every token, classified. Whitespace, indentation and comments are not tokens here -
        /// the lexer does not keep them, and an editor's own grammar colours comments anyway.
        /// </summary>
        public IReadOnlyList<ClassifiedToken> Tokens => _tokens ?? (_tokens = BuildTokens());

        /// <summary>
        /// The declarations the file contains, for the outline and the breadcrumb bar.
        /// </summary>
        public IReadOnlyList<OutlineItem> Outline => _outline ?? (_outline = BuildOutline());

        #endregion

        #region Diagnostics

        /// <summary>
        /// Gathers the problems from all three stages into one list.
        /// </summary>
        private IReadOnlyList<AnalysisDiagnostic> CollectDiagnostics(Exception fatal)
        {
            var syntax = new List<AnalysisDiagnostic>();

            if (_lexer.Failure != null)
                syntax.Add(Convert(_lexer.Failure));

            foreach (var curr in _parser.Failures)
                syntax.Add(Convert(curr));

            // a script that did not parse produces binder complaints about names its broken
            // statements never got round to declaring. Those are consequences, not problems.
            if (syntax.Count > 0)
            {
                HasSyntaxErrors = true;
                return syntax;
            }

            var result = new List<AnalysisDiagnostic>();

            foreach (var curr in _context.Diagnostics)
            {
                var start = curr.StartLocation ?? default(LexemLocation);
                var end = curr.EndLocation ?? start;

                result.Add(new AnalysisDiagnostic(curr.Message, curr.Severity == DiagnosticSeverity.Error, new TextSpan(start, end)));
            }

            if (fatal != null && result.Count == 0)
            {
                // a compiler exception that escaped the recovery points still knows where it came
                // from, and a diagnostic without a span lands on the first character of the file -
                // which is the one place the problem certainly is not
                result.Add(fatal is LensCompilerException compilerFailure
                    ? Convert(compilerFailure)
                    : new AnalysisDiagnostic(fatal.Message, true, default(TextSpan)));
            }

            return result;
        }

        /// <summary>
        /// Turns a compiler exception into a diagnostic.
        /// </summary>
        private static AnalysisDiagnostic Convert(LensCompilerException ex)
        {
            var start = ex.StartLocation ?? default(LexemLocation);
            var end = ex.EndLocation ?? start;

            return new AnalysisDiagnostic(ex.Message, true, new TextSpan(start, end));
        }

        #endregion

        #region Tokens

        /// <summary>
        /// Classifies every lexem, then upgrades the names that binding turned out to know
        /// something about.
        /// </summary>
        private IReadOnlyList<ClassifiedToken> BuildTokens()
        {
            var result = new List<ClassifiedToken>();
            var semantics = BuildSemanticClassification();

            foreach (var curr in _lexer.Lexems)
            {
                var kind = KindOf(curr.Type);
                if (kind == null)
                    continue;

                var span = SpanOf(curr);

                if (kind == TokenKind.Identifier)
                    kind = semantics.Classify(span) ?? TokenKind.Identifier;

                result.Add(new ClassifiedToken(span, kind.Value, curr.Value));
            }

            return result;
        }

        /// <summary>
        /// The lexical half of colouring: what the lexer already knows.
        /// </summary>
        private static TokenKind? KindOf(LexemType type)
        {
            switch (type)
            {
                case LexemType.Eof:
                case LexemType.NewLine:
                case LexemType.Indent:
                case LexemType.Dedent:
                    return null;

                case LexemType.Identifier:
                    return TokenKind.Identifier;

                case LexemType.Int:
                case LexemType.Long:
                case LexemType.Float:
                case LexemType.Double:
                case LexemType.Decimal:
                    return TokenKind.Number;

                case LexemType.Char:
                case LexemType.String:
                case LexemType.InterpolatedString:
                    return TokenKind.String;

                case LexemType.Regex:
                    return TokenKind.Regex;

                default:
                    return IsKeyword(type) ? TokenKind.Keyword : TokenKind.Operator;
            }
        }

        /// <summary>
        /// Whether a lexem type is a word rather than a symbol.
        /// </summary>
        private static bool IsKeyword(LexemType type)
        {
            switch (type)
            {
                case LexemType.Use:
                case LexemType.Using:
                case LexemType.Declare:
                case LexemType.Type:
                case LexemType.Record:
                case LexemType.Pure:
                case LexemType.Fun:
                case LexemType.If:
                case LexemType.Then:
                case LexemType.Else:
                case LexemType.For:
                case LexemType.In:
                case LexemType.While:
                case LexemType.Do:
                case LexemType.Try:
                case LexemType.Catch:
                case LexemType.Finally:
                case LexemType.Match:
                case LexemType.With:
                case LexemType.Case:
                case LexemType.When:
                case LexemType.Let:
                case LexemType.Var:
                case LexemType.New:
                case LexemType.Not:
                case LexemType.Is:
                case LexemType.As:
                case LexemType.Of:
                case LexemType.Typeof:
                case LexemType.Default:
                case LexemType.Ref:
                case LexemType.Throw:
                case LexemType.Yield:
                case LexemType.Await:
                case LexemType.Null:
                case LexemType.Unit:
                case LexemType.True:
                case LexemType.False:
                    return true;

                default:
                    return false;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The span an entity covers.
        /// </summary>
        internal static TextSpan SpanOf(LocationEntity entity)
        {
            return entity == null
                ? default(TextSpan)
                : new TextSpan(entity.StartLocation, entity.EndLocation);
        }

        /// <summary>
        /// Whether a span contains a position. The end is exclusive, except that a caret sitting
        /// immediately after a name is still considered to be on it - which is where the caret is
        /// when someone asks to rename what they have just typed.
        /// </summary>
        internal static bool Contains(TextSpan span, LexemLocation position, bool inclusiveEnd = true)
        {
            if (span.IsEmpty)
                return false;

            if (position.Line < span.Start.Line || position.Line > span.End.Line)
                return false;

            if (position.Line == span.Start.Line && position.Offset < span.Start.Offset)
                return false;

            if (position.Line == span.End.Line)
            {
                return inclusiveEnd
                    ? position.Offset <= span.End.Offset
                    : position.Offset < span.End.Offset;
            }

            return true;
        }

        /// <summary>
        /// The lexem the caret is on or immediately after, if any.
        /// </summary>
        internal Lexem LexemAt(LexemLocation position)
        {
            // strictly first: a caret between two tokens is inside neither, and answering with the
            // one that merely ends there would report '::' where the question was about the name
            // after it
            return FindLexem(position, false) ?? FindLexem(position, true);
        }

        /// <summary>
        /// The lexem covering a position, with the end either exclusive or inclusive.
        /// </summary>
        private Lexem FindLexem(LexemLocation position, bool inclusiveEnd)
        {
            foreach (var curr in _lexer.Lexems)
            {
                if (KindOf(curr.Type) == null)
                    continue;

                if (Contains(SpanOf(curr), position, inclusiveEnd))
                    return curr;
            }

            return null;
        }

        #endregion

        #region IDisposable implementation

        /// <summary>
        /// Releases the property slot this reading took. Analysing a file on every keystroke
        /// without this would grow a table for the life of the process.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            GlobalPropertyHelper.UnregisterContext(_context.ContextId);
        }

        #endregion
    }
}
