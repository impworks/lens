using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using Lens.Resolver;
using Lens.SyntaxTree.ControlFlow;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
	using System.Linq;

	using Lens.Compiler;


	/// <summary>
	/// Breaks a tuple expression into list of items.
	/// </summary>
	internal class MatchTupleRule : MatchRuleBase
	{
		#region Constructor

		public MatchTupleRule()
		{
			ElementRules = new List<MatchRuleBase>();
		}

		#endregion

		#region Fields

		/// <summary>
		/// Items of the tuple.
		/// </summary>
		public List<MatchRuleBase> ElementRules;

		/// <summary>
		/// The cached list of tuple item types.
		/// </summary>
		private Type[] ItemTypes;

		#endregion

		#region Resovle

		public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
		{
			if(ElementRules.Count < 1)
				Error(CompilerMessages.PatternTupleTooFewArgs);

			if (ElementRules.Count > 7)
				Error(CompilerMessages.PatternTupleTooManyArgs);

			if(!expressionType.IsTupleType())
				Error(CompilerMessages.PatternTypeMismatch, expressionType, string.Format("Tuple`{0}", ElementRules.Count));

			ItemTypes = expressionType.GetGenericArguments();
			if (ItemTypes.Length != ElementRules.Count)
				Error(CompilerMessages.PatternTypeMismatch, expressionType, string.Format("Tuple`{0}", ElementRules.Count));

			return ElementRules.Select((t, idx) => t.Resolve(ctx, ItemTypes[idx]))
						.SelectMany(subBindings => subBindings);
		}

		#endregion

		#region Expand

		public override IEnumerable<NodeBase> Expand(Context ctx, NodeBase expression, Label nextStatement)
		{
			for (var idx = 0; idx < ElementRules.Count; idx++)
			{
				var fieldName = string.Format("Item{0}", idx + 1);
				var tmpVar = ctx.Scope.DeclareImplicit(ctx, ItemTypes[idx], false);

				yield return Expr.Let(
					tmpVar,
					Expr.GetMember(expression, fieldName)
				);

				foreach (var rule in ElementRules[idx].Expand(ctx, Expr.Get(tmpVar), nextStatement))
					yield return rule;
			}
		}

		#endregion
	}
}
