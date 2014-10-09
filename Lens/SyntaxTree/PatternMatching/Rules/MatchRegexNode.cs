using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
	class MatchRegexNode : MatchRuleBase
	{
		#region Fields

		/// <summary>
		/// The regex string.
		/// </summary>
		public string Regex;

		#endregion

		public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
		{
			if(expressionType != typeof(string))
				Error(CompilerMessages.PatternTypeMismatch, expressionType, typeof(string));

			return NoBindings();
		}

		public override IEnumerable<NodeBase> Expand(Context ctx, NodeBase expression, Label nextStatement)
		{
			yield return MakeJumpIf(
				nextStatement,
				Expr.Not(
					Expr.Invoke(
						Expr.New(
							typeof(Regex),
							Expr.Str(Regex)
						),
						"IsMatch",
						expression
					)
				)
			);
		}
	}
}
