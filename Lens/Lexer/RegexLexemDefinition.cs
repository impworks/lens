using System.Text.RegularExpressions;

namespace Lens.Lexer
{
	/// <summary>
	/// A lexem defined by a regular expression - identifier, number, etc.
	/// </summary>
	internal class RegexLexemDefinition
	{
		#region Constructor

		public RegexLexemDefinition(string regex, LexemType type)
		{
			Regex = new Regex(@"\G" + regex, RegexOptions.Compiled);
			Type = type;
		}

		#endregion

		#region Fields

		public readonly Regex Regex;
		public readonly LexemType Type;

		#endregion
	}
}
