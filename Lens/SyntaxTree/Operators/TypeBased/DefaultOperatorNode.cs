using System;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;

namespace Lens.SyntaxTree.Operators.TypeBased
{
	/// <summary>
	/// A node representing the operator that returns a default value for the type.
	/// </summary>
	internal class DefaultOperatorNode : TypeOperatorNodeBase
	{
		#region Constructor

		public DefaultOperatorNode(TypeSignature type = null)
		{
			TypeSignature = type;
		}

		#endregion

		#region Fields

		/// <summary>
		/// Types that are equal to i4.0 in bytecode (according to C# compiler)
		/// </summary>
		private static readonly Type[] I4Types =
		{
			typeof (bool),
			typeof (byte),
			typeof (sbyte),
			typeof (short),
			typeof (ushort),
			typeof (int),
			typeof (uint)
		};

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn = true)
		{
			return Type ?? ctx.ResolveType(TypeSignature);
		}

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;
			var type = Resolve(ctx);

			if(type.IsVoid())
				error(CompilerMessages.VoidTypeDefault);

			if (I4Types.Contains(type))
				gen.EmitConstant(0);

			else if(type == typeof(long) || type == typeof(ulong))
				gen.EmitConstant(0L);

			else if(type == typeof(float))
				gen.EmitConstant(0.0f);

			else if(type == typeof(double))
				gen.EmitConstant(0.0);

			else if (type == typeof (decimal))
			{
				gen.EmitConstant(0);
				gen.EmitCreateObject(typeof(decimal).GetConstructor(new [] { typeof(int) }));
			}

			else if (!type.IsValueType)
				gen.EmitNull();

			else
			{
				var tmpVar = ctx.Scope.DeclareImplicit(ctx, Resolve(ctx), true);

				gen.EmitLoadLocal(tmpVar.LocalBuilder, true);
				gen.EmitInitObject(type);
				gen.EmitLoadLocal(tmpVar.LocalBuilder);
			}
		}

		#endregion

		#region Debug

		public override string ToString()
		{
			return string.Format("default({0})", Type != null ? Type.Name : TypeSignature);
		}

		#endregion
	}
}
