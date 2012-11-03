using System;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node to represent the null literal.
	/// </summary>
	public class NullNode : NodeBase
	{
		public override Type GetExpressionType()
		{
			return typeof (NullType);
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// A pseudotype to represent the null variable.
	/// </summary>
	public class NullType { }
}
