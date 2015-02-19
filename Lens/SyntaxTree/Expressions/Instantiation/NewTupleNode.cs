using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.Instantiation
{
	/// <summary>
	/// A node representing a new tuple declaration.
	/// </summary>
	internal class NewTupleNode : CollectionNodeBase<NodeBase>
	{
		#region Fields

		/// <summary>
		/// List of tuple item types.
		/// </summary>
		private Type[] _Types;

		#endregion

		#region Resolve

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

		#endregion

		#region Transform

		protected override IEnumerable<NodeChild> getChildren()
		{
			return Expressions.Select((expr, i) => new NodeChild(expr, x => Expressions[i] = x));
		}

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var tupleType = Resolve(ctx);

			var gen = ctx.CurrentMethod.Generator;

			foreach(var curr in Expressions)
				curr.Emit(ctx, true);

			var ctor = ctx.ResolveConstructor(tupleType, _Types);
			gen.EmitCreateObject(ctor.ConstructorInfo);
		}

		#endregion

		#region Debug

		public override string ToString()
		{
			return string.Format("tuple({0})", string.Join(";", Expressions));
		}

		#endregion
	}
}
