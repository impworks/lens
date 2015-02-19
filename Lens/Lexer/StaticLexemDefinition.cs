namespace Lens.Lexer
{
	/// <summary>
	/// A lexem defined by a static string - keyword, operator, etc.
	/// </summary>
	internal class StaticLexemDefinition
	{
		#region Constructor

		public StaticLexemDefinition(string repr, LexemType type)
		{
			Representation = repr;
			Type = type;
		}

		#endregion

		#region Fields

		public readonly string Representation;
		public readonly LexemType Type;

		#endregion
	}
}
