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
		#region Constants

		private static readonly MethodInfo _HandleMethod = typeof(Type).GetMethod("GetTypeFromHandle", new[] { typeof(RuntimeTypeHandle) });

		#endregion

		#region Constructor

		public TypeofOperatorNode(string type = null)
		{
			TypeSignature = type;
		}

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn = true)
		{
			return typeof (Type);
		}

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var type = Type ?? ctx.ResolveType(TypeSignature);
			var gen = ctx.CurrentMethod.Generator;

			gen.EmitConstant(type);
			gen.EmitCall(_HandleMethod);
		}

		#endregion

		#region Debug

		public override string ToString()
		{
			return string.Format("typeof({0})", Type != null ? Type.Name : TypeSignature);
		}

		#endregion
	}
}
