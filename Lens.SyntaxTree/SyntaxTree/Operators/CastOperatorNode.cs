using System;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing a cast expression.
	/// </summary>
	public class CastOperatorNode : NodeBase
	{
		/// <summary>
		/// The expression to cast.
		/// </summary>
		public NodeBase Expression { get; set; }

		/// <summary>
		/// The type to cast to.
		/// </summary>
		public TypeSignature Type { get; set; }


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
