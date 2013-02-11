using System;
using System.Linq;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the operator that returns a default value for the type.
	/// </summary>
	public class DefaultOperatorNode : TypeOperatorNodeBase
	{
		/// <summary>
		/// Types that are equal to i4.0 in bytecode (according to C# compiler)
		/// </summary>
		private static readonly Type[] I4Types = new[]
		{
			typeof (bool),
			typeof (byte),
			typeof (sbyte),
			typeof (short),
			typeof (ushort),
			typeof (int),
			typeof (uint)
		};

		/// <summary>
		/// Temp variable used to instantiate a valuetype object.
		/// </summary>
		private LocalName _TempLocalVariable;

		public DefaultOperatorNode(string type = null)
		{
			Type = type;
		}

		protected override Type resolveExpressionType(Context ctx)
		{
			return ctx.ResolveType(Type.Signature);
		}

		public override void ProcessClosures(Context ctx)
		{
			// register a local variable for current node if it's a value type.
			var type = GetExpressionType(ctx);
			if (type.IsValueType)
				_TempLocalVariable = ctx.CurrentScope.DeclareImplicitName(GetExpressionType(ctx), false);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var type = GetExpressionType(ctx);

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
				var id = _TempLocalVariable.LocalId.Value;

				gen.EmitLoadLocalAddress(id);
				gen.EmitInitObject(type);

				gen.EmitLoadLocal(id);
			}
		}

		public override string ToString()
		{
			return string.Format("default({0})", Type);
		}
	}
}
