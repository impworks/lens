using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.InteropServices.ComTypes;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;
using Lens.Compiler;
using Lens.SyntaxTree.ControlFlow;

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
		/// Expected type of the expression.
		/// </summary>
		public TypeSignature Type;

		/// <summary>
		/// Checks if the current name is prefixed with a "..." modifier in an array pattern.
		/// </summary>
		public bool IsArraySubsequence;

		/// <summary>
		/// Checks if the name is used as a placeholder.
		/// </summary>
		private bool IsWildcard
		{
			get { return Name == "_"; }
		}

		#endregion

		#region Resolve

		public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
		{
			if (!IsWildcard)
				yield return new PatternNameBinding(Name, expressionType);

			if (Type != null)
			{
				var specifiedType = ctx.ResolveType(Type);
				if(!specifiedType.IsExtendablyAssignableFrom(expressionType) && !expressionType.IsExtendablyAssignableFrom(specifiedType))
					Error(CompilerMessages.CastTypesMismatch);
			}
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
