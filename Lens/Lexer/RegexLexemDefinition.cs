using System.Text.RegularExpressions;

namespace Lens.Lexer
{
	public class RegexLexemDefinition
	{
		public readonly Regex Regex;

		public readonly LexemType Type;

		public RegexLexemDefinition(string regex, LexemType type)
		{
			Regex = new Regex("\\G" + regex, RegexOptions.Compiled);
			Type = type;
		}
	}
}
