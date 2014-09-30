using System;
using System.Collections.Generic;
using System.Linq;

using Lens.Compiler;
using Lens.Resolver;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching
{
	/// <summary>
	/// The pattern matching base expression.
	/// </summary>
	internal class MatchNode : NodeBase
	{
		#region Constructor

		public MatchNode()
		{
			MatchStatements = new List<MatchStatementNode>();
		}

		#endregion

		#region Fields

		/// <summary>
		/// The expression to match against rules.
		/// </summary>
		public NodeBase Expression;

		/// <summary>
		/// Match statements to test the expression against.
		/// </summary>
		public List<MatchStatementNode> MatchStatements;

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var stmtTypes = new List<Type>(MatchStatements.Count);
			foreach (var stmt in MatchStatements)
			{
				stmt.ParentNode = this;
				stmtTypes.Add(stmt.Resolve(ctx, mustReturn));
			}

			return stmtTypes.Any(x => x.IsVoid())
				? typeof(UnitType) 
				: stmtTypes.ToArray().GetMostCommonType();
		}

		#endregion

		#region Transform

		protected override IEnumerable<NodeChild> getChildren()
		{
			yield return new NodeChild(Expression, x => Expression = x);
			foreach(var stmt in MatchStatements)
				yield return new NodeChild(stmt, null);
		}

		#endregion

		#region Helpers

		#endregion
	}
}
