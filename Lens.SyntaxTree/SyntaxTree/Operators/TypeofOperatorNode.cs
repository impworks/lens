using System;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the typeof operator.
	/// </summary>
	public class TypeofOperatorNode : TypeOperatorNodeBase
	{
		public override Type GetExpressionType()
		{
			return typeof (Type);
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
