using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the typeof operator.
	/// </summary>
	public class TypeofOperatorNode : TypeOperatorNodeBase
	{
		public TypeofOperatorNode(string type = null)
		{
			Type = type;
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return typeof (Type);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var method = typeof(Type).GetMethod("GetTypeFromHandle", new[] { typeof(RuntimeTypeHandle) });

			gen.EmitConstant(ctx.ResolveType(Type));
			gen.EmitCall(method);
		}

		public override string ToString()
		{
			return string.Format("typeof({0})", Type);
		}
	}
}
