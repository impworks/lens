using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new array declaration.
	/// </summary>
	internal class NewArrayNode : ValueListNodeBase<NodeBase>
	{
		private Type m_ItemType;

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if(Expressions.Count == 0)
				error(CompilerMessages.ArrayEmpty);

			m_ItemType = resolveItemType(Expressions, ctx);
			return m_ItemType.MakeArrayType();
		}

		public override IEnumerable<NodeChild> GetChildren()
		{
			return Expressions.Select((expr, i) => new NodeChild(expr, x => Expressions[i] = x));
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var tmpVar = ctx.CurrentScopeFrame.DeclareImplicitName(ctx, Resolve(ctx), true);

			// create array
			var count = Expressions.Count;
			gen.EmitConstant(count);
			gen.EmitCreateArray(m_ItemType);
			gen.EmitSaveLocal(tmpVar);

			for (var idx = 0; idx < count; idx++)
			{
				var currType = Expressions[idx].Resolve(ctx);

				ctx.CheckTypedExpression(Expressions[idx], currType, true);

				if (!m_ItemType.IsExtendablyAssignableFrom(currType))
					error(Expressions[idx], CompilerMessages.ArrayElementTypeMismatch, currType, m_ItemType);

				gen.EmitLoadLocal(tmpVar);
				gen.EmitConstant(idx);

				var cast = Expr.Cast(Expressions[idx], m_ItemType);

				if (m_ItemType.IsValueType)
				{
					gen.EmitLoadIndex(m_ItemType, true);
					cast.Emit(ctx, true);
					gen.EmitSaveObject(m_ItemType);
				}
				else
				{
					cast.Emit(ctx, true);
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
