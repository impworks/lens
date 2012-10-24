using System;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a method being invoked.
	/// </summary>
	public class InvocationNode : InvocationNodeBase
	{
		/// <summary>
		/// An expression to invoke the method on.
		/// </summary>
		public NodeBase Expression { get; set; }

		/// <summary>
		/// The name of the method to invoke.
		/// </summary>
		public string MethodName { get; set; }

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
