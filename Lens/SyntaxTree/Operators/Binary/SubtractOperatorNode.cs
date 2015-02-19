using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Operators.Binary
{
	/// <summary>
	/// An operator node that subtracts a value from another value.
	/// </summary>
	internal class SubtractOperatorNode : BinaryOperatorNodeBase
	{
		#region Operator basics

		protected override string OperatorRepresentation
		{
			get { return "-"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_Subtraction"; }
		}

		#endregion

		#region Resolve

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType == typeof(string) && rightType == typeof(string) ? typeof(string) : null;
		}

		#endregion

		#region Transform

		protected override NodeBase expand(Context ctx, bool mustReturn)
		{
			if (!IsConstant)
			{
				if (Resolve(ctx) == typeof (string))
					return Expr.Invoke(LeftOperand, "Replace", RightOperand, Expr.Str(""));
			}

			return base.expand(ctx, mustReturn);
		}

		#endregion

		#region Emit

		protected override void emitOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx);
			ctx.CurrentMethod.Generator.EmitSubtract();
		}

		#endregion

		#region Constant unroll

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			if (left is string && right is string)
				return left.Replace(right, "");

			return left - right;
		}

		#endregion
	}
}
