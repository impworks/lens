using System.Diagnostics;
using Lens.SyntaxTree;
using Lens.Translations;

namespace Lens.Lexer
{
	internal partial class LensLexer
	{
		#region Lexem definition tables

		private readonly static StaticLexemDefinition[] Keywords = 
		{
			new StaticLexemDefinition("typeof", LexemType.Typeof),
			new StaticLexemDefinition("default", LexemType.Default),

			new StaticLexemDefinition("use", LexemType.Use),
			new StaticLexemDefinition("using", LexemType.Using),
			new StaticLexemDefinition("type", LexemType.Type),
			new StaticLexemDefinition("record", LexemType.Record),
			new StaticLexemDefinition("pure", LexemType.Pure),
			new StaticLexemDefinition("fun", LexemType.Fun),
			new StaticLexemDefinition("while", LexemType.While),
			new StaticLexemDefinition("do", LexemType.Do),
			new StaticLexemDefinition("if", LexemType.If),
			new StaticLexemDefinition("then", LexemType.Then),
			new StaticLexemDefinition("else", LexemType.Else),
			new StaticLexemDefinition("for", LexemType.For),
			new StaticLexemDefinition("in", LexemType.In),
			new StaticLexemDefinition("try", LexemType.Try),
			new StaticLexemDefinition("catch", LexemType.Catch),
			new StaticLexemDefinition("finally", LexemType.Finally),
			new StaticLexemDefinition("throw", LexemType.Throw),
			new StaticLexemDefinition("match", LexemType.Match),
			new StaticLexemDefinition("with", LexemType.With),
			new StaticLexemDefinition("case", LexemType.Case),
			new StaticLexemDefinition("when", LexemType.When),

			new StaticLexemDefinition("let", LexemType.Let),
			new StaticLexemDefinition("var", LexemType.Var),
			new StaticLexemDefinition("new", LexemType.New),
			new StaticLexemDefinition("not", LexemType.Not),
			new StaticLexemDefinition("ref", LexemType.Ref),
			new StaticLexemDefinition("is", LexemType.Is),
			new StaticLexemDefinition("as", LexemType.As),
			new StaticLexemDefinition("of", LexemType.Of),

			new StaticLexemDefinition("true", LexemType.True),
			new StaticLexemDefinition("false", LexemType.False),
			new StaticLexemDefinition("null", LexemType.Null),
		};

		private readonly static StaticLexemDefinition[] Operators = 
		{
			new StaticLexemDefinition("()", LexemType.Unit),

			new StaticLexemDefinition("|>", LexemType.PassRight),
			new StaticLexemDefinition("<|", LexemType.PassLeft),
			new StaticLexemDefinition("=>", LexemType.FatArrow),
			new StaticLexemDefinition("->", LexemType.Arrow),

			new StaticLexemDefinition("<:", LexemType.ShiftLeft),
			new StaticLexemDefinition(":>", LexemType.ShiftRight),

			new StaticLexemDefinition("==", LexemType.Equal),
			new StaticLexemDefinition("<=", LexemType.LessEqual),
			new StaticLexemDefinition(">=", LexemType.GreaterEqual),
			new StaticLexemDefinition("<>", LexemType.NotEqual),
			new StaticLexemDefinition("<", LexemType.Less),
			new StaticLexemDefinition(">", LexemType.Greater),
			new StaticLexemDefinition("=", LexemType.Assign),

			new StaticLexemDefinition("[", LexemType.SquareOpen),
			new StaticLexemDefinition("]", LexemType.SquareClose),
			new StaticLexemDefinition("(", LexemType.ParenOpen),
			new StaticLexemDefinition(")", LexemType.ParenClose),
			new StaticLexemDefinition("{", LexemType.CurlyOpen),
			new StaticLexemDefinition("}", LexemType.CurlyClose),

			new StaticLexemDefinition("+", LexemType.Plus),
			new StaticLexemDefinition("-", LexemType.Minus),
			new StaticLexemDefinition("**", LexemType.Power),
			new StaticLexemDefinition("*", LexemType.Multiply),
			new StaticLexemDefinition("/", LexemType.Divide),
			new StaticLexemDefinition("%", LexemType.Remainder),
            new StaticLexemDefinition("&&", LexemType.And),
			new StaticLexemDefinition("||", LexemType.Or),
			new StaticLexemDefinition("^^", LexemType.Xor),
			new StaticLexemDefinition("&", LexemType.BitAnd),
			new StaticLexemDefinition("|", LexemType.BitOr),
			new StaticLexemDefinition("^", LexemType.BitXor),

			new StaticLexemDefinition("::", LexemType.DoubleСolon),
			new StaticLexemDefinition(":", LexemType.Colon),
			new StaticLexemDefinition(",", LexemType.Comma),
			new StaticLexemDefinition("...", LexemType.Ellipsis),
			new StaticLexemDefinition("..", LexemType.DoubleDot),
			new StaticLexemDefinition(".", LexemType.Dot),
			new StaticLexemDefinition(";", LexemType.Semicolon),
			new StaticLexemDefinition("?", LexemType.QuestionMark),
			new StaticLexemDefinition("~", LexemType.Tilde)
		};

