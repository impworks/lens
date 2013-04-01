using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Translations;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new array declaration.
	/// </summary>
	public class NewArrayNode : ValueListNodeBase<NodeBase>
	{
		private Type m_ItemType;

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if(Expressions.Count == 0)
				Error(CompilerMessages.ArrayEmpty);

			m_ItemType = resolveItemType(Expressions, ctx);
			if (m_ItemType == null)
				Error(Expressions[0], CompilerMessages.ArrayTypeUnknown);

			return Expressions[0].GetExpressionType(ctx).MakeArrayType();
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			return Expressions;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var tmpVar = ctx.CurrentScope.DeclareImplicitName(ctx, GetExpressionType(ctx), true);

			// create array
			var count = Expressions.Count;
			gen.EmitConstant(count);
			gen.EmitCreateArray(m_ItemType);
			gen.EmitSaveLocal(tmpVar);

			for (var idx = 0; idx < count; idx++)
			{
				var currType = Expressions[idx].GetExpressionType(ctx);

				ctx.CheckTypedExpression(Expressions[idx], currType, true);

				if (!m_ItemType.IsExtendablyAssignableFrom(currType))
					Error(Expressions[idx], CompilerMessages.ArrayElementTypeMismatch, currType, m_ItemType);

				gen.EmitLoadLocal(tmpVar);
				gen.EmitConstant(idx);

				var cast = Expr.Cast(Expressions[idx], m_ItemType);

				if (m_ItemType.IsValueType)
				{
					gen.EmitLoadIndex(m_ItemType, true);
					cast.Compile(ctx, true);
					gen.EmitSaveObject(m_ItemType);
				}
				else
				{
					cast.Compile(ctx, true);
					gen.EmitSaveIndex(m_ItemType);
				}
			}

			gen.EmitLoadLocal(tmpVar);
		}

		public override string ToString()
		{
			return string.Format("array({0})", string.Join(";", Expressions));
		}
	}
}
