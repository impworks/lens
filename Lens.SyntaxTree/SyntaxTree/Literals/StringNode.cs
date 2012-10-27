using System;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing string literals.
	/// </summary>
	public class StringNode : LiteralNodeBase<string>
	{
		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
