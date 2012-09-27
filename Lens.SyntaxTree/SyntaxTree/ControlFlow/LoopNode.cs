using System;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	public class LoopNode : NodeBase
	{
		/// <summary>
		/// The condition.
		/// </summary>
		public NodeBase Condition { get; set; }

		/// <summary>
		/// The body of the loop.
		/// </summary>
		public CodeBlockNode Body { get; set; }

		public override LexemLocation EndLocation
		{
			get { return Body.EndLocation; }
			set { LocationSetError(); }
		}

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
