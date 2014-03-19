using System;
using System.Reflection;
using Lens.Compiler;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the typeof operator.
	/// </summary>
	internal class TypeofOperatorNode : TypeOperatorNodeBase
	{
		private static readonly MethodInfo _HandleMethod = typeof(Type).GetMethod("GetTypeFromHandle", new[] { typeof(RuntimeTypeHandle) });
		public TypeofOperatorNode(string type = null)
		{
			TypeSignature = type;
		}

		protected override Type resolve(Context ctx, bool mustReturn = true)
		{
			return typeof (Type);
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var type = Type ?? ctx.ResolveType(TypeSignature);
			var gen = ctx.CurrentILGenerator;

			gen.EmitConstant(type);
			gen.EmitCall(_HandleMethod);
		}

		public override string ToString()
		{
			return string.Format("typeof({0})", Type != null ? Type.Name : TypeSignature);
		}
	}
}
