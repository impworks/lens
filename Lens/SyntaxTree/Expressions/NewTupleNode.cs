using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new tuple declaration.
	/// </summary>
	internal class NewTupleNode : ValueListNodeBase<NodeBase>
	{
		private Type[] _Types;

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			if (Expressions.Count == 0)
				error(CompilerMessages.TupleNoArgs);

			if (Expressions.Count > 8)
				error(CompilerMessages.TupleTooManyArgs);

			var types = new List<Type>();
			foreach (var curr in Expressions)
			{
				var type = curr.Resolve(ctx);
				ctx.CheckTypedExpression(curr, type);

				types.Add(type);
			}

			_Types = types.ToArray();
			return FunctionalHelper.CreateTupleType(_Types);
		}

		public override IEnumerable<NodeChild> GetChildren()
		{
			return Expressions.Select((expr, i) => new NodeChild(expr, x => Expressions[i] = x));
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var tupleType = Resolve(ctx);

			var gen = ctx.CurrentILGenerator;

			foreach(var curr in Expressions)
				curr.Emit(ctx, true);

			var ctor = ctx.ResolveConstructor(tupleType, _Types);
			gen.EmitCreateObject(ctor.ConstructorInfo);
		}

		public override string ToString()
		{
			return string.Format("tuple({0})", string.Join(";", Expressions));
		}
	}
}
