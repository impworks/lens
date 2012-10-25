using System;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing read access to a member of a type, field or property.
	/// </summary>
	public class GetMemberNode : NodeBase
	{
		/// <summary>
		/// Expression to access a dynamic member.
		/// </summary>
		public NodeBase Expression { get; set; }

		/// <summary>
		/// Type signature to access a static type.
		/// </summary>
		public TypeSignature StaticType { get; set; }

		/// <summary>
		/// The name of the member to access.
		/// </summary>
		public string MemberName { get; set; }

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
