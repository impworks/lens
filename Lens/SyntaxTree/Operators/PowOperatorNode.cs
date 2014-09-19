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
		#region Constants

		private static readonly MethodInfo _PowMethod = typeof(Math).GetMethod("Pow", new[] { typeof(double), typeof(double) });

		#endregion

		#region Operator basics

		protected override string OperatorRepresentation
		{
			get { return "**"; }
		}

		#endregion

		#region Resolve

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType.IsNumericType() && rightType.IsNumericType() ? typeof (double) : null;
		}

		#endregion

		#region Emit

		protected override void emitOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx, typeof(double));
			ctx.CurrentMethod.Generator.EmitCall(_PowMethod);
		}

		#endregion

		#region Constant unroll

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return Math.Pow(left, right);
		}

		#endregion
	}
}
