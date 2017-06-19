using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
	/// <summary>
	/// Checks if the expression is of a specified type and applies a pattern to its label.
	/// </summary>
	internal class MatchTypeRule : MatchRuleBase
	{
		#region Fields

		/// <summary>
		/// Type signature.
		/// </summary>
		public TypeSignature Identifier;

		/// <summary>
		/// Nested rule of the label.
		/// </summary>
		public MatchRuleBase LabelRule;

		/// <summary>
		/// The actual type.
		/// </summary>
		private Type _type;

		#endregion

		#region Resolve

		public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
		{
			var typeEntity = ctx.FindType(Identifier.FullSignature);
			if(typeEntity == null || (!typeEntity.Kind.IsAnyOf(TypeEntityKind.Type, TypeEntityKind.TypeLabel)))
				Error(Identifier, CompilerMessages.PatternNotValidType, Identifier.FullSignature);

			_type = ctx.ResolveType(Identifier);
			if (!_type.IsExtendablyAssignableFrom(expressionType) && !expressionType.IsExtendablyAssignableFrom(_type))
				Error(CompilerMessages.PatternTypeMatchImpossible, _type, expressionType);

			try
			{
				var field = typeEntity.ResolveField("Tag");
				return LabelRule.Resolve(ctx, field.Type);
			}
			catch(KeyNotFoundException)
			{
				Error(CompilerMessages.PatternTypeNoTag, Identifier.FullSignature);
				return NoBindings();
			}
		}

		#endregion

		#region Expand

		public override IEnumerable<NodeBase> Expand(Context ctx, NodeBase expression, Label nextStatement)
		{
			// no need for temporary variable: field access is idempotent
			yield return MakeJumpIf(
				nextStatement,
				Expr.Not(
					Expr.Is(expression, _type)
				)
			);

			var rules = LabelRule.Expand(
				ctx,
				Expr.GetMember(
					Expr.Cast(expression, _type),
					"Tag"
				),
				nextStatement
			);

			foreach (var rule in rules)
				yield return rule;
		}

		#endregion
	}
}
