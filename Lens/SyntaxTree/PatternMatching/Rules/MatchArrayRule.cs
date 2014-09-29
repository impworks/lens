using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using Lens.Resolver;
using Lens.Translations;
using Lens.Compiler;
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
			Items = new List<MatchRuleBase>();
		}

		#endregion

		#region Fields

		/// <summary>
		/// The patterns of array items.
		/// </summary>
		public List<MatchRuleBase> Items;

		/// <summary>
		/// The array element's type.
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

		#endregion

		#region Resolve

		public override IEnumerable<PatternNameBinding> Resolve(Type expressionType)
		{
			if (expressionType.IsArray)
				ElementType = expressionType.GetElementType();

			else if(new [] { typeof(IEnumerable<>), typeof(IList<>) }.Any(expressionType.IsAppliedVersionOf))
				ElementType = expressionType.GetGenericArguments()[0];

			else
				Error(CompilerMessages.PatternTypeMismatch, expressionType, "IEnumerable<T>");

			IsIndexable = !expressionType.IsAppliedVersionOf(typeof (IEnumerable<>));

			for (var idx = 0; idx < Items.Count; idx++)
			{
				var subseq = Items[idx] as MatchNameRule;
				if (subseq != null && subseq.IsArraySubsequence)
				{
					if(SubsequenceIndex != null)
						Error(CompilerMessages.PatternArraySubsequences);

					SubsequenceIndex = idx;
				}

				var itemType = SubsequenceIndex != idx
					? ElementType
					: (IsIndexable ? ElementType.MakeArrayType() : typeof (IEnumerable<>).MakeGenericType(ElementType));

				var bindings = Items[idx].Resolve(itemType);
				foreach (var binding in bindings)
					yield return binding;
			}
		}

		#endregion

		#region Expand

		public override NodeBase Expand(Context ctx, NodeBase expression, Label nextStatement)
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
