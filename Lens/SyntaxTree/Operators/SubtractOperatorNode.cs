using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that subtracts a value from another value.
	/// </summary>
	internal class SubtractOperatorNode : BinaryOperatorNodeBase
	{
		protected override string OperatorRepresentation
		{
			get { return "-"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_Subtraction"; }
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType == typeof(string) && rightType == typeof(string) ? typeof(string) : null;
		}

		public override NodeBase Expand(Context ctx, bool mustReturn)
		{
			if (!IsConstant)
			{
				if (Resolve(ctx) == typeof (string))
					return Expr.Invoke(LeftOperand, "Replace", RightOperand, Expr.Str(""));
			}

			return null;
		}

		protected override void compileOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx);
			ctx.CurrentILGenerator.EmitSubtract();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			if (left is string && right is string)
				return left.Replace(right, "");

			return left - right;
		}
	}
}
