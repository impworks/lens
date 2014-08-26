using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new array declaration.
	/// </summary>
	internal class NewArrayNode : ValueListNodeBase<NodeBase>
	{
		private Type _ItemType;

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			if(Expressions.Count == 0)
				error(CompilerMessages.ArrayEmpty);

			_ItemType = resolveItemType(Expressions, ctx);
			return _ItemType.MakeArrayType();
		}

		protected override IEnumerable<NodeChild> getChildren()
		{
			return Expressions.Select((expr, i) => new NodeChild(expr, x => Expressions[i] = x));
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;
			var tmpVar = ctx.Scope.DeclareImplicit(ctx, Resolve(ctx), true);

			// create array
			var count = Expressions.Count;
			gen.EmitConstant(count);
			gen.EmitCreateArray(_ItemType);
			gen.EmitSaveLocal(tmpVar.LocalBuilder);

			for (var idx = 0; idx < count; idx++)
			{
				var currType = Expressions[idx].Resolve(ctx);

				ctx.CheckTypedExpression(Expressions[idx], currType, true);

				if (!_ItemType.IsExtendablyAssignableFrom(currType))
					error(Expressions[idx], CompilerMessages.ArrayElementTypeMismatch, currType, _ItemType);

				gen.EmitLoadLocal(tmpVar.LocalBuilder);
				gen.EmitConstant(idx);

				var cast = Expr.Cast(Expressions[idx], _ItemType);

				if (_ItemType.IsValueType)
				{
					gen.EmitLoadIndex(_ItemType, true);
					cast.Emit(ctx, true);
					gen.EmitSaveObject(_ItemType);
				}
				else
				{
					cast.Emit(ctx, true);
					gen.EmitSaveIndex(_ItemType);
				}
			}

			gen.EmitLoadLocal(tmpVar.LocalBuilder);
		}

		public override string ToString()
		{
			return string.Format("array({0})", string.Join(";", Expressions));
		}
	}
}
