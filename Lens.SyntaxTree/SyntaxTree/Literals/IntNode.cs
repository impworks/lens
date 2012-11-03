using System;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing an integer literal.
	/// </summary>
	public class IntNode : LiteralNodeBase<int>
	{
		public IntNode(int value = 0)
		{
			Value = value;
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
