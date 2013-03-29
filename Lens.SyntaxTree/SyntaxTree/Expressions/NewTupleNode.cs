using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new tuple declaration.
	/// </summary>
	public class NewTupleNode : ValueListNodeBase<NodeBase>
	{
		private Type[] m_Types;

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if (Expressions.Count == 0)
				Error("Tuple must contain at least one object!");

			if (Expressions.Count > 8)
				Error("Tuples cannot contain more than 8 objects. Use a structure or a nested tuple instead!");

			var types = new List<Type>();
			foreach (var curr in Expressions)
			{
				var type = curr.GetExpressionType(ctx);
				ctx.CheckTypedExpression(curr, type);

				types.Add(type);
			}

			m_Types = types.ToArray();
			return FunctionalHelper.CreateTupleType(m_Types);
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			return Expressions;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			GetExpressionType(ctx);

			var gen = ctx.CurrentILGenerator;

			foreach(var curr in Expressions)
				curr.Compile(ctx, true);

			var ctor = ctx.ResolveConstructor(GetExpressionType(ctx), m_Types);
			gen.EmitCreateObject(ctor);
		}

		public override string ToString()
		{
			return string.Format("tuple({0})", string.Join(";", Expressions));
		}
	}
}