		private readonly static RegexLexemDefinition[] RegexLexems =
		{
			new RegexLexemDefinition(@"(0|[1-9][0-9]*)(\.[0-9]+)?[Ff]", LexemType.Float),
			new RegexLexemDefinition(@"(0|[1-9][0-9]*)(\.[0-9]+)?[Mm]", LexemType.Decimal),
			new RegexLexemDefinition(@"(0|[1-9][0-9]*)\.[0-9]+", LexemType.Double),
			new RegexLexemDefinition(@"(0|[1-9][0-9]*)L", LexemType.Long),
			new RegexLexemDefinition(@"(0|[1-9][0-9]*)", LexemType.Int),
			new RegexLexemDefinition(@"([a-zA-Z_][0-9a-zA-Z_]*)", LexemType.Identifier),
			new RegexLexemDefinition(@"'([^'\\]|\\['ntr])*'", LexemType.Char),
			new RegexLexemDefinition(@"#.+?(?<!\#)#[a-zA-Z]*", LexemType.Regex)
		};

		#endregion

		#region Helper methods

        /// <summary>
        /// Returns the current char.
        /// </summary>
        [DebuggerStepThrough]
	    private char currChar()
	    {
	        return _Source[_Position];
	    }

        /// <summary>
        /// Returns the next char, if there is one.
        /// </summary>
        [DebuggerStepThrough]
	    private char? nextChar(int offset = 1)
        {
            var pos = _Position + offset;
            if (pos < 0 || pos >= _Source.Length)
                return null;

            return _Source[pos];
        }

		[DebuggerStepThrough]
		private void error(string src, params object[] args)
		{
			var loc = new LocationEntity { StartLocation = getPosition() };
			throw new LensCompilerException(string.Format(src, args), loc);
		}

		/// <summary>
		/// Checks if the cursor has run outside string bounds.
		/// </summary>
		[DebuggerStepThrough]
		private bool inBounds()
		{
			return _Position < _Source.Length;
		}

		/// <summary>
		/// Checks if the cursor is at comment start.
		/// </summary>
		[DebuggerStepThrough]
		private bool isComment()
		{
			return currChar() == '/' && nextChar() == '/';
		}

		/// <summary>
		/// Skips one or more symbols.
		/// </summary>
		[DebuggerStepThrough]
		private void skip(int count = 1)
		{
			_Position += count;
			_Offset += count;
		}

		/// <summary>
		/// Returns the current position in the string.
		/// </summary>
		[DebuggerStepThrough]
		private LexemLocation getPosition()
		{
			return new LexemLocation
			{
				Line = _Line,
				Offset = _Offset
			};
		}

		#endregion

		#region Escaping

		/// <summary>
		/// Processes the contents of a char literal.
		/// </summary>
		[DebuggerStepThrough]
		private Lexem transformCharLiteral(Lexem lex)
		{
			var value = lex.Value;
			if (value.Length < 3 || value.Length > 4)
				error(LexerMessages.IncorrectCharLiteral);

			value = value[1] == '\\'
				? escapeChar(value[2]).ToString()
				: value[1].ToString();

			return new Lexem(LexemType.Char, lex.StartLocation, lex.EndLocation, value);
		}

		/// <summary>
		/// Processes the contents of a regex literal.
		/// </summary>
		[DebuggerStepThrough]
		private Lexem transformRegexLiteral(Lexem lex)
		{
			return new Lexem(LexemType.Regex, lex.StartLocation, lex.EndLocation, lex.Value.Replace(@"\#", "#"));
		}

		/// <summary>
		/// Returns an escaped version of the given character.
		/// </summary>
		private char escapeChar(char t)
		{
			switch (t)
			{
				case 't':
					return '\t';

				case 'n':
					return '\n';

				case 'r':
					return '\r';

				case '\\':
				case '"':
				case '\'':
					return t;
			}

			error(LexerMessages.UnknownEscape, t);
			return ' ';
		}

		#endregion
	}
}
