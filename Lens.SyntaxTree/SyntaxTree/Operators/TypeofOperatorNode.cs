using System;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the typeof operator.
	/// </summary>
	public class TypeofOperatorNode : NodeBase
	{
		/// <summary>
		/// Type to be inspected.
		/// </summary>
		public TypeSignature Type { get; set; }

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
