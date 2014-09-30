using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.ControlFlow;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
	/// <summary>
	/// Breaks the array into a sequence of element patterns.
	/// </summary>
	internal class MatchArrayRule : MatchRuleBase
	{
		#region Constructor

		public MatchArrayRule()
		{
			ElementRules = new List<MatchRuleBase>();
		}

		#endregion

		#region Fields

		/// <summary>
		/// The patterns of array items.
		/// </summary>
		public List<MatchRuleBase> ElementRules;

		/// <summary>
		/// The sequence's complete type.
		/// </summary>
		private Type ExpressionType;

		/// <summary>
		/// The sequence element's type.
		/// </summary>
		private Type ElementType;

		/// <summary>
		/// Checks whether the source expression is indexable.
		/// </summary>
		private bool IsIndexable;

		/// <summary>
		/// The index of the subsequence item, if any.
		/// </summary>
		private int? SubsequenceIndex;

		/// <summary>
		/// Name of the field that returns the length of the array.
		/// </summary>
		private string SizeMemberName
		{
			get { return ExpressionType.IsArray ? "Length" : "Count"; }
		}

		#endregion

		#region Resolve

		public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
		{
			ExpressionType = expressionType;

			if (expressionType.IsArray)
				ElementType = expressionType.GetElementType();

			else if(new [] { typeof(IEnumerable<>), typeof(IList<>) }.Any(expressionType.IsAppliedVersionOf))
				ElementType = expressionType.GetGenericArguments()[0];

			else
				Error(CompilerMessages.PatternTypeMismatch, expressionType, "IEnumerable<T>");

			IsIndexable = !expressionType.IsAppliedVersionOf(typeof (IEnumerable<>));

			for (var idx = 0; idx < ElementRules.Count; idx++)
			{
				var subseq = ElementRules[idx] as MatchNameRule;
				if (subseq != null && subseq.IsArraySubsequence)
				{
					if(SubsequenceIndex != null)
						Error(CompilerMessages.PatternArraySubsequences);

					if(!IsIndexable && idx < ElementRules.Count-1)
						Error(CompilerMessages.PatternSubsequenceLocation);

					SubsequenceIndex = idx;
				}

				var itemType = SubsequenceIndex != idx
					? ElementType
					: (IsIndexable ? ElementType.MakeArrayType() : typeof (IEnumerable<>).MakeGenericType(ElementType));

				var bindings = ElementRules[idx].Resolve(ctx, itemType);
				foreach (var binding in bindings)
					yield return binding;
			}
		}

		#endregion

		#region Expand

		public override NodeBase Expand(Context ctx, NodeBase expression, Label nextStatement)
		{
			var block = new CodeBlockNode();

			if (SubsequenceIndex == null)
			{
				if (IsIndexable)
				{
					// array size must match exactly
					block.Add(
						MakeJumpIf(
							nextStatement,
							Expr.NotEqual(
								Expr.GetMember(expression, SizeMemberName),
								Expr.Int(ElementRules.Count)
							)
						)
					);
				}

				expandItemChecksIterated(ctx, block, expression, ElementRules.Count, nextStatement);
				return block;
			}

			if (IsIndexable)
			{
				// must contain at least N items
				block.Add(
					MakeJumpIf(
						nextStatement,
						Expr.Less(
							Expr.GetMember(expression, SizeMemberName),
							Expr.Int(ElementRules.Count - 1)
						)
					)
				);

				var subseqIdx = SubsequenceIndex.Value;
				var tempVar = ctx.Scope.DeclareImplicit(ctx, ElementType, false);

				// pre-subsequence
				for (var idx = 0; idx < subseqIdx; idx++)
				{
					block.AddRange(
						Expr.Set(
							tempVar,
							Expr.GetIdx(expression, Expr.Int(idx))
						),
						ElementRules[idx].Expand(ctx, Expr.Get(tempVar), nextStatement)
					);
				}

				// subsequence:
				// x = expr
				//     |> Skip before
				//     |> Take (expr.Length - before - after)
				//     |> ToArray ()
				var subseqVar = ctx.Scope.DeclareImplicit(ctx, ElementType.MakeArrayType(), false);
				block.AddRange(
					Expr.Set(
						subseqVar,
						Expr.Invoke(
							Expr.Invoke(
								Expr.Invoke(
									expression,
									"Skip",
									Expr.Int(subseqIdx)
								),
								"Take",
								Expr.Sub(
									Expr.GetMember(expression, SizeMemberName),
									Expr.Int(ElementRules.Count - 1)
								)
							),
							"ToArray"
						)
					),
					ElementRules[subseqIdx].Expand(ctx, Expr.Get(subseqVar), nextStatement)
				);

				// post-subsequence
				for (var idx = subseqIdx+1; idx < ElementRules.Count; idx++)
				{
					block.AddRange(
						Expr.Set(
							tempVar,
							Expr.GetIdx(
								expression,
								Expr.Sub(
									Expr.GetMember(expression, SizeMemberName),
									Expr.Int(ElementRules.Count - idx)
								)
							)
						),
						ElementRules[idx].Expand(ctx, Expr.Get(tempVar), nextStatement)
					);
				}
			}
			else
			{
				var itemsCount = ElementRules.Count - 1;
				expandItemChecksIterated(ctx, block, expression, itemsCount, nextStatement);

				// tmpVar = seq.Skip N
				var subseqVar = ctx.Scope.DeclareImplicit(ctx, typeof(IEnumerable<>).MakeGenericType(ElementType), false);
				block.AddRange(
					Expr.Set(
						subseqVar,
						Expr.Invoke(
							expression,
							"Skip",
							Expr.Int(itemsCount)
						)
					),
					ElementRules[ElementRules.Count - 1].Expand(ctx, Expr.Get(subseqVar), nextStatement)
				);
			}

			return block;
		}

		/// <summary>
		/// Checks all items in the array with corresponding rules.
		/// </summary>
		private void expandItemChecksIterated(Context ctx, CodeBlockNode block, NodeBase expression, int count, Label nextStatement)
		{
			var enumerableType = typeof(IEnumerable<>).MakeGenericType(ElementType);
			var enumeratorType = typeof(IEnumerator<>).MakeGenericType(ElementType);

			var enumeratorVar = ctx.Scope.DeclareImplicit(ctx, enumeratorType, false);
			var currentVar = ctx.Scope.DeclareImplicit(ctx, ElementType, false);

			block.Add(
				Expr.Set(
					enumeratorVar,
					Expr.Invoke(
						Expr.Cast(expression, enumerableType),
						"GetEnumerator"
					)
				)
			);

			for (var idx = 0; idx < count; idx++)
			{
				block.AddRange(
					// if not iter.MoveNext() then jump!
					MakeJumpIf(
						nextStatement,
						Expr.Not(
							Expr.Invoke(
								Expr.Get(enumeratorVar),
								"MoveNext"
							)
						)
					),

					// let currentVar = iter.Current
					Expr.Set(
						currentVar,
						Expr.GetMember(
							Expr.Get(enumeratorVar),
							"Current"
						)
					),

					ElementRules[idx].Expand(ctx, Expr.Get(currentVar), nextStatement)
				);
			}
		}

		#endregion
	}
}
