using System;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing a unit literal ().
	/// </summary>
	public class UnitNode : LiteralNodeBase<Unit>
	{
		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
