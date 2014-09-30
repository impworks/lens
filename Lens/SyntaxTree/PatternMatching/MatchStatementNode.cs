using System;
using System.Collections.Generic;
using System.Linq;

using Lens.Compiler;
using Lens.SyntaxTree.PatternMatching.Rules;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching
{
	/// <summary>
	/// A single statement with many optional rules and a result.
	/// </summary>
	internal class MatchStatementNode : NodeBase
	{
		#region Constructor

		public MatchStatementNode()
		{
			MatchRules = new List<MatchRuleBase>();
		}

		#endregion

		#region Fields

		/// <summary>
		/// The pointer to parent MatchNode.
		/// </summary>
		public MatchNode ParentNode;

		/// <summary>
		/// Result expression to return if the statement matches.
		/// </summary>
		public NodeBase Expression;

		/// <summary>
		/// The "when" expression that must evaluate to true for the statement to match.
		/// </summary>
		public NodeBase Condition;

		/// <summary>
		/// List of rules (separated by '|') that yield the same expression.
		/// </summary>
		public List<MatchRuleBase> MatchRules;

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var exprType = ParentNode.Expression.Resolve(ctx);

			// name group validation
			var bindingSets = MatchRules.Select(x => x.Resolve(ctx, exprType).ToArray()).ToArray();
			for (var idx = 1; idx < bindingSets.Length; idx++)
			{
				validatePatternBindingSets(bindingSets[0], bindingSets[idx]);
				validatePatternBindingSets(bindingSets[idx], bindingSets[0]);
			}

			if(bindingSets[0].Length == 0)
				return Expression.Resolve(ctx, mustReturn);

			var locals = bindingSets[0].Select(x => new Local(x.Name, x.Type)).ToArray();
			return Scope.WithTempLocals(ctx, () => Expression.Resolve(ctx, mustReturn), locals);
		}

		#endregion

		#region Transform

		protected override IEnumerable<NodeChild> getChildren()
		{
			yield return new NodeChild(Expression, x => Expression = x);
			if(Condition != null)
				yield return new NodeChild(Condition, x => Condition = x);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Makes sure that pattern binding set 'B' contains all items from set 'A' and throws an error otherwise.
		/// </summary>
		private void validatePatternBindingSets(PatternNameBinding[] a, PatternNameBinding[] b)
		{
			// find at least one variable that does not strictly match
			var extra = a.Except(b).FirstOrDefault();
			if (extra == null)
				return;

			// find a variable with the same name to check whether the error is in the name or in the type
			var nameSake = b.FirstOrDefault(x => x.Name == extra.Name);
			if(nameSake == null)
				error(CompilerMessages.PatternNameSetMismatch, extra.Name);
			else
				error(CompilerMessages.PatternNameTypeMismatch, extra.Name, extra.Type, nameSake.Type);
		}

		#endregion
	}
}
