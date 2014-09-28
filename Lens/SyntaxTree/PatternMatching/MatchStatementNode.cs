using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.SyntaxTree.PatternMatching.Rules;
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
			// todo: validate Condition, Match Rules & stuff

			return Expression.Resolve(ctx, mustReturn);
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

		#endregion
	}
}
