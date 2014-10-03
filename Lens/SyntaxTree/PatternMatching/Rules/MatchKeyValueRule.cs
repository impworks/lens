using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{

	/// <summary>
	/// Key-value pair pattern.
	/// </summary>
	internal class MatchKeyValueRule : MatchRuleBase
	{
		#region Fields

		/// <summary>
		/// Key pattern.
		/// </summary>
		public MatchRuleBase KeyRule;

		/// <summary>
		/// Value pattern.
		/// </summary>
		public MatchRuleBase ValueRule;

		/// <summary>
		/// The cached types of key and value.
		/// </summary>
		private Type[] Types;

		#endregion

		#region Resolve

		public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
		{
			if(!expressionType.IsAppliedVersionOf(typeof(KeyValuePair<,>)))
				Error(CompilerMessages.PatternTypeMismatch, expressionType, typeof(KeyValuePair<,>));

			Types = expressionType.GetGenericArguments();

			return KeyRule.Resolve(ctx, Types[0])
					  .Concat(ValueRule.Resolve(ctx, Types[1]));
		}

		#endregion

		#region Expand

		public override IEnumerable<NodeBase> Expand(Context ctx, NodeBase expression, Label nextStatement)
		{
			var tmpKey = ctx.Scope.DeclareImplicit(ctx, Types[0], false);
			var tmpValue = ctx.Scope.DeclareImplicit(ctx, Types[1], false);

			yield return Expr.Let(tmpKey, Expr.GetMember(expression, "Key"));
			foreach (var rule in KeyRule.Expand(ctx, Expr.Get(tmpKey), nextStatement))
				yield return rule;

			yield return Expr.Let(tmpValue, Expr.GetMember(expression, "Value"));
			foreach(var rule in ValueRule.Expand(ctx, Expr.Get(tmpValue), nextStatement))
				yield return rule;
		}

		#endregion
	}
}
