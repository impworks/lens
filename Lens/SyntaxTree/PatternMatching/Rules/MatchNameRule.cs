using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
	using Lens.Compiler;
	using Lens.SyntaxTree.ControlFlow;


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
		/// Expected type of the expression.
		/// </summary>
		public TypeSignature Type;

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

		public override NodeBase Expand(Context ctx, NodeBase expression, Label nextStatement)
		{
			var block = new CodeBlockNode();

			if (Type != null)
			{
				block.Add(
					Expr.If(
						Expr.Negate(Expr.Is(expression, Type)),
						Expr.Block(
							Expr.JumpTo(nextStatement)
						)
					)
				);
			}

			if (!IsWildcard)
			{
				block.Add(Expr.Let(Name, expression));
			}

			return block;
		}

		#endregion
	}
}
