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
		{
			_Position = 0;
			_Offset = 1;
			_Line = 1;
			_NewLine = true;

			_IndentLookup = new Stack<int>();
			Lexems = new List<Lexem>();

			_Source = src;

			parse();
			filterNewlines();
		}

		#endregion

		#region Fields

		/// <summary>
		/// Source code as single string.
		/// </summary>
		private readonly string _Source;

		/// <summary>
		/// Generated list of lexems.
		/// </summary>
		public List<Lexem> Lexems { get; private set; }

		/// <summary>
		/// Current position in the entire source string.
		/// </summary>
		private int _Position;

		/// <summary>
		/// Current line in source.
		/// </summary>
		private int _Line;

		/// <summary>
		/// Horizontal offset in current line.
		/// </summary>
		private int _Offset;

		/// <summary>
		/// Flag indicating the line has just started.
		/// </summary>
		private bool _NewLine;

		/// <summary>
		/// Lookup of identation levels.
		/// </summary>
		private readonly Stack<int> _IndentLookup;

		#endregion

		#region Private methods

		/// <summary>
		/// Processes the input string into a list of lexems.
		/// </summary>
		private void parse()
		{
			while (inBounds())
			{
				if (_NewLine)
				{
					processIndent();
					_NewLine = false;
				}

				if (processNewLine())
					continue;

				if (_Source[_Position] == '"')
				{
					processStringLiteral();
					if (!inBounds())
						break;
				}
				else if (isComment())
				{
					while (inBounds() && _Source[_Position] != '\r' && _Source[_Position] != '\n')
						_Position++;
				}
				else if (_Source[_Position] == '\t')
				{
					error(LexerMessages.TabChar);
				}
				else
				{
					var lex = processStaticLexem() ?? processRegexLexem();
					if (lex == null)
						error(LexerMessages.UnknownLexem);

					if (lex.Type == LexemType.Char)
						lex = transformCharLiteral(lex);
					else if (lex.Type == LexemType.Regex)
						lex = transformRegexLiteral(lex);

					Lexems.Add(lex);
				}

				skipSpaces();
			}

			if(Lexems[Lexems.Count - 1].Type != LexemType.NewLine)
				addLexem(LexemType.NewLine, getPosition());

			while (_IndentLookup.Count > 1)
			{
				addLexem(LexemType.Dedent, getPosition());
				_IndentLookup.Pop();
			}

			if(Lexems[Lexems.Count-1].Type == LexemType.NewLine)
				Lexems.RemoveAt(Lexems.Count-1);

			addLexem(LexemType.EOF, getPosition());
		}

		/// <summary>
		/// Detects indentation changes.
		/// </summary>
		private void processIndent()
		{
			var currIndent = 0;
			while (_Source[_Position] == ' ')
			{
				skip();
				currIndent++;
			}

			// empty line?
			if (_Source[_Position] == '\n' || _Source[_Position] == '\r')
				return;

			// first line?
			if (_IndentLookup.Count == 0)
				_IndentLookup.Push(currIndent);

				// indent increased
			else if (currIndent > _IndentLookup.Peek())
			{
				_IndentLookup.Push(currIndent);
				addLexem(LexemType.Indent, getPosition());
			}

				// indent decreased
			else if (currIndent < _IndentLookup.Peek())
			{
				while (true)
				{
					if (_IndentLookup.Count > 0)
						_IndentLookup.Pop();
					else
						error(LexerMessages.InconsistentIdentation);

					addLexem(LexemType.Dedent, getPosition());

					if (currIndent == _IndentLookup.Peek())
						break;
				}
			}
		}

		/// <summary>
		/// Moves the cursor forward to the first non-space character.
		/// </summary>
		private void skipSpaces()
		{
			while (inBounds() && _Source[_Position] == ' ')
				skip();
		}

		/// <summary>
		/// Parses a string out of the source code.
		/// </summary>
		private void processStringLiteral()
		{
			var start = getPosition();

			// skip first quote
			skip();

			var startPos = getPosition();
			var sb = new StringBuilder();
			var isEscaped = false;

			while (inBounds())
			{
				var ch = _Source[_Position];
				if (!isEscaped && ch == '\\')
				{
					isEscaped = true;
					continue;
				}

				if (isEscaped)
				{
					sb.Append(escapeChar(_Source[_Position + 1]));
					skip(2);
					isEscaped = false;
					continue;
				}

				if (ch == '"')
				{
					skip();
					Lexems.Add(new Lexem(LexemType.String, startPos, getPosition(), sb.ToString()));
					return;
				}

				if (ch == '\n')
				{
					_Offset = 1;
					_Line++;
				}

				sb.Append(ch);
				skip();
			}

			var end = getPosition();
			throw new LensCompilerException(LexerMessages.UnclosedString).BindToLocation(start, end);
		}

		/// <summary>
		/// Attempts to find a keyword or operator at the current position in the file.
		/// </summary>
		private Lexem processStaticLexem()
		{
			return processLexemList(Keywords, ch => ch != '_' && !char.IsLetterOrDigit(ch))
			       ?? processLexemList(Operators);
		}

		/// <summary>
		/// Attempts to find any of the given lexems at the current position in the string.
		/// </summary>
		private Lexem processLexemList(StaticLexemDefinition[] lexems, Func<char, bool> nextChecker = null)
		{
			foreach (var curr in lexems)
			{
				var rep = curr.Representation;
				var len = rep.Length;
				if (_Position + len > _Source.Length || _Source.Substring(_Position, len) != rep)
					continue;

				if (_Position + len < _Source.Length)
				{
					var nextCh = _Source[_Position + len];
					if (nextChecker != null && !nextChecker(nextCh))
						continue;
				}

				var start = getPosition();
				skip(len);
				var end = getPosition();
				return new Lexem(curr.Type, start, end);
			}

			return null;
		}

		/// <summary>
		/// Attempts to find any of the given regex-defined lexems at the current position in the string.
		/// </summary>
		private Lexem processRegexLexem()
		{
			foreach (var curr in RegexLexems)
			{
				var match = curr.Regex.Match(_Source, _Position);
				if (!match.Success)
					continue;

				var start = getPosition();
				skip(match.Length);
				var end = getPosition();
				return new Lexem(curr.Type, start, end, match.Value);
			}

			return null;
		}

		/// <summary>
		/// Removes redundant newlines from the list.
		/// </summary>
		private void filterNewlines()
		{
			var eaters = new[] {LexemType.Indent, LexemType.Dedent, LexemType.EOF};
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
		private bool processNewLine()
		{
			if (inBounds() && _Source[_Position] == '\r')
				skip();

			if (inBounds() && _Source[_Position] == '\n')
			{
				addLexem(LexemType.NewLine, getPosition());

				skip();
				_Offset = 0;
				_Line++;
				_NewLine = true;

				return true;
			}

			return false;
		}

		/// <summary>
		/// Appends a new lexem to the list.
		/// </summary>
		private void addLexem(LexemType type, LexemLocation loc)
		{
			Lexems.Add(new Lexem(type, loc, default(LexemLocation)));
		}

		#endregion
	}
}
