using System;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A conditional expression.
	/// </summary>
	public class ConditionNode : NodeBase
	{
		/// <summary>
		/// The condition.
		/// </summary>
		public NodeBase Condition { get; set; }

		/// <summary>
		/// The block of code to be executed if the condition is true.
		/// </summary>
		public CodeBlockNode TrueAction { get; set; }

		/// <summary>
		/// The block of code to be executed if the condition is false.
		/// </summary>
		public CodeBlockNode FalseAction { get; set; }

		public override LexemLocation EndLocation
		{
			get { return FalseAction == null ? TrueAction.EndLocation : FalseAction.EndLocation; }
			set { LocationSetError(); }
		}

		public override Type GetExpressionType()
		{
			var t1 = TrueAction.GetExpressionType();
			if (FalseAction != null)
			{
				var t2 = FalseAction.GetExpressionType();
				if (t1 != t2)
					Error("Inconsistent typing: the branches of the condition return objects of types {0} and {1} respectively.", t1.ToString(), t2.ToString());
			}

			return t1;
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
