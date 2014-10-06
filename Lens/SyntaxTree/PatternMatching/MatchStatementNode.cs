using System;
using System.Collections.Generic;
using System.Linq;

using Lens.Compiler;
using Lens.SyntaxTree.PatternMatching.Rules;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching
{
	using System.Reflection.Emit;

	using Lens.SyntaxTree.ControlFlow;


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

		/// <summary>
		/// List of names defined in the rule.
		/// </summary>
		private PatternNameBinding[] _BindingSet;

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var exprType = ParentNode.Expression.Resolve(ctx);

			// name group validation
			var bindingSets = MatchRules.Select(x => x.Resolve(ctx, exprType).ToArray()).ToArray();
			for (var idx = 0; idx < bindingSets.Length; idx++)
			{
				var duplicateName = bindingSets[idx].GroupBy(x => x.Name).FirstOrDefault(x => x.Count() > 1);
				if (duplicateName != null)
					error(CompilerMessages.PatternNameDuplicated, duplicateName.Key);

				if (idx > 0)
				{
					// do not compare binding set #0 with itself
					validatePatternBindingSets(bindingSets[0], bindingSets[idx]);
					validatePatternBindingSets(bindingSets[idx], bindingSets[0]);
				}
			}

			_BindingSet = bindingSets[0];

			if (_BindingSet.Length == 0)
				return Expression.Resolve(ctx, mustReturn);

			var locals = _BindingSet.Select(x => new Local(x.Name, x.Type)).ToArray();
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

		/// <summary>
		/// Expands the current rule into a block of checks.
		/// </summary>
		public CodeBlockNode ExpandRules(Context ctx, NodeBase expression, Label expressionLabel)
		{
			var block = new CodeBlockNode();
			
			// declare variables
			foreach (var binding in _BindingSet)
				block.Add(Expr.Var(binding.Name, binding.Type.FullName));

			foreach (var rule in MatchRules)
			{
				// current and next labels for each rule
				var ruleLabels = ParentNode.GetRuleLabels(rule);

				block.Add(Expr.JumpLabel(ruleLabels.CurrentRule));
				block.AddRange(rule.Expand(ctx, expression, ruleLabels.NextRule));

				if (Condition != null)
				{
					block.Add(
						Expr.If(
							Expr.Not(Condition),
							Expr.Block(Expr.JumpTo(ruleLabels.NextRule))
						)
					);
				}

				block.Add(Expr.JumpTo(expressionLabel));
			}

			block.AddRange(
				Expr.JumpLabel(expressionLabel),
				Expression,
				Expr.JumpTo(ParentNode.EndLabel)
			);

			return block;
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
