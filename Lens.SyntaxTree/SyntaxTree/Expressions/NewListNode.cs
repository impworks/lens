using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new List declaration.
	/// </summary>
	public class NewListNode : ValueListNodeBase<NodeBase>
	{
		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if(Expressions.Count == 0)
				Error("Use explicit constructor to create an empty list!");

			var itemType = Expressions[0].GetExpressionType(ctx);
			if(itemType == typeof(NullType))
				Error(Expressions[0], "Cannot infer type of the first item of the list. Please use casting to specify list type!");

			if(itemType.IsVoid())
				Error(Expressions[0], "An expression that returns a value is expected!");

			return typeof(List<>).MakeGenericType(itemType);
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
			
			var listType = GetExpressionType(ctx);
			var ctor = listType.GetConstructor(new[] {typeof (int)});
			var addMethod = listType.GetMethod("Add", new[] {itemType});

			var count = Expressions.Count;
			gen.EmitConstant(count);
			gen.EmitCreateObject(ctor);
			gen.EmitSaveLocal(tmpVar);

			foreach (var curr in Expressions)
			{
				var currType = curr.GetExpressionType(ctx);

				if (currType.IsVoid())
					Error(curr, "An expression that returns a value is expected!");

				if (!itemType.IsExtendablyAssignableFrom(currType))
					Error(curr, "Cannot add an object of type '{0}' to List<{1}>!", currType, itemType);

				gen.EmitLoadLocal(tmpVar);
				
				Expr.Cast(curr, currType).Compile(ctx, true);
				gen.EmitCall(addMethod);
			}

			gen.EmitLoadLocal(tmpVar);
		}

		public override string ToString()
		{
			return string.Format("list({0})", string.Join(";", Expressions));
		}
	}
}
