using System;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing "true" or "false" literals.
	/// </summary>
	public class BooleanNode : LiteralNodeBase<bool>
	{
		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
