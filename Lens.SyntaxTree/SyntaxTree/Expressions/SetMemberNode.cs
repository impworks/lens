using System;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing write access to a member of a type, field or property.
	/// </summary>
	public class SetMemberNode : NodeBase
	{
		/// <summary>
		/// The value to be assigned.
		/// </summary>
		public NodeBase Value { get; set; }

		public override Type GetExpressionType()
		{
			return Value.GetExpressionType();
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
