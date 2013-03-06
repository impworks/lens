using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Literals;
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

			m_Types = Expressions.Select(x => x.GetExpressionType(ctx)).ToArray();
			for (var idx = 0; idx < m_Types.Length; idx++)
			{
				var type = m_Types[idx];

				if (type == typeof(NullType))
					Error(Expressions[idx], "Cannot infer type of the tuple item. Please use casting to specify the type!");

				if (type.IsVoid())
					Error(Expressions[idx], "An expression that returns a value is expected!");
			}

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

			var ctor = GetExpressionType(ctx).GetConstructor(m_Types);
			gen.EmitCreateObject(ctor);
		}

		public override string ToString()
		{
			return string.Format("tuple({0})", string.Join(";", Expressions));
		}
	}
}
