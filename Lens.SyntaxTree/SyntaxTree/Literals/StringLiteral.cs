using System;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing string literals.
	/// </summary>
	public class StringLiteral : LiteralNodeBase<string>
	{
		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
