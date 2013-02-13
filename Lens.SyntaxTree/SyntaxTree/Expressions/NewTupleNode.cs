using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new tuple declaration.
	/// </summary>
	public class NewTupleNode : ValueListNodeBase<NodeBase>
	{
		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if (Expressions.Count == 0)
				Error("Tuple must contain at least one object!");

			if (Expressions.Count > 8)
				Error("Tuples cannot contain more than 8 objects. Use a structure or a nested tuple instead!");

			return FunctionalHelper.CreateTupleType(Expressions.Select(x => x.GetExpressionType(ctx)).ToArray());
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			return Expressions;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var types = Expressions.Select(x => x.GetExpressionType(ctx)).ToArray();

			foreach(var curr in Expressions)
				curr.Compile(ctx, true);

			var ctor = GetExpressionType(ctx).GetConstructor(types);
			gen.EmitCreateObject(ctor);
		}

		public override string ToString()
		{
			return string.Format("tuple({0})", string.Join(";", Expressions));
		}
	}
}
