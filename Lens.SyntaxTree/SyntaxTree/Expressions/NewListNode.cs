using System;
using System.Collections.Generic;
using System.Reflection;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Operators;
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

			return typeof(List<>).MakeGenericType(Expressions[0].GetExpressionType(ctx));
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
			var varId = tmpVar.LocalId.Value;

			var listType = GetExpressionType(ctx);
			var ctor = listType.GetConstructor(new[] {typeof (int)});
			var addMethod = listType.GetMethod("Add", new[] {itemType});

			var count = Expressions.Count;
			gen.EmitConstant(count);
			gen.EmitCreateObject(ctor);
			gen.EmitSaveLocal(varId);

			foreach (var curr in Expressions)
			{
				var currType = curr.GetExpressionType(ctx);
				if (listType.IsExtendablyAssignableFrom(currType))
					Error("Cannot add an object of type '{0}' to List<{1}>!", currType, itemType);

				gen.EmitLoadLocal(varId);
				var cast = new CastOperatorNode
				{
					Expression = curr,
					Type = currType
				};
				cast.Compile(ctx, true);
				gen.EmitCall(addMethod);
			}

			gen.EmitLoadLocal(varId);
		}

		public override string ToString()
		{
			return string.Format("list({0})", string.Join(";", Expressions));
		}
	}
}
