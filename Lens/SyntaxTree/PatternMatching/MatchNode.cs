using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.PatternMatching.Rules;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching
{
	using System.Security.Permissions;


	/// <summary>
	/// The pattern matching base expression.
	/// </summary>
	internal class MatchNode : NodeBase
	{
		#region Constructor

		public MatchNode()
		{
			MatchStatements = new List<MatchStatementNode>();
			_StatementLabels = new List<Tuple<MatchRuleBase, Label>>();
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

		/// <summary>
		/// The label after all blocks to jump to when a successful match has been found.
		/// </summary>
		public Label EndLabel { get; private set; }

		/// <summary>
		/// Statement labels.
		/// </summary>
		private List<Tuple<MatchRuleBase, Label>> _StatementLabels;

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

		protected override NodeBase expand(Context ctx, bool mustReturn)
		{
			defineLabels(ctx);

			var exprType = Expression.Resolve(ctx);
			var tmpVar = ctx.Scope.DeclareImplicit(ctx, exprType, false);

			var block = Expr.Block(ScopeKind.MatchRoot, Expr.Set(tmpVar, Expression));
			foreach(var stmt in MatchStatements)
				block.Add(stmt.ExpandRules(ctx, Expr.Get(tmpVar)));

			var selfType = Resolve(ctx, mustReturn);
			if (!selfType.IsVoid())
			{
				block.Add(
					Expr.Block(
						Expr.Default(selfType),
						Expr.JumpTo(EndLabel)
					)
				);
			}

			block.Add(Expr.JumpLabel(EndLabel));

			return block;
		}

		#endregion

		#region Label handling

		private void defineLabels(Context ctx)
		{
			foreach (var stmt in MatchStatements)
				foreach (var rule in stmt.MatchRules)
					_StatementLabels.Add(Tuple.Create(rule, ctx.CurrentMethod.Generator.DefineLabel()));

			EndLabel = ctx.CurrentMethod.Generator.DefineLabel();
		}

		/// <summary>
		/// Returns the current and the next labels for a match rule.
		/// </summary>
		public Tuple<Label, Label> GetLabelsFor(MatchRuleBase rule)
		{
			var currIndex = _StatementLabels.FindIndex(x => x.Item1 == rule);
			if(currIndex == -1)
				throw new ArgumentException("Rule does not belong to current match block!");

			var next = currIndex < _StatementLabels.Count - 1
				? _StatementLabels[currIndex + 1].Item2
				: EndLabel;

			return Tuple.Create(_StatementLabels[currIndex].Item2, next);
		}

		#endregion
	}
}
