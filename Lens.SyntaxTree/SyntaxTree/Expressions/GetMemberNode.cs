using System;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing read access to a member of a type, either field or property.
	/// </summary>
	public class GetMemberNode : MemberNodeBase
	{
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
