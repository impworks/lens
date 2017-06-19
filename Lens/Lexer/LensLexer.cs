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
			_position = 0;
			_offset = 1;
			_line = 1;
			_newLine = true;

			_indentLookup = new Stack<int>();
			Lexems = new List<Lexem>();

			_source = src;

			Parse();
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
				}

				if (ProcessNewLine())
					continue;

				if (CurrChar() == '"' || (CurrChar() == '@' && NextChar() == '"'))
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

			if(Lexems[Lexems.Count - 1].Type != LexemType.NewLine)
				AddLexem(LexemType.NewLine, GetPosition());

			while (_indentLookup.Count > 1)
			{
				AddLexem(LexemType.Dedent, GetPosition());
				_indentLookup.Pop();
			}

			if(Lexems[Lexems.Count-1].Type == LexemType.NewLine)
				Lexems.RemoveAt(Lexems.Count-1);

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

			// empty line?
			if (CurrChar() == '\n' || CurrChar() == '\r')
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
					if (_indentLookup.Count > 0)
						_indentLookup.Pop();
					else
						Error(LexerMessages.InconsistentIdentation);

					AddLexem(LexemType.Dedent, GetPosition());

					if (currIndent == _indentLookup.Peek())
						break;
				}
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
				        Lexems.Add(new Lexem(LexemType.String, startPos, GetPosition(), sb.ToString()));
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
			Lexems.Add(new Lexem(type, loc, default(LexemLocation)));
		}

		#endregion
	}
}
