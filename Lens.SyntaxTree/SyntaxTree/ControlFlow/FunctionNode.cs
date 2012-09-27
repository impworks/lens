using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A function definition node.
	/// </summary>
	public class FunctionNode : NodeBase
	{
		/// <summary>
		/// Function name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Function arguments.
		/// </summary>
		public Dictionary<string, VariableInfo> Arguments { get; set; }

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
			throw new NotImplementedException();
		}
	}
}
