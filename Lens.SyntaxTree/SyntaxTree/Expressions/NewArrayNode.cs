using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new array declaration.
	/// </summary>
	public class NewArrayNode : ValueListNodeBase<NodeBase>
	{
		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if(Expressions.Count == 0)
				Error("Array must contain at least one object!");

			var itemType = Expressions[0].GetExpressionType(ctx);
			if (itemType == typeof(NullType))
				Error(Expressions[0], "Cannot infer type of the first item of the array. Please use casting to specify array type!");

			if (itemType.IsVoid())
				Error(Expressions[0], "An expression that returns a value is expected!");

			return Expressions[0].GetExpressionType(ctx).MakeArrayType();
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			return Expressions;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var itemType = Expressions[0].GetExpressionType(ctx);
			var tmpVar = ctx.CurrentScope.DeclareImplicitName(ctx, GetExpressionType(ctx), true);

			// create array
			var count = Expressions.Count;
			gen.EmitConstant(count);
			gen.EmitCreateArray(itemType);
			gen.EmitSaveLocal(tmpVar);

			for (var idx = 0; idx < count; idx++)
			{
				var currType = Expressions[idx].GetExpressionType(ctx);

				if (currType.IsVoid())
					Error(Expressions[idx], "An expression that returns a value is expected!");

				if(!itemType.IsExtendablyAssignableFrom(currType))
					Error(Expressions[idx], "Cannot add object of type '{0}' to array of type '{1}'!", currType, itemType);

				gen.EmitLoadLocal(tmpVar);
				gen.EmitConstant(idx);

				var cast = Expr.Cast(Expressions[idx], itemType);

				if (itemType.IsValueType)
				{
					gen.EmitLoadIndex(itemType, true);
					cast.Compile(ctx, true);
					gen.EmitSaveObject(itemType);
				}
				else
				{
					cast.Compile(ctx, true);
					gen.EmitSaveIndex(itemType);
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
