using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing "true" or "false" literals.
	/// </summary>
	public class BooleanNode : LiteralNodeBase<bool>
	{
		public BooleanNode(bool value = false)
		{
			Value = value;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}
	}
}
