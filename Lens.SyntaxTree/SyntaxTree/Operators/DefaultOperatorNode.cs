using System;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the operator that returns a default value for the type.
	/// </summary>
	public class DefaultOperatorNode : TypeOperatorNodeBase
	{
		public DefaultOperatorNode(string type = null)
		{
			Type = type;
		}

		public override Type GetExpressionType()
		{
			return Type.Type;
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
