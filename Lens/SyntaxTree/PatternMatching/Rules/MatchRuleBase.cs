using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
	using Lens.Compiler;


	/// <summary>
	/// One particular rule to match againts an expression.
	/// </summary>
	internal abstract class MatchRuleBase : LocationEntity
	{
		/// <summary>
		/// Gets the list of variables bindings declared in the pattern.
		/// </summary>
		public abstract IEnumerable<PatternNameBinding> Resolve(Type expressionType);

		/// <summary>
		/// Returns the AST representation of this rule's checks.
		/// </summary>
		public abstract NodeBase Expand(Context ctx, NodeBase expression, Label nextStatement);

		#region Error reporting

		/// <summary>
		/// Reports an error bound to current matching rule.
		/// </summary>
		internal void Error(string message, params object[] args)
		{
			throw new LensCompilerException(
				string.Format(message, args),
				this
			);
		}

		#endregion
	}
}
