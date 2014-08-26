using System;
using System.Reflection;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that raises one value to the power of another value.
	/// </summary>
	internal class PowOperatorNode : BinaryOperatorNodeBase
	{
		private static readonly MethodInfo _PowMethod = typeof(Math).GetMethod("Pow", new[] { typeof(double), typeof(double) });

		protected override string OperatorRepresentation
		{
			get { return "**"; }
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType.IsNumericType() && rightType.IsNumericType() ? typeof (double) : null;
		}

		protected override void emitOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx, typeof(double));
			ctx.CurrentMethod.Generator.EmitCall(_PowMethod);
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return Math.Pow(left, right);
		}
	}
}
