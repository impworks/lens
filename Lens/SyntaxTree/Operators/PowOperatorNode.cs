using System;
using Lens.Compiler;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that raises one value to the power of another value.
	/// </summary>
	internal class PowOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "**"; }
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType.IsNumericType() && rightType.IsNumericType() ? typeof (double) : null;
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			loadAndConvertNumerics(ctx, typeof(double));

			var method = typeof(Math).GetMethod("Pow", new[] { typeof(double), typeof(double) });
			gen.EmitCall(method);
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return Math.Pow(left, right);
		}
	}
}
