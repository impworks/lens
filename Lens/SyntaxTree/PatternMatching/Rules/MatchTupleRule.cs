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
			Items = new List<MatchRuleBase>();
		}

		#endregion

		#region Fields

		/// <summary>
		/// Items of the tuple.
		/// </summary>
		public List<MatchRuleBase> Items;

		/// <summary>
		/// The cached list of tuple item types.
		/// </summary>
		private Type[] ItemTypes;

		#endregion

		#region Resovle

		public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
		{
			if(Items.Count < 1)
				Error(CompilerMessages.PatternTupleTooFewArgs);

			if (Items.Count > 7)
				Error(CompilerMessages.PatternTupleTooManyArgs);

			if(!expressionType.IsTupleType())
				Error(CompilerMessages.PatternTypeMismatch, expressionType, string.Format("Tuple`{0}", Items.Count));

			ItemTypes = expressionType.GetGenericArguments();
			if (ItemTypes.Length != Items.Count)
				Error(CompilerMessages.PatternTypeMismatch, expressionType, string.Format("Tuple`{0}", Items.Count));

			return Items.Select((t, idx) => t.Resolve(ctx, ItemTypes[idx]))
						.SelectMany(subBindings => subBindings);
		}

		#endregion

		#region Expand

		public override NodeBase Expand(Context ctx, NodeBase expression, Label nextStatement)
		{
			var block = new CodeBlockNode();

			for (var idx = 0; idx < Items.Count; idx++)
			{
				var fieldName = string.Format("Item{0}", idx + 1);
				var tmpVar = ctx.Scope.DeclareImplicit(ctx, ItemTypes[idx], false);

				block.Add(
					Expr.Let(
						tmpVar,
						Expr.GetMember(expression, fieldName)
					)
				);

				block.Add(
					Items[idx].Expand(ctx, Expr.Get(tmpVar), nextStatement)
				);
			}

			return block;
		}

		#endregion
	}
}
