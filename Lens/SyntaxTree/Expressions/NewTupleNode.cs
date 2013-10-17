using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
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
				Error(CompilerMessages.TupleNoArgs);

			if (Expressions.Count > 8)
				Error(CompilerMessages.TupleTooManyArgs);

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

		protected override void compile(Context ctx, bool mustReturn)
		{
			var tupleType = GetExpressionType(ctx);

			var gen = ctx.CurrentILGenerator;

			foreach(var curr in Expressions)
				curr.Compile(ctx, true);

			var ctor = ctx.ResolveConstructor(tupleType, m_Types);
			gen.EmitCreateObject(ctor.ConstructorInfo);
		}

		public override string ToString()
		{
			return string.Format("tuple({0})", string.Join(";", Expressions));
		}
	}
}
