using System;

namespace Lens.SyntaxTree.Utils
{
	/// <summary>
	/// A class representing info about a local variable.
	/// </summary>
	public class VariableInfo
	{
		public string Name { get; set; }
		public Type Type { get; set; }
		public bool IsConstant { get; set; }
		public ArgumentType ArgumentType { get; set; }
	}

	public enum ArgumentType
	{
		In,
		Ref,
		Out
	}
}
