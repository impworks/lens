using System;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing read access to a local variable or a function.
	/// </summary>
	public class GetIdentifierNode : IdentifierNodeBase, IEndLocationTrackingEntity
	{
		public GetIdentifierNode(string identifier = null)
		{
			Identifier = identifier;
		}

		public override Type GetExpressionType()
		{
			throw new NotImplementedException();
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
