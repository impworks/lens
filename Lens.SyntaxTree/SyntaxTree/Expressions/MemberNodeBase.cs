using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// The base node for 
	/// </summary>
	abstract public class MemberNodeBase : NodeBase
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
	}
}
