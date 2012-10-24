namespace Lens.SyntaxTree.Utils
{
	/// <summary>
	/// A node representing a function argument definition.
	/// </summary>
	public class FunctionArgument
	{
		/// <summary>
		/// Argument name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Argument type
		/// </summary>
		public string Type { get; set; }

		/// <summary>
		/// Argument modifier
		/// </summary>
		public ArgumentModifier Modifier { get; set; }
	}

	/// <summary>
	/// Argument type
	/// </summary>
	public enum ArgumentModifier
	{
		In,
		Ref,
		Out
	}
}
