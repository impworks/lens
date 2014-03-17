using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new List declaration.
	/// </summary>
	internal class NewListNode : ValueListNodeBase<NodeBase>
	{
		private Type m_ItemType;

		protected override Type resolve(Context ctx, bool mustReturn = true)
		{
			if(Expressions.Count == 0)
				error(CompilerMessages.ListEmpty);

			m_ItemType = resolveItemType(Expressions, ctx);

			return typeof(List<>).MakeGenericType(m_ItemType);
		}

		public override IEnumerable<NodeChild> GetChildren()
		{
			return Expressions.Select((expr, i) => new NodeChild(expr, x => Expressions[i] = x));
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var tmpVar = ctx.CurrentScopeFrame.DeclareImplicitName(ctx, Resolve(ctx), true);
			
			var listType = Resolve(ctx);
			var ctor = ctx.ResolveConstructor(listType, new[] {typeof (int)});
			var addMethod = ctx.ResolveMethod(listType, "Add", new[] { m_ItemType });

			var count = Expressions.Count;
			gen.EmitConstant(count);
			gen.EmitCreateObject(ctor.ConstructorInfo);
			gen.EmitSaveLocal(tmpVar);

			foreach (var curr in Expressions)
			{
				var currType = curr.Resolve(ctx);

				ctx.CheckTypedExpression(curr, currType, true);

				if (!m_ItemType.IsExtendablyAssignableFrom(currType))
					error(curr, CompilerMessages.ListElementTypeMismatch, currType, m_ItemType);

				gen.EmitLoadLocal(tmpVar);
				
				Expr.Cast(curr, addMethod.ArgumentTypes[0]).Emit(ctx, true);
				gen.EmitCall(addMethod.MethodInfo);
			}

			gen.EmitLoadLocal(tmpVar);
		}

		public override string ToString()
		{
			return string.Format("list({0})", string.Join(";", Expressions));
		}
	}
}
