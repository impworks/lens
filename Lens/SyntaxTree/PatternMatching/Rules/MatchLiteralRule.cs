using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using Lens.Compiler;
using Lens.SyntaxTree.Literals;
using Lens.Utils;
using Lens.Translations;

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

		public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
		{
			if (Literal.LiteralType == typeof (NullType))
			{
				if(expressionType.IsValueType)
					Error(CompilerMessages.PatternTypeMismatch, expressionType, Literal.LiteralType);
			}
			else if (expressionType != Literal.LiteralType)
			{
				Error(CompilerMessages.PatternTypeMismatch, expressionType, Literal.LiteralType);
			}

			return NoBindings();
		}

		#endregion

		#region Expand

		public override IEnumerable<NodeBase> Expand(Context ctx, NodeBase expression, Label nextStatement)
		{
			yield return Expr.If(
				Expr.NotEqual(Literal as NodeBase, expression),
				Expr.Block(
					Expr.JumpTo(nextStatement)
				)
			);
		}

		#endregion
	}
}
