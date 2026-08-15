using System;
using System.Collections.Generic;
using System.Text;
using Lens.SyntaxTree;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Lexer
{
    /// <summary>
    /// Parses the given string into a sequence of lexems.
    /// </summary>
    internal partial class LensLexer
    {
        #region Constructor

        public LensLexer(string src)
            : this(src, false)
        {
        }

        /// <summary>
        /// Lexes the source code.
        /// </summary>
        /// <param name="src">Source code.</param>
        /// <param name="tolerant">
        /// Whether a lexing failure should stop everything, or leave the lexems read so far behind
        /// and be reported in <see cref="Failure"/>.
        ///
        /// An editor needs the second: a file that is being typed into is malformed most of the
        /// time, and colouring the part that did lex beats colouring nothing.
        /// </param>
        public LensLexer(string src, bool tolerant)
        {
            _position = 0;
            _offset = 1;
            _line = 1;
            _newLine = true;

            _indentLookup = new Stack<int>();
            Lexems = new List<Lexem>();

            _source = src;

            if (tolerant)
            {
                try
                {
                    Parse();
                }
                catch (LensCompilerException ex)
                {
                    Failure = ex;
                }
                catch (Exception ex)
                {
                    // a mistake of ours rather than of the source, and the caller is an editor:
                    // colouring what was read beats answering nothing at all. The compiler's path
                    // does not catch this, so nothing is being swept under the carpet.
                    Failure = new LensCompilerException(ex.Message, ex);
                }
            }
            else
            {
                Parse();
            }

            CloseBlocks();
            FilterNewlines();
        }

        #endregion

        #region Fields

        /// <summary>
        /// Source code as single string.
        /// </summary>
        private readonly string _source;

        /// <summary>
        /// Generated list of lexems.
        /// </summary>
        public List<Lexem> Lexems { get; private set; }

        /// <summary>
        /// The problem that stopped lexing, in a tolerant run. Null when the source lexed cleanly.
        /// </summary>
        public LensCompilerException Failure { get; private set; }

        /// <summary>
        /// Current position in the entire source string.
        /// </summary>
        private int _position;

        /// <summary>
        /// Current line in source.
        /// </summary>
        private int _line;

        /// <summary>
        /// Horizontal offset in current line.
        /// </summary>
        private int _offset;

        /// <summary>
        /// Flag indicating the line has just started.
        /// </summary>
        private bool _newLine;

        /// <summary>
        /// Lookup of identation levels.
        /// </summary>
        private readonly Stack<int> _indentLookup;

        #endregion

        #region Private methods

        /// <summary>
        /// Processes the input string into a list of lexems.
        /// </summary>
        private void Parse()
        {
            while (InBounds())
            {
                if (_newLine)
                {
                    ProcessIndent();
                    _newLine = false;

                    // the indentation ran to the end of the file: there is no lexem after it, and
                    // looking for one is how a half-typed body used to report "unknown lexem"
                    if (!InBounds())
                        break;
                }

                if (ProcessNewLine())
                    continue;

                if (IsInterpolatedStringStart())
                {
                    ProcessInterpolatedStringLiteral();
                    if (!InBounds())
                        break;
                }
                else if (CurrChar() == '"' || (CurrChar() == '@' && NextChar() == '"'))
                {
                    ProcessStringLiteral();
                    if (!InBounds())
                        break;
                }
                else if (IsComment())
                {
                    while (InBounds() && CurrChar() != '\r' && CurrChar() != '\n')
                        _position++;
                }
                else if (CurrChar() == '\t')
                {
                    Error(LexerMessages.TabChar);
                }
                else
                {
                    var lex = ProcessStaticLexem() ?? ProcessRegexLexem();
                    if (lex == null)
                        Error(LexerMessages.UnknownLexem);

                    if (lex.Type == LexemType.Char)
                        lex = TransformCharLiteral(lex);
                    else if (lex.Type == LexemType.Regex)
                        lex = TransformRegexLiteral(lex);

                    Lexems.Add(lex);
                }

                SkipSpaces();
            }
        }

        /// <summary>
        /// Closes every block left open and terminates the stream.
        ///
        /// This is separate from the loop above because a tolerant run reaches it after a failure
        /// as well: the parser is given a well-formed stream either way, just a shorter one.
        /// </summary>
        private void CloseBlocks()
        {
            if (Lexems.Count > 0 && Lexems[Lexems.Count - 1].Type != LexemType.NewLine)
                AddLexem(LexemType.NewLine, GetPosition());

            while (_indentLookup.Count > 1)
            {
                AddLexem(LexemType.Dedent, GetPosition());
                _indentLookup.Pop();
            }

            if (Lexems.Count > 0 && Lexems[Lexems.Count - 1].Type == LexemType.NewLine)
                Lexems.RemoveAt(Lexems.Count - 1);

            AddLexem(LexemType.Eof, GetPosition());
        }

        /// <summary>
        /// Detects indentation changes.
        /// </summary>
        private void ProcessIndent()
        {
            var currIndent = 0;
            while (CurrChar() == ' ')
            {
                Skip();
                currIndent++;
            }

            // empty line? a line of nothing but spaces at the very end of the file is one too, and
            // is what an editor holds for as long as it takes to type the first character of a body
            if (CurrChar() == '\n' || CurrChar() == '\r' || !InBounds())
                return;

            // first line?
            if (_indentLookup.Count == 0)
                _indentLookup.Push(currIndent);

            // indent increased
            else if (currIndent > _indentLookup.Peek())
            {
                _indentLookup.Push(currIndent);
                AddLexem(LexemType.Indent, GetPosition());
            }

            // indent decreased
            else if (currIndent < _indentLookup.Peek())
            {
                while (true)
                {
                    // the outermost level is never popped: it is what the file itself is indented
                    // to, and popping it would leave nothing to compare the next line against
                    if (_indentLookup.Count < 2)
                        Error(LexerMessages.InconsistentIdentation);

                    _indentLookup.Pop();
                    AddLexem(LexemType.Dedent, GetPosition());

                    if (currIndent >= _indentLookup.Peek())
                        break;
                }

                if (currIndent != _indentLookup.Peek())
                    Error(LexerMessages.InconsistentIdentation);
            }
        }

        /// <summary>
        /// Moves the cursor forward to the first non-space character.
        /// </summary>
        private void SkipSpaces()
        {
            while (InBounds() && _source[_position] == ' ')
                Skip();
        }

        /// <summary>
        /// Parses a string out of the source code.
        /// </summary>
        private void ProcessStringLiteral()
        {
            var start = GetPosition();

            var isVerbatim = CurrChar() == '@';
            Skip(isVerbatim ? 2 : 1);

            var startPos = GetPosition();
            var sb = new StringBuilder();
            var isEscaped = false;

            while (InBounds())
            {
                var ch = CurrChar();

                if (!isEscaped && !isVerbatim && ch == '\\')
                {
                    isEscaped = true;
                    continue;
                }

                if (isEscaped)
                {
                    sb.Append(EscapeChar(NextChar().Value));
                    Skip(2);
                    isEscaped = false;
                    continue;
                }

                if (ch == '"')
                {
                    if (isVerbatim && NextChar() == '"')
                    {
                        sb.Append('"');
                        Skip(2);
                        continue;
                    }
                    else
                    {
                        Skip();
                        // 'start' rather than 'startPos': the span covers the quotes as well, so
                        // that an editor colouring by lexem does not leave them bare
                        Lexems.Add(new Lexem(LexemType.String, start, GetPosition(), sb.ToString()));
                        return;
                    }
                }

                if (ch == '\n')
                {
                    _offset = 1;
                    _line++;
                }

                sb.Append(ch);
                Skip();
            }

            var end = GetPosition();
            throw new LensCompilerException(LexerMessages.UnclosedString).BindToLocation(start, end);
        }

        /// <summary>
        /// Attempts to find a keyword or operator at the current position in the file.
        /// </summary>
        private Lexem ProcessStaticLexem()
        {
            return ProcessLexemList(Keywords, ch => ch != '_' && !char.IsLetterOrDigit(ch))
                   ?? ProcessLexemList(Operators);
        }

        /// <summary>
        /// Attempts to find any of the given lexems at the current position in the string.
        /// </summary>
        private Lexem ProcessLexemList(StaticLexemDefinition[] lexems, Func<char, bool> nextChecker = null)
        {
            foreach (var curr in lexems)
            {
                var rep = curr.Representation;
                var len = rep.Length;
                if (_position + len > _source.Length || _source.Substring(_position, len) != rep)
                    continue;

                if (_position + len < _source.Length)
                {
                    var nextCh = _source[_position + len];
                    if (nextChecker != null && !nextChecker(nextCh))
                        continue;
                }

                var start = GetPosition();
                Skip(len);
                var end = GetPosition();
                return new Lexem(curr.Type, start, end);
            }

            return null;
        }

        /// <summary>
        /// Attempts to find any of the given regex-defined lexems at the current position in the string.
        /// </summary>
        private Lexem ProcessRegexLexem()
        {
            foreach (var curr in RegexLexems)
            {
                var match = curr.Regex.Match(_source, _position);
                if (!match.Success)
                    continue;

                var start = GetPosition();
                Skip(match.Length);
                var end = GetPosition();
                return new Lexem(curr.Type, start, end, match.Value);
            }

            return null;
        }

        /// <summary>
        /// Removes redundant newlines from the list.
        /// </summary>
        private void FilterNewlines()
        {
            var eaters = new[] {LexemType.Indent, LexemType.Dedent, LexemType.Eof};
            var result = new List<Lexem>(Lexems.Count);

            var isStart = true;
            Lexem nl = null;
            foreach (var curr in Lexems)
            {
                if (curr.Type == LexemType.NewLine)
                {
                    if (!isStart)
                        nl = curr;
                }
                else
                {
                    if (nl != null)
                    {
                        if (!curr.Type.IsAnyOf(eaters))
                            result.Add(nl);

                        nl = null;
                    }

                    isStart = false;
                    result.Add(curr);
                }
            }

            Lexems = result;
        }

        /// <summary>
        /// Checks if the current position contains a newline character.
        /// </summary>
        private bool ProcessNewLine()
        {
            if (InBounds() && CurrChar() == '\r')
                Skip();

            if (InBounds() && CurrChar() == '\n')
            {
                AddLexem(LexemType.NewLine, GetPosition());

                Skip();
                _offset = 1;
                _line++;
                _newLine = true;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Appends a new lexem to the list.
        /// </summary>
        private void AddLexem(LexemType type, LexemLocation loc)
        {
            // a structural lexem is a zero-width marker, so it ends where it starts. It used to end
            // nowhere at all, which left every construct closed by a DEDENT - a record, an algebraic
            // type, a function with an indented body - with no end position: the parser takes the
            // end of a node from the last lexem it consumed.
            Lexems.Add(new Lexem(type, loc, loc));
        }

        #endregion
    }
}