using System.Collections.Generic;
using System.Text;
using Lens.SyntaxTree;
using Lens.Translations;

namespace Lens.Lexer
{
    internal partial class LensLexer
    {
        #region Detection

        /// <summary>
        /// Checks if the cursor is at the beginning of an interpolated string literal.
        /// </summary>
        private bool IsInterpolatedStringStart()
        {
            if (CurrChar() != '$')
                return false;

            var next = NextChar();
            if (next == '"')
                return true;

            return next == '@' && NextChar(2) == '"';
        }

        #endregion

        #region Interpolated strings

        /// <summary>
        /// Parses an interpolated string out of the source code.
        /// The literal chunks are unescaped right away, while the holes are kept as raw source code:
        /// they are lexed and parsed separately by the parser, which avoids making the lexer reentrant.
        /// </summary>
        private void ProcessInterpolatedStringLiteral()
        {
            var start = GetPosition();

            Skip(); // '$'
            var isVerbatim = CurrChar() == '@';
            Skip(isVerbatim ? 2 : 1); // '@"' or '"'

            var startPos = GetPosition();
            var parts = new List<InterpolatedStringPart>();
            var sb = new StringBuilder();

            void flush()
            {
                if (sb.Length == 0)
                    return;

                parts.Add(InterpolatedStringPart.FromLiteral(sb.ToString()));
                sb.Clear();
            }

            while (true)
            {
                if (!InBounds())
                    throw new LensCompilerException(LexerMessages.UnclosedString).BindToLocation(start, GetPosition());

                var ch = CurrChar();

                // escape sequences of a regular string
                if (!isVerbatim && ch == '\\')
                {
                    var next = NextChar();
                    if (next == null)
                        throw new LensCompilerException(LexerMessages.UnclosedString).BindToLocation(start, GetPosition());

                    sb.Append(EscapeChar(next.Value));
                    Skip(2);
                    continue;
                }

                if (ch == '"')
                {
                    // "" is an escaped quote in a verbatim string
                    if (isVerbatim && NextChar() == '"')
                    {
                        sb.Append('"');
                        Skip(2);
                        continue;
                    }

                    Skip();
                    break;
                }

                if (ch == '{')
                {
                    if (NextChar() == '{')
                    {
                        sb.Append('{');
                        Skip(2);
                        continue;
                    }

                    flush();

                    Skip(); // '{'
                    parts.Add(ProcessInterpolationHole());
                    continue;
                }

                if (ch == '}')
                {
                    if (NextChar() == '}')
                    {
                        sb.Append('}');
                        Skip(2);
                        continue;
                    }

                    Error(LexerMessages.UnescapedInterpolationBrace);
                }

                AppendChar(sb, ch);
            }

            flush();

            Lexems.Add(new Lexem(LexemType.InterpolatedString, startPos, GetPosition(), parts.ToArray()));
        }

        /// <summary>
        /// Scans a single hole of an interpolated string, assuming the opening brace has been consumed.
        /// The body is copied verbatim: brace depth and nested literals are tracked only to find
        /// the hole's boundaries, not to interpret its contents.
        /// </summary>
        private InterpolatedStringPart ProcessInterpolationHole()
        {
            var holeStart = GetPosition();
            var sb = new StringBuilder();
            string format = null;
            var depth = 0;

            while (true)
            {
                if (!InBounds())
                    throw new LensCompilerException(LexerMessages.UnclosedInterpolationHole).BindToLocation(holeStart, GetPosition());

                var ch = CurrChar();

                // nested string, char or regex literals may contain anything, including braces
                if (ch == '"' || ch == '\'' || ch == '#' || (ch == '@' && NextChar() == '"'))
                {
                    CopyNestedLiteral(sb);
                    continue;
                }

                if (ch == '{' || ch == '(' || ch == '[')
                {
                    depth++;
                }
                else if (ch == ')' || ch == ']')
                {
                    // unbalanced brackets are reported by the nested parser, not here
                    if (depth > 0)
                        depth--;
                }
                else if (ch == '}')
                {
                    if (depth == 0)
                    {
                        Skip();
                        break;
                    }

                    depth--;
                }
                else if (ch == ':' && depth == 0)
                {
                    // "::" is the static member access operator, not a format specifier
                    if (NextChar() == ':')
                    {
                        sb.Append("::");
                        Skip(2);
                        continue;
                    }

                    Skip();
                    format = ProcessInterpolationFormat(holeStart);
                    break;
                }

                AppendChar(sb, ch);
            }

            var expression = sb.ToString();
            if (expression.Trim().Length == 0)
                throw new LensCompilerException(LexerMessages.EmptyInterpolationHole).BindToLocation(holeStart, GetPosition());

            return InterpolatedStringPart.FromHole(expression, format, holeStart);
        }

        /// <summary>
        /// Reads the format specifier of a hole, assuming the colon has been consumed.
        /// </summary>
        private string ProcessInterpolationFormat(LexemLocation holeStart)
        {
            var sb = new StringBuilder();

            while (true)
            {
                if (!InBounds())
                    throw new LensCompilerException(LexerMessages.UnclosedInterpolationHole).BindToLocation(holeStart, GetPosition());

                var ch = CurrChar();
                if (ch == '}')
                {
                    Skip();
                    return sb.ToString();
                }

                AppendChar(sb, ch);
            }
        }

        /// <summary>
        /// Copies a nested string, char or regex literal into the buffer without interpreting it.
        /// </summary>
        private void CopyNestedLiteral(StringBuilder sb)
        {
            var ch = CurrChar();

            // verbatim string: "" is the only escape sequence
            if (ch == '@')
            {
                AppendChar(sb, ch);
                AppendChar(sb, CurrChar());

                while (InBounds())
                {
                    var curr = CurrChar();
                    AppendChar(sb, curr);

                    if (curr != '"')
                        continue;

                    if (InBounds() && CurrChar() == '"')
                    {
                        AppendChar(sb, '"');
                        continue;
                    }

                    return;
                }

                return;
            }

            // regex literal: ## is the only escape sequence, letters may trail the closing hash
            if (ch == '#')
            {
                AppendChar(sb, ch);

                while (InBounds())
                {
                    var curr = CurrChar();
                    AppendChar(sb, curr);

                    if (curr != '#')
                        continue;

                    if (InBounds() && CurrChar() == '#')
                    {
                        AppendChar(sb, '#');
                        continue;
                    }

                    while (InBounds() && char.IsLetter(CurrChar()))
                        AppendChar(sb, CurrChar());

                    return;
                }

                return;
            }

            // regular string or char literal
            var quote = ch;
            AppendChar(sb, ch);

            while (InBounds())
            {
                var curr = CurrChar();
                AppendChar(sb, curr);

                if (curr == '\\' && InBounds())
                {
                    AppendChar(sb, CurrChar());
                    continue;
                }

                if (curr == quote)
                    return;
            }
        }

        /// <summary>
        /// Appends a character to the buffer, advancing the cursor and tracking line breaks.
        /// </summary>
        private void AppendChar(StringBuilder sb, char ch)
        {
            sb.Append(ch);
            Skip();

            if (ch != '\n')
                return;

            _offset = 1;
            _line++;
        }

        #endregion
    }
}
