using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the typeof operator.
	/// </summary>
	internal class TypeofOperatorNode : TypeOperatorNodeBase
	{
		public TypeofOperatorNode(string type = null)
		{
			TypeSignature = type;
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return typeof (Type);
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			var type = Type ?? ctx.ResolveType(TypeSignature);
			var gen = ctx.CurrentILGenerator;
			var method = typeof(Type).GetMethod("GetTypeFromHandle", new[] { typeof(RuntimeTypeHandle) });

			gen.EmitConstant(type);
			gen.EmitCall(method);
		}

		public override string ToString()
		{
			return string.Format("typeof({0})", Type != null ? Type.Name : TypeSignature);
		}
	}
}
