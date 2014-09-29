using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Lens.SyntaxTree.Literals;
using Lens.Utils;
using Lens.Translations;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
	using Lens.Compiler;


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
			if (expressionType != Literal.LiteralType)
				Error(CompilerMessages.PatternTypeMismatch, expressionType, Literal.LiteralType);

			// no variables
			yield break;
		}

		#endregion

		#region Expand

		public override NodeBase Expand(Context ctx, NodeBase expression, Label nextStatement)
		{
			return Expr.If(
				Expr.NotEqual(Literal as NodeBase, expression),
				Expr.Block(
					Expr.JumpTo(nextStatement)
				)
			);
		}

		#endregion
	}
}
