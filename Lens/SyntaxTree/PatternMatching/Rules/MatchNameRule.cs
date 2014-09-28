using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
	/// <summary>
	/// Binds an expression to a name.
	/// </summary>
	internal class MatchNameRule : MatchRuleBase
	{
		#region Fields

		/// <summary>
		/// The desired name to bind to.
		/// </summary>
		public string Name;

		/// <summary>
		/// Checks if the name is used as a placeholder.
		/// </summary>
		private bool IsWildcard
		{
			get { return Name == "_"; }
		}

		#endregion

		#region Resolve

		public override IEnumerable<PatternNameBinding> Resolve(Type expressionType)
		{
			if (!IsWildcard)
				yield return new PatternNameBinding(Name, expressionType);
		}

		#endregion

		#region Expand

		public override NodeBase Expand(NodeBase expression, Label nextCheck)
		{
			return IsWildcard ? null : Expr.Let(Name, expression);
		}

		#endregion
	}
}
