using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Lens.SyntaxTree.Literals;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
	/// <summary>
	/// Matches the expression against a literal constant.
	/// </summary>
	internal class MatchLiteralRule : MatchRuleBase
	{
		#region Fields

		/// <summary>
		/// The constant to check against.
		/// </summary>
		public ILiteralNode Literal;

		#endregion

		#region Resolve

		public override IEnumerable<PatternNameBinding> Resolve(Type expressionType)
		{
			if(expressionType != Literal.LiteralType)
				throw new InvalidOperationException();

			// no variables
			yield break;
		}

		#endregion

		#region Expand

		public override NodeBase Expand(NodeBase expression, Label nextCheck)
		{
			return Expr.If(
				Expr.NotEqual(Literal as NodeBase, expression),
				Expr.Block(
					Expr.JumpTo(nextCheck)
				)
			);
		}

		#endregion
	}
}
