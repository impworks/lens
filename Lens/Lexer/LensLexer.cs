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
		public List<Lexem> Lexems { get; private set; }

		private int Position;
		private int Offset;
		private int Line;

		private bool NewLine;
		private Stack<int> IndentLookup;

		private string Source;

		public LensLexer(string src)
		{
			Position = 0;
			Offset = 1;
			Line = 1;
			NewLine = true;

			IndentLookup = new Stack<int>();
			Lexems = new List<Lexem>();

			Source = src;

			parse();
			filterNewlines();
		}

		/// <summary>
		/// Processes the input string into a list of lexems.
		/// </summary>
		private void parse()
		{
			while (inBounds())
			{
				if (NewLine)
				{
					processIndent();
					NewLine = false;
				}

				if (processNewLine())
					continue;

				if (Source[Position] == '"')
				{
					processStringLiteral();
					if (!inBounds())
						break;
				}
				else if (isComment())
				{
					while (inBounds() && Source[Position] != '\r' && Source[Position] != '\n')
						Position++;
				}
				else if (Source[Position] == '\t')
				{
					Error(LexerMessages.TabChar);
				}
				else
				{
					var lex = processStaticLexem() ?? processRegexLexem();
					if (lex == null)
						Error(LexerMessages.UnknownLexem);

					Lexems.Add(lex);
				}

				skipSpaces();
			}

			if(Lexems[Lexems.Count - 1].Type != LexemType.NewLine)
				addLexem(LexemType.NewLine, getPosition());

			while (IndentLookup.Count > 1)
			{
				addLexem(LexemType.Dedent, getPosition());
				IndentLookup.Pop();
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
			while (Source[Position] == ' ')
			{
				skip();
				currIndent++;
			}

			// empty line?
			if (Source[Position] == '\n' || Source[Position] == '\r')
				return;

			// first line?
			if (IndentLookup.Count == 0)
				IndentLookup.Push(currIndent);

				// indent increased
			else if (currIndent > IndentLookup.Peek())
			{
				IndentLookup.Push(currIndent);
				addLexem(LexemType.Indent, getPosition());
			}

				// indent decreased
			else if (currIndent < IndentLookup.Peek())
			{
				while (true)
				{
					if (IndentLookup.Count > 0)
						IndentLookup.Pop();
					else
						Error(LexerMessages.InconsistentIdentation);

					addLexem(LexemType.Dedent, getPosition());

					if (currIndent == IndentLookup.Peek())
						break;
				}
			}
		}

		/// <summary>
		/// Moves the cursor forward to the first non-space character.
		/// </summary>
		private void skipSpaces()
		{
			while (inBounds() && Source[Position] == ' ')
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
				var ch = Source[Position];
				if (!isEscaped && ch == '\\')
				{
					isEscaped = true;
					continue;
				}

				if (isEscaped)
				{
					sb.Append(escapeChar(Source[Position + 1]));
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
					Offset = 1;
					Line++;
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
				if (Position + len > Source.Length || Source.Substring(Position, len) != rep)
					continue;

				if (Position + len < Source.Length)
				{
					var nextCh = Source[Position + len];
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
				var match = curr.Regex.Match(Source, Position);
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
					return t;
			}

			Error(LexerMessages.UnknownEscape, t);
			return ' ';
		}

		/// <summary>
		/// Checks if the current position contains a newline character.
		/// </summary>
		private bool processNewLine()
		{
			if (inBounds() && Source[Position] == '\r')
				skip();

			if (inBounds() && Source[Position] == '\n')
			{
				addLexem(LexemType.NewLine, getPosition());

				skip();
				Offset = 0;
				Line++;
				NewLine = true;

				return true;
			}

			return false;
		}

		private void addLexem(LexemType type, LexemLocation loc)
		{
			Lexems.Add(new Lexem(type, loc, default(LexemLocation)));
		}
	}
}
