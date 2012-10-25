using System;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing assignment to an index.
	/// </summary>
	public class SetIndexNode : IndexNodeBase
	{
		/// <summary>
		/// Value to save at indexed location.
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
