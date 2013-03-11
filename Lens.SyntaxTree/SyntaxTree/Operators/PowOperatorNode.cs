using System;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that raises one value to the power of another value.
	/// </summary>
	public class PowOperatorNode : BinaryOperatorNodeBase
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
	}
}
