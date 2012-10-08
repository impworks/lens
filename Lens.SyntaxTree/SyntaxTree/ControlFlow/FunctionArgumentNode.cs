using System;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node representing a function argument definition.
	/// </summary>
	public class FunctionArgumentNode : NodeBase
	{
		/// <summary>
		/// Argument name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Argument type
		/// </summary>
		public string Type { get; set; }

		/// <summary>
		/// Argument modifier
		/// </summary>
		public ArgumentModifier Modifier { get; set; }

		public override Type GetExpressionType()
		{
			throw new InvalidOperationException("Function argument nodes have no type.");
		}

		public override void Compile()
		{
			// todo: add a record of self to current function's scope
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Argument type
	/// </summary>
	public enum ArgumentModifier
	{
		In,
		Ref,
		Out
	}
}
