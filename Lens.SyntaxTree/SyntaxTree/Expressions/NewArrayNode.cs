using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;

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
				if(currType != itemType)
					Error("Cannot add object of type '{0}' to array of type '{1}': item types must match exactly.", currType, itemType);

				gen.EmitLoadLocal(tmpVar);
				gen.EmitConstant(idx);

				if (itemType.IsValueType)
				{
					gen.EmitLoadIndex(itemType, true);
					Expressions[idx].Compile(ctx, true);
					gen.EmitSaveObject(itemType);
				}
				else
				{
					Expressions[idx].Compile(ctx, true);
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
