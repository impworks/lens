using System;
using System.Collections.Generic;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// An anonymous function definition node.
	/// </summary>
	public class FunctionNode : NodeBase
	{
		/// <summary>
		/// Function arguments.
		/// </summary>
		public Dictionary<string, FunctionArgumentNode> Arguments { get; set; }

		/// <summary>
		/// Function body.
		/// </summary>
		public CodeBlockNode Body { get; set; }

		public override Type GetExpressionType()
		{
			return Body.GetExpressionType();
		}

		public override void Compile()
		{
			registerArgVariables();

			throw new NotImplementedException();
		}

		#region Generic code for all functions, anonymous and named

		/// <summary>
		/// Declare variables for parameters.
		/// </summary>
		protected void registerArgVariables()
		{
			foreach (var currArg in Arguments)
				currArg.Value.Compile();
		}

		#endregion
	}
}
